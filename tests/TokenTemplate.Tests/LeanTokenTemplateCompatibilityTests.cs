#nullable enable
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using TokenTemplate.Tests.Support;

namespace TokenTemplate.Tests;

[TestFixture]
public class LeanTokenTemplateCompatibilityTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void LeanManifest_ExposesWalletLifecycleAndCompatibilitySurface()
    {
        var lean = LoadManifest("LeanTokenTemplate.manifest.json");
        var full = LoadManifest("TokenTemplate.manifest.json");

        var leanMethods = lean.Abi.Methods.Select(method => method.Name).ToHashSet();
        var fullMethods = full.Abi.Methods.Select(method => method.Name).ToHashSet();
        var leanEvents = lean.Abi.Events.Select(contractEvent => contractEvent.Name).ToHashSet();

        string[] requiredMethods =
        {
            "symbol", "decimals", "totalSupply", "balanceOf", "transfer",
            "getOwner", "getName", "getMintable", "getMaxSupply", "isUpgradeable",
            "isLocked", "isPausable", "isPaused", "getMetadataUri",
            "getAuthorizedFactory", "getPlatformFeeRate", "getCreatorFeeRate",
            "getBurnRate", "getClaimableCreatorFee", "getCreatorClaimant",
            "quoteTransfer", "verify", "setOwner", "lock", "setMetadataUri",
            "setMaxSupply", "setBurnRate", "setCreatorFee", "setPlatformFeeRate",
            "setPausable", "pause", "unpause", "burn", "claimCreatorFees",
            "claimCreatorFee", "mint", "mintByFactory", "transferByFactory",
            "authorizeFactory", "update", "onNEP17Payment"
        };

        Assert.Multiple(() =>
        {
            Assert.That(lean.SupportedStandards, Does.Contain("NEP-17"));
            foreach (string method in requiredMethods)
            {
                Assert.That(leanMethods, Does.Contain(method),
                    $"Lean manifest is missing required method '{method}'.");
            }

            foreach (string method in fullMethods.Where(method => !method.StartsWith("_", StringComparison.Ordinal)))
            {
                Assert.That(leanMethods, Does.Contain(method),
                    $"Lean manifest should expose full-template compatibility method '{method}'.");
            }

            foreach (string eventName in new[]
            {
                "Transfer", "OwnerChanged", "Locked", "BurnRateSet", "MetadataUriSet",
                "MaxSupplySet", "CreatorFeeRateSet", "PlatformFeeRateSet", "CreatorFeesClaimed"
            })
            {
                Assert.That(leanEvents, Does.Contain(eventName),
                    $"Lean manifest is missing required event '{eventName}'.");
            }
        });
    }

    [Test]
    public void TokenFactoryManifest_ExposesLeanProfileConfigurationSurface()
    {
        var factory = LoadManifest("TokenFactory.manifest.json");
        var methods = factory.Abi.Methods.Select(method => method.Name).ToHashSet();
        var events = factory.Abi.Events.Select(contractEvent => contractEvent.Name).ToHashSet();

        Assert.Multiple(() =>
        {
            foreach (string method in new[]
            {
                "getTokenProfile", "isLeanInitialized", "getLeanTemplateConfig",
                "setLeanNefAndManifest", "upgradeLeanTemplate"
            })
            {
                Assert.That(methods, Does.Contain(method),
                    $"TokenFactory manifest is missing lean-profile method '{method}'.");
            }

            Assert.That(events, Does.Contain("LeanTemplateUpgraded"));
            Assert.That(events, Does.Contain("TokenProfileRecorded"));
        });
    }

    [Test]
    public void LeanDeployGas_IsMeasuredBelowFullTemplateDeployGas()
    {
        var engine = new TestEngine(true);
        var owner = TestEngine.GetNewSigner();
        var factory = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(owner);

        long fullGas;
        using (var fullWatcher = engine.CreateGasWatcher())
        using (DeployFullTemplate(engine, new DeployParams
        {
            Name = "Full Gas Baseline",
            Symbol = "FGB",
            Owner = owner.Account,
            InitialSupply = 1_000,
            Mintable = BigInteger.One,
            AuthorizedFactory = factory.Account
        }))
        {
            fullGas = fullWatcher.Value;
        }

        engine.SetTransactionSigners(owner);
        using var leanEngine = LeanTokenTemplateTestSupport.DeployEngine(
            engine,
            factory.Account,
            "LeanGasEngine");

        engine.SetTransactionSigners(owner);
        long leanGas;
        using (var leanWatcher = engine.CreateGasWatcher())
        using (LeanTokenTemplateTestSupport.Deploy(engine, new LeanDeployParams
        {
            Name = "Lean Gas Baseline",
            Symbol = "LGB",
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = 1_000,
            EngineHash = leanEngine.Hash,
            ManifestName = "LeanGasBaseline"
        }))
        {
            leanGas = leanWatcher.Value;
        }

        Assert.Multiple(() =>
        {
            Assert.That(fullGas, Is.GreaterThan(0));
            Assert.That(leanGas, Is.GreaterThan(0));
            Assert.That(leanGas, Is.LessThan(fullGas),
                $"Expected lean deploy gas ({leanGas}) to stay below full deploy gas ({fullGas}).");
        });

        BigInteger savings = (BigInteger)fullGas - leanGas;
        decimal savingsPercent = Math.Round((decimal)savings / fullGas * 100, 2);
        NUnit.Framework.TestContext.Out.WriteLine($"FullTemplateDeployGasDatoshi={fullGas}");
        NUnit.Framework.TestContext.Out.WriteLine($"LeanTokenTemplateDeployGasDatoshi={leanGas}");
        NUnit.Framework.TestContext.Out.WriteLine($"DeploymentGasSavingsDatoshi={savings}");
        NUnit.Framework.TestContext.Out.WriteLine($"DeploymentGasSavingsPercent={savingsPercent}");
    }

    private static ContractManifest LoadManifest(string fileName) =>
        ContractManifest.Parse(File.ReadAllText(Path.Combine(ArtifactsPath, fileName)));

    private static TokenTemplateContract DeployFullTemplate(TestEngine engine, DeployParams deployParams)
    {
        var nef = NefFile.Parse(File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef")));
        var manifest = ContractManifest.Parse(File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json")));
        return engine.Deploy<TokenTemplateContract>(nef, manifest, deployParams.ToDeployArray());
    }
}
