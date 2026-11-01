[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stageRoot = Join-Path $projectRoot ('tmp/release-' + [Guid]::NewGuid().ToString('N'))
$publishRoot = Join-Path $stageRoot 'publish'
$packageRoot = Join-Path $stageRoot 'AuctionPurchase'
$distRoot = Join-Path $projectRoot 'dist'
$localApp = Join-Path $distRoot 'AuctionPurchase'

function Assert-ProjectPath([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    if (-not $absolute.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Release path is outside the project: $absolute"
    }
    return $absolute
}

try {
    New-Item -ItemType Directory -Force -Path $publishRoot,$packageRoot,$distRoot,$localApp | Out-Null
    & dotnet publish (Join-Path $projectRoot 'src/AuctionPurchase.UI/AuctionPurchase.UI.csproj') `
        -c Release -m:1 --self-contained true -p:DebugType=None -p:DebugSymbols=false -p:NuGetAudit=false -o $publishRoot
    if ($LASTEXITCODE -ne 0) { throw 'Application publishing failed.' }

    Copy-Item -LiteralPath (Join-Path $publishRoot 'AuctionPurchase.exe') -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'release-readme.txt') -Destination (Join-Path $packageRoot 'README.txt')
    foreach ($folder in @('Data','Backups','Exports')) {
        New-Item -ItemType Directory -Path (Join-Path $packageRoot $folder) | Out-Null
    }
    $settings = [ordered]@{
        DateFormat = 'dd/MM/yyyy'; IsDarkMode = $true
        DatabaseLocation = ''; BackupLocation = ''; ExportLocation = ''
    } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $packageRoot 'Data/settings.json'), $settings)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = Join-Path $stageRoot 'AuctionPurchase.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($packageRoot,$archive,[IO.Compression.CompressionLevel]::Optimal,$true)
    # Keep existing local data/settings intact when refreshing the runnable app.
    Copy-Item -LiteralPath (Join-Path $packageRoot 'AuctionPurchase.exe') -Destination $localApp -Force
    Copy-Item -LiteralPath (Join-Path $packageRoot 'README.txt') -Destination $localApp -Force
    if (-not (Test-Path -LiteralPath (Join-Path $localApp 'Data/settings.json'))) {
        New-Item -ItemType Directory -Force -Path (Join-Path $localApp 'Data') | Out-Null
        Copy-Item -LiteralPath (Join-Path $packageRoot 'Data/settings.json') -Destination (Join-Path $localApp 'Data/settings.json')
    }
    Move-Item -LiteralPath (Assert-ProjectPath $archive) -Destination (Assert-ProjectPath (Join-Path $distRoot 'AuctionPurchase.zip')) -Force
    Write-Output 'Ready: dist/AuctionPurchase/AuctionPurchase.exe and dist/AuctionPurchase.zip'
}
finally {
    $checkedStage = Assert-ProjectPath $stageRoot
    if (Test-Path -LiteralPath $checkedStage) { Remove-Item -LiteralPath $checkedStage -Recurse -Force }
}
