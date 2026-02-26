Feature: TokenTemplate — Smoke Test
  Validates that the BDD test infrastructure works end-to-end:
  the TestEngine starts, a TokenTemplate contract deploys successfully,
  and method invocation returns the correct value.
  This is the minimum proof that Phase 5 infrastructure is working.

  Scenario: Deploy TokenTemplate and verify symbol
    Given the TokenTemplate test engine is initialized
    When the contract is deployed with symbol "TST" and decimals 8
    And symbol() is called on the deployed contract
    Then the returned string is "TST"

  Scenario: Measure GAS cost of TokenTemplate deployment
    Given the TokenTemplate test engine is initialized
    When the contract is deployed with symbol "TST" and decimals 8
    Then the GAS consumed by deployment is 1080082170 datoshi
