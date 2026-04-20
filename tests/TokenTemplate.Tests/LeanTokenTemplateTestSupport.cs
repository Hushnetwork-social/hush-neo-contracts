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
    public static LeanTokenTemplateContract Deploy(TestEngine engine, LeanDeployParams parameters)
    {
        string artifactsPath = Path.Combine(AppContext.BaseDirectory, "artifacts");
        string manifestName = parameters.ManifestName;

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
            parameters.CreatorFeeRate
        };

        return engine.Deploy<LeanTokenTemplateContract>(nef, manifest, deployArgs);
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
}
