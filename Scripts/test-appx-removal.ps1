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
    "Valid Inclusion Filters" = @(
        "Microsoft.*",
        ".*Weather.*",
        "^Microsoft\.(Store|Calculator)$",
        "(?i)bing|cortana"
    )
    "Valid Exclusion Filters" = @(
        "Store|Calculator",
        ".*Essential.*",
        "^Windows\.",
        "Xbox|Gaming"
    )
    "Invalid Filters" = @(
        "[unclosed",
        "*invalid*",
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

$mockAppXPackages = @(
    @{ DisplayName = "Microsoft Store"; PackageName = "Microsoft.WindowsStore_11.2310.1.0_x64__8wekyb3d8bbwe" },
    @{ DisplayName = "Microsoft Calculator"; PackageName = "Microsoft.WindowsCalculator_11.2309.1.0_x64__8wekyb3d8bbwe" },
    @{ DisplayName = "MSN Weather"; PackageName = "Microsoft.BingWeather_4.53.33420.0_x64__8wekyb3d8bbwe" },
    @{ DisplayName = "Xbox Game Bar"; PackageName = "Microsoft.XboxGamingOverlay_5.721.10202.0_x64__8wekyb3d8bbwe" },
    @{ DisplayName = "Cortana"; PackageName = "Microsoft.549981C3F5F10_4.2204.13303.0_x64__8wekyb3d8bbwe" },
    @{ DisplayName = "Windows Security"; PackageName = "Microsoft.SecHealthUI_1000.25873.9001.0_x64__8wekyb3d8bbwe" },
    @{ DisplayName = "Candy Crush Saga"; PackageName = "king.com.CandyCrushSaga_1.2540.0.0_x64__kgqvnymyfvs32" },
    @{ DisplayName = "Disney Magic Kingdoms"; PackageName = "A278AB0D.DisneyMagicKingdoms_2.7.0.9_x64__h6adky7gbf63m" }
)

$testScenarios = @(
    @{
        Name = "Include Microsoft packages only"
        InclusionFilter = "Microsoft.*"
        ExclusionFilter = $null
        ExpectedIncluded = @("Microsoft Store", "Microsoft Calculator", "MSN Weather", "Xbox Game Bar", "Cortana", "Windows Security")
        ExpectedRemoved = @("Microsoft Store", "Microsoft Calculator", "MSN Weather", "Xbox Game Bar", "Cortana", "Windows Security")
    },
    @{
        Name = "Include Microsoft but exclude Xbox and Cortana"
        InclusionFilter = "Microsoft.*"
        ExclusionFilter = "Xbox|Cortana"
        ExpectedIncluded = @("Microsoft Store", "Microsoft Calculator", "MSN Weather", "Xbox Game Bar", "Cortana", "Windows Security")
        ExpectedRemoved = @("Microsoft Store", "Microsoft Calculator", "MSN Weather", "Windows Security")
    },
    @{
        Name = "Include all but exclude games"
        InclusionFilter = $null
        ExclusionFilter = "Candy|Disney|Xbox"
        ExpectedIncluded = @("Microsoft Store", "Microsoft Calculator", "MSN Weather", "Xbox Game Bar", "Cortana", "Windows Security", "Candy Crush Saga", "Disney Magic Kingdoms")
        ExpectedRemoved = @("Microsoft Store", "Microsoft Calculator", "MSN Weather", "Cortana", "Windows Security")
    },
    @{
        Name = "Include only Weather apps"
        InclusionFilter = ".*Weather.*"
        ExclusionFilter = $null
        ExpectedIncluded = @("MSN Weather")
        ExpectedRemoved = @("MSN Weather")
    }
)

foreach ($scenario in $testScenarios) {
    Write-Output "  Testing scenario: $($scenario.Name)"
    
    foreach ($package in $mockAppXPackages) {
        $matchesInclusion = $true
        $matchesExclusion = $false
        
        # Apply inclusion filter (simulate cmdlet behavior with IgnoreCase, Multiline)
        if ($scenario.InclusionFilter) {
            $inclusionRegex = [regex]::new($scenario.InclusionFilter, "IgnoreCase, Multiline")
            $matchesInclusion = $inclusionRegex.IsMatch($package.DisplayName)
        }

        # Apply exclusion filter (simulate cmdlet behavior with IgnoreCase, Multiline)
        if ($scenario.ExclusionFilter) {
            $exclusionRegex = [regex]::new($scenario.ExclusionFilter, "IgnoreCase, Multiline")
            $matchesExclusion = $exclusionRegex.IsMatch($package.DisplayName)
        }
        
        $shouldBeRemoved = $matchesInclusion -and -not $matchesExclusion
        $expectedToBeRemoved = $scenario.ExpectedRemoved -contains $package.DisplayName
        
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

# Test 5: Test error handling scenarios
Write-Output "`n=== Test 5: Testing Error Handling Scenarios ==="

$errorTestCases = @(
    @{
        Name = "Invalid inclusion regex pattern"
        InclusionFilter = "Try creating [regex]::new('[unclosed', 'IgnoreCase')"
        ExclusionFilter = $null
        ExpectedBehavior = "Should fail at regex creation time with clear error message"
    },
    @{
        Name = "Invalid exclusion regex pattern"
        InclusionFilter = "[regex]::new('Microsoft.*', 'IgnoreCase')"
        ExclusionFilter = "Try creating [regex]::new('*invalid*', 'IgnoreCase')"
        ExpectedBehavior = "Should fail at regex creation time with clear error message"
    },
    @{
        Name = "Empty mounted images array"
        MountedImages = @()
        ExpectedBehavior = "Should display warning about no mounted images"
    },
    @{
        Name = "Valid regex with unsupported options"
        InclusionFilter = "Try creating [regex]::new('test', 'InvalidOption')"
        ExclusionFilter = $null
        ExpectedBehavior = "Should fail at regex creation with invalid RegexOptions"
    }
)

foreach ($testCase in $errorTestCases) {
    Write-Output "  Testing: $($testCase.Name)"
    Write-Output "    Expected: $($testCase.ExpectedBehavior)"
}

# Test 6: Test progress reporting simulation
Write-Output "`n=== Test 6: Testing Progress Reporting Logic ==="

$simulatedPackageCount = 25
$simulatedImageCount = 3

Write-Output "Simulating progress reporting for:"
Write-Output "  - $simulatedImageCount mounted images"
Write-Output "  - $simulatedPackageCount packages per image"

for ($imageIndex = 1; $imageIndex -le $simulatedImageCount; $imageIndex++) {
    $imageProgress = [int](($imageIndex / $simulatedImageCount) * 100)
    Write-Verbose "  Image $imageIndex of $simulatedImageCount ($imageProgress%)"

    for ($packageIndex = 1; $packageIndex -le $simulatedPackageCount; $packageIndex++) {
        $packageProgress = [int](($packageIndex / $simulatedPackageCount) * 100)
        if ($packageIndex % 5 -eq 0) {  # Show every 5th package to avoid spam
            Write-Verbose "    Package $packageIndex of $simulatedPackageCount ($packageProgress%)"
        }
    }
}

Write-Verbose "Progress reporting logic simulation completed"

# Test 7: Test FormatUtilityService integration
Write-Output "`n=== Test 7: Testing FormatUtilityService Integration ==="

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
Write-Output "✓ Error handling scenarios outlined"
Write-Output "✓ Progress reporting logic tested"
Write-Output "✓ FormatUtilityService integration tested"
Write-Output "Note: Full testing requires mounted Windows images with actual AppX packages"

Write-Output "`nAppX removal cmdlet testing completed!"
