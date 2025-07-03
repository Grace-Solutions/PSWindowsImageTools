# Test script for new UnattendXML cmdlets with enhanced navigation
param(
    [switch]$Verbose
)

if ($Verbose) {
    $VerbosePreference = 'Continue'
}

Write-Output "=== Testing Enhanced UnattendXML Cmdlets ==="

# Import the module
try {
    Import-Module "$PSScriptRoot\..\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Output "Module imported successfully"
} catch {
    Write-Error "Failed to import module: $_"
    exit 1
}

# Create test directory
$testDir = "C:\Temp\UnattendXMLTest"
if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

Write-Output "`n=== Test 1: Creating Basic UnattendXML Configuration ==="

try {
    $config = New-UnattendXMLConfiguration -Template Basic -Architecture amd64 -Verbose
    Write-Output "Created UnattendXML configuration:"
    Write-Output "  Configuration passes: $($config.ConfigurationPasses -join ', ')"
    Write-Output "  Components count: $($config.Components.Count)"
    Write-Output "  Elements count: $($config.Elements.Count)"
    Write-Output "  Is Modified: $($config.IsModified)"
} catch {
    Write-Error "Failed to create UnattendXML configuration: $_"
}

Write-Output "`n=== Test 2: Adding Sample Components ==="

try {
    $configWithSamples = New-UnattendXMLConfiguration -Template Basic -IncludeSamples -Verbose
    Write-Output "Created configuration with samples:"
    Write-Output "  Configuration passes: $($configWithSamples.ConfigurationPasses -join ', ')"
    Write-Output "  Components count: $($configWithSamples.Components.Count)"
    
    if ($configWithSamples.Components.Count -gt 0) {
        Write-Output "  Sample components:"
        $configWithSamples.Components | ForEach-Object {
            Write-Output "    - $($_.Name) ($($_.Pass)) - XPath: $($_.XPath)"
        }
    }
} catch {
    Write-Error "Failed to create configuration with samples: $_"
}

Write-Output "`n=== Test 3: Exploring XML Elements with XPath ==="

try {
    $config = $configWithSamples
    Write-Output "XML Elements in configuration:"
    
    # Show first 10 elements with their XPath
    $config.Elements | Select-Object -First 10 | ForEach-Object {
        $valuePreview = if ($_.Value.Length -gt 30) { $_.Value.Substring(0, 30) + "..." } else { $_.Value }
        Write-Output "  - $($_.Name): '$valuePreview' - XPath: $($_.XPath)"
    }
    
    if ($config.Elements.Count -gt 10) {
        Write-Output "  ... and $($config.Elements.Count - 10) more elements"
    }
} catch {
    Write-Error "Failed to explore XML elements: $_"
}

Write-Output "`n=== Test 4: Setting Values Using Friendly Names ==="

try {
    # Test setting a value using friendly element name
    $modifiedConfig = $config | Set-UnattendXMLConfiguration -ElementName "HideEULAPage" -Value "false" -PassThru -Verbose
    Write-Output "Modified configuration using friendly name:"
    Write-Output "  Is Modified: $($modifiedConfig.IsModified)"
    
    # Find the element to verify the change
    $hideEulaElement = $modifiedConfig.FindElement("HideEULAPage")
    if ($hideEulaElement) {
        Write-Output "  HideEULAPage value: $($hideEulaElement.InnerText)"
        Write-Output "  Element XPath: $($modifiedConfig.GetXPath($hideEulaElement))"
    }
} catch {
    Write-Error "Failed to set value using friendly name: $_"
}

Write-Output "`n=== Test 5: Setting Values Using XPath ==="

try {
    # Get the XPath from the previous operation and use it directly
    $hideEulaElement = $modifiedConfig.FindElement("HideEULAPage")
    if ($hideEulaElement) {
        $xpath = $modifiedConfig.GetXPath($hideEulaElement)
        Write-Output "Using XPath: $xpath"
        
        $xpathModified = $modifiedConfig | Set-UnattendXMLConfiguration -XPath $xpath -Value "true" -PassThru -Verbose
        Write-Output "Modified configuration using XPath:"
        Write-Output "  Is Modified: $($xpathModified.IsModified)"
        
        # Verify the change
        $verifyElement = $xpathModified.XmlDocument.SelectSingleNode($xpath, $xpathModified.NamespaceManager)
        if ($verifyElement) {
            Write-Output "  HideEULAPage value after XPath modification: $($verifyElement.InnerText)"
        }
    }
} catch {
    Write-Error "Failed to set value using XPath: $_"
}

Write-Output "`n=== Test 6: Creating New Elements ==="

try {
    # Create a new element that doesn't exist
    $newElementConfig = $config | Set-UnattendXMLConfiguration -ElementName "ComputerName" -Value "TEST-PC" -Pass "specialize" -ComponentName "Microsoft-Windows-Shell-Setup" -CreateIfNotExists -PassThru -Verbose
    Write-Output "Created new element:"
    Write-Output "  Is Modified: $($newElementConfig.IsModified)"
    
    # Find the new element
    $computerNameElement = $newElementConfig.FindElement("ComputerName", "specialize", "Microsoft-Windows-Shell-Setup")
    if ($computerNameElement) {
        Write-Output "  ComputerName value: $($computerNameElement.InnerText)"
        Write-Output "  Element XPath: $($newElementConfig.GetXPath($computerNameElement))"
    }
} catch {
    Write-Error "Failed to create new element: $_"
}

Write-Output "`n=== Test 7: Exporting with Different Encodings ==="

try {
    $testFile = Join-Path $testDir "unattend-utf8.xml"
    $exportedFile = $config | Export-UnattendXMLConfiguration -Path $testFile -Encoding UTF8 -PassThru -Verbose
    Write-Output "Exported UnattendXML configuration:"
    Write-Output "  File: $($exportedFile.FullName)"
    Write-Output "  Size: $($exportedFile.Length) bytes"
    Write-Output "  Encoding: UTF8"
    
    # Test different encoding
    $testFileUtf16 = Join-Path $testDir "unattend-utf16.xml"
    $exportedFileUtf16 = $config | Export-UnattendXMLConfiguration -Path $testFileUtf16 -Encoding UTF16 -PassThru -Verbose
    Write-Output "  UTF16 File: $($exportedFileUtf16.FullName)"
    Write-Output "  UTF16 Size: $($exportedFileUtf16.Length) bytes"
} catch {
    Write-Error "Failed to export UnattendXML configuration: $_"
}

Write-Output "`n=== Test 8: Loading and Exploring Configuration ==="

try {
    $loadedConfig = Get-UnattendXMLConfiguration -Path $testFile -ShowComponents -ShowElements -ElementFilter "*OOBE*" -Verbose
    Write-Output "Loaded UnattendXML configuration:"
    Write-Output "  Source File: $($loadedConfig.SourceFilePath)"
    Write-Output "  Configuration passes: $($loadedConfig.ConfigurationPasses -join ', ')"
    Write-Output "  Components count: $($loadedConfig.Components.Count)"
    Write-Output "  Elements count: $($loadedConfig.Elements.Count)"
} catch {
    Write-Error "Failed to load UnattendXML configuration: $_"
}

Write-Output "`n=== Test 9: XML Navigation Examples ==="

try {
    Write-Output "Demonstrating easy XML navigation:"
    
    # Find all OOBE-related elements
    $oobeElements = $loadedConfig.Elements | Where-Object { $_.Name -like "*OOBE*" -or $_.FullName -like "*OOBE*" }
    Write-Output "  OOBE-related elements found: $($oobeElements.Count)"
    
    foreach ($element in $oobeElements) {
        Write-Output "    - $($element.Name): '$($element.Value)' - XPath: $($element.XPath)"
    }
    
    # Show how to get XPath for any element
    if ($loadedConfig.Components.Count -gt 0) {
        $firstComponent = $loadedConfig.Components[0]
        Write-Output "  First component XPath: $($firstComponent.XPath)"
        Write-Output "  Component details: $($firstComponent.Name) in $($firstComponent.Pass) pass"
    }
} catch {
    Write-Error "Failed XML navigation examples: $_"
}

Write-Output "`n=== Test 10: Validation and Error Handling ==="

try {
    # Test validation
    $validationErrors = $loadedConfig.Validate()
    if ($validationErrors.Count -eq 0) {
        Write-Output "✅ Configuration is valid"
    } else {
        Write-Output "❌ Validation errors:"
        $validationErrors | ForEach-Object { Write-Output "  - $_" }
    }
    
    # Test error handling with invalid XPath
    try {
        $loadedConfig | Set-UnattendXMLConfiguration -XPath "//invalid[xpath" -Value "test" -PassThru
    } catch {
        Write-Output "✅ Properly caught invalid XPath error: $($_.Exception.Message)"
    }
} catch {
    Write-Error "Failed validation test: $_"
}

Write-Output "`n=== Test Summary ==="
Write-Output "✅ UnattendXML configuration creation tested"
Write-Output "✅ Sample components addition tested"
Write-Output "✅ XML element exploration with XPath tested"
Write-Output "✅ Friendly name element modification tested"
Write-Output "✅ XPath-based element modification tested"
Write-Output "✅ New element creation tested"
Write-Output "✅ Multiple encoding export tested"
Write-Output "✅ Configuration loading and exploration tested"
Write-Output "✅ XML navigation examples demonstrated"
Write-Output "✅ Validation and error handling tested"
Write-Output ""
Write-Output "Key Features Demonstrated:"
Write-Output "• Easy element access by friendly names (no complex XPath required)"
Write-Output "• Automatic XPath generation for any element"
Write-Output "• Support for both friendly names and XPath for maximum flexibility"
Write-Output "• Multiple encoding options for XML export"
Write-Output "• Enhanced XML navigation and exploration capabilities"
Write-Output "• Proper namespace handling for Windows Unattend XML"

Write-Output "`nEnhanced UnattendXML cmdlet testing completed!"

# Cleanup
try {
    Remove-Item $testDir -Recurse -Force
    Write-Output "Test files cleaned up"
} catch {
    Write-Warning "Failed to clean up test files: $_"
}
