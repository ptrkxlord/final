import os

def xor_file(input_path, output_path, key=0xAA):
    if not os.path.exists(input_path):
        print(f"Error: {input_path} not found")
        return
    with open(input_path, "rb") as f:
        data = f.read()
    
    xor_data = bytearray(b ^ key for b in data)
    
    with open(output_path, "wb") as f:
        f.write(xor_data)
    print(f"Success: {input_path} -> {output_path} (XOR 0x{key:02X})")

if __name__ == "__main__":
    # SteamLogin
    xor_file("SteamLogin.exe", "SteamLogin.bin")
    
    # SteamAlert
    xor_file("okno/dist/SteamAlert.exe", "SteamAlert.bin")
