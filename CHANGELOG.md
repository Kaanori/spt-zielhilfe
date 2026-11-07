# Changelog

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
