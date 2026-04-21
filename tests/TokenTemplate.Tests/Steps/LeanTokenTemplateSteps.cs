#nullable enable
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using NUnit.Framework;
using Reqnroll;
using System;
using System.IO;
using System.Numerics;
using TokenTemplate.Tests.Support;
using TestContext = TokenTemplate.Tests.Support.TestContext;

namespace TokenTemplate.Tests.Steps;

[Binding]
public class LeanTokenTemplateSteps
{
    private static readonly string ArtifactsPath =
        Path.Combine(AppContext.BaseDirectory, "artifacts");

    private readonly TestContext _context;
    private int _rejectedMutationCount;
    private int _attemptedMutationCount;

    public LeanTokenTemplateSteps(TestContext context)
    {
        _context = context;
    }

    [Given("the LeanTokenTemplate test engine is initialized")]
    public void GivenLeanEngineIsInitialized()
    {
        Assert.That(_context.Engine, Is.Not.Null,
            "TestEngine must be initialized by ScenarioHooks.BeforeScenario");

        if (_context.LeanEngine is null)
        {
            _context.Engine.SetTransactionSigners(_context.OwnerSigner);
            _context.LeanEngine = LeanTokenTemplateTestSupport.DeployEngine(
                _context.Engine,
                _context.OwnerSigner.Account);
        }
    }

    [Given(@"a lean token is deployed with owner (\w+), symbol ""(.*)"", decimals (\d+), and initialSupply (\d+)")]
    public void GivenLeanTokenIsDeployed(string ownerWallet, string symbol, int decimals, long initialSupply)
    {
        DeployLeanToken("active", ownerWallet, symbol, decimals, initialSupply);
    }

    [Given(@"a lean token is deployed with owner (\w+), symbol ""(.*)"", decimals (\d+), initialSupply (\d+), maxSupply (\d+), and upgradeable (true|false)")]
    public void GivenLeanTokenIsDeployedWithLifecycleConfig(
        string ownerWallet,
        string symbol,
        int decimals,
        long initialSupply,
        long maxSupply,
        string upgradeable)
    {
        DeployLeanToken(
            "active",
            ownerWallet,
            symbol,
            decimals,
            initialSupply,
            maxSupply: maxSupply,
            upgradeable: bool.Parse(upgradeable),
            pausable: true);
    }

    [Given(@"(\w+) owns lean token (\w+) with metadata ""(.*)"" and initialSupply (\d+)")]
    public void GivenWalletOwnsLeanTokenWithMetadata(
        string ownerWallet,
        string tokenAlias,
        string metadataUri,
        long initialSupply)
    {
        DeployLeanToken(
            tokenAlias,
            ownerWallet,
            tokenAlias.ToUpperInvariant(),
            8,
            initialSupply,
            metadataUri: metadataUri,
            maxSupply: initialSupply * 10,
            upgradeable: true,
            pausable: true);
    }

    [Given("a TokenFactory with full and lean artifacts is deployed")]
    public void GivenTokenFactoryWithFullAndLeanArtifactsIsDeployed()
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        _context.Factory = DeployFactory(_context.OwnerSigner.Account);

        var fullNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenTemplate.nef"));
        var fullManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "TokenTemplate.manifest.json"));
        var leanNef = File.ReadAllBytes(Path.Combine(ArtifactsPath, "LeanTokenTemplate.nef"));
        var leanManifest = File.ReadAllText(Path.Combine(ArtifactsPath, "LeanTokenTemplate.manifest.json"));

        _context.Factory.SetNefAndManifest(fullNef, fullManifest);
        _context.Factory.SetLeanNefAndManifest(leanNef, leanManifest);
        _context.LeanEngine = LeanTokenTemplateTestSupport.DeployEngine(
            _context.Engine,
            _context.Factory.Hash,
            "LeanFactoryBddEngine");
        _context.Factory.SetLeanEngine(_context.LeanEngine.Hash);
    }

    [When("lean symbol\\(\\) is called")]
    public void WhenLeanSymbolIsCalled() => Capture(() => _context.LeanContract!.Symbol);

    [When("lean decimals\\(\\) is called")]
    public void WhenLeanDecimalsIsCalled() => Capture(() => _context.LeanContract!.Decimals);

    [When("lean totalSupply\\(\\) is called")]
    public void WhenLeanTotalSupplyIsCalled() => Capture(() => _context.LeanContract!.TotalSupply);

    [When(@"lean balanceOf\((\w+)\) is called")]
    public void WhenLeanBalanceOfIsCalled(string wallet) =>
        Capture(() => _context.LeanContract!.BalanceOf(WalletAddress(wallet)));

    [When(@"(\w+) calls lean transfer to (\w+) amount (\d+)")]
    public void WhenWalletCallsLeanTransfer(string fromWallet, string toWallet, long amount)
    {
        var signer = GetOrCreateWallet(fromWallet);
        _context.Engine.SetTransactionSigners(signer);
        Capture(() => _context.LeanContract!.Transfer(
            signer.Account,
            WalletAddress(toWallet),
            (BigInteger)amount,
            null));
    }

    [When(@"(\w+) calls lean transfer from (\w+) to (\w+) amount (\d+)")]
    public void WhenWalletCallsLeanTransferFrom(string signerWallet, string fromWallet, string toWallet, long amount)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(signerWallet));
        Capture(() => _context.LeanContract!.Transfer(
            WalletAddress(fromWallet),
            WalletAddress(toWallet),
            (BigInteger)amount,
            null));
    }

    [When(@"(\w+) updates lean metadata to ""(.*)""")]
    public void WhenWalletUpdatesLeanMetadata(string wallet, string metadataUri)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.SetMetadataUri(metadataUri));
    }

    [When(@"(\w+) attempts to update lean metadata to ""(.*)""")]
    public void WhenWalletAttemptsToUpdateLeanMetadata(string wallet, string metadataUri) =>
        WhenWalletUpdatesLeanMetadata(wallet, metadataUri);

    [When(@"(\w+) sets lean maxSupply to (\d+)")]
    public void WhenWalletSetsLeanMaxSupply(string wallet, long maxSupply)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.SetMaxSupply((BigInteger)maxSupply));
    }

    [When(@"(\w+) mints (\d+) lean tokens to (\w+)")]
    public void WhenWalletMintsLeanTokens(string wallet, long amount, string toWallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.mint(WalletAddress(toWallet), (BigInteger)amount));
    }

    [When(@"(\w+) enables lean pausable")]
    public void WhenWalletEnablesLeanPausable(string wallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.setPausable(true));
    }

    [When(@"(\w+) pauses the lean token")]
    public void WhenWalletPausesLeanToken(string wallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.pause());
    }

    [When(@"(\w+) unpauses the lean token")]
    public void WhenWalletUnpausesLeanToken(string wallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.unpause());
    }

    [When(@"(\w+) locks the lean token")]
    public void WhenWalletLocksLeanToken(string wallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.Lock());
    }

    [When(@"(\w+) transfers lean ownership to (\w+)")]
    public void WhenWalletTransfersLeanOwnership(string fromWallet, string toWallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(fromWallet));
        CaptureVoid(() => _context.LeanContract!.setOwner(WalletAddress(toWallet)));
    }

    [When(@"(\w+) renounces lean ownership")]
    public void WhenWalletRenouncesLeanOwnership(string wallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.setOwner(UInt160.Zero));
    }

    [When(@"(\w+) attempts every owner mutation on the lean token")]
    public void WhenWalletAttemptsEveryOwnerMutation(string wallet)
    {
        var signer = GetOrCreateWallet(wallet);
        var recipient = WalletAddress("walletC");
        _attemptedMutationCount = 11;
        _rejectedMutationCount = 0;

        ExpectRejected(signer, () => _context.LeanContract!.SetMetadataUri("ipfs://bad"));
        ExpectRejected(signer, () => _context.LeanContract!.SetMaxSupply(9_000));
        ExpectRejected(signer, () => _context.LeanContract!.SetBurnRate(100));
        ExpectRejected(signer, () => _context.LeanContract!.SetCreatorFee(100_000));
        ExpectRejected(signer, () => _context.LeanContract!.SetPlatformFeeRate(100_000));
        ExpectRejected(signer, () => _context.LeanContract!.setPausable(false));
        ExpectRejected(signer, () => _context.LeanContract!.pause());
        ExpectRejected(signer, () => _context.LeanContract!.unpause());
        ExpectRejected(signer, () => _context.LeanContract!.mint(recipient, 1));
        ExpectRejected(signer, () => _context.LeanContract!.Lock());
        ExpectRejected(signer, () => _context.LeanContract!.setOwner(signer.Account));
    }

    [When(@"(\w+) updates lean token (\w+) metadata to ""(.*)""")]
    public void WhenWalletUpdatesNamedLeanMetadata(string wallet, string tokenAlias, string metadataUri)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => LeanToken(tokenAlias).SetMetadataUri(metadataUri));
    }

    [When(@"(\w+) mints (\d+) lean token (\w+) to (\w+)")]
    public void WhenWalletMintsNamedLeanToken(string wallet, long amount, string tokenAlias, string toWallet)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => LeanToken(tokenAlias).mint(WalletAddress(toWallet), (BigInteger)amount));
    }

    [When(@"(\w+) creates a lean community token through the factory")]
    public void WhenWalletCreatesLeanCommunityTokenThroughFactory(string wallet)
    {
        var creator = GetOrCreateWallet(wallet);
        SimulateGasPayment(creator, 1_500_000_000, new object[]
        {
            "Lean Factory Token",
            "LFT",
            (BigInteger)1_000,
            (BigInteger)8,
            "community",
            "",
            (BigInteger)0,
            "lean-nep17"
        });

        var tokens = _context.Factory!.GetTokensByCreator(creator.Account, 0, 100);
        Assert.That(tokens, Is.Not.Null.And.Length.GreaterThan(0));
        _context.LastCreatedTokenHash = tokens![tokens.Length - 1];
        _context.LeanContract = _context.Engine.FromHash<LeanTokenTemplateContract>(
            _context.LastCreatedTokenHash,
            true);
        _context.NamedLeanTokens["factoryToken"] = _context.LeanContract;
    }

    [When("the factory owner attempts to mint the lean factory token to (\\w+)")]
    public void WhenFactoryOwnerAttemptsToMintLeanFactoryToken(string toWallet)
    {
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        CaptureVoid(() => _context.Factory!.MintTokens(
            _context.LastCreatedTokenHash,
            WalletAddress(toWallet),
            50));
    }

    [When(@"(\w+) mints the lean factory token to (\w+) amount (\d+)")]
    public void WhenWalletMintsLeanFactoryToken(string wallet, string toWallet, long amount)
    {
        _context.Engine.SetTransactionSigners(GetOrCreateWallet(wallet));
        CaptureVoid(() => _context.LeanContract!.mint(WalletAddress(toWallet), (BigInteger)amount));
    }

    [Then(@"lean balanceOf (\w+) is (\d+)")]
    public void ThenLeanBalanceOfWalletIs(string wallet, long expected)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.BalanceOf(WalletAddress(wallet)),
            Is.EqualTo((BigInteger)expected));
    }

    [Then(@"lean metadata is ""(.*)""")]
    public void ThenLeanMetadataIs(string expected)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.getMetadataUri(), Is.EqualTo(expected));
    }

    [Then(@"lean maxSupply is (\d+)")]
    public void ThenLeanMaxSupplyIs(long expected)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.getMaxSupply(), Is.EqualTo((BigInteger)expected));
    }

    [Then(@"lean totalSupply is (\d+)")]
    public void ThenLeanTotalSupplyIs(long expected)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.TotalSupply, Is.EqualTo((BigInteger)expected));
    }

    [Then(@"lean pausable is (true|false)")]
    public void ThenLeanPausableIs(string expected)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.isPausable(), Is.EqualTo(bool.Parse(expected)));
    }

    [Then(@"lean paused is (true|false)")]
    public void ThenLeanPausedIs(string expected)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.isPaused(), Is.EqualTo(bool.Parse(expected)));
    }

    [Then(@"lean token is locked")]
    public void ThenLeanTokenIsLocked()
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.isLocked(), Is.True);
    }

    [Then(@"lean owner is (\w+)")]
    public void ThenLeanOwnerIs(string wallet)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.getOwner(), Is.EqualTo(WalletAddress(wallet)));
    }

    [Then("lean owner is the zero address")]
    public void ThenLeanOwnerIsZero()
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.getOwner(), Is.EqualTo(UInt160.Zero));
    }

    [Then("every lean owner mutation is rejected")]
    public void ThenEveryLeanOwnerMutationIsRejected()
    {
        Assert.That(_rejectedMutationCount, Is.EqualTo(_attemptedMutationCount));
    }

    [Then(@"lean token (\w+) metadata is ""(.*)""")]
    public void ThenNamedLeanTokenMetadataIs(string tokenAlias, string expected)
    {
        AssertNoLastException();
        Assert.That(LeanToken(tokenAlias).getMetadataUri(), Is.EqualTo(expected));
    }

    [Then(@"lean token (\w+) totalSupply is (\d+)")]
    public void ThenNamedLeanTokenTotalSupplyIs(string tokenAlias, long expected)
    {
        AssertNoLastException();
        Assert.That(LeanToken(tokenAlias).TotalSupply, Is.EqualTo((BigInteger)expected));
    }

    [Then(@"lean token (\w+) balanceOf (\w+) is (\d+)")]
    public void ThenNamedLeanTokenBalanceOfWalletIs(string tokenAlias, string wallet, long expected)
    {
        AssertNoLastException();
        Assert.That(LeanToken(tokenAlias).BalanceOf(WalletAddress(wallet)),
            Is.EqualTo((BigInteger)expected));
    }

    [Then(@"the lean manifest exposes NEP-17 methods and Transfer event")]
    public void ThenLeanManifestExposesNep17MethodsAndTransferEvent()
    {
        var manifest = LoadManifest("LeanTokenTemplate.manifest.json");
        Assert.Multiple(() =>
        {
            foreach (string method in new[] { "symbol", "decimals", "totalSupply", "balanceOf", "transfer" })
            {
                Assert.That(Array.Exists(manifest.Abi.Methods, item => item.Name == method), Is.True,
                    $"Expected method '{method}' in lean manifest.");
            }

            Assert.That(Array.Exists(manifest.Abi.Events, item => item.Name == "Transfer"), Is.True);
        });
    }

    [Then(@"the lean manifest exposes owner lifecycle methods")]
    public void ThenLeanManifestExposesOwnerLifecycleMethods()
    {
        var manifest = LoadManifest("LeanTokenTemplate.manifest.json");
        foreach (string method in new[]
        {
            "getOwner", "setOwner", "lock", "setMetadataUri", "setMaxSupply",
            "setBurnRate", "setCreatorFee", "setPlatformFeeRate", "setPausable",
            "pause", "unpause", "mint", "burn", "claimCreatorFees", "claimCreatorFee", "update"
        })
        {
            Assert.That(Array.Exists(manifest.Abi.Methods, item => item.Name == method), Is.True,
                $"Expected method '{method}' in lean manifest.");
        }
    }

    [Then(@"the factory records the deployed token profile as ""(.*)""")]
    public void ThenFactoryRecordsTheDeployedTokenProfileAs(string expected)
    {
        AssertNoLastException();
        Assert.That(_context.Factory!.GetTokenProfile(_context.LastCreatedTokenHash), Is.EqualTo(expected));
    }

    [Then(@"the lean factory token owner is (\w+)")]
    public void ThenLeanFactoryTokenOwnerIs(string wallet)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.getOwner(), Is.EqualTo(WalletAddress(wallet)));
    }

    [Then(@"the lean factory token balance of (\w+) is (\d+)")]
    public void ThenLeanFactoryTokenBalanceOfWalletIs(string wallet, long expected)
    {
        AssertNoLastException();
        Assert.That(_context.LeanContract!.BalanceOf(WalletAddress(wallet)),
            Is.EqualTo((BigInteger)expected));
    }

    private LeanTokenTemplateContract DeployLeanToken(
        string alias,
        string ownerWallet,
        string symbol,
        int decimals,
        long initialSupply,
        string metadataUri = "",
        long maxSupply = 0,
        bool upgradeable = false,
        bool pausable = false)
    {
        var owner = GetOrCreateWallet(ownerWallet);
        var factory = GetOrCreateWallet("factory");
        _context.Engine.SetTransactionSigners(owner);

        var token = LeanTokenTemplateTestSupport.Deploy(_context.Engine, new LeanDeployParams
        {
            Name = $"Lean {symbol}",
            Symbol = symbol,
            Owner = owner.Account,
            LaunchFactory = factory.Account,
            InitialSupply = (BigInteger)initialSupply,
            Decimals = (BigInteger)decimals,
            MaxSupply = (BigInteger)maxSupply,
            MetadataUri = metadataUri,
            Upgradeable = upgradeable,
            Pausable = pausable,
            EngineHash = _context.LeanEngine?.Hash,
            ManifestName = $"Lean{Sanitize(alias)}{Sanitize(symbol)}"
        });

        _context.LeanContract = token;
        _context.NamedLeanTokens[alias] = token;
        return token;
    }

    private TokenFactoryContract DeployFactory(UInt160 ownerAddress)
    {
        var nef = NefFile.Parse(File.ReadAllBytes(Path.Combine(ArtifactsPath, "TokenFactory.nef")));
        var manifest = ContractManifest.Parse(File.ReadAllText(Path.Combine(ArtifactsPath, "TokenFactory.manifest.json")));
        return _context.Engine.Deploy<TokenFactoryContract>(nef, manifest, ownerAddress);
    }

    private void SimulateGasPayment(Signer signer, BigInteger amountDatoshi, object[] tokenData)
    {
        try
        {
            int callingScriptHashCalls = 0;
            _context.Engine.OnGetCallingScriptHash = (_, currentHash) =>
                callingScriptHashCalls++ == 0 ? _context.Engine.Native.GAS.Hash : currentHash;
            _context.Engine.SetTransactionSigners(new Signer { Account = signer.Account, Scopes = WitnessScope.Global });
            CaptureVoid(() => _context.Factory!.OnNEP17Payment(signer.Account, amountDatoshi, tokenData));
        }
        finally
        {
            _context.Engine.OnGetCallingScriptHash = null;
        }
    }

    private LeanTokenTemplateContract LeanToken(string alias) => _context.NamedLeanTokens[alias];

    private Signer GetOrCreateWallet(string name)
    {
        if (!_context.NamedSigners.TryGetValue(name, out var signer))
        {
            signer = TestEngine.GetNewSigner();
            _context.NamedSigners[name] = signer;
        }

        return signer;
    }

    private UInt160 WalletAddress(string name) => GetOrCreateWallet(name).Account;

    private void Capture(Func<object?> action)
    {
        _context.LastException = null;
        try { _context.LastResult = action(); }
        catch (Exception ex) { _context.LastException = ex; }
    }

    private void CaptureVoid(Action action) => Capture(() =>
    {
        action();
        return null;
    });

    private void ExpectRejected(Signer signer, Action action)
    {
        _context.Engine.SetTransactionSigners(signer);
        try
        {
            action();
        }
        catch
        {
            _rejectedMutationCount++;
        }
    }

    private void AssertNoLastException()
    {
        Assert.That(_context.LastException, Is.Null,
            $"Expected no exception but got: {_context.LastException?.Message}");
    }

    private static ContractManifest LoadManifest(string fileName) =>
        ContractManifest.Parse(File.ReadAllText(Path.Combine(ArtifactsPath, fileName)));

    private static string Sanitize(string value)
    {
        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = 'x';
        }

        return new string(chars);
    }
}
