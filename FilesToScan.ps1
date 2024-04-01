param (
    [string]$buildArtifactStagingDirectory,
    [string]$directoryToSearch
)

$filesToScan = @("Antlr.DOT.dll", "BoostTestAdapter.dll", "BoostTestPackage.dll", "BoostTestPlugin.dll", "BoostTestShared.dll", "ThirdPartySigning.dll", "VisualStudioAdapter.dll")
$FilesToScanDrop = "$buildArtifactStagingDirectory/FilesToScanDrop"

if (!(Test-Path -Path $FilesToScanDrop)) {
    New-Item -ItemType Directory -Path $FilesToScanDrop | Out-Null
}

foreach ($file in $filesToScan) {
    # Search in output directory for files we want to scan, but exclude any arm binaries.
    $sourcePaths = Get-ChildItem -Path $directoryToSearch -Recurse -Include $file -File | Where-Object { $_.DirectoryName -notmatch '\\arm\\|\\arm64\\' }
    foreach ($sourcePath in $sourcePaths) {
        $destinationPath = Join-Path $FilesToScanDrop $sourcePath.Name
        Copy-Item $sourcePath.FullName $destinationPath
        Write-Host "Found File to Scan: $sourcePath"
    }
}