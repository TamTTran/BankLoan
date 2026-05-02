<#
.SYNOPSIS
Extracts DTOs/models and interfaces from LoanFlow service files into separate files.
Keeps namespace as LoanFlow.Application.Services for zero-breaking-change migration.
#>

$svcDir   = "d:\Project\BankLoan\src\LoanFlow.Application\Services"
$dtoBase  = "d:\Project\BankLoan\src\LoanFlow.Application\DTOs"
$ifaceDir = "d:\Project\BankLoan\src\LoanFlow.Application\Interfaces"

# Skip files that don't follow the pattern or were already processed
$skipFiles = @(
    "LoanHelpers.cs",
    "NapasCoreModels.cs",
    "NapasOptions.cs",
    # Batch 1 already done:
    "DocumentService.cs",
    "DunningService.cs",
    "LoanTopUpService.cs",
    "FinancialStatementService.cs",
    "LiquidityReportService.cs",
    "InterestRateReportService.cs",
    "ConcentrationLimitService.cs",
    "StressTestService.cs",
    "RiskModelService.cs",
    "CoreBankingIntegrationService.cs"
)

# Map service file -> DTO folder name
$dtoFolderMap = @{
    "AccountingPeriodService.cs" = "AccountingPeriod"
    "AccountingService.cs"       = "Accounting"
    "ApprovalRuleService.cs"     = "ApprovalRule"
    "BranchService.cs"           = "Branch"
    "CapitalAdequacyService.cs"  = "CapitalAdequacy"
    "CicService.cs"              = "Cic"
    "CollateralLifecycleService.cs" = "CollateralLifecycle"
    "CollectionService.cs"       = "Collection"
    "CreditCheckService.cs"      = "CreditCheck"
    "DataExportService.cs"       = "DataExport"
    "EContractService.cs"        = "EContract"
    "EkycService.cs"             = "Ekyc"
    "EndOfDayService.cs"         = "EndOfDay"
    "ExposureLimitService.cs"    = "ExposureLimit"
    "LoanLifecycleService.cs"    = "LoanLifecycle"
    "NotificationService.cs"     = "Notification"
    "NplReportingService.cs"     = "NplReporting"
    "OverdueJobService.cs"       = "OverdueJob"
    "ReconciliationService.cs"   = "Reconciliation"
    "RegulatoryReportingService.cs" = "RegulatoryReporting"
    "ReportingService.cs"        = "Reporting"
    "ReversalService.cs"         = "Reversal"
}

function Extract-ServiceFile {
    param(
        [string]$FilePath
    )
    
    $fileName = Split-Path $FilePath -Leaf
    $lines = Get-Content $FilePath
    
    # Parse file into regions: usings, namespace, types
    $usings = @()
    $dtoBlocks = @()       # DTO/model class blocks
    $interfaceBlocks = @() # Interface blocks  
    $implBlocks = @()      # Implementation class blocks
    $currentBlock = @()
    $blockType = "none"    # none, dto, interface, impl
    $braceDepth = 0
    $inBlock = $false
    
    foreach ($line in $lines) {
        $trimmed = $line.TrimStart()
        
        # Collect using statements
        if ($trimmed -match '^using ' -and -not $inBlock) {
            $usings += $line
            continue
        }
        
        # Skip namespace declaration and empty/comment lines outside blocks
        if ($trimmed -match '^namespace ' -and -not $inBlock) { continue }
        if ($trimmed -eq '' -and -not $inBlock) { continue }
        if ($trimmed -match '^//' -and -not $inBlock) { continue }
        
        # Detect block start
        if (-not $inBlock) {
            # DTO/model class (includes Request, Scenario, Provider classes)
            if ($trimmed -match '^public (class|record|sealed record) (\w+)(Dto|Request|Scenario|ResultDto|ReportDto|Item|Point|ItemDto)\b' -and $trimmed -notmatch 'Service\b') {
                $blockType = "dto"
                $inBlock = $true
                $braceDepth = 0
                $currentBlock = @($line)
                if ($trimmed -match '\{') { $braceDepth++ }
                continue
            }
            # Interface
            if ($trimmed -match '^public interface I\w+') {
                $blockType = "interface"
                $inBlock = $true
                $braceDepth = 0
                $currentBlock = @($line)
                if ($trimmed -match '\{') { $braceDepth++ }
                continue
            }
            # Implementation class or other (Mock providers, etc.)
            if ($trimmed -match '^public (class|sealed|abstract) ') {
                $blockType = "impl"
                $inBlock = $true
                $braceDepth = 0
                $currentBlock = @($line)
                if ($trimmed -match '\{') { $braceDepth++ }
                continue
            }
            
            # Anything else outside a block goes to impl
            continue
        }
        
        # Inside a block - track braces
        $currentBlock += $line
        
        foreach ($char in $line.ToCharArray()) {
            if ($char -eq '{') { $braceDepth++ }
            if ($char -eq '}') { $braceDepth-- }
        }
        
        # Block ended
        if ($braceDepth -le 0 -and $inBlock) {
            switch ($blockType) {
                "dto"       { $dtoBlocks += ,@($currentBlock) }
                "interface" { $interfaceBlocks += ,@($currentBlock) }
                "impl"      { $implBlocks += ,@($currentBlock) }
            }
            $currentBlock = @()
            $inBlock = $false
            $blockType = "none"
        }
    }
    
    return @{
        Usings     = $usings
        Dtos       = $dtoBlocks
        Interfaces = $interfaceBlocks
        Impls      = $implBlocks
    }
}

$processedCount = 0
$errorFiles = @()

Get-ChildItem $svcDir -Filter "*.cs" | ForEach-Object {
    $fileName = $_.Name
    if ($skipFiles -contains $fileName) { return }
    
    $parsed = Extract-ServiceFile -FilePath $_.FullName
    
    $hasDtos = $parsed.Dtos.Count -gt 0
    $hasInterfaces = $parsed.Interfaces.Count -gt 0
    
    if (-not $hasDtos -and -not $hasInterfaces) {
        Write-Host "SKIP: $fileName (no DTOs or interfaces to extract)" -ForegroundColor DarkGray
        return
    }
    
    Write-Host "PROCESSING: $fileName (DTOs: $($parsed.Dtos.Count), Interfaces: $($parsed.Interfaces.Count), Impls: $($parsed.Impls.Count))" -ForegroundColor Cyan
    
    # 1. Write DTO file (if DTOs exist)
    if ($hasDtos -and $dtoFolderMap.ContainsKey($fileName)) {
        $dtoFolder = Join-Path $dtoBase $dtoFolderMap[$fileName]
        if (-not (Test-Path $dtoFolder)) { New-Item -Path $dtoFolder -ItemType Directory -Force | Out-Null }
        
        $dtoFileName = $dtoFolderMap[$fileName] + "Dtos.cs"
        $dtoFilePath = Join-Path $dtoFolder $dtoFileName
        
        # Filter usings for DTOs - only need basic ones
        $dtoUsings = $parsed.Usings | Where-Object { $_ -notmatch 'Microsoft\.EntityFrameworkCore|Microsoft\.Extensions|QuestPDF|System\.Net\.Http|System\.Security|System\.Text\.Json' }
        
        $dtoContent = @()
        if ($dtoUsings.Count -gt 0) {
            $dtoContent += $dtoUsings
            $dtoContent += ""
        }
        $dtoContent += "namespace LoanFlow.Application.Services;"
        
        foreach ($block in $parsed.Dtos) {
            $dtoContent += ""
            $dtoContent += $block
        }
        $dtoContent += ""
        
        Set-Content -Path $dtoFilePath -Value ($dtoContent -join "`n") -NoNewline
        Write-Host "  -> Created: $dtoFilePath" -ForegroundColor Green
    }
    
    # 2. Write Interface file (if interfaces exist)
    if ($hasInterfaces) {
        if (-not (Test-Path $ifaceDir)) { New-Item -Path $ifaceDir -ItemType Directory -Force | Out-Null }
        
        # Determine interface file name from the first interface
        $firstIfaceLine = ($parsed.Interfaces[0] | Where-Object { $_ -match 'public interface' })[0]
        if ($firstIfaceLine -match 'public interface (I\w+)') {
            $ifaceName = $Matches[1]
        } else {
            $ifaceName = "I" + $fileName.Replace("Service.cs", "Service")
        }
        $ifaceFilePath = Join-Path $ifaceDir "$ifaceName.cs"
        
        # Filter usings for interfaces
        $ifaceUsings = $parsed.Usings | Where-Object { $_ -match 'LoanFlow\.Application\.Common|LoanFlow\.Application\.DTOs' }
        
        $ifaceContent = @()
        if ($ifaceUsings.Count -gt 0) {
            $ifaceContent += $ifaceUsings
            $ifaceContent += ""
        }
        $ifaceContent += "namespace LoanFlow.Application.Services;"
        
        foreach ($block in $parsed.Interfaces) {
            $ifaceContent += ""
            $ifaceContent += $block
        }
        $ifaceContent += ""
        
        Set-Content -Path $ifaceFilePath -Value ($ifaceContent -join "`n") -NoNewline
        Write-Host "  -> Created: $ifaceFilePath" -ForegroundColor Green
    }
    
    # 3. Rewrite original service file (only impls)
    if ($parsed.Impls.Count -gt 0) {
        $svcContent = @()
        $svcContent += $parsed.Usings
        $svcContent += ""
        $svcContent += "namespace LoanFlow.Application.Services;"
        
        foreach ($block in $parsed.Impls) {
            $svcContent += ""
            $svcContent += $block
        }
        $svcContent += ""
        
        Set-Content -Path $_.FullName -Value ($svcContent -join "`n") -NoNewline
        Write-Host "  -> Rewrote: $($_.FullName)" -ForegroundColor Yellow
    }
    
    $script:processedCount++
}

Write-Host "`n========================================" -ForegroundColor White
Write-Host "Processed: $processedCount files" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor White
