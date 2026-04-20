param(
    [string]$Test = "all"
)

$ErrorActionPreference = "Stop"

$chain = "devnet/devnet.neo-express"
$supportedTests = @("all", "01", "02", "03", "lean")
if ($supportedTests -notcontains $Test) {
    throw "Unsupported -Test value '$Test'. Use one of: $($supportedTests -join ', ')"
}

$tokenRef = "#TT0"
$tempFiles = @()

function Invoke-Neo {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $output = & neoxp @Arguments 2>&1
    $raw = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "neoxp $($Arguments -join ' ') failed.`n$raw"
    }

    return $raw
}

function Invoke-NeoJson {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $raw = Invoke-Neo -Arguments ($Arguments + @("-j"))
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "Expected JSON output from neoxp $($Arguments -join ' ')"
    }

    return $raw | ConvertFrom-Json
}

function New-InvokeFile {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Steps,
        [Parameter(Mandatory = $true)]
        [string]$Prefix
    )

    $path = Join-Path $env:TEMP ("$Prefix-" + [Guid]::NewGuid().ToString() + ".neo-invoke.json")
    $json = ConvertTo-Json -InputObject $Steps -Depth 20
    [IO.File]::WriteAllText($path, $json)
    $script:tempFiles += $path
    return $path
}

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-IntegerResult {
    param(
        [Parameter(Mandatory = $true)]
        $Result
    )

    return [System.Numerics.BigInteger]::Parse([string]$Result.stack[0].value)
}

function Get-ArrayResult {
    param(
        [Parameter(Mandatory = $true)]
        $Result
    )

    return $Result.stack[0].value
}

function Get-TextResult {
    param(
        [Parameter(Mandatory = $true)]
        $Item
    )

    if ($Item.type -eq "ByteString" -or $Item.type -eq "ByteArray") {
        return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([string]$Item.value))
    }

    return [string]$Item.value
}

function Step {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

try {
    Step "Compiling contracts..."
    & (Join-Path $PSScriptRoot "compile.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "compile.ps1 failed"
    }

    Step "Resetting Neo Express state..."
    Invoke-Neo -Arguments @("stop", "-i", $chain) -AllowFailure | Out-Null
    Invoke-Neo -Arguments @("reset", "-a", "-f", "-i", $chain) | Out-Null

    Step "Funding devnet wallets..."
    Invoke-Neo -Arguments @("transfer", "1000", "GAS", "genesis", "deployer", "-i", $chain) | Out-Null
    Invoke-Neo -Arguments @("transfer", "100", "GAS", "genesis", "node1", "-i", $chain) | Out-Null

    Step "Deploying TokenFactory and BondingCurveRouter..."
    Invoke-Neo -Arguments @("contract", "deploy", "src/TokenFactory/bin/sc/TokenFactory.nef", "deployer", "-i", $chain) | Out-Null
    Invoke-Neo -Arguments @("contract", "deploy", "src/BondingCurveRouter/bin/sc/BondingCurveRouter.nef", "deployer", "-i", $chain) | Out-Null

    $contracts = Invoke-NeoJson -Arguments @("contract", "list", "-i", $chain)
    $factory = $contracts | Where-Object { $_.name -eq "TokenFactory" }
    $router = $contracts | Where-Object { $_.name -eq "BondingCurveRouter" }

    Assert-Condition ($null -ne $factory) "TokenFactory deployment was not found on devnet."
    Assert-Condition ($null -ne $router) "BondingCurveRouter deployment was not found on devnet."

    Step "Initializing factory artifacts and router linkage..."
    $nefBase64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path "src/TokenTemplate/bin/sc/TokenTemplate.nef")))
    $manifest = [IO.File]::ReadAllText((Resolve-Path "src/TokenTemplate/bin/sc/TokenTemplate.manifest.json"))
    $leanNefBase64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path "src/LeanTokenTemplate/bin/sc/LeanTokenTemplate.nef")))
    $leanManifest = [IO.File]::ReadAllText((Resolve-Path "src/LeanTokenTemplate/bin/sc/LeanTokenTemplate.manifest.json"))
    $initFile = New-InvokeFile -Prefix "feat074-init" -Steps @(
        @{
            contract = "BondingCurveRouter"
            operation = "setAuthorizedFactory"
            args = @(
                @{
                    type = "Hash160"
                    value = [string]$factory.hash
                }
            )
        },
        @{
            contract = "TokenFactory"
            operation = "setBondingCurveRouter"
            args = @(
                @{
                    type = "Hash160"
                    value = [string]$router.hash
                }
            )
        },
        @{
            contract = "TokenFactory"
            operation = "setNefAndManifest"
            args = @(
                @{
                    type = "ByteArray"
                    value = $nefBase64
                },
                @{
                    type = "String"
                    value = $manifest
                }
            )
        },
        @{
            contract = "TokenFactory"
            operation = "setLeanNefAndManifest"
            args = @(
                @{
                    type = "ByteArray"
                    value = $leanNefBase64
                },
                @{
                    type = "String"
                    value = $leanManifest
                }
            )
        }
    )
    Invoke-Neo -Arguments @("contract", "invoke", $initFile, "deployer", "-w", "Global", "-i", $chain) | Out-Null

    $isInitialized = Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TokenFactory", "isInitialized")
    $isLeanInitialized = Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TokenFactory", "isLeanInitialized")
    Assert-Condition ($isInitialized.stack[0].value -eq $true) "TokenFactory did not initialize on devnet."
    Assert-Condition ($isLeanInitialized.stack[0].value -eq $true) "TokenFactory did not initialize lean artifacts on devnet."

    if ($Test -eq "01") {
        Step "Smoke stage 01 passed."
        exit 0
    }

    if ($Test -eq "lean") {
        Step "Creating a lean community token through GasToken.transfer..."
        $leanCreateFile = New-InvokeFile -Prefix "feat108-lean-create" -Steps @(
            @{
                contract = "GasToken"
                operation = "transfer"
                args = @(
                    "@deployer",
                    "#TokenFactory",
                    1500000000,
                    @("LeanSmoke", "LSMK", 1000000, 8, "community", "", 0, "lean-nep17")
                )
            }
        )
        Invoke-Neo -Arguments @("contract", "invoke", $leanCreateFile, "deployer", "-w", "Global", "-i", $chain) | Out-Null

        $tokenCount = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TokenFactory", "getTokenCount"))
        Assert-Condition ($tokenCount -eq 1) "Expected one created token after lean launch smoke."

        $contractsAfterLeanCreate = Invoke-NeoJson -Arguments @("contract", "list", "-i", $chain)
        $ltt0 = $contractsAfterLeanCreate | Where-Object { $_.name -eq "LTT0" }
        Assert-Condition ($null -ne $ltt0) "Expected LTT0 contract after lean launch smoke."

        $profileResult = Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TokenFactory", "getTokenProfile", "#LTT0")
        $profile = Get-TextResult $profileResult.stack[0]
        Assert-Condition ($profile -eq "lean-nep17") "Expected LTT0 profile to be lean-nep17."

        $symbolResult = Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "LTT0", "symbol")
        $symbol = Get-TextResult $symbolResult.stack[0]
        Assert-Condition ($symbol -eq "LSMK") "Expected LTT0 symbol to be LSMK."

        $ownerBalanceBefore = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "LTT0", "balanceOf", "@deployer"))
        Assert-Condition ($ownerBalanceBefore -eq 1000000) "Expected deployer to receive lean initial supply."

        Step "Transferring lean tokens between wallets..."
        $leanTransferFile = New-InvokeFile -Prefix "feat108-lean-transfer" -Steps @(
            @{
                contract = "LTT0"
                operation = "transfer"
                args = @(
                    "@deployer",
                    "@node1",
                    12345,
                    $null
                )
            }
        )
        Invoke-Neo -Arguments @("contract", "invoke", $leanTransferFile, "deployer", "-w", "Global", "-i", $chain) | Out-Null

        $ownerBalanceAfter = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "LTT0", "balanceOf", "@deployer"))
        $nodeBalanceAfter = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "LTT0", "balanceOf", "@node1"))
        Assert-Condition ($ownerBalanceAfter -eq 987655) "Expected deployer lean balance to decrease by 12345."
        Assert-Condition ($nodeBalanceAfter -eq 12345) "Expected node1 lean balance to be 12345."

        Step "Lean devnet smoke passed."
        Write-Host "  Created token: LTT0" -ForegroundColor Green
        Write-Host "  Profile: $profile" -ForegroundColor Green
        Write-Host "  Symbol: $symbol" -ForegroundColor Green
        Write-Host "  Deployer balance before transfer: $ownerBalanceBefore" -ForegroundColor Green
        Write-Host "  Deployer balance after transfer: $ownerBalanceAfter" -ForegroundColor Green
        Write-Host "  node1 balance after transfer: $nodeBalanceAfter" -ForegroundColor Green
        exit 0
    }

    Step "Creating a direct speculation token through GasToken.transfer..."
    $createFile = New-InvokeFile -Prefix "feat074-create" -Steps @(
        @{
            contract = "GasToken"
            operation = "transfer"
            args = @(
                "@deployer",
                "#TokenFactory",
                1500000000,
                @("SpecToken", "SPEC", 1000000, 8, "speculation", "", 0, "GAS", 800000)
            )
        }
    )
    Invoke-Neo -Arguments @("contract", "invoke", $createFile, "deployer", "-w", "Global", "-i", $chain) | Out-Null

    $tokenCount = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TokenFactory", "getTokenCount"))
    Assert-Condition ($tokenCount -eq 1) "Expected one created token after speculation launch smoke."

    $contractsAfterCreate = Invoke-NeoJson -Arguments @("contract", "list", "-i", $chain)
    $tt0 = $contractsAfterCreate | Where-Object { $_.name -eq "TT0" }
    Assert-Condition ($null -ne $tt0) "Expected TT0 contract after speculation launch smoke."

    $ownerBalance = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TT0", "balanceOf", "@deployer"))
    $routerBalance = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TT0", "balanceOf", "#BondingCurveRouter"))
    Assert-Condition ($ownerBalance -eq 200000) "Owner retained inventory did not remain at 200000."
    Assert-Condition ($routerBalance -eq 800000) "Router custody inventory did not equal 800000 after activation."

    if ($Test -eq "02") {
        Step "Smoke stage 02 passed."
        exit 0
    }

    Step "Executing buy and sell smoke against the live curve..."
    $buyFile = New-InvokeFile -Prefix "feat074-buy" -Steps @(
        @{
            contract = "GasToken"
            operation = "transfer"
            args = @(
                "@node1",
                "#BondingCurveRouter",
                800000000,
                @($tokenRef, 1)
            )
        }
    )
    Invoke-Neo -Arguments @("contract", "invoke", $buyFile, "node1", "-w", "Global", "-i", $chain) | Out-Null

    $nodeBalanceAfterBuy = Get-IntegerResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "TT0", "balanceOf", "@node1"))
    Assert-Condition ($nodeBalanceAfterBuy -gt 0) "Trader did not receive any TT0 balance from the buy smoke."

    $sellAmount = [System.Numerics.BigInteger]::Divide($nodeBalanceAfterBuy, [System.Numerics.BigInteger]::Parse("2"))
    if ($sellAmount -le 0) {
        $sellAmount = [System.Numerics.BigInteger]::One
    }

    $sellFile = New-InvokeFile -Prefix "feat074-sell" -Steps @(
        @{
            contract = "TT0"
            operation = "transfer"
            args = @(
                "@node1",
                "#BondingCurveRouter",
                [long]$sellAmount,
                @(
                    1,
                    [long]$sellAmount
                )
            )
        }
    )
    Invoke-Neo -Arguments @("contract", "invoke", $sellFile, "node1", "-w", "Global", "-i", $chain) | Out-Null

    $curve = Get-ArrayResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "BondingCurveRouter", "getCurve", $tokenRef))
    $progress = Get-ArrayResult (Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "BondingCurveRouter", "getGraduationProgress", $tokenRef))
    $isReady = Invoke-NeoJson -Arguments @("contract", "run", "-r", "-i", $chain, "BondingCurveRouter", "isGraduationReady", $tokenRef)

    $curveState = Get-TextResult $curve[0]
    Assert-Condition (($curveState -eq "ACTIVE") -or ($curveState -eq "GRADUATION_READY")) "Curve state was neither ACTIVE nor GRADUATION_READY."
    Assert-Condition ($curve[9].value -eq "2") "Expected exactly two router trades in smoke flow."
    Assert-Condition ([System.Numerics.BigInteger]::Parse([string]$progress[2].value) -gt 0) "Expected graduation progress to remain inspectable after trading."
    Assert-Condition ($progress[3].value -eq $isReady.stack[0].value) "Expected graduation progress readiness flag to match isGraduationReady."

    Step "Devnet smoke passed."
    Write-Host "  Created token: TT0" -ForegroundColor Green
    Write-Host "  Curve state: $curveState" -ForegroundColor Green
    Write-Host "  Owner retained inventory: $ownerBalance" -ForegroundColor Green
    Write-Host "  Router launch inventory: $routerBalance" -ForegroundColor Green
    Write-Host "  Trader buy balance before sell: $nodeBalanceAfterBuy" -ForegroundColor Green
    Write-Host "  Trader sell amount: $sellAmount" -ForegroundColor Green
    Write-Host "  Graduation progress: $($progress[2].value) bps" -ForegroundColor Green
    Write-Host "  Graduation ready: $($isReady.stack[0].value)" -ForegroundColor Green
}
finally {
    foreach ($tempFile in $tempFiles) {
        if ($tempFile -and (Test-Path -LiteralPath $tempFile)) {
            Remove-Item -LiteralPath $tempFile -Force
        }
    }
}
