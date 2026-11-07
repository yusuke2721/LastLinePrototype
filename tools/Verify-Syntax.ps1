# PowerShell 7 ships Roslyn for Add-Type. This checks syntax, not Unity API binding.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition 'public static class LastLineSyntaxProbe {}'
$files = Get-ChildItem -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets') -Recurse -Filter '*.cs'
$modes = @(
    @('UNITY_EDITOR', 'ENABLE_INPUT_SYSTEM'),
    @('UNITY_EDITOR', 'ENABLE_LEGACY_INPUT_MANAGER'),
    @('UNITY_EDITOR', 'ENABLE_INPUT_SYSTEM', 'ENABLE_LEGACY_INPUT_MANAGER')
)
foreach ($symbols in $modes) {
    $options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithPreprocessorSymbols([string[]]$symbols)
    foreach ($file in $files) {
        $source = [string](Get-Content -LiteralPath $file.FullName -Raw)
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source, $options)
        $errors = @($tree.GetDiagnostics() | Where-Object { $_.Severity -eq 'Error' })
        if ($errors.Count -gt 0) {
            $errors | ForEach-Object { Write-Output ($file.Name + ': ' + $_.ToString()) }
            throw 'C# syntax validation failed'
        }
    }
    Write-Output ('PASS: C# syntax, ' + $files.Count + ' files, symbols=' + ($symbols -join ','))
}
Write-Output 'Unity API references, rendering, and Player builds still require Unity Editor.'
