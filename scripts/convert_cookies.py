
"""Convert cookies from Windows Filetime format to Unix timestamp format.

Input cookie example:
{
  "host": ".example.com",
  "name": "sid",
  "path": "/",
  "is_secure": true,
  "is_httponly": false,
  "expires": 13441397713475530,
  "value": "abc"
}

Output cookie example:
{
  "domain": ".example.com",
  "name": "sid",
  "path": "/",
  "secure": true,
  "httpOnly": false,
  "expirationDate": 1796924113,
  "value": "abc"
}
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Dict, List

WINDOWS_EPOCH_OFFSET_US = 11644473600000000

def filetime_us_to_unix_seconds(filetime_us: int) -> int:
    """Convert Windows Filetime in microseconds to Unix timestamp in seconds."""
    return int((int(filetime_us) - WINDOWS_EPOCH_OFFSET_US) // 1_000_000)

def convert_cookie(cookie: Dict[str, Any]) -> Dict[str, Any]:
    return {
        "domain": cookie.get("host", ""),
        "name": cookie.get("name", ""),
        "path": cookie.get("path", "/"),
        "secure": bool(cookie.get("is_secure", False)),
        "httpOnly": bool(cookie.get("is_httponly", False)),
        "expirationDate": filetime_us_to_unix_seconds(cookie.get("expires", 0)),
        "value": cookie.get("value", ""),
    }

def convert_cookies(data: Any) -> List[Dict[str, Any]]:
    if not isinstance(data, list):
        raise ValueError("Input JSON must be a list of cookie objects")
    return [convert_cookie(item) for item in data]

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Convert cookies from Windows Filetime (microseconds) to Unix timestamp (seconds)."
    )
    parser.add_argument("input", type=Path, help="Path to source JSON file")
    parser.add_argument("output", type=Path, help="Path to destination JSON file")
    parser.add_argument(
        "--indent",
        type=int,
        default=2,
        help="Indent for output JSON (default: 2)",
    )
    args = parser.parse_args()

    with args.input.open("r", encoding="utf-8") as f:
        source_data = json.load(f)

    converted = convert_cookies(source_data)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8") as f:
        json.dump(converted, f, ensure_ascii=False, indent=args.indent)
        f.write("\n")

    print(f"Converted {len(converted)} cookies: {args.input} -> {args.output}")

if __name__ == "__main__":
    main()
