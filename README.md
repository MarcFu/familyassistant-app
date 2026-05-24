# FamilyAssist

Home Assistant Add-on for household management — chore tracking, credit system, and HA entity sync.

Built with C# Blazor Server (.NET 9), MudBlazor v9, EF Core + SQLite.

## Features

### Chore Management
- Create and manage household chores with credit rewards
- **Schedule types:**
  - Daily, Specific Days, X times per week, Weekly, Monthly
  - **Permanent** — always available (e.g., dishes). When completed, immediately respawns
- Auto-generated icons via keyword matching or local Ollama (Material Design icons or emoji)
- Search and filter on chores list

### Task Workflow
- Tasks are auto-generated from schedules (4 weeks ahead, hourly + on page load)
- **Claim → Complete → Confirm** workflow
- Parents/Admins can complete without confirmation
- Children need parent confirmation before credits are awarded
- Permanent tasks: always show in "Today" view, never marked as missed
- **Ad-hoc tasks:** One-off tasks created manually via "Ad-hoc" button on Tasks page
  - Intent-based creation: "Für mich" (auto-claimed), "Für jemand anderen" (assigned), "Offen für alle" (unassigned)
  - Can use existing chore templates or define a custom name + credits
  - Role-based defaults: Parents default to assigning, Children default to self-claim

### Credit System
- Credits awarded on task confirmation
- Per-person credit balance tracked
- Full transaction history
- Missed credits audit log (for Taschengeld negotiations)

### Home Assistant Integration
- Import persons from HA (with avatar images via proxy endpoint)
- Sync credits to HA sensors (`sensor.familyassist_<name>_credits`)
- Sync open tasks to HA todo lists (configurable per person)
- Auto-detects Add-on mode (SUPERVISOR_TOKEN) vs dev mode (Long-Lived Token)

### Persons & Roles
- **Admin** — full access
- **Parent** — can confirm children's tasks
- **Child** — needs parent confirmation
- Pause toggle (paused persons don't get task assignments or missed-credits shame entries)

## Architecture

```
src/FamilyAssist/
├── Components/
│   ├── Pages/          # Blazor pages (Home, Chores, Tasks, Settings)
│   ├── Dialogs/        # AdHocTaskDialog, ChoreDialog, ScheduleDialog, IconPickerDialog, PersonEntityDialog
│   └── Layout/         # MainLayout with MudBlazor dark theme
├── Data/
│   └── AppDbContext.cs # EF Core context
├── Models/             # Person, Chore, ChoreSchedule, ChoreTask, etc.
├── Services/
│   ├── HomeAssistantService.cs    # HA REST API client
│   ├── HomeAssistantSyncService.cs # Background: sync to HA
│   ├── TaskGenerator.cs           # Core task generation logic (scoped)
│   ├── TaskGenerationService.cs   # Background: hourly trigger
│   ├── ChoreIconGenerator.cs      # Icon generation facade
│   ├── KeywordIconMapper.cs       # German keywords → Material icons
│   └── OllamaIconService.cs       # Local Ollama for AI-based icons
└── Migrations/         # EF Core migrations
```

## Configuration

### Development (User Secrets)
```bash
cd src/FamilyAssist
dotnet user-secrets set "HomeAssistant:BaseUrl" "http://your-ha:8123"
dotnet user-secrets set "HomeAssistant:Token" "your-long-lived-access-token"
```

### Production (Add-on mode)
Uses `SUPERVISOR_TOKEN` environment variable automatically. No config needed.

### Icon Generation (Settings → Icons tab)
| Mode | Description |
|------|-------------|
| Keyword | Offline mapping of German chore words to Material icons (fast, default) |
| Ollama Material | Local Ollama picks a Material Design icon name |
| Ollama Emoji | Local Ollama picks an emoji |

Ollama URL default: `http://localhost:11434`, Model default: `llama3.2:1b`

## Running

### Development
```bash
cd src/FamilyAssist
dotnet run
# or
dotnet watch
```
Opens at `http://localhost:5115`

### Docker / HA Add-on
```bash
docker build -t familyassist .
docker run -p 8099:8099 -e SUPERVISOR_TOKEN=... familyassist
```

## Database

- SQLite at `./data/familyassist.db` (dev) or `/data/familyassist.db` (production)
- Managed via EF Core Migrations (`dotnet ef migrations add <Name>`)
- On startup: `MigrateAsync()` applies pending migrations automatically

## Design Decisions

- **MudBlazor v9.4.0**: `MudAvatar` has no `Image` property — use `<img>` as ChildContent
- **Searchfields**: Always use `Clearable="true"` for X-to-clear UX
- **Permanent tasks**: Never expire, never "missed", always 1 open instance
- **Task generation is idempotent**: Safe to trigger multiple times (checks uniqueness by ScheduleId + DueDate + OccurrenceIndex)
- **Credits sync to HA**: Background service updates HA sensor entities periodically
- **Ad-hoc intent switch**: 3 options (ForMyself / ForSomeone / OpenForAll). REVIEWPOINT: Evaluate after usage whether "OpenForAll" adds value or if 2 options suffice

## Planned Features

- [ ] Icon picker with multiple options (generate → choose from suggestions)
- [ ] Event-triggered tasks (HA entity state change → auto-create task, e.g., washing machine done → "Wäsche aufhängen")
- [ ] Internet access rules (per-person device entity switches, time windows, credit spending)
- [ ] UniFi integration for network blocking
