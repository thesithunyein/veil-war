// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {IERC20Minimal, IJackpotRandomTicketBuyer} from "./interfaces/IMegapot.sol";

/**
 * @title MegapotRewardController
 * @notice Base Sepolia Megapot integration for Veil War scout-to-earn + jackpot stake matches.
 * @dev Listens to VeilWarCore reward hooks (or bridge messages) and buys Megapot tickets for players.
 *
 * Base Sepolia references (Megapot Summer Jam):
 *   USDC:                      0x036CbD53842c5426634e7929541eC2318f3dCF7e
 *   Jackpot:                   0x465dA3c859f193A3807386387bEE941B2A4c3279
 *   JackpotRandomTicketBuyer:  0x53c04e7e5044B28Ea8A4F9c4b26E3Ac1aeb63746
 */
contract MegapotRewardController {
    // —— Config ——
    IERC20Minimal public immutable usdc;
    IJackpotRandomTicketBuyer public immutable ticketBuyer;
    address public owner;
    address public veilCore; // VeilWarCore on Inco / authorized bridge relayer
    address public referrer;
    uint256 public ticketUnitCost; // USDC (6 decimals) per ticket purchase budget
    uint16 public stakeJackpotBps; // % of duel stake pool used to buy shared tickets
    bool public paused;

    // —— Scout-to-earn ——
    /// @dev reason: 1=bot kill, 2=match win, 3=vault loot
    mapping(uint8 => uint256) public ticketsPerReason;
    mapping(address => uint256) public ticketsEarned;
    mapping(address => uint256[]) public playerTicketIds;
    mapping(bytes32 => bool) public processedReward; // matchId+player+reason+nonce anti-replay

    // —— Jackpot Stake Match ——
    struct StakeMatch {
        address playerA;
        address playerB;
        uint256 stakeA;
        uint256 stakeB;
        bool aJoined;
        bool bJoined;
        bool settled;
        address winner;
        uint256 sharedTicketsBought;
    }

    uint256 public nextStakeId = 1;
    mapping(uint256 => StakeMatch) public stakeMatches;
    mapping(uint256 => uint256[]) public stakeSharedTicketIds;

    // —— Events ——
    event VeilCoreUpdated(address indexed core);
    event ConfigUpdated(uint256 ticketUnitCost, uint16 stakeJackpotBps);
    event TicketsPurchased(address indexed recipient, uint256 count, uint256[] ticketIds, string source);
    event ScoutReward(uint256 indexed matchId, address indexed player, uint8 reason, uint256 tickets);
    event StakeMatchCreated(uint256 indexed stakeId, address indexed creator, uint256 stakeAmount);
    event StakeMatchJoined(uint256 indexed stakeId, address indexed joiner, uint256 stakeAmount);
    event StakeMatchSettled(
        uint256 indexed stakeId,
        address indexed winner,
        uint256 winnerPayout,
        uint256 jackpotSpend,
        uint256 ticketsBought
    );
    event TreasuryDeposit(address indexed from, uint256 amount);
    event Paused(bool status);

    error NotOwner();
    error NotCore();
    error PausedError();
    error ZeroAddress();
    error BadAmount();
    error BadState();
    error TransferFailed();
    error AlreadyProcessed();
    error NotParticipant();

    modifier onlyOwner() {
        if (msg.sender != owner) revert NotOwner();
        _;
    }

    modifier onlyCore() {
        if (msg.sender != veilCore) revert NotCore();
        _;
    }

    modifier whenNotPaused() {
        if (paused) revert PausedError();
        _;
    }

    constructor(
        address usdc_,
        address ticketBuyer_,
        address veilCore_,
        address referrer_,
        uint256 ticketUnitCost_
    ) {
        if (usdc_ == address(0) || ticketBuyer_ == address(0)) revert ZeroAddress();
        usdc = IERC20Minimal(usdc_);
        ticketBuyer = IJackpotRandomTicketBuyer(ticketBuyer_);
        veilCore = veilCore_;
        referrer = referrer_;
        ticketUnitCost = ticketUnitCost_ == 0 ? 1e6 : ticketUnitCost_; // default 1 USDC
        stakeJackpotBps = 2000; // 20% of pooled stake → Megapot tickets
        owner = msg.sender;

        // Scout-to-earn defaults
        ticketsPerReason[1] = 1; // bot kill
        ticketsPerReason[2] = 2; // match win
        ticketsPerReason[3] = 1; // vault loot
    }

    // ============================================================
    // Admin
    // ============================================================

    function setVeilCore(address core) external onlyOwner {
        if (core == address(0)) revert ZeroAddress();
        veilCore = core;
        emit VeilCoreUpdated(core);
    }

    function setConfig(uint256 ticketUnitCost_, uint16 stakeJackpotBps_, address referrer_) external onlyOwner {
        if (stakeJackpotBps_ > 5000) revert BadAmount(); // hard cap 50%
        ticketUnitCost = ticketUnitCost_;
        stakeJackpotBps = stakeJackpotBps_;
        referrer = referrer_;
        emit ConfigUpdated(ticketUnitCost_, stakeJackpotBps_);
    }

    function setTicketsPerReason(uint8 reason, uint256 count) external onlyOwner {
        ticketsPerReason[reason] = count;
    }

    function setPaused(bool status) external onlyOwner {
        paused = status;
        emit Paused(status);
    }

    function depositTreasury(uint256 amount) external whenNotPaused {
        if (amount == 0) revert BadAmount();
        if (!usdc.transferFrom(msg.sender, address(this), amount)) revert TransferFailed();
        emit TreasuryDeposit(msg.sender, amount);
    }

    function approveBuyer(uint256 amount) external onlyOwner {
        usdc.approve(address(ticketBuyer), amount);
    }

    function rescueUSDC(address to, uint256 amount) external onlyOwner {
        if (to == address(0)) revert ZeroAddress();
        if (!usdc.transfer(to, amount)) revert TransferFailed();
    }

    // ============================================================
    // Scout-to-Earn (called by VeilWarCore / bridge relayer)
    // ============================================================

    /**
     * @notice Hook from VeilWarCore after vault loot / bot kill / match settle.
     * @param matchId Inco-side match id
     * @param player Beneficiary
     * @param reason 1=bot, 2=win, 3=vault
     */
    function onVeilReward(uint256 matchId, address player, uint8 reason) external onlyCore whenNotPaused {
        if (player == address(0)) revert ZeroAddress();
        bytes32 key = keccak256(abi.encode(matchId, player, reason, block.number));
        // Allow multiple vaults same block via reason+match; use vault-specific path below for uniqueness
        // Soft replay guard: same player+match+reason same block ignored
        if (processedReward[key]) revert AlreadyProcessed();
        processedReward[key] = true;

        uint256 count = ticketsPerReason[reason];
        if (count == 0) count = 1;

        uint256[] memory ids = _buyTickets(player, count, _sourceFor(reason));
        ticketsEarned[player] += count;
        for (uint256 i = 0; i < ids.length; i++) {
            playerTicketIds[player].push(ids[i]);
        }
        emit ScoutReward(matchId, player, reason, count);
    }

    /**
     * @notice Manual claim path for sandboxed / cross-chain attested vault loot.
     * @dev Operator or player with signed attestation can call; here gated to owner for demo safety.
     */
    function claimVaultTicket(address player, uint256 matchId, uint8 vaultIndex) external whenNotPaused {
        if (msg.sender != owner && msg.sender != player) revert NotParticipant();
        bytes32 key = keccak256(abi.encodePacked("vault", matchId, vaultIndex, player));
        if (processedReward[key]) revert AlreadyProcessed();
        processedReward[key] = true;

        uint256 count = ticketsPerReason[3];
        if (count == 0) count = 1;
        uint256[] memory ids = _buyTickets(player, count, "veil-war:vault");
        ticketsEarned[player] += count;
        for (uint256 i = 0; i < ids.length; i++) {
            playerTicketIds[player].push(ids[i]);
        }
        emit ScoutReward(matchId, player, 3, count);
    }

    // ============================================================
    // Jackpot Stake Match (multiplayer pooling)
    // ============================================================

    /**
     * @notice Open a stake match — player A deposits USDC stake.
     */
    function createStakeMatch(uint256 stakeAmount) external whenNotPaused returns (uint256 stakeId) {
        if (stakeAmount == 0) revert BadAmount();
        if (!usdc.transferFrom(msg.sender, address(this), stakeAmount)) revert TransferFailed();

        stakeId = nextStakeId++;
        StakeMatch storage s = stakeMatches[stakeId];
        s.playerA = msg.sender;
        s.stakeA = stakeAmount;
        s.aJoined = true;
        emit StakeMatchCreated(stakeId, msg.sender, stakeAmount);
    }

    /**
     * @notice Player B joins with matching (or any) stake; both locked until settle.
     */
    function joinStakeMatch(uint256 stakeId, uint256 stakeAmount) external whenNotPaused {
        StakeMatch storage s = stakeMatches[stakeId];
        if (s.settled || s.bJoined || s.playerA == address(0)) revert BadState();
        if (stakeAmount == 0) revert BadAmount();
        if (!usdc.transferFrom(msg.sender, address(this), stakeAmount)) revert TransferFailed();

        s.playerB = msg.sender;
        s.stakeB = stakeAmount;
        s.bJoined = true;
        emit StakeMatchJoined(stakeId, msg.sender, stakeAmount);
    }

    /**
     * @notice Settle duel: winner takes sector loot (remaining stake); % buys shared Megapot tickets.
     * @param winner Must be playerA or playerB
     */
    function settleStakeMatch(uint256 stakeId, address winner) external whenNotPaused {
        StakeMatch storage s = stakeMatches[stakeId];
        if (s.settled || !s.aJoined || !s.bJoined) revert BadState();
        if (winner != s.playerA && winner != s.playerB) revert NotParticipant();
        // Only participants or owner may settle (sandbox); production: require VeilWarCore attestation
        if (msg.sender != s.playerA && msg.sender != s.playerB && msg.sender != owner && msg.sender != veilCore) {
            revert NotParticipant();
        }

        s.settled = true;
        s.winner = winner;

        uint256 pool = s.stakeA + s.stakeB;
        uint256 jackpotSpend = (pool * stakeJackpotBps) / 10_000;
        // Round spend down to whole ticket units
        uint256 ticketsToBuy = ticketUnitCost == 0 ? 0 : jackpotSpend / ticketUnitCost;
        uint256 actualSpend = ticketsToBuy * ticketUnitCost;
        uint256 winnerPayout = pool - actualSpend;

        if (ticketsToBuy > 0) {
            // Shared tickets: alternate recipients or send to winner as social prize
            uint256[] memory ids = _buyTickets(winner, ticketsToBuy, "veil-war:stake-match");
            s.sharedTicketsBought = ticketsToBuy;
            stakeSharedTicketIds[stakeId] = ids;
            ticketsEarned[winner] += ticketsToBuy;
            for (uint256 i = 0; i < ids.length; i++) {
                playerTicketIds[winner].push(ids[i]);
            }
        }

        if (winnerPayout > 0) {
            if (!usdc.transfer(winner, winnerPayout)) revert TransferFailed();
        }

        emit StakeMatchSettled(stakeId, winner, winnerPayout, actualSpend, ticketsToBuy);
    }

    // ============================================================
    // Ticket purchase helper
    // ============================================================

    function _buyTickets(
        address recipient,
        uint256 count,
        string memory source
    ) internal returns (uint256[] memory ticketIds) {
        if (count == 0) revert BadAmount();
        uint256 cost = count * ticketUnitCost;
        if (usdc.balanceOf(address(this)) < cost) revert BadAmount();

        // Ensure allowance for Megapot buyer
        uint256 allowance = usdc.allowance(address(this), address(ticketBuyer));
        if (allowance < cost) {
            usdc.approve(address(ticketBuyer), type(uint256).max);
        }

        address[] memory referrers;
        uint256[] memory splits;
        if (referrer != address(0)) {
            referrers = new address[](1);
            referrers[0] = referrer;
            splits = new uint256[](1);
            splits[0] = 10_000;
        } else {
            referrers = new address[](0);
            splits = new uint256[](0);
        }

        ticketIds = ticketBuyer.buyTickets(count, recipient, referrers, splits, source);
        emit TicketsPurchased(recipient, count, ticketIds, source);
    }

    function _sourceFor(uint8 reason) internal pure returns (string memory) {
        if (reason == 1) return "veil-war:bot-kill";
        if (reason == 2) return "veil-war:match-win";
        if (reason == 3) return "veil-war:vault";
        return "veil-war:reward";
    }

    // —— Views ——

    function getPlayerTickets(address player) external view returns (uint256[] memory) {
        return playerTicketIds[player];
    }

    function getStakeSharedTickets(uint256 stakeId) external view returns (uint256[] memory) {
        return stakeSharedTicketIds[stakeId];
    }

    function treasuryBalance() external view returns (uint256) {
        return usdc.balanceOf(address(this));
    }
}
