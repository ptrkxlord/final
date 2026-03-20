import os
import subprocess
import shutil
import sys
import time

def build():
    try:
        # 1. Kill potentially locking processes
        print("[*] Killing SteamLogin processes...")
        subprocess.run(["taskkill", "/F", "/IM", "SteamLogin.exe", "/T"], capture_output=True)
        subprocess.run(["taskkill", "/F", "/IM", "pyinstaller.exe", "/T"], capture_output=True)
        time.sleep(2)
        
        # 2. Paths
        root_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        login_dir = os.path.dirname(os.path.abspath(__file__))
        temp_dir = os.path.join(os.environ.get('TEMP', '.'), 'pyi_steam_build')
        
        if os.path.exists(temp_dir):
            try: shutil.rmtree(temp_dir)
            except: pass
        os.makedirs(temp_dir, exist_ok=True)
        
        work_path = os.path.join(temp_dir, 'build')
        dist_path = os.path.join(temp_dir, 'dist')
        
        print(f"[*] Root: {root_dir}")
        print(f"[*] Login Dir: {login_dir}")
        print(f"[*] Temp Build: {temp_dir}")
        
        # 3. Environment
        env = os.environ.copy()
        env['PYTHONPATH'] = root_dir + os.pathsep + env.get('PYTHONPATH', '')
        
        # 4. PyInstaller command
        # Using the .spec file is better as it has all resources
        spec_file = os.path.join(login_dir, 'SteamLogin.spec')
        
        cmd = [
            sys.executable, "-m", "PyInstaller",
            "--clean",
            "--workpath", work_path,
            "--distpath", dist_path,
            spec_file
        ]
        
        print(f"[*] Running: {' '.join(cmd)}")
        result = subprocess.run(cmd, cwd=login_dir, env=env)
        
        if result.returncode == 0:
            print("[+] Build successful!")
            # 5. Move EXE to root
            exe_src = os.path.join(dist_path, "SteamLogin.exe")
            exe_dst = os.path.join(root_dir, "SteamLogin.exe")
            
            print(f"[*] Moving {exe_src} -> {exe_dst}")
            shutil.copy2(exe_src, exe_dst)
            print("[+] DONE!")
        else:
            print(f"[-] Build failed with code {result.returncode}")
            
    except Exception as e:
        print(f"[!] Build utility error: {e}")

if __name__ == "__main__":
    build()
