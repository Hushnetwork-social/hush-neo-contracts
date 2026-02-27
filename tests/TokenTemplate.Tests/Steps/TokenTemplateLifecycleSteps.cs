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
using TokenTemplate.Tests.Support;
using TestContext = TokenTemplate.Tests.Support.TestContext;

namespace TokenTemplate.Tests.Steps;

/// <summary>
/// Step definitions for FEAT-078 lifecycle BDD scenarios (Tasks 5.1, 5.2, 5.3).
///
/// Task 5.1: TokenTemplateLifecycleStorage.feature — new deploy params storage tests.
/// Task 5.2: TokenTemplateSetters.feature — SetBurnRate, SetMetadataUri, SetMaxSupply,
///           SetCreatorFee, SetPlatformFeeRate, AuthorizeFactory.
/// Task 5.3: TokenTemplateTransferFees.feature — 3-component fee system in Transfer().
///
/// Setter guard pattern (all setters):
///   Mock engine.OnGetCallingScriptHash = authorizedFactory address.
///   Call setter. Reset mock in finally block.
///   This simulates the factory contract calling the token setter method.
/// </summary>
[Binding]
public class TokenTemplateLifecycleSteps
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    private readonly TestContext _context;
    private readonly ContractSteps _contractSteps;

    // Captured GAS balances for before/after delta assertions in Task 5.3
    private readonly Dictionary<string, BigInteger> _gasBalanceBefore = new();

    // Stores the original (pre-AuthorizeFactory) authorizedFactory address for "old factory" test
    private UInt160 _originalFactory = UInt160.Zero;

    public TokenTemplateLifecycleSteps(TestContext context, ContractSteps contractSteps)
    {
        _context = context;
        _contractSteps = contractSteps;
    }

    // ── Wallet helpers ─────────────────────────────────────────────────────────

    private Signer GetOrCreateWallet(string name)
    {
        if (!_context.NamedSigners.TryGetValue(name, out var signer))
        {
            signer = TestEngine.GetNewSigner();
            _context.NamedSigners[name] = signer;
        }
        return signer;
    }

    private UInt160 WalletAddress(string name) => GetOrCreateWallet(name).Account;

    private BigInteger GasBalanceOf(UInt160 address)
    {
        if (address == UInt160.Zero) return BigInteger.Zero;
        var balance = _context.Engine.Native.GAS.BalanceOf(address);
        return balance ?? BigInteger.Zero;
    }

    // ── Raw deploy helper (bypasses DeployParams.ToDeployArray() fallback) ────

    private void DeployContractRaw(object[] rawArgs)
    {
        var nef      = Neo.SmartContract.NefFile.Parse(File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef")));
        var manifest = ContractManifest.Parse(File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json")));
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        using var watcher = _context.Engine.CreateGasWatcher();
        _context.Contract = _context.Engine.Deploy<TokenTemplateContract>(nef, manifest, rawArgs);
        _context.GasConsumedByLastDeploy = watcher.Value;
    }

    // ── Factory setter call helper ─────────────────────────────────────────────
    // Reads the current authorizedFactory from storage and mocks CallingScriptHash to it.

    private void CallAsFactory(Action action)
    {
        var factoryAddr = _context.Contract!.getAuthorizedFactory() ?? _context.OwnerSigner.Account;
        _context.LastException = null;
        _context.Engine.OnGetCallingScriptHash = (_, _) => factoryAddr;
        try { action(); }
        catch (Exception ex) { _context.LastException = ex; }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    // ── Task 5.1: Deploy steps ──────────────────────────────────────────────────

    [When("the contract is deployed with owner walletA and authorizedFactory walletB")]
    public void WhenDeployedWithOwnerAndAuthorizedFactoryWalletB()
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner             = WalletAddress("walletA"),
            AuthorizedFactory = WalletAddress("walletB")
        });
    }

    [When(@"the contract is deployed with owner walletA and platformFeeRate (\d+)")]
    public void WhenDeployedWithOwnerAndPlatformFeeRate(long rate)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner           = WalletAddress("walletA"),
            PlatformFeeRate = (BigInteger)rate
        });
    }

    [When(@"the contract is deployed with owner walletA and creatorFeeRate (\d+)")]
    public void WhenDeployedWithOwnerAndCreatorFeeRate(long rate)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner          = WalletAddress("walletA"),
            CreatorFeeRate = (BigInteger)rate
        });
    }

    [When("deploying the contract with zero authorizedFactory")]
    public void WhenDeployingWithZeroAuthorizedFactory()
    {
        _context.LastException = null;
        try
        {
            // Bypass DeployParams.ToDeployArray() fallback: pass UInt160.Zero directly as [10]
            var owner = WalletAddress("walletA");
            DeployContractRaw(new object[]
            {
                "Test", "TST", (BigInteger)0, (BigInteger)8, owner,
                (BigInteger)1, (BigInteger)0, (BigInteger)0, "", (BigInteger)0,
                UInt160.Zero, (BigInteger)0, (BigInteger)0
            });
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"deploying the contract with platformFeeRate (\d+)")]
    public void WhenDeployingWithPlatformFeeRate(long rate)
    {
        _context.LastException = null;
        try
        {
            _contractSteps.DeployWith(new DeployParams
            {
                Owner           = WalletAddress("walletA"),
                PlatformFeeRate = (BigInteger)rate
            });
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"deploying the contract with creatorFeeRate (\d+)")]
    public void WhenDeployingWithCreatorFeeRate(long rate)
    {
        _context.LastException = null;
        try
        {
            _contractSteps.DeployWith(new DeployParams
            {
                Owner          = WalletAddress("walletA"),
                CreatorFeeRate = (BigInteger)rate
            });
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When("getAuthorizedFactory\\(\\) is called")]
    public void WhenGetAuthorizedFactoryIsCalled()
    {
        _context.LastException = null;
        try { _context.LastResult = _context.Contract!.getAuthorizedFactory(); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.1: Assertion steps ───────────────────────────────────────────────

    [Then(@"getAuthorizedFactory\(\) returns (\w+)'s address")]
    public void ThenGetAuthorizedFactoryReturnsAddress(string wallet)
    {
        var expected = WalletAddress(wallet);
        Assert.That(_context.Contract!.getAuthorizedFactory(), Is.EqualTo(expected));
    }

    [Then(@"getPlatformFeeRate\(\) returns (\d+)")]
    public void ThenGetPlatformFeeRateReturns(long expected)
    {
        Assert.That(_context.Contract!.getPlatformFeeRate(), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"getCreatorFeeRate\(\) returns (\d+)")]
    public void ThenGetCreatorFeeRateReturns(long expected)
    {
        Assert.That(_context.Contract!.getCreatorFeeRate(), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"getBurnRate\(\) returns (\d+)")]
    public void ThenGetBurnRateReturns(long expected)
    {
        Assert.That(_context.Contract!.getBurnRate(), Is.EqualTo((BigInteger)expected));
    }

    // ── Task 5.2: Setup steps ───────────────────────────────────────────────────

    [Given("the contract is deployed for lifecycle setter tests")]
    public void GivenDeployedForLifecycleSetterTests()
    {
        // Deploy with owner walletA; AuthorizedFactory defaults to walletA (ToDeployArray fallback).
        _contractSteps.DeployWith(new DeployParams
        {
            Owner = WalletAddress("walletA")
        });
    }

    [Given(@"the contract is deployed for setter tests with initialSupply (\d+)")]
    public void GivenDeployedForSetterTestsWithInitialSupply(long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner         = WalletAddress("walletA"),
            Mintable      = 1,
            InitialSupply = (BigInteger)supply
        });
    }

    // ── Task 5.2: SetBurnRate steps ─────────────────────────────────────────────

    [When(@"the factory calls SetBurnRate with (\d+)")]
    public void WhenFactoryCallsSetBurnRate(long bps)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.SetBurnRate((BigInteger)bps));
    }

    [When(@"walletB calls SetBurnRate with (\d+)")]
    public void WhenWalletBCallsSetBurnRate(long bps)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletB"));
        _context.LastException = null;
        // No CallingScriptHash mock — walletB is not the authorized factory.
        try { _context.Contract!.SetBurnRate((BigInteger)bps); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When("the original factory calls SetBurnRate with (\\d+)")]
    public void WhenOriginalFactoryCallsSetBurnRate(long bps)
    {
        // After AuthorizeFactory(walletB), the original factory (walletA) is no longer authorized.
        // Call with walletA's address as CallingScriptHash — should be rejected.
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        _context.LastException = null;
        _context.Engine.OnGetCallingScriptHash = (_, _) => WalletAddress("walletA");
        try { _context.Contract!.SetBurnRate((BigInteger)bps); }
        catch (Exception ex) { _context.LastException = ex; }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    // ── Task 5.2: SetMetadataUri steps ──────────────────────────────────────────

    [When("the factory updates the metadata URI to \"(.*)\"")]
    public void WhenFactoryUpdatesMetadataUri(string uri)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.SetMetadataUri(uri));
    }

    // ── Task 5.2: SetMaxSupply steps ────────────────────────────────────────────

    [When(@"the factory sets max supply to (\d+)")]
    public void WhenFactorySetsMaxSupply(long newMax)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.SetMaxSupply((BigInteger)newMax));
    }

    // ── Task 5.2: SetCreatorFee steps ───────────────────────────────────────────

    [When(@"the factory sets creator fee to (\d+)")]
    public void WhenFactorySetsCreatorFee(long datoshi)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.SetCreatorFee((BigInteger)datoshi));
    }

    // ── Task 5.2: SetPlatformFeeRate steps ──────────────────────────────────────

    [When(@"the factory sets platform fee rate to (\d+)")]
    public void WhenFactorySetsPlatformFeeRate(long datoshi)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.SetPlatformFeeRate((BigInteger)datoshi));
    }

    // ── Task 5.2: AuthorizeFactory steps ────────────────────────────────────────

    [When("the factory authorizes walletB as new factory")]
    public void WhenFactoryAuthorizesWalletB()
    {
        _originalFactory = WalletAddress("walletA");  // Remember old factory
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.AuthorizeFactory(WalletAddress("walletB")));
    }

    [When("walletB calls AuthorizeFactory with walletC")]
    public void WhenWalletBCallsAuthorizeFactory()
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletB"));
        _context.LastException = null;
        // No CallingScriptHash mock — walletB is not the authorized factory.
        try { _context.Contract!.AuthorizeFactory(WalletAddress("walletC")); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When("the factory calls AuthorizeFactory with zero address")]
    public void WhenFactoryCallsAuthorizeFactoryWithZero()
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.AuthorizeFactory(UInt160.Zero));
    }

    // ── Task 5.2: Assertion steps ───────────────────────────────────────────────

    [Then(@"getAuthorizedFactory\(\) is walletB")]
    public void ThenGetAuthorizedFactoryIsWalletB()
    {
        Assert.That(_context.Contract!.getAuthorizedFactory(), Is.EqualTo(WalletAddress("walletB")));
    }

    // ── Task 5.3: Setup steps ───────────────────────────────────────────────────

    [Given(@"the contract is deployed with owner walletA, burn rate (\d+) bps, and initialSupply (\d+)")]
    public void GivenDeployedWithBurnRateAndInitialSupply(long bps, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner         = WalletAddress("walletA"),
            Mintable      = 1,
            InitialSupply = (BigInteger)supply
        });
        // Set burn rate via factory (walletA = authorizedFactory via fallback)
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        CallAsFactory(() => _context.Contract!.SetBurnRate((BigInteger)bps));
        Assert.That(_context.LastException, Is.Null, $"SetBurnRate failed: {_context.LastException?.Message}");
    }

    [Given(@"the contract is deployed with owner walletA, factory walletB, platformFeeRate (\d+), and initialSupply (\d+)")]
    public void GivenDeployedWithFactoryWalletBAndPlatformFee(long platformFee, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner             = WalletAddress("walletA"),
            AuthorizedFactory = WalletAddress("walletB"),
            Mintable          = 1,
            InitialSupply     = (BigInteger)supply,
            PlatformFeeRate   = (BigInteger)platformFee
        });
    }

    [Given(@"the contract is deployed with owner walletA, creatorFeeRate (\d+), and initialSupply (\d+)")]
    public void GivenDeployedWithCreatorFeeAndInitialSupply(long creatorFee, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner          = WalletAddress("walletA"),
            Mintable       = 1,
            InitialSupply  = (BigInteger)supply,
            CreatorFeeRate = (BigInteger)creatorFee
        });
    }

    [Given(@"the contract is deployed with owner walletA, factory walletB, platformFeeRate (\d+), burn rate (\d+) bps, and initialSupply (\d+)")]
    public void GivenDeployedWithFactoryAndBurnRateAndPlatformFee(long platformFee, long bps, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner             = WalletAddress("walletA"),
            AuthorizedFactory = WalletAddress("walletB"),
            Mintable          = 1,
            InitialSupply     = (BigInteger)supply,
            PlatformFeeRate   = (BigInteger)platformFee
        });
        // Set burn rate via walletB (the factory)
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        _context.Engine.OnGetCallingScriptHash = (_, _) => WalletAddress("walletB");
        try { _context.Contract!.SetBurnRate((BigInteger)bps); }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    [Given(@"the contract is deployed with owner walletA, factory walletB, mintable (\w+), and initialSupply (\d+)")]
    public void GivenDeployedWithFactoryAndMintable(string mintableStr, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner             = WalletAddress("walletA"),
            AuthorizedFactory = WalletAddress("walletB"),
            Mintable          = mintableStr == "true" ? 1 : 0,
            InitialSupply     = (BigInteger)supply
        });
    }

    [Given(@"the contract is deployed with owner walletA, factory walletB, maxSupply (\d+), and initialSupply (\d+)")]
    public void GivenDeployedWithFactoryAndMaxSupply(long maxSupply, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner             = WalletAddress("walletA"),
            AuthorizedFactory = WalletAddress("walletB"),
            Mintable          = 1,
            MaxSupply         = (BigInteger)maxSupply,
            InitialSupply     = (BigInteger)supply
        });
    }

    [Given(@"walletA mints (\d+) tokens to walletB")]
    public void GivenWalletAMints(long amount)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        _context.Contract!.mint(WalletAddress("walletB"), (BigInteger)amount);
    }

    // ── Task 5.3: GAS funding helper ────────────────────────────────────────────

    /// <summary>
    /// Funds a wallet with native GAS from the pre-funded committee or validators address.
    /// TestEngine wallets start with 0 native GAS; this enables GAS fee tests.
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

    // ── Task 5.3: Transfer steps ────────────────────────────────────────────────

    [When(@"(\w+) transfers (\d+) tokens to (\w+)")]
    public void WalletXTransfersToWalletY(string fromWallet, long amount, string toWallet)
    {
        // Pre-fund the sender with enough native GAS to pay platform/creator fees.
        // TestEngine wallets start with 0 GAS; without this, GAS.Transfer returns false silently.
        FundWalletWithGas(WalletAddress(fromWallet), 500_000_000); // 5 GAS — covers any fee config

        // Capture GAS balances BEFORE the transfer for delta assertions.
        _gasBalanceBefore.Clear();
        foreach (var (name, signer) in _context.NamedSigners)
            _gasBalanceBefore[name] = GasBalanceOf(signer.Account);
        var factoryAddr = _context.Contract?.getAuthorizedFactory() ?? UInt160.Zero;
        _gasBalanceBefore["__factory"] = GasBalanceOf(factoryAddr);

        // Use WitnessScope.Global so CheckWitness(from) returns true inside nested GAS.Transfer calls.
        // CalledByEntry only covers the entry contract; the GAS native's CheckWitness check is one level deeper.
        var fromSigner = GetOrCreateWallet(fromWallet);
        _context.Engine.SetTransactionSigners(new Signer { Account = fromSigner.Account, Scopes = WitnessScope.Global });
        _context.LastException = null;
        try
        {
            _context.Contract!.Transfer(
                WalletAddress(fromWallet), WalletAddress(toWallet), (BigInteger)amount, null);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"the authorized factory mints (\d+) tokens to (\w+)")]
    public void AuthorizedFactoryMintsTokens(long amount, string toWallet)
    {
        // Capture GAS balances BEFORE the mint.
        _gasBalanceBefore.Clear();
        foreach (var (name, signer) in _context.NamedSigners)
            _gasBalanceBefore[name] = GasBalanceOf(signer.Account);
        var factoryAddr = _context.Contract?.getAuthorizedFactory() ?? UInt160.Zero;
        _gasBalanceBefore["__factory"] = GasBalanceOf(factoryAddr);

        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        _context.LastException = null;
        _context.Engine.OnGetCallingScriptHash = (_, _) => factoryAddr;
        try { _context.Contract!.MintByFactory(WalletAddress(toWallet), (BigInteger)amount); }
        catch (Exception ex) { _context.LastException = ex; }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    [When(@"(\w+) calls MintByFactory (\d+) to (\w+)")]
    public void WalletCallsMintByFactory(string callerWallet, long amount, string toWallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(callerWallet));
        _context.LastException = null;
        // No CallingScriptHash override — caller is not the authorized factory.
        try { _context.Contract!.MintByFactory(WalletAddress(toWallet), (BigInteger)amount); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Task 5.3: Token balance assertions ─────────────────────────────────────

    [Then(@"(\w+)'s token balance is (\d+)")]
    public void ThenWalletTokenBalanceIs(string wallet, long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        Assert.That(_context.Contract!.BalanceOf(WalletAddress(wallet)),
            Is.EqualTo((BigInteger)expected));
    }

    // ── Task 5.3: GAS balance delta assertions ──────────────────────────────────

    [Then(@"(\w+)'s GAS balance increased by (\d+) datoshi from the transfer")]
    public void ThenWalletGasBalanceIncreasedBy(string wallet, long expectedDelta)
    {
        BigInteger currentBalance;
        BigInteger beforeBalance;

        if (wallet == "factory" || wallet == "__factory")
        {
            var factoryAddr = _context.Contract!.getAuthorizedFactory() ?? UInt160.Zero;
            currentBalance = GasBalanceOf(factoryAddr);
            beforeBalance  = _gasBalanceBefore.TryGetValue("__factory", out var b) ? b : 0;
        }
        else
        {
            currentBalance = GasBalanceOf(WalletAddress(wallet));
            beforeBalance  = _gasBalanceBefore.TryGetValue(wallet, out var b) ? b : 0;
        }

        Assert.That(currentBalance - beforeBalance, Is.EqualTo((BigInteger)expectedDelta),
            $"Expected {wallet}'s GAS balance to increase by {expectedDelta} datoshi, " +
            $"actual delta = {currentBalance - beforeBalance}");
    }
}
