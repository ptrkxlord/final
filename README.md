# 🌑 VANGUARD C2: BLACK EDITION
### *Hardened. Redundant. Unstoppable.*

![Logo](https://img.shields.io/badge/Edition-Black-black?style=for-the-badge&logo=target)
![Platform](https://img.shields.io/badge/Platform-Windows-blue?style=for-the-badge&logo=windows)
![Payload](https://img.shields.io/badge/Payload-NativeAOT-yellow?style=for-the-badge&logo=c-sharp)

---

## 🇺🇸 ENGLISH DESCRIPTION

**Vanguard Black Edition** is a high-performance, multi-layered command and control (C2) infrastructure built for maximum resilience and stealth. It features a monolithic NativeAOT binary architecture that guarantees zero-dependency execution across all modern Windows environments.

### 🛡️ KEY FEATURES
*   **Triple-Redundant Failover**: Automatic rotation between **3 secondary bot tokens** if the primary channel is neutralized.
*   **Censorship-Resistant Routing**: Prioritized route selection: `Direct` ➡️ `L1 VPS` ➡️ `L2 Backup` ➡️ `Gist Proxy Mesh`.
*   **StringVault 2.0**: All sensitive operational data (tokens, IDs, IPs) are AES-encrypted and injected into the binary at build-time.
*   **Stealth Core**: In-memory AMSI/ETW bypasses, process hollowing for pro-modules, and syscall-level execution.
*   **Discord Pro Module**: Integrated hidden browser bridge for real-time user-impersonation and remote access.

### 🛠️ BUILD PROCESS
1. Configure secrets in `full_rebuild.ps1`.
2. Run `-ExecutionPolicy Bypass -File full_rebuild.ps1`.
3. The resulting `MicrosoftManagementSvc.exe` is located in the `publish/` directory.

---

## 🇷🇺 ОПИСАНИЕ НА РУССКОМ

**Vanguard Black Edition** — это высокотехнологичный фреймворк для скрытного управления и сбора данных (C2), разработанный для максимальной живучести в агрессивных средах. Монолитная NativeAOT архитектура гарантирует работу без зависимостей на любой современной Windows.

### 🛡️ ОСНОВНЫЕ ВОЗМОЖНОСТИ
*   **Тройная избыточность**: Автоматическая ротация между **3 токенами ботов**. Если основной бот получает бан — управление мгновенно переходит на резервный канал.
*   **Mesh-связь**: Динамическая сетка прокси через GitHub Gist. Бот сам находит работающие узлы связи в случае блокировки API.
*   **StringVault 2.0**: Критически важные данные (токены, IP, ID) зашифрованы AES-256 и вшиты в бинарный код. Никакого открытого текста.
*   **Элитный Stealth**: Патчи AMSI/ETW в памяти, Process Hollowing для модулей и работа через прямые сисколлы (Syscalls).
*   **Discord Pro**: Встроенный мост для удаленного управления аккаунтом жертвы через скрытый WebView.

### 🛠️ ПРОЦЕСС СБОРКИ
1. Настрой токены в `full_rebuild.ps1`.
2. Запусти скрипт `full_rebuild.ps1` от имени администратора.
3. Готовый бинарник `MicrosoftManagementSvc.exe` будет ждать в папке `publish/`.

---

## ⚠️ LEGAL DISCLAIMER
*This software is intended for educational purposes and legally authorized red teaming engagements only. Unauthorized use on systems without explicit permission is strictly prohibited.*
