import re

path = 'c:/Users/zxc23/OneDrive/Desktop/final/okno/steam_notice.py'
with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Pattern for the hardcoded temp path
# file:///C:/Users/zxc23/AppData/Local/Temp/_MEI50362/site_dump/
pattern = r'file:///C:/Users/[^/]+/AppData/Local/Temp/_MEI\d+/site_dump/'

# We replace it with nothing or just 'site_dump/' 
# In webview, relative paths often work if the base_uri is set or if they are in the same folder.
# But here it looks like it's inside a base64 string.
# Base64 of 'file:///C:/Users/...' will be different.
# WAIT! The string in line 20 IS base64. 
# I need to decode the base64, fix it, and re-encode.

import base64

def fix_b64(match):
    b64_str = match.group(0)
    try:
        decoded = base64.b64decode(b64_str).decode('utf-8', errors='ignore')
        # Fix the decoded string
        fixed = re.sub(pattern, 'site_dump/', decoded)
        # Re-encode
        return base64.b64encode(fixed.encode('utf-8')).decode('utf-8')
    except:
        return b64_str

# Line 19 is EMBEDDED_HTML_INDEX
# Line 20 is EMBEDDED_HTML_RED
# I'll just find all long base64 strings and try to fix them.

new_content = re.sub(r'[A-Za-z0-9+/=]{100,}', fix_b64, content)

with open(path, 'w', encoding='utf-8') as f:
    f.write(new_content)

print("Cleaned steam_notice.py from hardcoded paths.")
