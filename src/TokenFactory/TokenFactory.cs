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
        // TokenInfo format: object[] { symbol, creator, supply, mode, tier, createdAt, imageUrl }
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

        // ── Registry append helpers (Phase 3) ────────────────────────────────

        private static void AppendGlobalToken(UInt160 contractHash)
        {
            BigInteger index = StorageGetTotalTokenCount();
            StorageSetGlobalTokenAtIndex(index, contractHash);
            StorageSetTotalTokenCount(index + 1);
        }

        private static void AppendCreatorToken(UInt160 creator, UInt160 contractHash)
        {
            BigInteger index = StorageGetCreatorTokenCount(creator);
            StorageSetCreatorTokenAtIndex(creator, index, contractHash);
            StorageSetCreatorTokenCount(creator, index + 1);
        }

        // ── TokenCreated event ────────────────────────────────────────────────
        // args: contractHash, creator, symbol, supply, mode, tier

        [DisplayName("TokenCreated")]
        public static event Action<UInt160, UInt160, string, BigInteger, string, string> OnTokenCreated;

        // ── onNEP17Payment — factory entry point ──────────────────────────────
        // Triggered by GAS token transfer to this contract.
        // Validates payment, deploys TokenTemplate, writes registry, emits event.
        // On any guard failure, throw → NeoVM aborts the TX → GAS auto-refunded.

        [DisplayName("onNEP17Payment")]
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            // Guard 1: Only GAS accepted
            ExecutionEngine.Assert(Runtime.CallingScriptHash == GAS.Hash, "Only GAS accepted");

            // Guard 2: Factory not paused
            ExecutionEngine.Assert(!StorageGetPaused(), "Factory is paused");

            // Guard 3: Factory initialized (NEF bytes stored)
            ByteString nef = StorageGetNefBytes();
            ExecutionEngine.Assert(nef is not null, "Factory not initialized");

            // Guard 4: Sufficient fee
            ExecutionEngine.Assert(amount >= StorageGetMinFee(), "Insufficient fee");

            // Guard 5: Data format — expect object[]{name, symbol, supply, decimals, mode, imageUrl}
            object[] tokenData = (object[])data;
            ExecutionEngine.Assert(tokenData.Length == 6, "Expected 6 data elements");

            // Guard 6: Mode check — only "community" supported in FEAT-070
            string mode = (string)tokenData[4];
            ExecutionEngine.Assert(mode == "community", "Unsupported mode");

            string imageUrl = (string)tokenData[5];

            // Extract token parameters from payment data
            string name         = (string)tokenData[0];
            string symbol       = (string)tokenData[1];
            BigInteger supply   = (BigInteger)tokenData[2];
            BigInteger decimals = (BigInteger)tokenData[3];

            // Build 10-element deploy params for TokenTemplate._deploy()
            // Bool params (mintable, upgradeable, pausable) MUST be BigInteger 0, NOT C# bool false
            object[] tokenParams = new object[]
            {
                name,           // [0] name
                symbol,         // [1] symbol
                supply,         // [2] initialSupply
                decimals,       // [3] decimals (TokenTemplate casts to byte)
                from,           // [4] owner — payment sender becomes token owner
                (BigInteger)0,  // [5] mintable = false
                (BigInteger)0,  // [6] maxSupply = uncapped
                (BigInteger)0,  // [7] upgradeable = false
                imageUrl,       // [8] metadataUri — user-supplied image/icon URL
                (BigInteger)0,  // [9] pausable = false
            };

            // Deploy the TokenTemplate instance.
            // Neo contract hash = Hash160(callerHash || nefChecksum || manifestName).
            // Since callerHash (this factory) and the NEF are fixed, we must vary the manifest
            // name per deployment to prevent "Contract Already Exists" collisions.
            // We use the current global token count as a unique suffix: "TT0", "TT1", etc.
            // The stored manifest starts with {"name":"TokenTemplate" (23 chars); we replace
            // only the name field while preserving all ABI/groups/trust data unchanged.
            string manifest = StorageGetManifest();
            BigInteger count = StorageGetTotalTokenCount();
            string uniqueManifest = "{\"name\":\"TT" + StdLib.Itoa(count, 10) + "\"" + manifest.Substring(23);
            Contract deployed = ContractManagement.Deploy(nef, uniqueManifest, tokenParams);
            UInt160 contractHash = deployed.Hash;

            // Write registry: global list + per-creator index + token info record
            AppendGlobalToken(contractHash);
            AppendCreatorToken(from, contractHash);

            object[] tokenInfo = new object[]
            {
                symbol,                     // [0] symbol
                from,                       // [1] creator
                supply,                     // [2] supply
                mode,                       // [3] mode ("community")
                "standard",                 // [4] tier
                (BigInteger)Runtime.Time,   // [5] createdAt (block timestamp)
                imageUrl,                   // [6] imageUrl
            };
            StorageSetTokenInfo(contractHash, tokenInfo);

            // Emit event
            OnTokenCreated(contractHash, from, symbol, supply, mode, "standard");
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

        // ── Registry query API (Phase 4) ─────────────────────────────────────

        [Safe]
        public static BigInteger GetTokenCount() =>
            StorageGetTotalTokenCount();

        [Safe]
        public static object[] GetToken(UInt160 hash) =>
            StorageGetTokenInfo(hash);

        /// <summary>
        /// Returns a page of token hashes created by the given address.
        /// page is 0-indexed; pageSize is the max items per page.
        /// Returns empty array if creator has no tokens or page is out of range.
        /// </summary>
        [Safe]
        public static UInt160[] GetTokensByCreator(UInt160 creator, BigInteger page, BigInteger pageSize)
        {
            BigInteger count = StorageGetCreatorTokenCount(creator);
            BigInteger startIndex = page * pageSize;
            if (startIndex >= count)
                return new UInt160[0];
            BigInteger end = startIndex + pageSize;
            if (end > count) end = count;
            int len = (int)(end - startIndex);
            UInt160[] result = new UInt160[len];
            for (int i = 0; i < len; i++)
                result[i] = StorageGetCreatorTokenAtIndex(creator, startIndex + i);
            return result;
        }

        [Safe]
        public static bool IsInitialized() =>
            StorageGetNefBytes() is not null;

        [Safe]
        public static bool IsPaused() =>
            StorageGetPaused();

        [Safe]
        public static BigInteger GetMinFee() =>
            StorageGetMinFee();

        [Safe]
        public static UInt160 GetTreasury() =>
            StorageGetTreasury();

        [Safe]
        public static bool GetPremiumTiersEnabled() =>
            StorageGetPremiumTiersEnabled();

        // ── Admin functions (Phase 4) ─────────────────────────────────────────
        // All admin methods begin with: Assert(Runtime.CheckWitness(GetOwner()), "Unauthorized")
        // Note: SetOwner() is implemented in Phase 2 (public owner management above).

        public static void SetNefAndManifest(ByteString nef, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "Unauthorized");
            ExecutionEngine.Assert(nef is not null, "NEF must not be empty");
            ExecutionEngine.Assert(nef.Length > 0, "NEF must not be empty");
            ExecutionEngine.Assert(manifest != null && manifest.Length > 0, "Manifest must not be empty");
            StorageSetNefBytes(nef);
            StorageSetManifest(manifest);
        }

        public static void SetFee(BigInteger standardFeeDataoshi)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "Unauthorized");
            ExecutionEngine.Assert(standardFeeDataoshi > 0, "Fee must be positive");
            StorageSetMinFee(standardFeeDataoshi);
        }

        public static void SetTreasuryAddress(UInt160 address)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "Unauthorized");
            ExecutionEngine.Assert(address.IsValid && !address.IsZero, "Address must be valid");
            StorageSetTreasury(address);
        }

        public static void SetPremiumTiersEnabled(bool enabled)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "Unauthorized");
            StorageSetPremiumTiersEnabled(enabled);
        }

        public static void Pause()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "Unauthorized");
            StorageSetPaused(true);
        }

        public static void Unpause()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "Unauthorized");
            StorageSetPaused(false);
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
