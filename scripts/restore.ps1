param (
    [string]$HostName = "127.0.0.1",
    [string]$Port = "3306",
    [string]$User = "loanflow",
    [string]$Password = "LoanFlow@2026!",
    [string]$Database = "LoanFlowLite",
    [Parameter(Mandatory=$true)]
    [string]$BackupFile
)

if (-not (Test-Path -Path $BackupFile)) {
    Write-Host "Backup file does not exist: $BackupFile" -ForegroundColor Red
    exit 1
}

Write-Host "WARNING: You are about to restore database '$Database' from file '$BackupFile'." -ForegroundColor Yellow
$confirmation = Read-Host "Are you sure you want to proceed? This will overwrite existing data! (y/N)"

if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
    Write-Host "Restore cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host "Starting restore process..." -ForegroundColor Cyan

$mysqlPath = "mysql"
# Need to use cmd /c because piping file into mysql is easier in un-managed shell execution, or get-content
try {
    # Using Get-Content and piping to mysql process
    $args = @(
        "-h", $HostName,
        "-P", $Port,
        "-u", $User,
        "--password=$Password",
        $Database
    )
    
    Get-Content $BackupFile -Encoding UTF8 | & $mysqlPath $args

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Restore completed successfully." -ForegroundColor Green
    } else {
        Write-Host "Restore failed with exit code $LASTEXITCODE." -ForegroundColor Red
    }
}
catch {
    Write-Host "Error executing restore: $_" -ForegroundColor Red
}
