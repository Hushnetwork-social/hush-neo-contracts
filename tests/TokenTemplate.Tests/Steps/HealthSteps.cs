using Neo.SmartContract.Manifest;
using NUnit.Framework;
using Reqnroll;
using System;
using System.IO;

namespace TokenTemplate.Tests.Steps;

[Binding]
public class HealthSteps
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    private string _nefPath = string.Empty;

    [Given("the TokenTemplate contract artifact exists")]
    public void GivenTheContractArtifactExists()
    {
        _nefPath = Path.Combine(ArtifactsPath, "TokenTemplate.nef");
        Assert.That(File.Exists(_nefPath), Is.True,
            $"TokenTemplate.nef not found at {_nefPath}. " +
            "Build src/TokenTemplate first: dotnet build");
    }

    [Then("the NEF file can be read without error")]
    public void ThenTheNefFileCanBeRead()
    {
        byte[] nefBytes = File.ReadAllBytes(_nefPath);
        Assert.That(nefBytes.Length, Is.GreaterThan(0), "NEF file is empty");

        // Verify manifest exists alongside NEF
        string manifestPath = Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json");
        Assert.That(File.Exists(manifestPath), Is.True,
            $"TokenTemplate.manifest.json not found at {manifestPath}");

        string manifestJson = File.ReadAllText(manifestPath);
        ContractManifest manifest = ContractManifest.Parse(manifestJson);
        Assert.That(manifest.Name, Is.EqualTo("TokenTemplate"));
    }
}
