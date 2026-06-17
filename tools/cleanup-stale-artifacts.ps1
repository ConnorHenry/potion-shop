Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Remove-GeneratedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $targetPath = (Resolve-Path (Join-Path $repoRoot $RelativePath) -ErrorAction SilentlyContinue)
    if ($null -eq $targetPath) {
        Write-Host "Skip missing: $RelativePath"
        return
    }

    $resolvedPath = $targetPath.Path
    if (-not $resolvedPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside repository root: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    Write-Host "Removed: $resolvedPath"
}

Write-Host "Cleaning generated artifacts in $repoRoot"

Remove-GeneratedDirectory ".godot"
Remove-GeneratedDirectory "bin"
Remove-GeneratedDirectory "obj"
Remove-GeneratedDirectory "tmp"
Remove-GeneratedDirectory "tests/Potion.Tests/bin"
Remove-GeneratedDirectory "tests/Potion.Tests/obj"

Write-Host "Cleanup complete."
