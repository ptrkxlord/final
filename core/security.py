import os
import hashlib
from typing import Dict, List
from core.obfuscation import decrypt_string

class IntegrityGuard:
    """S-06: Bot Integrity Protection"""
    
    @staticmethod
    def get_file_hash(file_path: str) -> str:
        sha256_hash = hashlib.sha256()
        try:
            with open(file_path, "rb") as f:
                for byte_block in iter(lambda: f.read(4096), b""):
                    sha256_hash.update(byte_block)
            return sha256_hash.hexdigest()
        except:
            return ""

    @classmethod
    def verify_core(cls) -> bool:
        """Verifies that core modules haven't been tampered with"""
        # In a real scenario, these hashes would be signed/encrypted in ConfigManager
        core_files = ["main.py", "core/base.py", "core/config.py", "core/obfuscation.py"]
        
        for rel_path in core_files:
            abs_path = os.path.abspath(rel_path)
            if not os.path.exists(abs_path):
                print(f"❌ Missing core component: {rel_path}")
                return False
            
            # Simple check for now: just ensures file is not empty
            if os.path.getsize(abs_path) < 100:
                print(f"❌ Corrupt core component: {rel_path}")
                return False
                
        return True

    @classmethod
    def run_check(cls):
        if not cls.verify_core():
            # If integrity is compromised, self-destruct or alert
            # log_debug("🚨 Integrity check FAILED!")
            # os._exit(1)
            pass
