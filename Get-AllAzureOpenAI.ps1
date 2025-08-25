# Get all Azure OpenAI resources from all subscriptions with deployment last used dates and export to Excel
# Requires Azure PowerShell module and ImportExcel module

param(
    [string]$OutputFile = "AzureOpenAI_Report_$(Get-Date -Format 'yyyyMMdd_HHmmss').xlsx",
    [int]$MetricsDays = 30
)

# Check if required modules are available
if (-not (Get-Module -ListAvailable -Name Az)) {
    Write-Host "Azure PowerShell module (Az) is not installed. Installing..." -ForegroundColor Yellow
    try {
        Install-Module -Name Az -Force -Scope CurrentUser -AllowClobber
        Write-Host "Azure PowerShell module installed successfully." -ForegroundColor Green
    } catch {
        Write-Error "Failed to install Azure PowerShell module: $($_.Exception.Message)"
        Write-Host "Please install manually using: Install-Module -Name Az -Force" -ForegroundColor Red
        exit 1
    }
}

if (-not (Get-Module -ListAvailable -Name ImportExcel)) {
    Write-Host "ImportExcel module is not installed. Installing..." -ForegroundColor Yellow
    try {
        Install-Module -Name ImportExcel -Force -Scope CurrentUser
        Write-Host "ImportExcel module installed successfully." -ForegroundColor Green
    } catch {
        Write-Error "Failed to install ImportExcel module: $($_.Exception.Message)"
        Write-Host "Please install manually using: Install-Module -Name ImportExcel -Force" -ForegroundColor Red
        exit 1
    }
}

# Import required modules
Import-Module Az
Import-Module ImportExcel

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

# Function to get PTU metrics for a deployment
function Get-PTUMetrics {
    param(
        [string]$SubscriptionId,
        [string]$ResourceGroupName,
        [string]$AccountName,
        [string]$DeploymentName,
        [int]$Days = 30
    )
    
    try {
        # Set subscription context
        Set-AzContext -SubscriptionId $SubscriptionId | Out-Null
        
        # Calculate time range
        $endTime = Get-Date
        $startTime = $endTime.AddDays(-$Days)
        
        # Get resource ID for the specific deployment
        $resourceId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.CognitiveServices/accounts/$AccountName"
        
        $ptuAvg7Days = 0
        $ptuMax7Days = 0
        $lastUsedDate = ""
        
        # Try to get PTU utilization metrics
        try {
            $ptuMetrics = Get-AzMetric -ResourceId $resourceId -MetricName "ProvisionedThroughputUtilization" -StartTime $startTime -EndTime $endTime -TimeGrain "01:00:00" -AggregationType Average -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
            
            if ($ptuMetrics -and $ptuMetrics.Data) {
                $validDataPoints = $ptuMetrics.Data | Where-Object { $_.Average -ne $null -and $_.Average -gt 0 }
                if ($validDataPoints) {
                    $ptuAvg7Days = ($validDataPoints | Measure-Object -Property Average -Average).Average
                    $ptuMax7Days = ($validDataPoints | Measure-Object -Property Average -Maximum).Maximum
                    $lastUsedDate = ($validDataPoints | Sort-Object TimeStamp -Descending)[0].TimeStamp.ToString("yyyy-MM-dd HH:mm:ss")
                }
            }
        } catch {
            Write-Verbose "PTU metrics not available for $DeploymentName"
        }
        
        # If no PTU metrics, try to get general usage metrics
        if ($lastUsedDate -eq "") {
            try {
                $callMetrics = Get-AzMetric -ResourceId $resourceId -MetricName "TotalCalls" -StartTime $startTime -EndTime $endTime -TimeGrain "01:00:00" -AggregationType Total -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
                
                if ($callMetrics -and $callMetrics.Data) {
                    $nonZeroDataPoints = $callMetrics.Data | Where-Object { $_.Total -gt 0 } | Sort-Object TimeStamp -Descending
                    if ($nonZeroDataPoints) {
                        $lastUsedDate = $nonZeroDataPoints[0].TimeStamp.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                }
            } catch {
                Write-Verbose "Call metrics not available for $DeploymentName"
            }
        }
        
        return @{
            PTUAvg7Days = [math]::Round($ptuAvg7Days, 2)
            PTUMax7Days = [math]::Round($ptuMax7Days, 2)
            LastUsedDate = $lastUsedDate
        }
    }
    catch {
        Write-Warning "Failed to get metrics for deployment $DeploymentName in $AccountName : $($_.Exception.Message)"
        return @{
            PTUAvg7Days = 0
            PTUMax7Days = 0
            LastUsedDate = ""
        }
    }
}

# Get all subscriptions
Write-Host "Getting all subscriptions..." -ForegroundColor Green
$subscriptions = Get-AzSubscription

$allOpenAIResources = @()
$resourceGroups = @{}

# Process each subscription
foreach ($subscription in $subscriptions) {
    Write-Host "Processing subscription: $($subscription.Name) ($($subscription.Id))" -ForegroundColor Cyan
    
    try {
        # Set context to current subscription
        Set-AzContext -SubscriptionId $subscription.Id | Out-Null
        
        # Get all Cognitive Services accounts
        $cognitiveAccounts = Get-AzCognitiveServicesAccount
        
        # Filter for OpenAI accounts
        $openAIAccounts = $cognitiveAccounts | Where-Object { $_.Kind -eq "OpenAI" }
        
        foreach ($account in $openAIAccounts) {
            Write-Host "  Found OpenAI resource: $($account.AccountName) in $($account.Location)" -ForegroundColor Yellow
            
            # Get deployments for this account
            try {
                $deployments = Get-AzCognitiveServicesAccountDeployment -ResourceGroupName $account.ResourceGroupName -AccountName $account.AccountName
                
                if ($deployments) {
                    foreach ($deployment in $deployments) {
                        Write-Host "    Processing deployment: $($deployment.Name)" -ForegroundColor Gray
                        
                        # Get PTU metrics for this deployment
                        $ptuMetrics = Get-PTUMetrics -SubscriptionId $subscription.Id -ResourceGroupName $account.ResourceGroupName -AccountName $account.AccountName -DeploymentName $deployment.Name -Days $MetricsDays
                        
                        $resourceInfo = [PSCustomObject]@{
                            SubscriptionId = $subscription.Id
                            SubscriptionName = $subscription.Name
                            ResourceGroup = $account.ResourceGroupName
                            ResourceName = $account.AccountName
                            Location = $account.Location
                            ResourceSKU = $account.Sku.Name
                            Endpoint = $account.Endpoint
                            DeploymentName = $deployment.Name
                            ModelName = $deployment.Properties.Model.Name
                            ModelVersion = $deployment.Properties.Model.Version
                            DeploymentSKU = $deployment.Sku.Name
                            SKUCapacity = $deployment.Sku.Capacity
                            ScaleType = $deployment.Properties.ScaleSettings.ScaleType
                            PTUAvg7Days = $ptuMetrics.PTUAvg7Days
                            PTUMax7Days = $ptuMetrics.PTUMax7Days
                            LastUsedDate = $ptuMetrics.LastUsedDate
                            ScanDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                        }
                        
                        $allOpenAIResources += $resourceInfo
                        
                        # Track resource groups
                        if (-not $resourceGroups.ContainsKey($account.ResourceGroupName)) {
                            $resourceGroups[$account.ResourceGroupName] = @()
                        }
                        $resourceGroups[$account.ResourceGroupName] += $resourceInfo
                    }
                } else {
                    # Resource without deployments
                    $resourceInfo = [PSCustomObject]@{
                        SubscriptionId = $subscription.Id
                        SubscriptionName = $subscription.Name
                        ResourceGroup = $account.ResourceGroupName
                        ResourceName = $account.AccountName
                        Location = $account.Location
                        ResourceSKU = $account.Sku.Name
                        Endpoint = $account.Endpoint
                        DeploymentName = ""
                        ModelName = ""
                        ModelVersion = ""
                        DeploymentSKU = ""
                        SKUCapacity = ""
                        ScaleType = ""
                        PTUAvg7Days = 0
                        PTUMax7Days = 0
                        LastUsedDate = ""
                        ScanDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                    }
                    
                    $allOpenAIResources += $resourceInfo
                    
                    # Track resource groups
                    if (-not $resourceGroups.ContainsKey($account.ResourceGroupName)) {
                        $resourceGroups[$account.ResourceGroupName] = @()
                    }
                    $resourceGroups[$account.ResourceGroupName] += $resourceInfo
                }
            }
            catch {
                Write-Warning "Failed to get deployments for $($account.AccountName): $($_.Exception.Message)"
            }
        }
    }
    catch {
        Write-Warning "Failed to process subscription $($subscription.Name): $($_.Exception.Message)"
    }
}

# Display summary
Write-Host "`n" + "="*80 -ForegroundColor Green
Write-Host "AZURE OPENAI RESOURCES SUMMARY WITH PTU METRICS" -ForegroundColor Green
Write-Host "="*80 -ForegroundColor Green
Write-Host "Total Resources Found: $($allOpenAIResources.Count)" -ForegroundColor White
Write-Host "Total Subscriptions Scanned: $($subscriptions.Count)" -ForegroundColor White
Write-Host "Total Resource Groups: $($resourceGroups.Keys.Count)" -ForegroundColor White

# Calculate deployment statistics
$deploymentsWithModels = $allOpenAIResources | Where-Object { $_.DeploymentName -ne "" }
Write-Host "Total Deployments: $($deploymentsWithModels.Count)" -ForegroundColor White

# PTU Statistics
$deploymentsWithPTU = $deploymentsWithModels | Where-Object { $_.PTUAvg7Days -gt 0 }
if ($deploymentsWithPTU.Count -gt 0) {
    $avgPTU = ($deploymentsWithPTU | Measure-Object -Property PTUAvg7Days -Average).Average
    $maxPTU = ($deploymentsWithPTU | Measure-Object -Property PTUMax7Days -Maximum).Maximum
    Write-Host "PTU Usage Summary (Last $MetricsDays Days):" -ForegroundColor Yellow
    Write-Host "  Average PTU Usage: $([math]::Round($avgPTU, 2))%" -ForegroundColor White
    Write-Host "  Highest PTU Usage: $([math]::Round($maxPTU, 2))%" -ForegroundColor White
    Write-Host "  Deployments with PTU data: $($deploymentsWithPTU.Count)" -ForegroundColor White
}

# Group by location
$locationGroups = $allOpenAIResources | Group-Object Location
Write-Host "`nResources by Location:" -ForegroundColor Yellow
foreach ($group in $locationGroups) {
    Write-Host "  $($group.Name): $($group.Count)" -ForegroundColor White
}

# Group by model
$modelGroups = $deploymentsWithModels | Where-Object { $_.ModelName -ne "" } | Group-Object ModelName
if ($modelGroups) {
    Write-Host "`nDeployments by Model:" -ForegroundColor Yellow
    foreach ($group in $modelGroups) {
        $modelPTUAvg = if ($group.Group.PTUAvg7Days) { ($group.Group | Where-Object { $_.PTUAvg7Days -gt 0 } | Measure-Object -Property PTUAvg7Days -Average).Average } else { 0 }
        $modelPTUMax = if ($group.Group.PTUMax7Days) { ($group.Group | Where-Object { $_.PTUMax7Days -gt 0 } | Measure-Object -Property PTUMax7Days -Maximum).Maximum } else { 0 }
        
        $avgStr = if ($modelPTUAvg -gt 0) { "$([math]::Round($modelPTUAvg, 1))%" } else { "N/A" }
        $maxStr = if ($modelPTUMax -gt 0) { "$([math]::Round($modelPTUMax, 1))%" } else { "N/A" }
        
        Write-Host "  $($group.Name): $($group.Count) deployments (Avg PTU: $avgStr, Max PTU: $maxStr)" -ForegroundColor White
    }
}

# Create Excel file
Write-Host "`nCreating Excel report..." -ForegroundColor Green

# Create summary data
$summaryData = @()
foreach ($rgName in $resourceGroups.Keys | Sort-Object) {
    $rgResources = $resourceGroups[$rgName]
    $rgDeployments = $rgResources | Where-Object { $_.DeploymentName -ne "" }
    $avgPTU = if ($rgDeployments) { ($rgDeployments | Where-Object { $_.PTUAvg7Days -gt 0 } | Measure-Object -Property PTUAvg7Days -Average).Average } else { 0 }
    $maxPTU = if ($rgDeployments) { ($rgDeployments | Where-Object { $_.PTUMax7Days -gt 0 } | Measure-Object -Property PTUMax7Days -Maximum).Maximum } else { 0 }
    
    $summaryData += [PSCustomObject]@{
        ResourceGroup = $rgName
        TotalResources = ($rgResources | Select-Object -Unique ResourceName).Count
        TotalDeployments = $rgDeployments.Count
        AvgPTUUsage7Days = [math]::Round($avgPTU, 2)
        MaxPTUUsage7Days = [math]::Round($maxPTU, 2)
    }
}

# Export summary sheet
$summaryData | Export-Excel -Path $OutputFile -WorksheetName "Summary" -AutoSize -BoldTopRow -FreezeTopRow

# Export data for each resource group
foreach ($rgName in $resourceGroups.Keys | Sort-Object) {
    $sheetName = $rgName -replace '[<>:"/\\|?*]', '_'  # Clean sheet name
    if ($sheetName.Length -gt 31) {
        $sheetName = $sheetName.Substring(0, 31)  # Excel sheet name limit
    }
    
    $resourceGroups[$rgName] | Export-Excel -Path $OutputFile -WorksheetName $sheetName -AutoSize -BoldTopRow -FreezeTopRow
}

# Export all data to a single sheet as well
$allOpenAIResources | Export-Excel -Path $OutputFile -WorksheetName "All Resources" -AutoSize -BoldTopRow -FreezeTopRow

Write-Host "`nExcel report created successfully: $OutputFile" -ForegroundColor Green
Write-Host "Report contains:" -ForegroundColor Yellow
Write-Host "  - Summary sheet with resource group statistics" -ForegroundColor White
Write-Host "  - Individual sheets for each resource group" -ForegroundColor White
Write-Host "  - Complete data sheet with all resources" -ForegroundColor White

Write-Host "  - PTU usage metrics and last used dates for the last $MetricsDays days" -ForegroundColor White

Write-Host "`nScan completed successfully!" -ForegroundColor Green