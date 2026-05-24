# Projekt-Regeln für KI-Assistenten

## Bug-Tracking

- Bugs werden fortlaufend nummeriert: `BUG-001`, `BUG-002`, ...
- **Commit-Messages:** `fix(BUG-003): Kurzbeschreibung`
- **Code-Kommentar** am Fix (wenn nicht offensichtlich): `// BUG-003: Erklärung`
- Nummerierung wird projektübergreifend fortgesetzt, nie zurückgesetzt.

## Datenbank & Enums

- **Enums, die in der Datenbank als Integer gespeichert werden, MÜSSEN explizite numerische Werte haben.**
- Neue Enum-Werte IMMER am Ende anhängen, NIE zwischen bestehenden Werten einfügen.
- Grund: Ein Einfügen verschiebt die Werte aller nachfolgenden Einträge und korrumpiert bestehende DB-Daten still und leise.

## EF Core Migrations

- Nach Model-Änderungen immer eine Migration erstellen (`dotnet ef migrations add ...`).
- Die App muss ggf. vorher gestoppt werden (File-Lock auf .exe).

## Build

- Die App läuft oft im Hintergrund. Build-Fehler vom Typ MSB3027 (File-Lock) sind keine Code-Fehler.
- Bei echtem Build-Test: App vorher stoppen (`Stop-Process -Name "HassCompanion"`).

## UI-Pattern: Outlined Cards (Home Assistant Style)

- **Einzelne Items in Listen/Grids:** `<MudPaper Outlined="true" Class="pa-3 mb-2">`
- **Dashboard-Kacheln:** `<MudPaper Outlined="true">` in einem `MudGrid`
- **Kein Elevation/Schatten** — HA nutzt seit 2022.11 outlined statt shadows (Material Design 3).
- **Admin-Tabellen** (viele Spalten, dichte Daten) bleiben als `MudTable`.
- **CSS-Basis:** `border-radius: 12px`, `border-color: rgba(255,255,255,0.12)`
- **Buttons:** Pill-shaped (`border-radius: 9999px`), primäre Aktionen mit Label, sekundäre/destruktive als Icon-only mit Tooltip.
- **FAB (Floating Action Button):** Immer `position: fixed; bottom: 24px; right: 24px; z-index: 100;`, `Size.Large`, mit `MudTooltip`. Nie inline in einer Toolbar.
- **Farben:** Primary `#03a9f4`, Secondary `#ff9800`, Surface `#1c1c1c`, Background `#121212`.

## Architektur: HA-Proxy

- **Alle** Kommunikation mit Home Assistant geht durch `IHomeAssistantService`.
- REST + WebSocket werden intern vom selben Service verwaltet.
- Weitere Services (Theme, Events) bauen **auf** dem Proxy auf, erstellen keine eigenen HA-Verbindungen.
- Bild-/Datei-Serving an den Client läuft über dedizierte API-Endpoints (Proxy-Pattern):
  - `/api/ha-image` — HA-Bilder (Entity-Pictures)
  - `/api/attachment/{id}` — Task-Kommentar-Bilder von Disk

## Geplant: Foto-Retention

- **Setting:** `AttachmentRetentionDays` (AppSettings Key-Value, Default: 30)
- **Logik:** Bilder (TaskAttachment) werden X Tage nach Task-Abschluss (Confirmed/Missed/Cancelled) gelöscht.
- **Text-Kommentare bleiben** (kein Disk-Platz, historisch relevant).
- **Offene Tasks** behalten ihre Bilder bis Abschluss + Frist.
- **Background-Job:** `AttachmentCleanupService`, läuft 1×/Tag.
- **Settings-UI:** Admin-Only NumericField, Min 7, Max 365.

## Geplant: HA WebSocket (im Proxy)

- `IHomeAssistantService` erweitert um: `ConnectWebSocketAsync`, `GetUserDarkModeAsync`, `SubscribeStateChangedAsync`, `UnsubscribeAsync`.
- `HomeAssistantService` wird Singleton (hält langlebigen WebSocket).
- URL: `BaseUrl.Replace("http","ws") + "/api/websocket"`, Auth mit bestehendem Token.
- Auto-Reconnect bei Disconnect.

## Geplant: Light/Dark Mode

- `HaThemeService` liest Theme via WebSocket (`frontend/get_user_data`).
- `MainLayout.razor` bindet `IsDarkMode` an den Service.
- Fallback: Dark bis WebSocket antwortet.

## Geplant: Event-Trigger (Sensor → Auto-Task)

- Konfigurierbar: Entity + Trigger-Zustand + Ziel-Chore.
- `HaEventTriggerService` subscribt via WebSocket auf `state_changed`.
- Debouncing gegen State-Flatter.
