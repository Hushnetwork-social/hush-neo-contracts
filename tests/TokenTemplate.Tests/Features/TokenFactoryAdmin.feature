Feature: TokenFactory — Admin Functions

  Background:
    Given the TokenFactory test engine is initialized
    And the factory is deployed with owner walletA

  Scenario: Owner can set NEF and manifest
    When walletA calls setNefAndManifest with the TokenTemplate artifacts
    And isInitialized() is called
    Then the result is true

  Scenario: Non-owner cannot set NEF and manifest
    When walletB calls setNefAndManifest with the TokenTemplate artifacts
    Then the transaction is aborted

  Scenario: Owner can update the minimum fee
    When walletA calls setFee(2000000000)
    And getMinFee() is called
    Then the result is 2000000000

  Scenario: Non-owner cannot update the minimum fee
    When walletB calls setFee(2000000000)
    Then the transaction is aborted

  Scenario: Owner can pause the factory
    And the factory is initialized
    When walletA calls pause()
    And isPaused() is called
    Then the result is true

  Scenario: Owner can unpause the factory
    And the factory is initialized and paused
    When walletA calls unpause()
    And isPaused() is called
    Then the result is false

  Scenario: Owner can transfer ownership
    When walletA calls setOwner(walletB)
    And getOwner() is called
    Then the result equals walletB's address

  Scenario: Old owner loses admin rights after ownership transfer
    When walletA calls setOwner(walletB)
    And walletA calls setFee(9999)
    Then the transaction is aborted

  Scenario: New owner can exercise admin rights after transfer
    When walletA calls setOwner(walletB)
    And walletB calls setFee(9999)
    And getMinFee() is called
    Then the result is 9999

  Scenario: Owner can set treasury address
    When walletA calls setTreasuryAddress(walletB)
    And getTreasury() is called
    Then the result equals walletB's address

  Scenario: Owner can enable premium tiers
    When walletA calls setPremiumTiersEnabled(true)
    And getPremiumTiersEnabled() is called
    Then the result is true
