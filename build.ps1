# build FurryPet.exe with the .NET Framework compiler bundled with Windows
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "csc.exe not found - .NET Framework 4.x is required"
}

$icon = "assets\umi\face.ico"
if (-not (Test-Path $icon)) { $icon = "assets\blue\face.ico" }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    /win32icon:"$icon" /out:"FurryPet.exe" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll /reference:System.Core.dll /reference:Microsoft.CSharp.dll `
    "src\Core.cs" "src\Pet.cs" "src\Panels.cs"

if ($LASTEXITCODE -ne 0) { throw "compile failed ($LASTEXITCODE)" }
Write-Host "OK - FurryPet.exe rebuilt" -ForegroundColor Green
