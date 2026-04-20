$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "src\LoanFlow.API\LoanFlow.API.csproj"
$dotnetHome = Join-Path $repoRoot ".dotnet"

$env:DOTNET_CLI_HOME = $dotnetHome
$env:DB_MIGRATE_ON_STARTUP = "true"

Write-Host "Applying database migrations using LoanFlow.API --migrate-only..."
dotnet run --project $apiProject -- --migrate-only
