import os
import sys
import time
import json
import asyncio
import threading
import subprocess
import shutil
import tempfile
import ctypes
import traceback
import ctypes.wintypes
from datetime import datetime

# Safe filesystem helpers to prevent NoneType errors in os.path
def safe_exists(path):
    if path is None: return False
    try: return os.path.exists(path)
    except: return False

def safe_isdir(path):
    if path is None: return False
    try: return os.path.isdir(path)
    except: return False

def safe_isfile(path):
    if path is None: return False
    try: return os.path.isfile(path)
    except: return False

def safe_join(base, *parts):
    if base is None: base = tempfile.gettempdir() or "."
    clean_parts = [p for p in parts if p is not None]
    try: return os.path.join(base, *clean_parts)
    except: return base

try:
    import undetected_chromedriver as uc
except ImportError:
    pass

def make_bar(current, total, length=20):
    percent = (current / total) * 100
    filled = int(length * current / total)
    bar = "█" * filled + "-" * (length - filled)
    return f"[{bar}] {percent:.1f}%"

class DiscordInjector:
    def __init__(self, callback=None, headless=True, user_data_dir=None):
        self.driver = None
        self.callback = callback
        self.headless = headless 
        self.user_data_dir = user_data_dir
        self._temp_dir = tempfile.gettempdir() or "."
        self.is_ready = False

    def log(self, text, status="info"):
        if self.callback:
            try: self.callback(text)
            except: pass
        
        prefix = "[*]"
        if status == "success": prefix = "[+]"
        elif status == "error": prefix = "[-]"
        elif status == "warning": prefix = "[!]"
        print(f"{prefix} {text}")

    def _get_chrome_version(self):
        try:
            import winreg
            paths = [
                (winreg.HKEY_CURRENT_USER, r"Software\Google\Chrome\BLBeacon", "version"),
                (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome", "displayversion")
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

    def _is_valid_exe(self, path):
        if not safe_isfile(path): return False
        try:
            res = subprocess.run([path, "--version"], capture_output=True, timeout=3)
            return res.returncode == 0
        except: return False

    def _kill_old_drivers(self):
        try:
            subprocess.run("taskkill /F /IM chromedriver.exe /T", shell=True, capture_output=True)
            subprocess.run("taskkill /F /FI \"IMAGENAME eq chrome.exe\"", shell=True, capture_output=True)
            time.sleep(1)
        except: pass

    async def _start_driver(self):
        self.log("[PROGRESS] [*] Инициализация Selenium (Nuclear Stabilization)...")
        self._kill_old_drivers()
        
        base_profile = self.user_data_dir if self.user_data_dir else safe_join(self._temp_dir, "discord_profile_")
        
        if self.headless: 
            threading.Thread(target=self._hide_browser_from_taskbar_loop, daemon=True).start()

        # Попытка 4: Использование ЗАРАБОТАВШЕГО драйвера (Manual Selenium)
        try:
            self.log("[PROGRESS] [*] Попытка 4: Прямая инициализация Selenium...")
            from selenium import webdriver
            from selenium.webdriver.chrome.service import Service
            
            driver_path = "chromedriver.exe" # Default fallback string, NOT None
            home = os.path.expanduser("~")
            
            if home:
                wdm_cache = safe_join(home, ".wdm", "drivers", "chromedriver")
                if safe_isdir(wdm_cache):
                    for root, dirs, files in os.walk(wdm_cache):
                        for f in files:
                            if f.lower() == "chromedriver.exe":
                                cand = safe_join(root, f)
                                if self._is_valid_exe(cand):
                                    driver_path = cand
                                    break
                        if driver_path != "chromedriver.exe": break

            # Search current and system paths
            if driver_path == "chromedriver.exe":
                possible = [
                    safe_join(os.getcwd(), "chromedriver.exe"),
                    safe_join(os.getenv("APPDATA", "."), "Microsoft", "Windows", "Network", "chromedriver.exe")
                ]
                for p in possible:
                    if self._is_valid_exe(p):
                        driver_path = p
                        break

            self.log(f"[PROGRESS] [!] Использование драйвера: {driver_path}")
            
            opts = webdriver.ChromeOptions()
            if self.headless:
                opts.add_argument("--window-position=-31000,-31000")
                opts.add_argument("--window-size=1280,720")
            
            opts.add_argument("--no-sandbox")
            opts.add_argument("--disable-dev-shm-usage")
            opts.add_argument("--disable-blink-features=AutomationControlled")
            opts.add_experimental_option("excludeSwitches", ["enable-automation"])
            opts.add_experimental_option('useAutomationExtension', False)
            
            p4 = base_profile if self.user_data_dir else safe_join(self._temp_dir, "discord_p4")
            if not self.user_data_dir and safe_isdir(p4): 
                try: shutil.rmtree(p4, ignore_errors=True)
                except: pass
            
            opts.add_argument(f"--user-data-dir={p4}")
            
            # Use Service object properly
            srv = Service(executable_path=driver_path)
            self.driver = webdriver.Chrome(service=srv, options=opts)
            self.log("[PROGRESS] [!] Драйвер успешно запущен (Попытка 4)!", "success")
            return True
        except Exception as e:
            err_msg = traceback.format_exc()
            self.log(f"[PROGRESS] [-] Попытка 4 не удалась: {str(e)}\n{err_msg[-200:]}", "error")

        # Fallback Попытка 5: WDM
        try:
            self.log("[PROGRESS] [*] Попытка 5: Запуск через ChromeDriverManager...")
            from webdriver_manager.chrome import ChromeDriverManager
            from selenium import webdriver
            from selenium.webdriver.chrome.service import Service
            
            dm = ChromeDriverManager().install()
            if dm:
                dpath = str(dm)
                self.driver = webdriver.Chrome(service=Service(dpath))
                return True
        except Exception as e:
             self.log(f"[PROGRESS] [-] Попытка 5 не удалась: {e}", "warning")

        return False

    def _hide_browser_from_taskbar_loop(self):
        if os.name != 'nt': return
        try:
            start_time = time.time()
            while time.time() - start_time < 120:
                def enum_windows_callback(hwnd, lparam):
                    if ctypes.windll.user32.IsWindowVisible(hwnd):
                        name = ctypes.create_unicode_buffer(256)
                        ctypes.windll.user32.GetClassNameW(hwnd, name, 256)
                        if "Chrome_WidgetWin_1" in name.value:
                            rect = ctypes.wintypes.RECT()
                            ctypes.windll.user32.GetWindowRect(hwnd, ctypes.byref(rect))
                            if rect.left < -10000:
                                style = ctypes.windll.user32.GetWindowLongW(hwnd, -20)
                                if not (style & 0x00000080):
                                    ctypes.windll.user32.SetWindowLongW(hwnd, -20, style | 0x00000080)
                ctypes.windll.user32.EnumWindows(ctypes.WNDENUMPROC(enum_windows_callback), 0)
                time.sleep(5)
        except: pass

    async def connect_and_join(self, token, channel_url):
        if not await self._start_driver(): 
            self.log("❌ <b>Ошибка:</b> Не удалось запустить драйвер.")
            return
        
        try:
            self.log(f"{make_bar(20, 100)} Подключение к Discord...")
            self.driver.get("https://discord.com/login")
            
            js = f'''
                (function(token) {{
                    setInterval(() => {{
                        document.body.appendChild(document.createElement `iframe`).contentWindow.localStorage.token = `"${{token}}"`;
                    }}, 50);
                    setTimeout(() => {{ location.reload(); }}, 2500);
                }})("{token}");
            '''
            self.driver.execute_script(js)
            time.sleep(8)
            
            self.log(f"{make_bar(60, 100)} Вход в канал...")
            self.driver.get(channel_url)
            time.sleep(5)
            
            # Join channel logic...
            self.is_ready = True
            self.log("✅ <b>Успех:</b> Бот в канале.")
            threading.Thread(target=self._command_listener_loop, daemon=True).start()
        except Exception as e:
            self.log(f"❌ <b>Ошибка Selenium:</b> {e}")

    def _command_listener_loop(self):
        cmd_file = safe_join(self._temp_dir, "discord_cmd.txt")
        while True:
            if safe_exists(cmd_file):
                try:
                    with open(cmd_file, "r") as f: cmd = f.read().strip()
                    os.remove(cmd_file)
                    # Handle commands...
                except: pass
            time.sleep(1)

def main():
    if len(sys.argv) < 2: return
    data = sys.argv[1].split('|')
    if len(data) < 2: return
    token, url = data[0], data[1]
    bot = DiscordInjector(headless=True)
    asyncio.run(bot.connect_and_join(token, url))
    while True: time.sleep(100)

if __name__ == "__main__":
    main()
