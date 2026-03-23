Feature: TokenTemplate - Transfer Fee System
  Verify the three-component fee system in Transfer() (FEAT-078 / FEAT-093):
  1. Token burn (basis points deducted from amount, sent to address(0))
  2. Platform GAS fee (fixed datoshi to authorizedFactory per transfer)
  3. Creator GAS fee (fixed datoshi accrued in the token contract per transfer)
  All fees are skipped when from == address(0) (factory mint path).
  Token burn is skipped when to == address(0) (burn() call path).

  Background:
    Given the TokenTemplate test engine is initialized

  Scenario: Transfer deducts burn rate from amount
    Given the contract is deployed with owner walletA, burn rate 200 bps, and initialSupply 100000
    When walletA transfers 1000 tokens to walletB
    Then walletB's token balance is 980
    And totalSupply() is 99980

  Scenario: Burn rate rounds to 0 for tiny amounts - no burn occurs
    Given the contract is deployed with owner walletA, burn rate 1 bps, and initialSupply 100000
    When walletA transfers 50 tokens to walletB
    Then walletB's token balance is 50
    And totalSupply() is 100000

  Scenario: Zero burn rate - full amount transferred
    Given the contract is deployed with owner walletA, burn rate 0 bps, and initialSupply 100000
    When walletA transfers 1000 tokens to walletB
    Then walletB's token balance is 1000
    And totalSupply() is 100000

  Scenario: Burn from burn() call - burn rate NOT applied to burn itself
    Given the contract is deployed with owner walletA, burn rate 200 bps, and initialSupply 100000
    When walletA calls burn 1000
    Then totalSupply() is 99000

  Scenario: Holder burn collects platform and creator GAS fees without recursive burn-rate tax
    Given the contract is deployed with owner walletA, factory walletC, platformFeeRate 1000000, creatorFeeRate 500000, burn rate 200 bps, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB burns 10000 tokens via burn()
    Then walletB's token balance is 90000
    And totalSupply() is 90000
    And walletC's GAS balance increased by 1000000 datoshi from the transfer
    And the token contract's GAS balance increased by 500000 datoshi from the transfer
    And getClaimableCreatorFee() returns 500000

  Scenario: Direct transfer pulls platform GAS fee to factory
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 1000000, and initialSupply 10000
    When walletA transfers 100 tokens to walletC
    Then walletB's GAS balance increased by 1000000 datoshi from the transfer

  Scenario: Platform fee of 0 - no GAS transfer
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 0, and initialSupply 10000
    When walletA transfers 100 tokens to walletC
    Then walletB's GAS balance increased by 0 datoshi from the transfer

  Scenario: Platform fee skipped when from == address(0) - factory mint path
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 1000000, and initialSupply 0
    When the authorized factory mints 1000 tokens to walletA
    Then walletB's GAS balance increased by 0 datoshi from the transfer

  Scenario: Direct transfer accrues creator GAS fee in the token contract
    Given the contract is deployed with owner walletA, creatorFeeRate 500000, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB transfers 10000 tokens to walletC
    Then the token contract's GAS balance increased by 500000 datoshi from the transfer
    And getClaimableCreatorFee() returns 500000

  Scenario: Creator fee of 0 - no GAS transfer
    Given the contract is deployed with owner walletA, creatorFeeRate 0, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB transfers 10000 tokens to walletC
    Then the token contract's GAS balance increased by 0 datoshi from the transfer
    And getClaimableCreatorFee() returns 0

  Scenario: Creator fee accrual remains visible when sender is also the creator
    Given the contract is deployed with owner walletA, creatorFeeRate 500000, and initialSupply 100000
    When walletA transfers 10000 tokens to walletB
    Then the token contract's GAS balance increased by 500000 datoshi from the transfer
    And getClaimableCreatorFee() returns 500000

  Scenario: Creator can partially claim accrued creator fees and leave the remainder
    Given the contract is deployed with owner walletA, real factory walletC, creatorFeeRate 500000, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB transfers 10000 tokens to walletC
    And walletB transfers 10000 tokens to walletD
    And walletA claims 250000 creator-fee GAS from the token
    Then factory's GAS balance increased by 50000000 datoshi from the claim
    And getClaimableCreatorFee() returns 750000

  Scenario: Creator can claim all remaining accrued creator fees
    Given the contract is deployed with owner walletA, real factory walletC, creatorFeeRate 500000, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB transfers 10000 tokens to walletC
    And walletA claims all creator-fee GAS from the token
    Then factory's GAS balance increased by 50000000 datoshi from the claim
    And getClaimableCreatorFee() returns 0

  Scenario: Transfer with burn and platform GAS fee
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 1000000, burn rate 100 bps, and initialSupply 100000
    When walletA transfers 1000 tokens to walletC
    Then walletC's token balance is 990
    And totalSupply() is 99990
    And walletB's GAS balance increased by 1000000 datoshi from the transfer

  Scenario: Transfer with burn, platform GAS fee, and creator GAS fee
    Given the contract is deployed with owner walletA, factory walletC, platformFeeRate 1000000, creatorFeeRate 500000, burn rate 200 bps, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB transfers 10000 tokens to walletD
    Then walletD's token balance is 9800
    And totalSupply() is 99800
    And walletC's GAS balance increased by 1000000 datoshi from the transfer
    And the token contract's GAS balance increased by 500000 datoshi from the transfer
    And getClaimableCreatorFee() returns 500000

  Scenario: Zero-config token charges no token taxes
    Given the contract is deployed with owner walletA, factory walletC, platformFeeRate 0, creatorFeeRate 0, burn rate 0 bps, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB transfers 1000 tokens to walletD
    Then walletD's token balance is 1000
    And totalSupply() is 100000
    And walletC's GAS balance increased by 0 datoshi from the transfer
    And the token contract's GAS balance increased by 0 datoshi from the transfer
    And getClaimableCreatorFee() returns 0

  Scenario: Transfer with configured GAS taxes aborts when the holder has insufficient GAS
    Given the contract is deployed with owner walletA, factory walletC, platformFeeRate 1000000, creatorFeeRate 500000, burn rate 200 bps, and initialSupply 0
    And walletA mints 100000 tokens to walletB
    When walletB transfers 10000 tokens to walletD without additional GAS funding
    Then the transaction is aborted
    And walletD's token balance remains 0
    And totalSupply() is 100000
    And walletC's GAS balance increased by 0 datoshi from the transfer
    And the token contract's GAS balance increased by 0 datoshi from the transfer
    And getClaimableCreatorFee() returns 0

  Scenario: Factory MintByFactory is exempt from all fees
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 1000000, burn rate 200 bps, and initialSupply 0
    When the authorized factory mints 1000 tokens to walletC
    Then walletC's token balance is 1000
    And totalSupply() is 1000
    And walletB's GAS balance increased by 0 datoshi from the transfer

  Scenario: Paused token transfer is rejected
    Given the contract is deployed with owner walletA, pausable true
    And the owner has paused the contract
    When walletA transfers 100 tokens to walletB
    Then the transaction is aborted

  Scenario: MintByFactory increases balance without fees
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 1000000, and initialSupply 0
    When the authorized factory mints 5000 tokens to walletA
    Then walletA's token balance is 5000
    And totalSupply() is 5000

  Scenario: MintByFactory blocked on non-mintable token
    Given the contract is deployed with owner walletA, factory walletB, mintable false, and initialSupply 0
    When the authorized factory mints 1000 tokens to walletA
    Then the transaction is aborted

  Scenario: MintByFactory blocked when locked
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 0, and initialSupply 0
    And the owner has locked the contract
    When the authorized factory mints 1000 tokens to walletA
    Then the transaction is aborted

  Scenario: MintByFactory blocked when maxSupply would be exceeded
    Given the contract is deployed with owner walletA, factory walletB, maxSupply 1000, and initialSupply 500
    When the authorized factory mints 600 tokens to walletA
    Then the transaction is aborted

  Scenario: Non-factory cannot call MintByFactory
    Given the contract is deployed with owner walletA, factory walletB, platformFeeRate 0, and initialSupply 0
    When walletC calls MintByFactory 1000 to walletA
    Then the transaction is aborted
