param(
    [ValidateSet("install", "remove", "status")]
    [string]$Action = "status"
)

$exe = Join-Path $PSScriptRoot "FurryPet.exe"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$valueName = "FurryPetTemplate"

switch ($Action) {
    "install" {
        Set-ItemProperty -Path $runKey -Name $valueName -Value ('"' + $exe + '"')
        Write-Host "已加入开机自启：$exe"
    }
    "remove" {
        Remove-ItemProperty -Path $runKey -Name $valueName -ErrorAction SilentlyContinue
        Write-Host "已取消开机自启"
    }
    default {
        $v = (Get-ItemProperty -Path $runKey -Name $valueName -ErrorAction SilentlyContinue).$valueName
        if ($v) { Write-Host "已开启：$v" } else { Write-Host "未开启" }
    }
}
