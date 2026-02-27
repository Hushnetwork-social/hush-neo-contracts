#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
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
/// Domain step definitions for Phase 6 feature files:
/// MintBurn, OwnerManagement, UpgradeableLock, Pausable, EdgeCases.
/// Covers: deploy with multiple flags, mint, burn, setOwner, lock, pause, setPausable.
/// </summary>
[Binding]
public class DomainSteps
{
    private readonly TestContext _context;
    private readonly ContractSteps _contractSteps;

    public DomainSteps(TestContext context, ContractSteps contractSteps)
    {
        _context = context;
        _contractSteps = contractSteps;
    }

    // ── Wallet helpers ────────────────────────────────────────────────────────

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

    private void SignAs(string walletName) =>
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(walletName));

    // ── Deploy steps (multi-flag variants) ────────────────────────────────────

    [Given(@"the contract is deployed with owner walletA")]
    public void GivenDeployedWithOwnerWalletA()
    {
        _contractSteps.DeployWith(new DeployParams { Owner = WalletAddress("walletA") });
    }

    [Given(@"the contract is deployed with owner walletA, mintable (\w+)")]
    public void GivenDeployedWithOwnerAndMintable(string mintableStr)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner    = WalletAddress("walletA"),
            Mintable = mintableStr == "true" ? 1 : 0
        });
    }

    [Given(@"the contract is deployed with owner walletA, mintable (\w+), initialSupply (\d+)")]
    public void GivenDeployedWithOwnerMintableSupply(string mintableStr, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner         = WalletAddress("walletA"),
            Mintable      = mintableStr == "true" ? 1 : 0,
            InitialSupply = (BigInteger)supply
        });
    }

    [Given(@"the contract is deployed with owner walletA, mintable (\w+), initialSupply (\d+), maxSupply (\d+)")]
    public void GivenDeployedWithOwnerMintableSupplyMax(string mintableStr, long supply, long maxSupply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner         = WalletAddress("walletA"),
            Mintable      = mintableStr == "true" ? 1 : 0,
            InitialSupply = (BigInteger)supply,
            MaxSupply     = (BigInteger)maxSupply
        });
    }

    [Given(@"the contract is deployed with owner walletA, upgradeable (\w+)")]
    public void GivenDeployedWithOwnerUpgradeable(string upgradeableStr)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner       = WalletAddress("walletA"),
            Upgradeable = upgradeableStr == "true" ? 1 : 0
        });
    }

    [Given(@"the contract is deployed with owner walletA, upgradeable (\w+), pausable (\w+)")]
    public void GivenDeployedWithOwnerUpgradeablePausable(string upgradeableStr, string pausableStr)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner       = WalletAddress("walletA"),
            Upgradeable = upgradeableStr == "true" ? 1 : 0,
            Pausable    = pausableStr == "true" ? 1 : 0
        });
    }

    [Given(@"the contract is deployed with owner walletA, pausable (\w+)")]
    public void GivenDeployedWithOwnerPausable(string pausableStr)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner    = WalletAddress("walletA"),
            Pausable = pausableStr == "true" ? 1 : 0
        });
    }

    [Given(@"the contract is deployed with owner walletA, pausable (\w+), initialSupply (\d+)")]
    public void GivenDeployedWithOwnerPausableSupply(string pausableStr, long supply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner         = WalletAddress("walletA"),
            Pausable      = pausableStr == "true" ? 1 : 0,
            InitialSupply = (BigInteger)supply
        });
    }

    // ── Deploy steps that expect failure ──────────────────────────────────────

    [When(@"deploying the contract with decimals (\d+)")]
    public void WhenDeployingWithDecimalsThatMayFail(int decimals)
    {
        _context.LastException = null;
        try
        {
            _contractSteps.DeployWith(new DeployParams
            {
                Decimals = (BigInteger)decimals,
                Owner    = WalletAddress("walletA")
            });
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"deploying the contract with symbol """"")]
    public void WhenDeployingWithEmptySymbol()
    {
        _context.LastException = null;
        try
        {
            _contractSteps.DeployWith(new DeployParams
            {
                Symbol = "",
                Owner  = WalletAddress("walletA")
            });
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When("deploying the contract with zero address as owner")]
    public void WhenDeployingWithZeroOwner()
    {
        _context.LastException = null;
        try
        {
            _contractSteps.DeployWith(new DeployParams { Owner = UInt160.Zero });
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [Then("the deploy is aborted")]
    public void ThenDeployIsAborted()
    {
        Assert.That(_context.LastException, Is.Not.Null,
            "Expected deploy to be aborted (exception), but no exception was thrown.");
    }

    // ── Burn steps ────────────────────────────────────────────────────────────

    [When(@"walletA calls burn (\d+)")]
    public void WhenWalletACallsBurn(long amount)
    {
        SignAs("walletA");
        _context.LastException = null;
        try { _context.Contract!.burn((BigInteger)amount); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Mint steps ────────────────────────────────────────────────────────────

    [When(@"the owner calls mint (\w+) (\d+)")]
    public void WhenOwnerCallsMint(string targetWallet, long amount)
    {
        SignAs("walletA");
        _context.LastException = null;
        try { _context.Contract!.mint(WalletAddress(targetWallet), (BigInteger)amount); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"walletA calls mint (\w+) (\d+)")]
    public void WhenWalletACallsMint(string targetWallet, long amount)
    {
        SignAs("walletA");
        _context.LastException = null;
        try { _context.Contract!.mint(WalletAddress(targetWallet), (BigInteger)amount); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"walletB calls mint (\w+) (\d+)")]
    public void WhenWalletBCallsMint(string targetWallet, long amount)
    {
        SignAs("walletB");
        _context.LastException = null;
        try { _context.Contract!.mint(WalletAddress(targetWallet), (BigInteger)amount); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Owner management steps ────────────────────────────────────────────────

    [When(@"walletA calls setOwner walletB")]
    public void WhenWalletACallsSetOwnerWalletB()
    {
        SignAs("walletA");
        _context.LastException = null;
        try { _context.Contract!.setOwner(WalletAddress("walletB")); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"walletB calls setOwner walletC")]
    public void WhenWalletBCallsSetOwnerWalletC()
    {
        SignAs("walletB");
        _context.LastException = null;
        try { _context.Contract!.setOwner(WalletAddress("walletC")); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"walletA calls setOwner zero")]
    public void WhenWalletACallsSetOwnerZero()
    {
        SignAs("walletA");
        _context.LastException = null;
        try { _context.Contract!.setOwner(UInt160.Zero); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [Given("walletA has renounced ownership")]
    public void GivenWalletARenounced()
    {
        SignAs("walletA");
        _context.Contract!.setOwner(UInt160.Zero);
    }

    [Then(@"getOwner\(\) is walletB")]
    public void ThenGetOwnerIsWalletB()
    {
        Assert.That(_context.Contract!.getOwner(), Is.EqualTo(WalletAddress("walletB")));
    }

    [Then(@"getOwner\(\) is zero address")]
    public void ThenGetOwnerIsZeroAddress()
    {
        Assert.That(_context.Contract!.getOwner(), Is.EqualTo(UInt160.Zero));
    }

    // ── Lock steps ────────────────────────────────────────────────────────────

    [When("the owner calls lock")]
    public void WhenOwnerCallsLock()
    {
        SignAs("walletA");
        _context.LastException = null;
        // FEAT-078: Lock() now requires CallingScriptHash == authorizedFactory.
        // In test deployments, authorizedFactory == owner (DeployParams fallback).
        _context.Engine.OnGetCallingScriptHash = (_, _) => _context.OwnerSigner.Account;
        try { _context.Contract!.Lock(); }
        catch (Exception ex) { _context.LastException = ex; }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    [When(@"walletB calls lock")]
    public void WhenWalletBCallsLock()
    {
        SignAs("walletB");
        _context.LastException = null;
        // No CallingScriptHash mock — walletB is not the authorizedFactory; should be rejected.
        try { _context.Contract!.Lock(); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [Given("the owner has locked the contract")]
    public void GivenOwnerHasLockedContract()
    {
        SignAs("walletA");
        // lock() requires CallingScriptHash == authorizedFactory.
        // When deployed without an explicit factory, factory defaults to owner (walletA).
        // When deployed with an explicit factory (e.g. walletB), we must use that factory hash.
        var factory = _context.Contract!.getAuthorizedFactory() ?? _context.OwnerSigner.Account;
        _context.Engine.OnGetCallingScriptHash = (_, _) => factory;
        try { _context.Contract!.Lock(); }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    // ── Pause steps ───────────────────────────────────────────────────────────

    [When("the owner calls pause")]
    public void WhenOwnerCallsPause()
    {
        SignAs("walletA");
        _context.LastException = null;
        _context.Engine.OnGetCallingScriptHash = (_, _) => _context.OwnerSigner.Account;
        try { _context.Contract!.pause(); }
        catch (Exception ex) { _context.LastException = ex; }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    [When("the owner calls unpause")]
    public void WhenOwnerCallsUnpause()
    {
        SignAs("walletA");
        _context.LastException = null;
        _context.Engine.OnGetCallingScriptHash = (_, _) => _context.OwnerSigner.Account;
        try { _context.Contract!.unpause(); }
        catch (Exception ex) { _context.LastException = ex; }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    [When(@"walletB calls pause")]
    public void WhenWalletBCallsPause()
    {
        SignAs("walletB");
        _context.LastException = null;
        // No CallingScriptHash mock — walletB is not the authorizedFactory; should be rejected.
        try { _context.Contract!.pause(); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"the owner calls setPausable (\w+)")]
    public void WhenOwnerCallsSetPausable(string valueStr)
    {
        SignAs("walletA");
        bool value = valueStr == "true";
        _context.LastException = null;
        _context.Engine.OnGetCallingScriptHash = (_, _) => _context.OwnerSigner.Account;
        try { _context.Contract!.setPausable(value); }
        catch (Exception ex) { _context.LastException = ex; }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    [Given("the owner has paused the contract")]
    public void GivenOwnerHasPausedContract()
    {
        SignAs("walletA");
        _context.Engine.OnGetCallingScriptHash = (_, _) => _context.OwnerSigner.Account;
        try { _context.Contract!.pause(); }
        finally { _context.Engine.OnGetCallingScriptHash = null; }
    }

    // ── Numeric assertion steps ────────────────────────────────────────────────

    [Then(@"totalSupply\(\) is (\d+)")]
    public void ThenTotalSupplyIs(long expected)
    {
        Assert.That(_context.Contract!.TotalSupply, Is.EqualTo((BigInteger)expected));
    }

    // ── Factory dependency guard steps ────────────────────────────────────────

    /// <summary>
    /// Verifies that onNEP17Payment always throws — the template never holds tokens.
    /// Critical for FEAT-070: the factory must not accidentally forward tokens to a deployed instance.
    /// </summary>
    [When("a NEP-17 transfer is sent to the contract")]
    public void WhenNep17TransferSentToContract()
    {
        _context.LastException = null;
        try
        {
            _context.Engine.SetTransactionSigners(_context.OwnerSigner);
            _context.Contract!.OnNEP17Payment(UInt160.Zero, 1_000_000_000, null);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    /// <summary>
    /// Deploys with initialSupply > maxSupply — should be rejected by the H1 guard added to _deploy().
    /// Critical for FEAT-070: factory must not produce tokens in an un-mintable broken state.
    /// </summary>
    [When(@"deploying the contract with initialSupply (\d+) and maxSupply (\d+)")]
    public void WhenDeployingWithInitialSupplyExceedingMaxSupply(long initialSupply, long maxSupply)
    {
        _context.LastException = null;
        try
        {
            _contractSteps.DeployWith(new DeployParams
            {
                Owner         = WalletAddress("walletA"),
                InitialSupply = (BigInteger)initialSupply,
                Mintable      = 1,
                MaxSupply     = (BigInteger)maxSupply
            });
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    /// <summary>
    /// Calls update() with deliberately different deploy parameters.
    /// The double-deploy guard (if update) return in _deploy()) must prevent re-initialization.
    /// Critical for FEAT-070: deployed token instances must not be re-initialized after a code upgrade.
    /// </summary>
    [When("the owner upgrades the contract with different deploy parameters")]
    public void WhenOwnerUpgradesWithDifferentDeployParams()
    {
        _context.LastException = null;
        try
        {
            var artifactsDir = Path.Combine(AppContext.BaseDirectory, "artifacts");
            var nef      = File.ReadAllBytes(Path.Combine(artifactsDir, "TokenTemplate.nef"));
            var manifest = File.ReadAllText(Path.Combine(artifactsDir, "TokenTemplate.manifest.json"));

            // If the double-deploy guard were absent, these params would overwrite storage
            var differentParams = new DeployParams
            {
                Name   = "CHANGED NAME",
                Symbol = "CHG",
                Owner  = _context.OwnerSigner.Account
            }.ToDeployArray();

            _context.Engine.SetTransactionSigners(_context.OwnerSigner);
            _context.Contract!.update(nef, manifest, differentParams);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }
}
