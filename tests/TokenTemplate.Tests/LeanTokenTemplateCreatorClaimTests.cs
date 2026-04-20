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
public class LeanTokenTemplateCreatorClaimTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void ClaimCreatorFee_WithRealFactory_CollectsOperationFeeAndKeepsLocalBalance()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipientA = TestEngine.GetNewSigner();
        var recipientB = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var factory = DeployFactory(engine, owner.Account);
        using var token = DeployLeanToken(engine, owner.Account, factory.Hash);

        FundWalletWithGas(engine, owner.Account, 1_000_000_000);
        engine.SetTransactionSigners(new Signer { Account = owner.Account, Scopes = WitnessScope.Global });
        token.Transfer(owner.Account, recipientA.Account, 10_000, null);
        token.Transfer(owner.Account, recipientB.Account, 10_000, null);

        Assert.That(token.getClaimableCreatorFee(), Is.EqualTo((BigInteger)1_000_000));

        var factoryGasBeforePartial = GasBalanceOf(engine, factory.Hash);
        token.claimCreatorFee(250_000);

        Assert.Multiple(() =>
        {
            Assert.That(GasBalanceOf(engine, factory.Hash) - factoryGasBeforePartial, Is.EqualTo((BigInteger)50_000_000));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo((BigInteger)750_000));
        });

        var factoryGasBeforeAll = GasBalanceOf(engine, factory.Hash);
        token.claimCreatorFees();

        Assert.Multiple(() =>
        {
            Assert.That(GasBalanceOf(engine, factory.Hash) - factoryGasBeforeAll, Is.EqualTo((BigInteger)50_000_000));
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo(BigInteger.Zero));
        });
    }

    [Test]
    public void ClaimCreatorFee_WithNonClaimant_IsRejectedAndBalanceRemainsLocal()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var outsider = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        using var token = DeployLeanToken(engine, owner.Account, factory.Account);

        FundWalletWithGas(engine, owner.Account, 500_000_000);
        engine.SetTransactionSigners(new Signer { Account = owner.Account, Scopes = WitnessScope.Global });
        token.Transfer(owner.Account, recipient.Account, 10_000, null);

        FundWalletWithGas(engine, outsider.Account, 500_000_000);
        engine.SetTransactionSigners(new Signer { Account = outsider.Account, Scopes = WitnessScope.Global });

        Assert.Multiple(() =>
        {
            Assert.That(() => token.claimCreatorFee(100_000), Throws.Exception);
            Assert.That(token.getClaimableCreatorFee(), Is.EqualTo((BigInteger)500_000));
        });
    }

    [Test]
    public void CreatorFeeClaimableBalance_RemainsIsolatedAcrossLeanTokens()
    {
        var engine = new TestEngine(true);
        var ownerA = TestEngine.GetNewSigner();
        var ownerB = TestEngine.GetNewSigner();
        var recipient = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();

        engine.SetTransactionSigners(ownerA);
        using var tokenA = DeployLeanToken(engine, ownerA.Account, factory.Account, "LeanClaimableA");

        engine.SetTransactionSigners(ownerB);
        using var tokenB = DeployLeanToken(engine, ownerB.Account, factory.Account, "LeanClaimableB");

        FundWalletWithGas(engine, ownerA.Account, 500_000_000);
        engine.SetTransactionSigners(new Signer { Account = ownerA.Account, Scopes = WitnessScope.Global });
        tokenA.Transfer(ownerA.Account, recipient.Account, 10_000, null);

        Assert.Multiple(() =>
        {
            Assert.That(tokenA.getClaimableCreatorFee(), Is.EqualTo((BigInteger)500_000));
            Assert.That(tokenB.getClaimableCreatorFee(), Is.EqualTo(BigInteger.Zero));
        });
    }

    private static LeanTokenTemplateContract DeployLeanToken(
        TestEngine engine,
        UInt160 ownerAddress,
        UInt160 factoryAddress,
        string manifestName = "LeanCreatorClaim")
    {
        return LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Creator Claim",
            Symbol = "LCC",
            Owner = ownerAddress,
            LaunchFactory = factoryAddress,
            InitialSupply = 100_000,
            PlatformFeeRate = 1_000_000,
            CreatorFeeRate = 500_000,
            ManifestName = manifestName
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
}
