@echo off
call "I:\Microsoft\VC\Auxiliary\Build\vcvars32.bat" >nul
cl /nologo /EHsc /O2 /W4 /MT /I"I:\AO\src\Tools\AOClientRoomSpaceGuard\ProxyDll\src" main2.cpp "I:\AO\src\Tools\AOClientRoomSpaceGuard\ProxyDll\src\battlesuit_social_patch.cpp" "I:\AO\src\Tools\AOClientRoomSpaceGuard\ProxyDll\src\logging.cpp" /Fe:bstest2.exe /link /MACHINE:X86 || exit /b 1
bstest2.exe I:\AO\client-ddogs\Gamecode.dll
