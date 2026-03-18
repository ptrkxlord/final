import os
import sys
import base64
from Crypto.Cipher import AES

# Fixed keys from core/obfuscation.py
SALT = b'JO9eZJe98f43Wqdm'
AES_KEY = b'U8hFTWiuhDCg8LRUw1uLxyF0v0QIP1aB'

def encrypt_aes(plain_str: str) -> str:
    iv = os.urandom(16)
    cipher = AES.new(AES_KEY, AES.MODE_CBC, iv=iv)
    data = plain_str.encode('utf-8')
    padding_len = 16 - (len(data) % 16)
    data += bytes([padding_len] * padding_len)
    ct = cipher.encrypt(data)
    return base64.b64encode(iv + ct).decode('utf-8')

def get_cs_bytes_str(plain_str: str) -> str:
    data = plain_str.encode('utf-8')
    bytes_list = []
    for i in range(len(data)):
        bytes_list.append(f"0x{data[i] ^ SALT[i % len(SALT)]:02x}")
    bytes_str = ", ".join(bytes_list)
    return bytes_str

def main():
    print("=== MANUAL C# STRING OBFUSCATOR ===")
    print("AES Key (32): U8hFTWiuhDCg8LRUw1uLxyF0v0QIP1aB")
    print("XOR Salt (16): JO9eZJe98f43Wqdm")
    
    while True:
        try:
            s = input("\nEnter string to encrypt (or Ctrl+C to exit): ")
            if not s: continue
            
            aes_str = encrypt_aes(s)
            xor_str = get_cs_bytes_str(s)
            
            print("\n[DAes] Advanced AES Encryption:")
            print(f'Protector.DAes("{aes_str}")')
            
            print("\n[DStr] Basic XOR Encryption:")
            print(f'Protector.DStr(new byte[] {{ {xor_str} }})')
        except KeyboardInterrupt:
            print("\nExiting...")
            break

if __name__ == '__main__':
    main()
