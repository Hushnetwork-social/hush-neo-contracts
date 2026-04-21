#nullable enable
using Neo;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using System.Numerics;

namespace TokenTemplate.Tests;

[TestFixture]
public class LeanTokenTemplateStorageTests
{
    [Test]
    public void Deploy_InitializesFacadeAndSharedEngineStorageFromDeployParameters()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var factorySigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(
            engine,
            factorySigner.Account,
            "LeanStorageEngine");

        engine.SetTransactionSigners(ownerSigner);
        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Storage Token",
            Symbol = "LST",
            Owner = ownerSigner.Account,
            LaunchFactory = factorySigner.Account,
            InitialSupply = 123_456,
            MaxSupply = 500_000,
            MetadataUri = "ipfs://lean-storage",
            Pausable = true,
            Upgradeable = true,
            PlatformFeeRate = 1_000_000,
            CreatorFeeRate = 500_000,
            EngineHash = leanEngine.Hash
        });

        Assert.Multiple(() =>
        {
            Assert.That(token.getLeanEngine(), Is.EqualTo(leanEngine.Hash));
            Assert.That(token.getTokenId(), Is.EqualTo(token.Hash));
            Assert.That(leanEngine.isTokenRegistered(token.Hash), Is.True);
            Assert.That(leanEngine.getTokenIdByFacade(token.Hash), Is.EqualTo(token.Hash));
            Assert.That(leanEngine.getFacade(token.Hash), Is.EqualTo(token.Hash));
            Assert.That(token.Symbol, Is.EqualTo("LST"));
            Assert.That(token.Decimals, Is.EqualTo((byte)8));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)123_456));
            Assert.That(token.BalanceOf(ownerSigner.Account), Is.EqualTo((BigInteger)123_456));
            Assert.That(leanEngine.TotalSupply(token.Hash), Is.EqualTo((BigInteger)123_456));
            Assert.That(leanEngine.BalanceOf(token.Hash, ownerSigner.Account), Is.EqualTo((BigInteger)123_456));
            Assert.That(leanEngine.getTokenOwner(token.Hash), Is.EqualTo(ownerSigner.Account));
            Assert.That(leanEngine.getName(token.Hash), Is.EqualTo("Lean Storage Token"));
            Assert.That(leanEngine.getSymbol(token.Hash), Is.EqualTo("LST"));
            Assert.That(leanEngine.getDecimals(token.Hash), Is.EqualTo((BigInteger)8));
            Assert.That(token.getOwner(), Is.EqualTo(ownerSigner.Account));
            Assert.That(token.getName(), Is.EqualTo("Lean Storage Token"));
            Assert.That(token.getMintable(), Is.True);
            Assert.That(token.getMaxSupply(), Is.EqualTo((BigInteger)500_000));
            Assert.That(token.isUpgradeable(), Is.True);
            Assert.That(token.isLocked(), Is.False);
            Assert.That(token.isPausable(), Is.True);
            Assert.That(token.isPaused(), Is.False);
            Assert.That(token.getMetadataUri(), Is.EqualTo("ipfs://lean-storage"));
            Assert.That(token.getAuthorizedFactory(), Is.EqualTo(factorySigner.Account));
            Assert.That(token.getPlatformFeeRate(), Is.EqualTo((BigInteger)1_000_000));
            Assert.That(token.getCreatorFeeRate(), Is.EqualTo((BigInteger)500_000));
            Assert.That(token.getBurnRate(), Is.EqualTo(BigInteger.Zero));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo(BigInteger.Zero));
            Assert.That(token.getCreatorClaimant(), Is.EqualTo(ownerSigner.Account));
            Assert.That(token.verify(), Is.True);
        });
    }

    [Test]
    public void Deploy_TwoLeanTokensKeepOwnerMetadataAndSupplyIsolated()
    {
        var engine = new TestEngine(true);
        var ownerA = TestEngine.GetNewSigner();
        var ownerB = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();

        engine.SetTransactionSigners(ownerA);
        using var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(
            engine,
            factory.Account,
            "LeanSharedIsolationEngine");

        engine.SetTransactionSigners(ownerA);
        using var tokenA = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Alpha",
            Symbol = "LTA",
            Owner = ownerA.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 100,
            MetadataUri = "ipfs://alpha",
            EngineHash = leanEngine.Hash,
            ManifestName = "LeanTokenAlpha"
        });

        engine.SetTransactionSigners(ownerB);
        using var tokenB = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Beta",
            Symbol = "LTB",
            Owner = ownerB.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 200,
            MetadataUri = "ipfs://beta",
            EngineHash = leanEngine.Hash,
            ManifestName = "LeanTokenBeta"
        });

        engine.SetTransactionSigners(ownerA);
        tokenA.SetMetadataUri("ipfs://alpha-updated");
        tokenA.mint(ownerA.Account, 25);

        Assert.Multiple(() =>
        {
            Assert.That(tokenA.getLeanEngine(), Is.EqualTo(leanEngine.Hash));
            Assert.That(tokenB.getLeanEngine(), Is.EqualTo(leanEngine.Hash));
            Assert.That(tokenA.getOwner(), Is.EqualTo(ownerA.Account));
            Assert.That(tokenB.getOwner(), Is.EqualTo(ownerB.Account));
            Assert.That(tokenA.getMetadataUri(), Is.EqualTo("ipfs://alpha-updated"));
            Assert.That(tokenB.getMetadataUri(), Is.EqualTo("ipfs://beta"));
            Assert.That(tokenA.TotalSupply, Is.EqualTo((BigInteger)125));
            Assert.That(tokenB.TotalSupply, Is.EqualTo((BigInteger)200));
            Assert.That(tokenA.BalanceOf(ownerA.Account), Is.EqualTo((BigInteger)125));
            Assert.That(tokenB.BalanceOf(ownerB.Account), Is.EqualTo((BigInteger)200));
            Assert.That(leanEngine.TotalSupply(tokenA.Hash), Is.EqualTo((BigInteger)125));
            Assert.That(leanEngine.TotalSupply(tokenB.Hash), Is.EqualTo((BigInteger)200));
            Assert.That(leanEngine.getMetadataUri(tokenA.Hash), Is.EqualTo("ipfs://alpha-updated"));
            Assert.That(leanEngine.getMetadataUri(tokenB.Hash), Is.EqualTo("ipfs://beta"));
        });
    }

    [Test]
    public void SetMetadataUri_WithLaunchFactorySigner_DoesNotMutateSharedEngineStorage()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var factorySigner = TestEngine.GetNewSigner();

        engine.SetTransactionSigners(ownerSigner);
        using var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(
            engine,
            factorySigner.Account,
            "LeanOwnerIsolationEngine");

        engine.SetTransactionSigners(ownerSigner);
        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Local Owner",
            Symbol = "LLO",
            Owner = ownerSigner.Account,
            LaunchFactory = factorySigner.Account,
            MetadataUri = "ipfs://owner-only",
            EngineHash = leanEngine.Hash
        });

        engine.SetTransactionSigners(factorySigner);

        Assert.That(() => token.SetMetadataUri("ipfs://factory-write"), Throws.Exception);
        Assert.That(token.getMetadataUri(), Is.EqualTo("ipfs://owner-only"));
        Assert.That(leanEngine.getMetadataUri(token.Hash), Is.EqualTo("ipfs://owner-only"));
    }

    [Test]
    public void TokenOwner_CannotMutateAnotherTokenThroughSharedEngineFacade()
    {
        var engine = new TestEngine(true);
        var ownerA = TestEngine.GetNewSigner();
        var ownerB = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();

        engine.SetTransactionSigners(ownerA);
        using var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(
            engine,
            factory.Account,
            "LeanCrossOwnerEngine");

        using var tokenA = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Owner A",
            Symbol = "LOA",
            Owner = ownerA.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 100,
            EngineHash = leanEngine.Hash,
            ManifestName = "LeanCrossOwnerA"
        });

        engine.SetTransactionSigners(ownerB);
        using var tokenB = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Owner B",
            Symbol = "LOB",
            Owner = ownerB.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 200,
            MetadataUri = "ipfs://beta",
            EngineHash = leanEngine.Hash,
            ManifestName = "LeanCrossOwnerB"
        });

        engine.SetTransactionSigners(ownerA);
        tokenA.Lock();

        Assert.Multiple(() =>
        {
            Assert.That(() => tokenB.mint(ownerA.Account, 50), Throws.Exception);
            Assert.That(() => tokenB.SetMetadataUri("ipfs://owner-a-overwrite"), Throws.Exception);
            Assert.That(() => tokenB.SetBurnRate(100), Throws.Exception);
            Assert.That(() => tokenB.SetCreatorFee(100_000), Throws.Exception);
            Assert.That(() => tokenB.SetMaxSupply(1_000), Throws.Exception);
            Assert.That(() => tokenB.Lock(), Throws.Exception);
            Assert.That(() => tokenB.claimCreatorFee(1), Throws.Exception);
            Assert.That(tokenB.TotalSupply, Is.EqualTo((BigInteger)200));
            Assert.That(tokenB.getMetadataUri(), Is.EqualTo("ipfs://beta"));
            Assert.That(tokenB.getBurnRate(), Is.EqualTo(BigInteger.Zero));
            Assert.That(tokenB.isLocked(), Is.False);
            Assert.That(tokenA.isLocked(), Is.True);
            Assert.That(tokenA.TotalSupply, Is.EqualTo((BigInteger)100));
        });
    }

    [Test]
    public void SharedEngine_UnknownAndMalformedTokenIdsAreRejected()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var unknownToken = TestEngine.GetNewSigner().Account;
        engine.SetTransactionSigners(ownerSigner);

        using var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(
            engine,
            ownerSigner.Account,
            "LeanMalformedTokenEngine");

        Assert.Multiple(() =>
        {
            Assert.That(() => leanEngine.getName(UInt160.Zero), Throws.Exception);
            Assert.That(() => leanEngine.TotalSupply(UInt160.Zero), Throws.Exception);
            Assert.That(() => leanEngine.getName(unknownToken), Throws.Exception);
            Assert.That(() => leanEngine.TotalSupply(unknownToken), Throws.Exception);
        });
    }
}
