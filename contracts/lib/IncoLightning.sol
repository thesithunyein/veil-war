// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

/**
 * @title IncoLightning (local development shim)
 * @notice Mirrors Inco Lightning encrypted types + `e` ops (docs.inco.org/guide/operations).
 * @dev Replace this file with the official Inco Lightning SDK for production deploys.
 *      Shim stores cleartext under the hood so Foundry / local compile works without fhEVM.
 */

type euint256 is uint256;
type ebool is uint256; // 0 / 1

library e {
    // —— conversions ——
    function asEuint256(uint256 a) internal pure returns (euint256) {
        return euint256.wrap(a);
    }

    function asEbool(bool a) internal pure returns (ebool) {
        return ebool.wrap(a ? 1 : 0);
    }

    function asEbool(euint256 a) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) == 0 ? 0 : 1);
    }

    function asEuint256(ebool a) internal pure returns (euint256) {
        return euint256.wrap(ebool.unwrap(a));
    }

    // —— math ——
    function add(euint256 a, euint256 b) internal pure returns (euint256) {
        return euint256.wrap(euint256.unwrap(a) + euint256.unwrap(b));
    }

    function sub(euint256 a, euint256 b) internal pure returns (euint256) {
        return euint256.wrap(euint256.unwrap(a) - euint256.unwrap(b));
    }

    function mul(euint256 a, euint256 b) internal pure returns (euint256) {
        return euint256.wrap(euint256.unwrap(a) * euint256.unwrap(b));
    }

    function absDiff(euint256 a, euint256 b) internal pure returns (euint256) {
        uint256 x = euint256.unwrap(a);
        uint256 y = euint256.unwrap(b);
        return euint256.wrap(x >= y ? x - y : y - x);
    }

    // —— comparisons ——
    function eq(euint256 a, euint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) == euint256.unwrap(b) ? 1 : 0);
    }

    function eq(euint256 a, uint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) == b ? 1 : 0);
    }

    function ne(euint256 a, euint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) != euint256.unwrap(b) ? 1 : 0);
    }

    function le(euint256 a, euint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) <= euint256.unwrap(b) ? 1 : 0);
    }

    function le(euint256 a, uint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) <= b ? 1 : 0);
    }

    function lt(euint256 a, euint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) < euint256.unwrap(b) ? 1 : 0);
    }

    function ge(euint256 a, euint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) >= euint256.unwrap(b) ? 1 : 0);
    }

    function gt(euint256 a, euint256 b) internal pure returns (ebool) {
        return ebool.wrap(euint256.unwrap(a) > euint256.unwrap(b) ? 1 : 0);
    }

    function and(ebool a, ebool b) internal pure returns (ebool) {
        return ebool.wrap((ebool.unwrap(a) != 0 && ebool.unwrap(b) != 0) ? 1 : 0);
    }

    function or(ebool a, ebool b) internal pure returns (ebool) {
        return ebool.wrap((ebool.unwrap(a) != 0 || ebool.unwrap(b) != 0) ? 1 : 0);
    }

    function not(ebool a) internal pure returns (ebool) {
        return ebool.wrap(ebool.unwrap(a) == 0 ? 1 : 0);
    }

    function select(ebool cond, euint256 ifTrue, euint256 ifFalse) internal pure returns (euint256) {
        return eboolUnwrap(cond) ? ifTrue : ifFalse;
    }

    function randBounded(uint256 upper) internal view returns (euint256) {
        uint256 r = uint256(keccak256(abi.encodePacked(block.prevrandao, msg.sender, gasleft()))) % upper;
        return euint256.wrap(r);
    }

    /// @dev Shim "reveal" — production Inco emits async decrypt; here we expose clear for local tests.
    function reveal(euint256 a) internal pure returns (uint256) {
        return euint256.unwrap(a);
    }

    function reveal(ebool a) internal pure returns (bool) {
        return ebool.unwrap(a) != 0;
    }

    function eboolUnwrap(ebool a) private pure returns (bool) {
        return ebool.unwrap(a) != 0;
    }
}
