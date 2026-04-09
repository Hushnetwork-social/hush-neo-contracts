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
public class BondingCurveRouterTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void DirectSpeculationLaunch_RegistersCurveAndMovesExactInventory()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var factory = DeployFactory(engine, ownerSigner.Account);
        using var router = DeployRouter(engine, ownerSigner.Account, factory.Hash);
        BootstrapFactoryAndRouter(engine, ownerSigner.Account, factory, router);

        SimulateGasPayment(engine, factory, ownerSigner, 1_500_000_000, new object[]
        {
            "Spec Token", "SPEC", (BigInteger)1_000_000, (BigInteger)8,
            "speculation", "", (BigInteger)0, "GAS", (BigInteger)800_000
        });

        var tokenHash = GetLatestCreatedToken(factory, ownerSigner.Account);
        using var token = engine.FromHash<TokenTemplateContract>(tokenHash, true);

        var curve = router.GetCurve(tokenHash)!;

        Assert.Multiple(() =>
        {
            Assert.That(ParseText(factory.GetToken(tokenHash)![3]), Is.EqualTo("speculation"));
            Assert.That(router.IsCurveRegistered(tokenHash), Is.True);
            Assert.That(token.BalanceOf(ownerSigner.Account), Is.EqualTo((BigInteger)200_000));
            Assert.That(token.BalanceOf(router.Hash), Is.EqualTo((BigInteger)800_000));
            Assert.That(ParseText(curve[0]), Is.EqualTo("ACTIVE"));
            Assert.That(ParseText(curve[1]), Is.EqualTo("GAS"));
            Assert.That(ParseBigInteger(curve[11]), Is.EqualTo((BigInteger)800_000));
            Assert.That(ParseBigInteger(curve[12]), Is.EqualTo((BigInteger)200_000));
            Assert.That(ParseBigInteger(curve[13]), Is.EqualTo((BigInteger)1_000_000));
        });
    }

    [Test]
    public void CommunityToSpeculation_RegistersCurveThroughFactoryLifecycle()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var factory = DeployFactory(engine, ownerSigner.Account);
        using var router = DeployRouter(engine, ownerSigner.Account, factory.Hash);
        BootstrapFactoryAndRouter(engine, ownerSigner.Account, factory, router);

        SimulateGasPayment(engine, factory, ownerSigner, 1_500_000_000, new object[]
        {
            "Community Token", "COMM", (BigInteger)1_000_000, (BigInteger)8,
            "community", "", (BigInteger)0
        });

        var tokenHash = GetLatestCreatedToken(factory, ownerSigner.Account);
        using var token = engine.FromHash<TokenTemplateContract>(tokenHash, true);

        FundWalletWithGas(engine, ownerSigner.Account, 500_000_000);
        engine.SetTransactionSigners(new Signer { Account = ownerSigner.Account, Scopes = WitnessScope.Global });
        factory.ChangeTokenMode(tokenHash, "speculation", new object[] { "GAS", (BigInteger)600_000 });

        var curve = router.GetCurve(tokenHash)!;

        Assert.Multiple(() =>
        {
            Assert.That(ParseText(factory.GetToken(tokenHash)![3]), Is.EqualTo("speculation"));
            Assert.That(router.IsCurveRegistered(tokenHash), Is.True);
            Assert.That(token.BalanceOf(ownerSigner.Account), Is.EqualTo((BigInteger)400_000));
            Assert.That(token.BalanceOf(router.Hash), Is.EqualTo((BigInteger)600_000));
            Assert.That(ParseBigInteger(curve[11]), Is.EqualTo((BigInteger)600_000));
            Assert.That(ParseBigInteger(curve[12]), Is.EqualTo((BigInteger)400_000));
        });
    }

    [Test]
    public void BuyAndSell_KeepTradingAfterGraduationReady()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var traderSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var factory = DeployFactory(engine, ownerSigner.Account);
        using var router = DeployRouter(engine, ownerSigner.Account, factory.Hash);
        BootstrapFactoryAndRouter(engine, ownerSigner.Account, factory, router);

        SimulateGasPayment(engine, factory, ownerSigner, 1_500_000_000, new object[]
        {
            "Curve Token", "CURV", (BigInteger)1_000_000, (BigInteger)8,
            "speculation", "", (BigInteger)0, "GAS", (BigInteger)800_000
        });

        var tokenHash = GetLatestCreatedToken(factory, ownerSigner.Account);
        using var token = engine.FromHash<TokenTemplateContract>(tokenHash, true);

        FundWalletWithGas(engine, ownerSigner.Account, 500_000_000);
        engine.SetTransactionSigners(new Signer { Account = ownerSigner.Account, Scopes = WitnessScope.Global });
        factory.SetTokenBurnRate(tokenHash, 100);

        FundWalletWithGas(engine, traderSigner.Account, 12_000_000_000);
        engine.SetTransactionSigners(new Signer { Account = traderSigner.Account, Scopes = WitnessScope.Global });
        var bought = engine.Native.GAS.Transfer(traderSigner.Account, router.Hash, 8_000_000_000, new object[] { tokenHash, (BigInteger)1 });
        Assert.That(bought, Is.True);

        var boughtBalance = token.BalanceOf(traderSigner.Account) ?? BigInteger.Zero;
        Assert.That(boughtBalance, Is.GreaterThan(BigInteger.Zero));
        Assert.That(router.IsGraduationReady(tokenHash), Is.True);

        var sellAmount = boughtBalance / 2;
        Assert.That(sellAmount, Is.GreaterThan(BigInteger.Zero));
        var sold = token.Transfer(traderSigner.Account, router.Hash, sellAmount, new object[] { (BigInteger)1, sellAmount });
        Assert.That(sold, Is.True);

        var curve = router.GetCurve(tokenHash)!;
        var progress = router.GetGraduationProgress(tokenHash)!;

        Assert.Multiple(() =>
        {
            Assert.That(ParseText(curve[0]), Is.EqualTo("GRADUATION_READY"));
            Assert.That(router.IsGraduationReady(tokenHash), Is.True);
            Assert.That(ParseBoolean(progress[3]), Is.True);
            Assert.That(ParseBigInteger(curve[9]), Is.EqualTo((BigInteger)2));
        });
    }

    [Test]
    public void GetSellQuote_ReflectsTokenBurnAndGasFees()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var factory = DeployFactory(engine, ownerSigner.Account);
        using var router = DeployRouter(engine, ownerSigner.Account, factory.Hash);
        BootstrapFactoryAndRouter(engine, ownerSigner.Account, factory, router);

        factory.SetAllTokensPlatformFee(1_000_000, 0, 0);

        SimulateGasPayment(engine, factory, ownerSigner, 1_500_000_000, new object[]
        {
            "Fee Token", "FEE", (BigInteger)1_000_000, (BigInteger)8,
            "speculation", "", (BigInteger)500_000, "GAS", (BigInteger)750_000
        });

        var tokenHash = GetLatestCreatedToken(factory, ownerSigner.Account);

        FundWalletWithGas(engine, ownerSigner.Account, 500_000_000);
        engine.SetTransactionSigners(new Signer { Account = ownerSigner.Account, Scopes = WitnessScope.Global });
        factory.SetTokenBurnRate(tokenHash, 200);

        var sellQuote = router.GetSellQuote(tokenHash, 100_000)!;

        Assert.Multiple(() =>
        {
            Assert.That(ParseBigInteger(sellQuote[0]), Is.EqualTo((BigInteger)100_000));
            Assert.That(ParseBigInteger(sellQuote[1]), Is.EqualTo((BigInteger)2_000));
            Assert.That(ParseBigInteger(sellQuote[2]), Is.EqualTo((BigInteger)98_000));
            Assert.That(ParseBigInteger(sellQuote[5]), Is.EqualTo((BigInteger)1_000_000));
            Assert.That(ParseBigInteger(sellQuote[6]), Is.EqualTo((BigInteger)500_000));
        });
    }

    private static void BootstrapFactoryAndRouter(TestEngine engine, UInt160 ownerAddress, TokenFactoryContract factory, BondingCurveRouterContract router)
    {
        var nefBytes = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));

        factory.SetNefAndManifest(nefBytes, manifest);
        factory.SetBondingCurveRouter(router.Hash);
        Assert.That(router.GetAuthorizedFactory(), Is.EqualTo(factory.Hash));
    }

    private static BondingCurveRouterContract DeployRouter(TestEngine engine, UInt160 ownerAddress, UInt160 factoryHash)
    {
        var nefPath = Path.Combine(ArtifactsPath, "BondingCurveRouter.nef");
        var manifestPath = Path.Combine(ArtifactsPath, "BondingCurveRouter.manifest.json");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        return engine.Deploy<BondingCurveRouterContract>(nef, manifest, new object[] { ownerAddress, factoryHash });
    }

    private static TokenFactoryContract DeployFactory(TestEngine engine, UInt160 ownerAddress)
    {
        var nefPath = Path.Combine(ArtifactsPath, "TokenFactory.nef");
        var manifestPath = Path.Combine(ArtifactsPath, "TokenFactory.manifest.json");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        return engine.Deploy<TokenFactoryContract>(nef, manifest, ownerAddress);
    }

    private static void SimulateGasPayment(TestEngine engine, TokenFactoryContract factory, Signer signer, BigInteger amountDatoshi, object[] tokenData)
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

    private static BigInteger ParseBigInteger(object? item) => item switch
    {
        BigInteger bi => bi,
        byte b => b,
        int i => i,
        long l => l,
        Neo.VM.Types.PrimitiveType primitive => new BigInteger(primitive.GetSpan()),
        _ => BigInteger.Parse(item?.ToString() ?? "0"),
    };

    private static bool ParseBoolean(object? item) => item switch
    {
        bool value => value,
        Neo.VM.Types.Boolean boolean => boolean.GetBoolean(),
        Neo.VM.Types.PrimitiveType primitive => primitive.GetBoolean(),
        _ => bool.Parse(item?.ToString() ?? "false"),
    };

    private static string ParseText(object? item) => item switch
    {
        Neo.VM.Types.ByteString byteString => byteString.GetString() ?? string.Empty,
        Neo.VM.Types.PrimitiveType primitive => primitive.GetString() ?? string.Empty,
        _ => (item?.ToString() ?? string.Empty).Trim('"'),
    };
}
