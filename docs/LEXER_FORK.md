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

The next requested setting change is independent view-distance and smooth
distance-fade controls. It is not implemented by the controls change.
