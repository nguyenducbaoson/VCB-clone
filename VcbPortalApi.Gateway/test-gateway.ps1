# Kiem tra VcbPortalApi.Gateway.
#
#   .\test-gateway.ps1
#   .\test-gateway.ps1 -Gateway http://localhost:5000 -Prefix /apimp
#
# Chay khi CA HAI dang song: VcbPortalApi va gateway.
# Dung curl.exe chu khong dung 'curl' — trong PowerShell 5.1 'curl' la bi danh
# cua Invoke-WebRequest, khong hieu -s/-o/-w.

param(
    [string]$Gateway = 'http://localhost:5000',
    [string]$Prefix  = '/apimp'
)

$ErrorActionPreference = 'Continue'
$pass = 0
$fail = 0

function Show($name, $expected, $actual) {
    $ok = $actual -eq $expected
    if ($ok) { $script:pass++ } else { $script:fail++ }
    $mark  = if ($ok) { 'OK  ' } else { 'SAI ' }
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host ("  {0} {1,-46} mong doi {2,-16} nhan {3}" -f $mark, $name, $expected, $actual) -ForegroundColor $color
}

# Ma HTTP cua mot request. -H de gia lap IP client vi RealIPHeader = X-Forwarded-For.
function Code($path, $ip, $method = 'GET') {
    curl.exe -s -o NUL -w '%{http_code}' --max-time 10 -X $method -H "X-Forwarded-For: $ip" "$Gateway$path"
}

function Body($path, $ip, $method = 'GET') {
    curl.exe -s --max-time 10 -X $method -H "X-Forwarded-For: $ip" "$Gateway$path"
}

$stamp = (Get-Date).Ticks % 200 + 20   # IP khac nhau moi lan chay, tranh dinh bo dem cu

Write-Host ''
Write-Host "Gateway: $Gateway   Prefix: $Prefix" -ForegroundColor Cyan
Write-Host ('=' * 78)

# ── 1. Gateway song, va no noi dung ve backend ────────────────────────────────
Write-Host ''
Write-Host '1. /health' -ForegroundColor Yellow
$health = Body '/health' '10.0.0.1'
Write-Host "  $health"
if ($health -match '"status":"ok"') {
    Show 'backend duoc bao la song' 'ok' 'ok'
} elseif ($health -match '"status":"degraded"') {
    Write-Host '  CHU Y: status = degraded -> VcbPortalApi chua chay hoac sai dia chi.' -ForegroundColor Red
    Write-Host '  Sua ReverseProxy:Clusters:vcbportalapi:Destinations:primary:Address' -ForegroundColor Red
    Write-Host '  trong appsettings.Dev.json cho dung cong ma VcbPortalApi dang nghe.' -ForegroundColor Red
} else {
    Write-Host '  Gateway khong tra loi. Kiem tra no da chay chua va dung cong chua.' -ForegroundColor Red
    exit 1
}

# ── 2. Chuyen tiep ────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '2. Chuyen tiep sang VcbPortalApi' -ForegroundColor Yellow
$c = Code "$Prefix/user/auth" "10.0.$stamp.1" 'POST'
Write-Host ("  {0}/user/auth -> {1}   (bat ky ma nao NGOAI 404/502 deu la da toi duoc API)" -f $Prefix, $c)
Show 'route khong khai bao bi tu choi' '404' (Code '/khong-he-co-route' "10.0.$stamp.2")

# ── 3. Gioi han tan suat theo tung endpoint ───────────────────────────────────
# Luat GET:*/basic/rat = 5 lan / 30m. Lan thu 6 tu cung mot IP phai bi chan.
Write-Host ''
Write-Host '3. Luat GET:*/basic/rat = 5 lan / 30 phut' -ForegroundColor Yellow
$ip = "10.0.$stamp.10"
for ($i = 1; $i -le 5; $i++) { $null = Code "$Prefix/basic/rat" $ip }
Show 'lan thu 6 bi chan' '429' (Code "$Prefix/basic/rat" $ip)

Write-Host '  Body tra ve khi bi chan:'
Write-Host ('    ' + (Body "$Prefix/basic/rat" $ip))

# ── 4. Moi endpoint mot bo dem rieng ──────────────────────────────────────────
# Cung IP vua bi chan o tren, nhung endpoint khac (luat 30 lan / 30m) van phai qua.
Write-Host ''
Write-Host '4. Cung IP do sang endpoint khac -> bo dem rieng' -ForegroundColor Yellow
$c = Code "$Prefix/ma/rat" $ip
$ok = if ($c -eq '429') { '429' } else { 'khong bi chan' }
Show 'GET:*/ma/rat (30 lan/30m) van di duoc' 'khong bi chan' $ok

# ── 5. Moi IP mot bo dem rieng ────────────────────────────────────────────────
Write-Host ''
Write-Host '5. IP khac vao lai endpoint da bi chan' -ForegroundColor Yellow
$ip2 = "10.0.$stamp.11"
$c = Code "$Prefix/basic/rat" $ip2
$ok = if ($c -eq '429') { '429' } else { 'khong bi chan' }
Show 'IP moi duoc cap han muc rieng' 'khong bi chan' $ok

# ── 6. Luat POST ──────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '6. Luat POST:*/user/pwd = 10 lan / 30 phut' -ForegroundColor Yellow
$ip3 = "10.0.$stamp.12"
for ($i = 1; $i -le 10; $i++) { $null = Code "$Prefix/user/pwd" $ip3 'POST' }
Show 'lan thu 11 bi chan' '429' (Code "$Prefix/user/pwd" $ip3 'POST')

# ── 7. IpWhitelist ────────────────────────────────────────────────────────────
# Lay IP dau tien trong IpWhitelist cua file moi truong dang chay.
Write-Host ''
Write-Host '7. IP trong IpWhitelist khong bi gioi han' -ForegroundColor Yellow
$env_ = $env:ASPNETCORE_ENVIRONMENT; if (-not $env_) { $env_ = 'Dev' }
$cfg = Join-Path $PSScriptRoot "..\VcbPortalApi\appsettings.$env_.json"
if (Test-Path $cfg) {
    $wl = (Get-Content $cfg -Raw | ConvertFrom-Json).IpRateLimiting.IpWhitelist
    if ($wl -and $wl.Count -gt 0) {
        $free = $wl[0]
        for ($i = 1; $i -le 7; $i++) { $null = Code "$Prefix/basic/rat" $free }
        $c = Code "$Prefix/basic/rat" $free
        $ok = if ($c -eq '429') { '429' } else { 'khong bi chan' }
        Show "IP $free qua 8 lan (han muc 5)" 'khong bi chan' $ok
    } else {
        Write-Host '  Bo qua: IpWhitelist rong.' -ForegroundColor DarkGray
    }
} else {
    Write-Host "  Bo qua: khong doc duoc $cfg" -ForegroundColor DarkGray
}

# ── 8. /health khong bi dem ───────────────────────────────────────────────────
# Luat "*" = 100 lan / 3m se nuot ca health check neu khong mien tru.
Write-Host ''
Write-Host '8. /health khong bi tinh vao han muc (110 lan)' -ForegroundColor Yellow
$ip4 = "10.0.$stamp.13"
$blocked = $false
for ($i = 1; $i -le 110; $i++) {
    if ((Code '/health' $ip4) -eq '429') { $blocked = $true; break }
}
$ok = if ($blocked) { "bi chan o lan $i" } else { 'khong bi chan' }
Show '/health duoc mien tru' 'khong bi chan' $ok

# ── Tong ket ──────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host ('=' * 78)
Write-Host ("  Dat: {0}    Hong: {1}" -f $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ''
if ($fail -gt 0) { exit 1 }
