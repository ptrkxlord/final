// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#pragma once

#include "../core/common.hpp"
#include <vector>
#include <string>
#include <optional>

namespace Injector {

    struct BrowserInfo {
        std::wstring type;
        std::wstring exeName;
        std::wstring exePath;
        std::wstring userDataPath;
        std::string displayName;
        std::string version;
        bool isGecko = false;
    };

    class BrowserDiscovery {
    public:
        static std::vector<BrowserInfo> FindAll();
        static std::optional<BrowserInfo> FindSpecific(const std::wstring& type);

    private:
        struct PathResult {
            std::wstring exePath;
            std::wstring exeName;
            std::wstring userDataPath;
        };
        static PathResult ResolvePaths(const std::wstring& browserType, const std::wstring& exeName);
        static std::wstring QueryRegistryValue(const std::wstring& keyPath, const std::wstring& valueName);
        static std::string GetFileVersion(const std::wstring& filePath);
        static std::wstring ResolvePackageFolder(const std::wstring& prefix);
        static std::wstring FindUserAppData();
        static std::vector<BrowserInfo> DeepScan();
    };

}
