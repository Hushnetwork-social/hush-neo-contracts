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
    [ContractAuthor("<Your Name Or Company Here>", "<Your Public Email Here>")]
    [ContractDescription("<Description Here>")]
    [ContractVersion("<Version String Here>")]
    [ContractSourceCode("https://github.com/neo-project/neo-devpack-dotnet/tree/master/src/Neo.SmartContract.Template/templates/neocontractnep17/TokenTemplate.cs")]
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
        private const byte Prefix_MetadataUri = 0x19;
        private const byte Prefix_Owner       = 0xff;

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

        // Owner can permanently renounce upgrade rights. Cannot be undone.
        // Also blocks setPausable() but NOT pause()/unpause() (runtime vs property).
        [DisplayName("lock")]
        public static void Lock()
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(!StorageGetLocked(), "Already locked");
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

        // ── Pausable controls ─────────────────────────────────────────────────
        // setPausable is a property-level change — requires upgradeable=true AND !locked.
        // pause/unpause are runtime controls — allowed while locked if pausable=true.

        public static void setPausable(bool value)
        {
            ExecutionEngine.Assert(IsOwner(), "No authorization");
            ExecutionEngine.Assert(StorageGetUpgradeable(), "Contract is not upgradeable");
            ExecutionEngine.Assert(!StorageGetLocked(), "Contract is locked");
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

        // ── NEP17 base class overrides ────────────────────────────────────────

        public override string Symbol { [Safe] get => StorageGetSymbol(); }

        public override byte Decimals { [Safe] get => StorageGetDecimals(); }

        // Blocks all token transfers while the contract is paused.
        // Transfer is static in Nep17Token — shadow with `new` and delegate to base class.
        public static new bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(!StorageGetPaused(), "Token transfers are paused");
            return Nep17Token.Transfer(from, to, amount, data);
        }

        // Any token holder can burn their own tokens.
        // The caller is identified via CallingScriptHash (contract-to-contract)
        // or the transaction Sender (direct user invocation).
        public static void burn(BigInteger amount)
        {
            ExecutionEngine.Assert(amount > 0, "Amount must be positive");

            UInt160 caller = Runtime.CallingScriptHash;
            if (caller == null || caller.IsZero)
                caller = Runtime.Transaction.Sender;

            ExecutionEngine.Assert(Runtime.CheckWitness(caller), "No Authorization");
            Nep17Token.Burn(caller, amount);
        }

        // Owner-only. Requires mintable=true and maxSupply cap enforcement.
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
                // Read TotalSupply directly from base class storage (Prefix_TotalSupply = 0x00)
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

        // Deploy parameters (object[] of length 10, in order):
        //   [0] name         string   — non-empty display name
        //   [1] symbol       string   — non-empty ticker (e.g. "HCT")
        //   [2] initialSupply BigInteger — >= 0; minted to owner on deploy
        //   [3] decimals     byte     — 0-18
        //   [4] owner        UInt160  — valid, non-zero
        //   [5] mintable     bool     — stored as BigInteger 1/0
        //   [6] maxSupply    BigInteger — 0 = uncapped; ignored if !mintable
        //   [7] upgradeable  bool     — stored as BigInteger 1/0
        //   [8] metadataUri  string   — IPFS URI; may be empty
        //   [9] pausable     bool     — stored as BigInteger 1/0
        public static void _deploy(object data, bool update)
        {
            if (update) return;

            object[] args = (object[])data;
            ExecutionEngine.Assert(args.Length == 10, "Expected 10 deploy parameters");

            string name          = (string)args[0];
            string symbol        = (string)args[1];
            BigInteger initialSupply = (BigInteger)args[2];
            byte decimals        = (byte)(BigInteger)args[3];
            UInt160 owner        = (UInt160)args[4];
            bool mintable        = (BigInteger)args[5] != 0;
            BigInteger maxSupply = (BigInteger)args[6];
            bool upgradeable     = (BigInteger)args[7] != 0;
            string metadataUri   = (string)args[8];
            bool pausable        = (BigInteger)args[9] != 0;

            ExecutionEngine.Assert(name.Length > 0, "Name must not be empty");
            ExecutionEngine.Assert(symbol.Length > 0, "Symbol must not be empty");
            ExecutionEngine.Assert(initialSupply >= 0, "InitialSupply must be >= 0");
            ExecutionEngine.Assert(decimals <= 18, "Decimals must be 0-18");
            ExecutionEngine.Assert(owner.IsValid && !owner.IsZero, "Invalid owner address");
            ExecutionEngine.Assert(maxSupply >= 0, "MaxSupply must be >= 0");

            StorageSetName(name);
            StorageSetSymbol(symbol);
            StorageSetDecimals(decimals);
            StorageSetMintable(mintable);
            StorageSetMaxSupply(maxSupply);
            StorageSetUpgradeable(upgradeable);
            StorageSetPausable(pausable);
            StorageSetMetadataUri(metadataUri);
            // locked=false and paused=false: storage default (key absent = false)

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
