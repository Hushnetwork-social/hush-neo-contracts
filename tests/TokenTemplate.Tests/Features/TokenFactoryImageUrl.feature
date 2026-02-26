Feature: TokenFactory — Image URL Support

  Background:
    Given the TokenFactory test engine is initialized
    And the factory is deployed and initialized

  Scenario: Token created with imageUrl stores it on the deployed contract
    When walletA transfers 1500000000 GAS to the factory with token params "IconToken" "ICN" 1000 8 "community" "https://example.com/icon.png"
    Then the transaction succeeds
    And the registry token imageUrl is "https://example.com/icon.png"

  Scenario: Token created with empty imageUrl stores empty string
    When walletA transfers 1500000000 GAS to the factory with token params "NoIconToken" "NIC" 1000 8 "community" ""
    Then the transaction succeeds
    And the registry token imageUrl is ""

  Scenario: imageUrl is stored in the deployed TokenTemplate contract via metadataUri
    When walletA transfers 1500000000 GAS to the factory with token params "MetaToken" "META" 5000 8 "community" "https://ipfs.io/ipfs/Qm123abc"
    Then the transaction succeeds
    And the registry token imageUrl is "https://ipfs.io/ipfs/Qm123abc"

  Scenario: Registry GetToken includes imageUrl at index 6
    When walletA transfers 1500000000 GAS to the factory with token params "RegToken" "RGT" 99999 8 "community" "https://cdn.example.com/rgt.png"
    Then the registry token has symbol "RGT" mode "community" tier "standard" supply 99999
    And the registry token imageUrl is "https://cdn.example.com/rgt.png"
