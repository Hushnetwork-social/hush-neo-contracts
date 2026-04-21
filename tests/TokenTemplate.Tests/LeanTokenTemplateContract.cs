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

    [DisplayName("getLeanEngine")]
    public abstract UInt160? getLeanEngine();

    [DisplayName("getTokenId")]
    public abstract UInt160? getTokenId();

    [DisplayName("balanceOf")]
    public abstract BigInteger? BalanceOf(UInt160? account);

    [DisplayName("transfer")]
    public abstract bool? Transfer(UInt160? from, UInt160? to, BigInteger? amount, object? data = null);

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

    [DisplayName("quoteTransfer")]
    public abstract object[]? quoteTransfer(UInt160? from, UInt160? to, BigInteger? amount);

    [DisplayName("setOwner")]
    public abstract void setOwner(UInt160? newOwner);

    [DisplayName("lock")]
    public abstract void Lock();

    [DisplayName("setMetadataUri")]
    public abstract void SetMetadataUri(string? uri);

    [DisplayName("setMaxSupply")]
    public abstract void SetMaxSupply(BigInteger? newMax);

    [DisplayName("setBurnRate")]
    public abstract void SetBurnRate(BigInteger? bps);

    [DisplayName("setCreatorFee")]
    public abstract void SetCreatorFee(BigInteger? datoshi);

    [DisplayName("setPlatformFeeRate")]
    public abstract void SetPlatformFeeRate(BigInteger? datoshi);

    [DisplayName("setPausable")]
    public abstract void setPausable(bool? value);

    [DisplayName("pause")]
    public abstract void pause();

    [DisplayName("unpause")]
    public abstract void unpause();

    [DisplayName("mint")]
    public abstract void mint(UInt160? to, BigInteger? amount);

    [DisplayName("burn")]
    public abstract void burn(BigInteger? amount);

    [DisplayName("claimCreatorFees")]
    public abstract void claimCreatorFees();

    [DisplayName("claimCreatorFee")]
    public abstract void claimCreatorFee(BigInteger? amount);

    [DisplayName("mintByFactory")]
    public abstract void MintByFactory(UInt160? to, BigInteger? amount);

    [DisplayName("transferByFactory")]
    public abstract void TransferByFactory(UInt160? from, UInt160? to, BigInteger? amount, object? data = null);

    [DisplayName("authorizeFactory")]
    public abstract void AuthorizeFactory(UInt160? newFactory);

    [DisplayName("update")]
    public abstract void update(byte[]? nefFile, string? manifest, object? data = null);

    [DisplayName("onNEP17Payment")]
    public abstract void OnNEP17Payment(UInt160? from, BigInteger? amount, object? data = null);
}
