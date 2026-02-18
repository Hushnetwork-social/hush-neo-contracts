Feature: NEP-17 Compliance
  Verify the TokenTemplate contract fully complies with the NEP-17 standard:
  symbol, decimals, totalSupply, balanceOf, and transfer behaviours.

  Scenario: symbol() returns the value set at deployment
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, symbol "HCT" and decimals 8
    When symbol() is called
    Then the result is "HCT"

  Scenario: decimals() returns the value set at deployment
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, symbol "TST" and decimals 0
    When decimals() is called
    Then the numeric result is 0

  Scenario: totalSupply() returns the initial supply after deployment
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 1000000000
    When totalSupply() is called
    Then the numeric result is 1000000000

  Scenario: balanceOf() returns 0 for address with no tokens
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 0
    When balanceOf(walletB) is called
    Then the numeric result is 0

  Scenario: balanceOf(owner) returns full initial supply
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 500
    When balanceOf(walletA) is called
    Then the numeric result is 500

  Scenario: transfer() moves tokens between accounts
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 1000
    When walletA calls transfer to walletB amount 300
    Then balanceOf walletA is 700
    And balanceOf walletB is 300

  Scenario: transfer() returns false when caller is not the from account
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 1000
    When walletB calls transfer from walletA to walletB amount 100
    Then the boolean result is false
    And balanceOf walletA is 1000

  Scenario: transfer() returns false when balance is insufficient
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 100
    When walletA calls transfer to walletB amount 200
    Then the boolean result is false

  Scenario: transfer() with amount 0 succeeds
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 0
    When walletA calls transfer to walletB amount 0
    Then the boolean result is true
