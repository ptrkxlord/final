from core.resolver import (Resolver, _URLLIB_PARSE)
urllib.parse = Resolver.get_mod(_URLLIB_PARSE)

from core.resolver import (Resolver, _OS, _JSON, _UUID)
os = Resolver.get_mod(_OS)
json = Resolver.get_mod(_JSON)
uuid = Resolver.get_mod(_UUID)

import mimetypes

class CloudModule:
    """Модуль для выгрузки больших файлов на внешние сервисы (GoFile)"""
    
    @staticmethod
    def get_best_server():
        """Получение лучшего сервера GoFile"""
        try:
            with urllib.request.urlopen("https://api.gofile.io/getBestServer") as response:
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
            status_callback("Получение сервера для загрузки...")
            
        server = CloudModule.get_best_server()
        url = "https://{server}.gofile.io/uploadFile"
        
        try:
            boundary = str(uuid.uuid4())
            content_type = "multipart/form-data; boundary={boundary}"
            filename = os.path.basename(file_path)
            
            # Подготовка заголовка и подвала multipart
            header = (
                "--{boundary}\\r\\n" +
                "Content-Disposition: form-data; name=\"file\"; filename=\"{filename}\"\\r\\n" +
                "Content-Type: {mimetypes.guess_type(file_path)[0] or \'application/octet-stream\'}\\r\\n\\r\\n"
            ).encode()
            footer = "\\r\\n--{boundary}--\\r\\n".encode()
            
            file_size = os.path.getsize(file_path)
            total_size = len(header) + file_size + len(footer)

            if status_callback:
                status_callback("Загрузка в облако ({file_size // 1024 // 1024} MB)...")

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
            print("[!] Cloud Upload Error: {e}")
        return None

if __name__ == "__main__":
    test_file = "test.txt"
    with open(test_file, "w") as f: f.write("Hello GoFile!")
    link = CloudModule.upload_file(test_file, lambda s: print("Status: {s}"))
    print("Link: {link}")
    if os.path.exists(test_file): os.remove(test_file)