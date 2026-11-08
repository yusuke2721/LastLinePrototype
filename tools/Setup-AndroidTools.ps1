param(
    [string]$CachePath = (Join-Path $env:LOCALAPPDATA 'Packages\UnityTechnologies.UnityHub_2vrhnee42bhxm\LocalCache\Roaming\UnityHub\downloads')
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $repo 'Builds\AndroidTools'
[IO.Directory]::CreateDirectory($toolsRoot) | Out-Null

function Expand-ToolArchive([string]$Archive, [string]$Destination, [bool]$StripRoot) {
    $archivePath = Join-Path $CachePath $Archive
    $targetRoot = [IO.Path]::GetFullPath($Destination).TrimEnd('\') + '\'
    [IO.Directory]::CreateDirectory($targetRoot) | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $count = 0
        foreach ($entry in $zip.Entries) {
            $relative = $entry.FullName.Replace('/', '\')
            if ($StripRoot) {
                $separator = $relative.IndexOf('\')
                if ($separator -lt 0) { continue }
                $relative = $relative.Substring($separator + 1)
            }
            if ([string]::IsNullOrEmpty($relative)) { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $targetRoot $relative))
            if (!$target.StartsWith($targetRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escapes tool directory' }
            if ($entry.Name -eq '') { [IO.Directory]::CreateDirectory($target) | Out-Null; continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            $count++
        }
        Write-Output ('PASS: ' + $Archive + ' extracted (' + $count + ' files)')
    } finally { $zip.Dispose() }
}

Expand-ToolArchive 'jdk17.0.18-8_15e8817d1f5db6db3571ebe7430ef37f7fa8e60e8ff6f3e18ca1cb4c29f78774.zip' (Join-Path $toolsRoot 'JDK') $false
Expand-ToolArchive 'android-ndk-r27c-windows.zip' (Join-Path $toolsRoot 'NDK') $true
Expand-ToolArchive 'build-tools_r36_windows.zip' (Join-Path $toolsRoot 'SDK\build-tools\36.0.0') $true
Expand-ToolArchive 'platform-tools_r37.0.1-win.zip' (Join-Path $toolsRoot 'SDK\platform-tools') $true
Expand-ToolArchive 'platform-36_r02.zip' (Join-Path $toolsRoot 'SDK\platforms\android-36') $true
Expand-ToolArchive 'commandlinetools-win-12266719_latest.zip' (Join-Path $toolsRoot 'SDK\cmdline-tools\16.0') $true
Expand-ToolArchive 'cmake-3.22.1-windows.zip' (Join-Path $toolsRoot 'SDK\cmake\3.22.1') $false
Write-Output ('Android external tools: ' + $toolsRoot)
