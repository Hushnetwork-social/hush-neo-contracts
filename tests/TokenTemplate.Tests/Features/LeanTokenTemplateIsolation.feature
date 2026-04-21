Feature: LeanTokenTemplate Isolation
  Separate wallet-native lean facades keep token-scoped shared-engine storage isolated.

  Background:
    Given the LeanTokenTemplate test engine is initialized

  Scenario: Owner operation affects only one lean token
    Given walletA owns lean token tokenA with metadata "ipfs://alpha" and initialSupply 100
    And walletB owns lean token tokenB with metadata "ipfs://beta" and initialSupply 200
    When walletA updates lean token tokenA metadata to "ipfs://alpha-updated"
    And walletA mints 50 lean token tokenA to walletC
    Then lean token tokenA metadata is "ipfs://alpha-updated"
    And lean token tokenA totalSupply is 150
    And lean token tokenA balanceOf walletC is 50
    And lean token tokenB metadata is "ipfs://beta"
    And lean token tokenB totalSupply is 200
    And lean token tokenB balanceOf walletC is 0
