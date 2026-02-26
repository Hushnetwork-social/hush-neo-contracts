Feature: TokenTemplate — Lifecycle Setters
  Verify all six factory-gated setter methods (FEAT-078):
  SetBurnRate, SetMetadataUri, SetMaxSupply, SetCreatorFee, SetPlatformFeeRate, AuthorizeFactory.
  Each setter requires !locked AND CallingScriptHash == authorizedFactory.

  Background:
    Given the TokenTemplate test engine is initialized

  # ── SetBurnRate ──────────────────────────────────────────────────────────────

  Scenario: Factory sets burn rate — stored correctly
    Given the contract is deployed for lifecycle setter tests
    When the factory calls SetBurnRate with 200
    Then getBurnRate() returns 200

  Scenario: Factory sets burn rate to 0 — clears burn
    Given the contract is deployed for lifecycle setter tests
    When the factory calls SetBurnRate with 200
    And the factory calls SetBurnRate with 0
    Then getBurnRate() returns 0

  Scenario: SetBurnRate rejects value above 1000
    Given the contract is deployed for lifecycle setter tests
    When the factory calls SetBurnRate with 1001
    Then the transaction is aborted

  Scenario: SetBurnRate rejected on locked token
    Given the contract is deployed for lifecycle setter tests
    And the owner has locked the contract
    When the factory calls SetBurnRate with 200
    Then the transaction is aborted

  Scenario: Direct caller (non-factory) cannot call SetBurnRate
    Given the contract is deployed for lifecycle setter tests
    When walletB calls SetBurnRate with 200
    Then the transaction is aborted

  # ── SetMetadataUri ────────────────────────────────────────────────────────────

  Scenario: Factory updates metadata URI
    Given the contract is deployed for lifecycle setter tests
    When the factory updates the metadata URI to "https://example.com/icon.png"
    Then getMetadataUri() returns "https://example.com/icon.png"

  Scenario: Factory clears metadata URI (empty string)
    Given the contract is deployed for lifecycle setter tests
    When the factory updates the metadata URI to "https://example.com/icon.png"
    And the factory updates the metadata URI to ""
    Then the transaction is aborted

  Scenario: SetMetadataUri rejected on locked token
    Given the contract is deployed for lifecycle setter tests
    And the owner has locked the contract
    When the factory updates the metadata URI to "https://example.com/icon.png"
    Then the transaction is aborted

  # ── SetMaxSupply ──────────────────────────────────────────────────────────────

  Scenario: Factory sets max supply higher than current total
    Given the contract is deployed for setter tests with initialSupply 1000
    When the factory sets max supply to 5000000
    Then getMaxSupply() returns 5000000

  Scenario: Factory removes cap by setting maxSupply to 0
    Given the contract is deployed for setter tests with initialSupply 1000
    When the factory sets max supply to 5000000
    And the factory sets max supply to 0
    Then getMaxSupply() returns 0

  Scenario: SetMaxSupply rejects value below totalSupply
    Given the contract is deployed for setter tests with initialSupply 1000
    When the factory sets max supply to 500
    Then the transaction is aborted

  Scenario: SetMaxSupply rejected on locked token
    Given the contract is deployed for setter tests with initialSupply 0
    And the owner has locked the contract
    When the factory sets max supply to 5000000
    Then the transaction is aborted

  # ── SetCreatorFee ─────────────────────────────────────────────────────────────

  Scenario: Factory sets creator fee rate
    Given the contract is deployed for lifecycle setter tests
    When the factory sets creator fee to 100000
    Then getCreatorFeeRate() returns 100000

  Scenario: SetCreatorFee rejects rate above 5000000
    Given the contract is deployed for lifecycle setter tests
    When the factory sets creator fee to 5000001
    Then the transaction is aborted

  Scenario: SetCreatorFee accepts 0 (no fee)
    Given the contract is deployed for lifecycle setter tests
    When the factory sets creator fee to 0
    Then getCreatorFeeRate() returns 0

  Scenario: SetCreatorFee rejected on locked token
    Given the contract is deployed for lifecycle setter tests
    And the owner has locked the contract
    When the factory sets creator fee to 100000
    Then the transaction is aborted

  # ── SetPlatformFeeRate ────────────────────────────────────────────────────────

  Scenario: Factory sets platform fee rate
    Given the contract is deployed for lifecycle setter tests
    When the factory sets platform fee rate to 500000
    Then getPlatformFeeRate() returns 500000

  Scenario: SetPlatformFeeRate rejected on locked token
    Given the contract is deployed for lifecycle setter tests
    And the owner has locked the contract
    When the factory sets platform fee rate to 500000
    Then the transaction is aborted

  # ── AuthorizeFactory ──────────────────────────────────────────────────────────

  Scenario: Current factory can authorize new factory
    Given the contract is deployed for lifecycle setter tests
    When the factory authorizes walletB as new factory
    Then getAuthorizedFactory() returns walletB's address

  Scenario: Non-factory cannot authorize new factory
    Given the contract is deployed for lifecycle setter tests
    When walletB calls AuthorizeFactory with walletC
    Then the transaction is aborted

  Scenario: AuthorizeFactory rejects zero address
    Given the contract is deployed for lifecycle setter tests
    When the factory calls AuthorizeFactory with zero address
    Then the transaction is aborted

  Scenario: AuthorizeFactory rejected on locked token
    Given the contract is deployed for lifecycle setter tests
    And the owner has locked the contract
    When the factory authorizes walletB as new factory
    Then the transaction is aborted

  Scenario: After AuthorizeFactory, old factory setter calls are rejected
    Given the contract is deployed for lifecycle setter tests
    When the factory authorizes walletB as new factory
    And the original factory calls SetBurnRate with 200
    Then the transaction is aborted
