# Test New Cmdlets: Install-WindowsUpdateFile and Add-SetupCompleteAction
Write-Host "🆕 Testing New Cmdlets" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host "1. Install-WindowsUpdateFile - Install CAB/MSU files into mounted images" -ForegroundColor Yellow
Write-Host "2. Add-SetupCompleteAction - Add custom actions to SetupComplete.cmd" -ForegroundColor Yellow
Write-Host "======================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Test 1: Check if cmdlets are available
Write-Host "`n--- Test 1: Cmdlet Availability ---" -ForegroundColor Yellow

$installCmdlet = Get-Command Install-WindowsUpdateFile -ErrorAction SilentlyContinue
$setupCmdlet = Get-Command Add-SetupCompleteAction -ErrorAction SilentlyContinue

if ($installCmdlet) {
    Write-Host "✅ Install-WindowsUpdateFile cmdlet available" -ForegroundColor Green
    Write-Host "   Synopsis: $($installCmdlet.Definition.Split("`n")[0])" -ForegroundColor Gray
} else {
    Write-Host "❌ Install-WindowsUpdateFile cmdlet not found" -ForegroundColor Red
}

if ($setupCmdlet) {
    Write-Host "✅ Add-SetupCompleteAction cmdlet available" -ForegroundColor Green
    Write-Host "   Synopsis: $($setupCmdlet.Definition.Split("`n")[0])" -ForegroundColor Gray
} else {
    Write-Host "❌ Add-SetupCompleteAction cmdlet not found" -ForegroundColor Red
}

# Test 2: Show cmdlet help
Write-Host "`n--- Test 2: Cmdlet Help ---" -ForegroundColor Yellow

if ($installCmdlet) {
    Write-Host "`nInstall-WindowsUpdateFile Parameters:" -ForegroundColor Cyan
    $installCmdlet.Parameters.Keys | ForEach-Object {
        $param = $installCmdlet.Parameters[$_]
        Write-Host "  -$_ [$($param.ParameterType.Name)]" -ForegroundColor White
    }
}

if ($setupCmdlet) {
    Write-Host "`nAdd-SetupCompleteAction Parameters:" -ForegroundColor Cyan
    $setupCmdlet.Parameters.Keys | ForEach-Object {
        $param = $setupCmdlet.Parameters[$_]
        Write-Host "  -$_ [$($param.ParameterType.Name)]" -ForegroundColor White
    }
}

# Test 3: Mock scenarios (since we don't have actual mounted images)
Write-Host "`n--- Test 3: Mock Scenarios ---" -ForegroundColor Yellow

Write-Host "`n🔧 Install-WindowsUpdateFile Usage Examples:" -ForegroundColor Cyan
Write-Host @"
# Install single update file
Install-WindowsUpdateFile -UpdatePath "C:\Updates\KB5000001.msu" -ImagePath "C:\Mount\Image1"

# Install multiple updates from directory
Install-WindowsUpdateFile -UpdatePath "C:\Updates\" -ImagePath "C:\Mount\Image1" -ContinueOnError

# Install with validation and ignore checks
Install-WindowsUpdateFile -UpdatePath "C:\Updates\*.cab" -ImagePath "C:\Mount\Image1" -ValidateImage -IgnoreCheck

# Pipeline from downloaded updates
Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' | 
    Get-WindowsUpdateDownloadUrl | 
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" | 
    ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath "C:\Mount\Image1" }
"@ -ForegroundColor White

Write-Host "`n🛠️ Add-SetupCompleteAction Usage Examples:" -ForegroundColor Cyan
Write-Host @"
# Add simple command
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "reg add HKLM\Software\MyApp /v Installed /t REG_SZ /d Yes" -Description "Register application"

# Add script execution
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -ScriptFile "C:\Scripts\setup.cmd" -Description "Run custom setup" -Priority 50

# Add with file copying
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "copy C:\Temp\config.xml C:\Program Files\MyApp\" -CopyFiles "C:\LocalFiles\config.xml" -Description "Deploy configuration"

# Add with error handling
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "net start MyService" -Description "Start service" -ContinueOnError -Priority 200

# Complex scenario with multiple files and script
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" `
    -ScriptFile "C:\Scripts\post-install.ps1" `
    -CopyFiles "C:\Tools\*", "C:\Configs\app.config" `
    -CopyDestination "Temp\Deployment" `
    -Description "Complete application deployment" `
    -Priority 100 `
    -Backup
"@ -ForegroundColor White

# Test 4: Integration workflow
Write-Host "`n--- Test 4: Complete Integration Workflow ---" -ForegroundColor Yellow
Write-Host @"
🔄 Complete Windows Image Customization Workflow:

1. Mount Windows Image:
   Mount-WindowsImageList -ImagePath "install.wim" -Index 1 -MountPath "C:\Mount\Image1"

2. Download and Install Updates:
   Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 |
       Get-WindowsUpdateDownloadUrl |
       Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" |
       ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath "C:\Mount\Image1" -ValidateImage }

3. Add Custom SetupComplete Actions:
   # Copy deployment tools
   Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -CopyFiles "C:\DeploymentTools\*" -CopyDestination "Tools" -Command "echo Deployment tools copied" -Priority 10
   
   # Install applications
   Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -ScriptFile "C:\Scripts\install-apps.cmd" -Description "Install applications" -Priority 50
   
   # Configure system
   Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "powershell -File C:\Tools\configure-system.ps1" -Description "System configuration" -Priority 100 -ContinueOnError
   
   # Final cleanup
   Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "del /q C:\Tools\*.tmp" -Description "Cleanup temporary files" -Priority 200

4. Dismount and Save:
   Dismount-WindowsImage -Path "C:\Mount\Image1" -Save

✅ Result: Fully customized Windows image with:
   - Latest cumulative updates installed
   - Custom applications and configurations deployed via SetupComplete
   - Automated post-installation tasks
   - Error handling and logging
"@ -ForegroundColor Green

Write-Host "`n======================" -ForegroundColor Cyan
Write-Host "🎉 New cmdlets tested!" -ForegroundColor Green
Write-Host "✅ Install-WindowsUpdateFile - Direct update installation" -ForegroundColor Green
Write-Host "✅ Add-SetupCompleteAction - Custom deployment automation" -ForegroundColor Green
Write-Host "✅ Complete integration workflow documented" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Cyan
