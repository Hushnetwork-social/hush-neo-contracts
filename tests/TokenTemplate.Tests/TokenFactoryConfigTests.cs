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
public class TokenFactoryConfigTests
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Test]
    public void GetConfig_WhenFactoryIsFresh_ReturnsStableDefaultOrder()
    {
        // Arrange
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);
        using var factory = DeployFactory(engine, ownerSigner.Account);

        // Act
        var config = factory.GetConfig();

        // Assert
        Assert.That(config, Is.Not.Null);
        Assert.That(config!.Length, Is.EqualTo(8));
        Assert.That(ParseBigInteger(config[0]), Is.EqualTo((BigInteger)1_500_000_000));
        Assert.That(ParseBigInteger(config[1]), Is.EqualTo((BigInteger)50_000_000));
        Assert.That(ParseBoolean(config[2]), Is.False);
        Assert.That(ParseHash(config[3]), Is.EqualTo(ownerSigner.Account));
        Assert.That(ParseHash(config[4]), Is.EqualTo(UInt160.Zero));
        Assert.That(ParseBigInteger(config[5]), Is.EqualTo(BigInteger.One));
        Assert.That(ParseBoolean(config[6]), Is.False);
        Assert.That(ParseBoolean(config[7]), Is.False);
    }

    [Test]
    public void GetConfig_ReflectsFeePauseAndTemplateStateAfterInitialization()
    {
        // Arrange
        var engine = new TestEngine(true);
        var ownerSigner = TestEngine.GetNewSigner();
        engine.SetTransactionSigners(ownerSigner);
        using var factory = DeployFactory(engine, ownerSigner.Account);

        var nefBytes = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var manifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));

        factory.SetFee(2_000_000_000);
        factory.SetUpdateFee(75_000_000);
        factory.Pause();
        factory.SetNefAndManifest(nefBytes, manifest);

        // Act
        var config = factory.GetConfig();

        // Assert
        Assert.That(config, Is.Not.Null);
        Assert.That(config!.Length, Is.EqualTo(8));
        Assert.That(ParseBigInteger(config[0]), Is.EqualTo((BigInteger)2_000_000_000));
        Assert.That(ParseBigInteger(config[1]), Is.EqualTo((BigInteger)75_000_000));
        Assert.That(ParseBoolean(config[2]), Is.True);
        Assert.That(ParseHash(config[3]), Is.EqualTo(ownerSigner.Account));
        Assert.That(ParseHash(config[4]), Is.Not.EqualTo(UInt160.Zero));
        Assert.That(ParseBigInteger(config[5]), Is.EqualTo(BigInteger.One));
        Assert.That(ParseBoolean(config[6]), Is.True);
        Assert.That(ParseBoolean(config[7]), Is.True);
    }

    private static TokenFactoryContract DeployFactory(TestEngine engine, UInt160 ownerAddress)
    {
        var nefPath = Path.Combine(ArtifactsPath, "TokenFactory.nef");
        var manifestPath = Path.Combine(ArtifactsPath, "TokenFactory.manifest.json");

        Assert.That(File.Exists(nefPath), Is.True, $"TokenFactory NEF not found: {nefPath}");
        Assert.That(File.Exists(manifestPath), Is.True, $"TokenFactory manifest not found: {manifestPath}");

        var nef = NefFile.Parse(File.ReadAllBytes(nefPath));
        var manifest = ContractManifest.Parse(File.ReadAllText(manifestPath));

        return engine.Deploy<TokenFactoryContract>(nef, manifest, ownerAddress);
    }

    private static UInt160 ParseHash(object? item) => item switch
    {
        UInt160 hash => hash,
        byte[] bytes when bytes.Length == 20 => new UInt160(bytes),
        Neo.VM.Types.ByteString byteString => new UInt160(byteString.GetSpan()),
        Neo.VM.Types.PrimitiveType primitive => new UInt160(primitive.GetSpan()),
        _ => UInt160.Zero,
    };

    private static BigInteger ParseBigInteger(object? item) => item switch
    {
        BigInteger bi => bi,
        byte b => b,
        int i => i,
        long l => l,
        Neo.VM.Types.PrimitiveType primitive => new BigInteger(primitive.GetSpan()),
        _ => BigInteger.Parse(item?.ToString() ?? "0"),
    };

    private static bool ParseBoolean(object? item) => item switch
    {
        bool value => value,
        Neo.VM.Types.Boolean boolean => boolean.GetBoolean(),
        Neo.VM.Types.PrimitiveType primitive => primitive.GetBoolean(),
        _ => bool.Parse(item?.ToString() ?? "false"),
    };
}
