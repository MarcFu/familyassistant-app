## 0.1.4

- Fix: `dev_mode` add-on option was not read (snake_case JSON key mismatch)
- Fix: App language now correctly synced from Home Assistant frontend settings
- Dev page: new "HA Configuration" diagnostics table showing all values read from HA (language, theme, colors, dev_mode, timestamps)

## 0.1.3

- Fix: Ingress routing 404 — all NavigateTo/Href converted to relative paths
- Fix: Avatar images not loading behind Ingress (relative API paths)
- Fix: User detection via X-Remote-User-Id header (official HA Ingress mechanism)
- New: HA User Mapping on Dev page for existing databases
- New: `dev_mode` add-on config option controls Dev page visibility

## 0.1.0

- Initial release
- Chore tracking with flexible schedules (daily, weekly, monthly, interval, specific days)
- Credit/reward system with HA sensor sync
- Task generation + missed-task handling
- Person management synced from Home Assistant
- Internet rules enforcement per device
- Event triggers (HA sensor → auto-create task)
- Backup & restore with validation
- Icon picker (Material, Emoji, Ollama AI-generated SVG)
- Multi-language support (de/en/fr)
- Mobile-responsive Material Design 3 UI (HA-style)
