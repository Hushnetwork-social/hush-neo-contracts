#nullable enable
using Neo.SmartContract.Testing;
using Reqnroll;
using TokenTemplate.Tests.Support;

namespace TokenTemplate.Tests.Steps.Hooks;

/// <summary>
/// Reqnroll scenario lifecycle hooks.
/// BeforeScenario: creates a fresh TestEngine and owner signer for isolation.
/// AfterScenario: disposes the deployed contract (if any).
/// </summary>
[Binding]
public class ScenarioHooks
{
    private readonly TestContext _context;

    public ScenarioHooks(TestContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Runs before every scenario. Creates a fresh in-process Neo chain
    /// and a random owner signer so each scenario starts with clean state.
    /// </summary>
    [BeforeScenario]
    public void BeforeScenario()
    {
        _context.Engine = new TestEngine(true);
        _context.OwnerSigner = TestEngine.GetNewSigner();
        _context.Engine.SetTransactionSigners(_context.OwnerSigner);
        // Register walletA as the owner signer for multi-wallet scenarios
        _context.NamedSigners["walletA"] = _context.OwnerSigner;
    }

    /// <summary>
    /// Runs after every scenario. Disposes the deployed contract proxy.
    /// The TestEngine (and its in-memory storage) is discarded naturally.
    /// </summary>
    [AfterScenario]
    public void AfterScenario()
    {
        _context.Contract?.Dispose();
        _context.Contract = null;
    }
}
