# Get all Azure Firewall Policies from all subscriptions and export to Excel files
# Requires Azure PowerShell module and ImportExcel module

# Check if required modules are available
if (-not (Get-Module -ListAvailable -Name Az)) {
    Write-Error "Azure PowerShell module (Az) is not installed. Please install it using: Install-Module -Name Az"
    exit 1
}

if (-not (Get-Module -ListAvailable -Name ImportExcel)) {
    Write-Host "ImportExcel module is not installed. Installing..." -ForegroundColor Yellow
    try {
        Install-Module -Name ImportExcel -Force -Scope CurrentUser -AllowClobber
        Write-Host "ImportExcel module installed successfully." -ForegroundColor Green
    } catch {
        Write-Error "Failed to install ImportExcel module: $($_.Exception.Message)"
        Write-Host "Please install manually using: Install-Module -Name ImportExcel -Force" -ForegroundColor Red
        exit 1
    }
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

$allFirewallPolicies = @()

foreach ($subscription in $subscriptions) {
    Write-Host "Processing subscription: $($subscription.Name) ($($subscription.Id))" -ForegroundColor Cyan
    
    # Set the subscription context
    try {
        Set-AzContext -SubscriptionId $subscription.Id | Out-Null
    } catch {
        Write-Warning "Failed to set context for subscription $($subscription.Name): $($_.Exception.Message)"
        continue
    }
    
    # Get all Firewall Policies in the subscription using Get-AzResource
    try {
        # Use Get-AzResource to find all firewall policies
        $firewallPolicyResources = Get-AzResource -ResourceType "Microsoft.Network/firewallPolicies"
        $firewallPolicies = @()
        
        foreach ($policyResource in $firewallPolicyResources) {
            try {
                $policy = Get-AzFirewallPolicy -Name $policyResource.Name -ResourceGroupName $policyResource.ResourceGroupName
                $firewallPolicies += $policy
            } catch {
                Write-Warning "Failed to get details for policy $($policyResource.Name): $($_.Exception.Message)"
            }
        }
        
        Write-Host "Found $($firewallPolicies.Count) Firewall Policies in subscription $($subscription.Name)" -ForegroundColor Yellow
        
        foreach ($policy in $firewallPolicies) {
            Write-Host "  Processing Firewall Policy: $($policy.Name)" -ForegroundColor White
            
            # Get detailed policy information
            $policyDetails = Get-AzFirewallPolicy -Name $policy.Name -ResourceGroupName $policy.ResourceGroupName
            
            $policyInfo = [PSCustomObject]@{
                SubscriptionName = $subscription.Name
                SubscriptionId = $subscription.Id
                ResourceGroupName = $policy.ResourceGroupName
                PolicyName = $policy.Name
                Location = $policy.Location
                ThreatIntelMode = $policyDetails.ThreatIntelMode
                ThreatIntelWhitelist = if ($policyDetails.ThreatIntelWhitelist) { ($policyDetails.ThreatIntelWhitelist.IpAddresses -join ", ") } else { "" }
                Policy = $policyDetails
            }
            
            $allFirewallPolicies += $policyInfo
        }
    } catch {
        Write-Warning "Failed to get Firewall Policies from subscription $($subscription.Name): $($_.Exception.Message)"
    }
}

# Process each firewall policy and create Excel files
Write-Host "`nProcessing Firewall Policies and creating Excel files..." -ForegroundColor Green

$totalPoliciesProcessed = 0

foreach ($policyInfo in $allFirewallPolicies) {
    $policy = $policyInfo.Policy
    $excelPath = "FirewallPolicy_$($policy.Name)_$(Get-Date -Format 'yyyyMMdd_HHmmss').xlsx"
    
    Write-Host "Creating Excel file: $excelPath for policy: $($policy.Name)" -ForegroundColor Yellow
    
    # Remove existing file if it exists
    if (Test-Path $excelPath) {
        Remove-Item $excelPath -Force
    }
    
    # Initialize collections for different rule types
    $ruleCollections = @()
    $dnatRules = @()
    $networkRules = @()
    $applicationRules = @()
    
    # Process Rule Collection Groups
    if ($policy.RuleCollectionGroups) {
        foreach ($ruleCollectionGroupRef in $policy.RuleCollectionGroups) {
            try {
                # Extract resource group and name from the reference ID
                $rgName = $policyInfo.ResourceGroupName
                $ruleCollectionGroupName = ($ruleCollectionGroupRef.Id -split '/')[-1]
                
                Write-Host "    Processing Rule Collection Group: $ruleCollectionGroupName" -ForegroundColor White
                
                # Get the rule collection group details
                $ruleCollectionGroup = Get-AzFirewallPolicyRuleCollectionGroup -Name $ruleCollectionGroupName -ResourceGroupName $rgName -FirewallPolicyName $policy.Name
                
                # Process each rule collection in the group
                foreach ($ruleCollection in $ruleCollectionGroup.Properties.RuleCollection) {
                    # Add rule collection info
                    $collectionInfo = [PSCustomObject]@{
                        RuleCollectionGroupName = $ruleCollectionGroupName
                        RuleCollectionName = $ruleCollection.Name
                        RuleCollectionType = $ruleCollection.RuleCollectionType
                        Priority = if ($ruleCollection.Priority) { $ruleCollection.Priority } else { "" }
                        Action = if ($ruleCollection.Action) { $ruleCollection.Action.Type } else { "" }
                        RuleCount = if ($ruleCollection.Rules) { $ruleCollection.Rules.Count } else { 0 }
                    }
                    $ruleCollections += $collectionInfo
                    
                    # Process rules based on collection type
                    switch ($ruleCollection.RuleCollectionType) {
                        "FirewallPolicyNatRuleCollection" {
                            foreach ($rule in $ruleCollection.Rules) {
                                $dnatRule = [PSCustomObject]@{
                                    RuleCollectionGroup = $ruleCollectionGroupName
                                    RuleCollection = $ruleCollection.Name
                                    RuleName = $rule.Name
                                    RuleType = $rule.RuleType
                                    IpProtocols = if ($rule.IpProtocols) { ($rule.IpProtocols -join ", ") } else { "" }
                                    SourceAddresses = if ($rule.SourceAddresses) { ($rule.SourceAddresses -join ", ") } else { "" }
                                    SourceIpGroups = if ($rule.SourceIpGroups) { ($rule.SourceIpGroups -join ", ") } else { "" }
                                    DestinationAddresses = if ($rule.DestinationAddresses) { ($rule.DestinationAddresses -join ", ") } else { "" }
                                    DestinationPorts = if ($rule.DestinationPorts) { ($rule.DestinationPorts -join ", ") } else { "" }
                                    TranslatedAddress = if ($rule.TranslatedAddress) { $rule.TranslatedAddress } else { "" }
                                    TranslatedPort = if ($rule.TranslatedPort) { $rule.TranslatedPort } else { "" }
                                    TranslatedFqdn = if ($rule.TranslatedFqdn) { $rule.TranslatedFqdn } else { "" }
                                }
                                $dnatRules += $dnatRule
                            }
                        }
                        "FirewallPolicyFilterRuleCollection" {
                            foreach ($rule in $ruleCollection.Rules) {
                                if ($rule.RuleType -eq "NetworkRule") {
                                    $networkRule = [PSCustomObject]@{
                                        RuleCollectionGroup = $ruleCollectionGroupName
                                        RuleCollection = $ruleCollection.Name
                                        RuleName = $rule.Name
                                        RuleType = $rule.RuleType
                                        IpProtocols = if ($rule.IpProtocols) { ($rule.IpProtocols -join ", ") } else { "" }
                                        SourceAddresses = if ($rule.SourceAddresses) { ($rule.SourceAddresses -join ", ") } else { "" }
                                        SourceIpGroups = if ($rule.SourceIpGroups) { ($rule.SourceIpGroups -join ", ") } else { "" }
                                        DestinationAddresses = if ($rule.DestinationAddresses) { ($rule.DestinationAddresses -join ", ") } else { "" }
                                        DestinationIpGroups = if ($rule.DestinationIpGroups) { ($rule.DestinationIpGroups -join ", ") } else { "" }
                                        DestinationFqdns = if ($rule.DestinationFqdns) { ($rule.DestinationFqdns -join ", ") } else { "" }
                                        DestinationPorts = if ($rule.DestinationPorts) { ($rule.DestinationPorts -join ", ") } else { "" }
                                    }
                                    $networkRules += $networkRule
                                } elseif ($rule.RuleType -eq "ApplicationRule") {
                                    $applicationRule = [PSCustomObject]@{
                                        RuleCollectionGroup = $ruleCollectionGroupName
                                        RuleCollection = $ruleCollection.Name
                                        RuleName = $rule.Name
                                        RuleType = $rule.RuleType
                                        SourceAddresses = if ($rule.SourceAddresses) { ($rule.SourceAddresses -join ", ") } else { "" }
                                        SourceIpGroups = if ($rule.SourceIpGroups) { ($rule.SourceIpGroups -join ", ") } else { "" }
                                        TargetFqdns = if ($rule.TargetFqdns) { ($rule.TargetFqdns -join ", ") } else { "" }
                                        TargetUrls = if ($rule.TargetUrls) { ($rule.TargetUrls -join ", ") } else { "" }
                                        FqdnTags = if ($rule.FqdnTags) { ($rule.FqdnTags -join ", ") } else { "" }
                                        Protocols = if ($rule.Protocols) { 
                                            ($rule.Protocols | ForEach-Object { "$($_.ProtocolType):$($_.Port)" }) -join ", " 
                                        } else { "" }
                                        WebCategories = if ($rule.WebCategories) { ($rule.WebCategories -join ", ") } else { "" }
                                    }
                                    $applicationRules += $applicationRule
                                }
                            }
                        }
                    }
                }
            } catch {
                Write-Warning "Failed to process rule collection group $ruleCollectionGroupName in policy $($policy.Name): $($_.Exception.Message)"
            }
        }
    }
    
    # Export to Excel with multiple sheets
    if ($ruleCollections.Count -gt 0) {
        Write-Host "    Adding Rule Collections sheet ($($ruleCollections.Count) collections)" -ForegroundColor White
        $ruleCollections | Export-Excel -Path $excelPath -WorksheetName "Rule Collections" -AutoSize -AutoFilter -FreezeTopRow
    }
    
    if ($dnatRules.Count -gt 0) {
        Write-Host "    Adding DNAT Rules sheet ($($dnatRules.Count) rules)" -ForegroundColor White
        $dnatRules | Export-Excel -Path $excelPath -WorksheetName "DNAT Rules" -AutoSize -AutoFilter -FreezeTopRow
    }
    
    if ($networkRules.Count -gt 0) {
        Write-Host "    Adding Network Rules sheet ($($networkRules.Count) rules)" -ForegroundColor White
        $networkRules | Export-Excel -Path $excelPath -WorksheetName "Network Rules" -AutoSize -AutoFilter -FreezeTopRow
    }
    
    if ($applicationRules.Count -gt 0) {
        Write-Host "    Adding Application Rules sheet ($($applicationRules.Count) rules)" -ForegroundColor White
        $applicationRules | Export-Excel -Path $excelPath -WorksheetName "Application Rules" -AutoSize -AutoFilter -FreezeTopRow
    }
    
    # If no rules found, create an info sheet
    if ($ruleCollections.Count -eq 0 -and $dnatRules.Count -eq 0 -and $networkRules.Count -eq 0 -and $applicationRules.Count -eq 0) {
        $noRulesInfo = [PSCustomObject]@{
            Message = "No rules found in this firewall policy"
            PolicyName = $policy.Name
            ResourceGroup = $policyInfo.ResourceGroupName
            Subscription = $policyInfo.SubscriptionName
        }
        $noRulesInfo | Export-Excel -Path $excelPath -WorksheetName "Policy Info" -AutoSize -AutoFilter -FreezeTopRow
    }
    
    $totalPoliciesProcessed++
    Write-Host "Excel file created: $excelPath" -ForegroundColor Green
}

# Display summary
Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "Total subscriptions processed: $($subscriptions.Count)" -ForegroundColor Green
Write-Host "Total Firewall Policies found: $($allFirewallPolicies.Count)" -ForegroundColor Green
Write-Host "Total Excel files created: $totalPoliciesProcessed" -ForegroundColor Green
Write-Host "Each Excel file contains separate sheets for:" -ForegroundColor Yellow
Write-Host "  - Rule Collections (parent collections)" -ForegroundColor Yellow
Write-Host "  - DNAT Rules (child rules)" -ForegroundColor Yellow
Write-Host "  - Network Rules (child rules)" -ForegroundColor Yellow
Write-Host "  - Application Rules (child rules)" -ForegroundColor Yellow