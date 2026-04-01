// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#include "browser_discovery.hpp"
#include "../sys/internal_api.hpp"
#include <algorithm>
#include <map>
#include <filesystem>

#pragma comment(lib, "version.lib")

namespace Injector {

    namespace {
        const std::map<std::wstring, std::pair<std::wstring, std::string>> g_browserMap = {
            // Western browsers
            {L"chrome", {L"chrome.exe", "Chrome"}},
            {L"chrome-beta", {L"chrome.exe", "Chrome Beta"}},
            {L"chrome-dev", {L"chrome.exe", "Chrome Dev"}},
            {L"chrome-canary", {L"chrome.exe", "Chrome Canary"}},
            {L"edge", {L"msedge.exe", "Edge"}},
            {L"edge-beta", {L"msedge.exe", "Edge Beta"}},
            {L"edge-dev", {L"msedge.exe", "Edge Dev"}},
            {L"edge-canary", {L"msedge.exe", "Edge Canary"}},
            {L"brave", {L"brave.exe", "Brave"}},
            {L"brave-beta", {L"brave.exe", "Brave Beta"}},
            {L"avast", {L"AvastBrowser.exe", "Avast"}},
            {L"opera", {L"opera.exe", "Opera"}},
            {L"opera-gx", {L"opera.exe", "Opera GX"}},
            {L"vivaldi", {L"vivaldi.exe", "Vivaldi"}},
            {L"yandex", {L"browser.exe", "Yandex"}},
            {L"chromium", {L"chrome.exe", "Chromium"}},
            {L"iridium", {L"iridium.exe", "Iridium"}},
            {L"centbrowser", {L"chrome.exe", "Cent"}},
            // Gecko browsers
            {L"firefox", {L"firefox.exe", "Firefox"}},
            {L"waterfox", {L"waterfox.exe", "Waterfox"}},
            {L"palemoon", {L"palemoon.exe", "Pale Moon"}},
            // Chinese browsers
            {L"360se", {L"360se.exe", "360 Safe Browser"}},
            {L"360chrome", {L"360chrome.exe", "360 Chrome"}},
            {L"qq", {L"QQBrowser.exe", "QQ Browser"}},
            {L"sogou", {L"SogouExplorer.exe", "Sogou Explorer"}},
            {L"uc", {L"UCBrowser.exe", "UC Browser"}},
            {L"maxthon", {L"Maxthon.exe", "Maxthon"}},
            {L"baidu", {L"BaiduBrowser.exe", "Baidu Browser"}},
            {L"coccoc", {L"coccoc.exe", "Cốc Cốc"}},
            // Add more as needed
        };
    }

    static bool ValidatePathForBrowser(const std::wstring& path, const std::wstring& browserType) {
        std::wstring lowerPath = path;
        std::transform(lowerPath.begin(), lowerPath.end(), lowerPath.begin(), ::towlower);

        if (browserType == L"chrome") {
            return lowerPath.find(L"\\google\\chrome\\") != std::wstring::npos &&
                   lowerPath.find(L"\\google\\chrome beta\\") == std::wstring::npos;
        } else if (browserType == L"chrome-beta") {
            return lowerPath.find(L"\\google\\chrome beta\\") != std::wstring::npos;
        } else if (browserType == L"chrome-dev") {
            return lowerPath.find(L"\\google\\chrome dev\\") != std::wstring::npos;
        } else if (browserType == L"chrome-canary") {
            return lowerPath.find(L"\\google\\chrome sx s\\") != std::wstring::npos || lowerPath.find(L"\\google\\chrome sxs\\") != std::wstring::npos;
        } else if (browserType == L"edge-beta") {
            return lowerPath.find(L"\\microsoft\\edge beta\\") != std::wstring::npos;
        } else if (browserType == L"edge-dev") {
            return lowerPath.find(L"\\microsoft\\edge dev\\") != std::wstring::npos;
        } else if (browserType == L"edge-canary") {
            return lowerPath.find(L"\\microsoft\\edge canary\\") != std::wstring::npos;
        } else if (browserType == L"qq") {
            return lowerPath.find(L"\\tencent\\qqbrowser\\") != std::wstring::npos;
        } else if (browserType == L"360chrome") {
            return lowerPath.find(L"\\360chrome\\") != std::wstring::npos;
        } else if (browserType == L"sogou") {
            return lowerPath.find(L"\\sogouexplorer\\") != std::wstring::npos;
        } else if (browserType == L"brave") {
            return lowerPath.find(L"\\brave") != std::wstring::npos;
        } else if (browserType == L"vivaldi") {
            return lowerPath.find(L"\\vivaldi") != std::wstring::npos;
        } else if (browserType == L"opera") {
            return lowerPath.find(L"\\opera") != std::wstring::npos && lowerPath.find(L"gx") == std::wstring::npos;
        } else if (browserType == L"opera-gx") {
            return lowerPath.find(L"\\opera") != std::wstring::npos && lowerPath.find(L"gx") != std::wstring::npos;
        }
        return true;
    }

    std::vector<BrowserInfo> BrowserDiscovery::FindAll() {
        std::vector<BrowserInfo> results;
        for (const auto& info : g_browserMap) {
            std::wstring type = info.first;
            auto paths = ResolvePaths(type, info.second.first);

            if (!paths.exePath.empty() || !paths.userDataPath.empty()) {
                if (!paths.exePath.empty() && !ValidatePathForBrowser(paths.exePath, type)) continue;

                bool isGecko = (type == L"firefox" || type == L"waterfox" || type == L"palemoon");
                results.push_back({type, paths.exeName, paths.exePath, paths.userDataPath, info.second.second, GetFileVersion(paths.exePath), isGecko, paths.isColdOnly});
            }
        }
        
        // Deep Scan fallback for unknown clones
        auto deepOnes = DeepScan();
        for (const auto& d : deepOnes) {
            // Deduplicate: avoid if userDataPath is already in results
            bool exists = false;
            for (const auto& r : results) {
                if (r.userDataPath == d.userDataPath) {
                    exists = true;
                    break;
                }
            }
            if (!exists) results.push_back(d);
        }

        return results;
    }

    std::optional<BrowserInfo> BrowserDiscovery::FindSpecific(const std::wstring& type) {
        std::wstring lowerType = type;
        std::transform(lowerType.begin(), lowerType.end(), lowerType.begin(), ::towlower);

        auto it = g_browserMap.find(lowerType);
        if (it == g_browserMap.end()) return std::nullopt;

        auto paths = ResolvePaths(lowerType, it->second.first);
        if (paths.exePath.empty() && paths.userDataPath.empty()) return std::nullopt;

        return BrowserInfo{lowerType, paths.exeName, paths.exePath, paths.userDataPath, it->second.second, GetFileVersion(paths.exePath)};
    }

    BrowserDiscovery::PathResult BrowserDiscovery::ResolvePaths(const std::wstring& browserType, const std::wstring& exeName) {
        std::vector<std::pair<std::wstring, std::wstring>> altRegistry;
        PathResult res;
        res.exeName = exeName;
        std::wstring localApp = FindUserAppData();

        // Standard registry heuristics
        altRegistry = {
            {L"\\Registry\\Machine\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\" + exeName, L""},
            {L"\\Registry\\Machine\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\App Paths\\" + exeName, L""},
            {L"\\Registry\\Machine\\SOFTWARE\\Clients\\StartMenuInternet\\" + (browserType == L"chrome" ? L"Google Chrome" : (browserType == L"edge" ? L"Microsoft Edge" : exeName)) + L"\\shell\\open\\command", L""}
        };

        if (browserType.find(L"edge") != std::wstring::npos) {
            altRegistry.push_back({L"\\Registry\\Machine\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge", L"InstallLocation"});
            altRegistry.push_back({L"\\Registry\\Machine\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge", L"InstallLocation"});
            altRegistry.push_back({L"\\Registry\\Machine\\SOFTWARE\\Microsoft\\EdgeUpdate\\Clients\\{56EB18F8-8008-4CBD-B6D0-58844795078C}", L"location"});
        } else if (browserType == L"firefox") {
            altRegistry.push_back({L"\\Registry\\Machine\\SOFTWARE\\Mozilla\\Mozilla Firefox", L"CurrentVersion"});
            // Special handling for Firefox version-based path
            auto version = QueryRegistryValue(L"\\Registry\\Machine\\SOFTWARE\\Mozilla\\Mozilla Firefox", L"CurrentVersion");
            if (!version.empty()) {
                auto path = QueryRegistryValue(L"\\Registry\\Machine\\SOFTWARE\\Mozilla\\Mozilla Firefox\\" + version + L"\\Main", L"Install Directory");
                if (!path.empty()) {
                    res.exePath = path + L"\\firefox.exe";
                    res.userDataPath = localApp + L"\\Mozilla\\Firefox\\Profiles";
                    if (std::filesystem::exists(res.exePath)) return res;
                }
            }
            // Firefox Store version
            auto pkg = ResolvePackageFolder(L"Mozilla.Firefox");
            if (!pkg.empty()) {
                res.exePath = pkg + L"\\VFS\\ProgramFiles\\Firefox\\firefox.exe"; // Heuristic
                res.userDataPath = pkg + L"\\LocalCache\\Roaming\\Mozilla\\Firefox\\Profiles";
                return res;
            }
        } else if (browserType == L"brave") {
            auto pkg = ResolvePackageFolder(L"BraveSoftwareInc.BraveBrowser");
            if (!pkg.empty()) {
                res.userDataPath = pkg + L"\\LocalCache\\Roaming\\BraveSoftware\\Brave-Browser\\User Data";
                return res;
            }
        } else if (browserType == L"vivaldi") {
            auto pkg = ResolvePackageFolder(L"VivaldiTechnologies.VivaldiBrowser");
            if (!pkg.empty()) {
                res.userDataPath = pkg + L"\\LocalCache\\Roaming\\Vivaldi\\User Data";
                return res;
            }
        } else if (browserType == L"opera" || browserType == L"opera-gx") {
            auto pkg = ResolvePackageFolder(L"OperaSoftwareAS.Opera");
            if (!pkg.empty()) {
                res.userDataPath = pkg + L"\\LocalCache\\Roaming\\Opera Software\\" + (browserType == L"opera-gx" ? L"Opera GX Stable" : L"Opera Stable");
                return res;
            }
        }

        // Broad disk heuristics as absolute fallback
        wchar_t* userProfile;
        if (_wdupenv_s(&userProfile, nullptr, L"USERPROFILE") == 0 && userProfile) {
            std::wstring up(userProfile);
            free(userProfile);
            
            std::vector<std::pair<std::wstring, std::wstring>> heuristicPairs = {
                {up + L"\\AppData\\Local\\" + browserType + L"\\Application\\" + exeName, up + L"\\AppData\\Local\\" + browserType + L"\\User Data"},
                {up + L"\\AppData\\Local\\Google\\Chrome\\Application\\" + exeName, up + L"\\AppData\\Local\\Google\\Chrome\\User Data"},
                {up + L"\\AppData\\Local\\BraveSoftware\\Brave-Browser\\Application\\brave.exe", up + L"\\AppData\\Local\\BraveSoftware\\Brave-Browser\\User Data"},
                {up + L"\\AppData\\Local\\Vivaldi\\Application\\vivaldi.exe", up + L"\\AppData\\Local\\Vivaldi\\User Data"}
            };

            for (const auto& pair : heuristicPairs) {
                try {
                    if (pair.first.find(exeName) != std::wstring::npos && std::filesystem::exists(pair.first)) {
                        res.exePath = pair.first;
                        res.userDataPath = pair.second;
                        return res;
                    }
                } catch (...) {}
            }
        }

        for (const auto& [regKey, valueName] : altRegistry) {
            auto result = QueryRegistryValue(regKey, valueName);
            if (!result.empty()) {
                std::wstring fullPath;
                if (valueName == L"InstallLocation" || valueName == L"InstallPath") {
                    fullPath = result + L"\\" + exeName;
                } else {
                    size_t start = (result[0] == L'"') ? 1 : 0;
                    size_t end = result.find(L'"', start);
                    if (end == std::wstring::npos) end = result.find(L' ', start);
                    if (end == std::wstring::npos) end = result.length();
                    fullPath = result.substr(start, end - start);
                }
                
                if (!fullPath.empty() && std::filesystem::exists(fullPath) && ValidatePathForBrowser(fullPath, browserType)) {
                    res.exePath = fullPath;
                    // Guess UserData based on exePath
                    if (res.userDataPath.empty()) {
                        if (fullPath.find(L"\\AppData\\Local\\") != std::wstring::npos) {
                            res.userDataPath = fullPath.substr(0, fullPath.find(L"\\Application\\")) + L"\\User Data";
                        } else {
                            // Fallback to local appdata with vendor folders
                            if (browserType == L"chrome") res.userDataPath = localApp + L"\\Google\\Chrome\\User Data";
                            else if (browserType.find(L"edge") != std::wstring::npos) res.userDataPath = localApp + L"\\Microsoft\\Edge\\User Data";
                            else if (browserType == L"brave") res.userDataPath = localApp + L"\\BraveSoftware\\Brave-Browser\\User Data";
                            else if (browserType == L"vivaldi") res.userDataPath = localApp + L"\\Vivaldi\\User Data";
                            else res.userDataPath = localApp + L"\\" + browserType + L"\\User Data";
                        }
                    }
                    return res;
                }
            }
        }

        // Generic check: if UserData is in Packages, it's likely a Store/Sandbox app
        if (!res.userDataPath.empty()) {
            std::wstring lowerUDP = res.userDataPath;
            std::transform(lowerUDP.begin(), lowerUDP.end(), lowerUDP.begin(), ::towlower);
            if (lowerUDP.find(L"\\packages\\") != std::wstring::npos) {
                res.isColdOnly = true;
            }
        }

        return res;
    }

    std::wstring BrowserDiscovery::FindUserAppData() {
        wchar_t* localAppData;
        if (_wdupenv_s(&localAppData, nullptr, L"LOCALAPPDATA") == 0 && localAppData) {
            std::wstring res(localAppData);
            free(localAppData);
            return res;
        }
        return L"";
    }

    std::wstring BrowserDiscovery::QueryRegistryValue(const std::wstring& keyPath, const std::wstring& valueName) {
        std::vector<wchar_t> pathBuffer(keyPath.begin(), keyPath.end());
        pathBuffer.push_back(L'\0');

        UNICODE_STRING_SYSCALLS keyName;
        keyName.Buffer = pathBuffer.data();
        keyName.Length = static_cast<USHORT>(keyPath.length() * sizeof(wchar_t));
        keyName.MaximumLength = static_cast<USHORT>(pathBuffer.size() * sizeof(wchar_t));

        OBJECT_ATTRIBUTES objAttr;
        InitializeObjectAttributes(&objAttr, &keyName, OBJ_CASE_INSENSITIVE, nullptr, nullptr);

        HANDLE hKey = nullptr;
        NTSTATUS status = NtOpenKey_syscall(&hKey, KEY_READ, &objAttr);
        if (status != 0) return L"";

        Core::UniqueHandle keyGuard(hKey);

        std::vector<wchar_t> valueBuffer(valueName.begin(), valueName.end());
        valueBuffer.push_back(L'\0');

        UNICODE_STRING_SYSCALLS valueNameStr;
        valueNameStr.Buffer = valueName.empty() ? nullptr : valueBuffer.data();
        valueNameStr.Length = static_cast<USHORT>(valueName.length() * sizeof(wchar_t));
        valueNameStr.MaximumLength = static_cast<USHORT>(valueBuffer.size() * sizeof(wchar_t));

        ULONG bufferSize = 4096;
        std::vector<BYTE> buffer(bufferSize);
        ULONG resultLength = 0;

        status = NtQueryValueKey_syscall(hKey, &valueNameStr, KeyValuePartialInformation,
                                         buffer.data(), bufferSize, &resultLength);

        if (status == STATUS_BUFFER_TOO_SMALL || status == STATUS_BUFFER_OVERFLOW) {
            buffer.resize(resultLength);
            bufferSize = resultLength;
            status = NtQueryValueKey_syscall(hKey, &valueNameStr, KeyValuePartialInformation,
                                             buffer.data(), bufferSize, &resultLength);
        }

        if (status != 0) return L"";

        auto kvpi = reinterpret_cast<PKEY_VALUE_PARTIAL_INFORMATION>(buffer.data());
        if (kvpi->Type != 1 && kvpi->Type != 2) return L"";
        if (kvpi->DataLength < sizeof(wchar_t) * 2) return L"";

        size_t charCount = kvpi->DataLength / sizeof(wchar_t);
        std::wstring path(reinterpret_cast<wchar_t*>(kvpi->Data), charCount);
        while (!path.empty() && path.back() == L'\0') path.pop_back();

        if (path.empty()) return L"";

        if (kvpi->Type == 2) {
            std::vector<wchar_t> expanded(MAX_PATH * 2);
            DWORD size = ExpandEnvironmentStringsW(path.c_str(), expanded.data(), static_cast<DWORD>(expanded.size()));
            if (size > 0 && size <= expanded.size()) {
                path = std::wstring(expanded.data());
            }
        }

        return path;
    }

    std::wstring BrowserDiscovery::ResolvePackageFolder(const std::wstring& prefix) {
        wchar_t* localAppData;
        if (_wdupenv_s(&localAppData, nullptr, L"LOCALAPPDATA") != 0 || !localAppData) return L"";
        
        std::wstring packagesPath = std::wstring(localAppData) + L"\\Packages";
        free(localAppData);

        try {
            if (!std::filesystem::exists(packagesPath)) return L"";

            std::wstring lowerPrefix = prefix;
            std::transform(lowerPrefix.begin(), lowerPrefix.end(), lowerPrefix.begin(), ::towlower);

            for (const auto& entry : std::filesystem::directory_iterator(packagesPath)) {
                if (entry.is_directory()) {
                    std::wstring name = entry.path().filename().wstring();
                    std::wstring lowerName = name;
                    std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(), ::towlower);
                    
                    if (lowerName.find(lowerPrefix) == 0) {
                        return entry.path().wstring();
                    }
                }
            }
        } catch (...) {}

        return L"";
    }

    std::string BrowserDiscovery::GetFileVersion(const std::wstring& filePath) {
        DWORD dummy = 0;
        DWORD size = GetFileVersionInfoSizeW(filePath.c_str(), &dummy);
        if (size == 0) return "";

        std::vector<BYTE> buffer(size);
        if (!GetFileVersionInfoW(filePath.c_str(), 0, size, buffer.data())) return "";

        VS_FIXEDFILEINFO* fileInfo = nullptr;
        UINT len = 0;
        if (!VerQueryValueW(buffer.data(), L"\\", reinterpret_cast<LPVOID*>(&fileInfo), &len)) return "";
        if (len == 0 || fileInfo == nullptr) return "";

        return std::to_string(HIWORD(fileInfo->dwFileVersionMS)) + "." +
               std::to_string(LOWORD(fileInfo->dwFileVersionMS)) + "." +
               std::to_string(HIWORD(fileInfo->dwFileVersionLS)) + "." +
               std::to_string(LOWORD(fileInfo->dwFileVersionLS));
    }

    std::vector<BrowserInfo> BrowserDiscovery::DeepScan() {
        std::vector<BrowserInfo> deepResults;
        std::vector<std::wstring> roots = { FindUserAppData(), L"" };
        
        wchar_t* roamingBuffer;
        if (_wdupenv_s(&roamingBuffer, nullptr, L"APPDATA") == 0 && roamingBuffer) {
            roots[1] = roamingBuffer;
            free(roamingBuffer);
        }

        for (const auto& root : roots) {
            if (root.empty()) continue;
            try {
                std::filesystem::path rootPath(root);
                if (!std::filesystem::exists(rootPath)) continue;

                for (const auto& entry : std::filesystem::recursive_directory_iterator(rootPath)) {
                    if (entry.is_directory()) {
                        std::wstring path = entry.path().wstring();
                        std::wstring localState = path + L"\\Local State";
                        
                        if (std::filesystem::exists(localState)) {
                            BrowserInfo info;
                            info.userDataPath = path;
                            info.type = L"custom-chromium";
                            info.isGecko = false;
                            
                            auto parent = entry.path().parent_path();
                            for (int i = 0; i < 4 && !parent.empty(); ++i) {
                                bool foundExe = false;
                                for (const auto& file : std::filesystem::directory_iterator(parent)) {
                                    if (file.is_regular_file() && file.path().extension() == L".exe") {
                                        std::wstring exeName = file.path().filename().wstring();
                                        std::wstring lowerExe = exeName;
                                        std::transform(lowerExe.begin(), lowerExe.end(), lowerExe.begin(), ::towlower);
                                        
                                        if (lowerExe == L"setup.exe" || lowerExe == L"uninstall.exe" || 
                                            lowerExe == L"update.exe" || lowerExe == L"crashreporter.exe" ||
                                            lowerExe.find(L"telegram") != std::wstring::npos ||
                                            lowerExe.find(L"discord") != std::wstring::npos) continue;
                                        
                                        info.exePath = file.path().wstring();
                                        info.exeName = exeName;
                                        info.displayName = parent.filename().string();
                                        info.version = GetFileVersion(info.exePath);
                                        foundExe = true;
                                        break;
                                    }
                                }
                                if (foundExe) break;
                                parent = parent.parent_path();
                            }
                            
                            if (!info.exePath.empty()) deepResults.push_back(info);
                        }
                    }
                }
            } catch (...) {}
        }
        return deepResults;
    }

}
