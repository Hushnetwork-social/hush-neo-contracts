#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using System;
using System.IO;
using System.Numerics;

namespace TokenTemplate.Tests;

[TestFixture]
public class LeanTokenTemplateRouterEconomicsTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void LeanSpeculationLaunch_WithConfiguredFees_MovesCurveInventoryWithoutTransferEconomics()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creator = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var factory = DeployFactory(engine, owner.Account);
        using var router = DeployRouter(engine, owner.Account, factory.Hash);
        BootstrapFullAndLeanTemplates(factory);
        factory.SetBondingCurveRouter(router.Hash);
        factory.SetAllTokensPlatformFee(800_000, 0, 10);

        var factoryGasBefore = GasBalanceOf(engine, factory.Hash);

        TransferGasToFactory(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Launch Exempt",
            "LLE",
            (BigInteger)1_000,
            (BigInteger)8,
            "speculation",
            "",
            (BigInteger)500_000,
            "GAS",
            (BigInteger)600,
            "starter",
            "lean-nep17"
        });

        UInt160 tokenHash = GetLatestCreatedToken(factory, creator.Account);
        using var token = engine.FromHash<LeanTokenTemplateContract>(tokenHash, true);

        Assert.Multiple(() =>
        {
            Assert.That(token.BalanceOf(router.Hash), Is.EqualTo((BigInteger)600));
            Assert.That(token.BalanceOf(creator.Account), Is.EqualTo((BigInteger)400));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo(BigInteger.Zero));
            Assert.That(GasBalanceOf(engine, token.Hash), Is.EqualTo(BigInteger.Zero));
            Assert.That(GasBalanceOf(engine, factory.Hash) - factoryGasBefore, Is.EqualTo((BigInteger)1_500_000_000));
        });
    }

    [Test]
    public void LeanSpeculationBuy_WithCreatorFeeEnabled_AccruesCreatorFeeDeposit()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creator = TestEngine.GetNewSigner();
        var trader = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var factory = DeployFactory(engine, owner.Account);
        using var router = DeployRouter(engine, owner.Account, factory.Hash);
        BootstrapFullAndLeanTemplates(factory);
        factory.SetBondingCurveRouter(router.Hash);

        TransferGasToFactory(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Creator Fee Curve",
            "LCF",
            (BigInteger)1_000_000_000,
            (BigInteger)8,
            "speculation",
            "",
            (BigInteger)5_000_000,
            "GAS",
            (BigInteger)1_000_000_000,
            "growth",
            "lean-nep17"
        });

        UInt160 tokenHash = GetLatestCreatedToken(factory, creator.Account);
        using var token = engine.FromHash<LeanTokenTemplateContract>(tokenHash, true);
        var buyQuote = router.GetBuyQuote(tokenHash, 10_000_000_000)!;

        FundWalletWithGas(engine, trader.Account, 20_000_000_000);
        engine.SetTransactionSigners(new Signer { Account = trader.Account, Scopes = WitnessScope.Global });
        var bought = engine.Native.GAS.Transfer(
            trader.Account,
            router.Hash,
            10_000_000_000,
            new object[] { tokenHash, (BigInteger)1 }
        );

        Assert.Multiple(() =>
        {
            Assert.That(ParseBigInteger(buyQuote[7]), Is.EqualTo((BigInteger)5_000_000));
            Assert.That(bought, Is.True);
            Assert.That(token.BalanceOf(trader.Account), Is.GreaterThan(BigInteger.Zero));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo((BigInteger)5_000_000));
        });
    }

    private static TokenFactoryContract DeployFactory(TestEngine engine, UInt160 ownerAddress)
    {
        var nefPath = Path.Combine(ArtifactsPath, "TokenFactory.nef");
        var manifestPath = Path.Combine(ArtifactsPath, "TokenFactory.manifest.json");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        return engine.Deploy<TokenFactoryContract>(nef, manifest, ownerAddress);
    }

    private static BondingCurveRouterContract DeployRouter(TestEngine engine, UInt160 ownerAddress, UInt160 factoryHash)
    {
        var nefPath = Path.Combine(ArtifactsPath, "BondingCurveRouter.nef");
        var manifestPath = Path.Combine(ArtifactsPath, "BondingCurveRouter.manifest.json");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        return engine.Deploy<BondingCurveRouterContract>(nef, manifest, new object[] { ownerAddress, factoryHash });
    }

    private static void BootstrapFullAndLeanTemplates(TokenFactoryContract factory)
    {
        var fullNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var fullManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        var leanNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "LeanTokenTemplate.nef"));
        var leanManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "LeanTokenTemplate.manifest.json"));

        factory.SetNefAndManifest(fullNef, fullManifest);
        factory.SetLeanNefAndManifest(leanNef, leanManifest);
    }

    private static void TransferGasToFactory(TestEngine engine, TokenFactoryContract factory, Signer signer, BigInteger amountDatoshi, object[] tokenData)
    {
        FundWalletWithGas(engine, signer.Account, amountDatoshi + 1_000_000_000);
        engine.SetTransactionSigners(new Signer { Account = signer.Account, Scopes = WitnessScope.Global });
        bool transferred = engine.Native.GAS.Transfer(signer.Account, factory.Hash, amountDatoshi, tokenData) == true;
        Assert.That(transferred, Is.True);
    }

    private static UInt160 GetLatestCreatedToken(TokenFactoryContract factory, UInt160 creator)
    {
        var tokens = factory.GetTokensByCreator(creator, 0, 100);
        Assert.That(tokens, Is.Not.Null.And.Length.GreaterThan(0));
        return tokens![tokens.Length - 1];
    }

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
