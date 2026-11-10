$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$dst = Join-Path $env:LOCALAPPDATA 'TarkovWin'
Get-Process TarkovShell,TarkovWelcome -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process TarkovShell,TarkovWelcome -ErrorAction SilentlyContinue | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $dst | Out-Null
for ($i = 0; $i -lt 20; $i++) {
  try { Copy-Item "$src\*.exe" $dst -Force -ErrorAction Stop; break }
  catch { if ($i -eq 19) { throw }; Start-Sleep -Milliseconds 500 }
}
New-Item -ItemType Directory -Force "$dst\data" | Out-Null
Copy-Item "$src\data\*" "$dst\data" -Recurse -Force

#
$ck = 'HKCU:\Control Panel\Cursors'
$bk = Join-Path $dst 'cursors_backup.json'
if (-not (Test-Path $bk)) {
  $o = @{}
  foreach ($n in 'Arrow','Hand','Wait','AppStarting','(default)') { $o[$n] = (Get-ItemProperty $ck).$n }
  $o | ConvertTo-Json | Set-Content $bk -Encoding UTF8
}
$map = @{ Arrow='arrow.cur'; Hand='hand.cur'; Wait='busy.cur'; AppStarting='busy.cur' }
foreach ($k in $map.Keys) { Set-ItemProperty $ck $k (Join-Path $dst "data\$($map[$k])") }
Set-ItemProperty $ck '(default)' ''
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class CurApi { [DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a,int b,int c,int d); }'
[void][CurApi]::SystemParametersInfo(0x57,0,0,3)   # SPI_SETCURSORS

#
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' 'TarkovWin' "`"$dst\TarkovShell.exe`" --daemon"
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' 'TarkovWelcome' "`"$dst\TarkovWelcome.exe`""
Start-Process "$dst\TarkovShell.exe" '--daemon'
'Installed: ' + $dst
