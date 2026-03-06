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
public class TokenFactoryGovernanceTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void UpgradeTemplate_AfterBootstrap_IncrementsVersionAndChangesTemplateHash()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);
        using var factory = DeployFactory(engine, ownerSigner.Account);

        var nefBytes = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        factory.SetNefAndManifest(nefBytes, manifest);

        var before = factory.GetConfig();
        var upgradedManifest = manifest.Replace("\"name\":\"TokenTemplate\"", "\"name\":\"TokenTemplateV2\"");

        factory.UpgradeTemplate(nefBytes, upgradedManifest);

        var after = factory.GetConfig();
        Assert.That(ParseBigInteger(before![5]), Is.EqualTo(BigInteger.One));
        Assert.That(ParseBigInteger(after![5]), Is.EqualTo((BigInteger)2));
        Assert.That(ParseHash(after[4]), Is.Not.EqualTo(ParseHash(before[4])));
    }

    [Test]
    public void ClaimAndUpgrade_RemainAllowedWhilePaused_ButLifecycleMutationsAbort()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);
        using var factory = DeployFactory(engine, ownerSigner.Account);

        var nefBytes = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        factory.SetNefAndManifest(nefBytes, manifest);

        var creatorSigner = ownerSigner;
        SimulateGasPayment(engine, factory, creatorSigner, 1_500_000_000, new object[]
        {
            "My Token", "MYTOK", (BigInteger)1_000_000, (BigInteger)8,
            "community", "https://cdn.hushnetwork.social/mytok.png", (BigInteger)0
        });
        var tokenHash = GetLatestCreatedToken(factory, creatorSigner.Account);

        factory.SetPaused(true);

        Assert.That(
            () => factory.MintTokens(tokenHash, TestEngine.GetNewSigner().Account, 100_000),
            Throws.Exception);

        TransferGasToFactory(engine, factory.Hash, 300_000_000);

        var ownerBalanceBefore = engine.Native.GAS.BalanceOf(ownerSigner.Account) ?? BigInteger.Zero;
        engine.SetTransactionSigners(ownerSigner);
        factory.Claim(engine.Native.GAS.Hash, 100_000_000);
        factory.UpgradeTemplate(nefBytes, manifest.Replace("\"name\":\"TokenTemplate\"", "\"name\":\"TokenTemplateV3\""));
        var ownerBalanceAfter = engine.Native.GAS.BalanceOf(ownerSigner.Account) ?? BigInteger.Zero;

        Assert.That(ownerBalanceAfter - ownerBalanceBefore, Is.EqualTo((BigInteger)100_000_000));
        Assert.That(ParseBigInteger(factory.GetConfig()![5]), Is.EqualTo((BigInteger)2));
    }

    [Test]
    public void SetOperationFee_AffectsNextLifecycleOperation()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);
        using var factory = DeployFactory(engine, ownerSigner.Account);

        var nefBytes = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        factory.SetNefAndManifest(nefBytes, manifest);

        SimulateGasPayment(engine, factory, ownerSigner, 1_500_000_000, new object[]
        {
            "My Token", "MYTOK", (BigInteger)1_000_000, (BigInteger)8,
            "community", "", (BigInteger)0
        });
        var tokenHash = GetLatestCreatedToken(factory, ownerSigner.Account);

        factory.SetOperationFee(75_000_000);
        FundWalletWithGas(engine, ownerSigner.Account, 500_000_000);

        var factoryBalanceBefore = engine.Native.GAS.BalanceOf(factory.Hash) ?? BigInteger.Zero;
        engine.SetTransactionSigners(new Signer { Account = ownerSigner.Account, Scopes = WitnessScope.Global });
        factory.MintTokens(tokenHash, TestEngine.GetNewSigner().Account, 100_000);
        var factoryBalanceAfter = engine.Native.GAS.BalanceOf(factory.Hash) ?? BigInteger.Zero;

        Assert.That(factoryBalanceAfter - factoryBalanceBefore, Is.EqualTo((BigInteger)75_000_000));
    }

    [Test]
    public void GovernanceMutations_RejectNonOwnerCallers()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var otherSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);
        using var factory = DeployFactory(engine, ownerSigner.Account);

        var nefBytes = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        factory.SetNefAndManifest(nefBytes, manifest);

        TransferGasToFactory(engine, factory.Hash, 200_000_000);

        engine.SetTransactionSigners(otherSigner);

        Assert.Multiple(() =>
        {
            Assert.That(() => factory.SetCreationFee(2_000_000_000), Throws.Exception);
            Assert.That(() => factory.SetOperationFee(75_000_000), Throws.Exception);
            Assert.That(() => factory.SetPaused(true), Throws.Exception);
            Assert.That(() => factory.UpgradeTemplate(nefBytes, manifest.Replace("\"name\":\"TokenTemplate\"", "\"name\":\"TokenTemplateV4\"")), Throws.Exception);
            Assert.That(() => factory.Claim(engine.Native.GAS.Hash, 100_000_000), Throws.Exception);
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

    private static void SimulateGasPayment(TestEngine engine, TokenFactoryContract factory, Signer signer, BigInteger amountDatoshi, object[] tokenData)
    {
        try
        {
            engine.OnGetCallingScriptHash = (_, _) => engine.Native.GAS.Hash;
            engine.SetTransactionSigners(signer);
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

    private static void TransferGasToFactory(TestEngine engine, UInt160 factoryHash, BigInteger datoshi)
    {
        foreach (var funder in new[] { engine.CommitteeAddress, engine.ValidatorsAddress })
        {
            var funderBalance = engine.Native.GAS.BalanceOf(funder) ?? BigInteger.Zero;
            if (funderBalance < datoshi) continue;

            engine.SetTransactionSigners(new Signer { Account = funder, Scopes = WitnessScope.Global });
            if (engine.Native.GAS.Transfer(funder, factoryHash, datoshi, null) == true)
                return;
        }

        Assert.Fail($"TransferGasToFactory({datoshi}) failed.");
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
}
