// ---------------------------------------------------------------
//  Einmalige Uebernahme der Einstellungen von Version 1.1.0 (deutsche
//  Sektions- und Schluesselnamen) auf die englischen Namen ab 1.2.0.
//
//  So geht es: Vor den Binds merkt sich Vorhandene() alle Eintraege, die
//  die cfg schon enthaelt. Nach den Binds geht Uebernehmen() die Tabelle
//  unten durch; fuer jeden neuen Eintrag, den es NICHT schon unter dem
//  neuen Namen gab, wird der alte Wert (BepInEx haelt unbekannte Eintraege
//  in ConfigFile.OrphanedEntries) gelesen, bei Aufzaehlungen auf den neuen
//  Mitgliedsnamen umgesetzt und in den neuen Eintrag geschrieben.
//  Diese Datei haengt nur an BepInEx - sie wird auch vom Offline-Test
//  verwendet.
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace Zielhilfe
{
	internal static class Einstellungsumzug
	{
		/// <summary>Eine Zeile der Tabelle: alter Platz -> neuer Platz.</summary>
		internal struct Zeile
		{
			public string AltSektion, AltKey, NeuSektion, NeuKey;
		}

		internal static readonly List<Zeile> Tabelle = Baue();

		// Aufzaehlungen: alter Mitgliedsname -> neuer Mitgliedsname (je Typname).
		private static readonly Dictionary<string, Dictionary<string, string>> Werte = new Dictionary<string, Dictionary<string, string>>
		{
			["Ausloeser"] = new Dictionary<string, string>
			{
				["TasteHalten"] = "HoldKey", ["TasteUmschalten"] = "ToggleKey", ["Immer"] = "Always"
			},
			["Zielmodus"] = new Dictionary<string, string>
			{
				["SoftaimMaus"] = "SoftAimMouse", ["AltesSoftaim"] = "LegacySoftAim", ["AltesFesthalten"] = "LegacyLockOn"
			},
			["Radiusart"] = new Dictionary<string, string>
			{
				["Meter"] = "Meters", ["Grad"] = "Degrees"
			},
			["Knochen"] = new Dictionary<string, string>
			{
				["Kopf"] = "Head", ["Brust"] = "Chest", ["Becken"] = "Pelvis", ["Beine"] = "Legs"
			},
			["Kopfbox"] = new Dictionary<string, string>
			{
				["Sichtbar"] = "Visible", ["Kopfmitte"] = "HeadCenter", ["Gesicht"] = "Face",
				["Kiefer"] = "Jaw", ["Schaedeldecke"] = "Skull",
				// uralter Wert aus der Zeit vor den Kopfboxen
				["Knochen"] = "Visible"
			}
		};

		private static void A(List<Zeile> l, string altSek, string neuSek, params string[] paare)
		{
			for (int i = 0; i + 1 < paare.Length; i += 2)
			{
				l.Add(new Zeile { AltSektion = altSek, AltKey = paare[i], NeuSektion = neuSek, NeuKey = paare[i + 1] });
			}
		}

		private static List<Zeile> Baue()
		{
			List<Zeile> l = new List<Zeile>();
			// Ab 1.3.0 wohnen die beiden Hauptschalter in "0 Quick Setup". Diese Zeilen
			// stehen vorn, damit der erste Treffer (Fassung 1.2.0) vor dem deutschen gewinnt.
			A(l, "1 General", "0 Quick Setup", "Enabled", "Enabled");
			A(l, "6 ESP", "0 Quick Setup", "Enabled", "Enemy ESP");
			A(l, "1 Allgemein", "0 Quick Setup", "Eingeschaltet", "Enabled");
			A(l, "1 Allgemein", "1 General", "Taste", "Hotkey");
			A(l, "1b Soft Aim per Maus", "1b Soft Aim (mouse)",
				"Art der Hilfe", "Assist type",
				"Wann sie hilft", "When active",
				"Nur im Anschlag (ADS)", "Only when aiming down sights (ADS)",
				"Zone nach Panzerung waehlen", "Choose zone by armor",
				"Erlaubte Zone: Kopf", "Allowed zone: Head",
				"Erlaubte Zone: Rumpf", "Allowed zone: Torso",
				"Erlaubte Zone: Becken", "Allowed zone: Pelvis",
				"Erlaubte Zone: Beine", "Allowed zone: Legs",
				"Nur Feinde", "Enemies only",
				"Rueckstoss-Ausgleich quer (Prozent)", "Recoil compensation horizontal (%)",
				"Rueckstoss-Ausgleich hoch (Prozent)", "Recoil compensation vertical (%)",
				"Radius-Art", "Radius type",
				"Radius in Metern ab Zonenrand", "Radius in meters from zone edge",
				"Radius hoechstens x Grad", "Maximum radius (degrees)",
				"Radius mindestens x Grad", "Minimum radius (degrees)",
				"Radius in Grad", "Radius in degrees",
				"Anziehung (Gewicht)", "Pull strength (weight)",
				"Reibung", "Friction",
				"Korrektur hoechstens x Grad je Sekunde", "Maximum correction (degrees per second)",
				"Halten ohne Maus (Magnet) hoechstens x Grad je Sekunde", "Hold without mouse (magnet), maximum degrees per second",
				"Mitdrehen Anteil", "Target tracking share",
				"Mitdrehen hoechstens x Grad je Sekunde", "Target tracking maximum degrees per second",
				"Deckel durch Zoom teilen", "Divide caps by zoom",
				"Magnet weicht eigener Maus ab x Grad je Sekunde (Bildschirm)", "Magnet yields to own mouse above x degrees per second (screen)",
				"Einfang-Schub Dauer (s)", "Capture boost duration (s)",
				"Einfang-Schub hoechstens x Grad je Sekunde", "Capture boost maximum degrees per second",
				"Mitdrehen: Hilfe-Drehung nicht als eigene zaehlen", "Tracking: do not count assist rotation as own",
				"Nach Loslassen wieder greifen nach x Sekunden", "Re-engage after release (s)",
				"Loesen nach Weg (Grad)", "Release after travel (degrees)",
				"Magnet Nachziehzeit in Sekunden", "Magnet pull time in seconds");
			// Waffenklassen: gleiche zehn Schluessel in sechs Sektionen.
			string[] klassenSchluessel =
			{
				"Eigene Werte statt allgemeiner", "Use own values instead of general",
				"Radius in Metern ab Zonenrand", "Radius in meters from zone edge",
				"Anziehung (Gewicht)", "Pull strength (weight)",
				"Reibung", "Friction",
				"Halten ohne Maus (Magnet) hoechstens x Grad je Sekunde", "Hold without mouse (magnet), maximum degrees per second",
				"Magnet Nachziehzeit in Sekunden", "Magnet pull time in seconds",
				"Mitdrehen Anteil", "Target tracking share",
				"Mitdrehen hoechstens x Grad je Sekunde", "Target tracking maximum degrees per second",
				"Einfang-Schub Dauer (s)", "Capture boost duration (s)",
				"Einfang-Schub hoechstens x Grad je Sekunde", "Capture boost maximum degrees per second"
			};
			string[][] klassen =
			{
				new[] { "1c Klasse Pistole", "1c Class Pistol" },
				new[] { "1d Klasse MP", "1d Class SMG" },
				new[] { "1e Klasse Sturmgewehr und Karabiner", "1e Class Assault Rifle and Carbine" },
				new[] { "1f Klasse DMR und Scharfschuetze", "1f Class DMR and Sniper" },
				new[] { "1g Klasse Schrotflinte", "1g Class Shotgun" },
				new[] { "1h Klasse MG", "1h Class Machine Gun" }
			};
			foreach (string[] k in klassen)
			{
				A(l, k[0], k[1], klassenSchluessel);
			}
			A(l, "2 Ziel", "2 Target",
				"Reichweite in Metern", "Range in meters",
				"Koerperstelle", "Body part",
				"Kopf-Trefferbox", "Head hitbox",
				"Hoehe in der Kopfbox", "Height within head box",
				"Auf die Brust ausweichen, wenn der Kopf zu ist", "Fall back to chest when head is covered",
				"Nur freie Sicht", "Clear line of sight only",
				"Durchschiessbares ignorieren", "Ignore shoot-through obstacles",
				"Durchschuss ab Wahrscheinlichkeit", "Shoot-through from probability",
				"Nur Ebenen pruefen, auf denen Kugeln fliegen", "Check only layers bullets travel on",
				"Wechselvorsprung in Pixeln", "Target switch margin (pixels)");
			A(l, "3 Wen auslassen", "3 Exclusions",
				"Scavs", "Scavs",
				"PMCs", "PMCs",
				"Bosse und Leibwaechter", "Bosses and guards");
			A(l, "5 Vorhalt", "5 Lead",
				"Kugelfall ausgleichen", "Compensate bullet drop",
				"Eingeschaltet", "Enabled",
				"Faktor", "Factor",
				"Hoechstens x Meter", "Maximum (meters)",
				"Erst ab x m/s", "Only above (m/s)",
				"Beruhigung (Sekunden)", "Smoothing (seconds)",
				"Glaettung mindestens x Sekunden", "Minimum smoothing (seconds)",
				"Bildverzug ausgleichen (Bilder)", "Compensate frame delay (frames)",
				"Nur waagerechte Bewegung", "Horizontal movement only",
				"Bei Richtungswechsel zuruecknehmen", "Reduce on direction change");
			A(l, "8 Diagnose", "8 Diagnostics",
				"Kampf-Aufzeichnung", "Combat recording",
				"Aufzeichnung hoechstens x MB je Raid", "Recording limit per raid (MB)",
				"Aufzeichnung: nur die letzten x Raids behalten", "Recording: keep only the last x raids",
				"Gefecht beginnt, wenn ein Feind dich sieht, bis x Meter", "Fight starts when an enemy sees you, up to x meters",
				"Diagnose ins Protokoll", "Diagnostics to log",
				"Diagnose-Takt in Sekunden", "Diagnostics interval in seconds");
			A(l, "9 Alte Arten - Allgemein", "9 Legacy Modes - General",
				"Wann sie zieht", "When it pulls");
			A(l, "9 Alte Arten - Bewegung", "9 Legacy Modes - Movement",
				"Nachziehgeschwindigkeit", "Pull speed",
				"Softaim hoechstens x Grad je Sekunde", "Soft aim maximum degrees per second",
				"Totzone in Grad", "Dead zone (degrees)",
				"Eigene Maus hat Vorrang", "Own mouse has priority",
				"Vorrang ab x Grad je Sekunde", "Priority above x degrees per second",
				"Danach x Sekunden nicht ziehen", "Then do not pull for x seconds",
				"Mitdrehen bei bewegten Zielen", "Track moving targets",
				"Mitdrehen beruhigen (Sekunden)", "Tracking smoothing (seconds)",
				"Mitdrehen hoechstens x Grad je Sekunde", "Tracking maximum degrees per second");
			A(l, "9 Alte Arten - Rueckstoss", "9 Legacy Modes - Recoil",
				"Rueckstoss ausgleichen", "Compensate recoil",
				"Beruhigen (Sekunden)", "Smoothing (seconds)",
				"Muendung nachregeln", "Correct muzzle",
				"Nachregeln wie schnell", "Correction speed",
				"Nachregeln hoechstens x Grad", "Correction maximum (degrees)",
				"Hoechstens x Grad", "Maximum (degrees)",
				"Aus der Huefte ausgleichen", "Compensate from hip",
				"Huefte hoechstens x Grad", "Hip maximum (degrees)");
			A(l, "9 Alte Arten - Ziel", "9 Legacy Modes - Target",
				"Blickfeld in Pixeln", "Field of view (pixels)",
				"Blickfeld in Grad", "Field of view (degrees)",
				"Zielwinkel vom Feuerpunkt statt von der Kamera", "Aim angle from fire point instead of camera");

			// --- Aeltere Plaetze (vor 1.1.0): gelten nur, wenn es den Eintrag
			// am neueren alten Platz nicht gibt (die Tabelle wird der Reihe nach
			// abgearbeitet, der erste Treffer gewinnt).
			A(l, "1 Allgemein", "9 Legacy Modes - General", "Wann sie zieht", "When it pulls");
			A(l, "1 Allgemein", "8 Diagnostics",
				"Diagnose ins Protokoll", "Diagnostics to log",
				"Diagnose-Takt in Sekunden", "Diagnostics interval in seconds");
			A(l, "2 Ziel", "9 Legacy Modes - Target",
				"Blickfeld in Pixeln", "Field of view (pixels)",
				"Blickfeld in Grad", "Field of view (degrees)",
				"Zielwinkel vom Feuerpunkt statt von der Kamera", "Aim angle from fire point instead of camera");
			A(l, "4 Bewegung", "9 Legacy Modes - Movement",
				"Nachziehgeschwindigkeit", "Pull speed",
				"Softaim hoechstens x Grad je Sekunde", "Soft aim maximum degrees per second",
				"Totzone in Grad", "Dead zone (degrees)",
				"Eigene Maus hat Vorrang", "Own mouse has priority",
				"Vorrang ab x Grad je Sekunde", "Priority above x degrees per second",
				"Danach x Sekunden nicht ziehen", "Then do not pull for x seconds",
				"Mitdrehen bei bewegten Zielen", "Track moving targets",
				"Mitdrehen beruhigen (Sekunden)", "Tracking smoothing (seconds)",
				"Mitdrehen hoechstens x Grad je Sekunde", "Tracking maximum degrees per second");
			A(l, "6 Rueckstoss", "9 Legacy Modes - Recoil",
				"Rueckstoss ausgleichen", "Compensate recoil",
				"Beruhigen (Sekunden)", "Smoothing (seconds)",
				"Muendung nachregeln", "Correct muzzle",
				"Nachregeln wie schnell", "Correction speed",
				"Nachregeln hoechstens x Grad", "Correction maximum (degrees)",
				"Hoechstens x Grad", "Maximum (degrees)",
				"Aus der Huefte ausgleichen", "Compensate from hip",
				"Huefte hoechstens x Grad", "Hip maximum (degrees)");
			return l;
		}

		/// <summary>
		/// BepInEx haelt Eintraege der cfg, die (noch) zu keinem Bind gehoeren, in der
		/// privaten Eigenschaft ConfigFile.OrphanedEntries.
		/// </summary>
		internal static Dictionary<ConfigDefinition, string> Waisen(ConfigFile c)
		{
			PropertyInfo pi = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			return pi?.GetValue(c, null) as Dictionary<ConfigDefinition, string>;
		}

		/// <summary>VOR den Binds aufrufen: alles, was die cfg schon enthaelt.</summary>
		public static HashSet<ConfigDefinition> Vorhandene(ConfigFile c)
		{
			Dictionary<ConfigDefinition, string> w = Waisen(c);
			return w == null ? new HashSet<ConfigDefinition>() : new HashSet<ConfigDefinition>(w.Keys);
		}

		/// <summary>Alten Wert auf den neuen Mitgliedsnamen umsetzen (nur bei umbenannten Aufzaehlungen).</summary>
		internal static string Umsetzen(Type typ, string alt)
		{
			string w = alt.Trim();
			if (Werte.TryGetValue(typ.Name, out var karte) && karte.TryGetValue(w, out var neu))
			{
				return neu;
			}
			return alt;
		}

		/// <summary>
		/// NACH den Binds aufrufen. Liefert, wie viele Einstellungen aus den alten
		/// Namen uebernommen wurden. Ueberschreibt nie einen Wert, den es unter dem
		/// neuen Namen schon gab.
		/// </summary>
		public static int Uebernehmen(ConfigFile c, HashSet<ConfigDefinition> vorher)
		{
			Dictionary<ConfigDefinition, string> w = Waisen(c);
			if (w == null)
			{
				return 0;
			}
			int anzahl = 0;
			bool alt = c.SaveOnConfigSet;
			c.SaveOnConfigSet = false;
			HashSet<ConfigDefinition> fertig = new HashSet<ConfigDefinition>();
			try
			{
				foreach (Zeile z in Tabelle)
				{
					ConfigDefinition neu = new ConfigDefinition(z.NeuSektion, z.NeuKey);
					ConfigDefinition alter = new ConfigDefinition(z.AltSektion, z.AltKey);
					if (alter.Equals(neu) || fertig.Contains(neu) || vorher.Contains(neu))
					{
						continue;
					}
					if (!w.TryGetValue(alter, out var wert) || !c.ContainsKey(neu))
					{
						continue;
					}
					ConfigEntryBase e = c[neu];
					try
					{
						e.SetSerializedValue(Umsetzen(e.SettingType, wert));
						w.Remove(alter);
						fertig.Add(neu);
						anzahl++;
					}
					catch
					{
						// Wert nicht lesbar: neuer Eintrag behaelt seinen Standard.
					}
				}
			}
			finally
			{
				c.SaveOnConfigSet = alt;
			}
			if (anzahl > 0)
			{
				c.Save();
			}
			return anzahl;
		}
	}
}
