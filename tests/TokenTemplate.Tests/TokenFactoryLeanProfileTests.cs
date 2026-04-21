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
public class TokenFactoryLeanProfileTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void LeanTemplateConfig_TracksLeanArtifactsSeparatelyFromFullTemplate()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);
        using var factory = DeployFactory(engine, owner.Account);

        var fullNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var fullManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        var leanNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "LeanTokenTemplate.nef"));
        var leanManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "LeanTokenTemplate.manifest.json"));

        factory.SetNefAndManifest(fullNef, fullManifest);
        Assert.Multiple(() =>
        {
            Assert.That(factory.IsInitialized(), Is.True);
            Assert.That(factory.IsLeanInitialized(), Is.False);
            Assert.That(ParseBigInteger(factory.GetConfig()![5]), Is.EqualTo(BigInteger.One));
            Assert.That(ParseBigInteger(factory.GetLeanTemplateConfig()![1]), Is.EqualTo(BigInteger.One));
            Assert.That(ParseBoolean(factory.GetLeanTemplateConfig()![2]), Is.False);
        });

        factory.SetLeanNefAndManifest(leanNef, leanManifest);
        Assert.That(factory.IsLeanInitialized(), Is.False);
        var leanBefore = factory.GetLeanTemplateConfig();
        using var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(engine, factory.Hash, "LeanFactoryConfigEngine");
        factory.SetLeanEngine(leanEngine.Hash);
        var leanInitialized = factory.GetLeanTemplateConfig();
        factory.UpgradeLeanTemplate(leanNef, leanManifest.Replace("\"name\":\"LeanTokenTemplate\"", "\"name\":\"LeanTokenTemplateV2\""));
        var leanAfter = factory.GetLeanTemplateConfig();

        Assert.Multiple(() =>
        {
            Assert.That(factory.IsLeanInitialized(), Is.True);
            Assert.That(ParseBoolean(leanBefore![2]), Is.True);
            Assert.That(ParseBoolean(leanBefore[3]), Is.True);
            Assert.That(ParseHash(leanBefore[4]), Is.EqualTo(UInt160.Zero));
            Assert.That(ParseHash(leanInitialized![4]), Is.EqualTo(leanEngine.Hash));
            Assert.That(factory.GetLeanEngine(), Is.EqualTo(leanEngine.Hash));
            Assert.That(ParseHash(leanBefore[0]), Is.Not.EqualTo(UInt160.Zero));
            Assert.That(ParseBigInteger(leanBefore[1]), Is.EqualTo(BigInteger.One));
            Assert.That(ParseBigInteger(leanAfter![1]), Is.EqualTo((BigInteger)2));
            Assert.That(ParseHash(leanAfter[0]), Is.Not.EqualTo(ParseHash(leanBefore[0])));
            Assert.That(ParseHash(leanAfter[4]), Is.EqualTo(leanEngine.Hash));
            Assert.That(ParseBigInteger(factory.GetConfig()![5]), Is.EqualTo(BigInteger.One));
        });
    }

    [Test]
    public void LeanProfileCreation_DeploysLeanTokenAndRecordsProfile()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creator = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);
        using var factory = DeployFactory(engine, owner.Account);
        using var leanEngine = BootstrapFullAndLeanTemplates(engine, factory);

        SimulateGasPayment(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Factory Token",
            "LFT",
            (BigInteger)1_000,
            (BigInteger)8,
            "community",
            "ipfs://lean-factory-token",
            (BigInteger)0,
            "lean-nep17"
        });

        UInt160 tokenHash = GetLatestCreatedToken(factory, creator.Account);
        object[] tokenInfo = factory.GetToken(tokenHash)!;
        using var token = engine.FromHash<LeanTokenTemplateContract>(tokenHash, true);

        Assert.Multiple(() =>
        {
            Assert.That(factory.GetTokenProfile(tokenHash), Is.EqualTo("lean-nep17"));
            Assert.That(ParseText(tokenInfo[0]), Is.EqualTo("LFT"));
            Assert.That(ParseHash(tokenInfo[1]), Is.EqualTo(creator.Account));
            Assert.That(ParseBigInteger(tokenInfo[2]), Is.EqualTo((BigInteger)1_000));
            Assert.That(ParseText(tokenInfo[3]), Is.EqualTo("community"));
            Assert.That(token.Symbol, Is.EqualTo("LFT"));
            Assert.That(token.getName(), Is.EqualTo("Lean Factory Token"));
            Assert.That(token.getOwner(), Is.EqualTo(creator.Account));
            Assert.That(token.getAuthorizedFactory(), Is.EqualTo(factory.Hash));
            Assert.That(token.getLeanEngine(), Is.EqualTo(leanEngine.Hash));
            Assert.That(token.getTokenId(), Is.EqualTo(tokenHash));
            Assert.That(ParseHash(tokenInfo[10]), Is.EqualTo(leanEngine.Hash));
            Assert.That(leanEngine.isTokenRegistered(tokenHash), Is.True);
            Assert.That(token.BalanceOf(creator.Account), Is.EqualTo((BigInteger)1_000));
        });
    }

    [Test]
    public void FactoryLifecycleMethods_RejectLeanProfileButFullProfileStillWorks()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creator = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);
        using var factory = DeployFactory(engine, owner.Account);
        using var leanEngine = BootstrapFullAndLeanTemplates(engine, factory);

        SimulateGasPayment(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Full Factory Token", "FFT", (BigInteger)1_000, (BigInteger)8,
            "community", "", (BigInteger)0
        });
        UInt160 fullHash = GetLatestCreatedToken(factory, creator.Account);

        SimulateGasPayment(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Factory Token", "LFT", (BigInteger)1_000, (BigInteger)8,
            "community", "", (BigInteger)0, "lean-nep17"
        });
        UInt160 leanHash = GetLatestCreatedToken(factory, creator.Account);

        FundWalletWithGas(engine, creator.Account, 500_000_000);
        engine.SetTransactionSigners(new Signer { Account = creator.Account, Scopes = WitnessScope.Global });
        factory.MintTokens(fullHash, recipient.Account, 100);

        Assert.Multiple(() =>
        {
            Assert.That(ParseBigInteger(factory.GetToken(fullHash)![2]), Is.EqualTo((BigInteger)1_100));
            Assert.That(() => factory.MintTokens(leanHash, recipient.Account, 100), Throws.Exception);
            Assert.That(() => factory.SetTokenBurnRate(leanHash, 100), Throws.Exception);
            Assert.That(() => factory.SetTokenMaxSupply(leanHash, 2_000), Throws.Exception);
            Assert.That(() => factory.UpdateTokenMetadata(leanHash, "ipfs://blocked"), Throws.Exception);
            Assert.That(() => factory.SetCreatorFee(leanHash, 100_000), Throws.Exception);
            Assert.That(() => factory.ChangeTokenMode(leanHash, "speculation", new object[] { "GAS", (BigInteger)500, "starter" }), Throws.Exception);
            Assert.That(() => factory.LockToken(leanHash), Throws.Exception);
            Assert.That(ParseBigInteger(factory.GetToken(leanHash)![2]), Is.EqualTo((BigInteger)1_000));
            Assert.That(factory.GetTokenProfile(fullHash), Is.EqualTo("full-nep17"));
            Assert.That(factory.GetTokenProfile(leanHash), Is.EqualTo("lean-nep17"));
        });
    }

    [Test]
    public void LeanSpeculationLaunch_RegistersRouterCurveWithoutFactoryTransferAuthority()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creator = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);
        using var factory = DeployFactory(engine, owner.Account);
        using var router = DeployRouter(engine, owner.Account, factory.Hash);
        using var leanEngine = BootstrapFullAndLeanTemplates(engine, factory);
        factory.SetBondingCurveRouter(router.Hash);

        SimulateGasPayment(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Speculation Token",
            "LST",
            (BigInteger)1_000,
            (BigInteger)8,
            "speculation",
            "",
            (BigInteger)0,
            "GAS",
            (BigInteger)600,
            "starter",
            "lean-nep17"
        });

        UInt160 tokenHash = GetLatestCreatedToken(factory, creator.Account);
        using var token = engine.FromHash<LeanTokenTemplateContract>(tokenHash, true);

        Assert.Multiple(() =>
        {
            Assert.That(factory.GetTokenProfile(tokenHash), Is.EqualTo("lean-nep17"));
            Assert.That(router.IsCurveRegistered(tokenHash), Is.True);
            Assert.That(ParseText(factory.GetToken(tokenHash)![3]), Is.EqualTo("speculation"));
            Assert.That(token.BalanceOf(router.Hash), Is.EqualTo((BigInteger)600));
            Assert.That(token.BalanceOf(creator.Account), Is.EqualTo((BigInteger)400));
            Assert.That(() => token.TransferByFactory(creator.Account, router.Hash, 1, null), Throws.Exception);
        });
    }

    [Test]
    public void SetAllTokensPlatformFee_UpdatesExistingLeanTokenAndFutureLeanDefault()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creator = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);
        using var factory = DeployFactory(engine, owner.Account);
        using var leanEngine = BootstrapFullAndLeanTemplates(engine, factory);

        SimulateGasPayment(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Existing Platform Fee",
            "LEP",
            (BigInteger)1_000,
            (BigInteger)8,
            "community",
            "",
            (BigInteger)0,
            "lean-nep17"
        });

        UInt160 existingHash = GetLatestCreatedToken(factory, creator.Account);
        using var existingToken = engine.FromHash<LeanTokenTemplateContract>(existingHash, true);

        engine.SetTransactionSigners(owner);
        factory.SetAllTokensPlatformFee(800_000, 0, 10);

        SimulateGasPayment(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Future Platform Fee",
            "LFP",
            (BigInteger)1_000,
            (BigInteger)8,
            "community",
            "",
            (BigInteger)0,
            "lean-nep17"
        });

        UInt160 futureHash = GetLatestCreatedToken(factory, creator.Account);
        using var futureToken = engine.FromHash<LeanTokenTemplateContract>(futureHash, true);

        Assert.Multiple(() =>
        {
            Assert.That(factory.GetPlatformFeeRate(), Is.EqualTo((BigInteger)800_000));
            Assert.That(existingToken.getPlatformFeeRate(), Is.EqualTo((BigInteger)800_000));
            Assert.That(futureToken.getPlatformFeeRate(), Is.EqualTo((BigInteger)800_000));
        });
    }

    [Test]
    public void SetAllTokensPlatformFee_UpdatesReadOnlyLeanTokenButOwnerEconomicsRemainLocked()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creator = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);
        using var factory = DeployFactory(engine, owner.Account);
        using var leanEngine = BootstrapFullAndLeanTemplates(engine, factory);

        SimulateGasPayment(engine, factory, creator, 1_500_000_000, new object[]
        {
            "Lean Read Only Platform Fee",
            "LRP",
            (BigInteger)1_000,
            (BigInteger)8,
            "community",
            "",
            (BigInteger)0,
            "lean-nep17"
        });

        UInt160 tokenHash = GetLatestCreatedToken(factory, creator.Account);
        using var token = engine.FromHash<LeanTokenTemplateContract>(tokenHash, true);

        engine.SetTransactionSigners(creator);
        token.Lock();

        engine.SetTransactionSigners(owner);
        factory.SetAllTokensPlatformFee(900_000, 0, 10);

        engine.SetTransactionSigners(creator);

        Assert.Multiple(() =>
        {
            Assert.That(token.isLocked(), Is.True);
            Assert.That(token.getPlatformFeeRate(), Is.EqualTo((BigInteger)900_000));
            Assert.That(() => token.SetBurnRate(100), Throws.Exception);
            Assert.That(() => token.SetCreatorFee(100_000), Throws.Exception);
        });
    }

    [Test]
    public void LeanEconomicsStorage_RemainsIsolatedAcrossTokensAfterPlatformBatchUpdate()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var creatorA = TestEngine.GetNewSigner();
        var creatorB = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);
        using var factory = DeployFactory(engine, owner.Account);
        using var leanEngine = BootstrapFullAndLeanTemplates(engine, factory);

        SimulateGasPayment(engine, factory, creatorA, 1_500_000_000, new object[]
        {
            "Lean Alpha Economics",
            "LAE",
            (BigInteger)1_000,
            (BigInteger)8,
            "community",
            "",
            (BigInteger)0,
            "lean-nep17"
        });
        UInt160 tokenAHash = GetLatestCreatedToken(factory, creatorA.Account);
        using var tokenA = engine.FromHash<LeanTokenTemplateContract>(tokenAHash, true);

        SimulateGasPayment(engine, factory, creatorB, 1_500_000_000, new object[]
        {
            "Lean Beta Economics",
            "LBE",
            (BigInteger)1_000,
            (BigInteger)8,
            "community",
            "",
            (BigInteger)0,
            "lean-nep17"
        });
        UInt160 tokenBHash = GetLatestCreatedToken(factory, creatorB.Account);
        using var tokenB = engine.FromHash<LeanTokenTemplateContract>(tokenBHash, true);

        engine.SetTransactionSigners(creatorA);
        tokenA.SetBurnRate(150);
        tokenA.SetCreatorFee(200_000);

        engine.SetTransactionSigners(creatorB);
        tokenB.SetBurnRate(250);
        tokenB.SetCreatorFee(300_000);

        engine.SetTransactionSigners(owner);
        factory.SetAllTokensPlatformFee(700_000, 0, 10);

        Assert.Multiple(() =>
        {
            Assert.That(tokenA.getPlatformFeeRate(), Is.EqualTo((BigInteger)700_000));
            Assert.That(tokenB.getPlatformFeeRate(), Is.EqualTo((BigInteger)700_000));
            Assert.That(tokenA.getBurnRate(), Is.EqualTo((BigInteger)150));
            Assert.That(tokenB.getBurnRate(), Is.EqualTo((BigInteger)250));
            Assert.That(tokenA.getCreatorFeeRate(), Is.EqualTo((BigInteger)200_000));
            Assert.That(tokenB.getCreatorFeeRate(), Is.EqualTo((BigInteger)300_000));
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

    private static LeanTokenEngineContract BootstrapFullAndLeanTemplates(TestEngine engine, TokenFactoryContract factory)
    {
        var fullNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var fullManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        var leanNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "LeanTokenTemplate.nef"));
        var leanManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "LeanTokenTemplate.manifest.json"));

        factory.SetNefAndManifest(fullNef, fullManifest);
        factory.SetLeanNefAndManifest(leanNef, leanManifest);
        var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(engine, factory.Hash, "LeanFactoryEngine");
        factory.SetLeanEngine(leanEngine.Hash);
        return leanEngine;
    }

    private static void SimulateGasPayment(TestEngine engine, TokenFactoryContract factory, Signer signer, BigInteger amountDatoshi, object[] tokenData)
    {
        try
        {
            int callingScriptHashCalls = 0;
            engine.OnGetCallingScriptHash = (_, currentHash) =>
                callingScriptHashCalls++ == 0 ? engine.Native.GAS.Hash : currentHash;
            engine.SetTransactionSigners(new Signer { Account = signer.Account, Scopes = WitnessScope.Global });
            factory.OnNEP17Payment(signer.Account, amountDatoshi, tokenData);
        }
        finally
        {
            engine.OnGetCallingScriptHash = null;
        }
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

    private static UInt160 ParseHash(object? item) => item switch
    {
        UInt160 hash => hash,
        byte[] bytes when bytes.Length == 20 => new UInt160(bytes),
        Neo.VM.Types.ByteString byteString => new UInt160(byteString.GetSpan()),
        Neo.VM.Types.PrimitiveType primitive => new UInt160(primitive.GetSpan()),
        _ => UInt160.Zero,
    };

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
        string value => value,
        byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
        Neo.VM.Types.ByteString byteString => byteString.GetString() ?? "",
        Neo.VM.Types.PrimitiveType primitive => primitive.GetString() ?? "",
        _ => item?.ToString() ?? "",
    };
}
