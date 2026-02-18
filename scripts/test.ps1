# Run ephemeral tests — fresh chain, compile, deploy, verify, stop
# Usage: .\scripts\test.ps1
# Usage: .\scripts\test.ps1 -Test 01

param(
    [string]$Test = "all"
)

$ErrorActionPreference = "Stop"

# Step 1: Compile all contracts
Write-Host "Compiling contracts..." -ForegroundColor Cyan
foreach ($c in @("TokenTemplate", "TokenFactory")) {
    Push-Location "src\$c"
    dotnet build -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $c" -ForegroundColor Red
        exit 1
    }
    Pop-Location
}
Write-Host "  All contracts compiled OK" -ForegroundColor Green

# Step 2: Start ephemeral chain in background
Write-Host "Starting ephemeral chain..." -ForegroundColor Cyan
$chainJob = Start-Job -ScriptBlock {
    neoxp run --discard --seconds-per-block 1
}

# Wait for RPC to be available
$rpcReady = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 1
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:40332" -Method POST `
            -ContentType "application/json" `
            -Body '{"jsonrpc":"2.0","method":"getblockcount","params":[],"id":1}' `
            -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $rpcReady = $true
            break
        }
    } catch {}
}

if (-not $rpcReady) {
    Write-Host "Chain did not start in time" -ForegroundColor Red
    Stop-Job $chainJob
    exit 1
}
Write-Host "  Chain is ready" -ForegroundColor Green

# Step 3: Run tests
$testFiles = Get-ChildItem "tests\*.batch" | Sort-Object Name

if ($Test -ne "all") {
    $testFiles = $testFiles | Where-Object { $_.Name -like "$Test*" }
}

$passed = 0
$failed = 0

foreach ($testFile in $testFiles) {
    Write-Host ""
    Write-Host "Running: $($testFile.Name)" -ForegroundColor Cyan
    neoxp batch $testFile.FullName
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  PASSED" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "  FAILED" -ForegroundColor Red
        $failed++
    }
}

# Step 4: Stop chain
Stop-Job $chainJob
Remove-Job $chainJob

Write-Host ""
Write-Host "Results: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
exit $(if ($failed -eq 0) { 0 } else { 1 })
