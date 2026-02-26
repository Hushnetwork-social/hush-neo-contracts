Feature: TokenFactory — Storage Schema
  Verifies the storage defaults and property persistence of the TokenFactory contract.
  Step definitions are implemented in Phase 5 (TokenFactorySteps.cs).

  Background:
    Given the TokenFactory test engine is initialized

  # ── Deployment defaults ──────────────────────────────────────────────────────

  Scenario: Owner is set during deploy
    Given the factory is deployed with owner walletA
    When getOwner() is called
    Then the result equals walletA's address

  Scenario: MinFee defaults to 15 GAS
    Given a freshly deployed TokenFactory
    When getMinFee() is called
    Then the result is 1500000000

  Scenario: Factory is not paused by default
    Given a freshly deployed TokenFactory
    When isPaused() is called
    Then the result is false

  Scenario: PremiumTiersEnabled defaults to false
    Given a freshly deployed TokenFactory
    When getPremiumTiersEnabled() is called
    Then the result is false

  Scenario: Treasury is zero address by default
    Given a freshly deployed TokenFactory
    When getTreasury() is called
    Then the result is the zero address

  Scenario: Token count starts at zero
    Given a freshly deployed TokenFactory
    When getTokenCount() is called
    Then the result is 0

  # ── Initialization state ──────────────────────────────────────────────────────

  Scenario: Factory is not initialized before setNefAndManifest
    Given a freshly deployed TokenFactory
    When isInitialized() is called
    Then the result is false

  Scenario: Factory is initialized after setNefAndManifest
    Given a freshly deployed TokenFactory
    And the owner calls setNefAndManifest with the TokenTemplate artifacts
    When isInitialized() is called
    Then the result is true

  # ── Admin setters ─────────────────────────────────────────────────────────────

  Scenario: Owner can update the minimum fee
    Given a freshly deployed TokenFactory
    When the owner calls setFee with 2000000000
    And getMinFee() is called
    Then the result is 2000000000

  Scenario: Owner can set a treasury address
    Given a freshly deployed TokenFactory
    When the owner calls setTreasuryAddress with walletA
    And getTreasury() is called
    Then the result equals walletA's address

  Scenario: Owner can pause the factory
    Given a freshly deployed TokenFactory
    When the owner calls pause()
    And isPaused() is called
    Then the result is true

  Scenario: Owner can unpause the factory
    Given a freshly deployed TokenFactory
    And the owner has paused the factory
    When the owner calls unpause()
    And isPaused() is called
    Then the result is false

  Scenario: Owner can enable premium tiers
    Given a freshly deployed TokenFactory
    When the owner calls setPremiumTiersEnabled with true
    And getPremiumTiersEnabled() is called
    Then the result is true
