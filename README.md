# RyLib

[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://github.com/Ryannlt/RyLib/blob/main/LICENSE)

RyLib is a UI library for [BepInEx](https://github.com/BepInEx/BepInEx) client mods for **Holdfast: Nations At
War**. Mods register P menu tabs, buttons, key hints and markers with RyLib by name, and RyLib places them in the
game's UI. Several mods can add to the same menus and bars at once without overlapping.

RyLib adds nothing to the game by itself. Mods that use it list it as a dependency, and mod managers install it
with them.

## Features

| Feature | Description |
| --- | --- |
| [P menu tabs](#p-menu-tabs) | Header tabs with pages of controls, a shared **Mods** tab with one sub tab per mod, and map pages. |
| [Players tab bar](#players-tab-bar) | Controls in the bar under the players list, next to Admin Raygun. |
| [Player row actions](#player-row-actions) | Buttons on an expanded player row, in the same grid as the game's buttons. |
| [Kill log actions](#kill-log-actions) | Actions for the killer and the victim when a kill log row is clicked. |
| [Key hints](#key-hints) | Entries in the free roam and spectating key bars. |
| [World marks](#world-marks) | Rings, beams, labels and a glow visible through walls, on players or positions. |
| [Minimap marks](#minimap-marks) | Markers on the game's minimap. |
| [Map pages](#map-pages) | An overhead image of the battle with every player drawn by class and faction. |
| [Helpers](#helpers) | Faction colours, game icons in TextMeshPro text, and stock icon names. |

## Using RyLib in a mod

Reference `RyLib.dll`, add the dependency, and register in your plugin's `Awake`:

```csharp
[BepInPlugin(Guid, "MyMod", "1.0.0")]
[BepInDependency(RyLib.RyLibPlugin.Guid)]
public class MyMod : BaseUnityPlugin
{
    public const string Guid = "com.example.mymod";

    private void Awake()
    {
        RyLib.PlayerRow.AddAction(Guid, "Wave", RyLib.StockRowButton.Heal, playerId => Wave(playerId));
    }
}
```

Add `"Ryanlt-RyLib-1.0.0"` to the dependencies in `manifest.json`.

### Conventions

- Every call takes your plugin GUID as `owner`. RyLib uses it in log messages and to keep each mod's
  registrations separate.
- Register everything in `Awake`. RyLib creates the UI when the game builds its own, and again after a map change.
- Each callback runs inside its own `try`/`catch`. An exception is logged with your GUID and does not affect other
  mods.
- Registering the same label twice from one owner logs a warning and returns `false`.
- Tabs, pages and mod tabs are looked up by name. Asking for an existing name returns the existing one, and
  content from every mod is added to it. Asking for an existing name as a different kind of page is refused and
  logged with both owners.
- A `Func<bool> visible` parameter is called several times a second, and the UI shows or hides to match. `null`
  means always visible.

### P menu tabs

```csharp
ModTab    PMenu.ModTab(string owner, string name, string icon = null, bool adminOnly = true, Func<bool> visible = null)
HeaderTab PMenu.Header(string owner, string name, string icon = null)
Page      HeaderTab.Page(string owner, string name, string icon = null, bool adminOnly = true, Func<bool> visible = null)
Page      HeaderTab.MapPage(string owner, string name, string icon = null, Func<bool> visible = null)
bool      ModTab.Section(string owner, string title, Action<Section> build)
bool      Page.Section(string owner, string title, Action<Section> build)
```

`PMenu.ModTab` adds a sub tab to the shared **Mods** header tab. The Mods tab lists one button per mod on the
left and shows the selected mod's sections on the right.

```csharp
RyLib.ModTab tab = RyLib.PMenu.ModTab(Guid, "MyMod", RyLib.StockIcon.Shield);
tab.Section(Guid, "Display", section =>
{
    section.Toggle("Show rings", Settings.ShowRings, RyLib.StockIcon.Players);
    section.Stepper("Ring size", Settings.RingSize, 0.5f, "0.0");
});
```

`PMenu.Header` adds a header tab of your own after the game's tabs. The game's tab names are reserved: `Players`,
`Rules`, `Admin`, `Maps`, `Kill Log` and `Artillery`. RyLib sizes every header label and icon to fit the bar,
including the game's.

A header tab holds pages. `Page` holds sections and `MapPage` holds a map. A header with more than one page shows
a row of icon buttons for switching between them. A header with no visible pages is hidden.

Pages and mod tabs with `adminOnly` set show only after the server accepts an `rc login`.

### Section controls

A section is a titled group of buttons in the game's style. Each control is bound to a BepInEx `ConfigEntry`.
The control saves the value when clicked and updates when the config file changes.

```csharp
void Toggle(string label, ConfigEntry<bool> entry, string icon = null)
void Cycle<T>(string label, ConfigEntry<T> entry, Func<T, string> text = null, Func<T, bool> isOn = null, string icon = null)
void Stepper(string label, ConfigEntry<float> entry, float step, string format = "0.00", string icon = null)
void Button(string label, Action onClick, string icon = null)
```

| Control | Behaviour |
| --- | --- |
| `Toggle` | Switches the entry on and off. Lit while on. |
| `Cycle` | Moves to the next enum value on each click. `text` gives the label for each value and `isOn` sets which values light the button. |
| `Stepper` | Shows the value. Left click adds `step`, right click subtracts it, within the entry's acceptable range. |
| `Button` | Calls `onClick`. |

`icon` is the name of any sprite the game has loaded. `StockIcon` has some of them.

### Players tab bar

```csharp
bool PlayersBar.Toggle(string owner, string label, ConfigEntry<bool> entry, string icon = null)
bool PlayersBar.Cycle<T>(string owner, string label, ConfigEntry<T> entry, Func<T, string> text = null,
                         Func<T, bool> isOn = null, string icon = null)
```

Adds a `Toggle` or `Cycle` control to the bar under the players list, next to the game's Admin Raygun button.

### Player row actions

```csharp
bool PlayerRow.AddAction(string owner, string label, StockRowButton icon, Action<int> onClick,
                         Func<bool> visible = null)
```

Adds a button to the Actions block of an expanded player row in the P menu players tab. The block is only
available to a logged in admin. Buttons from every mod go in a grid below the game's six buttons, at the same
size and spacing, and the row grows to fit them.

- `icon` uses the symbol of one of the game's row buttons: `Slay`, `Revive`, `Slap`, `Heal`, `Bring`, `GoTo`,
  `Message`, `Kick` or `Ban`.
- `onClick` receives the network player ID of the row's player.
- When `visible` returns `false` the button is removed and the grid closes the gap.

These buttons also appear in the kill log actions and on the map's selected player panel.

### Kill log actions

```csharp
bool KillLog.EnableActions(string owner, Func<bool> visible = null)
```

Turns on kill log actions. Clicking a row in the admin kill log opens a panel under it with actions for the
killer and the victim: Spectate, Go To, Bring, Slay, Revive, Heal, Slap, Kick and every mod's player row
actions. Slay, Slap and Kick need a second click within four seconds.

The actions are off until a mod calls `EnableActions`, and stay on while any mod that called it returns `true`
from `visible`.

### Key hints

```csharp
bool KeyHints.Add(string owner, string label, Func<KeyCode[]> keys, Func<bool> visible = null)
```

Adds an entry to the key bar in free roam, after Admin Raygun, and to the key bar while spectating, after Report
Player. The entry is a copy of one of the game's entries. Several keys share one entry and are shown as `[ / ]`.
For a key the game has no icon for, RyLib draws one from the game's own key icons. Hints are shown only to a
logged in admin.

```csharp
RyLib.KeyHints.Add(Guid, "Spectate Rambos", () => new[] { KeyCode.LeftBracket, KeyCode.RightBracket });
```

### World marks

```csharp
bool World.Layer(string owner, Action<List<WorldMark>> fill, float hz = 10f)
```

`fill` is called `hz` times a second with an empty list. Add a `WorldMark` for each thing to draw. Marks are
moved every frame, and a mark missing from the list is removed.

| `WorldMark` field | Description |
| --- | --- |
| `Key` | ID for the mark, unique within your layer. |
| `Follow` | Transform to follow. When `null`, `Position` is used. |
| `Glow` | Object to glow in `Colour`, visible through walls. Uses the game's highlighter. If two layers glow the same object, the first layer's glow is used. |
| `Colour` | Colour for the ring, beam, label and glow. |
| `Label`, `LabelHeight` | Text above the mark, drawn under the P menu. `null` for no label. |
| `RingRadius`, `BeamHeight` | Ground ring and vertical beam in metres. `0` for none. |

### Minimap marks

```csharp
bool Minimap.Layer(string owner, Action<List<MinimapMark>> fill, float hz = 4f)
```

Adds markers to the game's minimap. They use the game's minimap pointer class, which handles scaling, rotation
and clamping to the edge.

| `MinimapMark` field | Description |
| --- | --- |
| `Key` | ID for the mark. |
| `Follow` | Transform to track. Required. |
| `Shape` | `Ring`, `Dot` or `Flag`. |
| `Faction` | Faction the mark belongs to. Marks for your faction are blue and marks for the enemy are red. Enemy marks only show when the game shows that faction, while it is revealed, in last stand or during an all charge. With `None` the mark always shows in `Colour`. |
| `AlwaysShow` | Shows the mark regardless of the reveal rules. Use this only for admin features. |
| `Size` | Size in pixels. `0` for the default. |

### Map pages

```csharp
bool Page.MapLayer(string owner, Action<List<MapMark>> fill)
```

A map page renders an overhead image of the current map once per round, framed on the spawns, and draws each
player as their class icon in their faction's colour. The icons come from the game: the spawn menu class
icons, the minimap officer marker and ability icons.

The side panel has:

- A legend of the classes each faction can spawn, with live counts.
- A search box that filters the icons.
- Zoom and pan.
- For the selected player, Spectate, Go To, Bring, Teleport To, Slay and every mod's player row actions.

Teleport To and Teleport Me wait for a click on the map before teleporting.

A map layer adds marks to the map:

| `MapMark` field | Description |
| --- | --- |
| `Key` | ID for the mark. |
| `OnPlayer`, `PlayerId` | Attaches the mark to a player's icon. Otherwise the mark is placed at `Position`. |
| `Shape` | `Ring`, `Dot`, `Flag`, or `Outline`. `Outline` draws a coloured outline around the player's class icon. |
| `Colour` | Mark colour. |
| `Size` | Size in pixels. `0` for the default. |
| `Tooltip` | Text shown on hover. |

A map page only updates while it is open.

### Helpers

| Helper | Description |
| --- | --- |
| `Factions.Colour(FactionCountry)` | Colour for a faction. |
| `Factions.Name(FactionCountry)` | Display name for a faction. |
| `Factions.ClassName(PlayerClass)` | Display name for a class. |
| `TextIcons.Tag(string gameSprite)` | A `<sprite>` tag that draws the named game sprite in TextMeshPro text, tinted by the text colour. Returns `null` while the sprite is not loaded. A failed sprite is retried after ten seconds. |
| `StockIcon` | Names of P menu sprites for the `icon` parameters. |
| `MapStyle.IconSize`, `GlowStyle` | RyLib's own config entries, for mods that show them on a page. |

## Settings

The config file is `BepInEx\config\com.ryannlt.rylib.cfg`. Changes to the file apply without a restart.

| Setting | Default | Effect |
| --- | --- | --- |
| `[Glow] ScaleWithDistance` | `true` | Scales glow outlines down with distance. |
| `[Glow] OutlineWidth` | `0.6` | Outline thickness. `0` turns the outline off. |
| `[Glow] Brightness` | `0.7` | Colour multiplier. Values above 1 make the game's bloom brighten the outline. |
| `[Glow] Glow` | `2` | Halo strength. `0` turns the halo off. |
| `[Glow] GlowWidth` | `0.04` | Halo width. |
| `[Map] IconSize` | `22` | Size of the class icons on map pages, in pixels. |

## Building

`build.ps1` compiles against the game's assemblies and the BepInEx in an r2modman profile, then copies
`RyLib.dll` into that profile. `package.ps1` writes a Thunderstore zip to `Package\`. The options and
requirements are the same as [AdminHelper's](https://github.com/Ryannlt/AdminHelper#building).

The PNG files in `Icons\` are embedded in the DLL. They are class icons that the game does not include, as white
shapes on a transparent background. RyLib turns them into outlined icons that can be tinted. All other icons are
read from the game while it runs, and no game art is included in RyLib.

## Licence

[MIT](https://github.com/Ryannlt/RyLib/blob/main/LICENSE).
