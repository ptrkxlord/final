import base64
import os
import ctypes

def _get_dynamic_salt() -> bytes:
    """
    Placeholder for build-time salt.
    """
    # SALT_PLACEHOLDER
    return b'n2xkNQYbZwj8r9fz'

_SALT = _get_dynamic_salt()

def _get_salt() -> bytes:
    return _SALT
def decrypt_string(encoded_str: str) -> str:
    """
    Decries strings using the native PolymorphicEngine if available.
    Falls back to legacy XOR if DLL not loaded.
    """
    if not encoded_str: return ""
    
    try:
        # Try native AES first
        from StealthModule import Protector
        dec = Protector.DAes(encoded_str)
        if dec: return dec
    except:
        pass

    try:
        # Fallback to legacy XOR
        data = base64.b64decode(encoded_str)
        xor_data = bytearray()
        salt = _get_salt()
        for i in range(len(data)):
            xor_data.append(data[i] ^ salt[i % len(salt)])
        return xor_data.decode('utf-8')
    except Exception:
        return encoded_str

def encrypt_string(plain_str: str) -> str:
    """
    Helper for developers. Legacy XOR remains for now.
    """
    data = plain_str.encode('utf-8')
    xor_data = bytearray()
    salt = _get_salt()
    for i in range(len(data)):
        xor_data.append(data[i] ^ salt[i % len(salt)])
    return base64.b64encode(xor_data).decode('utf-8')
if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1:
        target = " ".join(sys.argv[1:])
        print(f"Encrypted: {encrypt_string(target)}")
    else:
        print("Usage: python obfuscation.py <string_to_encrypt>")
        test = "Hello World"
        enc = encrypt_string(test)
        dec = decrypt_string(enc)
        print(f"Test: {test} -> {enc} -> {dec}")