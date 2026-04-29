# Swiss Poster UI Redesign

## Design Direction

**International Style / Swiss Poster** — editorial minimalism applied to a gaming utility. Warm off-white base, pure black accent bar, Georgia serif display type, Segoe UI body. The tool reads like a museum wall label, not a hacker terminal.

Distinctive because: almost every gaming optimization tool defaults to dark+neon. This goes the opposite way — warm, restrained, typographic.

## Color Palette

### Light Theme (default)
- Page background: `#F6F2EC` (warm off-white)
- Left bar: `#141210` (pure near-black)
- Text primary: `#1A1410`
- Text secondary: `#8A8070`
- Text disabled: `#C0B8A8`
- Dividers: `#E8E2D8` (warm 1px)
- Accent (active check): `#1A1410` (black fill)

### Dark Theme
- Page background: `#1A1816` (warm black)
- Left bar: `#F0ECE4` (cream — inverted from light)
- Text primary: `#F0ECE4`
- Text secondary: `#8A8070`
- Text disabled: `#5A5450`
- Dividers: `#2A2622`
- Accent (active check): `#F0ECE4` (cream fill)

No other accent colors. Hierarchy is purely typographic + spatial.

## Typography

| Role | Font | Size | Weight | Letter-spacing |
|------|------|------|--------|----------------|
| App title (left bar) | Georgia | 20px | Regular | 1px |
| Section labels | Segoe UI | 7px | Regular | 3px (uppercase) |
| Item labels | Segoe UI | 11px | Regular | — |
| Item descriptions | Segoe UI | 9px | Regular | — |
| Status text (giant) | Georgia | 40px | Light (300) | — |
| Left bar status | Segoe UI | 11px | Regular | 1px |
| Inline selector (power/timer) | Segoe UI | 9px | Regular | — |
| Runtime options | Segoe UI | 11px | Regular | — |
| ARM button | Segoe UI | 10px | Regular | 2px (uppercase) |
| Carved mark | Georgia | 13px | Regular | — |

## Layout Structure

```
┌──────────┬─────────────────────────────────────┐
│          │  SECTION LABELS (7px uppercase)      │
│  BLACK   │                                     │
│  BAR     │  Item name ───────────────── desc   │
│  170px   │  Item name ───────────────── desc   │
│          │  Item name ── ▾selector ─── apply   │
│  BRAND   │  ...                                │
│  LOGO    │                                     │
│          │  SECTION: CUSTOM GAMES              │
│  STATUS  │  [tag] [tag] [+ add]                │
│  DOT     │  inline add form...                 │
│          │                                     │
│  ARM     │  SECTION: RUNTIME (2-col grid)      │
│  BUTTON  │  □ option    □ option               │
│          │  ■ option    ■ option               │
│  SAVE    ├─────────────────────────────────────│
│          │  IDLE              monitoring text  │
│  № 001   │  (40px Georgia light)               │
└──────────┴─────────────────────────────────────┘
```

- Window size: 880×680 (slightly taller to accommodate expanded layout)
- Left bar: fixed 170px, full height
- Right content: flex column, 22px/26px padding
- No scroll viewers — all content fits in one viewport

## Component Design

### Square Checkbox
Replaces all toggle switches. 14px square for main items, 12px for runtime items, 10px for custom game tags.
- **On**: solid fill (black in light theme, cream in dark)
- **Off**: 1.5px border outline, text fades to disabled color
- No animation needed — the binary starkness is the point

### Inline Selector (Power Plan / Timer Resolution)
Underlined text showing current value + `▾` indicator. Positioned inline after the item label. An `apply` text link on the far right. Click opens a dropdown styled consistently (thin border, no shadow). The surrounding toggle square controls whether the feature activates at all.

### Custom Games
- **Existing entries**: inline tags with background `#ECE6DC` (light) / `#242220` (dark), showing process name + two micro-squares (priority, CPU0) + × remove
- **Add form**: single-row inline below tags — two underline-inputs (process name, display name) + two micro-checkboxes (Hi priority, CPU0 unbind) + "add" text link
- **Empty state**: dashed-border "+ add" tag only

### Runtime Options
2-column grid, 14px squares, full text labels. Same visual weight as optimization items — no visual hierarchy difference between "Throttle SGuard" and "Auto-start on boot".

### Left Bar
- Top: app title (Georgia, two lines) + short divider line + "PERFORMANCE UTILITY v1.1"
- Middle: "STATUS" label + status dot + "IDLE" text
- Bottom: ARM button (border-only, 1.5px) + "save" text link

### Carved Mark
"№ 001" at the very bottom of the left bar. Implemented as two stacked TextBlocks:

```xaml
<!-- Shadow layer (1px down, dark-on-dark = depth illusion) -->
<TextBlock Text="№ 001" Foreground="#000000" Opacity="0.5"
           Margin="0,1,0,0" />
<!-- Light layer (on top, carved highlight) -->
<TextBlock Text="№ 001" Foreground="#F6F2EC" Opacity="0.15" />
```

This creates a debossed/carved effect — no color, only depth.

### Status Indicator
- **Idle**: dim dot (35% opacity), giant "IDLE" in Georgia 40px light, same color as dividers (nearly invisible)
- **Active**: dot becomes solid black (light) / solid cream (dark), "IDLE" changes to game name display, status text updates

### Theme Toggle
No visible toggle in the UI. Detect from Windows system theme (`AppsUseLightTheme` registry key) on startup. Could add a manual toggle later if requested.

## WPF Implementation Notes

- All styles in `Window.Resources` as before, but rewritten from scratch
- Custom window chrome preserved (WindowStyle=None, custom drag area)
- MVVM bindings preserved — only XAML templates and styles change
- Georgia font is available on all Windows 10/11 installations
- No external dependencies or font files needed
- DarkComboBox restyled to match inline-selector aesthetic (underline, no border box)
- ScrollViewer removed — content fits 680px height

## What Does NOT Change
- `MainWindow.xaml.cs` code-behind logic
- `MainViewModel.cs` and all services
- `App.xaml.cs` startup flow
- System tray behavior
- All MVVM bindings and event wiring
- Window resize constraints
