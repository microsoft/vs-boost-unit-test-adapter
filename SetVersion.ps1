Param([parameter(Mandatory=$true)] [string] $version)

$files_to_update = @(
  "Antlr.DOT\Properties\AssemblyInfo.cs",
  "BoostTestAdapter\Properties\AssemblyInfo.cs",
  "BoostTestPackage\Properties\AssemblyInfo.cs",
  "BoostTestPlugin\Properties\AssemblyInfo.cs",
  "BoostTestPlugin\source.extension.vsixmanifest",
  "BoostTestShared\Properties\AssemblyInfo.cs",
  "VisualStudioAdapter\Properties\AssemblyInfo.cs"
)

$files_to_update | ForEach-Object {
  (Get-Content $_) | ForEach-Object { $_.replace("0.0.0.0", $version) } | Set-Content $_
}