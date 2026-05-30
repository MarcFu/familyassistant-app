# FamilyAssistant — Feature List

Dev-facing overview of implemented and planned features.
Status: `done` | `partial` | `planned`

---

## Task / Chore Management

| Feature | Status | Description |
|---------|--------|-------------|
| Chore Definitions | done | Name, description, icon (Material/emoji), color, credit reward |
| Flexible Scheduling | done | Daily, SpecificDays, Weekly, XTimesPerWeek, Monthly, EveryXWeeks, EveryXMonths, Permanent |
| Multi-per-day | done | Multiple occurrences per day with time labels (e.g. "morgens", "abends") |
| Auto Task Generation | done | Background service creates concrete tasks from schedules (hourly) |
| Task Lifecycle | done | Open -> Claimed -> PendingConfirmation -> Confirmed / Missed / Cancelled |
| Ad-hoc Tasks | done | One-off tasks not tied to a schedule |
| Task Comments | done | Threaded text comments on tasks, per-person |
| Task Attachments | done | Image attachments (camera + gallery), resized to 2048px JPEG Q85, stored on disk |
| Attachment Retention | done | Auto-delete attachments X days after task completion (admin-configurable, default 30d) |
| Backup / Restore | done | Admin ZIP backup with SQLite online copy + attachments; local backup list with download/delete; restore validation checks migration compatibility and attachment image references; restore is applied safely on restart |

## Credits & Rewards

| Feature | Status | Description |
|---------|--------|-------------|
| Credit Earning | done | Credits awarded on confirmed task completion |
| Credit Spending | done | Spend credits on extra internet time |
| Manual Adjustments | done | Parents can give bonuses or penalties |
| Balance Tracking | done | Per-person balance + full transaction history |
| Missed Credits Log | done | Accountability log for missed tasks |

## Internet Access Control

| Feature | Status | Description |
|---------|--------|-------------|
| Internet Rules | done | Time-window or daily-budget rules per person/category (Gaming, Messenger, Full) |
| Day-of-Week Rules | done | Rules apply on configurable weekdays/weekend/all |
| Credit Extensions | done | Buy extra minutes with credits (configurable cost) |
| Device Entities | done | Per-person HA switch entities for controlling access |

## Home Assistant Integration

| Feature | Status | Description |
|---------|--------|-------------|
| REST API Proxy | done | All HA communication via `IHomeAssistantService` (entity states, service calls, image proxy) |
| WebSocket Connection | done | Persistent WS with auth, message loop, auto-reconnect |
| Person Sync | done | Import person entities + entity pictures from HA |
| Todo List Sync | done | Push open tasks to HA todo lists per person (every 5 min) |
| Credit Sensor Sync | done | Push credit balances as HA sensor entities |
| Theme Sync | done | Read dark/light mode from HA frontend user data via WebSocket |
| State Subscriptions | done | Subscribe to entity `state_changed` events with entity filtering |
| Event Triggers | done | Configurable sensor -> auto-task triggers with debouncing |
| Add-on Mode | done | Auto-detects `SUPERVISOR_TOKEN` env for seamless HA Add-on deployment |

## AI Icon Generation

| Feature | Status | Description |
|---------|--------|-------------|
| Ollama Integration | done | Local LLM suggests Material Design icons or emojis for chores |
| Model Tier Adaptation | done | Adjusts prompt complexity by model size (Small/Medium/Large, regex-detected) |
| Keyword Fallback | done | Deterministic icon mapping when Ollama unavailable/times out |
| Icon Picker | done | Searchable icon browser (Material Design + emoji) with AI tab |

## UI / Theming

| Feature | Status | Description |
|---------|--------|-------------|
| Dark Mode (default) | done | Full MudBlazor dark palette (HA-style colors) |
| Light Mode | done | PaletteLight defined, reactive to HA theme preference |
| HA-style Cards | done | Outlined cards (no elevation), pill buttons, FAB pattern |
| HA-style Pickers | done | Searchable outlined picker dialogs for Chores, HA entities, trigger entities, and larger device multi-selects |
| Mobile Layout | done | Responsive, direct camera access on mobile (`capture="environment"`) |
| Onboarding / Setup | done | First-run setup page: HA connection check, person import, role assignment, first chore |

## User Management

| Feature | Status | Description |
|---------|--------|-------------|
| Roles | done | Admin, Parent, Child, Guest with permission differentiation |
| User Context | done | Per-circuit scoped session (resolved from HA or dev page) |
| Person Pause | done | Vacation mode — skips task generation while paused |

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/ha-image?path=...` | GET | Proxies HA images with auth header (1h cache) |
| `/api/attachment/{id}` | GET | Serves task attachments from disk (24h cache) |
| `/api/upload-attachment` | POST | Direct file upload (multipart, bypasses SignalR) |
| `/api/backup/list` | GET | Lists locally stored backup ZIPs |
| `/api/backup/file/{fileName}` | GET | Downloads a locally stored backup ZIP |
| `/api/backup/file/{fileName}` | DELETE | Deletes a locally stored backup ZIP |
| `/api/backup/restore` | POST | Uploads and validates ZIP backup; schedules restore for next restart |
| `/api/debug/simulate-state-change` | POST | Simulate HA state_changed event for testing |

## Background Services

| Service | Interval | Purpose |
|---------|----------|---------|
| `TaskGenerationService` | 1 hour | Generate task instances from active schedules |
| `HomeAssistantSyncService` | 5 min | Sync credits + tasks to HA (sensors, todo lists) |
| `HaWebSocketStartupService` | startup | Establish persistent WebSocket, read initial theme |
| `HaEventTriggerService` | 2 min sync | Subscribe to entity changes, debounce, create ad-hoc tasks |
| `AttachmentCleanupService` | 1/day | Delete expired attachments based on retention setting |

## Infrastructure

| Feature | Status | Description |
|---------|--------|-------------|
| SQLite DB | done | EF Core with migrations, persistent at `/data/familyassistant.db` |
| Backup / Restore | done | ZIP backup format with manifest, local backup storage, startup restore, and pre-restore safety backup |
| Docker / Add-on | done | Dockerfile with `/data` volume, `config.yaml` for HA Add-on |
| HA Entity State Cache | done | 60s `IMemoryCache` for `/api/states` used by entity pickers to reduce HA load and picker latency |
| Dev Page | done | User switching, testing tools |
| Settings Page | done | Admin configuration UI (Ollama, HA, schedules) |
| System Status | done | HA connection status (REST + WebSocket) in Settings > System tab |

## Review / Open Questions

| Topic | Notes |
|-------|-------|
| HA Settings Real-World Flow | Dev: User Secrets. Add-on: Supervisor Token via env. Diskutieren: UI-basierte Konfiguration? Auto-Discovery? Token-Rotation? |
| Mobile File Upload | Gelöst via JS-managed file picker + HTTP POST. InputFile/SignalR bypassed. |
| Event Trigger E2E Test | Braucht echte HA-Instanz mit WebSocket. Debug-Endpoint `/api/debug/simulate-state-change` vorhanden (nur in Development). |

## Agenda (geplante Verbesserungen)

| Topic | Priority | Notes |
|-------|----------|-------|
| i18n / Lokalisierung | High | Alle UI-Strings sind hardcoded deutsch. IStringLocalizer + .resx einführen. |
| CI/CD Pipeline | Medium | GitHub Actions: build + test auf PRs. Optional: Docker-Image publish. |
| Auto-Migrate Fehler-UX | Low | Hilfreiche Fehlermeldung wenn DB-Migration fehlschlagt statt App-Crash. |
