# Zielhilfe – Format der Kampf-Aufzeichnung (Format 1)

Quelle: `src/Aufzeichnung.cs` (Schreiben), Abgriffe in `src/Zielhilfe.cs`, Einschlag je Schuss in
`src/Einschlag.cs`. Auswertung der Einschläge: `~/Werkzeuge/zielhilfe/einschlaege.py`.

## Dateien

```
BepInEx/Zielhilfe-Aufzeichnung/<yyyyMMdd-HHmmss>-<Karte>/gefecht-<NNN>-<HHmmss>.jsonl.gz
```

- Ein Ordner je Raid; ältere Ordner werden beim Raid-Start gelöscht, so dass höchstens
  „Aufzeichnung: nur die letzten x Raids behalten“ (Standard 20) bleiben.
- Eine Datei je Gefecht, gzip, darin **JSON Lines** (eine JSON-Zeile je Bild, UTF-8, `\n`).
- Je Raid höchstens „Aufzeichnung höchstens x MB je Raid“ (Standard 200, gepackt). Danach
  nichts mehr.
- Beim Beenden des Spiels mitten im Gefecht kann der gzip-Abschluss fehlen
  (Datei bis zum letzten Block lesbar, z. B. `zcat` mit Fehlermeldung am Ende).

## Gefecht

- **Beginn**, sobald eins zutrifft:
  - Ziel der Hilfe im Fangbereich,
  - eigener Schuss,
  - Treffer auf mich,
  - ein Feind hat mich als Ziel (`GoalEnemy`) und sieht mich, bis
    „Gefecht beginnt, wenn ein Feind dich sieht, bis x Meter“ (Standard 150).
- **Ende**: 4 s nach der letzten solchen Aktivität.
- **Vorlauf**: die Bilder der letzten 1 s vor Beginn stehen direkt nach der Kopfzeile
  (Ringpuffer mit 512 Bildern; bei mehr als 512 FPS entsprechend kürzer).

## Einheiten und Koordinaten

- **Welt**: Unity, linkshändig, **y nach oben**. Meter, m/s, Sekunden.
- **Winkel**: Grad.
- **`Player.Rotation` = `[x, y]`**:
  - `x` = Gieren (Yaw) = `atan2(dir.x, dir.z)` in Grad, wächst nach rechts (von oben gesehen
    im Uhrzeigersinn, von +z nach +x).
  - `y` = Nicken (Pitch) = `-asin(dir.y)` in Grad, **positiv = nach unten**.
- **Maus → Drehung**: Das Spiel rechnet in `MoveInputTranslator.TranslateAxes`
  `delta = (inv.x * axes[2], inv.y * axes[3])` und in `MovementState.ClampRotation`
  `Rotation += delta * k` (begrenzt auf `RotationSpeedClamp`, meist 99).
  `k = GetRotationMultiplier() * StateSensitivity` (Mausempfindlichkeit, im Anschlag die
  ADS-Empfindlichkeit).
- **Korrekturanteile** (`maus.anz/reib/mag/schub/mit/rcs`) sind bereits in **Grad im Drehraum
  von `Player.Rotation`** (x = Gieren, y = Nicken), für dieses Bild.

## Zahlen

Festkomma mit Punkt; nachfolgende Nullen fehlen (`1.5`, `-0.03`, `0`).
Bool als `0`/`1`. Fehlende Werte: Feld fehlt.

## Kopfzeile (erste Zeile)

```json
{"kopf":{"format":1,"plugin":"1.0.0","karte":"bigmap","datum":"2026-11-02T14:10:00",
 "zeit":1234.5678,"gefecht":3,"ich":"<ProfileId>",
 "einstellungen":{"<Sektion>/<Schlüssel>":"<Wert wie in der cfg>", ...},
 "ausruestung":[{"slot":"FirstPrimaryWeapon","tpl":"<TemplateId>","name":"<_name>"}, ...]}}
```

`einstellungen` enthält ALLE Einträge der Zielhilfe-cfg (auch Klassenwerte und alte Arten).

## Bildzeile

```json
{"t":1234.5678,"dt":0.00694,"f":56789,
 "ich":{...},"maus":{...},"hilfe":{...},"geg":[...],"ev":[...]}
```

| Feld | Bedeutung |
|---|---|
| `t` | `Time.time` (s), Zeitstempel des Bildes |
| `dt` | `Time.deltaTime` (s) |
| `f` | `Time.frameCount` |

### `ich`

| Feld | Bedeutung |
|---|---|
| `p` | Position (m) |
| `v` | `Player.Velocity` (m/s) |
| `r` | `Player.Rotation` `[Gieren, Nicken]` (°), Stand **vor** der Mausdrehung dieses Bildes |
| `fp` | Feuerpunkt nach `AdjustShotVectors` (Startpunkt der Kugel) |
| `sd` | Schussrichtung nach `AdjustShotVectors` (Einheitsvektor, ohne Streuung) |
| `kam`, `kamVor` | Kameraort, Kamera-Vorwärtsrichtung |
| `fov` | Kamera-Sichtfeld (°, vertikal; bei Zoom kleiner) |
| `ads` | im Anschlag |
| `pose` | `MovementContext.PoseLevel` (0 = tief geduckt … 1 = stehend) |
| `liegt`, `sprint` | liegend, sprintet |
| `lehnen` | `MovementContext.Tilt` (Lehnen, ± ≈ 5) |
| `k` | Empfindlichkeitsfaktor (s. oben) |
| `w.kl` | Waffenklasse (Allgemein, Pistole, MP, Sturmgewehr, Praezision, Schrot, MG, Werfer) |
| `w.eigen` | Klasse benutzt eigene Werte |
| `w.bereit` / `w.grund` | Schusswaffe schussbereit / sonst Grund (z. B. Operationsname beim Nachladen) |
| `w.tpl` | Template-ID der Waffe |
| `w.pen`, `w.v0`, `w.patrone` | Durchschlag, Mündungsgeschwindigkeit (m/s, InitialSpeed × SpeedFactor) und Template-ID der Patrone, mit der das Plugin rechnet: solange Waffe und Magazin gleich sind, die zuletzt WIRKLICH gefeuerte, sonst `CurrentAmmoTemplate` |
| `w.visier`, `w.zoom`, `w.nullung` | aktives Visier (Template-ID von `CurrentAimingMod`), aktuelle Vergrößerung, eingestellte Nullungsdistanz (m). Für den Fallausgleich ohne Belang (die Schusslinie hat ihre eigene Nullung); für das Gefühl: Korrekturen in Grad erscheinen im Glas × `zoom` |

Die Waffenstreuung steht als Winkel in `hilfe.streuung` und im `schuss`-Ereignis (`streuung`).

### `maus`

| Feld | Bedeutung |
|---|---|
| `eingabe` | 1 = das Spiel hat in diesem Bild Maus-Achsen an die Figur gegeben. 0 = nicht (Inventar/Menü/Karte offen: `UIScreen.TranslateAxes` setzt `axes = null`, die Kette endet vorher) – dann fehlen `roh`, `inv` und `fertig`, die Figur dreht sich in dem Bild nicht per Maus |
| `roh` | `[axes[2], axes[3]]` vom Spieler, VOR der Hilfe. **Eingabe-Einheiten (Achswerte), keine Grad**: Grad = Wert × `inv` × `k` (z. B. 0,37 × 0,038 ≈ 0,014°) |
| `inv` | `[_xDirection, _yDirection]` (±1, Maus umkehren) |
| `fertig` | `[axes[2], axes[3]]` NACH der Hilfe (das, was ans Spiel ging), ebenfalls Eingabe-Einheiten; enthält die eigene Mausbewegung, große Werte stammen von ihr (`roh`), nicht von der Hilfe |
| `teile` | 1 = die Korrekturanteile unten stammen aus diesem Bild |
| `anz` | Umbiegen/Anziehung (°) |
| `reib` | Reibung (°) |
| `mag` | Magnet mit normalem Deckel (°) |
| `schub` | Zusatz des Einfang-Schubs über den normalen Magnet hinaus (°) |
| `mit` | Mitdrehen (°) |
| `rcs` | Rückstoß-Ausgleich (°) |
| `weg`, `gel` | Lösen-nach-Weg-Summe, gelöst. Seit Bau „eigenzielen“ im **Bildschirm-Maß** (Grad × `w.zoom`), Schwelle 2 bzw. Rückkehr unter 1 |
| `schubAktiv`, `schubRest` | Einfang-Schub aktiv, Restzeit (s) |

Drehung des Bildes ≈ `inv * fertig * k`. Die Summe der Anteile entspricht `inv*(fertig-roh)*k`
bis auf die Umrechnung „quer mal cos(Nicken)“, die intern in der Bildebene gerechnet wird.

### `hilfe` (nur wenn die Hilfe ein Ziel hat)

| Feld | Bedeutung |
|---|---|
| `ziel` | ProfileId des Ziels |
| `zone` | führende Zone (Kopf/Brust/Becken/Beine) |
| `rand` | Winkelabstand Schusslinie → Rand der Trefferkollider der Zone (°), 0 = darauf |
| `win` | Winkel zum Zielpunkt (°) |
| `radius` | Fangradius (°) |
| `imRadius` | im Fangbereich |
| `zielRate` | Drehrate der Linie Feuerpunkt → Zielpunkt `[Gieren, Nicken]` (°/s, geglättet 0,08 s) |
| `wahl` | Zonenwahl samt Begründung (Text: Erwartung je Zone roh und geglättet, ggf. „Wechsel zu X in t s“, P je Zone, Durchschlag, Streuung, Unruhe). Beständigkeit: Erwartungswerte geglättet (0,3 s); gewechselt wird erst, wenn eine andere Zone geglättet um ≥ 20 % besser ist und das 0,5 s anhält (sofort, wenn die aktuelle Zone verdeckt wird); neues Ziel setzt zurück. Die Kopfbox bleibt, solange sie frei ist und nicht > 20 % schlechter gepanzert als die beste |
| `sicht` | Sicht zur Wunschzone: `frei`, `durch <Material …>` oder `verdeckt von …` |
| `sichtZonen` | Sicht je Zone `[Kopf, Rumpf, Becken, Beine]` in diesem Bild: `1` frei, `2` frei durch durchschießbares Material (eigene Patrone), `0` verdeckt, `-1` Zone nicht vorhanden, von der Patrone nicht durchschlagbar oder in „Erlaubte Zone“ gesperrt (Becken/Beine ab Werk gesperrt, außer als Ausweich, wenn Kopf und Rumpf nicht möglich sind; dann steht „AUSWEICH“ in `wahl`). Nur Zonen mit 1/2 ziehen (Anziehung, Reibung, Magnet, Mitdrehen, Schub). Geprüft vom Feuerpunkt; Wunschzone an ihrem Zielpunkt, andere Zonen an der Mitte ihrer Hülle |
| `fallCm`, `entf` | **Angewandter** Fallausgleich in cm und die Entfernung in m, für die er gerechnet wurde. Der Wunsch-Zielpunkt (Kopfbox-Punkt bzw. Zonenpunkt) wird in Welt-y um `fallCm/100` m angehoben, VOR dem Vorhalt (`Zielpunkt.MitVorhalt`: `basis.y += Fallausgleich`). Im Soft-Aim-Modus ist das der volle Kugelfall `Fall(entf)` unter der Abschusslinie (die Nullung steckt schon in der Schusslinie). `entf` = Abstand von der EIGENEN Position (Füße) zum Zielpunkt ohne Vorhalt. Fall aus derselben Schritt-Rechnung wie das Spiel (`TrajectoryCalculator.NextForPlayer`, 0,01 s, G1) |
| `streuung` | Streuungsradius der eigenen Waffe (°): atan(CoI·AmmoFactor·BarrelDeviation·WeaponSpread/100), Abweichung gleichverteilt in der Kugel (projiziert). CoI = `Weapon.GetTotalCenterOfImpact(false)` = CoI des Laufs × (1 − ΣAccuracy/100), AmmoFactor = (100+\|ammoAccr\|)/100 bei ammoAccr ≤ 0, BarrelDeviation = `Weapon.GetBarrelDeviation()` (Haltbarkeit, enthält WeaponSpread schon einmal; `Shot()` multipliziert ihn ein zweites Mal). **Bis Bau „einschlag“ 03.11. immer 0, wenn „Zone nach Panzerung wählen“ aus war** (Wert wurde nur in der Zonenwahl gesetzt) – ebenso `p` (bleibt bei ausgeschalteter Zonenwahl weiterhin 0) |
| `unruhe` | gemessene Zielunruhe je Achse (°, 1 σ): Abweichung der Schusslinie um ihr gleitendes Mittel (0,5 s), gemessen gegen einen FESTEN Körperpunkt (Mitte der Rumpf-Hülle ohne Vorhalt) – unabhängig von der Zonenwahl. Bis Bau 154 112 Byte: Restfehler zum Zielpunkt der führenden Zone (sprang bei jedem Zonenwechsel mit) |
| `p` | Trefferanteil je Zone bei Schuss auf den gewählten Punkt: Kopf, Rumpf, Becken, Beine, Arme |

### `geg` – Gegner

Feindliche Gegner (`BotsGroup`-Logik) in Reichweite, die das Ziel sind, im Sichtfeld der
Kamera liegen oder mich als Ziel haben.

- Höchstens 8, das Ziel zuerst, sonst die nächsten.
- Das Ziel steht in jedem Bild; andere Gegner höchstens alle 0,1 s.
- Ein Gegner, der in einem Bild fehlt, ist dort nicht neu gemessen; er ist nicht verschwunden.
- Ohne Sichtstrahl-Prüfung, um Leistung zu sparen.

| Feld | Bedeutung |
|---|---|
| `id`, `n`, `rolle` | ProfileId, Name, `WildSpawnType` |
| `ziel` | ist das Ziel der Hilfe |
| `p`, `v` | Position (m), Geschwindigkeit (m/s) |
| `r` | `Player.Rotation` des Gegners (Körperdrehung, °) |
| `blick` | `LookDirection` (Einheitsvektor) |
| `pose`, `liegt`, `sprint` | wie bei `ich` |
| `zonen.Kopf/Rumpf/Becken/Beine/Arme` | `[cx,cy,cz, hx,hy,hz]`: Mitte und Halbmaße der achsparallelen Hülle aller Kollider der Zone (m) |
| `kopfboxen.<Typ>` | `[cx,cy,cz, a1x,a1y,a1z, a2x,a2y,a2z, a3x,a3y,a3z]`: Mitte und die drei Halbachsen-Vektoren des einbeschriebenen Ellipsoids des Kolliders (Box: Halbmaße, Kapsel: Radius/halbe Höhe) in Weltkoordinaten. Typen: HeadCommon, Eyes, Jaw, ParietalHead, BackHead, Ears, NeckFront, NeckBack |
| `durch` | 13 Werte (0..1): Durchschlagchance der eigenen Patrone für die 8 Kopfboxen (Reihenfolge wie oben), Rumpf vorn, hinten, seitlich, Becken vorn, hinten; −1 = nicht lesbar. Höchstens 1× je s neu berechnet |
| `ki.schicht` | `BotOwner.Brain.ActiveLayerName()` (KI-Schicht; mit SAIN deren Schichten, z. B. „SAIN : Combat Solo“) |
| `ki.knoten` | `Brain.GetLastNode()` (aktive Logik) |
| `ki.entscheidung` | `Brain.LastDecision` |
| `ki.endziel` | `BotOwner.Mover.TargetPoint` (Endziel der Navigation, m) |
| `ki.ecke`, `ki.rest` | `Mover.CurrentCornerPoint` (nächster Wegpunkt), `Mover.DistDestination` (Restweg, m) – nur mit Pfad |
| `ki.bewegt` | `Mover.IsMoving` |
| `ki.hatZiel`, `ki.zielIchBin`, `ki.siehtZiel`, `ki.kannSchiessen` | `Memory.GoalEnemy`: vorhanden, bin ich, `IsVisible`, `CanShoot` |
| `ki.inDeckung`, `ki.deckung` | `Memory.IsInCover`, Ort des Deckungspunkts (`CurCustomCoverPoint.BasePosition`) |
| `ki.sain.aktiv` | SAIN-Schichten aktiv (`SAINLayersActive`) |
| `ki.sain.kampf/trupp/selbst` | `Decision.CurrentCombatDecision / CurrentSquadDecision / CurrentSelfDecision` |
| `ki.sain.feind`, `sieht`, `sichtlinie` | `EnemyController.GoalEnemy`: `EnemyProfileId`, `IsVisible`, `InLineOfSight` |

SAIN wird per Reflexion gelesen (`SAIN.Components.BotComponent` am BotOwner-Objekt). Ohne SAIN
fehlt `ki.sain`.

### `ev` – Ereignisse dieses Bildes (nur wenn vorhanden)

| `typ` | Felder |
|---|---|
| `schuss` | `nr` (Schussnummer im Raid, gleiche Nummer wie `ergebnis` und die EINSCHLAG-Zeile), `ziel`, `zielQuelle` (`hilfe` = Ziel der Hilfe, `strahl` = nächster Spieler ≤ 2,5 m neben der Schusslinie), `streuung` (°, s. `hilfe`), `vorhalt` (nur wenn die Hilfe in diesem/dem letzten Bild für dieses Ziel gerechnet hat): `cm` (angewandter Vorhalt-Vektor Welt, cm, samt Bildverzug, ohne Fall), `bild` [quer, hoch] cm in der Bildebene, `tempoGl` (geglättetes Tempo, aus dem gerechnet wurde, m/s Welt), `tempoRoh` (`Player.Velocity` im selben Bild), `tempoGlBild`/`tempoRohBild` [quer, hoch] m/s in der Bildebene, `flugzeit` (s, für den Vorhalt), `fallCm` (angewandter Fallausgleich); dazu `fp`, `sd` (Schusslinie beim Schuss: Feuerpunkt und `WeaponDirection` nach `AdjustShotVectors`, OHNE Streuung; die Nullung der Kimme steckt darin, die eines Zielfernrohrs nicht – die dreht nur das Glas), `zone`, `rand`, `win`, `imRadius`, `gefeuert` / `benutzt` / `vorhergesagt` (je `{tpl, pen, v0}`: wirklich gefeuerte Patrone aus `Shot()`, die Patrone, mit der das Plugin bis zu diesem Schuss gerechnet hat, und `Weapon.CurrentAmmoTemplate`), `fallCm`, `entf` (angewandter Fallausgleich zu diesem Schuss) |
| `kugel` | echte Startwerte der Kugel aus `InitiateShot`: `p` (Start), `dir` (Richtung MIT Streuung), `tpl` (Patrone). Folgt direkt auf `schuss` |
| `treffer` | eigene Kugel trifft: `kol` (Kollidertyp), `platte` (falls Panzerplatte), `opfer` (ProfileId), `kill` (Opfer danach tot) |
| `getroffen` | ich werde getroffen: `kol`, `von` (ProfileId des Schützen) |
| `zielwechsel` | `neu` (ProfileId oder leer) |
| `einschlag` | erster Einschlag einer EIGENEN Kugel in der Umgebung (kein Körper): `kol` (Kollidername), `mat` (Material), `durch` (durchschlagen), `abpr` (abgeprallt), `p` (Ort), `entf` (m von mir), `vorZiel` (m vom Brustkorb des Hilfe-Ziels, −1 = kein Ziel). Beleg `Shot.HandleCollision` → `CreateFragments` (`BulletState` StopHit / RicochetHit / sonst durchschlagen) |

Ein `treffer` gehört zum letzten `schuss` davor (Flugzeit < 1 s).

### `ergebnis` – Einschlag je Schuss (ab Bau „einschlag“ 03.11.)

Ein Eintrag je eigenem Schuss, geschrieben wenn die Kugel samt Splittern fertig ist
(`Shot.ReturnShotToPool`), meist 1–2 Bilder nach dem letzten Einschlag; spätestens 8 s nach dem
Schuss (`grund` „keine Freigabe binnen 8 s“). Also NICHT im Bild des Einschlags (dessen Zeit steht in
`einschlaege[].t`).

| Feld | Bedeutung |
|---|---|
| `nr`, `tSchuss` | Schussnummer, `Time.time` beim Schuss |
| `grund` | `einschlag`, `verfallen` (TimeSinceShot > AmmoLifeTime, nichts getroffen), `ohne Einschlag freigegeben` (Raid-Ende), `keine Freigabe binnen 8 s`, `verdraengt` (mehr als 16 Kugeln gleichzeitig unterwegs) |
| `fp`, `sd`, `kdir` | Schusslinie (ohne Streuung) und echte Startrichtung MIT Streuung |
| `einschlaege[]` | bis zu 4 Einschläge der Kette (Hauptkugel, dann Splitter nach Durchschuss/Abpraller; ab 1.4.4 steht der ERSTE Körpertreffer auf dem 4. Platz, wenn er erst nach dem 4. Einschlag kam): `p`, `kol` (Kollidername), `mat` (MaterialType), `zust` (BulletState danach: StopHit, RicochetHit, DeviationHit = durchschlagen und ggf. abgelenkt, FragmentationHit, Flying), `ebene` (Unity-Layer des Kolliders), `abl`/`ablChance` (TrajectoryDeviation und -Chance des Materials, nur wenn > 0), `splitter` (1 = Splitter, nicht Hauptkugel), `flug` (Flugzeit bis hier, s, über die Kette summiert, genau bis zum Treffpunkt), `t`, bei Körpern `koerper` (Kollidertyp[/Platte]) und `opfer` |
| `anzahl` | Zahl aller Einschläge (auch über 4) |
| `letzterOrt` | Ort der Kugel bei der Freigabe, wenn nichts getroffen wurde |
| `durchgang` | Punkt, an dem Kugel/Splitter die Zieltiefe durchflog (aus `PositionHistory`, linear zwischen zwei Flugschritten) |
| `ziel`, `zielN`, `zielQuelle` | wie beim `schuss` |
| `entf`, `flugzeitZiel` | Feuerpunkt → Kopfmitte beim Schuss (m), geschätzte Flugzeit bis dahin |
| `beimSchuss` / `beimEinschlag` | Ziel im Moment des Schusses / des ERSTEN Einschlags: `kopf` (Mitte der HeadCommon-Box aus ihrem Transform), beim Einschlag zusätzlich `kopfPhys` (dieselbe aus `Collider.bounds`, Physik-Stand), `rumpf` (Mitte der Hülle der 8 Rumpf-Kollider), `p`, `v`, `pose`, `liegt` |
| `beimDurchgang` | `kopf`, `rumpf`, `t` aus dem Bild, das `tSchuss + flugzeitZiel` am nächsten lag |
| `vorhalt` | wie im `schuss`-Ereignis, `bild` hier um die Linie zum Bezugskopf |
| `bezug` | woran `a`/`b`/`c` gemessen sind: `koerper` / `koerper-nach-hindernis` (Körpertreffer, Kopf beim ersten Einschlag), `durchgang` / `durchgang-nach-hindernis` (vorbeigeflogen, Kopf zur Durchgangszeit), `einschlag` (Einschlag vor oder bis 1,5 m hinter dem Ziel, Kopf beim Einschlag), `einschlag-hinten` / `letzter-ort` (kein Durchgang gefunden, unsicher) |
| `pz` | der so gewählte Punkt |
| `a`, `b`, `c` | Abweichung von `pz` in cm [quer + = rechts, hoch + = oben] in der Bildebene des Schützen, auf die Zieltiefe umgerechnet (Winkel × Entfernung): `a` gegen den Kopf zum Bezugszeitpunkt, `b` gegen den Kopf beim Schuss, `c` = `a` minus angewandter Vorhalt (= wo die Kugel ohne Vorhalt gelandet wäre) |
| `linie` | wo die Schusslinie OHNE Streuung in Zieltiefe am Kopf beim Schuss vorbeiläuft (cm); enthält Fallausgleich und Vorhalt |
| `streu` | Anteil der Waffenstreuung (`kdir` gegen `sd`) in Zieltiefe (cm) |
| `kopfWeg` | Kopfbewegung Schuss → Bezugszeitpunkt (cm, Welt) |
| `vorZiel`, `abstandZiel`, `durchlaessig` | nur mit Einschlag: erster Einschlag so viele m vor der Zieltiefe (entlang der Linie, + = davor), Abstand zur nächsten von Kopf-/Rumpfmitte (m), 1 = weiches/durchschießbares Material (Gras, WoodThin, Chainfence, Stoff, Pappe, Glas, Gitter) |
| `treffer`, `zone`, `zielGetroffen` | Körpertreffer in der Kette, Zone (Kopf/Rumpf/Becken/Beine/Arme), Opfer = Ziel |

### Protokollzeile `EINSCHLAG` (bei „Diagnose ins Protokoll“)

```
[Zielhilfe] EINSCHLAG t=<Time.time> schuss=<nr> | Punkt x,y,z | <Kollider> [<Material>, <BulletState>, Ebene <Layer>(, Ablenkung d mit Chance p)] (Koerper <Typ> am Ziel|an anderem) | Flugzeit s
  | Ziel <Name> (<Quelle>) <m> m, Treffer <Zone>|daneben | Ziel-Punkt x,y,z | Bezug <bezug>
  | (a) Kopf jetzt quer ± hoch ± cm | (b) Kopf beim Schuss … | (c) ohne Vorhalt … | Linie …, Streuung …
  | Kopf bewegt cm (hoch ±), Haltung S->E, Tempo m/s | Vorhalt quer ± hoch ± cm, Fall cm | x m vor dem Ziel, y m vom Ziel (DURCHLAESSIG: …) | Folge n: …
```

Ohne Einschlag: `kein Einschlag (<grund>)`, „Punkt“ ist dann der Durchgang bzw. der letzte Ort.
Die `SCHUSS`-Zeile endet seit demselben Bau mit `| schuss=<nr> | Vorhalt quer/hoch cm, Flugzeit | Tempo
geglaettet quer … m/s (Betrag), momentan quer … | Fallausgleich cm`. Die alte Zeile
`EINSCHLAG t=… bild=…` (nur Umgebung) entfällt; das `einschlag`-Ereignis der Aufzeichnung bleibt.

## Leistung

Die Diagnosezeile („Diagnose ins Protokoll“) zeigt `Aufzeichnung … ms/Bild` (gleitendes Mittel),
gepackte und rohe MB sowie verlorene Blöcke (Warteschlange voll).
Grobe Größe: etwa 25 MB roh und 4 MB gepackt je Gefechtsminute bei 144 FPS, Ziel in jedem Bild
und 7 weiteren Gegnern.
