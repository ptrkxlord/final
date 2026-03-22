from core.resolver import (Resolver, _URLLIB_REQUEST)
urllib_request = Resolver.get_mod(_URLLIB_REQUEST)

from core.resolver import (Resolver, _TIME, _THREADING)
time = Resolver.get_mod(_TIME)
threading = Resolver.get_mod(_THREADING)

import random
from core.error_logger import log_error, log_info

class DecoyManager:
    """H-08: Traffic Shaping & Decoy Traffic"""
    
    DECOY_SITES = [
        "https://www.google.com/search?q=weather+today",
        "https://www.wikipedia.org/",
        "https://github.com/trending",
        "https://www.microsoft.com/en-us/windows",
        "https://www.apple.com/",
        "https://www.amazon.com/best-sellers",
        "https://news.ycombinator.com/",
        "https://stackoverflow.com/questions?sort=votes",
        "https://www.reddit.com/r/technology/top/",
        "https://www.nytimes.com/"
    ]
    
    USER_AGENTS = [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    ]

    def __init__(self):
        self.running = False
        self.lock = threading.Lock()

    def _send_decoy_request(self):
        """Sends a single decoy request to a random site"""
        try:
            url = random.choice(self.DECOY_SITES)
            ua = random.choice(self.USER_AGENTS)
            
            req = urllib_request.Request(url, headers={'User-Agent': ua})
            with urllib_request.urlopen(req, timeout=10) as response:
                _ = response.read(1024) # Read a small chunk to simulate interaction
            
            # log_info(f"Decoy request sent to {url}", "Decoy")
            return True
        except Exception as e:
            # log_error(f"Decoy request failed: {e}", "Decoy")
            return False

    def start_background_decoy(self, interval_min=30, interval_max=300):
        """Starts the decoy loop in a background thread"""
        def run():
            while self.running:
                try:
                    self._send_decoy_request()
                    # Sleep for random time between requests to avoid fixed patterns
                    sleep_time = random.randint(interval_min, interval_max)
                    time.sleep(sleep_time)
                except Exception:
                    time.sleep(60)
        
        with self.lock:
            if not self.running:
                self.running = True
                threading.Thread(target=run, daemon=True).start()
                log_info("Traffic shaping decoy daemon started", "Decoy")

    def stop(self):
        with self.lock:
            self.running = False
            log_info("Decoy traffic stopped", "Decoy")