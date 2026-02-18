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
/// Step definitions for SpikeDeploy.feature.
///
/// Validates the critical FEAT-070 assumption: ContractManagement.Deploy()
/// is callable from within a running Neo N3 contract.
///
/// The spike works by:
///   1. Deploying the SpikeDeploy contract (a minimal factory stub)
///   2. Calling spike.deployTemplate(tokenTemplateNef, manifest, params)
///   3. SpikeDeploy internally calls ContractManagement.Deploy() in the NeoVM
///   4. The returned hash is used to get a TokenTemplateContract proxy
///   5. We assert on the deployed token's state (symbol, balanceOf)
/// </summary>
[Binding]
public class SpikeDeploySteps
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    private readonly TestContext _context;

    public SpikeDeploySteps(TestContext context)
    {
        _context = context;
    }

    // ── Infrastructure ────────────────────────────────────────────────────────

    [Given("the SpikeDeploy test engine is initialized")]
    public void GivenSpikeEngineIsInitialized()
    {
        Assert.That(_context.Engine, Is.Not.Null,
            "TestEngine must be initialized by ScenarioHooks.BeforeScenario");
    }

    // ── Spike factory deploy steps ────────────────────────────────────────────

    [When(@"the spike factory deploys a TokenTemplate with symbol ""(.*)"" and owner (\w+)")]
    public void WhenSpikeDeploysTokenTemplate(string symbol, string ownerWallet)
    {
        DeployTokenViaSpike(symbol, ownerWallet, initialSupply: 0);
    }

    [When(@"the spike factory deploys a TokenTemplate with symbol ""(.*)"", owner (\w+), and initialSupply (\d+)")]
    public void WhenSpikeDeploysTokenTemplateWithSupply(string symbol, string ownerWallet, long initialSupply)
    {
        DeployTokenViaSpike(symbol, ownerWallet, (BigInteger)initialSupply);
    }

    // ── Assertion steps ───────────────────────────────────────────────────────

    [Then("the returned contract hash is not zero")]
    public void ThenReturnedHashIsNotZero()
    {
        Assert.That(_context.LastException, Is.Null,
            $"deployTemplate() threw: {_context.LastException?.Message}");
        Assert.That(_context.LastDeployedTokenHash, Is.Not.EqualTo(UInt160.Zero),
            "Expected a non-zero contract hash from ContractManagement.Deploy()");
    }

    [Then(@"calling symbol\(\) on the deployed token returns ""(.*)""")]
    public void ThenDeployedTokenSymbolIs(string expected)
    {
        var token = GetDeployedTokenProxy();
        Assert.That(token.Symbol, Is.EqualTo(expected));
    }

    [Then(@"balanceOf (\w+) on the deployed token is (\d+)")]
    public void ThenDeployedTokenBalanceIs(string walletName, long expected)
    {
        var address = _context.NamedSigners.TryGetValue(walletName, out var signer)
            ? signer.Account
            : throw new InvalidOperationException($"Wallet '{walletName}' not found in NamedSigners");

        var token = GetDeployedTokenProxy();
        Assert.That(token.BalanceOf(address), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"totalSupply of the deployed token is (\d+)")]
    public void ThenDeployedTokenTotalSupplyIs(long expected)
    {
        var token = GetDeployedTokenProxy();
        Assert.That(token.TotalSupply, Is.EqualTo((BigInteger)expected));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Core spike logic:
    ///   1. Deploy SpikeDeploy contract (the factory stub)
    ///   2. Load TokenTemplate NEF + manifest bytes
    ///   3. Call spike.deployTemplate(nef, manifest, tokenParams)
    ///      → this calls ContractManagement.Deploy() from within NeoVM
    ///   4. Store the returned hash for assertion steps
    /// </summary>
    private void DeployTokenViaSpike(string symbol, string ownerWallet, BigInteger initialSupply)
    {
        _context.LastException = null;
        _context.LastDeployedTokenHash = UInt160.Zero;

        try
        {
            // Step 1: deploy the SpikeDeploy factory stub
            var spikeNefPath      = Path.Combine(ArtifactsPath, "SpikeDeploy.nef");
            var spikeManifestPath = Path.Combine(ArtifactsPath, "SpikeDeploy.manifest.json");

            Assert.That(File.Exists(spikeNefPath),      Is.True, $"SpikeDeploy NEF not found: {spikeNefPath}");
            Assert.That(File.Exists(spikeManifestPath), Is.True, $"SpikeDeploy manifest not found: {spikeManifestPath}");

            var spikeNef      = Neo.SmartContract.NefFile.Parse(File.ReadAllBytes(spikeNefPath));
            var spikeManifest = ContractManifest.Parse(File.ReadAllText(spikeManifestPath));

            _context.Engine.SetTransactionSigners(_context.OwnerSigner);
            _context.SpikeContract = _context.Engine.Deploy<SpikeDeployContract>(spikeNef, spikeManifest, null);

            // Step 2: load TokenTemplate artifacts as raw bytes/string for ContractManagement.Deploy()
            var tokenNefBytes  = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
            var tokenManifest  = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));

            // Step 3: build the 10-param object[] for TokenTemplate._deploy()
            var ownerAddress = _context.NamedSigners.TryGetValue(ownerWallet, out var signer)
                ? signer.Account
                : _context.OwnerSigner.Account;

            var tokenParams = new DeployParams
            {
                Symbol        = symbol,
                Owner         = ownerAddress,
                InitialSupply = initialSupply
            }.ToDeployArray();

            // Step 4: call spike.deployTemplate() — this calls ContractManagement.Deploy() in NeoVM
            _context.Engine.SetTransactionSigners(_context.OwnerSigner);
            var returnedHash = _context.SpikeContract.deployTemplate(tokenNefBytes, tokenManifest, tokenParams);

            Assert.That(returnedHash, Is.Not.Null, "deployTemplate() returned null hash");
            _context.LastDeployedTokenHash = returnedHash!;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    /// <summary>Gets a TokenTemplateContract proxy for the factory-deployed token instance.</summary>
    private TokenTemplateContract GetDeployedTokenProxy()
    {
        Assert.That(_context.LastException, Is.Null,
            $"Cannot get token proxy — deployTemplate() threw: {_context.LastException?.Message}");
        Assert.That(_context.LastDeployedTokenHash, Is.Not.EqualTo(UInt160.Zero),
            "Cannot get token proxy — no contract hash stored");

        if (_context.DeployedToken is null)
            _context.DeployedToken = _context.Engine.FromHash<TokenTemplateContract>(
                _context.LastDeployedTokenHash, true);

        return _context.DeployedToken;
    }
}
