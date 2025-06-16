# Comprehensive Test Script for Get-WindowsImageList Cmdlet
# Tests basic functionality, filtering, and advanced features

Write-Host "=== GET-WINDOWSIMAGELIST CMDLET COMPREHENSIVE TEST ===" -ForegroundColor Yellow
Write-Host "Testing all functionality including filtering capabilities" -ForegroundColor Cyan
Write-Host ""

# Test parameters
$esdFile = "C:\Users\gsadmin\Downloads\WindowsImages\26100.2033.241004-2336.ge_release_svc_refresh_CLIENTBUSINESS_VOL_x64FRE_en-us.esd"

# Check if test file exists
if (!(Test-Path $esdFile)) {
    Write-Host "ERROR: Test ESD file not found: $esdFile" -ForegroundColor Red
    Write-Host "Please ensure the ESD file exists for testing" -ForegroundColor Yellow
    exit 1
}

# Import the module
try {
    Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force
    Write-Host "SUCCESS: Module imported successfully" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to import module: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "TEST 1: Basic Image List Retrieval" -ForegroundColor Green
Write-Host "Getting all images from ESD file..." -ForegroundColor White

try {
    $allImages = Get-WindowsImageList -ImagePath $esdFile -Verbose
    
    if ($allImages -and $allImages.Count -gt 0) {
        Write-Host "SUCCESS: Retrieved $($allImages.Count) images" -ForegroundColor Green
        
        # Display basic info for first few images
        $allImages | Select-Object -First 3 | ForEach-Object {
            Write-Host "  Image $($_.Index): $($_.Name)" -ForegroundColor Cyan
            Write-Host "    Edition: $($_.Edition)" -ForegroundColor White
            Write-Host "    Architecture: $($_.Architecture)" -ForegroundColor White
            Write-Host "    Size: $([math]::Round($_.Size / 1GB, 2)) GB" -ForegroundColor White
            Write-Host ""
        }
    } else {
        Write-Host "ERROR: No images retrieved" -ForegroundColor Red
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 2: Filtering by Edition" -ForegroundColor Green
Write-Host "Testing InclusionFilter with edition names..." -ForegroundColor White

try {
    # Test inclusion filter for Professional editions
    $proImages = Get-WindowsImageList -ImagePath $esdFile -InclusionFilter { $_.Edition -like "*Pro*" } -Verbose
    
    if ($proImages) {
        Write-Host "SUCCESS: Found $($proImages.Count) Professional edition(s)" -ForegroundColor Green
        $proImages | ForEach-Object {
            Write-Host "  $($_.Name) - $($_.Edition)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "INFO: No Professional editions found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 3: Filtering by Size" -ForegroundColor Green
Write-Host "Testing InclusionFilter with size criteria..." -ForegroundColor White

try {
    # Test inclusion filter for images larger than 4GB
    $largeImages = Get-WindowsImageList -ImagePath $esdFile -InclusionFilter { $_.Size -gt 4GB } -Verbose
    
    if ($largeImages) {
        Write-Host "SUCCESS: Found $($largeImages.Count) image(s) larger than 4GB" -ForegroundColor Green
        $largeImages | ForEach-Object {
            Write-Host "  $($_.Name) - $([math]::Round($_.Size / 1GB, 2)) GB" -ForegroundColor Cyan
        }
    } else {
        Write-Host "INFO: No images larger than 4GB found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 4: Exclusion Filter" -ForegroundColor Green
Write-Host "Testing ExclusionFilter to exclude certain editions..." -ForegroundColor White

try {
    # Test exclusion filter to exclude Home editions
    $nonHomeImages = Get-WindowsImageList -ImagePath $esdFile -ExclusionFilter { $_.Edition -like "*Home*" } -Verbose
    
    if ($nonHomeImages) {
        Write-Host "SUCCESS: Found $($nonHomeImages.Count) non-Home edition(s)" -ForegroundColor Green
        $nonHomeImages | Select-Object -First 3 | ForEach-Object {
            Write-Host "  $($_.Name) - $($_.Edition)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "INFO: All images are Home editions or no images found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 5: Combined Filters" -ForegroundColor Green
Write-Host "Testing both InclusionFilter and ExclusionFilter together..." -ForegroundColor White

try {
    # Test combined filters: include Windows 11, exclude Home
    $filteredImages = Get-WindowsImageList -ImagePath $esdFile `
        -InclusionFilter { $_.Name -like "*Windows 11*" } `
        -ExclusionFilter { $_.Edition -like "*Home*" } `
        -Verbose
    
    if ($filteredImages) {
        Write-Host "SUCCESS: Found $($filteredImages.Count) Windows 11 non-Home edition(s)" -ForegroundColor Green
        $filteredImages | ForEach-Object {
            Write-Host "  $($_.Name) - $($_.Edition)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "INFO: No Windows 11 non-Home editions found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 6: Complex Filter Logic" -ForegroundColor Green
Write-Host "Testing complex filter with multiple conditions..." -ForegroundColor White

try {
    # Test complex filter: Professional or Enterprise editions with specific size
    $complexFilter = Get-WindowsImageList -ImagePath $esdFile -InclusionFilter { 
        ($_.Edition -like "*Pro*" -or $_.Edition -like "*Enterprise*") -and $_.Size -gt 3GB 
    } -Verbose
    
    if ($complexFilter) {
        Write-Host "SUCCESS: Found $($complexFilter.Count) Pro/Enterprise edition(s) > 3GB" -ForegroundColor Green
        $complexFilter | ForEach-Object {
            Write-Host "  $($_.Name) - $($_.Edition) - $([math]::Round($_.Size / 1GB, 2)) GB" -ForegroundColor Cyan
        }
    } else {
        Write-Host "INFO: No Pro/Enterprise editions > 3GB found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 7: Object Pipeline Testing" -ForegroundColor Green
Write-Host "Testing that returned objects work properly in PowerShell pipeline..." -ForegroundColor White

try {
    # Test pipeline operations
    $pipelineTest = Get-WindowsImageList -ImagePath $esdFile | 
        Where-Object { $_.Size -gt 2GB } | 
        Sort-Object Size -Descending | 
        Select-Object -First 2 Name, Edition, @{Name="SizeGB"; Expression={[math]::Round($_.Size / 1GB, 2)}}
    
    if ($pipelineTest) {
        Write-Host "SUCCESS: Pipeline operations work correctly" -ForegroundColor Green
        Write-Host "Top 2 largest images > 2GB:" -ForegroundColor Cyan
        $pipelineTest | Format-Table -AutoSize
    } else {
        Write-Host "INFO: No images > 2GB found for pipeline test" -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 8: JSON Export/Import Testing" -ForegroundColor Green
Write-Host "Testing that objects can be exported and imported as JSON..." -ForegroundColor White

try {
    # Test JSON export/import
    $jsonTest = Get-WindowsImageList -ImagePath $esdFile | Select-Object -First 1
    $jsonString = $jsonTest | ConvertTo-Json -Depth 3
    $importedObject = $jsonString | ConvertFrom-Json
    
    if ($importedObject -and $importedObject.Name -eq $jsonTest.Name) {
        Write-Host "SUCCESS: JSON export/import works correctly" -ForegroundColor Green
        Write-Host "Original: $($jsonTest.Name)" -ForegroundColor Cyan
        Write-Host "Imported: $($importedObject.Name)" -ForegroundColor Cyan
    } else {
        Write-Host "ERROR: JSON export/import failed" -ForegroundColor Red
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 9: Error Handling" -ForegroundColor Green
Write-Host "Testing error handling with invalid file..." -ForegroundColor White

try {
    # Test with non-existent file
    $errorTest = Get-WindowsImageList -ImagePath "C:\NonExistent\File.wim" -ErrorAction SilentlyContinue
    
    if ($errorTest) {
        Write-Host "ERROR: Should have failed with non-existent file" -ForegroundColor Red
    } else {
        Write-Host "SUCCESS: Properly handled non-existent file" -ForegroundColor Green
    }
} catch {
    Write-Host "SUCCESS: Exception properly thrown for invalid file" -ForegroundColor Green
}

Write-Host ""
Write-Host "TEST 10: Performance Test" -ForegroundColor Green
Write-Host "Testing performance of image list retrieval..." -ForegroundColor White

try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $perfTest = Get-WindowsImageList -ImagePath $esdFile
    $stopwatch.Stop()
    
    $duration = $stopwatch.Elapsed.TotalSeconds
    Write-Host "SUCCESS: Retrieved $($perfTest.Count) images in $([math]::Round($duration, 2)) seconds" -ForegroundColor Green
    
    if ($duration -lt 10) {
        Write-Host "PERFORMANCE: Good - under 10 seconds" -ForegroundColor Green
    } elseif ($duration -lt 30) {
        Write-Host "PERFORMANCE: Acceptable - under 30 seconds" -ForegroundColor Yellow
    } else {
        Write-Host "PERFORMANCE: Slow - over 30 seconds" -ForegroundColor Red
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== TEST SUMMARY ===" -ForegroundColor Yellow
Write-Host "All Get-WindowsImageList cmdlet tests completed!" -ForegroundColor Green
Write-Host "Check the results above for any failures or issues." -ForegroundColor Cyan
Write-Host ""
Write-Host "Key Features Tested:" -ForegroundColor White
Write-Host "• Basic image list retrieval" -ForegroundColor Green
Write-Host "• InclusionFilter with scriptblock" -ForegroundColor Green
Write-Host "• ExclusionFilter with scriptblock" -ForegroundColor Green
Write-Host "• Combined filter usage" -ForegroundColor Green
Write-Host "• Complex filter logic" -ForegroundColor Green
Write-Host "• PowerShell pipeline compatibility" -ForegroundColor Green
Write-Host "• JSON export/import capability" -ForegroundColor Green
Write-Host "• Error handling" -ForegroundColor Green
Write-Host "• Performance measurement" -ForegroundColor Green
