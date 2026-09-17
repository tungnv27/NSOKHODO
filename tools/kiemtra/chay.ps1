<#
  Chay bo kiem tra cua NSOKHODO.

      powershell -ExecutionPolicy Bypass -File tools\kiemtra\chay.ps1

  Project KHONG co test framework (xem CLAUDE.md). Bo nay khong phai unit test day du -
  no la mot bo HARNESS nap thang NSOKHODO.exe roi goi vao ben trong (reflection, hoac
  bien dich kem -r:NSOKHODO.exe voi KiemKho).

  KiemLoi       phan loi giu lai tu NSOBAOTATL + chan dung/mac/ban/vut mon (D38)
  KiemHp        nhanh sua "HP da day" cua loi
  KiemHopThoai  dung thu MapPickerForm + MainForm bo cuc A (thu muc tam)
  KiemKho       toan bo phan kho, CHAY OFFLINE: bo dau/tem, lenh chat, cai dat, so kho,
                hang cho + giu cho, phien giao dich (gia lap goi server), log theo ngay,
                kenh chat, bo dieu phoi (vai, loi moi, lenh chat, rut, don kho, nha clone,
                Leader du phong). Mat khoang 20 giay vi co cho that (1,5 s truoc goi 46...).

  ⚠ PHAI build Release TRUOC khi chay, va phai kiem EXE that su duoc ghi de
    (xem ghi chu "build xanh chua chac EXE moi" trong CLAUDE.md).
  ⚠ csc.exe cua .NET Framework chi hieu C# 5 - file kiem tra khong duoc dung cu phap moi hon.
#>

param([string]$Exe = "")   # -Exe <duong dan>: kiem ban build o cho khac (app dang chay khoa bin\Release)

$ErrorActionPreference = "Stop"
$goc = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = if ($Exe) { $Exe } else { Join-Path $goc "..\..\src\NSOKHODO\bin\Release\net452\NSOKHODO.exe" }

if (-not (Test-Path $exe)) {
    Write-Host "KHONG THAY $exe - hay chay 'dotnet build NSOKHODO.sln -c Release' truoc." -ForegroundColor Red
    exit 1
}
$env:NSOKHODO_EXE = (Resolve-Path $exe).Path
Write-Host ("Kiem tra tren: " + $env:NSOKHODO_EXE)
Write-Host ("EXE build luc: " + (Get-Item $env:NSOKHODO_EXE).LastWriteTime)
Write-Host ""

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { Write-Host "Khong thay csc.exe cua .NET Framework." -ForegroundColor Red; exit 1 }

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$tong = 0

foreach ($ten in @("KiemLoi", "KiemHp", "KiemHopThoai", "KiemKho")) {
    $src = Join-Path $goc "$ten.cs"
    $out = Join-Path $env:TEMP "$ten.exe"
    $thamSo = @("-nologo", "-codepage:65001", "-out:$out", "-r:System.Windows.Forms.dll", "-r:System.Drawing.dll")
    # KiemKho goi thang kieu cua NSOKHODO (khong qua reflection) -> can tham chieu exe khi bien dich;
    # luc chay no tu nap exe qua AssemblyResolve.
    if ($ten -eq "KiemKho") { $thamSo += "-r:$($env:NSOKHODO_EXE)" }
    & $csc @thamSo $src
    if ($LASTEXITCODE -ne 0) { Write-Host "$ten : KHONG BIEN DICH DUOC" -ForegroundColor Red; $tong++; continue }

    Write-Host "########## $ten ##########" -ForegroundColor Cyan
    & $out
    if ($LASTEXITCODE -ne 0) { $tong += $LASTEXITCODE }
}

Write-Host ""
if ($tong -eq 0) { Write-Host ">>> TAT CA PASS" -ForegroundColor Green }
else { Write-Host ">>> CO $tong CHO SAI" -ForegroundColor Red }
exit $tong
