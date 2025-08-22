# Get all NSG rules from all subscriptions and export to Excel files
# Requires Azure PowerShell module and ImportExcel module

# Check if required modules are available
if (-not (Get-Module -ListAvailable -Name Az)) {
    Write-Error "Azure PowerShell module (Az) is not installed. Please install it using: Install-Module -Name Az"
    exit 1
}

if (-not (Get-Module -ListAvailable -Name ImportExcel)) {
    Write-Error "ImportExcel module is not installed. Please install it using: Install-Module -Name ImportExcel"
    exit 1
}

# Connect to Azure (if not already connected)
try {
    $context = Get-AzContext
    if (-not $context) {
        Write-Host "Connecting to Azure..." -ForegroundColor Yellow
        Connect-AzAccount
    }
} catch {
    Write-Error "Failed to connect to Azure: $($_.Exception.Message)"
    exit 1
}

# Get all subscriptions
Write-Host "Getting all subscriptions..." -ForegroundColor Green
$subscriptions = Get-AzSubscription

$allNsgsByResourceGroup = @{}

foreach ($subscription in $subscriptions) {
    Write-Host "Processing subscription: $($subscription.Name) ($($subscription.Id))" -ForegroundColor Cyan
    
    # Set the subscription context
    try {
        Set-AzContext -SubscriptionId $subscription.Id | Out-Null
    } catch {
        Write-Warning "Failed to set context for subscription $($subscription.Name): $($_.Exception.Message)"
        continue
    }
    
    # Get all NSGs in the subscription
    try {
        $nsgs = Get-AzNetworkSecurityGroup
        Write-Host "Found $($nsgs.Count) NSGs in subscription $($subscription.Name)" -ForegroundColor Yellow
        
        foreach ($nsg in $nsgs) {
            Write-Host "  Processing NSG: $($nsg.Name)" -ForegroundColor White
            
            $nsgRules = @()
            
            # Get security rules (custom rules)
            foreach ($rule in $nsg.SecurityRules) {
                $ruleInfo = [PSCustomObject]@{
                    RuleName = $rule.Name
                    RuleType = "Custom"
                    Priority = $rule.Priority
                    Direction = $rule.Direction
                    Access = $rule.Access
                    Protocol = $rule.Protocol
                    SourcePortRange = if ($rule.SourcePortRange) { ($rule.SourcePortRange -join ", ") } else { "*" }
                    DestinationPortRange = if ($rule.DestinationPortRange) { ($rule.DestinationPortRange -join ", ") } else { "*" }
                    SourceAddressPrefix = if ($rule.SourceAddressPrefix) { ($rule.SourceAddressPrefix -join ", ") } else { "*" }
                    DestinationAddressPrefix = if ($rule.DestinationAddressPrefix) { ($rule.DestinationAddressPrefix -join ", ") } else { "*" }
                    Description = if ($rule.Description) { $rule.Description } else { "" }
                }
                $nsgRules += $ruleInfo
            }
            
            # Get default security rules
            foreach ($rule in $nsg.DefaultSecurityRules) {
                $ruleInfo = [PSCustomObject]@{
                    RuleName = $rule.Name
                    RuleType = "Default"
                    Priority = $rule.Priority
                    Direction = $rule.Direction
                    Access = $rule.Access
                    Protocol = $rule.Protocol
                    SourcePortRange = if ($rule.SourcePortRange) { ($rule.SourcePortRange -join ", ") } else { "*" }
                    DestinationPortRange = if ($rule.DestinationPortRange) { ($rule.DestinationPortRange -join ", ") } else { "*" }
                    SourceAddressPrefix = if ($rule.SourceAddressPrefix) { ($rule.SourceAddressPrefix -join ", ") } else { "*" }
                    DestinationAddressPrefix = if ($rule.DestinationAddressPrefix) { ($rule.DestinationAddressPrefix -join ", ") } else { "*" }
                    Description = if ($rule.Description) { $rule.Description } else { "" }
                }
                $nsgRules += $ruleInfo
            }
            
            # Sort by Direction (Inbound first) then by Priority
            $sortedRules = $nsgRules | Sort-Object @{Expression="Direction"; Descending=$false}, @{Expression="Priority"; Descending=$false}
            
            # Group by Resource Group
            $resourceGroupName = $nsg.ResourceGroupName
            if (-not $allNsgsByResourceGroup.ContainsKey($resourceGroupName)) {
                $allNsgsByResourceGroup[$resourceGroupName] = @{}
            }
            $allNsgsByResourceGroup[$resourceGroupName][$nsg.Name] = $sortedRules
        }
    } catch {
        Write-Warning "Failed to get NSGs from subscription $($subscription.Name): $($_.Exception.Message)"
    }
}

# Create Excel files by Resource Group
Write-Host "`nCreating Excel files by Resource Group..." -ForegroundColor Green

$totalRulesCount = 0
$filesCreated = 0

foreach ($resourceGroupName in $allNsgsByResourceGroup.Keys) {
    $excelPath = "NSG_Rules_$($resourceGroupName)_$(Get-Date -Format 'yyyyMMdd_HHmmss').xlsx"
    
    Write-Host "Creating Excel file: $excelPath" -ForegroundColor Yellow
    
    # Remove existing file if it exists
    if (Test-Path $excelPath) {
        Remove-Item $excelPath -Force
    }
    
    foreach ($nsgName in $allNsgsByResourceGroup[$resourceGroupName].Keys) {
        $nsgRules = $allNsgsByResourceGroup[$resourceGroupName][$nsgName]
        
        if ($nsgRules.Count -gt 0) {
            Write-Host "  Adding worksheet: $nsgName ($($nsgRules.Count) rules)" -ForegroundColor White
            
            # Export to Excel with NSG name as worksheet
            $nsgRules | Export-Excel -Path $excelPath -WorksheetName $nsgName -AutoSize -AutoFilter -FreezeTopRow
            
            $totalRulesCount += $nsgRules.Count
        }
    }
    
    $filesCreated++
    Write-Host "Excel file created: $excelPath" -ForegroundColor Green
}

# Display summary
Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "Total Excel files created: $filesCreated" -ForegroundColor Green
Write-Host "Total NSG rules processed: $totalRulesCount" -ForegroundColor Green
Write-Host "Files are sorted by: Direction (Inbound → Outbound) → Priority (Low → High)" -ForegroundColor Yellow