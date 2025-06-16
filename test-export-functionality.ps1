# Test script for actual export functionality using DISM.exe directly
Write-Host "Testing Real Export Functionality" -ForegroundColor Green

# Test parameters
$sourceESD = "C:\Users\gsadmin\Downloads\WindowsImages\26100.2033.241004-2336.ge_release_svc_refresh_CLIENTBUSINESS_VOL_x64FRE_en-us.esd"
$destinationWIM = "C:\Users\gsadmin\Downloads\WindowsImages\test-export.wim"
$sourceIndex = 6  # Windows 11 Enterprise

# Clean up any existing test file
if (Test-Path $destinationWIM) {
    Remove-Item $destinationWIM -Force
    Write-Host "Cleaned up existing test file" -ForegroundColor Yellow
}

Write-Host "`nTesting export of Windows 11 Enterprise (Index $sourceIndex)" -ForegroundColor Yellow
Write-Host "Source: $sourceESD" -ForegroundColor White
Write-Host "Destination: $destinationWIM" -ForegroundColor White

try {
    Write-Host "`nStarting export operation using DISM.exe directly..." -ForegroundColor Green
    $startTime = Get-Date

    # Use DISM.exe directly for testing
    $dismExe = "$env:SystemRoot\System32\dism.exe"
    $arguments = "/Export-Image /SourceImageFile:`"$sourceESD`" /SourceIndex:$sourceIndex /DestinationImageFile:`"$destinationWIM`" /Compress:Max"

    Write-Host "Executing: $dismExe $arguments" -ForegroundColor Cyan

    $process = Start-Process -FilePath $dismExe -ArgumentList $arguments -Wait -PassThru -NoNewWindow

    $endTime = Get-Date
    $duration = $endTime - $startTime

    if ($process.ExitCode -eq 0) {
        Write-Host "`n✅ Export completed successfully!" -ForegroundColor Green
        Write-Host "Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor White

        if (Test-Path $destinationWIM) {
            $fileInfo = Get-Item $destinationWIM
            Write-Host "Output file size: $([math]::Round($fileInfo.Length / 1GB, 2)) GB" -ForegroundColor White

            # Test the exported WIM by getting image info
            Write-Host "`nVerifying exported image..." -ForegroundColor Yellow
            Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force
            $imageInfo = Get-WindowsImageList -ImagePath $destinationWIM

            if ($imageInfo) {
                Write-Host "✅ Exported image verified successfully!" -ForegroundColor Green
                Write-Host "Image Name: $($imageInfo.Name)" -ForegroundColor White
                Write-Host "Edition: $($imageInfo.Edition)" -ForegroundColor White
                Write-Host "Architecture: $($imageInfo.Architecture)" -ForegroundColor White
                Write-Host "Size: $([math]::Round($imageInfo.Size / 1GB, 2)) GB" -ForegroundColor White
            } else {
                Write-Host "⚠️ Could not verify exported image" -ForegroundColor Yellow
            }
        } else {
            Write-Host "⚠️ Export reported success but file not found" -ForegroundColor Yellow
        }
    } else {
        Write-Host "`n❌ Export failed with exit code: $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "`n❌ Export test failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTest completed." -ForegroundColor Green
