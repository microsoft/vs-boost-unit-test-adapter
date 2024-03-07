Param([parameter(Mandatory=$true)] [string] $version)

$files_to_update = @(
  "vs-boost-unit-test-adapter\Antlr.DOT\Properties\AssemblyInfo.cs",
  "vs-boost-unit-test-adapter\BoostTestAdapter\Properties\AssemblyInfo.cs",
  "vs-boost-unit-test-adapter\BoostTestPackage\Properties\AssemblyInfo.cs",
  "vs-boost-unit-test-adapter\BoostTestPlugin\Properties\AssemblyInfo.cs",
  "vs-boost-unit-test-adapter\BoostTestPlugin\source.extension.vsixmanifest",
  "vs-boost-unit-test-adapter\BoostTestShared\Properties\AssemblyInfo.cs",
  "vs-boost-unit-test-adapter\VisualStudioAdapter\Properties\AssemblyInfo.cs"
)

$files_to_update | ForEach-Object {
  (Get-Content $_) | ForEach-Object { $_.replace("0.0.0.0", $version) } | Set-Content $_
}