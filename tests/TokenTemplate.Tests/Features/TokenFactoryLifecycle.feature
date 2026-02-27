Feature: TokenFactory — Token Lifecycle Operations
  Verify all FEAT-078 creator-callable lifecycle methods and batch admin methods.
  All lifecycle methods require: token exists, creator == CheckWitness, token !locked,
  update fee (0.5 GAS) paid to factory (except LockToken which charges no fee).

  Background:
    Given the TokenFactory test engine is initialized
    And the factory is deployed and initialized
    And walletA has created community token MYTOK

  # ── MintTokens ───────────────────────────────────────────────────────────────

  Scenario: Creator mints additional tokens via factory
    When walletA calls factory MintTokens 500000 to walletB
    Then the transaction succeeds
    And walletB's token balance on MYTOK is 500000

  Scenario: Non-creator cannot mint via factory
    When walletB calls factory MintTokens 500000 to walletB
    Then the transaction is aborted

  Scenario: MintTokens fails on non-mintable token
    Given walletA has created non-mintable token NOMIN
    When walletA calls factory MintTokens on NOMIN 500000 to walletB
    Then the transaction is aborted

  Scenario: MintTokens fails on locked token
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory MintTokens 500000 to walletB
    Then the transaction is aborted

  Scenario: MintTokens collects update fee from creator
    When walletA calls factory MintTokens 100000 to walletB
    Then the factory GAS balance increased from the operation

  # ── SetTokenBurnRate ─────────────────────────────────────────────────────────

  Scenario: Creator sets burn rate on their token
    When walletA calls factory SetTokenBurnRate 200
    Then the transaction succeeds

  Scenario: SetTokenBurnRate updates tokenInfo registry
    When walletA calls factory SetTokenBurnRate 300
    Then the registry token burn rate is 300

  Scenario: SetTokenBurnRate fails for non-creator
    When walletB calls factory SetTokenBurnRate 200
    Then the transaction is aborted

  Scenario: SetTokenBurnRate rejects bps above 1000
    When walletA calls factory SetTokenBurnRate 1001
    Then the transaction is aborted

  Scenario: SetTokenBurnRate fails on locked token
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory SetTokenBurnRate 200
    Then the transaction is aborted

  Scenario: SetTokenBurnRate collects update fee
    When walletA calls factory SetTokenBurnRate 100
    Then the factory GAS balance increased from the operation

  # ── SetTokenMaxSupply ─────────────────────────────────────────────────────────

  Scenario: Creator adjusts max supply
    When walletA calls factory SetTokenMaxSupply 5000000
    Then the transaction succeeds
    And the registry token max supply is 5000000

  Scenario: Creator removes cap by setting 0
    When walletA calls factory SetTokenMaxSupply 0
    Then the registry token max supply is 0

  Scenario: SetTokenMaxSupply fails below current supply
    When walletA calls factory SetTokenMaxSupply 500
    Then the transaction is aborted

  Scenario: SetTokenMaxSupply updates tokenInfo registry
    When walletA calls factory SetTokenMaxSupply 9999999
    Then the registry token max supply is 9999999

  Scenario: SetTokenMaxSupply fails on locked token
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory SetTokenMaxSupply 5000000
    Then the transaction is aborted

  # ── UpdateTokenMetadata ───────────────────────────────────────────────────────

  Scenario: Creator updates image URL
    When walletA calls factory UpdateTokenMetadata "https://cdn.hushnetwork.social/mytok.png"
    Then the transaction succeeds
    And the registry token imageUrl is "https://cdn.hushnetwork.social/mytok.png"

  Scenario: UpdateTokenMetadata updates tokenInfo registry
    When walletA calls factory UpdateTokenMetadata "https://new.example.com/icon.png"
    Then the registry token imageUrl is "https://new.example.com/icon.png"

  Scenario: Non-creator cannot update metadata
    When walletB calls factory UpdateTokenMetadata "https://evil.example.com/icon.png"
    Then the transaction is aborted

  Scenario: UpdateTokenMetadata fails on locked token
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory UpdateTokenMetadata "https://locked.example.com/icon.png"
    Then the transaction is aborted

  # ── SetCreatorFee ─────────────────────────────────────────────────────────────

  Scenario: Creator sets transfer fee rate
    When walletA calls factory SetCreatorFee 100000
    Then the transaction succeeds

  Scenario: SetCreatorFee rejects rate above 5000000
    When walletA calls factory SetCreatorFee 5000001
    Then the transaction is aborted

  Scenario: SetCreatorFee emits event and collects fee
    When walletA calls factory SetCreatorFee 200000
    Then the factory GAS balance increased from the operation

  Scenario: SetCreatorFee fails on locked token
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory SetCreatorFee 100000
    Then the transaction is aborted

  # ── ChangeTokenMode ───────────────────────────────────────────────────────────

  Scenario: Creator transitions community to speculation
    When walletA calls factory ChangeTokenMode to "speculation"
    Then the registry token mode is "speculation"

  Scenario: Creator transitions community to crowdfunding
    When walletA calls factory ChangeTokenMode to "crowdfunding"
    Then the registry token mode is "crowdfunding"

  Scenario: Creator reverts speculation to community
    Given the token mode is "speculation"
    When walletA calls factory ChangeTokenMode to "community"
    Then the registry token mode is "community"

  Scenario: Speculation to crowdfunding transition is rejected
    Given the token mode is "speculation"
    When walletA calls factory ChangeTokenMode to "crowdfunding"
    Then the transaction is aborted

  Scenario: Crowdfunding to speculation transition is rejected
    Given the token mode is "crowdfunding"
    When walletA calls factory ChangeTokenMode to "speculation"
    Then the transaction is aborted

  Scenario: ChangeTokenMode stores modeParams
    When walletA calls factory ChangeTokenMode to "speculation" with params
    Then the mode params are stored for MYTOK

  Scenario: ChangeTokenMode updates tokenInfo mode
    When walletA calls factory ChangeTokenMode to "crowdfunding"
    Then the registry token mode is "crowdfunding"

  Scenario: ChangeTokenMode fails on locked token
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory ChangeTokenMode to "speculation"
    Then the transaction is aborted

  # ── LockToken ────────────────────────────────────────────────────────────────

  Scenario: Creator locks their token
    When walletA calls factory LockToken
    Then the transaction succeeds

  Scenario: LockToken sets tokenInfo locked to 1
    When walletA calls factory LockToken
    Then the registry token is locked

  Scenario: Non-creator cannot lock token
    When walletB calls factory LockToken
    Then the transaction is aborted

  Scenario: Locking already-locked token is rejected
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory LockToken
    Then the transaction is aborted

  Scenario: After lock, MintTokens is rejected
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory MintTokens 100000 to walletB
    Then the transaction is aborted

  Scenario: LockToken does not collect update fee
    When walletA calls factory LockToken
    Then the factory GAS balance did not increase from the operation

  # ── Batch admin methods ───────────────────────────────────────────────────────

  Scenario: Owner authorizes all tokens to new factory in one batch
    When the owner calls factory AuthorizeAllTokens walletB offset 0 batchSize 10
    Then MYTOK's authorizedFactory is walletB's address

  Scenario: SetAllTokensPlatformFee updates all tokens in batch
    When the owner calls factory SetAllTokensPlatformFee 2000000 offset 0 batchSize 10
    Then MYTOK's platformFeeRate is 2000000

  Scenario: Batch methods reject non-owner caller
    When walletB calls factory AuthorizeAllTokens walletC offset 0 batchSize 10
    Then the transaction is aborted

  Scenario: Batch with offset beyond count processes zero tokens
    When the owner calls factory AuthorizeAllTokens walletB offset 100 batchSize 10
    Then the transaction succeeds

  # ── Atomic staged changes (single tx) ───────────────────────────────────────

  Scenario: ApplyTokenChanges updates metadata in one atomic call
    When walletA calls factory ApplyTokenChanges metadata "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme"
    Then the transaction succeeds
    And the registry token imageUrl is "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme"

  Scenario: ApplyTokenChanges updates burn rate in one atomic call
    When walletA calls factory ApplyTokenChanges burnRate 180
    Then the transaction succeeds
    And the registry token burn rate is 180

  Scenario: ApplyTokenChanges updates max supply in one atomic call
    When walletA calls factory ApplyTokenChanges maxSupply 2200000
    Then the transaction succeeds
    And the registry token max supply is 2200000

  Scenario: ApplyTokenChanges updates creator fee in one atomic call
    When walletA calls factory ApplyTokenChanges creatorFee 250000
    Then the transaction succeeds
    And the token creator fee rate is 250000

  Scenario: ApplyTokenChanges updates mode in one atomic call
    When walletA calls factory ApplyTokenChanges mode "speculation"
    Then the transaction succeeds
    And the registry token mode is "speculation"

  Scenario: ApplyTokenChanges updates 2 fields atomically
    When walletA calls factory ApplyTokenChanges with 2 changes metadata "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme" and burnRate 220
    Then the transaction succeeds
    And the registry token imageUrl is "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme"
    And the registry token burn rate is 220

  Scenario: ApplyTokenChanges updates 3 fields atomically
    When walletA calls factory ApplyTokenChanges with 3 changes creatorFee 300000 burnRate 250 and mint 500000 to walletB
    Then the transaction succeeds
    And the token creator fee rate is 300000
    And the registry token burn rate is 250
    And the registry token supply is 1500000
    And walletB's token balance on MYTOK is 500000

  Scenario: ApplyTokenChanges updates 4 fields atomically
    When walletA calls factory ApplyTokenChanges with 4 changes metadata "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme" burnRate 125 creatorFee 150000 and mode "speculation"
    Then the transaction succeeds
    And the registry token imageUrl is "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme"
    And the registry token burn rate is 125
    And the token creator fee rate is 150000
    And the registry token mode is "speculation"

  Scenario: ApplyTokenChanges fails on locked token
    Given walletA has locked the MYTOK token via factory
    When walletA calls factory ApplyTokenChanges burnRate 180
    Then the transaction is aborted

  Scenario: ApplyTokenChanges rejects maxSupply and mint in the same call
    When walletA calls factory ApplyTokenChanges with maxSupply 2500000 and mint 500000 to walletB
    Then the transaction is aborted

  Scenario: ApplyTokenChanges can update metadata and lock atomically
    When walletA calls factory ApplyTokenChanges metadata "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme" and lock
    Then the transaction succeeds
    And the registry token imageUrl is "https://scarlet-given-sheep-822.mypinata.cloud/ipfs/bafybeic7fqu2ri7bd4jhxlvfu35pzzoqhbb54etgk56q5dqmbymicdanme"
    And the registry token is locked

  Scenario: After atomic lock, later changes are rejected
    Given walletA has applied metadata update and lock atomically on MYTOK
    When walletA calls factory UpdateTokenMetadata "https://locked-after-atomic.example/icon.png"
    Then the transaction is aborted
