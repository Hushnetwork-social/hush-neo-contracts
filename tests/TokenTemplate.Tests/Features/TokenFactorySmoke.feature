Feature: TokenFactory — Smoke Test
  Validates that the TokenFactory compiles, deploys, and reports its GAS cost.
  Mirrors SmokeTest.feature which covers the same metrics for TokenTemplate.

  Scenario: Deploy TokenFactory and verify initial token count
    Given the TokenFactory test engine is initialized
    And a freshly deployed TokenFactory
    Then getTokenCount() returns 0

  Scenario: Measure GAS cost of TokenFactory deployment
    Given the TokenFactory test engine is initialized
    And a freshly deployed TokenFactory
    Then the factory deployment GAS is 1124311110 datoshi
