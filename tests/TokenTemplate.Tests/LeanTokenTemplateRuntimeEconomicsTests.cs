#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using System.Numerics;

namespace TokenTemplate.Tests;

[TestFixture]
public class LeanTokenTemplateRuntimeEconomicsTests
{
    [Test]
    public void QuoteTransfer_WithLocalFeesAndBurnRate_ReturnsExpectedBreakdown()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = DeployLeanToken(engine, owner.Account, factory.Account);
        token.SetBurnRate(200);

        var transferQuote = token.quoteTransfer(owner.Account, recipient.Account, 10_000);
        var burnQuote = token.quoteTransfer(owner.Account, UInt160.Zero, 10_000);
        var mintQuote = token.quoteTransfer(UInt160.Zero, recipient.Account, 1_000);

        Assert.Multiple(() =>
        {
            Assert.That(ParseBigInteger(transferQuote![0]), Is.EqualTo((BigInteger)10_000));
            Assert.That(ParseBigInteger(transferQuote[1]), Is.EqualTo((BigInteger)9_800));
            Assert.That(ParseBigInteger(transferQuote[2]), Is.EqualTo((BigInteger)200));
            Assert.That(ParseBigInteger(transferQuote[3]), Is.EqualTo((BigInteger)200));
            Assert.That(ParseBigInteger(transferQuote[4]), Is.EqualTo((BigInteger)1_000_000));
            Assert.That(ParseBigInteger(transferQuote[5]), Is.EqualTo((BigInteger)500_000));
            Assert.That(ParseBigInteger(transferQuote[6]), Is.EqualTo((BigInteger)1_500_000));
            Assert.That(ParseBigInteger(transferQuote[7]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(transferQuote[8]), Is.EqualTo(BigInteger.Zero));

            Assert.That(ParseBigInteger(burnQuote![1]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(burnQuote[2]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(burnQuote[3]), Is.EqualTo((BigInteger)10_000));
            Assert.That(ParseBigInteger(burnQuote[6]), Is.EqualTo((BigInteger)1_500_000));
            Assert.That(ParseBigInteger(burnQuote[8]), Is.EqualTo(BigInteger.One));

            Assert.That(ParseBigInteger(mintQuote![1]), Is.EqualTo((BigInteger)1_000));
            Assert.That(ParseBigInteger(mintQuote[4]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(mintQuote[5]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(mintQuote[7]), Is.EqualTo(BigInteger.One));
        });
    }

    [Test]
    public void Transfer_WithBurnPlatformAndCreatorFee_AppliesLocalEconomics()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = DeployLeanToken(engine, owner.Account, factory.Account);
        token.SetBurnRate(200);

        FundWalletWithGas(engine, owner.Account, 500_000_000);
        var factoryGasBefore = GasBalanceOf(engine, factory.Account);
        var tokenGasBefore = GasBalanceOf(engine, token.Hash);

        engine.SetTransactionSigners(new Signer { Account = owner.Account, Scopes = WitnessScope.Global });
        var result = token.Transfer(owner.Account, recipient.Account, 10_000, null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(token.BalanceOf(owner.Account), Is.EqualTo((BigInteger)90_000));
            Assert.That(token.BalanceOf(recipient.Account), Is.EqualTo((BigInteger)9_800));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)99_800));
            Assert.That(GasBalanceOf(engine, factory.Account) - factoryGasBefore, Is.EqualTo((BigInteger)1_000_000));
            Assert.That(GasBalanceOf(engine, token.Hash) - tokenGasBefore, Is.EqualTo((BigInteger)500_000));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo((BigInteger)500_000));
        });
    }

    [Test]
    public void Transfer_WithConfiguredFeesAndNoGas_AbortsAtomically()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = DeployLeanToken(engine, owner.Account, factory.Account);
        token.SetBurnRate(200);

        SetGasBalance(engine, owner.Account, BigInteger.Zero);
        var factoryGasBefore = GasBalanceOf(engine, factory.Account);
        var tokenGasBefore = GasBalanceOf(engine, token.Hash);

        engine.SetTransactionSigners(new Signer { Account = owner.Account, Scopes = WitnessScope.Global });

        Assert.Multiple(() =>
        {
            Assert.That(() => token.Transfer(owner.Account, recipient.Account, 10_000, null), Throws.Exception);
            Assert.That(token.BalanceOf(owner.Account), Is.EqualTo((BigInteger)100_000));
            Assert.That(token.BalanceOf(recipient.Account), Is.EqualTo(BigInteger.Zero));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)100_000));
            Assert.That(GasBalanceOf(engine, factory.Account) - factoryGasBefore, Is.EqualTo(BigInteger.Zero));
            Assert.That(GasBalanceOf(engine, token.Hash) - tokenGasBefore, Is.EqualTo(BigInteger.Zero));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo(BigInteger.Zero));
        });
    }

    [Test]
    public void Burn_CollectsGasFeesWithoutApplyingBurnRateToBurnedTokens()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = DeployLeanToken(engine, owner.Account, factory.Account);
        token.SetBurnRate(200);

        FundWalletWithGas(engine, owner.Account, 500_000_000);
        var factoryGasBefore = GasBalanceOf(engine, factory.Account);
        var tokenGasBefore = GasBalanceOf(engine, token.Hash);

        engine.SetTransactionSigners(new Signer { Account = owner.Account, Scopes = WitnessScope.Global });
        token.burn(10_000);

        Assert.Multiple(() =>
        {
            Assert.That(token.BalanceOf(owner.Account), Is.EqualTo((BigInteger)90_000));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)90_000));
            Assert.That(GasBalanceOf(engine, factory.Account) - factoryGasBefore, Is.EqualTo((BigInteger)1_000_000));
            Assert.That(GasBalanceOf(engine, token.Hash) - tokenGasBefore, Is.EqualTo((BigInteger)500_000));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo((BigInteger)500_000));
        });
    }

    private static LeanTokenTemplateContract DeployLeanToken(TestEngine engine, UInt160 ownerAddress, UInt160 factoryAddress) =>
        LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Runtime Economics",
            Symbol = "LRE",
            Owner = ownerAddress,
            LaunchFactory = factoryAddress,
            InitialSupply = 100_000,
            PlatformFeeRate = 1_000_000,
            CreatorFeeRate = 500_000,
            ManifestName = "LeanRuntimeEconomics"
        });

    private static void FundWalletWithGas(TestEngine engine, UInt160 walletAddress, BigInteger datoshi)
    {
        foreach (var funder in new[] { engine.CommitteeAddress, engine.ValidatorsAddress })
        {
            var funderBalance = engine.Native.GAS.BalanceOf(funder) ?? BigInteger.Zero;
            if (funderBalance < datoshi) continue;

            engine.SetTransactionSigners(new Signer { Account = funder, Scopes = WitnessScope.CalledByEntry });
            if (engine.Native.GAS.Transfer(funder, walletAddress, datoshi, null) == true)
                return;
        }

        Assert.Fail($"FundWalletWithGas({datoshi}) failed.");
    }

    private static void SetGasBalance(TestEngine engine, UInt160 walletAddress, BigInteger targetDatoshi)
    {
        var current = GasBalanceOf(engine, walletAddress);
        if (current == targetDatoshi) return;

        if (current < targetDatoshi)
        {
            FundWalletWithGas(engine, walletAddress, targetDatoshi - current);
            return;
        }

        engine.SetTransactionSigners(new Signer { Account = walletAddress, Scopes = WitnessScope.Global });
        var transferred = engine.Native.GAS.Transfer(walletAddress, engine.CommitteeAddress, current - targetDatoshi, null);
        Assert.That(transferred, Is.True);
    }

    private static BigInteger GasBalanceOf(TestEngine engine, UInt160 account) =>
        engine.Native.GAS.BalanceOf(account) ?? BigInteger.Zero;

    private static BigInteger ParseBigInteger(object? item) => item switch
    {
        BigInteger bi => bi,
        byte b => b,
        int i => i,
        long l => l,
        Neo.VM.Types.PrimitiveType primitive => new BigInteger(primitive.GetSpan()),
        _ => BigInteger.Parse(item?.ToString() ?? "0"),
    };
}
