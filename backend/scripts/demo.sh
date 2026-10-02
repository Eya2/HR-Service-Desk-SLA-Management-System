#!/usr/bin/env bash
# End-to-end demo against the running Docker stack (docker compose up -d --build).
# Needs curl and jq. Reads SEED_PASSWORD from .env (next to docker-compose.yml).
#
#   ./scripts/demo.sh            # API on http://localhost:5080
#   API=http://host:port ./scripts/demo.sh
#
# Story: Amira asks for a payslip correction → her manager approves → the desk sends a signed webhook to the
# payroll system → payroll takes the case and resolves it through the integration API → Amira closes and rates.
# Along the way: the SLA deadline (business hours, holidays skipped), a confidential case hidden from an HR
# officer, the audit log and the dashboard.
set -euo pipefail

cd "$(dirname "$0")/.."
API="${API:-http://localhost:5080}"
PASSWORD="${SEED_PASSWORD:-$(grep -E '^SEED_PASSWORD=' .env | cut -d= -f2-)}"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

bold() { printf '\n\033[1m%s\033[0m\n' "$*"; }
info() { printf '  %s\n' "$*"; }

login() {
  curl -sf "$API/api/auth/login" -H 'Content-Type: application/json' \
    -d "$(jq -n --arg e "$1" --arg p "$PASSWORD" '{email: $e, password: $p}')" | jq -r .accessToken
}
get() { curl -sf "$API$2" -H "Authorization: Bearer $1"; }
post() { curl -sf -X POST "$API$2" -H "Authorization: Bearer $1" -H 'Content-Type: application/json' -d "$3"; }

bold "1. Sign in (demo accounts of Acme Tunisie)"
AMIRA=$(login amira.bensalah@acme.example)
YOUSSEF=$(login youssef.haddad@acme.example)
LEILA=$(login leila.mansour@acme.example)
NADIA=$(login nadia.jaziri@acme.example)
info "Amira (employee), Youssef (manager), Leila (HR officer), Nadia (HR admin)"

bold "2. Amira asks for a payslip correction"
TYPE=$(get "$AMIRA" /api/request-types | jq -r '.[] | select(.name == "Payslip correction") | .id')
printf '%%PDF-1.4\n%% demo payslip\n%%%%EOF\n' > "$TMP/payslip.pdf"
PAYLOAD=$(jq -nc --arg t "$TYPE" '{requestTypeId: $t, title: "Payslip: overtime missing", description: "8 hours of overtime are missing.",
  values: {payPeriod: "2026-09", issue: "missing_overtime", expectedAmount: 120}}')
CASE=$(curl -sf "$API/api/tickets" -H "Authorization: Bearer $AMIRA" -F "payload=$PAYLOAD" -F "payslip=@$TMP/payslip.pdf;type=application/pdf")
ID=$(jq -r .id <<<"$CASE"); REF=$(jq -r .reference <<<"$CASE")
info "$REF submitted — status: $(get "$AMIRA" "/api/tickets/$ID" | jq -r .status) (the SLA clock is paused during approval)"

bold "3. Youssef approves from his queue"
APPROVAL=$(get "$YOUSSEF" /api/approvals/pending | jq -r --arg id "$ID" '.[] | select(.ticketId == $id) | .approvalId')
post "$YOUSSEF" "/api/tickets/$ID/approvals/$APPROVAL/decision" '{"approve": true}' >/dev/null
DETAILS=$(get "$NADIA" "/api/tickets/$ID")
info "status: $(jq -r .status <<<"$DETAILS"), team: $(jq -r .team.name <<<"$DETAILS"), assignee: $(jq -r .assignee.fullName <<<"$DETAILS")"
info "deadlines (business hours of the Tunisia calendar, public holidays skipped):"
info "  first response by $(jq -r .sla.firstResponseDueAt <<<"$DETAILS"), resolution by $(jq -r .sla.resolutionDueAt <<<"$DETAILS")"

bold "4. The payroll system receives a signed webhook and works the case (mock-payroll container)"
for _ in $(seq 1 30); do
  STATUS=$(get "$AMIRA" "/api/tickets/$ID" | jq -r .status)
  [ "$STATUS" = "Resolved" ] && break
  sleep 2
done
info "status: $STATUS"
get "$AMIRA" "/api/tickets/$ID" | jq -r '.comments[] | "  \(.authorName): \(.body)"'
info "received events: http://localhost:${MOCK_PAYROLL_PORT:-8090}"

bold "5. Amira closes the case and rates it"
post "$AMIRA" "/api/tickets/$ID/status" '{"status": "Closed"}' >/dev/null
post "$AMIRA" "/api/tickets/$ID/satisfaction" '{"score": 5, "comment": "Quick and clear, thanks!"}' >/dev/null
info "closed and rated 5/5"

bold "6. Confidentiality: a harassment report is visible to the restricted group only"
RTYPE=$(get "$AMIRA" /api/request-types | jq -r '.[] | select(.name == "Harassment report") | .id')
RPAYLOAD=$(jq -nc --arg t "$RTYPE" '{requestTypeId: $t, title: "Report", values: {incidentDate: "2026-09-28", whatHappened: "Repeated inappropriate remarks during meetings."}}')
RID=$(curl -sf "$API/api/tickets" -H "Authorization: Bearer $AMIRA" -F "payload=$RPAYLOAD" | jq -r .id)
info "Leila (HR officer): HTTP $(curl -s -o /dev/null -w '%{http_code}' "$API/api/tickets/$RID" -H "Authorization: Bearer $LEILA")"
info "Nadia (HR admin, restricted group): HTTP $(curl -s -o /dev/null -w '%{http_code}' "$API/api/tickets/$RID" -H "Authorization: Bearer $NADIA")"

bold "7. Late cases, escalations and the dashboard (last 30 days)"
get "$NADIA" "/api/tickets?scope=All&activeOnly=true&pageSize=100" \
  | jq -r '[.items[] | select(.slaState == "Breached" or .slaState == "AtRisk")] | .[:5][] | "  \(.reference)  \(.slaState)  \(.requestTypeName)"'
get "$NADIA" /api/dashboard | jq -r '.kpis | "  created \(.created), resolved \(.resolved), SLA compliance \(.slaCompliancePercent)%, satisfaction \(.averageSatisfaction)/5 (\(.ratings) ratings)"'

bold "8. Audit log (sensitive access and changes)"
get "$NADIA" "/api/audit-logs?pageSize=5" | jq -r '.items[] | "  \(.occurredAt[0:16])  \(.userName)  \(.action)  \(.summary)"'

bold "Done. Open http://localhost:8080 and sign in with the same accounts to see it in the app."
