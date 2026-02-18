Feature: Owner Management
  Verify ownership transfer, renounce, and access control.

  Scenario: Owner transfers ownership to another address
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA
    When walletA calls setOwner walletB
    Then getOwner() is walletB

  Scenario: Non-owner cannot transfer ownership
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA
    When walletB calls setOwner walletC
    Then the transaction is aborted

  Scenario: Owner renounces ownership by passing zero address
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA
    When walletA calls setOwner zero
    Then getOwner() is zero address

  Scenario: Owner-only method fails after renounce
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable true
    And walletA has renounced ownership
    When walletA calls mint walletA 100
    Then the transaction is aborted
