param(
    [string]$Configuration,
    [string]$RevitVersion,
    [string]$MSBuildProjectDirectory,
    [string]$AppData
)

if ($env:GITHUB_ACTIONS -eq "true" -or $env:GITHUB_RUN_ID)
{
    Write-Host ""
    Write-Host "****** SKIPPING LOCAL DEV ADDIN COPY *******"
    Write-Host "****** env:GITHUB_ACTIONS: $env:GITHUB_ACTIONS *******"
    Write-Host "****** env:GITHUB_RUN_ID: $env:GITHUB_RUN_ID *******"
    Write-Host ""
    exit 0 # Exit execution with success code
}

# Define path variables
$BuildOutputPath = "$MSBuildProjectDirectory\bin\Debug\$RevitVersion"
$RvtAddinsAddinPath = "$AppData\Autodesk\REVIT\Addins\$RevitVersion"
$RvtAddinsDllPath = "$AppData\Autodesk\REVIT\Addins\$RevitVersion\PE_Tools"
$BundlePath = "C:\ProgramData\Autodesk\ApplicationPlugins\PE_Tools.bundle"

# Echo parameters information
Write-Host "****** LOCAL DEV ADDIN COPY *******"
Write-Host "* Copying **$Configuration** build output (.dll and .addin)"
Write-Host "* from **$BuildOutputPath**"
Write-Host "* to **$RvtAddinsAddinPath**"

# Create dll directory if it doesn't exist
if (-not (Test-Path $RvtAddinsDllPath))
{
    New-Item -ItemType Directory -Path $RvtAddinsDllPath -Force | Out-Null
    Write-Host "*     - Created dir: $RvtAddinsDllPath"
}

# Copy .addin files
Copy-Item -Path "$MSBuildProjectDirectory\*.addin" -Destination $RvtAddinsAddinPath -Force
Write-Host "*     - Copied .addin files to: $RvtAddinsAddinPath"

# Copy .dll files from build output
Copy-Item -Path "$BuildOutputPath\*.dll" -Destination $RvtAddinsDllPath -Force
Write-Host "*     - Copied .dll files to: $RvtAddinsDllPath"

# Delete bundle directory if it exists
if (Test-Path $BundlePath)
{
    Remove-Item -Path $BundlePath -Recurse -Force
    Write-Host "*     - Deleted bundle directory: $BundlePath"
}
else
{
    Write-Host "*     - Bundle directory not found: $BundlePath"
}

# Launch auto-approval script in background to handle security dialog
$AutoApproveScript = "$MSBuildProjectDirectory\AutoApproveAddin.ps1"
if (Test-Path $AutoApproveScript)
{
    Write-Host "*     - Launching auto-approval script for security dialog..."
    $LogFile = "$MSBuildProjectDirectory\AutoApproveAddin.log"
    Write-Host "*     - Script path: $AutoApproveScript"
    Write-Host "*     - Log file will be: $LogFile"

    try
    {
        # Launch with Normal window style to ensure UI access, but minimize it
        # This ensures the process has proper UI automation access
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "powershell.exe"
        $psi.Arguments = "-ExecutionPolicy Bypass -WindowStyle Minimized -NoProfile -File `"$AutoApproveScript`" -TimeoutSeconds 60 -LogFile `"$LogFile`" -ScriptDirectory `"$MSBuildProjectDirectory`""
        $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Minimized
        $psi.UseShellExecute = $true
        $psi.CreateNoWindow = $false

        $process = [System.Diagnostics.Process]::Start($psi)

        if ($null -ne $process)
        {
            Write-Host "*     - Auto-approval script started (PID: $( $process.Id ))"
            # Give it a moment to start and create the log file
            Start-Sleep -Milliseconds 500
            if (Test-Path $LogFile)
            {
                Write-Host "*     - Log file created successfully"
            }
            else
            {
                Write-Host "*     - WARNING: Log file not found after launch"
            }
        }
        else
        {
            Write-Host "*     - ERROR: Failed to start auto-approval script"
        }
    }
    catch
    {
        Write-Host "*     - ERROR launching script: $( $_.Exception.Message )"
    }
}
else
{
    Write-Host "*     - WARNING: Auto-approval script not found at: $AutoApproveScript"
} 