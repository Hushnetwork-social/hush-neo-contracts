Feature: Upgradeable and Lock
  Verify the upgradeable flag, lock() operation, and post-lock restrictions.

  Scenario: isUpgradeable returns true when set at deploy
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable true
    Then isUpgradeable() returns true

  Scenario: isUpgradeable returns false when set at deploy
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable false
    Then isUpgradeable() returns false

  Scenario: Owner can lock an upgradeable contract
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable true
    When the owner calls lock
    Then isLocked() returns true

  Scenario: Locking twice is rejected
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable true
    And the owner has locked the contract
    When the owner calls lock
    Then the transaction is aborted

  Scenario: Non-owner cannot lock
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable true
    When walletB calls lock
    Then the transaction is aborted

  Scenario: setPausable is blocked after lock
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable true, pausable false
    And the owner has locked the contract
    When the owner calls setPausable true
    Then the transaction is aborted

  Scenario: pause still works after lock if pausable=true
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable true, pausable true
    And the owner has locked the contract
    When the owner calls pause
    Then isPaused() returns true
