param(
    [int]$TimeoutSeconds = 30,
    [string]$SearchPattern = "*template*",
    [string]$LogFile = "",
    [string]$ScriptDirectory = "",
    [switch]$DisableLogFile
)

# Allow disabling via environment variable
if ($env:PE_TOOLS_DISABLE_AUTO_OPEN -eq "true") {
    exit 0
}

# Initialize logging
$script:LogFileEnabled = -not $DisableLogFile
$script:LogFilePath = $null

# Configuration
$script:PollingIntervalMs = 500

if ($script:LogFileEnabled) {
    if ([string]::IsNullOrEmpty($LogFile)) {
        if (-not [string]::IsNullOrEmpty($ScriptDirectory) -and (Test-Path $ScriptDirectory)) {
            $script:LogFilePath = Join-Path $ScriptDirectory "AutoOpenDocument.log"
        }
        else {
            $scriptPath = $MyInvocation.MyCommand.Path
            if (-not [string]::IsNullOrEmpty($scriptPath)) {
                $scriptDir = Split-Path -Parent $scriptPath
                $script:LogFilePath = Join-Path $scriptDir "AutoOpenDocument.log"
            }
            else {
                $script:LogFilePath = "$env:TEMP\PE_Tools_AutoOpen.log"
            }
        }
    }
    else {
        $script:LogFilePath = $LogFile
    }

    try {
        $logDir = Split-Path -Parent $script:LogFilePath
        if (-not (Test-Path $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
    }
    catch {
        $script:LogFilePath = "$env:TEMP\PE_Tools_AutoOpen.log"
    }
}

function Write-Log {
    param([string]$Message)
    
    try {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $logMessage = "[$timestamp] $Message"
        
        try {
            Write-Host $logMessage -ErrorAction SilentlyContinue
        }
        catch {
            # Silently fail if Write-Host fails
        }
        
        if ($script:LogFileEnabled -and $null -ne $script:LogFilePath) {
            try {
                Add-Content -Path $script:LogFilePath -Value $logMessage -Encoding UTF8 -ErrorAction Stop
            }
            catch {
                # Silently fail if we can't write to log file
            }
        }
    }
    catch {
        # Silently fail if logging completely fails - don't crash the program
    }
}

try {
    Write-Log "=== SCRIPT START ==="
    Write-Log "Timeout: $TimeoutSeconds seconds, Pattern: '$SearchPattern'"

    # Load UI Automation assemblies
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes

    # Find Revit process
    $revitProcesses = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
    if ($null -ne $revitProcesses -and $revitProcesses.Count -gt 0) {
        Write-Log "Revit PID: $($revitProcesses[0].Id)"
    }
    else {
        Write-Log "WARNING: No Revit process"
    }

    # Simple polling loop - just search by name
    $startTime = Get-Date
    $timeout = (Get-Date).AddSeconds($TimeoutSeconds)
    $browserFound = $false
    $browserElement = $null
    $pollCount = 0

    Write-Log "Polling for 'Cloud First Model Browser'..."

    while ((Get-Date) -lt $timeout -and -not $browserFound) {
        $pollCount++
        
        try {
            # Simple search by name only - fastest approach
            $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                "Cloud First Model Browser"
            )
            
            $elements = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
                [System.Windows.Automation.TreeScope]::Descendants,
                $nameCondition
            )
            
            if ($null -ne $elements -and $elements.Count -gt 0) {
                Write-Log "Poll $pollCount`: FOUND $($elements.Count) element(s)!"
                
                foreach ($elem in $elements) {
                    Write-Log "  - ControlType: $($elem.Current.ControlType.ProgrammaticName)"
                    Write-Log "  - ProcessId: $($elem.Current.ProcessId)"
                    Write-Log "  - ClassName: '$($elem.Current.ClassName)'"
                    
                    # Accept the first match
                    $browserElement = $elem
                    $browserFound = $true
                    break
                }
            }
            else {
                # Silent poll
                if ($pollCount % 10 -eq 0) {
                    Write-Log "Poll $pollCount`: Still searching..."
                }
            }
        }
        catch {
            Write-Log "ERROR: $($_.Exception.Message)"
        }
    
        if (-not $browserFound) {
            Start-Sleep -Milliseconds $script:PollingIntervalMs
        }
    }

    Write-Log ""
    if (-not $browserFound) {
        Write-Log "=== RESULT: NOT FOUND ($pollCount polls) ==="
        exit 0
    }

    Write-Log "=== RESULT: FOUND! ($pollCount polls) ==="
    Write-Log "Browser properties:"
    Write-Log "  Name: '$($browserElement.Current.Name)'"
    Write-Log "  ControlType: $($browserElement.Current.ControlType.ProgrammaticName)"
    Write-Log "  ProcessId: $($browserElement.Current.ProcessId)"
    Write-Log "  NativeWindowHandle: $($browserElement.Current.NativeWindowHandle)"
    
    # Check if we can access it
    try {
        $testAccess = $browserElement.Current.IsEnabled
        Write-Log "  IsEnabled: $testAccess"
    }
    catch {
        Write-Log "  ERROR: Cannot access element properties: $($_.Exception.Message)"
    }

    Write-Log "=== SCRIPT END ==="
    exit 0
}
catch {
    Write-Log "FATAL: $($_.Exception.Message)"
    Write-Log "Stack: $($_.ScriptStackTrace)"
    exit 1
}
