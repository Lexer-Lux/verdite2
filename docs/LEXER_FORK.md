# Lexer's Verdite2 fork

This fork targets Japanese **King's Field II**, the US King's Field disc
SLUS-00158. It retains Verdite2's local disc recompilation and native source-mod
loader. Releases contain build inputs, never generated game code or disc data.
Launcher update checks use `Lexer-Lux/verdite2`.

## Controls

The native Input menu owns the controls; no player-facing controls mod is needed.
The keyboard default is E to interact, left Shift to run, Space to attack, Q to
cast, and Tab/Escape for the menu. Existing known stock and upstream defaults
migrate once; custom layouts remain unchanged.

Controller defaults use both RT/RB to attack, both LT/LB to cast, Cross to
interact, Circle to run during play and to go back in menus, and Options/Start
to open the menu. Sticks use the port's existing twin-stick implementation.
The controller preset button restores these defaults. L3 in the binding table
is the separate Run action, bound to physical Circle, and may be rebound.

`Separate run input` is independent of the binding tables. Turning it off restores
the game's original shared use/run behavior; it does not erase bindings.
`SeparateRun` scopes the game's use-mask bit to the run check at 8002957C and
suppresses the menu-opening bit at 80029CBC while Run is held. It restores only
the bits it changed, leaving combat, movement and menu logic in the game.

F2 toggles Remaster Editor; Shift+E no longer does. The optional shipped debug
mod uses F10 instead, retaining its other shortcuts.

## Verification so far

The launcher and all game sources compiled on Windows with .NET 10.0.403.
A private headless session booted a copied slot 1 save into fdat02 with no active
mods, and logged both separate-run detours committed. This establishes build
and attachment, not controller feel or complete menu acceptance.

A scratch probe executed the original game's run routine against disc-initialized
RAM. Interact alone did not run; Run and Run+Interact did; disabling separate input
restored vanilla held-Interact running. Every scoped input bit was restored.
Keyboard events opened and closed Remaster Editor on F2; repeated keydown did not
toggle it. Physical controller feel and menu-back presentation remain unaccepted.

The reported consumed Fire Crystal with no learned spell remains under
investigation. No gameplay-mod fix or save repair is claimed here.

## View distance and smooth distance fade

Request: "adding in that separate view distance settings things": a numeric
draw/view distance and a separate, persisted smooth distance-fade switch.

The Settings window's Video > Experimental group now has **View distance (tiles)**
and **Smooth distance fade**. The game's PORT SETTINGS > WORLD page exposes the
same values: numeric half-tile steps, plus an independent OFF/ON row. Existing
values between menu steps remain untouched until edited.

| Setting | Saved key | Default | Effect |
|---|---|---|---|
| View distance | `kf2.renderdistance` | 10.5 | Extends geometry visibility to at most 15 tiles ahead. |
| Smooth distance fade | `kf2.distancefade.on` | False | Dithers GPU-drawn main-view scenery out over the last tile of horizontal distance. |
| Enhancement distance (existing) | `kf2.enhancedistance` | 0 (everywhere) | Limits graphics effects, not geometry visibility. |
| Fog (existing) | `kf2.fog` | 2 | Chooses corner, pixel-depth or eye-distance atmospheric fog. |

The fade uses the renderer's existing `DistanceFade` shader and per-frame
`RetainedScene.SetFade` path, which previously had no port caller. Toggling it
does not change the visibility grid, draw distance, fog, or enhancement distance.
Off publishes zero weights, restoring the previous transition. It deliberately
does not rewrite the atmospheric fog to expose scenery the game hides in darkness.
It is a screen-door dither, not alpha blending or a change to the fog curve.

The draw limit is forward depth in the native cone; the fade is horizontal
distance from the camera. At the sides of the view the fade can therefore hide
geometry before the cone's forward edge. GPU geometry must be active: software
rendering and packet-only fallback faces do not acquire a fade. The renderer
excludes reflections, sky and the first-person arm from this main-view effect.

The upper bound is real: native tile placement carries camera-relative geometry
in signed 16-bit coordinates, with 2,048 units per tile. Extending beyond 15
needs a different geometry path, not a larger slider. The lower bound retains
the game's own 10.5-tile visibility; this change extends that visibility rather
than replacing its flood/culling logic. Non-finite inputs reset to the stock bound.

`KF2_DISTANCEFADE=0|1` (also off/on, false/true) is a diagnostic boot override.
Like other overrides, it locks the corresponding in-game setting. Native menu
Save/Discard and the Settings window use the same two persisted keys.

### Lexeditor integration handoff

The plugin owner should expose the float `kf2.renderdistance` (10.5..15) and bool
`kf2.distancefade.on` (False), using the existing native-settings adapter and shared
Settings screen. Do not map the checkbox to enhancement distance, even fog, or
`kf2.fog`. No gameplay mod or new player-library item is required for these native
settings. This branch contains no plugin edits; it builds on controls commit
`2715edf`, so that controls work must be retained when integrating it.

### Verification

All native game sources compile with .NET 10.0.403 against private generated
sources. `tests/distance` checks finite bounds, independent toggle/distance frame
values, actual renderer CPU weights at the near/middle/far fade band, fallback,
menu live edits/discard, independent writes, disk reload and removal of the fade
key on reset. Run only this check against a private game build:

```powershell
dotnet run --project tests/distance/DistanceCheck.csproj -c Release `
  --artifacts-path $env:TEMP/lexeditor-dev/kf2-distance/check `
  -p:GameBuild=ABSOLUTE_PRIVATE_GAME_BUILD_DIRECTORY
```

An isolated headless New Game reached `fdat02` with disabled memory cards and no
mods. The real settings command changed fade alone, changed distance from 15 to
14.5, toggled fade again without moving distance, then discarded back to 15/on.
The geometry probe measured 11 extra cells per frame at 15, and 10 at 14.5.
Player cards, `settings.json` and `interface.ini` retained their SHA-256 hashes.
Headless did not exercise the GPU shader.

The check opens an ImGui context without any native window and writes only in a
temporary directory, which it removes. It needs the build's native `cimgui.dll`
(the project selects win-x64). The actual DistancePage controls were rendered
offscreen from their ImGui draw lists and inspected. This establishes control
layout, not how the fade looks in a game scene. GPU-rendered gameplay appearance
and a delivered installed candidate remain acceptance work; this branch does not
replace the active installation or modify player saves.

Remaining acceptance:

- Integrate the two keys through the plugin owner's shared Settings adapter.
- Build and deliver the fork through that owner's installation path.
- Check the GPU-rendered scene with fade off/on at fixed view distance, then
  change view distance while holding fade fixed. Check sky, water, models and arm.
- Obtain CI results. On 2026-11-03 the fork's Actions API listed zero workflows
  despite the inherited `ci.yml` existing on `main`; enabling it by filename
  returned HTTP 404. No CI pass is claimed and no remote default branch was pushed.
