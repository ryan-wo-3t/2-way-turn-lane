[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$UnityProjectPath,
    [Parameter(Mandatory = $true)][string]$PostProcessorPath,
    [Parameter(Mandatory = $true)][string]$ReferenceListPath
)

$ErrorActionPreference = 'Stop'
foreach ($path in @($AssemblyPath, $PostProcessorPath, $ReferenceListPath,
    (Join-Path $UnityProjectPath 'Library/ScriptAssemblies/Unity.Entities.CodeGen.dll'),
    (Join-Path $UnityProjectPath 'Library/ScriptAssemblies/Unity.Burst.dll'))) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing postprocessor prerequisite: $path. Open the official Unity mod project with its matching Editor and let script compilation finish."
    }
}

# Native invocation uses CreateProcess rather than expanding a long reference list in cmd.exe.
# Preserve the official processor's reference list, platforms and debug/verbose options.
$processorArguments = @('PostProcess', $AssemblyPath, '-u', $UnityProjectPath)
foreach ($reference in Get-Content -LiteralPath $ReferenceListPath) {
    if (![string]::IsNullOrWhiteSpace($reference)) {
        $processorArguments += @('-r', $reference)
    }
}
$processorArguments += @('-p', 'Windows', '-p', 'macOS', '-p', 'Linux', '-d', '-v')
& $PostProcessorPath @processorArguments
if ($LASTEXITCODE -ne 0) { throw "Official mod postprocessor exited with code $LASTEXITCODE" }
