# Debug the result type
Write-Host "Debugging Result Type" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force
$results = Search-WindowsUpdateCatalog -Query 'Windows 11 Security Update' -MaxResults 1

Write-Host "Results type: $($results.GetType().FullName)" -ForegroundColor Yellow
Write-Host "Results count: $($results.Count)" -ForegroundColor Yellow

if ($results.Count -gt 0) {
    $first = $results[0]
    Write-Host "First item type: $($first.GetType().FullName)" -ForegroundColor Yellow
    Write-Host "First item properties:" -ForegroundColor Yellow
    $first | Get-Member -MemberType Property | Select-Object Name, MemberType | Format-Table
    
    # Check if it has Updates property
    if ($first.PSObject.Properties['Updates']) {
        Write-Host "Found Updates property with $($first.Updates.Count) items" -ForegroundColor Green
        if ($first.Updates.Count -gt 0) {
            $update = $first.Updates[0]
            Write-Host "Update type: $($update.GetType().FullName)" -ForegroundColor Green
            Write-Host "Update SizeInBytes: $($update.SizeInBytes)" -ForegroundColor Green
            
            # Test size methods on the actual update
            try {
                $kb = $update.ToKB()
                Write-Host "✅ ToKB() works: $kb KB" -ForegroundColor Green
            } catch {
                Write-Host "❌ ToKB() failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

Write-Host "`nDebug completed" -ForegroundColor Cyan
