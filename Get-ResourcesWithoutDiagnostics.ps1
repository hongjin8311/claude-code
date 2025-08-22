# Get all Azure resources without diagnostic settings from all subscriptions and export to Excel
# Requires Azure PowerShell module and ImportExcel module

param(
    [string]$OutputFile = "ResourcesWithoutDiagnostics_$(Get-Date -Format 'yyyyMMdd_HHmmss').xlsx",
    [string[]]$ExcludeResourceTypes = @(
        "Microsoft.Network/networkSecurityGroups/securityRules",
        "Microsoft.Authorization/policyAssignments",
        "Microsoft.Authorization/roleAssignments",
        "Microsoft.Authorization/policyDefinitions",
        "Microsoft.Authorization/roleDefinitions"
    )
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
        Install-Module -Name ImportExcel -Force -Scope CurrentUser -AllowClobber
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

# Suppress Azure PowerShell breaking change warnings
$env:SuppressAzurePowerShellBreakingChangeWarnings = "true"

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

$allResourcesWithoutDiagnostics = @()
$resourceTypeSupport = @{}
$processedResources = 0
$totalResources = 0

# Function to check if resource type supports diagnostic settings
function Test-DiagnosticSettingsSupport {
    param(
        [string]$ResourceType,
        [string]$ResourceId
    )
    
    # Cache results to avoid repeated API calls for same resource type
    if ($resourceTypeSupport.ContainsKey($ResourceType)) {
        return $resourceTypeSupport[$ResourceType]
    }
    
    try {
        # Try to get diagnostic settings for the resource
        $diagnosticSettings = Get-AzDiagnosticSetting -ResourceId $ResourceId -ErrorAction Stop
        $resourceTypeSupport[$ResourceType] = $true
        return $true
    } catch {
        if ($_.Exception.Message -like "*does not support diagnostic settings*" -or 
            $_.Exception.Message -like "*ResourceTypeNotSupported*" -or
            $_.Exception.Message -like "*DiagnosticSettingsNotSupported*") {
            $resourceTypeSupport[$ResourceType] = $false
            return $false
        } else {
            # If it's a different error, assume it supports diagnostic settings
            $resourceTypeSupport[$ResourceType] = $true
            return $true
        }
    }
}

# Function to check diagnostic settings for a resource
function Get-ResourceDiagnosticStatus {
    param(
        [object]$Resource
    )
    
    # Skip certain resource types that don't support diagnostic settings
    if ($ExcludeResourceTypes -contains $Resource.ResourceType) {
        return $null
    }
    
    # Check if resource type supports diagnostic settings
    if (-not (Test-DiagnosticSettingsSupport -ResourceType $Resource.ResourceType -ResourceId $Resource.ResourceId)) {
        return $null
    }
    
    try {
        $diagnosticSettings = Get-AzDiagnosticSetting -ResourceId $Resource.ResourceId -ErrorAction Stop
        
        if ($diagnosticSettings -and $diagnosticSettings.Count -gt 0) {
            # Has diagnostic settings
            return $null
        } else {
            # No diagnostic settings found
            return [PSCustomObject]@{
                SubscriptionName = $Resource.SubscriptionName
                SubscriptionId = $Resource.SubscriptionId
                ResourceGroupName = $Resource.ResourceGroupName
                ResourceName = $Resource.Name
                ResourceType = $Resource.ResourceType
                Location = $Resource.Location
                ResourceId = $Resource.ResourceId
                Tags = ($Resource.Tags | ConvertTo-Json -Compress)
                Status = "No Diagnostic Settings"
                LastChecked = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            }
        }
    } catch {
        Write-Warning "Failed to check diagnostic settings for $($Resource.Name): $($_.Exception.Message)"
        return [PSCustomObject]@{
            SubscriptionName = $Resource.SubscriptionName
            SubscriptionId = $Resource.SubscriptionId
            ResourceGroupName = $Resource.ResourceGroupName
            ResourceName = $Resource.Name
            ResourceType = $Resource.ResourceType
            Location = $Resource.Location
            ResourceId = $Resource.ResourceId
            Tags = ($Resource.Tags | ConvertTo-Json -Compress)
            Status = "Check Failed: $($_.Exception.Message)"
            LastChecked = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
    }
}

foreach ($subscription in $subscriptions) {
    Write-Host "Processing subscription: $($subscription.Name) ($($subscription.Id))" -ForegroundColor Yellow
    
    try {
        # Set subscription context
        Set-AzContext -SubscriptionId $subscription.Id | Out-Null
        
        # Get all resources in the subscription
        Write-Host "  Getting all resources..." -ForegroundColor Cyan
        $resources = Get-AzResource
        
        if ($resources) {
            $totalResources += $resources.Count
            Write-Host "  Found $($resources.Count) resources in subscription" -ForegroundColor Cyan
            
            foreach ($resource in $resources) {
                $processedResources++
                Write-Progress -Activity "Checking diagnostic settings" -Status "Processing $($resource.Name) ($processedResources/$totalResources)" -PercentComplete (($processedResources / $totalResources) * 100)
                
                # Add subscription info to resource object
                $resource | Add-Member -NotePropertyName "SubscriptionName" -NotePropertyValue $subscription.Name -Force
                $resource | Add-Member -NotePropertyName "SubscriptionId" -NotePropertyValue $subscription.Id -Force
                
                $diagnosticStatus = Get-ResourceDiagnosticStatus -Resource $resource
                
                if ($diagnosticStatus) {
                    $allResourcesWithoutDiagnostics += $diagnosticStatus
                    Write-Host "    ✗ $($resource.Name) - No diagnostic settings" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "  No resources found in subscription" -ForegroundColor Gray
        }
        
    } catch {
        Write-Error "Failed to process subscription $($subscription.Name): $($_.Exception.Message)"
    }
}

Write-Progress -Activity "Checking diagnostic settings" -Completed

# Create summary information
$summary = [PSCustomObject]@{
    "Total Subscriptions Processed" = $subscriptions.Count
    "Total Resources Checked" = $totalResources
    "Resources Without Diagnostic Settings" = $allResourcesWithoutDiagnostics.Count
    "Report Generated" = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "Excluded Resource Types" = ($ExcludeResourceTypes -join "; ")
}

# Group resources by subscription for better organization
$resourcesBySubscription = $allResourcesWithoutDiagnostics | Group-Object -Property SubscriptionName

Write-Host "`nSummary:" -ForegroundColor Green
Write-Host "  Total subscriptions processed: $($subscriptions.Count)" -ForegroundColor White
Write-Host "  Total resources checked: $totalResources" -ForegroundColor White
Write-Host "  Resources without diagnostic settings: $($allResourcesWithoutDiagnostics.Count)" -ForegroundColor Red

# Export to Excel with multiple sheets
Write-Host "`nExporting results to Excel: $OutputFile" -ForegroundColor Green

try {
    # Create main summary sheet
    $summary | Export-Excel -Path $OutputFile -WorksheetName "Summary" -AutoSize -TableStyle Medium2
    
    # Export all resources to main sheet
    if ($allResourcesWithoutDiagnostics.Count -gt 0) {
        $allResourcesWithoutDiagnostics | Export-Excel -Path $OutputFile -WorksheetName "All Resources" -AutoSize -TableStyle Medium2 -FreezeTopRow
        
        # Create separate sheets for each subscription with resources
        foreach ($subscriptionGroup in $resourcesBySubscription) {
            $subscriptionName = $subscriptionGroup.Name -replace '[^\w\s-]', '_'  # Remove invalid characters for sheet name
            if ($subscriptionName.Length -gt 31) {
                $subscriptionName = $subscriptionName.Substring(0, 31)  # Excel sheet name limit
            }
            
            $subscriptionGroup.Group | Export-Excel -Path $OutputFile -WorksheetName $subscriptionName -AutoSize -TableStyle Medium2 -FreezeTopRow
        }
        
        # Create resource type analysis
        $resourceTypeAnalysis = $allResourcesWithoutDiagnostics | Group-Object -Property ResourceType | ForEach-Object {
            [PSCustomObject]@{
                ResourceType = $_.Name
                Count = $_.Count
                Resources = ($_.Group.ResourceName -join "; ")
            }
        } | Sort-Object Count -Descending
        
        $resourceTypeAnalysis | Export-Excel -Path $OutputFile -WorksheetName "By Resource Type" -AutoSize -TableStyle Medium2 -FreezeTopRow
        
        Write-Host "Excel file created successfully!" -ForegroundColor Green
        Write-Host "Location: $(Resolve-Path $OutputFile)" -ForegroundColor White
    } else {
        Write-Host "No resources without diagnostic settings found!" -ForegroundColor Green
        # Still create the summary sheet
        $summary | Export-Excel -Path $OutputFile -WorksheetName "Summary" -AutoSize -TableStyle Medium2
    }
    
} catch {
    Write-Error "Failed to export to Excel: $($_.Exception.Message)"
    
    # Fallback to CSV export
    Write-Host "Attempting CSV export as fallback..." -ForegroundColor Yellow
    $csvFile = $OutputFile -replace '\.xlsx$', '.csv'
    $allResourcesWithoutDiagnostics | Export-Csv -Path $csvFile -NoTypeInformation
    Write-Host "CSV file created: $csvFile" -ForegroundColor Yellow
}

Write-Host "`nScript completed!" -ForegroundColor Green
Write-Host "Resources without diagnostic settings by resource type:" -ForegroundColor Yellow
$allResourcesWithoutDiagnostics | Group-Object ResourceType | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count)" -ForegroundColor White
}