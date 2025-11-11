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

# Copy .dll files from project root
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