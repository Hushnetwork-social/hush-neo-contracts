#nullable enable
using Neo;
using NUnit.Framework;
using Reqnroll;
using System.Numerics;
using TokenTemplate.Tests.Support;
using TestContext = TokenTemplate.Tests.Support.TestContext;

namespace TokenTemplate.Tests.Steps;

/// <summary>
/// Step definitions for StorageSchema.feature.
/// Each "deploy with X" step deploys the full contract with one param overridden.
/// Each "method() returns Y" step calls the getter and asserts the value.
/// </summary>
[Binding]
public class StorageSchemaSteps
{
    private readonly TestContext _context;
    private readonly ContractSteps _contractSteps;

    public StorageSchemaSteps(TestContext context, ContractSteps contractSteps)
    {
        _context = context;
        _contractSteps = contractSteps;
    }

    // ── Deploy-with-single-param steps ────────────────────────────────────────

    [When(@"the contract is deployed with name ""(.*)""")]
    public void WhenDeployedWithName(string name)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Name  = name,
            Owner = _context.OwnerSigner.Account
        });
    }

    [When(@"the contract is deployed with symbol ""(.*)""")]
    public void WhenDeployedWithSymbol(string symbol)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Symbol = symbol,
            Owner  = _context.OwnerSigner.Account
        });
    }

    [When(@"the contract is deployed with decimals (\d+)")]
    public void WhenDeployedWithDecimals(int decimals)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Decimals = (BigInteger)decimals,
            Owner    = _context.OwnerSigner.Account
        });
    }

    [When(@"the contract is deployed with maxSupply (\d+)")]
    public void WhenDeployedWithMaxSupply(long maxSupply)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            MaxSupply = (BigInteger)maxSupply,
            Owner     = _context.OwnerSigner.Account
        });
    }

    [When(@"the contract is deployed with mintable (.+)")]
    public void WhenDeployedWithMintable(string mintableStr)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Mintable = mintableStr == "true" ? 1 : 0,
            Owner    = _context.OwnerSigner.Account
        });
    }

    [When(@"the contract is deployed with upgradeable (.+)")]
    public void WhenDeployedWithUpgradeable(string upgradeableStr)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Upgradeable = upgradeableStr == "true" ? 1 : 0,
            Owner       = _context.OwnerSigner.Account
        });
    }

    [When(@"the contract is deployed with pausable (.+)")]
    public void WhenDeployedWithPausable(string pausableStr)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Pausable = pausableStr == "true" ? 1 : 0,
            Owner    = _context.OwnerSigner.Account
        });
    }

    [When(@"the contract is deployed with metadataUri ""(.*)""")]
    public void WhenDeployedWithMetadataUri(string uri)
    {
        _contractSteps.DeployWith(new DeployParams
        {
            MetadataUri = uri,
            Owner       = _context.OwnerSigner.Account
        });
    }

    [When("the contract is deployed with default parameters")]
    public void WhenDeployedWithDefaults()
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner = _context.OwnerSigner.Account
        });
    }

    [When("the contract is deployed with a specific owner address")]
    public void WhenDeployedWithSpecificOwner()
    {
        _contractSteps.DeployWith(new DeployParams
        {
            Owner = _context.OwnerSigner.Account
        });
        // LastDeployedOwner is set inside DeployWith via ContractSteps
    }

    // ── Getter assertion steps ─────────────────────────────────────────────────

    [Then(@"getName\(\) returns ""(.*)""")]
    public void ThenGetNameReturns(string expected)
    {
        Assert.That(_context.Contract!.getName(), Is.EqualTo(expected));
    }

    [Then(@"symbol\(\) returns ""(.*)""")]
    public void ThenSymbolReturns(string expected)
    {
        Assert.That(_context.Contract!.Symbol, Is.EqualTo(expected));
    }

    [Then(@"decimals\(\) returns (\d+)")]
    public void ThenDecimalsReturns(int expected)
    {
        Assert.That((int?)_context.Contract!.Decimals, Is.EqualTo(expected));
    }

    [Then(@"getMaxSupply\(\) returns (\d+)")]
    public void ThenGetMaxSupplyReturns(long expected)
    {
        Assert.That(_context.Contract!.getMaxSupply(), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"getMintable\(\) returns (.+)")]
    public void ThenGetMintableReturns(string expectedStr)
    {
        bool expected = expectedStr == "true";
        Assert.That(_context.Contract!.getMintable(), Is.EqualTo(expected));
    }

    [Then(@"isUpgradeable\(\) returns (.+)")]
    public void ThenIsUpgradeableReturns(string expectedStr)
    {
        bool expected = expectedStr == "true";
        Assert.That(_context.Contract!.isUpgradeable(), Is.EqualTo(expected));
    }

    [Then(@"isLocked\(\) returns (.+)")]
    public void ThenIsLockedReturns(string expectedStr)
    {
        bool expected = expectedStr == "true";
        Assert.That(_context.Contract!.isLocked(), Is.EqualTo(expected));
    }

    [Then(@"isPausable\(\) returns (.+)")]
    public void ThenIsPausableReturns(string expectedStr)
    {
        bool expected = expectedStr == "true";
        Assert.That(_context.Contract!.isPausable(), Is.EqualTo(expected));
    }

    [Then(@"isPaused\(\) returns (.+)")]
    public void ThenIsPausedReturns(string expectedStr)
    {
        bool expected = expectedStr == "true";
        Assert.That(_context.Contract!.isPaused(), Is.EqualTo(expected));
    }

    [Then(@"getMetadataUri\(\) returns ""(.*)""")]
    public void ThenGetMetadataUriReturns(string expected)
    {
        Assert.That(_context.Contract!.getMetadataUri(), Is.EqualTo(expected));
    }

    [Then("getOwner\\(\\) returns that exact address")]
    public void ThenGetOwnerReturnsThatExactAddress()
    {
        Assert.That(_context.Contract!.getOwner(), Is.EqualTo(_context.LastDeployedOwner));
    }
}
