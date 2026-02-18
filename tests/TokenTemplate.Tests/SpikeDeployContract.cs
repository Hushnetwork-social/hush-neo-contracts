#nullable enable
using Neo;
using Neo.SmartContract.Testing;
using System.ComponentModel;

namespace TokenTemplate.Tests;

/// <summary>
/// Strongly-typed abstract proxy for the SpikeDeploy contract.
/// Used exclusively by spike scenarios to validate that
/// ContractManagement.Deploy() is callable from within a running Neo N3 contract.
/// </summary>
public abstract class SpikeDeployContract : SmartContract
{
    protected SpikeDeployContract(SmartContractInitialize initialize) : base(initialize) { }

    /// <summary>
    /// Calls ContractManagement.Deploy() from within contract execution.
    /// Returns the hash of the newly deployed contract.
    /// </summary>
    [DisplayName("deployTemplate")]
    public abstract UInt160? deployTemplate(byte[]? nef, string? manifest, object[]? deployParams);
}
