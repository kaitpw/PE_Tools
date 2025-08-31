# Hardcoded variables for direct command-line use
$APPDATA = $env:APPDATA
$REVIT_VERSIONS = 2023..2027
$PROJECT_NAME = "PE_Tools"

foreach ($REVIT_VERSION in $REVIT_VERSIONS) {
    $RvtAddinsAddinPath = "$APPDATA\Autodesk\REVIT\Addins\$REVIT_VERSION"
    $RvtAddinsDllPath = "$APPDATA\Autodesk\REVIT\Addins\$REVIT_VERSION\$PROJECT_NAME"

    Write-Host "****** UNDO LOCAL DEV ADDIN COPY for Revit $REVIT_VERSION *******"
    Write-Host "* Removing $PROJECT_NAME.addin from: $RvtAddinsAddinPath"
    Write-Host "* Removing $PROJECT_NAME.dll from: $RvtAddinsDllPath"

    # Remove the .addin file
    $addinTarget = Join-Path $RvtAddinsAddinPath "$PROJECT_NAME.addin"
    if (Test-Path $addinTarget) {
        Remove-Item $addinTarget -Force
        Write-Host "*     - Removed: $addinTarget"
    } else {
        Write-Host "*     - Not found: $addinTarget"
    }

    # Remove the .dll file
    $dllTarget = Join-Path $RvtAddinsDllPath "$PROJECT_NAME.dll"
    if (Test-Path $RvtAddinsDllPath) {
        Remove-Item $RvtAddinsDllPath -Force
        Write-Host "*     - Removed: $RvtAddinsDllPath"
    } else {
        Write-Host "*     - Not found: $dllTarget"
    }
}

Write-Host "****** UNDO COMPLETE FOR ALL VERSIONS *******" 