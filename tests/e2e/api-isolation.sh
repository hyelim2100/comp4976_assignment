#!/usr/bin/env bash
# Tenant isolation of MEMS.Api: cross-tenant tokens, ids, slugs, uploads. Prints markdown tables.
# Run with the Aspire AppHost started:  bash tests/e2e/api-isolation.sh
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
# Native Windows curl needs Windows paths for -F file uploads (Git Bash paths are not converted there).
winpath() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }
API=${API:-https://localhost:7201}
PUB=${PUB:-https://localhost:5001}
json() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }
login() { curl -sk -X POST "$API/api/auth/login" -H 'Content-Type: application/json' -d "{\"userName\":\"$1\",\"password\":\"P@\$\$w0rd\"}" | json 'd["token"]'; }

PASS=0; FAIL=0
row() { # name method url expected [token] [body] [content-type]
  local name="$1" method="$2" url="$3" expected="$4" token="$5" body="$6"
  local args=(-sk -o "$TMP/body" -w '%{http_code}' -X "$method" "$url")
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$body" ] && args+=(-H 'Content-Type: application/json' -d "$body")
  local code; code=$(curl "${args[@]}")
  local ok="❌"; if [ "$code" = "$expected" ]; then ok="✅"; PASS=$((PASS+1)); else FAIL=$((FAIL+1)); fi
  echo "| $name | \`$method ${url#$API}\` | $expected | $code | $ok |"
}

BURNABY=$(login burnabyadmin); PM=$(login pm); SUPER=$(login Super)
SURREY_CAND=$(curl -sk "$API/api/surrey/candidates" | json 'd[0]["id"]')
SURREY_POS=$(curl -sk "$API/api/surrey/positions" | json 'd[0]["id"]')
BBY_POS=$(curl -sk "$API/api/burnaby/positions" | json 'd[0]["id"]')
BBY_LOGO=$(curl -sk "$API/api/burnaby/municipality" | json 'd["logoImageId"]')
CAND='{"name":"Test Candidate","positionId":'"$SURREY_POS"',"email":"t@example.com"}'
MUNI='{"name":"Surrey","slug":"surrey","province":"British Columbia","electionDate":"2026-10-17","primaryColor":"#0E7C86"}'

echo "### A. Admin token of another tenant (burnabyadmin -> Surrey, pm -> Burnaby)"
echo "| Scenario | Request | Expected | Actual | Result |"; echo "|---|---|---|---|---|"
row "burnabyadmin: list Surrey candidates" GET "$API/api/surrey/candidates" 403 "$BURNABY"
row "burnabyadmin: get Surrey candidate" GET "$API/api/surrey/candidates/$SURREY_CAND" 403 "$BURNABY"
row "burnabyadmin: create candidate in Surrey" POST "$API/api/surrey/candidates" 403 "$BURNABY" "$CAND"
row "burnabyadmin: update Surrey candidate" PUT "$API/api/surrey/candidates/$SURREY_CAND" 403 "$BURNABY" "$CAND"
row "burnabyadmin: delete Surrey candidate" DELETE "$API/api/surrey/candidates/$SURREY_CAND" 403 "$BURNABY"
row "burnabyadmin: create position in Surrey" POST "$API/api/surrey/positions" 403 "$BURNABY" '{"name":"X","seatsAvailable":1}'
row "burnabyadmin: update Surrey municipality" PUT "$API/api/surrey/municipality" 403 "$BURNABY" "$MUNI"
row "pm: delete Burnaby position" DELETE "$API/api/burnaby/positions/$BBY_POS" 403 "$PM"
row "burnabyadmin: own route + Surrey candidate id (GET)" GET "$API/api/burnaby/candidates/$SURREY_CAND" 404 "$BURNABY"
row "burnabyadmin: own route + Surrey candidate id (PUT)" PUT "$API/api/burnaby/candidates/$SURREY_CAND" 404 "$BURNABY" "$CAND"
row "burnabyadmin: own route + Surrey candidate id (DELETE)" DELETE "$API/api/burnaby/candidates/$SURREY_CAND" 404 "$BURNABY"
row "burnabyadmin: own route + Surrey position id (PUT)" PUT "$API/api/burnaby/positions/$SURREY_POS" 404 "$BURNABY" '{"name":"X","seatsAvailable":1}'
row "burnabyadmin: candidate with a Surrey positionId in the body" POST "$API/api/burnaby/candidates" 400 "$BURNABY" "$CAND"
row "burnabyadmin: delete tenant (Super Admin only)" DELETE "$API/api/burnaby/municipality" 403 "$BURNABY"
row "burnabyadmin: create tenant (Super Admin only)" POST "$API/api/municipalities" 403 "$BURNABY" '{}'
row "write without a token" POST "$API/api/burnaby/positions" 401 "" '{"name":"X","seatsAvailable":1}'
row "forged / invalid token" POST "$API/api/burnaby/positions" 401 "abc.def.ghi" '{"name":"X","seatsAvailable":1}'

echo; echo "### B. Anonymous public GETs and tenant resolution"
echo "| Scenario | Request | Expected | Actual | Result |"; echo "|---|---|---|---|---|"
row "municipality directory (not tenant-filtered)" GET "$API/api/municipalities" 200
row "Burnaby candidates" GET "$API/api/burnaby/candidates" 200
row "mixed-case slug /Richmond" GET "$API/api/Richmond/positions" 200
row "unknown slug" GET "$API/api/vancouver/candidates" 404
row "candidate id of another tenant" GET "$API/api/burnaby/candidates/$SURREY_CAND" 404
row "Burnaby logo on the Burnaby route" GET "$API/api/burnaby/images/$BBY_LOGO" 200
row "Burnaby logo on the Surrey route" GET "$API/api/surrey/images/$BBY_LOGO" 404
row "through the Public host proxy" GET "$PUB/api/burnaby/municipality" 200
row "login with a wrong password" POST "$API/api/auth/login" 401 "" '{"userName":"burnabyadmin","password":"wrong"}'
row "Swagger UI" GET "$API/swagger/index.html" 200
row "OpenAPI document" GET "$API/openapi/v1.json" 200

echo; echo "### C. Own-tenant writes and Super Admin scope"
echo "| Scenario | Request | Expected | Actual | Result |"; echo "|---|---|---|---|---|"
NEW=$(curl -sk -X POST "$API/api/burnaby/candidates" -H "Authorization: Bearer $BURNABY" -H 'Content-Type: application/json' -d '{"name":"API Test","positionId":'"$BBY_POS"'}' | json 'd["id"]')
row "burnabyadmin: update own candidate" PUT "$API/api/burnaby/candidates/$NEW" 204 "$BURNABY" '{"name":"API Test 2","positionId":'"$BBY_POS"'}'
row "validation error (email / URL format)" POST "$API/api/burnaby/candidates" 400 "$BURNABY" '{"name":"","positionId":'"$BBY_POS"',"email":"not-an-email","website":"nope"}'
PNG="$HERE/../../MEMS.Data/SeedData/Images/seed-logo-surrey.png"
printf 'this is not an image' > "$TMP/fake.png"
code=$(curl -sk -o /dev/null -w '%{http_code}' -X PUT "$API/api/burnaby/candidates/$NEW/photo" -H "Authorization: Bearer $BURNABY" -F "file=@$(winpath "$PNG");type=image/png")
if [ "$code" = 204 ]; then r="✅"; PASS=$((PASS+1)); else r="❌"; FAIL=$((FAIL+1)); fi
echo "| burnabyadmin: upload a real PNG photo | \`PUT /api/burnaby/candidates/$NEW/photo\` | 204 | $code | $r |"
code=$(curl -sk -o /dev/null -w '%{http_code}' -X PUT "$API/api/burnaby/candidates/$NEW/photo" -H "Authorization: Bearer $BURNABY" -F "file=@$(winpath "$TMP/fake.png");type=image/png")
if [ "$code" = 400 ]; then r="✅"; PASS=$((PASS+1)); else r="❌"; FAIL=$((FAIL+1)); fi
echo "| fake image (text file named .png) | \`PUT /api/burnaby/candidates/$NEW/photo\` | 400 | $code | $r |"
row "burnabyadmin: delete own candidate" DELETE "$API/api/burnaby/candidates/$NEW" 204 "$BURNABY"
row "Super (capital-S login): Burnaby candidates" GET "$API/api/burnaby/candidates" 200 "$SUPER"
row "Super: Burnaby route + Surrey candidate id" GET "$API/api/burnaby/candidates/$SURREY_CAND" 404 "$SUPER"
SUPERPOS=$(curl -sk -X POST "$API/api/surrey/positions" -H "Authorization: Bearer $SUPER" -H 'Content-Type: application/json' -d '{"name":"Super Test","seatsAvailable":2,"municipalityId":1}')
echo "| Super: POST /api/surrey/positions with municipalityId=1 (Burnaby) | -> stored in tenant | surrey | $(echo "$SUPERPOS" | json 'd["municipalitySlug"]') | $([ "$(echo "$SUPERPOS" | json 'd["municipalitySlug"]')" = surrey ] && echo ✅ || echo ❌) |"
row "Super: delete that position" DELETE "$API/api/surrey/positions/$(echo "$SUPERPOS" | json 'd["id"]')" 204 "$SUPER"

echo; echo "### D. Response contents"
echo "- municipalitySlug values in /api/burnaby/candidates with a Super token: $(curl -sk "$API/api/burnaby/candidates" -H "Authorization: Bearer $SUPER" | json 'sorted(set(c["municipalitySlug"] for c in d))') (count $(curl -sk "$API/api/burnaby/candidates" -H "Authorization: Bearer $SUPER" | json 'len(d)'))"
echo "- municipalitySlug values in anonymous /api/burnaby/candidates: $(curl -sk "$API/api/burnaby/candidates" | json 'sorted(set(c["municipalitySlug"] for c in d))')"
echo "- /api/municipalities count: $(curl -sk "$API/api/municipalities" | json 'len(d)')"
echo "- 403 response body: \`$(curl -sk "$API/api/surrey/candidates" -H "Authorization: Bearer $BURNABY")\`"
echo "- 400 response body: \`$(curl -sk -X POST "$API/api/burnaby/candidates" -H "Authorization: Bearer $BURNABY" -H 'Content-Type: application/json' -d '{"name":"","positionId":'"$BBY_POS"',"email":"not-an-email"}')\`"
echo; echo "**TOTAL: $PASS passed, $FAIL failed**"
