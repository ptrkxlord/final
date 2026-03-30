// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#include "process_manager.hpp"
#include "../sys/internal_api.hpp"
#include <iostream>

namespace Injector {

    ProcessManager::ProcessManager(const BrowserInfo& browser) : m_browser(browser) {}

    ProcessManager::~ProcessManager() {
        // Ensure cleanup if not explicitly terminated
        if (m_hProcess) Terminate();
    }

    void ProcessManager::CreateSuspended() {
        STARTUPINFOW si{};
        PROCESS_INFORMATION pi{};
        si.cb = sizeof(si);

        if (!CreateProcessW(m_browser.exePath.c_str(), nullptr, nullptr, nullptr,
                            FALSE, CREATE_SUSPENDED, nullptr, nullptr, &si, &pi)) {
            throw std::runtime_error("CreateProcessW failed: " + std::to_string(GetLastError()));
        }

        m_hProcess.reset(pi.hProcess);
        m_hThread.reset(pi.hThread);
        m_pid = pi.dwProcessId;

        CheckArchitecture();
    }

    void ProcessManager::Attach(DWORD pid) {
        // Open process with required access for injection
        m_hProcess.reset(OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid));
        if (!m_hProcess) {
            throw std::runtime_error("OpenProcess failed: " + std::to_string(GetLastError()));
        }
        m_pid = pid;
        m_hThread.reset(nullptr); // No thread handle for attach
        
        CheckArchitecture();
    }

    void ProcessManager::Terminate() {
        if (m_hProcess && m_hThread) { // Only terminate if we created it
            NtTerminateProcess_syscall(m_hProcess.get(), 0);
            WaitForSingleObject(m_hProcess.get(), 2000);
            m_hProcess.reset();
        } else if (m_hProcess) {
            m_hProcess.reset(); // Just detach
        }
    }

    void ProcessManager::CheckArchitecture() {
        USHORT processMachine = 0;
        USHORT nativeMachine = 0;
        
        if (!IsWow64Process2(m_hProcess.get(), &processMachine, &nativeMachine)) {
             // Fallback for older Windows or failures: assume injector matches target if we can open it
             // but better to be explicit.
             throw std::runtime_error("IsWow64Process2 failed: " + std::to_string(GetLastError()));
        }

        // If processMachine is IMAGE_FILE_MACHINE_UNKNOWN, it's native (matching nativeMachine)
        m_arch = (processMachine == IMAGE_FILE_MACHINE_UNKNOWN) ? nativeMachine : processMachine;

        // Injector architecture detection
#if defined(_M_X64)
        constexpr USHORT injectorArch = IMAGE_FILE_MACHINE_AMD64;
        constexpr const char* injectorArchName = "x64";
#elif defined(_M_ARM64)
        constexpr USHORT injectorArch = IMAGE_FILE_MACHINE_ARM64;
        constexpr const char* injectorArchName = "ARM64";
#else
        constexpr USHORT injectorArch = 0;
        constexpr const char* injectorArchName = "Unknown";
#endif

        if (m_arch != injectorArch) {
            std::string error = "Architecture mismatch!\n";
            error += "  Injector: " + std::string(injectorArchName) + "\n";
            error += "  Target:   0x" + std::to_string(m_arch) + "\n";
            error += "  Solution: Use native version of chromelevator.exe";
            throw std::runtime_error(error);
        }
    }

}
