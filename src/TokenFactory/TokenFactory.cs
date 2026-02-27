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
        private const byte Prefix_ModeParams          = 0x30; // FEAT-078: serialized object[]: [0x30]+contractHash
        private const byte Prefix_Owner               = 0xff; // UInt160: factory owner
        private const byte Prefix_NefBytes            = 0xf0; // ByteString: stored TokenTemplate NEF
        private const byte Prefix_Manifest            = 0xf1; // string: stored TokenTemplate manifest JSON
        private const byte Prefix_MinFee              = 0xe0; // BigInteger: minimum creation fee (datoshi)
        private const byte Prefix_PremiumTiersEnabled = 0xe1; // BigInteger 0/1: premium tiers flag
        private const byte Prefix_Treasury            = 0xe2; // UInt160: treasury address
        private const byte Prefix_Paused              = 0xe3; // BigInteger 0/1: factory paused flag
        private const byte Prefix_UpdateFee           = 0xe4; // FEAT-078: BigInteger (datoshi) — fee to call lifecycle setters via factory
        private const byte Prefix_PlatformFeeRate     = 0xe5; // FEAT-078: BigInteger (datoshi) — per-transfer platform fee passed to deployed tokens

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

        private static BigInteger StorageGetUpdateFee()
        {
            ByteString raw = Storage.Get(new[] { Prefix_UpdateFee });
            return raw is null ? 50_000_000 : (BigInteger)raw;
        }

        private static void StorageSetUpdateFee(BigInteger value) =>
            Storage.Put(new[] { Prefix_UpdateFee }, value);

        private static BigInteger StorageGetPlatformFeeRate()
        {
            ByteString raw = Storage.Get(new[] { Prefix_PlatformFeeRate });
            return raw is null ? 0 : (BigInteger)raw;
        }

        private static void StorageSetPlatformFeeRate(BigInteger value) =>
            Storage.Put(new[] { Prefix_PlatformFeeRate }, value);

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
        // TokenInfo format: object[] { symbol, creator, supply, mode, tier, createdAt, imageUrl, burnRate, maxSupply, locked }
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

        // ── ModeParams storage helpers (FEAT-078) ─────────────────────────────
        // Key: [0x30] + contractHash (20 bytes)
        // Stores mode-specific parameters (e.g., speculation thresholds, crowdfunding targets).
        // Kept in factory registry; no router calls in FEAT-078. Router calls added via
        // factory upgrade in FEAT-073/074 — factory redeploy must then call AuthorizeAllTokens().

        private static object[] StorageGetModeParams(UInt160 contractHash)
        {
            ByteString key = (ByteString)new byte[] { Prefix_ModeParams } + (ByteString)contractHash;
            ByteString raw = Storage.Get(key);
            return raw is null ? null : (object[])StdLib.Deserialize(raw);
        }

        private static void StorageSetModeParams(UInt160 contractHash, object[] value)
        {
            ByteString key = (ByteString)new byte[] { Prefix_ModeParams } + (ByteString)contractHash;
            Storage.Put(key, StdLib.Serialize(value));
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

        // ── Registry append helpers ────────────────────────────────────────────

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

        // ── Events ───────────────────────────────────────────────────────────

        [DisplayName("TokenCreated")]
        public static event Action<UInt160, UInt160, string, BigInteger, string, string> OnTokenCreated;

        // FEAT-078 lifecycle events

        [DisplayName("TokenMinted")]
        public static event Action<UInt160, UInt160, BigInteger, BigInteger> OnTokenMinted;

        [DisplayName("TokenBurnRateSet")]
        public static event Action<UInt160, BigInteger> OnTokenBurnRateSet;

        [DisplayName("TokenMaxSupplySet")]
        public static event Action<UInt160, BigInteger> OnTokenMaxSupplySet;

        [DisplayName("TokenMetadataUpdated")]
        public static event Action<UInt160, string> OnTokenMetadataUpdated;

        [DisplayName("TokenCreatorFeeSet")]
        public static event Action<UInt160, BigInteger> OnTokenCreatorFeeSet;

        [DisplayName("TokenModeChanged")]
        public static event Action<UInt160, string, string> OnTokenModeChanged;

        [DisplayName("TokenLocked")]
        public static event Action<UInt160, UInt160, BigInteger> OnTokenLocked;

        [DisplayName("TokensMigrated")]
        public static event Action<UInt160, BigInteger, BigInteger> OnTokensMigrated;

        [DisplayName("PlatformFeeRateUpdated")]
        public static event Action<BigInteger, BigInteger, BigInteger> OnPlatformFeeRateUpdated;

        // ── onNEP17Payment — factory entry point ──────────────────────────────
        // Triggered by GAS token transfer to this contract.
        // Validates payment, deploys TokenTemplate, writes registry, emits event.
        // On any guard failure, throw → NeoVM aborts the TX → GAS auto-refunded.
        //
        // FEAT-078: GAS payments with null data are fee accumulation receipts (update fees
        // and platform fees collected from lifecycle methods and token transfers). These
        // must be accepted silently — they are not token creation requests.

        [DisplayName("onNEP17Payment")]
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            // Guard 1: Only GAS accepted
            ExecutionEngine.Assert(Runtime.CallingScriptHash == GAS.Hash, "Only GAS accepted");

            // FEAT-078: Early return for fee accumulation payments (null data = not a creation request).
            // Update fees from lifecycle methods and platform fees from token transfers arrive here.
            if (data is null) return;

            // Guard 2: Factory not paused
            ExecutionEngine.Assert(!StorageGetPaused(), "Factory is paused");

            // Guard 3: Factory initialized (NEF bytes stored)
            ByteString nef = StorageGetNefBytes();
            ExecutionEngine.Assert(nef is not null, "Factory not initialized");

            // Guard 4: Sufficient fee
            ExecutionEngine.Assert(amount >= StorageGetMinFee(), "Insufficient fee");

            // Guard 5: Data format — expect object[]{name, symbol, supply, decimals, mode, imageUrl, creatorFeeRate}
            object[] tokenData = (object[])data;
            ExecutionEngine.Assert(tokenData.Length == 7, "Expected 7 data elements");

            // Guard 6: Mode check — only "community" supported in FEAT-070
            string mode = (string)tokenData[4];
            ExecutionEngine.Assert(mode == "community", "Unsupported mode");

            string imageUrl             = (string)tokenData[5];
            BigInteger creatorFeeRate   = (BigInteger)tokenData[6];

            // Validate creatorFeeRate before passing to token
            ExecutionEngine.Assert(creatorFeeRate >= 0 && creatorFeeRate <= 5_000_000, "creatorFeeRate exceeds maximum");

            // Extract token parameters from payment data
            string name         = (string)tokenData[0];
            string symbol       = (string)tokenData[1];
            BigInteger supply   = (BigInteger)tokenData[2];
            BigInteger decimals = (BigInteger)tokenData[3];

            // Build 13-element deploy params for TokenTemplate._deploy()
            // Bool params (mintable, upgradeable, pausable) MUST be BigInteger 0/1, NOT C# bool
            object[] tokenParams = new object[]
            {
                name,                                   // [0] name
                symbol,                                 // [1] symbol
                supply,                                 // [2] initialSupply
                decimals,                               // [3] decimals (TokenTemplate casts to byte)
                from,                                   // [4] owner — payment sender becomes token owner
                (BigInteger)1,                          // [5] mintable = true (FEAT-078: factory controls minting)
                (BigInteger)0,                          // [6] maxSupply = uncapped
                (BigInteger)0,                          // [7] upgradeable = false
                imageUrl,                               // [8] metadataUri — user-supplied image/icon URL
                (BigInteger)0,                          // [9] pausable = false
                Runtime.ExecutingScriptHash,            // [10] authorizedFactory — this factory is the authorized caller
                StorageGetPlatformFeeRate(),            // [11] platformFeeRate — current factory-level platform fee
                creatorFeeRate,                         // [12] creatorFeeRate — per-transfer fee to token creator
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
                (BigInteger)0,              // [7] burnRate (basis points) — 0 at creation
                (BigInteger)0,              // [8] maxSupply — 0 = uncapped (community mode)
                (BigInteger)0,              // [9] locked — 0 = unlocked at creation
            };
            StorageSetTokenInfo(contractHash, tokenInfo);

            // Emit event
            OnTokenCreated(contractHash, from, symbol, supply, mode, "standard");
        }

        // ── Public owner management ───────────────────────────────────────────

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

        // ── Registry query API ────────────────────────────────────────────────

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

        [Safe]
        public static BigInteger GetUpdateFee() =>
            StorageGetUpdateFee();

        [Safe]
        public static BigInteger GetPlatformFeeRate() =>
            StorageGetPlatformFeeRate();

        [Safe]
        public static object[] GetModeParams(UInt160 tokenHash) =>
            StorageGetModeParams(tokenHash);

        // ── Admin functions ───────────────────────────────────────────────────

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

        public static void SetUpdateFee(BigInteger newFee)
        {
            ExecutionEngine.Assert(IsOwner(), "Unauthorized");
            ExecutionEngine.Assert(newFee >= 0, "Fee must be non-negative");
            StorageSetUpdateFee(newFee);
        }

        // ── FEAT-078: Token lifecycle methods ─────────────────────────────────
        // All creator lifecycle methods follow the same guard pattern:
        //   1. Load tokenInfo — assert token exists
        //   2. Assert Runtime.CheckWitness(creator) — only the creator
        //   3. Assert tokenInfo[9] == 0 — token not locked
        //   4. Validate inputs
        //   5. Collect update fee via GAS.Transfer (except LockToken — no fee for irreversible ops)
        //   6. Delegate to token via Contract.Call
        //   7. Update factory registry
        //   8. Emit event

        // ── Task 4.1: MintTokens ──────────────────────────────────────────────

        public static void MintTokens(UInt160 tokenHash, UInt160 to, BigInteger amount)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");
            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token is locked");
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            ExecutionEngine.Assert(to.IsValid && !to.IsZero, "Invalid recipient");
            GAS.Transfer(creator, Runtime.ExecutingScriptHash, StorageGetUpdateFee(), null);
            Contract.Call(tokenHash, "mintByFactory", CallFlags.All, new object[] { to, amount });
            tokenInfo[2] = (BigInteger)tokenInfo[2] + amount;
            StorageSetTokenInfo(tokenHash, tokenInfo);
            OnTokenMinted(tokenHash, to, amount, (BigInteger)Runtime.Time);
        }

        // ── Task 4.2: SetTokenBurnRate, SetTokenMaxSupply, UpdateTokenMetadata ─

        public static void SetTokenBurnRate(UInt160 tokenHash, BigInteger bps)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");
            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token is locked");
            ExecutionEngine.Assert(bps >= 0 && bps <= 1000, "Burn rate out of range");
            GAS.Transfer(creator, Runtime.ExecutingScriptHash, StorageGetUpdateFee(), null);
            Contract.Call(tokenHash, "setBurnRate", CallFlags.All, new object[] { bps });
            tokenInfo[7] = bps;
            StorageSetTokenInfo(tokenHash, tokenInfo);
            OnTokenBurnRateSet(tokenHash, bps);
        }

        public static void SetTokenMaxSupply(UInt160 tokenHash, BigInteger newMax)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");
            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token is locked");
            GAS.Transfer(creator, Runtime.ExecutingScriptHash, StorageGetUpdateFee(), null);
            Contract.Call(tokenHash, "setMaxSupply", CallFlags.All, new object[] { newMax });
            tokenInfo[8] = newMax;
            StorageSetTokenInfo(tokenHash, tokenInfo);
            OnTokenMaxSupplySet(tokenHash, newMax);
        }

        public static void UpdateTokenMetadata(UInt160 tokenHash, string imageUrl)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");
            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token is locked");
            ExecutionEngine.Assert(imageUrl != null && imageUrl.Length > 0, "imageUrl must not be empty");
            GAS.Transfer(creator, Runtime.ExecutingScriptHash, StorageGetUpdateFee(), null);
            Contract.Call(tokenHash, "setMetadataUri", CallFlags.All, new object[] { imageUrl });
            tokenInfo[6] = imageUrl;
            StorageSetTokenInfo(tokenHash, tokenInfo);
            OnTokenMetadataUpdated(tokenHash, imageUrl);
        }

        // ── Task 4.3: SetCreatorFee ───────────────────────────────────────────

        public static void SetCreatorFee(UInt160 tokenHash, BigInteger newRate)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");
            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token is locked");
            ExecutionEngine.Assert(newRate >= 0 && newRate <= 5_000_000, "Creator fee out of range");
            GAS.Transfer(creator, Runtime.ExecutingScriptHash, StorageGetUpdateFee(), null);
            Contract.Call(tokenHash, "setCreatorFee", CallFlags.All, new object[] { newRate });
            // creatorFeeRate is NOT tracked in tokenInfo (lives on the token); event is sufficient for indexer
            OnTokenCreatorFeeSet(tokenHash, newRate);
        }

        // ── Task 4.4: ChangeTokenMode ─────────────────────────────────────────
        // Mode transition matrix:
        //   community   → speculation   ✅
        //   community   → crowdfunding  ✅
        //   speculation → community     ✅  (pre-launch only; router check deferred to FEAT-073)
        //   crowdfunding→ community     ✅  (pre-presale only; router check deferred to FEAT-074)
        //   speculation → crowdfunding  ❌
        //   crowdfunding→ speculation   ❌
        //
        // modeParams stored in factory registry (Prefix_ModeParams).
        // When FEAT-073/074 add router calls, this factory must be redeployed.
        // Factory redeploy must call AuthorizeAllTokens() to update each token's authorizedFactory.

        public static void ChangeTokenMode(UInt160 tokenHash, string newMode, object[] modeParams)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");
            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token is locked");
            string oldMode = (string)tokenInfo[3];
            bool validTransition =
                (oldMode == "community"    && (newMode == "speculation" || newMode == "crowdfunding")) ||
                (oldMode == "speculation"  && newMode == "community") ||
                (oldMode == "crowdfunding" && newMode == "community");
            ExecutionEngine.Assert(validTransition, "Invalid mode transition");
            GAS.Transfer(creator, Runtime.ExecutingScriptHash, StorageGetUpdateFee(), null);
            if (modeParams != null && modeParams.Length > 0)
                StorageSetModeParams(tokenHash, modeParams);
            tokenInfo[3] = newMode;
            StorageSetTokenInfo(tokenHash, tokenInfo);
            OnTokenModeChanged(tokenHash, oldMode, newMode);
        }

        // ── Task 4.5: LockToken ───────────────────────────────────────────────
        // Locking is irreversible — no update fee is charged (fee would be anti-user-trust
        // for a permanent surrender of control).

        public static void LockToken(UInt160 tokenHash)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");
            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token already locked");
            Contract.Call(tokenHash, "lock", CallFlags.All, new object[0]);
            tokenInfo[9] = (BigInteger)1;
            StorageSetTokenInfo(tokenHash, tokenInfo);
            OnTokenLocked(tokenHash, creator, (BigInteger)Runtime.Time);
        }

        // Atomic lifecycle batch endpoint used by FEAT-078 staged changes.
        // Sentinel values:
        //   imageUrl      = ""   -> unchanged
        //   burnRate      = -1   -> unchanged
        //   creatorFee    = -1   -> unchanged
        //   newMode       = ""   -> unchanged
        //   newMaxSupply  = -1   -> unchanged
        //   mintAmount    = 0    -> unchanged (mintTo ignored)
        //   lockToken     = false-> unchanged
        //
        // Fee model:
        //   One update fee is charged when at least one fee-bearing change is present
        //   (metadata, burn rate, creator fee, mode, max supply, mint).
        //   Lock-only batch remains fee-free, consistent with LockToken().
        public static void ApplyTokenChanges(
            UInt160 tokenHash,
            string imageUrl,
            BigInteger burnRate,
            BigInteger creatorFeeRate,
            string newMode,
            object[] modeParams,
            BigInteger newMaxSupply,
            UInt160 mintTo,
            BigInteger mintAmount,
            bool lockToken)
        {
            object[] tokenInfo = StorageGetTokenInfo(tokenHash);
            ExecutionEngine.Assert(tokenInfo != null, "Token not found");

            UInt160 creator = (UInt160)tokenInfo[1];
            ExecutionEngine.Assert(Runtime.CheckWitness(creator), "No authorization");
            ExecutionEngine.Assert((BigInteger)tokenInfo[9] == 0, "Token is locked");

            bool applyMetadata   = imageUrl != null && imageUrl.Length > 0;
            bool applyBurnRate   = burnRate >= 0;
            bool applyCreatorFee = creatorFeeRate >= 0;
            bool applyMode       = newMode != null && newMode.Length > 0;
            bool applyMaxSupply  = newMaxSupply >= 0;
            bool applyMint       = mintAmount > 0;
            bool applyLock       = lockToken;

            bool hasAny =
                applyMetadata || applyBurnRate || applyCreatorFee ||
                applyMode || applyMaxSupply || applyMint || applyLock;
            ExecutionEngine.Assert(hasAny, "No changes requested");
            ExecutionEngine.Assert(
                !(applyMaxSupply && applyMint),
                "Cannot combine maxSupply and mint in one batch");

            bool chargeFee =
                applyMetadata || applyBurnRate || applyCreatorFee ||
                applyMode || applyMaxSupply || applyMint;
            if (chargeFee)
                GAS.Transfer(creator, Runtime.ExecutingScriptHash, StorageGetUpdateFee(), null);

            if (applyMetadata)
            {
                Contract.Call(tokenHash, "setMetadataUri", CallFlags.All, new object[] { imageUrl });
                tokenInfo[6] = imageUrl;
                OnTokenMetadataUpdated(tokenHash, imageUrl);
            }

            if (applyBurnRate)
            {
                ExecutionEngine.Assert(burnRate <= 1000, "Burn rate out of range");
                Contract.Call(tokenHash, "setBurnRate", CallFlags.All, new object[] { burnRate });
                tokenInfo[7] = burnRate;
                OnTokenBurnRateSet(tokenHash, burnRate);
            }

            if (applyCreatorFee)
            {
                ExecutionEngine.Assert(creatorFeeRate <= 5_000_000, "Creator fee out of range");
                Contract.Call(tokenHash, "setCreatorFee", CallFlags.All, new object[] { creatorFeeRate });
                OnTokenCreatorFeeSet(tokenHash, creatorFeeRate);
            }

            if (applyMode)
            {
                string oldMode = (string)tokenInfo[3];
                bool validTransition =
                    (oldMode == "community"    && (newMode == "speculation" || newMode == "crowdfunding")) ||
                    (oldMode == "speculation"  && newMode == "community") ||
                    (oldMode == "crowdfunding" && newMode == "community");
                ExecutionEngine.Assert(validTransition, "Invalid mode transition");
                if (modeParams != null && modeParams.Length > 0)
                    StorageSetModeParams(tokenHash, modeParams);
                tokenInfo[3] = newMode;
                OnTokenModeChanged(tokenHash, oldMode, newMode);
            }

            if (applyMaxSupply)
            {
                Contract.Call(tokenHash, "setMaxSupply", CallFlags.All, new object[] { newMaxSupply });
                tokenInfo[8] = newMaxSupply;
                OnTokenMaxSupplySet(tokenHash, newMaxSupply);
            }

            if (applyMint)
            {
                ExecutionEngine.Assert(mintTo.IsValid && !mintTo.IsZero, "Invalid recipient");
                Contract.Call(tokenHash, "mintByFactory", CallFlags.All, new object[] { mintTo, mintAmount });
                tokenInfo[2] = (BigInteger)tokenInfo[2] + mintAmount;
                OnTokenMinted(tokenHash, mintTo, mintAmount, (BigInteger)Runtime.Time);
            }

            if (applyLock)
            {
                Contract.Call(tokenHash, "lock", CallFlags.All, new object[0]);
                tokenInfo[9] = (BigInteger)1;
                OnTokenLocked(tokenHash, creator, (BigInteger)Runtime.Time);
            }

            StorageSetTokenInfo(tokenHash, tokenInfo);
        }

        // ── Task 4.6: AuthorizeAllTokens, SetAllTokensPlatformFee ────────────
        // Paginated batch admin methods — both owner-only, batchSize capped at 50.
        // Used for factory migration (AuthorizeAllTokens) and global fee updates.

        public static void AuthorizeAllTokens(UInt160 newFactoryHash, BigInteger offset, BigInteger batchSize)
        {
            ExecutionEngine.Assert(IsOwner(), "Unauthorized");
            ExecutionEngine.Assert(newFactoryHash.IsValid && !newFactoryHash.IsZero, "Invalid factory hash");
            if (batchSize > 50) batchSize = 50;
            BigInteger total = StorageGetTotalTokenCount();
            BigInteger count = 0;
            int batchInt = (int)batchSize;
            for (int i = 0; i < batchInt; i++)
            {
                BigInteger idx = offset + i;
                if (idx >= total) break;
                UInt160 tokenHash = StorageGetGlobalTokenAtIndex(idx);
                if (tokenHash is not null)
                {
                    Contract.Call(tokenHash, "authorizeFactory", CallFlags.All, new object[] { newFactoryHash });
                    count++;
                }
            }
            OnTokensMigrated(newFactoryHash, offset, count);
        }

        public static void SetAllTokensPlatformFee(BigInteger newRate, BigInteger offset, BigInteger batchSize)
        {
            ExecutionEngine.Assert(IsOwner(), "Unauthorized");
            ExecutionEngine.Assert(newRate >= 0, "Rate must be non-negative");
            if (batchSize > 50) batchSize = 50;
            BigInteger total = StorageGetTotalTokenCount();
            BigInteger count = 0;
            int batchInt = (int)batchSize;
            for (int i = 0; i < batchInt; i++)
            {
                BigInteger idx = offset + i;
                if (idx >= total) break;
                UInt160 tokenHash = StorageGetGlobalTokenAtIndex(idx);
                if (tokenHash is not null)
                {
                    Contract.Call(tokenHash, "setPlatformFeeRate", CallFlags.All, new object[] { newRate });
                    count++;
                }
            }
            // Update factory default so future tokens get the new platform fee rate
            StorageSetPlatformFeeRate(newRate);
            OnPlatformFeeRateUpdated(newRate, offset, count);
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
            StorageSetMinFee(1_500_000_000);    // Default: 15 GAS (covers ~10.17 GAS deployment + margin)
            StorageSetUpdateFee(50_000_000);    // FEAT-078 default: 0.5 GAS per lifecycle setter call
            StorageSetPlatformFeeRate(0);       // FEAT-078 default: no platform fee on transfers
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
