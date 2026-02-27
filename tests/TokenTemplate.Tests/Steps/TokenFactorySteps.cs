#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using Reqnroll;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
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
///   - Admin calls preceded by SetTransactionSigners(signer)
///
/// GAS payment simulation (Phase 6):
///   Direct proxy calls to OnNEP17Payment set CallingScriptHash = some non-GAS hash, failing Guard 1.
///   To simulate a real GAS Transfer, we set engine.OnGetCallingScriptHash to return GAS.Hash
///   for the duration of the OnNEP17Payment call. This fakes the NeoVM execution context.
/// </summary>
[Binding]
public class TokenFactorySteps
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    // Default valid token params for shortcut steps (7 elements: name, symbol, supply, decimals, mode, imageUrl, creatorFeeRate)
    private static readonly object[] ValidTokenData =
        new object[] { "ValidToken", "VLD", (BigInteger)1000, (BigInteger)8, "community", "", (BigInteger)0 };

    private readonly TestContext _context;

    // ── Task 5.4: Named token registry (lifecycle scenarios) and GAS tracking ──
    private readonly Dictionary<string, UInt160> _namedTokens = new();
    private BigInteger _factoryGasBefore = BigInteger.Zero;

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

    // ── Phase 6: Additional setup steps ──────────────────────────────────────

    /// <summary>
    /// Initializes the factory (calls SetNefAndManifest) using the owner signer.
    /// Used as an additional "And" in admin scenarios that started with Background deploy.
    /// </summary>
    [Given("the factory is initialized")]
    public void TheFactoryIsInitialized() => CallSetNefAndManifest();

    [Given("the factory is initialized and paused")]
    public void TheFactoryIsInitializedAndPaused()
    {
        CallSetNefAndManifest();
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.Pause();
    }

    [Given("the factory is paused by the owner")]
    public void TheFactoryIsPausedByOwner()
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.Pause();
    }

    /// <summary>
    /// Creates N tokens for the given wallet via simulated GAS payment.
    /// Used in registry pagination scenarios.
    /// </summary>
    [Given(@"(\w+) has created (\d+) tokens?")]
    [Given(@"(\w+) has already created (\d+) tokens?")]
    public void WalletHasCreatedTokens(string wallet, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var tokenData = new object[]
            {
                $"Token{i}", $"TK{i}", (BigInteger)(1000 + i), (BigInteger)8, "community", "", (BigInteger)0
            };
            SimulateGasPayment(wallet, 1_500_000_000, tokenData);
            Assert.That(_context.LastException, Is.Null,
                $"Token creation {i + 1} failed: {_context.LastException?.Message}");
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

    // ── Phase 6: GAS payment simulation steps (When) ─────────────────────────

    /// <summary>
    /// Simulates a GAS payment with the 5-param form (no imageUrl).
    /// imageUrl defaults to "" — all existing feature file scenarios use this form.
    ///
    /// Step text example:
    ///   When walletA transfers 1500000000 GAS to the factory with token params "MyToken" "MTK" 1000000 8 "community"
    /// </summary>
    // Note: mode uses (\w+) instead of (.*) — (\w+) stops at non-word chars (quotes, slashes),
    // preventing ambiguity with the 6-param form that appends an imageUrl quoted string.
    [When(@"(\w+) transfers (\d+) GAS to the factory with token params ""(.*)"" ""(.*)"" (\d+) (\d+) ""(\w+)""")]
    public void WalletTransfersGasWithTokenParams(
        string wallet, long amount, string name, string symbol,
        long supply, long decimals, string mode)
    {
        var tokenData = new object[]
        {
            name, symbol, (BigInteger)supply, (BigInteger)decimals, mode, "", (BigInteger)0
        };
        SimulateGasPayment(wallet, (BigInteger)amount, tokenData);
    }

    /// <summary>
    /// Simulates a GAS payment with all 6 token params including imageUrl.
    ///
    /// Step text example:
    ///   When walletA transfers 1500000000 GAS to the factory with token params "MyToken" "MTK" 1000000 8 "community" "https://example.com/icon.png"
    /// </summary>
    [When(@"(\w+) transfers (\d+) GAS to the factory with token params ""(.*)"" ""(.*)"" (\d+) (\d+) ""(.*)"" ""(.*)""")]
    public void WalletTransfersGasWithTokenParamsAndImageUrl(
        string wallet, long amount, string name, string symbol,
        long supply, long decimals, string mode, string imageUrl)
    {
        var tokenData = new object[]
        {
            name, symbol, (BigInteger)supply, (BigInteger)decimals, mode, imageUrl, (BigInteger)0
        };
        SimulateGasPayment(wallet, (BigInteger)amount, tokenData);
    }

    /// <summary>
    /// Transfers GAS with default valid token params (name="ValidToken", symbol="VLD", supply=1000, decimals=8, mode="community").
    /// The amount is still configurable to test boundary conditions.
    /// </summary>
    [When(@"(\w+) transfers (\d+) GAS to the factory with valid token params")]
    public void WalletTransfersGasWithValidParams(string wallet, long amount)
    {
        SimulateGasPayment(wallet, (BigInteger)amount, ValidTokenData);
    }

    /// <summary>
    /// Calls OnNEP17Payment WITHOUT the CallingScriptHash override.
    /// This simulates a non-GAS NEP-17 token transfer, which should fail Guard 1.
    /// </summary>
    [When("a non-GAS token is transferred to the factory")]
    public void ANonGasTokenIsTransferredToFactory()
    {
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(_context.OwnerSigner);
            _context.Factory!.OnNEP17Payment(_context.OwnerSigner.Account, 1_500_000_000, ValidTokenData);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    /// <summary>Sends a payment with only 3 data elements (Guard 5 requires 5).</summary>
    [When(@"(\w+) sends payment with 3 data elements")]
    public void WalletSendsPaymentWith3DataElements(string wallet)
    {
        var badData = new object[] { "name", "sym", "community" };
        SimulateGasPayment(wallet, 1_500_000_000, badData);
    }

    /// <summary>Sends a payment with null data (Guard 5 cast throws).</summary>
    [When(@"(\w+) sends payment with null data")]
    public void WalletSendsPaymentWithNullData(string wallet)
    {
        SimulateGasPayment(wallet, 1_500_000_000, null!);
    }

    /// <summary>Sends a payment that is guaranteed to be rejected (below minimum fee).</summary>
    [When(@"(\w+) sends a payment that is rejected")]
    public void WalletSendsARejectedPayment(string wallet)
    {
        SimulateGasPayment(wallet, 100, ValidTokenData);
    }

    // ── Phase 5: Query steps (When) ───────────────────────────────────────────

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

    // ── Phase 6: Registry query steps (When) ─────────────────────────────────

    [When(@"getTokensByCreator for (\w+) page (\d+) size (\d+) is called")]
    public void GetTokensByCreatorIsCalled(string wallet, int page, int pageSize)
    {
        var address = GetOrCreateWallet(wallet).Account;
        _context.LastResult = _context.Factory!.GetTokensByCreator(address, page, pageSize);
    }

    [When("getToken with zero hash is called")]
    public void GetTokenWithZeroHashIsCalled()
    {
        _context.LastResult = _context.Factory!.GetToken(UInt160.Zero);
    }

    // ── Phase 6: Admin call steps with specific wallets (When) ───────────────

    [When(@"(\w+) calls setNefAndManifest with the TokenTemplate artifacts")]
    public void WalletCallsSetNefAndManifest(string wallet)
    {
        var signer = GetOrCreateWallet(wallet);
        _context.LastException = null;
        try
        {
            var nefBytes      = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
            var manifestString = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.SetNefAndManifest(nefBytes, manifestString);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"(\w+) calls setFee\((\d+)\)")]
    public void WalletCallsSetFee(string wallet, long fee)
    {
        var signer = GetOrCreateWallet(wallet);
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.SetFee((BigInteger)fee);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"(\w+) calls pause\(\)")]
    public void WalletCallsPause(string wallet)
    {
        var signer = GetOrCreateWallet(wallet);
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.Pause();
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"(\w+) calls unpause\(\)")]
    public void WalletCallsUnpause(string wallet)
    {
        var signer = GetOrCreateWallet(wallet);
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.Unpause();
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"(\w+) calls setOwner\((\w+)\)")]
    public void WalletCallsSetOwner(string wallet, string newOwnerWallet)
    {
        var signer       = GetOrCreateWallet(wallet);
        var newOwnerAddr = GetOrCreateWallet(newOwnerWallet).Account;
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.SetOwner(newOwnerAddr);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"(\w+) calls setTreasuryAddress\((\w+)\)")]
    public void WalletCallsSetTreasuryAddress(string wallet, string treasuryWallet)
    {
        var signer      = GetOrCreateWallet(wallet);
        var treasuryAddr = GetOrCreateWallet(treasuryWallet).Account;
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.SetTreasuryAddress(treasuryAddr);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"(\w+) calls setPremiumTiersEnabled\((\w+)\)")]
    public void WalletCallsSetPremiumTiersEnabled(string wallet, string value)
    {
        var signer  = GetOrCreateWallet(wallet);
        bool enabled = bool.Parse(value);
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.SetPremiumTiersEnabled(enabled);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    // ── Phase 5: Admin call steps with owner (When) ───────────────────────────

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

    // ── Phase 6: Core assertion steps (Then) ─────────────────────────────────

    [Then("the transaction succeeds")]
    public void TheTransactionSucceeds()
    {
        Assert.That(_context.LastException, Is.Null,
            $"Expected no exception but got: {_context.LastException?.Message}");
    }

    /// <summary>Combined call + assert for token count. Avoids needing a When + Then pair.</summary>
    [Then(@"getTokenCount\(\) returns (\d+)")]
    public void GetTokenCountReturnsThen(long expected)
    {
        var actual = _context.Factory!.GetTokenCount();
        Assert.That(actual, Is.EqualTo((BigInteger)expected),
            $"Expected getTokenCount() == {expected} but got {actual}");
    }

    [Then(@"getTokenCount\(\) is still (\d+)")]
    public void GetTokenCountIsStill(long expected)
    {
        var actual = _context.Factory!.GetTokenCount();
        Assert.That(actual, Is.EqualTo((BigInteger)expected),
            $"Expected token count to remain {expected} but got {actual}");
    }

    [Then("the returned token hash is not zero")]
    public void TheReturnedTokenHashIsNotZero()
    {
        Assert.That(_context.LastException, Is.Null,
            $"Cannot verify hash — payment threw: {_context.LastException?.Message}");
        Assert.That(_context.LastCreatedTokenHash, Is.Not.EqualTo(UInt160.Zero),
            "Expected a non-zero contract hash from factory token creation");
    }

    [Then(@"the deployed token's symbol\(\) returns ""(.*)""")]
    public void TheDeployedTokenSymbolReturns(string expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Cannot get token — payment threw: {_context.LastException?.Message}");
        Assert.That(_context.LastCreatedTokenHash, Is.Not.EqualTo(UInt160.Zero),
            "No token was created");
        var token = _context.Engine.FromHash<TokenTemplateContract>(_context.LastCreatedTokenHash, true);
        Assert.That(token.Symbol, Is.EqualTo(expected));
    }

    [Then(@"the registry token has symbol ""(.*)"" mode ""(.*)"" tier ""(.*)"" supply (\d+)")]
    public void TheRegistryTokenHasFields(string symbol, string mode, string tier, long supply)
    {
        Assert.That(_context.LastCreatedTokenHash, Is.Not.EqualTo(UInt160.Zero), "No token created");
        var info = _context.Factory!.GetToken(_context.LastCreatedTokenHash);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        // tokenInfo: [0]=symbol, [1]=creator, [2]=supply, [3]=mode, [4]=tier, [5]=createdAt
        Assert.That(ParseString(info![0]), Is.EqualTo(symbol),  "symbol mismatch");
        Assert.That(ParseString(info[3]),  Is.EqualTo(mode),    "mode mismatch");
        Assert.That(ParseString(info[4]),  Is.EqualTo(tier),    "tier mismatch");
        Assert.That(ParseBigInteger(info[2]), Is.EqualTo((BigInteger)supply), "supply mismatch");
    }

    [Then(@"the registry token imageUrl is ""(.*)""")]
    public void TheRegistryTokenImageUrlIs(string expected)
    {
        Assert.That(_context.LastCreatedTokenHash, Is.Not.EqualTo(UInt160.Zero), "No token created");
        var info = _context.Factory!.GetToken(_context.LastCreatedTokenHash);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        // tokenInfo[6] = imageUrl
        var actual = info!.Length > 6 ? ParseString(info[6]) : "";
        Assert.That(actual, Is.EqualTo(expected), "imageUrl mismatch");
    }

    [Then(@"the registry token creator equals (\w+)'s address")]
    public void TheRegistryTokenCreatorEquals(string wallet)
    {
        Assert.That(_context.LastCreatedTokenHash, Is.Not.EqualTo(UInt160.Zero), "No token created");
        var info = _context.Factory!.GetToken(_context.LastCreatedTokenHash);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        // tokenInfo[1] = creator address (UInt160)
        var expected = GetOrCreateWallet(wallet).Account;
        var actual   = ParseHash(info![1]);
        Assert.That(actual, Is.EqualTo(expected),
            $"Expected creator {expected} but got {actual}");
    }

    [Then(@"getTokensByCreator for (\w+) page (\d+) size (\d+) contains the last created token")]
    public void GetTokensByCreatorContainsLastToken(string wallet, int page, int pageSize)
    {
        var address = GetOrCreateWallet(wallet).Account;
        var tokens  = _context.Factory!.GetTokensByCreator(address, page, pageSize);
        Assert.That(tokens, Is.Not.Null, "GetTokensByCreator returned null");
        Assert.That(tokens, Does.Contain(_context.LastCreatedTokenHash),
            $"Expected GetTokensByCreator to contain {_context.LastCreatedTokenHash}");
    }

    [Then(@"getTokensByCreator for (\w+) page (\d+) size (\d+) has (\d+) results?")]
    public void GetTokensByCreatorHasNResults(string wallet, int page, int pageSize, int expected)
    {
        var address = GetOrCreateWallet(wallet).Account;
        var tokens  = _context.Factory!.GetTokensByCreator(address, page, pageSize);
        int actual  = tokens?.Length ?? 0;
        Assert.That(actual, Is.EqualTo(expected),
            $"Expected {expected} token(s) for {wallet} (page {page}, size {pageSize}) but got {actual}");
    }

    // ── Phase 6: Registry assertion steps (Then) ─────────────────────────────

    [Then("an empty result is returned")]
    public void AnEmptyResultIsReturned()
    {
        if (_context.LastResult is UInt160[] arr)
            Assert.That(arr, Is.Empty, $"Expected empty array but got {arr.Length} items");
        else
            Assert.That(_context.LastResult == null || (_context.LastResult is Array a && a.Length == 0),
                Is.True, $"Expected null or empty result but got: {_context.LastResult}");
    }

    [Then(@"(\d+) results? are returned")]
    public void NResultsAreReturned(int expected)
    {
        Assert.That(_context.LastResult, Is.Not.Null, "Expected a result but got null");
        int actual = _context.LastResult switch
        {
            UInt160[] arr => arr.Length,
            Array a       => a.Length,
            _             => throw new InvalidCastException($"Expected array result but got {_context.LastResult!.GetType().Name}")
        };
        Assert.That(actual, Is.EqualTo(expected),
            $"Expected {expected} result(s) but got {actual}");
    }

    [Then("the result is null")]
    public void TheResultIsNull()
    {
        Assert.That(_context.LastResult, Is.Null,
            $"Expected null but got: {_context.LastResult}");
    }

    // ── Phase 5: Assertion steps (Then) ──────────────────────────────────────

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
        var actual   = _context.LastResult as UInt160;
        Assert.That(actual, Is.EqualTo(expected),
            $"Expected {wallet}'s address ({expected}) but got: {actual}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Simulates a GAS NEP-17 payment to the factory by overriding the engine's
    /// CallingScriptHash to return GAS.Hash for the duration of the OnNEP17Payment call.
    /// This causes the factory's Guard 1 ("Only GAS accepted") to pass.
    ///
    /// On success, populates LastCreatedTokenHash and LastTokenCreatedEvent.
    /// On failure, populates LastException and leaves LastCreatedTokenHash = Zero.
    /// </summary>
    private void SimulateGasPayment(string walletName, BigInteger amountDatoshi, object[] tokenData)
    {
        var signer = GetOrCreateWallet(walletName);
        _context.LastException        = null;
        _context.LastCreatedTokenHash = UInt160.Zero;

        try
        {
            // Override CallingScriptHash to simulate being called by the GAS contract.
            // The factory checks: Runtime.CallingScriptHash == GAS.Hash (Guard 1).
            _context.Engine.OnGetCallingScriptHash = (_, _) => _context.Engine.Native.GAS.Hash;
            _context.Engine.SetTransactionSigners(signer);
            _context.Factory!.OnNEP17Payment(signer.Account, amountDatoshi, tokenData);

            // Capture the newly created token hash from the per-creator registry.
            // GetTokensByCreator returns tokens oldest-first; the last element is the newest.
            var tokens = _context.Factory.GetTokensByCreator(signer.Account, 0, 10_000);
            if (tokens != null && tokens.Length > 0)
            {
                _context.LastCreatedTokenHash = tokens[tokens.Length - 1];
                // Also set LastDeployedTokenHash so SpikeDeploySteps assertions (balanceOf, totalSupply)
                // can be reused without modification for factory scenarios.
                _context.LastDeployedTokenHash = _context.LastCreatedTokenHash;
                _context.DeployedToken         = null; // force proxy re-creation by SpikeDeploySteps
            }
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
        finally
        {
            _context.Engine.OnGetCallingScriptHash = null;
        }
    }

    /// <summary>Resolves a wallet name to its Signer, creating a new one if not already registered.</summary>
    private Signer GetOrCreateWallet(string name)
    {
        if (!_context.NamedSigners.TryGetValue(name, out var signer))
        {
            signer = TestEngine.GetNewSigner();
            _context.NamedSigners[name] = signer;
        }
        return signer;
    }

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
        using var watcher = _context.Engine.CreateGasWatcher();
        _context.Factory  = _context.Engine.Deploy<TokenFactoryContract>(nef, manifest, ownerAddress);
        _context.GasConsumedByFactoryDeploy = watcher.Value;
    }

    [Then(@"the factory deployment GAS is (\d+) datoshi")]
    public void ThenFactoryDeploymentGasIs(long expectedDatoshi) =>
        Assert.That(_context.GasConsumedByFactoryDeploy, Is.EqualTo(expectedDatoshi),
            $"Actual factory deployment GAS was {_context.GasConsumedByFactoryDeploy} datoshi " +
            $"({_context.GasConsumedByFactoryDeploy / 100_000_000.0:F8} GAS)");

    /// <summary>
    /// Loads TokenTemplate artifacts and calls SetNefAndManifest as the owner.
    /// Required before the factory can deploy new tokens.
    /// </summary>
    private void CallSetNefAndManifest()
    {
        var nefBytes       = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifestString = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));

        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory!.SetNefAndManifest(nefBytes, manifestString);
    }

    /// <summary>Resolves a wallet name to its UInt160 address from NamedSigners (no creation).</summary>
    private UInt160 ResolveWalletAddress(string wallet)
    {
        if (_context.NamedSigners.TryGetValue(wallet, out var signer))
            return signer.Account;
        throw new InvalidOperationException(
            $"Wallet '{wallet}' not registered. Available: {string.Join(", ", _context.NamedSigners.Keys)}");
    }

    /// <summary>
    /// Converts an object from a NeoVM contract's object[] return to a C# string.
    /// The TestEngine may return Neo.VM.Types.ByteString items for elements in an object[].
    /// ByteString.ToString() wraps content in quotes (e.g. "\"EVT\""), so we use GetString()
    /// for the raw UTF-8 value instead.
    /// </summary>
    private static string ParseString(object? item) => item switch
    {
        string s => s,
        byte[] b => Encoding.UTF8.GetString(b),
        Neo.VM.Types.ByteString bs => bs.GetString() ?? "",
        Neo.VM.Types.PrimitiveType pt => Encoding.UTF8.GetString(pt.GetSpan()),
        _ => item?.ToString() ?? ""
    };

    /// <summary>
    /// Converts an object from a NeoVM object[] return to a UInt160.
    /// The TestEngine returns Neo.VM.Types.ByteString for hash fields in deserialized arrays.
    /// </summary>
    private static UInt160 ParseHash(object? item) => item switch
    {
        UInt160 h => h,
        byte[] b when b.Length == 20 => new UInt160(b),
        Neo.VM.Types.ByteString bs => new UInt160(bs.GetSpan()),
        Neo.VM.Types.PrimitiveType pt => new UInt160(pt.GetSpan()),
        _ => UInt160.Zero
    };

    /// <summary>
    /// Converts an object from a NeoVM object[] return to a BigInteger.
    /// </summary>
    private static BigInteger ParseBigInteger(object? item) => item switch
    {
        BigInteger bi => bi,
        byte b        => (BigInteger)b,
        int i         => (BigInteger)i,
        long l        => (BigInteger)l,
        Neo.VM.Types.PrimitiveType pt => new BigInteger(pt.GetSpan()),
        _ => BigInteger.Parse(item?.ToString() ?? "0")
    };

    // ── Task 5.4: GAS balance helper ──────────────────────────────────────────

    private BigInteger GasBalanceOf(UInt160 address)
    {
        if (address == null || address == UInt160.Zero) return BigInteger.Zero;
        return _context.Engine.Native.GAS.BalanceOf(address) ?? BigInteger.Zero;
    }

    /// <summary>
    /// Funds a wallet with native GAS from the pre-funded committee or validators address.
    /// TestEngine wallets start with 0 native GAS; this enables update fee collection tests.
    /// The validators signer is set temporarily; callers must set their own signer after this.
    /// </summary>
    private void FundWalletWithGas(UInt160 walletAddress, BigInteger datoshi)
    {
        // Try CommitteeAddress first (receives genesis GAS in InitializeContract).
        // Fall back to ValidatorsAddress if committee has insufficient funds.
        foreach (var funder in new[] { _context.Engine.CommitteeAddress, _context.Engine.ValidatorsAddress })
        {
            var funderBalance = _context.Engine.Native.GAS.BalanceOf(funder) ?? BigInteger.Zero;
            if (funderBalance < datoshi) continue;

            var funderSigner = new Signer { Account = funder, Scopes = WitnessScope.CalledByEntry };
            _context.Engine.SetTransactionSigners(funderSigner);
            var ok = _context.Engine.Native.GAS.Transfer(funder, walletAddress, datoshi, null);
            if (ok == true) return;
        }
        // Diagnostic: both sources failed — report actual balances in test failure
        var committeeBalance  = _context.Engine.Native.GAS.BalanceOf(_context.Engine.CommitteeAddress)  ?? BigInteger.Zero;
        var validatorsBalance = _context.Engine.Native.GAS.BalanceOf(_context.Engine.ValidatorsAddress) ?? BigInteger.Zero;
        Assert.Fail(
            $"FundWalletWithGas({datoshi}) failed — no pre-funded source available. " +
            $"CommitteeBalance={committeeBalance}, ValidatorsBalance={validatorsBalance}");
    }

    // ── Task 5.4: Background and setup steps (Given) ──────────────────────────

    /// <summary>
    /// Creates a community token MYTOK with 1,000,000 initial supply via the factory.
    /// Stores the hash in _namedTokens["MYTOK"] and _context.LastCreatedTokenHash.
    /// </summary>
    [Given("walletA has created community token MYTOK")]
    public void GivenWalletAHasCreatedCommunityTokenMYTOK()
    {
        var tokenData = new object[]
        {
            "My Token", "MYTOK", (BigInteger)1_000_000, (BigInteger)8,
            "community", "https://cdn.hushnetwork.social/mytok.png", (BigInteger)0
        };
        SimulateGasPayment("walletA", 1_500_000_000, tokenData);
        Assert.That(_context.LastException, Is.Null,
            $"MYTOK creation failed: {_context.LastException?.Message}");
        _namedTokens["MYTOK"] = _context.LastCreatedTokenHash;
    }

    /// <summary>
    /// Deploys a TokenTemplate directly with mintable=false.
    /// NOT registered in factory registry — factory.MintTokens will abort ("Token not found").
    /// This simulates a non-mintable token from the factory lifecycle perspective.
    /// </summary>
    [Given("walletA has created non-mintable token NOMIN")]
    public void GivenWalletAHasCreatedNonMintableTokenNOMIN()
    {
        var nef      = Neo.SmartContract.NefFile.Parse(File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef")));
        var manifest = ContractManifest.Parse(File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json")));
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        var walletA = GetOrCreateWallet("walletA").Account;
        // Deploy directly with mintable=0; authorizedFactory=walletA so deploy doesn't reject zero.
        var nomParams = new object[]
        {
            "No Mint Token", "NOMIN", (BigInteger)0, (BigInteger)8, walletA,
            (BigInteger)0,  // mintable = false
            (BigInteger)0, (BigInteger)0, "", (BigInteger)0,
            walletA, (BigInteger)0, (BigInteger)0
        };
        var nomToken = _context.Engine.Deploy<TokenTemplateContract>(nef, manifest, nomParams);
        _namedTokens["NOMIN"] = nomToken.Hash;
    }

    /// <summary>
    /// Locks the named token (e.g. MYTOK) via the factory as walletA (the creator).
    /// Used as a Given setup step for "fails on locked token" scenarios.
    /// </summary>
    [Given(@"walletA has locked the (\w+) token via factory")]
    public void GivenWalletAHasLockedTheTokenViaFactory(string tokenName)
    {
        var tokenHash = _namedTokens[tokenName];
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        _context.LastException = null;
        try { _context.Factory!.LockToken(tokenHash); }
        catch (Exception ex) { _context.LastException = ex; }
        Assert.That(_context.LastException, Is.Null,
            $"LockToken ({tokenName}) failed in Given: {_context.LastException?.Message}");
    }

    /// <summary>
    /// Sets the MYTOK mode to the given value before the actual test step.
    /// Used in ChangeTokenMode transition scenarios.
    /// </summary>
    [Given(@"the token mode is ""(\w+)""")]
    public void GivenTheTokenModeIs(string mode)
    {
        var tokenHash = _namedTokens["MYTOK"];
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        _context.LastException = null;
        try { _context.Factory!.ChangeTokenMode(tokenHash, mode, null); }
        catch (Exception ex) { _context.LastException = ex; }
        Assert.That(_context.LastException, Is.Null,
            $"ChangeTokenMode to '{mode}' failed in Given: {_context.LastException?.Message}");
    }

    // ── Task 5.4: MintTokens (When) ──────────────────────────────────────────

    /// <summary>Calls factory.MintTokens on MYTOK from the specified wallet.</summary>
    [When(@"(\w+) calls factory MintTokens (\d+) to (\w+)")]
    public void WalletCallsFactoryMintTokens(string fromWallet, long amount, string toWallet)
    {
        var tokenHash = _namedTokens["MYTOK"];
        // Fund caller with native GAS so the factory's update-fee GAS.Transfer succeeds.
        FundWalletWithGas(GetOrCreateWallet(fromWallet).Account, 500_000_000);
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        // Use WitnessScope.Global: factory calls GAS.Transfer(creator,...) as a nested call,
        // so CalledByEntry scope would make CheckWitness(creator) return false there.
        var mintSigner = GetOrCreateWallet(fromWallet);
        _context.Engine.SetTransactionSigners(new Signer { Account = mintSigner.Account, Scopes = WitnessScope.Global });
        _context.LastException = null;
        try
        {
            _context.Factory!.MintTokens(
                tokenHash, GetOrCreateWallet(toWallet).Account, (BigInteger)amount);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    /// <summary>Calls factory.MintTokens on a named token other than MYTOK.</summary>
    [When(@"(\w+) calls factory MintTokens on (\w+) (\d+) to (\w+)")]
    public void WalletCallsFactoryMintTokensOnToken(string fromWallet, string tokenName, long amount, string toWallet)
    {
        var tokenHash = _namedTokens[tokenName];
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        _context.LastException = null;
        try
        {
            _context.Factory!.MintTokens(
                tokenHash, GetOrCreateWallet(toWallet).Account, (BigInteger)amount);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.4: SetTokenBurnRate (When) ─────────────────────────────────────

    [When(@"(\w+) calls factory SetTokenBurnRate (\d+)")]
    public void WalletCallsFactorySetTokenBurnRate(string fromWallet, long bps)
    {
        var tokenHash = _namedTokens["MYTOK"];
        // Fund caller so the factory's update-fee GAS.Transfer succeeds.
        FundWalletWithGas(GetOrCreateWallet(fromWallet).Account, 500_000_000);
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        // Use WitnessScope.Global: factory calls GAS.Transfer(creator,...) as a nested call.
        var burnSigner = GetOrCreateWallet(fromWallet);
        _context.Engine.SetTransactionSigners(new Signer { Account = burnSigner.Account, Scopes = WitnessScope.Global });
        _context.LastException = null;
        try { _context.Factory!.SetTokenBurnRate(tokenHash, (BigInteger)bps); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.4: SetTokenMaxSupply (When) ────────────────────────────────────

    [When(@"(\w+) calls factory SetTokenMaxSupply (\d+)")]
    public void WalletCallsFactorySetTokenMaxSupply(string fromWallet, long newMax)
    {
        var tokenHash = _namedTokens["MYTOK"];
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        _context.LastException = null;
        try { _context.Factory!.SetTokenMaxSupply(tokenHash, (BigInteger)newMax); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.4: UpdateTokenMetadata (When) ─────────────────────────────────

    [When(@"(\w+) calls factory UpdateTokenMetadata ""(.*)""")]
    public void WalletCallsFactoryUpdateTokenMetadata(string fromWallet, string imageUrl)
    {
        var tokenHash = _namedTokens["MYTOK"];
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        _context.LastException = null;
        try { _context.Factory!.UpdateTokenMetadata(tokenHash, imageUrl); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.4: SetCreatorFee (When) ───────────────────────────────────────

    [When(@"(\w+) calls factory SetCreatorFee (\d+)")]
    public void WalletCallsFactorySetCreatorFee(string fromWallet, long newRate)
    {
        var tokenHash = _namedTokens["MYTOK"];
        // Fund caller so the factory's update-fee GAS.Transfer succeeds.
        FundWalletWithGas(GetOrCreateWallet(fromWallet).Account, 500_000_000);
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        // Use WitnessScope.Global: factory calls GAS.Transfer(creator,...) as a nested call.
        var feeSigner = GetOrCreateWallet(fromWallet);
        _context.Engine.SetTransactionSigners(new Signer { Account = feeSigner.Account, Scopes = WitnessScope.Global });
        _context.LastException = null;
        try { _context.Factory!.SetCreatorFee(tokenHash, (BigInteger)newRate); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.4: ChangeTokenMode (When) ─────────────────────────────────────

    [When(@"(\w+) calls factory ChangeTokenMode to ""(\w+)""")]
    public void WalletCallsFactoryChangeTokenMode(string fromWallet, string newMode)
    {
        var tokenHash = _namedTokens["MYTOK"];
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        _context.LastException = null;
        try { _context.Factory!.ChangeTokenMode(tokenHash, newMode, null); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"(\w+) calls factory ChangeTokenMode to ""(\w+)"" with params")]
    public void WalletCallsFactoryChangeTokenModeWithParams(string fromWallet, string newMode)
    {
        var tokenHash  = _namedTokens["MYTOK"];
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        _context.LastException = null;
        // Sample mode params (e.g. speculation thresholds)
        var modeParams = new object[] { (BigInteger)1000, (BigInteger)5000 };
        try { _context.Factory!.ChangeTokenMode(tokenHash, newMode, modeParams); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.4: LockToken (When) ────────────────────────────────────────────

    [When(@"(\w+) calls factory LockToken")]
    public void WalletCallsFactoryLockToken(string fromWallet)
    {
        var tokenHash = _namedTokens["MYTOK"];
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        _context.LastException = null;
        try { _context.Factory!.LockToken(tokenHash); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"(\w+) calls factory ApplyTokenChanges metadata ""(.*)""")]
    public void WalletCallsFactoryApplyTokenChangesMetadata(string fromWallet, string imageUrl)
    {
        CallApplyTokenChanges(fromWallet, imageUrl: imageUrl);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges burnRate (\d+)")]
    public void WalletCallsFactoryApplyTokenChangesBurnRate(string fromWallet, long burnRate)
    {
        CallApplyTokenChanges(fromWallet, burnRate: burnRate);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges maxSupply (\d+)")]
    public void WalletCallsFactoryApplyTokenChangesMaxSupply(string fromWallet, long maxSupply)
    {
        CallApplyTokenChanges(fromWallet, newMaxSupply: maxSupply);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges creatorFee (\d+)")]
    public void WalletCallsFactoryApplyTokenChangesCreatorFee(string fromWallet, long creatorFeeRate)
    {
        CallApplyTokenChanges(fromWallet, creatorFeeRate: creatorFeeRate);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges mode ""(\w+)""")]
    public void WalletCallsFactoryApplyTokenChangesMode(string fromWallet, string mode)
    {
        CallApplyTokenChanges(fromWallet, newMode: mode);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges mint (\d+) to (\w+)")]
    public void WalletCallsFactoryApplyTokenChangesMint(string fromWallet, long mintAmount, string toWallet)
    {
        CallApplyTokenChanges(
            fromWallet,
            mintTo: GetOrCreateWallet(toWallet).Account,
            mintAmount: mintAmount);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges with 2 changes metadata ""(.*)"" and burnRate (\d+)")]
    public void WalletCallsFactoryApplyTokenChangesTwo(string fromWallet, string imageUrl, long burnRate)
    {
        CallApplyTokenChanges(fromWallet, imageUrl: imageUrl, burnRate: burnRate);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges with 3 changes creatorFee (\d+) burnRate (\d+) and mint (\d+) to (\w+)")]
    public void WalletCallsFactoryApplyTokenChangesThree(
        string fromWallet, long creatorFeeRate, long burnRate, long mintAmount, string toWallet)
    {
        CallApplyTokenChanges(
            fromWallet,
            creatorFeeRate: creatorFeeRate,
            burnRate: burnRate,
            mintTo: GetOrCreateWallet(toWallet).Account,
            mintAmount: mintAmount);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges with maxSupply (\d+) and mint (\d+) to (\w+)")]
    public void WalletCallsFactoryApplyTokenChangesMaxSupplyAndMint(
        string fromWallet, long maxSupply, long mintAmount, string toWallet)
    {
        CallApplyTokenChanges(
            fromWallet,
            newMaxSupply: maxSupply,
            mintTo: GetOrCreateWallet(toWallet).Account,
            mintAmount: mintAmount);
    }

    [When(@"(\w+) calls factory ApplyTokenChanges metadata ""(.*)"" and lock")]
    public void WalletCallsFactoryApplyTokenChangesMetadataAndLock(string fromWallet, string imageUrl)
    {
        CallApplyTokenChanges(fromWallet, imageUrl: imageUrl, lockToken: true);
    }

    [Given(@"(\w+) has applied metadata update and lock atomically on (\w+)")]
    public void GivenWalletHasAppliedMetadataAndLockAtomically(string fromWallet, string tokenName)
    {
        Assert.That(_namedTokens.ContainsKey(tokenName), Is.True, $"Unknown token alias: {tokenName}");
        var tokenHash = _namedTokens[tokenName];
        var signer = GetOrCreateWallet(fromWallet);
        FundWalletWithGas(signer.Account, 500_000_000);
        _context.Engine.SetTransactionSigners(new Signer { Account = signer.Account, Scopes = WitnessScope.Global });
        _context.LastException = null;
        try
        {
            _context.Factory!.ApplyTokenChanges(
                tokenHash,
                "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme",
                (BigInteger)(-1),
                (BigInteger)(-1),
                "",
                new object[0],
                (BigInteger)(-1),
                UInt160.Zero,
                (BigInteger)0,
                true);
        }
        catch (Exception ex) { _context.LastException = ex; }

        Assert.That(_context.LastException, Is.Null,
            $"Atomic metadata+lock setup failed: {_context.LastException?.Message}");
    }

    [When(@"(\w+) calls factory ApplyTokenChanges with 4 changes metadata ""(.*)"" burnRate (\d+) creatorFee (\d+) and mode ""(\w+)""")]
    public void WalletCallsFactoryApplyTokenChangesFour(
        string fromWallet, string imageUrl, long burnRate, long creatorFeeRate, string mode)
    {
        CallApplyTokenChanges(
            fromWallet,
            imageUrl: imageUrl,
            burnRate: burnRate,
            creatorFeeRate: creatorFeeRate,
            newMode: mode);
    }

    // ── Task 5.4: Batch admin methods (When) ──────────────────────────────────

    [When(@"the owner calls factory AuthorizeAllTokens (\w+) offset (\d+) batchSize (\d+)")]
    public void OwnerCallsFactoryAuthorizeAllTokens(string newFactoryWallet, long offset, long batchSize)
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        var newFactoryAddr = GetOrCreateWallet(newFactoryWallet).Account;
        _context.LastException = null;
        try
        {
            _context.Factory!.AuthorizeAllTokens(
                newFactoryAddr, (BigInteger)offset, (BigInteger)batchSize);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"(\w+) calls factory AuthorizeAllTokens (\w+) offset (\d+) batchSize (\d+)")]
    public void WalletCallsFactoryAuthorizeAllTokens(
        string fromWallet, string newFactoryWallet, long offset, long batchSize)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        var newFactoryAddr = GetOrCreateWallet(newFactoryWallet).Account;
        _context.LastException = null;
        try
        {
            _context.Factory!.AuthorizeAllTokens(
                newFactoryAddr, (BigInteger)offset, (BigInteger)batchSize);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"the owner calls factory SetAllTokensPlatformFee (\d+) offset (\d+) batchSize (\d+)")]
    public void OwnerCallsFactorySetAllTokensPlatformFee(long newRate, long offset, long batchSize)
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.LastException = null;
        try
        {
            _context.Factory!.SetAllTokensPlatformFee(
                (BigInteger)newRate, (BigInteger)offset, (BigInteger)batchSize);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.4: Token balance on named token (Then) ─────────────────────────

    [Then(@"(\w+)'s token balance on (\w+) is (\d+)")]
    public void ThenWalletTokenBalanceOnNamedTokenIs(string wallet, string tokenName, long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var tokenHash = _namedTokens[tokenName];
        var token     = _context.Engine.FromHash<TokenTemplateContract>(tokenHash, true);
        Assert.That(token.BalanceOf(GetOrCreateWallet(wallet).Account),
            Is.EqualTo((BigInteger)expected));
    }

    // ── Task 5.4: Factory GAS delta assertions (Then) ─────────────────────────

    [Then("the factory GAS balance increased from the operation")]
    public void ThenFactoryGasBalanceIncreased()
    {
        var current = GasBalanceOf(_context.Factory!.Hash);
        Assert.That(current, Is.GreaterThan(_factoryGasBefore),
            $"Expected factory GAS balance to increase " +
            $"(before={_factoryGasBefore}, current={current})");
    }

    [Then("the factory GAS balance did not increase from the operation")]
    public void ThenFactoryGasBalanceDidNotIncrease()
    {
        var current = GasBalanceOf(_context.Factory!.Hash);
        Assert.That(current, Is.EqualTo(_factoryGasBefore),
            $"Expected factory GAS balance to stay the same " +
            $"(before={_factoryGasBefore}, current={current})");
    }

    // ── Task 5.4: Registry field assertions (Then) ────────────────────────────

    [Then(@"the registry token burn rate is (\d+)")]
    public void ThenRegistryTokenBurnRateIs(long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var info = _context.Factory!.GetToken(_namedTokens["MYTOK"]);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        // tokenInfo[7] = burnRate
        Assert.That(ParseBigInteger(info![7]), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"the registry token max supply is (\d+)")]
    public void ThenRegistryTokenMaxSupplyIs(long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var info = _context.Factory!.GetToken(_namedTokens["MYTOK"]);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        // tokenInfo[8] = maxSupply
        Assert.That(ParseBigInteger(info![8]), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"the registry token supply is (\d+)")]
    public void ThenRegistryTokenSupplyIs(long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var info = _context.Factory!.GetToken(_namedTokens["MYTOK"]);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        Assert.That(ParseBigInteger(info![2]), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"the token creator fee rate is (\d+)")]
    public void ThenTokenCreatorFeeRateIs(long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var tokenHash = _namedTokens["MYTOK"];
        var token = _context.Engine.FromHash<TokenTemplateContract>(tokenHash, true);
        Assert.That(token.getCreatorFeeRate(), Is.EqualTo((BigInteger)expected));
    }

    [Then("the registry token is locked")]
    public void ThenRegistryTokenIsLocked()
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var info = _context.Factory!.GetToken(_namedTokens["MYTOK"]);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        // tokenInfo[9] = locked (1 = locked)
        Assert.That(ParseBigInteger(info![9]), Is.EqualTo(BigInteger.One),
            "Expected token to be locked (tokenInfo[9] == 1)");
    }

    [Then(@"the registry token mode is ""(\w+)""")]
    public void ThenRegistryTokenModeIs(string expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var info = _context.Factory!.GetToken(_namedTokens["MYTOK"]);
        Assert.That(info, Is.Not.Null, "GetToken returned null");
        // tokenInfo[3] = mode
        Assert.That(ParseString(info![3]), Is.EqualTo(expected));
    }

    [Then("the mode params are stored for MYTOK")]
    public void ThenModeParamsAreStoredForMYTOK()
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var modeParams = _context.Factory!.GetModeParams(_namedTokens["MYTOK"]);
        Assert.That(modeParams, Is.Not.Null, "Expected modeParams to be stored but got null");
        Assert.That(modeParams!.Length, Is.GreaterThan(0), "Expected non-empty modeParams");
    }

    // ── Task 5.4: Token contract property assertions (Then) ──────────────────

    [Then(@"(\w+)'s authorizedFactory is (\w+)'s address")]
    public void ThenTokenAuthorizedFactoryIs(string tokenName, string wallet)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var tokenHash = _namedTokens[tokenName];
        var token     = _context.Engine.FromHash<TokenTemplateContract>(tokenHash, true);
        var expected  = GetOrCreateWallet(wallet).Account;
        Assert.That(token.getAuthorizedFactory(), Is.EqualTo(expected),
            $"Expected {tokenName}.authorizedFactory == {wallet} ({expected})");
    }

    [Then(@"(\w+)'s platformFeeRate is (\d+)")]
    public void ThenTokenPlatformFeeRateIs(string tokenName, long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        var tokenHash = _namedTokens[tokenName];
        var token     = _context.Engine.FromHash<TokenTemplateContract>(tokenHash, true);
        Assert.That(token.getPlatformFeeRate(), Is.EqualTo((BigInteger)expected));
    }

    private void CallApplyTokenChanges(
        string fromWallet,
        string imageUrl = "",
        long burnRate = -1,
        long creatorFeeRate = -1,
        string newMode = "",
        long newMaxSupply = -1,
        UInt160? mintTo = null,
        long mintAmount = 0,
        bool lockToken = false)
    {
        var tokenHash = _namedTokens["MYTOK"];
        FundWalletWithGas(GetOrCreateWallet(fromWallet).Account, 500_000_000);
        _factoryGasBefore = GasBalanceOf(_context.Factory!.Hash);
        var signer = GetOrCreateWallet(fromWallet);
        _context.Engine.SetTransactionSigners(new Signer { Account = signer.Account, Scopes = WitnessScope.Global });
        _context.LastException = null;
        try
        {
            _context.Factory!.ApplyTokenChanges(
                tokenHash,
                imageUrl,
                (BigInteger)burnRate,
                (BigInteger)creatorFeeRate,
                newMode,
                new object[0],
                (BigInteger)newMaxSupply,
                mintTo ?? UInt160.Zero,
                (BigInteger)mintAmount,
                lockToken);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }
}
