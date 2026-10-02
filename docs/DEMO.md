# Demo guide

Two ways to see the whole product: a **five-minute script** against the running stack, and a **guided tour** of the
web app. The same story is also an automated test with a simulated clock
(`backend/tests/HrServiceDesk.IntegrationTests/Demo/DemoScenarioTests.cs`), so it is checked on every push.

## Start the stack

```bash
cd backend && cp .env.example .env    # then set the secrets (see the comments in the file)
docker compose up -d --build          # API, PostgreSQL, MailHog, mock payroll
cd ../frontend && docker compose up -d --build   # web app (on the frontend branch)
```

| Service | URL |
|---|---|
| Web app | http://localhost:8080 |
| API documentation (Swagger: desk API and integration API) | http://localhost:5080/swagger |
| E-mails sent by the desk (MailHog) | http://localhost:8025 |
| Mock payroll system (received webhooks) | http://localhost:8090 |
| Background jobs (Hangfire) | http://localhost:5080/hangfire |

The first start seeds two organisations with six weeks of history: about 65 cases for **Acme Tunisie** (Tunisia
calendar) and 45 for **Globex France** (France calendar), in every status, with ratings, late cases and a
confidential report. The SLA monitor runs every minute and escalates what is at risk or late.

## Demo accounts

Every account uses the password set in `SEED_PASSWORD`.

| Role | Acme Tunisie | Globex France |
|---|---|---|
| Employee | amira.bensalah@acme.example (+ 6 colleagues, e.g. mehdi.trabelsi@) | camille.martin@globex.example (+ 6 colleagues) |
| Manager | youssef.haddad@acme.example | julien.bernard@globex.example |
| HR officer | leila.mansour@acme.example | sophie.laurent@globex.example |
| Payroll specialist | sami.gharbi@acme.example | thomas.petit@globex.example |
| HR admin (restricted group) | nadia.jaziri@acme.example | claire.moreau@globex.example |
| Auditor | hedi.chaabane@acme.example | antoine.dubois@globex.example |
| Platform super admin | platform.admin@platform.example | |

## The five-minute script

```bash
cd backend && ./scripts/demo.sh
```

It plays the story through the API and prints each step: payslip correction → manager approval → signed
webhook to payroll → payroll resolves through the integration API → the employee closes and rates →
a confidential report hidden from an HR officer → late cases, dashboard figures and the audit log.

## Guided tour of the web app

1. **Employee** — sign in as Amira. Try the language switcher (Français, العربية for right-to-left).
   In *My requests → Help center*, search "certificat": accent-insensitive results, best match first.
   Open *New request → Payslip correction*: while you type the title, related articles are suggested.
   Submit (attach any PDF). The case waits for the manager's approval: the SLA clock is paused.
2. **Manager** — sign in as Youssef, *Approvals*: approve in one click. The case goes to the Payroll team.
3. **Payroll integration** — within seconds the mock payroll (http://localhost:8090) receives the signed
   webhook, takes the case and resolves it with a payroll reference, as "Payroll connector (API)".
4. **Employee** — back as Amira: the case shows the payroll reply in the conversation and history.
   Close it: the "How did we do?" card appears; rate it.
5. **HR officer** — sign in as Leila, *HR queue*: SLA badges (on track, at risk, late) with deadlines in
   business hours. Open a case: the SLA card shows targets and deadlines; the history shows escalations.
   The confidential harassment report is not in her queue.
6. **HR admin** — sign in as Nadia: the confidential report is visible to her (restricted group).
   *Dashboards*: SLA compliance, volume, backlog, workload, satisfaction; switch to tables; export CSV;
   try the dark theme. *Administration*: workflows, teams, SLA calendar (check a deadline over a holiday),
   escalation rules, retention, help-center articles, and *Integrations* (API keys, webhooks, delivery
   history with retry). *Audit*: who opened sensitive cases (bank details, leave).
7. **E-mails** — http://localhost:8025 shows the notifications sent along the way.

## Integration API in two calls

```bash
KEY=$(grep PAYROLL_API_KEY backend/.env | cut -d= -f2)
curl -s "http://localhost:5080/api/integration/v1/tickets?category=Payroll&pageSize=5" -H "X-Api-Key: $KEY" | jq '.items[] | {reference, status}'
```

Webhook receivers verify `X-HrDesk-Signature: t=<unix>,v1=<hex HMAC-SHA256(secret, "t.body")>` and ignore
duplicates by `X-HrDesk-Delivery`; see `backend/tools/MockPayroll/Program.cs` for a complete receiver.
