using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

using System;
using System.ComponentModel;
using System.Numerics;

namespace HushNetwork.Contracts
{
    [DisplayName(nameof(BondingCurveRouter))]
    [ContractAuthor("HushNetwork", "dev@hushnetwork.social")]
    [ContractDescription("Shared speculation bonding-curve router for HushNetwork tokens")]
    [ContractVersion("1.0.0")]
    [ContractPermission(Permission.Any, Method.Any)]
    public class BondingCurveRouter : Neo.SmartContract.Framework.SmartContract
    {
        private const byte Prefix_Curve = 0x10;
        private const byte Prefix_PendingCurve = 0x11;
        private const byte Prefix_Owner = 0xff;
        private const byte Prefix_Paused = 0xfe;
        private const byte Prefix_AuthorizedFactory = 0xfd;

        private const byte QuoteAssetGas = 0x01;
        private const byte QuoteAssetNeo = 0x02;

        private static readonly BigInteger PriceScale = 1_000_000_000_000_000_000;

        public delegate void OnCurveRegisteredDelegate(UInt160 tokenHash, UInt160 creator, string quoteAsset, BigInteger virtualQuote, BigInteger virtualTokens, BigInteger curveInventory, BigInteger retainedInventory, BigInteger totalSupply, BigInteger graduationThreshold);

        [DisplayName("CurveRegistered")]
        public static event OnCurveRegisteredDelegate OnCurveRegistered;

        public delegate void OnTradeDelegate(UInt160 tokenHash, UInt160 trader, string side, string quoteAsset, BigInteger grossQuoteAmount, BigInteger grossTokenAmount, BigInteger netQuoteAmount, BigInteger netTokenAmount, BigInteger newPrice);

        [DisplayName("Trade")]
        public static event OnTradeDelegate OnTrade;

        public delegate void OnGraduationReadyDelegate(UInt160 tokenHash, string quoteAsset, BigInteger realQuote, BigInteger graduationThreshold);

        [DisplayName("GraduationReady")]
        public static event OnGraduationReadyDelegate OnGraduationReady;

        private static UInt160 StorageGetOwner()
        {
            ByteString raw = Storage.Get(new[] { Prefix_Owner });
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static void StorageSetOwner(UInt160 value) => Storage.Put(new[] { Prefix_Owner }, value);

        private static bool StorageGetPaused() => (BigInteger)Storage.Get(new[] { Prefix_Paused }) != 0;

        private static void StorageSetPaused(bool value)
        {
            if (value) Storage.Put(new[] { Prefix_Paused }, (BigInteger)1);
            else Storage.Delete(new[] { Prefix_Paused });
        }

        private static UInt160 StorageGetAuthorizedFactory()
        {
            ByteString raw = Storage.Get(new[] { Prefix_AuthorizedFactory });
            return raw is null ? UInt160.Zero : (UInt160)raw;
        }

        private static void StorageSetAuthorizedFactory(UInt160 value) =>
            Storage.Put(new[] { Prefix_AuthorizedFactory }, value);

        private static object[] StorageGetCurve(UInt160 tokenHash)
        {
            ByteString key = (ByteString)new byte[] { Prefix_Curve } + (ByteString)tokenHash;
            ByteString raw = Storage.Get(key);
            return raw is null ? null : (object[])StdLib.Deserialize(raw);
        }

        private static void StorageSetCurve(UInt160 tokenHash, object[] value)
        {
            ByteString key = (ByteString)new byte[] { Prefix_Curve } + (ByteString)tokenHash;
            Storage.Put(key, StdLib.Serialize(value));
        }

        private static object[] StorageGetPendingCurve(UInt160 tokenHash)
        {
            ByteString key = (ByteString)new byte[] { Prefix_PendingCurve } + (ByteString)tokenHash;
            ByteString raw = Storage.Get(key);
            return raw is null ? null : (object[])StdLib.Deserialize(raw);
        }

        private static void StorageSetPendingCurve(UInt160 tokenHash, object[] value)
        {
            ByteString key = (ByteString)new byte[] { Prefix_PendingCurve } + (ByteString)tokenHash;
            Storage.Put(key, StdLib.Serialize(value));
        }

        private static void StorageDeletePendingCurve(UInt160 tokenHash)
        {
            ByteString key = (ByteString)new byte[] { Prefix_PendingCurve } + (ByteString)tokenHash;
            Storage.Delete(key);
        }

        private static bool IsOwner() => Runtime.CheckWitness(StorageGetOwner());

        private static void AssertOwnerAuthorized() => ExecutionEngine.Assert(IsOwner(), "Unauthorized");

        private static void AssertFactoryAuthorized()
        {
            UInt160 authorizedFactory = StorageGetAuthorizedFactory();
            ExecutionEngine.Assert(
                Runtime.CallingScriptHash == authorizedFactory ||
                Runtime.EntryScriptHash == authorizedFactory ||
                Runtime.CallingScriptHash == GAS.Hash,
                "Unauthorized factory"
            );
        }

        private static void AssertRouterActive() => ExecutionEngine.Assert(!StorageGetPaused(), "Router is paused");

        private static string QuoteAssetToString(BigInteger quoteAssetCode)
        {
            if (quoteAssetCode == (BigInteger)QuoteAssetGas) return "GAS";
            if (quoteAssetCode == (BigInteger)QuoteAssetNeo) return "NEO";
            return "";
        }

        private static UInt160 QuoteAssetToHash(BigInteger quoteAssetCode)
        {
            if (quoteAssetCode == (BigInteger)QuoteAssetGas) return GAS.Hash;
            if (quoteAssetCode == (BigInteger)QuoteAssetNeo) return NEO.Hash;
            return UInt160.Zero;
        }

        private static bool QuoteAssetMatchesCaller(BigInteger quoteAssetCode, UInt160 callerHash)
        {
            if (quoteAssetCode == (BigInteger)QuoteAssetGas) return callerHash == GAS.Hash;
            if (quoteAssetCode == (BigInteger)QuoteAssetNeo) return callerHash == NEO.Hash;
            return false;
        }

        private static BigInteger ParseQuoteAssetCode(string quoteAsset)
        {
            ExecutionEngine.Assert(quoteAsset == "GAS" || quoteAsset == "NEO", "Unsupported quote asset");
            return quoteAsset == "GAS" ? QuoteAssetGas : QuoteAssetNeo;
        }

        private static BigInteger GetDefaultVirtualQuote(BigInteger quoteAssetCode)
        {
            if (quoteAssetCode == (BigInteger)QuoteAssetGas) return 100_000_000;
            if (quoteAssetCode == (BigInteger)QuoteAssetNeo) return 10;
            return 0;
        }

        private static BigInteger GetDefaultVirtualTokens(BigInteger quoteAssetCode) => 100_000;

        private static BigInteger GetDefaultGraduationThreshold(BigInteger quoteAssetCode)
        {
            if (quoteAssetCode == (BigInteger)QuoteAssetGas) return 500_000_000;
            if (quoteAssetCode == (BigInteger)QuoteAssetNeo) return 50;
            return 0;
        }

        private static BigInteger CeilDiv(BigInteger numerator, BigInteger denominator)
        {
            ExecutionEngine.Assert(denominator > 0, "Division by zero");
            if (numerator <= 0) return 0;
            return (numerator + denominator - 1) / denominator;
        }

        private static BigInteger SafeTokenBalanceOf(UInt160 tokenHash, UInt160 account)
        {
            object raw = Contract.Call(tokenHash, "balanceOf", CallFlags.ReadOnly, new object[] { account });
            return raw is null ? 0 : (BigInteger)raw;
        }

        private static BigInteger SafeTokenTotalSupply(UInt160 tokenHash)
        {
            object raw = Contract.Call(tokenHash, "totalSupply", CallFlags.ReadOnly, Array.Empty<object>());
            return raw is null ? 0 : (BigInteger)raw;
        }

        private static BigInteger SafeTokenBurnRate(UInt160 tokenHash)
        {
            object raw = Contract.Call(tokenHash, "getBurnRate", CallFlags.ReadOnly, Array.Empty<object>());
            return raw is null ? 0 : (BigInteger)raw;
        }

        private static BigInteger SafeTokenPlatformFee(UInt160 tokenHash)
        {
            object raw = Contract.Call(tokenHash, "getPlatformFeeRate", CallFlags.ReadOnly, Array.Empty<object>());
            return raw is null ? 0 : (BigInteger)raw;
        }

        private static BigInteger SafeTokenCreatorFee(UInt160 tokenHash)
        {
            object raw = Contract.Call(tokenHash, "getCreatorFeeRate", CallFlags.ReadOnly, Array.Empty<object>());
            return raw is null ? 0 : (BigInteger)raw;
        }

        private static object[] SafeTokenQuoteTransfer(UInt160 tokenHash, BigInteger amount)
        {
            object raw = Contract.Call(
                tokenHash,
                "quoteTransfer",
                CallFlags.ReadOnly,
                new object[] { Runtime.ExecutingScriptHash, Runtime.ExecutingScriptHash, amount }
            );
            return raw is null ? null : (object[])raw;
        }

        private static object[] GetFactoryTokenInfo(UInt160 factoryHash, UInt160 tokenHash)
        {
            object raw = Contract.Call(factoryHash, "getToken", CallFlags.ReadOnly, new object[] { tokenHash });
            return raw is null ? null : (object[])raw;
        }

        private static BigInteger ComputeInvariant(BigInteger virtualQuote, BigInteger virtualTokens, BigInteger realQuote, BigInteger tokenReserve) =>
            (virtualQuote + realQuote) * (virtualTokens + tokenReserve);

        private static BigInteger ComputePrice(BigInteger virtualQuote, BigInteger virtualTokens, BigInteger realQuote, BigInteger tokenReserve)
        {
            BigInteger totalTokens = virtualTokens + tokenReserve;
            if (totalTokens <= 0) return 0;
            return (virtualQuote + realQuote) * PriceScale / totalTokens;
        }

        private static BigInteger GetQuoteRequiredToDrain(object[] curve)
        {
            BigInteger virtualQuote = (BigInteger)curve[2];
            BigInteger virtualTokens = (BigInteger)curve[3];
            BigInteger realQuote = (BigInteger)curve[4];
            BigInteger k = (BigInteger)curve[6];

            if (virtualTokens <= 0) return 0;

            BigInteger targetQuoteTotal = CeilDiv(k, virtualTokens);
            BigInteger currentQuoteTotal = virtualQuote + realQuote;
            return targetQuoteTotal > currentQuoteTotal ? targetQuoteTotal - currentQuoteTotal : 0;
        }

        private static object[] BuildZeroBuyQuote(BigInteger grossQuoteIn) =>
            new object[] { grossQuoteIn, (BigInteger)0, grossQuoteIn, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0 };

        private static object[] BuildZeroSellQuote(BigInteger grossTokenIn) =>
            new object[] { grossTokenIn, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0, (BigInteger)0 };

        private static object[] BuildBuyQuote(UInt160 tokenHash, object[] curve, BigInteger grossQuoteIn)
        {
            if (curve is null || grossQuoteIn <= 0) return BuildZeroBuyQuote(grossQuoteIn);

            BigInteger virtualQuote = (BigInteger)curve[2];
            BigInteger virtualTokens = (BigInteger)curve[3];
            BigInteger realQuote = (BigInteger)curve[4];
            BigInteger tokenReserve = (BigInteger)curve[5];
            BigInteger k = (BigInteger)curve[6];

            BigInteger quoteConsumed = 0;
            BigInteger quoteRefund = grossQuoteIn;
            BigInteger grossTokenOut = 0;
            BigInteger newRealQuote = realQuote;
            BigInteger newTokenReserve = tokenReserve;
            BigInteger capped = 0;

            if (tokenReserve > 0)
            {
                BigInteger quoteToDrain = GetQuoteRequiredToDrain(curve);
                if (quoteToDrain > 0 && grossQuoteIn >= quoteToDrain)
                {
                    quoteConsumed = quoteToDrain;
                    quoteRefund = grossQuoteIn - quoteConsumed;
                    grossTokenOut = tokenReserve;
                    newRealQuote = realQuote + quoteConsumed;
                    newTokenReserve = 0;
                    capped = 1;
                }
                else
                {
                    BigInteger newQuoteTotal = virtualQuote + realQuote + grossQuoteIn;
                    BigInteger newTokenTotal = CeilDiv(k, newQuoteTotal);
                    if (newTokenTotal < virtualTokens) newTokenTotal = virtualTokens;
                    newTokenReserve = newTokenTotal - virtualTokens;
                    if (newTokenReserve < 0) newTokenReserve = 0;
                    grossTokenOut = tokenReserve - newTokenReserve;
                    if (grossTokenOut < 0) grossTokenOut = 0;
                    quoteConsumed = grossQuoteIn;
                    quoteRefund = 0;
                    newRealQuote = realQuote + quoteConsumed;
                }
            }

            BigInteger burnAmount = 0;
            if (grossTokenOut > 0)
            {
                BigInteger burnRate = SafeTokenBurnRate(tokenHash);
                if (burnRate > 0) burnAmount = grossTokenOut * burnRate / 10000;
            }

            return new object[]
            {
                grossQuoteIn,
                quoteConsumed,
                quoteRefund,
                grossTokenOut,
                burnAmount,
                grossTokenOut - burnAmount,
                (BigInteger)0,
                (BigInteger)0,
                ComputePrice(virtualQuote, virtualTokens, newRealQuote, newTokenReserve),
                capped
            };
        }

        private static object[] BuildSellQuote(UInt160 tokenHash, object[] curve, BigInteger declaredGrossTokenIn, BigInteger actualNetTokenIn)
        {
            if (curve is null || actualNetTokenIn <= 0) return BuildZeroSellQuote(declaredGrossTokenIn);
            if (declaredGrossTokenIn <= 0 || declaredGrossTokenIn < actualNetTokenIn) declaredGrossTokenIn = actualNetTokenIn;

            BigInteger burnAmount = 0;
            BigInteger platformFee = SafeTokenPlatformFee(tokenHash);
            BigInteger creatorFee = SafeTokenCreatorFee(tokenHash);

            object[] transferQuote = SafeTokenQuoteTransfer(tokenHash, declaredGrossTokenIn);
            if (transferQuote != null && (BigInteger)transferQuote[1] == actualNetTokenIn)
            {
                burnAmount = (BigInteger)transferQuote[2];
                platformFee = (BigInteger)transferQuote[4];
                creatorFee = (BigInteger)transferQuote[5];
            }
            else if (declaredGrossTokenIn == actualNetTokenIn)
            {
                burnAmount = 0;
            }

            BigInteger virtualQuote = (BigInteger)curve[2];
            BigInteger virtualTokens = (BigInteger)curve[3];
            BigInteger realQuote = (BigInteger)curve[4];
            BigInteger tokenReserve = (BigInteger)curve[5];
            BigInteger k = (BigInteger)curve[6];

            BigInteger currentQuoteTotal = virtualQuote + realQuote;
            BigInteger newTokenTotal = virtualTokens + tokenReserve + actualNetTokenIn;
            BigInteger newQuoteTotal = CeilDiv(k, newTokenTotal);
            BigInteger quoteOut = newQuoteTotal < currentQuoteTotal ? currentQuoteTotal - newQuoteTotal : 0;
            BigInteger liquidityOkay = quoteOut <= realQuote ? 1 : 0;
            BigInteger nextRealQuote = liquidityOkay != 0 ? realQuote - quoteOut : realQuote;

            return new object[]
            {
                declaredGrossTokenIn,
                burnAmount,
                actualNetTokenIn,
                quoteOut,
                quoteOut,
                platformFee,
                creatorFee,
                ComputePrice(virtualQuote, virtualTokens, nextRealQuote, tokenReserve + actualNetTokenIn),
                liquidityOkay
            };
        }

        private static void TransferQuoteAsset(UInt160 assetHash, UInt160 recipient, BigInteger amount)
        {
            if (amount <= 0) return;
            bool transferred = (bool)Contract.Call(assetHash, "transfer", CallFlags.All, new object[] { Runtime.ExecutingScriptHash, recipient, amount, null });
            ExecutionEngine.Assert(transferred, "Quote asset transfer failed");
        }

        private static void MaybeLatchGraduationReady(UInt160 tokenHash, object[] curve)
        {
            if ((BigInteger)curve[8] != 0) return;
            if ((BigInteger)curve[7] <= 0) return;
            if ((BigInteger)curve[4] < (BigInteger)curve[7]) return;
            curve[8] = (BigInteger)1;
            OnGraduationReady(tokenHash, QuoteAssetToString((BigInteger)curve[1]), (BigInteger)curve[4], (BigInteger)curve[7]);
        }

        private static bool TryHandlePendingRegistration(UInt160 tokenHash, UInt160 from, BigInteger amount)
        {
            object[] pending = StorageGetPendingCurve(tokenHash);
            if (pending is null) return false;

            ExecutionEngine.Assert(from == (UInt160)pending[0], "Registration sender mismatch");
            ExecutionEngine.Assert(amount == (BigInteger)pending[2], "Registration inventory mismatch");

            UInt160 creator = (UInt160)pending[0];
            BigInteger quoteAssetCode = (BigInteger)pending[1];
            BigInteger curveInventory = (BigInteger)pending[2];
            BigInteger retainedInventory = (BigInteger)pending[3];
            BigInteger totalSupply = curveInventory + retainedInventory;
            BigInteger virtualQuote = (BigInteger)pending[5];
            BigInteger virtualTokens = (BigInteger)pending[6];
            BigInteger graduationThreshold = (BigInteger)pending[7];

            StorageSetCurve(tokenHash, new object[]
            {
                creator,
                quoteAssetCode,
                virtualQuote,
                virtualTokens,
                (BigInteger)0,
                curveInventory,
                ComputeInvariant(virtualQuote, virtualTokens, 0, curveInventory),
                graduationThreshold,
                (BigInteger)0,
                (BigInteger)0,
                (BigInteger)Runtime.Time,
                curveInventory,
                retainedInventory,
                totalSupply
            });

            StorageDeletePendingCurve(tokenHash);

            OnCurveRegistered(
                tokenHash,
                creator,
                QuoteAssetToString(quoteAssetCode),
                virtualQuote,
                virtualTokens,
                curveInventory,
                retainedInventory,
                totalSupply,
                graduationThreshold
            );

            return true;
        }

        [Safe]
        public static UInt160 GetOwner() => StorageGetOwner();

        [Safe]
        public static bool IsPaused() => StorageGetPaused();

        [Safe]
        public static UInt160 GetAuthorizedFactory() => StorageGetAuthorizedFactory();

        [Safe]
        public static bool Verify() => IsOwner();

        [Safe]
        public static bool IsCurveRegistered(UInt160 tokenHash) => StorageGetCurve(tokenHash) is not null;

        public static void SetOwner(UInt160 newOwner)
        {
            AssertOwnerAuthorized();
            ExecutionEngine.Assert(newOwner.IsValid && !newOwner.IsZero, "Invalid owner");
            StorageSetOwner(newOwner);
        }

        public static void SetPaused(bool paused)
        {
            AssertOwnerAuthorized();
            StorageSetPaused(paused);
        }

        public static void SetAuthorizedFactory(UInt160 factoryHash)
        {
            AssertOwnerAuthorized();
            ExecutionEngine.Assert(factoryHash.IsValid && !factoryHash.IsZero, "Invalid factory");
            StorageSetAuthorizedFactory(factoryHash);
        }

        [Safe]
        public static object[] GetCurve(UInt160 tokenHash)
        {
            object[] curve = StorageGetCurve(tokenHash);
            if (curve is null)
            {
                return new object[]
                {
                    "NOT_FOUND",
                    "",
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0,
                    false,
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0,
                    (BigInteger)0
                };
            }

            bool graduationReady = (BigInteger)curve[8] != 0;
            return new object[]
            {
                graduationReady ? "GRADUATION_READY" : "ACTIVE",
                QuoteAssetToString((BigInteger)curve[1]),
                (BigInteger)curve[2],
                (BigInteger)curve[4],
                (BigInteger)curve[5],
                (BigInteger)curve[6],
                (BigInteger)curve[7],
                graduationReady,
                ComputePrice((BigInteger)curve[2], (BigInteger)curve[3], (BigInteger)curve[4], (BigInteger)curve[5]),
                (BigInteger)curve[9],
                (BigInteger)curve[10],
                (BigInteger)curve[11],
                (BigInteger)curve[12],
                (BigInteger)curve[13],
                (BigInteger)curve[3]
            };
        }

        [Safe]
        public static BigInteger GetPrice(UInt160 tokenHash)
        {
            object[] curve = StorageGetCurve(tokenHash);
            if (curve is null) return 0;
            return ComputePrice((BigInteger)curve[2], (BigInteger)curve[3], (BigInteger)curve[4], (BigInteger)curve[5]);
        }

        [Safe]
        public static object[] GetBuyQuote(UInt160 tokenHash, BigInteger quoteIn)
        {
            object[] curve = StorageGetCurve(tokenHash);
            return curve is null ? BuildZeroBuyQuote(quoteIn) : BuildBuyQuote(tokenHash, curve, quoteIn);
        }

        [Safe]
        public static object[] GetSellQuote(UInt160 tokenHash, BigInteger tokenIn)
        {
            object[] curve = StorageGetCurve(tokenHash);
            if (curve is null) return BuildZeroSellQuote(tokenIn);

            object[] transferQuote = SafeTokenQuoteTransfer(tokenHash, tokenIn);
            BigInteger netTokenIn = transferQuote is null ? tokenIn : (BigInteger)transferQuote[1];
            return BuildSellQuote(tokenHash, curve, tokenIn, netTokenIn);
        }

        [Safe]
        public static bool IsGraduationReady(UInt160 tokenHash)
        {
            object[] curve = StorageGetCurve(tokenHash);
            return curve is not null && (BigInteger)curve[8] != 0;
        }

        [Safe]
        public static object[] GetGraduationProgress(UInt160 tokenHash)
        {
            object[] curve = StorageGetCurve(tokenHash);
            if (curve is null)
                return new object[] { (BigInteger)0, (BigInteger)0, (BigInteger)0, false };

            BigInteger threshold = (BigInteger)curve[7];
            BigInteger progressBps = threshold > 0 ? (BigInteger)curve[4] * 10000 / threshold : 0;
            if (progressBps > 10000) progressBps = 10000;

            return new object[]
            {
                (BigInteger)curve[4],
                threshold,
                progressBps,
                (BigInteger)curve[8] != 0
            };
        }

        public static void RegisterCurve(UInt160 tokenHash, string quoteAsset, BigInteger curveInventory)
        {
            AssertFactoryAuthorized();
            AssertRouterActive();
            ExecutionEngine.Assert(tokenHash.IsValid && !tokenHash.IsZero, "Invalid token hash");
            ExecutionEngine.Assert(curveInventory > 0, "Curve inventory must be positive");
            ExecutionEngine.Assert(StorageGetCurve(tokenHash) is null, "Curve already registered");
            ExecutionEngine.Assert(StorageGetPendingCurve(tokenHash) is null, "Curve registration already pending");

            object[] tokenInfo = GetFactoryTokenInfo(StorageGetAuthorizedFactory(), tokenHash);
            ExecutionEngine.Assert(tokenInfo is not null, "Token not found");
            ExecutionEngine.Assert((string)tokenInfo[3] == "speculation", "Token is not in speculation mode");

            UInt160 creator = (UInt160)tokenInfo[1];
            BigInteger creatorBalance = SafeTokenBalanceOf(tokenHash, creator);
            ExecutionEngine.Assert(creatorBalance >= curveInventory, "Insufficient owner balance for curve inventory");

            BigInteger quoteAssetCode = ParseQuoteAssetCode(quoteAsset);
            StorageSetPendingCurve(tokenHash, new object[]
            {
                creator,
                quoteAssetCode,
                curveInventory,
                creatorBalance - curveInventory,
                SafeTokenTotalSupply(tokenHash),
                GetDefaultVirtualQuote(quoteAssetCode),
                GetDefaultVirtualTokens(quoteAssetCode),
                GetDefaultGraduationThreshold(quoteAssetCode)
            });
        }

        [DisplayName("onNEP17Payment")]
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            UInt160 caller = Runtime.CallingScriptHash;

            if (TryHandlePendingRegistration(caller, from, amount))
                return;

            AssertRouterActive();

            if (caller == GAS.Hash || caller == NEO.Hash)
            {
                object[] buyArgs = (object[])data;
                ExecutionEngine.Assert(buyArgs is not null && buyArgs.Length >= 2, "Buy payload must be [tokenHash, minTokensOut]");

                UInt160 tokenHash = (UInt160)buyArgs[0];
                BigInteger minTokensOut = (BigInteger)buyArgs[1];
                object[] curve = StorageGetCurve(tokenHash);

                ExecutionEngine.Assert(curve is not null, "Curve not found");
                ExecutionEngine.Assert(QuoteAssetMatchesCaller((BigInteger)curve[1], caller), "Wrong quote asset for curve");

                object[] buyQuote = BuildBuyQuote(tokenHash, curve, amount);
                BigInteger quoteConsumed = (BigInteger)buyQuote[1];
                BigInteger quoteRefund = (BigInteger)buyQuote[2];
                BigInteger grossTokenOut = (BigInteger)buyQuote[3];
                BigInteger netTokensOut = (BigInteger)buyQuote[5];

                ExecutionEngine.Assert(grossTokenOut > 0, "Quote input too small");
                ExecutionEngine.Assert(netTokensOut >= minTokensOut, "Slippage exceeded");

                curve[4] = (BigInteger)curve[4] + quoteConsumed;
                curve[5] = (BigInteger)curve[5] - grossTokenOut;
                curve[9] = (BigInteger)curve[9] + 1;
                MaybeLatchGraduationReady(tokenHash, curve);
                StorageSetCurve(tokenHash, curve);

                if (quoteRefund > 0)
                    TransferQuoteAsset(caller, from, quoteRefund);

                bool transferred = (bool)Contract.Call(
                    tokenHash,
                    "transfer",
                    CallFlags.All,
                    new object[] { Runtime.ExecutingScriptHash, from, grossTokenOut, null }
                );
                ExecutionEngine.Assert(transferred, "Token transfer failed");

                OnTrade(
                    tokenHash,
                    from,
                    "BUY",
                    QuoteAssetToString((BigInteger)curve[1]),
                    amount,
                    grossTokenOut,
                    quoteConsumed,
                    netTokensOut,
                    ComputePrice((BigInteger)curve[2], (BigInteger)curve[3], (BigInteger)curve[4], (BigInteger)curve[5])
                );

                return;
            }

            object[] sellCurve = StorageGetCurve(caller);
            ExecutionEngine.Assert(sellCurve is not null, "Curve not found");

            BigInteger minQuoteOut = 0;
            BigInteger declaredGrossTokenIn = amount;

            if (data is BigInteger directMinQuoteOut)
            {
                minQuoteOut = directMinQuoteOut;
            }
            else
            {
                object[] sellArgs = (object[])data;
                if (sellArgs != null && sellArgs.Length > 0)
                {
                    minQuoteOut = (BigInteger)sellArgs[0];
                    if (sellArgs.Length > 1)
                    {
                        BigInteger declaredGross = (BigInteger)sellArgs[1];
                        if (declaredGross >= amount)
                            declaredGrossTokenIn = declaredGross;
                    }
                }
            }

            object[] sellQuote = BuildSellQuote(caller, sellCurve, declaredGrossTokenIn, amount);
            BigInteger quoteOut = (BigInteger)sellQuote[3];

            ExecutionEngine.Assert((BigInteger)sellQuote[8] != 0, "Insufficient quote reserve");
            ExecutionEngine.Assert(quoteOut > 0, "Quote output too small");
            ExecutionEngine.Assert(quoteOut >= minQuoteOut, "Slippage exceeded");

            sellCurve[4] = (BigInteger)sellCurve[4] - quoteOut;
            sellCurve[5] = (BigInteger)sellCurve[5] + amount;
            sellCurve[9] = (BigInteger)sellCurve[9] + 1;
            StorageSetCurve(caller, sellCurve);

            TransferQuoteAsset(QuoteAssetToHash((BigInteger)sellCurve[1]), from, quoteOut);

            OnTrade(
                caller,
                from,
                "SELL",
                QuoteAssetToString((BigInteger)sellCurve[1]),
                quoteOut,
                (BigInteger)sellQuote[0],
                quoteOut,
                amount,
                ComputePrice((BigInteger)sellCurve[2], (BigInteger)sellCurve[3], (BigInteger)sellCurve[4], (BigInteger)sellCurve[5])
            );
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;

            if (data != null)
            {
                object[] args = (object[])data;
                UInt160 owner = (UInt160)args[0];
                ExecutionEngine.Assert(owner.IsValid && !owner.IsZero, "Invalid owner");
                StorageSetOwner(owner);

                if (args.Length > 1)
                {
                    UInt160 factoryHash = (UInt160)args[1];
                    if (factoryHash.IsValid && !factoryHash.IsZero)
                        StorageSetAuthorizedFactory(factoryHash);
                }

                return;
            }

            StorageSetOwner(Runtime.Transaction.Sender);
        }
    }
}
