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
        // Convention: StorageGet*()/StorageSet*() are the raw read/write layer.
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
        public static UInt160 GetOwner() => StorageGetOwner();

        private static bool IsOwner() =>
            Runtime.CheckWitness(GetOwner());

        public delegate void OnSetOwnerDelegate(UInt160 previousOwner, UInt160 newOwner);

        [DisplayName("SetOwner")]
        public static event OnSetOwnerDelegate OnSetOwner;

        public static void SetOwner(UInt160 newOwner)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No Authorization!");

            ExecutionEngine.Assert(newOwner.IsValid && !newOwner.IsZero, "owner must be valid");

            UInt160 previous = GetOwner();
            StorageSetOwner(newOwner);
            OnSetOwner(previous, newOwner);
        }

        // ── NEP17 base class overrides ────────────────────────────────────────

        public override string Symbol { [Safe] get => StorageGetSymbol(); }

        public override byte Decimals { [Safe] get => StorageGetDecimals(); }

        // Burn and Mint will be refactored in Phase 3 (caller-only burn, owner mint).
        // Kept as scaffold-compatible stubs for Phase 2 build verification.
        public static new void Burn(UInt160 account, BigInteger amount)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No Authorization!");
            Nep17Token.Burn(account, amount);
        }

        public static new void Mint(UInt160 to, BigInteger amount)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No Authorization!");
            Nep17Token.Mint(to, amount);
        }

        // ── Contract lifecycle ────────────────────────────────────────────────

        [Safe]
        public static bool Verify() => IsOwner();

        // Full 10-parameter deploy parsing is implemented in Phase 3.
        // Phase 2: owner stored via StorageSetOwner; other params pending.
        public static void _deploy(object data, bool update)
        {
            if (update) return;

            if (data is null) data = Runtime.Transaction.Sender;

            UInt160 initialOwner = (UInt160)data;
            ExecutionEngine.Assert(initialOwner.IsValid && !initialOwner.IsZero, "owner must exist");

            StorageSetOwner(initialOwner);
            OnSetOwner(null, initialOwner);
        }

        public static void Update(ByteString nefFile, string manifest, object data = null)
        {
            if (!IsOwner())
                throw new InvalidOperationException("No authorization.");
            ContractManagement.Update(nefFile, manifest, data);
        }
    }
}
