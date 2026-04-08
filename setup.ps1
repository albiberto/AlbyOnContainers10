# ============================================================
# Product Data Manager - NuGet Restore Script
# Run this from: c:\Users\viezzi\RiderProjects\albyoncontainers
# ============================================================
#
# All project files (.csproj), source code, and configuration
# files have been pre-created by Antigravity.
# This script only needs to restore NuGet packages and build.
# ============================================================

$ErrorActionPreference = "Stop"

Write-Host "=== Restoring NuGet Packages ===" -ForegroundColor Cyan
dotnet restore AlbyonContainers.sln

Write-Host ""
Write-Host "=== Building Solution ===" -ForegroundColor Cyan
dotnet build AlbyonContainers.sln --no-restore

Write-Host ""
Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host ""
Write-Host "To run the application with Aspire:" -ForegroundColor Yellow
Write-Host "  dotnet run --project src/AlbyonContainers.AppHost" -ForegroundColor White
Write-Host ""
Write-Host "Make sure Podman is running before starting." -ForegroundColor Yellow
