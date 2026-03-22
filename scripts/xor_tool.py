import os

# 1. Diagnostic: Find strings in binary-like python file
def find_strings(file_path):
    with open(file_path, 'rb') as f:
        data = f.read()
    
    print(f"File size: {len(data)} bytes")
    
    # Simple search for common strings
    targets = [b'makedirs', b'site_dump', b'commun', b'steamcommunity']
    for t in targets:
        idx = data.find(t)
        if idx != -1:
            print(f"Found target '{t.decode()}' at {idx}")
            # Show context (100 bytes around)
            start = max(0, idx - 50)
            end = min(len(data), idx + 50)
            print(f"Context: {data[start:end]}")

# 2. XOR Tool
def xor_file(in_path, out_path, key=0xAA):
    with open(in_path, 'rb') as f:
        data = f.read()
    
    xored = bytearray(b ^ key for b in data)
    
    with open(out_path, 'wb') as f:
        f.write(xored)
    print(f"Successfully XORed {in_path} -> {out_path}")

if __name__ == "__main__":
    find_strings('c:/Users/zxc23/OneDrive/Desktop/final/okno/steam_notice.py')
    xor_file('c:/Users/zxc23/OneDrive/Desktop/final/okno/dist/SteamAlert.exe', 'c:/Users/zxc23/OneDrive/Desktop/final/SteamAlert.bin')
