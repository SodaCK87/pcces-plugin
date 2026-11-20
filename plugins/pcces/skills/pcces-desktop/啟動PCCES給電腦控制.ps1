# 以一般權限（中完整性）啟動 PCCES，讓 Claude 的電腦控制點得動它。
# PCCES 的 manifest 是 requireAdministrator，正常啟動是高完整性，UIPI 會擋掉一般權限程式送的點擊與按鍵。
# 結束碼：0＝PCCES 已以一般權限在跑；1＝要使用者先處理（關掉高權限的 PCCES、或改用一般終端機）；2＝找不到 PCCES。

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class PcxIntegrity {
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool i, int pid);
  [DllImport("advapi32.dll")] static extern bool OpenProcessToken(IntPtr p, uint a, out IntPtr t);
  [DllImport("advapi32.dll")] static extern bool GetTokenInformation(IntPtr t, int c, IntPtr b, int l, out int r);
  [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr s, uint i);
  [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr s);
  public static int Get(int pid) {
    IntPtr h = OpenProcess(0x1000, false, pid); if (h == IntPtr.Zero) return -1;
    IntPtr t; if (!OpenProcessToken(h, 8, out t)) return -1;
    int n; GetTokenInformation(t, 25, IntPtr.Zero, 0, out n);
    IntPtr b = Marshal.AllocHGlobal(n);
    try {
      if (!GetTokenInformation(t, 25, b, n, out n)) return -1;
      IntPtr sid = Marshal.ReadIntPtr(b);
      int c = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
      return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(c - 1)));
    } finally { Marshal.FreeHGlobal(b); }
  }
}
'@

function Get-LevelText([int] $lv) {
    if ($lv -ge 0x3000) { return "高（系統管理員）0x{0:X}" -f $lv }
    if ($lv -ge 0x2000) { return "中（一般權限）0x{0:X}" -f $lv }
    if ($lv -lt 0) { return '讀不到（多半是高權限）' }
    return "低 0x{0:X}" -f $lv
}

$me = [PcxIntegrity]::Get($PID)
if ($me -ge 0x3000) {
    Write-Host '這個終端機是系統管理員權限，從這裡啟動的 PCCES 也會是高權限，電腦控制點不動。' -ForegroundColor Yellow
    Write-Host '請改開一般的終端機（不要選「以系統管理員身分執行」）再跑這支。'
    exit 1
}

$running = @(Get-Process PccesMain -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    foreach ($p in $running) {
        $lv = [PcxIntegrity]::Get($p.Id)
        Write-Host ("PCCES 已在執行：PID {0}，權限 {1}" -f $p.Id, (Get-LevelText $lv))
        if ($lv -lt 0 -or $lv -ge 0x3000) {
            Write-Host '這個 PCCES 是高權限，電腦控制點不動。請先把 PCCES 整個關掉，再跑一次這支。' -ForegroundColor Yellow
            exit 1
        }
    }
    Write-Host 'PCCES 已是一般權限，可以直接操作。' -ForegroundColor Green
    exit 0
}

$dir = $null
$keys = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
$hit = Get-ItemProperty $keys -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -like 'PccesWin*' -and $_.InstallLocation } | Select-Object -First 1
if ($hit) { $dir = $hit.InstallLocation.TrimEnd('\') }
if (-not $dir) { $dir = 'C:\Program Files (x86)\PccesWin4.3' }
$exe = Join-Path $dir 'PccesMain.exe'
if (-not (Test-Path $exe)) {
    Write-Host "找不到 PCCES：$exe" -ForegroundColor Red
    exit 2
}

# RunAsInvoker 讓 Windows 忽略 manifest 的提權要求，用呼叫者（一般權限）的 token 啟動
$env:__COMPAT_LAYER = 'RunAsInvoker'
Start-Process -FilePath $exe -WorkingDirectory $dir
Remove-Item Env:\__COMPAT_LAYER

$p = $null
for ($i = 0; $i -lt 30 -and -not $p; $i++) {
    Start-Sleep -Milliseconds 500
    $p = Get-Process PccesMain -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $p) {
    Write-Host 'PCCES 15 秒內沒有出現，請看畫面有沒有錯誤訊息。' -ForegroundColor Red
    exit 1
}
$lv = [PcxIntegrity]::Get($p.Id)
Write-Host ("已啟動 PCCES：PID {0}，權限 {1}" -f $p.Id, (Get-LevelText $lv))
if ($lv -ge 0x2000 -and $lv -lt 0x3000) {
    Write-Host '可以開始用電腦控制操作。登入帳號 PCCES、密碼空白。' -ForegroundColor Green
    exit 0
}
Write-Host '權限不是一般權限，電腦控制可能點不動。' -ForegroundColor Yellow
exit 1
