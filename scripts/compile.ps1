# Compile all contracts
# Usage: .\scripts\compile.ps1
# Usage: .\scripts\compile.ps1 -Contract TokenTemplate

param(
    [string]$Contract = "all"
)

$contracts = @("TokenTemplate", "TokenFactory")

if ($Contract -ne "all") {
    $contracts = @($Contract)
}

foreach ($c in $contracts) {
    $path = "src\$c"
    if (Test-Path $path) {
        Write-Host "Compiling $c..." -ForegroundColor Cyan
        Push-Location $path
        dotnet build
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  OK: bin\sc\$c.nef" -ForegroundColor Green
        } else {
            Write-Host "  FAILED" -ForegroundColor Red
        }
        Pop-Location
    } else {
        Write-Host "Contract $c not found at $path" -ForegroundColor Yellow
    }
}
