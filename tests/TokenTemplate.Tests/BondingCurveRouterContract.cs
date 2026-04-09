#nullable enable
using Neo;
using Neo.SmartContract.Testing;
using System.ComponentModel;
using System.Numerics;

namespace TokenTemplate.Tests;

public abstract class BondingCurveRouterContract : SmartContract
{
    protected BondingCurveRouterContract(SmartContractInitialize initialize) : base(initialize) { }

    [DisplayName("getOwner")]
    public abstract UInt160? GetOwner();

    [DisplayName("isPaused")]
    public abstract bool IsPaused();

    [DisplayName("getAuthorizedFactory")]
    public abstract UInt160? GetAuthorizedFactory();

    [DisplayName("verify")]
    public abstract bool Verify();

    [DisplayName("isCurveRegistered")]
    public abstract bool IsCurveRegistered(UInt160? tokenHash);

    [DisplayName("getCurve")]
    public abstract object[]? GetCurve(UInt160? tokenHash);

    [DisplayName("getPrice")]
    public abstract BigInteger GetPrice(UInt160? tokenHash);

    [DisplayName("getBuyQuote")]
    public abstract object[]? GetBuyQuote(UInt160? tokenHash, BigInteger? quoteIn);

    [DisplayName("getSellQuote")]
    public abstract object[]? GetSellQuote(UInt160? tokenHash, BigInteger? tokenIn);

    [DisplayName("isGraduationReady")]
    public abstract bool IsGraduationReady(UInt160? tokenHash);

    [DisplayName("getGraduationProgress")]
    public abstract object[]? GetGraduationProgress(UInt160? tokenHash);

    [DisplayName("setOwner")]
    public abstract void SetOwner(UInt160? newOwner);

    [DisplayName("setPaused")]
    public abstract void SetPaused(bool paused);

    [DisplayName("setAuthorizedFactory")]
    public abstract void SetAuthorizedFactory(UInt160? factoryHash);

    [DisplayName("registerCurve")]
    public abstract void RegisterCurve(UInt160? tokenHash, string? quoteAsset, BigInteger? curveInventory);

    [DisplayName("onNEP17Payment")]
    public abstract void OnNEP17Payment(UInt160? from, BigInteger? amount, object? data = null);
}
