[CmdletBinding()]
param([string]$Id = 'melonloader-0.7.3-x64')
$ErrorActionPreference = 'Stop'
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'external-dependencies.json') -Raw | ConvertFrom-Json
$entry = @($catalog.downloads | Where-Object { $_.id -eq $Id })
if ($entry.Count -ne 1) { throw 'Unknown dependency ID. Read external-dependencies.json.' }
$entry = $entry[0]
$cache = Join-Path $PSScriptRoot 'ExternalDependencies'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$destination = Join-Path $cache $entry.filename
Invoke-WebRequest -Uri $entry.url -OutFile $destination -UseBasicParsing
$algorithm = $entry.hash_algorithm
if ((Get-FileHash -LiteralPath $destination -Algorithm $algorithm).Hash.ToLowerInvariant() -ne $entry.hash) {
    Remove-Item -LiteralPath $destination
    throw 'Downloaded dependency hash does not match the pinned official source.'
}
Write-Host "Verified download: $destination"
Write-Host $entry.status
Write-Host 'Nothing was executed or installed. Legacy assets require migration and runtime validation.'
