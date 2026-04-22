#nullable enable
using Neo;
using Neo.SmartContract.Testing;
using System.ComponentModel;
using System.Numerics;

namespace TokenTemplate.Tests;

/// <summary>
/// Strongly-typed abstract proxy for the shared LeanTokenEngine contract.
/// </summary>
public abstract class LeanTokenEngineContract : SmartContract
{
    protected LeanTokenEngineContract(SmartContractInitialize initialize) : base(initialize) { }

    [DisplayName("getOwner")]
    public abstract UInt160? getOwner();

    [DisplayName("verify")]
    public abstract bool? verify();

    [DisplayName("isTokenRegistered")]
    public abstract bool? isTokenRegistered(UInt160? tokenId);

    [DisplayName("getTokenIdByFacade")]
    public abstract UInt160? getTokenIdByFacade(UInt160? facadeHash);

    [DisplayName("getFacade")]
    public abstract UInt160? getFacade(UInt160? tokenId);

    [DisplayName("getToken")]
    public abstract object[]? getToken(UInt160? tokenId);

    [DisplayName("getTokenOwner")]
    public abstract UInt160? getTokenOwner(UInt160? tokenId);

    [DisplayName("getName")]
    public abstract string? getName(UInt160? tokenId);

    [DisplayName("getSymbol")]
    public abstract string? getSymbol(UInt160? tokenId);

    [DisplayName("getDecimals")]
    public abstract BigInteger? getDecimals(UInt160? tokenId);

    [DisplayName("getMintable")]
    public abstract bool? getMintable(UInt160? tokenId);

    [DisplayName("getMaxSupply")]
    public abstract BigInteger? getMaxSupply(UInt160? tokenId);

    [DisplayName("isUpgradeable")]
    public abstract bool? isUpgradeable(UInt160? tokenId);

    [DisplayName("isLocked")]
    public abstract bool? isLocked(UInt160? tokenId);

    [DisplayName("isPausable")]
    public abstract bool? isPausable(UInt160? tokenId);

    [DisplayName("isPaused")]
    public abstract bool? isPaused(UInt160? tokenId);

    [DisplayName("getMetadataUri")]
    public abstract string? getMetadataUri(UInt160? tokenId);

    [DisplayName("getAuthorizedFactory")]
    public abstract UInt160? getAuthorizedFactory(UInt160? tokenId);

    [DisplayName("getPlatformFeeRate")]
    public abstract BigInteger? getPlatformFeeRate(UInt160? tokenId);

    [DisplayName("getCreatorFeeRate")]
    public abstract BigInteger? getCreatorFeeRate(UInt160? tokenId);

    [DisplayName("getBurnRate")]
    public abstract BigInteger? getBurnRate(UInt160? tokenId);

    [DisplayName("getClaimableCreatorFee")]
    public abstract BigInteger? getClaimableCreatorFee(UInt160? tokenId);

    [DisplayName("getCreatorClaimant")]
    public abstract UInt160? getCreatorClaimant(UInt160? tokenId);

    [DisplayName("balanceOf")]
    public abstract BigInteger? BalanceOf(UInt160? tokenId, UInt160? account);

    [DisplayName("totalSupply")]
    public abstract BigInteger? TotalSupply(UInt160? tokenId);

    [DisplayName("quoteTransfer")]
    public abstract object[]? QuoteTransfer(UInt160? tokenId, UInt160? from, UInt160? to, BigInteger? amount);

    [DisplayName("registerToken")]
    public abstract bool? RegisterToken(
        UInt160? tokenId,
        string? name,
        string? symbol,
        BigInteger? initialSupply,
        BigInteger? decimals,
        UInt160? owner,
        BigInteger? mintable,
        BigInteger? maxSupply,
        BigInteger? upgradeable,
        string? metadataUri,
        BigInteger? pausable,
        UInt160? launchFactory,
        BigInteger? platformFeeRate,
        BigInteger? creatorFeeRate);
}
