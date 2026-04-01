// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#include "aes_gcm.hpp"
#include <memory>

#pragma comment(lib, "bcrypt.lib")

#ifndef NT_SUCCESS
#define NT_SUCCESS(Status) (((NTSTATUS)(Status)) >= 0)
#endif

namespace Crypto {

    std::optional<std::vector<uint8_t>> AesGcm::Decrypt(const std::vector<uint8_t>& key, const std::vector<uint8_t>& encryptedData) {
        // Chromium Standard: [vXX Prefix (3 bytes)] + IV(12) + Ciphertext + Tag(16)
        // Some older implementations might omit the prefix.
        
        size_t prefixLen = 0;
        if (encryptedData.size() > 3 && encryptedData[0] == 'v' && isdigit(encryptedData[1]) && isdigit(encryptedData[2])) {
            prefixLen = 3;
        }

        constexpr size_t IV_LEN = 12;
        constexpr size_t TAG_LEN = 16;
        size_t overhead = prefixLen + IV_LEN + TAG_LEN;

        if (encryptedData.size() < overhead) return std::nullopt;

        BCRYPT_ALG_HANDLE hAlg = nullptr;
        if (!NT_SUCCESS(BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_AES_ALGORITHM, nullptr, 0))) return std::nullopt;
        
        auto algCloser = [](BCRYPT_ALG_HANDLE h) { if(h) BCryptCloseAlgorithmProvider(h, 0); };
        std::unique_ptr<void, decltype(algCloser)> algGuard(hAlg, algCloser);

        if (!NT_SUCCESS(BCryptSetProperty(hAlg, BCRYPT_CHAINING_MODE, (PUCHAR)BCRYPT_CHAIN_MODE_GCM, sizeof(BCRYPT_CHAIN_MODE_GCM), 0))) return std::nullopt;

        BCRYPT_KEY_HANDLE hKey = nullptr;
        if (!NT_SUCCESS(BCryptGenerateSymmetricKey(hAlg, &hKey, nullptr, 0, (PUCHAR)key.data(), (ULONG)key.size(), 0))) return std::nullopt;

        auto keyCloser = [](BCRYPT_KEY_HANDLE h) { if(h) BCryptDestroyKey(h); };
        std::unique_ptr<void, decltype(keyCloser)> keyGuard(hKey, keyCloser);

        const uint8_t* iv = encryptedData.data() + prefixLen;
        const uint8_t* tag = encryptedData.data() + (encryptedData.size() - TAG_LEN);
        const uint8_t* ct = iv + IV_LEN;
        ULONG ctLen = static_cast<ULONG>(encryptedData.size() - overhead);

        BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authInfo;
        BCRYPT_INIT_AUTH_MODE_INFO(authInfo);
        authInfo.pbNonce = (PUCHAR)iv;
        authInfo.cbNonce = IV_LEN;
        authInfo.pbTag = (PUCHAR)tag;
        authInfo.cbTag = TAG_LEN;

        std::vector<uint8_t> plain(ctLen > 0 ? ctLen : 1);
        ULONG outLen = 0;

        if (!NT_SUCCESS(BCryptDecrypt(hKey, (PUCHAR)ct, ctLen, &authInfo, nullptr, 0, plain.data(), (ULONG)plain.size(), &outLen, 0))) {
            return std::nullopt;
        }

        plain.resize(outLen);
        return plain;
    }

}
