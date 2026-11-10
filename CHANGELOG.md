# Changelog

## 1.4.7 — 2026-11-09

The aim assist no longer acts on enemies hidden behind cover a bullet could penetrate. Reported: "The aim assist should not shoot through hidden obstacles."

- **Cause:** visibility was defined by the bullet. Materials a bullet passes (fabric, cardboard, paper, generic soft material) counted as "see-through", and with the game's penetration rule thin sheet metal, wooden boards and plastic counted as clear as well. The assist pulled to - and the AFK guard fired at - enemies behind tarps, tents, cardboard, thin metal and fences you could not see.
- **New: "Only visible targets (cover blocks even if penetrable)"** (section "2 Target", default on). Visibility is now optical: every opaque obstacle hides. See-through are only clear glass (frosted glass, recognised by its render material name, hides), broken glass, chain-link fence and grates (they have gaps), open windows (nothing there), low grass, and - as in 1.4.6 - bushes, tree crowns and tall grass by depth. The game's "TransparentCollider" layer stays outside the check, because the game's bots see through it as well. At least one of head, chest or pelvis must be visible for the assist to pull.
- **AFK guard:** fires only at a visible enemy. It now checks both: the aim point is visible (same optical rule as the aim assist, incl. foliage) and the bullet's flight path is clear (unchanged from 1.4.3). If not, it holds fire and shows "holding fire, target not visible"; the log says `AFK guard: holding fire at ... - not visible: covered by ...`.
- **ESP:** the visible/covered display uses the same optical rule (head, chest or pelvis).
- Off = old behavior (penetrable cover counts as clear, "Shoot-through from probability" applies).
- Performance unchanged: same rays, results cached per frame; the frosted-glass check runs once per glass collider.

## 1.4.6 — 2026-11-09

Bushes and foliage now hide enemies from the aim assist. Reported: "The aim assist also pulls through bushes where I can't see the enemy."

- **Cause:** the line-of-sight ray only checked the layers bullets travel on and ignored trigger colliders. Bushes and tree crowns are trigger volumes on the game's "Foliage" layer (the same ones that make the rustling sound), so the ray never saw them. Tall-grass colliders on the bullet layers were rated "see-through" because a bullet passes them.
- **New: "Bushes and foliage block aim assist"** (default on). The check now also casts against the Foliage layer, exactly like the game's bots do for their own vision (`HighPolyWithTerrainMaskAI`, foliage counted by the length of the line inside the bush). Colliders made of tall-grass material count the same way. Low grass on the ground (Grass layer) never blocks - for bots it never fully blocks either.
- **New: "Foliage depth that hides (m)"** (default 0.3): how much foliage the line to a body point may cross before it counts as covered. Your own bush (the one you stand in) never blocks.
- **Head, chest or pelvis:** an enemy is a candidate if at least one of the three is clearly visible (pelvis is new). Zone choice already checks every zone on its own, so the assist only pulls to a zone you can actually see.
- Performance: one extra ray per check, and only when no solid cover was found first. Results are cached per frame, so the same line checked twice in one frame (held head box, zone choice) costs no new ray.
- Unchanged: the ESP's visible/covered display and the AFK guard (it checks the bullet's flight path, and a bullet goes through a bush).

## 1.4.5 — 2026-11-08

Soft Aim by mouse: **free zone inside the hit zone.** Reported: "I tried to correct myself, but got pulled back." The hold magnet always pulled the shot line to the centre of the zone, even when it already was on the head.

- **New: "Free movement inside hit zone"** (default on). While the predicted impact (real shot line incl. lead and bullet drop) lies inside the desired hit zone minus a safety margin, pull, friction and magnet no longer act against your own mouse movement. Towards the zone edge they fade back in smoothly (no jerk), outside the zone they work as before. Target tracking stays (difference only, as before).
- **Hold point instead of zone centre.** Inside the free zone the magnet holds the line at the point *you* put it: the hold point moves with your own mouse movement (not with the assist or recoil compensation) and stays inside the free zone. Recoil and sway are not part of your mouse movement, so the magnet still pulls them back - to your point, not to the centre.
- **New: "Free zone inner margin (%)"** (default 15): margin in percent of the zone width, at least 2 cm at the target, at most 90 % of the half width.
- Simulation (magnet 14 °/s, 0.05 s, head per shot off -> on): standing target 30 m, still mouse, sustained fire 98 -> 96 % (without hold point it would be 92 %); single shots 98 -> 94 %. Trade-off: hand tremor counts as own movement and is no longer centred inside the free zone - bots 30 m with 5 °/s tremor 25 -> 17 %, with 2 °/s 55 -> 42 %; 80 m unchanged (6 %). A larger margin gives part of it back (30 %: 18 / 47 %, 45 %: 21 / 49 %).
- Diagnostics: the Soft Aim line shows `free zone <0..1> (<x> % of frames, 5 s), hold point <angle> from aim point`; shot lines show `free zone <0..1>`.
- Tests: offline tests for the free zone share, own small movement without counter-pull (old: 0.40 °/s back to centre, new: 0), magnet at the edge/outside, and still mouse with recoil and sway.

## 1.4.4 — 2026-11-08

Soft Aim by mouse: target tracking follows the aim point instead of an old speed. Reported case (Lighthouse, AR with 6x scope, a bot sprinting across the view at 89-95 m): 8 shots, 4 hits; of the 4 misses 2 stopped in thick metal in front of the target and 2 flew past.

- **Target tracking no longer keeps turning after the target brakes or turns.** It used the lead speed, which is already smoothed over 0.12-0.15 s, and smoothed it a second time (0.08 s): about 0.23 s old in total. When the bot braked and reversed, tracking kept turning 0.3 s in the old direction (+2.4 to +0.7 °/s) while the hold magnet pulled against it (-2.6 °/s); the line stayed 0.15-0.19° (25-30 cm) ahead of the aim point and the shot missed by 43 cm. It also ignored that the aim point moves when the lead itself grows or shrinks. Tracking now uses the target's current speed plus the rate of change of the lead (only one smoothing, jumps when the lead switches on/off are left to the magnet). Replayed on all recordings of the day: line-to-aim-point error (RMS) 10.1 -> 9.0 cm, frames more than 10 cm off 15 % -> 8 %; this fight 9.9 -> 6.8 cm, the missed shot's tracking error 25 -> 8 cm.
- Checked and unchanged: the lead itself has no systematic error (head movement during the flight vs. ballistic lead, 31 shots at moving targets: mean -1.0 cm; the remaining scatter, about 1/3 of the lead, is the bot changing speed during the 0.1 s flight), bullet drop is right (vertical error at 89-95 m within ±9 cm).
- Impact log/recording: a body hit that came after four other impacts (barricade, fragment, two chain-link fences, then the chest plate) was dropped and the shot was logged as a miss. The first body hit of the chain is now always kept.
- Diagnostics: the line after `limit` now also shows the limit that really applied to the nearest enemy (in Meters mode it depends on the distance, e.g. `limit 1.00° at 95 m = 1.66 m, inside`).
- Tests: offline tests for the lead change rate, the impact chain and the limit text.

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
