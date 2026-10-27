# Gera tudo: pacotes gráficos, ícone e o MyAquario.exe
#   .\build.ps1             -> build\MyAquario\ (com os pacotes ao lado, para testar sem internet)
#   .\build.ps1 -SemPacotes -> build\MyAquario\ sem os pacotes (eles serão baixados do GitHub)
param([switch]$SemPacotes)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'

dotnet build "$root\tools\PackBuilder\PackBuilder.csproj" -c Release -o "$build\packbuilder" --nologo -v q
if ($LASTEXITCODE) { throw 'Falha ao compilar o PackBuilder' }

& "$build\packbuilder\PackBuilder.exe" "$build\packs" "$root\src\MyAquario\assets\app.ico"
if ($LASTEXITCODE) { throw 'Falha ao gerar os pacotes' }

$out = Join-Path $build 'MyAquario'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet build "$root\src\MyAquario\MyAquario.csproj" -c Release -o $out --nologo -v q
if ($LASTEXITCODE) { throw 'Falha ao compilar o MyAquario' }
Get-ChildItem $out -Filter *.pdb | Remove-Item

if (-not $SemPacotes) {
    New-Item -ItemType Directory -Force "$out\packs" | Out-Null
    Copy-Item "$build\packs\pack-*.zip" "$out\packs\"
}

Write-Host ""
Write-Host "Pronto: $out\MyAquario.exe"
Write-Host "Pacotes para o GitHub Releases: $build\packs\pack-normal.zip e pack-ultra.zip"
