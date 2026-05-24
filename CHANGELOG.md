# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Renamed project from HassCompanion to FamilyAssist
- Debug endpoint `/api/debug/simulate-state-change` now only available in Development environment
- Upload endpoint now validates task/comment existence, content types, and sanitizes filenames
- Image proxy endpoint now validates path parameter (must start with `/api/`)

### Added
- MIT License
- Onboarding setup page (`/setup`) — HA connection check, person import with role assignment, first chore creation
- Automatic redirect to setup on first start (0 persons in DB)
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
