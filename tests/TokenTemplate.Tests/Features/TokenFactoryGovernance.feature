Feature: TokenFactory - Governance Operations
  Verify FEAT-079 owner-only governance mutations, pause policy, claims, and template upgrades.

  Background:
    Given the TokenFactory test engine is initialized
    And the factory is deployed and initialized

  Scenario: Owner can update the creation fee
    When walletA calls setCreationFee(2000000000)
    And getMinFee() is called
    Then the result is 2000000000

  Scenario: Owner can update the operation fee
    When walletA calls setOperationFee(75000000)
    And getUpdateFee() is called
    Then the result is 75000000

  Scenario: Non-owner cannot update governance fees
    When walletB calls setCreationFee(2000000000)
    Then the transaction is aborted

  Scenario: Paused factory blocks token lifecycle mutations
    Given walletA has created community token MYTOK
    And the owner has paused the factory
    When walletA calls factory MintTokens 500000 to walletB
    Then the transaction is aborted

  Scenario: Template upgrade remains allowed while paused
    Given the owner has paused the factory
    When walletA calls upgradeTemplate with the TokenTemplate artifacts
    Then the transaction succeeds
    And the config template version is 2

  Scenario: Claim remains allowed while paused
    Given the owner has paused the factory
    And the factory has collected 300000000 GAS
    When walletA claims 100000000 GAS from the factory
    Then the transaction succeeds
    And the owner GAS balance increased by 100000000

  Scenario: Owner can partially claim GAS and leave the remainder in the factory
    Given the factory has collected 300000000 GAS
    When walletA claims 100000000 GAS from the factory
    Then the transaction succeeds
    And the owner GAS balance increased by 100000000
    And the factory GAS balance is 200000000

  Scenario: Owner can claim all remaining GAS from the factory
    Given the factory has collected 300000000 GAS
    When walletA claims all GAS from the factory
    Then the transaction succeeds
    And the owner GAS balance increased by 300000000
    And the factory GAS balance is 0
