Feature: LeanTokenTemplate NEP-17 Compatibility
  Lean tokens must look like independent NEP-17 tokens to wallets.

  Background:
    Given the LeanTokenTemplate test engine is initialized

  Scenario: Wallet-facing lean token reads work
    Given a lean token is deployed with owner walletA, symbol "LNN", decimals 8, and initialSupply 1000
    When lean symbol() is called
    Then the result is "LNN"
    When lean decimals() is called
    Then the numeric result is 8
    When lean totalSupply() is called
    Then the numeric result is 1000
    When lean balanceOf(walletA) is called
    Then the numeric result is 1000
    When lean balanceOf(walletB) is called
    Then the numeric result is 0

  Scenario: Lean transfer handles success, zero amount, insufficient balance, and invalid witness
    Given a lean token is deployed with owner walletA, symbol "LTX", decimals 8, and initialSupply 1000
    When walletA calls lean transfer to walletB amount 300
    Then the boolean result is true
    And lean balanceOf walletA is 700
    And lean balanceOf walletB is 300
    When walletA calls lean transfer to walletB amount 0
    Then the boolean result is true
    When walletA calls lean transfer to walletB amount 2000
    Then the boolean result is false
    When walletB calls lean transfer from walletA to walletB amount 100
    Then the boolean result is false
    And lean balanceOf walletA is 700
    And lean balanceOf walletB is 300

  Scenario: Lean manifest exposes NEP-17 surface
    Then the lean manifest exposes NEP-17 methods and Transfer event
