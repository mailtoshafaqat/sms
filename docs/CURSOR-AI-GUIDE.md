# Using Cursor AI with SMS

This project was built with help from **Cursor’s AI assistant**. The full chat history is **not** stored inside this git folder — it lives in the original developer’s Cursor workspace. The next developer should use this guide plus `HANDOVER.md` and the code itself.

---

## What is saved in this repo

| Resource | Location | Use for |
|----------|----------|---------|
| Technical handover | `docs/HANDOVER.md` | Architecture, setup, routes, RBAC, testing |
| Quick start | `README.md` | Run app in 5 minutes |
| Source code | `src/`, `tests/` | Truth for behaviour |
| This guide | `docs/CURSOR-AI-GUIDE.md` | How to work with Cursor on SMS |

**Not in the repo:** Raw Cursor chat transcripts (unless someone exports them manually).

---

## How the next developer can get AI help

1. **Open this folder in Cursor**  
   `File → Open Folder` → select the `SMS` project root (where `SMS.slnx` is).

2. **Point the AI at the handover doc**  
   In chat, try:
   > Read `docs/HANDOVER.md` and help me run the app locally.

3. **Ask scoped questions**  
   Good prompts:
   - “Where is student save validation?”
   - “How do I add a new Admin settings page?”
   - “Why does gate kiosk need internet for face?”
   - “Add a test for Coordinator role on daily attendance.”

4. **Use @ mentions**  
   - `@docs/HANDOVER.md` — project context  
   - `@src/SMS.Application/Services/StudentService.cs` — specific file  
   - `@Codebase` — broad search (for larger tasks)

5. **Use Agent mode for changes**  
   For implementation (new page, bug fix, migration), use Cursor **Agent** so it can edit files and run `dotnet build` / `dotnet test`.

---

## Important decisions (from development — ask AI to respect these)

- **Local / offline first** — SQL Server on school PC or LAN; no parent portal.
- **Biometric** — Local test + gate kiosk only; **no real ZKTeco SDK yet**. Extend `BiometricWorker.cs` when hardware arrives.
- **WhatsApp** — `wa.me` link queue only; no WhatsApp Business API.
- **Roles** — Admin, Coordinator, Teacher; teachers see **assigned sections only**.
- **Student code** — Auto-generated if blank: `STU-{sectionId}-{rollNumber}`.
- **Errors** — Logged to `AppExceptionLogs`; Admin → Error Logs; copy report for support.
- **English UI only** — Urdu/RTL removed intentionally.
- **Do not commit secrets** — `appsettings.json` has dev SQL password; use `appsettings.Development.json` or env vars in production.

---

## Suggested first Cursor session for new dev

Copy this into Cursor chat:

```text
I'm the new developer on the SMS school management system (.NET 8 Blazor Server).

1. Read docs/HANDOVER.md and docs/CURSOR-AI-GUIDE.md
2. Help me verify: docker SQL, dotnet ef database update, dotnet run, dotnet test
3. Summarize anything outdated in HANDOVER.md vs the current code
```

---

## Exporting chat history (optional — original developer)

If the **original developer** wants to share Cursor conversations:

1. In Cursor chat, use **Export** / copy important threads into a file under `docs/` (e.g. `docs/chat-exports/`), **or**
2. Share the Cursor project link if the team uses shared Cursor workspace (org-dependent).

Do **not** commit passwords, connection strings with production credentials, or personal data in exports.

---

## If AI gives wrong answers

Always verify against:

```bash
dotnet build
dotnet test
```

And read the actual service/page code. `HANDOVER.md` should be updated when major features change.
