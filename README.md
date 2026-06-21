# SMS — School Management System

Local-first school management for a single campus: students, classes, attendance, biometric gate (local test), academic years, promotion, and WhatsApp notification queue.

**Stack:** .NET 8 · Blazor Server · EF Core · SQL Server

---

## Quick start

```bash
# 1. Start SQL Server (Docker)
docker compose up -d

# 2. Restore, migrate, run
dotnet restore
dotnet ef database update --project src/SMS.Infrastructure --startup-project src/SMS.Web
dotnet run --project src/SMS.Web
```

Open **http://localhost:5258**

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@school.local` | `Admin@123` |
| Coordinator | `coordinator@school.local` | `Coordinator@123` |
| Teacher | `teacher@school.local` | `Teacher@123` |

Connection string: `src/SMS.Web/appsettings.json` (default: `localhost,14331` / Docker).

---

## Documentation

| Document | Audience |
|----------|----------|
| **[docs/HANDOVER.md](docs/HANDOVER.md)** | **Next developer** — full technical handover, architecture, routes, RBAC, biometric, testing, troubleshooting |
| **[docs/CURSOR-AI-GUIDE.md](docs/CURSOR-AI-GUIDE.md)** | **Next developer** — how to use Cursor AI on this project (chat is not stored in repo) |
| `/help` in the app | End-user help for school staff |

---

## Solution layout

```text
SMS.slnx
├── src/SMS.Domain
├── src/SMS.Application
├── src/SMS.Infrastructure   # EF migrations, workers, PDF export
├── src/SMS.Web              # Blazor UI
└── tests/SMS.Tests          # 34 integration tests
```

---

## Tests

```bash
dotnet test
```

Stop the running web app before `dotnet build` or EF migrations to avoid file-lock errors.

---

## Key routes

| Route | Purpose |
|-------|---------|
| `/` | Dashboard |
| `/students`, `/classes` | Roster & structure |
| `/attendance/manual`, `/daily`, `/register` | Attendance |
| `/attendance/gate` | Gate kiosk |
| `/attendance/live` | Live scan monitor |
| `/settings/school`, `/users`, `/exception-logs` | Admin |

See **[docs/HANDOVER.md](docs/HANDOVER.md)** for the complete route list and role matrix.

---

## Biometric note

Gate kiosk uses **local simulated** biometric (camera + browser fingerprint). Real **ZKTeco** hardware is not integrated — extend `BiometricWorker.cs` when hardware is available.

---

## License / third-party

- **QuestPDF** — Community license (monthly register PDF export)
- **face-api** — loaded from CDN for local face enrollment/test
