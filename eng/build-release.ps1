param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.2.2',

    [Parameter()]
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [Parameter()]
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$releaseBase = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $workspaceRoot "artifacts\release\v$Version"
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
$publishDirectory = Join-Path $releaseBase 'publish'
$artifactDirectory = Join-Path $releaseBase 'artifacts'

$resolvedWorkspace = [System.IO.Path]::GetFullPath($workspaceRoot)
$resolvedReleaseBase = [System.IO.Path]::GetFullPath($releaseBase)
if (-not $resolvedReleaseBase.StartsWith(
        $resolvedWorkspace + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output must stay inside the workspace: $resolvedReleaseBase"
}

if (Test-Path -LiteralPath $resolvedReleaseBase) {
    Remove-Item -LiteralPath $resolvedReleaseBase -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

$localDotnet = Join-Path $workspaceRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
}
else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

$project = Join-Path $workspaceRoot 'src\ScreenTranslator.App\ScreenTranslator.App.csproj'
& $dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $workspaceRoot 'LICENSE') `
    -Destination (Join-Path $publishDirectory 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'README.md') `
    -Destination (Join-Path $publishDirectory 'README.md')

$forbiddenNames = @(
    'settings.json',
    'browser-follow.log',
    'native-host.json'
)
$forbidden = Get-ChildItem -LiteralPath $publishDirectory -Recurse -File |
    Where-Object {
        $_.Name -in $forbiddenNames -or
        $_.Extension -in @('.pfx', '.pem', '.key', '.bin')
    }
if ($forbidden) {
    throw "Release contains forbidden local or secret files: $($forbidden.FullName -join ', ')"
}

$executable = Join-Path $publishDirectory 'ScreenTranslator.exe'
$extensionDirectory = Join-Path $publishDirectory 'browser-extension'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published executable is missing: $executable"
}
if (-not (Test-Path -LiteralPath (Join-Path $extensionDirectory 'manifest.json'))) {
    throw "Published browser extension is missing."
}

$portableZip = Join-Path $artifactDirectory 'ScreenTranslator-Portable-x64.zip'
$extensionZip = Join-Path $artifactDirectory 'ScreenTranslator-Browser-Extension.zip'
Compress-Archive -Path (Join-Path $publishDirectory '*') `
    -DestinationPath $portableZip `
    -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $extensionDirectory '*') `
    -DestinationPath $extensionZip `
    -CompressionLevel Optimal

[PSCustomObject]@{
    Version = $Version
    ReleaseRoot = $releaseBase
    PublishDirectory = $publishDirectory
    ArtifactDirectory = $artifactDirectory
    PortableZip = $portableZip
    ExtensionZip = $extensionZip
} | ConvertTo-Json
