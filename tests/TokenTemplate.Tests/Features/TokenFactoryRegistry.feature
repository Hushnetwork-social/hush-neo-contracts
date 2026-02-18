Feature: TokenFactory — Registry Queries

  Background:
    Given the TokenFactory test engine is initialized
    And the factory is deployed and initialized

  Scenario: getTokensByCreator with no tokens returns empty list
    When getTokensByCreator for walletA page 0 size 10 is called
    Then an empty result is returned

  Scenario: getTokensByCreator returns first page correctly
    Given walletA has created 5 tokens
    When getTokensByCreator for walletA page 0 size 3 is called
    Then 3 results are returned

  Scenario: getTokensByCreator returns second page correctly
    Given walletA has created 5 tokens
    When getTokensByCreator for walletA page 1 size 3 is called
    Then 2 results are returned

  Scenario: getTokensByCreator page beyond range returns empty list
    Given walletA has created 2 tokens
    When getTokensByCreator for walletA page 1 size 5 is called
    Then an empty result is returned

  Scenario: getToken returns null for unregistered hash
    When getToken with zero hash is called
    Then the result is null

  Scenario: Global token count matches total created by all creators
    Given walletA has created 2 tokens
    And walletB has created 1 token
    When getTokenCount() is called
    Then the result is 3
