# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- HA-style searchable picker dialogs for Chores, Home Assistant entities, trigger entities, and larger device multi-selects
- 60s Home Assistant entity state cache for picker data to reduce repeated `/api/states` calls
- Per-person notify entity test button in the person links dialog

### Fixed
- BUG-006: Reduced Home Assistant CPU spikes by skipping unchanged REST state writes, repeated todo item adds, and redundant switch service calls
- BUG-007: Fixed mobile picker row wrapping and Sunday-based week calculations for task filters, dashboard stats, and X-times-per-week scheduling
- BUG-008: Fixed production circuit crashes when opening the trigger entity picker after adding the initial HA entity render limit

## [0.1.7] - 2025-05-29

### Added
- Achievement system: 40+ badges with honeycomb board, tier system (Base/Bronze/Silver/Gold/Legendary), category-based colors
- Achievement persistence: `PersonAchievement` DB table, unlock evaluation, reveal tracking
- Achievement unlock animation overlay (fullscreen, spring easing, glow, queue)
- Pixel-perfect responsive honeycomb board (measures actual container width via JS, loading skeleton until ready)
- Chore soft-delete: archiving instead of permanent deletion, `IsDeleted` flag with cascading filter
- Confirm dialog component for destructive actions
- Task unclaim button (return claimed task to Open)
- "Bald fällig" warning chip on tasks approaching deadline (≥80% of lifetime elapsed)
- Triggered task timestamp display (HH:mm next to chore name)
- Task sorting: own claimed first, then status priority, then newest

### Changed
- AchievementBoard uses one-shot JS measurement (Pull pattern) instead of callback-based ResizeObserver (Push) — eliminates Blazor Server lifecycle timing issues
- Dev page uses AchievementBoard component instead of inline board with duplicated logic
- Cleaned up theme-interop.js (removed broken elementResize block)

### Fixed
- JS interop error "No interop methods are registered for renderer 1" on Achievement Badges expansion panel

### Changed
- Renamed project from HassCompanion to FamilyAssistant
- Debug endpoint `/api/debug/simulate-state-change` now only available in Development environment
- Upload endpoint now validates task/comment existence, content types, and sanitizes filenames
- Image proxy endpoint now validates path parameter (must start with `/api/`)

### Added
- MIT License
- Onboarding setup page (`/setup`) — HA connection check, person import with role assignment, first chore creation
- Admin backup/restore UI with local ZIP backup list, download/delete actions, and ZIP import for SQLite database and task attachments
- Backup API endpoints for listing, downloading, deleting, and restoring local backup ZIPs
- Startup restore flow that applies prepared backups before EF Core migration and keeps a pre-restore safety backup
- Automatic redirect to setup on first start (0 persons in DB)

### Fixed
- BUG-001: Temporary SQLite backup files remained locked on Windows during ZIP creation
- BUG-002: Direct backup download could create repeated backup ZIPs when the browser/proxy retried the GET download
- BUG-003: Removed direct create-and-download backup flow and reject duplicate backup creation events
- BUG-004: Event trigger race condition — concurrent state_changed callbacks could create duplicate tasks (CAS debounce fix)
- BUG-005: NavigationException + JS interop error on first page load during SSR prerender (MudTabs fires ActivePanelIndexChanged during disposal)

### Security
- Restore validation now checks migration compatibility on a temporary database copy, rejects unknown migrations, verifies attachment DB references, rejects orphan/missing attachments, and verifies attachment files are valid images
- Test project with 36 unit tests (TaskGenerator, validation rules)
- CHANGELOG.md
- CONTRIBUTING.md
- `.dockerignore` for faster Docker builds
- Dockerfile hardening (non-root user, healthcheck, pinned image tags, Alpine)

### Security
- Server-side validation on all API endpoints (prevents SSRF, path traversal, invalid uploads)
- Debug endpoints gated behind `IsDevelopment()` check

## [0.1.0] - 2025-05-24

### Added
- Chore management with flexible scheduling (Daily, SpecificDays, Weekly, XTimesPerWeek, Monthly, EveryXMonths, EveryXWeeks, Permanent)
- Multi-per-day task support with time labels
- Automatic task generation from schedules (background service, hourly)
- Task lifecycle: Open -> Claimed -> PendingConfirmation -> Confirmed / Missed / Cancelled
- Ad-hoc tasks (one-off, intent-based creation)
- Credit system with earning, spending, manual adjustments, and balance tracking
- Missed credits accountability log
- Internet access rules (time-window, daily-budget, day-of-week, credit extensions)
- Home Assistant integration: REST API proxy, WebSocket connection, person sync, todo list sync, credit sensor sync, theme sync, state subscriptions, event triggers
- AI icon generation via Ollama (Material Design icons + emoji) with keyword fallback
- Searchable icon picker with MRU
- Task comments and image attachments (camera + gallery)
- Attachment retention (auto-cleanup after configurable days)
- Dark/light mode synced from HA frontend preferences
- HA Add-on mode with automatic SUPERVISOR_TOKEN detection
- Responsive mobile-first UI with MudBlazor (HA-style outlined cards)
- Person management with roles (Admin, Parent, Child, Guest)
- Vacation/pause mode for persons
- Settings page with system status, Ollama config, trigger management
- Dev page for testing and user switching
