#!/bin/sh
# Bonus demo: two backend processes (backend on :8080, backend2 on :8081) share one database.
# Fires many applications at both at once and checks that no more holds are granted than seats.
#   docker compose --profile bonus up -d --build
#   docker compose --profile bonus run --rm bonus-check
set -eu

A="${API_A:-http://backend:8080}"
B="${API_B:-http://backend2:8080}"
N="${REQUESTS:-200}"
SEATS="${SEATS:-10}"

class_json=$(curl -fsS -X POST "$A/classes" -H 'Content-Type: application/json' \
  -d "{\"name\":\"Bonus check $(date +%s)\",\"totalSeats\":$SEATS}")
class_id=$(printf '%s' "$class_json" | sed -E 's/.*"id":"([^"]+)".*/\1/')
echo "Class $class_id with $SEATS seats. Sending $N applications, alternating $A and $B ..."

dir=$(mktemp -d)
i=1
while [ "$i" -le "$N" ]; do
  if [ $((i % 2)) -eq 0 ]; then base="$A"; else base="$B"; fi
  student=$(printf '2015%013d' "$i")
  (curl -s -o /dev/null -w '%{http_code}\n' -X POST "$base/classes/$class_id/holds" \
     -H "X-Student-Id: $student" > "$dir/$i") &
  i=$((i + 1))
done
wait

created=$(cat "$dir"/* | grep -c '^201$' || true)
full=$(cat "$dir"/* | grep -c '^409$' || true)
other=$((N - created - full))
rm -rf "$dir"

echo "201 Created:  $created"
echo "409 Conflict: $full"
echo "Other:        $other"
echo "Seat counts seen by backend : $(curl -fsS "$A/classes/$class_id")"
echo "Seat counts seen by backend2: $(curl -fsS "$B/classes/$class_id")"

if [ "$created" -eq "$SEATS" ] && [ "$other" -eq 0 ]; then
  echo "PASS: exactly $SEATS holds were granted across two processes."
else
  echo "FAIL: expected exactly $SEATS holds (got $created) and no other status codes (got $other)."
  exit 1
fi
