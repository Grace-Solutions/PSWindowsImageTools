# Simple test for UnattendXML functionality
Import-Module "$PSScriptRoot\..\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force

Write-Output "=== Testing UnattendXML Basic Functionality ==="

# Create configuration with samples
$config = New-UnattendXMLConfiguration -Template Basic -IncludeSamples
Write-Output "Components found: $($config.Components.Count)"
Write-Output "Elements found: $($config.Elements.Count)"

# Try to find HideEULAPage element
$hideEula = $config.FindElement('HideEULAPage')
if ($hideEula) {
    Write-Output "Found HideEULAPage: $($hideEula.InnerText)"
    Write-Output "XPath: $($config.GetXPath($hideEula))"
    
    # Try to modify it using friendly name
    $modified = $config | Set-UnattendXMLConfiguration -ElementName "HideEULAPage" -Value "false" -PassThru
    if ($modified) {
        $newValue = $modified.FindElement('HideEULAPage')
        Write-Output "Modified HideEULAPage to: $($newValue.InnerText)"
    }
} else {
    Write-Output "HideEULAPage not found"
}

# Show all elements for debugging
Write-Output "`nAll elements:"
$config.Elements | ForEach-Object {
    Write-Output "  $($_.Name): '$($_.Value)' - XPath: $($_.XPath)"
}

Write-Output "`nTest completed"
