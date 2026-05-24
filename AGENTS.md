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
