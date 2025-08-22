# Get all NSG rules from all subscriptions
# Requires Azure PowerShell module and proper authentication

# Check if Azure PowerShell module is available
if (-not (Get-Module -ListAvailable -Name Az)) {
    Write-Error "Azure PowerShell module (Az) is not installed. Please install it using: Install-Module -Name Az"
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

$allNsgRules = @()

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
            
            # Get security rules (custom rules)
            foreach ($rule in $nsg.SecurityRules) {
                $ruleInfo = [PSCustomObject]@{
                    SubscriptionName = $subscription.Name
                    SubscriptionId = $subscription.Id
                    ResourceGroupName = $nsg.ResourceGroupName
                    NSGName = $nsg.Name
                    RuleName = $rule.Name
                    RuleType = "Custom"
                    Priority = $rule.Priority
                    Direction = $rule.Direction
                    Access = $rule.Access
                    Protocol = $rule.Protocol
                    SourcePortRange = ($rule.SourcePortRange -join ", ")
                    DestinationPortRange = ($rule.DestinationPortRange -join ", ")
                    SourceAddressPrefix = ($rule.SourceAddressPrefix -join ", ")
                    DestinationAddressPrefix = ($rule.DestinationAddressPrefix -join ", ")
                    Description = $rule.Description
                }
                $allNsgRules += $ruleInfo
            }
            
            # Get default security rules
            foreach ($rule in $nsg.DefaultSecurityRules) {
                $ruleInfo = [PSCustomObject]@{
                    SubscriptionName = $subscription.Name
                    SubscriptionId = $subscription.Id
                    ResourceGroupName = $nsg.ResourceGroupName
                    NSGName = $nsg.Name
                    RuleName = $rule.Name
                    RuleType = "Default"
                    Priority = $rule.Priority
                    Direction = $rule.Direction
                    Access = $rule.Access
                    Protocol = $rule.Protocol
                    SourcePortRange = ($rule.SourcePortRange -join ", ")
                    DestinationPortRange = ($rule.DestinationPortRange -join ", ")
                    SourceAddressPrefix = ($rule.SourceAddressPrefix -join ", ")
                    DestinationAddressPrefix = ($rule.DestinationAddressPrefix -join ", ")
                    Description = $rule.Description
                }
                $allNsgRules += $ruleInfo
            }
        }
    } catch {
        Write-Warning "Failed to get NSGs from subscription $($subscription.Name): $($_.Exception.Message)"
    }
}

# Display results
Write-Host "`nTotal NSG rules found: $($allNsgRules.Count)" -ForegroundColor Green

# Export to CSV
$csvPath = "NSG_Rules_Report_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
$allNsgRules | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
Write-Host "Results exported to: $csvPath" -ForegroundColor Green

# Display summary by subscription
Write-Host "`nSummary by subscription:" -ForegroundColor Yellow
$allNsgRules | Group-Object SubscriptionName | Select-Object Name, Count | Format-Table -AutoSize

# Display the first 10 rules as sample
if ($allNsgRules.Count -gt 0) {
    Write-Host "`nSample of first 10 rules:" -ForegroundColor Yellow
    $allNsgRules | Select-Object -First 10 | Format-Table -AutoSize
}