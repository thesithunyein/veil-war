// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {e, euint256, ebool} from "./lib/IncoLightning.sol";

/**
 * @title VeilWarCore
 * @notice Confidential fog-of-war duel on a 16×16 grid using Inco Lightning encrypted state.
 * @dev Positions & vault coords stay encrypted. Line-of-sight uses e.eq / e.le over Manhattan range.
 *      Looting a Megapot Vault emits VaultLooted for the Base MegapotRewardController to settle tickets.
 */
contract VeilWarCore {
    uint8 public constant GRID = 16;
    uint8 public constant MAX_VAULTS = 8;
    uint8 public constant MAX_PLAYERS = 2;

    enum MatchPhase {
        Lobby,
        Active,
        Settled
    }

    struct EncryptedCoord {
        euint256 x;
        euint256 y;
    }

    struct Vault {
        euint256 x;
        euint256 y;
        bool looted;
        bool exists;
    }

    struct Match {
        address playerA;
        address playerB;
        EncryptedCoord posA;
        EncryptedCoord posB;
        EncryptedCoord bot;
        bool botAlive;
        MatchPhase phase;
        uint8 visionRadius; // cleartext radius (game rule), comparisons still FHE
        uint64 startedAt;
        address winner;
        uint8 vaultCount;
    }

    uint256 public nextMatchId = 1;
    mapping(uint256 => Match) public matches;
    mapping(uint256 => mapping(uint8 => Vault)) public vaults; // matchId => vaultIndex
    mapping(uint256 => mapping(address => bool)) public hasClaimedVaultCredit;

    address public owner;
    address public rewardController; // MegapotRewardController (Base / bridge sink)

    event MatchCreated(uint256 indexed matchId, address indexed creator, uint8 visionRadius);
    event MatchJoined(uint256 indexed matchId, address indexed joiner);
    event ScoutMoved(uint256 indexed matchId, address indexed player);
    event BotMoved(uint256 indexed matchId);
    event VisionChecked(uint256 indexed matchId, address indexed observer, bool targetVisible);
    event VaultLooted(uint256 indexed matchId, address indexed looter, uint8 vaultIndex);
    event BotDefeated(uint256 indexed matchId, address indexed victor);
    event MatchSettled(uint256 indexed matchId, address indexed winner);
    event RewardControllerUpdated(address indexed controller);

    error NotOwner();
    error BadPhase();
    error NotPlayer();
    error InvalidCoord();
    error AlreadyLooted();
    error VaultMissing();
    error BotDead();
    error ZeroAddress();
    error MatchFull();

    modifier onlyOwner() {
        if (msg.sender != owner) revert NotOwner();
        _;
    }

    modifier inMatch(uint256 matchId) {
        Match storage m = matches[matchId];
        if (msg.sender != m.playerA && msg.sender != m.playerB) revert NotPlayer();
        _;
    }

    constructor(address rewardController_) {
        owner = msg.sender;
        if (rewardController_ != address(0)) rewardController = rewardController_;
    }

    function setRewardController(address controller) external onlyOwner {
        if (controller == address(0)) revert ZeroAddress();
        rewardController = controller;
        emit RewardControllerUpdated(controller);
    }

    /**
     * @notice Create a duel lobby. Creator's scout starts encrypted at (spawnX, spawnY).
     * @param visionRadius Chebyshev/Manhattan LOS radius in cells (typically 2–3).
     */
    function createMatch(uint8 spawnX, uint8 spawnY, uint8 visionRadius) external returns (uint256 matchId) {
        if (spawnX >= GRID || spawnY >= GRID) revert InvalidCoord();
        if (visionRadius == 0 || visionRadius > 6) revert InvalidCoord();

        matchId = nextMatchId++;
        Match storage m = matches[matchId];
        m.playerA = msg.sender;
        m.posA = EncryptedCoord({x: e.asEuint256(spawnX), y: e.asEuint256(spawnY)});
        m.visionRadius = visionRadius;
        m.phase = MatchPhase.Lobby;
        m.botAlive = true;
        // Cloaked bot spawn opposite corner (encrypted)
        m.bot = EncryptedCoord({x: e.asEuint256(GRID - 1 - spawnX), y: e.asEuint256(GRID - 1 - spawnY)});

        _seedVaults(matchId, spawnX, spawnY);
        emit MatchCreated(matchId, msg.sender, visionRadius);
    }

    function joinMatch(uint256 matchId, uint8 spawnX, uint8 spawnY) external {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Lobby) revert BadPhase();
        if (m.playerB != address(0)) revert MatchFull();
        if (spawnX >= GRID || spawnY >= GRID) revert InvalidCoord();

        m.playerB = msg.sender;
        m.posB = EncryptedCoord({x: e.asEuint256(spawnX), y: e.asEuint256(spawnY)});
        m.phase = MatchPhase.Active;
        m.startedAt = uint64(block.timestamp);
        emit MatchJoined(matchId, msg.sender);
    }

    /**
     * @notice Solo Quick Duel — activate without a second human (bot already seeded).
     */
    function startSolo(uint256 matchId) external inMatch(matchId) {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Lobby) revert BadPhase();
        if (msg.sender != m.playerA) revert NotPlayer();
        m.phase = MatchPhase.Active;
        m.startedAt = uint64(block.timestamp);
    }

    /**
     * @notice Submit a new encrypted scout position (plaintext inputs encrypted on-chain via `e.asEuint256`).
     * @dev Production Inco Lightning: pass ciphertext handles from the client SDK instead of clear coords.
     */
    function moveScout(uint256 matchId, uint8 newX, uint8 newY) external inMatch(matchId) {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Active) revert BadPhase();
        if (newX >= GRID || newY >= GRID) revert InvalidCoord();

        EncryptedCoord storage pos = msg.sender == m.playerA ? m.posA : m.posB;
        pos.x = e.asEuint256(newX);
        pos.y = e.asEuint256(newY);
        emit ScoutMoved(matchId, msg.sender);

        _tryLootVaults(matchId, msg.sender, pos);
    }

    /**
     * @notice Move cloaked bot (keeper / host). Encrypted destination.
     */
    function moveBot(uint256 matchId, uint8 newX, uint8 newY) external inMatch(matchId) {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Active) revert BadPhase();
        if (!m.botAlive) revert BotDead();
        if (newX >= GRID || newY >= GRID) revert InvalidCoord();
        m.bot.x = e.asEuint256(newX);
        m.bot.y = e.asEuint256(newY);
        emit BotMoved(matchId);
    }

    /**
     * @notice Confidential LOS: returns whether observer can see target using encrypted Chebyshev distance.
     * @dev Uses e.le on max(|dx|,|dy|) vs visionRadius without revealing absolute positions on-chain
     *      (shim reveals for local tests; production keeps ebool encrypted until gateway decrypt).
     */
    function checkVisionOnBot(uint256 matchId) external inMatch(matchId) returns (bool visible) {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Active) revert BadPhase();

        EncryptedCoord storage observer = msg.sender == m.playerA ? m.posA : m.posB;
        visible = _inVision(observer, m.bot, m.visionRadius);
        emit VisionChecked(matchId, msg.sender, visible);
    }

    function checkVisionOnRival(uint256 matchId) external inMatch(matchId) returns (bool visible) {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Active) revert BadPhase();
        if (m.playerB == address(0)) revert BadPhase();

        EncryptedCoord storage observer = msg.sender == m.playerA ? m.posA : m.posB;
        EncryptedCoord storage target = msg.sender == m.playerA ? m.posB : m.posA;
        visible = _inVision(observer, target, m.visionRadius);
        emit VisionChecked(matchId, msg.sender, visible);
    }

    /**
     * @notice Attack bot if currently in vision (verified via FHE LOS).
     */
    function attackBot(uint256 matchId) external inMatch(matchId) {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Active) revert BadPhase();
        if (!m.botAlive) revert BotDead();

        EncryptedCoord storage observer = msg.sender == m.playerA ? m.posA : m.posB;
        bool visible = _inVision(observer, m.bot, m.visionRadius);
        if (!visible) revert InvalidCoord();

        m.botAlive = false;
        emit BotDefeated(matchId, msg.sender);
        // Scout-to-earn: notify reward layer
        _notifyReward(matchId, msg.sender, 1); // reason code 1 = bot kill
    }

    function settleMatch(uint256 matchId, address winner) external inMatch(matchId) {
        Match storage m = matches[matchId];
        if (m.phase != MatchPhase.Active) revert BadPhase();
        if (winner != m.playerA && winner != m.playerB && winner != msg.sender) revert NotPlayer();
        m.phase = MatchPhase.Settled;
        m.winner = winner;
        emit MatchSettled(matchId, winner);
        _notifyReward(matchId, winner, 2); // reason 2 = match win
    }

    // —— Internal FHE LOS + vaults ——

    function _inVision(EncryptedCoord storage a, EncryptedCoord storage b, uint8 radius) internal view returns (bool) {
        euint256 dx = e.absDiff(a.x, b.x);
        euint256 dy = e.absDiff(a.y, b.y);
        // Chebyshev distance = max(dx, dy)
        ebool dxGe = e.ge(dx, dy);
        euint256 chebyshev = e.select(dxGe, dx, dy);
        ebool inRange = e.le(chebyshev, uint256(radius));
        return e.reveal(inRange);
    }

    function _seedVaults(uint256 matchId, uint8 avoidX, uint8 avoidY) internal {
        Match storage m = matches[matchId];
        // Deterministic but encrypted vault placements (4 vaults)
        uint8[4] memory xs = [uint8(3), 8, 12, 5];
        uint8[4] memory ys = [uint8(7), 2, 11, 14];
        uint8 count;
        for (uint8 i = 0; i < 4; i++) {
            if (xs[i] == avoidX && ys[i] == avoidY) continue;
            vaults[matchId][count] = Vault({
                x: e.asEuint256(xs[i]),
                y: e.asEuint256(ys[i]),
                looted: false,
                exists: true
            });
            count++;
        }
        m.vaultCount = count;
    }

    function _tryLootVaults(uint256 matchId, address looter, EncryptedCoord storage pos) internal {
        Match storage m = matches[matchId];
        for (uint8 i = 0; i < m.vaultCount; i++) {
            Vault storage v = vaults[matchId][i];
            if (!v.exists || v.looted) continue;
            ebool sameX = e.eq(pos.x, v.x);
            ebool sameY = e.eq(pos.y, v.y);
            ebool hit = e.and(sameX, sameY);
            if (e.reveal(hit)) {
                v.looted = true;
                emit VaultLooted(matchId, looter, i);
                _notifyReward(matchId, looter, 3); // reason 3 = vault loot
            }
        }
    }

    function _notifyReward(uint256 matchId, address player, uint8 reason) internal {
        address ctrl = rewardController;
        if (ctrl == address(0)) return;
        // low-level call keeps core deployable even if controller not ready
        (bool ok, ) = ctrl.call(
            abi.encodeWithSignature("onVeilReward(uint256,address,uint8)", matchId, player, reason)
        );
        ok; // silence — controller may reject; event already emitted for indexers
    }

    // —— Views (shim plaintext for UI / tests) ——

    function getVaultCount(uint256 matchId) external view returns (uint8) {
        return matches[matchId].vaultCount;
    }

    function isVaultLooted(uint256 matchId, uint8 vaultIndex) external view returns (bool) {
        return vaults[matchId][vaultIndex].looted;
    }

    function revealScoutForOwner(uint256 matchId) external view inMatchView(matchId) returns (uint256 x, uint256 y) {
        Match storage m = matches[matchId];
        EncryptedCoord storage pos = msg.sender == m.playerA ? m.posA : m.posB;
        x = e.reveal(pos.x);
        y = e.reveal(pos.y);
    }

    modifier inMatchView(uint256 matchId) {
        Match storage m = matches[matchId];
        if (msg.sender != m.playerA && msg.sender != m.playerB) revert NotPlayer();
        _;
    }
}
