Feature: TokenTemplate — Lifecycle Storage Fields
  Verify that all new lifecycle storage fields (FEAT-078) are persisted correctly
  by _deploy() and that validation guards reject invalid inputs.

  Background:
    Given the TokenTemplate test engine is initialized

  Scenario: Deploy with authorizedFactory stores the address
    When the contract is deployed with owner walletA and authorizedFactory walletB
    Then getAuthorizedFactory() returns walletB's address

  Scenario: Deploy with platformFeeRate stores the rate
    When the contract is deployed with owner walletA and platformFeeRate 1000000
    Then getPlatformFeeRate() returns 1000000

  Scenario: Deploy with creatorFeeRate stores the rate
    When the contract is deployed with owner walletA and creatorFeeRate 500000
    Then getCreatorFeeRate() returns 500000

  Scenario: getBurnRate() returns 0 on fresh deploy
    When the contract is deployed with default parameters
    Then getBurnRate() returns 0

  Scenario: Deploy rejects zero authorizedFactory
    When deploying the contract with zero authorizedFactory
    Then the deploy is aborted

  Scenario: Deploy rejects platformFeeRate above maximum
    When deploying the contract with platformFeeRate 10000001
    Then the deploy is aborted

  Scenario: Deploy rejects creatorFeeRate above maximum
    When deploying the contract with creatorFeeRate 5000001
    Then the deploy is aborted

  Scenario: Deploy accepts creatorFeeRate of 0
    When the contract is deployed with owner walletA and creatorFeeRate 0
    Then the transaction succeeds

  Scenario: getAuthorizedFactory returns factory set at deploy
    When the contract is deployed with owner walletA and authorizedFactory walletB
    And getAuthorizedFactory() is called
    Then the result equals walletB's address
