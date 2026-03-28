import os
import base64
import sys
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.backends import default_backend

def encrypt_file_gcm(file_path, key_b64):
    key = base64.b64decode(key_b64)
    with open(file_path, 'rb') as f:
        data = f.read()

    nonce = os.urandom(12)
    aesgcm = AESGCM(key)
    # cryptography's AesGcm returns [Ciphertext][Tag] where Tag is 16 bytes
    ciphertext_with_tag = aesgcm.encrypt(nonce, data, None)
    
    tag = ciphertext_with_tag[-16:]
    ciphertext = ciphertext_with_tag[:-16]

    # Vanguard GCM Format: [Nonce: 12][Tag: 16][Ciphertext: N]
    with open(file_path, 'wb') as f:
        f.write(nonce)
        f.write(tag)
        f.write(ciphertext)

def wrap_key(session_key_b64, master_key_b64):
    session_key = base64.b64decode(session_key_b64)
    master_key = base64.b64decode(master_key_b64)
    
    # Simple AES-CBC Key Wrap (Key is 32 bytes, block is 16, no padding needed)
    iv = b'\x00' * 16
    cipher = Cipher(algorithms.AES(master_key), modes.CBC(iv), backend=default_backend())
    encryptor = cipher.encryptor()
    wrapped = encryptor.update(session_key) + encryptor.finalize()
    return base64.b64encode(wrapped).decode()

if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(1)
        
    mode = sys.argv[1]
    if mode == "encrypt":
        encrypt_file_gcm(sys.argv[2], sys.argv[3])
    elif mode == "wrap":
        print(wrap_key(sys.argv[2], sys.argv[3]))
