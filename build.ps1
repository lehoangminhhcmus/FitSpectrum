param([switch]$Test)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$sources = (Get-Item Models.cs,Fitter.cs,Gui.cs).FullName
& $csc /nologo /target:winexe "/out:$PSScriptRoot\PhoFit-Colegram.exe" /r:System.Numerics.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    New-Item -ItemType Directory -Force outputs | Out-Null
    & $csc /nologo /main:Smoke "/out:$PSScriptRoot\Smoke.exe" /r:System.Numerics.dll (Get-Item Models.cs,Fitter.cs,Smoke.cs).FullName
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & .\Smoke.exe
    if ($LASTEXITCODE -ne 0) { throw 'Test failed.' }
}

