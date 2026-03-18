import os
import random
import shutil
from core.error_logger import log_error, log_info

class SecureWiper:
    """S-10: Anti-Forensics - Securely wipes files and directories"""

    @staticmethod
    def wipe_file(file_path: str, passes: int = 1):
        """Overwrites file content with random bytes before deleting it."""
        if not os.path.exists(file_path):
            return
        
        try:
            file_size = os.path.getsize(file_path)
            if file_size == 0:
                os.remove(file_path)
                return

            with open(file_path, "ba+", buffering=0) as f:
                for _ in range(passes):
                    f.seek(0)
                    # Use os.urandom for better randomness, or random.getrandbits for speed
                    # For Anti-Forensics, we want a balance.
                    f.write(os.urandom(file_size))
                    f.flush()
                    os.fsync(f.fileno())
            
            os.remove(file_path)
            # log_info(f"Securely wiped and removed {os.path.basename(file_path)}", "Wiper")
        except Exception as e:
            # log_error(f"Failed to wipe file {file_path}: {e}", "Wiper")
            try: os.remove(file_path) # Fallback to normal delete
            except: pass

    @staticmethod
    def wipe_directory(dir_path: str, recursive: bool = True):
        """Recursively wipes all files in a directory before removing it."""
        if not os.path.exists(dir_path):
            return

        try:
            for root, dirs, files in os.walk(dir_path, topdown=False):
                for name in files:
                    SecureWiper.wipe_file(os.path.join(root, name))
                for name in dirs:
                    try: os.rmdir(os.path.join(root, name))
                    except: pass
            
            try: os.rmdir(dir_path)
            except: pass
            log_info(f"Securely wiped directory {os.path.basename(dir_path)}", "Wiper")
        except Exception as e:
            log_error(f"Failed to wipe directory {dir_path}: {e}", "Wiper")
            shutil.rmtree(dir_path, ignore_errors=True) # Fallback
