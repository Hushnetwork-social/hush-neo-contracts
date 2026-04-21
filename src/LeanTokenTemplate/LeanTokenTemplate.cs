using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

using System;
using System.ComponentModel;
using System.Numerics;

namespace HushNetwork.Contracts
{
    [DisplayName(nameof(LeanTokenTemplate))]
    [ContractAuthor("HushNetwork", "dev@hushnetwork.social")]
    [ContractDescription("HushNetwork ultra-lean wallet-native NEP-17 token facade")]
    [ContractVersion("1.0.0")]
    [ContractSourceCode("https://github.com/Hushnetwork-social/hush-neo-contracts/tree/master/src/LeanTokenTemplate/LeanTokenTemplate.cs")]
    [ContractPermission(Permission.Any, Method.Any)]
    [SupportedStandards(NepStandard.Nep17)]
    public class LeanTokenTemplate : SmartContract
    {
        private const byte Prefix_Engine = 0x10;

        private static UInt160 TokenId() => Runtime.ExecutingScriptHash;

        private static UInt160 StorageGetEngine()
        {
            ByteString raw = Storage.Get(new[] { Prefix_Engine });
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static void StorageSetEngine(UInt160 engineHash) => Storage.Put(new[] { Prefix_Engine }, engineHash);

        private static object EngineRead(string method, object[] args) =>
            Contract.Call(StorageGetEngine(), method, CallFlags.ReadOnly, args);

        private static object EngineCall(string method, object[] args) =>
            Contract.Call(StorageGetEngine(), method, CallFlags.All, args);

        private static bool IsSourceBalanceControlledByCallingContract(UInt160 from) =>
            Runtime.CallingScriptHash.IsValid && Runtime.CallingScriptHash == from;

        private static bool IsLaunchFactoryControlledTransfer() =>
            Runtime.CallingScriptHash.IsValid && Runtime.CallingScriptHash == getAuthorizedFactory();

        private static BigInteger EngineInteger(string method) =>
            (BigInteger)EngineRead(method, new object[] { TokenId() });

        private static bool EngineFlag(string method)
        {
            object raw = EngineRead(method, new object[] { TokenId() });
            return raw is bool flag ? flag : (BigInteger)raw != 0;
        }

        private static UInt160 EngineHash(string method) =>
            (UInt160)EngineRead(method, new object[] { TokenId() });

        private static string EngineString(string method) =>
            (string)EngineRead(method, new object[] { TokenId() });

        private static UInt160 GetEngineOwner() =>
            (UInt160)EngineRead("getOwner", Array.Empty<object>());

        private static bool IsOwner()
        {
            UInt160 owner = getOwner();
            return owner.IsValid && !owner.IsZero && Runtime.CheckWitness(owner);
        }

        private static void AssertOwner() =>
            ExecutionEngine.Assert(IsOwner(), "No authorization");

        private static bool IsFactoryAuthorized()
        {
            UInt160 factory = getAuthorizedFactory();
            UInt160 engineOwner = GetEngineOwner();

            return
                (engineOwner.IsValid && !engineOwner.IsZero && Runtime.CheckWitness(engineOwner)) ||
                (factory.IsValid && !factory.IsZero &&
                    (Runtime.CallingScriptHash == factory || Runtime.EntryScriptHash == factory));
        }

        private static void AssertFactoryAuthorized() =>
            ExecutionEngine.Assert(IsFactoryAuthorized(), "No authorization");

        private static BigInteger GetFactoryOperationFeeOrZero()
        {
            UInt160 factory = getAuthorizedFactory();
            if (!factory.IsValid || factory.IsZero)
                return 0;

            Contract factoryContract = ContractManagement.GetContract(factory);
            if (factoryContract is null)
                return 0;

            object raw = Contract.Call(factory, "getUpdateFee", CallFlags.ReadOnly, Array.Empty<object>());
            return raw is null ? 0 : (BigInteger)raw;
        }

        private static UInt160 GetFactoryRouterOrZero()
        {
            UInt160 factory = getAuthorizedFactory();
            if (!factory.IsValid || factory.IsZero)
                return UInt160.Zero;

            Contract factoryContract = ContractManagement.GetContract(factory);
            if (factoryContract is null)
                return UInt160.Zero;

            object raw = Contract.Call(factory, "getBondingCurveRouter", CallFlags.ReadOnly, Array.Empty<object>());
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static void CollectTransferGasFees(UInt160 from, BigInteger platformFee, BigInteger creatorFee)
        {
            if (platformFee > 0)
            {
                bool platformTransferred = GAS.Transfer(from, getAuthorizedFactory(), platformFee, null);
                ExecutionEngine.Assert(platformTransferred, "Platform fee transfer failed");
            }

            if (creatorFee > 0)
            {
                UInt160 creatorClaimant = getCreatorClaimant();
                if (creatorClaimant != UInt160.Zero)
                {
                    bool creatorTransferred = GAS.Transfer(from, Runtime.ExecutingScriptHash, creatorFee, null);
                    ExecutionEngine.Assert(creatorTransferred, "Creator fee transfer failed");
                    EngineCall("addCreatorClaimable", new object[] { TokenId(), creatorFee });
                }
            }
        }

        private static void PostTransfer(UInt160 from, UInt160 to, BigInteger amount, object data)
        {
            if (to == UInt160.Zero || amount <= 0)
                return;

            Contract target = ContractManagement.GetContract(to);
            if (target is null)
                return;

            Contract.Call(to, "onNEP17Payment", CallFlags.All, new object[] { from, amount, data });
        }

        public delegate void OnTransferDelegate(UInt160 from, UInt160 to, BigInteger amount);

        [DisplayName("Transfer")]
        public static event OnTransferDelegate OnTransfer;

        public delegate void OnOwnerChangedDelegate(UInt160 previousOwner, UInt160 newOwner);

        [DisplayName("OwnerChanged")]
        public static event OnOwnerChangedDelegate OnOwnerChanged;

        public delegate void OnMetadataUriSetDelegate(UInt160 caller, string uri, ulong timestamp);

        [DisplayName("MetadataUriSet")]
        public static event OnMetadataUriSetDelegate OnMetadataUriSet;

        public delegate void OnLockedDelegate(ulong timestamp);

        [DisplayName("Locked")]
        public static event OnLockedDelegate OnLocked;

        public delegate void OnBurnRateSetDelegate(UInt160 caller, BigInteger newRate, ulong timestamp);

        [DisplayName("BurnRateSet")]
        public static event OnBurnRateSetDelegate OnBurnRateSet;

        public delegate void OnMaxSupplySetDelegate(UInt160 caller, BigInteger newMax, ulong timestamp);

        [DisplayName("MaxSupplySet")]
        public static event OnMaxSupplySetDelegate OnMaxSupplySet;

        public delegate void OnCreatorFeeRateSetDelegate(UInt160 caller, BigInteger newRate, ulong timestamp);

        [DisplayName("CreatorFeeRateSet")]
        public static event OnCreatorFeeRateSetDelegate OnCreatorFeeRateSet;

        public delegate void OnPlatformFeeRateSetDelegate(UInt160 caller, BigInteger newRate, ulong timestamp);

        [DisplayName("PlatformFeeRateSet")]
        public static event OnPlatformFeeRateSetDelegate OnPlatformFeeRateSet;

        public delegate void OnFactoryAuthorizedDelegate(UInt160 previousFactory, UInt160 newFactory);

        [DisplayName("FactoryAuthorized")]
        public static event OnFactoryAuthorizedDelegate OnFactoryAuthorized;

        public delegate void OnCreatorFeesClaimedDelegate(UInt160 claimant, BigInteger amount, ulong timestamp);

        [DisplayName("CreatorFeesClaimed")]
        public static event OnCreatorFeesClaimedDelegate OnCreatorFeesClaimed;

        [Safe]
        public static UInt160 getLeanEngine() => StorageGetEngine();

        [Safe]
        public static UInt160 getTokenId() => TokenId();

        [Safe]
        [DisplayName("symbol")]
        public static string Symbol() => EngineString("getSymbol");

        [Safe]
        [DisplayName("decimals")]
        public static byte Decimals() => (byte)EngineInteger("getDecimals");

        [Safe]
        [DisplayName("totalSupply")]
        public static BigInteger TotalSupply() => EngineInteger("totalSupply");

        [Safe]
        [DisplayName("balanceOf")]
        public static BigInteger BalanceOf(UInt160 account) =>
            (BigInteger)EngineRead("balanceOf", new object[] { TokenId(), account });

        [Safe]
        public static UInt160 getOwner() => EngineHash("getTokenOwner");

        [Safe]
        public static string getName() => EngineString("getName");

        [Safe]
        public static bool getMintable() => EngineFlag("getMintable");

        [Safe]
        public static BigInteger getMaxSupply() => EngineInteger("getMaxSupply");

        [Safe]
        public static bool isUpgradeable() => EngineFlag("isUpgradeable");

        [Safe]
        public static bool isLocked() => EngineFlag("isLocked");

        [Safe]
        public static bool isPausable() => EngineFlag("isPausable");

        [Safe]
        public static bool isPaused() => EngineFlag("isPaused");

        [Safe]
        public static string getMetadataUri() => EngineString("getMetadataUri");

        [Safe]
        public static UInt160 getAuthorizedFactory() => EngineHash("getAuthorizedFactory");

        [Safe]
        public static BigInteger getPlatformFeeRate() => EngineInteger("getPlatformFeeRate");

        [Safe]
        public static BigInteger getCreatorFeeRate() => EngineInteger("getCreatorFeeRate");

        [Safe]
        public static BigInteger getBurnRate() => EngineInteger("getBurnRate");

        [Safe]
        public static BigInteger getClaimableCreatorFee() => EngineInteger("getClaimableCreatorFee");

        [Safe]
        public static UInt160 getCreatorClaimant() => EngineHash("getCreatorClaimant");

        [Safe]
        public static bool verify()
        {
            UInt160 owner = getOwner();
            return owner.IsValid && !owner.IsZero && Runtime.CheckWitness(owner);
        }

        [Safe]
        public static object[] quoteTransfer(UInt160 from, UInt160 to, BigInteger amount) =>
            (object[])EngineRead("quoteTransfer", new object[] { TokenId(), from, to, amount });

        [DisplayName("transfer")]
        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            if (!from.IsValid || from.IsZero || !to.IsValid || amount < 0)
                return false;

            if (from != Runtime.CallingScriptHash && !Runtime.CheckWitness(from))
                return false;

            ExecutionEngine.Assert(!isPaused(), "Token transfers are paused");
            if (BalanceOf(from) < amount)
                return false;

            bool sourceBalanceControlledByCallingContract = IsSourceBalanceControlledByCallingContract(from);
            bool launchFactoryControlledTransfer = IsLaunchFactoryControlledTransfer();
            BigInteger platformFeeCollected = BigInteger.Zero;
            BigInteger creatorFeeCollected = BigInteger.Zero;

            if (from != UInt160.Zero &&
                !sourceBalanceControlledByCallingContract &&
                !launchFactoryControlledTransfer &&
                Runtime.CheckWitness(from))
            {
                platformFeeCollected = getPlatformFeeRate();
                creatorFeeCollected = getCreatorClaimant() == UInt160.Zero ? BigInteger.Zero : getCreatorFeeRate();
                CollectTransferGasFees(from, platformFeeCollected, creatorFeeCollected);
            }

            object[] result = (object[])EngineCall("transfer", new object[] { TokenId(), from, to, amount });
            bool transferred = (BigInteger)result[0] != 0;
            if (!transferred)
                return false;

            BigInteger recipientAmount = (BigInteger)result[1];
            BigInteger burnAmount = (BigInteger)result[2];

            if (recipientAmount > 0)
            {
                OnTransfer(from, to, recipientAmount);
                PostTransfer(from, to, recipientAmount, data);
            }

            if (burnAmount > 0)
                OnTransfer(from, UInt160.Zero, burnAmount);

            if (amount > 0)
            {
                EngineCall(
                    "recordTransferEconomics",
                    new object[]
                    {
                        TokenId(),
                        from,
                        to,
                        amount,
                        recipientAmount,
                        burnAmount,
                        platformFeeCollected,
                        creatorFeeCollected
                    });
            }

            if (amount == 0)
                OnTransfer(from, to, amount);

            return true;
        }

        public static void setOwner(UInt160 newOwner)
        {
            AssertOwner();
            UInt160 previous = getOwner();
            EngineCall("setOwner", new object[] { TokenId(), newOwner });
            OnOwnerChanged(previous, newOwner);
        }

        [DisplayName("lock")]
        public static void Lock()
        {
            AssertOwner();
            EngineCall("lock", new object[] { TokenId() });
            OnLocked(Runtime.Time);
        }

        public static void SetMetadataUri(string uri)
        {
            AssertOwner();
            EngineCall("setMetadataUri", new object[] { TokenId(), uri });
            OnMetadataUriSet(getOwner(), uri, Runtime.Time);
        }

        public static void SetMaxSupply(BigInteger newMax)
        {
            AssertOwner();
            EngineCall("setMaxSupply", new object[] { TokenId(), newMax });
            OnMaxSupplySet(getOwner(), newMax, Runtime.Time);
        }

        public static void SetBurnRate(BigInteger bps)
        {
            AssertOwner();
            EngineCall("setBurnRate", new object[] { TokenId(), bps });
            OnBurnRateSet(getOwner(), bps, Runtime.Time);
        }

        public static void SetCreatorFee(BigInteger datoshi)
        {
            AssertOwner();
            EngineCall("setCreatorFee", new object[] { TokenId(), datoshi });
            OnCreatorFeeRateSet(getOwner(), datoshi, Runtime.Time);
        }

        public static void SetPlatformFeeRate(BigInteger datoshi)
        {
            AssertFactoryAuthorized();
            EngineCall("setPlatformFeeRate", new object[] { TokenId(), datoshi });
            OnPlatformFeeRateSet(Runtime.Transaction.Sender, datoshi, Runtime.Time);
        }

        public static void setPausable(bool value)
        {
            AssertOwner();
            EngineCall("setPausable", new object[] { TokenId(), value });
        }

        public static void pause()
        {
            AssertOwner();
            EngineCall("pause", new object[] { TokenId() });
        }

        public static void unpause()
        {
            AssertOwner();
            EngineCall("unpause", new object[] { TokenId() });
        }

        public static void AuthorizeFactory(UInt160 newFactory)
        {
            AssertFactoryAuthorized();
            UInt160 previous = getAuthorizedFactory();
            EngineCall("authorizeFactory", new object[] { TokenId(), newFactory });
            OnFactoryAuthorized(previous, newFactory);
        }

        public static void mint(UInt160 to, BigInteger amount)
        {
            AssertOwner();
            EngineCall("mint", new object[] { TokenId(), to, amount });
            OnTransfer(UInt160.Zero, to, amount);
            PostTransfer(UInt160.Zero, to, amount, null);
        }

        public static void MintByFactory(UInt160 to, BigInteger amount)
        {
            AssertFactoryAuthorized();
            EngineCall("mintByFactory", new object[] { TokenId(), to, amount });
            OnTransfer(UInt160.Zero, to, amount);
            PostTransfer(UInt160.Zero, to, amount, null);
        }

        public static void TransferByFactory(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            AssertFactoryAuthorized();
            EngineCall("transferByFactory", new object[] { TokenId(), from, to, amount });
            OnTransfer(from, to, amount);
            PostTransfer(from, to, amount, data);
        }

        public static void burn(BigInteger amount)
        {
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            UInt160 caller = Runtime.Transaction.Sender;
            ExecutionEngine.Assert(Runtime.CheckWitness(caller), "No authorization");
            ExecutionEngine.Assert(BalanceOf(caller) >= amount, "Insufficient balance");
            ExecutionEngine.Assert(Transfer(caller, UInt160.Zero, amount, null), "Burn failed");
        }

        public static void claimCreatorFees()
        {
            ClaimCreatorFeesInternal(getClaimableCreatorFee());
        }

        public static void claimCreatorFee(BigInteger amount)
        {
            ClaimCreatorFeesInternal(amount);
        }

        private static void ClaimCreatorFeesInternal(BigInteger amount)
        {
            UInt160 claimant = getCreatorClaimant();
            ExecutionEngine.Assert(claimant.IsValid && !claimant.IsZero, "Creator claimant not configured");
            ExecutionEngine.Assert(Runtime.CheckWitness(claimant), "No authorization");
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            ExecutionEngine.Assert(getClaimableCreatorFee() >= amount, "Insufficient creator fee balance");

            BigInteger operationFee = GetFactoryOperationFeeOrZero();
            if (operationFee > 0)
            {
                bool operationFeeTransferred = GAS.Transfer(
                    claimant,
                    getAuthorizedFactory(),
                    operationFee,
                    null
                );
                ExecutionEngine.Assert(operationFeeTransferred, "Creator fee claim operation fee transfer failed");
            }

            bool transferred = GAS.Transfer(Runtime.ExecutingScriptHash, claimant, amount, null);
            ExecutionEngine.Assert(transferred, "Creator fee claim transfer failed");

            EngineCall("claimCreatorFee", new object[] { TokenId(), amount });
            OnCreatorFeesClaimed(claimant, amount, Runtime.Time);
        }

        [DisplayName("onNEP17Payment")]
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            if (Runtime.CallingScriptHash != GAS.Hash)
                throw new InvalidOperationException("Only GAS accepted.");

            if (amount <= 0)
                return;

            if (data is string marker && marker == "creator_fee_deposit")
            {
                UInt160 router = GetFactoryRouterOrZero();
                if (router.IsValid && !router.IsZero && from == router)
                    EngineCall("addCreatorClaimable", new object[] { TokenId(), amount });
            }
        }

        public static void update(ByteString nefFile, string manifest, object data = null)
        {
            ExecutionEngine.Assert(verify(), "No authorization");
            ExecutionEngine.Assert(isUpgradeable(), "Contract is not upgradeable");
            ExecutionEngine.Assert(!isLocked(), "Contract is locked");
            ContractManagement.Update(nefFile, manifest, data);
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;

            object[] args = (object[])data;
            ExecutionEngine.Assert(args.Length == 14, "Expected 14 deploy parameters");

            string name = (string)args[0];
            string symbol = (string)args[1];
            BigInteger initialSupply = (BigInteger)args[2];
            BigInteger decimals = (BigInteger)args[3];
            UInt160 owner = (UInt160)args[4];
            BigInteger mintable = (BigInteger)args[5];
            BigInteger maxSupply = (BigInteger)args[6];
            BigInteger upgradeable = (BigInteger)args[7];
            string metadataUri = (string)args[8];
            BigInteger pausable = (BigInteger)args[9];
            UInt160 launchFactory = (UInt160)args[10];
            BigInteger platformFeeRate = (BigInteger)args[11];
            BigInteger creatorFeeRate = (BigInteger)args[12];
            UInt160 engineHash = (UInt160)args[13];

            ExecutionEngine.Assert(engineHash.IsValid && !engineHash.IsZero, "Invalid lean engine address");
            ExecutionEngine.Assert(ContractManagement.GetContract(engineHash) is not null, "Lean engine not deployed");

            StorageSetEngine(engineHash);
            EngineCall(
                "registerToken",
                new object[]
                {
                    TokenId(),
                    name,
                    symbol,
                    initialSupply,
                    decimals,
                    owner,
                    mintable,
                    maxSupply,
                    upgradeable,
                    metadataUri,
                    pausable,
                    launchFactory,
                    platformFeeRate,
                    creatorFeeRate
                });

            OnOwnerChanged(UInt160.Zero, owner);
            if (initialSupply > 0)
                OnTransfer(UInt160.Zero, owner, initialSupply);
        }
    }
}
