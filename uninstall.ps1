$dst = Join-Path $env:LOCALAPPDATA 'TarkovWin'
Get-Process TarkovShell,TarkovWelcome -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' 'TarkovWin' -ErrorAction SilentlyContinue
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' 'TarkovWelcome' -ErrorAction SilentlyContinue
$ck = 'HKCU:\Control Panel\Cursors'
$bk = Join-Path $dst 'cursors_backup.json'
if (Test-Path $bk) {
  $o = Get-Content $bk -Raw | ConvertFrom-Json
  foreach ($p in $o.PSObject.Properties) { Set-ItemProperty $ck $p.Name ([string]$p.Value) }
}
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class CurApi { [DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a,int b,int c,int d); }'
[void][CurApi]::SystemParametersInfo(0x57,0,0,3)
Remove-Item $dst -Recurse -Force -ErrorAction SilentlyContinue
'Removed, cursors restored.'
