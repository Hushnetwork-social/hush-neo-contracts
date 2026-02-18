Feature: TokenTemplate Health Check
  Verify the BDD test framework is correctly wired to Neo.SmartContract.Testing
  and can load the compiled TokenTemplate contract artifact.

  Scenario: Contract artifact loads successfully
    Given the TokenTemplate contract artifact exists
    Then the NEF file can be read without error
