param (
    [string]$HostName = "127.0.0.1",
    [string]$Port = "3306",
    [string]$User = "loanflow",
    [string]$Password = $env:DB_PASSWORD,  # H7 fix: never hardcode — read from env
    [string]$Database = "LoanFlowLite",
    [string]$BackupDir = "D:\Project\BankLoan\backups"
)

# Ensure backup directory exists
if (-not (Test-Path -Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir | Out-Null
    Write-Host "Created backup directory at $BackupDir" -ForegroundColor Green
}

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupFile = Join-Path -Path $BackupDir -ChildPath "${Database}_backup_${Timestamp}.sql"

Write-Host "Starting backup for database '$Database' to '$BackupFile'..." -ForegroundColor Cyan

# Define mysqldump command
$mysqldumpPath = "mysqldump" # Assuming it's in PATH or Docker. For docker, you'd prepend `docker exec -i <container>`
$args = @(
    "-h", $HostName,
    "-P", $Port,
    "-u", $User,
    "--password=$Password",
    "--single-transaction",
    "--routines",
    "--triggers",
    $Database
)

try {
    # Execute mysqldump
    & $mysqldumpPath $args | Out-File -FilePath $BackupFile -Encoding UTF8
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Backup completed successfully: $BackupFile" -ForegroundColor Green
    } else {
        Write-Host "Backup failed with exit code $LASTEXITCODE." -ForegroundColor Red
        if (Test-Path $BackupFile) { Remove-Item $BackupFile }
    }
}
catch {
    Write-Host "Error executing backup: $_" -ForegroundColor Red
}
