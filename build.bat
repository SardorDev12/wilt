@echo off
REM Builds Wilt into a single self-contained .exe (no Visual Studio required).
REM Requires only the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

setlocal
cd /d "%~dp0"

echo Restoring and building Wilt...
dotnet publish src\Wilt\Wilt.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

if errorlevel 1 (
    echo.
    echo BUILD FAILED. Copy the error output above and send it back.
    exit /b 1
)

echo.
echo BUILD SUCCEEDED.
echo Your app is at: %cd%\publish\Wilt.exe
endlocal
