@echo off
rem Builds AutoTyper.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set OPTS=/nologo /codepage:65001 /target:winexe /optimize+

rem Pass 1: build once to render the icon from the same drawing code the window uses.
%CSC% %OPTS% /out:icon-gen.exe AutoTyper.cs || exit /b 1
start "" /wait icon-gen.exe --write-icon AutoTyper.ico
del icon-gen.exe

rem Pass 2: the real exe, with the icon embedded.
%CSC% %OPTS% /win32icon:AutoTyper.ico /out:AutoTyper.exe AutoTyper.cs || exit /b 1
del AutoTyper.ico
echo Built AutoTyper.exe
