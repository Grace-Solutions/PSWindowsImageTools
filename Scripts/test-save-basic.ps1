# Basic test of Save cmdlet functionality
Write-Host "💾 Basic Save Cmdlet Test" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Test the cmdlet parameters
Write-Host "`n--- Testing Save Cmdlet Parameters ---" -ForegroundColor Yellow
$saveCmd = Get-Command Save-WindowsUpdateCatalogResult
Write-Host "Available parameters:" -ForegroundColor Cyan
$saveCmd.Parameters.Keys | Where-Object { $_ -notin @('Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'InformationAction', 'ErrorVariable', 'WarningVariable', 'InformationVariable', 'OutVariable', 'OutBuffer', 'PipelineVariable') } | Sort-Object | ForEach-Object {
    $param = $saveCmd.Parameters[$_]
    Write-Host "  -$($_): $($param.ParameterType.Name)" -ForegroundColor White
}

# Test with a small search
Write-Host "`n--- Testing Basic Search and URL Retrieval ---" -ForegroundColor Yellow
$result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -MaxResults 1
Write-Host "Found: $($result.Title)" -ForegroundColor Cyan
Write-Host "Size: $($result.SizeFormatted)" -ForegroundColor White

# Get download URL
$result | Get-WindowsUpdateDownloadUrl
Write-Host "Has download URLs: $($result.HasDownloadUrls)" -ForegroundColor Green
Write-Host "Download URL count: $($result.DownloadUrls.Count)" -ForegroundColor White

if ($result.DownloadUrls.Count -gt 0) {
    Write-Host "First URL: $($result.DownloadUrls[0].OriginalString)" -ForegroundColor Gray
} else {
    Write-Host "❌ No download URLs found!" -ForegroundColor Red
}

Write-Host "`n=========================" -ForegroundColor Cyan
Write-Host "✅ Basic test complete!" -ForegroundColor Green
