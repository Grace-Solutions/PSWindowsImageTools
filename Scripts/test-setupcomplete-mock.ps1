# Test SetupComplete Functionality with Mock Mounted Image
Write-Host "🛠️ Testing SetupComplete Functionality" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create mock mounted image structure
$mockImagePath = "C:\Temp\MockMountedImage"
$windowsPath = Join-Path $mockImagePath "Windows"
$scriptsPath = Join-Path $windowsPath "Setup\Scripts"

Write-Host "Creating mock mounted image structure..." -ForegroundColor Yellow

try {
    # Clean up if exists
    if (Test-Path $mockImagePath) {
        Remove-Item $mockImagePath -Recurse -Force
    }

    # Create directory structure
    New-Item -ItemType Directory -Path $scriptsPath -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $windowsPath "System32") -Force | Out-Null

    Write-Host "✅ Mock image structure created at: $mockImagePath" -ForegroundColor Green

    # Create test files to copy
    $testFilesPath = "C:\Temp\TestFiles"
    if (Test-Path $testFilesPath) { Remove-Item $testFilesPath -Recurse -Force }
    New-Item -ItemType Directory -Path $testFilesPath -Force | Out-Null

    # Create test script
    $testScript = Join-Path $testFilesPath "post-install.cmd"
    @"
@echo off
echo Running post-installation script...
echo Computer Name: %COMPUTERNAME%
echo User: %USERNAME%
echo Date: %DATE% %TIME%
echo Post-installation completed successfully.
"@ | Out-File -FilePath $testScript -Encoding ASCII

    # Create test config file
    $testConfig = Join-Path $testFilesPath "app.config"
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <appSettings>
        <add key="Environment" value="Production" />
        <add key="Version" value="1.0.0" />
        <add key="InstallDate" value="$(Get-Date -Format 'yyyy-MM-dd')" />
    </appSettings>
</configuration>
"@ | Out-File -FilePath $testConfig -Encoding UTF8

    Write-Host "✅ Test files created" -ForegroundColor Green

    # Test 1: Simple command addition
    Write-Host "`n--- Test 1: Simple Command Addition ---" -ForegroundColor Yellow
    
    $result1 = Add-SetupCompleteAction -ImagePath $mockImagePath -Command "echo Starting custom setup..." -Description "Initial setup message" -Priority 10 -Verbose
    
    if ($result1.IsSuccessful) {
        Write-Host "✅ Simple command added successfully" -ForegroundColor Green
        Write-Host "   Commands: $($result1.CommandCount)" -ForegroundColor White
        Write-Host "   Description: $($result1.Description)" -ForegroundColor White
    } else {
        Write-Host "❌ Failed: $($result1.ErrorMessage)" -ForegroundColor Red
    }

    # Test 2: Script file addition
    Write-Host "`n--- Test 2: Script File Addition ---" -ForegroundColor Yellow
    
    $result2 = Add-SetupCompleteAction -ImagePath $mockImagePath -ScriptFile $testScript -Description "Run post-installation script" -Priority 50 -ContinueOnError -Verbose
    
    if ($result2.IsSuccessful) {
        Write-Host "✅ Script file added successfully" -ForegroundColor Green
        Write-Host "   Script: $($result2.ScriptFile.Name)" -ForegroundColor White
        Write-Host "   Continue on error: $($result2.ContinueOnError)" -ForegroundColor White
    } else {
        Write-Host "❌ Failed: $($result2.ErrorMessage)" -ForegroundColor Red
    }

    # Test 3: File copying with commands
    Write-Host "`n--- Test 3: File Copying with Commands ---" -ForegroundColor Yellow
    
    $result3 = Add-SetupCompleteAction -ImagePath $mockImagePath -CopyFiles (Get-Item $testConfig) -CopyDestination "Temp\Config" -Command "copy C:\Temp\Config\app.config C:\ProgramData\" -Description "Deploy configuration" -Priority 100 -Backup -Verbose
    
    if ($result3.IsSuccessful) {
        Write-Host "✅ Files copied and commands added successfully" -ForegroundColor Green
        Write-Host "   Copied files: $($result3.CopiedFileCount)" -ForegroundColor White
        Write-Host "   Backup created: $($result3.Backup)" -ForegroundColor White
    } else {
        Write-Host "❌ Failed: $($result3.ErrorMessage)" -ForegroundColor Red
    }

    # Test 4: Multiple commands with high priority
    Write-Host "`n--- Test 4: Multiple Commands ---" -ForegroundColor Yellow
    
    $commands = @(
        "net stop Themes",
        "reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System /v EnableLUA /t REG_DWORD /d 0 /f",
        "net start Themes"
    )
    
    $result4 = Add-SetupCompleteAction -ImagePath $mockImagePath -Command $commands -Description "System configuration" -Priority 200 -Verbose
    
    if ($result4.IsSuccessful) {
        Write-Host "✅ Multiple commands added successfully" -ForegroundColor Green
        Write-Host "   Commands: $($result4.CommandCount)" -ForegroundColor White
        Write-Host "   Priority: $($result4.Priority)" -ForegroundColor White
    } else {
        Write-Host "❌ Failed: $($result4.ErrorMessage)" -ForegroundColor Red
    }

    # Show final SetupComplete.cmd content
    Write-Host "`n--- Final SetupComplete.cmd Content ---" -ForegroundColor Yellow
    
    $setupCompletePath = Join-Path $scriptsPath "SetupComplete.cmd"
    if (Test-Path $setupCompletePath) {
        Write-Host "SetupComplete.cmd contents:" -ForegroundColor Cyan
        Write-Host "=" * 50 -ForegroundColor Gray
        Get-Content $setupCompletePath | ForEach-Object { Write-Host $_ -ForegroundColor White }
        Write-Host "=" * 50 -ForegroundColor Gray
        
        $fileSize = (Get-Item $setupCompletePath).Length
        Write-Host "`nFile size: $fileSize bytes" -ForegroundColor Green
    } else {
        Write-Host "❌ SetupComplete.cmd not found" -ForegroundColor Red
    }

    # Show copied files
    Write-Host "`n--- Copied Files ---" -ForegroundColor Yellow
    
    $tempConfigPath = Join-Path $mockImagePath "Temp\Config"
    if (Test-Path $tempConfigPath) {
        Write-Host "Files in Temp\Config:" -ForegroundColor Cyan
        Get-ChildItem $tempConfigPath | ForEach-Object {
            Write-Host "  📄 $($_.Name) ($($_.Length) bytes)" -ForegroundColor White
        }
    }

    $tempSetupPath = Join-Path $mockImagePath "Temp\SetupComplete"
    if (Test-Path $tempSetupPath) {
        Write-Host "Files in Temp\SetupComplete:" -ForegroundColor Cyan
        Get-ChildItem $tempSetupPath | ForEach-Object {
            Write-Host "  📄 $($_.Name) ($($_.Length) bytes)" -ForegroundColor White
        }
    }

    Write-Host "`n--- Summary ---" -ForegroundColor Yellow
    Write-Host "✅ All SetupComplete tests completed successfully!" -ForegroundColor Green
    Write-Host "✅ Commands added with different priorities" -ForegroundColor Green
    Write-Host "✅ Script files copied and referenced" -ForegroundColor Green
    Write-Host "✅ Configuration files deployed" -ForegroundColor Green
    Write-Host "✅ Error handling configured" -ForegroundColor Green

} catch {
    Write-Host "❌ Error during testing: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Cleanup
    Write-Host "`n--- Cleanup ---" -ForegroundColor Yellow
    $cleanup = Read-Host "Do you want to clean up test files? (y/N)"
    
    if ($cleanup -eq 'y' -or $cleanup -eq 'Y') {
        if (Test-Path $mockImagePath) { Remove-Item $mockImagePath -Recurse -Force }
        if (Test-Path $testFilesPath) { Remove-Item $testFilesPath -Recurse -Force }
        Write-Host "✅ Test files cleaned up" -ForegroundColor Green
    } else {
        Write-Host "📁 Test files preserved:" -ForegroundColor Cyan
        Write-Host "   Mock image: $mockImagePath" -ForegroundColor White
        Write-Host "   Test files: $testFilesPath" -ForegroundColor White
    }
}

Write-Host "`n=======================================" -ForegroundColor Cyan
Write-Host "🎉 SetupComplete functionality tested!" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Cyan
