#nullable enable
using Neo;
using Neo.SmartContract.Testing;
using System.ComponentModel;
using System.Numerics;

namespace TokenTemplate.Tests;

/// <summary>
/// Strongly-typed abstract proxy for the TokenFactory contract.
///
/// The TestEngine uses Moq to create a concrete subclass at runtime, automatically
/// wiring each abstract method to Invoke(abiMethodName, args) on the in-process NeoVM.
/// [DisplayName] attributes map C# names to ABI method names (camelCase in ABI).
///
/// Deploy parameter (_deploy data): single UInt160 initial owner address.
/// If null, the contract falls back to Runtime.Transaction.Sender.
/// </summary>
public abstract class TokenFactoryContract : SmartContract
{
    protected TokenFactoryContract(SmartContractInitialize initialize) : base(initialize) { }

    // ── Registry queries ──────────────────────────────────────────────────────

    [DisplayName("getTokenCount")]
    public abstract BigInteger GetTokenCount();

    [DisplayName("getToken")]
    public abstract object[]? GetToken(UInt160? hash);

    [DisplayName("getTokensByCreator")]
    public abstract UInt160[]? GetTokensByCreator(UInt160? creator, BigInteger? page, BigInteger? pageSize);

    // ── Status queries ────────────────────────────────────────────────────────

    [DisplayName("getOwner")]
    public abstract UInt160? GetOwner();

    [DisplayName("isInitialized")]
    public abstract bool IsInitialized();

    [DisplayName("isPaused")]
    public abstract bool IsPaused();

    [DisplayName("getMinFee")]
    public abstract BigInteger GetMinFee();

    [DisplayName("getTreasury")]
    public abstract UInt160? GetTreasury();

    [DisplayName("getPremiumTiersEnabled")]
    public abstract bool GetPremiumTiersEnabled();

    // ── Admin functions ───────────────────────────────────────────────────────

    [DisplayName("setNefAndManifest")]
    public abstract void SetNefAndManifest(byte[]? nef, string? manifest);

    [DisplayName("setFee")]
    public abstract void SetFee(BigInteger? standardFeeDataoshi);

    [DisplayName("setTreasuryAddress")]
    public abstract void SetTreasuryAddress(UInt160? address);

    [DisplayName("setPremiumTiersEnabled")]
    public abstract void SetPremiumTiersEnabled(bool enabled);

    [DisplayName("pause")]
    public abstract void Pause();

    [DisplayName("unpause")]
    public abstract void Unpause();

    [DisplayName("setOwner")]
    public abstract void SetOwner(UInt160? newOwner);

    // ── NEP-17 callback ───────────────────────────────────────────────────────

    [DisplayName("onNEP17Payment")]
    public abstract void OnNEP17Payment(UInt160? from, BigInteger? amount, object? data = null);
}
