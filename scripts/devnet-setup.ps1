# First-time devnet setup — run once after cloning
# Usage: .\scripts\devnet-setup.ps1
#
# Creates the deployer wallet and saves the genesis checkpoint.
# Requires the devnet to be running (devnet-start.ps1) in another terminal.

Write-Host "Setting up HushNetwork DevNet..." -ForegroundColor Cyan

# Create deployer wallet
neoxp wallet create deployer --input devnet\devnet.neo-express

# Run setup batch
neoxp batch devnet\setup.batch --input devnet\devnet.neo-express

Write-Host ""
Write-Host "DevNet ready." -ForegroundColor Green
Write-Host "Checkpoint saved: devnet\checkpoints\00-genesis" -ForegroundColor Green
