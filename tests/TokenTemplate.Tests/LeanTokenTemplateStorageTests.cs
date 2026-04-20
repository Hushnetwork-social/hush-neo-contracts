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
    public void Deploy_InitializesLeanLocalStorageFromDeployParameters()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var factorySigner = TestEngine.GetNewSigner();
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
            CreatorFeeRate = 500_000
        });

        Assert.Multiple(() =>
        {
            Assert.That(token.Symbol, Is.EqualTo("LST"));
            Assert.That(token.Decimals, Is.EqualTo((byte)8));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)123_456));
            Assert.That(token.BalanceOf(ownerSigner.Account), Is.EqualTo((BigInteger)123_456));
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
        using var tokenA = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Alpha",
            Symbol = "LTA",
            Owner = ownerA.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 100,
            MetadataUri = "ipfs://alpha",
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
            ManifestName = "LeanTokenBeta"
        });

        engine.SetTransactionSigners(ownerA);
        tokenA.SetMetadataUri("ipfs://alpha-updated");

        Assert.Multiple(() =>
        {
            Assert.That(tokenA.getOwner(), Is.EqualTo(ownerA.Account));
            Assert.That(tokenB.getOwner(), Is.EqualTo(ownerB.Account));
            Assert.That(tokenA.getMetadataUri(), Is.EqualTo("ipfs://alpha-updated"));
            Assert.That(tokenB.getMetadataUri(), Is.EqualTo("ipfs://beta"));
            Assert.That(tokenA.TotalSupply, Is.EqualTo((BigInteger)100));
            Assert.That(tokenB.TotalSupply, Is.EqualTo((BigInteger)200));
            Assert.That(tokenA.BalanceOf(ownerA.Account), Is.EqualTo((BigInteger)100));
            Assert.That(tokenB.BalanceOf(ownerB.Account), Is.EqualTo((BigInteger)200));
        });
    }

    [Test]
    public void SetMetadataUri_WithLaunchFactorySigner_DoesNotMutateLeanLocalStorage()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var factorySigner = TestEngine.GetNewSigner();

        engine.SetTransactionSigners(ownerSigner);
        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Local Owner",
            Symbol = "LLO",
            Owner = ownerSigner.Account,
            LaunchFactory = factorySigner.Account,
            MetadataUri = "ipfs://owner-only"
        });

        engine.SetTransactionSigners(factorySigner);

        Assert.That(() => token.SetMetadataUri("ipfs://factory-write"), Throws.Exception);
        Assert.That(token.getMetadataUri(), Is.EqualTo("ipfs://owner-only"));
    }

}
