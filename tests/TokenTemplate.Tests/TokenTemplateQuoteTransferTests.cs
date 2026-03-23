#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using System;
using System.IO;
using System.Numerics;

namespace TokenTemplate.Tests;

[TestFixture]
public class TokenTemplateQuoteTransferTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void QuoteTransfer_WithConfiguredFeesAndBurnRate_ReturnsExpectedBreakdown()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var factorySigner = TestEngine.GetNewSigner();
        var recipientSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var token = DeployToken(engine, ownerSigner.Account, factorySigner.Account);
        SetBurnRateAsFactory(engine, token, factorySigner.Account, 200);

        var quote = token.quoteTransfer(ownerSigner.Account, recipientSigner.Account, 10_000);

        Assert.That(quote, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ParseBigInteger(quote![0]), Is.EqualTo((BigInteger)10_000));
            Assert.That(ParseBigInteger(quote[1]), Is.EqualTo((BigInteger)9_800));
            Assert.That(ParseBigInteger(quote[2]), Is.EqualTo((BigInteger)200));
            Assert.That(ParseBigInteger(quote[3]), Is.EqualTo((BigInteger)200));
            Assert.That(ParseBigInteger(quote[4]), Is.EqualTo((BigInteger)1_000_000));
            Assert.That(ParseBigInteger(quote[5]), Is.EqualTo((BigInteger)500_000));
            Assert.That(ParseBigInteger(quote[6]), Is.EqualTo((BigInteger)1_500_000));
            Assert.That(ParseBigInteger(quote[7]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[8]), Is.EqualTo(BigInteger.Zero));
        });
    }

    [Test]
    public void QuoteTransfer_ForDirectBurn_ReturnsFullDestroyedAmountAndNoRecipient()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var factorySigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var token = DeployToken(engine, ownerSigner.Account, factorySigner.Account);
        SetBurnRateAsFactory(engine, token, factorySigner.Account, 200);

        var quote = token.quoteTransfer(ownerSigner.Account, UInt160.Zero, 10_000);

        Assert.That(quote, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ParseBigInteger(quote![0]), Is.EqualTo((BigInteger)10_000));
            Assert.That(ParseBigInteger(quote[1]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[2]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[3]), Is.EqualTo((BigInteger)10_000));
            Assert.That(ParseBigInteger(quote[4]), Is.EqualTo((BigInteger)1_000_000));
            Assert.That(ParseBigInteger(quote[5]), Is.EqualTo((BigInteger)500_000));
            Assert.That(ParseBigInteger(quote[6]), Is.EqualTo((BigInteger)1_500_000));
            Assert.That(ParseBigInteger(quote[7]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[8]), Is.EqualTo(BigInteger.One));
        });
    }

    [Test]
    public void QuoteTransfer_ForMintTransfer_IsFeeExempt()
    {
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        var factorySigner = TestEngine.GetNewSigner();
        var recipientSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);

        using var token = DeployToken(engine, ownerSigner.Account, factorySigner.Account);
        SetBurnRateAsFactory(engine, token, factorySigner.Account, 200);

        var quote = token.quoteTransfer(UInt160.Zero, recipientSigner.Account, 1_000);

        Assert.That(quote, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ParseBigInteger(quote![0]), Is.EqualTo((BigInteger)1_000));
            Assert.That(ParseBigInteger(quote[1]), Is.EqualTo((BigInteger)1_000));
            Assert.That(ParseBigInteger(quote[2]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[3]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[4]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[5]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[6]), Is.EqualTo(BigInteger.Zero));
            Assert.That(ParseBigInteger(quote[7]), Is.EqualTo(BigInteger.One));
            Assert.That(ParseBigInteger(quote[8]), Is.EqualTo(BigInteger.Zero));
        });
    }

    private static TokenTemplateContract DeployToken(TestEngine engine, UInt160 ownerAddress, UInt160 factoryAddress)
    {
        var nefPath = Path.Combine(ArtifactsPath, "TokenTemplate.nef");
        var manifestPath = Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        var deployArgs = new object[]
        {
            "QuoteToken",
            "QTE",
            (BigInteger)100_000,
            (BigInteger)8,
            ownerAddress,
            BigInteger.One,
            BigInteger.Zero,
            BigInteger.One,
            "",
            BigInteger.One,
            factoryAddress,
            (BigInteger)1_000_000,
            (BigInteger)500_000
        };

        return engine.Deploy<TokenTemplateContract>(nef, manifest, deployArgs);
    }

    private static void SetBurnRateAsFactory(TestEngine engine, TokenTemplateContract token, UInt160 factoryAddress, BigInteger burnRateBps)
    {
        try
        {
            engine.OnGetCallingScriptHash = (_, _) => factoryAddress;
            token.SetBurnRate(burnRateBps);
        }
        finally
        {
            engine.OnGetCallingScriptHash = null;
        }
    }

    private static BigInteger ParseBigInteger(object? item) => item switch
    {
        BigInteger bi => bi,
        byte b => b,
        int i => i,
        long l => l,
        Neo.VM.Types.PrimitiveType primitive => new BigInteger(primitive.GetSpan()),
        _ => BigInteger.Parse(item?.ToString() ?? "0"),
    };
}
