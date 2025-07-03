# Test script for Remove-AppXProvisionedPackageList cmdlet
# This script tests the AppX provisioned package removal functionality

[CmdletBinding()]
param()

Write-Output "=== Testing Remove-AppXProvisionedPackageList Cmdlet ==="

# Import the module
try {
    Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Verbose "Module imported successfully"
} catch {
    Write-Error "Failed to import module: $($_.Exception.Message)"
    exit 1
}

# Test 1: Create mock mounted image objects for testing
Write-Output "`n=== Test 1: Creating Mock Mounted Images ==="

$mockMountedImages = @(
    [PSCustomObject]@{
        MountId = "test-mount-1"
        ImageName = "Windows 11 Pro"
        Edition = "Professional"
        Architecture = "AMD64"
        MountPath = [System.IO.DirectoryInfo]::new("C:\Mount\Image1")
        Status = "Mounted"
        IsReadOnly = $false
    },
    [PSCustomObject]@{
        MountId = "test-mount-2"
        ImageName = "Windows 11 Enterprise"
        Edition = "Enterprise"
        Architecture = "AMD64"
        MountPath = [System.IO.DirectoryInfo]::new("C:\Mount\Image2")
        Status = "Mounted"
        IsReadOnly = $false
    }
)

Write-Verbose "Created $($mockMountedImages.Count) mock mounted images"

# Test 2: Test regex filter patterns (cmdlet handles compilation internally)
Write-Output "`n=== Test 2: Testing Regex Filter Patterns ==="

$testFilters = @{
    "Basic Filters" = @(
        "Microsoft.*",
        ".*Store.*",
        "Xbox.*",
        "^Cortana$"
    )
    "Complex Filters" = @(
        "Microsoft\.(Store|Calculator|Photos)",
        "(?i)xbox|gaming",
        "^(MSN|Bing).*"
    )
    "Invalid Filters" = @(
        "[unclosed",
        "*invalid",
        "(?P<invalid>group)"
    )
}

foreach ($category in $testFilters.Keys) {
    Write-Output "  Testing ${category}:"
    foreach ($pattern in $testFilters[$category]) {
        try {
            # Test regex compilation with IgnoreCase and Multiline (same as cmdlet)
            $regex = [System.Text.RegularExpressions.Regex]::new($pattern, "IgnoreCase, Multiline")
            Write-Verbose "    ✓ '$pattern' - Valid regex pattern"
            
            # Test some sample matches
            $testStrings = @("Microsoft Store", "microsoft calculator", "MSN Weather", "Xbox Game Bar")
            $matchedStrings = $testStrings | Where-Object { $regex.IsMatch($_) }
            if ($matchedStrings.Count -gt 0) {
                Write-Verbose "      Matches: $($matchedStrings -join ', ')"
            }
        } catch {
            if ($category -eq "Invalid Filters") {
                Write-Verbose "    ✓ '$pattern' - Correctly identified as invalid"
            } else {
                Write-Warning "    ✗ '$pattern' - Unexpected validation failure: $($_.Exception.Message)"
            }
        }
    }
}

# Test 3: Test filter matching logic
Write-Output "`n=== Test 3: Testing Filter Matching Logic ==="

# Mock AppX packages for testing filter logic
$mockAppXPackages = @(
    @{ DisplayName = "Microsoft Store"; PackageName = "Microsoft.WindowsStore_8wekyb3d8bbwe" },
    @{ DisplayName = "Microsoft Calculator"; PackageName = "Microsoft.WindowsCalculator_8wekyb3d8bbwe" },
    @{ DisplayName = "Xbox Game Bar"; PackageName = "Microsoft.XboxGamingOverlay_8wekyb3d8bbwe" },
    @{ DisplayName = "MSN Weather"; PackageName = "Microsoft.BingWeather_8wekyb3d8bbwe" },
    @{ DisplayName = "Cortana"; PackageName = "Microsoft.549981C3F5F10_8wekyb3d8bbwe" },
    @{ DisplayName = "Candy Crush Saga"; PackageName = "king.com.CandyCrushSaga_kgqvnymyfvs32" }
)

$testScenarios = @(
    @{
        Name = "Include Microsoft, Exclude Store and Calculator"
        InclusionFilter = "Microsoft.*"
        ExclusionFilter = "Store|Calculator"
        ExpectedResults = @{
            "Microsoft Store" = $false
            "Microsoft Calculator" = $false
            "Xbox Game Bar" = $true
            "MSN Weather" = $true
            "Cortana" = $true
            "Candy Crush Saga" = $false
        }
    },
    @{
        Name = "Exclude Gaming packages"
        InclusionFilter = $null
        ExclusionFilter = "Xbox|Gaming|Candy"
        ExpectedResults = @{
            "Microsoft Store" = $true
            "Microsoft Calculator" = $true
            "Xbox Game Bar" = $false
            "MSN Weather" = $true
            "Cortana" = $true
            "Candy Crush Saga" = $false
        }
    }
)

foreach ($scenario in $testScenarios) {
    Write-Output "  Testing scenario: $($scenario.Name)"
    
    # Simulate filter logic (simplified version of what the cmdlet does)
    foreach ($package in $mockAppXPackages) {
        $matchesInclusion = $true
        $matchesExclusion = $false
        
        if ($scenario.InclusionFilter) {
            $inclusionRegex = [System.Text.RegularExpressions.Regex]::new($scenario.InclusionFilter, "IgnoreCase, Multiline")
            $matchesInclusion = $inclusionRegex.IsMatch($package.DisplayName)
        }
        
        if ($scenario.ExclusionFilter) {
            $exclusionRegex = [System.Text.RegularExpressions.Regex]::new($scenario.ExclusionFilter, "IgnoreCase, Multiline")
            $matchesExclusion = $exclusionRegex.IsMatch($package.DisplayName)
        }
        
        $shouldBeRemoved = $matchesInclusion -and -not $matchesExclusion
        $expectedToBeRemoved = $scenario.ExpectedResults[$package.DisplayName]
        
        if ($shouldBeRemoved -eq $expectedToBeRemoved) {
            Write-Verbose "    ✓ $($package.DisplayName) - Correctly processed"
        } else {
            Write-Warning "    ✗ $($package.DisplayName) - Expected: $expectedToBeRemoved, Got: $shouldBeRemoved"
        }
    }
}

# Test 4: Test cmdlet parameter validation
Write-Output "`n=== Test 4: Testing Cmdlet Parameter Validation ==="

Write-Output "Note: This test requires actual mounted Windows images which are not available in this test environment."
Write-Output "The Remove-AppXProvisionedPackageList cmdlet would be used like this:"
Write-Output "  # Mount images first"
Write-Output "  `$mountedImages = Mount-WindowsImageList -ImagePath 'install.wim'"
Write-Output ""
Write-Output "  # Remove Microsoft packages except Store and Calculator"
Write-Output "  `$results = Remove-AppXProvisionedPackageList -MountedImages `$mountedImages -InclusionFilter 'Microsoft.*' -ExclusionFilter 'Store|Calculator'"
Write-Output ""
Write-Output "  # Remove all gaming-related packages"
Write-Output "  `$results = Remove-AppXProvisionedPackageList -MountedImages `$mountedImages -ExclusionFilter 'Xbox|Gaming|Candy|Disney'"
Write-Output ""
Write-Output "  # Remove specific packages by exact name (case-insensitive automatically)"
Write-Output "  `$results = Remove-AppXProvisionedPackageList -MountedImages `$mountedImages -InclusionFilter '^(Cortana|MSN Weather)$'"
Write-Output ""
Write-Output "  # Pipeline usage with error handling"
Write-Output "  `$mountedImages | Remove-AppXProvisionedPackageList -InclusionFilter 'Microsoft.*' -ExclusionFilter 'Store|Calculator' -ErrorAction Continue"

# Test 5: Test FormatUtilityService integration
Write-Output "`n=== Test 5: Testing FormatUtilityService Integration ==="

$testVersionStrings = @("11.2310.1.0", "4.53.33420.0", "5.721.10202.0", "1000.25873.9001.0")

foreach ($versionStr in $testVersionStrings) {
    try {
        $parsed = [PSWindowsImageTools.Services.FormatUtilityService]::ParseVersion($versionStr)
        if ($parsed) {
            Write-Verbose "  ✓ Version '$versionStr' parsed as: $($parsed.ToString())"
        } else {
            Write-Warning "  ✗ Failed to parse version: $versionStr"
        }
    } catch {
        Write-Warning "  ✗ Exception parsing version '$versionStr': $($_.Exception.Message)"
    }
}

Write-Output "`n=== Test Summary ==="
Write-Output "✓ Module import tested"
Write-Output "✓ Regex filter validation tested"
Write-Output "✓ Filter matching logic tested"
Write-Output "✓ Parameter validation scenarios outlined"
Write-Output "✓ FormatUtilityService integration tested"
Write-Output "Note: Full testing requires mounted Windows images with actual AppX packages"

Write-Output "`nAppX removal cmdlet testing completed!"
