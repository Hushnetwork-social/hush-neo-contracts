#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using System.Numerics;

namespace TokenTemplate.Tests;

[TestFixture]
public class LeanTokenTemplateLifecycleTests
{
    [Test]
    public void Transfer_WithOwnerWitness_UpdatesBalances()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Transfer",
            Symbol = "LTX",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            ManifestName = "LeanTransfer"
        });

        var result = token.Transfer(owner.Account, recipient.Account, 250, null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(token.BalanceOf(owner.Account), Is.EqualTo((BigInteger)750));
            Assert.That(token.BalanceOf(recipient.Account), Is.EqualTo((BigInteger)250));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)1_000));
        });
    }

    [Test]
    public void OwnerLifecycle_WithOwnerWitness_UpdatesLeanLocalStateAndSupply()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Lifecycle",
            Symbol = "LLC",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            MaxSupply = 2_000,
            Upgradeable = true,
            ManifestName = "LeanLifecycle"
        });

        token.SetMetadataUri("ipfs://updated");
        token.SetBurnRate(250);
        token.SetCreatorFee(300_000);
        token.SetMaxSupply(2_500);
        token.mint(recipient.Account, 500);
        token.setPausable(true);
        token.pause();
        token.unpause();

        Assert.Multiple(() =>
        {
            Assert.That(token.getMetadataUri(), Is.EqualTo("ipfs://updated"));
            Assert.That(token.getBurnRate(), Is.EqualTo((BigInteger)250));
            Assert.That(token.getCreatorFeeRate(), Is.EqualTo((BigInteger)300_000));
            Assert.That(token.getPlatformFeeRate(), Is.EqualTo(BigInteger.Zero));
            Assert.That(token.getMaxSupply(), Is.EqualTo((BigInteger)2_500));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)1_500));
            Assert.That(token.BalanceOf(recipient.Account), Is.EqualTo((BigInteger)500));
            Assert.That(token.isPausable(), Is.True);
            Assert.That(token.isPaused(), Is.False);
        });
    }

    [Test]
    public void TokenOwner_CannotSetPlatformFeeRateDirectly()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        var outsider = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Platform Owner Block",
            Symbol = "LPB",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            PlatformFeeRate = 250_000,
            ManifestName = "LeanPlatformOwnerBlock"
        });

        Assert.Multiple(() =>
        {
            Assert.That(() => token.SetPlatformFeeRate(700_000), Throws.Exception);
            Assert.That(token.getPlatformFeeRate(), Is.EqualTo((BigInteger)250_000));
        });

        engine.SetTransactionSigners(outsider);

        Assert.Multiple(() =>
        {
            Assert.That(() => token.SetPlatformFeeRate(700_000), Throws.Exception);
            Assert.That(token.getPlatformFeeRate(), Is.EqualTo((BigInteger)250_000));
        });
    }

    [Test]
    public void Transfer_WithBurnRate_ReducesRecipientAmountAndTotalSupply()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Burn Transfer",
            Symbol = "LBT",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            ManifestName = "LeanBurnTransfer"
        });

        token.SetBurnRate(1_000);
        var result = token.Transfer(owner.Account, recipient.Account, 100, null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(token.BalanceOf(owner.Account), Is.EqualTo((BigInteger)900));
            Assert.That(token.BalanceOf(recipient.Account), Is.EqualTo((BigInteger)90));
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)990));
        });
    }

    [Test]
    public void OwnerLifecycle_WithNonOwnerWitness_IsRejected()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var nonOwner = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Non Owner",
            Symbol = "LNO",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            MaxSupply = 2_000,
            Upgradeable = true,
            ManifestName = "LeanNonOwner"
        });

        engine.SetTransactionSigners(nonOwner);

        Assert.Multiple(() =>
        {
            Assert.That(() => token.mint(nonOwner.Account, 10), Throws.Exception);
            Assert.That(() => token.SetMetadataUri("ipfs://bad"), Throws.Exception);
            Assert.That(() => token.SetMaxSupply(3_000), Throws.Exception);
            Assert.That(() => token.SetBurnRate(100), Throws.Exception);
            Assert.That(() => token.SetCreatorFee(100_000), Throws.Exception);
            Assert.That(() => token.SetPlatformFeeRate(100_000), Throws.Exception);
            Assert.That(() => token.setPausable(true), Throws.Exception);
            Assert.That(() => token.Lock(), Throws.Exception);
            Assert.That(() => token.setOwner(nonOwner.Account), Throws.Exception);
        });

        Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)1_000));
    }

    [Test]
    public void Lock_BlocksConfigurationAndMintButAllowsPauseParity()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Lock",
            Symbol = "LLK",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            MaxSupply = 2_000,
            Pausable = true,
            Upgradeable = true,
            ManifestName = "LeanLock"
        });

        token.Lock();
        token.pause();
        token.unpause();

        Assert.Multiple(() =>
        {
            Assert.That(token.isLocked(), Is.True);
            Assert.That(token.isPaused(), Is.False);
            Assert.That(() => token.mint(recipient.Account, 1), Throws.Exception);
            Assert.That(() => token.SetMetadataUri("ipfs://locked"), Throws.Exception);
            Assert.That(() => token.SetMaxSupply(3_000), Throws.Exception);
            Assert.That(() => token.SetBurnRate(100), Throws.Exception);
            Assert.That(() => token.setPausable(false), Throws.Exception);
            Assert.That(() => token.update(System.Array.Empty<byte>(), "{}", null), Throws.Exception);
        });
    }

    [Test]
    public void Renounce_BlocksOwnerControlButNormalTransfersContinue()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Renounce",
            Symbol = "LRN",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            MaxSupply = 2_000,
            Pausable = true,
            Upgradeable = true,
            ManifestName = "LeanRenounce"
        });

        token.setOwner(UInt160.Zero);
        var transferred = token.Transfer(owner.Account, recipient.Account, 100, null);

        Assert.Multiple(() =>
        {
            Assert.That(transferred, Is.True);
            Assert.That(token.getOwner(), Is.EqualTo(UInt160.Zero));
            Assert.That(token.BalanceOf(recipient.Account), Is.EqualTo((BigInteger)100));
            Assert.That(() => token.mint(recipient.Account, 1), Throws.Exception);
            Assert.That(() => token.SetMetadataUri("ipfs://renounced"), Throws.Exception);
            Assert.That(() => token.SetMaxSupply(3_000), Throws.Exception);
            Assert.That(() => token.SetBurnRate(100), Throws.Exception);
            Assert.That(() => token.pause(), Throws.Exception);
            Assert.That(() => token.Lock(), Throws.Exception);
            Assert.That(() => token.update(System.Array.Empty<byte>(), "{}", null), Throws.Exception);
        });
    }

    [Test]
    public void FactoryCompatibilityMethods_AreExposedAndAuthorizeConfiguredFactory()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Factory Reject",
            Symbol = "LFR",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            ManifestName = "LeanFactoryReject"
        });

        engine.SetTransactionSigners(new Signer { Account = factory.Account, Scopes = WitnessScope.Global });

        token.MintByFactory(recipient.Account, 1);
        token.TransferByFactory(owner.Account, recipient.Account, 1, null);
        token.AuthorizeFactory(recipient.Account);

        Assert.Multiple(() =>
        {
            Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)1_001));
            Assert.That(token.BalanceOf(owner.Account), Is.EqualTo((BigInteger)999));
            Assert.That(token.BalanceOf(recipient.Account), Is.EqualTo((BigInteger)2));
            Assert.That(token.getAuthorizedFactory(), Is.EqualTo(recipient.Account));
        });
    }
}
