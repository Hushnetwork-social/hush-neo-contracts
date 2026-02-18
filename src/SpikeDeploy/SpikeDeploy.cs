using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System.ComponentModel;

namespace HushNetwork.Spike
{
    /// <summary>
    /// Spike contract: validates that ContractManagement.Deploy() is callable
    /// from within a running Neo N3 contract.
    ///
    /// If deployTemplate() succeeds, the factory pattern used by FEAT-070 is valid.
    /// If it throws, we pivot to the event + off-chain deployer fallback.
    ///
    /// This contract has no production use — it exists solely to answer the spike question.
    /// </summary>
    [DisplayName(nameof(SpikeDeploy))]
    [ContractAuthor("HushNetwork", "dev@hushnetwork.social")]
    [ContractDescription("Spike: validates ContractManagement.Deploy() from within a contract")]
    [ContractVersion("1.0.0")]
    [ContractPermission(Permission.Any, Method.Any)]
    public class SpikeDeploy : Neo.SmartContract.Framework.SmartContract
    {
        /// <summary>
        /// Calls ContractManagement.Deploy() from within contract execution.
        /// Returns the UInt160 hash of the newly deployed contract.
        /// </summary>
        /// <param name="nef">Compiled NEF bytes of the contract to deploy</param>
        /// <param name="manifest">JSON manifest of the contract to deploy</param>
        /// <param name="deployParams">Parameters passed to the deployed contract's _deploy()</param>
        /// <returns>Contract hash of the newly deployed instance</returns>
        public static UInt160 deployTemplate(ByteString nef, string manifest, object[] deployParams)
        {
            Contract deployed = ContractManagement.Deploy(nef, manifest, deployParams);
            return deployed.Hash;
        }
    }
}
