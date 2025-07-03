# Example ADK workflow demonstrating the enhanced process monitoring
# This script shows a complete workflow from ADK installation to component management

[CmdletBinding()]
param(
    [switch]$ForceReinstall,
    [string]$CustomPath = ""
)

Write-Output "=== Enhanced ADK Workflow Example ==="
Write-Output "This example demonstrates the complete ADK management workflow with enhanced process monitoring"
Write-Output ""

# Check for administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Error "Administrator privileges are required for ADK management"
    Write-Output "Please run this script as Administrator"
    exit 1
}

# Import the module
try {
    Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Output "✓ Module imported successfully"
} catch {
    Write-Error "✗ Failed to import module: $($_.Exception.Message)"
    exit 1
}

Write-Output "`n=== Step 1: ADK Installation Management ==="

try {
    # Check current ADK status
    Write-Output "Checking current ADK installations..."
    $existingADKs = Get-ADKInstallation
    
    if ($existingADKs) {
        Write-Output "Found existing ADK installations:"
        foreach ($adk in $existingADKs) {
            Write-Output "  - $($adk.DisplayName) v$($adk.Version)"
            Write-Output "    WinPE: $($adk.HasWinPEAddon), Deployment Tools: $($adk.HasDeploymentTools)"
        }
    } else {
        Write-Output "No existing ADK installations found"
    }
    
    # Install or verify ADK
    if ($ForceReinstall) {
        Write-Output "`nForce reinstalling ADK..."
        $installParams = @{
            Force = $true
            IncludeWinPE = $true
            IncludeDeploymentTools = $true
        }
        
        if ($CustomPath) {
            $installParams.InstallPath = $CustomPath
            Write-Output "Using custom installation path: $CustomPath"
        }
        
        Write-Output "Starting ADK installation with enhanced monitoring..."
        Write-Output "You will see:"
        Write-Output "  - Command line being executed"
        Write-Output "  - Process ID and runtime updates every 10 seconds"
        Write-Output "  - Progress percentage based on timeout"
        Write-Output ""
        
        $adk = Install-ADK @installParams
    } else {
        Write-Output "`nEnsuring ADK is available (default behavior: skip if present)..."
        $adk = Install-ADK -IncludeWinPE -IncludeDeploymentTools
    }
    
    if ($adk) {
        Write-Output "✓ ADK is ready: $($adk.DisplayName) v$($adk.Version)"
        Write-Output "  Installation path: $($adk.InstallationPath.FullName)"
        Write-Output "  Supported architectures: $($adk.SupportedArchitectures -join ', ')"
    } else {
        Write-Error "✗ Failed to ensure ADK availability"
        exit 1
    }
    
} catch {
    Write-Error "✗ ADK installation management failed: $($_.Exception.Message)"
    exit 1
}

Write-Output "`n=== Step 2: Component Discovery ==="

try {
    Write-Output "Discovering available WinPE Optional Components..."
    
    # Get all components
    $allComponents = Get-WinPEOptionalComponent -ADKInstallation $adk
    
    if ($allComponents) {
        Write-Output "✓ Found $($allComponents.Count) optional components"
        
        # Show category breakdown
        $categories = $allComponents | Group-Object Category | Sort-Object Name
        Write-Output "`nComponent categories:"
        foreach ($category in $categories) {
            Write-Output "  - $($category.Name): $($category.Count) components"
        }
        
        # Get specific component types for demonstration
        $scriptingComponents = $allComponents | Where-Object { $_.Category -eq 'Scripting' }
        $networkingComponents = $allComponents | Where-Object { $_.Category -eq 'Networking' }
        
        Write-Output "`nExample components:"
        if ($scriptingComponents) {
            Write-Output "  Scripting components ($($scriptingComponents.Count)):"
            $scriptingComponents | ForEach-Object { Write-Output "    - $($_.Name) ($($_.SizeFormatted))" }
        }
        
        if ($networkingComponents) {
            Write-Output "  Networking components ($($networkingComponents.Count)):"
            $networkingComponents | Select-Object -First 3 | ForEach-Object { Write-Output "    - $($_.Name) ($($_.SizeFormatted))" }
            if ($networkingComponents.Count -gt 3) {
                Write-Output "    ... and $($networkingComponents.Count - 3) more"
            }
        }
    } else {
        Write-Warning "⚠ No optional components found"
    }
    
} catch {
    Write-Error "✗ Component discovery failed: $($_.Exception.Message)"
}

Write-Output "`n=== Step 3: Process Monitoring Demonstration ==="

Write-Output "The enhanced process monitoring provides:"
Write-Output "  ✓ Command line display for transparency"
Write-Output "  ✓ Process ID tracking"
Write-Output "  ✓ Runtime updates every 10 seconds"
Write-Output "  ✓ Progress percentage with timeout awareness"
Write-Output "  ✓ Automatic timeout handling (60 min for install, 30 min for uninstall)"
Write-Output "  ✓ Graceful process termination on timeout"
Write-Output ""
Write-Output "Example monitoring output during ADK installation:"
Write-Output "  [10:30:15] Process 1234 running for 2.5 minutes (timeout: 60 min)"
Write-Output "  [10:30:25] Command line: C:\Temp\adksetup.exe /quiet /norestart /features OptionId.DeploymentTools OptionId.WindowsPreinstallationEnvironment"
Write-Output "  [10:30:35] Process 1234 running for 2.8 minutes (timeout: 60 min)"

Write-Output "`n=== Step 4: Pipeline Integration Example ==="

try {
    Write-Output "Demonstrating pipeline integration..."
    
    # Pipeline example: ADK → Components → Filter
    $pipelineResult = Get-ADKInstallation -Latest -RequireWinPE | 
        Get-WinPEOptionalComponent -Category Scripting | 
        Where-Object { $_.Name -like "*PowerShell*" -or $_.Name -like "*NetFx*" }
    
    if ($pipelineResult) {
        Write-Output "✓ Pipeline successful: Found $($pipelineResult.Count) scripting components"
        $pipelineResult | ForEach-Object { Write-Output "  - $($_.Name)" }
    } else {
        Write-Output "⚠ No matching components found in pipeline"
    }
    
} catch {
    Write-Error "✗ Pipeline demonstration failed: $($_.Exception.Message)"
}

Write-Output "`n=== Step 5: Cleanup Options ==="

Write-Output "ADK cleanup options:"
Write-Output "  - Uninstall-ADK                    # Remove latest ADK with confirmation"
Write-Output "  - Uninstall-ADK -Force             # Remove latest ADK silently"
Write-Output "  - Uninstall-ADK -All               # Remove all ADK installations with confirmation"
Write-Output "  - Uninstall-ADK -All -Force        # Remove all ADK installations silently"
Write-Output ""
Write-Output "Note: Uninstallation also uses enhanced process monitoring with:"
Write-Output "  - Command line display"
Write-Output "  - 30-minute timeout for uninstall operations"
Write-Output "  - Progress tracking and runtime updates"

Write-Output "`n=== Workflow Summary ==="
Write-Output "✓ ADK Management: Automatic installation with skip-if-present default"
Write-Output "✓ Component Discovery: Comprehensive component scanning and categorization"
Write-Output "✓ Process Monitoring: Enhanced tracking with command line display and timeouts"
Write-Output "✓ Pipeline Integration: Seamless cmdlet chaining"
Write-Output "✓ Error Handling: Robust error handling with detailed logging"
Write-Output ""
Write-Output "Key improvements:"
Write-Output "  • Default skip-if-present behavior (no counter-intuitive parameters)"
Write-Output "  • Enhanced process monitoring with command line display"
Write-Output "  • Runtime tracking with 10-second update intervals"
Write-Output "  • Automatic timeout handling (60 min install, 30 min uninstall)"
Write-Output "  • Process ID tracking and graceful termination"
Write-Output ""
Write-Output "Complete ADK management workflow ready for production use!"
