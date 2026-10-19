[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$GameDirectory, [switch]$Minimal)
$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$results = @()
$required = @('Mods/SkyCoop.dll','Mods/SkyCoop.pdb','UserLibs/Steamworks.NET.dll')
if (-not $Minimal) { $required += @('Mods/multiplayerstuff.unity3d','Mods/SkyCoop.modcomponent') }
foreach ($relative in @('Mods/SkyCoop.dll','Mods/SkyCoop.pdb','UserLibs/Steamworks.NET.dll','Mods/multiplayerstuff.unity3d','Mods/SkyCoop.modcomponent','Mods/SkyCoopResources/skycoop-character','Mods/SkyCoopResources/character.json')) {
    $file = Join-Path $game $relative
    $present = Test-Path -LiteralPath $file
    $hash = if ($present) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    $results += [pscustomobject]@{ file=$relative; present=$present; required=($relative -in $required); sha256=$hash }
}
$loader = Join-Path $game 'MelonLoader/net6/MelonLoader.dll'
$version = if (Test-Path -LiteralPath $loader) { [Reflection.AssemblyName]::GetAssemblyName($loader).Version.ToString() } else { $null }
$runtimes = @()
if (Get-Command dotnet -ErrorAction SilentlyContinue) { $runtimes = @(& dotnet --list-runtimes) }
[pscustomobject]@{
    mode=$(if ($Minimal) {'minimal-survival-render-probe'} else {'legacy-full-mode-blocked'})
    loaderVersion=$version
    matchesExperimentalPin=($version -eq '0.7.3.0')
    desktopRuntime6Detected=(@($runtimes | Where-Object { $_ -match '^Microsoft.WindowsDesktop.App 6\.' }).Count -gt 0)
    portableRuntimeNote='If using loader portable runtime, verify its startup log separately.'
    files=$results
    gameVersion='Must be established from game/store logs; executable presence is insufficient.'
    assetsCompatible='NOT PROVEN: original component has a format blocker; headers/hashes alone do not prove compatibility.'
    playable=$false
} | ConvertTo-Json -Depth 5
