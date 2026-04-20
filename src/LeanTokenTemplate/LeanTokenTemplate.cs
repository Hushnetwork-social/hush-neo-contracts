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

        private static BigInteger StorageGetCreatorClaimable() => StorageGetInteger(Prefix_CreatorClaimable);

        private static UInt160 StorageGetCreatorClaimant() => StorageGetHash(Prefix_CreatorClaimant);
        private static void StorageSetCreatorClaimant(UInt160 value) => Storage.Put(new[] { Prefix_CreatorClaimant }, value);

        private static UInt160 StorageGetOwner() => StorageGetHash(Prefix_Owner);
        private static void StorageSetOwner(UInt160 value) => Storage.Put(new[] { Prefix_Owner }, value);

        private static bool IsOwner()
        {
            UInt160 owner = StorageGetOwner();
            return owner.IsValid && !owner.IsZero && Runtime.CheckWitness(owner);
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

        public delegate void OnOwnerChangedDelegate(UInt160 previousOwner, UInt160 newOwner);

        [DisplayName("OwnerChanged")]
        public static event OnOwnerChangedDelegate OnOwnerChanged;

        public delegate void OnMetadataUriSetDelegate(UInt160 caller, string uri, ulong timestamp);

        [DisplayName("MetadataUriSet")]
        public static event OnMetadataUriSetDelegate OnMetadataUriSet;

        // First owner-local mutator; the remaining lifecycle methods use the same local storage model.
        public static void SetMetadataUri(string uri)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(uri != null && uri.Length > 0, "URI must not be null or empty");
            StorageSetMetadataUri(uri);
            OnMetadataUriSet(StorageGetOwner(), uri, Runtime.Time);
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
