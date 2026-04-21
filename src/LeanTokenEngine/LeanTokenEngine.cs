using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

using System;
using System.ComponentModel;
using System.Numerics;

namespace HushNetwork.Contracts
{
    [DisplayName(nameof(LeanTokenEngine))]
    [ContractAuthor("HushNetwork", "dev@hushnetwork.social")]
    [ContractDescription("Shared token-scoped storage engine for HushNetwork lean NEP-17 facades")]
    [ContractVersion("1.0.0")]
    [ContractSourceCode("https://github.com/Hushnetwork-social/hush-neo-contracts/tree/master/src/LeanTokenEngine/LeanTokenEngine.cs")]
    [ContractPermission(Permission.Any, Method.Any)]
    public class LeanTokenEngine : SmartContract
    {
        private const byte Prefix_TokenInfo = 0x10;
        private const byte Prefix_FacadeToken = 0x11;
        private const byte Prefix_Balance = 0x12;
        private const byte Prefix_TotalSupply = 0x13;
        private const byte Prefix_Owner = 0xff;

        private const int Info_Facade = 0;
        private const int Info_Owner = 1;
        private const int Info_Name = 2;
        private const int Info_Symbol = 3;
        private const int Info_Decimals = 4;
        private const int Info_Mintable = 5;
        private const int Info_MaxSupply = 6;
        private const int Info_Upgradeable = 7;
        private const int Info_MetadataUri = 8;
        private const int Info_Pausable = 9;
        private const int Info_LaunchFactory = 10;
        private const int Info_PlatformFeeRate = 11;
        private const int Info_CreatorFeeRate = 12;
        private const int Info_BurnRate = 13;
        private const int Info_CreatorClaimable = 14;
        private const int Info_CreatorClaimant = 15;
        private const int Info_Locked = 16;
        private const int Info_Paused = 17;

        public delegate void OnTokenRegisteredDelegate(UInt160 tokenId, UInt160 facadeHash, UInt160 owner, string symbol, BigInteger initialSupply);

        [DisplayName("TokenRegistered")]
        public static event OnTokenRegisteredDelegate OnTokenRegistered;

        public delegate void OnTokenTransferDelegate(UInt160 tokenId, UInt160 from, UInt160 to, BigInteger amount);

        [DisplayName("TokenTransfer")]
        public static event OnTokenTransferDelegate OnTokenTransfer;

        public delegate void OnTokenOwnerChangedDelegate(UInt160 tokenId, UInt160 previousOwner, UInt160 newOwner);

        [DisplayName("TokenOwnerChanged")]
        public static event OnTokenOwnerChangedDelegate OnTokenOwnerChanged;

        private static ByteString TokenInfoKey(UInt160 tokenId)
        {
            AssertTokenId(tokenId);
            return (ByteString)new byte[] { Prefix_TokenInfo } + (ByteString)tokenId;
        }

        private static ByteString FacadeTokenKey(UInt160 facadeHash)
        {
            ExecutionEngine.Assert(facadeHash.IsValid && !facadeHash.IsZero, "Invalid facade");
            return (ByteString)new byte[] { Prefix_FacadeToken } + (ByteString)facadeHash;
        }

        private static ByteString BalanceKey(UInt160 tokenId, UInt160 account)
        {
            AssertTokenId(tokenId);
            ExecutionEngine.Assert(account.IsValid && !account.IsZero, "Invalid account");
            return (ByteString)new byte[] { Prefix_Balance } + (ByteString)tokenId + (ByteString)account;
        }

        private static ByteString TotalSupplyKey(UInt160 tokenId)
        {
            AssertTokenId(tokenId);
            return (ByteString)new byte[] { Prefix_TotalSupply } + (ByteString)tokenId;
        }

        private static void AssertTokenId(UInt160 tokenId) =>
            ExecutionEngine.Assert(tokenId.IsValid && !tokenId.IsZero, "Invalid token id");

        private static UInt160 StorageGetOwner()
        {
            ByteString raw = Storage.Get(new[] { Prefix_Owner });
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static void StorageSetOwner(UInt160 owner) => Storage.Put(new[] { Prefix_Owner }, owner);

        private static object[] StorageGetTokenInfoOrNull(UInt160 tokenId)
        {
            ByteString raw = Storage.Get(TokenInfoKey(tokenId));
            return raw is null ? null : (object[])StdLib.Deserialize(raw);
        }

        private static object[] RequireToken(UInt160 tokenId)
        {
            object[] info = StorageGetTokenInfoOrNull(tokenId);
            ExecutionEngine.Assert(info is not null, "Token not registered");
            return info;
        }

        private static void StorageSetTokenInfo(UInt160 tokenId, object[] info) =>
            Storage.Put(TokenInfoKey(tokenId), StdLib.Serialize(info));

        private static UInt160 StorageGetTokenIdByFacadeOrZero(UInt160 facadeHash)
        {
            ByteString raw = Storage.Get(FacadeTokenKey(facadeHash));
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static void StorageSetFacadeToken(UInt160 facadeHash, UInt160 tokenId) =>
            Storage.Put(FacadeTokenKey(facadeHash), tokenId);

        private static BigInteger StorageGetTotalSupply(UInt160 tokenId)
        {
            ByteString raw = Storage.Get(TotalSupplyKey(tokenId));
            return raw is null ? BigInteger.Zero : (BigInteger)raw;
        }

        private static void StorageSetTotalSupply(UInt160 tokenId, BigInteger value)
        {
            if (value > 0)
                Storage.Put(TotalSupplyKey(tokenId), value);
            else
                Storage.Delete(TotalSupplyKey(tokenId));
        }

        private static BigInteger StorageGetBalance(UInt160 tokenId, UInt160 account)
        {
            if (!account.IsValid || account.IsZero) return BigInteger.Zero;
            ByteString raw = Storage.Get(BalanceKey(tokenId, account));
            return raw is null ? BigInteger.Zero : (BigInteger)raw;
        }

        private static void StorageSetBalance(UInt160 tokenId, UInt160 account, BigInteger value)
        {
            if (value > 0)
                Storage.Put(BalanceKey(tokenId, account), value);
            else
                Storage.Delete(BalanceKey(tokenId, account));
        }

        private static BigInteger InfoInteger(object[] info, int index) => (BigInteger)info[index];

        private static bool InfoFlag(object[] info, int index) => InfoInteger(info, index) != 0;

        private static void AssertCallingFacade(object[] info) =>
            ExecutionEngine.Assert(Runtime.CallingScriptHash == (UInt160)info[Info_Facade], "Invalid token facade");

        private static void AssertUnlocked(object[] info) =>
            ExecutionEngine.Assert(!InfoFlag(info, Info_Locked), "Contract is locked");

        private static bool IsFactoryAuthorized(object[] info)
        {
            UInt160 launchFactory = (UInt160)info[Info_LaunchFactory];
            UInt160 platformOwner = StorageGetOwner();

            return
                (platformOwner.IsValid && !platformOwner.IsZero && Runtime.CheckWitness(platformOwner)) ||
                (launchFactory.IsValid && !launchFactory.IsZero &&
                    (Runtime.CallingScriptHash == launchFactory || Runtime.EntryScriptHash == launchFactory));
        }

        private static void AssertFactoryAuthorized(object[] info) =>
            ExecutionEngine.Assert(IsFactoryAuthorized(info), "No authorization");

        private static void AddBalance(UInt160 tokenId, UInt160 account, BigInteger amount)
        {
            if (amount <= 0) return;
            StorageSetBalance(tokenId, account, StorageGetBalance(tokenId, account) + amount);
        }

        private static void SubtractBalance(UInt160 tokenId, UInt160 account, BigInteger amount)
        {
            if (amount <= 0) return;
            BigInteger current = StorageGetBalance(tokenId, account);
            ExecutionEngine.Assert(current >= amount, "Insufficient balance");
            StorageSetBalance(tokenId, account, current - amount);
        }

        private static void MintInternal(UInt160 tokenId, UInt160 to, BigInteger amount)
        {
            ExecutionEngine.Assert(to.IsValid && !to.IsZero, "Invalid recipient");
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");

            object[] info = RequireToken(tokenId);
            BigInteger maxSupply = InfoInteger(info, Info_MaxSupply);
            BigInteger currentSupply = StorageGetTotalSupply(tokenId);
            if (maxSupply > 0)
                ExecutionEngine.Assert(currentSupply + amount <= maxSupply, "MaxSupply exceeded");

            StorageSetTotalSupply(tokenId, currentSupply + amount);
            AddBalance(tokenId, to, amount);
            OnTokenTransfer(tokenId, UInt160.Zero, to, amount);
        }

        [Safe]
        public static UInt160 getOwner() => StorageGetOwner();

        [Safe]
        public static bool verify()
        {
            UInt160 owner = StorageGetOwner();
            return owner.IsValid && !owner.IsZero && Runtime.CheckWitness(owner);
        }

        [Safe]
        public static bool isTokenRegistered(UInt160 tokenId) => StorageGetTokenInfoOrNull(tokenId) is not null;

        [Safe]
        public static UInt160 getTokenIdByFacade(UInt160 facadeHash) => StorageGetTokenIdByFacadeOrZero(facadeHash);

        [Safe]
        public static UInt160 getFacade(UInt160 tokenId) => (UInt160)RequireToken(tokenId)[Info_Facade];

        [Safe]
        public static object[] getToken(UInt160 tokenId) => RequireToken(tokenId);

        [Safe]
        public static UInt160 getTokenOwner(UInt160 tokenId) => (UInt160)RequireToken(tokenId)[Info_Owner];

        [Safe]
        public static string getName(UInt160 tokenId) => (string)RequireToken(tokenId)[Info_Name];

        [Safe]
        public static string getSymbol(UInt160 tokenId) => (string)RequireToken(tokenId)[Info_Symbol];

        [Safe]
        public static BigInteger getDecimals(UInt160 tokenId) => InfoInteger(RequireToken(tokenId), Info_Decimals);

        [Safe]
        public static bool getMintable(UInt160 tokenId) => InfoFlag(RequireToken(tokenId), Info_Mintable);

        [Safe]
        public static BigInteger getMaxSupply(UInt160 tokenId) => InfoInteger(RequireToken(tokenId), Info_MaxSupply);

        [Safe]
        public static bool isUpgradeable(UInt160 tokenId) => InfoFlag(RequireToken(tokenId), Info_Upgradeable);

        [Safe]
        public static bool isLocked(UInt160 tokenId) => InfoFlag(RequireToken(tokenId), Info_Locked);

        [Safe]
        public static bool isPausable(UInt160 tokenId) => InfoFlag(RequireToken(tokenId), Info_Pausable);

        [Safe]
        public static bool isPaused(UInt160 tokenId) => InfoFlag(RequireToken(tokenId), Info_Paused);

        [Safe]
        public static string getMetadataUri(UInt160 tokenId) => (string)RequireToken(tokenId)[Info_MetadataUri];

        [Safe]
        public static UInt160 getAuthorizedFactory(UInt160 tokenId) => (UInt160)RequireToken(tokenId)[Info_LaunchFactory];

        [Safe]
        public static BigInteger getPlatformFeeRate(UInt160 tokenId) => InfoInteger(RequireToken(tokenId), Info_PlatformFeeRate);

        [Safe]
        public static BigInteger getCreatorFeeRate(UInt160 tokenId) => InfoInteger(RequireToken(tokenId), Info_CreatorFeeRate);

        [Safe]
        public static BigInteger getBurnRate(UInt160 tokenId) => InfoInteger(RequireToken(tokenId), Info_BurnRate);

        [Safe]
        public static BigInteger getClaimableCreatorFee(UInt160 tokenId) => InfoInteger(RequireToken(tokenId), Info_CreatorClaimable);

        [Safe]
        public static UInt160 getCreatorClaimant(UInt160 tokenId) => (UInt160)RequireToken(tokenId)[Info_CreatorClaimant];

        [Safe]
        public static BigInteger balanceOf(UInt160 tokenId, UInt160 account)
        {
            RequireToken(tokenId);
            return StorageGetBalance(tokenId, account);
        }

        [Safe]
        public static BigInteger totalSupply(UInt160 tokenId)
        {
            RequireToken(tokenId);
            return StorageGetTotalSupply(tokenId);
        }

        [Safe]
        public static object[] quoteTransfer(UInt160 tokenId, UInt160 from, UInt160 to, BigInteger amount)
        {
            object[] info = RequireToken(tokenId);
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
                BigInteger burnRate = InfoInteger(info, Info_BurnRate);
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

            BigInteger platformFeeRate = isMint ? BigInteger.Zero : InfoInteger(info, Info_PlatformFeeRate);
            BigInteger creatorFeeRate = isMint || (UInt160)info[Info_CreatorClaimant] == UInt160.Zero
                ? BigInteger.Zero
                : InfoInteger(info, Info_CreatorFeeRate);

            return new object[]
            {
                grossAmount,
                recipientAmount,
                transferBurnAmount,
                totalTokenBurned,
                platformFeeRate,
                creatorFeeRate,
                platformFeeRate + creatorFeeRate,
                isMint ? BigInteger.One : BigInteger.Zero,
                isDirectBurn ? BigInteger.One : BigInteger.Zero
            };
        }

        public static bool registerToken(
            UInt160 tokenId,
            string name,
            string symbol,
            BigInteger initialSupply,
            BigInteger decimals,
            UInt160 owner,
            BigInteger mintable,
            BigInteger maxSupply,
            BigInteger upgradeable,
            string metadataUri,
            BigInteger pausable,
            UInt160 launchFactory,
            BigInteger platformFeeRate,
            BigInteger creatorFeeRate)
        {
            UInt160 facadeHash = Runtime.CallingScriptHash;

            AssertTokenId(tokenId);
            ExecutionEngine.Assert(facadeHash.IsValid && !facadeHash.IsZero, "Invalid facade");
            ExecutionEngine.Assert(name != null && name.Length > 0, "Name must not be empty");
            ExecutionEngine.Assert(symbol != null && symbol.Length > 0, "Symbol must not be empty");
            ExecutionEngine.Assert(initialSupply >= 0, "InitialSupply must be >= 0");
            ExecutionEngine.Assert(decimals >= 0 && decimals <= 18, "Decimals must be 0-18");
            ExecutionEngine.Assert(owner.IsValid && !owner.IsZero, "Invalid owner address");
            ExecutionEngine.Assert(maxSupply >= 0, "MaxSupply must be >= 0");
            if (maxSupply > 0)
                ExecutionEngine.Assert(initialSupply <= maxSupply, "InitialSupply must not exceed MaxSupply");
            ExecutionEngine.Assert(launchFactory.IsValid && !launchFactory.IsZero, "Invalid launch factory address");
            ExecutionEngine.Assert(platformFeeRate >= 0 && platformFeeRate <= 10_000_000, "PlatformFeeRate exceeds maximum");
            ExecutionEngine.Assert(creatorFeeRate >= 0 && creatorFeeRate <= 5_000_000, "CreatorFeeRate exceeds maximum");
            ExecutionEngine.Assert(StorageGetTokenInfoOrNull(tokenId) is null, "Token already registered");
            ExecutionEngine.Assert(StorageGetTokenIdByFacadeOrZero(facadeHash) == UInt160.Zero, "Facade already registered");

            object[] info = new object[]
            {
                facadeHash,
                owner,
                name,
                symbol,
                decimals,
                mintable != 0 ? BigInteger.One : BigInteger.Zero,
                maxSupply,
                upgradeable != 0 ? BigInteger.One : BigInteger.Zero,
                metadataUri ?? "",
                pausable != 0 ? BigInteger.One : BigInteger.Zero,
                launchFactory,
                platformFeeRate,
                creatorFeeRate,
                BigInteger.Zero,
                BigInteger.Zero,
                owner,
                BigInteger.Zero,
                BigInteger.Zero
            };

            StorageSetTokenInfo(tokenId, info);
            StorageSetFacadeToken(facadeHash, tokenId);

            if (initialSupply > 0)
            {
                StorageSetTotalSupply(tokenId, initialSupply);
                StorageSetBalance(tokenId, owner, initialSupply);
                OnTokenTransfer(tokenId, UInt160.Zero, owner, initialSupply);
            }

            OnTokenRegistered(tokenId, facadeHash, owner, symbol, initialSupply);
            return true;
        }

        public static void setOwner(UInt160 tokenId, UInt160 newOwner)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            ExecutionEngine.Assert(newOwner.IsValid, "Invalid owner address");

            UInt160 previous = (UInt160)info[Info_Owner];
            info[Info_Owner] = newOwner;
            StorageSetTokenInfo(tokenId, info);
            OnTokenOwnerChanged(tokenId, previous, newOwner);
        }

        [DisplayName("lock")]
        public static void Lock(UInt160 tokenId)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            ExecutionEngine.Assert(!InfoFlag(info, Info_Locked), "Already locked");
            info[Info_Locked] = BigInteger.One;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void setMetadataUri(UInt160 tokenId, string uri)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(uri != null && uri.Length > 0, "URI must not be null or empty");
            info[Info_MetadataUri] = uri;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void setMaxSupply(UInt160 tokenId, BigInteger newMax)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(newMax >= 0, "MaxSupply must be >= 0");
            if (newMax > 0)
                ExecutionEngine.Assert(newMax >= StorageGetTotalSupply(tokenId), "NewMaxSupply cannot be less than current totalSupply");

            info[Info_MaxSupply] = newMax;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void setBurnRate(UInt160 tokenId, BigInteger bps)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(bps >= 0 && bps <= 1000, "BurnRate must be 0-1000 basis points");
            info[Info_BurnRate] = bps;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void setCreatorFee(UInt160 tokenId, BigInteger datoshi)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(datoshi >= 0 && datoshi <= 5_000_000, "CreatorFee must be 0-5,000,000 datoshi");
            info[Info_CreatorFeeRate] = datoshi;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void setPlatformFeeRate(UInt160 tokenId, BigInteger datoshi)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertFactoryAuthorized(info);
            ExecutionEngine.Assert(datoshi >= 0, "PlatformFeeRate must be >= 0");
            info[Info_PlatformFeeRate] = datoshi;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void setPausable(UInt160 tokenId, bool value)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(InfoFlag(info, Info_Upgradeable), "Contract is not upgradeable");
            info[Info_Pausable] = value ? BigInteger.One : BigInteger.Zero;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void pause(UInt160 tokenId)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            ExecutionEngine.Assert(InfoFlag(info, Info_Pausable), "Token is not pausable");
            info[Info_Paused] = BigInteger.One;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void unpause(UInt160 tokenId)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            ExecutionEngine.Assert(InfoFlag(info, Info_Pausable), "Token is not pausable");
            info[Info_Paused] = BigInteger.Zero;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void authorizeFactory(UInt160 tokenId, UInt160 newFactory)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertFactoryAuthorized(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(newFactory.IsValid && !newFactory.IsZero, "Invalid factory address");
            info[Info_LaunchFactory] = newFactory;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void mint(UInt160 tokenId, UInt160 to, BigInteger amount)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(InfoFlag(info, Info_Mintable), "Token is not mintable");
            MintInternal(tokenId, to, amount);
        }

        public static void mintByFactory(UInt160 tokenId, UInt160 to, BigInteger amount)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertFactoryAuthorized(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(InfoFlag(info, Info_Mintable), "Token is not mintable");
            MintInternal(tokenId, to, amount);
        }

        public static object[] transfer(UInt160 tokenId, UInt160 from, UInt160 to, BigInteger amount)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            ExecutionEngine.Assert(!InfoFlag(info, Info_Paused), "Token transfers are paused");

            if (!from.IsValid || from.IsZero || !to.IsValid || amount < 0)
                return new object[] { BigInteger.Zero, BigInteger.Zero, BigInteger.Zero };

            if (amount == 0)
                return new object[] { BigInteger.One, BigInteger.Zero, BigInteger.Zero };

            BigInteger balance = StorageGetBalance(tokenId, from);
            if (balance < amount)
                return new object[] { BigInteger.Zero, BigInteger.Zero, BigInteger.Zero };

            BigInteger burnAmount = BigInteger.Zero;
            BigInteger recipientAmount = amount;

            if (to == UInt160.Zero)
            {
                recipientAmount = BigInteger.Zero;
                burnAmount = amount;
            }
            else
            {
                BigInteger burnRate = InfoInteger(info, Info_BurnRate);
                if (burnRate > 0)
                {
                    burnAmount = amount * burnRate / 10000;
                    if (burnAmount > 0)
                    {
                        ExecutionEngine.Assert(amount > burnAmount, "Burn amount exceeds transfer amount");
                        recipientAmount -= burnAmount;
                    }
                }
            }

            StorageSetBalance(tokenId, from, balance - amount);
            if (recipientAmount > 0)
                AddBalance(tokenId, to, recipientAmount);
            if (burnAmount > 0)
                StorageSetTotalSupply(tokenId, StorageGetTotalSupply(tokenId) - burnAmount);

            if (recipientAmount > 0)
                OnTokenTransfer(tokenId, from, to, recipientAmount);
            if (burnAmount > 0)
                OnTokenTransfer(tokenId, from, UInt160.Zero, burnAmount);

            return new object[] { BigInteger.One, recipientAmount, burnAmount };
        }

        public static void transferByFactory(UInt160 tokenId, UInt160 from, UInt160 to, BigInteger amount)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            AssertFactoryAuthorized(info);
            AssertUnlocked(info);
            ExecutionEngine.Assert(from.IsValid && !from.IsZero, "Invalid sender");
            ExecutionEngine.Assert(to.IsValid && !to.IsZero, "Invalid recipient");
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            SubtractBalance(tokenId, from, amount);
            AddBalance(tokenId, to, amount);
            OnTokenTransfer(tokenId, from, to, amount);
        }

        public static void addCreatorClaimable(UInt160 tokenId, BigInteger amount)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            info[Info_CreatorClaimable] = InfoInteger(info, Info_CreatorClaimable) + amount;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void claimCreatorFee(UInt160 tokenId, BigInteger amount)
        {
            object[] info = RequireToken(tokenId);
            AssertCallingFacade(info);

            ExecutionEngine.Assert(amount > 0, "Amount must be positive");

            BigInteger claimable = InfoInteger(info, Info_CreatorClaimable);
            ExecutionEngine.Assert(claimable >= amount, "Insufficient creator fee balance");
            info[Info_CreatorClaimable] = claimable - amount;
            StorageSetTokenInfo(tokenId, info);
        }

        public static void setEngineOwner(UInt160 newOwner)
        {
            UInt160 owner = StorageGetOwner();
            ExecutionEngine.Assert(owner.IsValid && !owner.IsZero && Runtime.CheckWitness(owner), "No authorization");
            ExecutionEngine.Assert(newOwner.IsValid && !newOwner.IsZero, "Invalid owner");
            StorageSetOwner(newOwner);
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;

            UInt160 initialOwner = data is null ? Runtime.Transaction.Sender : (UInt160)data;
            ExecutionEngine.Assert(initialOwner.IsValid && !initialOwner.IsZero, "owner must exist");
            StorageSetOwner(initialOwner);
        }
    }
}
