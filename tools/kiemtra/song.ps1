<#
  TEST SONG tren server that (KHONG nam trong chay.ps1). Chay kho khong giao dien + acc dong vai nguoi choi.

      powershell -ExecutionPolicy Bypass -File tools\kiemtra\song.ps1 -ChuanBi
      powershell -ExecutionPolicy Bypass -File tools\kiemtra\song.ps1 -Kho   -Bo tungkhodo9
      powershell -ExecutionPolicy Bypass -File tools\kiemtra\song.ps1 -Nguoi -Acc tungkhodo9 [-Lenh lenh-nguoi.txt]

  -ChuanBi  tao tools\kiemtra\song\ (bo qua git), chep acc + cai dat kho tu ban Release NEU chua co,
            chep NSOKHODO.exe vao song\bin\ de bien dich harness. Luc CHAY moi vai nap ban chep rieng
            (song\bin\kho, song\bin\<acc>) -> build lai / chay lai kho khong bi acc nguoi choi khoa file,
            bien dich KhoSong.exe + NguoiChoi.exe.
  -Kho      chay KhoSong (log: song\kho-out.log). Lenh: noi dong vao song\lenh-kho.txt
  -Nguoi    chay NguoiChoi (log: song\<acc>-out.log). Lenh: noi dong vao song\<Lenh>

  Acc nguoi choi phai la acc CUA USER, khong nam trong kho (-Bo) hoac dang "nha".
  ⚠ Duong dan thu muc KHONG duoc co dau cach (lenh chay qua cmd /c).
  ⚠ Danh sach acc trong song\ la BAN SAO - dung commit (thu muc da nam trong .gitignore).
#>
param([switch]$ChuanBi, [switch]$Kho, [switch]$Nguoi, [string]$Bo = "", [string]$Acc = "", [string]$Lenh = "lenh-nguoi.txt")

$ErrorActionPreference = "Stop"
$goc = Split-Path -Parent $MyInvocation.MyCommand.Path
$song = Join-Path $goc "song"
$rel = Join-Path $goc "..\..\src\NSOKHODO\bin\Release\net452"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if ($ChuanBi) {
    New-Item -ItemType Directory -Force "$song\Data\Accounts", "$song\Data\Kho", "$song\bin" | Out-Null
    $ds = "Mặc định.txt"
    if (-not (Test-Path "$song\Data\Accounts\$ds")) { Copy-Item "$rel\Data\Accounts\$ds" "$song\Data\Accounts\" }
    foreach ($f in "servers.txt", "settings.txt", "map_levels.txt") {
        if ((Test-Path "$rel\Data\$f") -and -not (Test-Path "$song\Data\$f")) { Copy-Item "$rel\Data\$f" "$song\Data\" }
    }
    foreach ($f in "tenmon.txt", $ds) {
        if ((Test-Path "$rel\Data\Kho\$f") -and -not (Test-Path "$song\Data\Kho\$f")) { Copy-Item "$rel\Data\Kho\$f" "$song\Data\Kho\" }
    }
    try { Copy-Item "$rel\NSOKHODO.exe" "$song\bin\NSOKHODO.exe" -Force } catch { Write-Host "song\bin\NSOKHODO.exe dang bi khoa - bien dich voi ban cu" }
    Write-Host ("EXE chep luc: " + (Get-Item "$song\bin\NSOKHODO.exe").LastWriteTime)
    foreach ($t in "KhoSong", "NguoiChoi") {
        & $csc -nologo -codepage:65001 "-out:$song\$t.exe" "-r:$song\bin\NSOKHODO.exe" "$goc\$t.cs"
        if ($LASTEXITCODE -ne 0) { Write-Host "$t : KHONG BIEN DICH DUOC" -ForegroundColor Red; exit 1 }
    }
    Write-Host "Da chuan bi $song"
    exit 0
}

# Moi vai chay tu thu muc rieng bin\kho / bin\<acc> (ban chep harness + NSOKHODO.exe): build lai hay bien dich
# lai harness khong bi tien trinh dang chay khoa file. File dang bi khoa (vai do dang chay) thi giu ban cu.
if (-not $Kho -and -not $Nguoi) { Write-Host "Dung: -ChuanBi | -Kho [-Bo acc,..] | -Nguoi -Acc <acc> [-Lenh file]"; exit 2 }
if ($Nguoi -and -not $Acc) { Write-Host "Thieu -Acc" -ForegroundColor Red; exit 2 }
$vai = if ($Kho) { "kho" } else { $Acc }
$chay = "$song\bin\$vai"
New-Item -ItemType Directory -Force $chay | Out-Null
$exeVai = if ($Kho) { "KhoSong.exe" } else { "NguoiChoi.exe" }
foreach ($f in @("$rel\NSOKHODO.exe", "$song\$exeVai")) {
    try { Copy-Item $f $chay -Force } catch { Write-Host ("Giu ban cu cua " + (Split-Path -Leaf $f) + " trong $chay") }
}
$env:NSOKHODO_EXE = "$chay\NSOKHODO.exe"
Set-Location $song
# Qua cmd: chuyen huong cua Windows PowerShell 5.1 ghi UTF-16, grep khong doc duoc.
if ($Kho) {
    $a = @($song)
    if ($Bo) { $a += "--bo=$Bo" }
    cmd /c ("$chay\KhoSong.exe " + ($a -join " ") + " > $song\kho-out.log 2>&1")
    exit $LASTEXITCODE
}
cmd /c "$chay\NguoiChoi.exe $song $Acc $Lenh > $song\$Acc-out.log 2>&1"
exit $LASTEXITCODE
Write-Host "Dung: -ChuanBi | -Kho [-Bo acc,..] | -Nguoi -Acc <acc> [-Lenh file]"
