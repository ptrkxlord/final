import os
import secrets
import string

def obfuscate_pe(file_path):
    """
    H-02: Custom PE header/section obfuscation.
    Renames common sections and modifies markers to evade signature scanners.
    """
    if not os.path.exists(file_path): return
    
    print(f"Applying stealth patches to {os.path.basename(file_path)}...")
    try:
        with open(file_path, "rb") as f:
            data = bytearray(f.read())
            
        # 1. Rename Sections (UPX, .text, .data etc)
        # We look for common section names and replace them with random junk
        targets = [b"UPX0", b"UPX1", b".text", b".rdata", b".data", b".reloc"]
        for target in targets:
            start = 0
            while True:
                idx = data.find(target, start)
                if idx == -1: break
                
                # Replace with random 4-5 char string
                new_name = "".join(secrets.choice(string.ascii_letters) for _ in range(len(target))).encode()
                data[idx:idx+len(target)] = new_name
                start = idx + 1
        
        # 2. Add Junk Overlay to change hash
        junk = secrets.token_bytes(secrets.randbelow(1024) + 512)
        data.extend(junk)
        
        with open(file_path, "wb") as f:
            f.write(data)
        print(f"✅ {os.path.basename(file_path)} patched.")
    except Exception as e:
        print(f"❌ Failed to patch {file_path}: {e}")

if __name__ == "__main__":
    bin_dir = "bin"
    if os.path.exists(bin_dir):
        for f in os.listdir(bin_dir):
            if f.endswith(".dll") or f.endswith(".exe"):
                obfuscate_pe(os.path.join(bin_dir, f))
