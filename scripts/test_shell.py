import os
import clr
import sys
from core.resolver import Resolver

# Настройка путей
project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(project_root)
bin_dir = os.path.join(project_root, 'bin')
sys.path.append(bin_dir)

print("🧪 Тестирование ShellManager (кодировка)...")

try:
    clr.AddReference('shell')
    Resolver.load_native()
    from VanguardCore import ShellManager
    print("✅ ShellManager загружен.")
except Exception as e:
    print(f"❌ Ошибка загрузки ShellManager: {e}")
    sys.exit(1)

def test_command(cmd):
    print(f"\n> {cmd}")
    try:
        # Выполняем команду
        output = ShellManager.ExecuteCommand(cmd)
        print("--- ВЫВОД ---")
        print(output)
        print("-------------")
    except Exception as e:
        print(f"❌ Ошибка выполнения: {e}")

if __name__ == "__main__":
    # Тестируем команду, которая обычно выдает кириллицу
    test_command("chcp")
    test_command("ipconfig")
    test_command("dir")