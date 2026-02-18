Feature: TokenTemplate — Storage Schema
  Verify that every storage property can be written and read back correctly.
  These tests validate the raw data layer in isolation, before business-logic
  rules are applied. Step definitions are implemented in Phase 5.

  Background:
    Given the TokenTemplate test engine is initialized

  # ── String properties ────────────────────────────────────────────────────

  Scenario: Name is stored and retrieved correctly
    When the contract is deployed with name "HushNetwork Community Token"
    Then getName() returns "HushNetwork Community Token"

  Scenario: Symbol is stored and retrieved correctly
    When the contract is deployed with symbol "HCT"
    Then symbol() returns "HCT"

  # ── Numeric properties ───────────────────────────────────────────────────

  Scenario: Decimals is stored and retrieved correctly
    When the contract is deployed with decimals 8
    Then decimals() returns 8

  Scenario: Decimals accepts zero (integer-like token)
    When the contract is deployed with decimals 0
    Then decimals() returns 0

  Scenario: MaxSupply is stored and retrieved correctly
    When the contract is deployed with maxSupply 1000000000
    Then getMaxSupply() returns 1000000000

  Scenario: MaxSupply zero means uncapped
    When the contract is deployed with maxSupply 0
    Then getMaxSupply() returns 0

  # ── Boolean flags ─────────────────────────────────────────────────────────

  Scenario: Mintable flag stored correctly when true
    When the contract is deployed with mintable true
    Then getMintable() returns true

  Scenario: Mintable flag stored correctly when false
    When the contract is deployed with mintable false
    Then getMintable() returns false

  Scenario: Upgradeable flag stored correctly when true
    When the contract is deployed with upgradeable true
    Then isUpgradeable() returns true

  Scenario: Upgradeable flag stored correctly when false
    When the contract is deployed with upgradeable false
    Then isUpgradeable() returns false

  Scenario: Locked defaults to false on fresh deploy
    When the contract is deployed with default parameters
    Then isLocked() returns false

  Scenario: Pausable flag stored correctly when true
    When the contract is deployed with pausable true
    Then isPausable() returns true

  Scenario: Pausable flag stored correctly when false
    When the contract is deployed with pausable false
    Then isPausable() returns false

  Scenario: Paused state defaults to false on fresh deploy
    When the contract is deployed with default parameters
    Then isPaused() returns false

  # ── IPFS metadata ─────────────────────────────────────────────────────────

  Scenario: MetadataUri stored and retrieved correctly
    When the contract is deployed with metadataUri "ipfs://QmTestHash123"
    Then getMetadataUri() returns "ipfs://QmTestHash123"

  Scenario: MetadataUri can be empty string
    When the contract is deployed with metadataUri ""
    Then getMetadataUri() returns ""

  # ── Owner ────────────────────────────────────────────────────────────────

  Scenario: Owner address stored and retrieved correctly
    When the contract is deployed with a specific owner address
    Then getOwner() returns that exact address
