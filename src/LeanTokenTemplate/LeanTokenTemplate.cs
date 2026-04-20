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
    [ContractDescription("HushNetwork lean wallet-native NEP-17 token template")]
    [ContractVersion("1.0.0")]
    [ContractSourceCode("https://github.com/Hushnetwork-social/hush-neo-contracts/tree/master/src/LeanTokenTemplate/LeanTokenTemplate.cs")]
    [ContractPermission(Permission.Any, Method.Any)]
    [SupportedStandards(NepStandard.Nep17)]
    public class LeanTokenTemplate : Neo.SmartContract.Framework.Nep17Token
    {
        // Base Nep17Token reserves:
        //   0x00 - totalSupply
        //   0x01 - balances
        private const byte Prefix_Name = 0x10;
        private const byte Prefix_Symbol = 0x11;
        private const byte Prefix_Decimals = 0x12;
        private const byte Prefix_Mintable = 0x13;
        private const byte Prefix_MaxSupply = 0x14;
        private const byte Prefix_Upgradeable = 0x15;
        private const byte Prefix_Locked = 0x16;
        private const byte Prefix_Pausable = 0x17;
        private const byte Prefix_Paused = 0x18;
        private const byte Prefix_MetadataUri = 0x19;
        private const byte Prefix_LaunchFactory = 0x1a;
        private const byte Prefix_PlatformFeeRate = 0x1b;
        private const byte Prefix_CreatorFeeRate = 0x1c;
        private const byte Prefix_BurnRate = 0x1d;
        private const byte Prefix_CreatorClaimable = 0x1e;
        private const byte Prefix_CreatorClaimant = 0x1f;
        private const byte Prefix_Owner = 0xff;

        private static string StorageGetString(byte prefix)
        {
            ByteString raw = Storage.Get(new[] { prefix });
            return raw is null ? "" : (string)raw;
        }

        private static BigInteger StorageGetInteger(byte prefix)
        {
            ByteString raw = Storage.Get(new[] { prefix });
            return raw is null ? BigInteger.Zero : (BigInteger)raw;
        }

        private static bool StorageGetFlag(byte prefix) => StorageGetInteger(prefix) != 0;

        private static UInt160 StorageGetHash(byte prefix)
        {
            ByteString raw = Storage.Get(new[] { prefix });
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static void StorageSetFlag(byte prefix, bool value)
        {
            if (value)
                Storage.Put(new[] { prefix }, BigInteger.One);
            else
                Storage.Delete(new[] { prefix });
        }

        private static string StorageGetName() => StorageGetString(Prefix_Name);
        private static void StorageSetName(string value) => Storage.Put(new[] { Prefix_Name }, value);

        private static string StorageGetSymbol() => StorageGetString(Prefix_Symbol);
        private static void StorageSetSymbol(string value) => Storage.Put(new[] { Prefix_Symbol }, value);

        private static byte StorageGetDecimals()
        {
            BigInteger value = StorageGetInteger(Prefix_Decimals);
            return (byte)value;
        }

        private static void StorageSetDecimals(byte value) =>
            Storage.Put(new[] { Prefix_Decimals }, (BigInteger)value);

        private static bool StorageGetMintable() => StorageGetFlag(Prefix_Mintable);
        private static void StorageSetMintable(bool value) => StorageSetFlag(Prefix_Mintable, value);

        private static BigInteger StorageGetMaxSupply() => StorageGetInteger(Prefix_MaxSupply);
        private static void StorageSetMaxSupply(BigInteger value) => Storage.Put(new[] { Prefix_MaxSupply }, value);

        private static bool StorageGetUpgradeable() => StorageGetFlag(Prefix_Upgradeable);
        private static void StorageSetUpgradeable(bool value) => StorageSetFlag(Prefix_Upgradeable, value);

        private static bool StorageGetLocked() => StorageGetFlag(Prefix_Locked);

        private static bool StorageGetPausable() => StorageGetFlag(Prefix_Pausable);
        private static void StorageSetPausable(bool value) => StorageSetFlag(Prefix_Pausable, value);

        private static bool StorageGetPaused() => StorageGetFlag(Prefix_Paused);

        private static string StorageGetMetadataUri() => StorageGetString(Prefix_MetadataUri);
        private static void StorageSetMetadataUri(string value) => Storage.Put(new[] { Prefix_MetadataUri }, value);

        private static UInt160 StorageGetLaunchFactory() => StorageGetHash(Prefix_LaunchFactory);
        private static void StorageSetLaunchFactory(UInt160 value) => Storage.Put(new[] { Prefix_LaunchFactory }, value);

        private static BigInteger StorageGetPlatformFeeRate() => StorageGetInteger(Prefix_PlatformFeeRate);
        private static void StorageSetPlatformFeeRate(BigInteger value) => Storage.Put(new[] { Prefix_PlatformFeeRate }, value);

        private static BigInteger StorageGetCreatorFeeRate() => StorageGetInteger(Prefix_CreatorFeeRate);
        private static void StorageSetCreatorFeeRate(BigInteger value) => Storage.Put(new[] { Prefix_CreatorFeeRate }, value);

        private static BigInteger StorageGetBurnRate() => StorageGetInteger(Prefix_BurnRate);
        private static void StorageSetBurnRate(BigInteger value) => Storage.Put(new[] { Prefix_BurnRate }, value);

        private static BigInteger StorageGetCreatorClaimable() => StorageGetInteger(Prefix_CreatorClaimable);
        private static void StorageSetCreatorClaimable(BigInteger value)
        {
            if (value > 0)
                Storage.Put(new[] { Prefix_CreatorClaimable }, value);
            else
                Storage.Delete(new[] { Prefix_CreatorClaimable });
        }

        private static UInt160 StorageGetCreatorClaimant() => StorageGetHash(Prefix_CreatorClaimant);
        private static void StorageSetCreatorClaimant(UInt160 value) => Storage.Put(new[] { Prefix_CreatorClaimant }, value);

        private static UInt160 StorageGetOwner() => StorageGetHash(Prefix_Owner);
        private static void StorageSetOwner(UInt160 value) => Storage.Put(new[] { Prefix_Owner }, value);

        private static void StorageSetLocked(bool value) => StorageSetFlag(Prefix_Locked, value);

        private static void StorageSetPaused(bool value) => StorageSetFlag(Prefix_Paused, value);

        private static bool IsOwner()
        {
            UInt160 owner = StorageGetOwner();
            return owner.IsValid && !owner.IsZero && Runtime.CheckWitness(owner);
        }

        private static BigInteger GetFactoryOperationFeeOrZero()
        {
            UInt160 factory = StorageGetLaunchFactory();
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
            UInt160 factory = StorageGetLaunchFactory();
            if (!factory.IsValid || factory.IsZero)
                return UInt160.Zero;

            Contract factoryContract = ContractManagement.GetContract(factory);
            if (factoryContract is null)
                return UInt160.Zero;

            object raw = Contract.Call(factory, "getBondingCurveRouter", CallFlags.ReadOnly, Array.Empty<object>());
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static bool IsSourceBalanceControlledByCallingContract(UInt160 from) =>
            Runtime.CallingScriptHash.IsValid && Runtime.CallingScriptHash == from;

        private static bool IsLaunchFactoryControlledTransfer() =>
            Runtime.CallingScriptHash.IsValid && Runtime.CallingScriptHash == StorageGetLaunchFactory();

        private static void CollectTransferGasFees(UInt160 from)
        {
            BigInteger platformFee = StorageGetPlatformFeeRate();
            if (platformFee > 0)
            {
                bool platformTransferred = GAS.Transfer(from, StorageGetLaunchFactory(), platformFee, null);
                ExecutionEngine.Assert(platformTransferred, "Platform fee transfer failed");
            }

            BigInteger creatorFee = StorageGetCreatorFeeRate();
            if (creatorFee > 0)
            {
                UInt160 creatorClaimant = StorageGetCreatorClaimant();
                if (creatorClaimant != UInt160.Zero)
                {
                    bool creatorTransferred = GAS.Transfer(from, Runtime.ExecutingScriptHash, creatorFee, null);
                    ExecutionEngine.Assert(creatorTransferred, "Creator fee transfer failed");
                    StorageSetCreatorClaimable(StorageGetCreatorClaimable() + creatorFee);
                }
            }
        }

        private static BigInteger ApplyTransferBurn(UInt160 from, BigInteger amount)
        {
            BigInteger burnRate = StorageGetBurnRate();
            if (burnRate <= 0) return amount;

            BigInteger burnAmount = amount * burnRate / 10000;
            if (burnAmount <= 0) return amount;

            ExecutionEngine.Assert(amount > burnAmount, "Burn amount exceeds transfer amount");
            Burn(from, burnAmount);
            return amount - burnAmount;
        }

        public override string Symbol { [Safe] get => StorageGetSymbol(); }

        public override byte Decimals { [Safe] get => StorageGetDecimals(); }

        [Safe]
        public static UInt160 getOwner() => StorageGetOwner();

        [Safe]
        public static string getName() => StorageGetName();

        [Safe]
        public static bool getMintable() => StorageGetMintable();

        [Safe]
        public static BigInteger getMaxSupply() => StorageGetMaxSupply();

        [Safe]
        public static bool isUpgradeable() => StorageGetUpgradeable();

        [Safe]
        public static bool isLocked() => StorageGetLocked();

        [Safe]
        public static bool isPausable() => StorageGetPausable();

        [Safe]
        public static bool isPaused() => StorageGetPaused();

        [Safe]
        public static string getMetadataUri() => StorageGetMetadataUri();

        [Safe]
        public static UInt160 getAuthorizedFactory() => StorageGetLaunchFactory();

        [Safe]
        public static BigInteger getPlatformFeeRate() => StorageGetPlatformFeeRate();

        [Safe]
        public static BigInteger getCreatorFeeRate() => StorageGetCreatorFeeRate();

        [Safe]
        public static BigInteger getBurnRate() => StorageGetBurnRate();

        [Safe]
        public static BigInteger getClaimableCreatorFee() => StorageGetCreatorClaimable();

        [Safe]
        public static UInt160 getCreatorClaimant() => StorageGetCreatorClaimant();

        [Safe]
        public static bool verify() => IsOwner();

        [Safe]
        public static object[] quoteTransfer(UInt160 from, UInt160 to, BigInteger amount)
        {
            BigInteger grossAmount = amount < 0 ? 0 : amount;
            bool isMint = from == UInt160.Zero;
            bool isDirectBurn = !isMint && to == UInt160.Zero;

            BigInteger transferBurnAmount = BigInteger.Zero;
            BigInteger totalTokenBurned = BigInteger.Zero;
            BigInteger recipientAmount = grossAmount;

            if (isDirectBurn)
            {
                recipientAmount = BigInteger.Zero;
                totalTokenBurned = grossAmount;
            }
            else if (!isMint && grossAmount > 0)
            {
                BigInteger burnRate = StorageGetBurnRate();
                if (burnRate > 0)
                {
                    transferBurnAmount = grossAmount * burnRate / 10000;
                    if (transferBurnAmount > 0)
                    {
                        recipientAmount -= transferBurnAmount;
                        totalTokenBurned = transferBurnAmount;
                    }
                }
            }

            BigInteger platformFeeRate = isMint ? BigInteger.Zero : StorageGetPlatformFeeRate();
            BigInteger creatorFeeRate = isMint || StorageGetCreatorClaimant() == UInt160.Zero
                ? BigInteger.Zero
                : StorageGetCreatorFeeRate();

            return new object[]
            {
                grossAmount,
                recipientAmount,
                transferBurnAmount,
                totalTokenBurned,
                isMint ? BigInteger.Zero : platformFeeRate,
                creatorFeeRate,
                isMint ? BigInteger.Zero : platformFeeRate + creatorFeeRate,
                isMint ? BigInteger.One : BigInteger.Zero,
                isDirectBurn ? BigInteger.One : BigInteger.Zero
            };
        }

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

        public delegate void OnCreatorFeesClaimedDelegate(UInt160 claimant, BigInteger amount, ulong timestamp);

        [DisplayName("CreatorFeesClaimed")]
        public static event OnCreatorFeesClaimedDelegate OnCreatorFeesClaimed;

        public static void setOwner(UInt160 newOwner)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(newOwner.IsValid, "Invalid owner address");

            UInt160 previous = StorageGetOwner();
            StorageSetOwner(newOwner);
            OnOwnerChanged(previous, newOwner);
        }

        [DisplayName("lock")]
        public static void Lock()
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Already locked");
            StorageSetLocked(true);
            OnLocked(Runtime.Time);
        }

        public static void SetMetadataUri(string uri)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(uri != null && uri.Length > 0, "URI must not be null or empty");
            StorageSetMetadataUri(uri);
            OnMetadataUriSet(StorageGetOwner(), uri, Runtime.Time);
        }

        public static void SetMaxSupply(BigInteger newMax)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(newMax >= 0, "MaxSupply must be >= 0");

            if (newMax > 0)
            {
                BigInteger currentSupply = StorageGetInteger(0x00);
                ExecutionEngine.Assert(newMax >= currentSupply, "NewMaxSupply cannot be less than current totalSupply");
            }

            StorageSetMaxSupply(newMax);
            OnMaxSupplySet(StorageGetOwner(), newMax, Runtime.Time);
        }

        public static void SetBurnRate(BigInteger bps)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(bps >= 0 && bps <= 1000, "BurnRate must be 0-1000 basis points");
            StorageSetBurnRate(bps);
            OnBurnRateSet(StorageGetOwner(), bps, Runtime.Time);
        }

        public static void SetCreatorFee(BigInteger datoshi)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(datoshi >= 0 && datoshi <= 5_000_000, "CreatorFee must be 0-5,000,000 datoshi");
            StorageSetCreatorFeeRate(datoshi);
            OnCreatorFeeRateSet(StorageGetOwner(), datoshi, Runtime.Time);
        }

        public static void SetPlatformFeeRate(BigInteger datoshi)
        {
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetLaunchFactory(), "No authorization");
            ExecutionEngine.Assert(datoshi >= 0, "PlatformFeeRate must be >= 0");
            StorageSetPlatformFeeRate(datoshi);
            OnPlatformFeeRateSet(Runtime.CallingScriptHash, datoshi, Runtime.Time);
        }

        public static void setPausable(bool value)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(StorageGetUpgradeable(), "Contract is not upgradeable");
            StorageSetPausable(value);
        }

        public static void pause()
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(StorageGetPausable(), "Token is not pausable");
            StorageSetPaused(true);
        }

        public static void unpause()
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(StorageGetPausable(), "Token is not pausable");
            StorageSetPaused(false);
        }

        public static new bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(!StorageGetPaused(), "Token transfers are paused");
            bool sourceBalanceControlledByCallingContract = IsSourceBalanceControlledByCallingContract(from);
            bool launchFactoryControlledTransfer = IsLaunchFactoryControlledTransfer();

            if (from != UInt160.Zero)
            {
                if (!sourceBalanceControlledByCallingContract &&
                    !launchFactoryControlledTransfer &&
                    Runtime.CheckWitness(from))
                {
                    CollectTransferGasFees(from);
                }

                if (to != UInt160.Zero && !launchFactoryControlledTransfer)
                    amount = ApplyTransferBurn(from, amount);
            }

            if (to == UInt160.Zero)
            {
                Burn(from, amount);
                return true;
            }

            return Nep17Token.Transfer(from, to, amount, data);
        }

        public static void burn(BigInteger amount)
        {
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");

            UInt160 caller = Runtime.Transaction.Sender;
            ExecutionEngine.Assert(Runtime.CheckWitness(caller), "No authorization");
            ExecutionEngine.Assert(BalanceOf(caller) >= amount, "Insufficient balance");
            ExecutionEngine.Assert(Transfer(caller, UInt160.Zero, amount, null), "Burn failed");
        }

        public static void mint(UInt160 to, BigInteger amount)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(StorageGetMintable(), "Token is not mintable");
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            ExecutionEngine.Assert(to.IsValid && !to.IsZero, "Invalid recipient");

            BigInteger maxSupply = StorageGetMaxSupply();
            if (maxSupply > 0)
            {
                BigInteger currentSupply = StorageGetInteger(0x00);
                ExecutionEngine.Assert(currentSupply + amount <= maxSupply, "MaxSupply exceeded");
            }

            Nep17Token.Mint(to, amount);
        }

        public static void claimCreatorFees()
        {
            ClaimCreatorFeesInternal(StorageGetCreatorClaimable());
        }

        public static void claimCreatorFee(BigInteger amount)
        {
            ClaimCreatorFeesInternal(amount);
        }

        private static void ClaimCreatorFeesInternal(BigInteger amount)
        {
            UInt160 claimant = StorageGetCreatorClaimant();
            ExecutionEngine.Assert(claimant.IsValid && !claimant.IsZero, "Creator claimant not configured");
            ExecutionEngine.Assert(Runtime.CheckWitness(claimant), "No authorization");
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");

            BigInteger claimable = StorageGetCreatorClaimable();
            ExecutionEngine.Assert(claimable >= amount, "Insufficient creator fee balance");

            BigInteger operationFee = GetFactoryOperationFeeOrZero();
            if (operationFee > 0)
            {
                bool operationFeeTransferred = GAS.Transfer(
                    claimant,
                    StorageGetLaunchFactory(),
                    operationFee,
                    null
                );
                ExecutionEngine.Assert(operationFeeTransferred, "Creator fee claim operation fee transfer failed");
            }

            bool transferred = GAS.Transfer(Runtime.ExecutingScriptHash, claimant, amount, null);
            ExecutionEngine.Assert(transferred, "Creator fee claim transfer failed");

            StorageSetCreatorClaimable(claimable - amount);
            OnCreatorFeesClaimed(claimant, amount, Runtime.Time);
        }

        public static void MintByFactory(UInt160 to, BigInteger amount)
        {
            ExecutionEngine.Assert(false, "Lean token has no factory mint authority");
        }

        public static void TransferByFactory(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(false, "Lean token has no factory transfer authority");
        }

        public static void AuthorizeFactory(UInt160 newFactory)
        {
            ExecutionEngine.Assert(false, "Lean token has no factory authority");
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
                    StorageSetCreatorClaimable(StorageGetCreatorClaimable() + amount);
            }
        }

        public static void update(ByteString nefFile, string manifest, object data = null)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(StorageGetUpgradeable(), "Contract is not upgradeable");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ContractManagement.Update(nefFile, manifest, data);
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;

            object[] args = (object[])data;
            ExecutionEngine.Assert(args.Length == 13, "Expected 13 deploy parameters");

            string name = (string)args[0];
            string symbol = (string)args[1];
            BigInteger initialSupply = (BigInteger)args[2];
            byte decimals = (byte)(BigInteger)args[3];
            UInt160 owner = (UInt160)args[4];
            bool mintable = (BigInteger)args[5] != 0;
            BigInteger maxSupply = (BigInteger)args[6];
            bool upgradeable = (BigInteger)args[7] != 0;
            string metadataUri = (string)args[8];
            bool pausable = (BigInteger)args[9] != 0;
            UInt160 launchFactory = (UInt160)args[10];
            BigInteger platformFeeRate = (BigInteger)args[11];
            BigInteger creatorFeeRate = (BigInteger)args[12];

            ExecutionEngine.Assert(name != null && name.Length > 0, "Name must not be empty");
            ExecutionEngine.Assert(symbol != null && symbol.Length > 0, "Symbol must not be empty");
            ExecutionEngine.Assert(initialSupply >= 0, "InitialSupply must be >= 0");
            ExecutionEngine.Assert(decimals <= 18, "Decimals must be 0-18");
            ExecutionEngine.Assert(owner.IsValid && !owner.IsZero, "Invalid owner address");
            ExecutionEngine.Assert(maxSupply >= 0, "MaxSupply must be >= 0");
            if (maxSupply > 0)
                ExecutionEngine.Assert(initialSupply <= maxSupply, "InitialSupply must not exceed MaxSupply");
            ExecutionEngine.Assert(launchFactory.IsValid, "Invalid launch factory address");
            ExecutionEngine.Assert(platformFeeRate >= 0 && platformFeeRate <= 10_000_000, "PlatformFeeRate exceeds maximum");
            ExecutionEngine.Assert(creatorFeeRate >= 0 && creatorFeeRate <= 5_000_000, "CreatorFeeRate exceeds maximum");

            StorageSetName(name);
            StorageSetSymbol(symbol);
            StorageSetDecimals(decimals);
            StorageSetMintable(mintable);
            StorageSetMaxSupply(maxSupply);
            StorageSetUpgradeable(upgradeable);
            StorageSetPausable(pausable);
            StorageSetMetadataUri(metadataUri);
            StorageSetLaunchFactory(launchFactory);
            StorageSetPlatformFeeRate(platformFeeRate);
            StorageSetCreatorFeeRate(creatorFeeRate);
            StorageSetCreatorClaimant(owner);
            StorageSetOwner(owner);
            OnOwnerChanged(UInt160.Zero, owner);

            if (initialSupply > 0)
                Nep17Token.Mint(owner, initialSupply);
        }
    }
}
