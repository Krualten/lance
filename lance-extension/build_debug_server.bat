@echo off
setlocal
set "DOTNET=dotnet"
if exist "%USERPROFILE%\.dotnet10\dotnet.exe" set "DOTNET=%USERPROFILE%\.dotnet10\dotnet.exe"

pushd ..
"%DOTNET%" publish LanceServer\LanceServer.csproj -c Debug -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o lance-extension\server\win-x64
popd

if exist server\win-x64\config.json del server\win-x64\config.json
if exist server\win-x64\preprocessor_config.json del server\win-x64\preprocessor_config.json
