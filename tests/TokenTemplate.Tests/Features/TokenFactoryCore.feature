Feature: TokenFactory — Core Token Creation

  Background:
    Given the TokenFactory test engine is initialized
    And the factory is deployed and initialized

  Scenario: Successful community token creation deploys a TokenTemplate
    When walletA transfers 1500000000 GAS to the factory with token params "MyToken" "MTK" 1000000 8 "community"
    Then the transaction succeeds
    And getTokenCount() returns 1
    And the returned token hash is not zero
    And the deployed token's symbol() returns "MTK"

  Scenario: Creator wallet receives initial supply after creation
    When walletA transfers 1500000000 GAS to the factory with token params "FundToken" "FND" 500000 2 "community"
    Then the transaction succeeds
    And balanceOf walletA on the deployed token is 500000
    And totalSupply of the deployed token is 500000

  Scenario: TokenInfo is stored in the registry after creation
    When walletA transfers 1500000000 GAS to the factory with token params "RegToken" "RGT" 99999 8 "community"
    Then the registry token has symbol "RGT" mode "community" tier "standard" supply 99999

  Scenario: Creator address is recorded in TokenInfo
    When walletA transfers 1500000000 GAS to the factory with token params "MyToken" "MTK" 1000 8 "community"
    Then the registry token creator equals walletA's address

  Scenario: Registry is fully populated after successful creation
    When walletA transfers 1500000000 GAS to the factory with token params "EvtToken" "EVT" 777 8 "community"
    Then the transaction succeeds
    And the registry token has symbol "EVT" mode "community" tier "standard" supply 777

  Scenario: getTokensByCreator includes new token after creation
    When walletA transfers 1500000000 GAS to the factory with token params "MyToken" "MTK" 1000 8 "community"
    Then getTokensByCreator for walletA page 0 size 10 contains the last created token

  Scenario: Two creators each get their own per-creator index
    When walletA transfers 1500000000 GAS to the factory with token params "TokenA" "TKA" 100 8 "community"
    And walletB transfers 1500000000 GAS to the factory with token params "TokenB" "TKB" 200 8 "community"
    Then getTokensByCreator for walletA page 0 size 10 has 1 result
    And getTokensByCreator for walletB page 0 size 10 has 1 result

  Scenario: Payment exactly at minimum fee is accepted
    When walletA transfers 1500000000 GAS to the factory with token params "MyToken" "MTK" 100 8 "community"
    Then the transaction succeeds

  Scenario: Payment above minimum fee is accepted
    When walletA transfers 2000000000 GAS to the factory with token params "MyToken" "MTK" 100 8 "community"
    Then the transaction succeeds
