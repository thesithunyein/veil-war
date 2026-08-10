// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {IJackpotRandomTicketBuyer} from "../interfaces/IMegapot.sol";

/**
 * @title MockMegapotBuyer
 * @notice Local / Foundry stand-in for JackpotRandomTicketBuyer.
 */
contract MockMegapotBuyer is IJackpotRandomTicketBuyer {
    uint256 public nextId = 1;
    mapping(address => uint256[]) public owned;

    event MockBuy(address indexed recipient, uint256 count, string source);

    function buyTickets(
        uint256 count,
        address recipient,
        address[] calldata,
        uint256[] calldata,
        string calldata source
    ) external returns (uint256[] memory ticketIds) {
        ticketIds = new uint256[](count);
        for (uint256 i = 0; i < count; i++) {
            uint256 id = nextId++;
            ticketIds[i] = id;
            owned[recipient].push(id);
        }
        emit MockBuy(recipient, count, source);
    }
}
