# Test script for FileInfo parameter changes
Import-Module "$PSScriptRoot\..\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force

Write-Output "=== Testing FileInfo Parameters ==="

# Test Autopilot with FileInfo
Write-Output "`n=== Test 1: Autopilot Export with FileInfo ==="
$config = New-AutopilotConfiguration -TenantId "12345" -TenantDomain "test.com"
$outputFile = "C:\Temp\test-autopilot.json"
$result = $config | Export-AutopilotConfiguration -OutputFile $outputFile -PassThru
Write-Output "Exported to: $($result.FullName)"
Write-Output "File exists: $($result.Exists)"
Write-Output "File size: $($result.Length) bytes"

# Test Autopilot Load with FileInfo
Write-Output "`n=== Test 2: Autopilot Load with FileInfo ==="
$loadedConfig = Get-AutopilotConfiguration -File $outputFile -Validate
Write-Output "Loaded from: $($loadedConfig.SourceFilePath)"
Write-Output "Tenant Domain: $($loadedConfig.CloudAssignedTenantDomain)"

# Test UnattendXML with FileInfo
Write-Output "`n=== Test 3: UnattendXML Export with FileInfo ==="
$xmlConfig = New-UnattendXMLConfiguration -Template Basic -IncludeSamples
$xmlOutputFile = "C:\Temp\test-unattend.xml"
$xmlResult = $xmlConfig | Export-UnattendXMLConfiguration -OutputFile $xmlOutputFile -Encoding UTF8 -PassThru
Write-Output "Exported to: $($xmlResult.FullName)"
Write-Output "File exists: $($xmlResult.Exists)"
Write-Output "File size: $($xmlResult.Length) bytes"

# Test UnattendXML Load with FileInfo
Write-Output "`n=== Test 4: UnattendXML Load with FileInfo ==="
$loadedXmlConfig = Get-UnattendXMLConfiguration -File $xmlOutputFile -Validate
Write-Output "Loaded from: $($loadedXmlConfig.SourceFilePath)"
Write-Output "Configuration passes: $($loadedXmlConfig.ConfigurationPasses -join ', ')"
Write-Output "Components: $($loadedXmlConfig.Components.Count)"

# Cleanup
Remove-Item $outputFile -Force -ErrorAction SilentlyContinue
Remove-Item $xmlOutputFile -Force -ErrorAction SilentlyContinue

Write-Output "`n=== FileInfo Parameter Test Complete ==="
