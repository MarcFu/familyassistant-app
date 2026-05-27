## 0.1.7

- **Breaking change**: User preferences (language, theme, colors) now read directly from HA config storage files instead of WebSocket API
- New: `config:ro` mount — add-on reads `/config/.storage/frontend.user_data_{userId}` for per-user settings
- New: Build timestamp shown on Dev page
- Dev page: shows file path, user ID, and "Raw File" button for preference diagnostics
- Removed: WebSocket-based theme/language refresh (never worked with SUPERVISOR_TOKEN)
- Simplified: `HaWebSocketStartupService` only manages WS connection (still needed for event triggers)

## 0.1.6

- Fix: HA language/theme not read — `frontend/get_user_data` response has a `value` wrapper that was not unwrapped
- Fix: dark mode parsing (false was not correctly detected)
- Dev page: shows raw HA WebSocket response for diagnostics
- Auto-refresh theme on every WebSocket (re)connect via `WebSocketConnected` event

## 0.1.5

- Dev page: added WebSocket connection status + Refresh Status detail
- Dev page: Refresh button now attempts WebSocket reconnect if disconnected
- Improved diagnostics: shows exact reason if HA theme data cannot be read

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
