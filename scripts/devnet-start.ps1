# Start the persistent devnet
# Usage: .\scripts\devnet-start.ps1
#
# The devnet persists state between sessions.
# Use checkpoints to save/restore specific states.
# RPC available at: http://localhost:40332

Write-Host "Starting HushNetwork DevNet (persistent)..." -ForegroundColor Cyan
Write-Host "RPC: http://localhost:40332" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

neoxp run --input devnet\devnet.neo-express --seconds-per-block 15
