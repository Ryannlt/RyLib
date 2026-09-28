# RyLib

[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://github.com/Ryannlt/RyLib/blob/main/LICENSE)

A shared UI library for [BepInEx](https://github.com/BepInEx/BepInEx) client mods for **Holdfast: Nations At
War**. Mods ask RyLib for UI by name - a P menu tab, a switch, a button on a player's row, a marker on the
minimap - and RyLib decides where it goes, so two mods never draw on top of each other.

It does nothing on its own. Install it because a mod you use depends on it; a mod manager does that for you.

## Why a library

The game's P menu has pieces that only one mod can safely own at a time: the tab bar and the tab ids the menu
keys everything by, the grid of buttons on an expanded player row, the bar under the players list, the kill log
rows. The same goes for the minimap's pointer list and a player's glow. Two mods each patching those directly
will overlap or break each other. RyLib owns them once and hands out space.

## What a mod can ask for

| | |
| --- | --- |
| [P menu tabs](#p-menu-tabs) | Header tabs with pages of switches, a shared **Mods** tab with one sub tab per mod, and map pages. |
| [Players tab bar](#players-tab-bar) | Switches in the bar under the players list, next to Admin Raygun. |
| [Player row actions](#player-row-actions) | Buttons on an expanded player row, laid out with the stock ones. |
| [Kill log actions](#kill-log-actions) | A click on a kill log row opens actions for the killer and the victim. |
| [Key hints](#key-hints) | An entry in the free roam and spectating key bars, drawn like the game's own. |
| [World marks](#world-marks) | Rings, beams, floating labels and a through-walls glow on players or places. |
| [Minimap marks](#minimap-marks) | Markers on the game's minimap that follow its own reveal rules. |
| [Map pages](#map-pages) | An overhead picture of the battle with every player drawn by class and faction. |
| [Helpers](#helpers) | Faction colours, the game's own icons inside TextMeshPro text, stock icon names. |

## For mod authors

Reference `RyLib.dll`, declare the dependency, and register in your plugin's `Awake`:

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

And in `manifest.json`: `"Ryanlt-RyLib-1.0.0"`.

### Rules every call follows

- **Pass your plugin GUID as the owner.** It is how RyLib names you in its log, and how it tells your
  registrations apart from another mod's.
- **Register in `Awake`.** RyLib builds the UI itself whenever the game creates it, and rebuilds it after a map
  change, so you never touch the game's UI objects.
- **Your callbacks cannot break anyone else's.** Each one runs in its own guard; an exception is logged with your
  GUID and the rest of the UI carries on.
- **Duplicates are refused, not doubled.** Registering the same label twice from one owner logs a warning and
  returns `false`.
- **Names are shared on purpose.** Tabs, pages and mod tabs are get-or-create by name, so two mods asking for the
  same one both get it and their content stacks. Asking for an existing name as a different kind of page is a
  collision; it is refused and both owners are logged.
- **`visible` callbacks are polled.** Anything that takes a `Func<bool> visible` asks it a few times a second, so
  a setting can show and hide your UI live. Leaving it `null` means always shown.

### P menu tabs

```csharp
ModTab    PMenu.ModTab(string owner, string name, string icon = null, bool adminOnly = true, Func<bool> visible = null)
HeaderTab PMenu.Header(string owner, string name, string icon = null)
Page      HeaderTab.Page(string owner, string name, string icon = null, bool adminOnly = true, Func<bool> visible = null)
Page      HeaderTab.MapPage(string owner, string name, string icon = null, Func<bool> visible = null)
bool      ModTab.Section(string owner, string title, Action<Section> build)
bool      Page.Section(string owner, string title, Action<Section> build)
```

**Most mods want `PMenu.ModTab`.** It adds a sub tab to one shared **Mods** header, in the style of ULX: a
column of buttons on the left, one per mod, and the selected mod's sections on the right. Your settings sit
beside every other mod's without costing a header each.

```csharp
RyLib.ModTab tab = RyLib.PMenu.ModTab(Guid, "MyMod", RyLib.StockIcon.Shield);
tab.Section(Guid, "Display", section =>
{
    section.Toggle("Show rings", Settings.ShowRings, RyLib.StockIcon.Players);
    section.Stepper("Ring size", Settings.RingSize, 0.5f, "0.0");
});
```

`PMenu.Header` makes a header tab of your own, for something big enough to deserve one. Headers are laid out
after the game's own, which are reserved (`Players`, `Rules`, `Admin`, `Maps`, `Kill Log`, `Artillery`); RyLib
fits every label and icon into the space left, the stock ones included. A header holds pages - `Page` for
sections, `MapPage` for a map - and several pages in one header get a strip of icon buttons to switch between
them. A header with no visible page is hidden.

`adminOnly` pages and mod tabs appear only once the server has accepted an `rc login`.

### Section controls

A section is a titled block of buttons in the game's own style. Every control is bound to a BepInEx
`ConfigEntry`, so it saves itself and follows edits made to the config file.

```csharp
void Toggle(string label, ConfigEntry<bool> entry, string icon = null)
void Cycle<T>(string label, ConfigEntry<T> entry, Func<T, string> text = null, Func<T, bool> isOn = null, string icon = null)
void Stepper(string label, ConfigEntry<float> entry, float step, string format = "0.00", string icon = null)
void Button(string label, Action onClick, string icon = null)
```

- `Toggle` lights while the entry is on.
- `Cycle` steps through an enum's values on each click. `text` names each value, `isOn` says which ones light it.
- `Stepper` shows the value; left click raises it by `step`, right click lowers it, clamped to the entry's
  acceptable range.
- `icon` is the name of any sprite the game has loaded; `StockIcon` lists some.

### Players tab bar

```csharp
bool PlayersBar.Toggle(string owner, string label, ConfigEntry<bool> entry, string icon = null)
bool PlayersBar.Cycle<T>(string owner, string label, ConfigEntry<T> entry, Func<T, string> text = null,
                         Func<T, bool> isOn = null, string icon = null)
```

The same controls in the bar under the players list, beside the game's Admin Raygun button. Keep it to a
switch an admin flips mid round.

### Player row actions

```csharp
bool PlayerRow.AddAction(string owner, string label, StockRowButton icon, Action<int> onClick,
                         Func<bool> visible = null)
```

Adds a button to the Actions block of an expanded player row in the P menu players tab, which only a logged in
admin can open. Buttons from every mod fill a grid below the stock six - Slay, Revive, Slap, Heal, Bring, Go To -
matching their size and spacing, adding rows as needed and growing the row to fit.

- `icon` borrows the symbol of a stock button: `Slay`, `Revive`, `Slap`, `Heal`, `Bring`, `GoTo`, `Message`,
  `Kick` or `Ban`.
- `onClick` gets the network player id of the row's player.
- `visible` hides the button; the grid closes up around it.

These buttons also appear in the kill log actions and on the map's selected player, when those are in use.

### Kill log actions

```csharp
bool KillLog.EnableActions(string owner, Func<bool> visible = null)
```

Turns on actions in the admin kill log: clicking a row opens a panel under it split into the killer and the
victim, each with Spectate, Go To, Bring, Slay, Revive, Heal, Slap and Kick, followed by every mod's player row
actions. Slay, Slap and Kick ask for a second click within four seconds. It is off until a mod asks for it, and
stays on while any mod that asked says yes.

### Key hints

```csharp
bool KeyHints.Add(string owner, string label, Func<KeyCode[]> keys, Func<bool> visible = null)
```

Adds an entry to the key bar the game shows in free roam (beside Admin Raygun) and while spectating (after Report
Player), copied from the game's own entries so it matches them. Several keys share one entry, as `[ / ]`. Keys
the game has no icon for are drawn from one of its own key caps, so they look the same. Hints show only to a
logged in admin.

```csharp
RyLib.KeyHints.Add(Guid, "Spectate Rambos", () => new[] { KeyCode.LeftBracket, KeyCode.RightBracket });
```

### World marks

```csharp
bool World.Layer(string owner, Action<List<WorldMark>> fill, float hz = 10f)
```

Your `fill` is called `hz` times a second with an empty list; add a `WorldMark` for everything to draw. RyLib
keeps what is drawn in step with the list, moves each mark every frame, and removes marks you stop adding.

| `WorldMark` field | |
| --- | --- |
| `Key` | Your id for the mark, unique within your layer. |
| `Follow` | A transform to follow. When `null`, `Position` is used. |
| `Glow` | An object to glow in `Colour`, drawn through walls with the game's own highlighter. When two layers glow the same object, the first one wins. |
| `Colour` | Colour of the ring, beam, label and glow. |
| `Label`, `LabelHeight` | Floating text above the mark, drawn under the P menu rather than over it. `null` for none. |
| `RingRadius`, `BeamHeight` | A ground ring and a vertical beam, in metres. `0` for none. |

### Minimap marks

```csharp
bool Minimap.Layer(string owner, Action<List<MinimapMark>> fill, float hz = 4f)
```

Markers on the game's minimap, built from its own pointer class so they scale, rotate and clamp to the edge
like the game's.

| `MinimapMark` field | |
| --- | --- |
| `Key` | Your id for the mark. |
| `Follow` | The transform it tracks. Required. |
| `Shape` | `Ring`, `Dot` or `Flag`. |
| `Faction` | The side the mark belongs to. It is then drawn blue on your side and red on the enemy's, and the enemy's only shows when the game would show that faction: revealed, last stand or an all charge. `None` always shows it, in `Colour`. |
| `AlwaysShow` | Ignore those reveal rules. Keep this for admins. |
| `Size` | Pixels. `0` for the default. |

### Map pages

```csharp
bool Page.MapLayer(string owner, Action<List<MapMark>> fill)
```

A map page renders an overhead picture of the current map once per round, framed on the spawns, and draws every
player on it by class in their faction's colour. The icons are the game's own: the spawn menu's class glyphs,
the minimap's officer marker and the ability icons. The side panel has a legend of the classes each faction can
spawn with live counts, a search box, zoom and pan, and for a selected player Spectate, Go To, Bring, Teleport To
and Slay, plus every mod's player row actions. A teleport only fires after its button has been pressed, so a
stray click on the map never moves anyone.

A map layer adds your own marks on top:

| `MapMark` field | |
| --- | --- |
| `Key` | Your id for the mark. |
| `OnPlayer`, `PlayerId` | Pin the mark to a player's icon. Otherwise it sits at `Position`. |
| `Shape` | `Ring`, `Dot`, `Flag`, or `Outline`, which draws a coloured outline around the player's own class icon. |
| `Colour`, `Size`, `Tooltip` | `Size` in pixels, `0` for the default. The tooltip shows on hover. |

A map page does its work only while it is open, so it costs nothing otherwise.

### Helpers

- `Factions.Colour(FactionCountry)`, `Factions.Name(FactionCountry)`, `Factions.ClassName(PlayerClass)`: one
  palette and one set of names, so every mod colours a faction the same way.
- `TextIcons.Tag(string gameSprite)`: returns a `<sprite>` tag that draws that game sprite inside any
  TextMeshPro text, tinted by the text colour, or `null` if the sprite is not loaded yet. Ask again later; a
  failed sprite is retried every ten seconds.
- `StockIcon`: names of stock P menu sprites for the `icon` parameters.
- `MapStyle.IconSize` and the `GlowStyle` entries: RyLib's own settings, for a mod that wants to put them on a
  page.

## Settings

`BepInEx\config\com.ryannlt.rylib.cfg`, read live.

| Setting | Default | Effect |
| --- | --- | --- |
| `[Glow] ScaleWithDistance` | `true` | Shrink glow outlines with distance, so a far player's halo is not bigger than they are. |
| `[Glow] OutlineWidth` | `0.6` | Outline thickness. `0` turns it off. |
| `[Glow] Brightness` | `0.7` | Colour multiplier. Above 1 lets the game's bloom make it glow. |
| `[Glow] Glow` | `2` | Halo strength. `0` turns it off. |
| `[Glow] GlowWidth` | `0.04` | Halo width. |
| `[Map] IconSize` | `22` | Size of the class icons on map pages, in pixels. |

## Building

Same as the author's other mods: `build.ps1` compiles against the game's own assemblies and BepInEx from an
r2modman profile, and drops `RyLib.dll` into that profile. `package.ps1` stages a Thunderstore zip. See
[AdminHelper](https://github.com/Ryannlt/AdminHelper#building) for the details, which are identical.

`Icons\*.png` are embedded in the DLL. They are class icons the game does not have, white on transparent at any
size, turned into outlined, tintable icons at runtime. Every other icon is read from the running game, so none
of the game's art is shipped.

## Licence

[MIT](https://github.com/Ryannlt/RyLib/blob/main/LICENSE).
