# SMS — School Deployment Guide

Single reference for installing SMS at a school: **Windows server + SQL Express + IIS + phone gate**.

The mobile phone is **not** where the app lives — it is only a browser for the gate camera. The app and database run on one **school PC or server**.

---

## 1. Architecture

```text
[School Windows PC / Server]
   ├── IIS  →  SMS.Web (published .NET 8 app)
   └── SQL Express  →  SMS database

[Admin laptop]     →  https://school-server/  (students, settings, enroll)
[Gate phone/tablet] →  https://school-server/attendance/gate
```

| Component | Where it runs |
|-----------|----------------|
| Application + database | School Windows PC |
| Gate camera / face scan | Phone browser (same Wi‑Fi as server) |
| Internet | Optional (face models cache from CDN on first use) |

---

## 2. Prerequisites

Install on the **school server**:

| Requirement | Notes |
|-------------|--------|
| **Windows 10/11 or Windows Server** | Recommended |
| **.NET 8 Hosting Bundle** | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) — includes ASP.NET Core runtime for IIS |
| **SQL Server Express** | Free; install on same PC as the app |
| **IIS** | Web Server role + ASP.NET Core module (from Hosting Bundle) |
| **Same Wi‑Fi** | Gate phone must reach the server IP |

---

## 3. SQL Server Express

### 3.1 Install

1. Download **SQL Server Express** and **SSMS** (optional, for backups).
2. During setup:
   - Instance name: often `SQLEXPRESS`
   - Authentication: **Mixed Mode** (SQL login) **or** Windows Authentication
3. Ensure the **SQL Server service** is running (Services → `SQL Server (SQLEXPRESS)`).

### 3.2 Connection string

Edit `appsettings.json` in the published app folder (e.g. `C:\inetpub\sms\appsettings.json`).

**Windows Authentication (app and SQL on same PC):**

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SMS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

**SQL login:**

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SMS;User Id=sms_app;Password=YOUR_STRONG_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

Create the login in SSMS if using SQL auth. Do **not** expose SQL port 1433 to the internet.

### 3.3 SQL Express limits

- Database up to ~**10 GB** — enough for a typical single school for many years.
- For very large multi-campus setups, consider full SQL Server later.

### 3.4 Migrations (automatic)

**You do not need to run migrations manually on the school PC.**

On every app start, `DatabaseSeeder` runs:

1. `MigrateAsync()` — applies any pending EF migrations  
2. Seeds demo school structure (classes, devices) if the database is empty  
3. Creates default **Admin** and **Coordinator** accounts if missing  

Optional for developers only:

```powershell
dotnet ef database update --project src/SMS.Infrastructure --startup-project src/SMS.Web
```

---

## 4. Publish the application

On a build machine (or the server if SDK is installed):

```powershell
cd C:\path\to\SMS
dotnet publish src/SMS.Web -c Release -o C:\inetpub\sms
```

Copy the entire `C:\inetpub\sms` folder to the school server if built elsewhere.

Update `appsettings.json` (and `appsettings.Production.json` if used) with the production connection string **before** going live.

---

## 5. IIS setup

### 5.1 Install hosting bundle

Install **.NET 8 Hosting Bundle**, then restart IIS:

```powershell
iisreset
```

### 5.2 Create site

1. Open **IIS Manager**.
2. **Application Pools** → Add:
   - Name: `SMS`
   - .NET CLR version: **No Managed Code**
   - Identity: ApplicationPoolIdentity (or a dedicated service account if using Windows SQL auth)
3. **Sites** → Add:
   - Site name: `SMS`
   - Physical path: `C:\inetpub\sms`
   - Binding: **https**, port **443** (and/or http 80 for redirect)
   - Application pool: `SMS`

### 5.3 Folder permissions

Grant the app pool read/execute on `C:\inetpub\sms` and write access to:

- `App_Data\` (backups, uploads)
- Any folder used for student photos if configured

### 5.4 Ports — IIS vs development

| Mode | Ports | Config file |
|------|-------|-------------|
| **Development** (`dotnet run`) | HTTP **5258**, HTTPS **7258** | `launchSettings.json` — **not used after publish** |
| **IIS (production)** | HTTP **80**, HTTPS **443** (standard) | IIS site **Bindings** in IIS Manager |

**After publish you do not set 5258 or 7258.** Those are only for local `dotnet run`.

In IIS Manager → your site → **Bindings**:

| Type | Port | Use |
|------|------|-----|
| `https` | **443** | Main URL — admin + gate phone (camera works) |
| `http` | **80** | Optional — redirect users to HTTPS |

Example URLs on the school network:

```text
https://192.168.1.10/
https://192.168.1.10/attendance/gate
```

No `:7258` in the URL when using IIS on port 443.

**Custom port (optional):** you may bind HTTPS to e.g. **8443** if 443 is in use — then use `https://192.168.1.10:8443/attendance/gate` and open that port in the firewall. You still do **not** need 5258/7258.

**Config files to check after publish:**

| File | What to set |
|------|-------------|
| `appsettings.json` | SQL `DefaultConnection` only |
| `web.config` | Auto-created by `dotnet publish` — usually no edits |
| `launchSettings.json` | **Not deployed** — ignore for IIS |

Optional environment variable on the server (IIS site → Configuration Editor or system env):

```text
ASPNETCORE_ENVIRONMENT=Production
```

### 5.5 HTTPS (required for gate camera on phones)

Browsers **block the camera on HTTP** from phones. Use **HTTPS**.

| Environment | Certificate |
|-------------|-------------|
| Testing on LAN | Dev/self-signed cert — phone: Advanced → Proceed |
| Production | Proper certificate (internal CA or trusted cert for school IP/name) |

Bind HTTPS in IIS (port **443**). Gate URL example:

```text
https://192.168.1.10/attendance/gate
```

### 5.6 Windows Firewall

Allow inbound on the school Wi‑Fi profile:

- **443** (HTTPS) — required for admin + gate phones  
- **80** (HTTP) — optional, redirect to HTTPS  

SQL Server: keep **localhost only** unless SQL is on another machine on LAN.

---

## 6. First login and accounts

### 6.1 Default accounts (created once at first startup)

| Role | Email | Initial password |
|------|-------|------------------|
| **Admin** | `admin@school.local` | `Admin@123` |
| **Coordinator** | `coordinator@school.local` | `Coordinator@123` |

Passwords are **not** shown on the login page. These are for **first install only**.

**Teachers are not created automatically.** Admin creates them under **Settings → User Accounts**.

### 6.2 After first login (mandatory)

1. Sign in as **Admin**.
2. **Settings → User Accounts** → change Admin and Coordinator passwords.
3. Create **teacher** accounts.
4. **Settings → Staff Assignment** → assign teachers to sections.
5. **Settings → School Settings** → school name, start time, grace minutes.

---

## 7. Attendance gate (mobile phone)

### 7.1 Enroll faces

1. On laptop or phone (HTTPS): **Attendance → Local Biometric Test** (admin only).
2. Select student → **Start Camera** → **Enroll Face** 2–3 times.
3. Confirm gate enrollment count increases.

**Best practice:** enroll on the **same phone** used at the gate.

### 7.2 Gate kiosk URL

On the gate phone (Chrome, same Wi‑Fi):

```text
https://YOUR-SERVER-IP/attendance/gate
```

Example: `https://192.168.1.10/attendance/gate`

1. Accept certificate warning if using self-signed cert.
2. **Allow camera** when prompted.
3. Log in (admin, coordinator, or teacher — gate allows all three roles).
4. Stand in front of camera — **green ✓** = attendance recorded.

Optional: **Add to Home Screen** (PWA) for full-screen kiosk.

### 7.3 Mobile — what is required

| Item | Required? |
|------|-----------|
| HTTPS | **Yes** — camera blocked on HTTP |
| Camera permission | **Yes** — tap Allow in browser |
| Same Wi‑Fi as server | **Yes** |
| App Store install | **No** — web/PWA only |
| SQL on phone | **No** |

### 7.4 Multiple gate devices (optional)

You may use **more than one phone** at the same time (e.g. front gate + back gate). All connect to the **same server URL** and **same database**. Enroll each student once; all gates share enrolled faces.

---

## 8. Admin password reset (emergency)

If the admin forgets the password, run on the **school server** (SQL must be running).

**From project folder (development or if repo is on server):**

```powershell
cd C:\path\to\SMS
.\scripts\reset-admin-password.ps1
```

**IIS published folder:**

```powershell
cd C:\path\to\SMS
.\scripts\reset-admin-password.ps1 -ConfigPath "C:\inetpub\sms"
```

**Defaults:** email `admin@school.local`, password `Admin@123`.

**Custom password:**

```powershell
.\scripts\reset-admin-password.ps1 -ConfigPath "C:\inetpub\sms" -Password "Admin@123"
```

**Without PowerShell script:**

```powershell
dotnet run --project tools/SMS.AdminReset -- --config C:\inetpub\sms
```

The tool resets the password, clears lockout, reactivates the account, or **creates** admin if missing. **Change the password again** after signing in.

---

## 9. Backup

1. **Settings → Database Backup** (admin) → Create Backup Now.
2. Download `.bak` files and store off-site (USB or another PC).
3. Run a backup before term-end, promotions, or major changes.

---

## 10. Go-live checklist

- [ ] SQL Express installed and service running  
- [ ] Connection string in `appsettings.json` (published folder)  
- [ ] App published to `C:\inetpub\sms` (or chosen path)  
- [ ] IIS site + app pool (.NET 8, No Managed Code)  
- [ ] HTTPS bound; firewall allows 443 on school Wi‑Fi  
- [ ] First start — app opens in browser  
- [ ] Admin + Coordinator passwords changed  
- [ ] Teachers created and assigned to sections  
- [ ] Students and classes entered  
- [ ] Faces enrolled (Local Biometric Test)  
- [ ] Gate tested from phone on Wi‑Fi (`/attendance/gate`)  
- [ ] Backup tested once  

---

## 11. Troubleshooting

| Problem | What to check |
|---------|----------------|
| Cannot open site from phone | Same Wi‑Fi? Correct IP? Firewall 443 open? |
| Camera blocked | Use **https://** not http:// |
| Face not recognized | Re-enroll on **same phone** as gate; good lighting |
| Login fails | SQL running? Connection string correct? |
| Admin locked out | Run `scripts\reset-admin-password.ps1` |
| App won't start after publish | .NET 8 Hosting Bundle installed? `iisreset` |
| Build errors on dev PC | Stop running `SMS.Web` before `dotnet build` |

In-app help for staff: **`/help`** (after login).

Technical/developer details: **`docs/HANDOVER.md`**.

---

## 12. Do not do this

- Do **not** copy only “built files” to the phone — the phone cannot host the app or SQL.  
- Do **not** use `dotnet run` for daily school use — use IIS.  
- Do **not** leave default passwords (`Admin@123`) in production.  
- Do **not** expose SQL Server to the public internet.

---

*ARTechnologies — SMS School Management System*
