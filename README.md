# 🦆🐀 DUCK DUCK RAT: GHOST PHOENIX EDITION
### *The Pinnacle of Stealth, Resilience, and Operational Dominance.*

![Banner](file:///C:/Users/zxc23/.gemini/antigravity/brain/73770afa-43e7-4acd-8eb4-d05be495e056/duckduckrat_banner_1775148818864.png)

![Edition](https://img.shields.io/badge/Edition-Ghost_Phoenix_v8.1-cyan?style=for-the-badge)
![Tech](https://img.shields.io/badge/Tech-NativeAOT_Monolith-black?style=for-the-badge)
![Stealth](https://img.shields.io/badge/Stealth-Zero_Telemetry-green?style=for-the-badge)

---

## 🇺🇸 PROJECT OVERVIEW (ENGLISH)

**DuckDuckRat** is an elite, high-resilience Command & Control (C2) framework engineered for advanced post-exploitation scenarios. The **Ghost Phoenix Edition** introduces a revolutionary stealth architecture, focusing on **Hardware Breakpoint Evasion**, **COM-based UAC bypass**, and **Global Session Monarchy**.

### 📡 1. UNSTOPPABLE COMMUNICATION (C2 RESILIENCE)
The framework utilizes a **Quad-Stage Fallback Routine** to ensure operational continuity in hostile network environments:
| Priority | Transport | Mechanism | Use Case |
| :--- | :--- | :--- | :--- |
| **P1** | **Direct** | Standard HTTPS to Telegram API | Internal Testing / Clear Regions |
| **P2** | **Proxy L1/L2** | Hardened SOCKS5 VPS Tunnels | Standard Deployment / ISP Bypassing |
| **P3** | **Gateway Fronting** | Cloudflare/Workers CDN Fronting | China/GFW/Deep Packet Inspection |
| **P4** | **Gist Proxy Mesh** | Dynamic Mesh Grid via GitHub Gists | API Ban / Total Connectivity Loss |

### 🛡️ 2. GHOST PHOENIX STEALTH ENGINE
*   **Reflective Shield**: Zero-byte patching of `AMSI.dll` and `ETW` using hardware breakpoints and VEH (Vectored Exception Handling) to blind system sensors without modifying code files.
*   **COM Elevation**: Leverages `ICMLuaUtil` Elevated Monikers for silent, registry-free UAC bypass, eliminating all behavioral detection triggers.
*   **Session Monarchy**: A token-bound Global Mutex ensures that only one polling instance exists per machine, preventing C2 session conflicts and API bans.
*   **The Janitor**: Proactive registry and task sanitization to purge legacy artifacts (`FinalBot`, `EmoCore`) and keep a zero-trace profile.

### 📦 3. MODULAR ARSENAL
*   **Omnivore Stealer**: Advanced browser extraction supporting 30+ variants, bypassing App-Bound Encryption (ABE) and DPAPI protection.
*   **Discord Pro Svc**: Hidden WebView integration for remote access through hijacked Discord sessions.
*   **Process Hollowing**: Stealthy injection of pro-modules into white-listed system processes like `svchost.exe` and `explorer.exe`.

---

## 🇷🇺 ПОЛНОЕ ОПИСАНИЕ (РУССКИЙ)

**DuckDuckRat** — это ультимативный фреймворк для скрытного управления и извлечения данных в самых агрессивных средах (EDR/AV). Издание **Ghost Phoenix** устанавливает новые стандарты незаметности, используя низкоуровневые хуки аппаратного уровня и бесшумные методы повышения прав.

### 🛡️ ТЕХНОЛОГИИ ПРИЗРАКА
*   **Reflective Shield**: Ослепление AMSI и ETW через аппаратные брейкпоинты. Бот не меняет код системных DLL, работая на уровне прерываний процессора.
*   **COM Elevation**: Бесшумный обход UAC через системные моникеры `ICMLuaUtil`. Никаких подозрительных ключей в реестре — только легитимные вызовы Windows.
*   **Монархия сессий**: Глобальный Mutex, привязанный к токену бота, исключает конфликты поллинга и бан сессий.
*   **Генеральная уборка**: Модуль `The Janitor` вычищает любые следы старых версий бикона и артефакты из планировщика задач.

---

### 🛠️ CONFIGURATION & BUILD
1.  Configure your C2 secrets in `full_rebuild.ps1`.
2.  Assign your startup banner GIF ID in `Core/Constants.cs`.
3.  Execute `powershell -File full_rebuild.ps1`.
4.  The final result is a zero-dependency, system-mimicking binary (e.g. `WinUpdate_XXXX.exe`).

---

## ⚠️ LEGAL NOTICE
Unauthorized use of this tool on systems you do not own is strictly prohibited. This framework is designed for legal cybersecurity research and authorized penetration testing campaigns.
