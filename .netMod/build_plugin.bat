@echo off
echo Building RE3 Crowd Control Plugin...

cd /d "%~dp0"
set "RE3_MANAGED_PATH="
for /f "tokens=1,2 delims==" %%A in ('findstr /i "RE3ManagedPath" RE3DotNet-CC.csproj') do set "RE3_MANAGED_PATH=%%B"
set "RE3_MANAGED_PATH=%RE3_MANAGED_PATH:~1,-1%"
if not exist "%RE3_MANAGED_PATH%\REFramework.NET.application.dll" (
    echo.
    echo Missing RE3 generated managed assemblies.
    echo Ensure REFramework has generated managed DLLs in:
    echo   %RE3_MANAGED_PATH%
    echo Then rebuild, or update RE3ManagedPath in RE3DotNet-CC.csproj.
    echo.
    pause
    exit /b 1
)
dotnet build --configuration Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Plugin automatically copied to: C:\Program Files (x86)\Steam\steamapps\common\RESIDENT EVIL 3\reframework\plugins\managed\
    echo.
    echo The plugin is now ready to use! Just launch RE3 with REFramework.
) else (
    echo.
    echo Build failed! Check the error messages above.
)

pause
