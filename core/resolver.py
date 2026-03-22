import base64
import sys
import hashlib

class Resolver:
    """
    [A-08] Dynamic API Resolution System.
    Hides imports and attribute access behind encoded strings.
    """
    _cache = {}

    @staticmethod
    def _d(s: str) -> str:
        return base64.b64decode(s).decode('utf-8')

    @staticmethod
    def _h(name: str) -> str:
        """DJB2-like hash for Python strings (simplified)"""
        return hashlib.md5(name.encode()).hexdigest()[:8]

    @classmethod
    def get_mod(cls, enc_name: str):
        """Dynamic import with caching"""
        if enc_name in cls._cache:
            return cls._cache[enc_name]
        
        real_name = cls._d(enc_name)
        mod = __import__(real_name)
        # Handle submodules like 'os.path'
        if '.' in real_name:
            for part in real_name.split('.')[1:]:
                mod = getattr(mod, part)
        
        cls._cache[enc_name] = mod
        return mod

    @classmethod
    def get_attr(cls, mod, enc_attr: str):
        """Dynamic attribute access"""
        return getattr(mod, cls._d(enc_attr))

    @classmethod
    def get_mod_by_hash(cls, name_hash: str, real_name: str):
        """
        Resolution by hash + provided real name (obfuscation only).
        Python doesn't support reverse hash lookup easily, so we still 
        need the real name but we use the hash as a cache key/identifier.
        """
        if name_hash in cls._cache:
            return cls._cache[name_hash]
        
        mod = __import__(real_name)
        if '.' in real_name:
            for part in real_name.split('.')[1:]:
                mod = getattr(mod, part)
        
        cls._cache[name_hash] = mod
        return mod

    @classmethod
    def decrypt(cls, data: str) -> str:
        """Legacy decryption wrapper for dynamic data"""
        if not data: return ""
        try:
            # Simple base64 decode for now, as we moved more complex logic to native
            # If XOR is needed, it can be added here
            return cls._d(data)
        except:
            return data

    @classmethod
    def load_native(cls):
        """Standardized loader for C# native components"""
        try:
            import clr
            import os
            base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
            dll_path = os.path.join(base_dir, "bin", "SafetyManager.dll")
            if os.path.exists(dll_path):
                clr.AddReference(dll_path)
                return True
            
            # Fallback search
            alt_path = os.path.join(base_dir, "defense", "SafetyManager.dll")
            if os.path.exists(alt_path):
                clr.AddReference(alt_path)
                return True
        except Exception:
            pass
        return False

# Common modules (base64 encoded)
_OS = 'b3M='
_OS_PATH = 'b3MucGF0aA=='
_SUBPROCESS = 'c3VicHJvY2Vzcw=='
_CTYPES = 'Y3R5cGVz'
_CTYPES_WINTYPES = 'Y3R5cGVzLndpbnR5cGVz'
_SHUTIL = 'c2h1dGls'
_PSUTIL = 'cHN1dGls'
_SQLITE3 = 'c3FsaXRlMw=='
_BASE64 = 'YmFzZTY0'
_RE = 'cmU='
_JSON = 'anNvbg=='
_TEMPFILE = 'dGVtcGZpbGU='
_RANDOM = 'cmFuZG9t'
_TIME = 'dGltZQ=='
_PATHLIB = 'cGF0aGxpYg=='
_DATETIME = 'ZGF0ZXRpbWU='
_URLLIB_REQUEST = 'dXJsbGliLnJlcXVlc3Q='
_CONCURRENT_FUTURES = 'Y29uY3VycmVudC5mdXR1cmVz'
_SYS = 'c3lz'
_IO = 'aW8='
_BUILTINS = 'YnVpbHRpbnM='
_THREADING = 'dGhyZWFkaW5n'
_QUEUE = 'cXVldWU='
_WINREG = 'd2lucmVn'
_SOCKET = 'c29ja2V0'
_PLATFORM = 'cGxhdGZvcm0='
_HASHLIB = 'aGFzaGxpYg=='
_URLLIB_PARSE = 'dXJsbGliLnBhcnNl'
_LOGGING = 'bG9nZ2luZw=='
_WARNINGS = 'd2FybmluZ3M='
_UUID = 'dXVpZA=='
_COLLECTIONS = 'Y29sbGVjdGlvbnM='
_REQUESTS = 'cmVxdWVzdHM='
_TYPING = 'dHlwaW5n'
_TELEBOT = 'dGVsZWJvdA=='
_ZIPFILE = 'emlwZmlsZQ=='
_URLLIB = 'dXJsbGli'
_TRACEBACK = 'dHJhY2ViYWNr'
