#!/bin/bash
# End-to-end test of the online game server, without phones or Unity:
# downloads the published Linux server (release "son-server"), starts it locally and plays a match
# against it with two fake phones (FakePhone.cs: lobby, start, bots, crates, doors, shots, hits,
# kills, deaths, match end). Needs: gh (logged in), mono (mcs), Linux x86_64.
#   Tools~/online-test/run.sh [solo|squad]
set -euo pipefail
MODE=${1:-solo}
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
WORK=${TMPDIR:-/tmp}/zm-online-test
mkdir -p "$WORK"
REPO=olcaydunder/zootopiamobile
aid=$(gh api repos/$REPO/releases/tags/son-server --jq '.assets[] | select(.name=="ZootopiaServer.tar.gz") | .id')
if [ ! -f "$WORK/asset-$aid" ]; then
  rm -rf "$WORK/srv" && mkdir -p "$WORK/srv"
  gh api repos/$REPO/releases/assets/$aid -H "Accept: application/octet-stream" > "$WORK/server.tar.gz"
  tar -xzf "$WORK/server.tar.gz" -C "$WORK/srv" && touch "$WORK/asset-$aid"
fi
N="$ROOT/Assets/Scripts/Game/Net"
mcs -out:"$WORK/fake.exe" -main:FakePhoneTest "$N/NetBuffer.cs" "$N/NetConnection.cs" "$N/NetSocket.cs" "$ROOT/Tools~/online-test/FakePhone.cs" > /dev/null
cd "$WORK/srv"
./ZootopiaServer.x86_64 -batchmode -nographics -server -port 7777 -code 123456 -mode "$MODE" -matchType private -logFile "$WORK/server.log" > /dev/null 2>&1 &
SRV=$!
cd "$WORK"
set +e
LANG=C.UTF-8 timeout 280 mono fake.exe 7777 "$(cat srv/VERSION)" "$MODE"
RESULT=$?
sleep 15
if kill -0 $SRV 2>/dev/null; then echo "server did not quit by itself"; kill $SRV; RESULT=1; fi
grep -n "Exception" "$WORK/server.log" | head -20
echo "server log: $WORK/server.log"
exit $RESULT
