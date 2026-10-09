# Changelog

## 1.1.0

- **Settings rows.** Sections can hold sliders, dropdowns and text fields, copied from the game's Admin tab:
  `Section.Slider`, `Section.Dropdown` and `Section.TextField`.
- **`Section.ConfirmButton`** needs a second click within four seconds.
- **Admin tab look.** Section pages and the Mods tab use the Admin tab's header with its gold bars, section titles
  have a divider line, and sliders and text boxes sit in the Admin tab's boxes.
- **Hide the Rules tab.** `PMenu.StockTabVisible(owner, StockTab.Rules, visible)` hides the game's Rules tab, and
  the other headers share its space.
- **Map pages.**
  - The image is 8192 pixels on the long side.
  - It covers only the terrains that hold the spawns and capture points.
  - Players, horses and ships are hidden while it is taken.
  - Battle Area frames the live spawn positions, so moved spawns are framed correctly.
- **`TextIcons.Swatch()`** gives a rounded square for color keys.
- **American spelling.** `WorldMark.Colour`, `MinimapMark.Colour`, `MapMark.Colour` and `Factions.Colour()` are
  now `Color`. Mods built against 1.0.0 that use them need a rebuild.

## 1.0.0

First release. Everything a mod can ask for is listed in the README; in short:

- **P menu tabs.** Header tabs with pages of switches, cycles, steppers and buttons bound to config entries, and a
  shared **Mods** tab where each mod gets its own sub tab. Every header, the game's own included, is fitted to
  the space.
- **Players tab.** Switches in the bar beside Admin Raygun, and buttons on an expanded player's admin row, laid
  out in the same grid as the stock ones.
- **Kill log actions.** A click on a kill log row opens actions for both the killer and the victim.
- **Key hints.** Entries in the free roam and spectating key bars, with key caps drawn to match the game's.
- **World and minimap marks.** Rings, beams, labels and a through-walls glow in the world, and markers on the
  minimap that follow the game's own reveal rules.
- **Map pages.** An overhead picture of the battle with every player drawn by class and faction, a class legend
  with live counts, search, zoom, and admin actions that never fire from a stray click.
