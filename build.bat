@echo off
rem Tests, then builds release\HD2 Mod Setting GUI.exe (WPF on .NET Framework 4.8:
rem the one file a player needs). Needs the .NET SDK (8 or later): dotnet on PATH,
rem or set DOTNET to its dotnet.exe.
cd /d "%~dp0"
if "%DOTNET%"=="" set DOTNET=dotnet
"%DOTNET%" test Hd2ModSettingGui.sln -c Release || exit /b 1
"%DOTNET%" build src\Hd2ModSettingGui\Hd2ModSettingGui.csproj -c Release || exit /b 1
if not exist release mkdir release
copy /y "src\Hd2ModSettingGui\bin\Release\net48\HD2 Mod Setting GUI.exe" "release\HD2 Mod Setting GUI.exe" >nul || exit /b 1
echo.
echo Built: release\HD2 Mod Setting GUI.exe
