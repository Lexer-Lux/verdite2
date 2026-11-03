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

- The plugin owner's Settings adapter now integrates both keys. Preview
  v0.5.3-lexer.1 was downloaded through its pinned installer and privately booted.
- Check the GPU-rendered scene with fade off/on at fixed view distance, then
  change view distance while holding fade fixed. Check sky, water, models and arm.
- Obtain CI results. On 2026-11-03 the fork's Actions API listed zero workflows
  despite the inherited `ci.yml` existing on `main`; enabling it by filename
  returned HTTP 404. No CI pass is claimed and no remote default branch was pushed.

## Classic and Modern setup choices

Both first-run launcher setup and the in-game Run Setup use the new Controls
choice page in onboarding.json version2. Classic uses the exact keyboard layout
from upstream15c54b2 and its runtime's ordinary/analog controller tables, with
separate run disabled. Modern uses this fork's E/Shift keyboard layout, requested
controller face/trigger/bumper bindings, and separate run. Keep current controls
does not replace customized bindings.

A setup choice queues kf2.controls.profile (1 Classic,2 Modern), then the game
consumes it. It is a request, not a second ongoing binding configuration. Thus
selecting the same preset again deliberately resets later custom bindings, while
ordinary startups preserve them. Existing one-time binding migrations still
recognize known defaults only. Classic and Modern both retain the native menu's
Escape and extra Up/Down inputs; compatible secondary layouts do not broaden
migration recognition. F2 remains the editor hotkey.

The isolated tests/controls/ControlsCheck.csproj check uses the real shared
Onboarding.Apply and native Setup live setter. It measured first-run Classic,
both upstream controller tables, preset consumption, reselecting the same preset,
Keep after Classic, Modern bindings/separate run, custom binding preservation and
disk reload. It creates no native window and opens no disc or player data:

```powershell
dotnet run --project tests/controls/ControlsCheck.csproj -c Release `
  --artifacts-path $env:TEMP/lexeditor-dev/kf2-controls/check `
  -p:GameBuild=ABSOLUTE_PRIVATE_GAME_BUILD_DIRECTORY -- `
  ABSOLUTE_SOURCE_DIRECTORY/config/onboarding.json
```

## Look default and enemy-turn compensation

The native gamepad yaw and pitch defaults are x3. Explicit saved values, including
older configurations without an analog-defaults marker, are retained. The old
destructive defaults migration was removed; no bindings, saves or live player
settings are rewritten for this change. Classic/Modern binding choices remain
independent of sensitivity.

The original full-stick sensitivity is x1: yaw is35 angle units per15Hz tick
standing,28 moving, and pitch32. Angles use4096 units per revolution. The actual
hooked native look routine measured x3 yaw105/84 and pitch96; these are below the
x4 overspeed ceiling. Thus the compensation ratio is3/1=3 for this game.

EnemyTurn defaults to x3 speed and x9 acceleration on the existing native turns
toward the player, retaining the native target clamp and zero-speed actions.
The measured chase baseline is speed11/acceleration4: compensation supplies33/36.
The actual hooked native quarter turn takes94 ticks at x1 and32 ticks at x3.
Non-player turns retain their original timing, and byte comparisons confirm no
other creature fields change. Additional standing tracking defaults off; it is a
separate optional AI feature, not part of proportional compensation. Explicit
saved enemy-turn and tracking choices remain intact. The native setup suggests
x3 without extra acceleration and x4 when optional look acceleration reaches the
player's x4 ceiling. Real controller feel and combat balance remain human checks.

The covering tests/controls/Program.cs also measures these native routines in
private RAM and verifies custom sensitivity, bindings and native preference-file
preservation. It creates no game window and does not open a player save.

## Native notification API for source mods

NativeNotifications.Post(owner,glyphs), PostText(owner,text), Flush(cpu,memory)
and Cancel(owner) let independent source mods share the original eight-slot
feedback queue. The API calls native enqueue80033F08 and original font80021E10;
it uses the native toast's location, brightness and lifetime rather than an
ImGui message overlay. Pending messages are capped at512, each text at24glyphs.
Original item-name glyph bytes support the game's font; PostText accepts A-Z and
spaces for English status messages.

IDs232..239 correspond to queue slots because the native controller coalesces
equal consecutive IDs. Cancel compacts the queue, preserving other owners and
vanilla IDs/coin amounts, and cancels its own active fade. Load resets custom
messages. Call Cancel during a source mod's OnUnload. No mod-specific gameplay
policy lives in this API: Better UI posts names and Better Poison owns poison
transitions/settings as independently enabled user-library mods.

Private direct native checks measured queue ordering, owner-scoped cancellation,
coin preservation, eight original-font glyph packets and native brightness, CPU
stack restoration and both source mods compiling through the real ModCompiler.
A private headless source-fork boot loaded Better UI and Better Poison, committed
their1/7hooks and the API hooks, displayed POISONED/POISON CURED through the actual
font routine, and measured a green poison wash. No player data or live session
was changed. GPU appearance remains a human check.
