using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

using System;
using System.ComponentModel;
using System.Numerics;

namespace HushNetwork.Contracts
{
    [DisplayName(nameof(TokenFactory))]
    [ContractAuthor("HushNetwork", "dev@hushnetwork.social")]
    [ContractDescription("Token Factory — deploys NEP-17 TokenTemplate instances on payment")]
    [ContractVersion("1.0.0")]
    [ContractPermission(Permission.Any, Method.Any)]
    public class TokenFactory : Neo.SmartContract.Framework.SmartContract
    {
        // ── Storage prefix constants ──────────────────────────────────────────
        // SmartContract base class reserves NO storage keys — all prefixes are safe.

        private const byte Prefix_TotalTokenCount     = 0x01; // BigInteger: global token counter
        private const byte Prefix_GlobalTokenList     = 0x02; // UInt160 per-index: [0x02]+index
        private const byte Prefix_TokenInfo           = 0x10; // byte[] serialized: [0x10]+contractHash
        private const byte Prefix_CreatorIndex        = 0x20; // compound: [0x20]+creator+[0x00/0x01]+index
        private const byte Prefix_Owner               = 0xff; // UInt160: factory owner
        private const byte Prefix_NefBytes            = 0xf0; // ByteString: stored TokenTemplate NEF
        private const byte Prefix_Manifest            = 0xf1; // string: stored TokenTemplate manifest JSON
        private const byte Prefix_MinFee              = 0xe0; // BigInteger: minimum creation fee (datoshi)
        private const byte Prefix_PremiumTiersEnabled = 0xe1; // BigInteger 0/1: premium tiers flag
        private const byte Prefix_Treasury            = 0xe2; // UInt160: treasury address
        private const byte Prefix_Paused              = 0xe3; // BigInteger 0/1: factory paused flag

        // ── Owner storage helpers ─────────────────────────────────────────────

        private static UInt160 StorageGetOwner()
        {
            return (UInt160)Storage.Get(new[] { Prefix_Owner });
        }

        private static void StorageSetOwner(UInt160 value) =>
            Storage.Put(new[] { Prefix_Owner }, value);

        private static bool IsOwner() =>
            Runtime.CheckWitness(StorageGetOwner());

        // ── Config storage helpers ────────────────────────────────────────────

        private static ByteString StorageGetNefBytes()
        {
            return Storage.Get(new[] { Prefix_NefBytes });
        }

        private static void StorageSetNefBytes(ByteString value) =>
            Storage.Put(new[] { Prefix_NefBytes }, value);

        private static string StorageGetManifest()
        {
            ByteString raw = Storage.Get(new[] { Prefix_Manifest });
            return raw is null ? "" : (string)raw;
        }

        private static void StorageSetManifest(string value) =>
            Storage.Put(new[] { Prefix_Manifest }, value);

        private static BigInteger StorageGetMinFee()
        {
            ByteString raw = Storage.Get(new[] { Prefix_MinFee });
            return raw is null ? 1_500_000_000 : (BigInteger)raw;
        }

        private static void StorageSetMinFee(BigInteger value) =>
            Storage.Put(new[] { Prefix_MinFee }, value);

        private static bool StorageGetPremiumTiersEnabled() =>
            (BigInteger)Storage.Get(new[] { Prefix_PremiumTiersEnabled }) != 0;

        private static void StorageSetPremiumTiersEnabled(bool value)
        {
            if (value)
                Storage.Put(new[] { Prefix_PremiumTiersEnabled }, (BigInteger)1);
            else
                Storage.Delete(new[] { Prefix_PremiumTiersEnabled });
        }

        private static UInt160 StorageGetTreasury()
        {
            return (UInt160)Storage.Get(new[] { Prefix_Treasury });
        }

        private static void StorageSetTreasury(UInt160 value) =>
            Storage.Put(new[] { Prefix_Treasury }, value);

        private static bool StorageGetPaused() =>
            (BigInteger)Storage.Get(new[] { Prefix_Paused }) != 0;

        private static void StorageSetPaused(bool value)
        {
            if (value)
                Storage.Put(new[] { Prefix_Paused }, (BigInteger)1);
            else
                Storage.Delete(new[] { Prefix_Paused });
        }

        // ── Token count helpers ───────────────────────────────────────────────

        private static BigInteger StorageGetTotalTokenCount() =>
            (BigInteger)Storage.Get(new[] { Prefix_TotalTokenCount });

        private static void StorageSetTotalTokenCount(BigInteger value) =>
            Storage.Put(new[] { Prefix_TotalTokenCount }, value);

        // ── Global token list helpers ─────────────────────────────────────────
        // Key: [0x02] + (ByteString)(BigInteger)index  (little-endian, variable length)

        private static UInt160 StorageGetGlobalTokenAtIndex(BigInteger index)
        {
            ByteString key = (ByteString)new byte[] { Prefix_GlobalTokenList } + (ByteString)index;
            return (UInt160)Storage.Get(key);
        }

        private static void StorageSetGlobalTokenAtIndex(BigInteger index, UInt160 hash)
        {
            ByteString key = (ByteString)new byte[] { Prefix_GlobalTokenList } + (ByteString)index;
            Storage.Put(key, hash);
        }

        // ── TokenInfo storage helpers ─────────────────────────────────────────
        // TokenInfo format: object[] { symbol, creator, supply, mode, tier, createdAt }
        // Key: [0x10] + contractHash (20 bytes)

        private static object[] StorageGetTokenInfo(UInt160 contractHash)
        {
            ByteString key = (ByteString)new byte[] { Prefix_TokenInfo } + (ByteString)contractHash;
            ByteString raw = Storage.Get(key);
            return raw is null ? null : (object[])StdLib.Deserialize(raw);
        }

        private static void StorageSetTokenInfo(UInt160 contractHash, object[] info)
        {
            ByteString key = (ByteString)new byte[] { Prefix_TokenInfo } + (ByteString)contractHash;
            Storage.Put(key, StdLib.Serialize(info));
        }

        // ── Per-creator index helpers ─────────────────────────────────────────
        // Count key:  [0x20] + creator (20 bytes) + [0x00]  → BigInteger
        // Token key:  [0x20] + creator (20 bytes) + [0x01] + (ByteString)index → UInt160

        private static BigInteger StorageGetCreatorTokenCount(UInt160 creator)
        {
            ByteString key = (ByteString)new byte[] { Prefix_CreatorIndex }
                + (ByteString)creator
                + (ByteString)new byte[] { 0x00 };
            return (BigInteger)Storage.Get(key);
        }

        private static void StorageSetCreatorTokenCount(UInt160 creator, BigInteger count)
        {
            ByteString key = (ByteString)new byte[] { Prefix_CreatorIndex }
                + (ByteString)creator
                + (ByteString)new byte[] { 0x00 };
            Storage.Put(key, count);
        }

        private static UInt160 StorageGetCreatorTokenAtIndex(UInt160 creator, BigInteger index)
        {
            ByteString key = (ByteString)new byte[] { Prefix_CreatorIndex }
                + (ByteString)creator
                + (ByteString)new byte[] { 0x01 }
                + (ByteString)index;
            return (UInt160)Storage.Get(key);
        }

        private static void StorageSetCreatorTokenAtIndex(UInt160 creator, BigInteger index, UInt160 hash)
        {
            ByteString key = (ByteString)new byte[] { Prefix_CreatorIndex }
                + (ByteString)creator
                + (ByteString)new byte[] { 0x01 }
                + (ByteString)index;
            Storage.Put(key, hash);
        }

        // ── Public owner management ───────────────────────────────────────────
        // Full public API (getTokenCount, getToken, etc.) implemented in Phase 4.

        [Safe]
        public static UInt160 GetOwner()
        {
            return StorageGetOwner();
        }

        public delegate void OnSetOwnerDelegate(UInt160 previousOwner, UInt160 newOwner);

        [DisplayName("SetOwner")]
        public static event OnSetOwnerDelegate OnSetOwner;

        public static void SetOwner(UInt160 newOwner)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No Authorization!");

            ExecutionEngine.Assert(newOwner.IsValid && !newOwner.IsZero, "owner must be valid");

            UInt160 previous = StorageGetOwner();
            StorageSetOwner(newOwner);
            OnSetOwner(previous, newOwner);
        }

        // ── Deploy ────────────────────────────────────────────────────────────

        public static void _deploy(object data, bool update)
        {
            if (update)
            {
                return;
            }

            if (data is null) data = Runtime.Transaction.Sender;

            UInt160 initialOwner = (UInt160)data;

            ExecutionEngine.Assert(initialOwner.IsValid && !initialOwner.IsZero, "owner must exist");

            StorageSetOwner(initialOwner);
            StorageSetMinFee(1_500_000_000); // Default: 15 GAS (covers ~10.17 GAS deployment + margin)
            OnSetOwner(null, initialOwner);
        }

        public static void Update(ByteString nefFile, string manifest, object data = null)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No authorization.");
            ContractManagement.Update(nefFile, manifest, data);
        }

        public static void Destroy()
        {
            if (!IsOwner())
                throw new InvalidOperationException("No authorization.");
            ContractManagement.Destroy();
        }
    }
}
