# Test script for new Autopilot JSON structure
Import-Module "$PSScriptRoot\..\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force

Write-Output "=== Testing New Autopilot JSON Structure ==="

# Create a new configuration
$config = New-AutopilotConfiguration -TenantId "87789cc7-933c-4e19-99b3-73a1953e3df7" -TenantDomain "contoso.onmicrosoft.com" -DeviceName "XYL-%SERIAL%"

Write-Output "`n=== Configuration Properties ==="
Write-Output "CloudAssignedTenantId: $($config.CloudAssignedTenantId)"
Write-Output "CloudAssignedDeviceName: $($config.CloudAssignedDeviceName)"
Write-Output "CloudAssignedAutopilotUpdateTimeout: $($config.CloudAssignedAutopilotUpdateTimeout)"
Write-Output "CloudAssignedAutopilotUpdateDisabled: $($config.CloudAssignedAutopilotUpdateDisabled)"
Write-Output "CloudAssignedForcedEnrollment: $($config.CloudAssignedForcedEnrollment)"
Write-Output "Version: $($config.Version)"
Write-Output "CommentFile: $($config.CommentFile)"
Write-Output "CloudAssignedOobeConfig: $($config.CloudAssignedOobeConfig)"
Write-Output "CloudAssignedDomainJoinMethod: $($config.CloudAssignedDomainJoinMethod)"
Write-Output "ZtdCorrelationId: $($config.ZtdCorrelationId)"
Write-Output "CloudAssignedTenantDomain: $($config.CloudAssignedTenantDomain)"

Write-Output "`n=== CloudAssignedAadServerData ==="
Write-Output $config.CloudAssignedAadServerData

# Export to JSON
$testFile = "C:\Temp\test-autopilot.json"
$config | Export-AutopilotConfiguration -Path $testFile -Force

Write-Output "`n=== Exported JSON Content ==="
Get-Content $testFile | Write-Output

# Load it back
$loadedConfig = Get-AutopilotConfiguration -Path $testFile

Write-Output "`n=== Loaded Configuration Validation ==="
$validationErrors = $loadedConfig.Validate()
if ($validationErrors.Count -eq 0) {
    Write-Output "✅ Configuration is valid"
} else {
    Write-Output "❌ Validation errors:"
    $validationErrors | ForEach-Object { Write-Output "  - $_" }
}

# Clean up
Remove-Item $testFile -Force -ErrorAction SilentlyContinue

Write-Output "`n=== Test Complete ==="
