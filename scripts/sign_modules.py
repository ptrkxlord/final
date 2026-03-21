import os
import clr
import sys
import time

# Переходим в корень проекта, если мы не там
project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(project_root)

# Добавляем путь к скомпилированным DLL
bin_dir = os.path.join(project_root, 'bin')
sys.path.append(bin_dir)

import shutil

print(f"🧪 Подготовка к загрузке pe_utils.dll из {bin_dir}...")
try:
    # Создаем временную копию, чтобы не блокировать основной файл при подписании
    dll_path = os.path.join(bin_dir, 'pe_utils.dll')
    temp_dll_path = os.path.join(bin_dir, 'pe_utils_temp.dll')
    
    if not os.path.exists(dll_path):
        print(f"❌ Файл не найден: {dll_path}")
        sys.exit(1)
        
    shutil.copy2(dll_path, temp_dll_path)
    clr.AddReference(os.path.abspath(temp_dll_path))
    from VanguardCore import PEManager
    print("✅ PEManager загружен (через временную копию).")
except Exception as e:
    print(f"❌ Ошибка загрузки PEManager: {e}")
    sys.exit(1)

def sign_files():
    files = [f for f in os.listdir(bin_dir) if f.endswith('.dll') or f.endswith('.exe')]
    
    print(f"🕯️  Клонирование подписей для {len(files)} файлов в {bin_dir}...")
    
    success_count = 0
    for file in files:
        path = os.path.abspath(os.path.join(bin_dir, file))
        # Мы используем встроенный в PEManager метод CloneFromSystem
        # который перебирает notepad.exe, calc.exe и другие системные файлы
        if PEManager.CloneFromSystem(path):
            print(f"  [+] {file} -> Подписано подписью MS")
            success_count += 1
        else:
            print(f"  [-] {file} -> Ошибка подписания")
            
    print(f"\n✨ Результат: {success_count}/{len(files)} файлов подписано.")

if __name__ == "__main__":
    sign_files()
