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
/// Step definitions for TokenFactory BDD scenarios.
///
/// Covers Phase 5 (infrastructure + storage verification) and Phase 6 (core, admin, registry).
/// Follow the same patterns as SpikeDeploySteps.cs:
///   - Artifacts loaded from AppContext.BaseDirectory/artifacts/
///   - Factory deployed via Engine.Deploy(nef, manifest, ownerAddress)
///   - Admin calls preceded by SetTransactionSigners(OwnerSigner)
/// </summary>
[Binding]
public class TokenFactorySteps
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    private readonly TestContext _context;

    public TokenFactorySteps(TestContext context)
    {
        _context = context;
    }

    // ── Infrastructure ────────────────────────────────────────────────────────

    [Given("the TokenFactory test engine is initialized")]
    public void GivenFactoryEngineIsInitialized()
    {
        Assert.That(_context.Engine, Is.Not.Null,
            "TestEngine must be initialized by ScenarioHooks.BeforeScenario");
    }

    // ── Factory deploy steps ──────────────────────────────────────────────────

    [Given("a freshly deployed TokenFactory")]
    public void AFreshlyDeployedTokenFactory() =>
        DeployFactory(_context.OwnerSigner.Account);

    [Given(@"the factory is deployed with owner (\w+)")]
    public void TheFactoryIsDeployedWithOwner(string wallet) =>
        DeployFactory(ResolveWalletAddress(wallet));

    [Given("the factory is deployed and initialized")]
    public void TheFactoryIsDeployedAndInitialized()
    {
        DeployFactory(_context.OwnerSigner.Account);
        CallSetNefAndManifest();
    }

    /// <summary>
    /// Handles compound states like "deployed and paused".
    /// The negative lookahead (?!initialized) prevents ambiguity with the specific
    /// "deployed and initialized" step above.
    /// </summary>
    [Given(@"the factory is deployed and (?!initialized)(\w+)")]
    public void TheFactoryIsDeployedAndState(string state)
    {
        TheFactoryIsDeployedAndInitialized();
        switch (state)
        {
            case "paused":
                _context.Engine.SetTransactionSigners(_context.OwnerSigner);
                _context.Factory!.Pause();
                break;
            default:
                throw new NotImplementedException($"Unknown factory state: '{state}'");
        }
    }

    // ── Admin setup steps ─────────────────────────────────────────────────────

    [Given("the owner calls setNefAndManifest with the TokenTemplate artifacts")]
    public void TheOwnerCallsSetNefAndManifest() => CallSetNefAndManifest();

    [Given("the owner has paused the factory")]
    public void TheOwnerHasPausedTheFactory()
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.Pause();
    }

    // ── Query steps (When) ────────────────────────────────────────────────────

    [When(@"getTokenCount\(\) is called")]
    public void GetTokenCountIsCalled() =>
        _context.LastResult = _context.Factory!.GetTokenCount();

    [When(@"getOwner\(\) is called")]
    public void GetOwnerIsCalled() =>
        _context.LastResult = _context.Factory!.GetOwner();

    [When(@"isInitialized\(\) is called")]
    public void IsInitializedIsCalled() =>
        _context.LastResult = _context.Factory!.IsInitialized();

    [When(@"isPaused\(\) is called")]
    public void IsPausedIsCalled() =>
        _context.LastResult = _context.Factory!.IsPaused();

    [When(@"getMinFee\(\) is called")]
    public void GetMinFeeIsCalled() =>
        _context.LastResult = _context.Factory!.GetMinFee();

    [When(@"getTreasury\(\) is called")]
    public void GetTreasuryIsCalled() =>
        _context.LastResult = _context.Factory!.GetTreasury();

    [When(@"getPremiumTiersEnabled\(\) is called")]
    public void GetPremiumTiersEnabledIsCalled() =>
        _context.LastResult = _context.Factory!.GetPremiumTiersEnabled();

    // ── Admin call steps (When) ───────────────────────────────────────────────

    [When(@"the owner calls setFee with (\d+)")]
    public void TheOwnerCallsSetFee(string fee)
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.SetFee(BigInteger.Parse(fee));
    }

    [When(@"the owner calls setTreasuryAddress with (\w+)")]
    public void TheOwnerCallsSetTreasuryAddress(string wallet)
    {
        var address = ResolveWalletAddress(wallet);
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.SetTreasuryAddress(address);
    }

    [When(@"the owner calls pause\(\)")]
    public void TheOwnerCallsPause()
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.Pause();
    }

    [When(@"the owner calls unpause\(\)")]
    public void TheOwnerCallsUnpause()
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.Unpause();
    }

    [When(@"the owner calls setPremiumTiersEnabled with (\w+)")]
    public void TheOwnerCallsSetPremiumTiersEnabled(string value)
    {
        bool enabled = bool.Parse(value);
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.SetPremiumTiersEnabled(enabled);
    }

    // ── Assertion steps (Then) ────────────────────────────────────────────────

    [Then(@"the result is (\d+)")]
    public void TheResultIsNumeric(string expected)
    {
        BigInteger expectedValue = BigInteger.Parse(expected);
        BigInteger actual = _context.LastResult switch
        {
            BigInteger bi => bi,
            byte b        => (BigInteger)b,
            int i         => (BigInteger)i,
            long l        => (BigInteger)l,
            _ => throw new InvalidCastException(
                $"Expected numeric result but got: {_context.LastResult?.GetType().Name ?? "null"}")
        };
        Assert.That(actual, Is.EqualTo(expectedValue));
    }

    // Note: (true|false) alternation is NOT supported by Reqnroll — use two literal steps.
    [Then("the result is true")]
    public void TheResultIsTrue() =>
        Assert.That((bool)_context.LastResult!, Is.True);

    [Then("the result is false")]
    public void TheResultIsFalse() =>
        Assert.That((bool)_context.LastResult!, Is.False);

    [Then("the result is the zero address")]
    public void TheResultIsZeroAddress()
    {
        var result = _context.LastResult as UInt160;
        Assert.That(result == null || result == UInt160.Zero, Is.True,
            $"Expected zero/null address but got: {result}");
    }

    [Then(@"the result equals (\w+)'s address")]
    public void TheResultEqualsAddress(string wallet)
    {
        var expected = ResolveWalletAddress(wallet);
        var actual = _context.LastResult as UInt160;
        Assert.That(actual, Is.EqualTo(expected),
            $"Expected {wallet}'s address ({expected}) but got: {actual}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Deploys the TokenFactory contract with the given owner address.
    /// Owner is passed as deploy data; the contract stores it as the admin.
    /// </summary>
    private void DeployFactory(UInt160 ownerAddress)
    {
        var nefPath      = Path.Combine(ArtifactsPath, "TokenFactory.nef");
        var manifestPath = Path.Combine(ArtifactsPath, "TokenFactory.manifest.json");

        Assert.That(File.Exists(nefPath),      Is.True, $"TokenFactory NEF not found: {nefPath}");
        Assert.That(File.Exists(manifestPath), Is.True, $"TokenFactory manifest not found: {manifestPath}");

        var nef      = Neo.SmartContract.NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory = _context.Engine.Deploy<TokenFactoryContract>(nef, manifest, ownerAddress);
    }

    /// <summary>
    /// Loads TokenTemplate artifacts and calls SetNefAndManifest as the owner.
    /// Required before the factory can deploy new tokens.
    /// </summary>
    private void CallSetNefAndManifest()
    {
        var nefBytes      = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifestString = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));

        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.SetNefAndManifest(nefBytes, manifestString);
    }

    /// <summary>Resolves a wallet name to its UInt160 address from NamedSigners.</summary>
    private UInt160 ResolveWalletAddress(string wallet)
    {
        if (_context.NamedSigners.TryGetValue(wallet, out var signer))
            return signer.Account;
        throw new InvalidOperationException(
            $"Wallet '{wallet}' not registered. Available: {string.Join(", ", _context.NamedSigners.Keys)}");
    }
}
