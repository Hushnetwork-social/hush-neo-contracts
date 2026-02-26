#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Testing;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TokenTemplate.Tests.Support;

/// <summary>
/// Per-scenario shared state. One instance per Reqnroll scenario,
/// injected by the DI container into hooks and step classes.
/// </summary>
public class TestContext
{
    /// <summary>In-process Neo blockchain engine. Created fresh per scenario.</summary>
    public TestEngine Engine { get; set; } = null!;

    /// <summary>The signer used as the contract owner. Set in BeforeScenario.</summary>
    public Signer OwnerSigner { get; set; } = null!;

    /// <summary>The deployed contract proxy. Null until a deploy step runs.</summary>
    public TokenTemplateContract? Contract { get; set; }

    /// <summary>Return value from the last contract method call.</summary>
    public object? LastResult { get; set; }

    /// <summary>Exception thrown by the last contract method call (if any).</summary>
    public Exception? LastException { get; set; }

    /// <summary>
    /// GAS fee consumed by the last Deploy() call, in datoshi (1 GAS = 100_000_000 datoshi).
    /// Populated by ContractSteps.DeployContract() via engine.CreateGasWatcher().
    /// </summary>
    public long GasConsumedByLastDeploy { get; set; }

    /// <summary>The owner address used in the most recent Deploy() call.</summary>
    public UInt160 LastDeployedOwner { get; set; } = UInt160.Zero;

    /// <summary>
    /// Named wallet signers for multi-account scenarios.
    /// "walletA" = owner signer (set by ScenarioHooks). "walletB", "walletC" created on demand.
    /// </summary>
    public Dictionary<string, Signer> NamedSigners { get; } = new();

    /// <summary>Notifications captured from the last contract invocation.</summary>
    public List<Neo.VM.Types.Array> LastNotifications { get; } = new();

    // ── Factory-specific state ────────────────────────────────────────────────

    /// <summary>The deployed TokenFactory contract proxy (factory scenarios).</summary>
    public TokenFactoryContract? Factory { get; set; }

    /// <summary>Hash of the last token deployed by the factory (factory scenarios).</summary>
    public UInt160 LastCreatedTokenHash { get; set; } = UInt160.Zero;

    /// <summary>GAS consumed by the last TokenFactory Deploy() call, in datoshi.</summary>
    public long GasConsumedByFactoryDeploy { get; set; }

    // ── Spike-specific state ──────────────────────────────────────────────────

    /// <summary>The deployed SpikeDeploy contract proxy (spike scenarios only).</summary>
    public SpikeDeployContract? SpikeContract { get; set; }

    /// <summary>Hash of the token contract deployed by the spike factory call.</summary>
    public UInt160 LastDeployedTokenHash { get; set; } = UInt160.Zero;

    /// <summary>Proxy wrapping the factory-deployed TokenTemplate instance (spike scenarios).</summary>
    public TokenTemplateContract? DeployedToken { get; set; }
}

/// <summary>
/// Strongly-typed deploy parameters for TokenTemplate._deploy().
/// Use ToDeployArray() to convert to the object[] format the contract expects.
/// </summary>
public record DeployParams
{
    public string Name { get; init; } = "Test Token";
    public string Symbol { get; init; } = "TST";
    public BigInteger InitialSupply { get; init; } = 0;
    public BigInteger Decimals { get; init; } = 8;
    public UInt160 Owner { get; init; } = UInt160.Zero;
    public BigInteger Mintable { get; init; } = 0;      // 1=true, 0=false
    public BigInteger MaxSupply { get; init; } = 0;
    public BigInteger Upgradeable { get; init; } = 0;   // 1=true, 0=false
    public string MetadataUri { get; init; } = "";
    public BigInteger Pausable { get; init; } = 0;      // 1=true, 0=false

    /// <summary>
    /// Converts to the object[] format expected by TokenTemplate._deploy().
    /// Order: name, symbol, initialSupply, decimals, owner, mintable, maxSupply, upgradeable, metadataUri, pausable
    /// </summary>
    public object[] ToDeployArray() => new object[]
    {
        Name, Symbol, InitialSupply, Decimals, Owner,
        Mintable, MaxSupply, Upgradeable, MetadataUri, Pausable
    };
}
