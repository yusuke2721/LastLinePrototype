param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [Parameter(Mandatory=$true)][string]$EditorPath
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
$editor=(Resolve-Path -LiteralPath $EditorPath).Path
$output=Join-Path ([IO.Path]::GetTempPath()) ('LastLine-Compile-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($output)|Out-Null
$dotnet=Join-Path $editor 'Data\DotNetSdk\dotnet.exe'
$compiler=Get-ChildItem -LiteralPath (Join-Path $editor 'Data\DotNetSdk\sdk') -Recurse -Filter 'csc.dll' |
    Where-Object {$_.FullName -like '*\Roslyn\bincore\csc.dll'} | Sort-Object FullName -Descending | Select-Object -First 1
$artifact=Get-ChildItem -LiteralPath (Join-Path $project 'Library\Bee\artifacts') -Recurse -Filter 'Assembly-CSharp.rsp' |
    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if(-not $compiler -or -not $artifact -or -not (Test-Path -LiteralPath $dotnet)){throw 'Open and compile the Unity project once; Unity DotNetSdk and Bee response files are required.'}
Push-Location $project
try {
    foreach($assembly in @('Assembly-CSharp','Assembly-CSharp-Editor')) {
        $original=Join-Path $artifact.DirectoryName ($assembly+'.rsp')
        $lines=@(foreach($line in (Get-Content -LiteralPath $original)) {
            if($line -match '^-out:') {'-out:"'+(Join-Path $output ($assembly+'.dll'))+'"'}
            elseif($line -match '^-refout:') {'-refout:"'+(Join-Path $output ($assembly+'.ref.dll'))+'"'}
            elseif($line -match '^-r:".*[/\\]Assembly-CSharp\.ref\.dll"$') {'-r:"'+(Join-Path $output 'Assembly-CSharp.ref.dll')+'"'}
            elseif($line -match '^"Assets/LastLine/Scripts/.*\.cs"$') {continue}
            else {$line}
        })
        $folder=if($assembly -eq 'Assembly-CSharp'){'Runtime'}else{'Editor'}
        $sources=Get-ChildItem -LiteralPath (Join-Path $repo ('Assets\LastLine\Scripts\'+$folder)) -Filter '*.cs'
        $lines+=@($sources | ForEach-Object {'"'+$_.FullName+'"'})
        $response=Join-Path $output ($assembly+'.rsp')
        [IO.File]::WriteAllLines($response,$lines,[Text.UTF8Encoding]::new($false))
        & $dotnet $compiler.FullName ('@'+$response)
        if($LASTEXITCODE -ne 0){throw ($assembly+' compile failed')}
        Write-Output ('PASS: '+$assembly+' compiled against the Unity project references ('+$sources.Count+' LastLine files)')
    }
} finally {Pop-Location}
Write-Output ('Temporary compiler outputs: '+$output)
