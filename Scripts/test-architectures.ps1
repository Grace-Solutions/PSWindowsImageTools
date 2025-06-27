# Test what architectures are actually in the data
Import-Module '.\Module\PSWindowsImageTools' -Force
$results = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -MaxResults 10
Write-Host "Architectures found:" -ForegroundColor Cyan
$results | Group-Object Architecture | Select-Object Name, Count | Format-Table
