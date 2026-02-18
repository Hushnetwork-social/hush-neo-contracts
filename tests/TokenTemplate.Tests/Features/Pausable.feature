Feature: Pausable
  Verify pause/unpause controls and transfer blocking when paused.

  Scenario: Contract starts unpaused
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, pausable true
    Then isPaused() returns false

  Scenario: Owner can pause
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, pausable true
    When the owner calls pause
    Then isPaused() returns true

  Scenario: Transfers are blocked when paused
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, pausable true, initialSupply 1000
    And the owner has paused the contract
    When walletA calls transfer to walletB amount 100
    Then the transaction is aborted

  Scenario: Owner can unpause
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, pausable true
    And the owner has paused the contract
    When the owner calls unpause
    Then isPaused() returns false

  Scenario: pause is rejected when pausable=false
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, pausable false
    When the owner calls pause
    Then the transaction is aborted

  Scenario: Non-owner cannot pause
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, pausable true
    When walletB calls pause
    Then the transaction is aborted

  Scenario: setPausable enables pause on upgradeable contract
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable true, pausable false
    When the owner calls setPausable true
    And the owner calls pause
    Then isPaused() returns true

  Scenario: setPausable is blocked on non-upgradeable contract
    Given the TokenTemplate test engine is initialized
    And the contract is deployed with owner walletA, upgradeable false, pausable false
    When the owner calls setPausable true
    Then the transaction is aborted
