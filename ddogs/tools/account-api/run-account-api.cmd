@echo off
rem Dungeon Dogs AO account service (port 8098)
set "DOTNET_ROOT=I:\AO\tools\dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
set "DOTNET_CLI_HOME=I:\AO\cache\dotnet-home"
set "TEMP=I:\AO\cache\temp"
set "TMP=I:\AO\cache\temp"
set "AO_ACCOUNT_API_PORT=8098"
"I:\AO\tools\dotnet\dotnet.exe" "I:\AO\tools\account-api\bin-publish\account-api.dll" >> "I:\AO\logs\account-api.out.log" 2>&1
