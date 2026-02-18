#nullable enable
using Neo;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using Reqnroll;
using System;
using System.IO;
using System.Numerics;
using TokenTemplate.Tests.Support;
using TestContext = TokenTemplate.Tests.Support.TestContext;

namespace TokenTemplate.Tests.Steps;

/// <summary>
/// Base step definitions for deploying and invoking the TokenTemplate contract.
/// Covers the smoke test and the common patterns shared by all feature files.
/// StorageSchema and domain-specific steps are added in Phase 6.
/// </summary>
[Binding]
public class ContractSteps
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    private readonly TestContext _context;

    public ContractSteps(TestContext context)
    {
        _context = context;
    }

    // ── Infrastructure steps ──────────────────────────────────────────────────

    [Given("the TokenTemplate test engine is initialized")]
    public void GivenEngineIsInitialized()
    {
        Assert.That(_context.Engine, Is.Not.Null,
            "TestEngine must be initialized by ScenarioHooks.BeforeScenario");
    }

    // ── Deploy steps ──────────────────────────────────────────────────────────

    /// <summary>
    /// Deploys TokenTemplate with a specific symbol and 8 decimals.
    /// All other parameters use safe defaults (no initial supply, non-mintable, etc.).
    /// </summary>
    [When(@"the contract is deployed with symbol ""(.*)"" and decimals (.*)")]
    public void WhenContractDeployedWithSymbolAndDecimals(string symbol, int decimals)
    {
        var deployParams = new DeployParams
        {
            Name        = "Test Token",
            Symbol      = symbol,
            InitialSupply = 0,
            Decimals    = (BigInteger)decimals,
            Owner       = _context.OwnerSigner.Account,
            Mintable    = 0,
            MaxSupply   = 0,
            Upgradeable = 0,
            MetadataUri = "",
            Pausable    = 0
        };

        DeployContract(deployParams);
    }

    /// <summary>
    /// Deploys TokenTemplate with default parameters (symbol "TST", 8 decimals).
    /// Used as a convenience step when the specific values don't matter for the test.
    /// </summary>
    [Given("a freshly deployed TokenTemplate contract")]
    public void GivenFreshlyDeployedContract()
    {
        var deployParams = new DeployParams
        {
            Owner = _context.OwnerSigner.Account
        };

        DeployContract(deployParams);
    }

    // ── Invocation steps ──────────────────────────────────────────────────────

    [When(@"symbol\(\) is called on the deployed contract")]
    [When(@"symbol\(\) is called")]
    public void WhenSymbolIsCalled()
    {
        _context.LastException = null;
        try
        {
            _context.LastResult = _context.Contract!.Symbol;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    // ── Assertion steps ───────────────────────────────────────────────────────

    [Then(@"the returned string is ""(.*)""")]
    [Then(@"the result is ""(.*)""")]
    public void ThenReturnedStringIs(string expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Expected no exception but got: {_context.LastException?.Message}");
        Assert.That(_context.LastResult?.ToString(), Is.EqualTo(expected));
    }

    [Then("the transaction is aborted")]
    public void ThenTransactionIsAborted()
    {
        Assert.That(_context.LastException, Is.Not.Null,
            "Expected the transaction to be aborted (exception), but no exception was thrown.");
    }

    [Then(@"the transaction is aborted with ""(.*)""")]
    public void ThenTransactionIsAbortedWith(string message)
    {
        Assert.That(_context.LastException, Is.Not.Null,
            "Expected the transaction to be aborted (exception), but no exception was thrown.");
        Assert.That(_context.LastException!.Message, Does.Contain(message));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    [Then(@"the GAS consumed by deployment is (\d+) datoshi")]
    public void ThenGasConsumedByDeploymentIs(long expectedDatoshi)
    {
        Assert.That(_context.GasConsumedByLastDeploy, Is.EqualTo(expectedDatoshi),
            $"Expected deploy to consume {expectedDatoshi} datoshi " +
            $"but actually consumed {_context.GasConsumedByLastDeploy} datoshi " +
            $"({_context.GasConsumedByLastDeploy / 100_000_000m} GAS).");
    }

    /// <summary>Public helper so other step classes can trigger a deploy.</summary>
    public void DeployWith(DeployParams deployParams) => DeployContract(deployParams);

    private void DeployContract(DeployParams deployParams)
    {
        string nefPath      = Path.Combine(ArtifactsPath, "TokenTemplate.nef");
        string manifestPath = Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json");

        Assert.That(File.Exists(nefPath),      Is.True, $"NEF not found at: {nefPath}");
        Assert.That(File.Exists(manifestPath), Is.True, $"Manifest not found at: {manifestPath}");

        var nef      = Neo.SmartContract.NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        // Set transaction signer to the contract owner before deploying
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);

        // Measure GAS consumed by the deploy transaction (datoshi = GAS × 100_000_000)
        using var watcher = _context.Engine.CreateGasWatcher();
        _context.Contract = _context.Engine.Deploy<TokenTemplateContract>(
            nef, manifest, deployParams.ToDeployArray());
        _context.GasConsumedByLastDeploy = watcher.Value;
        _context.LastDeployedOwner = deployParams.Owner;
    }
}
