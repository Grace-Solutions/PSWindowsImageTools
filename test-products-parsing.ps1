# Test Products parsing logic
Write-Host "Testing Products Parsing Logic" -ForegroundColor Cyan

# Sample products text from the catalog
$sampleText = "Windows 11 Client  version 24H2 and later  Servicing Drivers  Windows 11 Client S  Upgrade & Servicing Drivers"

Write-Host "Sample text: '$sampleText'" -ForegroundColor Yellow
Write-Host "Length: $($sampleText.Length)" -ForegroundColor Gray

# Examine the actual characters
Write-Host "`nCharacter analysis:" -ForegroundColor Cyan
for ($i = 0; $i -lt [Math]::Min(50, $sampleText.Length); $i++) {
    $char = $sampleText[$i]
    $code = [int][char]$char
    if ($char -eq ' ') {
        Write-Host "[$i]: SPACE (32)" -ForegroundColor Yellow
    } elseif ($code -eq 160) {
        Write-Host "[$i]: NON-BREAKING SPACE (160)" -ForegroundColor Red
    } elseif ($code -lt 32) {
        Write-Host "[$i]: CONTROL ($code)" -ForegroundColor Red
    } else {
        Write-Host "[$i]: '$char' ($code)" -ForegroundColor Gray
    }
}

# Test splitting by multiple spaces
Write-Host "`nSplitting by multiple spaces:" -ForegroundColor Cyan
$spaceSeparated = $sampleText.Split(@("  ", "   ", "    "), [System.StringSplitOptions]::RemoveEmptyEntries)
Write-Host "Count: $($spaceSeparated.Count)" -ForegroundColor Gray
for ($i = 0; $i -lt $spaceSeparated.Count; $i++) {
    Write-Host "  [$i]: '$($spaceSeparated[$i])'" -ForegroundColor White
}

# Test the expected result
$expectedProducts = @(
    "Windows 11 Client",
    "version 24H2 and later",
    "Servicing Drivers", 
    "Windows 11 Client S",
    "Upgrade & Servicing Drivers"
)

Write-Host "`nExpected products:" -ForegroundColor Green
$expectedProducts | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }

Write-Host "`nTest completed" -ForegroundColor Cyan
