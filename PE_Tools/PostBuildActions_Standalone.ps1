param(
    [string]$Configuration = "Debug",
    [string]$RevitVersion = "",
    [string]$ProjectDirectory = "",
    [string]$AppDataPath = ""
)

# Function to prompt for user input with default value
function Get-UserInput
{
    param(
        [string]$Prompt,
        [string]$Default
    )

    if ($Default)
    {
        $input = Read-Host "$Prompt (default: $Default)"
        if ( [string]::IsNullOrWhiteSpace($input))
        {
            return $Default
        }
        return $input
    }
    else
    {
        return Read-Host $Prompt
    }
}

# Function to get available Revit versions from AppData
function Get-AvailableRevitVersions
{
    param([string]$AppDataPath)

    $revitAddinsPath = "$AppDataPath\Autodesk\REVIT\Addins"
    if (Test-Path $revitAddinsPath)
    {
        $versions = Get-ChildItem -Path $revitAddinsPath -Directory | Where-Object { $_.Name -match '^\d{4}$' } | Sort-Object Name -Descending
        return $versions.Name
    }
    return @()
}

Write-Host "****** STANDALONE PE_Tools ADDIN COPY SCRIPT *******"
Write-Host ""

# Skip if running in GitHub Actions
if ($env:GITHUB_ACTIONS -eq "true" -or $env:GITHUB_RUN_ID)
{
    Write-Host "****** SKIPPING LOCAL DEV ADDIN COPY *******"
    Write-Host "****** env:GITHUB_ACTIONS: $env:GITHUB_ACTIONS *******"
    Write-Host "****** env:GITHUB_RUN_ID: $env:GITHUB_RUN_ID *******"
    Write-Host ""
    exit 0
}

# Set default values
if ( [string]::IsNullOrWhiteSpace($ProjectDirectory))
{
    $ProjectDirectory = $PSScriptRoot
}

if ( [string]::IsNullOrWhiteSpace($AppDataPath))
{
    $AppDataPath = $env:APPDATA
}

# Get available Revit versions
$availableVersions = Get-AvailableRevitVersions -AppDataPath $AppDataPath

Write-Host "Project Directory: $ProjectDirectory"
Write-Host "AppData Path: $AppDataPath"
Write-Host ""

# Prompt for Configuration if not provided
if ( [string]::IsNullOrWhiteSpace($Configuration))
{
    $Configuration = Get-UserInput "Enter build configuration" "Debug"
}

# Prompt for Revit Version if not provided
if ( [string]::IsNullOrWhiteSpace($RevitVersion))
{
    if ($availableVersions.Count -gt 0)
    {
        Write-Host "Available Revit versions found:"
        for ($i = 0; $i -lt $availableVersions.Count; $i++) {
            Write-Host "  [$( $i + 1 )] $( $availableVersions[$i] )"
        }
        Write-Host ""

        $selection = Get-UserInput "Select Revit version (number or type version manually)" "1"

        # Check if selection is a number
        if ($selection -match '^\d+$')
        {
            $index = [int]$selection - 1
            if ($index -ge 0 -and $index -lt $availableVersions.Count)
            {
                $RevitVersion = $availableVersions[$index]
            }
            else
            {
                Write-Host "Invalid selection. Using first available version: $( $availableVersions[0] )"
                $RevitVersion = $availableVersions[0]
            }
        }
        else
        {
            $RevitVersion = $selection
        }
    }
    else
    {
        $RevitVersion = Get-UserInput "Enter Revit version (e.g., 2025)" ""
        if ( [string]::IsNullOrWhiteSpace($RevitVersion))
        {
            Write-Host "Error: Revit version is required"
            exit 1
        }
    }
}

Write-Host ""
Write-Host "Configuration: $Configuration"
Write-Host "Revit Version: $RevitVersion"
Write-Host ""

# Define path variables
$BuildOutputPath = "$ProjectDirectory\bin\$Configuration\$RevitVersion"
$RvtAddinsAddinPath = "$AppDataPath\Autodesk\REVIT\Addins\$RevitVersion"
$RvtAddinsDllPath = "$AppDataPath\Autodesk\REVIT\Addins\$RevitVersion\PE_Tools"
$BundlePath = "C:\ProgramData\Autodesk\ApplicationPlugins\PE_Tools.bundle"

# Verify build output exists
if (-not (Test-Path $BuildOutputPath))
{
    Write-Host "Error: Build output directory not found: $BuildOutputPath"
    Write-Host "Please build the project first or check the Configuration and RevitVersion parameters."
    exit 1
}

# Check for .addin file
$addinFiles = Get-ChildItem -Path "$ProjectDirectory\*.addin"
if ($addinFiles.Count -eq 0)
{
    Write-Host "Error: No .addin files found in: $ProjectDirectory"
    exit 1
}

# Check for .dll files
$dllFiles = Get-ChildItem -Path "$BuildOutputPath\*.dll"
if ($dllFiles.Count -eq 0)
{
    Write-Host "Error: No .dll files found in: $BuildOutputPath"
    exit 1
}

# Echo operation information
Write-Host "****** COPYING ADDIN FILES *******"
Write-Host "* Copying **$Configuration** build output (.dll and .addin)"
Write-Host "* from **$BuildOutputPath**"
Write-Host "* to **$RvtAddinsAddinPath**"
Write-Host ""

# Create Revit addins directory if it doesn't exist
if (-not (Test-Path $RvtAddinsAddinPath))
{
    New-Item -ItemType Directory -Path $RvtAddinsAddinPath -Force | Out-Null
    Write-Host "*     - Created dir: $RvtAddinsAddinPath"
}

# Create dll directory if it doesn't exist
if (-not (Test-Path $RvtAddinsDllPath))
{
    New-Item -ItemType Directory -Path $RvtAddinsDllPath -Force | Out-Null
    Write-Host "*     - Created dir: $RvtAddinsDllPath"
}

try
{
    # Copy .addin files
    Copy-Item -Path "$ProjectDirectory\*.addin" -Destination $RvtAddinsAddinPath -Force
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

    Write-Host ""
    Write-Host "****** COPY OPERATION COMPLETED SUCCESSFULLY *******"
}
catch
{
    Write-Host "Error during copy operation: $( $_.Exception.Message )"
    exit 1
}
