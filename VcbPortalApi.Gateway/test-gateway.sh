#!/usr/bin/env bash
# Kiem tra VcbPortalApi.Gateway. Chay bang Git Bash.
#
#   ./test-gateway.sh
#   ./test-gateway.sh http://localhost:5000 /apimp
#
# Chay khi CA HAI dang song: VcbPortalApi va gateway.

GW="${1:-http://localhost:5000}"
PREFIX="${2:-/apimp}"

pass=0
fail=0

show() { # $1 ten bai, $2 mong doi, $3 nhan duoc
    if [ "$2" = "$3" ]; then
        pass=$((pass + 1))
        printf '  OK   %-44s mong doi %-14s nhan %s\n' "$1" "$2" "$3"
    else
        fail=$((fail + 1))
        printf '  SAI  %-44s mong doi %-14s nhan %s\n' "$1" "$2" "$3"
    fi
}

# Ma HTTP. -H de gia lap IP client vi RealIPHeader = X-Forwarded-For.
code() { # $1 duong dan, $2 ip, $3 method
    curl -s -o /dev/null -w '%{http_code}' --max-time 10 \
         -X "${3:-GET}" -H "X-Forwarded-For: $2" "$GW$1"
}

body() {
    curl -s --max-time 10 -X "${3:-GET}" -H "X-Forwarded-For: $2" "$GW$1"
}

# IP khac nhau moi lan chay, tranh dinh bo dem con song tu lan truoc
n=$(( ($$ % 200) + 20 ))

echo
echo "Gateway: $GW   Prefix: $PREFIX"
printf '=%.0s' {1..78}; echo

# ── 1. Gateway song, va no noi dung ve backend ────────────────────────────────
echo
echo '1. /health'
health=$(body /health 10.0.0.1)
echo "  $health"
case "$health" in
    *'"status":"ok"'*)
        show 'backend duoc bao la song' 'ok' 'ok' ;;
    *'"status":"degraded"'*)
        echo '  CHU Y: degraded -> VcbPortalApi chua chay hoac sai dia chi.'
        echo '  Sua ReverseProxy:Clusters:vcbportalapi:Destinations:primary:Address'
        fail=$((fail + 1)) ;;
    *)
        echo '  Gateway khong tra loi. Kiem tra no da chay chua va dung cong chua.'
        exit 1 ;;
esac

# ── 2. Chuyen tiep ────────────────────────────────────────────────────────────
echo
echo '2. Chuyen tiep sang VcbPortalApi'
c=$(code "$PREFIX/user/auth" "10.$n.1.1" POST)
echo "  $PREFIX/user/auth -> $c   (bat ky ma nao NGOAI 404/502 deu la da toi duoc API)"
show 'route khong khai bao bi tu choi' '404' "$(code /khong-he-co-route "10.$n.1.2")"

# ── 3. Luat theo endpoint ─────────────────────────────────────────────────────
echo
echo '3. Luat GET:*/basic/rat = 5 lan / 30 phut'
ip="10.$n.2.1"
for i in 1 2 3 4 5; do code "$PREFIX/basic/rat" "$ip" >/dev/null; done
show 'lan thu 6 bi chan' '429' "$(code "$PREFIX/basic/rat" "$ip")"
echo '  Body tra ve khi bi chan:'
echo "    $(body "$PREFIX/basic/rat" "$ip")"

# ── 4. Moi endpoint mot bo dem rieng ──────────────────────────────────────────
echo
echo '4. Cung IP do sang endpoint khac -> bo dem rieng'
c=$(code "$PREFIX/ma/rat" "$ip")
[ "$c" = "429" ] && r=429 || r='khong bi chan'
show 'GET:*/ma/rat (30 lan/30m) van di duoc' 'khong bi chan' "$r"

# ── 5. Moi IP mot bo dem rieng ────────────────────────────────────────────────
echo
echo '5. IP khac vao lai endpoint da bi chan'
c=$(code "$PREFIX/basic/rat" "10.$n.2.2")
[ "$c" = "429" ] && r=429 || r='khong bi chan'
show 'IP moi duoc cap han muc rieng' 'khong bi chan' "$r"

# ── 6. Luat POST ──────────────────────────────────────────────────────────────
echo
echo '6. Luat POST:*/user/pwd = 10 lan / 30 phut'
ip3="10.$n.3.1"
for i in $(seq 1 10); do code "$PREFIX/user/pwd" "$ip3" POST >/dev/null; done
show 'lan thu 11 bi chan' '429' "$(code "$PREFIX/user/pwd" "$ip3" POST)"

# ── 7. IpWhitelist ────────────────────────────────────────────────────────────
echo
echo '7. IP trong IpWhitelist khong bi gioi han'
envname="${ASPNETCORE_ENVIRONMENT:-Dev}"
cfg="$(dirname "$0")/../VcbPortalApi/appsettings.$envname.json"
if [ -f "$cfg" ]; then
    free=$(grep -A20 '"IpWhitelist"' "$cfg" | grep -oE '"[^"]+"' | sed -n '2p' | tr -d '"')
    if [ -n "$free" ]; then
        for i in $(seq 1 7); do code "$PREFIX/basic/rat" "$free" >/dev/null; done
        c=$(code "$PREFIX/basic/rat" "$free")
        [ "$c" = "429" ] && r=429 || r='khong bi chan'
        show "IP $free qua 8 lan (han muc 5)" 'khong bi chan' "$r"
    else
        echo '  Bo qua: khong doc duoc IpWhitelist.'
    fi
else
    echo "  Bo qua: khong thay $cfg"
fi

# ── 8. /health khong bi dem ───────────────────────────────────────────────────
echo
echo '8. /health khong bi tinh vao han muc (110 lan)'
blocked=''
for i in $(seq 1 110); do
    if [ "$(code /health "10.$n.4.1")" = "429" ]; then blocked="bi chan o lan $i"; break; fi
done
show '/health duoc mien tru' 'khong bi chan' "${blocked:-khong bi chan}"

# ── Tong ket ──────────────────────────────────────────────────────────────────
echo
printf '=%.0s' {1..78}; echo
echo "  Dat: $pass    Hong: $fail"
echo
[ "$fail" -gt 0 ] && exit 1 || exit 0
