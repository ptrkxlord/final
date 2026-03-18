from abc import ABC, abstractmethod
import os
import threading
from typing import Dict, Any, Optional

class BaseModule(ABC):
    """
    A-04: Standardized interface for all modules in PTRKXLORD.
    Ensures consistent execution, reporting, and lifecycle management.
    """
    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        self.bot = bot
        self.report_manager = report_manager
        self.temp_dir = temp_dir if temp_dir else os.environ.get("TEMP", ".")
        self._lock = threading.Lock()
        self.is_running = False

    @abstractmethod
    def run(self) -> bool:
        """
        Main execution entry point. Should be thread-safe.
        Returns True if successful.
        """
        pass

    def steal(self) -> Dict[str, Any]:
        """
        Optional: Return structured data (passwords, cookies, etc.)
        """
        return {}

    def get_stats(self) -> Dict[str, int]:
        """
        Return execution statistics (items collected, etc.)
        """
        return {"items": 0}

    def log(self, message: str):
        """
        Standardized logging for modules.
        """
        from core.obfuscation import decrypt_string
        # Can be expanded to send debug messages to C2 or file
        print(f"[{self.__class__.__name__}] {message}")
