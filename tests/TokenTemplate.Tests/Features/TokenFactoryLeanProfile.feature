Feature: TokenFactory Lean Profile
  TokenFactory launches lean tokens but does not administer their local state afterward.

  Background:
    Given the LeanTokenTemplate test engine is initialized
    And a TokenFactory with full and lean artifacts is deployed

  Scenario: Factory launches but does not control lean token
    When walletA creates a lean community token through the factory
    Then the factory records the deployed token profile as "lean-nep17"
    And the lean factory token owner is walletA
    And the lean factory token balance of walletA is 1000
    When the factory owner attempts to mint the lean factory token to walletB
    Then the transaction is aborted
    When walletA mints the lean factory token to walletB amount 50
    Then the lean factory token balance of walletB is 50
