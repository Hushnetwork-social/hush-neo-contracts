#nullable enable
using Neo;
using Neo.SmartContract.Testing;
using System.ComponentModel;
using System.Numerics;

namespace TokenTemplate.Tests;

/// <summary>
/// Strongly-typed abstract proxy for the TokenTemplate NEP-17 contract.
///
/// The TestEngine uses Moq to create a concrete subclass at runtime, automatically
/// wiring each abstract method/property to Invoke(abiMethodName, args) on the
/// in-process NeoVM. [DisplayName] attributes map C# names to ABI method names.
///
/// Deploy parameters (_deploy order):
///   [0] name         string
///   [1] symbol       string
///   [2] initialSupply BigInteger
///   [3] decimals     BigInteger (byte 0-18)
///   [4] owner        UInt160
///   [5] mintable     BigInteger (1=true, 0=false)
///   [6] maxSupply    BigInteger (0=uncapped)
///   [7] upgradeable  BigInteger (1=true, 0=false)
///   [8] metadataUri  string
///   [9] pausable     BigInteger (1=true, 0=false)
/// </summary>
public abstract class TokenTemplateContract : SmartContract
{
    protected TokenTemplateContract(SmartContractInitialize initialize) : base(initialize) { }

    // ── NEP17 standard ────────────────────────────────────────────────────────

    /// <summary>Token ticker symbol (e.g. "HCT"). Maps to ABI method "symbol".</summary>
    public abstract string? Symbol { [DisplayName("symbol")] get; }

    /// <summary>Decimal precision (0-18). Maps to ABI method "decimals".</summary>
    public abstract byte? Decimals { [DisplayName("decimals")] get; }

    /// <summary>Total token supply in circulation. Maps to ABI method "totalSupply".</summary>
    public abstract BigInteger? TotalSupply { [DisplayName("totalSupply")] get; }

    [DisplayName("balanceOf")]
    public abstract BigInteger? BalanceOf(UInt160? account);

    [DisplayName("transfer")]
    public abstract bool? Transfer(UInt160? from, UInt160? to, BigInteger? amount, object? data = null);

    // ── Custom read-only getters ──────────────────────────────────────────────

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

    [DisplayName("getOwner")]
    public abstract UInt160? getOwner();

    [DisplayName("verify")]
    public abstract bool? verify();

    // ── Owner / upgrade ───────────────────────────────────────────────────────

    [DisplayName("setOwner")]
    public abstract void setOwner(UInt160? newOwner);

    [DisplayName("lock")]
    public abstract void Lock();

    [DisplayName("update")]
    public abstract void update(byte[]? nefFile, string? manifest, object? data = null);

    // ── Pausable ──────────────────────────────────────────────────────────────

    [DisplayName("setPausable")]
    public abstract void setPausable(bool? value);

    [DisplayName("pause")]
    public abstract void pause();

    [DisplayName("unpause")]
    public abstract void unpause();

    // ── Mint / Burn ───────────────────────────────────────────────────────────

    [DisplayName("mint")]
    public abstract void mint(UInt160? to, BigInteger? amount);

    [DisplayName("burn")]
    public abstract void burn(BigInteger? amount);

    [DisplayName("onNEP17Payment")]
    public abstract void OnNEP17Payment(UInt160? from, BigInteger? amount, object? data = null);
}
