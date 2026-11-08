# Changelog

## 1.4.3 — 2026-11-08

AFK guard: stops shooting into cover. In the reported raid (Streets, M4A1 with 6x scope, M995) 86 of 121 guard shots hit an obstacle first and only 5 of those still hit the target; shots with a really clear path hit 24 of 35.

- **The guard checks the bullet's flight path, not a straight ray to the aim point.** The ray used to end at the aim point *without* lead, while the bullet flew to the lead point (up to 1.5 m to the side at 230 m) - e.g. into a tram the check ray missed by 0.7-1.4 m. The check now runs from the muzzle to the lead point along the arc the bullet flies above the straight line (bullet drop compensation).
- **Only see-through material counts as clear for the guard** (glass, chain-link fence, grates, grass, fabric, cardboard ...). By the game's penetration rule a 5.56 round "goes through" car bodies (MetalThin), garbage containers (Plastic) and wooden walls, but in all recordings the target was hit afterwards in only 16 % (MetalThin), 18 % (WoodThick) and 0 % (Plastic) of such shots - fragmentation, deflection and several wall layers are not part of that rule. Bots behind such cover are no longer engaged; the guard holds fire and logs `AFK guard: holding fire at ... - flight path covered by ...`. The normal aim assist keeps the game rule ("Ignore shoot-through obstacles").
- **"Aim tolerance (cm)" now really is centimetres at the target.** A hidden minimum of 0.05° turned 10 cm into 21 cm beyond 115 m; at 230-250 m the guard fired with the line 10-21 cm off. Up to 115 m nothing changes.
- Tests: offline tests for the flight path (with four real shots from the raid) and the tolerance.

## 1.4.2 — 2026-11-08

- **ESP: boxes were offset (too low / towards the bottom-left) with upscalers.** FSR (e.g. FSR4Bridge), DLSS mods and the game's own upscaling shrink `camera.rect` to the render share (e.g. 0.59 × 0.59) and then stretch the image to the full screen. The ESP used that shrunk rect and squeezed all boxes into the lower-left part of the screen. It now detects a uniformly shrunk rect anchored at 0/0 and maps to the full screen; real letterboxing still works as before.
- ESP draws with the identity `GUI.matrix` (another mod may leave it scaled) and writes one diagnostic line at the first draw (screen and camera resolution, camera rect, FOV, GUI matrix) to the BepInEx log, so setups that still misplace boxes can be identified from the log.
- **Aim assist: the hold magnet let go while the mouse was still.** With a target crossing the view (especially through magnified scopes) the "magnet yields to own mouse" check compared the movement *relative to the target*, so a still hand counted as "steering away" and the line drifted behind the head. The magnet now only yields when your own mouse actually moves faster than the threshold.

## 1.4.1 — 2026-11-07

Fixes "hopping" of the aim point and of the target.

- **Aim point stays put (aim assist and AFK guard).** With a covered head, "Fall back to chest" used to flip the aim point between head and chest whenever the line of sight flickered at a cover edge. Head to chest now switches only after the head was covered for 0.2 s in a row, and back to the head only after it was free for 0.35 s in a row. The chosen head box (HeadCommon / Eyes / Jaw ...) is held as long as it stays free and is only re-chosen when it is blocked.
- **Soft Aim by mouse: zone visibility is debounced.** A hit zone (head, torso, pelvis, legs) now counts as covered only after 0.25 s without a free line and as free again after 0.3 s, so the pull no longer swaps between head and torso frame by frame.
- **Target no longer dropped for a moment of cover.** The held target of the aim assist stays a candidate for 0.5 s after it disappears behind an edge, instead of switching to the next enemy and back.
- **AFK guard: sticky target.** A target is kept while it lives and is an attacker or in range; it is only given up after the line of fire has been blocked for longer than the new setting **Keep target while hidden (s)** (default 0.75). An attacker is never swapped for a non-attacker. Switching happens only when a new attacker appears while the current target is not one, or when another target of the same kind is less than half as far away. A target may drift 10 % beyond the range before it is dropped.
- **AFK guard: bursts stay on one point.** While the trigger is held the head/chest choice is frozen, and when the head is blocked the guard keeps the chest until the head is free again for a while.
- Tests: new offline tests for target priority (the fight from the report), head box hysteresis and zone debouncing.

## 1.4.0 — 2026-11-07

- New **AFK guard** (section **"7 AFK Guard"**), an automated defense mode for offline single player: press **Pause** to switch it on before you step away, press it again when you are back. While on, you stay where you are and the mod aims and fires by itself - first at anyone who hit you or shot close past you (remembered for 30 s), then at every hostile bot within 25 m with a clear line of fire. Same aim point as the aim assist (armor-based zone, lead, bullet drop), bursts for full-auto, single shots for semi-auto, automatic reload with the game's own reload, optional aiming down sights.
- The trigger is always released when the guard is switched off, loses its target, an inventory/menu is open or the game is paused; it switches itself off on death and at the end of the raid. Notification plus a status line at the top of the screen while it runs.
- The guard's hotkey is visible in the reduced Quick Setup view. The normal aim assist pauses while the guard runs.
- It does not move, heal, throw grenades or fix malfunctions.

## 1.3.0 — 2026-11-07

- New first F12 section **"0 Quick Setup"** for people who find the ~140 settings too much: **Enabled**, **Strength** (Light / Medium / Strong / Custom), **Aim at** (Smart / Head / Body / Custom), **Enemy ESP** and **Show all settings**.
- Strength and Aim at only write to the existing settings (no second set of values). Changing one of those values by hand switches the choice to the matching preset or Custom automatically.
- "Show all settings" off hides everything except Quick Setup and a few essentials. Default: off on fresh installs, **on** for existing installations so nothing disappears.
- Existing configs are never changed: on the first start the mod only detects whether the current values equal Light/Medium/Strong, otherwise Strength shows Custom. The old "1 General / Enabled" and "6 ESP / Enabled" values move to "0 Quick Setup" (Enabled, Enemy ESP).

## 1.2.0 — 2026-11-07

- Everything in English: F12 menu (142 settings), descriptions, notifications and log lines.
- Settings from 1.1.0 (German names) are migrated automatically on first start.
- New: **Enemy ESP** (sections "6 ESP" and "6b ESP Appearance", off by default, toggle with Insert): boxes/corner boxes/markers, type and boss names, distance, health bar, head dot, per-type colours, occluded enemies dimmed. Correct placement with upscalers (FSR) via the non-jittered projection.
- The combat recording folder is now `BepInEx/Zielhilfe-Recordings`.

## 1.1.0 — 2026-11-07

- First public release (SPT 4.1.6). Combat recording off by default.
