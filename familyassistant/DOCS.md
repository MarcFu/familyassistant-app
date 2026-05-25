# FamilyAssistant

Household management app for Home Assistant — designed for families with kids.

## Features

- **Chore Schedules**: Daily, weekly, monthly, interval-based, or specific weekdays
- **Credit System**: Kids earn credits for completed chores, synced as HA sensors
- **Task Confirmation**: Parents confirm task completion before credits are awarded
- **Internet Rules**: Manage device access based on chore completion
- **Backup & Restore**: Full backup including database and photo attachments
- **Multi-Language**: German, English, French

## How it works

1. Install the app from the Home Assistant App Store
2. Open FamilyAssistant from the sidebar
3. Set up family members (auto-discovered from HA `person` entities)
4. Create chores with schedules
5. Kids mark tasks as done, parents confirm

## Data Storage

All data is stored in `/data/` (mapped HA data volume):
- `familyassistant.db` — SQLite database
- `attachments/` — Photo attachments from task comments
- `backups/` — Server-side backup archives

## Configuration

No manual configuration required. The app automatically connects to Home Assistant via the Supervisor API.
