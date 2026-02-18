#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using Reqnroll;
using System;
using System.Numerics;
using TokenTemplate.Tests.Support;
using TestContext = TokenTemplate.Tests.Support.TestContext;

namespace TokenTemplate.Tests.Steps;

/// <summary>
/// Step definitions for NEP-17 compliance and multi-wallet scenarios.
/// Covers: deploy-with-owner, symbol/decimals/totalSupply/balanceOf/transfer.
/// </summary>
[Binding]
public class Nep17Steps
{
    private readonly TestContext _context;
    private readonly ContractSteps _contractSteps;

    public Nep17Steps(TestContext context, ContractSteps contractSteps)
    {
        _context = context;
        _contractSteps = contractSteps;
    }

    // ── Wallet helpers ────────────────────────────────────────────────────────

    /// <summary>Returns the signer for a named wallet, creating walletB/walletC on demand.</summary>
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

    // ── Deploy steps ──────────────────────────────────────────────────────────

    [Given(@"the contract is deployed with owner walletA, symbol ""(.*)"" and decimals (\d+)")]
    public void GivenDeployedWithOwnerSymbolDecimals(string symbol, int decimals)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Symbol   = symbol,
            Decimals = (BigInteger)decimals,
            Owner    = WalletAddress("walletA")
        });
    }

    [Given(@"the contract is deployed with owner walletA, initialSupply (\d+)")]
    public void GivenDeployedWithOwnerAndSupply(long initialSupply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            InitialSupply = (BigInteger)initialSupply,
            Owner         = WalletAddress("walletA")
        });
    }

    [Given(@"the contract is deployed with owner walletA, symbol ""(.*)"" and decimals (\d+) and initialSupply (\d+)")]
    public void GivenDeployedFull(string symbol, int decimals, long initialSupply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Symbol        = symbol,
            Decimals      = (BigInteger)decimals,
            InitialSupply = (BigInteger)initialSupply,
            Owner         = WalletAddress("walletA")
        });
    }

    // ── Invocation steps ──────────────────────────────────────────────────────

    [When("decimals\\(\\) is called")]
    public void WhenDecimalsIsCalled()
    {
        _context.LastException = null;
        try { _context.LastResult = _context.Contract!.Decimals; }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When("totalSupply\\(\\) is called")]
    public void WhenTotalSupplyIsCalled()
    {
        _context.LastException = null;
        try { _context.LastResult = _context.Contract!.TotalSupply; }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"balanceOf\(walletA\) is called")]
    public void WhenBalanceOfWalletAIsCalled()
    {
        _context.LastException = null;
        try { _context.LastResult = _context.Contract!.BalanceOf(WalletAddress("walletA")); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"balanceOf\(walletB\) is called")]
    public void WhenBalanceOfWalletBIsCalled()
    {
        _context.LastException = null;
        try { _context.LastResult = _context.Contract!.BalanceOf(WalletAddress("walletB")); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"walletA calls transfer to walletB amount (\d+)")]
    public void WhenWalletATransfersToWalletB(long amount)
    {
        _context.LastException = null;
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletA"));
        try
        {
            _context.LastResult = _context.Contract!.Transfer(
                WalletAddress("walletA"), WalletAddress("walletB"), (BigInteger)amount, null);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    [When(@"walletB calls transfer from walletA to walletB amount (\d+)")]
    public void WhenWalletBTriesToTransferFromWalletA(long amount)
    {
        _context.LastException = null;
        _context.Engine.SetTransactionSigners(GetOrCreateWallet("walletB"));
        try
        {
            _context.LastResult = _context.Contract!.Transfer(
                WalletAddress("walletA"), WalletAddress("walletB"), (BigInteger)amount, null);
        }
        catch (Exception ex) { _context.LastException = ex; }
    }

    // ── Assertion steps ───────────────────────────────────────────────────────

    [Then(@"the numeric result is (\d+)")]
    public void ThenNumericResultIs(long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Expected no exception but got: {_context.LastException?.Message}");
        // Unbox to BigInteger regardless of underlying type (byte, BigInteger, int, long)
        BigInteger actual = _context.LastResult switch
        {
            BigInteger bi => bi,
            byte b        => (BigInteger)b,
            int i         => (BigInteger)i,
            long l        => (BigInteger)l,
            _             => throw new InvalidCastException(
                $"Cannot convert {_context.LastResult?.GetType()} to BigInteger")
        };
        Assert.That(actual, Is.EqualTo((BigInteger)expected));
    }

    [Then(@"the boolean result is (.+)")]
    public void ThenBooleanResultIs(string expectedStr)
    {
        bool expected = expectedStr.Trim() == "true";
        Assert.That(_context.LastException, Is.Null,
            $"Expected no exception but got: {_context.LastException?.Message}");
        Assert.That(_context.LastResult, Is.EqualTo(expected));
    }

    [Then(@"balanceOf walletA is (\d+)")]
    public void ThenBalanceOfWalletAIs(long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        Assert.That(_context.Contract!.BalanceOf(WalletAddress("walletA")),
            Is.EqualTo((BigInteger)expected));
    }

    [Then(@"balanceOf walletB is (\d+)")]
    public void ThenBalanceOfWalletBIs(long expected)
    {
        Assert.That(_context.LastException, Is.Null,
            $"Previous step threw: {_context.LastException?.Message}");
        Assert.That(_context.Contract!.BalanceOf(WalletAddress("walletB")),
            Is.EqualTo((BigInteger)expected));
    }
}
