# extract_dtos_interfaces.ps1
# Automated DTO and Interface extraction from service files
# Strategy: Parse each .cs file, extract DTO classes and interfaces to separate files
# Namespace: LoanFlow.Application.Services (kept same to avoid breaking changes)

$svcDir = "d:\Project\BankLoan\src\LoanFlow.Application\Services"
$dtoBaseDir = "d:\Project\BankLoan\src\LoanFlow.Application\DTOs"
$ifaceDir = "d:\Project\BankLoan\src\LoanFlow.Application\Interfaces"

# Map: ServiceFile -> (DtoFolder, DtoTypes[], InterfaceTypes[])
# We will parse each file and extract based on pattern matching

$files = Get-ChildItem $svcDir -Filter "*.cs" | Where-Object { $_.Name -ne "LoanHelpers.cs" -and $_.Name -ne "NapasCoreModels.cs" -and $_.Name -ne "NapasOptions.cs" -and $_.Name -ne "INapasCoreService.cs" }

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $lines = Get-Content $file.FullName
    
    # Check if file has DTOs or interfaces to extract
    $hasDtos = $content -match '(?m)^public (class|record) \w+(Dto|Request|Scenario|Item)\b'
    $hasInterface = $content -match '(?m)^public interface I\w+'
    
    if (-not $hasDtos -and -not $hasInterface) {
        Write-Host "SKIP: $($file.Name) - no DTOs or interfaces found"
        continue
    }

    Write-Host "PROCESSING: $($file.Name)"
    Write-Host "  Has DTOs: $hasDtos"
    Write-Host "  Has Interface: $hasInterface"
}

Write-Host "`nDone scanning. Files that need processing are listed above."
