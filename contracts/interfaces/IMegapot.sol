// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

/**
 * @title IJackpotRandomTicketBuyer
 * @notice Megapot Base Sepolia ticket buyer (JackpotRandomTicketBuyer).
 * @dev Mainnet/Sepolia: buyTickets(count, recipient, referrers, referralSplitBps, source)
 */
interface IJackpotRandomTicketBuyer {
    function buyTickets(
        uint256 count,
        address recipient,
        address[] calldata referrers,
        uint256[] calldata referralSplitBps,
        string calldata source
    ) external returns (uint256[] memory ticketIds);
}

interface IERC20Minimal {
    function approve(address spender, uint256 amount) external returns (bool);
    function transferFrom(address from, address to, uint256 amount) external returns (bool);
    function transfer(address to, uint256 amount) external returns (bool);
    function balanceOf(address account) external view returns (uint256);
    function allowance(address owner, address spender) external view returns (uint256);
}
