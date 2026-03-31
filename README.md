# 🌑 VANGUARD C2: THE BLACK EDITION [FINAL]
### *Superiority in Resilience, Stealth, and Operational Continuity.*

![Edition](https://img.shields.io/badge/Edition-Black_V3.0-black?style=for-the-badge)
![Tech](https://img.shields.io/badge/Tech-NativeAOT_Monolith-blue?style=for-the-badge)
![Redundancy](https://img.shields.io/badge/Redundancy-Triple_Redundant-red?style=for-the-badge)

---

## 🇺🇸 PROJECT OVERVIEW (ENGLISH)

**Vanguard C2** is a modular, high-resilience framework designed for elite red-teaming operations. The **Black Edition** represents the pinnacle of C2 evolution, focusing on **Triple-Redundancy Communication**, **Advanced Anti-Analysis**, and **Self-Healing Connectivity**. It is compiled into a zero-dependency, monolithic binary that functions as a single source of truth for all operational activities.

### 📡 1. UNSTOPPABLE COMMUNICATION (C2 RESILIENCE)
The Black Edition utilizes a **Quad-Stage Fallback Routine** to ensure the operator never loses control:
| Priority | Transport | Mechanism | Use Case |
| :--- | :--- | :--- | :--- |
| **P1** | **Direct** | Standard HTTPS to Telegram API | Internal Testing / Clear Regions |
| **P2** | **Proxy L1/L2** | Hardened SOCKS5 VPS Tunnels | Standard Deployment / ISP Bypassing |
| **P3** | **Gateway Fronting** | Cloudflare/Workers CDN Fronting | China/GFW/Deep Packet Inspection |
| **P4** | **Gist Proxy Mesh** | Dynamic Mesh Grid via GitHub Gists | API Ban / Total Connectivity Loss |

> [!IMPORTANT]
> **Token Rotation (Failover)**: If `BOT_TOKEN_1` is neutralized, the orchestrator automatically rotates to `BOT_TOKEN_2` and `BOT_TOKEN_3` without interrupting the active session.

### 🛡️ 2. ELITE STEALTH & DEFENSE
*   **StringVault 2.0 Encryption**: All sensitive strings (APIs, IDs, Paths) are AES-encrypted at build-time using machine-unique salts.
*   **In-Memory Patching**: Dynamic patching of `AMSI.dll` and `ETW` at startup to blind system sensors.
*   **Process Hollowing V2**: Multi-target hollowing for launching pro-modules (`Bore`, `DiscordProSvc`) from white-listed system processes.
*   **Nuclear Trimming**: Optimized for the highest NativeAOT compression, resulting in a binary that lacks metadata and standard reflection signatures.

### 📦 3. MODULAR ARSENAL
*   **Omnivore Stealer**: Universal browser extraction supporting 30+ variants, bypassing ABE (App-Bound Encryption) and master-key protection.
*   **Discord Pro Svc**: A hidden WebView bridge that allows the operator to execute remote access through the victim's Discord token.
*   **Steam Phishing Client**: Pixel-perfect Motiva Sans-styled UI for high-fidelity credential harvesting.
*   **Silent Keylogger**: Stealthy input monitoring with dynamic IPC relay to the main bot.

---

## 🇷🇺 ПОЛНОЕ ОПИСАНИЕ (РУССКИЙ)

**Vanguard Black Edition** — это ультимативный фреймворк для скрытного управления и извлечения данных. Основной акцент сделан на **автономности модуля**, **тройном резервировании связи** и **обходе современных систем обнаружения (EDR/AV)**. Проект собирается в монолитный бинарник без внешних зависимостей.

### 📡 1. СЛОИСТАЯ СИСТЕМА СВЯЗИ
Бот использует 4 уровня выживания при блокировках:
1.  **Прямой канал**: Обмен данными напрямую с API Телеграм.
2.  **Защищенные туннели**: Работа через L1/L2 SOCKS5 прокси (VPS).
3.  **CDN Fronting**: Использование Cloudflare Worker в качестве шлюза для обхода блокировок на уровне DPI.
4.  **Mesh Grid**: Динамический список резервных прокси через GitHub Gist, если все остальные методы заблокированы.

### 🛡️ 2. ТЕХНОЛОГИИ ЗАЩИТЫ
*   **StringVault 2.0**: Шифрование всех строк внутри кода. Никаких токенов или IP в открытом виде — только зашифрованные куски данных, которые расшифровываются прямо в памяти.
*   **Патчинг в памяти**: Бот "ослепляет" защитников Windows, патча AMSI и ETW при запуске. Это позволяет модулям работать незаметно для антивирусов.
*   **NativeAOT Monolith**: Весь проект транслируется в нативный машинный код. Это убирает метаданные .NET и делает невозможным стандартный реверс-инжиниринг.

### 📦 3. МОДУЛЬНЫЙ ФУНКЦИОНАЛ
*   **Браузерный Стиллер**: Извлечение паролей, куки и токенов из 30+ браузеров, включая защиту Edge и Chrome (ABE).
*   **Discord Pro**: Модуль удаленного доступа, использующий украденный токен жертвы для входа через скрытый браузер.
*   **Steam Phishing**: Пиксель-перфект копия логин-окна Steam для сбора аутентификационных данных.
*   **Клавиатурный Шпион**: Скрытый мониторинг ввода с мгновенной отправкой отчетов в панель управления.

---

### 🛠️ CONFIGURATION & BUILD
1.  Open `full_rebuild.ps1`.
2.  Populate `$Secrets` with your Bot Tokens, Admin ID, and Gist Tokens.
3.  Execute `powershell -File full_rebuild.ps1`.
4.  The finalized monolith is located in `bin/Release/net8.0-windows/win-x64/publish/MicrosoftManagementSvc.exe`.

---

## ⚠️ LEGAL NOTICE
Unauthorized use of this tool on systems you do not own is strictly prohibited. This framework is designed for legal cybersecurity research and authorized penetration testing campaigns.
