import os
from PIL import Image

class SteganoModule:
    """
    H-09: Steganography module for hiding/extracting data in images.
    Uses LSB (Least Significant Bit) method.
    """
    @staticmethod
    def encode(carrier, data, output):
        """Hides data in the carrier image. Supports paths or file-like objects."""
        try:
            img = Image.open(carrier)
            # Ensure it's in a format with 3 or 4 channels
            if img.mode != 'RGB' and img.mode != 'RGBA':
                img = img.convert('RGB')
                
            width, height = img.size
            pixels = img.load()
            
            # Add a header for data length (4 bytes)
            data_bytes = data if isinstance(data, (bytes, bytearray)) else (data.read() if hasattr(data, 'read') else data.encode('utf-8'))
            msg_len = len(data_bytes)
            full_data = msg_len.to_bytes(4, 'big') + data_bytes
            
            binary_data = "".join(f"{b:08b}" for b in full_data)
            data_idx = 0
            
            for y in range(height):
                for x in range(width):
                    if data_idx < len(binary_data):
                        pixel = list(pixels[x, y])
                        # Modify the LSB of the first channel (Red)
                        pixel[0] = (pixel[0] & ~1) | int(binary_data[data_idx])
                        pixels[x, y] = tuple(pixel)
                        data_idx += 1
                    else:
                        break
                if data_idx >= len(binary_data):
                    break
            
            img.save(output, "PNG")
            return True
        except Exception:
            return False

    @staticmethod
    def decode(image_path):
        """Extracts data from the carrier image."""
        if not os.path.exists(image_path): return None
        
        try:
            img = Image.open(image_path)
            width, height = img.size
            pixels = img.load()
            
            binary_data = ""
            for y in range(height):
                for x in range(width):
                    pixel = pixels[x, y]
                    binary_data += str(pixel[0] & 1)
                    if len(binary_data) >= 32 and len(binary_data) % 8 == 0:
                        # Once we have enough bits, check if we can read the length
                        pass # Optimization could be added here
            
            # Convert binary to bytes
            all_bytes = bytearray()
            for i in range(0, len(binary_data), 8):
                byte_str = binary_data[i:i+8]
                if len(byte_str) < 8: break
                all_bytes.append(int(byte_str, 2))
            
            if len(all_bytes) < 4: return None
            msg_len = int.from_bytes(all_bytes[:4], 'big')
            if msg_len > len(all_bytes) - 4: return None # Sanitization
            
            return bytes(all_bytes[4:4+msg_len])
        except Exception:
            return None
