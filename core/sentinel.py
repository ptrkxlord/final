from core.resolver import (
    Resolver, _OS, _TIME, _HASHLIB, _THREADING, _JSON, _TYPING
)
os = Resolver.get_mod(_OS)
time = Resolver.get_mod(_TIME)
hashlib = Resolver.get_mod(_HASHLIB)
threading = Resolver.get_mod(_THREADING)
json = Resolver.get_mod(_JSON)
typing_mod = Resolver.get_mod(_TYPING)
Dict, List, Optional = typing_mod.Dict, typing_mod.List, typing_mod.Optional
from core.config import ConfigManager
from core.error_logger import log_error, log_info
from core.persistence import PersistManager

class SentinelGuard:
    """S-07: Anti-Tamper & Persistence Recovery Sentinel"""
    
    def __init__(self, bot, report_manager):
        self.bot = bot
        self.report_manager = report_manager
        self.core_hashes: Dict[str, str] = {}
        self.running = False
        self.lock = threading.Lock()
        self.persist_manager = PersistManager()
        self.hash_file = os.path.join(os.path.dirname(__file__), ".hashes.json")

    def _get_hash(self, path: str) -> str:
        """Improved hashing with error handling and larger buffer"""
        try:
            if not os.path.exists(path):
                log_error(f"File not found: {path}", "Sentinel")
                return ""
                
            sha256 = hashlib.sha256()
            with open(path, "rb") as f:
                for chunk in iter(lambda: f.read(65536), b""):
                    sha256.update(chunk)
            return sha256.hexdigest()
        except Exception as e:
            log_error(f"Hash error {path}: {e}", "Sentinel")
            return ""

    def snapshot_core(self):
        """Initializes hashes of core modules for monitoring"""
        core_dir = os.path.dirname(os.path.abspath(__file__))
        
        # Python modules
        for f in os.listdir(core_dir):
            if f.endswith(".py") and not f.startswith("__"):
                path = os.path.join(core_dir, f)
                self.core_hashes[f] = self._get_hash(path)
        
        # main.py
        main_path = os.path.join(os.path.dirname(core_dir), "main.py")
        if os.path.exists(main_path):
            self.core_hashes["main.py"] = self._get_hash(main_path)
        
        # Monitor native modules (.cs, .dll)
        native_dir = os.path.join(os.path.dirname(core_dir), "native_modules")
        if os.path.exists(native_dir):
            for f in os.listdir(native_dir):
                if f.endswith((".cs", ".dll")):
                    path = os.path.join(native_dir, f)
                    self.core_hashes[f"native/{f}"] = self._get_hash(path)
        
        # Monitor persistence DLL
        persist_dll = os.path.join(os.path.dirname(core_dir), "defense", "persist.dll")
        if os.path.exists(persist_dll):
            self.core_hashes["defense/persist.dll"] = self._get_hash(persist_dll)

        self._save_hashes()

    def _save_hashes(self):
        """Save hashes to persistent storage"""
        try:
            with open(self.hash_file, 'w') as f:
                json.dump(self.core_hashes, f)
        except Exception as e:
            log_error(f"Failed to save hashes: {e}", "Sentinel")

    def _load_hashes(self):
        """Load hashes from persistent storage"""
        try:
            if os.path.exists(self.hash_file):
                with open(self.hash_file, 'r') as f:
                    self.core_hashes = json.load(f)
        except Exception as e:
            log_error(f"Failed to load hashes: {e}", "Sentinel")

    def check_integrity(self) -> bool:
        """Verifies current hashes against snapshots"""
        core_dir = os.path.dirname(os.path.abspath(__file__))
        parent_dir = os.path.dirname(core_dir)
        
        for file_key, saved_hash in self.core_hashes.items():
            if file_key.startswith("native/"):
                path = os.path.join(parent_dir, "native_modules", file_key[7:])
            elif file_key == "main.py":
                path = os.path.join(parent_dir, "main.py")
            elif file_key.startswith("defense/"):
                path = os.path.join(parent_dir, file_key)
            else:
                path = os.path.join(core_dir, file_key)
            
            if not os.path.exists(path) or self._get_hash(path) != saved_hash:
                log_error(f"Integrity breach detected: {file_key}", "Sentinel")
                return False
        return True

    def recover_persistence(self):
        """Automatic recovery on violation"""
        try:
            log_info("Attempting persistence recovery...", "Sentinel")
            self.persist_manager.install_all()
            log_info("Persistence recovery complete", "Sentinel")
        except Exception as e:
            log_error(f"Recovery failed: {e}", "Sentinel")

    def run_daemon(self):
        """Background thread monitoring persistence and integrity"""
        self._load_hashes()
        if not self.core_hashes:
            self.snapshot_core()
            
        self.running = True
        log_info("Sentinel daemon started", "Sentinel")
        
        while self.running:
            try:
                # 1. Integrity Check
                if not self.check_integrity():
                    # self.report_manager.send_text("🚨 Sentinel: Core integrity breach detected!")
                    log_error("CRITICAL: Integrity breach!", "Sentinel")
                    self.recover_persistence()
                    # Optionally re-snapshot or alert more loudly
                
                # 2. Persistence Check
                # We check for a common name used in installation (e.g. "RuntimeBroker")
                if not self.persist_manager.check_all():
                    log_info("Persistence missing, reinstalling...", "Sentinel")
                    self.recover_persistence()
                
                time.sleep(300) # Check every 5 minutes
                
            except Exception as e:
                log_error(f"Sentinel error: {e}", "Sentinel")
                time.sleep(60)

    def stop(self):
        """Graceful shutdown"""
        self.running = False
        log_info("Sentinel stopped", "Sentinel")
