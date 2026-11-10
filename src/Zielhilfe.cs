// ---------------------------------------------------------------
//  Zielhilfe - eigene Zielhilfe, unabhaengig vom Cheat-Menue.
//
//  HERKUNFT DIESER DATEI (12.09.2026):
//  Der urspruengliche Quelltext lag im alten Home und ging beim
//  Wechsel auf CachyOS verloren. Wiederhergestellt wurde er durch
//  DEKOMPILIEREN der laufenden Zielhilfe.dll (Stand 05.09.2026).
//  Damit ist er garantiert das, was auch wirklich lief - die
//  urspruenglichen Kommentare sind dabei aber verloren gegangen.
//  Die Erklaerungstexte zu allen Reglern stehen weiterhin drin:
//  sie sind Teil der ConfigDescription und ueberleben das
//  Dekompilieren.
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace Zielhilfe
{
	internal enum Ausloeser
	{
		HoldKey,
		ToggleKey,
		Always
	}
	/// <summary>
	/// Welche Art von Hilfe laeuft.
	///
	/// SoftaimMaus = echtes Soft Aim: die Hilfe dreht NIE von selbst. Sie biegt
	/// nur die eigene Mausbewegung ein Stueck Richtung Ziel, solange der
	/// Treffpunkt der Waffe nahe genug an der Trefferzone liegt. Ohne
	/// Mausbewegung passiert nichts.
	/// AltesSoftaim = der bisherige Modus "Softaim statt Festhalten": jedes Bild
	/// eine gedeckelte Drehung aufs Ziel, unabhaengig von der Maus.
	/// AltesFesthalten = die Blickrichtung wird jedes Bild aufs Ziel gesetzt.
	/// </summary>
	/// <summary>
	/// Waffenklasse aus Weapon.WeapClass (WeaponTemplate.weapClass). Werte in der
	/// SPT-Datenbank (items.json): pistol, smg, assaultRifle, assaultCarbine,
	/// marksmanRifle, sniperRifle, shotgun, machinegun, grenadeLauncher,
	/// specialWeapon.
	/// </summary>
	internal enum Waffenklasse
	{
		Allgemein,
		Pistole,
		MP,
		Sturmgewehr,
		Praezision,
		Schrot,
		MG,
		Werfer
	}
	/// <summary>Wie der Fangradius von Soft Aim per Maus angegeben wird.</summary>
	internal enum Radiusart
	{
		Meters,
		Degrees
	}
	internal enum Zielmodus
	{
		SoftAimMouse,
		LegacySoftAim,
		LegacyLockOn
	}
	/// <summary>
	/// Wird vom ConfigurationManager (F12) per Reflexion ueber Klassen- und
	/// Feldnamen gelesen - kein Verweis auf dessen DLL noetig.
	/// </summary>
	internal sealed class ConfigurationManagerAttributes
	{
		public bool? IsAdvanced;

		public int? Order;

		// false = der ConfigurationManager blendet den Eintrag aus (wird beim
		// Oeffnen von F12 gelesen). Steuert "Show all settings" (QuickSetup.cs).
		public bool? Browsable;
	}
	internal static class Einstellungen
	{
		public static ConfigEntry<bool> An;

		public static ConfigEntry<Zielmodus> Art;

		public static ConfigEntry<Ausloeser> SoftWann;

		public static ConfigEntry<float> SoftRadius;

		public static ConfigEntry<Radiusart> SoftRadiusArt;

		public static ConfigEntry<float> SoftRadiusMeter;

		public static ConfigEntry<float> SoftRadiusMinGrad;

		public static ConfigEntry<float> SoftRadiusMaxGrad;

		public static ConfigEntry<bool> ZoneNachPanzerung;

		public static ConfigEntry<bool> NurFeinde;

		public static ConfigEntry<bool> ZoneKopf, ZoneRumpf, ZoneBecken, ZoneBeine;

		/// <summary>Darf auf diese Koerperstelle gezogen werden?</summary>
		public static bool Erlaubt(Knochen k)
		{
			switch (k)
			{
			case Knochen.Head:
				return ZoneKopf.Value;
			case Knochen.Chest:
				return ZoneRumpf.Value;
			case Knochen.Pelvis:
				return ZoneBecken.Value;
			default:
				return ZoneBeine.Value;
			}
		}

		public static ConfigEntry<float> RcsQuer;

		public static ConfigEntry<float> RcsHoch;

		// Je Waffenklasse (Index = Waffenklasse, 1..6 belegt).
		public static readonly ConfigEntry<bool>[] KlasseEigen = new ConfigEntry<bool>[8];

		public static readonly ConfigEntry<float>[] KlasseMeter = new ConfigEntry<float>[8];

		public static readonly ConfigEntry<float>[] KlasseStaerke = new ConfigEntry<float>[8];

		public static readonly ConfigEntry<float>[] KlasseReibung = new ConfigEntry<float>[8];

		public static readonly ConfigEntry<float>[] KlasseMagnet = new ConfigEntry<float>[8];

		public static readonly ConfigEntry<float>[] KlasseMagnetZeit = new ConfigEntry<float>[8];

		public static readonly ConfigEntry<float>[] KlasseMitAnteil = new ConfigEntry<float>[8];

		public static readonly ConfigEntry<float>[] KlasseMitDeckel = new ConfigEntry<float>[8];

		public static ConfigEntry<float> SoftMitAnteil;

		public static ConfigEntry<float> SoftSchubDauer;

		public static ConfigEntry<float> SoftSchubMax;

		public static ConfigEntry<float> SoftLoesenWeg;

		public static ConfigEntry<float> SoftWiedergreifen;

		public static ConfigEntry<bool> MitOhneHilfe;

		public static ConfigEntry<bool> DeckelDurchZoom;

		public static ConfigEntry<float> MagnetWeicht;

		public static ConfigEntry<bool> SoftFreizone;

		public static ConfigEntry<float> SoftFreizoneRand;

		public static ConfigEntry<bool> KampfAufzeichnung;

		public static ConfigEntry<int> AufzMaxMB;

		public static ConfigEntry<int> AufzMaxRaids;

		public static ConfigEntry<float> AufzSichtMeter;

		/// <summary>Die eigene ConfigFile (fuer die Kopfzeile der Aufzeichnung).</summary>
		public static ConfigFile Datei;

		public static readonly ConfigEntry<float>[] KlasseSchubDauer = new ConfigEntry<float>[8];

		public static readonly ConfigEntry<float>[] KlasseSchubMax = new ConfigEntry<float>[8];

		public static ConfigEntry<float> SoftMitDeckel;

		/// <summary>Die Werte, die fuer die Waffe in der Hand gelten.</summary>
		internal struct Werte
		{
			public Waffenklasse Klasse;

			public bool Eigen;

			public float Meter;

			public float Staerke;

			public float Reibung;

			public float Magnet;

			public float MagnetZeit;

			public float MitAnteil;

			public float MitDeckel;

			public float SchubDauer;

			public float SchubMax;
		}

		private static Werte _aktiv;

		private static int _aktivBild = -1;

		internal static Waffenklasse Klasse(Weapon w)
		{
			string k = w?.WeapClass ?? "";
			switch (k)
			{
			case "pistol":
				return Waffenklasse.Pistole;
			case "smg":
				return Waffenklasse.MP;
			case "assaultRifle":
			case "assaultCarbine":
				return Waffenklasse.Sturmgewehr;
			case "marksmanRifle":
			case "sniperRifle":
				return Waffenklasse.Praezision;
			case "shotgun":
				return Waffenklasse.Schrot;
			case "machinegun":
				return Waffenklasse.MG;
			case "grenadeLauncher":
				return Waffenklasse.Werfer;
			default:
				return Waffenklasse.Allgemein;
			}
		}

		/// <summary>
		/// Werte fuer die Waffe, die der eigene Spieler gerade haelt - einmal je
		/// Bild bestimmt. Klassen ohne "Eigene Werte" nehmen die allgemeinen
		/// aus "1b".
		/// </summary>
		internal static Werte Aktiv()
		{
			if (_aktivBild == Time.frameCount)
			{
				return _aktiv;
			}
			_aktivBild = Time.frameCount;
			Weapon w = null;
			try
			{
				w = Spielzugriff.Ich?.HandsController?.Item as Weapon;
			}
			catch
			{
			}
			Waffenklasse kl = Klasse(w);
			int i = (int)kl;
			Werte r = default(Werte);
			r.Klasse = kl;
			if (i >= 1 && i <= 6 && KlasseEigen[i] != null && KlasseEigen[i].Value)
			{
				r.Eigen = true;
				r.Meter = KlasseMeter[i].Value;
				r.Staerke = KlasseStaerke[i].Value;
				r.Reibung = KlasseReibung[i].Value;
				r.Magnet = KlasseMagnet[i].Value;
				r.MagnetZeit = KlasseMagnetZeit[i].Value;
				r.MitAnteil = KlasseMitAnteil[i].Value;
				r.MitDeckel = KlasseMitDeckel[i].Value;
				r.SchubDauer = KlasseSchubDauer[i].Value;
				r.SchubMax = KlasseSchubMax[i].Value;
			}
			else
			{
				r.Meter = SoftRadiusMeter.Value;
				r.Staerke = SoftStaerke.Value;
				r.Reibung = SoftReibung.Value;
				r.Magnet = SoftMagnet.Value;
				r.MagnetZeit = SoftMagnetZeit.Value;
				r.MitAnteil = SoftMitAnteil.Value;
				r.MitDeckel = SoftMitDeckel.Value;
				r.SchubDauer = SoftSchubDauer.Value;
				r.SchubMax = SoftSchubMax.Value;
			}
			_aktiv = r;
			return r;
		}

		/// <summary>
		/// DER Fangradius in Grad fuer eine Zone in dieser Entfernung (Meter,
		/// vom Feuerpunkt). Einziger Rechenweg - Zielsuche, Anziehung, Reibung
		/// und Magnet fragen alle hier.
		///   Art Meter: atan(Meter / Entfernung), begrenzt auf [Untergrenze, Obergrenze]
		///   Art Grad:  der feste Wert "Radius in Grad"
		/// </summary>
		public static float RadiusGrad(float entfernung)
		{
			if (SoftRadiusArt.Value == Radiusart.Degrees)
			{
				return Mathf.Max(0.01f, SoftRadius.Value);
			}
			float roh = Mathf.Atan(Mathf.Max(0.01f, Aktiv().Meter) / Mathf.Max(0.05f, entfernung)) * 57.29578f;
			float unten = Mathf.Min(SoftRadiusMinGrad.Value, SoftRadiusMaxGrad.Value);
			float oben = Mathf.Max(SoftRadiusMinGrad.Value, SoftRadiusMaxGrad.Value);
			return Mathf.Max(0.01f, Mathf.Clamp(roh, unten, oben));
		}

		public static ConfigEntry<float> SoftStaerke;

		public static ConfigEntry<float> SoftReibung;

		public static ConfigEntry<float> SoftDeckel;

		public static ConfigEntry<bool> SoftNurAnschlag;

		public static ConfigEntry<float> SoftMagnet;

		public static ConfigEntry<float> SoftMagnetZeit;

		public static ConfigEntry<Ausloeser> Modus;

		public static ConfigEntry<KeyCode> Taste;

		public static ConfigEntry<bool> Diagnose;

		public static ConfigEntry<float> DiagnoseTakt;

		public static ConfigEntry<float> Blickfeld;

		public static ConfigEntry<float> BlickfeldGrad;

		public static ConfigEntry<float> Reichweite;

		public static ConfigEntry<Knochen> Ziel;

		public static ConfigEntry<Kopfbox> Kopfziel;

		public static ConfigEntry<float> BoxHoehe;

		public static ConfigEntry<bool> NurSichtbare;

		// Ab 1.4.7: Sicht rein optisch (Durchschuss.NurOptisch).
		public static ConfigEntry<bool> NurOptisch;


		public static ConfigEntry<bool> DurchschussIgnorieren;

		public static ConfigEntry<float> DurchschussSchwelle;

		public static ConfigEntry<bool> NurKugelEbenen;

		// Ab 1.4.6: Buesche/Laub verdecken (Laub, unten).
		public static ConfigEntry<bool> LaubVerdeckt;

		public static ConfigEntry<float> LaubTiefe;

		public static ConfigEntry<float> Wechselvorsprung;

		public static ConfigEntry<bool> ScavsAuslassen;

		public static ConfigEntry<bool> PmcsAuslassen;

		public static ConfigEntry<bool> BosseAuslassen;

		public static ConfigEntry<float> Nachziehen;

		public static ConfigEntry<float> SoftaimHoechstens;

		public static ConfigEntry<bool> MuendungNachregeln;

		public static ConfigEntry<float> MuendungVerstaerkung;

		public static ConfigEntry<float> MuendungHoechstens;

		public static ConfigEntry<bool> BrustAusweichen;

		public static ConfigEntry<bool> VomFeuerpunkt;

		public static ConfigEntry<bool> MausVorrang;

		public static ConfigEntry<float> MausSchwelle;

		public static ConfigEntry<float> MausNachlauf;

		public static ConfigEntry<float> Totzone;

		public static ConfigEntry<float> NachfuehrungStaerke;

		public static ConfigEntry<float> NachfuehrungRuhe;

		public static ConfigEntry<float> NachfuehrungHoechstens;

		public static ConfigEntry<bool> VorhaltAn;

		public static ConfigEntry<bool> FallAusgleich;

		public static ConfigEntry<float> VorhaltFaktor;

		public static ConfigEntry<float> VorhaltHoechstens;

		public static ConfigEntry<float> VorhaltAbTempo;

		public static ConfigEntry<float> VorhaltRuhe;

		public static ConfigEntry<float> VorhaltRuheMin;

		public static ConfigEntry<float> Bildvorhalt;

		public static ConfigEntry<bool> NurWaagerecht;

		public static ConfigEntry<float> RueckstossAusgleich;

		public static ConfigEntry<float> RueckstossRuhe;

		public static ConfigEntry<float> RueckstossHoechstens;

		public static ConfigEntry<float> HuefteAusgleich;

		public static ConfigEntry<float> HuefteHoechstens;

		public static ConfigEntry<float> VorhaltDaempfung;

		// --- Menue-Werkzeuge -----------------------------------------------

		/// <summary>Eintrag nur fuer die alten Arten: Vermerk vorn, nur unter "Advanced settings" sichtbar.</summary>
		private static ConfigDescription Alt(string text)
		{
			return new ConfigDescription("[legacy modes only] " + text, null,
				new ConfigurationManagerAttributes { IsAdvanced = true });
		}

		private static ConfigDescription Alt(ConfigDescription d)
		{
			return new ConfigDescription("[legacy modes only] " + d.Description, d.AcceptableValues,
				new ConfigurationManagerAttributes { IsAdvanced = true });
		}

		/// <summary>Platz in der Sektion: hoeher = weiter oben (ConfigurationManager sortiert absteigend).</summary>
		private static ConfigDescription Reihe(string text, int platz)
		{
			return new ConfigDescription(text, null, new ConfigurationManagerAttributes { Order = platz });
		}

		private static ConfigDescription Reihe(ConfigDescription d, int platz)
		{
			return new ConfigDescription(d.Description, d.AcceptableValues,
				new ConfigurationManagerAttributes { Order = platz });
		}

		/// <summary>
		/// Die Eintraege, die BepInEx in der cfg gefunden, aber keinem Bind
		/// zugeordnet hat. BepInEx 5 haelt sie in der privaten Eigenschaft
		/// ConfigFile.OrphanedEntries und schreibt sie bei jedem Speichern
		/// zurueck - darum blieben alte Eintraege fuer immer in der Datei.
		/// </summary>
		private static Dictionary<ConfigDefinition, string> Waisen(ConfigFile c)
		{
			PropertyInfo pi = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			return pi?.GetValue(c, null) as Dictionary<ConfigDefinition, string>;
		}

		/// <summary>
		/// Nach allen Binds: was jetzt noch verwaist ist, gehoert zu keiner
		/// Einstellung mehr (alte Versionen, verschobene und schon uebernommene
		/// Eintraege). Loeschen und die cfg neu schreiben. Liefert die Namen.
		/// </summary>
		public static string WaisenEntfernen(ConfigFile c)
		{
			Dictionary<ConfigDefinition, string> w = Waisen(c);
			if (w == null || w.Count == 0)
			{
				return "";
			}
			List<string> namen = new List<string>();
			foreach (ConfigDefinition d in w.Keys)
			{
				namen.Add("[" + d.Section + "] " + d.Key);
			}
			w.Clear();
			c.Save();
			return string.Join(", ", namen.ToArray());
		}

		private static void KlasseAnlegen(ConfigFile c, Waffenklasse k, string sektion, bool eigen,
			float meter, float staerke, float reibung, float magnet, float zeit, string warum)
		{
			int i = (int)k;
			KlasseEigen[i] = Eintrag.B(c, sektion, "Use own values instead of general", eigen, Reihe(warum, 100));
			KlasseMeter[i] = Eintrag.B(c, sektion, "Radius in meters from zone edge", meter,
				new ConfigDescription("As in \"1b\", but for this class only (radius type Meters).", new AcceptableValueRange<float>(0.05f, 5f)));
			KlasseStaerke[i] = Eintrag.B(c, sektion, "Pull strength (weight)", staerke,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseReibung[i] = Eintrag.B(c, sektion, "Friction", reibung,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseMagnet[i] = Eintrag.B(c, sektion, "Hold without mouse (magnet), maximum degrees per second", magnet,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0f, 60f)));
			KlasseMagnetZeit[i] = Eintrag.B(c, sektion, "Magnet pull time in seconds", zeit,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0.02f, 1f)));
			KlasseMitAnteil[i] = Eintrag.B(c, sektion, "Target tracking share", 0.7f,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseMitDeckel[i] = Eintrag.B(c, sektion, "Target tracking maximum degrees per second", 60f,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0f, 200f)));
			KlasseSchubDauer[i] = Eintrag.B(c, sektion, "Capture boost duration (s)", 0.3f,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseSchubMax[i] = Eintrag.B(c, sektion, "Capture boost maximum degrees per second", 40f,
				new ConfigDescription("As in \"1b\", but for this class only.", new AcceptableValueRange<float>(0f, 150f)));
		}

		public static void Anlegen(ConfigFile c)
		{
			Datei = c;
			// Vor allen Binds merken, welche Eintraege die cfg schon unter den
			// neuen (englischen) Namen hat - die werden nie ueberschrieben.
			HashSet<ConfigDefinition> vorher = Einstellungsumzug.Vorhandene(c);
			KampfAufzeichnung = Eintrag.B(c, "8 Diagnostics", "Combat recording", defaultValue: false, Reihe(
				"Records every fight frame by frame (own player, mouse including all correction "
				+ "shares, enemies with hit zones, armor and AI intent, shots, hits) to "
				+ "BepInEx/Zielhilfe-Recordings/<date>-<map>/, one gzip file (JSON Lines) per fight. "
				+ "Format: see AUFZEICHNUNG-FORMAT.md next to the source code (field names are German). "
				+ "Written in a background thread.", 50));
			AufzMaxMB = Eintrag.B(c, "8 Diagnostics", "Recording limit per raid (MB)", 200, Reihe(new ConfigDescription(
				"After this, nothing more is recorded in this raid.",
				new AcceptableValueRange<int>(10, 5000)), 49));
			AufzMaxRaids = Eintrag.B(c, "8 Diagnostics", "Recording: keep only the last x raids", 20, Reihe(new ConfigDescription(
				"Older raid folders are deleted when a new raid starts.",
				new AcceptableValueRange<int>(1, 500)), 48));
			AufzSichtMeter = Eintrag.B(c, "8 Diagnostics", "Fight starts when an enemy sees you, up to x meters", 150f, Reihe(new ConfigDescription(
				"A fight starts with: target inside the capture area, own shot, a hit on you, "
				+ "or an enemy that has you as its target and sees you (up to this distance). "
				+ "It ends 4 s after the last such activity; 1 s before the start is recorded as well.",
				new AcceptableValueRange<float>(10f, 1000f)), 47));
			An = Eintrag.B(c, QuickSetup.Sektion, "Enabled", defaultValue: true, Reihe("Master switch. Off = the mod does nothing (aim assist and Enemy ESP).", 100));
			Modus = Eintrag.B(c, "9 Legacy Modes - General", "When it pulls", Ausloeser.HoldKey, Alt("HoldKey = only while the key is held (recommended). ToggleKey = key switches on and off. Always = permanently."));
			Taste = Eintrag.B(c, "1 General", "Hotkey", KeyCode.LeftAlt, Reihe("Used for \"When active\" = HoldKey or ToggleKey (and for the legacy modes).", 90));
			Diagnose = Eintrag.B(c, "8 Diagnostics", "Diagnostics to log", defaultValue: false, "On = writes a line to the BepInEx log at the diagnostics interval; with Soft Aim (mouse) also a SHOT line for each own shot and a HIT line for each hit: whether a raid is running, whether the key works, how many enemies were candidates and why they were dropped. Only switch on for troubleshooting.");

			DiagnoseTakt = Eintrag.B(c, "8 Diagnostics", "Diagnostics interval in seconds", 1f, new ConfigDescription(
					"How often a diagnostics line is written. 1 second is enough to "
					+ "see whether something is fundamentally stuck. To examine single "
					+ "shots set it MUCH smaller - 0 = every frame. Then you can see whether "
					+ "the aim point really jumps or whether the sampling was just too coarse. "
					+ "Careful: at 0 the log grows quickly.",
					new AcceptableValueRange<float>(0f, 5f)));

			// --- Soft Aim per Mauseingabe ------------------------------------
			Art = Eintrag.B(c, "1b Soft Aim (mouse)", "Assist type", Zielmodus.SoftAimMouse, Reihe(new ConfigDescription(
					"SoftAimMouse = real soft aim (recommended). The assist never turns on its "
					+ "own. It intercepts your mouse movement and bends it a bit towards the "
					+ "target as long as the weapon's point of impact is inside the radius around "
					+ "the hit zone. Mouse still = no correction.\n"
					+ "LegacySoftAim = the former mode \"soft aim instead of lock-on\": "
					+ "a capped rotation towards the target every frame, even without mouse input.\n"
					+ "LegacyLockOn = the view direction is set onto the target every frame.\n"
					+ "Their settings are in the \"9 Legacy Modes\" sections (only visible with "
					+ "\"Advanced settings\"); SoftAimMouse uses this section (plus target selection, "
					+ "line-of-sight check and exclusions from \"2 Target\"/\"3\" and the "
					+ "lead from \"5\")."), 100));
			SoftWann = Eintrag.B(c, "1b Soft Aim (mouse)", "When active", Ausloeser.Always, Reihe(new ConfigDescription(
					"Always = permanently ready. Harmless, because nothing happens without your own "
					+ "mouse movement. HoldKey / ToggleKey = as before, with the "
					+ "hotkey from \"1 General\"."), 95));
			SoftNurAnschlag = Eintrag.B(c, "1b Soft Aim (mouse)", "Only when aiming down sights (ADS)", defaultValue: false, Reihe("On = only while aiming down sights. Off = also from the hip. In both cases the calculation uses the weapon's "
				+ "pointing direction, not the camera - "
				+ "from the hip the muzzle points a few degrees off screen center, "
				+ "and the assist aims where the bullet will actually go.", 90));
			ZoneNachPanzerung = Eintrag.B(c, "1b Soft Aim (mouse)", "Choose zone by armor", defaultValue: true, Reihe("On = instead of the fixed body part, the zone with the best expected "
				+ "hit: for each enemy the worn armor is queried (same calculation as the game: "
				+ "ShotMatches + penetration chance from armor class, durability and penetration "
				+ "of the loaded round), plus the enemy's facing direction (chest/back plate, sides). "
				+ "Weights: head 1.0, torso 0.6, pelvis 0.25 (head and thorax are lethal, the belly is not). "
				+ "On the head, the least armored FREE head box is chosen (e.g. face "
				+ "without visor). Off = body part from \"2 Target\" as before.", 85));
			ZoneKopf = Eintrag.B(c, "1b Soft Aim (mouse)", "Allowed zone: Head", defaultValue: true, Reihe(
				"May the assist pull onto the head (zone selection, fallback zones).", 84));
			ZoneRumpf = Eintrag.B(c, "1b Soft Aim (mouse)", "Allowed zone: Torso", defaultValue: true, Reihe(
				"May the assist pull onto the thorax.", 83));
			ZoneBecken = Eintrag.B(c, "1b Soft Aim (mouse)", "Allowed zone: Pelvis", defaultValue: false, Reihe(
				"Off = pelvis only as a fallback, when head AND torso are covered or cannot be "
				+ "penetrated by the round (then pelvis first, then legs).", 82));
			ZoneBeine = Eintrag.B(c, "1b Soft Aim (mouse)", "Allowed zone: Legs", defaultValue: false, Reihe(
				"Off = legs only as a last resort (head and torso not possible, pelvis neither). "
				+ "A leg hit does not kill; with weight 0.1 (about 10 hits to kill) "
				+ "they would otherwise only win if the head is almost never hit.", 81));
			NurFeinde = Eintrag.B(c, "1b Soft Aim (mouse)", "Enemies only", defaultValue: true, Reihe("On = only bots whose group is hostile to you - the game's own logic "
				+ "(BotsGroup: listed as ally = never, already registered as enemy = yes, otherwise IsPlayerEnemy: side, karma, boss loyalty, "
				+ "friend/warn/enemy types from the bot settings). As a Scav, "
				+ "peaceful Scavs stay out until they turn hostile. Applies to all "
				+ "modes. \"3 Exclusions\" applies in addition.", 80));
			RcsQuer = Eintrag.B(c, "1b Soft Aim (mouse)", "Recoil compensation horizontal (%)", 0f, Reihe(new ConfigDescription(
					"Only while shooting, subtracts the NEWLY added weapon recoil, "
					+ "independent of a target, via the mouse movement (does not set the camera). "
					+ "It measures how far the shot line has moved relative to the "
					+ "body rotation since the last frame while the weapon's recoil "
					+ "(HandRotationRecoil + WeaponRecoil) is acting. 0 = off "
					+ "(default, because you pull the recoil down yourself - both together "
					+ "would overcorrect). Try 50.",
					new AcceptableValueRange<float>(0f, 100f)), 20));
			RcsHoch = Eintrag.B(c, "1b Soft Aim (mouse)", "Recoil compensation vertical (%)", 0f, Reihe(new ConfigDescription(
					"Like horizontal, for the vertical part (the larger one in EFT). 0 = off. "
					+ "Try 50.",
					new AcceptableValueRange<float>(0f, 100f)), 25));
			KlasseAnlegen(c, Waffenklasse.Pistole, "1c Class Pistol", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Off = general values from \"1b\". Pistols: short range, the maximum in "
				+ "degrees applies anyway - no reason to deviate.");
			KlasseAnlegen(c, Waffenklasse.MP, "1d Class SMG", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Off = general values from \"1b\".");
			KlasseAnlegen(c, Waffenklasse.Sturmgewehr, "1e Class Assault Rifle and Carbine", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Off = general values from \"1b\" - they are tuned for this case (10-80 m, "
				+ "automatic and single fire).");
			KlasseAnlegen(c, Waffenklasse.Praezision, "1f Class DMR and Sniper", true, 0.5f, 0.5f, 0.4f, 10f, 0.05f,
				"On by default: single shots at long range. Pull time as in general, "
				+ "0.05 s (simulation holding still: 30 m 96 %, 80 m 38 % head hits). Radius 0.5 m, because at "
				+ "range the minimum in degrees applies anyway and close neighbors should not "
				+ "be caught as well; friction 0.4 for fine settling in the scope.");
			KlasseAnlegen(c, Waffenklasse.Schrot, "1g Class Shotgun", true, 1f, 0.5f, 0.3f, 10f, 0.05f,
				"On by default: buckshot spreads (buckshotDispersion), a narrow miss "
				+ "often still hits with some of the pellets - the capture area may be wider "
				+ "(1 m instead of 0.75 m). May be switched off for slugs.");
			KlasseAnlegen(c, Waffenklasse.MG, "1h Class Machine Gun", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Off = general values from \"1b\".");
			SoftRadiusArt = Eintrag.B(c, "1b Soft Aim (mouse)", "Radius type", Radiusart.Meters, Reihe(new ConfigDescription(
					"Meters = the capture area is a DISTANCE at the enemy (\"Radius in meters "
					+ "from zone edge\"), converted with each enemy's distance: "
					+ "radius in degrees = atan(meters / distance). A fixed angle is too narrow up close "
					+ "and too wide far away: 3 degrees are only 16 cm at 3 m, but 4.2 m at 80 m. "
					+ "Degrees = the fixed value \"Radius in degrees\", as before."), 75));
			SoftRadiusMeter = Eintrag.B(c, "1b Soft Aim (mouse)", "Radius in meters from zone edge", 0.75f, Reihe(new ConfigDescription(
					"Only for radius type Meters. How far beside the hit zone the assist still "
					+ "acts, in meters at the enemy. Simulation (sweep across past the head, "
					+ "strength 0.5, friction 0.3): 0.5 m only pulls in lines that are up to about "
					+ "0.1 m off; 0.75 m also pulls lines 0.25 m off onto the head at 10-80 m; "
					+ "1 m pulls 0.5 m off in to 0.2 m.",
					new AcceptableValueRange<float>(0.05f, 5f)), 70));
			SoftRadiusMaxGrad = Eintrag.B(c, "1b Soft Aim (mouse)", "Maximum radius (degrees)", 12f, Reihe(new ConfigDescription(
					"Only for radius type Meters. Upper limit for close combat: at 1-2 m "
					+ "0.75 m would be 20-37 degrees, a large part of the screen. 12 degrees is "
					+ "about a sixth of the screen height on each side and still "
					+ "larger than the head at 1 m (5.7 degrees); the limit is reached from "
					+ "about 3.5 m.",
					new AcceptableValueRange<float>(1f, 30f)), 65));
			SoftRadiusMinGrad = Eintrag.B(c, "1b Soft Aim (mouse)", "Minimum radius (degrees)", 1f, Reihe(new ConfigDescription(
					"Only for radius type Meters. Lower limit for long range: 0.75 m "
					+ "would be only 0.14 degrees at 300 m - less than the weapon sway when "
					+ "aiming down sights. 1 degree (before: 0.3) applies from about 43 m: at range "
					+ "the assist then catches earlier (simulation 80 m: 90 % of the time to head "
					+ "1.5 -> 1.0 s). That the larger area does not stick is handled by \"Release "
					+ "after travel\".",
					new AcceptableValueRange<float>(0.05f, 5f)), 60));
			SoftRadius = Eintrag.B(c, "1b Soft Aim (mouse)", "Radius in degrees", 3f, Reihe(new ConfigDescription(
					"Only for radius type Degrees. "
					+ "How close the weapon's point of impact must be to a hit zone "
					+ "for the assist to act - measured from the EDGE of the zone (head, "
					+ "torso, pelvis), as an angle, so it stays the same when zooming. Anyone aiming "
					+ "at the body in close combat is inside the zone and not 8 degrees "
					+ "beside the head point. Towards the edge of the radius the effect fades to 0. "
					+ "Measured from the fire point along the shot direction, the zones "
					+ "are shifted by lead and bullet drop. The zone of the selected "
					+ "body part has priority; the nearest other one helps while "
					+ "the own one is out of reach. In this mode it replaces the field of view of the "
					+ "legacy modes.",
					new AcceptableValueRange<float>(0.2f, 15f)), 55));
			SoftStaerke = Eintrag.B(c, "1b Soft Aim (mouse)", "Pull strength (weight)", 0.5f, Reihe(new ConfigDescription(
					"How strongly the mouse movement is bent towards the target. Calculated as "
					+ "result = w x target vector + (1 - w) x mouse movement; the target vector "
					+ "points at the target and is as long as your own movement. This "
					+ "setting is w inside the radius. 0 = off, 1 = the movement points "
					+ "fully at the target. The assist never reverses your movement: if you pull "
					+ "away from the target you always get away. It backs off by itself as soon as "
					+ "your own movement already reaches the target point, and inside the zone it fades "
					+ "to 0 towards the center - so nothing snaps or sticks.",
					new AcceptableValueRange<float>(0f, 1f)), 50));
			SoftReibung = Eintrag.B(c, "1b Soft Aim (mouse)", "Friction", 0.3f, Reihe(new ConfigDescription(
					"Slows the part of your movement that goes ACROSS or AWAY from the target "
					+ "while you are inside the radius - full in the middle, fading to 0 "
					+ "towards the edge. Movement towards the target is never slowed. 0 = off, "
					+ "0.3 = default (noticeable, in the simulation never more than 30 % "
					+ "of forward motion lost), 1 = in the middle the sideways movement stands still "
					+ "(feels sticky).",
					new AcceptableValueRange<float>(0f, 1f)), 45));
			SoftDeckel = Eintrag.B(c, "1b Soft Aim (mouse)", "Maximum correction (degrees per second)", 120f, Reihe(new ConfigDescription(
					"Upper limit for what the assist changes in your movement "
					+ "(pull and friction together). Converted per frame with the frame time "
					+ "and therefore independent of the frame rate. On fast "
					+ "sweeps the cap applies, and the assist then hardly shows.",
					new AcceptableValueRange<float>(1f, 600f)), 40));

			SoftMagnet = Eintrag.B(c, "1b Soft Aim (mouse)", "Hold without mouse (magnet), maximum degrees per second", 10f, Reihe(new ConfigDescription(
					"The only part that also acts with a STILL mouse: if the "
					+ "shot line is inside the radius of a hit zone, it is slowly pulled back to the "
					+ "target point - against sway and recoil. The bullet's real flight direction is "
					+ "measured, so the recoil is already included. At most this many degrees per second; "
					+ "it fades towards the edge of the radius and eases off when you pull away. 0 = off (then "
					+ "again: mouse still = no correction). In the simulation: holding still "
					+ "with sustained fire at 30 m, 0 % head hits without, about 80 % with the magnet. "
					+ "Above 10 it gets sticky: already at 20 you can no longer get off the head "
					+ "with a very slow mouse movement (5 degrees/s); from 10 degrees/s you always can.",
					new AcceptableValueRange<float>(0f, 60f)), 35));
			SoftFreizone = Eintrag.B(c, "1b Soft Aim (mouse)", "Free movement inside hit zone", defaultValue: true, Reihe(
				"On = as long as the predicted impact (real shot line incl. lead and bullet drop) lies inside the "
				+ "desired hit zone minus the inner margin, pull, friction and magnet do NOT act against your own "
				+ "mouse movement - you can correct freely on the target. The magnet then holds the line at the "
				+ "point YOU chose (it moves with your mouse) instead of the zone centre, so recoil and sway are "
				+ "still caught. Towards the zone edge (inside the margin) the normal pull/friction/magnet fade "
				+ "back in, outside the zone they act as before. Target tracking stays. Off = old behaviour "
				+ "(magnet always pulls to the zone centre). Simulation 30 m, holding still with sustained fire: "
				+ "head per shot 98 -> 96 %. Trade-off: hand tremor is also your own movement and is no longer "
				+ "pulled back to the centre inside the free zone (bots 30 m, tremor 5 degrees/s: head per shot "
				+ "25 -> 17 %; a larger inner margin brings it back: 30 % -> 18, 45 % -> 21).", 34));
			SoftFreizoneRand = Eintrag.B(c, "1b Soft Aim (mouse)", "Free zone inner margin (%)", 15f, Reihe(new ConfigDescription(
				"Safety margin of the free zone inside the hit zone, in percent of the zone width (at least 2 cm "
				+ "at the target, at most 90 % of the half width). 15 = the outer 15 % of the zone width on each "
				+ "side are not free: there the assist fades back in so the line does not slip off the edge. "
				+ "Larger = smaller free core, safer; 0 = free up to the edge (still at least 2 cm).",
				new AcceptableValueRange<float>(0f, 45f)), 33));
			SoftMitAnteil = Eintrag.B(c, "1b Soft Aim (mouse)", "Target tracking share", 0.7f, Reihe(new ConfigDescription(
					"Turns along with moving targets - but only the DIFFERENCE: share x (turn rate "
					+ "of the line fire point-target point minus your own turn rate), both "
					+ "smoothed (0.08 s). If you already lead yourself, nothing is added. Only inside the "
					+ "capture area (like the magnet); if you pull away from the zone it releases (fully "
					+ "from 10 degrees/s) - you get away as fast as without it. 0 = off. Simulation "
					+ "(head per shot without/with): zigzag 30 m 28 -> 34 %, 80 m 19 -> 24 %; "
					+ "running straight 80 m 31 -> 37 %; turning away unchanged.",
					new AcceptableValueRange<float>(0f, 1f)), 28));
			SoftMitDeckel = Eintrag.B(c, "1b Soft Aim (mouse)", "Target tracking maximum degrees per second", 60f, Reihe(new ConfigDescription(
					"Upper limit of target tracking. A walking enemy at 10 m turns at about "
					+ "25 degrees/s, a sprinting one at 5 m at 70.",
					new AcceptableValueRange<float>(0f, 200f)), 27));
			DeckelDurchZoom = Eintrag.B(c, "1b Soft Aim (mouse)", "Divide caps by zoom", defaultValue: true, Reihe(
				"All corrections are in degrees (correct for the hit), but in a scope they appear "
				+ "larger by the magnification: 40 degrees/s capture boost feels like 240 at 6x. On = "
				+ "capture boost, target tracking and the pull cap are divided by the magnification, "
				+ "at most down to the magnet cap. The magnet itself stays: dividing it costs "
				+ "almost all hits (simulation holding still with sustained fire, 30 m: 95 % at 10 degrees/s, "
				+ "28 % at 10/4, 7 % at 10/6) - it carries the recoil recovery.", 29));
			MagnetWeicht = Eintrag.B(c, "1b Soft Aim (mouse)", "Magnet yields to own mouse above x degrees per second (screen)", 10f, Reihe(new ConfigDescription(
				"If you steer faster than this value - measured on screen (degrees per second x "
				+ "magnification) and relative to the target's movement - the magnet no longer holds AGAINST "
				+ "your movement (it only pulls across it). With a still or trembling mouse it holds fully "
				+ "(recoil recovery). 10 = boundary between trembling/fine correction and steering (75 % of "
				+ "measured mouse movements at 6x were below it). Simulation 80 m, 6x: swallowed "
				+ "own movement when sweeping away 17 -> 2 %, head while tracking 72 -> 59 %. 0 = off "
				+ "(more hits, more resistance), 3 = even freer (head 42 %).",
				new AcceptableValueRange<float>(0f, 100f)), 30));
			SoftSchubDauer = Eintrag.B(c, "1b Soft Aim (mouse)", "Capture boost duration (s)", 0.3f, Reihe(new ConfigDescription(
					"When the shot line enters the capture area of the desired zone (also right "
					+ "after a target change), the magnet may pull harder for this time "
					+ "(\"Capture boost maximum\"). Aborts immediately if you pull away from the zone at least "
					+ "10 degrees/s or \"Release after travel\" applies.",
					new AcceptableValueRange<float>(0f, 1f)), 26));
			SoftSchubMax = Eintrag.B(c, "1b Soft Aim (mouse)", "Capture boost maximum degrees per second", 40f, Reihe(new ConfigDescription(
					"Magnet cap during the capture boost instead of the normal one. 0 = off. "
					+ "Simulation 10 m: 90 % of the time to head after release 0.72 -> about 0.4 s.",
					new AcceptableValueRange<float>(0f, 150f)), 25));
			MitOhneHilfe = Eintrag.B(c, "1b Soft Aim (mouse)", "Tracking: do not count assist rotation as own", true, new ConfigDescription(
					"Target tracking compares the target's turn rate with YOUR rotation. On: the rotation the "
					+ "assist makes itself does not count - so it tracks walking enemies "
					+ "at full speed instead of only ~41 %. Off = old behavior."));
			SoftWiedergreifen = Eintrag.B(c, "1b Soft Aim (mouse)", "Re-engage after release (s)", 0.06f, new ConfigDescription(
					"If the assist was released by your own mouse movement, it engages again as soon as the mouse "
					+ "has been calm for this long - instead of waiting until the travel sum has decayed (up to 0.5 s). "
					+ "Recording Interchange 02.11. (6x, 50 m, enemy walking 2-5 m/s): released four times by "
					+ "small corrections, afterwards 0.3-0.5 s of no following, distance grew to ~1 degree. 0 = off.",
					new AcceptableValueRange<float>(0f, 0.5f)));
			SoftLoesenWeg = Eintrag.B(c, "1b Soft Aim (mouse)", "Release after travel (degrees)", 2f, Reihe(new ConfigDescription(
					"Sums up how far you move the mouse AWAY from the desired zone (towards the zone "
					+ "does not count, decays with 0.5 s). From this value on, magnet, target tracking "
					+ "and capture boost are fully off until the sum has fallen below half. "
					+ "Pull and friction stay. Against sticking when slowly sweeping "
					+ "past (simulation 5 degrees/s, 10-30 m: from almost always stuck to "
					+ "10-22 %). 0 = off.",
					new AcceptableValueRange<float>(0f, 10f)), 24));
			SoftMagnetZeit = Eintrag.B(c, "1b Soft Aim (mouse)", "Magnet pull time in seconds", 0.05f, Reihe(new ConfigDescription(
					"How fast the magnet removes a remainder: after this time about "
					+ "two thirds are gone. Small = holds firmer, large = softer. The "
					+ "hit rate when holding still depends mostly on this value, not on "
					+ "the cap above it. Simulation with measured SMG data (35 m, 800 rounds/min, "
					+ "recoil, sway, spread, one frame of measurement delay): 0.1 s = 49/58 % "
					+ "head hits at 60/144 fps, 0.05 s = 65/77 %, 0.03 s = 70/82 %. Below "
					+ "0.05 s it gets restless at a low frame rate (gain per frame at "
					+ "30 fps 0.67 instead of 0.49).",
					new AcceptableValueRange<float>(0.02f, 1f)), 30));

			Blickfeld = Eintrag.B(c, "9 Legacy Modes - Target", "Field of view (pixels)", 61f, Alt(new ConfigDescription("How far an enemy may be from the screen center to be considered at all. Small = only what you are almost aiming at.", new AcceptableValueRange<float>(10f, 600f))));
			BlickfeldGrad = Eintrag.B(c, "9 Legacy Modes - Target", "Field of view (degrees)", 3f, Alt(new ConfigDescription("Recommended instead of the pixel value: an angle stays the same when zooming. The pixel value does not - with a scope the camera's field of view shrinks, the same enemy is then mathematically many times farther from the center and drops out of the field of view. That is exactly why the aim assist helped at a low zoom level and no longer at a high one. 0 = use the pixel value after all.", new AcceptableValueRange<float>(0f, 30f))));
			Reichweite = Eintrag.B(c, "2 Target", "Range in meters", 930f, new ConfigDescription("Enemies farther away are ignored.", new AcceptableValueRange<float>(10f, 1500f)));
			Ziel = Eintrag.B(c, "2 Target", "Body part", Knochen.Head, "Head, Chest, Pelvis or Legs. With \"Choose zone by armor\" "
				+ "the assist picks the zone itself; then this value only determines the point at which "
				+ "target selection checks the line of sight.");
			Kopfziel = Eintrag.B(c, "2 Target", "Head hitbox", Kopfbox.Visible,
				new ConfigDescription(
					"What is aimed at when the body part is \"Head\". In the game the head "
					+ "is not a single box but eight - all count as a hit on the "
					+ "head, they differ in position and armor.\n"
					+ "Visible = recommended. Checks every box individually and takes the first "
					+ "one that is really FREE from the eye. If the enemy is behind a "
					+ "wall edge, half the head is covered - aiming at the covered half "
					+ "means shooting into the wall. Order of preference: head center, face, jaw, skull top, back of head, "
					+ "ears, front neck, back neck (with \"Choose zone by armor\" the least "
					+ "armored first). If none is free, the "
					+ "preferred one is taken.\n"
					+ "HeadCenter = always the big head box (HeadCommon), without a check.\n"
					+ "Face = the eye area (Eyes). Smaller, but the spot that helmets "
					+ "cover least often.\n"
					+ "Jaw = Jaw, just below.\n"
					+ "Skull = ParietalHead, on top."));

			BoxHoehe = Eintrag.B(c, "2 Target", "Height within head box", 0.72f, new ConfigDescription(
				"WHERE in the hit box to aim. 0 = bottom edge, 0.5 = middle, 1 = top edge. "
				+ "The middle of the head box is at chin height, not in the middle of the skull - "
				+ "at 0.5 the assist therefore visibly aimed too low. The value is converted to the "
				+ "actual height of the box, so it adapts by itself "
				+ "when the enemy is crouched or another box is chosen.",
				new AcceptableValueRange<float>(0f, 1f)));
			VomFeuerpunkt = Eintrag.B(c, "9 Legacy Modes - Target", "Aim angle from fire point instead of camera", defaultValue: true, Alt(new ConfigDescription(
					"The angle to the target used to be calculated from the CAMERA. "
					+ "The bullet starts at the muzzle, though, which sits quite a bit "
					+ "lower and further forward. At short range this offset is not a rounding error: "
					+ "10 cm of distance are already 0.57 degrees at 10 m - more than the error the assist "
					+ "is meant to remove in the first place. That is exactly why the measured "
					+ "muzzle error rose in close combat (0.70 degrees under 10 m versus "
					+ "0.30 degrees at 55-80 m), although nothing was aimed any worse there.\n\n"
					+ "When enabled, the aim angle is calculated from the fire point. A side effect that is NOT a bug: "
					+ "the crosshair then visibly sits slightly beside the enemy up close - "
					+ "that is parallax, and it belongs there. The shot "
					+ "lands, not the crosshair.\n\n"
					+ "The turn-rate measurement for target tracking deliberately stays on "
					+ "the camera: the fire point wobbles with the weapon animation, "
					+ "and that wobble has no place in a speed measurement.")));
			BrustAusweichen = Eintrag.B(c, "2 Target", "Fall back to chest when head is covered",
				defaultValue: true,
				new ConfigDescription(
					"Only without \"Choose zone by armor\" (there a covered head zone drops out anyway). "
					+ "If NONE of the eight head boxes is free, the assist used to aim at the "
					+ "head anyway - right into the cover standing in front of it. When enabled it then takes the chest: larger, "
					+ "and above all actually hittable. In the raid of 18.09.2026 "
					+ "this concerned 6 of 1231 frames, so it is not a big lever - "
					+ "but in exactly those frames the shot was certainly wasted."));
			NurSichtbare = Eintrag.B(c, "2 Target", "Clear line of sight only", defaultValue: true, "On = enemies behind walls are skipped.");
			NurOptisch = Eintrag.B(c, "2 Target", "Only visible targets (cover blocks even if penetrable)", defaultValue: true,
				new ConfigDescription(
					"On = visibility means what you can SEE, not where a bullet would get through. Every opaque "
					+ "obstacle hides an enemy - tarps, fabric, cardboard, thin sheet metal, wooden boards, plastic, "
					+ "car bodies - even if your round would penetrate it. Only really see-through things count as "
					+ "clear: clear glass (not frosted/milky), broken glass, chain-link fence and grates (they have "
					+ "gaps), open windows (nothing there), low grass; bushes, tree crowns and tall grass by depth "
					+ "(\"Foliage depth that hides\"). At least one of head, chest or pelvis must be visible for the "
					+ "assist to pull. Applies to aiming, zone choice, the AFK guard (it only fires at a visible "
					+ "enemy) and the ESP visible/covered display. "
					+ "Off = old behavior (penetrable cover counts as clear, \"Shoot-through from probability\")."));

			DurchschussIgnorieren = Eintrag.B(c, "2 Target", "Ignore shoot-through obstacles", defaultValue: true,
				"On = glass, chain-link fence, grates, tall grass and the like do "
				+ "NOT count as a sight obstruction. They stop the line-of-sight ray, but "
				+ "not the bullet - without this exception the assist drops enemies "
				+ "behind a window pane although you can shoot them. "
				+ "The rating comes from the game itself (material type and "
				+ "penetration values of the collider that was hit), not from a "
				+ "guessed list. Bodies never count as permeable.");

			DurchschussSchwelle = Eintrag.B(c, "2 Target", "Shoot-through from probability", 0.5f,
				new ConfigDescription(
					"Only used when \"Only visible targets\" is off. For everything that is not see-through anyway (wood, sheet metal, plastic ...): "
					+ "from which probability it counts as permeable. Calculated as in the "
					+ "game with your OWN loaded round: only if its penetration power "
					+ "is above the material's level, then chance = obstacle chance of the "
					+ "round + chance of the material. 1 = only what surely goes through.",
					new AcceptableValueRange<float>(0f, 1f)));
			NurKugelEbenen = Eintrag.B(c, "2 Target", "Check only layers bullets travel on", defaultValue: true,
				new ConfigDescription(
					"The line-of-sight ray used to run over ALL Unity layers. As a result it was stopped by "
					+ "things no bullet flies through - above all the "
					+ "invisible cubes of the visibility system (\"Cube (4)\" and "
					+ "similar, reported in the log as [no ballistic collider]). "
					+ "In an open field such a cube stood 11 m in front of an enemy at "
					+ "162 m and the aim assist did nothing at all. When enabled, only the layers the game "
					+ "itself uses for projectiles are checked. Switching off restores the old behavior."));
			LaubVerdeckt = Eintrag.B(c, "2 Target", "Bushes and foliage block aim assist", defaultValue: true,
				new ConfigDescription(
					"On = an enemy hidden behind or inside a bush, tree crown or tall grass counts as covered, "
					+ "even though a bullet would pass through. Uses the same foliage layer the game's bots use "
					+ "for their own vision (bush and tree trigger volumes), plus colliders made of the material "
					+ "\"tall grass\". Low grass on the ground never blocks. At least one of head, chest or pelvis "
					+ "must be clearly visible for the assist to act. Off = old behavior (foliage is ignored)."));
			LaubTiefe = Eintrag.B(c, "2 Target", "Foliage depth that hides (m)", 0.3f,
				new ConfigDescription(
					"How much foliage the line to a body point may cross before it counts as covered "
					+ "(length of the line inside bush/tree volumes). The game's bots stop seeing "
					+ "through foliage at 0.4-0.8 m. Smaller = stricter.",
					new AcceptableValueRange<float>(0f, 2f)));
			Wechselvorsprung = Eintrag.B(c, "2 Target", "Target switch margin (pixels)", 40f, new ConfigDescription("The current target is kept as long as no other one is better by at least this many pixels. Prevents jumping between two enemies standing next to each other. 0 = always the nearest.", new AcceptableValueRange<float>(0f, 300f)));
			ScavsAuslassen = Eintrag.B(c, "3 Exclusions", "Scavs", defaultValue: false, "");
			PmcsAuslassen = Eintrag.B(c, "3 Exclusions", "PMCs", defaultValue: false, "");
			BosseAuslassen = Eintrag.B(c, "3 Exclusions", "Bosses and guards", defaultValue: false, "");
			Nachziehen = Eintrag.B(c, "9 Legacy Modes - Movement", "Pull speed", 60f, Alt(new ConfigDescription("How fast the weapon pulls onto the target, in 1/second. High = hard and fast, low = soft. IMPORTANT: this value is independent of the frame rate - unlike a per-frame smoothing factor, which pulls faster by itself at high fps and changes its speed with every stutter.", new AcceptableValueRange<float>(2f, 200f))));
			SoftaimHoechstens = Eintrag.B(c, "9 Legacy Modes - Movement", "Soft aim maximum degrees per second", 80f, Alt(new ConfigDescription(
					"ONLY for \"Assist type\" = LegacySoftAim. "
					+ "How much the assist may contribute per second at most in soft aim mode. "
					+ "THIS is the setting for the feel: "
					+ "small = the assist only nudges gently and you lead, large = it "
					+ "takes the fine work off you. It can never overrule you, "
					+ "because your own rotation is left untouched. 80 is deliberately "
					+ "restrained - if it takes over too little, raise it; "
					+ "if it still feels sticky, go down to 40.",
					new AcceptableValueRange<float>(10f, 600f))));
			Totzone = Eintrag.B(c, "9 Legacy Modes - Movement", "Dead zone (degrees)", 0.05f, Alt(new ConfigDescription("Below this deviation no more correction is made. Prevents a fine constant jitter right on the target.", new AcceptableValueRange<float>(0f, 1f))));

			MausVorrang = Eintrag.B(c, "9 Legacy Modes - Movement", "Own mouse has priority", defaultValue: true, Alt("On = as soon as you turn fast yourself, the assist lets go. Without this "
				+ "it resets your view direction every frame and you cannot get away from "
				+ "a target once it is acquired."));

			MausSchwelle = Eintrag.B(c, "9 Legacy Modes - Movement", "Priority above x degrees per second", 50f, Alt(new ConfigDescription(
					"At which OWN turn speed the assist lets go. What is measured "
					+ "is the difference between what the assist last set "
					+ "and what is actually there in the next frame - that is your mouse. "
					+ "Too small and even recoil makes it let go; too large and you "
					+ "cannot get off the target. 50 is a calm starting value.",
					new AcceptableValueRange<float>(5f, 400f))));

			MausNachlauf = Eintrag.B(c, "9 Legacy Modes - Movement", "Then do not pull for x seconds", 0.25f, Alt(new ConfigDescription(
					"How long the assist stays off after your last turn. Without "
					+ "this delay it snaps back to the old target immediately between two "
					+ "mouse movements.",
					new AcceptableValueRange<float>(0f, 2f))));
			NachfuehrungStaerke = Eintrag.B(c, "9 Legacy Modes - Movement", "Track moving targets", 1f, Alt(new ConfigDescription("The most important setting against \"hits standing targets well, moving ones badly\". Plain pulling ALWAYS stays a bit behind a walking enemy - the lag is turn speed divided by pull speed. Here the turn speed of the aim line is measured and exactly that is turned along. 1.0 = lag removed completely. 0 = off, behaves as before. Above 1.0 it runs ahead of the target.", new AcceptableValueRange<float>(0f, 1.5f))));
			NachfuehrungRuhe = Eintrag.B(c, "9 Legacy Modes - Movement", "Tracking smoothing (seconds)", 0.08f, Alt(new ConfigDescription("Over how much time the measured turn speed is averaged. Small = reacts immediately to direction changes, but picks up every twitch. Large = calmer, lags briefly on sidesteps.", new AcceptableValueRange<float>(0.02f, 0.5f))));
			NachfuehrungHoechstens = Eintrag.B(c, "9 Legacy Modes - Movement", "Tracking maximum degrees per second", 150f, Alt(new ConfigDescription("Safety cap. Prevents an outlier in the measurements from yanking the weapon away.", new AcceptableValueRange<float>(10f, 400f))));
			FallAusgleich = Eintrag.B(c, "5 Lead", "Compensate bullet drop", defaultValue: true, "Raises the aim point by what the bullet drops on the way to the target - calculated against the distance your optic is currently zeroed for. Without it the shot is only right at the zero distance and goes too low beyond it.");
			VorhaltAn = Eintrag.B(c, "5 Lead", "Enabled", defaultValue: true, "Aims at moving enemies where they will be when the bullet arrives. Off = always aim directly at them.");
			VorhaltFaktor = Eintrag.B(c, "5 Lead", "Factor", 1f, new ConfigDescription("1.0 = the calculated flight time unchanged. The ballistics come from the game itself; normally nothing needs doing here. If you shoot behind: increase.", new AcceptableValueRange<float>(0f, 3f)));
			VorhaltHoechstens = Eintrag.B(c, "5 Lead", "Maximum (meters)", 4f, new ConfigDescription("Safety limit against outliers in the movement data.", new AcceptableValueRange<float>(0.5f, 20f)));
			VorhaltAbTempo = Eintrag.B(c, "5 Lead", "Only above (m/s)", 1.2f, new ConfigDescription("Below this the assist aims directly. Anyone who is just raising the weapon or turning gets no lead. Walking is about 2 m/s.", new AcceptableValueRange<float>(0f, 5f)));
			VorhaltRuhe = Eintrag.B(c, "5 Lead", "Smoothing (seconds)", 0.15f, new ConfigDescription("Over how much time the measured speed of the enemy is averaged. Small = follows direction changes immediately, but picks up every twitch. Large = calm, lags on sidesteps. NOTE, meaning changed: the old setting \"Beruhigung\" was a share per FRAME and therefore acted completely differently at 144 fps than at 60. The old entry in the cfg is no longer read.", new AcceptableValueRange<float>(0.02f, 1f)));
			VorhaltRuheMin = Eintrag.B(c, "5 Lead", "Minimum smoothing (seconds)", 0.06f,
				new ConfigDescription(
					"Lower bound for the smoothing of the target speed, "
					+ "independent of the flight time. Up close the flight time is so "
					+ "short that without this bound almost the instantaneous value "
					+ "is used - the lead direction then jitters and the aim point jumps. "
					+ "Chosen too high, the lead reacts sluggishly to direction changes and "
					+ "comes out too small: with 0.12 s the lead probe reported -24 cm, "
					+ "at about 0.05 s it was -4 cm. The probe in the log "
					+ "is the yardstick - it should be as close to 0 as possible.",
					new AcceptableValueRange<float>(0f, 0.5f)));
			Bildvorhalt = Eintrag.B(c, "5 Lead", "Compensate frame delay (frames)", 1f,
				new ConfigDescription(
					"Compensates your OWN delay, not the bullet's flight. The aim point "
					+ "is calculated at the start of a frame; by the time the shot is fired the "
					+ "target has moved on. Measured: at 7 degrees/s target rotation and 43 ms per "
					+ "frame that is 0.30 degrees - exactly the residual error that "
					+ "remained in close combat. 1 = lead by one frame, 0 = off. At a high frame rate "
					+ "it acts weaker by itself, because a frame is then shorter.",
					new AcceptableValueRange<float>(0f, 3f)));
			NurWaagerecht = Eintrag.B(c, "5 Lead", "Horizontal movement only", defaultValue: true, "On = the up and down while walking is ignored.");
			RueckstossAusgleich = Eintrag.B(c, "9 Legacy Modes - Recoil", "Compensate recoil", 0.8f, Alt(new ConfigDescription("The bullet leaves the MUZZLE, not the camera - confirmed in the game code (WeaponDirection). With recoil the muzzle climbs away from the camera: the view direction sits cleanly on the head, the burst still goes over it. With single fire this is barely noticeable, with sustained fire it grows with every shot. Here the view direction is shifted so that the MUZZLE points at the target. 1.0 = compensate fully, 0 = off. Only acts while aiming down sights; when hip-firing the weapon is permanently skewed, that is not recoil but the stance.", new AcceptableValueRange<float>(0f, 1f))));
			RueckstossRuhe = Eintrag.B(c, "9 Legacy Modes - Recoil", "Smoothing (seconds)", 0.04f, Alt(new ConfigDescription("Smooths the jitter of the weapon animation so that it does not carry through one to one into the view direction. Small = follows the recoil immediately, large = calmer, lags on fast bursts.", new AcceptableValueRange<float>(0.005f, 0.3f))));
			MuendungNachregeln = Eintrag.B(c, "9 Legacy Modes - Recoil", "Correct muzzle", defaultValue: true, Alt(new ConfigDescription(
					"THE answer to \"once it has locked on it no longer readjusts\". "
					+ "Previously the offset between camera and muzzle was calculated "
					+ "FORWARD and subtracted - open-loop control. In the log of the raid of 18.09.2026 it removed "
					+ "only about a tenth of the offset: the camera was 0.04 degrees off the head, the muzzle "
					+ "0.34 degrees below, and that did not change no matter how long you held on. "
					+ "When enabled, the place the MUZZLE "
					+ "really points is measured instead, and the remainder is corrected away cumulatively - "
					+ "until it is gone. That is the difference between "
					+ "\"calculated correctly once\" and \"keeps correcting until it sits\".")));
			MuendungVerstaerkung = Eintrag.B(c, "9 Legacy Modes - Recoil", "Correction speed", 6f, Alt(new ConfigDescription(
					"How fast the remainder is corrected away, in 1/second. 6 means "
					+ "roughly: after just under a second the remaining error is gone. "
					+ "Chosen too high the control starts to oscillate (the weapon "
					+ "lags when turning, that is a feedback loop) - then "
					+ "the aim point trembles, and the value should go down.",
					new AcceptableValueRange<float>(0f, 30f))));
			MuendungHoechstens = Eintrag.B(c, "9 Legacy Modes - Recoil", "Correction maximum (degrees)", 2f, Alt(new ConfigDescription(
					"Safety cap for the sum. Prevents the control from "
					+ "winding up further and further on a continuously wrong measurement.",
					new AcceptableValueRange<float>(0.1f, 8f))));
			RueckstossHoechstens = Eintrag.B(c, "9 Legacy Modes - Recoil", "Maximum (degrees)", 3f, Alt(new ConfigDescription("Safety cap. Tarkov's weapon lags behind when turning - if we turn against the offset, it lags further and the offset grows. This feedback is weak enough to stay stable with a strength below 1; the cap secures the rest.", new AcceptableValueRange<float>(0.5f, 10f))));
			HuefteAusgleich = Eintrag.B(c, "9 Legacy Modes - Recoil", "Compensate from hip", 1f, Alt(new ConfigDescription(
					"There used to be no compensation here at all: when hip-firing the weapon stands "
					+ "PERMANENTLY skewed relative to the camera, and a compensation turns the view direction "
					+ "away accordingly. It was measured, though, that the camera sits cleanly "
					+ "on the target while the MUZZLE is on average 2.8 degrees off - at 19 m "
					+ "about 90 cm. The shot surely misses. 0 = as before, "
					+ "1 = compensate fully.",
					new AcceptableValueRange<float>(0f, 1f))));
			HuefteHoechstens = Eintrag.B(c, "9 Legacy Modes - Recoil", "Hip maximum (degrees)", 8f, Alt(new ConfigDescription(
					"Own upper limit for hip compensation. It is larger than the one when "
					+ "aiming down sights, because the stance offset is larger there: measured "
					+ "median 3 degrees, in a tenth of the cases above 14. If the offset exceeds "
					+ "DOUBLE this limit, nothing is compensated at all - "
					+ "then the weapon is lowered or just being raised, and a "
					+ "compensation would only work against the animation.",
					new AcceptableValueRange<float>(0f, 20f))));
			VorhaltDaempfung = Eintrag.B(c, "5 Lead", "Reduce on direction change", 0.7f, new ConfigDescription("The lead assumes the enemy keeps walking straight. If he sidesteps, the assist aims where he WOULD have been - and that can be further off than no lead at all. Here it is measured how strongly he is currently changing direction, and the lead is reduced accordingly. 0 = off (always full lead), 1 = on a hard sidestep the lead drops out completely.", new AcceptableValueRange<float>(0f, 1f)));

			EspEinstellungen.Anlegen(c);

			// "7 AFK Guard" (ab 1.4.0): Notfall-Pause mit automatischer Verteidigung (AfkWache.cs).
			AfkEinstellungen.Anlegen(c);

			// "0 Quick Setup" (ab 1.3.0): fasst die wichtigsten Einstellungen zusammen.
			QuickSetup.Anlegen(c, vorher);

			// Einmalig: Werte aus den alten deutschen Namen uebernehmen.
			Migriert = Einstellungsumzug.Uebernehmen(c, vorher);

			// Erst NACH der Uebernahme: jetzt stehen die Werte des Nutzers in den
			// Eintraegen, und nur gegen die laesst sich "Strength" ermitteln.
			QuickSetup.NachUebernahme(c, vorher);
		}

		/// <summary>Wie viele Einstellungen beim Start aus den deutschen Namen uebernommen wurden.</summary>
		public static int Migriert;
	}
	internal sealed class Flugbahn
	{
		private const int MaxMeter = 1000;

		private const float Schritt = 0.001f;

		private const float MaxZeit = 3f;

		private readonly float[] _fallBisMeter = new float[1001];

		private readonly float[] _zeitBisMeter = new float[1001];

		private static readonly Dictionary<string, Flugbahn> _bekannt = new Dictionary<string, Flugbahn>();

		internal static Flugbahn Fuer(AmmoTemplate patrone, float laufzuschlag)
		{
			string key = string.Concat(patrone._id, "|", laufzuschlag.ToString("0.00"));
			if (!_bekannt.TryGetValue(key, out var value))
			{
				value = new Flugbahn(patrone, laufzuschlag);
				_bekannt[key] = value;
			}
			return value;
		}

		/// <summary>
		/// Flugbahn wie im Spiel, Schritt fuer Schritt nachgerechnet.
		///
		/// Bisher: Shot.FormTrajectory + PredictedTrajectoryCalculation. Beleg
		/// TrajectoryCalculator.Initialize: legt nur den Startpunkt an - die Bahn
		/// entsteht erst WAEHREND des Flugs (Next()); "history" kommt aus einem
		/// Vorrat (TrajectoryCalculatorHistory.Withdraw). Die Abfrage las also
		/// Eintraege einer FRUEHEREN Kugel oder leere - im Raid vom 02.11. lag der
		/// Fall dadurch auf 110 m bei 72 statt 60 cm, auf 150 m bei 108 statt 117 cm,
		/// und der Wert blieb je Patrone fuer die ganze Sitzung haengen. Ausserdem
		/// wurde der Rechner aus dem Vorrat nie zurueckgegeben.
		///
		/// Jetzt genau TrajectoryCalculator.NextForPlayer (Spieler-Kugeln mit
		/// Schwerkraft), Schritt 0,01 s:
		///   k  = Shot.CalculateG1DragCoefficient(|v|) x m x 0,0014223 / (d^2 x BC)
		///   a  = g - 1,2 x (pi d^2 / 4) x k x |v|^2 / (2 m) x v/|v|
		///   x' = x + v x 0,01 + 0,00005 x a,  v' = v + a x 0,01
		/// Startgeschwindigkeit InitialSpeed x Weapon.SpeedFactor wie in
		/// BallisticsCalculator.CreateShot. Fall = Abstand unter der Abschusslinie.
		/// </summary>
		private Flugbahn(AmmoTemplate patrone, float laufzuschlag)
		{
			float v0 = patrone.InitialSpeed * Mathf.Max(0.1f, laufzuschlag);
			float masse = patrone.BulletMassGram / 1000f;
			float d = patrone.BulletDiameterMilimeters / 1000f;
			float bc = Mathf.Max(0.001f, patrone.BallisticCoeficient);
			float bkoef = masse * 0.0014223f / (d * d * bc);
			float bremse = 1.2f * (d * d * Mathf.PI / 4f);
			float g = Physics.gravity.y;
			float px = 0f, py = 0f, vx = v0, vy = 0f, t = 0f;
			int i = 0;
			_zeitBisMeter[0] = 0f;
			_fallBisMeter[0] = 0f;
			i = 1;
			while (i <= 1000 && t < 3f)
			{
				float sp = Mathf.Sqrt(vx * vx + vy * vy);
				float k = EFT.Ballistics.Shot.CalculateG1DragCoefficient(sp) * bkoef;
				float f = (sp > 1E-04f) ? (bremse * k * sp * sp / (2f * masse) / sp) : 0f;
				float ax = 0f - f * vx;
				float ay = g - f * vy;
				float nx = px + vx * 0.01f + 5E-05f * ax;
				float ny = py + vy * 0.01f + 5E-05f * ay;
				float r0 = Mathf.Sqrt(px * px + py * py);
				float r1 = Mathf.Sqrt(nx * nx + ny * ny);
				while (i <= 1000 && r1 >= i)
				{
					float a = (r1 > r0) ? ((i - r0) / (r1 - r0)) : 1f;
					_zeitBisMeter[i] = t + 0.01f * a;
					_fallBisMeter[i] = 0f - (py + (ny - py) * a);
					i++;
				}
				px = nx;
				py = ny;
				vx += ax * 0.01f;
				vy += ay * 0.01f;
				t += 0.01f;
			}
			for (; i <= 1000; i++)
			{
				_zeitBisMeter[i] = t;
				_fallBisMeter[i] = _fallBisMeter[i - 1];
			}
		}

		internal float Fall(float entfernung)
		{
			if (entfernung <= 0f)
			{
				return 0f;
			}
			int num = Mathf.Clamp((int)entfernung, 0, 999);
			float t = Mathf.Clamp01(entfernung - (float)num);
			return Mathf.Lerp(_fallBisMeter[num], _fallBisMeter[num + 1], t);
		}

		internal float Zeit(float entfernung)
		{
			if (entfernung <= 0f)
			{
				return 0f;
			}
			int num = Mathf.Clamp((int)entfernung, 0, 999);
			float t = Mathf.Clamp01(entfernung - (float)num);
			return Mathf.Lerp(_zeitBisMeter[num], _zeitBisMeter[num + 1], t);
		}
	}
	[BepInPlugin("com.kaan.zielhilfe", "Zielhilfe (Aim Assist)", "1.4.7")]
	public class MainPlugin : BaseUnityPlugin
	{
		private readonly Zielsuche _suche = new Zielsuche();

		private readonly Nachfuehrung _nachfuehrung = new Nachfuehrung();

		private Player _letztesZiel;

		private bool _umschaltZustand;

		private bool _tasteWarUnten;

		// Was die Hilfe zuletzt selbst gesetzt hat. Weicht die Drehung im
		// naechsten Bild davon ab, kommt der Unterschied von der Maus des
		// Spielers (oder vom Rueckstoss des Spiels).
		private Vector2 _gesetzteDrehung;

		private bool _hatGesetzt;

		// In WELCHEM Bild zuletzt gesetzt wurde. Ohne das veraltet
		// _gesetzteDrehung: die Hilfe zieht nur in den Bildern, in denen sie
		// ein Ziel hat (im letzten Raid 4,8 % aller Bilder). In allen anderen
		// bleibt _gesetzteDrehung stehen, waehrend der Spieler sich weiterdreht.
		// Beim naechsten Ziel wird dann die Drehung VIELER Bilder durch die
		// Dauer EINES Bildes geteilt - gemessen wurden so bis zu 7324 Grad/s,
		// was keine Maus schafft. Das riss den Mausvorrang los und sperrte die
		// Hilfe 0,25 s lang, und zwar genau im 2. und 3. Bild einer Begegnung:
		// in 64 % der Begegnungen, also beim Aufschwenken aufs Ziel.
		private int _gesetztImBild = -99;

		private float _mausVorrangBis;

		public static float EigeneDrehrate;

		public static bool MausHatVorrang;

		private float _letzteDiagnose;

		private bool _kamerasGemeldet;

		private float _klemmt;

		private float _letzterFehlerGieren;

		private float _letzterFehlerNicken;

		/// <summary>Ist die Zielhilfe in diesem Bild aktiv (Taste gehalten / Immer)? Fuer die ESP-Einstellung "nur mit Zielhilfe".</summary>
		public static bool HilfeAktiv;

		private void Awake()
		{
			Einstellungen.Anlegen(base.Config);
			base.gameObject.AddComponent<EnemyEsp>();
			base.gameObject.AddComponent<AfkWache>();
			if (Einstellungen.Migriert > 0)
			{
				base.Logger.LogInfo("[Zielhilfe] Migrated " + Einstellungen.Migriert + " settings from the German names");
			}
			string entfernt = Einstellungen.WaisenEntfernen(base.Config);
			if (entfernt.Length > 0)
			{
				base.Logger.LogInfo("[Zielhilfe] Removed obsolete entries from the cfg: " + entfernt);
			}
			SoftAim.Protokoll = base.Logger;
			Aufzeichnung.Protokoll = base.Logger;
			try
			{
				// Der einzige Eingriff ins Spiel: die Mausbewegung abfangen
				// (siehe SoftAim.MausEingabe).
				new Harmony("com.kaan.zielhilfe").PatchAll(typeof(MainPlugin).Assembly);
			}
			catch (Exception ex)
			{
				base.Logger.LogError("[Zielhilfe] Mouse hook failed, Soft Aim (mouse) stays off: " + ex);
			}
			base.Logger.LogInfo("[Zielhilfe] Ready. Hotkey: " + Einstellungen.Taste.Value.ToString() + ", type: " + Einstellungen.Art.Value + ", mode: " + Einstellungen.Modus.Value);
		}

		private void Update()
		{
			HilfeAktiv = false;
			QuickSetup.Tick();
			try
			{
				bool flag = Einstellungen.Diagnose.Value && Time.time - _letzteDiagnose > Einstellungen.DiagnoseTakt.Value;
				if (!Einstellungen.An.Value)
				{
					return;
				}
				if (!Spielzugriff.ImRaid)
				{
					if (flag)
					{
						_letzteDiagnose = Time.time;
						base.Logger.LogInfo("[Zielhilfe] no raid active");
					}
					return;
				}
				if (AfkWache.Aktiv)
				{
					// Die AFK-Wache zielt und schiesst selbst (AfkWache.cs). Die
					// normale Hilfe ruht so lange ganz - sonst zoegen zwei Regler
					// an derselben Blickrichtung. Fuer das ESP ("nur mit Zielhilfe")
					// zaehlt die Wache als aktive Hilfe.
					HilfeAktiv = true;
					SoftAim.Vergessen();
					_suche.Vergessen();
					_nachfuehrung.Vergessen();
					_letztesZiel = null;
					return;
				}
				bool maus = Einstellungen.Art.Value == Zielmodus.SoftAimMouse;
				bool flag2 = Zieht(maus ? Einstellungen.SoftWann.Value : Einstellungen.Modus.Value);
				Player ich = Spielzugriff.Ich;
				Camera kamera = Spielzugriff.Kamera;
				if (maus && flag2 && Einstellungen.SoftNurAnschlag.Value && !SoftAim.ImAnschlag(ich))
				{
					flag2 = false;
				}
				if (maus && flag2 && !SoftAim.WaffeBereit(ich, out _))
				{
					// Keine Schusswaffe schussbereit (Messer, Granate, Nachladen,
					// Stoerung ...): keine Zielsuche, also auch keine Zonen.
					flag2 = false;
				}
				HilfeAktiv = flag2;
				Player ziel = null;
				Vector3 zielpunkt = Vector3.zero;
				bool flag3 = false;
				if (ich != null && kamera != null && (flag2 || flag))
				{
					// Einmal je Bild merken, was zu MIR gehoert - beide
					// Sichtpruefungen ueberspringen das danach.
					Zielpunkt.EigeneMerken(ich);
					// Um welche Achse der Suchkegel liegt. Die alten Arten suchen
					// wie bisher um die Blickrichtung der Kamera. Soft Aim per Maus
					// prueft den Radius um die SCHUSSLINIE (Feuerpunkt +
					// Laufrichtung nach AdjustShotVectors) - sonst waehlte die Suche
					// Gegner nach einem anderen Mass aus, als die Korrektur danach
					// anlegt, und aus der Huefte (Muendung einige Grad neben der
					// Bildmitte) fielen genau die Gegner heraus, auf die die Waffe zeigt.
					Vector3 achseOrt = kamera.transform.position;
					Vector3 achse = kamera.transform.forward;
					float grenzeGrad = Einstellungen.BlickfeldGrad.Value;
					if (maus)
					{
						// Die Grenze rechnet Suchen je Gegner aus seiner Entfernung
						// (Einstellungen.RadiusGrad); dieser Wert wird dort ersetzt.
						grenzeGrad = Einstellungen.RadiusGrad(10f);
						if (Rueckstoss.Schusslinie(ich, out var schussOrt, out var schussRichtung))
						{
							achseOrt = schussOrt;
							achse = schussRichtung;
						}
					}
					flag3 = _suche.Suchen(ich, kamera, achseOrt, achse, grenzeGrad, maus, out ziel, out zielpunkt);
				}
				if (flag && !_kamerasGemeldet)
				{
					_kamerasGemeldet = true;
					base.Logger.LogInfo("[Zielhilfe] Cameras in raid: " + Spielzugriff.AlleKameras());
					// Einmal zeigen, welche Kopf-Trefferboxen das Modell hat und wo ihre
					// Mitte gegenueber dem Kopfknochen liegt. Der Knochen sitzt am
					// Halsansatz - an den Zahlen sieht man, wie weit daneben er liegt.
					base.Logger.LogInfo("[Zielhilfe] Head boxes on own player (center "
						+ "relative to head bone, up/distance): " + Zielpunkt.Kopfboxen(ich));
				}
				if (flag)
				{
					_letzteDiagnose = Time.time;
					base.Logger.LogInfo($"[Zielhilfe] key={Einstellungen.Taste.Value} pressed={flag2} " + $"me={ich != null} camera={Spielzugriff.Kameraquelle} " + $"alive={_suche.Gesamt} outFriend={_suche.RausFreund} outFaction={_suche.RausFraktion} " + $"outFar={_suche.RausEntfernung} outBehind={_suche.RausHinterKamera} " + $"outNoBone={_suche.RausKeinKnochen} " + $"outFov={_suche.RausBlickfeld} outSight={_suche.RausSicht} " + $"left={_suche.Uebrig} | nearest enemy {_suche.NaechsterAbstand:0} px " + $"= {_suche.NaechsterWinkel:0.00}° from center | camera field of view " + $"{((kamera != null) ? kamera.fieldOfView : 0f):0.0}° | limit " + ((Einstellungen.BlickfeldGrad.Value > 0f) ? $"{Einstellungen.BlickfeldGrad.Value:0.0}°" : $"{Einstellungen.Blickfeld.Value:0} px") + Zielsuche.GrenzeText(_suche.NaechsterGrenze, _suche.NaechsterGrenzeEntf, _suche.NaechsterWinkel) + ((_suche.LetztesHindernis.Length > 0) ? (" | sight blocked by " + _suche.LetztesHindernis) : "") + " | aim point: " + ((Zielpunkt.LetzteWahl.Length > 0) ? Zielpunkt.LetzteWahl : Zielpunkt.LetzteQuelle) + ((Zielpunkt.LetzterVersatz > 0.001f) ? $" | lead shifts {Zielpunkt.LetzterVersatz * 100f:0} cm = {Zielpunkt.LetzterVersatzGrad:0.000}° [sideways {Zielpunkt.LetzterVersatzSeitlich * 100f:0} cm, up {Zielpunkt.LetzterVersatzHoch * 100f:0} cm]" : "") + ((Zielpunkt.ProbeText.Length > 0) ? (" | lead probe " + Zielpunkt.ProbeText) : "") + ((Zielpunkt.LetzterBildvorhalt > 0.001f) ? $" | frame delay {Zielpunkt.LetzterBildvorhalt * 100f:0} cm" : "") + ((Zielpunkt.LetzteBoxlage.Length > 0) ? (" | head boxes " + Zielpunkt.LetzteBoxlage) : "") + ((Zielpunkt.LetzteBoxhebung != 0f) ? $" | box lift {Zielpunkt.LetzteBoxhebung * 100f:0.0} cm" : "") + ((Zielpunkt.LetzterBoxfehler.Length > 0) ? (" | BOX ERROR " + Zielpunkt.LetzterBoxfehler) : "") + ((Zielpunkt.LetzteEntfernung > 0f) ? $" | {Zielpunkt.LetzteEntfernung:0} m, scope zeroed at {(Zielpunkt.LetzteEinschiessung < 0 ? $"{-Zielpunkt.LetzteEinschiessung} m (default!)" : $"{Zielpunkt.LetzteEinschiessung} m")}" : "") + ((Zielpunkt.LetzterFall != 0f) ? $" | drop compensation {Zielpunkt.LetzterFall * 100f:0} cm [drop {Zielpunkt.FallBeiZiel * 100f:0}/{Zielpunkt.FallBeiEinschuss * 100f:0} cm, zeroing acts {Zielpunkt.NullungWirkt * 100f:0}%, v0 {Zielpunkt.Muendungstempo:0} m/s]" : "") + $" | {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0} fps ({Time.smoothDeltaTime * 1000f:0} ms/frame)" + $" | aim line turns {(maus ? SoftAim.ZielRate.x : _nachfuehrung.Gieren):0.0}°/s horizontal, " + $"{(maus ? SoftAim.ZielRate.y : _nachfuehrung.Nicken):0.0}°/s vertical" + (MausHatVorrang ? " | MOUSE HAS PRIORITY" : "") + $" | own rotation {EigeneDrehrate:0}°/s" + " | residual error " + $"{_letzterFehlerGieren:0.000}° / {_letzterFehlerNicken:0.000}°" + ((Rueckstoss.MuendungZumZiel >= 0f) ? $" | MUZZLE to target {Rueckstoss.MuendungZumZiel:0.000}° (horizontal {Rueckstoss.FehlerGieren:0.000}°, vertical {Rueckstoss.FehlerNicken:0.000}°)" : "") + Aufzeichnung.Diagnose() + (maus ? SoftAim.Diagnose() : ((Einstellungen.Art.Value == Zielmodus.LegacySoftAim) ? $" | SOFTAIM, cap {Einstellungen.SoftaimHoechstens.Value:0}°/s" : " | LOCK-ON")) + $" | readjustment {Rueckstoss.NachGieren:0.00}° / {Rueckstoss.NachNicken:0.00}°" + ((Zielpunkt.LetzteParallaxe > 0.001f) ? $" | origin fire point, {Zielpunkt.LetzteParallaxe * 100f:0} cm beside the camera" : " | origin camera") + $" | direction change {Zielpunkt.LetzterWechsel:0.00}" + $" | muzzle vs. camera {Rueckstoss.LetzterGieren:0.00}° / " + $"{Rueckstoss.LetzterNicken:0.00}°" + (Rueckstoss.ImAnschlag ? " (ADS)" : $" | HIP, not compensated: {Rueckstoss.HuefteGieren:0.00}° / {Rueckstoss.HuefteNicken:0.00}°"));
				}
				if (!flag2 || !flag3 || !maus)
				{
					// Kein frisches Ziel fuer die Mauskorrektur - dann darf sie
					// auch keins aus einem frueheren Bild benutzen.
					SoftAim.Vergessen();
				}
				if (!flag2)
				{
					_suche.Vergessen();
					_nachfuehrung.Vergessen();
					Rueckstoss.Vergessen();
					Rueckstoss.Aufgeben();
					_letztesZiel = null;
					ZonenRuhe.Instanz.Vergessen();
					Zielpunkt.LetzteWahl = "";
					Zielpunkt.LetzterVersatz = 0f;
					Zielpunkt.LetzterVersatzGrad = 0f;
				}
				else if (flag3)
				{
					if ((object)ziel != _letztesZiel)
					{
						_nachfuehrung.Vergessen();
						// Der aufsummierte Rest gehoerte zum alten Gegner.
						Rueckstoss.Aufgeben();
						_letztesZiel = ziel;
					}
					// Erst jetzt, fuer das EINE gewaehlte Ziel, die Kopfbox suchen,
					// die gerade frei liegt. Im Suchlauf waere das acht Strahlen
					// pro Gegner und Bild.
					Knochen stelle = PunktWaehlen(ich, ziel, kamera, maus && Einstellungen.ZoneNachPanzerung.Value, ref zielpunkt, out bool kopfZu);
					Vector3 vorVorhalt = zielpunkt;
					if (maus)
					{
						// Soft Aim per Maus legt die SCHUSSLINIE auf den Zielpunkt,
						// nicht das Glas. Damit ist die Erhoehung der Nullung
						// weggerechnet und der Zielpunkt muss den ganzen Kugelfall
						// tragen (Fall b in Fallausgleich). Rueckstoss.Versatz und
						// Nachregeln laufen in dieser Art nicht - ohne diese Zeile
						// stuende hier ein Restwert aus einer alten Art.
						Rueckstoss.AktiverAnteil = 1f;
					}
					zielpunkt = Zielpunkt.MitVorhalt(ich, ziel, zielpunkt);
					// Wie weit schieben Vorhalt und Fallausgleich den Punkt von der
					// Box weg? Ein Kopf ist gut 20 cm gross - alles darueber geht
					// daneben, egal wie sauber die Box gewaehlt war.
					Vector3 versatz = zielpunkt - vorVorhalt;
					Zielpunkt.LetzterVersatz = versatz.magnitude;
					Zielpunkt.LetzterVersatzHoch = versatz.y;
					Zielpunkt.LetzterVersatzSeitlich =
						new Vector3(versatz.x, 0f, versatz.z).magnitude;
					Vector3 augeOrt = (kamera != null)
						? kamera.transform.position
						: (ich.Position + Vector3.up * 1.5f);
					Zielpunkt.LetzterVersatzGrad =
						Vector3.Angle(vorVorhalt - augeOrt, zielpunkt - augeOrt);
					if (maus)
					{
						// Nicht drehen - nur fuer die Mauskorrektur hinterlegen.
						ZonenMerken(ziel, zielpunkt, vorVorhalt, versatz, kopfZu, stelle,
							Vector3.Distance(ich.Position, vorVorhalt));
					}
					else
					{
						Hinziehen(ich, kamera, zielpunkt);
					}
				}
			}
			catch (Exception ex)
			{
				base.Logger.LogWarning("[Zielhilfe] " + ex.Message);
			}
		}

		/// <summary>
		/// Waehlt fuer das EINE Ziel die Stelle und den Punkt (vor Vorhalt/Fall):
		/// nach Panzerung (Panzerung.Waehlen) oder die eingestellte Koerperstelle
		/// samt freier Kopfbox und Brust-Ausweichen. zielpunkt kommt mit dem Punkt
		/// der Zielsuche (Zielpunkt.Fuer) herein und bleibt, wenn nichts Besseres
		/// gefunden wird. Gemeinsam fuer die Zielhilfe und die AFK-Wache (ab 1.4.0).
		/// </summary>
		internal static Knochen PunktWaehlen(Player ich, Player ziel, Camera kamera, bool nachPanzerung, ref Vector3 zielpunkt, out bool kopfZu)
		{
			kopfZu = false;
			Knochen stelle = Einstellungen.Ziel.Value;
			SoftAim.ZonenGrund = "body part";
			if (nachPanzerung)
			{
				// Ersetzt fuer diese Art die feste Koerperstelle samt
				// Kopfbox- und Brust-Ausweichlogik: dieselbe Sichtpruefung
				// je Kopfbox, nur in der Reihenfolge der Panzerung, und eine
				// verdeckte Kopfzone faellt weg.
				stelle = Panzerung.Waehlen(ich, ziel, kamera, out zielpunkt, out kopfZu);
			}
			else if (Einstellungen.Ziel.Value == Knochen.Head
				&& Einstellungen.Kopfziel.Value == Kopfbox.Visible
				&& Zielpunkt.SichtbareBox(ziel, kamera, out var boxmitte, out var boxname))
			{
				// Keine einzige Kopfbox frei? Dann steht Deckung davor.
				// Auf den Kopf zu zielen heisst dann, in die Deckung zu
				// schiessen - die Brust ist groesser und oft daneben frei.
				// Kopf <-> Brust nicht bei jedem Flackern der Sicht umschalten
				// (ZonenRuhe, Ruhe.cs): verdeckt erst nach kurzer Zeit auf die
				// Brust, zurueck erst, wenn der Kopf eine Weile am Stueck frei ist.
				bool kopfFrei = !boxname.EndsWith("(covered)");
				bool ausweichen = Einstellungen.BrustAusweichen != null && Einstellungen.BrustAusweichen.Value;
				if (ZonenRuhe.Instanz.Schritt(ziel, Time.time, kopfFrei, ausweichen))
				{
					zielpunkt = Zielpunkt.Koerper(ziel);
					Zielpunkt.LetzteWahl = "Chest(head covered)";
					kopfZu = true;
				}
				else
				{
					zielpunkt = boxmitte;
					Zielpunkt.LetzteWahl = boxname
						+ $"+{zielpunkt.y - ziel.Position.y:0.00}m";
				}
			}
			else
			{
				Zielpunkt.LetzteWahl = "";
			}
			if (!nachPanzerung && kopfZu)
			{
				stelle = Knochen.Chest;
			}
			return stelle;
		}

		private readonly Zone[] _zonen = new Zone[4];

		// Sicht je Zone entprellt (Ruhe.cs): eine Zone gilt erst nach ZoneZuSek
		// ununterbrochen verdeckt als zu und erst nach ZoneFreiSek ununterbrochen
		// frei wieder als frei. Ohne das zieht Soft Aim bei flackernder Sicht
		// bildweise zwischen Kopf und Rumpf hin und her.
		private readonly Entprellung[] _zonenSicht = { new Entprellung(), new Entprellung(), new Entprellung(), new Entprellung() };

		private const float ZoneZuSek = 0.25f;

		private const float ZoneFreiSek = 0.3f;

		private readonly Collider[][] _kollider = { new Collider[8], new Collider[8], new Collider[8], new Collider[8] };

		/// <summary>
		/// Legt die Trefferzonen des gewaehlten Gegners fuer Soft Aim per Maus an:
		/// die Zone der eingestellten Koerperstelle (Wunschzone, Zielpunkt =
		/// der gewaehlte Punkt samt Kopfbox-Wahl) und die uebrigen von Kopf,
		/// Rumpf, Becken als Rueckfall (Zielpunkt = Mitte ihrer Huelle). Alle
		/// Zonen werden um denselben Vorhalt/Kugelfall verschoben. Ist der Kopf
		/// verdeckt und wurde deshalb auf die Brust ausgewichen, faellt die
		/// Kopfzone weg - sonst zoege der Rueckfall wieder in die Deckung.
		/// </summary>
		private void ZonenMerken(Player ziel, Vector3 zielpunkt, Vector3 vorVorhalt, Vector3 versatz, bool kopfZu, Knochen stelle, float entfernung)
		{
			int anzahl = 0;
			int wunsch = -1;
			// Sicht JE ZONE in jedem Bild, vom Feuerpunkt aus: eine Zone hinter
			// Deckung zieht nicht - weder Anziehung noch Magnet, Mitdrehen oder
			// Schub. Ist die Wunschzone verdeckt, bleibt nur die freie Zone.
			Vector3 start = Zielpunkt.SichtStart(Spielzugriff.Ich, Spielzugriff.Kamera);
			SoftAim.SichtGrund = "";
			for (int i = 0; i < 4; i++)
			{
				Knochen art = (Knochen)i;
				SoftAim.SichtZonen[i] = -1;
				// Gesperrte Zonen ziehen auch nicht als Rueckfall - ausser sie sind
				// selbst die gewaehlte (Ausweich- oder eingestellte) Zone.
				if (art != stelle && !Einstellungen.Erlaubt(art))
				{
					continue;
				}
				if (art == Knochen.Head && kopfZu)
				{
					// Keine Kopfbox frei (SichtbareBox).
					SoftAim.SichtZonen[i] = 0;
					continue;
				}
				EBodyPartColliderType[] typen = Trefferzonen.Typen(art);
				bool nurDurchschlagbar = art == Knochen.Head && Einstellungen.ZoneNachPanzerung.Value;
				if (!Trefferzonen.Bilden(ziel, art.ToString(), typen, versatz, _kollider[i], nurDurchschlagbar, out var z))
				{
					continue;
				}
				Vector3 pruefpunkt = (art == stelle) ? vorVorhalt : (z.Mitte - versatz);
				bool roh = Zielpunkt.Sichtlinie(start, pruefpunkt, ziel, out var grund, out _, out _);
				bool frei = _zonenSicht[i].Schritt(ziel, Time.time, roh, ZoneFreiSek, ZoneZuSek);
				SoftAim.SichtZonen[i] = !frei ? 0 : ((roh && grund.StartsWith("through")) ? 2 : 1);
				if (art == stelle)
				{
					SoftAim.SichtGrund = grund;
				}
				if (!frei)
				{
					continue;
				}
				if (art == stelle)
				{
					z.Ziel = zielpunkt;
					wunsch = anzahl;
				}
				_zonen[anzahl++] = z;
			}
			if (anzahl == 0)
			{
				SoftAim.Vergessen();
				return;
			}
			// Mitdrehen bekommt das MOMENTANE Tempo des Ziels (ab 1.4.4) und den
			// Vorhalt dieses Bildes (dessen Aenderung bewegt den Zielpunkt mit).
			// Beides stammt aus dem MitVorhalt-Aufruf direkt davor (Update).
			Vector3 tempoRoh = Zielpunkt.VorhaltTempoRoh;
			tempoRoh.y = 0f;
			SoftAim.Merken(ziel, _zonen, anzahl, wunsch, entfernung, tempoRoh, Zielpunkt.VorhaltVektor);
		}

		private void LateUpdate()
		{
			try
			{
				if (Spielzugriff.ImRaid)
				{
					Einschlaege.Tick();
				}
				else
				{
					Einschlaege.RaidVorbei();
				}
			}
			catch (Exception ex)
			{
				Aufzeichnung.FehlerMelden(ex);
			}
			try
			{
				if (Einstellungen.KampfAufzeichnung.Value && Spielzugriff.ImRaid)
				{
					Aufzeichnung.Bild(Spielzugriff.Ich, Spielzugriff.Kamera);
				}
				else
				{
					Aufzeichnung.RaidVorbei();
				}
			}
			catch (Exception ex)
			{
				Aufzeichnung.FehlerMelden(ex);
			}
		}

		private void OnDestroy()
		{
			Aufzeichnung.RaidVorbei();
		}

		private bool Zieht(Ausloeser wann)
		{
			switch (wann)
			{
			case Ausloeser.Always:
				return true;
			case Ausloeser.ToggleKey:
			{
				bool flag = TasteUnten();
				if (flag && !_tasteWarUnten)
				{
					_umschaltZustand = !_umschaltZustand;
				}
				_tasteWarUnten = flag;
				return _umschaltZustand;
			}
			default:
				return TasteUnten();
			}
		}

		private static bool TasteUnten()
		{
			KeyCode value = Einstellungen.Taste.Value;
			return value switch
			{
				KeyCode.Mouse0 => Input.GetMouseButton(0), 
				KeyCode.Mouse1 => Input.GetMouseButton(1), 
				KeyCode.Mouse2 => Input.GetMouseButton(2), 
				KeyCode.Mouse3 => Input.GetMouseButton(3), 
				KeyCode.Mouse4 => Input.GetMouseButton(4), 
				KeyCode.Mouse5 => Input.GetMouseButton(5), 
				KeyCode.Mouse6 => Input.GetMouseButton(6), 
				_ => Input.GetKey(value), 
			};
		}

		private void Hinziehen(Player ich, Camera kamera, Vector3 zielWelt)
		{
			Vector3 vector = ((kamera != null) ? kamera.transform.position : (ich.Position + Vector3.up * 1.5f));
			// Ursprung fuer den ZIELWINKEL: der Punkt, an dem die Kugel startet.
			// Die Kamera bleibt daneben erhalten - sie wird weiter fuer die
			// Drehratenmessung und die Diagnose gebraucht.
			Vector3 ursprung = vector;
			if (Einstellungen.VomFeuerpunkt != null && Einstellungen.VomFeuerpunkt.Value)
			{
				try
				{
					if (ich.HandsController is Player.FirearmController waffe0)
					{
						Vector3 fp = waffe0.FireportPosition;
						// Gegen den Fall absichern, dass die Waffe gerade
						// gewechselt wird und der Punkt noch im Ursprung liegt.
						if ((fp - vector).sqrMagnitude < 4f)
						{
							ursprung = fp;
						}
					}
				}
				catch
				{
				}
			}
			Zielpunkt.LetzteParallaxe = (ursprung - vector).magnitude;
			Vector3 vector2 = zielWelt - ursprung;
			if (!(vector2.sqrMagnitude < 0.0001f))
			{
				vector2.Normalize();
				float num = Mathf.Atan2(vector2.x, vector2.z) * 57.29578f;
				float num2 = (0f - Mathf.Asin(Mathf.Clamp(vector2.y, -1f, 1f))) * 57.29578f;
				float deltaTime = Time.deltaTime;
				Vector3 relativTempo = Zielpunkt.LetztesTempo - ((ich != null) ? ich.Velocity : Vector3.zero);
				// ABSICHTLICH die Kamera und nicht der Feuerpunkt: der Feuerpunkt
				// schwingt mit der Waffenanimation, und dieses Schwingen wuerde
				// als Zieldrehung durchschlagen. Der Unterschied im Winkel ist
				// hier zweiter Ordnung, das Rauschen waere erster.
				_nachfuehrung.Messen(vector, zielWelt, relativTempo, deltaTime);
				_nachfuehrung.Beitrag(deltaTime, out var gieren, out var nicken);
				Rueckstoss.Versatz(ich, kamera, deltaTime, out var gieren2, out var nicken2);
				num += gieren2;
				num2 += nicken2;
				// Und obendrauf die Nachregelung aus dem, was die Muendung im
				// letzten Bild WIRKLICH getroffen haette. Sie bekommt als Mass
				// den Kamerafehler des letzten Bildes: solange der gross ist,
				// schwenkt die Hilfe noch auf und es gibt nichts nachzuregeln.
				Rueckstoss.Nachregeln(deltaTime,
					Mathf.Max(Mathf.Abs(_letzterFehlerGieren), Mathf.Abs(_letzterFehlerNicken)),
					out var nachG, out var nachN);
				num += nachG;
				num2 += nachN;
				Vector2 rotation = ich.Rotation;

				// --- Vorrang fuer die eigene Maus ---------------------------
				// Die Hilfe SETZT die Blickrichtung jedes Bild neu. Ohne diese
				// Bremse kaeme man von einem einmal erfassten Ziel nicht mehr weg.
				// Die Notbremse gibt es nur, weil das Festhalten den Zeiger
				// gefangen hielt. Im Softaim-Modus wird die Drehung nie gesetzt,
				// sondern nur ergaenzt - da ist nichts, wovon man befreit werden
				// muesste, und jede Sperre waere nur ein Loch in der Hilfe.
				bool weich = Einstellungen.Art.Value == Zielmodus.LegacySoftAim;
				if (weich)
				{
					MausHatVorrang = false;
					EigeneDrehrate = 0f;
				}
				if (Einstellungen.MausVorrang.Value && !weich)
				{
					// Nur vergleichen, wenn der Bezugspunkt aus dem unmittelbar
					// vorhergehenden Bild stammt. Sonst misst man nicht die Maus,
					// sondern die Luecke, in der die Hilfe gar nicht lief.
					if (_hatGesetzt && Time.frameCount == _gesetztImBild + 1 && deltaTime > 0.0001f)
					{
						float quer = Mathf.Abs(Mathf.DeltaAngle(_gesetzteDrehung.x, rotation.x));
						float hoch = Mathf.Abs(Mathf.DeltaAngle(_gesetzteDrehung.y, rotation.y));
						EigeneDrehrate = Mathf.Max(quer, hoch) / deltaTime;
						if (EigeneDrehrate > Einstellungen.MausSchwelle.Value)
						{
							_mausVorrangBis = Time.time + Einstellungen.MausNachlauf.Value;
						}
					}
					else
					{
						// Kein brauchbarer Bezugspunkt: in diesem Bild nicht
						// urteilen. Der Wert wird genullt, damit im Protokoll
						// nicht der alte stehen bleibt.
						EigeneDrehrate = 0f;
					}
					if (Time.time < _mausVorrangBis)
					{
						// Nicht ziehen. Die gemerkte Drehung verwerfen, sonst
						// gilt die eigene Bewegung im naechsten Bild erneut als
						// Abweichung und der Vorrang haelt sich selbst am Leben.
						_hatGesetzt = false;
						_letzterFehlerGieren = 0f;
						_letzterFehlerNicken = 0f;
						MausHatVorrang = true;
						return;
					}
					MausHatVorrang = false;
				}

				float value = Einstellungen.Totzone.Value;
				if (!(Mathf.Abs(gieren) > 0.0005f) && !(Mathf.Abs(nicken) > 0.0005f) && Mathf.Abs(Mathf.DeltaAngle(rotation.x, num)) < value && Mathf.Abs(Mathf.DeltaAngle(rotation.y, num2)) < value)
				{
					// In der Totzone wird nicht gedreht - dann ist die jetzige
					// Drehung der richtige Bezug fuers naechste Bild. Sonst
					// gilt das ruhige Verharren auf dem Ziel im naechsten Bild
					// als Mausbewegung.
					_gesetzteDrehung = rotation;
					_hatGesetzt = true;
					_gesetztImBild = Time.frameCount;
					_letzterFehlerGieren = 0f;
					_letzterFehlerNicken = 0f;
					return;
				}
				float value2 = 1f - Mathf.Exp((0f - Einstellungen.Nachziehen.Value) * deltaTime);
				value2 = Mathf.Clamp01(value2);
				bool vollerZug = true;
				if (weich)
				{
					// Softaim: NICHT setzen, sondern schieben.
					//
					// Der Unterschied ist der ganze Punkt. Beim Setzen ist es
					// gleichgueltig, was die Maus im selben Bild getan hat - der
					// Wert wird ueberschrieben, und der Zeiger klebt. Hier steht
					// alles, was die Maus getan hat, bereits in rotation und
					// bleibt erhalten; addiert wird nur die Korrektur, und die
					// ist auf SoftaimHoechstens Grad je Sekunde gedeckelt.
					// Der Spieler dreht also immer mit voller Geschwindigkeit
					// und die Hilfe legt hoechstens ihren Deckel obendrauf.
					float fehlerX = Mathf.DeltaAngle(rotation.x, num);
					float fehlerY = Mathf.DeltaAngle(rotation.y, num2);
					float deckel = Einstellungen.SoftaimHoechstens.Value * deltaTime;
					float wunschX = fehlerX * value2;
					float wunschY = fehlerY * value2;
					float zugX = Mathf.Clamp(wunschX, 0f - deckel, deckel);
					float zugY = Mathf.Clamp(wunschY, 0f - deckel, deckel);
					// Wurde gedeckelt, zieht die Hilfe mit voller Kraft und
					// kommt trotzdem nicht hinterher - dann ist ein stehender
					// Restfehler normal und kein Grund fuer den Notaus.
					vollerZug = Mathf.Abs(wunschX) <= deckel
								&& Mathf.Abs(wunschY) <= deckel;
					ich.Rotation = new Vector2(rotation.x + zugX + gieren,
											   rotation.y + zugY + nicken);
				}
				else
				{
					ich.Rotation = new Vector2(Mathf.LerpAngle(rotation.x, num, value2) + gieren, Mathf.LerpAngle(rotation.y, num2, value2) + nicken);
				}
				_gesetzteDrehung = ich.Rotation;
				_hatGesetzt = true;
				_gesetztImBild = Time.frameCount;
				_letzterFehlerGieren = Mathf.DeltaAngle(ich.Rotation.x, num);
				_letzterFehlerNicken = Mathf.DeltaAngle(ich.Rotation.y, num2);
				Rueckstoss.FehlerZumZiel(ich, vector, zielWelt);
				Notaus(deltaTime, vollerZug);
			}
		}

		private void Notaus(float dt, bool vollerZug)
		{
			float num = Mathf.Max(Mathf.Abs(_letzterFehlerGieren), Mathf.Abs(_letzterFehlerNicken));
			if (num > 2f && vollerZug)
			{
				_klemmt += dt;
			}
			else
			{
				_klemmt = 0f;
			}
			if (_klemmt > 0.5f)
			{
				_nachfuehrung.Vergessen();
				_klemmt = 0f;
				if (Einstellungen.Diagnose.Value)
				{
					base.Logger.LogWarning($"[Zielhilfe] Emergency stop: was {num:0.0}° off the target, " + "target tracking reset.");
				}
			}
		}
	}
	internal class Nachfuehrung
	{
		private bool _hat;

		public float LetzterBeitragGieren;

		public float LetzterBeitragNicken;

		public float Gieren { get; private set; }

		public float Nicken { get; private set; }

		public void Vergessen()
		{
			_hat = false;
			Gieren = 0f;
			Nicken = 0f;
			LetzterBeitragGieren = 0f;
			LetzterBeitragNicken = 0f;
		}

		public void Messen(Vector3 kamera, Vector3 zielpunkt, Vector3 relativTempo, float dt)
		{
			if (dt <= 0.0001f)
			{
				return;
			}
			Vector3 vector = zielpunkt - kamera;
			if (vector.sqrMagnitude < 0.09f)
			{
				Gieren = 0f;
				Nicken = 0f;
				return;
			}
			Vector3 vector2 = vector + relativTempo * 0.05f;
			if (vector2.sqrMagnitude < 0.09f)
			{
				Gieren = 0f;
				Nicken = 0f;
				return;
			}
			float current = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
			float target = Mathf.Atan2(vector2.x, vector2.z) * 57.29578f;
			float current2 = (0f - Mathf.Asin(Mathf.Clamp(vector.normalized.y, -1f, 1f))) * 57.29578f;
			float target2 = (0f - Mathf.Asin(Mathf.Clamp(vector2.normalized.y, -1f, 1f))) * 57.29578f;
			float num = Mathf.DeltaAngle(current, target) / 0.05f;
			float num2 = Mathf.DeltaAngle(current2, target2) / 0.05f;
			if (!_hat)
			{
				_hat = true;
				Gieren = num;
				Nicken = num2;
			}
			else
			{
				float value = Einstellungen.NachfuehrungRuhe.Value;
				float t = ((value <= 0.001f) ? 1f : (1f - Mathf.Exp((0f - dt) / value)));
				Gieren = Mathf.Lerp(Gieren, num, t);
				Nicken = Mathf.Lerp(Nicken, num2, t);
			}
			float value2 = Einstellungen.NachfuehrungHoechstens.Value;
			Gieren = Mathf.Clamp(Gieren, 0f - value2, value2);
			Nicken = Mathf.Clamp(Nicken, 0f - value2, value2);
		}

		public void Beitrag(float dt, out float gieren, out float nicken)
		{
			float value = Einstellungen.NachfuehrungStaerke.Value;
			gieren = Gieren * dt * value;
			nicken = Nicken * dt * value;
			LetzterBeitragGieren = gieren;
			LetzterBeitragNicken = nicken;
		}
	}
	internal static class Rueckstoss
	{
		public static float LetzterGieren;

		public static float LetzterNicken;

		// Beim Hueftschuss wird NICHT ausgeglichen (siehe unten), der Versatz
		// aber trotzdem gemessen - sonst weiss man nicht, wie gross das Problem
		// im Nahkampf ueberhaupt ist.
		public static float HuefteGieren;

		public static float HuefteNicken;

		public static bool ImAnschlag;

		// Wie stark die Muendung in DIESEM Bild auf den Zielpunkt gedreht
		// wurde - 0 = gar nicht, 1 = voll. Der Fallausgleich haengt daran:
		// zeigt die Muendung selbst aufs Ziel, ist die Erhoehung durch die
		// Nullung weggerechnet und der Zielpunkt muss den ganzen Fall tragen.
		public static float AktiverAnteil;

		// DIE entscheidende Zahl: Winkel zwischen der Muendung und der Richtung
		// zum Zielpunkt. Der Restfehler misst nur Kamera gegen Zielpunkt - die
		// Kugel verlaesst aber die Muendung. Erst dieser Wert sagt, ob der
		// Schuss den Kopf trifft.
		public static float MuendungZumZiel = -1f;

		// Derselbe Fehler, aber mit Vorzeichen und getrennt nach quer/hoch -
		// nur so laesst er sich wegregeln statt nur anzeigen.
		public static float FehlerGieren;

		public static float FehlerNicken;

		private static bool _hatFehler;

		// Aufsummierte Korrektur. Das ist der Unterschied zur alten
		// Vorwaertsrechnung: was ein Bild nicht geschafft hat, bleibt stehen
		// und wird im naechsten weiter abgebaut.
		private static float _nachGieren;

		private static float _nachNicken;

		public static float NachGieren => _nachGieren;

		public static float NachNicken => _nachNicken;

		private static float _gieren;

		private static float _nicken;

		public static void Vergessen()
		{
			_gieren = 0f;
			_nicken = 0f;
			LetzterGieren = 0f;
			LetzterNicken = 0f;
			AktiverAnteil = 0f;
			// ACHTUNG: die aufsummierte Nachregelung wird hier absichtlich NICHT
			// geloescht. Vergessen() laeuft auch, wenn nur der Vorwaertsausgleich
			// aus ist oder die Waffe einen Moment nicht greifbar war - die Summe
			// faenge sonst jedes Bild wieder bei null an. Sie wird per Aufgeben()
			// geloescht, und zwar beim Zielwechsel und wenn kein Ziel da ist.
		}

		/// <summary>
		/// Setzt nur die aufsummierte Nachregelung zurueck. Noetig bei jedem
		/// Zielwechsel: der Rest, der beim alten Gegner uebrig war, hat beim
		/// neuen nichts zu suchen.
		/// </summary>
		public static void Aufgeben()
		{
			_nachGieren = 0f;
			_nachNicken = 0f;
			_hatFehler = false;
		}

		/// <summary>
		/// Der eigentliche Regelkreis auf die MUENDUNG.
		///
		/// Gemessen wird, wohin die Muendung im letzten Bild wirklich gezeigt
		/// hat (FehlerZumZiel). Der Rest wird aufsummiert und auf den Zielwert
		/// gelegt - solange, bis er verschwunden ist. Anders als die
		/// Vorwaertsrechnung braucht das kein Modell davon, WIE der Versatz
		/// zustande kommt; es genuegt, ihn zu sehen.
		///
		/// Aufsummiert wird nur, wenn die Kamera schon nahe am Zielpunkt steht.
		/// Sonst liefe die Summe waehrend des Aufschwenkens voll und riesse die
		/// Waffe hinterher ueber das Ziel hinaus.
		/// </summary>
		public static void Nachregeln(float dt, float kamerafehler,
									  out float gieren, out float nicken)
		{
			gieren = 0f;
			nicken = 0f;
			if (Einstellungen.MuendungNachregeln == null
				|| !Einstellungen.MuendungNachregeln.Value)
			{
				Aufgeben();
				return;
			}
			if (_hatFehler)
			{
				// Dem Fallausgleich Bescheid sagen. Der rechnet anders, je
				// nachdem, WAS am Ende auf den Zielpunkt zeigt: zeigt das Glas
				// hin, hebt die Nullung den Lauf schon von selbst; zeigt die
				// MUENDUNG hin, ist diese Hebung weggerechnet und der Zielpunkt
				// muss den ganzen Kugelfall tragen.
				//
				// Und genau das tut dieser Regelkreis: er dreht die Muendung
				// auf den Zielpunkt. Im Spielcode nachgesehen ist
				// WeaponDirection = Fireport.TransformDirection(
				//   ProceduralWeaponAnimation.ShotDirection), und ShotDirection
				// kommt aus CalculateShotDirectionForIron - also aus Kimme,
				// Korn und der Einschiessentfernung der Waffe. Die Nullung
				// steckt damit bereits in der Laufrichtung drin; wer die
				// Muendung aufs Ziel dreht, rechnet sie weg.
				//
				// Ohne diese Zeile stimmte die Rechnung nur zufaellig, naemlich
				// solange der Vorwaertsausgleich ohnehin auf 1 stand.
				AktiverAnteil = 1f;
			}
			if (_hatFehler && kamerafehler < 1f)
			{
				float v = Einstellungen.MuendungVerstaerkung.Value;
				float grenze = Einstellungen.MuendungHoechstens.Value;
				_nachGieren = Mathf.Clamp(_nachGieren + FehlerGieren * v * dt,
										  0f - grenze, grenze);
				_nachNicken = Mathf.Clamp(_nachNicken + FehlerNicken * v * dt,
										  0f - grenze, grenze);
			}
			gieren = _nachGieren;
			nicken = _nachNicken;
		}

		public static void Versatz(Player ich, Camera kamera, float dt, out float gieren, out float nicken)
		{
			gieren = 0f;
			nicken = 0f;
			float value = Einstellungen.RueckstossAusgleich.Value;
			if (value <= 0.001f || ich == null || kamera == null)
			{
				Vergessen();
				return;
			}
			try
			{
				ImAnschlag = ich.ProceduralWeaponAnimation != null
							 && ich.ProceduralWeaponAnimation.IsAiming;
				if (!ImAnschlag)
				{
					// Aus der Huefte wurde frueher NICHT ausgeglichen - die
					// Waffe steht dort dauerhaft schief zur Kamera, das ist
					// Haltung und nicht Rueckstoss. Die Messung hat das aber
					// widerlegt: die Kamera lag sauber auf dem Ziel, die
					// MUENDUNG im Mittel 2,8 Grad daneben. Also wird jetzt
					// auch hier ausgeglichen, mit eigener Staerke und
					// eigener Obergrenze.
					Messen(ich, kamera, out HuefteGieren, out HuefteNicken);
					float huefte = ((Einstellungen.HuefteAusgleich != null)
						? Mathf.Clamp01(Einstellungen.HuefteAusgleich.Value) : 0f);
					if (huefte <= 0.001f)
					{
						Vergessen();
						return;
					}
					float grenze = ((Einstellungen.HuefteHoechstens != null)
						? Einstellungen.HuefteHoechstens.Value : 8f);
					// Waffe gesenkt oder im Anlegen: dann nicht dagegenhalten.
					if (Mathf.Sqrt(HuefteGieren * HuefteGieren + HuefteNicken * HuefteNicken)
						> grenze * 2f)
					{
						Vergessen();
						return;
					}
					float ruhe = Einstellungen.RueckstossRuhe.Value;
					float tH = ((ruhe <= 0.001f) ? 1f
						: (1f - Mathf.Exp((0f - Mathf.Max(0.0001f, dt)) / ruhe)));
					_gieren = Mathf.Clamp(Mathf.Lerp(_gieren, HuefteGieren, tH), 0f - grenze, grenze);
					_nicken = Mathf.Clamp(Mathf.Lerp(_nicken, HuefteNicken, tH), 0f - grenze, grenze);
					LetzterGieren = _gieren;
					LetzterNicken = _nicken;
					AktiverAnteil = huefte;
					gieren = (0f - _gieren) * huefte;
					nicken = (0f - _nicken) * huefte;
					return;
				}
				HuefteGieren = (HuefteNicken = 0f);
				// Die Richtung, in die die Kugel WIRKLICH fliegt (nach
				// AdjustShotVectors), nicht das rohe WeaponDirection. Vorher stand
				// hier der rohe Vektor - bei Sichtfeld 70 ist der Versatz damit
				// rund 1,4-mal zu klein gemessen, und der Rest blieb an der
				// Nachregelung haengen, die ihn nur langsam aufsummiert.
				if (!Schusslinie(ich, out _, out var weaponDirection))
				{
					Vergessen();
					return;
				}
				Vector3 forward = kamera.transform.forward;
				float b = Mathf.DeltaAngle(Gieren(forward), Gieren(weaponDirection));
				float b2 = Mathf.DeltaAngle(Nicken(forward), Nicken(weaponDirection));
				float value2 = Einstellungen.RueckstossRuhe.Value;
				float t = ((value2 <= 0.001f) ? 1f : (1f - Mathf.Exp((0f - Mathf.Max(0.0001f, dt)) / value2)));
				_gieren = Mathf.Lerp(_gieren, b, t);
				_nicken = Mathf.Lerp(_nicken, b2, t);
				float value3 = Einstellungen.RueckstossHoechstens.Value;
				_gieren = Mathf.Clamp(_gieren, 0f - value3, value3);
				_nicken = Mathf.Clamp(_nicken, 0f - value3, value3);
				LetzterGieren = _gieren;
				LetzterNicken = _nicken;
				AktiverAnteil = value;
				gieren = (0f - _gieren) * value;
				nicken = (0f - _nicken) * value;
			}
			catch
			{
				Vergessen();
			}
		}

		/// <summary>
		/// Misst, wie weit die MUENDUNG neben dem Zielpunkt liegt. Das ist die
		/// Zahl, die ueber Treffer oder Fehlschuss entscheidet - unabhaengig
		/// davon, wie sauber die Kamera liegt.
		/// </summary>
		public static void FehlerZumZiel(Player ich, Vector3 kameraOrt, Vector3 zielWelt)
		{
			MuendungZumZiel = -1f;
			try
			{
				if (!Schusslinie(ich, out var start, out var r))
				{
					return;
				}
				Vector3 zumZiel = zielWelt - start;
				if (zumZiel.sqrMagnitude < 0.0001f)
				{
					return;
				}
				Vector3 z = zumZiel.normalized;
				MuendungZumZiel = Vector3.Angle(r, z);
				// Vorzeichen: wie viel die MUENDUNG drehen muss, um auf dem Ziel
				// zu landen. Genau das kommt gleich auf den Zielwert drauf.
				FehlerGieren = Mathf.DeltaAngle(Gieren(r), Gieren(z));
				FehlerNicken = Mathf.DeltaAngle(Nicken(r), Nicken(z));
				_hatFehler = true;
			}
			catch
			{
			}
		}

		/// <summary>
		/// Die Schusslinie: Startpunkt und Flugrichtung der Kugel, genau so, wie
		/// Player.FirearmController.Shot() sie ans Geschoss uebergibt
		/// (CurrentFireport.position + WeaponDirection, beide durch
		/// AdjustShotVectors). Richtung normiert. Falsch, wenn keine Feuerwaffe
		/// in der Hand ist.
		/// </summary>
		internal static bool Schusslinie(Player ich, out Vector3 start, out Vector3 richtung)
		{
			start = Vector3.zero;
			richtung = Vector3.zero;
			try
			{
				if (!(ich?.HandsController is Player.FirearmController waffe))
				{
					return false;
				}
				richtung = waffe.WeaponDirection;
				if (richtung.sqrMagnitude < 0.0001f)
				{
					return false;
				}
				// WeaponDirection ist NICHT die Richtung, in die die Kugel
				// fliegt. Das Spiel schiebt beide Werte vorher noch durch
				// AdjustShotVectors - nachgesehen in Player.FirearmController,
				// wo Shot() und LauncherShot() genau das direkt vor InitiateShot
				// tun (in 4.0.x hiessen die beiden noch method_57/method_58).
				// Darin steckt die Sichtfeld-Entzerrung:
				//
				//   CalculateScaleValueByFov: Lerp(1, 0.65, InverseLerp(50, 75, fov))
				//   AdjustShotVectors:        direction.z *= RibcageScaleCurrent
				//
				// Bei Sichtfeld 70 ist der Faktor 0,72 - jede Winkelabweichung
				// wird dadurch um rund das 1,4-fache groesser als WeaponDirection
				// vermuten laesst. Wer gegen den unbereinigten Vektor regelt,
				// regelt auf den falschen Punkt. Hier wird deshalb die
				// spieleigene Rechnung aufgerufen statt sie nachzubauen; sie
				// liefert gleich auch den richtigen Startpunkt der Kugel.
				// Gemessen wird ab der MUENDUNG, nicht ab der Kamera. Vorher
				// stand hier der Kameraort - damit steckte der Abstand zwischen
				// Kamera und Muendung als Parallaxe mit im Winkel, und zwar um
				// so groesser, je naeher das Ziel ist: 10 cm Versatz sind auf
				// 10 m schon 0,57 Grad. Der Wert bestrafte also den Nahkampf,
				// ohne dass dort etwas falsch gezielt war. FireportPosition ist
				// der Punkt, an dem das Spiel die Kugel wirklich losschickt.
				start = waffe.FireportPosition;
				try
				{
					waffe.AdjustShotVectors(ref start, ref richtung);
				}
				catch
				{
					// Sollte die Signatur sich einmal aendern, lieber roh
					// weiterrechnen als gar nicht.
				}
				if (richtung.sqrMagnitude < 0.0001f)
				{
					return false;
				}
				richtung.Normalize();
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Misst den Winkel zwischen Blickrichtung und Muendung - ohne etwas
		/// auszugleichen. Gebraucht fuer den Hueftausgleich und die Diagnose.
		/// Wie in Versatz: die Schussrichtung nach AdjustShotVectors, nicht das
		/// rohe WeaponDirection.
		/// </summary>
		private static void Messen(Player ich, Camera kamera, out float gieren, out float nicken)
		{
			gieren = 0f;
			nicken = 0f;
			try
			{
				if (!Schusslinie(ich, out _, out var richtung))
				{
					return;
				}
				Vector3 blick = kamera.transform.forward;
				gieren = Mathf.DeltaAngle(Gieren(blick), Gieren(richtung));
				nicken = Mathf.DeltaAngle(Nicken(blick), Nicken(richtung));
			}
			catch
			{
			}
		}

		internal static float Gieren(Vector3 v)
		{
			return Mathf.Atan2(v.x, v.z) * 57.29578f;
		}

		internal static float Nicken(Vector3 v)
		{
			return (0f - Mathf.Asin(Mathf.Clamp(v.y, -1f, 1f))) * 57.29578f;
		}
	}
	internal static class Spielzugriff
	{
		public static string Kameraquelle = "?";

		public static GameWorld Welt
		{
			get
			{
				if (!Singleton<GameWorld>.Instantiated)
				{
					return null;
				}
				return Singleton<GameWorld>.Instance;
			}
		}

		public static Player Ich => Welt?.MainPlayer;

		public static Camera Kamera
		{
			get
			{
				if (EFT.CameraControl.CameraManager.Exist && EFT.CameraControl.CameraManager.Instance.Camera != null)
				{
					Kameraquelle = "CameraManager:" + EFT.CameraControl.CameraManager.Instance.Camera.name;
					return EFT.CameraControl.CameraManager.Instance.Camera;
				}
				Camera[] allCameras = Camera.allCameras;
				foreach (Camera camera in allCameras)
				{
					if (!(camera == null) && camera.isActiveAndEnabled)
					{
						string text = camera.name ?? "";
						if (text.IndexOf("fps", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							Kameraquelle = "searched:" + text;
							return camera;
						}
					}
				}
				Camera main = Camera.main;
				Kameraquelle = ((main != null) ? ("Camera.main:" + main.name) : "none");
				return main;
			}
		}

		public static bool ImRaid
		{
			get
			{
				if (!Singleton<AbstractGame>.Instantiated)
				{
					return false;
				}
				AbstractGame instance = Singleton<AbstractGame>.Instance;
				if (instance == null || instance.Status != GameStatus.Started)
				{
					return false;
				}
				return Welt?.MainPlayer != null;
			}
		}

		public static string AlleKameras()
		{
			List<string> list = new List<string>();
			Camera[] allCameras = Camera.allCameras;
			foreach (Camera camera in allCameras)
			{
				if (camera != null)
				{
					list.Add($"{camera.name}(depth {camera.depth:0})");
				}
			}
			return string.Join(", ", list.ToArray());
		}

		// Zuletzt WIRKLICH gefeuerte Patrone (aus Player.FirearmController.Shot),
		// mit Waffe und Magazin, aus denen sie kam.
		private static Weapon _gefWaffe;

		private static object _gefMagazin;

		private static AmmoTemplate _gefPatrone;

		/// <summary>Vom Schuss-Abgriff: diese Patrone ist gerade gefeuert worden.</summary>
		public static void Gefeuert(Weapon w, Ammo ammo)
		{
			_gefWaffe = w;
			_gefMagazin = w?.GetCurrentMagazine();
			_gefPatrone = ammo?.Template as AmmoTemplate;
		}

		/// <summary>
		/// Die Patrone, mit der die naechste Kugel fliegt - Grundlage fuer Fall,
		/// Flugzeit und Durchschlag.
		///
		/// Bisher Weapon.CurrentAmmoTemplate (Lager, sonst Magazin.Cartridges.Last).
		/// Im Raid vom 02.11. (Lighthouse, MP5) lieferte das nach dem ersten Schuss
		/// AP 6.3 (v0 337), geflogen ist aber nachweislich 7N31 (v0 482): Flugzeit
		/// bis zum Einschlag bei allen 7 Schuessen innerhalb eines Bildes zum
		/// 7N31-Modell, AP 6.3 waere 1-2 Bilder spaeter; Fall unter der Schusslinie
		/// im Mittel +1 cm zum 7N31-Modell. Der Fallausgleich hob dadurch auf 72 m
		/// um 7 cm zu hoch -> Schuesse ueber den Kopf. Die Spielquellen zeigen
		/// keinen Unterschied (gefeuert wird SingleShotData.AmmoToFire = Lager,
		/// nachgeladen Cartridges.Last); die Ursache des Widerspruchs ist offen
		/// und wird ueber "vorhergesagt"/"gefeuert" im Schuss-Ereignis weiter
		/// gemessen. Bis dahin gilt: Solange Waffe und Magazin dieselben sind,
		/// zaehlt die zuletzt WIRKLICH gefeuerte Patrone (Messung statt
		/// Vorhersage); sonst CurrentAmmoTemplate.
		/// </summary>
		public static AmmoTemplate Patrone(Player ich, out Weapon w)
		{
			w = ich?.HandsController?.Item as Weapon;
			if (w == null)
			{
				return null;
			}
			try
			{
				if ((object)w == _gefWaffe && _gefPatrone != null && (object)w.GetCurrentMagazine() == _gefMagazin)
				{
					return _gefPatrone;
				}
				return w.CurrentAmmoTemplate;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>Weapon.CurrentAmmoTemplate, nur fuer die Diagnose (Vorhersage des Spiels).</summary>
		public static AmmoTemplate Vorhersage(Player ich)
		{
			try
			{
				return (ich?.HandsController?.Item as Weapon)?.CurrentAmmoTemplate;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Ist dieser Bot mir gegenueber feindlich? Die Spiellogik selbst
		/// (BotsGroup.cs): als Verbuendeter gefuehrt (IsAlly) = nein; schon als
		/// Feind eingetragen (IsEnemy, z. B. nach Beschuss) = ja; sonst die
		/// Grundhaltung IsPlayerEnemy (Seite, Karma/HostileScavs, Boss-Treue,
		/// Freund-/Warn-/Feindtypen, Mark of Unknown). Ohne Bot-Gehirn (kein
		/// BotOwner/BotsGroup) gilt er als feindlich - lieber helfen als einen
		/// echten Gegner uebergehen.
		/// </summary>
		public static bool IstFeind(Player p, Player ich)
		{
			try
			{
				BotsGroup gruppe = p?.AIData?.BotOwner?.BotsGroup;
				if (gruppe == null || ich == null)
				{
					return true;
				}
				// Ueber object gecastet: die direkte Umwandlung Player -> IPlayer
				// verlangte zur Bauzeit die Dissonance-Assembly (Basis-Schnittstelle).
				IPlayer selbst = (IPlayer)(object)ich;
				if (gruppe.IsAlly(selbst))
				{
					return false;
				}
				if (gruppe.IsEnemy(selbst))
				{
					return true;
				}
				return gruppe.IsPlayerEnemy(selbst);
			}
			catch
			{
				return true;
			}
		}

		public static bool IstScav(Player p)
		{
			ProfileInfo infoClass = p?.Profile?.Info;
			if (infoClass == null)
			{
				return false;
			}
			if (infoClass.Side == EPlayerSide.Savage)
			{
				return true;
			}
			string text = Rolle(infoClass);
			if (!text.Contains("assault") && !text.Contains("marksman"))
			{
				return text.Contains("cursed");
			}
			return true;
		}

		public static bool IstPmc(Player p)
		{
			ProfileInfo infoClass = p?.Profile?.Info;
			if (infoClass == null)
			{
				return false;
			}
			int side = (int)infoClass.Side;
			if (side == 1 || side == 2)
			{
				return !IstBoss(p);
			}
			return false;
		}

		public static bool IstBoss(Player p)
		{
			string text = Rolle(p?.Profile?.Info);
			if (!text.Contains("boss") && !text.Contains("follower") && !text.StartsWith("big") && !text.StartsWith("bird") && !text.Contains("knight"))
			{
				return text.Contains("sectant");
			}
			return true;
		}

		internal static string RolleKlein(Player p)
		{
			return Rolle(p?.Profile?.Info);
		}

		private static string Rolle(ProfileInfo info)
		{
			try
			{
				return (info?.Settings != null) ? info.Settings.Role.ToString().ToLowerInvariant() : "";
			}
			catch
			{
				return "";
			}
		}
	}
	internal enum Knochen
	{
		Head,
		Chest,
		Pelvis,
		Legs
	}

	/// <summary>
	/// Welche Trefferbox am Kopf angezielt wird.
	///
	/// Der Kopf ist im Spiel keine einzige Box, sondern sieben:
	/// HeadCommon, ParietalHead, BackHead, Ears, Eyes, Jaw, NeckFront, NeckBack
	/// (Aufzaehlung EBodyPartColliderType). Alle zaehlen als Koerperteil "Head",
	/// unterscheiden sich aber in Panzerung und Lage.
	///
	/// "Knochen" ist der alte Weg: Kopfknochen plus fester Hoehenversatz.
	/// Der Versatz zeigt in WELTkoordinaten nach oben und dreht sich nicht mit
	/// dem Kopf mit - liegt der Gegner oder lehnt er, rutscht der Zielpunkt
	/// vom Kopf herunter. Die Trefferboxen haben dieses Problem nicht.
	/// </summary>
	internal enum Kopfbox
	{
		Visible,
		HeadCenter,
		Face,
		Jaw,
		Skull
	}
	/// <summary>
	/// Entscheidet, ob ein Hindernis den SCHUSS blockiert - nicht nur die Sicht.
	///
	/// Glas, Maschendraht, Gitter, hohes Gras, Pappe: all das haelt den
	/// Sichtstrahl auf, die Kugel aber nicht. Ohne diese Unterscheidung
	/// sortiert die Zielhilfe Gegner hinter einer Fensterscheibe aus,
	/// obwohl man sie problemlos erschiessen kann.
	///
	/// Die Bewertung kommt aus dem Spiel selbst: Jeder beschiessbare Kollider
	/// traegt einen BallisticCollider mit Materialart und Durchschusswerten
	/// (PenetrationChance, PenetrationLevel). Wir raten also nicht, sondern
	/// lesen ab.
	/// </summary>
	internal static class Durchschuss
	{
		// Materialien, durch die man SIEHT und SCHIESST. Bei denen ist die
		// Sache unabhaengig von den Zahlenwerten klar.
		private static readonly HashSet<MaterialType> Durchsichtig =
			new HashSet<MaterialType>
			{
				MaterialType.Glass, MaterialType.GlassShattered,
				MaterialType.GlassVisor, MaterialType.Chainfence,
				MaterialType.Grate, MaterialType.GrassHigh,
				MaterialType.GrassLow, MaterialType.Cardboard,
				MaterialType.GarbagePaper, MaterialType.Fabric,
				MaterialType.GenericSoft
			};

		// Ab 1.4.7 (Meldung 09.11.: "Der Aim Assist soll nicht durch verdeckte
		// Hindernisse schiessen"): was das AUGE durchlaesst. Nicht mehr dabei:
		// Fabric, Cardboard, GarbagePaper, GenericSoft (Planen, Stoff, Kartons,
		// Muell - undurchsichtig, auch wenn die Kugel durchgeht). GlassVisor sitzt
		// nur an Helmen, also an einem Koerper - der verdeckt ohnehin.
		// GrassHigh wertet SichtlinieRechnen nach Tiefe (Laub), GrassLow ist
		// bodennahes Gras (verdeckt nie, wie im Laub-Modell von 1.4.6).
		// Nicht im Strahl: die Ebene "TransparentCollider" - die Bots sehen durch
		// sie hindurch (LookSensor-Maske HighPolyWithTerrainMaskAI = Terrain |
		// HighPolyCollider | Grass | Foliage), das Spiel fuehrt sie also selbst
		// als durchsichtig.
		private static readonly HashSet<MaterialType> OptischDurchsichtig =
			new HashSet<MaterialType>
			{
				MaterialType.Glass, MaterialType.GlassShattered,
				MaterialType.Chainfence, MaterialType.Grate,
				MaterialType.GrassHigh, MaterialType.GrassLow
			};

		/// <summary>Sicht rein optisch ("Only visible targets ...")?</summary>
		public static bool NurOptisch => Einstellungen.NurOptisch != null && Einstellungen.NurOptisch.Value;

		// Milchglas: dieselbe Materialart Glass, erkennbar nur am Namen des
		// Render-Materials/Shaders. Je Kollider einmal ermittelt ("" = klar).
		private static readonly Dictionary<Collider, string> _milchig = new Dictionary<Collider, string>(256);

		private static readonly string[] MilchWoerter = { "frost", "milk", "matt", "opaque", "frozen", "privacy" };

		private static string Milchig(Collider c, BallisticCollider b)
		{
			if (_milchig.TryGetValue(c, out string treffer))
			{
				return treffer;
			}
			if (_milchig.Count > 4096)
			{
				_milchig.Clear();
			}
			treffer = "";
			try
			{
				// Nur Renderer am Kollider selbst oder die Scheibe des Fensters
				// (WindowBreaker.Renderer) - nicht die des Elternobjekts, das waere
				// oft der Rahmen.
				treffer = MilchigIn(c.GetComponent<Renderer>());
				if (treffer.Length == 0 && b != null && b.gameObject != c.gameObject)
				{
					treffer = MilchigIn(b.GetComponent<Renderer>());
				}
				if (treffer.Length == 0)
				{
					EFT.Interactive.WindowBreaker wb = c.GetComponentInParent<EFT.Interactive.WindowBreaker>();
					if (wb != null)
					{
						treffer = MilchigIn(wb.Renderer);
						if (treffer.Length == 0 && wb.Material != null)
						{
							treffer = MilchigName(wb.Material);
						}
					}
				}
			}
			catch
			{
				treffer = "";
			}
			_milchig[c] = treffer;
			return treffer;
		}

		private static string MilchigIn(Renderer r)
		{
			if (r == null)
			{
				return "";
			}
			Material[] mats = r.sharedMaterials;
			for (int i = 0; i < mats.Length; i++)
			{
				string t = MilchigName(mats[i]);
				if (t.Length > 0)
				{
					return t;
				}
			}
			return "";
		}

		private static string MilchigName(Material m)
		{
			if (m == null)
			{
				return "";
			}
			string n = (m.name + " " + ((m.shader != null) ? m.shader.name : "")).ToLowerInvariant();
			for (int i = 0; i < MilchWoerter.Length; i++)
			{
				if (n.Contains(MilchWoerter[i]))
				{
					return m.name;
				}
			}
			return "";
		}

		/// <summary>
		/// Wahr, wenn die Kugel durch dieses Hindernis geht.
		///
		/// Der Grund kommt als out-Wert zurueck und NICHT ueber ein gemeinsames
		/// Feld: frueher stand dort der Text des zuletzt untersuchten Kolliders,
		/// auch wenn die Pruefung vorher ausgestiegen war. Die Diagnose paarte
		/// dadurch Namen und Material falsch.
		/// </summary>
		// Die Ebenen, auf denen das Spiel Geschosse rechnen laesst. Abgelesen
		// an LayerMaskClass: HighPolyWithTerrainMask | HitCollider, dazu
		// Tueren, Spieler und Leichen. Alles andere - Sichtbarkeitswuerfel,
		// KI-Sperren, Wegfindungshelfer - haelt keine Kugel auf.
		private static int _ebenen = -1;

		private static readonly string[] EbenenNamen =
		{
			"Terrain", "HighPolyCollider", "HitCollider",
			"DoorLowPolyCollider", "Player", "Deadbody", "Interactive"
		};

		public static int Ebenenmaske()
		{
			if (Einstellungen.NurKugelEbenen == null || !Einstellungen.NurKugelEbenen.Value)
			{
				return -1;
			}
			if (_ebenen == -1)
			{
				int m = 0;
				for (int i = 0; i < EbenenNamen.Length; i++)
				{
					int e = LayerMask.NameToLayer(EbenenNamen[i]);
					if (e >= 0)
					{
						m |= 1 << e;
					}
				}
				// Findet sich keine einzige - dann lieber alles pruefen als
				// blind durch Waende zielen.
				_ebenen = ((m != 0) ? m : (-1));
			}
			return _ebenen;
		}

		/// <param name="nurDurchsichtig">
		/// AFK-Wache (ab 1.4.3): nur Durchsichtiges (Glas, Maschendraht, Gitter, Gras,
		/// Stoff, Pappe ...) zaehlt als frei, nicht "durchschlagbar nach Spielregel".
		/// Gemessen am Raid 08.11. (Streets, 5,56 mm Pen 53): die Regel nannte MetalThin
		/// (Level 4) und Plastic (Level 1) 100 % durchschlagbar, WoodThick (Level 25) 95 %;
		/// danach getroffen wurde das Ziel in allen Aufzeichnungen nur bei 16 % (MetalThin,
		/// 67 Schuesse), 18 % (WoodThick, 45) und 0 % (Plastic, 32) - Splitterung,
		/// Ablenkung und mehrere Wandlagen (Auto, Muelltonne) rechnet die Regel nicht.
		/// Fabric dagegen 77 % (22). Die normale Zielhilfe bleibt bei der Spielregel.
		/// </param>
		/// <param name="optisch">
		/// Ab 1.4.7: Sicht fuer das AUGE (OptischDurchsichtig, Milchglas verdeckt);
		/// Durchschlag zaehlt dann nicht. Hat Vorrang vor nurDurchsichtig.
		/// </param>
		public static bool Durchlaessig(Collider c, out string grund, bool nurDurchsichtig = false, bool optisch = false)
		{
			grund = "";
			if (c == null)
			{
				return false;
			}
			if (!Einstellungen.DurchschussIgnorieren.Value)
			{
				grund = "shoot-through disabled";
				return false;
			}
			try
			{
				BallisticCollider b = c.GetComponent<BallisticCollider>();
				if (b == null)
				{
					b = c.GetComponentInParent<BallisticCollider>();
				}
				if (b == null)
				{
					// Ebene mitschreiben: nur so laesst sich hinterher sagen,
					// ob das Hindernis echt war oder nur ein Hilfsobjekt.
					grund = "no ballistic collider, layer " + LayerMask.LayerToName(c.gameObject.layer);
					return false;
				}
				if (b is BodyPartCollider)
				{
					// Ein Koerper ist nie "durchlaessig" - sonst zielt die Hilfe
					// durch den Kameraden vor dem Gegner hindurch.
					grund = "body";
					return false;
				}
				MaterialType m = b.TypeOfMaterial;
				if (optisch)
				{
					if (!OptischDurchsichtig.Contains(m))
					{
						grund = $"{m} opaque (hides even if penetrable)";
						return false;
					}
					if (m == MaterialType.Glass)
					{
						string milch = Milchig(c, b);
						if (milch.Length > 0)
						{
							grund = $"Glass frosted ({milch})";
							return false;
						}
					}
					grund = $"{m} see-through";
					return true;
				}
				if (Durchsichtig.Contains(m))
				{
					grund = $"{m} see-through";
					return true;
				}
				if (nurDurchsichtig)
				{
					grund = $"{m} blocks (AFK guard: only see-through material counts as clear)";
					return false;
				}
				// Spielregel (BallisticCollider.IsPenetrated): durchschlagen nur, wenn
				// PenetrationChance > 0 UND Durchschlagskraft der Patrone >
				// PenetrationLevel; dann mit Wahrscheinlichkeit
				// PenetrationChanceObstacle der Patrone + PenetrationChance.
				// Bisher galt alles mit PenetrationChance >= Schwelle als frei -
				// unabhaengig von der eigenen Patrone (Holz-/Blechwand bei 9 mm).
				AmmoTemplate patrone = null;
				try
				{
					patrone = Spielzugriff.Patrone(Spielzugriff.Ich, out _);
				}
				catch
				{
				}
				float pen = (patrone != null) ? patrone.PenetrationPower : 0f;
				float chance = 0f;
				if (b.PenetrationChance >= float.Epsilon && pen > b.PenetrationLevel)
				{
					chance = Mathf.Clamp01(((patrone != null) ? patrone.PenetrationChanceObstacle : 0f) + b.PenetrationChance);
				}
				float schwelle = Einstellungen.DurchschussSchwelle.Value;
				if (chance > 0f && chance >= schwelle)
				{
					grund = $"{m} shoot-through (pen {pen:0} > {b.PenetrationLevel:0}, {chance * 100f:0} %)";
					return true;
				}
				grund = $"{m} blocks (pen {pen:0} / level {b.PenetrationLevel:0}, {chance * 100f:0} %)";
				return false;
			}
			catch (Exception ex)
			{
				grund = ex.GetType().Name;
				return false;
			}
		}

		/// <summary>Materialart des Kolliders (BallisticCollider), falls vorhanden.</summary>
		public static bool Material(Collider c, out MaterialType m)
		{
			m = default(MaterialType);
			try
			{
				BallisticCollider b = c.GetComponent<BallisticCollider>() ?? c.GetComponentInParent<BallisticCollider>();
				if (b == null || b is BodyPartCollider)
				{
					return false;
				}
				m = b.TypeOfMaterial;
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Buesche und Laub als Sichthindernis (ab 1.4.6).
	///
	/// Meldung 09.11.: "Die Zielhilfe aimt auch durch Bueche, wo ich den Gegner
	/// nicht sehen kann." Der Sichtstrahl lief nur ueber die Kugel-Ebenen
	/// (Durchschuss.Ebenenmaske) und ignorierte Trigger - Buesche sind aber
	/// TreeInteractive-Objekte mit TRIGGER-Kollidern auf der Ebene "Foliage"
	/// (TreeInteractive: OnTriggerEnter/Stay -> AIData.StayInTree ->
	/// FoliageIntersectionSystem "triggerCollider"). Doppelt unsichtbar.
	///
	/// Die Bots machen es so (Assembly-CSharp):
	///  - LookSensor.Mask = LayersMaskController.HighPolyWithTerrainMaskAI
	///    = Terrain | HighPolyCollider | Grass | Foliage,
	///  - EnemyPartVision.DoCastAndCacheObstacles: Physics.Linecast mit dieser
	///    Maske (Trigger zaehlen, queriesHitTriggers bleibt beim Unity-Standard),
	///    Treffer auf Foliage -> ELookObstacleType.Tree, auf Grass -> Grass,
	///  - ProcessVisionObstacles: Tree = Laenge der Strecke IN der Kugel
	///    (SphereIntersectionLength), Grass pauschal 0,6 m; method_7 macht daraus
	///    die Sicht: (1 - d/MAX_VISION_GRASS_METERS)/(1 + 1,5 d), ab
	///    d >= MAX_VISION_GRASS_METERS (0,4-0,8 m in den Bot-Daten) sieht der Bot nichts.
	/// Hier genauso: Strecke im Laub messen, ab "Foliage depth that hides" gilt
	/// der Punkt als verdeckt. Grass-Ebene (bodennahes Gras) NICHT - bei den Bots
	/// verdeckt sie nie ganz (0,6 m < 0,8 m), und Kaan soll nicht an jedem
	/// Grasbueschel die Hilfe verlieren. Dazu Kugel-Kollider aus Material
	/// GrassHigh (zaehlt fuer die Kugel als durchlaessig, fuer das Auge nicht).
	/// </summary>
	internal static class Laub
	{
		private static int _maske = -1;

		private static readonly RaycastHit[] _treffer = new RaycastHit[32];

		// Strecken [von, bis] auf dem Strahl, die im Laub liegen.
		private static readonly float[] _von = new float[48];

		private static readonly float[] _bis = new float[48];

		private static int _anzahl;

		public static bool An => Einstellungen.LaubVerdeckt != null && Einstellungen.LaubVerdeckt.Value;

		public static int Maske()
		{
			if (_maske == -1)
			{
				int e = LayerMask.NameToLayer("Foliage");
				_maske = (e >= 0) ? (1 << e) : 0;
			}
			return _maske;
		}

		public static void Beginnen()
		{
			_anzahl = 0;
		}

		/// <summary>
		/// Strecke des Strahls IN diesem Kollider merken: Eintritt aus dem
		/// Strahltreffer, Austritt per Rueckwaertsstrahl vom Zielpunkt aus
		/// (trifft er nicht, liegt der Zielpunkt im Kollider - der Gegner steckt
		/// im Busch). Beginnt der Strahl im Kollider (eigener Busch), meldet
		/// Unity keinen Treffer: der eigene Busch verdeckt also nicht.
		/// </summary>
		public static void Merken(Collider c, float eintritt, Vector3 punkt, Vector3 richtung, float laenge)
		{
			if (_anzahl >= _von.Length)
			{
				return;
			}
			float austritt = laenge;
			try
			{
				if (c.Raycast(new Ray(punkt, -richtung), out RaycastHit h, laenge))
				{
					austritt = laenge - h.distance;
				}
			}
			catch
			{
			}
			if (austritt <= eintritt)
			{
				return;
			}
			_von[_anzahl] = eintritt;
			_bis[_anzahl] = austritt;
			_anzahl++;
		}

		/// <summary>Laub-Kollider auf der Strecke dazunehmen (EIN Strahl, Trigger zaehlen).</summary>
		public static void Strahl(Vector3 start, Vector3 richtung, float laenge, Vector3 punkt, Player ziel,
								  HashSet<Collider> zielKollider, Transform zielWurzel, ref string name)
		{
			int maske = Maske();
			if (maske == 0)
			{
				return;
			}
			int n = Physics.RaycastNonAlloc(start, richtung, _treffer, laenge, maske, QueryTriggerInteraction.Collide);
			for (int i = 0; i < n; i++)
			{
				Collider c = _treffer[i].collider;
				if (c == null || Zielpunkt.IstEigener(c) || (ziel != null && Zielpunkt.ZumZiel(c, zielKollider, zielWurzel)))
				{
					continue;
				}
				int vorher = _anzahl;
				Merken(c, _treffer[i].distance, punkt, richtung, laenge);
				if (_anzahl > vorher && name.Length == 0)
				{
					name = c.name;
				}
			}
		}

		/// <summary>Gesamtlaenge im Laub (ueberlappende Kugeln eines Busches nur einmal).</summary>
		public static float Tiefe()
		{
			// Einfuegesortierung nach Eintritt - es sind nur wenige.
			for (int i = 1; i < _anzahl; i++)
			{
				float v = _von[i], b = _bis[i];
				int j = i - 1;
				while (j >= 0 && _von[j] > v)
				{
					_von[j + 1] = _von[j];
					_bis[j + 1] = _bis[j];
					j--;
				}
				_von[j + 1] = v;
				_bis[j + 1] = b;
			}
			float summe = 0f;
			float a = 0f, e = -1f;
			for (int i = 0; i < _anzahl; i++)
			{
				if (_von[i] > e)
				{
					if (e > a)
					{
						summe += e - a;
					}
					a = _von[i];
					e = _bis[i];
				}
				else if (_bis[i] > e)
				{
					e = _bis[i];
				}
			}
			if (e > a)
			{
				summe += e - a;
			}
			return summe;
		}
	}

	internal static class Zielpunkt
	{
		private struct Glaettung
		{
			public Vector3 Tempo;

			public float Zeit;

			public float Wechsel;
		}

		public static string LetzteQuelle = "";

		private static readonly Dictionary<int, Glaettung> Verlauf = new Dictionary<int, Glaettung>();

		public static float LetzterFall;

		// Auf weite Distanz entscheidend und bisher unsichtbar: Zielentfernung
		// und die Einschiessentfernung des Glases. Stimmt die zweite nicht,
		// rechnet der Fallausgleich systematisch falsch.
		public static float LetzteEntfernung;

		public static int LetzteEinschiessung;

		// Rohwerte der Flugbahn-Rechnung. Damit laesst sich nachpruefen, ob der
		// Betrag des Fallausgleichs stimmt - bei einem fest eingeschossenen Glas
		// ist er nicht abschaltbar, also muss er richtig sein.
		public static float FallBeiZiel;

		public static float FallBeiEinschuss;

		public static float Muendungstempo;

		// Wie viel die Nullung noch beitraegt: 1 = voll (Glas fuehrt),
		// 0 = gar nicht (Muendung wird auf den Zielpunkt gedreht).
		public static float NullungWirkt = 1f;

		public static Vector3 LetztesTempo;

		public static float LetzterWechsel;

		// Nur Diagnose (Einschlag-Aufzeichnung): was MitVorhalt im letzten
		// Aufruf wirklich angewandt hat. Wirkt nicht auf die Rechnung.
		public static Player VorhaltZiel;

		public static int VorhaltBild = -99;

		/// <summary>Angewandter Vorhalt (m, Welt) samt Bildverzug, ohne Fallausgleich.</summary>
		public static Vector3 VorhaltVektor;

		/// <summary>Geglaettetes Tempo, aus dem der Vorhalt gerechnet wurde (m/s, Welt).</summary>
		public static Vector3 VorhaltTempoGl;

		/// <summary>Momentanes Tempo des Ziels (Player.Velocity) im selben Bild.</summary>
		public static Vector3 VorhaltTempoRoh;

		public static float VorhaltFlugzeit;

		/// <summary>Angewandter Fallausgleich (m, Welt-y) in diesem Aufruf.</summary>
		public static float VorhaltFall;

		/// <summary>Punkt ohne Vorhalt und ohne Fall (Box/Zonenpunkt), fuer den Bezug.</summary>
		public static Vector3 VorhaltBasis;

		public static Vector3 Koerper(Player p)
		{
			PlayerBones playerBones = p?.PlayerBones;
			if (playerBones?.Ribcage != null)
			{
				return playerBones.Ribcage.position;
			}
			if (!(p != null))
			{
				return Vector3.zero;
			}
			return p.Position;
		}

		public static Vector3 Becken(Player p)
		{
			PlayerBones playerBones = p?.PlayerBones;
			// Wie die Zonenwahl (Pelvis.Original) - dann trifft die Ablage je Bild.
			Transform pb = playerBones?.Pelvis?.Original;
			if (pb != null)
			{
				return pb.position;
			}
			return (p != null) ? p.Position : Vector3.zero;
		}

		// Hebung ueber dem Kopfknochen fuer den Rueckfall ohne Kopfbox (frueher
		// der Regler "Kopf-Hoehenkorrektur in Metern", Standard 0,12 m).
		private const float KopfknochenHebung = 0.12f;

		public static Vector3 Fuer(Player p)
		{
			PlayerBones playerBones = p?.PlayerBones;
			if (playerBones == null)
			{
				LetzteQuelle = "NO BONES";
				if (!(p != null))
				{
					return Vector3.zero;
				}
				return p.Position;
			}
			Vector3 result;
			switch (Einstellungen.Ziel.Value)
			{
			case Knochen.Chest:
				result = ((playerBones.Ribcage != null) ? playerBones.Ribcage.position : p.Position);
				break;
			case Knochen.Pelvis:
				result = ((playerBones.Pelvis != null) ? playerBones.Pelvis.position : p.Position);
				break;
			case Knochen.Legs:
				result = Trefferzonen.Huelle(p, Trefferzonen.Oberschenkel, out var bh) ? bh.center : p.Position;
				break;
			default:
				if (Trefferbox(playerBones, Einstellungen.Kopfziel.Value, out var boxmitte, out var boxname))
				{
					// Die Mitte der echten Trefferbox - also genau der Koerper, mit dem
					// das Spiel den Treffer verrechnet. Dreht sich mit dem Kopf mit.
					result = boxmitte;
					LetzteQuelle = boxname + $"+{result.y - p.Position.y:0.00}m";
				}
				else if (playerBones.Head != null)
				{
					// Rueckfall nur, wenn KEINE Kopfbox gefunden wurde: Kopfknochen
					// (sitzt am Halsansatz) plus feste Hebung.
					result = playerBones.Head.position + Vector3.up * KopfknochenHebung;
					LetzteQuelle = $"Head+{result.y - p.Position.y:0.00}m";
				}
				else
				{
					result = p.Position;
					LetzteQuelle = "NO HEAD BONE";
				}
				break;
			}
			return result;
		}

		// Rueckfallketten. Fehlt die gewuenschte Box an einem Modell, wird der
		// Reihe nach die naechstbeste genommen - lieber eine andere Kopfbox als
		// zurueck auf den Knochen mit seinem Schaetzversatz.
		private static readonly EBodyPartColliderType[] KetteKopfmitte =
		{
			EBodyPartColliderType.HeadCommon, EBodyPartColliderType.ParietalHead,
			EBodyPartColliderType.Eyes
		};

		private static readonly EBodyPartColliderType[] KetteGesicht =
		{
			EBodyPartColliderType.Eyes, EBodyPartColliderType.Jaw,
			EBodyPartColliderType.HeadCommon
		};

		private static readonly EBodyPartColliderType[] KetteKiefer =
		{
			EBodyPartColliderType.Jaw, EBodyPartColliderType.Eyes,
			EBodyPartColliderType.HeadCommon
		};

		private static readonly EBodyPartColliderType[] KetteSchaedeldecke =
		{
			EBodyPartColliderType.ParietalHead, EBodyPartColliderType.HeadCommon,
			EBodyPartColliderType.Eyes
		};

		// Alle Kopfboxen, nach Vorliebe geordnet: zuerst die grosse Kopfbox,
		// dann der Rest. Hals zuletzt - er zaehlt zwar zum Kopf, ist aber die
		// schmalste Stelle.
		//
		// Frueher stand Eyes vorn, mit der Begruendung "am seltensten vom Helm
		// gedeckt". Das war falsch, und zwar doppelt:
		//   - Der Panzerungsanteil am Schaden ist dadurch NICHT gesunken
		//     (34 % vorher, 36 % nachher) - die Begruendung traegt nicht.
		//   - Eyes liegt 11 cm VOR dem Kopfknochen, also an der Gesichts-
		//     oberflaeche. HeadCommon liegt 6 cm entfernt, mitten im Kopf.
		//     Steht der Gegner nicht frontal, zielt Eyes auf den Rand der
		//     Trefferbox oder daneben. Am 13.09.2026 gemessen: 345 von 348
		//     Zielpunkten waren Eyes, Muendungsfehler 0,507° statt 0,335°.
		internal static readonly EBodyPartColliderType[] AlleKopfboxen =
		{
			EBodyPartColliderType.HeadCommon, EBodyPartColliderType.Eyes,
			EBodyPartColliderType.Jaw, EBodyPartColliderType.ParietalHead,
			EBodyPartColliderType.BackHead, EBodyPartColliderType.Ears,
			EBodyPartColliderType.NeckFront, EBodyPartColliderType.NeckBack
		};

		private static EBodyPartColliderType[] Kette(Kopfbox wunsch)
		{
			switch (wunsch)
			{
			case Kopfbox.Face:
				return KetteGesicht;
			case Kopfbox.Jaw:
				return KetteKiefer;
			case Kopfbox.Skull:
				return KetteSchaedeldecke;
			default:
				return KetteKopfmitte;
			}
		}

		/// <summary>
		/// Liefert die Mitte der gewuenschten Kopf-Trefferbox.
		///
		/// Warum das besser ist als der Kopfknochen: Der Knochen sitzt am
		/// Halsansatz, deshalb brauchte es frueher einen Hoehenversatz - und der
		/// zeigte in WELTkoordinaten nach oben, drehte sich also nicht mit dem
		/// Kopf mit. Bei liegenden, lehnenden oder nach oben schauenden Gegnern
		/// rutschte der Zielpunkt dadurch vom Kopf herunter.
		///
		/// PlayerBones fuehrt die Boxen selbst in BodyPartCollidersDictionary,
		// Letzte angewandte Verschiebung nach oben, fuer die Diagnose.
		public static float LetzteBoxhebung;

		// Abstand zwischen Kamera und Feuerpunkt - genau der Betrag, um den
		// der Zielwinkel frueher danebenlag.
		public static float LetzteParallaxe;

		/// <summary>
		/// Der Zielpunkt INNERHALB einer Trefferbox.
		///
		/// Bisher wurde schlicht box.Center genommen, also die Mitte des
		/// Kolliders. Die liegt bei der Kopfbox aber auf Kinnhoehe - im Bild
		/// sass das Fadenkreuz sichtbar am unteren Rand des Kopfes. Jetzt
		/// wird ein Anteil der Boxhoehe daraufgelegt: 0,5 waere die alte
		/// Mitte, 1 die Oberkante.
		/// </summary>
		private static Vector3 BoxPunkt(BodyPartCollider box)
		{
			Vector3 mitte = box.Center;
			try
			{
				float anteil = (Einstellungen.BoxHoehe != null) ? Einstellungen.BoxHoehe.Value : 0.5f;
				Collider c = box.Collider;
				if (c == null || Mathf.Abs(anteil - 0.5f) < 0.001f)
				{
					LetzteBoxhebung = 0f;
					return mitte;
				}
				float hoehe = c.bounds.size.y;
				if (hoehe <= 0.001f || hoehe > 1f)
				{
					// Unplausibel - dann lieber nichts verschieben.
					LetzteBoxhebung = 0f;
					return mitte;
				}
				float hebung = hoehe * (anteil - 0.5f);
				LetzteBoxhebung = hebung;
				mitte.y += hebung;
			}
			catch
			{
				LetzteBoxhebung = 0f;
			}
			return mitte;
		}

		/// und BodyPartCollider.Center liefert die Mitte des Kolliders in
		/// Weltkoordinaten - sie wandert und dreht sich mit dem Knochen mit.
		///
		/// ACHTUNG, nicht RealCenter benutzen: die Eigenschaft ruft ein Delegat
		/// func_0 auf, das BSG nirgends zuweist. Jeder Zugriff wirft eine
		/// NullReferenceException. Am 12.09.2026 im Raid gemessen: Tabelle mit
		/// 26 Eintraegen vorhanden, alle acht Kopfboxen warfen beim Zugriff.
		/// </summary>
		private static bool Trefferbox(PlayerBones knochen, Kopfbox wunsch,
									   out Vector3 mitte, out string name)
		{
			mitte = Vector3.zero;
			name = "";
			try
			{
				Dictionary<EBodyPartColliderType, BodyPartCollider> karte =
					knochen.BodyPartCollidersDictionary;
				if (karte == null)
				{
					return false;
				}
				EBodyPartColliderType[] kette = Kette(wunsch);
				for (int i = 0; i < kette.Length; i++)
				{
					if (!karte.TryGetValue(kette[i], out var box) || box == null)
					{
						continue;
					}
					if (box.Collider == null)
					{
						continue;
					}
					// Absichtlich NICHT auf collider.enabled pruefen: EFT haelt die
					// Trefferkoerper nicht zwingend aktiv, die Lage stimmt trotzdem.
					mitte = BoxPunkt(box);
					name = kette[i].ToString();
					return true;
				}
			}
			catch (Exception ex)
			{
				// Lieber auf den Knochen zurueckfallen als das Bild verlieren.
				LetzterBoxfehler = ex.GetType().Name + ": " + ex.Message;
			}
			return false;
		}

		/// <summary>
		/// Gehoert dieser Kollider zum Gegner selbst? Dann darf er dessen
		/// eigene Kopfboxen nicht verdecken.
		/// </summary>
		internal static bool ZumZiel(Collider c, HashSet<Collider> eigene, Transform wurzel)
		{
			if (eigene != null && eigene.Contains(c))
			{
				return true;
			}
			if (wurzel != null && c.transform != null && c.transform.IsChildOf(wurzel))
			{
				return true;
			}
			return false;
		}

		public static string LetzteBoxlage = "";

		public static string LetzterBoxfehler = "";

		// Die tatsaechlich gewaehlte Box und wie weit der ENDGUELTIGE Zielpunkt
		// (nach Vorhalt und Fallausgleich) von ihrer Mitte abweicht.
		// Getrennt von LetzteQuelle, weil Fuer() das Etikett im Suchlauf jedes
		// Bild ueberschreibt - die Diagnose zeigte dadurch HeadCommon an,
		// obwohl auf Eyes gezielt wurde.
		public static string LetzteWahl = "";

		public static float LetzterVersatz;

		// Getrennt nach SEITLICH und HOCH. Bisher stand im Protokoll nur der
		// Gesamtbetrag - und der ist auf Entfernung zu 90 % der Kugelfall, nicht
		// der Vorhalt. Wer "der Vorhalt ist zu weit vorne" pruefen will, braucht
		// die seitliche Zahl allein; die senkrechte gehoert zum Fallausgleich.
		public static float LetzterVersatzSeitlich;

		public static float LetzterVersatzHoch;

		// Entscheidend ist nicht, um wie viele Zentimeter der Vorhalt den Punkt
		// schiebt, sondern um wie viel GRAD - also quer zur Sichtlinie.
		// Verschiebt er entlang der Sichtlinie, aendert er den Zielwinkel nicht.
		public static float LetzterVersatzGrad;

		// Feste Ablage fuer die Strahltreffer der Box-Pruefung.
		private static readonly RaycastHit[] Strahl = new RaycastHit[24];

		// --- Die eigenen Kollider ---------------------------------------
		// Im Raid vom 19.09.2026 gemessen: von 133 Faellen, in denen die
		// Sichtpruefung einen Gegner verworfen hat, waren 123 der EIGENE
		// Unterarm (Base HumanRForearm1 auf 0,4 m, Base HumanLForearm1 auf
		// 0,8 m). Die Hilfe tat dann nichts, obwohl freie Schussbahn war.
		//
		// Der Regler "Sichtpruefung startet x Meter vor der Kamera" war der
		// bisherige Notbehelf - er schiebt den Strahlanfang vor den eigenen
		// Koerper. Das ist Flickwerk: zu klein gewaehlt haengt der Arm drin,
		// zu gross uebersieht man duenne Deckung dicht vor sich, und wo der
		// Arm gerade steckt, aendert sich mit jeder Animation.
		//
		// Richtig ist, die eigenen Koerperteile beim Namen zu nennen und zu
		// ueberspringen. Genau das passiert hier.
		public static HashSet<Collider> EigeneKollider;

		public static Transform EigeneWurzel;

		/// <summary>
		/// Merkt sich einmal je Bild, welche Kollider zum Spieler SELBST
		/// gehoeren - Koerper, Arme, Ausruestung, Waffe.
		/// </summary>
		public static void EigeneMerken(Player ich)
		{
			try
			{
				EigeneKollider = ich?.PlayerBones?.BodyPartCollidersHashSet;
				EigeneWurzel = ich?.Transform?.Original;
			}
			catch
			{
				EigeneKollider = null;
				EigeneWurzel = null;
			}
		}

		/// <summary>
		/// Startpunkt jeder Sichtpruefung: der Feuerpunkt (dort startet die Kugel,
		/// Schusslinie nach AdjustShotVectors), sonst die Kamera.
		/// </summary>
		public static Vector3 SichtStart(Player ich, Camera kamera)
		{
			if (ich != null && Rueckstoss.Schusslinie(ich, out var start, out _))
			{
				return start;
			}
			return (kamera != null) ? kamera.transform.position : ((ich != null) ? (ich.Position + Vector3.up * 1.5f) : Vector3.zero);
		}

		private static readonly RaycastHit[] _sichtStrahl = new RaycastHit[32];

		/// <summary>
		/// Freie Schussbahn von start bis GENAU zum Punkt? Uebersprungen werden nur
		/// die eigenen Kollider und die des Ziels selbst (Koerperteile, Panzerplatten,
		/// alles unter seinem Objekt - ZumZiel); jedes andere Hindernis zaehlt, ausser
		/// es ist durchsichtig (ab 1.4.7 Standard, Durchschuss.NurOptisch) bzw. die
		/// eigene Patrone schlaegt es nach Spielregel durch (Durchschuss.Durchlaessig).
		/// Keine Toleranz am Ende: die Strahllaenge endet am Punkt.
		/// </summary>
		/// <param name="nurDurchsichtig">Siehe Durchschuss.Durchlaessig (AFK-Wache).</param>
		// Ergebnis-Ablage je Bild (ab 1.4.6): dieselbe Strecke wird im selben Bild
		// mehrfach geprueft (gehaltene Kopfbox, dann die Box-Schleife; Zielsuche,
		// dann Zonenwahl) - jede weitere Abfrage kostet so keinen Strahl mehr.
		private struct SichtErgebnis
		{
			public Vector3 Start, Punkt;
			public int Ziel;
			public bool NurDurchsichtig, Laub, Optisch, Frei;
			public string Grund;
			public Collider Hindernis;
			public float Abstand;
		}

		private static readonly SichtErgebnis[] _sichtAblage = new SichtErgebnis[64];

		private static int _sichtAblageBild = -1;

		private static int _sichtAblageAnzahl;

		public static bool Sichtlinie(Vector3 start, Vector3 punkt, Player ziel, out string grund, out Collider hindernis, out float abstand, bool nurDurchsichtig = false)
		{
			// Laub nur fuer die Zielhilfe selbst. Die AFK-Wache (nurDurchsichtig)
			// prueft die Flugbahn der Kugel, und die geht durch einen Busch.
			bool laub = !nurDurchsichtig && Laub.An;
			// Ab 1.4.7: Sicht fuer das Auge - ebenfalls nur fuer die Zielhilfe; die
			// Flugbahn-Pruefung der AFK-Wache bleibt bei der Kugel (sie prueft die
			// Sicht zusaetzlich mit diesem Normalaufruf).
			bool optisch = !nurDurchsichtig && Durchschuss.NurOptisch;
			int zielId = (ziel != null) ? ziel.GetInstanceID() : 0;
			if (_sichtAblageBild != Time.frameCount)
			{
				_sichtAblageBild = Time.frameCount;
				_sichtAblageAnzahl = 0;
			}
			for (int k = 0; k < _sichtAblageAnzahl; k++)
			{
				ref SichtErgebnis s = ref _sichtAblage[k];
				if (s.Ziel == zielId && s.NurDurchsichtig == nurDurchsichtig && s.Laub == laub && s.Optisch == optisch && s.Start == start && s.Punkt == punkt)
				{
					grund = s.Grund;
					hindernis = s.Hindernis;
					abstand = s.Abstand;
					return s.Frei;
				}
			}
			bool frei = SichtlinieRechnen(start, punkt, ziel, out grund, out hindernis, out abstand, nurDurchsichtig, laub, optisch);
			if (_sichtAblageAnzahl < _sichtAblage.Length)
			{
				_sichtAblage[_sichtAblageAnzahl++] = new SichtErgebnis
				{
					Start = start, Punkt = punkt, Ziel = zielId, NurDurchsichtig = nurDurchsichtig, Laub = laub, Optisch = optisch,
					Frei = frei, Grund = grund, Hindernis = hindernis, Abstand = abstand
				};
			}
			return frei;
		}

		private static bool SichtlinieRechnen(Vector3 start, Vector3 punkt, Player ziel, out string grund, out Collider hindernis, out float abstand, bool nurDurchsichtig, bool laub, bool optisch)
		{
			grund = "clear";
			hindernis = null;
			abstand = 0f;
			Vector3 weg = punkt - start;
			float laenge = weg.magnitude;
			if (laenge < 0.01f)
			{
				return true;
			}
			Vector3 richtung = weg / laenge;
			int anzahl = Physics.RaycastNonAlloc(start, richtung, _sichtStrahl, laenge,
												 Durchschuss.Ebenenmaske(), QueryTriggerInteraction.Ignore);
			if (laub)
			{
				Laub.Beginnen();
			}
			string laubName = "";
			HashSet<Collider> zielKollider = null;
			Transform zielWurzel = null;
			try
			{
				zielKollider = ziel?.PlayerBones?.BodyPartCollidersHashSet;
				zielWurzel = ziel?.Transform?.Original;
			}
			catch
			{
			}
			float naechstes = float.MaxValue;
			string durch = "";
			string blockGrund = "";
			for (int i = 0; i < anzahl; i++)
			{
				Collider c = _sichtStrahl[i].collider;
				if (c == null || IstEigener(c))
				{
					continue;
				}
				if (ziel != null && ZumZiel(c, zielKollider, zielWurzel))
				{
					continue;
				}
				if (Durchschuss.Durchlaessig(c, out var g, nurDurchsichtig, optisch))
				{
					// Hohes Gras/Gestruepp als Kugel-Kollider: die Kugel geht durch,
					// das Auge nicht - wie Laub nach Tiefe werten.
					if (laub && Durchschuss.Material(c, out var mat) && mat == MaterialType.GrassHigh)
					{
						int vorher = laubName.Length;
						Laub.Merken(c, _sichtStrahl[i].distance, punkt, richtung, laenge);
						if (vorher == 0)
						{
							laubName = c.name;
						}
						continue;
					}
					if (durch.Length == 0)
					{
						durch = g;
					}
					continue;
				}
				if (_sichtStrahl[i].distance < naechstes)
				{
					naechstes = _sichtStrahl[i].distance;
					hindernis = c;
					blockGrund = g;
				}
			}
			if (hindernis != null)
			{
				abstand = naechstes;
				grund = $"covered by {hindernis.name} [{blockGrund}] at {naechstes:0.0} m";
				return false;
			}
			if (laub)
			{
				// Erst jetzt der Laub-Strahl: bei fester Deckung ist er unnoetig.
				Laub.Strahl(start, richtung, laenge, punkt, ziel, zielKollider, zielWurzel, ref laubName);
				float tiefe = Laub.Tiefe();
				if (tiefe > 0f && tiefe >= Einstellungen.LaubTiefe.Value)
				{
					abstand = 0f;
					grund = $"covered by {laubName} [foliage {tiefe:0.00} m]";
					return false;
				}
				if (tiefe > 0f && durch.Length == 0)
				{
					durch = $"foliage {tiefe:0.00} m";
				}
			}
			if (durch.Length > 0)
			{
				grund = "through " + durch;
			}
			return true;
		}

		/// <summary>
		/// Gehoert dieser Kollider zu mir selbst? Dann ist er kein Hindernis -
		/// die Kugel verlaesst die Muendung vor meinem Arm, nicht dahinter.
		/// </summary>
		public static bool IstEigener(Collider c)
		{
			return ZumZiel(c, EigeneKollider, EigeneWurzel);
		}

		/// <summary>
		/// Sucht die Kopf-Trefferbox, die vom Auge aus gerade FREI liegt.
		///
		/// Der Kopf ist acht einzelne Boxen. Steckt der Gegner hinter einer
		/// Mauerkante, ist der halbe Kopf verdeckt und der andere halbe nicht -
		/// auf die verdeckte Haelfte zu zielen heisst, in die Mauer zu schiessen.
		/// Hier wird deshalb jede Box einzeln geprueft und die erste freie
		/// genommen, in der Reihenfolge von AlleKopfboxen.
		///
		/// Die Pruefung ist absichtlich STRENGER als die allgemeine Sichtpruefung
		/// in Zielsuche: die arbeitet mit einer halben Meter Toleranz, was fuer
		/// "ist der Gegner ueberhaupt zu sehen" richtig ist, fuer einen Kopf von
		/// gut 20 Zentimetern aber sinnlos waere. Hier gilt eine Box nur dann als
		/// frei, wenn der Strahl auch tatsaechlich auf IHREM Kollider landet.
		///
		/// Laeuft nur fuer das bereits ausgewaehlte Ziel, nicht fuer jeden
		/// Gegner im Suchlauf - sonst waeren es acht Strahlen pro Gegner und Bild.
		/// </summary>
		// Zuletzt gewaehlte freie Kopfbox (Bestaendigkeit): BoxHalter (Ruhe.cs).

		/// <summary>Die gehaltene Kopfbox dieses Gegners, falls vorhanden.</summary>
		internal static bool GehalteneBox(Player ziel, out EBodyPartColliderType typ)
		{
			bool hat = BoxHalter.Instanz.Gehalten(ziel, out int b);
			typ = (EBodyPartColliderType)b;
			return hat;
		}

		public static bool SichtbareBox(Player ziel, Camera kamera,
										out Vector3 mitte, out string name,
										EBodyPartColliderType[] reihenfolge = null, bool halten = true)
		{
			// Reihenfolge der Vorliebe: ab Werk AlleKopfboxen, bei "Zone nach
			// Panzerung" die am wenigsten gepanzerte Box zuerst.
			EBodyPartColliderType[] boxen = reihenfolge ?? AlleKopfboxen;
			// Bestaendigkeit: die zuletzt gewaehlte Box DESSELBEN Gegners bleibt,
			// solange sie frei liegt - sonst springt der Zielpunkt zwischen
			// gleich guten Boxen (Eyes/HeadCommon 6-11 cm), sobald eine an der
			// Deckungskante flackert. Neu gewaehlt wird erst, wenn sie verdeckt
			// ist (BoxHalter, Ruhe.cs).
			if (halten && BoxHalter.Instanz.Gehalten(ziel, out int hid) && ziel?.PlayerBones?.BodyPartCollidersDictionary != null
				&& ziel.PlayerBones.BodyPartCollidersDictionary.TryGetValue((EBodyPartColliderType)hid, out var hb) && hb != null && hb.Collider != null)
			{
				Vector3 hp = BoxPunkt(hb);
				if (Sichtlinie(SichtStart(Spielzugriff.Ich, kamera), hp, ziel, out _, out _, out _))
				{
					mitte = hp;
					name = ((EBodyPartColliderType)hid).ToString();
					LetzteBoxlage = name + "=clear (held)";
					return true;
				}
			}
			mitte = Vector3.zero;
			name = "";
			try
			{
				PlayerBones knochen = ziel?.PlayerBones;
				Dictionary<EBodyPartColliderType, BodyPartCollider> karte =
					knochen?.BodyPartCollidersDictionary;
				if (karte == null || kamera == null)
				{
					return false;
				}
				// Vom Feuerpunkt aus - dort startet die Kugel. Eigene Kollider und
				// die des Gegners ueberspringt Sichtlinie; kein Vorsprung mehr noetig.
				Vector3 start = SichtStart(Spielzugriff.Ich, kamera);
				// Die eigenen Kollider des GEGNERS duerfen seine eigenen Kopfboxen
				// nicht verdecken. Von hinten liegt BackHead vor Eyes, und ein Helm
				// liegt vor allem - sonst gilt reihum jede Box als verdeckt und die
				// Notloesung zielt auf die abgewandte Seite. Blockieren darf nur
				// Welt-Geometrie. PlayerBones fuehrt die Liste selbst.
				HashSet<Collider> eigene = knochen.BodyPartCollidersHashSet;
				// Zusaetzlich zur Liste des Spiels: alles, was unterhalb des
				// Gegner-Objekts haengt. Die Liste kennt Koerperteile und
				// Panzerplatten - Helm, Rucksack und sonstige Ausruestung stehen
				// nicht zwingend drin, verdecken den Kopf aber genauso.
				Transform zielWurzel = ziel?.Transform?.Original;

				Vector3 ersteMitte = Vector3.zero;
				string ersteName = "";
				bool hatErste = false;
				string lage = "";
				string blockierer = "";

				for (int i = 0; i < boxen.Length; i++)
				{
					if (!karte.TryGetValue(boxen[i], out var box) || box == null)
					{
						continue;
					}
					Collider collider = box.Collider;
					if (collider == null)
					{
						continue;
					}
					Vector3 ziel2 = BoxPunkt(box);
					if (!hatErste || boxen[i] == EBodyPartColliderType.HeadCommon)
					{
						// Notloesung, falls am Ende KEINE Box frei liegt: die
						// grosse Kopfbox.
						//
						// Vorher stand hier "die naechstgelegene". Das war gut
						// gemeint - bei einem fliehenden Gegner liegen die Augen
						// auf der abgewandten Seite - aber falsch: die
						// naechstgelegene war dann NeckBack, also 25 cm unter der
						// Kopfmitte. Gezielt wurde damit auf Nacken und Ruecken.
						// HeadCommon ist unabhaengig von der Blickrichtung immer
						// der Kopf.
						ersteMitte = ziel2;
						ersteName = boxen[i].ToString();
						hatErste = true;
					}
					// Bis GENAU zum Punkt in der Box; die Box selbst und alles am
					// Gegner ueberspringt Sichtlinie, alles andere zaehlt.
					bool frei = Sichtlinie(start, ziel2, ziel, out _, out var block, out _);
					if (!frei && blockierer.Length == 0 && block != null)
					{
						blockierer = block.name;
					}
					if (Einstellungen.Diagnose.Value)
					{
						lage += boxen[i] + (frei ? "=clear " : "=closed ");
					}
					if (frei)
					{
						mitte = ziel2;
						name = boxen[i].ToString();
						LetzteBoxlage = lage.TrimEnd();
						BoxHalter.Instanz.Merke(ziel, (int)boxen[i]);
						return true;
					}
				}

				LetzteBoxlage = lage.TrimEnd()
					+ ((blockierer.Length > 0) ? $" (blocked by {blockierer})" : "");
				if (hatErste)
				{
					// Keine einzige Box frei. Lieber die bevorzugte nehmen als
					// gar nichts - die allgemeine Sichtpruefung haette den Gegner
					// ohnehin schon aussortiert, wenn er ganz verdeckt waere.
					mitte = ersteMitte;
					name = ersteName + "(covered)";
					return true;
				}
			}
			catch (Exception ex)
			{
				// Lieber auf den bisherigen Zielpunkt zurueckfallen.
				LetzterBoxfehler = ex.GetType().Name + ": " + ex.Message;
			}
			return false;
		}

		/// <summary>
		/// Listet die Kopf-Trefferboxen eines Spielers samt Lage auf. Nur fuer die
		/// Diagnose gedacht - damit man sieht, welche Boxen ein Modell ueberhaupt
		/// hat und wie weit ihre Mitte ueber dem Kopfknochen liegt.
		/// </summary>
		public static string Kopfboxen(Player p)
		{
			try
			{
				PlayerBones knochen = p?.PlayerBones;
				Dictionary<EBodyPartColliderType, BodyPartCollider> karte =
					knochen?.BodyPartCollidersDictionary;
				if (karte == null)
				{
					return "no bones";
				}
				Vector3 kopf = Vector3.zero;
				string kopfquelle = "head bone missing";
				try
				{
					if (knochen.Head != null)
					{
						kopf = knochen.Head.position;
						kopfquelle = "";
					}
				}
				catch (Exception ex2)
				{
					kopfquelle = "head bone throws " + ex2.GetType().Name;
				}

				string text = $"entries={karte.Count} {kopfquelle} ";
				for (int i = 0; i < AlleKopfboxen.Length; i++)
				{
					// Jede Box einzeln absichern, damit eine kaputte nicht die
					// ganze Auskunft verschluckt.
					try
					{
						if (!karte.TryGetValue(AlleKopfboxen[i], out var box))
						{
							text += AlleKopfboxen[i] + "=notInTable ";
							continue;
						}
						if (box == null)
						{
							text += AlleKopfboxen[i] + "=entry_null ";
							continue;
						}
						if (box.Collider == null)
						{
							text += AlleKopfboxen[i] + "=noCollider ";
							continue;
						}
						Vector3 vector = box.Center - kopf;
						text += $"{AlleKopfboxen[i]}={vector.y * 100f:+0;-0;0}cm/{vector.magnitude * 100f:0}cm"
								+ (box.Collider.enabled ? " " : "(off) ");
					}
					catch (Exception ex3)
					{
						text += AlleKopfboxen[i] + "=" + ex3.GetType().Name + " ";
					}
				}
				return text.TrimEnd();
			}
			catch (Exception ex)
			{
				return "Error: " + ex.Message;
			}
		}

		private static float Fallausgleich(Player ich, float entfernung)
		{
			LetzterFall = 0f;
			if (!Einstellungen.FallAusgleich.Value)
			{
				return 0f;
			}
			try
			{
				AmmoTemplate currentAmmoTemplate = Spielzugriff.Patrone(ich, out var weapon);
				if (weapon == null)
				{
					return 0f;
				}
				if (currentAmmoTemplate == null || currentAmmoTemplate.InitialSpeed < 1f)
				{
					return 0f;
				}
				int num = 0;
				SightComponent sightComponent = ich.ProceduralWeaponAnimation?.CurrentAimingMod;
				if (sightComponent != null)
				{
					num = sightComponent.GetCurrentOpticCalibrationDistance();
				}
				if (num <= 0)
				{
					num = 100;   // Notwert - taucht in der Diagnose als "Standard" auf
					LetzteEinschiessung = -100;
				}
				else
				{
					LetzteEinschiessung = num;
				}
				LetzteEntfernung = entfernung;
				Flugbahn flugbahn = Flugbahn.Fuer(currentAmmoTemplate, weapon.SpeedFactor);
				float num2 = flugbahn.Fall(entfernung);
				float num3 = flugbahn.Fall(num);
				FallBeiZiel = num2;
				FallBeiEinschuss = num3;
				Muendungstempo = currentAmmoTemplate.InitialSpeed * weapon.SpeedFactor;
				// WIE VIEL FALL MUSS AUF DEN ZIELPUNKT?
				//
				// Das haengt davon ab, WAS am Ende auf den Zielpunkt zeigt:
				//
				//  a) Zeigt das GLAS hin (kein Muendungsausgleich), steht der
				//     Lauf durch die Nullung schon von selbst hoeher. Dann
				//     fehlt nur der Rest:  Fall(Ziel) - Fall(Null)*Ziel/Null.
				//
				//  b) Zeigt die MUENDUNG hin, ist die Erhoehung der Nullung
				//     bereits weggerechnet - der Zielpunkt muss den GANZEN
				//     Fall tragen:  Fall(Ziel).
				//
				// Der Rueckstossausgleich misst den vollen Winkel zwischen
				// Kamera und Laufrichtung und dreht ihn weg. Bei Ausgleich 1
				// gilt also Fall b). Bisher rechneten wir immer nach a) - und
				// schossen dadurch zu tief, und zwar umso mehr, je naeher die
				// Nullung lag. Mit der Auto-Nullung (Nullung = Zielentfernung)
				// war der Fehler am groessten: dort wurde gar nichts mehr
				// ausgeglichen.
				// NICHT mehr "bin ich im Anschlag?", sondern "wird die
				// Muendung ausgeglichen?". Seit auch aus der Huefte
				// ausgeglichen wird, sind das zwei verschiedene Fragen -
				// und die alte lieferte dort die zu kleine Fallkorrektur.
				float anteil = Mathf.Clamp01(Rueckstoss.AktiverAnteil);
				NullungWirkt = 1f - anteil;
				return LetzterFall = num2 - NullungWirkt * num3 * (entfernung / Mathf.Max(1f, num));
			}
			catch
			{
				return 0f;
			}
		}

		// --- Vorhalt-Probe ------------------------------------------------
		// Misst, ob der Vorhalt stimmt, statt es zu glauben: zum Zeitpunkt
		// der Berechnung wird die vorhergesagte Zielposition gemerkt. Eine
		// Flugzeit spaeter wird sie mit der tatsaechlichen verglichen. Der
		// Fehler wird laengs der Bewegungsrichtung gemessen:
		//   positiv = zu viel Vorhalt, negativ = zu wenig.
		private struct Probe
		{
			public float Faellig;

			public Vector3 Vorhergesagt;

			public Vector3 Richtung;

			public int Ziel;
		}

		private static readonly List<Probe> Proben = new List<Probe>(32);

		private static readonly List<float> Fehler = new List<float>(200);

		private static float _letzteProbe = -999f;

		public static string ProbeText = "";

		public static float LetzterBildvorhalt;

		private static void ProbeMerken(Player ziel, Vector3 vorhergesagt, Vector3 richtung, float flugzeit)
		{
			// Hoechstens zehn Proben je Sekunde - sonst fuellt sich die Liste
			// mit fast identischen Messungen.
			if (Time.time - _letzteProbe < 0.1f || flugzeit <= 0.001f || Proben.Count > 24)
			{
				return;
			}
			_letzteProbe = Time.time;
			Proben.Add(new Probe
			{
				Faellig = Time.time + flugzeit,
				Vorhergesagt = vorhergesagt,
				Richtung = richtung,
				Ziel = ziel.GetHashCode()
			});
		}

		private static void ProbenEinloesen(Player ziel)
		{
			if (Proben.Count == 0)
			{
				return;
			}
			float jetzt = Time.time;
			int id = ziel.GetHashCode();
			for (int i = Proben.Count - 1; i >= 0; i--)
			{
				Probe pr = Proben[i];
				if (jetzt < pr.Faellig)
				{
					continue;
				}
				if (pr.Ziel == id)
				{
					float f = Vector3.Dot(pr.Vorhergesagt - ziel.Position, pr.Richtung);
					Fehler.Add(f);
					if (Fehler.Count > 200)
					{
						Fehler.RemoveAt(0);
					}
				}
				Proben.RemoveAt(i);
			}
			if (Fehler.Count >= 5)
			{
				float summe = 0f;
				float grosse = 0f;
				for (int k = 0; k < Fehler.Count; k++)
				{
					summe += Fehler[k];
					if (Fehler[k] > 0f)
					{
						grosse += 1f;
					}
				}
				float mittel = summe / Fehler.Count * 100f;
				ProbeText = $"{mittel:+0;-0} cm ({grosse / Fehler.Count * 100f:0}% too much, n={Fehler.Count})";
			}
		}

		public static Vector3 MitVorhalt(Player ich, Player ziel, Vector3 basis)
		{
			// Nur Diagnose: Stand dieses Aufrufs festhalten (Einschlag-Aufzeichnung).
			VorhaltZiel = ziel;
			VorhaltBild = Time.frameCount;
			VorhaltBasis = basis;
			VorhaltVektor = Vector3.zero;
			VorhaltTempoGl = Vector3.zero;
			VorhaltTempoRoh = (ziel != null) ? ziel.Velocity : Vector3.zero;
			VorhaltFlugzeit = 0f;
			VorhaltFall = 0f;
			if (ich == null || ziel == null)
			{
				return basis;
			}
			float num = Vector3.Distance(ich.Position, basis);
			if (num < 1f)
			{
				return basis;
			}
			// WIE STARK GLAETTEN? Der Vorhalt rechnet Weg = Tempo x Flugzeit
			// und unterstellt damit ein GLEICHBLEIBENDES Tempo waehrend der
			// ganzen Flugzeit. Gebraucht wird also das mittlere Tempo ueber
			// dieses Fenster, nicht der Augenblickswert. Bots treten auf der
			// Stelle und zucken zur Seite - der Augenblickswert liegt dann
			// ueber dem Mittel, und der Vorhalt faellt zu gross aus.
			// Darum glaetten wir mindestens ueber die Flugzeit (hoechstens
			// 0,15 s, sonst reagiert er zu traege). Der eingestellte Wert
			// bleibt die Untergrenze und kann weiter erhoeht werden.
			float flugzeit = Flugzeit(ich, num);
			// UNTERGRENZE der Glaettung, unabhaengig von der Flugzeit.
			// Auf 35 m betraegt die Flugzeit nur 0,05 s - die Glaettung war
			// damit praktisch aus, die Vorhalt-RICHTUNG zappelte, und der
			// Zielpunkt sprang schneller, als die Nachfuehrung folgen kann.
			// Gemessen: 25-49 m hatten 0,332 Grad Muendungsfehler, 50-99 m
			// nur 0,020 - bei 13-mal hoeherer Zieldrehung im Nahbereich.
			// Der BETRAG des Vorhalts stimmt (Probe -4 cm), nur die Unruhe
			// stoerte.
			float glaettung = Mathf.Max(Einstellungen.VorhaltRuhe.Value,
										Mathf.Max(Einstellungen.VorhaltRuheMin.Value,
												  Mathf.Min(flugzeit, 0.15f)));
			Vector3 vector = Beruhigt(ziel, glaettung);
			LetztesTempo = new Vector3(vector.x, 0f, vector.z);
			VorhaltTempoGl = vector;
			VorhaltFlugzeit = flugzeit;
			float value = Einstellungen.VorhaltAbTempo.Value;
			bool flag = vector.sqrMagnitude < value * value;
			if (!Einstellungen.VorhaltAn.Value)
			{
				vector = Vector3.zero;
			}
			float fallDiag = Fallausgleich(ich, num);
			VorhaltFall = fallDiag;
			basis.y += fallDiag;
			if (flag || !Einstellungen.VorhaltAn.Value)
			{
				return basis;
			}
			float value2 = 1f - Einstellungen.VorhaltDaempfung.Value * LetzterWechsel;
			value2 = Mathf.Clamp01(value2);
			Vector3 vector2 = vector * flugzeit * Einstellungen.VorhaltFaktor.Value * value2;
			float value3 = Einstellungen.VorhaltHoechstens.Value;
			if (vector2.sqrMagnitude > value3 * value3)
			{
				vector2 = vector2.normalized * value3;
			}
			ProbenEinloesen(ziel);
			if (vector2.sqrMagnitude > 0.0001f)
			{
				// Verglichen wird Wurzel gegen Wurzel - der Kopfversatz
				// kuerzt sich dabei heraus. Die Probe misst NUR den
				// ballistischen Vorhalt; der Bildverzug unten kommt erst
				// danach dazu und wuerde die Messung sonst verfaelschen.
				ProbeMerken(ziel, ziel.Position + vector2, vector2.normalized, flugzeit);
			}

			// EIGENEN Verzug ausgleichen: der Zielpunkt entsteht am
			// Bildanfang, der Schuss faellt spaeter. Bei 43 ms und 7 Grad/s
			// Zieldrehung liegt das Ziel dann 0,3 Grad weiter - gemessen als
			// Restfehler, der sonst nicht wegzubekommen war.
			float bilder = (Einstellungen.Bildvorhalt != null) ? Einstellungen.Bildvorhalt.Value : 0f;
			if (bilder > 0.001f)
			{
				float dt = Mathf.Clamp(Time.smoothDeltaTime, 0.005f, 0.2f);
				LetzterBildvorhalt = vector.magnitude * dt * bilder;
				vector2 += vector * dt * bilder;
			}
			else
			{
				LetzterBildvorhalt = 0f;
			}
			VorhaltVektor = vector2;
			return basis + vector2;
		}

		private static Vector3 Beruhigt(Player p, float glaettung)
		{
			Vector3 velocity = p.Velocity;
			if (Einstellungen.NurWaagerecht.Value)
			{
				velocity.y = 0f;
			}
			int hashCode = p.GetHashCode();
			float time = Time.time;
			if (Verlauf.TryGetValue(hashCode, out var value) && time - value.Zeit < 1f)
			{
				float num = Mathf.Max(0.0001f, time - value.Zeit);
				float value2 = glaettung;
				float t = ((value2 <= 0.001f) ? 1f : (1f - Mathf.Exp((0f - num) / value2)));
				float b = 0f;
				if (velocity.sqrMagnitude > 0.25f && value.Tempo.sqrMagnitude > 0.25f)
				{
					float num2 = Vector3.Dot(velocity.normalized, value.Tempo.normalized);
					b = Mathf.Clamp01((1f - num2) * 0.5f);
				}
				b = (LetzterWechsel = Mathf.Lerp(value.Wechsel, b, t));
				velocity = Vector3.Lerp(value.Tempo, velocity, t);
				Verlauf[hashCode] = new Glaettung
				{
					Tempo = velocity,
					Zeit = time,
					Wechsel = b
				};
				Aufraeumen(time);
				return velocity;
			}
			LetzterWechsel = 0f;
			Verlauf[hashCode] = new Glaettung
			{
				Tempo = velocity,
				Zeit = time,
				Wechsel = 0f
			};
			Aufraeumen(time);
			return velocity;
		}

		private static void Aufraeumen(float jetzt)
		{
			if (Verlauf.Count <= 32)
			{
				return;
			}
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, Glaettung> item in Verlauf)
			{
				if (jetzt - item.Value.Zeit > 3f)
				{
					list.Add(item.Key);
				}
			}
			foreach (int item2 in list)
			{
				Verlauf.Remove(item2);
			}
		}

		internal static float Flugzeit(Player ich, float entfernung)
		{
			try
			{
				AmmoTemplate currentAmmoTemplate = Spielzugriff.Patrone(ich, out var weapon);
				if (weapon != null && currentAmmoTemplate != null && currentAmmoTemplate.InitialSpeed > 1f)
				{
					return Flugbahn.Fuer(currentAmmoTemplate, weapon.SpeedFactor).Zeit(entfernung);
				}
			}
			catch
			{
			}
			return entfernung / 850f;
		}
	}
	internal class Zielsuche
	{
		private struct Treffer
		{
			public Player Spieler;

			public Vector3 Punkt;

			public float Abstand;
		}

		private Player _gehalten;

		private float _seit;

		// Seit wann das gehaltene Ziel nicht mehr zu sehen ist (-1 = sichtbar).
		// Ein kurzes Verschwinden hinter einer Kante laesst das Ziel nicht fallen
		// (sonst springt die Hilfe zum naechsten Gegner und beim Wiederauftauchen
		// zurueck).
		private float _verlustSeit = -1f;

		private const float SichtHalteSek = 0.5f;

		public int Gesamt;

		public int RausFraktion;

		public int RausFreund;

		public int RausEntfernung;

		public int RausHinterKamera;

		public int RausKeinKnochen;

		public int RausBlickfeld;

		public int RausSicht;

		public int Uebrig;

		public float NaechsterAbstand;

		public float NaechsterWinkel;

		// Grenze, die fuer den naechsten Gegner WIRKLICH galt (Grad; -1 = Pixel-
		// Grenze), und seine Entfernung (m) - im Meter-Modus haengt sie davon ab.
		public float NaechsterGrenze = -1f;

		public float NaechsterGrenzeEntf = -1f;

		public string LetztesHindernis = "";

		public Player Aktuell => _gehalten;

		/// <summary>
		/// Diagnose (ab 1.4.4): die Grenze, die fuer den naechsten Gegner galt.
		/// "limit" davor ist nur die feste Gradangabe der alten Arten; bei Soft Aim
		/// per Maus gilt je Gegner der Fangradius aus seiner Entfernung
		/// (Einstellungen.RadiusGrad, im Meter-Modus z. B. 1 m -> 1,0 Grad auf 95 m).
		/// </summary>
		internal static string GrenzeText(float grenze, float entfernung, float winkel)
		{
			if (grenze <= 0f || winkel < 0f)
			{
				return "";
			}
			string drin = (winkel <= grenze) ? "inside" : "outside";
			return FormattableString.Invariant($" (nearest enemy: limit {grenze:0.00}° at {entfernung:0} m = {entfernung * Mathf.Tan(grenze * 0.017453292f):0.00} m, {drin})");
		}

		/// <param name="position">Spitze des Suchkegels.</param>
		/// <param name="forward">Achse des Suchkegels (normiert).</param>
		/// <param name="grenzeGrad">Oeffnung des Kegels in Grad; 0 = die Pixel-Angabe
		/// "Blickfeld in Pixeln" benutzen.</param>
		/// <param name="zonenRand">Wahr = Abstand zum RAND der naechsten
		/// Trefferzone (Kopf, Rumpf, Becken) statt zum Zielpunkt messen - dasselbe
		/// Mass, mit dem Soft Aim per Maus danach rechnet.</param>
		public bool Suchen(Player ich, Camera kamera, Vector3 position, Vector3 forward, float grenzeGrad, bool zonenRand, out Player ziel, out Vector3 zielpunkt)
		{
			ziel = null;
			zielpunkt = Vector3.zero;
			List<Player> list = Spielzugriff.Welt?.AllAlivePlayersList;
			if (list == null || kamera == null)
			{
				return false;
			}
			float num = kamera.fieldOfView / (float)Mathf.Max(1, Screen.height);
			if (num <= 0f)
			{
				num = 0.05f;
			}
			float value = grenzeGrad;
			float value2 = Einstellungen.Blickfeld.Value;
			float value3 = Einstellungen.Reichweite.Value;
			Gesamt = (RausFraktion = (RausEntfernung = (RausHinterKamera = 0)));
			RausFreund = 0;
			RausKeinKnochen = (RausBlickfeld = (RausSicht = (Uebrig = 0)));
			NaechsterAbstand = -1f;
			NaechsterWinkel = -1f;
			NaechsterGrenze = -1f;
			NaechsterGrenzeEntf = -1f;
			Treffer treffer = default(Treffer);
			treffer.Abstand = float.MaxValue;
			Treffer treffer2 = default(Treffer);
			treffer2.Abstand = float.MaxValue;
			bool flag = false;
			foreach (Player item in list)
			{
				if (item == null || item == ich || item.HealthController == null || !item.HealthController.IsAlive)
				{
					continue;
				}
				Gesamt++;
				if (Einstellungen.NurFeinde.Value && !Spielzugriff.IstFeind(item, ich))
				{
					RausFreund++;
					continue;
				}
				if ((Einstellungen.ScavsAuslassen.Value && Spielzugriff.IstScav(item)) || (Einstellungen.PmcsAuslassen.Value && Spielzugriff.IstPmc(item)) || (Einstellungen.BosseAuslassen.Value && Spielzugriff.IstBoss(item)))
				{
					RausFraktion++;
					continue;
				}
				if (Vector3.Distance(ich.Position, item.Position) > value3)
				{
					RausEntfernung++;
					continue;
				}
				Vector3 vector = Zielpunkt.Fuer(item);
				if (vector == Vector3.zero)
				{
					RausKeinKnochen++;
					continue;
				}
				Vector3 vector2 = vector - position;
				if (Vector3.Dot(forward, vector2) <= 0f)
				{
					RausHinterKamera++;
					continue;
				}
				float num2 = Vector3.Angle(forward, vector2);
				// Grenze JE GEGNER: bei Soft Aim per Maus der Fangradius aus seiner
				// Entfernung (Einstellungen.RadiusGrad), sonst die feste Grenze.
				float grenze = value;
				float grenzeEntf = vector2.magnitude;
				if (zonenRand)
				{
					grenze = Einstellungen.RadiusGrad(vector2.magnitude);
					if (Trefferzonen.KleinsterAbstand(item, position, forward, out var rand, out var zonenEntfernung))
					{
						num2 = rand;
						grenze = Einstellungen.RadiusGrad(zonenEntfernung);
						grenzeEntf = zonenEntfernung;
					}
				}
				float num3 = num2 / num;
				if (NaechsterAbstand < 0f || num3 < NaechsterAbstand)
				{
					NaechsterAbstand = num3;
					NaechsterWinkel = num2;
					NaechsterGrenze = (grenze > 0f) ? grenze : -1f;
					NaechsterGrenzeEntf = grenzeEntf;
				}
				if ((grenze > 0f) ? (num2 > grenze) : (num3 > value2))
				{
					RausBlickfeld++;
					continue;
				}
				// Erst den ZIELPUNKT pruefen, nicht den Brustkorb. Ein Sniper hinter
				// einer Bruestung zeigt nur den Kopf - sein Brustkorb ist verdeckt,
				// und mit der alten Pruefung fiel er komplett aus der Hilfe heraus,
				// obwohl der Kopf frei lag und man ihn erschiessen konnte.
				// Der Koerper wird nur noch ersatzweise geprueft: liegt der Kopf
				// hinter Deckung, der Rumpf aber frei, ist der Gegner trotzdem
				// bekaempfbar. Dank Kurzschluss-Auswertung kostet das im Normalfall
				// weiterhin nur EINEN Strahl je Gegner.
				// Ab 1.4.6 dazu das Becken: Kopf, Brust, Becken - mindestens einer
				// muss frei zu sehen sein (Buesche verdecken jetzt, siehe Laub).
				if (Einstellungen.NurSichtbare.Value
					&& !Sichtbar(position, vector, item)
					&& !Sichtbar(position, Zielpunkt.Koerper(item), item)
					&& !Sichtbar(position, Zielpunkt.Becken(item), item))
				{
					if (item != _gehalten)
					{
						RausSicht++;
						continue;
					}
					if (_verlustSeit < 0f)
					{
						_verlustSeit = Time.time;
					}
					if (Time.time - _verlustSeit > SichtHalteSek)
					{
						RausSicht++;
						continue;
					}
					// Gehaltenes Ziel nur kurz verdeckt: bleibt Kandidat.
				}
				else if (item == _gehalten)
				{
					_verlustSeit = -1f;
				}
				Uebrig++;
				Treffer treffer3 = default(Treffer);
				treffer3.Spieler = item;
				treffer3.Punkt = vector;
				treffer3.Abstand = num3;
				Treffer treffer4 = treffer3;
				if (num3 < treffer.Abstand)
				{
					treffer = treffer4;
				}
				if (item == _gehalten)
				{
					treffer2 = treffer4;
					flag = true;
				}
			}
			if (treffer.Spieler == null)
			{
				_gehalten = null;
				_verlustSeit = -1f;
				return false;
			}
			if (flag && treffer2.Abstand <= treffer.Abstand + Einstellungen.Wechselvorsprung.Value)
			{
				ziel = treffer2.Spieler;
				zielpunkt = treffer2.Punkt;
				return true;
			}
			if (_gehalten != treffer.Spieler)
			{
				_gehalten = treffer.Spieler;
				_seit = Time.time;
				_verlustSeit = -1f;
			}
			ziel = treffer.Spieler;
			zielpunkt = treffer.Punkt;
			return true;
		}

		// Feste Ablage fuer die Strahltreffer - RaycastAll wuerde bei jedem
		// Bild neuen Speicher anfordern.
		private static readonly RaycastHit[] Strahltreffer = new RaycastHit[24];

		private bool Sichtbar(Vector3 start, Vector3 punkt, Player ziel)
		{
			if (Zielpunkt.Sichtlinie(start, punkt, ziel, out var grund, out var block, out _))
			{
				LetztesHindernis = "";
				return true;
			}
			LetztesHindernis = grund;
			return false;
		}

		public void Vergessen()
		{
			_gehalten = null;
			_verlustSeit = -1f;
		}
	}

	/// <summary>
	/// Eine Trefferzone des Gegners: Huelle (achsparallele Box in der Welt) um
	/// alle Kollider einer Koerperstelle, verschoben um Vorhalt und Kugelfall,
	/// dazu der Punkt, auf den gezogen wird.
	/// </summary>
	/// <summary>
	/// Panzerung des Gegners gegen die eigene Patrone - dieselbe Rechnung wie das
	/// Spiel (Player.ProceedDamageThroughArmor -> ArmorComponent.ShotMatches,
	/// ApplyDamage -> ShotSharedMethods.RealResistance(...).GetPenetrationChance):
	///  - angelegte Panzerung: Inventory.GetPutOnArmorsNonAlloc (Helm samt Visier,
	///    Weste, Platten - alle ArmorComponents in den Panzerungsplaetzen),
	///  - eine Box ist geschuetzt, wenn ShotMatches(Box, 0) zutrifft, eine Platte,
	///    wenn ShotMatches(_, Plattentyp) zutrifft (ArmorComponent.cs),
	///  - Durchschlagchance je Teil aus Haltbarkeit, Panzerklasse und
	///    PenetrationPower der geladenen Patrone; zerstoerte Teile
	///    (Haltbarkeit unter 0,01) schuetzen nicht.
	/// Eine Brust-/Rueckenplatte wird VOR dem Koerperkollider getroffen; beide
	/// Wahrscheinlichkeiten werden multipliziert. Querschlaeger am Helm
	/// (Deflects) bleiben unberuecksichtigt - Zufall des Spiels.
	/// Je Gegner zwischengespeichert, neu bei anderem Ziel, anderer Patrone oder
	/// nach einer halben Sekunde (Haltbarkeit sinkt mit jedem Treffer).
	/// </summary>
	internal static class Panzerung
	{
		private const EArmorPlateCollider PlattenVorn = EArmorPlateCollider.Plate_Granit_SAPI_chest | EArmorPlateCollider.Plate_Korund_chest;

		private const EArmorPlateCollider PlattenHinten = EArmorPlateCollider.Plate_Granit_SAPI_back | EArmorPlateCollider.Plate_6B13_back;

		private const EArmorPlateCollider PlattenSeite = EArmorPlateCollider.Plate_Granit_SSAPI_side_left_high
			| EArmorPlateCollider.Plate_Granit_SSAPI_side_left_low | EArmorPlateCollider.Plate_Granit_SSAPI_side_right_high
			| EArmorPlateCollider.Plate_Granit_SSAPI_side_right_low | EArmorPlateCollider.Plate_Korund_side_left_high
			| EArmorPlateCollider.Plate_Korund_side_left_low | EArmorPlateCollider.Plate_Korund_side_right_high
			| EArmorPlateCollider.Plate_Korund_side_right_low;

		private static readonly List<ArmorComponent> _liste = new List<ArmorComponent>(16);

		private static int _ziel;

		private static string _munition = "";

		private static float _zeit = -99f;

		private static readonly float[] _kopf = new float[8];

		private static float _rumpfVorn, _rumpfHinten, _rumpfSeite, _beckenVorn, _beckenHinten;

		private static readonly float[] _bein = new float[4];

		private static readonly float[] _arm = new float[4];

		// Streuung der eigenen Waffe (Grad, Radius) und Schaden der Patrone.
		private static float _streuung;

		private static float _schaden = 50f;

		public static float Streuung => _streuung;

		/// <summary>Trefferwahrscheinlichkeit je Zone fuer die gewaehlte Zone (Kopf, Rumpf, Becken, Beine, Arme).</summary>
		public static readonly float[] LetzteP = new float[5];

		private static float _pen;

		internal static readonly EBodyPartColliderType[] KopfReihenfolge = new EBodyPartColliderType[8];

		private static readonly int[] _index = new int[8];

		private static float Durch(EBodyPartColliderType typ, EArmorPlateCollider platte)
		{
			return Durch(_liste, _pen, typ, platte);
		}

		private static float Durch(List<ArmorComponent> liste, float pen, EBodyPartColliderType typ, EArmorPlateCollider platte)
		{
			float p = 1f;
			for (int i = 0; i < liste.Count; i++)
			{
				ArmorComponent a = liste[i];
				if (a == null || a.IsDestroyed || !a.ShotMatches(typ, platte))
				{
					continue;
				}
				float c = ShotSharedMethods.RealResistance(a.Repairable.Durability, a.Repairable.TemplateDurability, a.ArmorClass, pen)
					.GetPenetrationChance(pen);
				p *= Mathf.Clamp01(c / 100f);
			}
			return p;
		}

		private static float Platten(EArmorPlateCollider maske)
		{
			return Platten(_liste, _pen, maske);
		}

		private static float Platten(List<ArmorComponent> liste, float pen, EArmorPlateCollider maske)
		{
			// Nur EINE Platte sitzt an der Stelle; die schwaechste vorhandene zaehlt.
			float p = 1f;
			for (int bit = 1; bit <= 0x800; bit <<= 1)
			{
				EArmorPlateCollider e = (EArmorPlateCollider)bit;
				if ((maske & e) != 0)
				{
					float q = Durch(liste, pen, EBodyPartColliderType.None, e);
					if (q < 1f)
					{
						p = (p < 1f) ? Mathf.Max(p, q) : q;
					}
				}
			}
			return p;
		}

		private static void Bewerten(Player ich, Player ziel)
		{
			AmmoTemplate patrone = null;
			try
			{
				patrone = Spielzugriff.Patrone(ich, out _);
			}
			catch
			{
			}
			string mun = patrone?._id ?? "";
			int id = ziel.GetHashCode();
			if (id == _ziel && mun == _munition && Time.time - _zeit < 0.5f)
			{
				return;
			}
			_ziel = id;
			_munition = mun;
			_zeit = Time.time;
			_pen = (patrone != null) ? patrone.PenetrationPower : 30f;
			_liste.Clear();
			ziel.Inventory.GetPutOnArmorsNonAlloc(_liste);
			EBodyPartColliderType[] boxen = Zielpunkt.AlleKopfboxen;
			for (int i = 0; i < boxen.Length; i++)
			{
				_kopf[i] = Durch(boxen[i], (EArmorPlateCollider)0);
				_index[i] = i;
			}
			// Stabil absteigend nach Durchschlagchance; Gleichstand behaelt die
			// bisherige Vorliebe (AlleKopfboxen).
			for (int i = 1; i < boxen.Length; i++)
			{
				int j = i;
				while (j > 0 && _kopf[_index[j]] > _kopf[_index[j - 1]])
				{
					int t = _index[j];
					_index[j] = _index[j - 1];
					_index[j - 1] = t;
					j--;
				}
			}
			for (int i = 0; i < boxen.Length; i++)
			{
				KopfReihenfolge[i] = boxen[_index[i]];
			}
			_rumpfVorn = Durch(EBodyPartColliderType.RibcageUp, (EArmorPlateCollider)0) * Platten(PlattenVorn);
			_rumpfHinten = Durch(EBodyPartColliderType.SpineTop, (EArmorPlateCollider)0) * Platten(PlattenHinten);
			_rumpfSeite = Durch(EBodyPartColliderType.RightSideChestUp, (EArmorPlateCollider)0) * Platten(PlattenSeite);
			_beckenVorn = Durch(EBodyPartColliderType.Pelvis, (EArmorPlateCollider)0);
			_beckenHinten = Durch(EBodyPartColliderType.PelvisBack, (EArmorPlateCollider)0);
			for (int i = 0; i < 4; i++)
			{
				_bein[i] = Durch(Trefferzonen.Beine[i], (EArmorPlateCollider)0);
				_arm[i] = Durch(Trefferzonen.Arme[i], (EArmorPlateCollider)0);
			}
			_schaden = (patrone != null) ? Mathf.Max(1f, patrone.Damage) : 50f;
			_streuung = StreuungBerechnen(ich.HandsController?.Item as Weapon, patrone);
		}

		/// <summary>
		/// Streuung als Winkel-Radius (Grad). Beleg Player.FirearmController.Shot:
		///   Richtung x 100 + (CenterOfImpact) x AmmoFactor x Ueberhitzung x
		///   GetBarrelDeviation() x WeaponSpread x Zufallspunkt in der Einheitskugel
		/// mit CenterOfImpact = Weapon.GetTotalCenterOfImpact(false); der Winkel ist
		/// atan(Betrag / 100). Ueberhitzung und Double-Action-Zuschlag bleiben weg.
		/// Geprueft 03.11. (Player.cs FirearmController.Shot, Weapon.cs 1088-1112):
		/// Shot nimmt (float_3 + num3) * ammoFactor * Ueberhitzung *
		/// GetBarrelDeviation(weapon) * WeaponSpread; GetBarrelDeviation() enthaelt
		/// Buff.WeaponSpread schon einmal, Shot multipliziert ihn ein zweites Mal
		/// (0 -> 1). Ohne Reparatur-Buff ist er 1, beides faellt weg. float_3 =
		/// GetTotalCenterOfImpact(false) = CoI des Laufs * (1 - Summe Accuracy/100).
		/// AmmoFactor: ammoAccr <= 0 -> (100 + |ammoAccr|) / 100, sonst 100/(100+ammoAccr).
		/// </summary>
		private static Weapon _streuWaffe;

		private static string _streuPatrone = "";

		private static float _streuZeit = -99f;

		/// <summary>
		/// Streuung unabhaengig von der Zonenwahl auffrischen. Bis 03.11. setzte
		/// NUR Bewerten() den Wert - und das laeuft nur bei "Zone nach Panzerung
		/// waehlen" = an. Bei Kaan steht das auf aus, darum meldete die
		/// Aufzeichnung immer streuung 0 (Raid Customs 03.11.: 0 in jeder Zeile,
		/// echte Abweichung kugel.dir gegen schuss.sd dagegen 0,003-0,024 Grad).
		/// Gleiche Formel wie Bewerten (StreuungBerechnen), also keine Aenderung
		/// der Zonenwahl. Hoechstens 1x je s oder bei Waffen-/Patronenwechsel
		/// (GetTotalCenterOfImpact laeuft ueber LINQ).
		/// </summary>
		internal static void StreuungAuffrischen(Player ich)
		{
			Weapon w = ich?.HandsController?.Item as Weapon;
			AmmoTemplate patrone = null;
			try
			{
				patrone = Spielzugriff.Patrone(ich, out _);
			}
			catch
			{
			}
			string mun = patrone?._id ?? "";
			float jetzt = Time.time;
			if ((object)w == _streuWaffe && string.Equals(mun, _streuPatrone) && jetzt - _streuZeit < 1f)
			{
				return;
			}
			_streuWaffe = w;
			_streuPatrone = mun;
			_streuZeit = jetzt;
			_streuung = StreuungBerechnen(w, patrone);
		}

		private static float StreuungBerechnen(Weapon w, AmmoTemplate patrone)
		{
			if (w == null)
			{
				return 0f;
			}
			float coi = w.GetTotalCenterOfImpact(includeAmmo: false) * ((patrone != null) ? patrone.AmmoFactor : 1f) * w.GetBarrelDeviation();
			double spread = 1.0;
			try
			{
				// Dieselbe Komponente, die Shot() ueber GetItemComponent<BuffComponent>() holt.
				BuffComponent b = w.Buff;
				if (b != null && Math.Abs(b.WeaponSpread) > 1E-09)
				{
					spread = b.WeaponSpread;
				}
			}
			catch
			{
			}
			return Mathf.Atan(coi * (float)spread / 100f) * 57.29578f;
		}

		private static readonly List<ArmorComponent> _fremd = new List<ArmorComponent>(16);

		/// <summary>
		/// Durchschlag je Zone fuer einen beliebigen Gegner (fuer die Aufzeichnung),
		/// ohne den Zwischenspeicher der Zonenwahl anzufassen. werte: 8 Kopfboxen
		/// (Reihenfolge Zielpunkt.AlleKopfboxen), dann Rumpf vorn, hinten, seitlich,
		/// Becken vorn, hinten - je 0..1.
		/// </summary>
		internal static void Auswerten(Player ziel, float pen, float[] werte)
		{
			_fremd.Clear();
			ziel.Inventory.GetPutOnArmorsNonAlloc(_fremd);
			EBodyPartColliderType[] boxen = Zielpunkt.AlleKopfboxen;
			for (int i = 0; i < boxen.Length; i++)
			{
				werte[i] = Durch(_fremd, pen, boxen[i], (EArmorPlateCollider)0);
			}
			werte[8] = Durch(_fremd, pen, EBodyPartColliderType.RibcageUp, (EArmorPlateCollider)0) * Platten(_fremd, pen, PlattenVorn);
			werte[9] = Durch(_fremd, pen, EBodyPartColliderType.SpineTop, (EArmorPlateCollider)0) * Platten(_fremd, pen, PlattenHinten);
			werte[10] = Durch(_fremd, pen, EBodyPartColliderType.RightSideChestUp, (EArmorPlateCollider)0) * Platten(_fremd, pen, PlattenSeite);
			werte[11] = Durch(_fremd, pen, EBodyPartColliderType.Pelvis, (EArmorPlateCollider)0);
			werte[12] = Durch(_fremd, pen, EBodyPartColliderType.PelvisBack, (EArmorPlateCollider)0);
			_fremd.Clear();
		}

		/// <summary>Schlaegt die eigene Patrone diese Kopfbox ueberhaupt durch?</summary>
		internal static bool Durchschlagbar(EBodyPartColliderType typ)
		{
			EBodyPartColliderType[] boxen = Zielpunkt.AlleKopfboxen;
			for (int i = 0; i < boxen.Length; i++)
			{
				if (boxen[i] == typ)
				{
					return _kopf[i] > 0f;
				}
			}
			return true;
		}

		private static float KopfDurch(string boxname)
		{
			string n = boxname;
			int klammer = n.IndexOf('(');
			if (klammer >= 0)
			{
				n = n.Substring(0, klammer);
			}
			EBodyPartColliderType[] boxen = Zielpunkt.AlleKopfboxen;
			for (int i = 0; i < boxen.Length; i++)
			{
				if (boxen[i].ToString() == n)
				{
					return _kopf[i];
				}
			}
			return 1f;
		}

		// Fester Stichprobensatz: Projektion einer Gleichverteilung in der
		// Einheitskugel (wie Random.insideUnitSphere in Shot) und Normalverteilung.
		// Fest, damit die Wahl nicht von Bild zu Bild zittert.
		private const int Proben = 64;

		private static readonly Vector2[] _kugel = new Vector2[Proben];

		private static readonly Vector2[] _gauss = new Vector2[Proben];

		static Panzerung()
		{
			System.Random r = new System.Random(20261102);
			for (int i = 0; i < Proben; i++)
			{
				float x, y, z;
				do
				{
					x = (float)(r.NextDouble() * 2.0 - 1.0);
					y = (float)(r.NextDouble() * 2.0 - 1.0);
					z = (float)(r.NextDouble() * 2.0 - 1.0);
				}
				while (x * x + y * y + z * z > 1f);
				_kugel[i] = new Vector2(x, y);
				double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
				double m = Math.Sqrt(-2.0 * Math.Log(u1));
				_gauss[i] = new Vector2((float)(m * Math.Cos(2 * Math.PI * u2)), (float)(m * Math.Sin(2 * Math.PI * u2)));
			}
		}

		// Formen aller Trefferkollider des Gegners (Kopf 8, Rumpf 8, Becken 2,
		// Beine 4, Arme 4) mit Wert eines Treffers (Durchschlag x Gewicht).
		private const int MaxKollider = 26;

		private static readonly Vector3[] _fm = new Vector3[MaxKollider], _fa1 = new Vector3[MaxKollider], _fa2 = new Vector3[MaxKollider], _fa3 = new Vector3[MaxKollider];

		private static readonly float[] _fwert = new float[MaxKollider];

		private static readonly int[] _fzone = new int[MaxKollider];

		private static int _fanz;

		private static readonly Flaeche[] _fp = new Flaeche[MaxKollider];

		private static readonly bool[] _fok = new bool[MaxKollider];

		private static readonly float[] _ftiefe = new float[MaxKollider];

		private static void FormenSammeln(Player ziel, float rumpf, float becken)
		{
			_fanz = 0;
			Dictionary<EBodyPartColliderType, BodyPartCollider> karte = ziel.PlayerBones?.BodyPartCollidersDictionary;
			if (karte == null)
			{
				return;
			}
			// Gewicht eines durchschlagenden Treffers = Kehrwert der Treffer bis
			// zum Kill ueber diese Zone. Kopf (35 TP): ein Treffer ab 35 Schaden ->
			// min(1, Schaden/35). Brustkorb (85 TP): 1/ceil(85/Schaden), bei 52
			// Schaden 2 Treffer -> 0,5. Bauch, Beine, Arme toeten nicht selbst, nur
			// ueber den Schaden-Uebertrag zerstoerter Teile: grob 10 Treffer -> 0,1.
			// (Vorher halbes Gewicht ~0,4: damit gewannen auf 30 m die Beine mit
			// ihrer hohen Trefferchance gegen den Kopf - Raid 02.11., Gefecht 006.)
			float wKopf = Mathf.Min(1f, _schaden / 35f);
			float wRumpf = 1f / Mathf.Max(1f, Mathf.Ceil(85f / Mathf.Max(1f, _schaden)));
			float wBecken = 0.1f;
			float wBein = 0.1f;
			float wArm = 0.1f;
			EBodyPartColliderType[] kopf = Zielpunkt.AlleKopfboxen;
			for (int i = 0; i < kopf.Length; i++)
			{
				FormDazu(karte, kopf[i], 0, _kopf[i] * wKopf);
			}
			for (int i = 0; i < Trefferzonen.Rumpf.Length; i++)
			{
				FormDazu(karte, Trefferzonen.Rumpf[i], 1, rumpf * wRumpf);
			}
			for (int i = 0; i < Trefferzonen.Becken.Length; i++)
			{
				FormDazu(karte, Trefferzonen.Becken[i], 2, becken * wBecken);
			}
			for (int i = 0; i < 4; i++)
			{
				FormDazu(karte, Trefferzonen.Beine[i], 3, _bein[i] * wBein);
				FormDazu(karte, Trefferzonen.Arme[i], 4, _arm[i] * wArm);
			}
		}

		private static void FormDazu(Dictionary<EBodyPartColliderType, BodyPartCollider> karte, EBodyPartColliderType typ, int zone, float wert)
		{
			if (_fanz >= MaxKollider || !karte.TryGetValue(typ, out var box) || box == null)
			{
				return;
			}
			if (!Trefferzonen.Form(box.Collider, out _fm[_fanz], out _fa1[_fanz], out _fa2[_fanz], out _fa3[_fanz]))
			{
				return;
			}
			_fwert[_fanz] = wert;
			_fzone[_fanz] = zone;
			_fanz++;
		}

		/// <summary>
		/// Erwarteter Wert eines Schusses, der auf punkt zielt: Stichproben der
		/// Abweichung (Streuung der Waffe als Kugel-Gleichverteilung + gemessene
		/// Zielunruhe als Normalverteilung) in der Bildebene; je Stichprobe zaehlt
		/// der VORDERSTE getroffene Kollider mit seinem Wert. p = Trefferanteil je
		/// Zone (Kopf, Rumpf, Becken, Beine, Arme).
		/// </summary>
		private static float Erwartung(Vector3 start, Vector3 punkt, float[] p)
		{
			for (int z = 0; z < 5; z++)
			{
				p[z] = 0f;
			}
			Vector3 zu = punkt - start;
			if (zu.sqrMagnitude < 1E-04f || _fanz == 0)
			{
				return 0f;
			}
			Vector3 richtung = zu.normalized;
			float cos = Mathf.Max(0.1f, Mathf.Cos(Rueckstoss.Nicken(richtung) * 0.017453292f));
			for (int j = 0; j < _fanz; j++)
			{
				_fok[j] = Trefferzonen.ProjizierenForm(_fm[j], _fa1[j], _fa2[j], _fa3[j], start, richtung, cos, out _fp[j]);
				_ftiefe[j] = (_fm[j] - start).sqrMagnitude;
			}
			float rs = _streuung;
			float sigma = SoftAim.Unruhe;
			float wert = 0f;
			for (int i = 0; i < Proben; i++)
			{
				Vector2 o = _kugel[i] * rs + _gauss[i] * sigma;
				int best = -1;
				float tiefe = float.MaxValue;
				for (int j = 0; j < _fanz; j++)
				{
					if (!_fok[j] || _ftiefe[j] >= tiefe)
					{
						continue;
					}
					Trefferzonen.RandFlaeche(ref _fp[j], o, out var q);
					if (q <= 1f)
					{
						best = j;
						tiefe = _ftiefe[j];
					}
				}
				if (best >= 0)
				{
					wert += _fwert[best];
					p[_fzone[best]] += 1f;
				}
			}
			for (int z = 0; z < 5; z++)
			{
				p[z] /= Proben;
			}
			return wert / Proben;
		}

		private static readonly float[] _pTmp = new float[5];

		/// <summary>
		/// Waehlt die Zone mit dem hoechsten ERWARTETEN Treffer: fuer jede frei
		/// sichtbare Zone (Kopf: freie Kopfbox in der Reihenfolge der Panzerung;
		/// Rumpf, Becken, Beine: Sichtlinie vom Feuerpunkt) wird der Erwartungswert
		/// eines Schusses dorthin gerechnet (Erwartung) - einschliesslich der
		/// Treffer, die bei Streuung in NACHBARZONEN landen. Auf Entfernung, wenn die
		/// Streuung den Kopf weit uebersteigt, gewinnt so z. B. der ungepanzerte
		/// Rumpf oder die Beine statt des Kopfs; eine Brustplatte, die die Patrone
		/// nicht durchschlaegt, zaehlt dabei 0.
		/// </summary>
		internal static Knochen Waehlen(Player ich, Player ziel, Camera kamera, out Vector3 punkt, out bool kopfZu)
		{
			punkt = Zielpunkt.Koerper(ziel);
			kopfZu = true;
			try
			{
				Bewerten(ich, ziel);
			}
			catch (Exception ex)
			{
				SoftAim.ZonenGrund = "armor not readable (" + ex.GetType().Name + ")";
				for (int i = 0; i < 8; i++)
				{
					KopfReihenfolge[i] = Zielpunkt.AlleKopfboxen[i];
					_kopf[i] = 1f;
				}
				_rumpfVorn = _rumpfHinten = _rumpfSeite = _beckenVorn = _beckenHinten = 1f;
				for (int i = 0; i < 4; i++)
				{
					_bein[i] = _arm[i] = 1f;
				}
			}
			// Blickrichtung des Gegners: von vorn, von hinten oder von der Seite?
			Vector3 zuMir = ich.Position - ziel.Position;
			zuMir.y = 0f;
			Vector3 vorn = Quaternion.Euler(0f, ziel.Rotation.x, 0f) * Vector3.forward;
			float cosB = (zuMir.sqrMagnitude > 1E-04f) ? Vector3.Dot(vorn, zuMir.normalized) : 1f;
			string seite;
			float rumpf, becken;
			if (cosB > 0.5f)
			{
				seite = "front";
				rumpf = _rumpfVorn;
				becken = _beckenVorn;
			}
			else if (cosB < -0.5f)
			{
				seite = "back";
				rumpf = _rumpfHinten;
				becken = _beckenHinten;
			}
			else
			{
				seite = "side";
				rumpf = _rumpfSeite;
				becken = Mathf.Max(_beckenVorn, _beckenHinten);
			}
			Vector3 start = Zielpunkt.SichtStart(ich, kamera);
			FormenSammeln(ziel, rumpf, becken);

			// Kandidaten (Kopf, Rumpf, Becken, Beine) und ihre Sicht.
			// Kopfbox halten nur, wenn sie nicht deutlich schlechter gepanzert ist als
			// die beste (gleiche 20 %-Regel wie bei der Zonenwahl).
			bool halten = true;
			if (Zielpunkt.GehalteneBox(ziel, out var gehalten))
			{
				float bestK = 0f, diese = 0f;
				for (int i = 0; i < 8; i++)
				{
					bestK = Mathf.Max(bestK, _kopf[i]);
					if (Zielpunkt.AlleKopfboxen[i] == gehalten)
					{
						diese = _kopf[i];
					}
				}
				halten = diese * (1f + Vorsprung) >= bestK;
			}
			bool kopfFrei = Zielpunkt.SichtbareBox(ziel, kamera, out var boxmitte, out var boxname, KopfReihenfolge, halten)
							&& !boxname.EndsWith("(covered)");
			Vector3 rumpfPunkt = Zielpunkt.Koerper(ziel);
			Transform pb = ziel.PlayerBones?.Pelvis?.Original;
			Vector3 beckenPunkt = (pb != null) ? pb.position : ziel.Position;
			Vector3 beinPunkt = Trefferzonen.Huelle(ziel, Trefferzonen.Oberschenkel, out var bh) ? bh.center : ziel.Position;
			bool rumpfFrei = Zielpunkt.Sichtlinie(start, rumpfPunkt, ziel, out _, out _, out _);
			bool beckenFrei = Zielpunkt.Sichtlinie(start, beckenPunkt, ziel, out _, out _, out _);
			bool beinFrei = Zielpunkt.Sichtlinie(start, beinPunkt, ziel, out _, out _, out _);

			float eKopf = kopfFrei ? Erwartung(start, boxmitte, _pTmp) : -1f;
			float pKopfKopf = _pTmp[0];
			float eRumpf = rumpfFrei ? Erwartung(start, rumpfPunkt, _pTmp) : -1f;
			float eBecken = beckenFrei ? Erwartung(start, beckenPunkt, _pTmp) : -1f;
			float eBein = beinFrei ? Erwartung(start, beinPunkt, _pTmp) : -1f;

			// Erlaubte Zonen. Becken/Beine gesperrt -> nur als echter Ausweich, wenn
			// Kopf UND Rumpf nicht moeglich sind (verdeckt, gesperrt oder von der
			// Patrone nicht durchschlagbar); dann zuerst Becken, dann Beine.
			float dKopf = kopfFrei ? KopfDurch(boxname) : 0f;
			float dBein = Mathf.Max(Mathf.Max(_bein[0], _bein[1]), Mathf.Max(_bein[2], _bein[3]));
			bool kopfMoeglich = Einstellungen.Erlaubt(Knochen.Head) && eKopf >= 0f && dKopf > 0f;
			bool rumpfMoeglich = Einstellungen.Erlaubt(Knochen.Chest) && eRumpf >= 0f && rumpf > 0f;
			bool ausweich = !kopfMoeglich && !rumpfMoeglich;
			bool beckenMoeglich = eBecken >= 0f && becken > 0f;
			if (!Einstellungen.Erlaubt(Knochen.Head))
			{
				eKopf = -1f;
			}
			if (!Einstellungen.Erlaubt(Knochen.Chest))
			{
				eRumpf = -1f;
			}
			if (!Einstellungen.Erlaubt(Knochen.Pelvis) && !(ausweich && beckenMoeglich))
			{
				eBecken = -1f;
			}
			if (!Einstellungen.Erlaubt(Knochen.Legs) && !(ausweich && !beckenMoeglich && eBein >= 0f && dBein > 0f))
			{
				eBein = -1f;
			}
			AusweichAktiv = ausweich;
			Knochen wahl = Bestaendig(ziel, eKopf, eRumpf, eBecken, eBein);
			switch (wahl)
			{
			case Knochen.Head:
				punkt = boxmitte;
				break;
			case Knochen.Pelvis:
				punkt = beckenPunkt;
				break;
			case Knochen.Legs:
				punkt = beinPunkt;
				break;
			default:
				punkt = rumpfPunkt;
				break;
			}
			kopfZu = !kopfFrei;
			// Trefferanteile fuer die gewaehlte Zone festhalten (Diagnose).
			Erwartung(start, punkt, LetzteP);
			string grund = $"{wahl}" + (wahl == Knochen.Head ? $"/{boxname}" : "")
				+ $" [expectation head {Text(eKopf)}, torso {Text(eRumpf)}, pelvis {Text(eBecken)}, legs {Text(eBein)}; "
				+ $"smoothed {Text(_glatt[0])}/{Text(_glatt[1])}/{Text(_glatt[2])}/{Text(_glatt[3])}"
				+ ((_kand >= 0) ? $", switch to {(Knochen)_kand} in {Mathf.Max(0f, Mindestzeit - (Time.time - _kandSeit)):0.00} s" : "")
				+ (AusweichAktiv ? ", FALLBACK (head and torso not possible)" : "") + "; "
				+ $"P(head/torso/pelvis/legs/arms) {LetzteP[0] * 100f:0}/{LetzteP[1] * 100f:0}/{LetzteP[2] * 100f:0}/{LetzteP[3] * 100f:0}/{LetzteP[4] * 100f:0} %; "
				+ $"penetration head {(kopfFrei ? $"{KopfDurch(boxname) * 100f:0}%" : "covered")}, torso {seite} {rumpf * 100f:0}%, pelvis {becken * 100f:0}%; "
				+ $"spread {_streuung:0.000}°, unrest {SoftAim.Unruhe:0.000}°, pen {_pen:0}, damage {_schaden:0}]";
			SoftAim.ZonenGrund = grund;
			Zielpunkt.LetzteWahl = grund;
			return wahl;
		}

		// --- Bestaendigkeit der Zonenwahl -------------------------------------
		// Erwartungswerte je Zone werden geglaettet (0,3 s); die aktuelle Zone
		// bleibt, bis eine andere um VORSPRUNG besser ist UND das MINDESTZEIT lang
		// bleibt. Begruendung aus der Aufzeichnung 02.11. (166 m): die Werte lagen
		// dicht beieinander (Becken 0,21 / Rumpf 0,16-0,25), sprangen von Bild zu
		// Bild um bis 0,04 (= 17 % von 0,24) und der feste Stichprobensatz hat eine
		// Aufloesung von 1/64 je Treffer (rund 8 % von 0,2). 20 % liegt darueber;
		// 0,5 s sind bei 800 Schuss/min rund 6 Schuesse auf denselben Punkt.
		// Nachgespielt: 13,9 Wechsel/s -> 0,06/s (Glaettung allein: 0,5/s).
		// Wird die aktuelle Zone verdeckt, wird sofort gewechselt.
		private const float Glaettung = 0.3f;

		private const float Vorsprung = 0.2f;

		private const float Mindestzeit = 0.5f;

		private static readonly float[] _glatt = { -1f, -1f, -1f, -1f };

		private static Player _wahlZiel;

		private static int _akt = -1;

		private static int _kand = -1;

		private static float _kandSeit;

		private static readonly float[] _roh = new float[4];

		private static Knochen Bestaendig(Player ziel, float eKopf, float eRumpf, float eBecken, float eBein)
		{
			_roh[0] = eKopf;
			_roh[1] = eRumpf;
			_roh[2] = eBecken;
			_roh[3] = eBein;
			bool neu = (object)ziel != _wahlZiel || _akt < 0;
			float t = 1f - Mathf.Exp((0f - Time.deltaTime) / Glaettung);
			for (int i = 0; i < 4; i++)
			{
				if (_roh[i] < 0f)
				{
					_glatt[i] = -1f;   // verdeckt: sofort raus
				}
				else if (neu || _glatt[i] < 0f)
				{
					_glatt[i] = _roh[i];
				}
				else
				{
					_glatt[i] += (_roh[i] - _glatt[i]) * t;
				}
			}
			int best = 1;
			for (int i = 0; i < 4; i++)
			{
				if (_glatt[i] > _glatt[best])
				{
					best = i;
				}
			}
			if (neu)
			{
				_wahlZiel = ziel;
				_akt = best;
				_kand = -1;
			}
			else if (_glatt[_akt] < 0f)
			{
				_akt = best;
				_kand = -1;
			}
			else if (best != _akt && _glatt[best] >= _glatt[_akt] * (1f + Vorsprung) && _glatt[best] > _glatt[_akt])
			{
				// Die Zeit laeuft, solange IRGENDEINE andere Zone den Vorsprung hat -
				// wechseln sich zwei bessere Zonen als "beste" ab, beginnt sie nicht
				// jedes Mal neu. Gewechselt wird zur dann besten.
				if (_kand < 0)
				{
					_kandSeit = Time.time;
				}
				_kand = best;
				if (Time.time - _kandSeit >= Mindestzeit)
				{
					_akt = best;
					_kand = -1;
				}
			}
			else
			{
				_kand = -1;
			}
			return (Knochen)_akt;
		}

		/// <summary>Kopf und Rumpf nicht moeglich -> Becken/Beine als Ausweich zugelassen.</summary>
		public static bool AusweichAktiv;

		private static string Text(float e)
		{
			return (e < 0f) ? "covered" : e.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
		}
	}

	internal struct Zone
	{
		public string Name;

		public Vector3 Mitte;

		public Vector3 Halb;

		public Vector3 Ziel;

		// Die echten Trefferkollider der Zone (Puffer, Anzahl gueltig) und der
		// Vorhalt-/Kugelfall-Versatz, um den sie verschoben gemessen werden.
		public Collider[] Kollider;

		public int Anzahl;

		public Vector3 Versatz;
	}

	/// <summary>
	/// Ein Trefferkollider als Flaeche in der Bildebene (Grad): Mitte M und die
	/// Ellipse, in die sein EINBESCHRIEBENES Ellipsoid projiziert wird (inverse
	/// Formmatrix A, B, C). Einbeschrieben heisst: liegt ein Punkt in der
	/// Ellipse, liegt die Schusslinie sicher auf dem Kollider - "Rand 0" wird
	/// nie behauptet, wenn der Strahl daneben geht. Halb = kleinste Halbachse.
	/// </summary>
	internal struct Flaeche
	{
		public Vector2 M;

		public float A;

		public float B;

		public float C;

		public float Halb;
	}

	/// <summary>
	/// Die Trefferzonen eines Gegners und ihr Winkelabstand zur Schusslinie.
	///
	/// Bisher wurde der Abstand zu EINEM Punkt gemessen (der Kopfbox). Auf 3 m
	/// liegt der Kopf aber 8,5 Grad ueber der Brust - wer auf den Koerper zielt,
	/// war damit weit ausserhalb jedes Radius, obwohl er direkt vor der
	/// Trefferzone stand. Gemessen wird jetzt der Abstand zum RAND der Zone,
	/// im Winkel - zu den ECHTEN Trefferkollidern der Zone (Projizieren /
	/// RandFlaeche), nicht zu einer Huelle um alle: die Huelle der acht Kopf- und
	/// Halsboxen war auf Mitteldistanz deutlich groesser als die Trefferflaeche
	/// (Raid 02.11.: 12 von 25 Fehlschuessen auf 28-42 m meldeten "Rand 0").
	/// Die Huelle dient nur noch fuer Entfernung und Rueckfall-Zielpunkt.
	/// </summary>
	internal static class Trefferzonen
	{
		internal static readonly EBodyPartColliderType[] Kopf =
		{
			EBodyPartColliderType.HeadCommon, EBodyPartColliderType.ParietalHead,
			EBodyPartColliderType.BackHead, EBodyPartColliderType.Ears,
			EBodyPartColliderType.Eyes, EBodyPartColliderType.Jaw,
			EBodyPartColliderType.NeckFront, EBodyPartColliderType.NeckBack
		};

		internal static readonly EBodyPartColliderType[] Rumpf =
		{
			EBodyPartColliderType.RibcageUp, EBodyPartColliderType.RibcageLow,
			EBodyPartColliderType.SpineTop, EBodyPartColliderType.SpineDown,
			EBodyPartColliderType.RightSideChestUp, EBodyPartColliderType.LeftSideChestUp,
			EBodyPartColliderType.RightSideChestDown, EBodyPartColliderType.LeftSideChestDown
		};

		internal static readonly EBodyPartColliderType[] Becken =
		{
			EBodyPartColliderType.Pelvis, EBodyPartColliderType.PelvisBack
		};

		internal static readonly EBodyPartColliderType[] Beine =
		{
			EBodyPartColliderType.LeftThigh, EBodyPartColliderType.RightThigh,
			EBodyPartColliderType.LeftCalf, EBodyPartColliderType.RightCalf
		};

		internal static readonly EBodyPartColliderType[] Oberschenkel =
		{
			EBodyPartColliderType.LeftThigh, EBodyPartColliderType.RightThigh
		};

		internal static readonly EBodyPartColliderType[] Arme =
		{
			EBodyPartColliderType.LeftUpperArm, EBodyPartColliderType.RightUpperArm,
			EBodyPartColliderType.LeftForearm, EBodyPartColliderType.RightForearm
		};

		/// <summary>Kollidertypen je Koerperstelle (Zonen, auf die gezogen werden kann).</summary>
		internal static EBodyPartColliderType[] Typen(Knochen k)
		{
			switch (k)
			{
			case Knochen.Head:
				return Kopf;
			case Knochen.Chest:
				return Rumpf;
			case Knochen.Pelvis:
				return Becken;
			default:
				return Beine;
			}
		}

		private static readonly Collider[] _suchPuffer = new Collider[8];

		/// <summary>Huelle um alle Kollider der genannten Typen.</summary>
		internal static bool Huelle(Player p, EBodyPartColliderType[] typen, out Bounds huelle)
		{
			huelle = default(Bounds);
			Dictionary<EBodyPartColliderType, BodyPartCollider> karte = p?.PlayerBones?.BodyPartCollidersDictionary;
			if (karte == null)
			{
				return false;
			}
			bool hat = false;
			Vector3 ort = p.Position;
			for (int i = 0; i < typen.Length; i++)
			{
				if (!karte.TryGetValue(typen[i], out var box) || box == null || box.Collider == null)
				{
					continue;
				}
				Bounds b = box.Collider.bounds;
				// Unbrauchbare Huelle (Kollider ohne Ausdehnung oder weit weg
				// vom Spieler) nicht mitnehmen.
				if (b.extents.sqrMagnitude < 1E-08f || (b.center - ort).sqrMagnitude > 9f)
				{
					continue;
				}
				if (!hat)
				{
					huelle = b;
					hat = true;
				}
				else
				{
					huelle.Encapsulate(b);
				}
			}
			return hat;
		}

		internal static bool Bilden(Player p, string name, EBodyPartColliderType[] typen, Vector3 versatz,
			Collider[] puffer, bool nurDurchschlagbar, out Zone zone)
		{
			zone = default(Zone);
			if (!Huelle(p, typen, out var b))
			{
				return false;
			}
			zone.Name = name;
			zone.Mitte = b.center + versatz;
			zone.Halb = b.extents;
			zone.Ziel = zone.Mitte;
			zone.Versatz = versatz;
			zone.Kollider = puffer;
			Dictionary<EBodyPartColliderType, BodyPartCollider> karte = p.PlayerBones.BodyPartCollidersDictionary;
			Vector3 ort = p.Position;
			for (int i = 0; i < typen.Length && zone.Anzahl < puffer.Length; i++)
			{
				if (!karte.TryGetValue(typen[i], out var box) || box == null || box.Collider == null)
				{
					continue;
				}
				if ((box.Collider.bounds.center - ort).sqrMagnitude > 9f)
				{
					continue;
				}
				// Bei "Zone nach Panzerung": Kopfboxen, die die eigene Patrone
				// nicht durchschlaegt (Chance 0), sind keine Trefferflaeche.
				if (nurDurchschlagbar && !Panzerung.Durchschlagbar(typen[i]))
				{
					continue;
				}
				puffer[zone.Anzahl++] = box.Collider;
			}
			return zone.Anzahl > 0;
		}

		/// <summary>
		/// Projiziert einen Trefferkollider in die Bildebene um die Schusslinie.
		/// Box-, Kapsel- und Kugelkollider ueber ihre eigene Form (einbeschriebenes
		/// Ellipsoid: Halbmasse der Box, bei der Kapsel Radius und halbe Hoehe),
		/// die Halbachsen per Transform.TransformVector - das gilt fuer jede
		/// Drehung und Skalierung des Knochens. Andere Kollider ueber ihre Bounds.
		/// Die Form wird nicht ueber Physik abgefragt, also auch bei
		/// abgeschalteten Kollidern richtig.
		/// </summary>
		internal static bool Projizieren(Collider c, Vector3 versatz, Vector3 start, Vector3 richtung, float cos, out Flaeche f)
		{
			f = default(Flaeche);
			if (!Form(c, out var mitte, out var a1, out var a2, out var a3))
			{
				return false;
			}
			return ProjizierenForm(mitte + versatz, a1, a2, a3, start, richtung, cos, out f);
		}

		/// <summary>Wie Projizieren, mit schon bestimmter Form (Mitte, Halbachsen).</summary>
		internal static bool ProjizierenForm(Vector3 mitte, Vector3 a1, Vector3 a2, Vector3 a3, Vector3 start, Vector3 richtung, float cos, out Flaeche f)
		{
			f = default(Flaeche);
			Vector2 m = Lage(start, richtung, cos, mitte);
			Vector2 v1 = Lage(start, richtung, cos, mitte + a1) - m;
			Vector2 v2 = Lage(start, richtung, cos, mitte + a2) - m;
			Vector2 v3 = Lage(start, richtung, cos, mitte + a3) - m;
			// Projektion des Ellipsoids: Formmatrix Q = Summe v vT.
			float q00 = v1.x * v1.x + v2.x * v2.x + v3.x * v3.x + 1E-08f;
			float q01 = v1.x * v1.y + v2.x * v2.y + v3.x * v3.y;
			float q11 = v1.y * v1.y + v2.y * v2.y + v3.y * v3.y + 1E-08f;
			float det = q00 * q11 - q01 * q01;
			if (det <= 1E-14f)
			{
				return false;
			}
			f.M = m;
			f.A = q11 / det;
			f.B = (0f - q01) / det;
			f.C = q00 / det;
			float halbeSpur = 0.5f * (q00 + q11);
			float w = Mathf.Sqrt(Mathf.Max(0f, 0.25f * (q00 - q11) * (q00 - q11) + q01 * q01));
			f.Halb = Mathf.Sqrt(Mathf.Max(1E-08f, halbeSpur - w));
			return true;
		}

		/// <summary>
		/// Form eines Trefferkolliders in Weltkoordinaten: Mitte und die drei
		/// Halbachsen-Vektoren seines einbeschriebenen Ellipsoids (Box: Halbmasse,
		/// Kapsel: Radius / halbe Hoehe, Kugel: Radius; sonst achsparallele Bounds).
		/// </summary>
		internal static bool Form(Collider c, out Vector3 mitte, out Vector3 a1, out Vector3 a2, out Vector3 a3)
		{
			mitte = a1 = a2 = a3 = Vector3.zero;
			if (c == null)
			{
				return false;
			}
			Transform t = c.transform;
			if (c is BoxCollider box)
			{
				Vector3 h = box.size * 0.5f;
				mitte = t.TransformPoint(box.center);
				a1 = t.TransformVector(new Vector3(h.x, 0f, 0f));
				a2 = t.TransformVector(new Vector3(0f, h.y, 0f));
				a3 = t.TransformVector(new Vector3(0f, 0f, h.z));
			}
			else if (c is CapsuleCollider kapsel)
			{
				float r = kapsel.radius;
				float l = Mathf.Max(kapsel.height * 0.5f, r);
				Vector3 x = new Vector3(r, 0f, 0f);
				Vector3 y = new Vector3(0f, r, 0f);
				Vector3 z = new Vector3(0f, 0f, r);
				if (kapsel.direction == 0)
				{
					x.x = l;
				}
				else if (kapsel.direction == 1)
				{
					y.y = l;
				}
				else
				{
					z.z = l;
				}
				mitte = t.TransformPoint(kapsel.center);
				a1 = t.TransformVector(x);
				a2 = t.TransformVector(y);
				a3 = t.TransformVector(z);
			}
			else if (c is SphereCollider kugel)
			{
				float r = kugel.radius;
				mitte = t.TransformPoint(kugel.center);
				a1 = t.TransformVector(new Vector3(r, 0f, 0f));
				a2 = t.TransformVector(new Vector3(0f, r, 0f));
				a3 = t.TransformVector(new Vector3(0f, 0f, r));
			}
			else
			{
				Bounds bb = c.bounds;
				if (bb.extents.sqrMagnitude < 1E-08f)
				{
					return false;
				}
				mitte = bb.center;
				a1 = new Vector3(bb.extents.x, 0f, 0f);
				a2 = new Vector3(0f, bb.extents.y, 0f);
				a3 = new Vector3(0f, 0f, bb.extents.z);
			}
			return true;
		}

		/// <summary>
		/// Abstand des Punktes p (Bildebene) zum Rand der Flaeche, 0 = darauf.
		/// Gemessen entlang des Strahls von der Mitte - nie kleiner als der echte
		/// Abstand, sagt also eher "daneben" als "drauf".
		/// </summary>
		internal static float RandFlaeche(ref Flaeche f, Vector2 p, out float q)
		{
			Vector2 rel = p - f.M;
			q = Mathf.Sqrt(Mathf.Max(0f, f.A * rel.x * rel.x + 2f * f.B * rel.x * rel.y + f.C * rel.y * rel.y));
			if (q <= 1f)
			{
				return 0f;
			}
			return rel.magnitude * (1f - 1f / q);
		}

		/// <summary>
		/// Lage eines Weltpunkts gegenueber der Schusslinie in Grad, in der
		/// Bildebene: x = quer (Gieren mal cos Nicken, also echter Winkel),
		/// y = hoch (Nicken, positiv = tiefer, wie Player.Rotation.y).
		/// </summary>
		internal static Vector2 Lage(Vector3 start, Vector3 richtung, float cosNicken, Vector3 punkt)
		{
			Vector3 zu = punkt - start;
			if (zu.sqrMagnitude < 1E-06f)
			{
				return Vector2.zero;
			}
			zu.Normalize();
			return new Vector2(
				Mathf.DeltaAngle(Rueckstoss.Gieren(richtung), Rueckstoss.Gieren(zu)) * cosNicken,
				Mathf.DeltaAngle(Rueckstoss.Nicken(richtung), Rueckstoss.Nicken(zu)));
		}

		/// <summary>
		/// Kleinster Randabstand (Grad) von einer Achse zu Kopf, Rumpf oder
		/// Becken des Spielers, ohne Vorhalt. Fuer die Zielsuche.
		/// </summary>
		internal static bool KleinsterAbstand(Player p, Vector3 start, Vector3 richtung, out float rand, out float entfernung)
		{
			rand = float.MaxValue;
			entfernung = 0f;
			float cos = Mathf.Max(0.1f, Mathf.Cos(Rueckstoss.Nicken(richtung) * 0.017453292f));
			bool hat = false;
			EBodyPartColliderType[][] alle = { Kopf, Rumpf, Becken, Beine };
			for (int i = 0; i < alle.Length; i++)
			{
				if (!Bilden(p, "", alle[i], Vector3.zero, _suchPuffer, false, out var z))
				{
					continue;
				}
				for (int j = 0; j < z.Anzahl; j++)
				{
					if (!Projizieren(z.Kollider[j], Vector3.zero, start, richtung, cos, out var f))
					{
						continue;
					}
					float r = RandFlaeche(ref f, Vector2.zero, out _);
					if (r < rand)
					{
						rand = r;
						entfernung = (z.Mitte - start).magnitude;
					}
					hat = true;
				}
			}
			return hat;
		}
	}

	/// <summary>
	/// Echtes Soft Aim: faengt die eigene Mausbewegung ab und biegt sie ein
	/// Stueck Richtung Ziel. Dreht nie von selbst - ohne Mausbewegung tut es
	/// nichts.
	///
	/// DIE RECHNUNG (in Grad, in der Bildebene um die Schusslinie):
	///   d    = Mausbewegung dieses Bildes, als Winkel
	///   Zone = Ellipse um eine Trefferzone (Kopf, Rumpf, Becken), Abstand
	///          delta zum RAND der Zone, Radius R ab dem Rand
	///   prof = 1 - delta/R ausserhalb, innen min(1, Winkel zum Zielpunkt /
	///          kleinere Halbachse) - zur Zielmitte hin auf 0, damit nichts klebt
	///   w    = Staerke x prof x min(1, 1 + cos phi), hoechstens so viel, dass
	///          der Zielvektor nicht ueber den Zielpunkt hinausschiebt
	///   Schritt = w x (Richtung zum Zielpunkt) x |Schritt| + (1 - w) x Schritt
	///
	/// Die WUNSCHZONE (Koerperstelle) hat Vorrang. Die naechstgelegene andere
	/// Zone zieht nur als Rueckfall und wird mit (1 - prof der Wunschzone)
	/// abgeblendet: auf 3 m, auf der Brust, ist der Kopf ausser Reichweite und
	/// der Rumpf hilft; naehert man sich dem Kopf, uebernimmt er stufenlos.
	/// Ohne den Vorrang zog der Rumpf beim waagerechten Schwenk am Kopf vorbei
	/// die Bewegung nach unten (Simulation: bis 0,7 Grad) - das waere Kleben.
	///
	/// Die Bewegung eines Bildes wird als STRECKE in Teilschritten integriert,
	/// nicht nur an ihrem Anfang bewertet. Bei 400 Grad/s und 60 fps ist ein
	/// Bild 6,7 Grad lang - laenger als das Fenster um den Kopf; die Rechnung
	/// sah das Ziel dann an einem einzigen Punkt, und das Ergebnis hing an der
	/// Bildrate (Simulation: 10 m, 150 Grad/s: +0,61 Grad bei 60 fps gegen
	/// -0,01 bei 144). Mit Teilschritten: Abweichung zwischen 60/144/1000 fps
	/// unter 0,05 Grad bis 150 Grad/s.
	///
	/// min(1, 1 + cos phi): beim Wegziehen laeuft die Anziehung aus - die
	/// Bewegung wird nie umgekehrt, man kommt immer weg.
	/// </summary>
	internal static class SoftAim
	{
		internal static BepInEx.Logging.ManualLogSource Protokoll;

		private static Player _ziel;

		private static readonly Zone[] _zonen = new Zone[4];

		private static int _anzahl;

		// Index der Wunschzone in _zonen, -1 = keine (z. B. Kopf verdeckt).
		private static int _wunsch = -1;

		private static float _entfernung;

		// Mitdrehen: Zieltempo (momentan, plus Aenderung des Vorhalts; ab 1.4.4), Feuerpunkt, geglaettete
		// Drehraten (Grad/s, Drehraum von Player.Rotation: x quer, y hoch).
		private const float MitRuhe = 0.08f;

		// Ab dieser Mausgeschwindigkeit weg von der Zone ist das Mitdrehen ganz
		// abgeloest (wie im Simulationskern, "Wegziehen loest voll ab").
		private const float MitLoesen = 10f;

		private static Vector3 _zielTempo;

		private static Vector3 _start;

		private static Player _mitZiel;

		private static bool _mitHat;

		private static Vector2 _zielRateG;

		private static Vector2 _eigenRateG;

		private static Vector2 _letzteRotation;

		private static int _rotationBild = -99;

		public static Vector2 ZielRate;

		public static Vector2 MitGrad;

		// Vorhalt des letzten Bildes, fuer dessen Aenderung je Sekunde (ab 1.4.4).
		private static Vector3 _vhLetzt;

		private static float _vhZeit;

		private static int _vhBild = -99;

		private static bool _vhHat;

		private static Vector3 _vhRate;

		// Obergrenze der Vorhalt-Aenderung (m/s). Laeufer erreichen ~7 m/s; mehr
		// ist kein Laufen mehr, sondern ein Sprung der Rechnung.
		private const float VorhaltAenderungMax = 10f;

		/// <summary>
		/// Wie schnell sich der Vorhalt aendert (m/s, Welt). 0, wenn einer der
		/// beiden Vorhalte 0 ist (Vorhalt geht an oder aus: ein Sprung, kein
		/// Bewegen - Spruenge holt der Magnet), bei zu grossem oder fehlendem
		/// Zeitabstand, und hoechstens VorhaltAenderungMax.
		/// </summary>
		internal static Vector3 Vorhaltaenderung(Vector3 vorher, float tVorher, Vector3 jetzt, float tJetzt)
		{
			if (vorher.sqrMagnitude < 1E-06f || jetzt.sqrMagnitude < 1E-06f)
			{
				return Vector3.zero;
			}
			float dt = tJetzt - tVorher;
			if (dt <= 1E-04f || dt > 0.2f)
			{
				return Vector3.zero;
			}
			Vector3 r = (jetzt - vorher) / dt;
			if (r.sqrMagnitude > VorhaltAenderungMax * VorhaltAenderungMax)
			{
				r = r.normalized * VorhaltAenderungMax;
			}
			return r;
		}

		/// <summary>Vergroesserung des aktiven Glases in diesem Bild (1 ohne Optik/Anschlag).</summary>
		public static float Zoom = 1f;

		// Bestandteile der Korrektur dieses Bildes in Grad, Drehraum von
		// Player.Rotation (x quer, y hoch) - fuer die Aufzeichnung.
		public static Vector2 TeilAnz, TeilReib, TeilMag, TeilSchub, TeilMit;

		public static int TeilBild = -99;

		public static float WegSumme => _wegSumme;

		public static bool Geloest => _geloest;

		public static bool SchubAktiv => _schubBis > Time.time;

		public static float SchubRest => Mathf.Max(0f, _schubBis - Time.time);

		public static Player AktuellesZiel => _ziel;

		public static int WunschIndex => _wunsch;

		// Einfang-Schub und Loesen nach Weg.
		private static float _schubBis = -1f;

		private static bool _schubWar;

		private static float _wegSumme;

		private static bool _geloest;

		private static float _ruhe;

		private static void SchubLoesenZuruecksetzen()
		{
			_schubBis = -1f;
			_schubWar = false;
			_wegSumme = 0f;
			_geloest = false;
			_ruhe = 0f;
		}

		private static int _bild = -99;

		private static bool _fehlerGemeldet;

		// Diagnose: Stand des letzten abgefangenen Bildes.
		public static bool HatZiel;

		public static bool ImRadius;

		public static string ZoneName = "";

		public static float Rand = -1f;

		public static float RandWunsch = -1f;

		public static float Winkel = -1f;

		public static float Gewicht;

		public static float MagnetGrad;

		// Freizone (ab 1.4.5): Anteil 0..1 der Linie (1 = tief genug in der
		// Wunschzone), Anteil der Bilder in der Freizone (gleitend, 5 s) und der
		// Haltepunkt (Versatz Linie - Zielpunkt der Wunschzone, Bildebene in Grad),
		// den der Magnet in der Freizone statt der Zonenmitte haelt.
		public static float Frei;

		private static float _freiQuote;

		private static bool _freiQuoteHat;

		private static bool _ankerHat;

		private static Vector2 _anker;

		private static int _ankerZone = -1;

		public static Vector2 RohGrad;

		public static Vector2 KorrGrad;

		private static int _diagBild = -99;

		// Arbeitsfelder je Bild (keine Speicheranforderung im Eingabepfad).
		// Je Zone die projizierten Trefferkollider.
		private static readonly Flaeche[][] _f = { new Flaeche[8], new Flaeche[8], new Flaeche[8], new Flaeche[8] };

		private static readonly int[] _nf = new int[4];

		private static readonly float[] _klein = new float[4];

		private static readonly Vector2[] _a = new Vector2[4];

		// Fangradius (Grad) und Entfernung (m, vom Feuerpunkt) je Zone.
		private static readonly float[] _r = new float[4];

		private static readonly float[] _e = new float[4];

		/// <summary>Sicht zur Wunschzone in diesem Bild (frei / durch Material / verdeckt).</summary>
		public static string SichtGrund = "";

		/// <summary>
		/// Sicht je Zone (Kopf, Rumpf, Becken, Beine) im letzten ZonenMerken:
		/// 1 frei, 2 frei durch durchschiessbares Material, 0 verdeckt,
		/// -1 nicht vorhanden bzw. von der Patrone nicht durchschlagbar.
		/// </summary>
		public static readonly int[] SichtZonen = { -1, -1, -1, -1 };

		// Zielunruhe: gleitender Mittelwert der quadrierten Abweichung der
		// Schusslinie um ihr Mittel, gegenueber einem festen Koerperpunkt (0,5 s).
		private static float _winkelQ;

		private static bool _ruheHat;

		private static Player _ruheZiel;

		private static Vector2 _ruheMittel;

		/// <summary>Gemessene Zielunruhe je Achse (Grad, 1 Sigma).</summary>
		public static float Unruhe => Mathf.Sqrt(Mathf.Max(0f, _winkelQ) * 0.5f);

		public static float RadiusDiag = -1f;

		public static float RadiusMeterDiag = -1f;

		/// <summary>
		/// Trefferzonen des gewaehlten Gegners hinterlegen. wunsch = Index der
		/// Zone der Koerperstelle oder -1.
		/// </summary>
		public static void Merken(Player ziel, Zone[] zonen, int anzahl, int wunsch, float entfernung, Vector3 zielTempo, Vector3 vorhalt)
		{
			if ((object)ziel != _mitZiel)
			{
				// Neues Ziel: Glaettung der Drehraten neu beginnen - kein
				// Uebertrag der alten Drehrate, keine Spitze.
				_mitZiel = ziel;
				_mitHat = false;
				_zielRateG = Vector2.zero;
				_vhHat = false;
				// Zielwechsel: der naechste Eintritt in den Fangbereich loest den
				// Einfang-Schub aus, der Loese-Weg beginnt bei 0.
				SchubLoesenZuruecksetzen();
				_ruheHat = false;
				_ankerHat = false;
			}
			// MITDREHEN FOLGT DEM ZIELPUNKT, NICHT EINEM ALTEN TEMPO (ab 1.4.4).
			// Bis 1.4.3 kam hier das Vorhalt-Tempo an (0,12-0,15 s geglaettet) und
			// wurde unten noch einmal 0,08 s geglaettet - zusammen ~0,23 s alt.
			// Raid 08.11. (Lighthouse, Gegner sprintet quer auf 89-95 m, Zoom 6):
			// der Gegner bremste und drehte, das Mitdrehen drehte noch 0,3 s mit
			// +2,4 bis +0,7 Grad/s in die alte Richtung, der Magnet zog mit -2,6
			// Grad/s dagegen, die Linie lag 0,15-0,19 Grad (25-30 cm) vor dem
			// Zielpunkt -> Schuss 34 43 cm daneben. Ausserdem fehlte, dass sich der
			// Zielpunkt auch durch den VORHALT bewegt (Vorhalt waechst beim
			// Anlaufen, schrumpft beim Bremsen/Richtungswechsel).
			// Jetzt: momentanes Tempo + Aenderung des Vorhalts je Sekunde; die
			// Glaettung (MitRuhe) bleibt einmal. Nachgerechnet an allen
			// Aufzeichnungen des Tages: Abstand Linie-Zielpunkt im Mittel (RMS)
			// 10,1 -> 9,0 cm, Bilder ueber 10 cm 15 -> 8 %; Lighthouse 9,9 -> 6,8 cm.
			float jetzt = Time.time;
			int bildJetzt = Time.frameCount;
			if (bildJetzt != _vhBild)
			{
				_vhRate = _vhHat ? Vorhaltaenderung(_vhLetzt, _vhZeit, vorhalt, jetzt) : Vector3.zero;
				_vhLetzt = vorhalt;
				_vhZeit = jetzt;
				_vhBild = bildJetzt;
				_vhHat = true;
			}
			_zielTempo = zielTempo + _vhRate;
			_ziel = ziel;
			_anzahl = Mathf.Min(anzahl, _zonen.Length);
			for (int i = 0; i < _anzahl; i++)
			{
				_zonen[i] = zonen[i];
			}
			_wunsch = wunsch;
			_entfernung = entfernung;
			_bild = Time.frameCount;
		}

		public static void Vergessen()
		{
			_ziel = null;
			_anzahl = 0;
			_bild = -99;
			// Ohne Ziel keine Drehrate vom Ziel; beim Wiederaufnehmen frisch beginnen.
			_mitZiel = null;
			_mitHat = false;
			_zielRateG = Vector2.zero;
			ZielRate = Vector2.zero;
			MitGrad = Vector2.zero;
			SchubLoesenZuruecksetzen();
		}

		public static bool ImAnschlag(Player p)
		{
			return p?.HandsController != null && p.HandsController.IsAiming;
		}

		public static string ZonenGrund = "";

		public static string WaffeGrund = "";

		public static Vector2 RcsGrad;

		// Rueckstoss-Ausgleich: Fenster ab eigenem Schuss, Auslenkung beim
		// Fensterstart, bisher angewandte Summe (Grad, Drehraum).
		private static bool _rcsFenster;

		private static bool _rcsSpitze;

		private static bool _rcsStartOffen;

		private static Vector2 _rcsStart;

		private static Vector2 _rcsSumme;


		/// <summary>
		/// Ist eine Schusswaffe schussbereit in der Hand? Belegt in Player.cs,
		/// Player.FirearmController:
		///  - HandsController ist ein FirearmController (Messer, Granate, leere
		///    Haende, Heilen/Benutzen haben eigene Controller-Klassen),
		///  - CurrentOperation ist Idling, eine Feuer-Operation
		///    (FireOperationBase) oder RechamberOperation (Repetieren zwischen
		///    zwei Schuessen); Nachladen (BaseReloadOperation, InsertMag,
		///    PullOutMag, LoadCartridge...), Stoerung (MalfunctionOperation),
		///    Waffenwechsel (Remove/SpawnOperation), Zubehoer umbauen usw. fallen
		///    damit heraus,
		///  - IsInInteraction() = linke Hand beschaeftigt (Magazin/Munition
		///    pruefen laeuft im Idling als Animation),
		///  - Weapon.MalfState.State == None (gestoerte Waffe),
		///  - kein Granatwerfer (andere Ballistik).
		/// </summary>
		public static bool WaffeBereit(Player ich, out string grund)
		{
			grund = "";
			try
			{
				if (!(ich?.HandsController is Player.FirearmController fc))
				{
					grund = "no firearm";
				}
				else if (!(fc.Item is Weapon w))
				{
					grund = "no weapon";
				}
				else if (Einstellungen.Klasse(w) == Waffenklasse.Werfer)
				{
					grund = "grenade launcher";
				}
				else if (!(fc.CurrentOperation is Player.FirearmController.Idling)
						 && !(fc.CurrentOperation is Player.FirearmController.FireOperationBase)
						 && !(fc.CurrentOperation is Player.FirearmController.RechamberOperation))
				{
					grund = fc.CurrentOperation?.GetType().Name ?? "no operation";
				}
				else if (fc.IsInInteraction())
				{
					grund = "hand busy";
				}
				else if (w.MalfState.State != Weapon.EMalfunctionState.None)
				{
					grund = "malfunction " + w.MalfState.State;
				}
			}
			catch (Exception ex)
			{
				grund = ex.GetType().Name;
			}
			WaffeGrund = grund;
			return grund.Length == 0;
		}

		public static void RcsVergessen()
		{
			_rcsFenster = false;
			_rcsSumme = Vector2.zero;
			RcsGrad = Vector2.zero;
		}

		/// <summary>
		/// Eigene Drehrate aus der Aenderung von Player.Rotation zwischen zwei
		/// aufeinanderfolgenden Bildern (enthaelt Maus, Hilfe und Rueckstoss-
		/// Ausgleich), geglaettet mit 0,08 s. Jedes Bild aus dem Maus-Abgriff.
		/// </summary>
		public static void EigeneDrehungMessen(Player ich)
		{
			Vector2 rot = ich.Rotation;
			float dt = Time.deltaTime;
			if (Time.frameCount == _rotationBild + 1 && dt > 0f)
			{
				Vector2 delta = new Vector2(Mathf.DeltaAngle(_letzteRotation.x, rot.x),
											Mathf.DeltaAngle(_letzteRotation.y, rot.y));
				// Die Drehung, die die Hilfe im letzten Bild SELBST beigetragen hat
				// (Magnet, Schub, Mitdrehen), zaehlt nicht als "eigene" Drehung.
				// Sonst gleicht Mitdrehen gegen sich selbst ab: im Gleichgewicht nur
				// 0,7/1,7 = 41 % der Zielgeschwindigkeit, den Rest muss der Magnet
				// mit Verzug tragen (Aufzeichnung Interchange 03.11., 37 m, 6x,
				// Gegner laeuft 3-4 Grad/s: Abstand Median 0,43, 90 % 1,16 Grad).
				if (Einstellungen.MitOhneHilfe.Value && TeilBild == Time.frameCount - 1)
				{
					delta -= TeilMag + TeilSchub + TeilMit;
				}
				Vector2 rate = delta / dt;
				float t = 1f - Mathf.Exp((0f - dt) / MitRuhe);
				_eigenRateG += (rate - _eigenRateG) * t;
			}
			else
			{
				_eigenRateG = Vector2.zero;
			}
			_letzteRotation = rot;
			_rotationBild = Time.frameCount;
		}

		/// <summary>
		/// Drehrate der Linie Feuerpunkt -> Zielpunkt (Grad/s, Drehraum: x =
		/// Gieren, y = Nicken positiv nach unten). Gerechnet aus der RELATIVEN
		/// Geschwindigkeit (Zieltempo samt Vorhalt-Aenderung, s. Merken, minus eigenes Tempo):
		///   Gieren' = (rz vx - rx vz) / (rx^2 + rz^2)
		///   Nicken' = -(vy |r|^2 - ry (r.v)) / (|r|^2 h),  h = sqrt(rx^2 + rz^2)
		/// Das ist genau die Richtungsaenderung durch Bewegung - Spruenge des
		/// Zielpunkts (andere Kopfbox, Vorhalt an/aus, Zonenwechsel, Teleport)
		/// gehen nicht ein und erzeugen keine Spitze.
		/// </summary>
		private static Vector2 Drehrate(Vector3 punkt, Vector3 relTempo)
		{
			Vector3 r = punkt - _start;
			float h2 = r.x * r.x + r.z * r.z;
			float r2 = r.sqrMagnitude;
			if (h2 < 0.01f || r2 < 0.01f)
			{
				return Vector2.zero;
			}
			float h = Mathf.Sqrt(h2);
			float gieren = (r.z * relTempo.x - r.x * relTempo.z) / h2;
			float nicken = 0f - (relTempo.y * r2 - r.y * Vector3.Dot(r, relTempo)) / (r2 * h);
			return new Vector2(gieren, nicken) * 57.29578f;
		}

		/// <summary>Vom Schussabgriff: eigener Schuss gefallen.</summary>
		public static void EigenerSchuss()
		{
			if (!_rcsFenster)
			{
				_rcsFenster = true;
				_rcsSpitze = false;
				_rcsStartOffen = true;
				_rcsSumme = Vector2.zero;
			}
		}

		/// <summary>
		/// Rueckstoss-Auslenkung der Kugelbahn im Drehraum von Player.Rotation
		/// (Grad, quer/hoch) - NUR der Rueckstoss, nicht Schwanken oder Laufen.
		/// Beleg ProceduralWeaponAnimation.ApplyComplexRotation:
		///   r = Shootingg.CurrentRecoilEffect.GetHandRotationRecoil()
		///     + WeaponRecoilEffect.GetRotationRecoil()
		///   (bei ShotNeedsFovAdjustments x und z: atan(tan(.) x Kompensation),
		///   Kompensation = Player.RibcageScaleCurrentTarget, Player.cs:26023/26029)
		///   DeferredRotate: WeaponRootAnim wird um Quaternion.Euler(Drehung x r) gedreht.
		/// Hier wird diese Drehung an WeaponDirection rueckgaengig gemacht; beide
		/// Richtungen laufen durch AdjustShotVectors wie im Schuss, die Differenz ist
		/// die Auslenkung. Als aktuelle Drehung dient WeaponRootAnim.rotation (im
		/// Spiel: die Drehung kurz davor) - Fehler zweiter Ordnung.
		/// </summary>
		private static bool RueckstossAuslenkung(Player ich, out Vector2 auslenkung)
		{
			auslenkung = Vector2.zero;
			if (!(ich?.HandsController is Player.FirearmController fc))
			{
				return false;
			}
			var pwa = ich.ProceduralWeaponAnimation;
			var effekt = pwa?.Shootingg?.CurrentRecoilEffect;
			Transform wurzel = pwa?.HandsContainer?.WeaponRootAnim;
			if (effekt == null || wurzel == null)
			{
				return false;
			}
			Vector3 r = effekt.GetHandRotationRecoil() + effekt.WeaponRecoilEffect.GetRotationRecoil();
			float kompensation = ich.RibcageScaleCurrentTarget;
			if (pwa.ShotNeedsFovAdjustments && kompensation < 1f)
			{
				r.x = Mathf.Atan(Mathf.Tan(r.x * 0.017453292f) * kompensation) * 57.29578f;
				r.z = Mathf.Atan(Mathf.Tan(r.z * 0.017453292f) * kompensation) * 57.29578f;
			}
			Quaternion drehung = Quaternion.Euler(wurzel.rotation * r);
			Vector3 mit = fc.WeaponDirection;
			if (mit.sqrMagnitude < 1E-06f)
			{
				return false;
			}
			Vector3 ohne = Quaternion.Inverse(drehung) * mit;
			Vector3 o1 = fc.FireportPosition;
			Vector3 o2 = o1;
			fc.AdjustShotVectors(ref o1, ref mit);
			fc.AdjustShotVectors(ref o2, ref ohne);
			mit.Normalize();
			ohne.Normalize();
			auslenkung = new Vector2(Mathf.DeltaAngle(Rueckstoss.Gieren(ohne), Rueckstoss.Gieren(mit)),
									 Mathf.DeltaAngle(Rueckstoss.Nicken(ohne), Rueckstoss.Nicken(mit)));
			return true;
		}

		/// <summary>
		/// Rueckstoss-Ausgleich ueber die Mausbewegung. Aktiv nur im Fenster ab dem
		/// eigenen Schuss (SchussAbgriff) bis die Rueckstoss-Auslenkung abgeklungen
		/// ist, nie beim Sprinten (MovementContext.IsSprintEnabled). Die angewandte
		/// Summe ist immer genau -Anteil x (Auslenkung jetzt - Auslenkung beim
		/// Fensterstart): beim Ausschlag gegenlaeufig, beim Zurueckfedern genau so
		/// viel zurueck, wie ausgeglichen wurde - nie mehr; beim Schliessen des
		/// Fensters wird der Rest zurueckgegeben. Kein Nettoversatz.
		/// Liefert Mauseingabe-Einheiten (wie d in Korrigieren).
		/// </summary>
		public static Vector2 RcsSchritt(Player ich)
		{
			RcsGrad = Vector2.zero;
			float px = Mathf.Clamp01(Einstellungen.RcsQuer.Value / 100f);
			float py = Mathf.Clamp01(Einstellungen.RcsHoch.Value / 100f);
			bool sprint = false;
			try
			{
				sprint = ich.MovementContext != null && ich.MovementContext.IsSprintEnabled;
			}
			catch
			{
			}
			if ((px <= 0f && py <= 0f) || sprint || !_rcsFenster)
			{
				// Beim Sprinten (oder ausgeschaltet) Fenster schliessen ohne
				// Rueckgabe - der Rueckstoss bleibt dann wie im Spiel stehen.
				_rcsFenster = false;
				_rcsSumme = Vector2.zero;
				return Vector2.zero;
			}
			if (!RueckstossAuslenkung(ich, out var a))
			{
				_rcsFenster = false;
				_rcsSumme = Vector2.zero;
				return Vector2.zero;
			}
			if (_rcsStartOffen)
			{
				// Erstes Bild nach dem Schuss: Bezug merken (der Ausschlag kommt
				// erst mit dem naechsten Bild der Waffenanimation).
				_rcsStartOffen = false;
				_rcsStart = a;
				_rcsSumme = Vector2.zero;
				return Vector2.zero;
			}
			Vector2 ziel = new Vector2((0f - px) * (a.x - _rcsStart.x), (0f - py) * (a.y - _rcsStart.y));
			Vector2 schritt = ziel - _rcsSumme;
			_rcsSumme = ziel;
			Vector2 rest = a - _rcsStart;
			if (rest.magnitude > 0.005f)
			{
				_rcsSpitze = true;
			}
			else if (_rcsSpitze)
			{
				// Abgeklungen: den kleinen Rest zurueckgeben und schliessen.
				schritt -= _rcsSumme;
				_rcsSumme = Vector2.zero;
				_rcsFenster = false;
			}
			RcsGrad = schritt;
			MovementState zustand = ich.CurrentManagedState;
			float k = ich.GetRotationMultiplier() * ((zustand != null) ? zustand.StateSensitivity : 1f);
			return (k > 1E-06f) ? (schritt / k) : Vector2.zero;
		}

		private static string Kopfzeile(Player ich)
		{
			Einstellungen.Werte w = Einstellungen.Aktiv();
			bool feind = _ziel != null && Spielzugriff.IstFeind(_ziel, ich);
			return $"weapon {w.Klasse}{(w.Eigen ? " (own values)" : "")}, "
				+ ((WaffeGrund.Length == 0) ? "ready" : ("not ready: " + WaffeGrund))
				+ (_ziel != null ? $", enemy {(feind ? "yes" : "no")}" : "")
				+ $", zone choice {ZonenGrund}, RCS {RcsGrad.x:0.000}/{RcsGrad.y:0.000}°"
				+ $", tracking {MitGrad.x:0.0}/{MitGrad.y:0.0}°/s (aim line {ZielRate.x:0.0}/{ZielRate.y:0.0}°/s, own {_eigenRateG.x:0.0}/{_eigenRateG.y:0.0}°/s)"
				+ ((_schubBis > Time.time) ? $", boost yes ({_schubBis - Time.time:0.00} s)" : ", boost no")
				+ $", release travel {_wegSumme:0.00}° {(_geloest ? "released" : "not released")}";
		}

		/// <summary>
		/// Profil einer Zone am Punkt p: 0 ausserhalb des Radius, sonst
		/// 1 - delta/R (aussen) bzw. min(1, Winkel zum Zielpunkt / kleinere
		/// Halbachse) (innen). Liefert die Richtung zum Zielpunkt und dessen
		/// Winkelabstand.
		/// </summary>
		private static float Profil(int i, Vector2 p, out Vector2 e, out float winkel, out float innen, out float rand)
		{
			float radius = _r[i];
			e = Vector2.zero;
			rand = ZonenRand(i, p, out var drin);
			Vector2 zumZiel = _a[i] - p;
			winkel = zumZiel.magnitude;
			innen = 0f;
			if (rand >= radius || winkel < 1E-06f)
			{
				return 0f;
			}
			e = zumZiel / winkel;
			if (!drin)
			{
				innen = 1f - rand / radius;
				return innen;
			}
			innen = 1f;
			return Mathf.Min(1f, winkel / _klein[i]);
		}

		/// <summary>
		/// Kleinster Randabstand des Punktes p zu den Trefferkollidern der Zone;
		/// drin = p liegt nachweislich auf einem davon.
		/// </summary>
		private static float ZonenRand(int i, Vector2 p, out bool drin)
		{
			drin = false;
			float rand = float.MaxValue;
			Flaeche[] fl = _f[i];
			for (int j = 0; j < _nf[i]; j++)
			{
				float r = Trefferzonen.RandFlaeche(ref fl[j], p, out var q);
				if (q <= 1f)
				{
					drin = true;
					return 0f;
				}
				if (r < rand)
				{
					rand = r;
				}
			}
			return rand;
		}

		/// <summary>
		/// Lage der Schusslinie gegenueber den hinterlegten Zonen bestimmen:
		/// fuellt die Arbeitsfelder (_m, _h, _a) und die Diagnosewerte. Falsch,
		/// wenn kein frisches Ziel oder keine Feuerwaffe da ist.
		/// </summary>
		private static bool LageBestimmen(Player ich, out float cos, out float kleinste)
		{
			cos = 1f;
			kleinste = float.MaxValue;
			HatZiel = false;
			ImRadius = false;
			ZoneName = "";
			Rand = -1f;
			RandWunsch = -1f;
			Winkel = -1f;
			// Zielsuche (Update) und Eingabe laufen beide im Update-Schritt von
			// Unity, in nicht festgelegter Reihenfolge. Ein Ziel aus diesem oder
			// dem vorigen Bild gilt; alles Aeltere ist verworfen.
			if (_ziel == null || _anzahl == 0 || Time.frameCount - _bild > 1
				|| _ziel.HealthController == null || !_ziel.HealthController.IsAlive)
			{
				return false;
			}
			if (!Rueckstoss.Schusslinie(ich, out var start, out var richtung))
			{
				return false;
			}
			_start = start;
			HatZiel = true;
			RadiusDiag = -1f;
			RadiusMeterDiag = -1f;
			cos = Mathf.Max(0.1f, Mathf.Cos(Rueckstoss.Nicken(richtung) * 0.017453292f));
			for (int i = 0; i < _anzahl; i++)
			{
				_e[i] = (_zonen[i].Mitte - start).magnitude;
				_r[i] = Einstellungen.RadiusGrad(_e[i]);
				_a[i] = Trefferzonen.Lage(start, richtung, cos, _zonen[i].Ziel);
				_nf[i] = 0;
				_klein[i] = float.MaxValue;
				Zone z = _zonen[i];
				for (int j = 0; j < z.Anzahl && j < _f[i].Length; j++)
				{
					if (Trefferzonen.Projizieren(z.Kollider[j], z.Versatz, start, richtung, cos, out var f))
					{
						_f[i][_nf[i]++] = f;
						_klein[i] = Mathf.Min(_klein[i], f.Halb);
					}
				}
				if (_nf[i] == 0)
				{
					// Kein Kollider projizierbar: Zone zaehlt in diesem Bild nicht.
					_r[i] = 0f;
					_klein[i] = 0.01f;
				}
				kleinste = Mathf.Min(kleinste, _klein[i]);
			}
			// Fuehrende Zone nach derselben Regel wie die Rechnung: die
			// Wunschzone, wenn sie im Radius liegt, sonst die naechstgelegene.
			int fuehrt = -1;
			float naechste = float.MaxValue;
			for (int i = 0; i < _anzahl; i++)
			{
				float r = ZonenRand(i, Vector2.zero, out _);
				if (i == _wunsch)
				{
					RandWunsch = r;
				}
				else if (r < naechste)
				{
					naechste = r;
					fuehrt = i;
				}
			}
			if (_wunsch >= 0 && _wunsch < _anzahl && (RandWunsch < _r[_wunsch] || fuehrt < 0))
			{
				fuehrt = _wunsch;
			}
			if (fuehrt >= 0)
			{
				ZoneName = _zonen[fuehrt].Name;
				Rand = (fuehrt == _wunsch) ? RandWunsch : naechste;
				Winkel = _a[fuehrt].magnitude;
				RadiusDiag = _r[fuehrt];
				RadiusMeterDiag = _e[fuehrt] * Mathf.Tan(_r[fuehrt] * 0.017453292f);
			}
			ImRadius = fuehrt >= 0 && Rand >= 0f && Rand < _r[fuehrt];
			// Zielunruhe: Lage der Schusslinie gegenueber einem FESTEN Koerperpunkt
			// (Mitte der Rumpf-Huelle ohne Vorhalt), gemessen als Abweichung um ihr
			// gleitendes Mittel (0,5 s). Bisher wurde der Restfehler zum Zielpunkt
			// der fuehrenden Zone genommen - der springt bei jeder Zonen- oder
			// Kopfbox-Wahl, und dieser Sprung zaehlte als "Unruhe" und veraenderte
			// ueber die Trefferanteile wieder die Wahl (Rueckkopplung, Raid 02.11.:
			// 13,9 Zonenwechsel/s auf 166 m). Der feste Punkt haengt von der Wahl
			// nicht ab; uebrig bleibt Schwanken, Atmen, Rueckstoss.
			{
				int bezug = -1;
				for (int i = 0; i < _anzahl; i++)
				{
					if (_zonen[i].Name == "Chest")
					{
						bezug = i;
					}
				}
				if (bezug < 0)
				{
					bezug = 0;
				}
				Vector2 lage = Trefferzonen.Lage(start, richtung, cos, _zonen[bezug].Mitte - _zonen[bezug].Versatz);
				float tq = 1f - Mathf.Exp((0f - Time.deltaTime) / 0.5f);
				if (!_ruheHat || (object)_ruheZiel != _ziel)
				{
					_ruheHat = true;
					_ruheZiel = _ziel;
					_ruheMittel = lage;
				}
				else
				{
					_ruheMittel += (lage - _ruheMittel) * tq;
					_winkelQ += ((lage - _ruheMittel).sqrMagnitude - _winkelQ) * tq;
				}
			}
			return true;
		}

		/// <summary>
		/// Biegt die Mausbewegung d (Eingabe-Einheiten, Vorzeichen wie an
		/// Player.Rotate) Richtung Ziel und legt den Halte-Magnet dazu.
		/// Wahr, wenn d veraendert wurde.
		/// </summary>
		public static bool Korrigieren(Player ich, ref Vector2 d)
		{
			Gewicht = 0f;
			MagnetGrad = 0f;
			Frei = 0f;
			RohGrad = Vector2.zero;
			KorrGrad = Vector2.zero;
			_diagBild = Time.frameCount;
			TeilAnz = TeilReib = TeilMag = TeilSchub = TeilMit = Vector2.zero;
			TeilBild = Time.frameCount;
			Vector2 anzW = Vector2.zero, reibW = Vector2.zero;
			if (!LageBestimmen(ich, out var cos, out var kleinste))
			{
				_ankerHat = false;
				return false;
			}
			// Eingabe -> Grad: genau die Umrechnung aus MovementState.ClampRotation
			// (ApplyExternalSense = GetRotationMultiplier, mal StateSensitivity).
			MovementState zustand = ich.CurrentManagedState;
			float k = ich.GetRotationMultiplier() * ((zustand != null) ? zustand.StateSensitivity : 1f);
			RohGrad = d * k;
			KorrGrad = RohGrad;
			float dt = Time.deltaTime;
			if (k <= 1E-06f || dt <= 0f)
			{
				return false;
			}
			Einstellungen.Werte werte = Einstellungen.Aktiv();
			float magnetMax = Mathf.Max(0f, werte.Magnet);
			// Vergroesserung des aktiven Glases (SightComponent.GetCurrentOpticZoom).
			Zoom = 1f;
			try
			{
				if (ImAnschlag(ich))
				{
					SightComponent glas = ich.ProceduralWeaponAnimation?.CurrentAimingMod;
					if (glas != null)
					{
						Zoom = Mathf.Max(1f, glas.GetCurrentOpticZoom());
					}
				}
			}
			catch
			{
			}
			float zoomTeiler = (Einstellungen.DeckelDurchZoom.Value) ? Zoom : 1f;
			float schubMax = Mathf.Max(magnetMax, werte.SchubMax / zoomTeiler);
			float mitDeckel = Mathf.Max(magnetMax, werte.MitDeckel / zoomTeiler);
			float zugDeckel = Mathf.Max(magnetMax, Einstellungen.SoftDeckel.Value / zoomTeiler);
			bool bewegt = d.x != 0f || d.y != 0f;
			if (!bewegt && magnetMax <= 0f && werte.MitAnteil <= 0f)
			{
				// Keine Mausbewegung, kein Magnet, kein Mitdrehen - nichts tun.
				return false;
			}

			// In die Bildebene (echte Winkel): quer mal cos(Nicken).
			Vector2 dW = new Vector2(d.x * k * cos, d.y * k);
			float laenge = dW.magnitude;
			Vector2 dRicht = bewegt ? (dW / laenge) : Vector2.zero;
			float staerke = Mathf.Clamp01(werte.Staerke);
			// Reibung ist ein Anteil der Bewegung; im Glas wird aber der Bereich, in
			// dem sie greift, um die Vergroesserung groesser, und jede Feinkorrektur
			// dort wird gebremst. Darum durch den Zoom geteilt (Simulation 6x:
			// geschluckte Eigenbewegung 32 -> 19 %, Kopf beim Verfolgen gleich).
			float reibungMax = Mathf.Clamp01(werte.Reibung) / Mathf.Max(1f, Zoom);
			Vector2 p = Vector2.zero;

			// --- 1. Die eigene Bewegung umbiegen (nur wenn die Maus sich bewegt) ---
			if (bewegt)
			{
				// Teilschritte: ein Viertel der kleinsten Zonen-Halbachse, hoechstens 64.
				float schritt = Mathf.Max(0.005f, 0.25f * kleinste);
				int n = Mathf.Clamp(Mathf.CeilToInt(laenge / schritt), 1, 64);
				float l = laenge / n;
				bool ersterSchritt = true;
				for (int s = 0; s < n; s++)
				{
					// Wunschzone mit Vorrang ...
					float profW = 0f, innenW = 0f, winkelW = 0f;
					Vector2 eW = Vector2.zero;
					if (_wunsch >= 0 && _wunsch < _anzahl)
					{
						profW = Profil(_wunsch, p, out eW, out winkelW, out innenW, out _);
					}
					// ... und die naechstgelegene andere Zone als Rueckfall.
					float profR = 0f, innenR = 0f, winkelR = 0f, randR = float.MaxValue;
					Vector2 eR = Vector2.zero;
					for (int i = 0; i < _anzahl; i++)
					{
						if (i == _wunsch)
						{
							continue;
						}
						float pr = Profil(i, p, out var ei, out var wi, out var ii, out var ri);
						if (pr > 0f && ri < randR)
						{
							randR = ri;
							profR = pr;
							eR = ei;
							winkelR = wi;
							innenR = ii;
						}
					}
					profR *= 1f - profW;
					// Freizone: tief in der Wunschzone wirken Anziehung und Reibung
					// nicht, zum Rand (Sicherheitsrand) blenden sie wieder ein.
					float fr = 1f - FreiAnteil(p);
					profW *= fr;
					profR *= fr;

					Vector2 st = dRicht * l;
					// Reibung: Anteil quer oder weg von der fuehrenden Zone bremsen,
					// den Anteil zu ihr hin nie.
					if (reibungMax > 0f && (profW > 0f || profR > 0f))
					{
						bool wFuehrt = profW >= profR;
						Vector2 e = wFuehrt ? eW : eR;
						float f = reibungMax * (wFuehrt ? innenW : innenR) * fr;
						Vector2 hin = e * Mathf.Max(0f, Vector2.Dot(st, e));
						Vector2 vorReib = st;
						st = hin + (st - hin) * (1f - f);
						reibW += st - vorReib;
					}
					Vector2 vorZug = st;
					float ls = st.magnitude;
					if (ls > 1E-09f && (profW > 0f || profR > 0f))
					{
						float wW = Mathf.Min(staerke * profW * Mathf.Min(1f, 1f + Vector2.Dot(dRicht, eW)), winkelW / ls);
						float wR = Mathf.Min(staerke * profR * Mathf.Min(1f, 1f + Vector2.Dot(dRicht, eR)), winkelR / ls);
						float summe = wW + wR;
						Vector2 zug = eW * wW + eR * wR;
						if (summe > 1f)
						{
							zug /= summe;
							summe = 1f;
						}
						if (ersterSchritt)
						{
							Gewicht = summe;
						}
						st = zug * ls + (1f - summe) * st;
					}
					anzW += st - vorZug;
					ersterSchritt = false;
					p += st;
				}
				// Deckel fuer die Aenderung der eigenen Bewegung, in Grad je Sekunde.
				Vector2 aenderung = p - dW;
				float deckel = zugDeckel * dt;
				float betrag = aenderung.magnitude;
				if (betrag > deckel && betrag > 0f)
				{
					p = dW + aenderung * (deckel / betrag);
					anzW *= deckel / betrag;
					reibW *= deckel / betrag;
				}
			}

			// --- Drehrate der Ziellinie (fuer Loesen nach Weg und Mitdrehen) ---
			ZielRate = Vector2.zero;
			{
				int fz = (_wunsch >= 0 && _wunsch < _anzahl) ? _wunsch : 0;
				Vector3 rel = _zielTempo - ich.Velocity;
				Vector2 roh = Drehrate(_zonen[fz].Ziel, rel);
				float t = 1f - Mathf.Exp((0f - dt) / MitRuhe);
				if (!_mitHat)
				{
					_zielRateG = Vector2.zero;
					_mitHat = true;
				}
				_zielRateG += (roh - _zielRateG) * t;
				ZielRate = _zielRateG;
			}

			// --- Loesen nach Weg und Einfang-Schub (Wunschzone) -------------------
			// Weg weg von der Zone: Summe = max(0, Summe x e^(-dt/0,5) + weg), nur im
			// Fangbereich gezaehlt. weg = Mausweg von der Zone weg - aber nur, soweit
			// er das AUCH relativ zur Bewegung des Ziels ist (das Kleinere von beiden):
			// wer einen laufenden Gegner verfolgt und dabei vor der Kopfmitte liegt,
			// bewegt sich absolut "weg", relativ nicht; wer hinterherhaengt, relativ
			// "weg", absolut nicht. Beides ist kein Wegziehen (Simulation: sonst
			// 30 m laufend 66 -> 55 % bzw. 10 m 98 -> 84 %). Zur Zone hin baut ab.
			// Ab Schwelle gelöst (Magnet, Mitdrehen, Schub aus), wieder an unter der
			// Haelfte. Einfang-Schub: Eintritt in den Fangbereich -> fuer die Dauer
			// hoeherer Magnet-Deckel; Abbruch bei >= 10 Grad/s weg von der Zone oder
			// wenn geloest.
			{
				float innenS = 0f;
				Vector2 eS = Vector2.zero;
				float profS = 0f;
				if (_wunsch >= 0 && _wunsch < _anzahl)
				{
					profS = Profil(_wunsch, Vector2.zero, out eS, out _, out innenS, out _);
				}
				float schwelle = Mathf.Max(0f, Einstellungen.SoftLoesenWeg.Value);
				if (schwelle > 0f)
				{
					float weg = 0f;
					if (profS > 0f && bewegt)
					{
						Vector2 zielWeg = new Vector2(_zielRateG.x * cos, _zielRateG.y) * dt;
						float absolut = 0f - Vector2.Dot(dW, eS);
						float relativ = 0f - Vector2.Dot(dW - zielWeg, eS);
						weg = (absolut > 0f) ? Mathf.Max(0f, Mathf.Min(absolut, relativ)) : absolut;
						// Im Bildschirm-Mass: bei Vergroesserung Z ist dieselbe Handbewegung
						// nur 1/Z Grad - die 2 Grad Schwelle loeste bei 6x praktisch nie
						// (Aufzeichnung Sandbox 02.11.: 0 Bilder geloest). Simulation 6x:
						// geschluckte Eigenbewegung 90 -> 32 %, Kopf beim Verfolgen gleich.
						weg *= Mathf.Max(1f, Zoom);
					}
					_wegSumme = Mathf.Max(0f, _wegSumme * Mathf.Exp((0f - dt) / 0.5f) + weg);
					if (_wegSumme >= schwelle)
					{
						_geloest = true;
					}
					else if (_wegSumme < schwelle * 0.5f)
					{
						_geloest = false;
					}
					// Maus ruhig -> sofort wieder greifen (nicht erst nach dem Abklingen).
					_ruhe = bewegt ? 0f : _ruhe + dt;
					float wieder = Einstellungen.SoftWiedergreifen.Value;
					if (_geloest && !bewegt && wieder > 0f && _ruhe >= wieder)
					{
						_geloest = false;
						_wegSumme = 0f;
					}
				}
				else
				{
					_wegSumme = 0f;
					_geloest = false;
				}
				bool imBereich = innenS > 0f;
				if (imBereich && !_schubWar && schubMax > 0f && werte.SchubDauer > 0f)
				{
					_schubBis = Time.time + werte.SchubDauer;
				}
				_schubWar = imBereich;
				if (_schubBis > Time.time && bewegt && laenge / dt >= MitLoesen && profS > 0f && Vector2.Dot(dW, eS) < 0f)
				{
					_schubBis = -1f;
				}
				if (_geloest)
				{
					_schubBis = -1f;
					magnetMax = 0f;
				}
				else if (_schubBis > Time.time)
				{
					magnetMax = Mathf.Max(magnetMax, schubMax);
				}
			}

			// --- 2. Halte-Magnet: auch bei stiller Maus ---------------------------
			// Gemessen wird die echte Schusslinie (Feuerpunkt + Laufrichtung nach
			// AdjustShotVectors) - Rueckstoss und Schwanken stecken darin schon.
			// Der Magnet holt sie mit der Zeitkonstante zurueck aufs Ziel; das ist
			// der Rueckstoss- und Schwankausgleich dieser Art, ohne Versatz-Rechnung
			// daneben (die brauchten nur die alten Arten, weil sie die KAMERA
			// stellten). Exponentiell: kommt nie ueber den Zielpunkt hinaus,
			// unabhaengig von der Bildrate.
			// Wer vom Ziel wegzieht, schwaecht ihn ab: Gewicht 1 - (1 - g) x
			// min(1, Mausgeschwindigkeit / Deckel), g = min(1, 1 + cos phi).
			// --- Freizone: Haltepunkt nachfuehren -------------------------------
			// Liegt der vorhergesagte Einschlag tief genug in der Wunschzone, haelt
			// der Magnet die Linie an dem Punkt, an den der Spieler sie gelegt hat:
			// der Haltepunkt wandert mit der EIGENEN Mausbewegung (dW, ohne Hilfe
			// und ohne RCS) und bleibt in der Freizone. Rueckstoss und Schwanken
			// stecken nicht in dW - die holt der Magnet weiter zurueck, aber auf den
			// Haltepunkt statt auf die Zonenmitte. Kaan 08.11.: "versucht selbst
			// nachzujustieren, wurde zurueckgezogen". Simulation 30 m, stille Maus,
			// Dauerfeuer: Kopf 98 % ohne Freizone, 92 % mit Freizone ohne Haltepunkt,
			// 96 % mit Haltepunkt.
			bool ankerWirkt = FreizoneSchritt(dW, dt);

			Vector2 magnet = Vector2.zero;
			if (magnetMax > 0f)
			{
				float tau = Mathf.Max(0.01f, werte.MagnetZeit);
				float anteil = 1f - Mathf.Exp((0f - dt) / tau);
				float tempo = laenge / dt;
				float innenW = 0f, winkelW = 0f;
				Vector2 eW = Vector2.zero;
				if (_wunsch >= 0 && _wunsch < _anzahl)
				{
					Profil(_wunsch, Vector2.zero, out eW, out winkelW, out innenW, out _);
				}
				if (ankerWirkt)
				{
					HaltZiel(out eW, out winkelW);
				}
				float innenR = 0f, winkelR = 0f, randR = float.MaxValue;
				Vector2 eR = Vector2.zero;
				for (int i = 0; i < _anzahl; i++)
				{
					if (i == _wunsch)
					{
						continue;
					}
					float pr = Profil(i, Vector2.zero, out var ei, out var wi, out var ii, out var ri);
					if (pr > 0f && ri < randR)
					{
						randR = ri;
						eR = ei;
						winkelR = wi;
						innenR = ii;
					}
				}
				innenR *= 1f - innenW;
				magnet = Weichen(MagnetTeil(eW, winkelW, innenW, anteil, magnetMax, dt, dRicht, tempo)
					+ MagnetTeil(eR, winkelR, innenR, anteil, magnetMax, dt, dRicht, tempo), dW, dt, cos);
				MagnetGrad = magnet.magnitude / dt;
				// Anteil des Einfang-Schubs: Unterschied zum normalen Deckel.
				float normal = Mathf.Max(0f, werte.Magnet);
				if (magnetMax > normal)
				{
					Vector2 ohneSchub = Weichen(MagnetTeil(eW, winkelW, innenW, anteil, normal, dt, dRicht, tempo)
						+ MagnetTeil(eR, winkelR, innenR, anteil, normal, dt, dRicht, tempo), dW, dt, cos);
					TeilSchub = new Vector2((magnet - ohneSchub).x / cos, (magnet - ohneSchub).y);
					TeilMag = new Vector2(ohneSchub.x / cos, ohneSchub.y);
				}
				else
				{
					TeilMag = new Vector2(magnet.x / cos, magnet.y);
				}
			}

			// --- 3. Mitdrehen bei bewegten Zielen ------------------------------
			// m = Anteil x (Drehrate zum Ziel - eigene Drehrate), beide geglaettet
			// (0,08 s), mal Verlauf der Zone wie beim Magnet (innen 1, zum
			// Radiusrand auf 0; Rueckfallzone mit (1 - Verlauf der Wunschzone)),
			// gedeckelt; beim Wegziehen VON DER ZONE abgeloest:
			//   x (1 - (1 - g) x min(1, Mausgeschwindigkeit / 10 Grad/s)),
			//   g = min(1, 1 + cos(Mausrichtung, Richtung zur Zone)).
			Vector2 mit = Vector2.zero;
			if (werte.MitAnteil > 0f && mitDeckel > 0f && !_geloest)
			{
				float innenW = 0f;
				Vector2 eW = Vector2.zero;
				if (_wunsch >= 0 && _wunsch < _anzahl)
				{
					Profil(_wunsch, Vector2.zero, out eW, out _, out innenW, out _);
				}
				float innenR = 0f, randR = float.MaxValue;
				Vector2 eR = Vector2.zero;
				for (int i = 0; i < _anzahl; i++)
				{
					if (i == _wunsch)
					{
						continue;
					}
					float pr = Profil(i, Vector2.zero, out var ei, out _, out var ii, out var ri);
					if (pr > 0f && ri < randR)
					{
						randR = ri;
						eR = ei;
						innenR = ii;
					}
				}
				float verlauf = innenW + innenR * (1f - innenW);
				if (verlauf > 0f)
				{
					// Nur entlang der Bewegung des ZIELS (mit Vorzeichen): bei stehendem
					// Ziel war die Differenz = -eigene Drehrate und bremste jede eigene
					// Bewegung (Simulation: 37 % jeder Schwenkbewegung; Aufzeichnung
					// Sandbox: 164 Bilder gegen die Bewegung). Quer zur Zielbewegung
					// und bei ruhendem Ziel gibt es kein Mitdrehen; Verfolgen unveraendert.
					Vector2 diff = Vector2.zero;
					float zr = _zielRateG.magnitude;
					if (zr > 1E-03f)
					{
						Vector2 zrRicht = _zielRateG / zr;
						diff = zrRicht * Vector2.Dot(_zielRateG - _eigenRateG, zrRicht) * Mathf.Min(1f, zr / 1f);
					}
					// In die Bildebene (quer mal cos Nicken) wie die Mausbewegung.
					Vector2 rate = new Vector2(diff.x * cos, diff.y) * (Mathf.Clamp01(werte.MitAnteil) * verlauf);
					float betrag = rate.magnitude;
					if (betrag > mitDeckel)
					{
						rate *= mitDeckel / betrag;
					}
					if (bewegt)
					{
						Vector2 e = (innenW > 0f) ? eW : eR;
						float g = Mathf.Min(1f, 1f + Vector2.Dot(dRicht, e));
						// Das ist das "nie gegen die eigene Bewegung": wer von der
						// Zone wegzieht, loest es ab. KEIN zusaetzliches Abschneiden
						// gegen die Mausrichtung - nach einem Haken des Gegners zieht
						// die Hand noch in die alte Richtung, genau dann hilft das
						// Mitdrehen (Simulation Zickzack 30 m: 28 -> 34 % mit,
						// nur 29 % mit Abschneiden; Wegdrehen unveraendert).
						rate *= 1f - (1f - g) * Mathf.Min(1f, (laenge / dt) / MitLoesen);
					}
					mit = rate * dt;
				}
			}
			MitGrad = new Vector2(mit.x / Mathf.Max(0.1f, cos), mit.y) / dt;
			TeilMit = new Vector2(mit.x / cos, mit.y);
			TeilAnz = new Vector2(anzW.x / cos, anzW.y);
			TeilReib = new Vector2(reibW.x / cos, reibW.y);

			Vector2 ergebnis = p + magnet + mit;
			Vector2 r2 = new Vector2(ergebnis.x / (k * cos), ergebnis.y / k);
			if (float.IsNaN(r2.x) || float.IsNaN(r2.y) || float.IsInfinity(r2.x) || float.IsInfinity(r2.y))
			{
				return false;
			}
			if (r2 == d)
			{
				return false;
			}
			d = r2;
			KorrGrad = r2 * k;
			return true;
		}

		/// <summary>
		/// Freizone: 1, wenn der Punkt p (Bildebene, relativ zur Schusslinie) um
		/// mindestens den Sicherheitsrand tief in einem Trefferkollider der
		/// Wunschzone liegt; zum Rand weich (smoothstep) auf 0, ausserhalb 0.
		/// Tiefe je Kollider = (1 - q) x kleinste Halbachse: der Punkt liegt auf
		/// der um q verkleinerten Ellipse, deren Abstand zum Rand mindestens
		/// (1 - q) x kleinste Halbachse ist. Rand = Anteil der Zonenbreite
		/// (2 x groesste der kleinsten Halbachsen), mindestens 2 cm am Ziel,
		/// hoechstens 90 % der halben Breite.
		/// </summary>
		internal static float FreiAnteil(Vector2 p)
		{
			if (!Einstellungen.SoftFreizone.Value || _wunsch < 0 || _wunsch >= _anzahl || _nf[_wunsch] == 0)
			{
				return 0f;
			}
			Flaeche[] fl = _f[_wunsch];
			float halbMax = 0f, tiefe = 0f;
			for (int j = 0; j < _nf[_wunsch]; j++)
			{
				halbMax = Mathf.Max(halbMax, fl[j].Halb);
				Trefferzonen.RandFlaeche(ref fl[j], p, out var q);
				if (q < 1f)
				{
					tiefe = Mathf.Max(tiefe, (1f - q) * fl[j].Halb);
				}
			}
			if (tiefe <= 0f)
			{
				return 0f;
			}
			float zweiCm = Mathf.Atan2(0.02f, Mathf.Max(0.5f, _e[_wunsch])) * 57.29578f;
			float rand = Mathf.Max(2f * halbMax * Mathf.Clamp(Einstellungen.SoftFreizoneRand.Value, 0f, 45f) / 100f, zweiCm);
			rand = Mathf.Min(rand, 0.9f * halbMax);
			if (rand <= 1E-06f)
			{
				return 1f;
			}
			float u = Mathf.Clamp01(tiefe / rand);
			return u * u * (3f - 2f * u);
		}

		/// <summary>
		/// Freizone je Bild: Frei an der Linie bestimmen, den Haltepunkt mit der
		/// eigenen Mausbewegung dW (Bildebene, Grad) nachfuehren und in der
		/// Freizone halten, Anteil der Bilder mitzaehlen. Wahr = der Magnet haelt
		/// auf den Haltepunkt (HaltZiel) statt auf den Zielpunkt.
		/// </summary>
		internal static bool FreizoneSchritt(Vector2 dW, float dt)
		{
			Frei = FreiAnteil(Vector2.zero);
			bool wirkt = false;
			if (Frei > 0f && _wunsch >= 0 && _wunsch < _anzahl)
			{
				if (!_ankerHat || _ankerZone != _wunsch)
				{
					_anker = -_a[_wunsch];
					_ankerHat = true;
					_ankerZone = _wunsch;
				}
				_anker += dW;
				AnkerBegrenzen();
				wirkt = true;
			}
			else
			{
				_ankerHat = false;
			}
			if (Einstellungen.SoftFreizone.Value && dt > 0f)
			{
				float tq = 1f - Mathf.Exp((0f - dt) / 5f);
				float drin = (Frei >= 0.999f) ? 1f : 0f;
				_freiQuote = _freiQuoteHat ? (_freiQuote + (drin - _freiQuote) * tq) : drin;
				_freiQuoteHat = true;
			}
			return wirkt;
		}

		/// <summary>
		/// Richtung und Winkel, auf die der Magnet in der Freizone zieht: der
		/// Haltepunkt, zum Rand der Freizone weich zurueck auf den Zielpunkt
		/// (Frei -> 0 am Zonenrand, kein Sprung).
		/// </summary>
		internal static void HaltZiel(out Vector2 e, out float winkel)
		{
			Vector2 zuHalt = _a[_wunsch] + _anker * Frei;
			winkel = zuHalt.magnitude;
			e = (winkel > 1E-06f) ? (zuHalt / winkel) : Vector2.zero;
		}

		/// <summary>
		/// Haltepunkt in der Freizone halten: liegt er ausserhalb, wird er zum
		/// Zielpunkt hin verkuerzt, bis er wieder ganz frei liegt (Halbierung).
		/// </summary>
		private static void AnkerBegrenzen()
		{
			Vector2 ziel = _a[_wunsch];
			if (FreiAnteil(ziel + _anker) >= 0.999f)
			{
				return;
			}
			float lo = 0f, hi = 1f;
			for (int k = 0; k < 8; k++)
			{
				float m = 0.5f * (lo + hi);
				if (FreiAnteil(ziel + _anker * m) >= 0.999f)
				{
					lo = m;
				}
				else
				{
					hi = m;
				}
			}
			_anker *= lo;
		}

		/// <summary>
		/// Magnet bei aktiver eigener Maus: Bewegt sich die Hand relativ zum Ziel
		/// schneller als "Magnet weicht eigener Maus ab" (Bildschirm-Mass: Grad/s x
		/// Zoom), wird der Anteil des Magnets GEGEN diese Bewegung herausgenommen;
		/// quer dazu zieht er weiter. Bei stiller/zitternder Maus unveraendert.
		/// </summary>
		private static Vector2 Weichen(Vector2 magnet, Vector2 dW, float dt, float cos)
		{
			float schwelle = Einstellungen.MagnetWeicht.Value;
			if (schwelle <= 0f || dt <= 0f || magnet == Vector2.zero)
			{
				return magnet;
			}
			// Nur wenn die EIGENE Maus wirklich steuert. Frueher wurde allein die
			// Bewegung relativ zum Ziel geprueft: bei stiller Maus und quer laufendem
			// Gegner (Interchange 08.11.: 3,5 Grad/s x Zoom 6 = 21 > 10) galt die stille
			// Hand als "steuert weg", und der Magnet verlor genau seinen Zug zum Ziel -
			// die Linie fiel bis 1 Grad (45 cm auf 24 m) hinter den Kopf.
			if (dW.magnitude / dt * Mathf.Max(1f, Zoom) < schwelle)
			{
				return magnet;
			}
			Vector2 zielWeg = new Vector2(_zielRateG.x * cos, _zielRateG.y) * dt;
			Vector2 u = dW - zielWeg;
			float lu = u.magnitude;
			if (lu <= 1E-09f || lu / dt * Mathf.Max(1f, Zoom) < schwelle)
			{
				return magnet;
			}
			Vector2 uh = u / lu;
			float gegen = Vector2.Dot(magnet, uh);
			return (gegen < 0f) ? (magnet - uh * gegen) : magnet;
		}

		/// <summary>
		/// Ein Magnetbeitrag (Grad, Bildebene) zu einer Zone: Schritt zum
		/// Zielpunkt = min(Deckel x dt, Winkel x (1 - e^(-dt/tau))), mal Verlauf
		/// (1 innen, zum Radiusrand auf 0) und mal dem Nachlassen beim Wegziehen.
		/// </summary>
		private static Vector2 MagnetTeil(Vector2 e, float winkel, float verlauf, float anteil, float max, float dt, Vector2 mausRicht, float mausTempo)
		{
			if (verlauf <= 0f || winkel <= 0f)
			{
				return Vector2.zero;
			}
			float nachlass = 1f;
			if (mausTempo > 0f)
			{
				float g = Mathf.Min(1f, 1f + Vector2.Dot(mausRicht, e));
				nachlass = 1f - (1f - g) * Mathf.Min(1f, mausTempo / max);
			}
			float schritt = Mathf.Min(max * dt, winkel * anteil) * verlauf * nachlass;
			return e * schritt;
		}

		/// <summary>Lage im Moment des Schusses frisch bestimmen (fuer die Aufzeichnung).</summary>
		public static void SchussLage(Player ich)
		{
			LageBestimmen(ich, out _, out _);
		}

		/// <summary>
		/// Eine Protokollzeile je Schuss (aus dem Schuss-Abgriff). Rechnet die
		/// Lage frisch, im Moment des Schusses.
		/// </summary>
		public static string Schusszeile(Player ich, Camera kamera)
		{
			bool lage = LageBestimmen(ich, out _, out _);
			string kamText = "";
			if (kamera != null && Rueckstoss.Schusslinie(ich, out _, out var richtung))
			{
				Vector3 blick = kamera.transform.forward;
				kamText = $" | muzzle vs. camera {Mathf.DeltaAngle(Rueckstoss.Gieren(blick), Rueckstoss.Gieren(richtung)):0.00}°/"
					+ $"{Mathf.DeltaAngle(Rueckstoss.Nicken(blick), Rueckstoss.Nicken(richtung)):0.00}°";
			}
			string zielText = !lage ? "no target"
				: $"{_entfernung:0.0} m, zone {ZoneName}, edge {Rand:0.000}°, angle to aim point {Winkel:0.000}°, in radius {(ImRadius ? "yes" : "no")} (radius {RadiusDiag:0.00}° = {RadiusMeterDiag:0.00} m, {Einstellungen.SoftRadiusArt.Value})"
				  + ((_wunsch >= 0) ? $", desired zone {_zonen[_wunsch].Name} edge {RandWunsch:0.000}°" : "")
				  + $", sight {SichtGrund}"
				  + $", drop compensation {Zielpunkt.LetzterFall * 100f:0} cm = {Mathf.Atan2(Zielpunkt.LetzterFall, Mathf.Max(1f, Zielpunkt.LetzteEntfernung)) * 57.29578f:0.000}° at {Zielpunkt.LetzteEntfernung:0} m"
				  + $", spread {Panzerung.Streuung:0.000}°, unrest {Unruhe:0.000}°";
			return $"[Zielhilfe] SHOT t={Time.time:0.000} frame={Time.frameCount} | {Kopfzeile(ich)} | {zielText}"
				+ $" | mouse raw {RohGrad.x:0.000}/{RohGrad.y:0.000}° -> corrected {KorrGrad.x:0.000}/{KorrGrad.y:0.000}° ({Time.frameCount - _diagBild} frame(s) ago)"
				+ $" | magnet {MagnetGrad:0.0}°/s, weight {Gewicht:0.00}, free zone {Frei:0.00}" + kamText
				+ (ImAnschlag(ich) ? " | ADS" : " | hip");
		}

		public static string Diagnose()
		{
			if (Time.frameCount - _diagBild > 1)
			{
				return " | SOFT AIM MOUSE: input not queried | " + Kopfzeile(Spielzugriff.Ich);
			}
			if (!HatZiel)
			{
				return " | SOFT AIM MOUSE: no target | " + Kopfzeile(Spielzugriff.Ich);
			}
			return $" | SOFT AIM MOUSE: {Kopfzeile(Spielzugriff.Ich)} | {_entfernung:0.0} m, zone {ZoneName}, edge {Rand:0.000}° "
				+ ((RandWunsch >= 0f && _wunsch >= 0) ? $"(desired zone {_zonen[_wunsch].Name} {RandWunsch:0.000}°) " : "(desired zone missing) ")
				+ $"in radius {(ImRadius ? "yes" : "no")} (radius {RadiusDiag:0.00}° = {RadiusMeterDiag:0.00} m from edge, {Einstellungen.SoftRadiusArt.Value}), "
				+ $"angle to aim point {Winkel:0.000}°, weight {Gewicht:0.00}, magnet {MagnetGrad:0.0}°/s, "
				+ (Einstellungen.SoftFreizone.Value
					? $"free zone {Frei:0.00} ({_freiQuote * 100f:0} % of frames, 5 s){(_ankerHat ? $", hold point {_anker.magnitude:0.000}° from aim point" : "")}, "
					: "free zone off, ")
				+ $"mouse raw {RohGrad.x:0.000}/{RohGrad.y:0.000}° -> corrected {KorrGrad.x:0.000}/{KorrGrad.y:0.000}°";
		}

		public static void FehlerMelden(Exception ex)
		{
			if (!_fehlerGemeldet)
			{
				_fehlerGemeldet = true;
				Protokoll?.LogWarning("[Zielhilfe] Soft Aim (mouse): " + ex);
			}
		}
	}

	internal delegate void AchsenUebersetzer(ref float[] axes);

	/// <summary>
	/// Der Abgriff der Mausbewegung.
	///
	/// Im Spiel nachgesehen (Assembly-CSharp 4.1.6): InputManager sammelt die
	/// Achsen (EAxis: MoveX, MoveY, TurnX=2, TurnY=3, LookX=4, LookY=5, LeanX)
	/// und schickt sie durch GamePlayerOwner.TranslateAxes ->
	/// PlayerOwner.TranslateAxes -> MoveInputTranslator.TranslateAxes, und dort:
	///   _controller.Rotate(new Vector2(_xDirection * axes[2], _yDirection * axes[3]));
	/// Player.Rotate -> MovementState.Rotate -> ClampRotation (mal
	/// GetRotationMultiplier und StateSensitivity) -> MovementContext.Rotation +=.
	/// Ein Prefix hier sieht also genau die Mausdrehung des Spielers - nicht die
	/// der Bots (die rufen Player.Rotate aus BotSteering direkt) und nicht das
	/// Umsehen mit der mittleren Maustaste (axes[4]/[5] -> Player.Look). Bei
	/// offenem Inventar/Menue setzt UIScreen.TranslateAxes axes = null, die
	/// Kette endet vorher.
	/// </summary>
	[HarmonyPatch]
	internal static class MausEingabe
	{
		[HarmonyTargetMethod]
		internal static MethodBase Ziel()
		{
			// Ziel ueber einen Methodenzeiger statt als Zeichenkette: aendert ein
			// Spiel-Update Name oder Signatur, scheitert schon der Bau. Die
			// Instanz ohne Konstruktor dient nur dazu, den Zeiger bilden zu
			// koennen; aufgerufen wird nichts.
			MoveInputTranslator leer = (MoveInputTranslator)FormatterServices.GetUninitializedObject(typeof(MoveInputTranslator));
			AchsenUebersetzer zeiger = leer.TranslateAxes;
			return zeiger.Method;
		}

		[HarmonyPrefix]
		internal static void Vorher(MoveInputTranslator __instance, ref float[] axes)
		{
			try
			{
				if (axes == null || axes.Length < 4 || __instance == null)
				{
					return;
				}
				if (__instance._controller != null && __instance._controller.IsYourPlayer)
				{
					Aufzeichnung.MausRoh(axes[2], axes[3], __instance._xDirection, __instance._yDirection);
				}
				if (!Einstellungen.An.Value || Einstellungen.Art.Value != Zielmodus.SoftAimMouse || AfkWache.Aktiv)
				{
					return;
				}
				Player ich = __instance._controller;
				if (ich == null || !ich.IsYourPlayer || ich.MovementContext == null
					|| ich.MovementContext.IsAxesIgnored)
				{
					return;
				}
				if (!Spielzugriff.ImRaid || (object)Spielzugriff.Ich != ich)
				{
					return;
				}
				if (Einstellungen.SoftNurAnschlag.Value && !SoftAim.ImAnschlag(ich))
				{
					return;
				}
				SoftAim.EigeneDrehungMessen(ich);
				if (!SoftAim.WaffeBereit(ich, out _))
				{
					SoftAim.RcsVergessen();
					return;
				}
				int sx = __instance._xDirection;
				int sy = __instance._yDirection;
				Vector2 d = new Vector2(sx * axes[2], sy * axes[3]);
				bool geaendert = SoftAim.Korrigieren(ich, ref d);
				// Rueckstoss-Ausgleich: unabhaengig vom Ziel, NACH der Korrektur
				// dazugelegt - er ist keine eigene Mausbewegung und wird weder
				// umgebogen noch gebremst.
				Vector2 rcs = SoftAim.RcsSchritt(ich);
				if (rcs != Vector2.zero)
				{
					d += rcs;
					geaendert = true;
				}
				if (geaendert)
				{
					// _xDirection/_yDirection sind +1 oder -1 - sie sind ihr
					// eigener Kehrwert.
					axes[2] = sx * d.x;
					axes[3] = sy * d.y;
				}
				Aufzeichnung.MausFertig(axes[2], axes[3]);
			}
			catch (Exception ex)
			{
				SoftAim.FehlerMelden(ex);
			}
		}
	}
	/// <summary>
	/// Je Schuss eine Protokollzeile (nur bei "Diagnose ins Protokoll").
	/// Player.FirearmController.Shot ist die Stelle, an der das Spiel Start und
	/// Richtung der Kugel festlegt (Player.cs, FirearmController.Shot ->
	/// AdjustShotVectors -> InitiateShot); der Prefix sieht denselben Stand.
	/// </summary>
	[HarmonyPatch]
	internal static class SchussAbgriff
	{
		[HarmonyTargetMethod]
		internal static MethodBase Ziel()
		{
			// Ausdruck statt Zeichenkette: aendert sich Name oder Signatur,
			// scheitert der Bau. Shot ist nicht virtuell.
			return SymbolExtensions.GetMethodInfo((Player.FirearmController f) => f.Shot(null, null, 0, false));
		}

		[HarmonyPrefix]
		internal static void Vorher(Player.FirearmController __instance, Weapon weapon, Ammo ammo)
		{
			try
			{
				Player ich = Spielzugriff.Ich;
				if (ich == null || (object)ich.HandsController != __instance)
				{
					return;
				}
				// Oeffnet das Fenster des Rueckstoss-Ausgleichs (unabhaengig von
				// der Diagnose).
				SoftAim.EigenerSchuss();
				// Vor dem Merken: womit hat das Plugin bis eben gerechnet?
				AmmoTemplate benutzt = Spielzugriff.Patrone(ich, out _);
				AmmoTemplate vorhergesagt = Spielzugriff.Vorhersage(ich);
				Spielzugriff.Gefeuert(weapon, ammo);
				KugelAbgriff.Erwartet = true;
				// Akte fuer den Einschlag (Ziel, Kopf/Rumpf, angewandter Vorhalt) -
				// nur Diagnose, Einschlag.cs.
				int nr = Einschlaege.Schuss(ich);
				Aufzeichnung.Schuss(ich, weapon, ammo?.Template as AmmoTemplate, benutzt, vorhergesagt, nr);
				if (!Einstellungen.Diagnose.Value || !Einstellungen.An.Value
					|| Einstellungen.Art.Value != Zielmodus.SoftAimMouse)
				{
					return;
				}
				Panzerung.StreuungAuffrischen(ich);
				SoftAim.Protokoll?.LogInfo(SoftAim.Schusszeile(ich, Spielzugriff.Kamera) + Einschlaege.VorhaltText(nr));
			}
			catch (Exception ex)
			{
				SoftAim.FehlerMelden(ex);
			}
		}
	}

	/// <summary>
	/// Je Treffer der EIGENEN Kugel auf einen Koerper eine Protokollzeile. Damit
	/// laesst sich jede SCHUSS-Zeile hinterher einem Treffer zuordnen (Zeit) -
	/// oder eben keinem. BodyPartCollider.ApplyHit ist die Stelle, an der das
	/// Spiel den Treffer an den Spieler weitergibt; ArmorPlateCollider
	/// ueberschreibt sie fuer die Panzerplatten und ruft die Basis nicht auf.
	/// </summary>
	[HarmonyPatch]
	internal static class TrefferAbgriff
	{
		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Ziele()
		{
			// Ueberschriebene Methoden: ein Ausdruck lieferte die Basisklasse
			// (BallisticCollider), deshalb hier je Klasse per nameof und
			// Parametertypen, und nur, wenn die Klasse sie selbst deklariert.
			Type[] parameter = { typeof(DamageInfo), typeof(ShotId) };
			foreach (Type t in new[] { typeof(BodyPartCollider), typeof(ArmorPlateCollider) })
			{
				MethodInfo m = t.GetMethod(nameof(BodyPartCollider.ApplyHit), parameter);
				if (m != null && m.DeclaringType == t)
				{
					yield return m;
				}
			}
		}

		[HarmonyPostfix]
		internal static void Nachher(BodyPartCollider __instance, DamageInfo damageInfo)
		{
			try
			{
				Player ich = Spielzugriff.Ich;
				if (ich == null)
				{
					return;
				}
				Player opfer = __instance.playerBridge?.iPlayer as Player;
				object schuetze = damageInfo.Player?.iPlayer;
				if ((object)opfer == ich)
				{
					// AFK-Wache: wer mich trifft, wird zum Angreifer.
					AfkWache.Getroffen(schuetze as Player);
					Aufzeichnung.GetroffenWorden(ich, __instance, schuetze as Player);
					return;
				}
				if (damageInfo.Player == null || schuetze != (object)ich)
				{
					return;
				}
				Aufzeichnung.Treffer(ich, __instance, opfer);
				if (!Einstellungen.Diagnose.Value || Einstellungen.Art.Value != Zielmodus.SoftAimMouse)
				{
					return;
				}
				string platte = (__instance is ArmorPlateCollider ap) ? (" plate " + ap.ArmorPlateColliderType) : "";
				SoftAim.Protokoll?.LogInfo($"[Zielhilfe] HIT t={Time.time:0.000} frame={Time.frameCount} | {__instance.BodyPartColliderType}{platte}"
					+ $" | on {(opfer != null ? opfer.Profile?.Nickname : "?")}"
					+ ((opfer != null) ? $" at {Vector3.Distance(ich.Position, opfer.Position):0.0} m" : ""));
			}
			catch (Exception ex)
			{
				SoftAim.FehlerMelden(ex);
			}
		}
	}
	/// <summary>
	/// Einschlaege der EIGENEN Kugeln in der Umgebung (Wand, Boden, Deckung).
	/// Beleg EFT.Ballistics.Shot.HandleCollision: setzt bei jedem Einschlag
	/// HittedBallisticCollider und den Treffpunkt und ruft am Ende
	/// CreateFragments(), das ueber den Ausgang entscheidet: BulletState StopHit =
	/// gestoppt, RicochetHit = abgeprallt, FragmentationHit/DeviationHit oder
	/// weiter Flying = durchschlagen. Ein Postfix sieht also Kollider, Ort und
	/// Ausgang. Laeuft nur bei Einschlaegen (nicht je Flugschritt); eigene Kugeln
	/// ueber Shot.PlayerProfileID; nur der ERSTE Einschlag je Schuss (FireIndex,
	/// Hauptkugel ohne Parent). Koerpertreffer stehen schon als TREFFER da.
	/// </summary>
	[HarmonyPatch]
	internal static class EinschlagAbgriff
	{
		private static readonly HashSet<int> _gesehen = new HashSet<int>();

		private static string _ichId = "";

		private static Player _ichFuer;

		[HarmonyTargetMethod]
		internal static MethodBase Ziel()
		{
			return SymbolExtensions.GetMethodInfo((EFT.Ballistics.Shot s) => s.HandleCollision(0f, Vector3.zero, Vector3.zero));
		}

		[HarmonyPostfix]
		internal static void Nachher(EFT.Ballistics.Shot __instance)
		{
			try
			{
				if (__instance == null)
				{
					return;
				}
				Player ich = Spielzugriff.Ich;
				if (ich == null)
				{
					return;
				}
				if ((object)_ichFuer != ich)
				{
					_ichFuer = ich;
					_ichId = ich.Profile?.ProfileId ?? "";
					_gesehen.Clear();
				}
				if (_ichId.Length == 0 || __instance.PlayerProfileID != _ichId)
				{
					return;
				}
				// Jeder Einschlag der eigenen Kugel, auch Koerper und Splitter
				// (Einschlag.cs, EINSCHLAG-Zeile und "ergebnis" je Schuss).
				Einschlaege.Kollision(__instance);
				if (__instance.Parent != null)
				{
					return;
				}
				if (_gesehen.Contains(__instance.FireIndex))
				{
					return;
				}
				if (_gesehen.Count > 512)
				{
					_gesehen.Clear();
				}
				_gesehen.Add(__instance.FireIndex);
				BallisticCollider bc = __instance.HittedBallisticCollider;
				if (bc is BodyPartCollider)
				{
					return;
				}
				bool diag = Einstellungen.Diagnose.Value;
				bool aufz = Einstellungen.KampfAufzeichnung.Value;
				if (!diag && !aufz)
				{
					return;
				}
				Vector3 punkt = __instance.HitPoint;
				Collider kol = __instance.HitCollider;
				string name = (kol != null) ? kol.name : "?";
				string material = (bc != null) ? bc.TypeOfMaterial.ToString() : "?";
				EFT.Ballistics.Shot.EBulletState zustand = __instance.BulletState;
				bool abgeprallt = zustand == EFT.Ballistics.Shot.EBulletState.RicochetHit;
				bool durch = zustand != EFT.Ballistics.Shot.EBulletState.StopHit && !abgeprallt;
				float entfernung = Vector3.Distance(ich.Position, punkt);
				Player ziel = SoftAim.AktuellesZiel;
				float vorZiel = (ziel != null) ? Vector3.Distance(punkt, Zielpunkt.Koerper(ziel)) : -1f;
				// Die EINSCHLAG-Zeile im Protokoll schreibt seit 03.11. Einschlag.cs,
				// je Schuss eine (mit Schussnummer, auch Koerper und Fehlschuss).
				if (aufz)
				{
					Aufzeichnung.Einschlag(name, material, durch, abgeprallt, punkt, entfernung, vorZiel);
				}
			}
			catch (Exception ex)
			{
				SoftAim.FehlerMelden(ex);
			}
		}
	}
	/// <summary>
	/// Ende einer Kugel: BallisticsCalculator.UpdateShots gibt fertige Kugeln
	/// (IsShotAndParentFinished, alle Splitter fertig) ueber Shot.Release an
	/// Shot.ReturnShotToPool zurueck; dort loescht PrepareToPool danach
	/// PlayerProfileID, PositionHistory und Fragments. Der Prefix sieht die
	/// Hauptkugel also ein letztes Mal vollstaendig - auch eine, die nie etwas
	/// getroffen hat. Laeuft fuer alle Kugeln aller Spieler, darum sofortiger
	/// Ausstieg, wenn keine eigene Akte offen ist.
	/// </summary>
	[HarmonyPatch]
	internal static class KugelEndeAbgriff
	{
		[HarmonyTargetMethod]
		internal static MethodBase Ziel()
		{
			return SymbolExtensions.GetMethodInfo(() => EFT.Ballistics.Shot.ReturnShotToPool(null));
		}

		[HarmonyPrefix]
		internal static void Vorher(EFT.Ballistics.Shot shot)
		{
			try
			{
				Einschlaege.Freigabe(shot);
			}
			catch (Exception ex)
			{
				SoftAim.FehlerMelden(ex);
			}
		}
	}

	/// <summary>
	/// Echte Startrichtung der Kugel MIT Streuung. Beleg Player.FirearmController
	/// .Shot: nach AdjustShotVectors wird Richtung x 100 + Streuung x
	/// Zufallspunkt gebildet und als shotDirection an InitiateShot uebergeben.
	/// ClientFirearmController ueberschreibt InitiateShot; beide werden
	/// abgegriffen, gezaehlt wird nur der erste Aufruf je Schuss.
	/// </summary>
	[HarmonyPatch]
	internal static class KugelAbgriff
	{
		internal static bool Erwartet;

		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Ziele()
		{
			Type[] parameter = { typeof(IWeapon), typeof(Ammo), typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(int), typeof(float) };
			foreach (Type t in new[] { typeof(Player.FirearmController), typeof(ClientFirearmController) })
			{
				MethodInfo m = t.GetMethod(nameof(Player.FirearmController.InitiateShot), parameter);
				if (m != null && m.DeclaringType == t)
				{
					yield return m;
				}
			}
		}

		[HarmonyPrefix]
		internal static void Vorher(Player.FirearmController __instance, Ammo ammo, Vector3 shotPosition, Vector3 shotDirection)
		{
			try
			{
				// AFK-Wache: fremde Kugeln, die knapp an mir vorbeigehen
				// (Beinahetreffer), machen den Schuetzen ebenfalls zum Angreifer.
				if (AfkWache.Aktiv)
				{
					AfkWache.FremdeKugel(__instance, shotPosition, shotDirection);
				}
				if (!Erwartet)
				{
					return;
				}
				Player ich = Spielzugriff.Ich;
				if (ich == null || (object)ich.HandsController != __instance)
				{
					return;
				}
				Erwartet = false;
				Aufzeichnung.Kugelstart(shotPosition, shotDirection, ammo);
				Einschlaege.Kugel(shotPosition, shotDirection);
			}
			catch (Exception ex)
			{
				SoftAim.FehlerMelden(ex);
			}
		}
	}
}
