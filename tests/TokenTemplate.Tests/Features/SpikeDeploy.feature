Feature: Spike — ContractManagement.Deploy() callable from within a contract
  Validates the critical FEAT-070 architecture assumption:
  a running Neo N3 contract can call ContractManagement.Deploy() to create a new contract instance.

  This feature MUST pass before FEAT-070 factory implementation begins.
  If any scenario here fails, the factory architecture must pivot to the
  event + off-chain deployer fallback pattern.

  Scenario: Spike factory deploys a TokenTemplate and receives its contract hash
    Given the SpikeDeploy test engine is initialized
    When the spike factory deploys a TokenTemplate with symbol "SPIKE" and owner walletA
    Then the returned contract hash is not zero
    And calling symbol() on the deployed token returns "SPIKE"

  Scenario: Factory-deployed TokenTemplate has initial supply in creator wallet
    Given the SpikeDeploy test engine is initialized
    When the spike factory deploys a TokenTemplate with symbol "FUND", owner walletA, and initialSupply 1000000
    Then balanceOf walletA on the deployed token is 1000000
    And totalSupply of the deployed token is 1000000
