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

if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

:: 1. SQLite
echo [1/4] Compiling SQLite3...
cl %CFLAGS_SQLITE% /c "%LIBS_DIR%\sqlite\sqlite3.c" /Fo"%BUILD_DIR%\sqlite3.obj" >nul 2>&1
lib /NOLOGO /LTCG /OUT:"%BUILD_DIR%\sqlite3.lib" "%BUILD_DIR%\sqlite3.obj" >nul 2>&1

:: 2. Payload (Decryption logic)
echo [2/4] Compiling Decryption Payload...
cl %CFLAGS_COMMON% /c "%SRC_DIR%\sys\bootstrap.cpp" /Fo"%BUILD_DIR%\bootstrap.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%LIBS_DIR%\sqlite" /c "%SRC_DIR%\payload\payload_main.cpp" /Fo"%BUILD_DIR%\payload_main.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\com\elevator.cpp" /Fo"%BUILD_DIR%\elevator.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /I"%LIBS_DIR%\sqlite" /c "%SRC_DIR%\payload\data_extractor.cpp" /Fo"%BUILD_DIR%\data_extractor.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\crypto\aes_gcm.cpp" /Fo"%BUILD_DIR%\aes_gcm.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\crypto\chacha20.cpp" /Fo"%BUILD_DIR%\chacha20.obj"
cl %CFLAGS_COMMON% %CFLAGS_CPP% /c "%SRC_DIR%\sys\internal_api.cpp" /Fo"%BUILD_DIR%\internal_api_payload.obj"
ml64.exe /nologo /c /Fo"%BUILD_DIR%\syscall_trampoline_payload.obj" "%SRC_DIR%\sys\syscall_trampoline_x64.asm"

link /NOLOGO /LTCG /DLL /OUT:"%BUILD_DIR%\%PAYLOAD_DLL_NAME%" ^
    "%BUILD_DIR%\payload_main.obj" "%BUILD_DIR%\bootstrap.obj" "%BUILD_DIR%\elevator.obj" ^
    "%BUILD_DIR%\data_extractor.obj" "%BUILD_DIR%\aes_gcm.obj" ^
    "%BUILD_DIR%\chacha20.obj" "%BUILD_DIR%\internal_api_payload.obj" ^
    "%BUILD_DIR%\syscall_trampoline_payload.obj" "%BUILD_DIR%\sqlite3.lib" ^
    bcrypt.lib ole32.lib oleaut32.lib shell32.lib advapi32.lib crypt32.lib >nul 2>&1

:: 3. Encryptor (Required to embed payload into headers)
echo [3/4] Generating Payload Header...
cl %CFLAGS_COMMON% %CFLAGS_CPP% /Fe"%BUILD_DIR%\%ENCRYPTOR_EXE_NAME%" ^
    "%SRC_DIR%\tools\encryptor.cpp" "%BUILD_DIR%\chacha20.obj" /link /NOLOGO bcrypt.lib >nul 2>&1

"%BUILD_DIR%\%ENCRYPTOR_EXE_NAME%" "%BUILD_DIR%\%PAYLOAD_DLL_NAME%" "%BUILD_DIR%\chrome_decrypt.enc" "%BUILD_DIR%\%PAYLOAD_HEADER%" >nul 2>&1

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

lib /NOLOGO /LTCG /OUT:".\chromelevator.lib" ^
    "%BUILD_DIR%\injector_main.obj" "%BUILD_DIR%\browser_discovery.obj" ^
    "%BUILD_DIR%\browser_terminator.obj" "%BUILD_DIR%\process_manager.obj" ^
    "%BUILD_DIR%\pipe_server.obj" "%BUILD_DIR%\injector.obj" ^
    "%BUILD_DIR%\internal_api.obj" "%BUILD_DIR%\chacha20.obj" ^
    "%BUILD_DIR%\syscall_trampoline.obj" ^
    "%BUILD_DIR%\elevator.obj" "%BUILD_DIR%\sqlite3.lib"

echo [+] SUCCESS: chromelevator.lib is ready for monolithic integration.
