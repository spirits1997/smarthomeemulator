@echo off
REM Build script for Smart Home Emulator (Windows, .NET Framework 4.8)
REM Requires Visual Studio 2019/2022 or the standalone Build Tools for Visual Studio

setlocal

set CONFIG=Release

REM Try to find MSBuild
for /f "tokens=*" %%i in ('"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul') do (
    set MSBUILD=%%i
)

if not defined MSBUILD (
    REM Fallback to .NET Framework MSBuild
    if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
        set MSBUILD=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
    ) else (
        echo ERROR: MSBuild not found.
        echo Please install Visual Studio 2019/2022 or Build Tools for Visual Studio.
        echo See: https://visualstudio.microsoft.com/downloads/
        pause
        exit /b 1
    )
)

echo Using MSBuild: %MSBUILD%
echo Building %CONFIG% configuration...

"%MSBUILD%" SmartHomeEmulator\SmartHomeEmulator.csproj ^
    /p:Configuration=%CONFIG% ^
    /p:Platform="AnyCPU" ^
    /verbosity:minimal ^
    /nologo

if %ERRORLEVEL% neq 0 (
    echo Build FAILED.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Build succeeded!
echo Output: SmartHomeEmulator\bin\%CONFIG%\SmartHomeEmulator.exe
echo.
echo To run portably: copy the entire bin\%CONFIG% folder to any Windows 7+ machine
echo and run SmartHomeEmulator.exe. No installation required.
pause
