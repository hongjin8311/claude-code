# Get all Azure VMs from all subscriptions and export to Excel with resource group sheets
# Requires Azure PowerShell module and ImportExcel module

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

$allVMs = @()
$resourceGroups = @{}

foreach ($subscription in $subscriptions) {
    Write-Host "Processing subscription: $($subscription.Name) ($($subscription.Id))" -ForegroundColor Cyan
    
    # Set the subscription context
    try {
        Set-AzContext -SubscriptionId $subscription.Id | Out-Null
        Write-Host "  Set context to subscription: $($subscription.Name)" -ForegroundColor Yellow
    } catch {
        Write-Warning "Failed to set context for subscription $($subscription.Name): $($_.Exception.Message)"
        continue
    }

    # Get all VMs in the subscription
    try {
        $vms = Get-AzVM
        Write-Host "  Found $($vms.Count) VMs in subscription $($subscription.Name)" -ForegroundColor Green
        
        foreach ($vm in $vms) {
            Write-Host "    Processing VM: $($vm.Name)" -ForegroundColor White
            
            # Get detailed VM information
            $vmDetail = Get-AzVM -ResourceGroupName $vm.ResourceGroupName -Name $vm.Name -Status
            
            # Get network interfaces
            $networkInterfaces = @()
            foreach ($nicRef in $vm.NetworkProfile.NetworkInterfaces) {
                $nicId = $nicRef.Id
                $nic = Get-AzNetworkInterface | Where-Object { $_.Id -eq $nicId }
                
                if ($nic) {
                    $ipConfigurations = @()
                    foreach ($ipConfig in $nic.IpConfigurations) {
                        $subnet = ""
                        $vnet = ""
                        if ($ipConfig.Subnet) {
                            $subnetId = $ipConfig.Subnet.Id
                            $subnetParts = $subnetId.Split('/')
                            $vnet = $subnetParts[8]
                            $subnet = $subnetParts[10]
                        }
                        
                        $publicIP = ""
                        if ($ipConfig.PublicIpAddress) {
                            $pipId = $ipConfig.PublicIpAddress.Id
                            $pip = Get-AzPublicIpAddress | Where-Object { $_.Id -eq $pipId }
                            if ($pip) {
                                $publicIP = $pip.IpAddress
                            }
                        }
                        
                        $ipConfigurations += [PSCustomObject]@{
                            Name = $ipConfig.Name
                            PrivateIpAddress = $ipConfig.PrivateIpAddress
                            PrivateIpAllocationMethod = $ipConfig.PrivateIpAllocationMethod
                            PublicIpAddress = $publicIP
                            VirtualNetwork = $vnet
                            Subnet = $subnet
                        }
                    }
                    
                    $networkInterfaces += [PSCustomObject]@{
                        Name = $nic.Name
                        Primary = $nic.Primary
                        IpConfigurations = $ipConfigurations
                        Location = $nic.Location
                        EnableAcceleratedNetworking = $nic.EnableAcceleratedNetworking
                        EnableIPForwarding = $nic.EnableIPForwarding
                    }
                }
            }
            
            # Get OS disk information
            $osDisk = $vm.StorageProfile.OsDisk
            $osDiskInfo = [PSCustomObject]@{
                Name = $osDisk.Name
                OsType = $osDisk.OsType
                CreateOption = $osDisk.CreateOption
                Caching = $osDisk.Caching
                DiskSizeGB = $osDisk.DiskSizeGB
                ManagedDisk = if ($osDisk.ManagedDisk) { $osDisk.ManagedDisk.StorageAccountType } else { "Unmanaged" }
            }
            
            # Get data disks information
            $dataDisks = @()
            foreach ($dataDisk in $vm.StorageProfile.DataDisks) {
                $dataDisks += [PSCustomObject]@{
                    Name = $dataDisk.Name
                    Lun = $dataDisk.Lun
                    CreateOption = $dataDisk.CreateOption
                    Caching = $dataDisk.Caching
                    DiskSizeGB = $dataDisk.DiskSizeGB
                    ManagedDisk = if ($dataDisk.ManagedDisk) { $dataDisk.ManagedDisk.StorageAccountType } else { "Unmanaged" }
                }
            }
            
            # Get VM status
            $powerState = ($vmDetail.Statuses | Where-Object { $_.Code -like "PowerState/*" }).DisplayStatus
            $provisioningState = ($vmDetail.Statuses | Where-Object { $_.Code -like "ProvisioningState/*" }).DisplayStatus
            
            # Create VM object with all details
            $vmObject = [PSCustomObject]@{
                SubscriptionName = $subscription.Name
                SubscriptionId = $subscription.Id
                ResourceGroupName = $vm.ResourceGroupName
                VMName = $vm.Name
                Location = $vm.Location
                VMSize = $vm.HardwareProfile.VmSize
                PowerState = $powerState
                ProvisioningState = $provisioningState
                OSType = $vm.StorageProfile.OsDisk.OsType
                OSPublisher = $vm.StorageProfile.ImageReference.Publisher
                OSOffer = $vm.StorageProfile.ImageReference.Offer
                OSSku = $vm.StorageProfile.ImageReference.Sku
                OSVersion = $vm.StorageProfile.ImageReference.Version
                OSDiskName = $osDiskInfo.Name
                OSDiskType = $osDiskInfo.ManagedDisk
                OSDiskSizeGB = $osDiskInfo.DiskSizeGB
                DataDisksCount = $dataDisks.Count
                DataDisksDetails = ($dataDisks | ForEach-Object { "$($_.Name) ($($_.DiskSizeGB)GB, $($_.ManagedDisk))" }) -join "; "
                NetworkInterfacesCount = $networkInterfaces.Count
                PrimaryNIC = ($networkInterfaces | Where-Object { $_.Primary -eq $true }).Name
                PrivateIPAddresses = ($networkInterfaces | ForEach-Object { $_.IpConfigurations | ForEach-Object { $_.PrivateIpAddress } }) -join "; "
                PublicIPAddresses = ($networkInterfaces | ForEach-Object { $_.IpConfigurations | ForEach-Object { $_.PublicIpAddress } } | Where-Object { $_ -ne "" }) -join "; "
                VirtualNetworks = ($networkInterfaces | ForEach-Object { $_.IpConfigurations | ForEach-Object { $_.VirtualNetwork } } | Sort-Object -Unique) -join "; "
                Subnets = ($networkInterfaces | ForEach-Object { $_.IpConfigurations | ForEach-Object { "$($_.VirtualNetwork)/$($_.Subnet)" } } | Sort-Object -Unique) -join "; "
                AcceleratedNetworking = ($networkInterfaces | Where-Object { $_.EnableAcceleratedNetworking -eq $true }).Count -gt 0
                IPForwarding = ($networkInterfaces | Where-Object { $_.EnableIPForwarding -eq $true }).Count -gt 0
                AvailabilitySetId = $vm.AvailabilitySetReference.Id
                Tags = if ($vm.Tags) { ($vm.Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "; " } else { "" }
                CreatedTime = $vm.TimeCreated
            }
            
            $allVMs += $vmObject
            
            # Group by resource group for separate sheets
            if (-not $resourceGroups.ContainsKey($vm.ResourceGroupName)) {
                $resourceGroups[$vm.ResourceGroupName] = @()
            }
            $resourceGroups[$vm.ResourceGroupName] += $vmObject
        }
    } catch {
        Write-Warning "Failed to get VMs from subscription $($subscription.Name): $($_.Exception.Message)"
    }
}

# Create Excel file with separate sheets for each resource group
$excelFile = "Azure-VM-Inventory-$(Get-Date -Format 'yyyy-MM-dd-HHmm').xlsx"
Write-Host "Creating Excel file: $excelFile" -ForegroundColor Green

# Create summary sheet with all VMs
$allVMs | Export-Excel -Path $excelFile -WorksheetName "All VMs" -AutoSize -AutoFilter -FreezeTopRow -TableStyle Medium2

# Create separate sheets for each resource group
foreach ($rgName in $resourceGroups.Keys) {
    $rgVMs = $resourceGroups[$rgName]
    $sheetName = $rgName
    
    # Limit sheet name length (Excel has 31 character limit)
    if ($sheetName.Length -gt 31) {
        $sheetName = $sheetName.Substring(0, 31)
    }
    
    Write-Host "Creating sheet for resource group: $rgName ($($rgVMs.Count) VMs)" -ForegroundColor Cyan
    $rgVMs | Export-Excel -Path $excelFile -WorksheetName $sheetName -AutoSize -AutoFilter -FreezeTopRow -TableStyle Medium2
}

Write-Host "Excel file created successfully: $excelFile" -ForegroundColor Green
Write-Host "Total VMs processed: $($allVMs.Count)" -ForegroundColor Green
Write-Host "Resource Groups: $($resourceGroups.Keys.Count)" -ForegroundColor Green

# Display summary
Write-Host "`nSummary by Resource Group:" -ForegroundColor Yellow
foreach ($rgName in $resourceGroups.Keys | Sort-Object) {
    Write-Host "  $rgName: $($resourceGroups[$rgName].Count) VMs" -ForegroundColor White
}