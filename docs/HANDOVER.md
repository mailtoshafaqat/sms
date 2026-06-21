# SMS — Developer Handover & Technical Guide

This document is for the next developer taking over the **School Management System (SMS)**. It explains what was built, how to run and test it, and where to extend the code.

---

## 1. What this application is

SMS is a **local-first school management system** for a single school installation:

- Runs on the school’s PC or LAN (no cloud dependency for core features)
- **SQL Server** database on the same machine or Docker
- **Blazor Server** UI (.NET 8)
- **Clean Architecture**: Domain → Application → Infrastructure → Web

**Current scope (v1):** Students, classes, attendance (manual + biometric local test), academic years, promotion, WhatsApp notification queue, monthly register PDF/CSV, admin settings, error logging.

**Not in scope yet:** Parent portal, real ZKTeco hardware SDK, fees, payroll, accounts.

---

## 2. Technology stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8 |
| UI | Blazor Server (Interactive Server) |
| Auth | ASP.NET Core Identity (roles: Admin, Coordinator, Teacher) |
| ORM | Entity Framework Core 8 (Code First) |
| Database | SQL Server 2022 |
| PDF export | QuestPDF (Community license) |
| Face (local test) | face-api.js (CDN) + local templates in DB |
| Tests | xUnit + `WebApplicationFactory` integration tests |

---

## 3. Solution structure

```text
SMS.slnx
├── src/SMS.Domain/           Entities, enums, domain helpers
├── src/SMS.Application/      DTOs, service interfaces, application services
├── src/SMS.Infrastructure/   EF Core, repositories, workers, PDF/CSV export
├── src/SMS.Web/              Blazor pages, layouts, wwwroot (CSS/JS)
└── tests/SMS.Tests/          Integration tests (34 tests)
```

### Important folders

| Path | Purpose |
|------|---------|
| `src/SMS.Infrastructure/Data/` | `AppDbContext`, `DatabaseSeeder`, migrations |
| `src/SMS.Infrastructure/Data/Configurations/` | EF entity configurations |
| `src/SMS.Infrastructure/Repositories/` | Data access |
| `src/SMS.Infrastructure/Services/` | Workers, export, exception log, user access |
| `src/SMS.Infrastructure/Biometric/` | `BiometricWorker`, `SimulatedBiometricConnector` (placeholder) |
| `src/SMS.Web/Components/Pages/` | All UI routes |
| `src/SMS.Web/wwwroot/js/local-biometric.js` | Camera, face-api, WebAuthn fingerprint |
| `docs/HANDOVER.md` | This file |

---

## 4. Prerequisites

- **.NET 8 SDK**
- **SQL Server** (Express, full, or Docker — see below)
- **Windows** recommended for school deployment (current dev target)
- Optional: **Docker Desktop** for SQL Server container

---

## 5. First-time setup

### 5.1 Database (Docker — recommended)

```bash
cd SMS
docker compose up -d
```

SQL Server listens on **localhost:14331**  
SA password: `Password123!` (see `docker-compose.yml`)

### 5.2 Connection string

Edit `src/SMS.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,14331;Database=SMS;User Id=sa;Password=Password123!;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

For Windows SQL Express with Windows auth, use `Trusted_Connection=True` instead.

### 5.3 Restore, migrate, run

```bash
cd SMS
dotnet restore
dotnet ef database update --project src/SMS.Infrastructure --startup-project src/SMS.Web
dotnet run --project src/SMS.Web
```

Open: **http://localhost:5258**

> **Migrations at startup:** `Program.cs` calls `DatabaseSeeder.SeedAsync`, which runs `MigrateAsync()` on every app start. Manual `dotnet ef database update` is optional for local dev.

> **Important:** Stop the running app (`Ctrl+C`) before `dotnet build` or `dotnet ef migrations add` — otherwise DLL file locks cause build failures.

On first startup, `DatabaseSeeder` runs migrations and seeds demo school data. Default login accounts (created once if missing):

| Role | Email | Initial password |
|------|-------|------------------|
| Admin | `admin@school.local` | `Admin@123` |
| Coordinator | `coordinator@school.local` | `Coordinator@123` |

**Teacher accounts are not seeded.** Create them at **Settings → User Accounts**. Change all default passwords before deployment. Credentials are **not** shown on the login page.

> **Full school deployment (IIS, SQL Express, mobile gate, admin reset):** see **[DEPLOYMENT.md](DEPLOYMENT.md)**.

### 5.4 Admin password reset (emergency)

See **[DEPLOYMENT.md §8](DEPLOYMENT.md#8-admin-password-reset-emergency)**.

## 7. Roles & permissions

| Feature | Admin | Coordinator | Teacher |
|---------|:-----:|:-----------:|:-------:|
| Dashboard, students (scoped), classes | ✓ | ✓ | ✓ (section only) |
| Manual / daily attendance | ✓ | ✓ | ✓ (section only) |
| Mark absent (end of day) | ✓ | ✓ | — |
| Monthly register | ✓ | ✓ | ✓ |
| Promote students | ✓ | ✓ | — |
| Academic years | ✓ | ✓ | — |
| Parent notifications (WhatsApp queue) | ✓ | ✓ | — |
| Holiday calendar | ✓ | ✓ | — |
| School / biometric / backup / users / staff / error logs | ✓ | — | — |
| Biometric local test | ✓ | — | — |
| Gate kiosk / live gate | ✓ | ✓ | ✓ |

RBAC is implemented in:

- `[Authorize(Roles = "...")]` on pages
- `IUserAccessService` / `UserAccessService` for section filtering
- `AttendanceService.EnsureSectionAccessAsync` for attendance APIs

---

## 8. Feature inventory (what was built)

### Students & classes
- Student CRUD with pagination and search
- Auto student code if blank: `STU-{sectionId}-{rollNumber}`
- Validation: duplicate roll, code, biometric device ID
- Classes/sections **expandable tree** UI with reorder, add/edit inline
- Year-end **promotion** workflow

### Attendance
- Manual marking by section and date
- Daily sheet with filters and end-of-day **finalize absent**
- `AttendanceFinalizeWorker` — background job marks absent after school end time
- Monthly register with **PDF** and **CSV** download (US Letter landscape)
- Holiday calendar (one-off + annual recurring) + weekly off days

### Biometric (local / simulated — not real ZKTeco)
- **Gate kiosk** (`/attendance/gate`) — camera face + fingerprint button
- **Live gate** (`/attendance/live`) — monitor scans, admin simulate IN/OUT
- **Biometric test** (`/attendance/local-test`) — enroll face/fingerprint per student
- Templates stored in `StudentLocalTemplates`; maps in `StudentBiometricMaps`
- `BiometricWorker` + `SimulatedBiometricConnector` — **placeholder** for future hardware

### Coordinator & notifications
- Coordinator role with full attendance access
- Teacher–section assignment (`/settings/staff`)
- Academic year management (`/settings/academic-years`)
- WhatsApp notification queue (`/attendance/notifications`) — `wa.me` links, no API
- School toggles: `NotifyAbsent`, `NotifyLate`

### Admin & operations
- User accounts with activate/deactivate
- Database backup download (Admin)
- **Error logs** (`/settings/exception-logs`) — paginated, copyable reports for support
- Exception logging on all `SaveChanges` failures via `UnitOfWork`

### UI
- English only (Urdu/RTL removed)
- Compact layout, lighter login theme
- Toast notifications, confirm dialogs, tooltips

---

## 9. Routes reference

| Route | Description |
|-------|-------------|
| `/login` | Sign in |
| `/` | Dashboard (scoped by role) |
| `/students` | Student list |
| `/students/edit`, `/students/edit/{id}` | Add/edit student |
| `/students/promote` | Year-end promotion |
| `/classes` | Class/section tree |
| `/attendance/manual` | Mark attendance |
| `/attendance/daily` | Daily sheet + finalize |
| `/attendance/register` | Monthly register (PDF/CSV/print) |
| `/attendance/calendar` | Holiday calendar |
| `/attendance/notifications` | WhatsApp notification queue |
| `/attendance/live` | Live gate monitor |
| `/attendance/gate` | Full-screen gate kiosk |
| `/attendance/local-test` | Biometric enroll/test (Admin) |
| `/settings/school` | School settings |
| `/settings/biometric` | Device config |
| `/settings/staff` | Teacher–section assignment |
| `/settings/academic-years` | Academic sessions |
| `/settings/users` | User accounts |
| `/settings/backup` | DB backup |
| `/settings/exception-logs` | Application error logs |
| `/help` | In-app help |

### API-style download endpoints

| Endpoint | Auth | Output |
|----------|------|--------|
| `GET /attendance/register/export/pdf?sectionId=&year=&month=` | Logged in | PDF file |
| `GET /attendance/register/export/csv?sectionId=&year=&month=` | Logged in | CSV file |
| `GET /settings/backup/download/{fileName}` | Admin | Backup file |

---

## 10. Database

### Schemas
- `shared` — school, students, classes, academic years, exception logs
- `attendance` — logs, daily attendance, biometric maps, notifications
- ASP.NET Identity tables (default)

### Migrations

Migrations live in `src/SMS.Infrastructure/Migrations/`.

```bash
# Add new migration (app must be stopped)
dotnet ef migrations add YourMigrationName --project src/SMS.Infrastructure --startup-project src/SMS.Web

# Apply
dotnet ef database update --project src/SMS.Infrastructure --startup-project src/SMS.Web
```

Recent migrations include: coordinator notifications, app exception logs, section display order, promotions, holidays, local biometric templates.

### Key unique constraints (common save errors)

| Table | Constraint | Cause |
|-------|------------|-------|
| Students | `(SchoolId, StudentCode)` | Duplicate or empty student code |
| StudentEnrollments | `(AcademicYearId, SectionId, RollNumber)` | Duplicate roll in section |
| StudentBiometricMaps | `(BiometricDeviceId, BiometricUserId)` | Same gate ID on two students |

Errors are logged to `shared.AppExceptionLogs` and shown in Admin → Error Logs.

---

## 11. Architecture patterns

### Dependency injection
- `SMS.Application/DependencyInjection.cs` — application services
- `SMS.Infrastructure/DependencyInjection.cs` — EF, repos, workers, export

### Data access
- Repository interfaces in `SMS.Application/Interfaces/Repositories/`
- Implementations in `SMS.Infrastructure/Repositories/`
- `UnitOfWork` — scoped `DbContext` for writes; logs DB exceptions

### Blazor
- Most pages: `@rendermode InteractiveServer`
- Gate kiosk: `KioskLayout`, `prerender: false`
- Auth: `[Authorize]` / `[Authorize(Roles = "...")]`

---

## 12. Biometric & offline behaviour

### What works without internet
- Login, students, attendance, SQL Server — all local
- Gate **fingerprint** (WebAuthn) → local DB
- Simulated scans on Live Gate

### What needs local network (not internet)
- Blazor Server requires browser ↔ app server connection (e.g. `http://localhost:5258` or LAN IP)

### What may need internet (first visit)
- **face-api.js** and ML models load from `cdn.jsdelivr.net` (`App.razor`, `local-biometric.js`)
- After browser cache, face may work offline on that machine
- **Future improvement:** bundle models under `wwwroot/models/` for 100% offline face

### Real ZKTeco integration (not done)

Replace or extend:

- `src/SMS.Infrastructure/Biometric/SimulatedBiometricConnector.cs`
- `src/SMS.Infrastructure/Biometric/BiometricWorker.cs`

On each hardware scan, call:

```csharp
await attendanceService.ProcessBiometricScanAsync(biometricUserId, deviceId, direction);
```

---

## 13. Running tests

```bash
cd SMS
dotnet test
```

**34 integration tests** in `tests/SMS.Tests/Integration/AppFeatureTests.cs` covering:

- Login / auth
- School settings, classes, students, promotion
- Attendance manual, biometric, finalize
- Local biometric face enroll/match
- Holidays, backup listing

Tests use in-memory or test DB factory — see `SMS.Tests` project for setup.

---

## 14. Troubleshooting

| Problem | Fix |
|---------|-----|
| Build fails: file locked by SMS.Web | Stop `dotnet run` before build/migrate |
| Cannot connect to SQL | Check Docker `docker compose ps` or SQL Express service |
| 2nd student save fails | Was duplicate empty `StudentCode` — now auto-generated; check Error Logs |
| Gate face not working offline | Open gate once with internet to cache models, or bundle models locally |
| Print register shows URL footer | Use **Download PDF** or disable “Headers and footers” in browser print |
| WhatsApp notifications | Opens `wa.me` links only — no WhatsApp Business API configured |

---

## 15. Configuration reference

| Setting | File | Notes |
|---------|------|-------|
| Connection string | `appsettings.json` | SQL Server |
| App branding | `appsettings.json` → `AppBranding` | Company name, logo paths |
| DB backup path | `DatabaseBackup` section | Local + SQL Server backup folder |
| School timings | UI → School Settings | Start, end, late grace, weekly off |

---

## 16. Suggested next work (backlog)

1. **ZKTeco SDK** — wire real device in `BiometricWorker`
2. **Offline face models** — host face-api models in `wwwroot`
3. **Parent portal** — explicitly out of scope for now
4. **Fees / payroll / accounts** — new modules per schema pattern
5. **Coordinator/teacher tests** — expand RBAC test coverage
6. **PWA / offline Blazor** — would require architecture change (currently Server-only)

---

## 17. Handover checklist for new developer

- [ ] Clone repo, install .NET 8 SDK
- [ ] Start SQL (`docker compose up -d` or local SQL)
- [ ] Update `appsettings.json` connection string
- [ ] `dotnet ef database update` + `dotnet run --project src/SMS.Web`
- [ ] Log in as Admin, Coordinator, Teacher — verify nav differences
- [ ] Add 2 students (blank student code) — should succeed
- [ ] Mark attendance, finalize daily, check notifications page
- [ ] Download monthly register PDF and CSV
- [ ] Open gate kiosk, test fingerprint or local-test enroll
- [ ] Run `dotnet test` — expect 34 passed
- [ ] Read `BiometricWorker.cs` and `UserAccessService.cs` before changing RBAC or hardware

---

## 18. Contact & support workflow

When a school reports a bug:

1. Reproduce on `/settings/exception-logs` — find log ID from error toast
2. Copy full report and send to developer
3. Check constraint name in report for duplicate data issues

---

*Last updated: June 2026 — reflects Coordinator role, notifications, academic years, exception logs, register PDF/CSV, and local biometric gate.*

**Using Cursor AI:** See [CURSOR-AI-GUIDE.md](CURSOR-AI-GUIDE.md). Chat history is not in this repo; new developers open the project in Cursor and reference these docs.
