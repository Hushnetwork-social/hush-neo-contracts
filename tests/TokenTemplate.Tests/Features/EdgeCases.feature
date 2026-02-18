Feature: Edge Cases and Deployment Guards
  Boundary conditions, validation guards, and unusual input handling.

  Scenario: Deploy with decimals 0 (whole-unit token)
    Given the TokenTemplate test engine is initialized
    When the contract is deployed with decimals 0
    Then decimals() returns 0

  Scenario: Deploy with decimals 18 (maximum precision)
    Given the TokenTemplate test engine is initialized
    When the contract is deployed with decimals 18
    Then decimals() returns 18

  Scenario: Deploy with decimals 19 is rejected
    Given the TokenTemplate test engine is initialized
    When deploying the contract with decimals 19
    Then the deploy is aborted

  Scenario: Deploy with empty symbol is rejected
    Given the TokenTemplate test engine is initialized
    When deploying the contract with symbol ""
    Then the deploy is aborted

  Scenario: Deploy with zero address as owner is rejected
    Given the TokenTemplate test engine is initialized
    When deploying the contract with zero address as owner
    Then the deploy is aborted

  Scenario: Deploy with totalSupply 0 and mintable false is allowed
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, initialSupply 0
    Then totalSupply() is 0

  Scenario: maxSupply 0 means uncapped minting
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, mintable true, initialSupply 1000, maxSupply 0
    When the owner calls mint walletA 999999999
    Then totalSupply() is 1000000999

  Scenario: getMetadataUri returns the IPFS URI set at deployment
    Given the TokenTemplate test engine is initialized
    When the contract is deployed with metadataUri "ipfs://QmTest123"
    Then getMetadataUri() returns "ipfs://QmTest123"

  Scenario: getMetadataUri returns empty string when not provided
    Given the TokenTemplate test engine is initialized
    When the contract is deployed with metadataUri ""
    Then getMetadataUri() returns ""
