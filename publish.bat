@echo off
rem Builds one ChequeWriter.exe that runs on any 64-bit Windows 10/11 PC without installing .NET.
rem Output: publish\ChequeWriter.exe
setlocal
cd /d "%~dp0"
dotnet test || (echo Tests failed - not publishing. & exit /b 1)
dotnet publish src\ChequeWriter\ChequeWriter.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -o publish || exit /b 1
echo.
echo Done: %~dp0publish\ChequeWriter.exe
