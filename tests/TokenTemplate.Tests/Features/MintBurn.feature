Feature: Mint and Burn
  Verify mint (owner-only, mintable guard, maxSupply cap) and
  burn (any holder, amount guard) behaviours.

  # ── BURN ──────────────────────────────────────────────────────────────────

  Scenario: Owner can burn their own tokens
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 1000
    When walletA calls burn 500
    Then balanceOf walletA is 500
    And totalSupply() is 500

  Scenario: Burning more than balance is rejected
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 100
    When walletA calls burn 200
    Then the transaction is aborted

  Scenario: Burning 0 tokens is rejected
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 100
    When walletA calls burn 0
    Then the transaction is aborted

  # ── MINT ──────────────────────────────────────────────────────────────────

  Scenario: Owner mints when mintable=true and uncapped
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable true, initialSupply 1000
    When the owner calls mint walletB 500
    Then balanceOf walletB is 500
    And totalSupply() is 1500

  Scenario: Mint is rejected when mintable=false
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable false
    When the owner calls mint walletB 100
    Then the transaction is aborted

  Scenario: Non-owner cannot mint
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable true
    When walletB calls mint walletB 100
    Then the transaction is aborted

  Scenario: Mint beyond maxSupply is rejected
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable true, initialSupply 900, maxSupply 1000
    When the owner calls mint walletA 200
    Then the transaction is aborted

  Scenario: Mint up to maxSupply succeeds
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable true, initialSupply 800, maxSupply 1000
    When the owner calls mint walletA 200
    Then totalSupply() is 1000

  Scenario: Mint of 0 tokens is rejected
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable true
    When the owner calls mint walletA 0
    Then the transaction is aborted
