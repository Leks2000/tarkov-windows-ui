@echo off
rem Builds both executables with the C# compiler that ships with Windows (.NET Framework 4.x). No SDK needed.
setlocal
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set CSC=%FW%\csc.exe
cd /d "%~dp0"
"%CSC%" -nologo -codepage:65001 -target:winexe -out:TarkovShell.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:Accessibility.dll src\TarkovShell.cs || goto :err
"%CSC%" -nologo -codepage:65001 -target:winexe -out:TarkovWelcome.exe -r:"%FW%\WPF\PresentationFramework.dll" -r:"%FW%\WPF\PresentationCore.dll" -r:"%FW%\WPF\WindowsBase.dll" -r:"%FW%\System.Xaml.dll" src\Welcome.cs || goto :err
echo Build OK
exit /b 0
:err
echo Build FAILED
exit /b 1
