# HR Service Desk & SLA Management System

[![Backend CI](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/actions/workflows/backend.yml/badge.svg?branch=backend)](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/actions/workflows/backend.yml)
[![Frontend CI](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/actions/workflows/frontend.yml/badge.svg?branch=frontend)](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/actions/workflows/frontend.yml)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)
![Angular 22](https://img.shields.io/badge/Angular-22-DD0031)
![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16-4169E1)

A multi-tenant **HR case management platform** in the style of the HR service delivery suites used by large employers. Employees submit HR requests (payslip corrections, certificates, leave, bank detail changes…) from a catalog. Requests go through configurable **approval workflows**, are routed to the right HR team, and are tracked against **business-hours-aware SLAs** with automatic **escalation**. HR leadership follows everything on **dashboards**, and external systems such as payroll integrate through a **REST API and signed webhooks**. Employees can sign in with their company account (**single sign-on with Microsoft Entra ID**), and an **AI assistant** (Claude) routes requests written in plain words and drafts replies for HR.

> **Status:** complete — all 13 phases delivered, plus a visual catalog editor, single sign-on and an AI assistant; 664 backend and 123 frontend tests green in CI. See the [roadmap](#roadmap) and the [demo guide](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/blob/backend/docs/DEMO.md).

---

## Table of contents

- [Screenshots](#screenshots)
- [Why this project](#why-this-project)
- [Features](#features)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [Repository structure](#repository-structure)
- [Getting started](#getting-started)
- [Roadmap](#roadmap)
- [Demo scenario](#demo-scenario)
- [Integration API and webhooks](#integration-api-and-webhooks)
- [Engineering principles](#engineering-principles)

---

## Screenshots

| | |
|---|---|
| ![AI assistant](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/12-assistant.jpg) **AI request assistant** — the employee writes the need in plain words; the right request is suggested with its articles | ![Pre-filled form](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/13-assistant-prefilled-form.jpg) **Pre-filled form** — title, pay period, issue and amount read from the description |
| ![AI draft](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/14-ai-draft-reply.jpg) **Draft with AI** — a reply for the HR agent to review before sending; never offered on confidential or sensitive cases | |
| ![Dashboard](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/03-dashboard.jpg) **HR dashboard** — SLA compliance, volume, backlog, workload and satisfaction, with table views and CSV export | ![Dashboard, dark theme](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/04-dashboard-dark.jpg) **Dark theme** — every chart follows the theme with a colour-blind-safe palette |
| ![HR queue](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/05-hr-queue.jpg) **HR queue** — SLA badges with deadlines in business hours | ![Case](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/06-case.jpg) **A case** — approvals, conversation, SLA, documents, history and the employee's rating |
| ![Suggestions](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/07-new-request-suggestions.jpg) **Deflection** — help articles suggested while the employee types | ![Arabic](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/08-help-center-arabic.jpg) **Arabic, right to left** — also French and English |
| ![Integrations](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/09-integrations.jpg) **Integrations** — API keys and signed webhooks with delivery history | ![Approvals in French](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/11-approvals-fr.jpg) **Manager approvals** (in French) |
| ![Sign in](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/01-sign-in.jpg) **Sign in** with remember-me and password reset | ![Users](https://raw.githubusercontent.com/Eya2/HR-Service-Desk-SLA-Management-System/frontend/frontend/docs/screenshots/10-users.jpg) **User administration** — roles, manager, activation |

## Why this project

HR teams handle hundreds of sensitive, deadline-bound requests every month. Without a dedicated tool, those requests end up in mailboxes and spreadsheets: deadlines slip, approvals get lost, confidential cases leak, and nobody can say how the service is performing.

This system brings IT-service-management discipline (catalog, workflows, SLAs, escalation, audit) to HR, while respecting HR-specific constraints: confidentiality, GDPR retention, country-specific calendars (Tunisia, France) and multilingual users (French, English, Arabic).

## Features

| Area | What it does |
|---|---|
| **AI request assistant** | The employee describes the need in a sentence ("mes heures sup de septembre n'apparaissent pas sur ma fiche de paie"); the assistant picks the request type, writes a title, **pre-fills the form** (pay period, issue, amount…) and shows help articles. HR agents get **"Draft with AI"** replies to review and edit. Runs on **Claude** (Haiku for routing with structured tool output, Sonnet for writing) when an Anthropic key is configured, otherwise on a built-in bilingual FR/EN classifier. Confidential and sensitive cases are never sent to the model, and its answers are validated against the form |
| **Single sign-on** | OpenID Connect per organisation (Microsoft Entra ID or any compliant provider): authorization code + PKCE handled server-side, provider discovered from the e-mail domain, optional just-in-time accounts, "SSO required" with a break-glass password for HR Admins; a mock identity provider is included for the demo |
| **Request catalog** | Request types by category (Payroll, Leave & Absence, Contracts, Benefits, Onboarding/Offboarding, Training, Expenses, Certificates, Confidential), each with its own **dynamic form** defined as a JSON schema, designed by HR Admins in a **visual form builder** (drag-and-drop fields, live preview, sensitivity flag) |
| **HR cases (tickets)** | Reference numbers (`HR-2026-000123`), priorities, public replies and internal notes, attachments with type and size validation |
| **Status machine** | Explicit transition table (New → PendingApproval → Open → InProgress → WaitingOnEmployee → Resolved → Closed, plus Reopened, Rejected, Cancelled) with role-based permissions; every transition is audited |
| **Approval workflows** | Ordered approval steps per request type (Manager, HR Officer, Payroll Specialist…), edited by HR Admins; a rejection ends the flow |
| **Assignment** | Manual, round-robin and least-loaded strategies per team, with optimistic concurrency so two agents can't claim the same case |
| **SLA engine** | First-response and resolution targets per priority and request type, computed in **business hours** (working days, public holidays for Tunisia and France); the clock pauses while waiting on the employee or on approval; OnTrack / AtRisk / Breached states |
| **Escalation** | Rules such as "AtRisk → notify assignee" or "Breached → bump priority and reassign to tier 2", fired once per case (idempotent) |
| **Notifications** | Real-time in-app bell (SignalR) and email |
| **Confidentiality & GDPR** | Confidential cases (e.g. harassment reports) visible only to a restricted HR group, enforced in database queries; audit log of sensitive reads; automatic anonymization after a retention period |
| **Dashboards** | SLA compliance, response and resolution times, backlog, volume over time (pay-day peaks), agent workload, reopen rate, CSV export |
| **Employee portal** | Catalog search, dynamic forms, "My requests" with a status timeline, satisfaction rating (1–5 stars) on closed cases |
| **Help center** | Knowledge base with accent-insensitive full-text search (PostgreSQL), article suggestions while typing a request (deflection), views and "this answered my question" statistics, admin editor |
| **Manager view** | Pending approvals queue, team requests and stats |
| **Integration** | Scoped API keys (hashed, shown once), a versioned integration API, outbox-based webhooks signed with HMAC-SHA256 and retried for 9 hours, SSRF protection, and a mock payroll system that closes the loop |
| **Administration** | Users and roles, approval workflows, teams, SLA calendars and policies, escalation rules, data retention, help-center articles, integrations |
| **Multi-tenancy** | Every business row carries a `TenantId`, enforced by a global EF Core query filter |
| **i18n** | French, English and Arabic (right-to-left layout) |

### Roles

`Employee` · `Manager` · `HR Officer` · `Payroll Specialist` · `HR Admin` · `Auditor` (read-only) · `SuperAdmin` (tenant management)

## Tech stack

| Layer | Technologies |
|---|---|
| **Backend** | .NET 8, ASP.NET Core Web API, Entity Framework Core 8, PostgreSQL (Npgsql), MediatR (CQRS), FluentValidation, Serilog, Swagger/OpenAPI |
| **Background jobs** | Hangfire (PostgreSQL storage) |
| **Auth** | JWT access tokens + rotating refresh tokens, role- and policy-based authorization, OpenID Connect single sign-on (Microsoft Entra ID) |
| **AI** | Claude through the Anthropic Messages API (tool use for structured output), with a local bilingual fallback |
| **Frontend** | Angular 22 (standalone components, signals, lazy-loaded routes), Angular Material 3 with light and dark themes, Reactive Forms, RxJS, Chart.js, ngx-translate |
| **Tests** | xUnit, FluentAssertions, Testcontainers (real PostgreSQL), Karma + Jasmine |
| **DevOps** | Docker, Docker Compose, nginx, GitHub Actions, MailHog |

## Architecture

```mermaid
flowchart LR
    subgraph Browser
        WEB[Angular SPA<br/>portal · manager · agent · admin · dashboard]
    end
    subgraph Docker
        NGINX[web: nginx<br/>serves SPA, proxies /api]
        API[api: ASP.NET Core 8<br/>REST · SignalR · Hangfire]
        PG[(PostgreSQL 16<br/>app data + jobs)]
        MAIL[MailHog<br/>SMTP sink]
        PAY[mock-payroll<br/>webhook consumer]
        IDP[mock-idp<br/>OpenID Connect]
    end
    CLAUDE[Anthropic API<br/>Claude, optional]
    EXT[External system<br/>API key] --> API
    WEB --> NGINX --> API
    API --> PG
    API -- SMTP --> MAIL
    API -- HMAC-signed webhooks --> PAY
    API -- OIDC code + PKCE --> IDP
    API -. routing and drafts .-> CLAUDE
```

The backend follows **Clean Architecture**:

```mermaid
flowchart TB
    Api[Api<br/>controllers · middleware · auth · Swagger] --> Application
    Api --> Infrastructure
    Infrastructure[Infrastructure<br/>EF Core · Hangfire · email · storage · webhooks] --> Application
    Application[Application<br/>use cases · DTOs · validators · ports] --> Domain
    Domain[Domain<br/>entities · status machine · SLA calculator · rules]
```

- **Domain** has no framework dependency. The status machine, business-time calculator, assignment strategies and escalation evaluator are pure and unit-tested.
- **Application** holds one folder per use case (command or query, handler, validator, DTOs), dispatched through MediatR with validation and logging pipelines.
- **Infrastructure** implements the ports: EF Core with a tenant query filter, interceptors that stamp tenant and timestamps, jobs, email, file storage.
- **Api** stays thin: it binds requests, sends them to MediatR and maps results to HTTP and RFC 7807 ProblemDetails.

Full design notes: [`docs/ARCHITECTURE.md`](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/blob/backend/docs/ARCHITECTURE.md) (on the `backend` branch).

## Repository structure

The code is split into one branch per application:

| Branch | Content |
|---|---|
| [`main`](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/tree/main) | This README |
| [`backend`](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/tree/backend) | `backend/` (.NET solution, Dockerfile, compose for postgres, api and mailhog), `docs/`, backend CI |
| [`frontend`](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/tree/frontend) | `frontend/` (Angular app, nginx Dockerfile, compose for the web container), frontend CI |

Each folder is self-contained, and the branches share nothing but this README, so they can be merged together without conflicts.

## Getting started

### Prerequisites

- Docker Desktop (or Docker Engine with Compose v2)
- For local development without Docker: .NET SDK 8, Node.js 24 LTS

### 1. Start the backend (PostgreSQL, API, MailHog)

```bash
git clone -b backend https://github.com/Eya2/HR-Service-Desk-SLA-Management-System.git hr-backend
cd hr-backend/backend
cp .env.example .env        # then set the secrets: each line of the file explains how to generate it
                            # optional: ANTHROPIC_API_KEY=... to use Claude (otherwise the local assistant is used)
docker compose up -d --build
```

| Service | URL |
|---|---|
| API (Swagger) | http://localhost:5080/swagger |
| Health | http://localhost:5080/health/ready |
| MailHog (e-mails sent by the app) | http://localhost:8025 |
| Mock payroll system (webhooks received) | http://localhost:8090 |
| Background jobs (Hangfire, local requests only) | http://localhost:5080/hangfire |
| Mock identity provider (demo SSO: sign in as anyone `@acme.example`) | http://localhost:8095 |

### 2. Start the frontend

```bash
git clone -b frontend https://github.com/Eya2/HR-Service-Desk-SLA-Management-System.git hr-frontend
cd hr-frontend/frontend
docker compose up -d --build
```

The app is served at http://localhost:8080 and forwards `/api` to the backend on port 5080.

### Demo accounts

On first start the API seeds two organisations, one account per role plus six more employees each, and **six weeks of history** (about 110 cases in every status, approvals, ratings, late and at-risk cases, a confidential report), so queues and dashboards are alive from the first minute. Every account uses the password you set in `SEED_PASSWORD`.

| Organisation | Role | E-mail |
|---|---|---|
| Acme Tunisie | Employee | `amira.bensalah@acme.example` |
| Acme Tunisie | Manager (Amira's manager) | `youssef.haddad@acme.example` |
| Acme Tunisie | HR Officer | `leila.mansour@acme.example` |
| Acme Tunisie | Payroll Specialist | `sami.gharbi@acme.example` |
| Acme Tunisie | HR Admin | `nadia.jaziri@acme.example` |
| Acme Tunisie | Auditor | `hedi.chaabane@acme.example` |
| Globex France | Employee · Manager · HR Officer · Payroll · HR Admin · Auditor | `camille.martin@` · `julien.bernard@` · `sophie.laurent@` · `thomas.petit@` · `claire.moreau@` · `antoine.dubois@globex.example` |
| Platform | SuperAdmin | `platform.admin@platform.example` |

### Local development (without Docker for the apps)

```bash
# backend: needs a PostgreSQL instance (e.g. `docker compose up -d postgres` in backend/)
cd backend
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/HrServiceDesk.Api
dotnet user-secrets set "Seed:DemoPassword" "<a 12+ character password>" --project src/HrServiceDesk.Api
dotnet run --project src/HrServiceDesk.Api        # http://localhost:5080/swagger

# frontend: proxies /api to http://localhost:5080
cd frontend
npm ci
npm start                                         # http://localhost:4200
```

### Running the tests

```bash
cd backend && dotnet test          # 446 domain, 60 application, 158 integration tests (Testcontainers needs Docker running)
cd frontend && npm run test:ci     # 123 specs, Karma with headless Chrome
```

## Roadmap

The project is delivered in 13 phases. Each phase ends with a green build and test suite and is merged before the next one starts.

| # | Phase | Scope | Branch | Status |
|---|---|---|---|---|
| 1 | **Scaffolding** | Solution structure, EF Core + tenant filter, Serilog, Swagger, ProblemDetails, health checks, Docker Compose, Angular shell, CI | backend · frontend | ✅ Done |
| 2 | **Authentication** | Users, roles, policies, JWT + rotating refresh tokens, password policy, rate limiting; Angular login, layout, guards, interceptor | backend · frontend | ✅ Done |
| 3 | **Catalog & tickets** | Request types with JSON form schema, tickets CRUD, reference numbers, public/internal comments, attachments; employee portal | backend · frontend | ✅ Done |
| 4 | **Status machine & audit** | Transition table with role permissions, audit events, timeline UI, exhaustive tests | backend · frontend | ✅ Done |
| 5 | **Approval workflows** | Workflow engine, admin workflow editor, manager approval queue | backend · frontend | ✅ Done |
| 6 | **Teams & assignment** | Teams, manual / round-robin / least-loaded strategies, optimistic concurrency, agent queue | backend · frontend | ✅ Done |
| 7 | **SLA foundations** | Business calendars and holidays (TN/FR), business-time calculator, SLA policies, pause/resume | backend · frontend | ✅ Done |
| 8 | **SLA monitor & escalation** | Hangfire job every minute, escalation rules (idempotent), in-app and email notifications | backend · frontend | ✅ Done |
| 9 | **Confidentiality & GDPR** | Restricted HR group, sensitive-access audit log, retention and anonymization job, audit viewer | backend · frontend | ✅ Done |
| 10 | **Dashboards** | KPIs, charts, date and team filters, CSV export | backend · frontend | ✅ Done |
| 11 | **Knowledge base, CSAT & i18n** | FAQ with suggestions while typing, satisfaction ratings, FR / EN / AR with RTL | backend · frontend | ✅ Done |
| 12 | **Integration** | API keys, integration endpoints, HMAC-signed webhooks with retry, mock payroll container | backend | ✅ Done |
| 13 | **Demo & polish** | Six weeks of demo history, end-to-end demo script and test, user administration, screenshots, final documentation | backend · frontend | ✅ Done |
| + | **Catalog editor** | Visual form builder with drag-and-drop fields and live preview, sensitive request types | backend · frontend | ✅ Done |
| + | **Single sign-on** | OpenID Connect / Microsoft Entra ID per organisation, PKCE, JIT accounts, mock identity provider | backend · frontend | ✅ Done |
| + | **AI assistant** | Claude-powered request routing with pre-filled forms and drafted HR replies, local bilingual fallback, privacy guards | backend · frontend | ✅ Done |

### Definition of done

- `docker compose up` starts the full system with seeded demo data.
- All tests pass in CI.
- The [demo scenario](#demo-scenario) works end to end.

## Demo scenario

1. An **employee** submits a *payslip correction* through the portal's dynamic form (related help articles are suggested as she types).
2. Her **manager** approves it from the approval queue; the SLA clock was paused meanwhile.
3. The case is routed to the **Payroll** team; its deadline is computed in business hours and **skips a public holiday** (Evacuation Day, 15 October, in Tunisia).
4. The **payroll system** receives a signed webhook and answers through the integration API.
5. Time passes: the case turns **AtRisk** (the assignee is warned), then **Breached** (the manager is alerted, the priority raised).
6. The **dashboard** shows the breach; the **audit log** shows who opened sensitive cases.
7. A **confidential** harassment report stays invisible to a regular HR officer.
8. Payroll resolves the case; the employee closes it and **rates** the service.

Three ways to see it:

- **Script** — `cd backend && ./scripts/demo.sh` plays the story against the running stack and prints each step.
- **Guided tour** — [`docs/DEMO.md`](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/blob/backend/docs/DEMO.md) walks through the web app with each demo account.
- **Automated** — [`DemoScenarioTests`](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/blob/backend/backend/tests/HrServiceDesk.IntegrationTests/Demo/DemoScenarioTests.cs) runs the whole story with a simulated clock on every push.

## Integration API and webhooks

External systems call `/api/integration/v1` with an `X-Api-Key` header (keys are created by an HR Admin, scoped to `tickets:read` / `tickets:write`, and write as their own service account):

```bash
curl -H "X-Api-Key: $KEY" "http://localhost:5080/api/integration/v1/tickets?category=Payroll&updatedSince=2026-10-01T00:00:00Z"
curl -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
     -d '{"status":"Resolved","reason":"Booked as PAY-202610-0042."}' \
     http://localhost:5080/api/integration/v1/tickets/{id}/status
```

Webhooks carry thin payloads (identifiers, statuses, request type — never titles, comments or confidential cases), an `X-HrDesk-Delivery` id for idempotency and a signature to verify:

```text
X-HrDesk-Signature: t=1791000000,v1=<hex HMAC-SHA256(secret, "1791000000." + body)>
```

Failed deliveries are retried after 1 min, 5 min, 30 min, 2 h and 6 h, then marked failed and can be sent again from the admin screen. The [mock payroll](https://github.com/Eya2/HR-Service-Desk-SLA-Management-System/tree/backend/backend/tools/MockPayroll) is a complete receiver example.

## Engineering principles

- Clean Architecture, with business rules in the domain and no logic in controllers
- `async`/`await` end to end, DTOs separate from entities, RFC 7807 ProblemDetails for every error
- Multi-tenant isolation and confidentiality enforced on the server, in queries, never only in the UI
- Unit tests for domain logic, and integration tests against a real PostgreSQL through Testcontainers
- No secrets in the repository, and no personal data in logs
- Small, conventional commits (`feat:`, `fix:`, `test:`, `docs:`…)
