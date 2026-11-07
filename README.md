# Zielhilfe (aim assist)

A configurable single-player aim assist for SPT 4.1.6 (BepInEx client plugin): it nudges your own mouse movement toward enemy hit zones, with bullet-drop and lead compensation computed from the loaded round.

> **WARNING - single-player only. This is a cheat-like mod.**
>
> - Use it **only in offline SPT raids against AI bots**.
> - **Never** use it in live Escape from Tarkov or on any server with real players. That is cheating against real people and will get your account banned.
> - The mod reads enemy positions and armor, so it behaves like an aimbot-lite by design. The default "Soft Aim" never moves the camera on its own; it only bends the mouse movement you make yourself (see below).

Interface language: the F12 settings menu is in German (section and entry names appear exactly as listed in the tables below, written without umlauts). This README gives the English meaning.

## How this mod was made (AI disclosure)

This mod was **programmed by Claude**, an AI assistant made by Anthropic.
**Kaan was the tester**: Kaan has no programming background, played with the mod
on SPT 4.1.6, reported what went wrong, and Claude fixed it. No human programmer
has reviewed the code. It is open source so anyone can check it — if you find a
bug, please open an [issue](https://github.com/Kaanori/spt-zielhilfe/issues).

## Requirements

- SPT 4.1.6 (EFT 0.16.9). BepInEx ships with SPT.

## Installation

1. Download `Zielhilfe-1.1.0.zip` from the [Releases](https://github.com/Kaanori/spt-zielhilfe/releases) page.
2. Extract it **into your SPT game folder** (the folder that contains `EscapeFromTarkov.exe`).
   The ZIP already has the right layout; the mod ends up in `BepInEx/plugins/`.
   On Windows, make sure "Extract All" does not add an extra subfolder.
3. Restart the game.

## Uninstall

Delete the mod's files from `BepInEx/plugins/` (settings: `BepInEx/config/` for client mods) and restart the game.

## Features

### Targeting

- **Enemy filter.** By default only bots that the game itself considers hostile to you are used (the game's own `BotsGroup` / `IsPlayerEnemy` logic: side, karma, boss loyalty, friend/warn/enemy types). Friendly scavs are ignored until they turn hostile. Scavs, PMCs and bosses/guards can additionally be excluded individually.
- **Range and selection.** Enemies beyond a configurable range are ignored. The current target is kept until another enemy is clearly better (a switch margin in pixels), which stops the assist from flickering between two enemies standing next to each other.
- **Line of sight.** Enemies behind walls are skipped. The visibility ray uses the head box (or body point) that is actually reachable. By default the ray only checks the physics layers that bullets use, so invisible visibility-control cubes do not block it.
- **Shoot-through handling (glass, fences, foliage).** Glass, wire mesh, grates, tall grass and similar are not treated as obstacles. The decision comes from the game's own material and penetration data for the collider that was hit, evaluated for your currently loaded cartridge (penetration vs. material level, plus chance values). Other materials (wood, sheet metal, plastic ...) count as passable if the computed probability is above a threshold. Bodies never count as passable.
- **Zone choice by armor ("Zone nach Panzerung").** For each enemy the worn armor is queried and the zone with the best expected result is chosen, using the same calculation as the game (shot match plus penetration chance from armor class, durability and the cartridge's penetration), taking the enemy's facing into account (front/back plates, sides). Zones are weighted: head 1.0, torso 0.6, pelvis 0.25 (legs lower still). Zones can be individually allowed or disallowed; pelvis and legs act only as fallbacks when head and torso are covered or cannot be penetrated.
- **Head boxes.** The head in EFT consists of eight hit boxes. The mode "visible" checks each box individually and picks the first one with a free line of sight (order of preference: head centre, face, jaw, parietal/top, back of head, ears, neck front, neck back; with armor-based selection the least armored free box comes first). Alternatively a fixed box can be forced. Inside the box you choose the aim height (0 = bottom, 1 = top).
- **Fallback to the chest** when no head box is free (only without armor-based zone choice).

### Aiming / Soft Aim (default mode)

Three modes exist ("Art der Hilfe"):

1. **SoftaimMaus (default, recommended)** - real soft aim. The assist never turns the view by itself on its own initiative. It intercepts your mouse input (a Harmony patch on the game's move/look input translator) and bends it slightly toward the target while your weapon's real bullet line is close to a hit zone. Details:
   - **Capture radius.** Measured from the *edge* of a hit zone as an angle, either as a distance at the enemy in metres (converted with the enemy's distance, with a minimum and maximum angle) or as a fixed angle in degrees. Effect fades to zero toward the radius edge.
   - **Attraction (weight).** Result = w x target vector + (1 - w) x mouse movement. The assist never reverses your movement: pulling away from the target always works, and the pull relaxes when your own movement already reaches the target point and fades to zero toward the zone centre, so nothing snaps or sticks.
   - **Friction.** Slows the part of your movement that goes across or away from the target while inside the radius.
   - **Correction limit.** A cap in degrees per second (frame-rate independent) on attraction plus friction.
   - **Magnet (hold without mouse).** The only part that also acts while the mouse is still: it slowly pulls the actual bullet line back to the aim point to counter sway and recoil. It uses the real flight direction of the bullet, so recoil is already included. It yields to your own mouse movement (decay when you steer faster than a threshold, measured on screen including scope zoom) and has its own catch-up time.
   - **Catch-up boost.** When the bullet line enters the capture area (also right after a target change) the magnet may pull harder for a short time. It aborts when you pull away.
   - **Follow-through ("Mitdrehen") for moving targets.** Turns along with a moving target, but only the *difference* between the target line's angular velocity and your own turn rate (both smoothed), so it adds nothing if you already track. It is limited to the capture area and releases when you pull away from the zone.
   - **Release logic.** The assist sums up how far you move the mouse away from the wanted zone; above a threshold magnet, follow-through and boost switch off until the sum falls again. It re-engages after the mouse has been calm for a short time.
   - **Zoom scaling.** Corrections are in degrees, which look bigger through a scope; optionally the boost, follow-through and attraction caps are divided by the magnification.
   - **Weapon classes.** Pistol, SMG, assault rifle/carbine, marksman/sniper rifle, shotgun and machine gun can each have their own capture/magnet values ("1c" to "1h"). Marksman/sniper and shotgun ship with their own values enabled. Grenade launchers are excluded.
   - **Recoil compensation (optional).** Subtracts newly added weapon recoil from your mouse movement while firing, independent of a target. Off by default.
2. **AltesSoftaim (legacy)** - every frame a capped turn toward the target, independent of the mouse. Uses the "9 Alte Arten" settings.
3. **AltesFesthalten (legacy)** - the view direction is set onto the target every frame. Uses the "9 Alte Arten" settings.

The legacy modes also have: own-mouse priority (the assist lets go when you turn fast and stays off for a short time afterwards), a dead zone, tracking of moving targets, muzzle re-regulation (measures where the *muzzle* really points and regulates the remaining offset away), and hip-fire offset compensation. These settings are hidden unless "Advanced settings" is enabled in the F12 menu.

### Ballistics: lead and bullet drop

- **Bullet drop.** The aim point is raised by the amount the bullet falls on the way to the target. The trajectory is computed step by step, replicating the game's own player-bullet solver (`TrajectoryCalculator.NextForPlayer`, using the game's G1 drag function with the cartridge's mass, diameter, ballistic coefficient and initial speed times the weapon's speed factor). The drop is computed against the distance your sight is currently zeroed to. If the muzzle is what gets aimed (Soft Aim aims the bullet line), the zero's built-in elevation is already cancelled and the full drop is applied; if the sight is aimed, only the remainder is added.
- **Lead (target lead).** For moving enemies the aim point is moved to where the enemy will be when the bullet arrives, using the computed flight time. The enemy's velocity is smoothed (with a lower bound independent of flight time), only applied above a minimum speed, capped in metres, optionally horizontal only, and reduced when the enemy changes direction sharply. An additional term compensates your own frame latency (one frame ahead by default).
- **Fire point.** Angles are computed from the weapon's fire point (muzzle), not the camera, so parallax at close range is accounted for. Consequence: at close range the crosshair can sit visibly beside the enemy while the shot lands.

### Visibility and obstacles

See "Line of sight" and "Shoot-through handling" above. Hit-point checks use the game's ballistic colliders, not guessed lists.

### Hotkey

One configurable key (default Left Alt) used only when "Wann sie hilft" is set to hold or toggle (and for the legacy modes). It can be a keyboard key or a mouse button. With the default setting ("Immer") no key is needed.

## Controls

| Control | Default | Effect |
|---|---|---|
| "Taste" (F12 menu) | Left Alt | Hold or toggle the assist, depending on "Wann sie hilft" (Soft Aim) or "Wann sie zieht" (legacy modes). Not needed in the default always-on mode. |
| F12 (BepInEx Configuration Manager) | - | Opens the settings; enable "Advanced settings" to see the legacy sections. |

There are no other hotkeys. The mod also needs no extra overlay.

## Settings

Values were taken from the source. Entry names are shown as in the German F12 menu (written without umlauts).

### 1 Allgemein (general)

| Entry | Meaning | Default | Range |
|---|---|---|---|
| Eingeschaltet | Master switch. Off = the mod does nothing. | on | on/off |
| Taste | Key or mouse button for hold/toggle activation. | LeftAlt | any KeyCode |

### 1b Soft Aim per Maus (main section)

| Entry | Meaning | Default | Range |
|---|---|---|---|
| Art der Hilfe | Mode: SoftaimMaus, AltesSoftaim, AltesFesthalten. | SoftaimMaus | enum |
| Wann sie hilft | When active: Immer (always), TasteHalten (hold key), TasteUmschalten (toggle). Always is harmless since nothing happens without mouse movement. | Immer | enum |
| Nur im Anschlag (ADS) | Only when aiming down sights. Off = also from the hip (calculated with the weapon's bullet direction, not the camera). | off | on/off |
| Zone nach Panzerung waehlen | Choose the hit zone by armor and enemy facing (see Features). Off = fixed body part from "2 Ziel". | on | on/off |
| Erlaubte Zone: Kopf | Allow pulling onto the head. | on | on/off |
| Erlaubte Zone: Rumpf | Allow pulling onto the torso. | on | on/off |
| Erlaubte Zone: Becken | Allow pelvis. Off = pelvis only as fallback. | off | on/off |
| Erlaubte Zone: Beine | Allow legs. Off = legs only as last fallback. | off | on/off |
| Nur Feinde | Only enemies according to the game's own hostility logic. "3 Wen auslassen" applies additionally. | on | on/off |
| Rueckstoss-Ausgleich quer (Prozent) | Subtracts newly added sideways weapon recoil from your mouse movement while firing, independent of any target. 0 = off (you probably already pull against recoil; both together would over-correct). | 0 | 0-100 |
| Rueckstoss-Ausgleich hoch (Prozent) | Same for the vertical component. | 0 | 0-100 |
| Radius-Art | How the capture radius is specified: Meter (distance at the enemy, converted by distance) or Grad (fixed angle). | Meter | enum |
| Radius in Metern ab Zonenrand | Capture radius in metres beyond the zone edge (Meter mode). | 0.75 | 0.05-5 |
| Radius hoechstens x Grad | Upper angle limit for close range (Meter mode). | 12 | 1-30 |
| Radius mindestens x Grad | Lower angle limit for long range (Meter mode). | 1 | 0.05-5 |
| Radius in Grad | Fixed radius angle (Grad mode), measured from the zone edge. | 3 | 0.2-15 |
| Anziehung (Gewicht) | Weight w of the pull toward the target inside the radius. 0 = off, 1 = movement points fully at the target. | 0.5 | 0-1 |
| Reibung | Friction on movement across/away from the target inside the radius. 0 = off. | 0.3 | 0-1 |
| Korrektur hoechstens x Grad je Sekunde | Cap for attraction plus friction, in degrees per second. | 120 | 1-600 |
| Halten ohne Maus (Magnet) hoechstens x Grad je Sekunde | Magnet strength: pulls the bullet line back to the aim point even when the mouse is still. 0 = off. | 10 | 0-60 |
| Magnet Nachziehzeit in Sekunden | How fast the magnet removes a remainder (about two thirds gone after this time). Smaller = firmer. | 0.05 | 0.02-1 |
| Magnet weicht eigener Maus ab x Grad je Sekunde (Bildschirm) | Above this own on-screen turn rate the magnet no longer holds against your movement. 0 = off. | 10 | 0-100 |
| Mitdrehen Anteil | Share of the follow-through (target line turn rate minus your own turn rate). 0 = off. | 0.7 | 0-1 |
| Mitdrehen hoechstens x Grad je Sekunde | Cap for the follow-through. | 60 | 0-200 |
| Mitdrehen: Hilfe-Drehung nicht als eigene zaehlen | Do not count the rotation made by the assist itself as your own when comparing turn rates (so it can follow walking enemies at full speed). | on | on/off |
| Einfang-Schub Dauer (s) | Duration of the capture boost after the bullet line enters the capture area. | 0.3 | 0-1 |
| Einfang-Schub hoechstens x Grad je Sekunde | Magnet cap during the boost. 0 = off. | 40 | 0-150 |
| Deckel durch Zoom teilen | Divide boost, follow-through and attraction caps by the scope magnification. The magnet itself is not divided. | on | on/off |
| Loesen nach Weg (Grad) | If you move the mouse this far (summed) away from the wanted zone, magnet, follow-through and boost switch off until the sum halves. 0 = off. | 2 | 0-10 |
| Nach Loslassen wieder greifen nach x Sekunden | After a release by your own movement, re-engage once the mouse has been calm for this long. 0 = off. | 0.06 | 0-0.5 |

### 1c to 1h Klasse ... (per weapon class)

Sections: "1c Klasse Pistole", "1d Klasse MP", "1e Klasse Sturmgewehr und Karabiner", "1f Klasse DMR und Scharfschuetze", "1g Klasse Schrotflinte", "1h Klasse MG". Each has the same entries; the class values replace the general values from "1b" only when "Eigene Werte statt allgemeiner" is on.

| Entry | Meaning | Default (all classes unless noted) | Range |
|---|---|---|---|
| Eigene Werte statt allgemeiner | Use this class's own values instead of "1b". | off; **on** for DMR/sniper and shotgun | on/off |
| Radius in Metern ab Zonenrand | As in 1b. | 0.75 (DMR/sniper 0.5, shotgun 1.0) | 0.05-5 |
| Anziehung (Gewicht) | As in 1b. | 0.5 | 0-1 |
| Reibung | As in 1b. | 0.3 (DMR/sniper 0.4) | 0-1 |
| Halten ohne Maus (Magnet) hoechstens x Grad je Sekunde | As in 1b. | 10 | 0-60 |
| Magnet Nachziehzeit in Sekunden | As in 1b. | 0.05 | 0.02-1 |
| Mitdrehen Anteil | As in 1b. | 0.7 | 0-1 |
| Mitdrehen hoechstens x Grad je Sekunde | As in 1b. | 60 | 0-200 |
| Einfang-Schub Dauer (s) | As in 1b. | 0.3 | 0-1 |
| Einfang-Schub hoechstens x Grad je Sekunde | As in 1b. | 40 | 0-150 |

### 2 Ziel (targeting)

| Entry | Meaning | Default | Range |
|---|---|---|---|
| Reichweite in Metern | Enemies farther away are ignored. | 930 | 10-1500 |
| Koerperstelle | Preferred body part: Kopf, Brust, Becken, Beine. With armor-based zone choice it only determines the point where visibility is checked. | Kopf | enum |
| Kopf-Trefferbox | Which head box to aim at: Sichtbar (first free box), Kopfmitte, Gesicht, Kiefer, Schaedeldecke. | Sichtbar | enum |
| Hoehe in der Kopfbox | Aim height inside the box (0 bottom, 0.5 middle, 1 top), scaled to the box's real height. | 0.72 | 0-1 |
| Auf die Brust ausweichen, wenn der Kopf zu ist | Aim at the chest when no head box is free (only without armor-based zone choice). | on | on/off |
| Nur freie Sicht | Skip enemies behind walls. | on | on/off |
| Durchschiessbares ignorieren | Glass, mesh, grates, tall grass etc. do not count as obstacles (game's material/penetration data). | on | on/off |
| Durchschuss ab Wahrscheinlichkeit | Probability above which other materials count as passable for your loaded cartridge. | 0.5 | 0-1 |
| Nur Ebenen pruefen, auf denen Kugeln fliegen | Check only physics layers bullets use for the line of sight. | on | on/off |
| Wechselvorsprung in Pixeln | A different enemy must be better by this many pixels before the target switches. 0 = always the nearest. | 40 | 0-300 |

### 3 Wen auslassen (who to skip)

| Entry | Meaning | Default |
|---|---|---|
| Scavs | Ignore scavs. | off |
| PMCs | Ignore PMCs. | off |
| Bosse und Leibwaechter | Ignore bosses and guards. | off |

### 5 Vorhalt (lead and bullet drop)

| Entry | Meaning | Default | Range |
|---|---|---|---|
| Kugelfall ausgleichen | Compensate bullet drop against the sight's zero distance. | on | on/off |
| Eingeschaltet | Lead on/off. Off = always aim directly at the target. | on | on/off |
| Faktor | Multiplier on the computed flight time (1 = unchanged). Raise it if you shoot behind. | 1 | 0-3 |
| Hoechstens x Meter | Cap on the lead distance. | 4 | 0.5-20 |
| Erst ab x m/s | Minimum enemy speed for lead. | 1.2 | 0-5 |
| Beruhigung (Sekunden) | Averaging time for the enemy's measured velocity. | 0.15 | 0.02-1 |
| Glaettung mindestens x Sekunden | Lower bound for the smoothing, independent of flight time. | 0.06 | 0-0.5 |
| Bildverzug ausgleichen (Bilder) | Compensate your own frame delay by leading this many frames. 0 = off. | 1 | 0-3 |
| Nur waagerechte Bewegung | Ignore vertical motion (walking bob). | on | on/off |
| Bei Richtungswechsel zuruecknehmen | Reduce the lead when the enemy turns sharply. 0 = off, 1 = lead vanishes on a hard turn. | 0.7 | 0-1 |

### 8 Diagnose (diagnostics, off by default)

| Entry | Meaning | Default | Range |
|---|---|---|---|
| Diagnose ins Protokoll | Writes diagnostic lines to the BepInEx log (plus a shot line and hit line per own shot and impact analysis lines in Soft Aim). For troubleshooting only. | off | on/off |
| Diagnose-Takt in Sekunden | Interval of the diagnostic line; 0 = every frame (the log grows fast). | 1 | 0-5 |
| Kampf-Aufzeichnung | Records every engagement frame by frame to disk (see below). | off | on/off |
| Aufzeichnung hoechstens x MB je Raid | Size cap per raid. | 200 | 10-5000 |
| Aufzeichnung: nur die letzten x Raids behalten | Older raid folders are deleted when a new raid starts. | 20 | 1-500 |
| Gefecht beginnt, wenn ein Feind dich sieht, bis x Meter | Distance up to which an enemy that has you as a target and sees you starts an engagement. | 150 | 10-1000 |

### 9 Alte Arten (legacy modes, only visible with "Advanced settings")

Used only when "Art der Hilfe" is AltesSoftaim or AltesFesthalten (except where noted).

| Section | Entry | Meaning | Default | Range |
|---|---|---|---|---|
| 9 Alte Arten - Allgemein | Wann sie zieht | TasteHalten / TasteUmschalten / Immer. | TasteHalten | enum |
| 9 Alte Arten - Ziel | Blickfeld in Pixeln | Max. distance of an enemy from screen centre in pixels. | 61 | 10-600 |
| 9 Alte Arten - Ziel | Blickfeld in Grad | Same as an angle (zoom independent, preferred). 0 = use pixels. | 3 | 0-30 |
| 9 Alte Arten - Ziel | Zielwinkel vom Feuerpunkt statt von der Kamera | Compute the aim angle from the fire point instead of the camera. | on | on/off |
| 9 Alte Arten - Bewegung | Nachziehgeschwindigkeit | Pull speed toward the target (1/s, frame-rate independent). | 60 | 2-200 |
| 9 Alte Arten - Bewegung | Softaim hoechstens x Grad je Sekunde | Cap in AltesSoftaim. | 80 | 10-600 |
| 9 Alte Arten - Bewegung | Totzone in Grad | No further correction below this deviation. | 0.05 | 0-1 |
| 9 Alte Arten - Bewegung | Eigene Maus hat Vorrang | Assist lets go when you turn fast. | on | on/off |
| 9 Alte Arten - Bewegung | Vorrang ab x Grad je Sekunde | Own turn rate that triggers the release. | 50 | 5-400 |
| 9 Alte Arten - Bewegung | Danach x Sekunden nicht ziehen | Time the assist stays off after your last turn. | 0.25 | 0-2 |
| 9 Alte Arten - Bewegung | Mitdrehen bei bewegten Zielen | Follow-through strength (1 = lag fully removed). | 1 | 0-1.5 |
| 9 Alte Arten - Bewegung | Mitdrehen beruhigen (Sekunden) | Averaging time of the measured turn rate. | 0.08 | 0.02-0.5 |
| 9 Alte Arten - Bewegung | Mitdrehen hoechstens x Grad je Sekunde | Safety cap for the follow-through. | 150 | 10-400 |
| 9 Alte Arten - Rueckstoss | Rueckstoss ausgleichen | Shift the view so the muzzle (not the camera) points at the target; ADS only. 1 = full. | 0.8 | 0-1 |
| 9 Alte Arten - Rueckstoss | Beruhigen (Sekunden) | Smoothing of weapon animation jitter. | 0.04 | 0.005-0.3 |
| 9 Alte Arten - Rueckstoss | Muendung nachregeln | Measure where the muzzle really points and regulate the remainder away (closed loop). | on | on/off |
| 9 Alte Arten - Rueckstoss | Nachregeln wie schnell | Speed of that regulation (1/s). | 6 | 0-30 |
| 9 Alte Arten - Rueckstoss | Nachregeln hoechstens x Grad | Cap of the accumulated correction. | 2 | 0.1-8 |
| 9 Alte Arten - Rueckstoss | Hoechstens x Grad | Safety cap of the recoil compensation. | 3 | 0.5-10 |
| 9 Alte Arten - Rueckstoss | Aus der Huefte ausgleichen | Hip-fire offset compensation (0 = none, 1 = full). | 1 | 0-1 |
| 9 Alte Arten - Rueckstoss | Huefte hoechstens x Grad | Cap for hip compensation; if the offset exceeds twice this, nothing is compensated. | 8 | 0-20 |

## Compatibility

- Works standalone. Requires only **SPT 4.1.6** and BepInEx (which SPT ships with). The F12 menu needs the BepInEx Configuration Manager (normally already part of an SPT install).
- It hooks the game's mouse-input translator (look axes) and observes shots and hits (own shots, hit callbacks, bullet collisions) with Harmony. These hooks are resolved by method reference, so a game update that changes the signatures fails at load instead of misbehaving.
- **Auto Ranging / automatic zero.** Bullet-drop compensation is computed against the zero distance the sight currently reports. If another mod sets the zero to the target distance automatically, there is little drop left to compensate; the code accounts for this (it only applies the part not already covered by the zero).
- **Scopes / FSR.** The mod works from the aim camera and the active scope's magnification. Settings in degrees are scaled by the magnification where documented ("Deckel durch Zoom teilen"). Behaviour with third-party scope or FSR/upscaling mods has not been verified.
- **SAIN.** The recording reads SAIN's bot state via reflection if SAIN is present (no hard dependency); this only matters when the recording is enabled.
- **Other aim/recoil mods.** Not tested. Using another mod that also modifies mouse look or recoil together with "Rueckstoss-Ausgleich" may over-correct.
- **Fika (multiplayer coop): not tested.**

## Recording / diagnostics

Both are **off by default**.

- **Diagnose ins Protokoll** adds periodic status lines (interval configurable) and per-shot / per-hit lines to the BepInEx log. Meant for troubleshooting only; with an interval of 0 the log grows quickly.
- **Kampf-Aufzeichnung** records each engagement frame by frame into `BepInEx/Zielhilfe-Aufzeichnung/<date>-<map>/`, one gzip file (JSON Lines) per engagement: your own state, the mouse input with all correction components, enemies with hit zones, armor and AI intent, shots and hits. It writes from a background thread. An engagement starts when a target is in the capture area, you fire, you are hit, or an enemy that targets you sees you within the configured distance; it ends 4 s after the last such activity (1 s before the start is included). The size per raid and the number of kept raid folders are capped; older raid folders in that directory are deleted automatically. The format is meant for offline analysis and tuning.
- Per-shot impact analysis (where the bullet landed relative to the aim point) runs whenever either of the two switches is on; with both off it does nothing.

## Known limitations

- Single-player (offline) SPT only; not tested with Fika or any multiplayer setup.
- The mouse interception works on the game's look-axis input; If the hook fails, the log shows an error and Soft Aim stays off.
- The legacy modes (AltesSoftaim, AltesFesthalten) are kept for compatibility and are less refined; they can feel sticky or rigid and need their own tuning.
- The default capture, magnet and follow-through values were tuned with offline simulations of recorded fights and one player's mouse habits; they may need adjusting for other mice, sensitivities and frame rates.
- In the legacy modes, large hip-fire offsets (weapon lowered or being raised) are deliberately not compensated.
- Very low frame rates make the magnet unstable (very short catch-up times become jittery below about 30 fps).
- Armor-based zone choice depends on the game's armor data and the worn gear; heavily modded armor or ammunition systems may give unexpected zone choices.
- Grenade launchers are not supported.
- Fences, foliage and other deviating materials can still deflect bullets even when counted as passable.
- Settings text inside the game is German only.

## Building from source

Needs the .NET 10 SDK and an SPT 4.1.6 installation to build against.

```bash
./build.sh             # Linux: build only
./build.sh --install   # build and copy into the game folder
```
```powershell
.\build.ps1            # Windows
.\build.ps1 -Install
```

The game folder is set in `Directory.Build.props` (Windows default `C:/SPT`)
or via the environment variable `SPT_DIR`.

## Support

Bugs and questions: [GitHub issues](https://github.com/Kaanori/spt-zielhilfe/issues).

## License

MIT — see [LICENSE](LICENSE).
