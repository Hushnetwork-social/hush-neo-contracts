Feature: TokenFactory — Edge Cases and Guards

  Background:
    Given the TokenFactory test engine is initialized

  Scenario: Non-GAS token payment is rejected
    Given the factory is deployed and initialized
    When a non-GAS token is transferred to the factory
    Then the transaction is aborted

  Scenario: GAS below minimum fee is rejected
    Given the factory is deployed and initialized
    When walletA transfers 100 GAS to the factory with valid token params
    Then the transaction is aborted

  Scenario: Factory not initialized rejects any payment
    Given a freshly deployed TokenFactory
    When walletA transfers 1500000000 GAS to the factory with valid token params
    Then the transaction is aborted

  Scenario: Factory paused rejects new token creation
    Given the factory is deployed and initialized
    And the factory is paused by the owner
    When walletA transfers 1500000000 GAS to the factory with valid token params
    Then the transaction is aborted

  Scenario: Mode "premium" is rejected
    Given the factory is deployed and initialized
    When walletA transfers 1500000000 GAS to the factory with token params "T" "T" 100 8 "premium"
    Then the transaction is aborted

  Scenario: Mode "crowdfund" is rejected
    Given the factory is deployed and initialized
    When walletA transfers 1500000000 GAS to the factory with token params "T" "T" 100 8 "crowdfund"
    Then the transaction is aborted

  Scenario: Data array with only 3 elements is rejected
    Given the factory is deployed and initialized
    When walletA sends payment with 3 data elements
    Then the transaction is aborted

  Scenario: Data array with null data is accepted as fee accumulation receipt
    Given the factory is deployed and initialized
    When walletA sends payment with null data
    Then the transaction succeeds

  Scenario: Rejected payment does not update token count
    Given the factory is deployed and initialized
    When walletA transfers 100 GAS to the factory with valid token params
    Then getTokenCount() is still 0

  Scenario: Token count is preserved on failed creation
    Given the factory is deployed and initialized
    And walletA has already created 1 token
    When walletA sends a payment that is rejected
    Then getTokenCount() is still 1
