#nullable enable
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using System;
using System.IO;
using System.Numerics;

namespace TokenTemplate.Tests;

internal static class LeanTokenTemplateTestSupport
{
    public static LeanTokenEngineContract DeployEngine(
        TestEngine engine,
        UInt160 owner,
        string manifestName = "LeanTokenEngine")
    {
        string artifactsPath = Path.Combine(AppContext.BaseDirectory, "artifacts");
        var nefPath = Path.Combine(artifactsPath, "LeanTokenEngine.nef");
        var manifestPath = Path.Combine(artifactsPath, "LeanTokenEngine.manifest.json");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        string manifestJson = File.ReadAllText(manifestPath);
        if (manifestName != "LeanTokenEngine")
        {
            manifestJson = manifestJson.Replace(
                "\"name\":\"LeanTokenEngine\"",
                "\"name\":\"" + manifestName + "\"",
                StringComparison.Ordinal);
        }

        var manifest = ContractManifest.Parse(manifestJson);
        return engine.Deploy<LeanTokenEngineContract>(nef, manifest, owner);
    }

    public static LeanTokenTemplateContract Deploy(TestEngine engine, LeanDeployParams parameters)
    {
        string artifactsPath = Path.Combine(AppContext.BaseDirectory, "artifacts");
        string manifestName = parameters.ManifestName;
        UInt160 engineHash = ResolveEngineHash(engine, parameters);

        var nefPath = Path.Combine(artifactsPath, "LeanTokenTemplate.nef");
        var manifestPath = Path.Combine(artifactsPath, "LeanTokenTemplate.manifest.json");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        string manifestJson = File.ReadAllText(manifestPath);
        if (manifestName != "LeanTokenTemplate")
        {
            manifestJson = manifestJson.Replace(
                "\"name\":\"LeanTokenTemplate\"",
                "\"name\":\"" + manifestName + "\"",
                StringComparison.Ordinal);
        }

        var manifest = ContractManifest.Parse(manifestJson);
        var deployArgs = new object[]
        {
            parameters.Name,
            parameters.Symbol,
            parameters.InitialSupply,
            parameters.Decimals,
            parameters.Owner,
            parameters.Mintable ? BigInteger.One : BigInteger.Zero,
            parameters.MaxSupply,
            parameters.Upgradeable ? BigInteger.One : BigInteger.Zero,
            parameters.MetadataUri,
            parameters.Pausable ? BigInteger.One : BigInteger.Zero,
            parameters.LaunchFactory,
            parameters.PlatformFeeRate,
            parameters.CreatorFeeRate,
            engineHash
        };

        return engine.Deploy<LeanTokenTemplateContract>(nef, manifest, deployArgs);
    }

    private static UInt160 ResolveEngineHash(TestEngine engine, LeanDeployParams parameters)
    {
        var configuredEngineHash = parameters.EngineHash;
        if (configuredEngineHash is not null && configuredEngineHash != UInt160.Zero)
            return configuredEngineHash;

        UInt160 engineOwner = parameters.EngineOwner is not null &&
                              parameters.EngineOwner != UInt160.Zero
            ? parameters.EngineOwner
            : parameters.LaunchFactory != UInt160.Zero
                ? parameters.LaunchFactory
                : parameters.Owner;

        string engineManifestName = parameters.EngineManifestName;
        if (engineManifestName == "LeanTokenEngine" && parameters.ManifestName != "LeanTokenTemplate")
            engineManifestName = "LeanTokenEngine" + parameters.ManifestName;

        var leanEngine = DeployEngine(engine, engineOwner, engineManifestName);
        return leanEngine.Hash;
    }
}

internal sealed record LeanDeployParams
{
    public string Name { get; init; } = "Lean Token";
    public string Symbol { get; init; } = "LEAN";
    public UInt160 Owner { get; init; } = UInt160.Zero;
    public UInt160 LaunchFactory { get; init; } = UInt160.Zero;
    public BigInteger InitialSupply { get; init; } = BigInteger.Zero;
    public BigInteger Decimals { get; init; } = 8;
    public bool Mintable { get; init; } = true;
    public BigInteger MaxSupply { get; init; } = BigInteger.Zero;
    public string MetadataUri { get; init; } = "";
    public bool Pausable { get; init; }
    public bool Upgradeable { get; init; }
    public BigInteger PlatformFeeRate { get; init; } = BigInteger.Zero;
    public BigInteger CreatorFeeRate { get; init; } = BigInteger.Zero;
    public string ManifestName { get; init; } = "LeanTokenTemplate";
    public UInt160? EngineHash { get; init; }
    public UInt160? EngineOwner { get; init; }
    public string EngineManifestName { get; init; } = "LeanTokenEngine";
}
