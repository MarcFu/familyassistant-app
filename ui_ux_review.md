# UI/UX Review — FamilyAssistant (Home Assistant Add-on)

**Reviewer:** UI/UX Audit (automated)
**Date:** 2026-05-28
**App Version:** Current HEAD
**Reference:** Home Assistant Design Language (MD3, outlined cards, flat surfaces, pill buttons)

---

## Executive Summary

The app is **well-built** and follows many HA conventions correctly (outlined cards, pill buttons, 12px radius, dark-first theming, CSS variables). The `ResponsiveList` component is a strong pattern. However, several areas break HA's flat visual language (elevation on tabs/appbar), spacing is inconsistent across pages, and destructive actions universally lack confirmation — a significant UX risk for a family app where children interact.

**Overall HA Alignment:** ~75% — good foundation, but elevation usage, spacing inconsistency, and missing safety nets bring it down.

---

## Main Review Table

| # | View | Category | Severity | Problem | Improvement Idea | HA Guideline Reference |
|---|------|----------|----------|---------|------------------|------------------------|
| 1 | All | Elevation/Shadow | Medium | `MudAppBar Elevation="1"` and `MudDrawer Elevation="2"` produce box-shadows. CSS adds borders correctly, but shadows still render underneath. | Set `Elevation="0"` on both. The CSS border rules already provide HA-native separation. | HA uses border-only chrome since 2022.11 (MD3) |
| 2 | Settings | Elevation/Shadow | Medium | `MudTabs Elevation="2" Rounded="true"` creates a raised, shadowed tab bar — alien to HA's flat UI. | Use `Elevation="0" Outlined="true"` (as InternetRules already does correctly). | HA tabs are flat with bottom-border active indicator |
| 3 | Tasks | Destructive Action | High | "Cancel Task" executes immediately without confirmation. Irreversible (status → Cancelled). | Add `MudMessageBox` or confirmation dialog: "Cancel task [Name]? This cannot be undone." | HA confirms destructive actions (delete automation, remove device) |
| 4 | Settings | Destructive Action | High | "Delete Person" executes immediately. Removes all linked data (tasks, credits, devices). | Confirmation dialog with impact summary: "Remove [Name]? X tasks and Y credits will be lost." | HA always confirms entity removal |
| 5 | Settings | Destructive Action | High | "Delete Trigger" executes immediately without confirmation. | Simple confirmation dialog. | — |
| 6 | InternetRules | Destructive Action | High | "Delete Device" and "Delete Rule" execute immediately. Deleting a device may orphan rules. | Confirmation dialog. For devices: warn if rules reference it. | — |
| 7 | Admin | Destructive Action | High | "Reset Credits" has only a static warning alert above the form — no modal confirmation gate. | Add confirmation dialog showing current vs. target balance and delta. | HA confirms service calls with side-effects |
| 8 | Admin | Destructive Action | Medium | "Delete Backup" is immediate. "Restore Backup" triggers on file selection with no intermediate confirm. | Confirmation for both. Restore should show a 2-step: select file → confirm with warning. | — |
| 9 | Tasks | Spacing/Alignment | Low | Title uses `mb-2` while Dashboard/Settings use `mb-4`. Creates inconsistent vertical rhythm across pages. | Standardize to `mb-4` for all page titles (consistent 16px breathing room). | HA dashboards maintain consistent section spacing |
| 10 | Tasks vs Chores | Spacing/Alignment | Low | Filter toolbar: Tasks uses `gap-2` (8px), Chores uses `gap-3` (12px). Visually different density. | Standardize to `gap-3` on all filter toolbars for uniform horizontal rhythm. | — |
| 11 | Tasks | Spacing/Alignment | Low | Task cards use `mb-2` (8px gap between cards) which feels cramped for touch targets. HA list cards typically have more vertical breathing. | Use `mb-3` (12px) between task cards, or rely on grouped `ResponsiveList` pattern which already handles dividers properly. | HA list items have ~12-16px visual separation |
| 12 | Tasks | Spacing/Alignment | Medium | Permanent tasks section uses fixed `width: 200px` cards in a `flex-wrap` layout. On narrow screens (<400px) they cannot shrink, causing horizontal overflow. | Use `min-width: 160px; flex: 1 1 200px;` or switch to a responsive grid like `grid-template-columns: repeat(auto-fill, minmax(180px, 1fr))`. | HA cards are always fluid/responsive |
| 13 | Dashboard | Spacing/Alignment | Low | Stats cards use `min-width: 80px` with `gap-4` (16px). On ~360px screens, 3 cards + gaps may not fit in a row. | Use `flex: 1 1 80px` and allow wrapping, or reduce gap to `gap-3` on mobile. | — |
| 14 | Dashboard | Spacing/Alignment | Low | "Other Persons" section title uses `mt-2 mb-2` — very tight above a `MudGrid`. Feels crammed against the pending-confirmations card above it. | Use `mt-4 mb-3` for section headers to provide clear visual separation. | HA sections have generous top margins (24-32px) |
| 15 | Settings/Persons | Spacing/Alignment | Low | Grid columns use fixed `120px` and `130px` for Name/Role. Long names get truncated without tooltip. Short names waste space. | Use `minmax(100px, 1.5fr)` for Name column. Add `title` attribute or `MudTooltip` for overflow. | — |
| 16 | Chores | Heading Level | Low | Page title uses `Typo.h5` while Tasks and Dashboard use `Typo.h4`. Inconsistent visual hierarchy. | Standardize all page titles to `Typo.h4`. | HA panel titles are consistent size across views |
| 17 | All | Contrast/A11y | Medium | No `:focus-visible` styles defined anywhere in CSS. Keyboard users cannot see which element has focus. | Add `*:focus-visible { outline: 2px solid var(--mud-palette-primary); outline-offset: 2px; }` or per-component focus rings. | WCAG 2.1 AA requires visible focus indicators |
| 18 | MainLayout | Contrast/A11y | Medium | User avatar/name area is a `<div @onclick>` without `role="button"`, `tabindex="0"`, or keyboard handler. Screen readers ignore it; keyboard users cannot reach it. | Change to `<button>` or add `role="button" tabindex="0" @onkeydown` for Enter/Space. | WCAG — interactive elements must be keyboard-accessible |
| 19 | Achievement Board | Contrast/A11y | Low | Locked badges use `opacity: 0.82` + `filter: brightness(0.78)`. Combined effect pushes some text/icon combos below 3:1 contrast ratio. | Test with contrast checker. Consider `opacity: 0.9` + `brightness(0.85)` or add a subtle text shadow for icons. | WCAG AA: 3:1 for large text/icons, 4.5:1 for small text |
| 20 | Achievement Overlay | Contrast/A11y | Low | Dismiss action is cursor-only (click anywhere). No visible "Close" button, no keyboard handler, no `aria-label`. | Add a visible "Tap to dismiss" hint or small X button. Add `@onkeydown` for Escape. | HA modals always have explicit close affordance |
| 21 | Chores | Search Pattern | Low | Uses `MudTextField` with `Immediate="true"` (fires on every keystroke). Project convention (AGENTS.md) specifies `DebouncedTextField` (400ms). Inconsistent with Tasks page. | Replace with `<DebouncedTextField>` for consistency and to avoid excessive re-renders on large lists. | Internal project convention |
| 22 | Tasks | Filter Label | Low | Status filter `<MudSelect>` has `Label="Filter"` hardcoded in English. Not localized. | Use `Label="@L["Common.Filter"].Value"` | — |
| 23 | Chores | Empty State | Low | When filters produce zero results (but chores exist), no empty state is shown — just a blank area below the toolbar. | Add `@if (_filteredChores.Count == 0) { <MudAlert> No chores match your filter </MudAlert> }` | HA shows contextual empty states |
| 24 | Chores | Feedback | Low | `ToggleActive` gives no visual feedback (no snackbar). User only sees the switch flip. | Add snackbar: "Chore [Name] deactivated/activated." Consistent with Tasks page pattern. | HA confirms state changes with toast |
| 25 | Chores | A11y | Medium | `MudSwitch` in action column has no label or `aria-label`. Screen readers announce "switch" with no context. | Add `aria-label="@($"Toggle {chore.Name}")"` or use the `Label` parameter (visually hidden). | WCAG — form controls need accessible names |
| 26 | Chores | A11y | Low | Edit and Calendar icon buttons have no `Title` or `Tooltip`. Sighted users must guess the icon meaning. | Wrap in `<MudTooltip Text="Edit">` / `<MudTooltip Text="Schedule">` as Tasks page does for its icon buttons. | HA icon buttons always have tooltips |
| 27 | Settings | Mental Model | Medium | Persons: Role dropdown auto-saves on change. Icons/Storage/System tabs require explicit "Save" button. Mixed auto-save vs manual-save is confusing. | Either: (a) make all settings auto-save with snackbar, or (b) add "Save" to Persons tab too. Recommend (a) for HA consistency. | HA entity config auto-saves; settings panels also auto-apply |
| 28 | Settings | Mental Model | Low | "Import Person" is a single click with no undo/confirmation. User sees a card, clicks it, person is imported immediately. | Show brief confirmation or make it a button with label "Import" rather than the whole card being clickable. | — |
| 29 | InternetRules | Empty State | Medium | If no children exist, the tabs section renders empty — just the page title and nothing else. No guidance. | Add `<MudAlert>` explaining only children are shown here, with link to Settings to add/configure persons. | HA shows contextual help for empty states |
| 30 | InternetRules | Disabled State | Low | "Add Rule" button is disabled when no devices exist, but no tooltip/hint explains why. User sees a grayed-out button with no recourse. | Add `MudTooltip Text="Add a device first"` on the disabled button, or show inline helper text. | HA disabled controls explain their state |
| 31 | InternetRules | Localization | Medium | Column headers "Name", "Typ", "Modus", "Details" (lines 120-123) are hardcoded German, not using `@L[...]`. | Replace with localized keys. | — |
| 32 | Profile | Localization | Medium | Multiple hardcoded German strings: "Tasks erledigt", "Aktuelle Serie", "Rekord-Serie", "Noch nicht freigeschaltet", "Credits". | Replace with `@L[...]` keys. Already have DE/EN/FR resx files. | — |
| 33 | Profile | Dead End | Low | Unauthenticated state shows "Go to Einstellungen" but provides no clickable link. User must navigate manually. | Add `<MudLink Href="settings">` around the text. | — |
| 34 | Profile | Navigation | Medium | No entry in NavMenu — accessible only via avatar click in AppBar. `Nav.Profile` key exists but is unused. The mobile AppBar hides the username, making the tiny avatar the only access point. | Add Profile to NavMenu below the divider (above Settings), or show it in the user section. | HA user profile is always reachable from sidebar |
| 35 | All | Error Resilience | Medium | Only Tasks page has try/catch. Database errors on Chores, Home, Settings, InternetRules will crash the page with no recovery. | Wrap `OnInitializedAsync` data loading in try/catch. Show `<MudAlert Severity="Error">` with retry button on failure. | HA panels show error cards with retry |
| 36 | All | Unsaved Changes | Low | No page warns about unsaved changes when navigating away (Settings Icons/Storage/System tabs, Admin correction form). | Implement `NavigationLock` (Blazor 8+) with "Unsaved changes" prompt on forms that require explicit Save. | HA warns before discarding config changes |
| 37 | Tasks | Visual Weight | Low | Action buttons per task can stack to 4-5 buttons (Comment + Claim + Done + Cancel + Unclaim). On narrow widths, the action row wraps and dominates the card visually. | Consider collapsing secondary actions (Cancel, Unclaim) into a `MudMenu` overflow (three-dot menu) on mobile, keeping only the primary action visible. | HA entity cards use overflow menus for secondary actions |
| 38 | Tasks | Alignment | Low | Credits chip is inside `MudText Typo="caption"` (line 202-206). Chips inside text elements create baseline alignment issues — the chip sits slightly higher than surrounding text. | Move chip outside the `<MudText>` into its own flex item. Use `align-items: center` on the parent flex. | — |
| 39 | Dashboard | Visual Contrast | Low | "Pending Confirmations" card uses `border-color: var(--mud-palette-warning)` inline style override. This breaks the consistent `--ha-border-medium` language used everywhere else. | Use a left-border accent (`border-left: 3px solid var(--mud-palette-warning)`) while keeping the card border consistent, similar to HA alert cards. | HA uses left-accent borders for severity |
| 40 | ResponsiveList | Loading Flash | Low | First render shows `MudProgressLinear` until JS interop fires viewport size. Creates a brief flash on every page using the component. | Consider SSR-defaulting to desktop layout (most users) and correcting on mobile detection, avoiding the progress flash for majority case. | — |
| 41 | Settings/Triggers | Column Density | Medium | 7 columns (`Breakpoint.Lg`) but on 960-1280px screens it still shows grid mode with columns squeezed to minimum widths. Headers collide or truncate. | Ensure `Breakpoint.Lg` (1280px) is actually used — verify the `ResponsiveList` switches to cards below 1280px. Consider reducing to 5 visible columns (merge Status+Actions, hide Debounce into card mode). | — |
| 42 | All | Touch Target | Low | `MudIconButton Size="Size.Small"` renders ~28px touch targets. Mobile guidelines recommend 44px minimum. | Use `Size="Size.Medium"` on mobile card templates, or add padding to hit area via `Style="padding: 8px;"`. | Material Design: 48dp minimum touch target |
| 43 | MainLayout | Spacing | Low | `MudMainContent Class="px-4 pb-4"` — 16px padding on all sides except top. The nested `<div class="pt-4">` adds top padding, but creates a double-nesting pattern that makes spacing harder to reason about. | Move to `Class="pa-4"` on MudMainContent directly or use consistent padding approach. | — |

---

## Spacing & Alignment Audit

### Vertical Rhythm Inconsistencies

| Element | Tasks | Chores | Dashboard | Settings | InternetRules |
|---------|-------|--------|-----------|----------|---------------|
| Page title → content | `mb-2` | `mb-3` | `mb-4` | `mb-4` | `mb-4` |
| Section title → list | N/A | N/A | `mb-2` | `mb-3` | `mb-2` |
| Card → card gap | `mb-2` | via ResponsiveList | via MudGrid | via ResponsiveList | via ResponsiveList |
| Filter toolbar → content | `mb-4` | `mb-3` | N/A | N/A | N/A |

**Recommendation:** Standardize to:
- Page title: `mb-4` (16px)
- Section title: `mb-3` (12px)
- Filter toolbar: `mb-4` (16px)
- Card spacing: `mb-3` (12px) for non-grouped, dividers for grouped

### Horizontal Gaps

| Element | Value | Pages |
|---------|-------|-------|
| Filter toolbar gap | `gap-2` (8px) | Tasks |
| Filter toolbar gap | `gap-3` (12px) | Chores |
| Action buttons gap | `gap-1` (4px) | All pages |
| Dashboard stats | `gap-4` (16px) | Home |

**Recommendation:** Standardize filter toolbars to `gap-3`. Keep action buttons at `gap-1` (correct for small icon buttons).

### Padding Patterns

| Context | Grouped grid item | Grouped card item | Non-grouped card |
|---------|-------------------|-------------------|-----------------|
| Default (ResponsiveList) | `pa-2` (8px) | `pa-3` (12px) | `pa-2` / `pa-3` |
| Tasks (manual) | N/A | N/A | `pa-3` |
| Dashboard own card | N/A | N/A | `pa-5` (20px) |
| Dashboard other cards | N/A | N/A | `pa-3` |

**Recommendation:** The Dashboard "own card" using `pa-5` is appropriate for a hero card. Standard cards should consistently use `pa-3` (12px all sides). Grid items at `pa-2` feel compressed — consider `pa-3` to match card mode.

---

## Contrast & Accessibility Summary

| Issue | Location | WCAG Level | Impact |
|-------|----------|------------|--------|
| No focus-visible styles | Global CSS | AA | Keyboard users cannot navigate |
| Clickable div without role/tabindex | MainLayout (user pill) | A | Not keyboard/screen-reader accessible |
| Locked achievement low contrast | AchievementHex | AA | Icons may fall below 3:1 ratio |
| MudSwitch without label | Chores, Settings/Triggers | A | Screen readers announce "switch" with no context |
| Icon buttons without tooltips | Chores (Edit, Calendar) | AA | Sighted users must guess meaning |
| Overlay dismiss has no visible affordance | AchievementUnlockOverlay | AA | Users may not know how to dismiss |
| Small touch targets (28px) | All icon buttons Size.Small | Best Practice | Mobile users may mis-tap |

---

## Positive Patterns (Keep These)

| Pattern | Why It Works |
|---------|-------------|
| `ResponsiveList` with `Grouped="true"` | Perfect HA "list card" pattern, clean breakpoint handling |
| Extended FAB (icon + label) fixed bottom-right | Exactly how HA handles primary create actions |
| Pill-shaped buttons globally via CSS | Consistent with HA MD3 refresh |
| `DebouncedTextField` (Tasks) | Avoids excessive re-renders, smooth UX |
| Deep-link filters via query params | Enables dashboard → tasks flow, shareable URLs |
| Outlined cards with `border-radius: 12px` | Pixel-perfect HA alignment |
| Dark-first with CSS custom properties | Matches HA's default dark theme approach |
| Inline status actions (1-click Claim/Done) | Minimal friction for daily task flow |
| Snackbar feedback on mutations | Consistent with HA toast pattern |
| Role-based UI (children see less) | Appropriate for family context |

---

## Priority Recommendations (Top 5)

1. **~~Add confirmation dialogs for all destructive actions~~** — [x] Done. Cancel task, Delete person/trigger/device/rule, Reset credits, Delete backup all have `ShowMessageBoxAsync` confirmations.

2. **~~Remove all elevation/shadows~~** — [x] Done. `Elevation="0"` on AppBar, Drawer, and Settings tabs. Settings tabs also got `Outlined="true"`.

3. **~~Add focus-visible styles and fix semantic HTML~~** — [x] Done. Global `*:focus-visible` CSS rule + user pill converted to `<button>`.

4. **~~Standardize spacing~~** — [x] Done. All page titles → `Typo.h4 mb-4`, Tasks filter `gap-3`, Dashboard section `mt-4 mb-3`.

5. **Add Profile to NavMenu** — [ ] Cancelled by stakeholder decision. Avatar-only access is intentional.

---

## Implementation Log

### Completed Fixes

| # | Finding | Status | Notes |
|---|---------|--------|-------|
| 1 | AppBar/Drawer elevation | [x] Done | `Elevation="0"` on both |
| 2 | Settings Tabs elevation | [x] Done | `Elevation="0" Outlined="true"` |
| 3 | Cancel Task confirmation | [x] Done | `ShowMessageBoxAsync` + snackbar |
| 4 | Delete Person confirmation | [x] Done | `ShowMessageBoxAsync` |
| 5 | Delete Trigger confirmation | [x] Done | `ShowMessageBoxAsync` |
| 6 | Delete Device/Rule confirmation | [x] Done | Both in InternetRules |
| 7 | Reset Credits confirmation | [x] Done | Shows current vs target |
| 8 | Delete Backup confirmation | [x] Done | `ShowMessageBoxAsync` |
| 9 | Page title spacing | [x] Done | All standardized to `mb-4` |
| 10 | Filter toolbar gap | [x] Done | Tasks `gap-2` → `gap-3` |
| 14 | Dashboard section spacing | [x] Done | `mt-4 mb-3` |
| 16 | Chores heading level | [x] Done | `Typo.h5` → `Typo.h4` |
| 17 | Focus-visible CSS | [x] Done | Global rule in `app.css` |
| 18 | User pill semantics | [x] Done | `<div>` → `<button>` |
| 21 | Chores DebouncedTextField | [x] Done | Replaced `MudTextField Immediate` |
| 22 | Tasks filter localization | [x] Done | `@L["Common.Filter"]` |
| 23 | Chores filtered empty state | [x] Done | `MudAlert` when filters match nothing |
| 24 | Chores ToggleActive feedback | [x] Done | Snackbar on activate/deactivate |
| 25 | Chores MudSwitch aria-label | [x] Done | Both grid and card templates |
| 26 | Chores icon button tooltips | [x] Done | MudTooltip on Edit + Schedule |
| 27 | Settings auto-save feedback | [x] Done | Snackbar on role change + pause toggle |
| 29 | InternetRules no-children state | [x] Done | MudAlert with link to Settings |
| 30 | InternetRules disabled Add Rule | [x] Done | MudTooltip explains "Add device first" |
| 31 | InternetRules column headers | [x] Done | Localized via `@L[...]` |
| 32 | Profile localization | [x] Done | All hardcoded DE strings → resx keys |
| 33 | Profile dead-end link | [x] Done | Added `<MudLink Href="settings">` |
| 34 | Profile in NavMenu | [ ] Cancelled | Intentional design decision |
| 35 | Error handling on pages | [x] Done | try/catch + MudAlert on Home, Chores, InternetRules, Settings |
| 39 | Dashboard warning border | [x] Done | Changed to `border-left: 3px solid` accent |
| 11 | Task card spacing | [x] Done | `mb-2` → `mb-3` for 12px between cards |
| 12 | Permanent tasks responsive | [x] Done | `width: 200px` → `flex: 1 1 180px; min-width: 160px; max-width: 240px` |
| 13 | Dashboard stats mobile | [x] Done | Added `flex: 1 1 80px` for responsive growth |
| 15 | Person name columns | [x] Done | `120px 130px` → `minmax(100px, 1.5fr) minmax(100px, 1.5fr)` |
| 20 | Achievement overlay dismiss | [x] Done | Added X button, Escape/Enter/Space key handling, tabindex |
| 38 | Credits chip alignment | [x] Done | Extracted chips from `<MudText>` into flex sibling container |
| 42 | Touch targets mobile | [x] Done | CSS `@media (pointer: coarse)` → 40px min on small icon buttons |
| 41 | Triggers column density | [x] Done | Reduced from 7 to 5 columns (merged switch into actions, dropped debounce from grid). Breakpoint Lg→Md |
| 37 | Tasks overflow menu | [x] Done | Secondary actions (Unclaim, Cancel) moved into MudMenu 3-dot overflow; primary actions (Comment, Claim, Done, Confirm) stay visible |

### Not Yet Addressed (Backlog)

| # | Finding | Priority | Reason |
|---|---------|----------|--------|
| 19 | Achievement locked contrast | Low | Needs visual testing |
| 28 | Import Person no-confirm | Low | Low risk action |
| 36 | Unsaved changes warning | Low | Would need NavigationLock |
| 40 | ResponsiveList loading flash | Low | SSR optimization |
| 43 | MainLayout double padding | Low | Cosmetic |
