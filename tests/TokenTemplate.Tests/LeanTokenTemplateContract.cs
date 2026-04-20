#nullable enable
using Neo;
using Neo.SmartContract.Testing;
using System.ComponentModel;
using System.Numerics;

namespace TokenTemplate.Tests;

/// <summary>
/// Strongly-typed abstract proxy for the LeanTokenTemplate NEP-17 contract.
/// </summary>
public abstract class LeanTokenTemplateContract : SmartContract
{
    protected LeanTokenTemplateContract(SmartContractInitialize initialize) : base(initialize) { }

    public abstract string? Symbol { [DisplayName("symbol")] get; }

    public abstract byte? Decimals { [DisplayName("decimals")] get; }

    public abstract BigInteger? TotalSupply { [DisplayName("totalSupply")] get; }

    [DisplayName("balanceOf")]
    public abstract BigInteger? BalanceOf(UInt160? account);

    [DisplayName("getOwner")]
    public abstract UInt160? getOwner();

    [DisplayName("getName")]
    public abstract string? getName();

    [DisplayName("getMintable")]
    public abstract bool? getMintable();

    [DisplayName("getMaxSupply")]
    public abstract BigInteger? getMaxSupply();

    [DisplayName("isUpgradeable")]
    public abstract bool? isUpgradeable();

    [DisplayName("isLocked")]
    public abstract bool? isLocked();

    [DisplayName("isPausable")]
    public abstract bool? isPausable();

    [DisplayName("isPaused")]
    public abstract bool? isPaused();

    [DisplayName("getMetadataUri")]
    public abstract string? getMetadataUri();

    [DisplayName("getAuthorizedFactory")]
    public abstract UInt160? getAuthorizedFactory();

    [DisplayName("getPlatformFeeRate")]
    public abstract BigInteger? getPlatformFeeRate();

    [DisplayName("getCreatorFeeRate")]
    public abstract BigInteger? getCreatorFeeRate();

    [DisplayName("getBurnRate")]
    public abstract BigInteger? getBurnRate();

    [DisplayName("getClaimableCreatorFee")]
    public abstract BigInteger? getClaimableCreatorFee();

    [DisplayName("getCreatorClaimant")]
    public abstract UInt160? getCreatorClaimant();

    [DisplayName("verify")]
    public abstract bool? verify();

    [DisplayName("setMetadataUri")]
    public abstract void SetMetadataUri(string? uri);
}
