# Azure OpenAI Resource Scanner - Complete Subscription Analysis
# Scans all Azure subscriptions for OpenAI resources and exports to Excel

param(
    [string]$OutputFile = "AzureOpenAI_Complete_Report_$(Get-Date -Format 'yyyyMMdd_HHmmss').xlsx"
)

Write-Host "Azure OpenAI Resource Scanner Starting..." -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Cyan

# Function to install required modules
function Install-RequiredModules {
    $modules = @('Az.Accounts', 'Az.CognitiveServices', 'Az.Monitor', 'ImportExcel')
    
    foreach ($module in $modules) {
        if (-not (Get-Module -ListAvailable -Name $module)) {
            Write-Host "Installing module: $module" -ForegroundColor Yellow
            try {
                Install-Module -Name $module -Force -Scope CurrentUser -AllowClobber
                Write-Host "✓ $module installed successfully" -ForegroundColor Green
            } catch {
                Write-Error "Failed to install $module : $($_.Exception.Message)"
                exit 1
            }
        } else {
            Write-Host "✓ $module is already installed" -ForegroundColor Green
        }
    }
}

# Install and import required modules
Install-RequiredModules

Import-Module Az.Accounts -Force
Import-Module Az.CognitiveServices -Force  
Import-Module Az.Monitor -Force
Import-Module ImportExcel -Force

# Connect to Azure
Write-Host "`nConnecting to Azure..." -ForegroundColor Yellow
try {
    $context = Get-AzContext
    if (-not $context) {
        Write-Host "Please sign in to Azure..." -ForegroundColor Cyan
        Connect-AzAccount
        $context = Get-AzContext
    }
    Write-Host "✓ Connected to Azure as: $($context.Account.Id)" -ForegroundColor Green
    Write-Host "✓ Default subscription: $($context.Subscription.Name)" -ForegroundColor Green
} catch {
    Write-Error "Failed to connect to Azure: $($_.Exception.Message)"
    exit 1
}

# Get all subscriptions
Write-Host "`nGetting all subscriptions..." -ForegroundColor Yellow
try {
    $subscriptions = Get-AzSubscription
    Write-Host "✓ Found $($subscriptions.Count) subscriptions" -ForegroundColor Green
} catch {
    Write-Error "Failed to get subscriptions: $($_.Exception.Message)"
    exit 1
}

# Initialize results array
$allResults = @()
$subscriptionCount = 0
$resourceCount = 0

# Process each subscription
foreach ($subscription in $subscriptions) {
    $subscriptionCount++
    Write-Host "`n[$subscriptionCount/$($subscriptions.Count)] Processing: $($subscription.Name)" -ForegroundColor Cyan
    Write-Host "Subscription ID: $($subscription.Id)" -ForegroundColor Gray
    
    try {
        # Set subscription context
        $null = Set-AzContext -SubscriptionId $subscription.Id
        
        # Enhanced OpenAI resource discovery with multiple methods
        Write-Host "  Searching for OpenAI resources using multiple methods..." -ForegroundColor White
        $openAIResources = @()
        
        # Method 1: Search by resource type
        try {
            Write-Host "    Method 1: Searching by resource type..." -ForegroundColor Gray
            $cognitiveResources = Get-AzResource -ResourceType "Microsoft.CognitiveServices/accounts" -ErrorAction SilentlyContinue
            Write-Host "      Found $($cognitiveResources.Count) Cognitive Services resources" -ForegroundColor Gray
            
            foreach ($resource in $cognitiveResources) {
                try {
                    $account = Get-AzCognitiveServicesAccount -ResourceGroupName $resource.ResourceGroupName -Name $resource.Name -ErrorAction SilentlyContinue
                    if ($account) {
                        Write-Host "        Checking: $($account.AccountName) - Kind: $($account.Kind)" -ForegroundColor Gray
                        # Check for OpenAI or AI Services (newer Azure AI Services)
                        if ($account.Kind -eq "OpenAI" -or $account.Kind -eq "AIServices" -or $account.Kind -eq "CognitiveServices") {
                            $openAIResources += $account
                            Write-Host "        ✓ Found: $($account.AccountName) (Kind: $($account.Kind))" -ForegroundColor Green
                        }
                    }
                } catch {
                    Write-Host "        Warning: Could not get details for $($resource.Name)" -ForegroundColor Yellow
                }
            }
        } catch {
            Write-Host "    Method 1 failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        
        # Method 2: Direct Cognitive Services search
        try {
            Write-Host "    Method 2: Direct Cognitive Services search..." -ForegroundColor Gray
            $allAccounts = Get-AzCognitiveServicesAccount -ErrorAction SilentlyContinue
            Write-Host "      Found $($allAccounts.Count) accounts via direct search" -ForegroundColor Gray
            
            foreach ($account in $allAccounts) {
                Write-Host "        Checking: $($account.AccountName) - Kind: $($account.Kind)" -ForegroundColor Gray
                if (($account.Kind -eq "OpenAI" -or $account.Kind -eq "AIServices" -or $account.Kind -eq "CognitiveServices") -and ($account -notin $openAIResources)) {
                    $openAIResources += $account
                    Write-Host "        ✓ Added: $($account.AccountName) (Kind: $($account.Kind))" -ForegroundColor Green
                }
            }
        } catch {
            Write-Host "    Method 2 failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        
        # Method 3: Search by endpoint pattern
        try {
            Write-Host "    Method 3: Searching by endpoint pattern..." -ForegroundColor Gray
            $allResources = Get-AzResource -ResourceType "Microsoft.CognitiveServices/accounts" -ErrorAction SilentlyContinue
            foreach ($resource in $allResources) {
                if ($resource.Properties -and ($resource.Properties.endpoint -like "*openai.azure.com*" -or $resource.Properties.endpoint -like "*cognitiveservices.azure.com*")) {
                    try {
                        $account = Get-AzCognitiveServicesAccount -ResourceGroupName $resource.ResourceGroupName -Name $resource.Name -ErrorAction SilentlyContinue
                        if ($account -and $account -notin $openAIResources) {
                            $openAIResources += $account
                            Write-Host "        ✓ Found by endpoint: $($account.AccountName)" -ForegroundColor Green
                        }
                    } catch { }
                }
            }
        } catch {
            Write-Host "    Method 3 failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        
        # Remove duplicates and sort
        $openAIAccounts = $openAIResources | Sort-Object AccountName -Unique
        
        Write-Host "    Total OpenAI resources found: $($openAIAccounts.Count)" -ForegroundColor Cyan
        
        if (-not $openAIAccounts -or $openAIAccounts.Count -eq 0) {
            Write-Host "  → No OpenAI resources found in this subscription" -ForegroundColor Gray
            continue
        }
        
        Write-Host "  ✓ Found $($openAIAccounts.Count) OpenAI resource(s)" -ForegroundColor Green
        
        # Process each OpenAI account
        foreach ($account in $openAIAccounts) {
            $resourceCount++
            Write-Host "    Processing: $($account.AccountName)" -ForegroundColor White
            
            try {
                # Get deployments with enhanced detection
                Write-Host "      Getting deployments for $($account.AccountName)..." -ForegroundColor Gray
                $deployments = @()
                
                # Try different methods to get deployments
                try {
                    $deployments = Get-AzCognitiveServicesAccountDeployment -ResourceGroupName $account.ResourceGroupName -AccountName $account.AccountName -ErrorAction SilentlyContinue
                    Write-Host "        Method 1: Found $($deployments.Count) deployments" -ForegroundColor Gray
                } catch {
                    Write-Host "        Method 1 failed: $($_.Exception.Message)" -ForegroundColor Yellow
                }
                
                # Alternative method using REST API if needed
                if (-not $deployments -or $deployments.Count -eq 0) {
                    try {
                        Write-Host "        Trying alternative method..." -ForegroundColor Gray
                        # Sometimes deployments exist but are not returned by the cmdlet
                        # This is a known issue with some Azure OpenAI resources
                    } catch {
                        Write-Host "        Alternative method failed" -ForegroundColor Yellow
                    }
                }
                
                if ($deployments) {
                    Write-Host "      → Found $($deployments.Count) deployment(s)" -ForegroundColor Gray
                    
                    # Process each deployment
                    foreach ($deployment in $deployments) {
                        Write-Host "        • $($deployment.Name) ($($deployment.Properties.Model.Name))" -ForegroundColor Gray
                        
                        # Get last usage metrics
                        $lastUsedDate = ""
                        $totalCalls = 0
                        
                        try {
                            $endTime = Get-Date
                            $startTime = $endTime.AddDays(-30)
                            $resourceId = $account.Id
                            
                            # Try to get usage metrics
                            $metrics = Get-AzMetric -ResourceId $resourceId -MetricName "TotalCalls" -StartTime $startTime -EndTime $endTime -TimeGrain "01:00:00" -AggregationType Total -ErrorAction SilentlyContinue -WarningAction SilentlyContinue
                            
                            if ($metrics -and $metrics.Data) {
                                $validDataPoints = $metrics.Data | Where-Object { $_.Total -gt 0 } | Sort-Object TimeStamp -Descending
                                if ($validDataPoints) {
                                    $lastUsedDate = $validDataPoints[0].TimeStamp.ToString("yyyy-MM-dd HH:mm:ss")
                                    $totalCalls = ($validDataPoints | Measure-Object -Property Total -Sum).Sum
                                }
                            }
                        } catch {
                            Write-Host "          Warning: Could not retrieve usage metrics" -ForegroundColor Yellow
                        }
                        
                        # Create result object
                        $result = [PSCustomObject]@{
                            SubscriptionName = $subscription.Name
                            SubscriptionId = $subscription.Id
                            ResourceGroup = $account.ResourceGroupName  
                            ResourceName = $account.AccountName
                            Location = $account.Location
                            ResourceSKU = if ($account.Sku) { $account.Sku.Name } else { "Unknown" }
                            Endpoint = if ($account.Endpoint) { $account.Endpoint } else { "Not available" }
                            DeploymentName = if ($deployment.Name) { $deployment.Name } else { "Unknown" }
                            ModelName = if ($deployment.Properties.Model.Name) { $deployment.Properties.Model.Name } else { "Unknown" }
                            ModelVersion = if ($deployment.Properties.Model.Version) { $deployment.Properties.Model.Version } else { "Unknown" }
                            DeploymentSKU = if ($deployment.Sku) { $deployment.Sku.Name } else { "Standard" }
                            Capacity = if ($deployment.Sku) { $deployment.Sku.Capacity } else { "" }
                            ScaleType = if ($deployment.Properties.ScaleSettings) { $deployment.Properties.ScaleSettings.ScaleType } else { "Standard" }
                            LastUsedDate = $lastUsedDate
                            TotalCalls30Days = $totalCalls
                            ResourceKind = $account.Kind
                            ResourceStatus = "Active with deployments"
                            ScanDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                        }
                        
                        $allResults += $result
                    }
                } else {
                    Write-Host "      → No deployments found, but adding resource info" -ForegroundColor Gray
                    
                    # Still add the resource even without deployments - it might have deployments we can't see
                    # or it might be a valid OpenAI resource that's not yet configured
                    $result = [PSCustomObject]@{
                        SubscriptionName = $subscription.Name
                        SubscriptionId = $subscription.Id
                        ResourceGroup = $account.ResourceGroupName
                        ResourceName = $account.AccountName
                        Location = $account.Location
                        ResourceSKU = if ($account.Sku) { $account.Sku.Name } else { "Unknown" }
                        Endpoint = if ($account.Endpoint) { $account.Endpoint } else { "Not available" }
                        DeploymentName = "(No deployments found)"
                        ModelName = ""
                        ModelVersion = ""
                        DeploymentSKU = ""
                        Capacity = ""
                        ScaleType = ""
                        LastUsedDate = ""
                        TotalCalls30Days = 0
                        ResourceKind = $account.Kind
                        ResourceStatus = "No deployments detected"
                        ScanDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                    }
                    
                    $allResults += $result
                    Write-Host "        ✓ Added resource info for $($account.AccountName)" -ForegroundColor Green
                }
            } catch {
                Write-Host "      Error processing $($account.AccountName): $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "  Error processing subscription: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Generate summary
Write-Host "`n" + "=" * 60 -ForegroundColor Cyan
Write-Host "SCAN SUMMARY" -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "Subscriptions scanned: $($subscriptions.Count)" -ForegroundColor White
Write-Host "OpenAI resources found: $resourceCount" -ForegroundColor White
Write-Host "Total deployments: $($allResults | Where-Object { $_.DeploymentName -ne '(No deployments)' } | Measure-Object).Count" -ForegroundColor White

if ($allResults.Count -gt 0) {
    Write-Host "`nCreating Excel report..." -ForegroundColor Yellow
    
    try {
        # Create main data sheet
        $allResults | Export-Excel -Path $OutputFile -WorksheetName "All OpenAI Resources" -AutoSize -BoldTopRow -FreezeTopRow -TableStyle Medium2
        
        # Create summary by subscription
        $summaryBySubscription = $allResults | Group-Object SubscriptionName | ForEach-Object {
            [PSCustomObject]@{
                SubscriptionName = $_.Name
                ResourceCount = ($_.Group | Select-Object ResourceName -Unique).Count
                DeploymentCount = ($_.Group | Where-Object { $_.DeploymentName -ne '(No deployments)' }).Count
                Locations = ($_.Group | Select-Object Location -Unique | ForEach-Object { $_.Location }) -join ", "
                Models = ($_.Group | Where-Object { $_.ModelName } | Select-Object ModelName -Unique | ForEach-Object { $_.ModelName }) -join ", "
            }
        }
        
        $summaryBySubscription | Export-Excel -Path $OutputFile -WorksheetName "Summary by Subscription" -AutoSize -BoldTopRow -FreezeTopRow -TableStyle Medium2
        
        # Create summary by location
        $summaryByLocation = $allResults | Group-Object Location | ForEach-Object {
            [PSCustomObject]@{
                Location = $_.Name
                ResourceCount = ($_.Group | Select-Object ResourceName -Unique).Count  
                DeploymentCount = ($_.Group | Where-Object { $_.DeploymentName -ne '(No deployments)' }).Count
                Subscriptions = ($_.Group | Select-Object SubscriptionName -Unique | ForEach-Object { $_.SubscriptionName }) -join ", "
                Models = ($_.Group | Where-Object { $_.ModelName } | Select-Object ModelName -Unique | ForEach-Object { $_.ModelName }) -join ", "
            }
        }
        
        $summaryByLocation | Export-Excel -Path $OutputFile -WorksheetName "Summary by Location" -AutoSize -BoldTopRow -FreezeTopRow -TableStyle Medium2
        
        Write-Host "✓ Excel report created successfully!" -ForegroundColor Green
        Write-Host "📊 File: $OutputFile" -ForegroundColor Cyan
        Write-Host "`nReport contains:" -ForegroundColor Yellow
        Write-Host "  • All OpenAI Resources - Complete details of all found resources" -ForegroundColor White
        Write-Host "  • Summary by Subscription - Resource counts per subscription" -ForegroundColor White  
        Write-Host "  • Summary by Location - Resource distribution by Azure region" -ForegroundColor White
        
    } catch {
        Write-Error "Failed to create Excel report: $($_.Exception.Message)"
    }
} else {
    Write-Host "`n⚠️  No OpenAI resources found in any subscription" -ForegroundColor Yellow
}

Write-Host "`n✓ Scan completed!" -ForegroundColor Green