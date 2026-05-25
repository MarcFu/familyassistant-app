# Projekt-Regeln für KI-Assistenten

## Datei- und Ordnernamen

- **Lowercase** für Ordner- und Dateinamen verwenden, wo möglich und sinnvoll.
- Ausnahme: C#/.NET-Projektstruktur folgt PascalCase-Konvention (Namespaces = Ordnernamen, z.B. `Services/`, `Models/`).
- Für Config-Dateien, Scripts, Docker, YAML, Workflows etc. immer lowercase + kebab-case (z.B. `build.yaml`, `family-assistant/`).

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
- Bei echtem Build-Test: App vorher stoppen (`Stop-Process -Name "FamilyAssistant"`).

## API-Endpoints & Sicherheit

- **Debug-/Test-Endpoints** MÜSSEN in `if (app.Environment.IsDevelopment()) { ... }` eingeschlossen sein. Nie in Production verfügbar.
- **Antiforgery** darf nur disabled werden wenn technisch notwendig (z.B. JS-basierter Upload). Der Grund muss als Kommentar dokumentiert sein.
- Da die App ausschließlich als HA-Add-on betrieben wird (Ingress übernimmt Auth), gibt es keine eigene Authentifizierung. Diese Annahme in Kommentaren kennzeichnen.

## UI-Pattern: Outlined Cards (Home Assistant Style)

- **Einzelne Items in Listen/Grids:** `<MudPaper Outlined="true" Class="pa-3 mb-2">`
- **Dashboard-Kacheln:** `<MudPaper Outlined="true">` in einem `MudGrid`
- **Kein Elevation/Schatten** — HA nutzt seit 2022.11 outlined statt shadows (Material Design 3).
- **Admin-Tabellen** (viele Spalten, dichte Daten) bleiben als `MudTable`.
- **CSS-Basis:** `border-radius: 12px`, `border-color: rgba(255,255,255,0.12)`
- **Buttons:** Pill-shaped (`border-radius: 9999px`), primäre Aktionen mit Label, sekundäre/destruktive als Icon-only mit Tooltip.
- **FAB (Floating Action Button):** Nur auf **Listen-Seiten** (Tasks, Chores) für die primäre Create-Aktion. Immer als **Extended FAB** (Icon + Label), wie in HA üblich: `<MudFab StartIcon="..." Label="..." />`. Position: `position: fixed; bottom: 24px; right: 24px; z-index: 100;`, `Size.Large`. Kein `MudTooltip` nötig (Label ist selbsterklärend). Nie inline in einer Toolbar. **Nicht** auf Multi-Tab-Seiten (Settings) — dort normaler Button im Tab-Panel.
- **Farben:** Primary `#03a9f4`, Secondary `#ff9800`, Surface `#1c1c1c`, Background `#121212`.

## UI-Pattern: ResponsiveList (Standard für alle Listen)

- **Immer `Grouped="true"`** verwenden — eine Outlined Card umschließt Header + alle Items, Divider zwischen Items (HA "List Card" Pattern).
- **Grid-Spalten:** Feste `px` für Chips/Toggles/Icons, `minmax()` oder `fr` für variablen Text. **Nie `auto` für Content-Spalten** (verursacht ungleiche Ausrichtung). `auto` nur für die Actions-Spalte (identische Buttons pro Zeile).
- **Breakpoint:** Default `Md` (960px). Bei 7+ Spalten: `Breakpoint="Breakpoint.Lg"` (1280px) setzen.
- **Card-Mode (mobile):** Zeile 1 = `justify-space-between` → Identität links, Status-Badge rechts (nur bei Abweichung).

## UI-Pattern: Tabs & URL-Sync

- **Tabs auf Seiten-Ebene** (z.B. Settings, Admin, InternetRules) MÜSSEN im Query-String reflektiert werden (`?tab=log`).
- Beim Laden der Seite wird `?tab=…` geparst und der aktive Tab gesetzt. Beim Tab-Wechsel wird die URL aktualisiert (ohne Navigation/Reload).
- Tab-Werte sind lesbare Slugs (z.B. `correction`, `log`, `reset`), NICHT numerische Indizes.
- Pattern: `NavigationManager.NavigateTo($"?tab={slug}", replace: true, forceLoad: false)` + `ActivePanelIndexChanged` / `ActivePanelIndex`.
- **Dialoge** mit Tabs (z.B. IconPicker) brauchen KEINE URL-Sync.

## UI-Pattern: Filter

- **Dropdowns** verwenden `Value` + `ValueChanged` (reaktiv, sofortige Aktualisierung). Kein manueller Refresh-Button.
- **Textfelder** verwenden `DebouncedTextField` (400ms Verzögerung, Client-Side-Filterung auf bereits geladenen Daten).
- Filter-Zustand darf über Query-Parameter deep-linkbar sein (z.B. `?filter=Completed&person=3`).

## UI-Pattern: Editierbare Entitäten

- **Standard-Pattern:** Shared Dialog für Create UND Edit (gleiche Komponente).
  - Parameter: `Entity? Item` (null = neu) + `bool IsNew`.
  - Dialog-Titel + Button-Text passen sich an: "Erstellen" vs. "Speichern".
  - Beispiele: `ChoreDialog`, `ScheduleDialog`, `TriggerDialog`.
- **Einfache Felder** (Rolle, Active-Toggle): Dürfen inline in der Tabelle editierbar sein.
- **Tabellen-Aktionen:** Edit-Icon (✏️) + Delete-Icon (🗑️) pro Zeile.
- **Create-Button:** Innerhalb des Tab-Panels / über der Tabelle. Öffnet den gleichen Dialog leer.
- **Kein** inline-Formular für Create am Seitenende (inkonsistent, skaliert schlecht).
- **Dropdowns für verlinkte Entities:** Immer den aktuell verlinkten Wert anzeigen, auch wenn inaktiv. Inaktive Einträge mit `[inaktiv]`-Prefix markieren und ans Ende sortieren.

## Architektur: HA-Proxy

- **Alle** Kommunikation mit Home Assistant geht durch `IHomeAssistantService`.
- REST + WebSocket werden intern vom selben Service verwaltet.
- Weitere Services (Theme, Events) bauen **auf** dem Proxy auf, erstellen keine eigenen HA-Verbindungen.
- Bild-/Datei-Serving an den Client läuft über dedizierte API-Endpoints (Proxy-Pattern):
  - `/api/ha-image` — HA-Bilder (Entity-Pictures)
  - `/api/attachment/{id}` — Task-Kommentar-Bilder von Disk

## Architektur: QueryService-Pattern

- **Read-Only-Queries** (Filter, Listen, Dashboard-Daten) leben in dedizierten QueryServices (z.B. `IChoreTaskQueryService`).
- QueryServices sind die **Single Source of Truth** für Business-Filter-Logik — keine doppelte Where-Clause in Razor-Pages.
- **Mutationen** (Create, Update, Status-Wechsel, Credits) bleiben direkt auf `AppDbContext` in der Page/Component — kein Repository-Pattern nötig.
- QueryServices werden als `Scoped` registriert und sind Unit-Test-fähig (InMemory-DB).

## UX-Prinzip: Tasks-Seite

- **Sichtbar = erfordert Aktion.** Die Tasks-Seite zeigt nur offene, beanspruchte oder zu bestätigende Aufgaben.
- Erledigte Tasks verschwinden sofort aus "Heute"/"Offen" — Feedback über Snackbar, sichtbar im "Erledigt"-Filter und auf dem Dashboard.
- **PendingConfirmation** bleibt sichtbar (erfordert Eltern-Aktion).
- **Dashboard** = Übersicht/Motivation ("Was wurde geschafft?"). Deep-Links von Dashboard → Tasks-Seite mit passenden Query-Parametern.
- Dieses Prinzip gilt für alle Rhythmen (täglich, wöchentlich, monatlich): nach Erledigung verschwindet der Task aus der operativen Ansicht.

## EF Core: InMemory-Provider & Null-Guards

- Der **InMemory-Provider** wertet OR-Expressions client-seitig aus und short-circuited NICHT wie SQL.
- Bei nullable Navigation-Properties (z.B. `t.Schedule`) in komplexen OR-Bedingungen **immer** `t.ScheduleId != null &&` vor dem Zugriff auf `t.Schedule!.Rhythm` setzen.
- Pattern `(t.ScheduleId == null || t.Schedule!.Rhythm != X)` funktioniert (short-circuit innerhalb eines AND), aber in OR-Branches muss explizit gegen null geprüft werden.
- Betrifft alle Queries, die auch in InMemory-Tests laufen.

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

## UI-Pattern: Achievement Badges

- **Form:** Reguläres Pointy-Top-Hexagon. Seitenverhältnis `width : height = 1 : 1.1547` (√3:2).
- **Clip-Path:** `polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%)` — volle Container-Breite, kein prozentualer Einzug.
- **Ring-Effekt:** Outer-Element-Background sichtbar durch `inset` auf Inner-Element (Preview: 5px, Board: 4px). Inner nutzt den gleichen Clip-Path.
- **Glow:** `filter: drop-shadow()` statt `box-shadow` (box-shadow wird von clip-path abgeschnitten).

### Größen

| Kontext | Breite | Höhe | Row-Step |
|---|---|---|---|
| Preview (Desktop) | 104px | 120px | — |
| Board (Desktop) | 56px | 65px | 54px |
| Preview (Mobile ≤600px) | 92px | 106px | — |
| Board (Mobile ≤600px) | 50px | 58px | 48.5px |

### Farbsystem

Farben werden **ausschließlich aus der Kategorie** abgeleitet (`GetCategoryPalette`), NICHT pro Achievement individuell. `PrimaryColor`/`AccentColor`-Felder im Record sind Legacy und werden ignoriert.

| Kategorie | Farbname | Primary | Accent |
|---|---|---|---|
| Einstieg | Blue | `#0277bd` | `#4fc3f7` |
| Streak / Motivation | Burnt Orange | `#bf360c` | `#ff8a65` |
| Menge / Tagesleistung / Zuverlässigkeit | Green | `#2e7d32` | `#81c784` |
| Chore-Mastery | Purple | `#6a1b9a` | `#ce93d8` |
| Tageszeit / Timing | Amber | `#e65100` | `#ffcc80` |
| Event-Reaktion / Home Assistant | Cyan | `#006064` | `#4dd0e1` |
| Familie / Teamplay | Magenta | `#880e4f` | `#f48fb1` |
| Kategorie / Vielfalt | Lime | `#33691e` | `#aed581` |
| Kommentare | Teal | `#004d40` | `#80cbc4` |
| Konsistenz | Indigo | `#1a237e` | `#7986cb` |
| Hidden | Blue Grey | `#37474f` | `#90a4ae` |

### Ring = Tier-Farbe

| Tier | Ring-Farbe |
|---|---|
| Base | Kategorie-Accent |
| Bronze | `#cd7f32` |
| Silber | `#c0c0c0` |
| Gold | `#ffd700` |
| Legendär | `#b388ff` |
| Hidden | `#546e7a` |

### Regeln

- Neue Achievements bekommen **keine** eigenen Farben — die Kategorie bestimmt alles.
- Neue Kategorien brauchen einen neuen Eintrag in `GetCategoryPalette()` mit einem Farbton, der sich von allen bestehenden klar unterscheidet.
- Gold/Legendär erhalten zusätzlichen `filter: drop-shadow()` für stärkeren Glow (CSS-Klassen `.achievement-hex-gold`, `.achievement-hex-legendaer`).
- Locked-State überschreibt alle Farben mit Teal-Dark (`--achievement-primary: #00454a`, Accent/Ring: Teal).

## Review Points (visuell zu validieren)

- **Achievement Hex-Ratio (optische Korrektur):** Mathematisch korrekt wäre `1:1.1547` (√3:2),
  aber durch die vertikale Überschätzung des menschlichen Auges (vertical-horizontal illusion)
  wirkt `1:1.05` optisch gleichmäßiger. Dev-Seite hat einen Slider zum Vergleich.
  → Nach visuellem Test den finalen Ratio-Wert hier festhalten und als CSS-Default übernehmen.
  Aktueller Kandidat: **1.05**.

- **Card Status-Badge (oben rechts):** Abweichende operative Zustände (Pausiert, Inaktiv)
  werden oben rechts als Chip angezeigt (Zeile 1: `justify-space-between`, Identität links, Status rechts).
  Normalzustand = kein Badge. → Nach visuellem Test als feste UI-Regel übernehmen oder anpassen.

  ```
  ┌───────────────────────────────────────────────┐
  │ [Avatar/Icon + Name]           [⚠ Status]    │  ← nur bei Abweichung
  │ Details / Attribute / Chips                    │
  │                                [Actions ...]   │
  └───────────────────────────────────────────────┘
  ```
