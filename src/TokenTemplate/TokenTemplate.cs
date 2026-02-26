using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract.Template
{
    [DisplayName(nameof(TokenTemplate))]
    [ContractAuthor("HushNetwork", "dev@hushnetwork.social")]
    [ContractDescription("HushNetwork Token Launcher — parameterized NEP-17 token template")]
    [ContractVersion("1.0.0")]
    [ContractSourceCode("https://github.com/Hushnetwork-social/hush-neo-contracts/tree/master/src/TokenTemplate/TokenTemplate.cs")]
    [ContractPermission(Permission.Any, Method.Any)]
    [SupportedStandards(NepStandard.Nep17)]
    public class TokenTemplate : Neo.SmartContract.Framework.Nep17Token
    {
        // ── Storage prefix constants ──────────────────────────────────────────
        // Base class reserves:
        //   0x00 — Prefix_TotalSupply (TokenContract)
        //   0x01 — Prefix_Balance     (TokenContract, per-account StorageMap)
        // Custom keys use 0x10–0x19 range. Owner uses 0xff (scaffold compat).

        private const byte Prefix_Name        = 0x10;
        private const byte Prefix_Symbol      = 0x11;
        private const byte Prefix_Decimals    = 0x12;
        private const byte Prefix_Mintable    = 0x13;
        private const byte Prefix_MaxSupply   = 0x14;
        private const byte Prefix_Upgradeable = 0x15;
        private const byte Prefix_Locked      = 0x16;
        private const byte Prefix_Pausable    = 0x17;
        private const byte Prefix_Paused      = 0x18;
        private const byte Prefix_MetadataUri       = 0x19;
        private const byte Prefix_AuthorizedFactory = 0x1a; // FEAT-078: UInt160 — factory allowed to call setters
        private const byte Prefix_PlatformFeeRate   = 0x1b; // FEAT-078: BigInteger (datoshi) — per-transfer fee to factory
        private const byte Prefix_CreatorFeeRate    = 0x1c; // FEAT-078: BigInteger (datoshi) — per-transfer fee to creator
        private const byte Prefix_BurnRate          = 0x1d; // FEAT-078: BigInteger (basis points 0–1000) — tokens burned per transfer
        private const byte Prefix_Owner             = 0xff;

        // ── Private storage helpers ───────────────────────────────────────────
        // StorageGet*/StorageSet* are the raw read/write layer.
        // No business-rule validation here — that belongs in public methods.

        private static string StorageGetName()
        {
            ByteString raw = Storage.Get(new[] { Prefix_Name });
            return raw is null ? "" : (string)raw;
        }
        private static void StorageSetName(string value) =>
            Storage.Put(new[] { Prefix_Name }, value);

        private static string StorageGetSymbol()
        {
            ByteString raw = Storage.Get(new[] { Prefix_Symbol });
            return raw is null ? "" : (string)raw;
        }
        private static void StorageSetSymbol(string value) =>
            Storage.Put(new[] { Prefix_Symbol }, value);

        private static byte StorageGetDecimals()
        {
            ByteString raw = Storage.Get(new[] { Prefix_Decimals });
            return raw is null ? (byte)0 : (byte)(BigInteger)raw;
        }
        private static void StorageSetDecimals(byte value) =>
            Storage.Put(new[] { Prefix_Decimals }, (BigInteger)value);

        private static bool StorageGetMintable() =>
            (BigInteger)Storage.Get(new[] { Prefix_Mintable }) != 0;
        private static void StorageSetMintable(bool value)
        {
            if (value)
                Storage.Put(new[] { Prefix_Mintable }, (BigInteger)1);
            else
                Storage.Delete(new[] { Prefix_Mintable });
        }

        private static BigInteger StorageGetMaxSupply() =>
            (BigInteger)Storage.Get(new[] { Prefix_MaxSupply });
        private static void StorageSetMaxSupply(BigInteger value) =>
            Storage.Put(new[] { Prefix_MaxSupply }, value);

        private static bool StorageGetUpgradeable() =>
            (BigInteger)Storage.Get(new[] { Prefix_Upgradeable }) != 0;
        private static void StorageSetUpgradeable(bool value)
        {
            if (value)
                Storage.Put(new[] { Prefix_Upgradeable }, (BigInteger)1);
            else
                Storage.Delete(new[] { Prefix_Upgradeable });
        }

        private static bool StorageGetLocked() =>
            (BigInteger)Storage.Get(new[] { Prefix_Locked }) != 0;
        private static void StorageSetLocked(bool value)
        {
            if (value)
                Storage.Put(new[] { Prefix_Locked }, (BigInteger)1);
            else
                Storage.Delete(new[] { Prefix_Locked });
        }

        private static bool StorageGetPausable() =>
            (BigInteger)Storage.Get(new[] { Prefix_Pausable }) != 0;
        private static void StorageSetPausable(bool value)
        {
            if (value)
                Storage.Put(new[] { Prefix_Pausable }, (BigInteger)1);
            else
                Storage.Delete(new[] { Prefix_Pausable });
        }

        private static bool StorageGetPaused() =>
            (BigInteger)Storage.Get(new[] { Prefix_Paused }) != 0;
        private static void StorageSetPaused(bool value)
        {
            if (value)
                Storage.Put(new[] { Prefix_Paused }, (BigInteger)1);
            else
                Storage.Delete(new[] { Prefix_Paused });
        }

        private static string StorageGetMetadataUri()
        {
            ByteString raw = Storage.Get(new[] { Prefix_MetadataUri });
            return raw is null ? "" : (string)raw;
        }
        private static void StorageSetMetadataUri(string value) =>
            Storage.Put(new[] { Prefix_MetadataUri }, value);

        private static UInt160 StorageGetAuthorizedFactory() =>
            (UInt160)Storage.Get(new[] { Prefix_AuthorizedFactory });
        private static void StorageSetAuthorizedFactory(UInt160 value) =>
            Storage.Put(new[] { Prefix_AuthorizedFactory }, value);

        private static BigInteger StorageGetPlatformFeeRate() =>
            (BigInteger)Storage.Get(new[] { Prefix_PlatformFeeRate });
        private static void StorageSetPlatformFeeRate(BigInteger value) =>
            Storage.Put(new[] { Prefix_PlatformFeeRate }, value);

        private static BigInteger StorageGetCreatorFeeRate() =>
            (BigInteger)Storage.Get(new[] { Prefix_CreatorFeeRate });
        private static void StorageSetCreatorFeeRate(BigInteger value) =>
            Storage.Put(new[] { Prefix_CreatorFeeRate }, value);

        private static BigInteger StorageGetBurnRate() =>
            (BigInteger)Storage.Get(new[] { Prefix_BurnRate });
        private static void StorageSetBurnRate(BigInteger value) =>
            Storage.Put(new[] { Prefix_BurnRate }, value);

        private static UInt160 StorageGetOwner() =>
            (UInt160)Storage.Get(new[] { Prefix_Owner });
        private static void StorageSetOwner(UInt160 value) =>
            Storage.Put(new[] { Prefix_Owner }, value);

        // ── Owner ─────────────────────────────────────────────────────────────

        [Safe]
        public static UInt160 getOwner() => StorageGetOwner();

        private static bool IsOwner() =>
            Runtime.CheckWitness(getOwner());

        public delegate void OnOwnerChangedDelegate(UInt160 previousOwner, UInt160 newOwner);

        [DisplayName("OwnerChanged")]
        public static event OnOwnerChangedDelegate OnOwnerChanged;

        public delegate void OnLockedDelegate(ulong timestamp);

        [DisplayName("Locked")]
        public static event OnLockedDelegate OnLocked;

        // newOwner may be UInt160.Zero to renounce ownership.
        public static void setOwner(UInt160 newOwner)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No Authorization");

            ExecutionEngine.Assert(newOwner.IsValid, "Invalid owner address");

            UInt160 previous = getOwner();
            StorageSetOwner(newOwner);
            OnOwnerChanged(previous, newOwner);
        }

        // Irrevocably locks the token — no lifecycle changes after this. FEAT-078.
        // Guard: !locked first (cheapest), then CallingScriptHash == authorizedFactory.
        [DisplayName("lock")]
        public static void Lock()
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Already locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            StorageSetLocked(true);
            OnLocked(Runtime.Time);
        }

        // ── Public read-only API ──────────────────────────────────────────────

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
        public static UInt160 getAuthorizedFactory() => StorageGetAuthorizedFactory();

        [Safe]
        public static BigInteger getPlatformFeeRate() => StorageGetPlatformFeeRate();

        [Safe]
        public static BigInteger getCreatorFeeRate() => StorageGetCreatorFeeRate();

        [Safe]
        public static BigInteger getBurnRate() => StorageGetBurnRate();

        // ── Factory lifecycle setters (FEAT-078) ──────────────────────────────
        // Guard pattern for all setters below:
        //   1. Assert !StorageGetLocked()                                    — cheapest check first
        //   2. Assert Runtime.CallingScriptHash == StorageGetAuthorizedFactory() — only factory

        public delegate void OnBurnRateSetDelegate(UInt160 caller, BigInteger newRate, ulong timestamp);

        [DisplayName("BurnRateSet")]
        public static event OnBurnRateSetDelegate OnBurnRateSet;

        public delegate void OnMetadataUriSetDelegate(UInt160 caller, string uri, ulong timestamp);

        [DisplayName("MetadataUriSet")]
        public static event OnMetadataUriSetDelegate OnMetadataUriSet;

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

        // Sets the per-transfer token burn rate in basis points. 100 = 1%, 1000 = 10% (max).
        public static void SetBurnRate(BigInteger bps)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(bps >= 0 && bps <= 1000, "BurnRate must be 0–1000 basis points");
            StorageSetBurnRate(bps);
            OnBurnRateSet(Runtime.CallingScriptHash, bps, Runtime.Time);
        }

        // Updates the IPFS/HTTP metadata URI. Must be a non-empty string.
        public static void SetMetadataUri(string uri)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(uri != null && uri.Length > 0, "URI must not be null or empty");
            StorageSetMetadataUri(uri);
            OnMetadataUriSet(Runtime.CallingScriptHash, uri, Runtime.Time);
        }

        // Updates the maximum supply cap. 0 = uncapped. Cannot be set below current totalSupply.
        public static void SetMaxSupply(BigInteger newMax)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            if (newMax > 0)
            {
                BigInteger currentSupply = (BigInteger)Storage.Get(new[] { (byte)0x00 });
                ExecutionEngine.Assert(newMax >= currentSupply, "NewMaxSupply cannot be less than current totalSupply");
            }
            StorageSetMaxSupply(newMax);
            OnMaxSupplySet(Runtime.CallingScriptHash, newMax, Runtime.Time);
        }

        // Sets the per-transfer creator GAS fee in datoshi (1 GAS = 100,000,000 datoshi).
        public static void SetCreatorFee(BigInteger datoshi)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(datoshi >= 0 && datoshi <= 5_000_000, "CreatorFee must be 0–5,000,000 datoshi");
            StorageSetCreatorFeeRate(datoshi);
            OnCreatorFeeRateSet(Runtime.CallingScriptHash, datoshi, Runtime.Time);
        }

        // Sets the per-transfer platform GAS fee in datoshi.
        public static void SetPlatformFeeRate(BigInteger datoshi)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(datoshi >= 0, "PlatformFeeRate must be >= 0");
            StorageSetPlatformFeeRate(datoshi);
            OnPlatformFeeRateSet(Runtime.CallingScriptHash, datoshi, Runtime.Time);
        }

        // Transfers factory authority to a new factory. Irrevocable for the old factory.
        public static void AuthorizeFactory(UInt160 newFactory)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(newFactory.IsValid && !newFactory.IsZero, "Invalid factory address");
            UInt160 previousFactory = StorageGetAuthorizedFactory();
            StorageSetAuthorizedFactory(newFactory);
            OnFactoryAuthorized(previousFactory, newFactory);
        }

        // ── Pausable controls ─────────────────────────────────────────────────
        // setPausable is a property-level change — requires !locked AND factory authorization.
        // pause/unpause are runtime controls — factory authorization required; locked does not block.

        public static void setPausable(bool value)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(StorageGetUpgradeable(), "Contract is not upgradeable");
            StorageSetPausable(value);
        }

        public static void pause()
        {
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(StorageGetPausable(), "Token is not pausable");
            StorageSetPaused(true);
        }

        public static void unpause()
        {
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(StorageGetPausable(), "Token is not pausable");
            StorageSetPaused(false);
        }

        // ── NEP17 base class overrides ────────────────────────────────────────

        public override string Symbol { [Safe] get => StorageGetSymbol(); }

        public override byte Decimals { [Safe] get => StorageGetDecimals(); }

        // Transfer applies the three-component fee system (FEAT-078):
        //   1. Platform GAS fee → authorizedFactory  (if platformFeeRate > 0 and CheckWitness(from))
        //   2. Creator GAS fee  → owner              (if creatorFeeRate > 0 and CheckWitness(from))
        //   3. Token burn       → address(0)          (if burnRate > 0 and normal non-mint transfer)
        //
        // Exemptions:
        //   from == address(0) — factory mint: all fees skipped
        //   to   == address(0) — user burn:    GAS fees apply; token burn skipped (already destroying)
        //
        // GAS fees are collected BEFORE the token transfer to ensure atomic revert on failure.
        public static new bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(!StorageGetPaused(), "Token transfers are paused");

            // Mint calls (from == address(0)) are fully exempt from fees.
            if (from != UInt160.Zero)
            {
                // GAS fees: collected only when the sender signed the transaction directly.
                if (Runtime.CheckWitness(from))
                {
                    BigInteger platformFee = StorageGetPlatformFeeRate();
                    if (platformFee > 0)
                        GAS.Transfer(from, StorageGetAuthorizedFactory(), platformFee, null);

                    BigInteger creatorFee = StorageGetCreatorFeeRate();
                    if (creatorFee > 0)
                    {
                        UInt160 owner = StorageGetOwner();
                        if (owner != UInt160.Zero)
                            GAS.Transfer(from, owner, creatorFee, null);
                    }
                }

                // Token burn: skip when to == address(0) (caller is already destroying tokens).
                if (to != UInt160.Zero)
                {
                    BigInteger burnRate = StorageGetBurnRate();
                    if (burnRate > 0)
                    {
                        BigInteger burnAmount = amount * burnRate / 10000;
                        if (burnAmount > 0)
                        {
                            ExecutionEngine.Assert(amount > burnAmount, "Burn amount exceeds transfer amount");
                            // Destroy burnt tokens — reduces totalSupply and fires Transfer(from, null, burnAmount).
                            Burn(from, burnAmount);
                            amount -= burnAmount;
                        }
                    }
                }
            }

            // When to is address(0), destroy the tokens (reduces totalSupply) rather than
            // transferring to the zero address (which would leave totalSupply unchanged).
            if (to == UInt160.Zero)
            {
                Burn(from, amount);
                return true;
            }

            return Nep17Token.Transfer(from, to, amount, data);
        }

        // Any token holder can burn their own tokens.
        // Routes through Transfer() so GAS fees are collected; token burn rate is NOT applied (to == 0).
        public static void burn(BigInteger amount)
        {
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");

            UInt160 caller = Runtime.Transaction.Sender;
            ExecutionEngine.Assert(Runtime.CheckWitness(caller), "No Authorization");
            ExecutionEngine.Assert(BalanceOf(caller) >= amount, "Insufficient balance");
            ExecutionEngine.Assert(Transfer(caller, UInt160.Zero, amount, null), "Burn failed");
        }

        // Owner-only direct mint. Retained as creator fallback; does not collect fees.
        // Factory-mediated minting uses MintByFactory() instead.
        public static void mint(UInt160 to, BigInteger amount)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No Authorization");

            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            ExecutionEngine.Assert(to.IsValid && !to.IsZero, "Invalid recipient");
            ExecutionEngine.Assert(StorageGetMintable(), "Token is not mintable");

            BigInteger maxSupply = StorageGetMaxSupply();
            if (maxSupply > 0)
            {
                BigInteger currentSupply = (BigInteger)Storage.Get(new[] { (byte)0x00 });
                ExecutionEngine.Assert(currentSupply + amount <= maxSupply, "MaxSupply exceeded");
            }

            Nep17Token.Mint(to, amount);
        }

        // Factory-mediated mint. Only callable by the authorized factory contract.
        // Uses Nep17Token.Mint directly — the resulting Transfer(address(0), to, amount) event
        // triggers the from==0 exemption in Transfer(), so no fees are collected.
        public static void MintByFactory(UInt160 to, BigInteger amount)
        {
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ExecutionEngine.Assert(Runtime.CallingScriptHash == StorageGetAuthorizedFactory(), "No authorization");
            ExecutionEngine.Assert(StorageGetMintable(), "Token is not mintable");
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");
            ExecutionEngine.Assert(to.IsValid && !to.IsZero, "Invalid recipient");

            BigInteger maxSupply = StorageGetMaxSupply();
            if (maxSupply > 0)
            {
                BigInteger currentSupply = (BigInteger)Storage.Get(new[] { (byte)0x00 });
                ExecutionEngine.Assert(currentSupply + amount <= maxSupply, "MaxSupply exceeded");
            }

            Nep17Token.Mint(to, amount);
        }

        // Rejects all incoming NEP-17 transfers — this contract holds no tokens.
        [DisplayName("onNEP17Payment")]
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            throw new InvalidOperationException("This contract does not accept token transfers.");
        }

        // ── Contract lifecycle ────────────────────────────────────────────────

        [Safe]
        public static bool verify() => IsOwner();

        // Deploy parameters (object[] of length 13, in order):
        //   [0]  name              string     — non-empty display name
        //   [1]  symbol            string     — non-empty ticker (e.g. "HCT")
        //   [2]  initialSupply     BigInteger — >= 0; minted to owner on deploy
        //   [3]  decimals          byte       — 0-18
        //   [4]  owner             UInt160    — valid, non-zero
        //   [5]  mintable          BigInteger — 1/0 (always 1 for FEAT-078 tokens)
        //   [6]  maxSupply         BigInteger — 0 = uncapped
        //   [7]  upgradeable       BigInteger — 1/0
        //   [8]  metadataUri       string     — IPFS URI; may be empty
        //   [9]  pausable          BigInteger — 1/0
        //   [10] authorizedFactory UInt160    — valid, non-zero; only this contract may call setters
        //   [11] platformFeeRate   BigInteger — 0–10,000,000 datoshi per transfer to factory
        //   [12] creatorFeeRate    BigInteger — 0–5,000,000 datoshi per transfer to creator
        public static void _deploy(object data, bool update)
        {
            if (update) return;

            object[] args = (object[])data;
            ExecutionEngine.Assert(args.Length == 13, "Expected 13 deploy parameters");

            string name                   = (string)args[0];
            string symbol                 = (string)args[1];
            BigInteger initialSupply      = (BigInteger)args[2];
            byte decimals                 = (byte)(BigInteger)args[3];
            UInt160 owner                 = (UInt160)args[4];
            bool mintable                 = (BigInteger)args[5] != 0;
            BigInteger maxSupply          = (BigInteger)args[6];
            bool upgradeable              = (BigInteger)args[7] != 0;
            string metadataUri            = (string)args[8];
            bool pausable                 = (BigInteger)args[9] != 0;
            UInt160 authorizedFactory     = (UInt160)args[10];
            BigInteger platformFeeRate    = (BigInteger)args[11];
            BigInteger creatorFeeRate     = (BigInteger)args[12];

            ExecutionEngine.Assert(name.Length > 0, "Name must not be empty");
            ExecutionEngine.Assert(symbol.Length > 0, "Symbol must not be empty");
            ExecutionEngine.Assert(initialSupply >= 0, "InitialSupply must be >= 0");
            ExecutionEngine.Assert(decimals <= 18, "Decimals must be 0-18");
            ExecutionEngine.Assert(owner.IsValid && !owner.IsZero, "Invalid owner address");
            ExecutionEngine.Assert(maxSupply >= 0, "MaxSupply must be >= 0");
            if (maxSupply > 0)
                ExecutionEngine.Assert(initialSupply <= maxSupply, "InitialSupply must not exceed MaxSupply");
            ExecutionEngine.Assert(authorizedFactory.IsValid && !authorizedFactory.IsZero, "Invalid factory address");
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
            StorageSetAuthorizedFactory(authorizedFactory);
            StorageSetPlatformFeeRate(platformFeeRate);
            StorageSetCreatorFeeRate(creatorFeeRate);
            // locked=false, paused=false, burnRate=0: storage default (key absent = false/zero)

            StorageSetOwner(owner);
            OnOwnerChanged(null, owner);

            if (initialSupply > 0)
                Nep17Token.Mint(owner, initialSupply);
        }

        public static void update(ByteString nefFile, string manifest, object data = null)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(StorageGetUpgradeable(), "Contract is not upgradeable");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
            ContractManagement.Update(nefFile, manifest, data);
        }
    }
}
