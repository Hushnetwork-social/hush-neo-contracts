Feature: LeanTokenTemplate Owner Lifecycle
  Token owners can perform the same local lifecycle operations on lean tokens.

  Background:
    Given the LeanTokenTemplate test engine is initialized

  Scenario: Owner mutates lean token lifecycle properties
    Given a lean token is deployed with owner walletA, symbol "LIF", decimals 8, initialSupply 1000, maxSupply 2000, and upgradeable true
    When walletA updates lean metadata to "ipfs://updated"
    And walletA sets lean maxSupply to 2500
    And walletA mints 250 lean tokens to walletB
    And walletA enables lean pausable
    And walletA pauses the lean token
    Then lean paused is true
    When walletA unpauses the lean token
    Then lean metadata is "ipfs://updated"
    And lean maxSupply is 2500
    And lean totalSupply is 1250
    And lean balanceOf walletB is 250
    And lean pausable is true
    And lean paused is false

  Scenario: Lock blocks configuration and minting
    Given a lean token is deployed with owner walletA, symbol "LLK", decimals 8, initialSupply 1000, maxSupply 2000, and upgradeable true
    When walletA locks the lean token
    Then lean token is locked
    When walletA attempts to update lean metadata to "ipfs://locked"
    Then the transaction is aborted

  Scenario: Non-owner cannot use owner mutations
    Given a lean token is deployed with owner walletA, symbol "LNO", decimals 8, initialSupply 1000, maxSupply 2000, and upgradeable true
    When walletB attempts every owner mutation on the lean token
    Then every lean owner mutation is rejected
    And lean totalSupply is 1000

  Scenario: Ownership transfer and renounce follow full template behavior
    Given a lean token is deployed with owner walletA, symbol "LRO", decimals 8, initialSupply 1000, maxSupply 2000, and upgradeable true
    When walletA transfers lean ownership to walletB
    Then lean owner is walletB
    When walletB renounces lean ownership
    Then lean owner is the zero address
    When walletA calls lean transfer to walletC amount 100
    Then the boolean result is true
    And lean balanceOf walletC is 100

  Scenario: Lean manifest exposes lifecycle surface
    Then the lean manifest exposes owner lifecycle methods
