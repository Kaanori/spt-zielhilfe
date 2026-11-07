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
		TasteHalten,
		TasteUmschalten,
		Immer
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
		Meter,
		Grad
	}
	internal enum Zielmodus
	{
		SoftaimMaus,
		AltesSoftaim,
		AltesFesthalten
	}
	/// <summary>
	/// Wird vom ConfigurationManager (F12) per Reflexion ueber Klassen- und
	/// Feldnamen gelesen - kein Verweis auf dessen DLL noetig.
	/// </summary>
	internal sealed class ConfigurationManagerAttributes
	{
		public bool? IsAdvanced;

		public int? Order;
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
			case Knochen.Kopf:
				return ZoneKopf.Value;
			case Knochen.Brust:
				return ZoneRumpf.Value;
			case Knochen.Becken:
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
			if (SoftRadiusArt.Value == Radiusart.Grad)
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


		public static ConfigEntry<bool> DurchschussIgnorieren;

		public static ConfigEntry<float> DurchschussSchwelle;

		public static ConfigEntry<bool> NurKugelEbenen;

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
			return new ConfigDescription("[nur alte Arten] " + text, null,
				new ConfigurationManagerAttributes { IsAdvanced = true });
		}

		private static ConfigDescription Alt(ConfigDescription d)
		{
			return new ConfigDescription("[nur alte Arten] " + d.Description, d.AcceptableValues,
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
		/// Ein Eintrag hat die Sektion gewechselt: den Wert aus dem alten Platz
		/// uebernehmen (einmalig, dann ist der alte Platz verbraucht).
		/// </summary>
		private static void Uebernehmen<T>(ConfigFile c, string altSektion, ConfigEntry<T> e)
		{
			if (altSektion == e.Definition.Section)
			{
				return;
			}
			Dictionary<ConfigDefinition, string> w = Waisen(c);
			ConfigDefinition alt = new ConfigDefinition(altSektion, e.Definition.Key);
			if (w != null && w.TryGetValue(alt, out var wert))
			{
				e.SetSerializedValue(wert);
				w.Remove(alt);
			}
		}

		private static void KnochenErsetzen(ConfigFile c)
		{
			Dictionary<ConfigDefinition, string> w = Waisen(c);
			ConfigDefinition d = new ConfigDefinition("2 Ziel", "Kopf-Trefferbox");
			if (w != null && w.TryGetValue(d, out var wert) && wert.Trim() == "Knochen")
			{
				w[d] = Kopfbox.Sichtbar.ToString();
			}
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
			KlasseEigen[i] = c.Bind(sektion, "Eigene Werte statt allgemeiner", eigen, Reihe(warum, 100));
			KlasseMeter[i] = c.Bind(sektion, "Radius in Metern ab Zonenrand", meter,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse (Radius-Art Meter).", new AcceptableValueRange<float>(0.05f, 5f)));
			KlasseStaerke[i] = c.Bind(sektion, "Anziehung (Gewicht)", staerke,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseReibung[i] = c.Bind(sektion, "Reibung", reibung,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseMagnet[i] = c.Bind(sektion, "Halten ohne Maus (Magnet) hoechstens x Grad je Sekunde", magnet,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0f, 60f)));
			KlasseMagnetZeit[i] = c.Bind(sektion, "Magnet Nachziehzeit in Sekunden", zeit,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0.02f, 1f)));
			KlasseMitAnteil[i] = c.Bind(sektion, "Mitdrehen Anteil", 0.7f,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseMitDeckel[i] = c.Bind(sektion, "Mitdrehen hoechstens x Grad je Sekunde", 60f,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0f, 200f)));
			KlasseSchubDauer[i] = c.Bind(sektion, "Einfang-Schub Dauer (s)", 0.3f,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0f, 1f)));
			KlasseSchubMax[i] = c.Bind(sektion, "Einfang-Schub hoechstens x Grad je Sekunde", 40f,
				new ConfigDescription("Wie in \"1b\", nur fuer diese Klasse.", new AcceptableValueRange<float>(0f, 150f)));
		}

		public static void Anlegen(ConfigFile c)
		{
			Datei = c;
			KampfAufzeichnung = c.Bind("8 Diagnose", "Kampf-Aufzeichnung", defaultValue: false, Reihe(
				"Schreibt jedes Gefecht bildgenau mit (eigener Spieler, Maus samt allen "
				+ "Korrekturanteilen, Gegner mit Trefferzonen, Panzerung und KI-Absicht, Schuesse, "
				+ "Treffer) nach BepInEx/Zielhilfe-Aufzeichnung/<Datum>-<Karte>/, je Gefecht eine "
				+ "gzip-Datei (JSON Lines). Format: AUFZEICHNUNG-FORMAT.md neben dem Quelltext. "
				+ "Geschrieben wird in einem Hintergrund-Faden.", 50));
			AufzMaxMB = c.Bind("8 Diagnose", "Aufzeichnung hoechstens x MB je Raid", 200, Reihe(new ConfigDescription(
				"Danach wird in diesem Raid nichts mehr aufgezeichnet.",
				new AcceptableValueRange<int>(10, 5000)), 49));
			AufzMaxRaids = c.Bind("8 Diagnose", "Aufzeichnung: nur die letzten x Raids behalten", 20, Reihe(new ConfigDescription(
				"Aeltere Raid-Ordner werden beim Start eines neuen Raids geloescht.",
				new AcceptableValueRange<int>(1, 500)), 48));
			AufzSichtMeter = c.Bind("8 Diagnose", "Gefecht beginnt, wenn ein Feind dich sieht, bis x Meter", 150f, Reihe(new ConfigDescription(
				"Ein Gefecht beginnt bei: Ziel im Fangbereich, eigenem Schuss, Treffer auf dich, "
				+ "oder ein Feind hat dich als Ziel und sieht dich (bis zu dieser Entfernung). "
				+ "Es endet 4 s nach der letzten solchen Aktivitaet; 1 s davor wird mitgeschrieben.",
				new AcceptableValueRange<float>(10f, 1000f)), 47));
			An = c.Bind("1 Allgemein", "Eingeschaltet", defaultValue: true, Reihe("Aus = die Zielhilfe ruehrt sich nicht.", 100));
			Modus = c.Bind("9 Alte Arten - Allgemein", "Wann sie zieht", Ausloeser.TasteHalten, Alt("TasteHalten = nur solange die Taste unten ist (empfohlen). TasteUmschalten = Taste schaltet an und aus. Immer = dauerhaft."));
			Uebernehmen(c, "1 Allgemein", Modus);
			Taste = c.Bind("1 Allgemein", "Taste", KeyCode.LeftAlt, Reihe("Fuer \"Wann sie hilft\" = TasteHalten oder TasteUmschalten (und fuer die alten Arten).", 90));
			Diagnose = c.Bind("8 Diagnose", "Diagnose ins Protokoll", defaultValue: false, "An = schreibt im Diagnose-Takt eine Zeile ins BepInEx-Protokoll, bei Soft Aim per Maus dazu je eigenem Schuss eine SCHUSS- und je Treffer eine TREFFER-Zeile: ob ein Raid laeuft, ob die Taste greift, wie viele Gegner in Frage kamen und woran sie ausgeschieden sind. Nur zur Fehlersuche einschalten.");
			Uebernehmen(c, "1 Allgemein", Diagnose);

			DiagnoseTakt = c.Bind("8 Diagnose", "Diagnose-Takt in Sekunden", 1f, new ConfigDescription(
					"Wie oft eine Diagnosezeile geschrieben wird. 1 Sekunde reicht, um zu "
					+ "sehen ob etwas grundsaetzlich klemmt. Zum Untersuchen einzelner "
					+ "Schuesse VIEL kleiner stellen - 0 = jedes Bild. Dann sieht man, ob "
					+ "der Zielpunkt wirklich springt oder ob nur das Messraster zu grob "
					+ "war. Achtung: bei 0 waechst das Protokoll schnell.",
					new AcceptableValueRange<float>(0f, 5f)));
			Uebernehmen(c, "1 Allgemein", DiagnoseTakt);

			// --- Soft Aim per Mauseingabe ------------------------------------
			Art = c.Bind("1b Soft Aim per Maus", "Art der Hilfe", Zielmodus.SoftaimMaus, Reihe(new ConfigDescription(
					"SoftaimMaus = echtes Soft Aim (empfohlen). Die Hilfe dreht nie von "
					+ "selbst. Sie faengt deine Mausbewegung ab und biegt sie ein Stueck "
					+ "Richtung Ziel, solange der Treffpunkt der Waffe innerhalb des "
					+ "Radius um die Trefferzone liegt. Maus still = keine Korrektur.\n"
					+ "AltesSoftaim = der fruehere Modus \"Softaim statt Festhalten\": "
					+ "jedes Bild eine gedeckelte Drehung aufs Ziel, auch ohne Maus.\n"
					+ "AltesFesthalten = die Blickrichtung wird jedes Bild aufs Ziel gesetzt.\n"
					+ "Ihre Regler stehen in den Sektionen \"9 Alte Arten\" (nur mit "
					+ "\"Advanced settings\" sichtbar); fuer SoftaimMaus gilt diese Sektion (dazu "
					+ "Zielwahl, Sichtpruefung und Auslassen aus \"2 Ziel\"/\"3\" und der "
					+ "Vorhalt aus \"5\")."), 100));
			SoftWann = c.Bind("1b Soft Aim per Maus", "Wann sie hilft", Ausloeser.Immer, Reihe(new ConfigDescription(
					"Immer = dauerhaft bereit. Unbedenklich, weil ohne eigene Mausbewegung "
					+ "nichts passiert. TasteHalten / TasteUmschalten = wie bisher mit der "
					+ "Taste aus \"1 Allgemein\"."), 95));
			SoftNurAnschlag = c.Bind("1b Soft Aim per Maus", "Nur im Anschlag (ADS)", defaultValue: false, Reihe("An = nur beim Anvisieren. Aus = auch aus der Huefte. Gerechnet wird in "
				+ "beiden Faellen mit der Laufrichtung der Waffe, nicht mit der Kamera - "
				+ "aus der Huefte steht die Muendung einige Grad neben der Bildmitte, "
				+ "und die Hilfe zielt dahin, wo die Kugel hinfliegt.", 90));
			ZoneNachPanzerung = c.Bind("1b Soft Aim per Maus", "Zone nach Panzerung waehlen", defaultValue: true, Reihe("An = statt der festen Koerperstelle die Zone mit dem besten erwarteten "
				+ "Treffer: je Gegner wird die angelegte Panzerung abgefragt (dieselbe "
				+ "Rechnung wie das Spiel: ShotMatches + Durchschlagchance aus Panzerklasse, "
				+ "Haltbarkeit und Durchschlag der geladenen Patrone), dazu Blickrichtung "
				+ "des Gegners (Brust-/Rueckenplatte, Seiten). Gewichtet Kopf 1,0, Rumpf 0,6, "
				+ "Becken 0,25 (Kopf und Brustkorb sind toedlich, der Bauch nicht). Am Kopf "
				+ "wird die am wenigsten gepanzerte FREIE Kopfbox genommen (z. B. Gesicht "
				+ "ohne Visier). Aus = Koerperstelle aus \"2 Ziel\" wie bisher.", 85));
			ZoneKopf = c.Bind("1b Soft Aim per Maus", "Erlaubte Zone: Kopf", defaultValue: true, Reihe(
				"Darf die Hilfe auf den Kopf ziehen (Zonenwahl, Rueckfall-Zonen).", 84));
			ZoneRumpf = c.Bind("1b Soft Aim per Maus", "Erlaubte Zone: Rumpf", defaultValue: true, Reihe(
				"Darf die Hilfe auf den Brustkorb ziehen.", 83));
			ZoneBecken = c.Bind("1b Soft Aim per Maus", "Erlaubte Zone: Becken", defaultValue: false, Reihe(
				"Aus = Becken nur als Ausweich, wenn Kopf UND Rumpf verdeckt oder von der Patrone "
				+ "nicht durchschlagbar sind (dann zuerst Becken, dann Beine).", 82));
			ZoneBeine = c.Bind("1b Soft Aim per Maus", "Erlaubte Zone: Beine", defaultValue: false, Reihe(
				"Aus = Beine nur als letzter Ausweich (Kopf und Rumpf nicht moeglich, Becken auch nicht). "
				+ "Ein Beintreffer toetet nicht; mit Gewicht 0,1 (rund 10 Treffer bis zum Kill) gewinnen "
				+ "sie sonst nur, wenn der Kopf fast nie getroffen wird.", 81));
			NurFeinde = c.Bind("1b Soft Aim per Maus", "Nur Feinde", defaultValue: true, Reihe("An = nur Bots, deren Gruppe dir gegenueber feindlich ist - die Spiellogik "
				+ "selbst (BotsGroup: als Verbuendeter gefuehrt = nie, bereits als Feind "
				+ "eingetragen = ja, sonst IsPlayerEnemy: Seite, Karma, Boss-Treue, "
				+ "Freund-/Warn-/Feindtypen der Bot-Einstellungen). Als Scav bleiben "
				+ "friedliche Scavs also aussen vor, bis sie feindlich werden. Gilt fuer alle "
				+ "Arten. \"3 Wen auslassen\" wirkt zusaetzlich.", 80));
			RcsQuer = c.Bind("1b Soft Aim per Maus", "Rueckstoss-Ausgleich quer (Prozent)", 0f, Reihe(new ConfigDescription(
					"Zieht nur beim Schiessen den NEU hinzugekommenen Rueckstoss der Waffe ab, "
					+ "unabhaengig von einem Ziel, ueber die Mausbewegung (nicht die Kamera "
					+ "setzen). Gemessen wird, wie weit sich die Schusslinie gegenueber der "
					+ "Koerperdrehung seit dem letzten Bild verschoben hat, solange der "
					+ "Rueckstoss der Waffe (HandRotationRecoil + WeaponRecoil) wirkt. 0 = aus "
					+ "(Standard, weil du den Rueckstoss bisher selbst ziehst - beides zusammen "
					+ "wuerde ueberziehen). Zum Probieren 50.",
					new AcceptableValueRange<float>(0f, 100f)), 20));
			RcsHoch = c.Bind("1b Soft Aim per Maus", "Rueckstoss-Ausgleich hoch (Prozent)", 0f, Reihe(new ConfigDescription(
					"Wie quer, fuer den senkrechten Anteil (bei EFT der groessere). 0 = aus. "
					+ "Zum Probieren 50.",
					new AcceptableValueRange<float>(0f, 100f)), 25));
			KlasseAnlegen(c, Waffenklasse.Pistole, "1c Klasse Pistole", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Aus = allgemeine Werte aus \"1b\". Pistolen: kurze Entfernung, die Obergrenze in "
				+ "Grad greift ohnehin - kein Grund zur Abweichung.");
			KlasseAnlegen(c, Waffenklasse.MP, "1d Klasse MP", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Aus = allgemeine Werte aus \"1b\".");
			KlasseAnlegen(c, Waffenklasse.Sturmgewehr, "1e Klasse Sturmgewehr und Karabiner", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Aus = allgemeine Werte aus \"1b\" - die sind an diesem Fall (10-80 m, "
				+ "Dauer- und Einzelfeuer) ausgelegt.");
			KlasseAnlegen(c, Waffenklasse.Praezision, "1f Klasse DMR und Scharfschuetze", true, 0.5f, 0.5f, 0.4f, 10f, 0.05f,
				"Ab Werk AN: Einzelschuesse auf grosse Entfernung. Nachziehzeit wie allgemein "
				+ "0,05 s (Simulation Stillhalten: 30 m 96 %, 80 m 38 % Kopftreffer). Radius 0,5 m, weil auf "
				+ "Entfernung ohnehin die Untergrenze in Grad greift und nahe Nachbarn nicht "
				+ "mitgefangen werden sollen; Reibung 0,4 fuer das feine Ablegen im Glas.");
			KlasseAnlegen(c, Waffenklasse.Schrot, "1g Klasse Schrotflinte", true, 1f, 0.5f, 0.3f, 10f, 0.05f,
				"Ab Werk AN: Schrot streut (buckshotDispersion), ein knapper Fehlschuss "
				+ "trifft oft noch mit einem Teil der Kugeln - der Fangbereich darf weiter "
				+ "sein (1 m statt 0,75 m). Bei Flintenlaufgeschossen ggf. aus.");
			KlasseAnlegen(c, Waffenklasse.MG, "1h Klasse MG", false, 0.75f, 0.5f, 0.3f, 10f, 0.05f,
				"Aus = allgemeine Werte aus \"1b\".");
			SoftRadiusArt = c.Bind("1b Soft Aim per Maus", "Radius-Art", Radiusart.Meter, Reihe(new ConfigDescription(
					"Meter = der Fangbereich ist ein ABSTAND am Gegner (\"Radius in Metern "
					+ "ab Zonenrand\"), umgerechnet mit der Entfernung jedes Gegners: "
					+ "Radius in Grad = atan(Meter / Entfernung). Ein fester Winkel ist nah "
					+ "zu eng und fern zu weit: 3 Grad sind auf 3 m nur 16 cm, auf 80 m aber "
					+ "4,2 m. Grad = wie bisher der feste Wert \"Radius in Grad\"."), 75));
			SoftRadiusMeter = c.Bind("1b Soft Aim per Maus", "Radius in Metern ab Zonenrand", 0.75f, Reihe(new ConfigDescription(
					"Nur fuer Radius-Art Meter. Wie weit neben der Trefferzone die Hilfe noch "
					+ "greift, in Metern am Gegner. Simulation (Schwenk quer am Kopf vorbei, "
					+ "Staerke 0,5, Reibung 0,3): 0,5 m holt nur Linien heran, die bis etwa "
					+ "0,1 m danebenliegen; 0,75 m holt auf 10-80 m auch 0,25 m daneben noch "
					+ "auf den Kopf; 1 m holt 0,5 m daneben auf 0,2 m heran.",
					new AcceptableValueRange<float>(0.05f, 5f)), 70));
			SoftRadiusMaxGrad = c.Bind("1b Soft Aim per Maus", "Radius hoechstens x Grad", 12f, Reihe(new ConfigDescription(
					"Nur fuer Radius-Art Meter. Obergrenze fuer den Nahkampf: auf 1-2 m waeren "
					+ "0,75 m sonst 20-37 Grad, also ein grosser Teil des Bildes. 12 Grad ist "
					+ "ungefaehr ein Sechstel der Bildhoehe zu jeder Seite und immer noch "
					+ "groesser als der Kopf auf 1 m (5,7 Grad); erreicht wird die Grenze ab "
					+ "etwa 3,5 m.",
					new AcceptableValueRange<float>(1f, 30f)), 65));
			SoftRadiusMinGrad = c.Bind("1b Soft Aim per Maus", "Radius mindestens x Grad", 1f, Reihe(new ConfigDescription(
					"Nur fuer Radius-Art Meter. Untergrenze fuer grosse Entfernungen: 0,75 m "
					+ "waeren auf 300 m nur 0,14 Grad - weniger als das Schwanken der Waffe im "
					+ "Anschlag. 1 Grad (vorher 0,3) greift ab etwa 43 m: auf Entfernung faengt "
					+ "die Hilfe dann frueher ein (Simulation 80 m: 90 % der Zeit bis Kopf "
					+ "1,5 -> 1,0 s). Dass der groessere Bereich nicht klebt, regelt \"Loesen "
					+ "nach Weg\".",
					new AcceptableValueRange<float>(0.05f, 5f)), 60));
			SoftRadius = c.Bind("1b Soft Aim per Maus", "Radius in Grad", 3f, Reihe(new ConfigDescription(
					"Nur fuer Radius-Art Grad. "
					+ "Wie nahe der Treffpunkt der Waffe an einer Trefferzone sein muss, "
					+ "damit die Hilfe eingreift - gemessen ab dem RAND der Zone (Kopf, "
					+ "Rumpf, Becken), als Winkel, bleibt also beim Zoomen gleich. Wer im "
					+ "Nahkampf auf den Koerper zielt, ist damit in der Zone und nicht 8 Grad "
					+ "neben dem Kopfpunkt. Zum Rand des Radius hin laeuft die Wirkung auf 0 "
					+ "aus. Gemessen wird vom Feuerpunkt entlang der Schussrichtung, die Zonen "
					+ "sind um Vorhalt und Kugelfall verschoben. Die Zone der eingestellten "
					+ "Koerperstelle hat Vorrang; die naechste andere hilft, solange die "
					+ "eigene ausser Reichweite ist. Ersetzt in dieser Art das Blickfeld der "
					+ "alten Arten.",
					new AcceptableValueRange<float>(0.2f, 15f)), 55));
			SoftStaerke = c.Bind("1b Soft Aim per Maus", "Anziehung (Gewicht)", 0.5f, Reihe(new ConfigDescription(
					"Wie stark die Mausbewegung Richtung Ziel gebogen wird. Gerechnet wird "
					+ "Ergebnis = w x Zielvektor + (1 - w) x Mausbewegung; der Zielvektor "
					+ "zeigt aufs Ziel und ist so lang wie deine eigene Bewegung. Dieser "
					+ "Regler ist w im Inneren des Radius. 0 = aus, 1 = die Bewegung zeigt "
					+ "voll aufs Ziel. Die Hilfe kehrt deine Bewegung nie um: wer vom Ziel "
					+ "wegzieht, kommt immer weg. Sie nimmt sich von selbst zurueck, sobald "
					+ "deine eigene Bewegung schon "
					+ "bis zum Zielpunkt reicht, und innerhalb der Zone laeuft sie zur Mitte "
					+ "hin auf 0 - so schnappt und klebt nichts.",
					new AcceptableValueRange<float>(0f, 1f)), 50));
			SoftReibung = c.Bind("1b Soft Aim per Maus", "Reibung", 0.3f, Reihe(new ConfigDescription(
					"Bremst den Teil deiner Bewegung, der QUER oder WEG vom Ziel geht, "
					+ "solange du im Radius bist - in der Mitte voll, zum Rand auf 0 "
					+ "auslaufend. Die Bewegung zum Ziel hin wird nie gebremst. 0 = aus, "
					+ "0,3 = Standard (spuerbar, in der Simulation nie mehr als 30 % "
					+ "Vortrieb weg), 1 = in der Mitte steht die Querbewegung still "
					+ "(fuehlt sich klebrig an).",
					new AcceptableValueRange<float>(0f, 1f)), 45));
			SoftDeckel = c.Bind("1b Soft Aim per Maus", "Korrektur hoechstens x Grad je Sekunde", 120f, Reihe(new ConfigDescription(
					"Obergrenze fuer das, was die Hilfe an deiner Bewegung aendert "
					+ "(Anziehung und Reibung zusammen). Wird je Bild mit der Bilddauer "
					+ "umgerechnet und haengt damit nicht von der Bildrate ab. Bei schnellen "
					+ "Schwenks greift der Deckel, die Hilfe faellt dann kaum auf.",
					new AcceptableValueRange<float>(1f, 600f)), 40));

			SoftMagnet = c.Bind("1b Soft Aim per Maus", "Halten ohne Maus (Magnet) hoechstens x Grad je Sekunde", 10f, Reihe(new ConfigDescription(
					"Der einzige Teil, der auch bei STILLER Maus wirkt: liegt die "
					+ "Schusslinie im Radius einer Trefferzone, wird sie langsam zum "
					+ "Zielpunkt zurueckgeholt - gegen Schwanken und Rueckstoss. Gemessen "
					+ "wird die echte Flugrichtung der Kugel, der Rueckstoss steckt also "
					+ "schon darin. Hoechstens so viele Grad je Sekunde; zum Radiusrand "
					+ "laeuft er aus, beim Wegziehen laesst er nach. 0 = aus (dann gilt "
					+ "wieder: Maus still = keine Korrektur). In der Simulation: Stillhalten "
					+ "mit Dauerfeuer auf 30 m 0 % Kopftreffer ohne, rund 80 % mit Magnet. "
					+ "Ueber 10 wird es klebrig: schon bei 20 kommt man mit sehr langsamer "
					+ "Mausbewegung (5 Grad/s) nicht mehr vom Kopf weg; ab 10 Grad/s geht "
					+ "es immer.",
					new AcceptableValueRange<float>(0f, 60f)), 35));
			SoftMitAnteil = c.Bind("1b Soft Aim per Maus", "Mitdrehen Anteil", 0.7f, Reihe(new ConfigDescription(
					"Dreht bei bewegten Zielen mit - aber nur die DIFFERENZ: Anteil x (Drehrate "
					+ "der Linie Feuerpunkt-Zielpunkt minus deine eigene Drehrate), beides "
					+ "geglaettet (0,08 s). Fuehrst du schon selbst mit, kommt nichts dazu. Nur im "
					+ "Fangbereich (wie der Magnet); ziehst du von der Zone weg, loest es ab (ab "
					+ "10 Grad/s ganz) - du kommst so schnell weg wie ohne. 0 = aus. Simulation "
					+ "(Kopf je Schuss ohne/mit): Zickzack 30 m 28 -> 34 %, 80 m 19 -> 24 %; "
					+ "geradeaus laufend 80 m 31 -> 37 %; Wegdrehen unveraendert.",
					new AcceptableValueRange<float>(0f, 1f)), 28));
			SoftMitDeckel = c.Bind("1b Soft Aim per Maus", "Mitdrehen hoechstens x Grad je Sekunde", 60f, Reihe(new ConfigDescription(
					"Obergrenze des Mitdrehens. Ein laufender Gegner auf 10 m dreht mit rund "
					+ "25 Grad/s, ein sprintender auf 5 m mit 70.",
					new AcceptableValueRange<float>(0f, 200f)), 27));
			DeckelDurchZoom = c.Bind("1b Soft Aim per Maus", "Deckel durch Zoom teilen", defaultValue: true, Reihe(
				"Alle Korrekturen sind in Grad (richtig fuer den Treffer), im Zielfernrohr erscheinen sie "
				+ "aber um die Vergroesserung groesser: 40 Grad/s Einfang-Schub wirken bei 6x wie 240. An = "
				+ "Einfang-Schub, Mitdrehen und Anziehungs-Deckel werden durch die Vergroesserung geteilt, "
				+ "hoechstens bis auf den Magnet-Deckel. Der Magnet selbst bleibt: ihn zu teilen kostet "
				+ "fast alle Treffer (Simulation Stillhalten mit Dauerfeuer, 30 m: 95 % bei 10 Grad/s, "
				+ "28 % bei 10/4, 7 % bei 10/6) - er traegt die Rueckstoss-Erholung.", 29));
			MagnetWeicht = c.Bind("1b Soft Aim per Maus", "Magnet weicht eigener Maus ab x Grad je Sekunde (Bildschirm)", 10f, Reihe(new ConfigDescription(
				"Steuerst du selbst schneller als dieser Wert - gemessen im Bild (Grad je Sekunde x "
				+ "Vergroesserung) und relativ zur Bewegung des Ziels -, haelt der Magnet nicht mehr GEGEN "
				+ "deine Bewegung (zieht nur noch quer dazu). Bei stiller oder zitternder Maus haelt er voll "
				+ "(Rueckstoss-Erholung). 10 = Grenze zwischen Zittern/Feinkorrektur und Steuern (75 % von "
				+ "Gemessene Mausbewegungen bei 6x lagen darunter). Simulation 80 m, 6x: geschluckte "
				+ "Eigenbewegung beim Wegschwenken 17 -> 2 %, Kopf beim Verfolgen 72 -> 59 %. 0 = aus "
				+ "(mehr Treffer, mehr Gegenhalten), 3 = noch freier (Kopf 42 %).",
				new AcceptableValueRange<float>(0f, 100f)), 30));
			SoftSchubDauer = c.Bind("1b Soft Aim per Maus", "Einfang-Schub Dauer (s)", 0.3f, Reihe(new ConfigDescription(
					"Tritt die Schusslinie in den Fangbereich der Wunschzone ein (auch direkt "
					+ "nach einem Zielwechsel), darf der Magnet fuer diese Zeit staerker ziehen "
					+ "(\"Einfang-Schub hoechstens\"). Bricht sofort ab, wenn du mit mindestens "
					+ "10 Grad/s von der Zone wegziehst oder \"Loesen nach Weg\" greift.",
					new AcceptableValueRange<float>(0f, 1f)), 26));
			SoftSchubMax = c.Bind("1b Soft Aim per Maus", "Einfang-Schub hoechstens x Grad je Sekunde", 40f, Reihe(new ConfigDescription(
					"Magnet-Deckel waehrend des Einfang-Schubs statt des normalen. 0 = aus. "
					+ "Simulation 10 m: 90 % der Zeit bis Kopf nach dem Loslassen 0,72 -> rund 0,4 s.",
					new AcceptableValueRange<float>(0f, 150f)), 25));
			MitOhneHilfe = c.Bind("1b Soft Aim per Maus", "Mitdrehen: Hilfe-Drehung nicht als eigene zaehlen", true, new ConfigDescription(
					"Mitdrehen gleicht die Ziel-Drehrate mit DEINER Drehung ab. An: die Drehung, die die "
					+ "Hilfe selbst macht, zaehlt dabei nicht mit - so dreht sie bei laufenden Gegnern "
					+ "mit voller Geschwindigkeit mit statt nur mit ~41 %. Aus = altes Verhalten."));
			SoftWiedergreifen = c.Bind("1b Soft Aim per Maus", "Nach Loslassen wieder greifen nach x Sekunden", 0.06f, new ConfigDescription(
					"Wurde die Hilfe durch eigene Mausbewegung geloest, greift sie wieder, sobald die Maus "
					+ "so lange ruhig ist - statt zu warten, bis die Weg-Summe abgeklungen ist (bis 0,5 s). "
					+ "Aufzeichnung Interchange 02.11. (6x, 50 m, Gegner laeuft 2-5 m/s): viermal geloest durch "
					+ "kleine Korrekturen, danach 0,3-0,5 s kein Folgen, Abstand wuchs auf ~1 Grad. 0 = aus.",
					new AcceptableValueRange<float>(0f, 0.5f)));
			SoftLoesenWeg = c.Bind("1b Soft Aim per Maus", "Loesen nach Weg (Grad)", 2f, Reihe(new ConfigDescription(
					"Summiert, wie weit du die Maus von der Wunschzone WEG bewegst (zur Zone hin "
					+ "zaehlt dagegen, klingt mit 0,5 s ab). Ab diesem Wert sind Magnet, Mitdrehen "
					+ "und Einfang-Schub ganz aus, bis die Summe unter die Haelfte gefallen ist. "
					+ "Anziehung und Reibung bleiben. Gegen das Kleben beim langsamen "
					+ "Vorbeischwenken (Simulation 5 Grad/s, 10-30 m: von fast immer haengen auf "
					+ "10-22 %). 0 = aus.",
					new AcceptableValueRange<float>(0f, 10f)), 24));
			SoftMagnetZeit = c.Bind("1b Soft Aim per Maus", "Magnet Nachziehzeit in Sekunden", 0.05f, Reihe(new ConfigDescription(
					"Wie schnell der Magnet einen Rest wegholt: nach dieser Zeit sind "
					+ "rund zwei Drittel weg. Klein = haelt fester, gross = weicher. Die "
					+ "Trefferquote beim Stillhalten haengt vor allem an diesem Wert, nicht "
					+ "am Deckel darueber. Simulation mit gemessenen MP-Daten (35 m, 800 Schuss/min, "
					+ "Rueckstoss, Schwanken, Streuung, ein Bild Messverzug): 0,1 s = 49/58 % "
					+ "Kopftreffer bei 60/144 fps, 0,05 s = 65/77 %, 0,03 s = 70/82 %. Unter "
					+ "0,05 s wird es bei niedriger Bildrate unruhig (Verstaerkung je Bild bei "
					+ "30 fps 0,67 statt 0,49).",
					new AcceptableValueRange<float>(0.02f, 1f)), 30));

			Blickfeld = c.Bind("9 Alte Arten - Ziel", "Blickfeld in Pixeln", 61f, Alt(new ConfigDescription("Wie weit ein Gegner von der Bildschirmmitte weg sein darf, damit er ueberhaupt in Frage kommt. Klein = nur was du fast schon anvisierst.", new AcceptableValueRange<float>(10f, 600f))));
			Uebernehmen(c, "2 Ziel", Blickfeld);
			BlickfeldGrad = c.Bind("9 Alte Arten - Ziel", "Blickfeld in Grad", 3f, Alt(new ConfigDescription("Empfohlen statt der Pixel-Angabe: Ein Winkel bleibt beim Zoomen gleich. Die Pixel-Angabe nicht - beim Zielfernrohr schrumpft das Sichtfeld der Kamera, derselbe Gegner liegt dann rechnerisch vielfach weiter von der Mitte entfernt und faellt aus dem Blickfeld. Genau deshalb half die Zielhilfe bei kleiner Zoomstufe und bei grosser nicht mehr. 0 = doch die Pixel-Angabe benutzen.", new AcceptableValueRange<float>(0f, 30f))));
			Uebernehmen(c, "2 Ziel", BlickfeldGrad);
			Reichweite = c.Bind("2 Ziel", "Reichweite in Metern", 930f, new ConfigDescription("Weiter entfernte Gegner werden nicht beachtet.", new AcceptableValueRange<float>(10f, 1500f)));
			Ziel = c.Bind("2 Ziel", "Koerperstelle", Knochen.Kopf, "Kopf, Brust, Becken oder Beine. Bei \"Zone nach Panzerung\" "
				+ "waehlt die Hilfe die Zone selbst; dann bestimmt dieser Wert nur noch den Punkt, an dem "
				+ "die Zielsuche die freie Sicht prueft.");
			// "Knochen" gibt es nicht mehr: ein alter Eintrag faellt still auf
			// Sichtbar zurueck statt beim Einlesen eine Warnung zu erzeugen.
			KnochenErsetzen(c);
			Kopfziel = c.Bind("2 Ziel", "Kopf-Trefferbox", Kopfbox.Sichtbar,
				new ConfigDescription(
					"Worauf bei Koerperstelle \"Kopf\" gezielt wird. Der Kopf ist im Spiel "
					+ "keine einzige Box, sondern acht - alle zaehlen als Treffer am "
					+ "Kopf, sie unterscheiden sich in Lage und Panzerung.\n"
					+ "Sichtbar = empfohlen. Prueft jede Box einzeln und nimmt die erste, "
					+ "die vom Auge aus wirklich FREI liegt. Steckt der Gegner hinter einer "
					+ "Mauerkante, ist der halbe Kopf verdeckt - auf die verdeckte Haelfte "
					+ "zu zielen heisst, in die Mauer zu schiessen. Reihenfolge der "
					+ "Vorliebe: Kopfmitte, Gesicht, Kiefer, Schaedeldecke, Hinterkopf, "
					+ "Ohren, Hals vorn, Hals hinten (bei \"Zone nach Panzerung\" die am "
					+ "wenigsten gepanzerte zuerst). Liegt keine frei, wird die "
					+ "bevorzugte genommen.\n"
					+ "Kopfmitte = immer die grosse Kopfbox (HeadCommon), ohne Pruefung.\n"
					+ "Gesicht = die Augenpartie (Eyes). Kleiner, aber die Stelle, die "
					+ "Helme am seltensten decken.\n"
					+ "Kiefer = Jaw, knapp darunter.\n"
					+ "Schaedeldecke = ParietalHead, oben."));

			BoxHoehe = c.Bind("2 Ziel", "Hoehe in der Kopfbox", 0.72f, new ConfigDescription(
				"WO in der Trefferbox gezielt wird. 0 = Unterkante, 0,5 = Mitte, 1 = Oberkante. "
				+ "Die Mitte der Kopfbox liegt auf Kinnhoehe, nicht in der Schaedelmitte - "
				+ "bei 0,5 zielte die Hilfe deshalb sichtbar zu tief. Der Wert wird auf die "
				+ "tatsaechliche Hoehe der Box umgerechnet, passt sich also von selbst an, "
				+ "wenn der Gegner geduckt ist oder eine andere Box gewaehlt wird.",
				new AcceptableValueRange<float>(0f, 1f)));
			VomFeuerpunkt = c.Bind("9 Alte Arten - Ziel", "Zielwinkel vom Feuerpunkt statt von der Kamera", defaultValue: true, Alt(new ConfigDescription(
					"Der Winkel zum Ziel wurde bisher von der KAMERA aus gerechnet. "
					+ "Die Kugel startet aber an der Muendung, und die sitzt ein "
					+ "gutes Stueck tiefer und weiter vorn. Auf kurze Entfernung ist "
					+ "dieser Versatz kein Rundungsfehler: 10 cm Abstand sind auf "
					+ "10 m bereits 0,57 Grad - mehr als der Fehler, den die Hilfe "
					+ "ueberhaupt wegregeln soll. Genau deshalb stieg der gemessene "
					+ "Muendungsfehler im Nahkampf an (0,70 Grad unter 10 m gegen "
					+ "0,30 Grad auf 55-80 m), obwohl dort nichts falscher gezielt "
					+ "war.\n\n"
					+ "Eingeschaltet wird der Zielwinkel vom Feuerpunkt aus "
					+ "gerechnet. Nebenwirkung, die KEIN Fehler ist: das Fadenkreuz "
					+ "steht dann im Nahbereich sichtbar etwas neben dem Gegner - "
					+ "das ist die Parallaxe, und sie gehoert dorthin. Der Schuss "
					+ "sitzt, nicht das Fadenkreuz.\n\n"
					+ "Die Drehratenmessung fuer das Mitdrehen bleibt bewusst an "
					+ "der Kamera: der Feuerpunkt wackelt mit der Waffenanimation, "
					+ "und dieses Wackeln hat in einer Geschwindigkeitsmessung "
					+ "nichts zu suchen.")));
			Uebernehmen(c, "2 Ziel", VomFeuerpunkt);
			BrustAusweichen = c.Bind("2 Ziel", "Auf die Brust ausweichen, wenn der Kopf zu ist",
				defaultValue: true,
				new ConfigDescription(
					"Nur ohne \"Zone nach Panzerung\" (dort faellt eine verdeckte Kopfzone ohnehin weg). "
					+ "Liegt KEINE der acht Kopfboxen frei, zielte die Hilfe bisher "
					+ "trotzdem auf den Kopf - also mitten in die Deckung, die davor "
					+ "steht. Eingeschaltet nimmt sie dann die Brust: groesser, und "
					+ "vor allem tatsaechlich zu treffen. Im Raid vom 18.09.2026 "
					+ "betraf das 6 von 1231 Bildern, es ist also kein grosser Hebel - "
					+ "aber in genau diesen Bildern war der Schuss sicher vergeudet."));
			NurSichtbare = c.Bind("2 Ziel", "Nur freie Sicht", defaultValue: true, "An = Gegner hinter Waenden werden uebergangen.");

			DurchschussIgnorieren = c.Bind("2 Ziel", "Durchschiessbares ignorieren", defaultValue: true,
				"An = Glas, Maschendraht, Gitter, hohes Gras und aehnliches gelten "
				+ "NICHT als Sichthindernis. Das haelt zwar den Sichtstrahl auf, die "
				+ "Kugel aber nicht - ohne diese Ausnahme sortiert die Hilfe Gegner "
				+ "hinter einer Fensterscheibe aus, obwohl man sie erschiessen kann. "
				+ "Die Bewertung kommt aus dem Spiel selbst (Materialart und "
				+ "Durchschusswerte des getroffenen Kolliders), nicht aus einer "
				+ "geratenen Liste. Koerper zaehlen nie als durchlaessig.");

			DurchschussSchwelle = c.Bind("2 Ziel", "Durchschuss ab Wahrscheinlichkeit", 0.5f,
				new ConfigDescription(
					"Fuer alles, was nicht ohnehin durchsichtig ist (Holz, Blech, Plastik ...): "
					+ "ab welcher Wahrscheinlichkeit es als durchlaessig gilt. Gerechnet wie im "
					+ "Spiel mit der EIGENEN geladenen Patrone: nur wenn ihre Durchschlagskraft "
					+ "ueber der Stufe des Materials liegt, dann Chance = Hindernis-Chance der "
					+ "Patrone + Chance des Materials. 1 = nur was sicher durchgeht.",
					new AcceptableValueRange<float>(0f, 1f)));
			NurKugelEbenen = c.Bind("2 Ziel", "Nur Ebenen pruefen, auf denen Kugeln fliegen", defaultValue: true,
				new ConfigDescription(
					"Der Sichtstrahl lief bisher ueber ALLE Unity-Ebenen. Dadurch hielten "
					+ "ihn Dinge auf, durch die gar keine Kugel fliegt - vor allem die "
					+ "unsichtbaren Wuerfel der Sichtbarkeitssteuerung (\"Cube (4)\" und "
					+ "aehnliche, im Protokoll als [kein Ballistik-Kollider] gemeldet). "
					+ "Auf offenem Feld stand so ein Wuerfel auf 11 m vor einem Gegner auf "
					+ "162 m und die Zielhilfe tat gar nichts. Eingeschaltet wird nur noch "
					+ "auf den Ebenen geprueft, die das Spiel selbst fuer Geschosse "
					+ "benutzt. Ausschalten stellt das alte Verhalten wieder her."));
			Wechselvorsprung = c.Bind("2 Ziel", "Wechselvorsprung in Pixeln", 40f, new ConfigDescription("Am aktuellen Ziel wird festgehalten, solange kein anderer um mindestens so viele Pixel besser liegt. Verhindert das Springen zwischen zwei nebeneinanderstehenden Gegnern. 0 = immer der Naechste.", new AcceptableValueRange<float>(0f, 300f)));
			ScavsAuslassen = c.Bind("3 Wen auslassen", "Scavs", defaultValue: false, "");
			PmcsAuslassen = c.Bind("3 Wen auslassen", "PMCs", defaultValue: false, "");
			BosseAuslassen = c.Bind("3 Wen auslassen", "Bosse und Leibwaechter", defaultValue: false, "");
			Nachziehen = c.Bind("9 Alte Arten - Bewegung", "Nachziehgeschwindigkeit", 60f, Alt(new ConfigDescription("Wie schnell die Waffe aufs Ziel zieht, in 1/Sekunde. Hoch = hart und schnell, niedrig = weich. WICHTIG: Dieser Wert ist bildratenunabhaengig - anders als das Smoothing des Cheat-Menues, das bei hohen fps von selbst schneller zog und mit jeder Ruckelei die Geschwindigkeit aenderte.", new AcceptableValueRange<float>(2f, 200f))));
			Uebernehmen(c, "4 Bewegung", Nachziehen);
			// "Softaim statt Festhalten" (an/aus) ist in "Art der Hilfe"
			// aufgegangen: AltesSoftaim = an, AltesFesthalten = aus.
			SoftaimHoechstens = c.Bind("9 Alte Arten - Bewegung", "Softaim hoechstens x Grad je Sekunde", 80f, Alt(new ConfigDescription(
					"NUR fuer \"Art der Hilfe\" = AltesSoftaim. "
					+ "Wie viel die Hilfe im Softaim-Modus hoechstens je Sekunde "
					+ "beisteuern darf. DAS ist der Regler fuer das Spielgefuehl: "
					+ "klein = die Hilfe schubst nur sanft und du fuehrst, gross = sie "
					+ "nimmt dir die Feinarbeit ab. Sie kann dich nie ueberstimmen, "
					+ "weil deine eigene Drehung unangetastet bleibt. 80 ist bewusst "
					+ "zurueckhaltend gewaehlt - wenn sie zu wenig abnimmt, hochsetzen; "
					+ "wenn es sich immer noch klebrig anfuehlt, runter auf 40.",
					new AcceptableValueRange<float>(10f, 600f))));
			Uebernehmen(c, "4 Bewegung", SoftaimHoechstens);
			Totzone = c.Bind("9 Alte Arten - Bewegung", "Totzone in Grad", 0.05f, Alt(new ConfigDescription("Unterhalb dieser Abweichung wird nicht mehr nachgeregelt. Verhindert ein feines Dauerzittern direkt auf dem Ziel.", new AcceptableValueRange<float>(0f, 1f))));
			Uebernehmen(c, "4 Bewegung", Totzone);

			MausVorrang = c.Bind("9 Alte Arten - Bewegung", "Eigene Maus hat Vorrang", defaultValue: true, Alt("An = sobald du selbst schnell drehst, laesst die Hilfe los. Ohne das "
				+ "setzt sie deine Blickrichtung jedes Bild neu und du kommst vom "
				+ "einmal erfassten Ziel nicht mehr weg."));
			Uebernehmen(c, "4 Bewegung", MausVorrang);

			MausSchwelle = c.Bind("9 Alte Arten - Bewegung", "Vorrang ab x Grad je Sekunde", 50f, Alt(new ConfigDescription(
					"Ab welcher EIGENEN Drehgeschwindigkeit die Hilfe loslaesst. Gemessen "
					+ "wird der Unterschied zwischen dem, was die Hilfe zuletzt gesetzt hat, "
					+ "und dem, was im naechsten Bild wirklich dasteht - das ist deine Maus. "
					+ "Zu klein und schon der Rueckstoss laesst sie los; zu gross und du "
					+ "kommst nicht vom Ziel weg. 50 ist ein ruhiger Startwert.",
					new AcceptableValueRange<float>(5f, 400f))));
			Uebernehmen(c, "4 Bewegung", MausSchwelle);

			MausNachlauf = c.Bind("9 Alte Arten - Bewegung", "Danach x Sekunden nicht ziehen", 0.25f, Alt(new ConfigDescription(
					"Wie lange die Hilfe nach deiner letzten Drehung noch aus bleibt. Ohne "
					+ "Nachlauf schnappt sie zwischen zwei Mausbewegungen sofort zurueck "
					+ "aufs alte Ziel.",
					new AcceptableValueRange<float>(0f, 2f))));
			Uebernehmen(c, "4 Bewegung", MausNachlauf);
			NachfuehrungStaerke = c.Bind("9 Alte Arten - Bewegung", "Mitdrehen bei bewegten Zielen", 1f, Alt(new ConfigDescription("Der wichtigste Regler gegen \"trifft stehende gut, laufende schlecht\". Das blosse Nachziehen bleibt bei einem laufenden Gegner IMMER ein Stueck hinter ihm zurueck - der Rueckstand ist Drehgeschwindigkeit geteilt durch Nachziehgeschwindigkeit. Hier wird gemessen, wie schnell sich die Ziellinie dreht, und genau das mitgedreht. 1,0 = Rueckstand vollstaendig weg. 0 = aus, verhaelt sich wie vorher. Ueber 1,0 laeuft sie dem Ziel voraus.", new AcceptableValueRange<float>(0f, 1.5f))));
			Uebernehmen(c, "4 Bewegung", NachfuehrungStaerke);
			NachfuehrungRuhe = c.Bind("9 Alte Arten - Bewegung", "Mitdrehen beruhigen (Sekunden)", 0.08f, Alt(new ConfigDescription("Ueber wie viel Zeit die gemessene Drehgeschwindigkeit gemittelt wird. Klein = reagiert sofort auf Richtungswechsel, uebernimmt aber jedes Zucken. Gross = ruhiger, haengt bei Haken kurz nach.", new AcceptableValueRange<float>(0.02f, 0.5f))));
			Uebernehmen(c, "4 Bewegung", NachfuehrungRuhe);
			NachfuehrungHoechstens = c.Bind("9 Alte Arten - Bewegung", "Mitdrehen hoechstens x Grad je Sekunde", 150f, Alt(new ConfigDescription("Sicherheitsdeckel. Verhindert, dass ein Ausreisser in den Messwerten die Waffe wegreisst.", new AcceptableValueRange<float>(10f, 400f))));
			Uebernehmen(c, "4 Bewegung", NachfuehrungHoechstens);
			FallAusgleich = c.Bind("5 Vorhalt", "Kugelfall ausgleichen", defaultValue: true, "Hebt den Zielpunkt um das an, was die Kugel bis zum Ziel faellt - gerechnet gegen die Entfernung, auf die deine Optik gerade genullt ist. Ohne das sitzt der Schuss nur auf der Nullungsdistanz richtig und geht darueber hinaus zu tief.");
			VorhaltAn = c.Bind("5 Vorhalt", "Eingeschaltet", defaultValue: true, "Zielt bei bewegten Gegnern dorthin, wo sie sein werden, wenn die Kugel ankommt. Aus = immer direkt drauf.");
			VorhaltFaktor = c.Bind("5 Vorhalt", "Faktor", 1f, new ConfigDescription("1,0 = die ausgerechnete Flugzeit unveraendert. Die Ballistik kommt aus dem Spiel selbst; hier ist normalerweise nichts zu tun. Schiesst du hinterher: erhoehen.", new AcceptableValueRange<float>(0f, 3f)));
			VorhaltHoechstens = c.Bind("5 Vorhalt", "Hoechstens x Meter", 4f, new ConfigDescription("Sicherheitsgrenze gegen Ausreisser in den Bewegungsdaten.", new AcceptableValueRange<float>(0.5f, 20f)));
			VorhaltAbTempo = c.Bind("5 Vorhalt", "Erst ab x m/s", 1.2f, new ConfigDescription("Darunter wird direkt gezielt. Wer nur die Waffe hebt oder sich dreht, bekommt keinen Vorhalt. Gehen sind rund 2 m/s.", new AcceptableValueRange<float>(0f, 5f)));
			VorhaltRuhe = c.Bind("5 Vorhalt", "Beruhigung (Sekunden)", 0.15f, new ConfigDescription("Ueber wie viel Zeit die gemessene Geschwindigkeit des Gegners gemittelt wird. Klein = folgt Richtungswechseln sofort, uebernimmt aber jedes Zucken. Gross = ruhig, haengt bei Haken nach. ACHTUNG, Bedeutung geaendert: Der alte Regler \"Beruhigung\" war ein Anteil je BILD und wirkte dadurch bei 144 fps ganz anders als bei 60 - derselbe Fehler, wegen dem der Aimbot des Cheat-Menues ersetzt wurde. Der alte Eintrag in der cfg wird nicht mehr gelesen.", new AcceptableValueRange<float>(0.02f, 1f)));
			VorhaltRuheMin = c.Bind("5 Vorhalt", "Glaettung mindestens x Sekunden", 0.06f,
				new ConfigDescription(
					"Untere Schranke fuer die Glaettung der Zielgeschwindigkeit, "
					+ "unabhaengig von der Flugzeit. Im Nahbereich ist die Flugzeit so "
					+ "kurz, dass ohne diese Schranke fast der Augenblickswert benutzt "
					+ "wird - die Vorhalt-Richtung zappelt dann und der Zielpunkt springt. "
					+ "Zu hoch gewaehlt reagiert der Vorhalt traege auf Richtungswechsel und "
					+ "faellt zu klein aus: mit 0,12 s meldete die Vorhalt-Probe -24 cm, "
					+ "bei rund 0,05 s waren es -4 cm. Die Probe im Protokoll ist der "
					+ "Massstab - sie soll moeglichst nahe an 0 liegen.",
					new AcceptableValueRange<float>(0f, 0.5f)));
			Bildvorhalt = c.Bind("5 Vorhalt", "Bildverzug ausgleichen (Bilder)", 1f,
				new ConfigDescription(
					"Gleicht den EIGENEN Verzug aus, nicht den Kugelflug. Der Zielpunkt "
					+ "wird zu Beginn eines Bildes berechnet; bis der Schuss faellt, ist das "
					+ "Ziel schon weiter. Gemessen: bei 7 Grad/s Zieldrehung und 43 ms je "
					+ "Bild sind das 0,30 Grad - genau der Restfehler, der im Nahkampf "
					+ "uebrig blieb. 1 = ein Bild vorhalten, 0 = aus. Bei hoher Bildrate "
					+ "wirkt es von selbst schwaecher, weil ein Bild dann kuerzer ist.",
					new AcceptableValueRange<float>(0f, 3f)));
			NurWaagerecht = c.Bind("5 Vorhalt", "Nur waagerechte Bewegung", defaultValue: true, "An = das Auf und Ab beim Laufen wird ignoriert.");
			RueckstossAusgleich = c.Bind("9 Alte Arten - Rueckstoss", "Rueckstoss ausgleichen", 0.8f, Alt(new ConfigDescription("Die Kugel verlaesst die MUENDUNG, nicht die Kamera - im Spielcode belegt (WeaponDirection). Beim Rueckstoss klettert die Muendung gegenueber der Kamera weg: die Blickrichtung liegt sauber auf dem Kopf, die Salve geht trotzdem darueber. Bei Einzelfeuer faellt das kaum auf, bei Dauerfeuer waechst es mit jedem Schuss. Hier wird die Blickrichtung so verschoben, dass die MUENDUNG aufs Ziel zeigt. 1,0 = voll ausgleichen, 0 = aus. Wirkt nur im Anschlag; beim Hueftschuss steht die Waffe dauerhaft schief, das ist kein Rueckstoss sondern die Haltung.", new AcceptableValueRange<float>(0f, 1f))));
			Uebernehmen(c, "6 Rueckstoss", RueckstossAusgleich);
			RueckstossRuhe = c.Bind("9 Alte Arten - Rueckstoss", "Beruhigen (Sekunden)", 0.04f, Alt(new ConfigDescription("Glaettet das Zittern der Waffenanimation, damit es nicht eins zu eins in die Blickrichtung durchschlaegt. Klein = folgt dem Rueckstoss sofort, gross = ruhiger, hinkt bei schnellen Salven nach.", new AcceptableValueRange<float>(0.005f, 0.3f))));
			Uebernehmen(c, "6 Rueckstoss", RueckstossRuhe);
			MuendungNachregeln = c.Bind("9 Alte Arten - Rueckstoss", "Muendung nachregeln", defaultValue: true, Alt(new ConfigDescription(
					"DIE Antwort auf \"sobald er fixiert hat, justiert er nicht mehr "
					+ "nach\". Bisher wurde der Versatz zwischen Kamera und Muendung "
					+ "VORWAERTS ausgerechnet und abgezogen - eine Steuerung ohne "
					+ "Rueckmeldung. Im Protokoll des Raids vom 18.09.2026 nahm sie "
					+ "nur rund ein Zehntel des Versatzes weg: die Kamera lag mit "
					+ "0,04 Grad auf dem Kopf, die Muendung 0,34 Grad darunter, und "
					+ "daran aenderte sich nichts mehr, egal wie lange man draufhielt. "
					+ "Eingeschaltet wird stattdessen gemessen, wohin die MUENDUNG "
					+ "wirklich zeigt, und der Rest wird aufsummiert weggeregelt - "
					+ "solange, bis er weg ist. Das ist der Unterschied zwischen "
					+ "\"einmal richtig gerechnet\" und \"regelt nach, bis es sitzt\".")));
			Uebernehmen(c, "6 Rueckstoss", MuendungNachregeln);
			MuendungVerstaerkung = c.Bind("9 Alte Arten - Rueckstoss", "Nachregeln wie schnell", 6f, Alt(new ConfigDescription(
					"Wie schnell der Rest weggeregelt wird, in 1/Sekunde. 6 heisst "
					+ "grob: nach einer knappen Sekunde ist der bleibende Fehler weg. "
					+ "Zu hoch gewaehlt schaukelt sich die Regelung auf (die Waffe "
					+ "haengt beim Drehen nach, das ist eine Rueckkopplung) - dann "
					+ "zittert der Zielpunkt, und der Wert gehoert runter.",
					new AcceptableValueRange<float>(0f, 30f))));
			Uebernehmen(c, "6 Rueckstoss", MuendungVerstaerkung);
			MuendungHoechstens = c.Bind("9 Alte Arten - Rueckstoss", "Nachregeln hoechstens x Grad", 2f, Alt(new ConfigDescription(
					"Sicherheitsdeckel fuer die Summe. Verhindert, dass die Regelung "
					+ "bei einer laufend falschen Messung immer weiter aufdreht.",
					new AcceptableValueRange<float>(0.1f, 8f))));
			Uebernehmen(c, "6 Rueckstoss", MuendungHoechstens);
			RueckstossHoechstens = c.Bind("9 Alte Arten - Rueckstoss", "Hoechstens x Grad", 3f, Alt(new ConfigDescription("Sicherheitsdeckel. Tarkovs Waffe haengt beim Drehen nach - drehen wir gegen den Versatz, haengt sie weiter nach und der Versatz waechst. Diese Rueckkopplung ist schwach genug, um mit einer Staerke unter 1 stabil zu bleiben; der Deckel sichert den Rest ab.", new AcceptableValueRange<float>(0.5f, 10f))));
			Uebernehmen(c, "6 Rueckstoss", RueckstossHoechstens);
			HuefteAusgleich = c.Bind("9 Alte Arten - Rueckstoss", "Aus der Huefte ausgleichen", 1f, Alt(new ConfigDescription(
					"Frueher stand hier gar kein Ausgleich: die Waffe steht aus der Huefte "
					+ "DAUERHAFT schief zur Kamera, und ein Ausgleich dreht die Blickrichtung "
					+ "entsprechend weg. Gemessen wurde aber, dass die Kamera dabei sauber auf "
					+ "dem Ziel liegt und die MUENDUNG im Mittel 2,8 Grad daneben - auf 19 m "
					+ "rund 90 cm. Der Schuss geht damit sicher vorbei. 0 = wie frueher, "
					+ "1 = voll ausgleichen.",
					new AcceptableValueRange<float>(0f, 1f))));
			Uebernehmen(c, "6 Rueckstoss", HuefteAusgleich);
			HuefteHoechstens = c.Bind("9 Alte Arten - Rueckstoss", "Huefte hoechstens x Grad", 8f, Alt(new ConfigDescription(
					"Eigene Obergrenze fuer den Hueftausgleich. Sie ist groesser als die im "
					+ "Anschlag, weil der Haltungsversatz dort groesser ausfaellt: gemessen "
					+ "im Median 3 Grad, in einem Zehntel der Faelle ueber 14. Uebersteigt "
					+ "der Versatz das DOPPELTE dieser Grenze, wird gar nicht ausgeglichen - "
					+ "dann ist die Waffe gesenkt oder wird gerade angelegt, und ein "
					+ "Ausgleich wuerde nur gegen die Animation arbeiten.",
					new AcceptableValueRange<float>(0f, 20f))));
			Uebernehmen(c, "6 Rueckstoss", HuefteHoechstens);
			VorhaltDaempfung = c.Bind("5 Vorhalt", "Bei Richtungswechsel zuruecknehmen", 0.7f, new ConfigDescription("Der Vorhalt nimmt an, der Gegner laufe geradeaus weiter. Schlaegt er einen Haken, zielt die Hilfe dorthin, wo er GEWESEN waere - und das kann weiter danebenliegen als gar kein Vorhalt. Hier wird gemessen, wie stark er gerade die Richtung wechselt, und der Vorhalt entsprechend zurueckgenommen. 0 = aus (immer voller Vorhalt), 1 = bei einem harten Haken faellt der Vorhalt ganz weg.", new AcceptableValueRange<float>(0f, 1f)));
		}
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
	[BepInPlugin("com.kaan.zielhilfe", "Zielhilfe", "1.1.0")]
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

		private void Awake()
		{
			Einstellungen.Anlegen(base.Config);
			string entfernt = Einstellungen.WaisenEntfernen(base.Config);
			if (entfernt.Length > 0)
			{
				base.Logger.LogInfo("[Zielhilfe] Veraltete Eintraege aus der cfg entfernt: " + entfernt);
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
				base.Logger.LogError("[Zielhilfe] Maus-Abgriff fehlgeschlagen, Soft Aim per Maus bleibt aus: " + ex);
			}
			base.Logger.LogInfo("[Zielhilfe] Bereit. Taste: " + Einstellungen.Taste.Value.ToString() + ", Art: " + Einstellungen.Art.Value + ", Modus: " + Einstellungen.Modus.Value);
		}

		private void Update()
		{
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
						base.Logger.LogInfo("[Zielhilfe] kein Raid aktiv");
					}
					return;
				}
				bool maus = Einstellungen.Art.Value == Zielmodus.SoftaimMaus;
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
					base.Logger.LogInfo("[Zielhilfe] Kameras im Raid: " + Spielzugriff.AlleKameras());
					// Einmal zeigen, welche Kopf-Trefferboxen das Modell hat und wo ihre
					// Mitte gegenueber dem Kopfknochen liegt. Der Knochen sitzt am
					// Halsansatz - an den Zahlen sieht man, wie weit daneben er liegt.
					base.Logger.LogInfo("[Zielhilfe] Kopfboxen am eigenen Spieler (Mitte "
						+ "gegenueber Kopfknochen, hoch/Abstand): " + Zielpunkt.Kopfboxen(ich));
				}
				if (flag)
				{
					_letzteDiagnose = Time.time;
					base.Logger.LogInfo($"[Zielhilfe] Taste={Einstellungen.Taste.Value} gedrueckt={flag2} " + $"ich={ich != null} kamera={Spielzugriff.Kameraquelle} " + $"lebend={_suche.Gesamt} rausFreund={_suche.RausFreund} rausFraktion={_suche.RausFraktion} " + $"rausWeit={_suche.RausEntfernung} rausHinten={_suche.RausHinterKamera} " + $"rausKeinKnochen={_suche.RausKeinKnochen} " + $"rausBlickfeld={_suche.RausBlickfeld} rausSicht={_suche.RausSicht} " + $"uebrig={_suche.Uebrig} | naechster Gegner {_suche.NaechsterAbstand:0} px " + $"= {_suche.NaechsterWinkel:0.00}° von der Mitte | Sichtfeld der Kamera " + $"{((kamera != null) ? kamera.fieldOfView : 0f):0.0}° | Grenze " + ((Einstellungen.BlickfeldGrad.Value > 0f) ? $"{Einstellungen.BlickfeldGrad.Value:0.0}°" : $"{Einstellungen.Blickfeld.Value:0} px") + ((_suche.LetztesHindernis.Length > 0) ? (" | Sicht blockiert von " + _suche.LetztesHindernis) : "") + " | Zielpunkt: " + ((Zielpunkt.LetzteWahl.Length > 0) ? Zielpunkt.LetzteWahl : Zielpunkt.LetzteQuelle) + ((Zielpunkt.LetzterVersatz > 0.001f) ? $" | Vorhalt schiebt {Zielpunkt.LetzterVersatz * 100f:0} cm = {Zielpunkt.LetzterVersatzGrad:0.000}° [seitlich {Zielpunkt.LetzterVersatzSeitlich * 100f:0} cm, hoch {Zielpunkt.LetzterVersatzHoch * 100f:0} cm]" : "") + ((Zielpunkt.ProbeText.Length > 0) ? (" | Vorhalt-Probe " + Zielpunkt.ProbeText) : "") + ((Zielpunkt.LetzterBildvorhalt > 0.001f) ? $" | Bildverzug {Zielpunkt.LetzterBildvorhalt * 100f:0} cm" : "") + ((Zielpunkt.LetzteBoxlage.Length > 0) ? (" | Kopfboxen " + Zielpunkt.LetzteBoxlage) : "") + ((Zielpunkt.LetzteBoxhebung != 0f) ? $" | Boxhebung {Zielpunkt.LetzteBoxhebung * 100f:0.0} cm" : "") + ((Zielpunkt.LetzterBoxfehler.Length > 0) ? (" | BOXFEHLER " + Zielpunkt.LetzterBoxfehler) : "") + ((Zielpunkt.LetzteEntfernung > 0f) ? $" | {Zielpunkt.LetzteEntfernung:0} m, Glas auf {(Zielpunkt.LetzteEinschiessung < 0 ? $"{-Zielpunkt.LetzteEinschiessung} m (Standard!)" : $"{Zielpunkt.LetzteEinschiessung} m")}" : "") + ((Zielpunkt.LetzterFall != 0f) ? $" | Fallausgleich {Zielpunkt.LetzterFall * 100f:0} cm [Fall {Zielpunkt.FallBeiZiel * 100f:0}/{Zielpunkt.FallBeiEinschuss * 100f:0} cm, Nullung wirkt {Zielpunkt.NullungWirkt * 100f:0}%, v0 {Zielpunkt.Muendungstempo:0} m/s]" : "") + $" | {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0} fps ({Time.smoothDeltaTime * 1000f:0} ms/Bild)" + $" | Ziellinie dreht {(maus ? SoftAim.ZielRate.x : _nachfuehrung.Gieren):0.0}°/s quer, " + $"{(maus ? SoftAim.ZielRate.y : _nachfuehrung.Nicken):0.0}°/s hoch" + (MausHatVorrang ? " | MAUS HAT VORRANG" : "") + $" | eigene Drehung {EigeneDrehrate:0}°/s" + " | Restfehler " + $"{_letzterFehlerGieren:0.000}° / {_letzterFehlerNicken:0.000}°" + ((Rueckstoss.MuendungZumZiel >= 0f) ? $" | MUENDUNG zum Ziel {Rueckstoss.MuendungZumZiel:0.000}° (quer {Rueckstoss.FehlerGieren:0.000}°, hoch {Rueckstoss.FehlerNicken:0.000}°)" : "") + Aufzeichnung.Diagnose() + (maus ? SoftAim.Diagnose() : ((Einstellungen.Art.Value == Zielmodus.AltesSoftaim) ? $" | SOFTAIM, Deckel {Einstellungen.SoftaimHoechstens.Value:0}°/s" : " | FESTHALTEN")) + $" | Nachregelung {Rueckstoss.NachGieren:0.00}° / {Rueckstoss.NachNicken:0.00}°" + ((Zielpunkt.LetzteParallaxe > 0.001f) ? $" | Ursprung Feuerpunkt, {Zielpunkt.LetzteParallaxe * 100f:0} cm neben der Kamera" : " | Ursprung Kamera") + $" | Richtungswechsel {Zielpunkt.LetzterWechsel:0.00}" + $" | Muendung vs. Kamera {Rueckstoss.LetzterGieren:0.00}° / " + $"{Rueckstoss.LetzterNicken:0.00}°" + (Rueckstoss.ImAnschlag ? " (Anschlag)" : $" | HUEFTE, nicht ausgeglichen: {Rueckstoss.HuefteGieren:0.00}° / {Rueckstoss.HuefteNicken:0.00}°"));
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
					bool kopfZu = false;
					bool nachPanzerung = maus && Einstellungen.ZoneNachPanzerung.Value;
					Knochen stelle = Einstellungen.Ziel.Value;
					SoftAim.ZonenGrund = "Koerperstelle";
					if (nachPanzerung)
					{
						// Ersetzt fuer diese Art die feste Koerperstelle samt
						// Kopfbox- und Brust-Ausweichlogik: dieselbe Sichtpruefung
						// je Kopfbox, nur in der Reihenfolge der Panzerung, und eine
						// verdeckte Kopfzone faellt weg.
						stelle = Panzerung.Waehlen(ich, ziel, kamera, out zielpunkt, out kopfZu);
					}
					else if (Einstellungen.Ziel.Value == Knochen.Kopf
						&& Einstellungen.Kopfziel.Value == Kopfbox.Sichtbar
						&& Zielpunkt.SichtbareBox(ziel, kamera, out var boxmitte, out var boxname))
					{
						// Keine einzige Kopfbox frei? Dann steht Deckung davor.
						// Auf den Kopf zu zielen heisst dann, in die Deckung zu
						// schiessen - die Brust ist groesser und oft daneben frei.
						if (boxname.EndsWith("(verdeckt)")
							&& Einstellungen.BrustAusweichen != null
							&& Einstellungen.BrustAusweichen.Value)
						{
							zielpunkt = Zielpunkt.Koerper(ziel);
							Zielpunkt.LetzteWahl = "Brust(Kopf verdeckt)";
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
						stelle = Knochen.Brust;
					}
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

		private readonly Zone[] _zonen = new Zone[4];

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
				if (art == Knochen.Kopf && kopfZu)
				{
					// Keine Kopfbox frei (SichtbareBox).
					SoftAim.SichtZonen[i] = 0;
					continue;
				}
				EBodyPartColliderType[] typen = Trefferzonen.Typen(art);
				bool nurDurchschlagbar = art == Knochen.Kopf && Einstellungen.ZoneNachPanzerung.Value;
				if (!Trefferzonen.Bilden(ziel, art.ToString(), typen, versatz, _kollider[i], nurDurchschlagbar, out var z))
				{
					continue;
				}
				Vector3 pruefpunkt = (art == stelle) ? vorVorhalt : (z.Mitte - versatz);
				bool frei = Zielpunkt.Sichtlinie(start, pruefpunkt, ziel, out var grund, out _, out _);
				SoftAim.SichtZonen[i] = !frei ? 0 : (grund.StartsWith("durch") ? 2 : 1);
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
			SoftAim.Merken(ziel, _zonen, anzahl, wunsch, entfernung, Zielpunkt.LetztesTempo);
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
			case Ausloeser.Immer:
				return true;
			case Ausloeser.TasteUmschalten:
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
				bool weich = Einstellungen.Art.Value == Zielmodus.AltesSoftaim;
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
					base.Logger.LogWarning($"[Zielhilfe] Notaus: stand {num:0.0}° neben dem Ziel, " + "Mitdrehen zurueckgesetzt.");
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
							Kameraquelle = "gesucht:" + text;
							return camera;
						}
					}
				}
				Camera main = Camera.main;
				Kameraquelle = ((main != null) ? ("Camera.main:" + main.name) : "keine");
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
					list.Add($"{camera.name}(tiefe {camera.depth:0})");
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
		Kopf,
		Brust,
		Becken,
		Beine
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
		Sichtbar,
		Kopfmitte,
		Gesicht,
		Kiefer,
		Schaedeldecke
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

		public static bool Durchlaessig(Collider c, out string grund)
		{
			grund = "";
			if (c == null)
			{
				return false;
			}
			if (!Einstellungen.DurchschussIgnorieren.Value)
			{
				grund = "Durchschuss abgeschaltet";
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
					grund = "kein Ballistik-Kollider, Ebene " + LayerMask.LayerToName(c.gameObject.layer);
					return false;
				}
				if (b is BodyPartCollider)
				{
					// Ein Koerper ist nie "durchlaessig" - sonst zielt die Hilfe
					// durch den Kameraden vor dem Gegner hindurch.
					grund = "Koerper";
					return false;
				}
				MaterialType m = b.TypeOfMaterial;
				if (Durchsichtig.Contains(m))
				{
					grund = $"{m} durchsichtig";
					return true;
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
					grund = $"{m} durchschiessbar (Pen {pen:0} > {b.PenetrationLevel:0}, {chance * 100f:0} %)";
					return true;
				}
				grund = $"{m} haelt (Pen {pen:0} / Stufe {b.PenetrationLevel:0}, {chance * 100f:0} %)";
				return false;
			}
			catch (Exception ex)
			{
				grund = ex.GetType().Name;
				return false;
			}
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

		// Hebung ueber dem Kopfknochen fuer den Rueckfall ohne Kopfbox (frueher
		// der Regler "Kopf-Hoehenkorrektur in Metern", Standard 0,12 m).
		private const float KopfknochenHebung = 0.12f;

		public static Vector3 Fuer(Player p)
		{
			PlayerBones playerBones = p?.PlayerBones;
			if (playerBones == null)
			{
				LetzteQuelle = "KEINE KNOCHEN";
				if (!(p != null))
				{
					return Vector3.zero;
				}
				return p.Position;
			}
			Vector3 result;
			switch (Einstellungen.Ziel.Value)
			{
			case Knochen.Brust:
				result = ((playerBones.Ribcage != null) ? playerBones.Ribcage.position : p.Position);
				break;
			case Knochen.Becken:
				result = ((playerBones.Pelvis != null) ? playerBones.Pelvis.position : p.Position);
				break;
			case Knochen.Beine:
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
					LetzteQuelle = "KEIN KOPFKNOCHEN";
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
			case Kopfbox.Gesicht:
				return KetteGesicht;
			case Kopfbox.Kiefer:
				return KetteKiefer;
			case Kopfbox.Schaedeldecke:
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
		/// die eigene Patrone schlaegt es nach Spielregel durch (Durchschuss.Durchlaessig).
		/// Keine Toleranz am Ende: die Strahllaenge endet am Punkt.
		/// </summary>
		public static bool Sichtlinie(Vector3 start, Vector3 punkt, Player ziel, out string grund, out Collider hindernis, out float abstand)
		{
			grund = "frei";
			hindernis = null;
			abstand = 0f;
			Vector3 weg = punkt - start;
			float laenge = weg.magnitude;
			if (laenge < 0.01f)
			{
				return true;
			}
			int anzahl = Physics.RaycastNonAlloc(start, weg / laenge, _sichtStrahl, laenge,
												 Durchschuss.Ebenenmaske(), QueryTriggerInteraction.Ignore);
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
				if (Durchschuss.Durchlaessig(c, out var g))
				{
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
				grund = $"verdeckt von {hindernis.name} [{blockGrund}] auf {naechstes:0.0} m";
				return false;
			}
			if (durch.Length > 0)
			{
				grund = "durch " + durch;
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
		// Zuletzt gewaehlte freie Kopfbox (Bestaendigkeit).
		private static Player _halteZiel;

		private static EBodyPartColliderType _halteBox;

		private static bool _halteHat;

		/// <summary>Die gehaltene Kopfbox dieses Gegners, falls vorhanden.</summary>
		internal static bool GehalteneBox(Player ziel, out EBodyPartColliderType typ)
		{
			typ = _halteBox;
			return _halteHat && (object)_halteZiel == ziel;
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
			// Deckungskante flackert.
			if (halten && (object)_halteZiel == ziel && _halteHat && ziel?.PlayerBones?.BodyPartCollidersDictionary != null
				&& ziel.PlayerBones.BodyPartCollidersDictionary.TryGetValue(_halteBox, out var hb) && hb != null && hb.Collider != null)
			{
				Vector3 hp = BoxPunkt(hb);
				if (Sichtlinie(SichtStart(Spielzugriff.Ich, kamera), hp, ziel, out _, out _, out _))
				{
					mitte = hp;
					name = _halteBox.ToString();
					LetzteBoxlage = name + "=frei (gehalten)";
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
						lage += boxen[i] + (frei ? "=frei " : "=zu ");
					}
					if (frei)
					{
						mitte = ziel2;
						name = boxen[i].ToString();
						LetzteBoxlage = lage.TrimEnd();
						_halteZiel = ziel;
						_halteBox = boxen[i];
						_halteHat = true;
						return true;
					}
				}

				LetzteBoxlage = lage.TrimEnd()
					+ ((blockierer.Length > 0) ? $" (blockiert von {blockierer})" : "");
				if (hatErste)
				{
					// Keine einzige Box frei. Lieber die bevorzugte nehmen als
					// gar nichts - die allgemeine Sichtpruefung haette den Gegner
					// ohnehin schon aussortiert, wenn er ganz verdeckt waere.
					mitte = ersteMitte;
					name = ersteName + "(verdeckt)";
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
					return "keine Knochen";
				}
				Vector3 kopf = Vector3.zero;
				string kopfquelle = "Kopfknochen fehlt";
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
					kopfquelle = "Kopfknochen wirft " + ex2.GetType().Name;
				}

				string text = $"Eintraege={karte.Count} {kopfquelle} ";
				for (int i = 0; i < AlleKopfboxen.Length; i++)
				{
					// Jede Box einzeln absichern, damit eine kaputte nicht die
					// ganze Auskunft verschluckt.
					try
					{
						if (!karte.TryGetValue(AlleKopfboxen[i], out var box))
						{
							text += AlleKopfboxen[i] + "=nichtInTabelle ";
							continue;
						}
						if (box == null)
						{
							text += AlleKopfboxen[i] + "=Eintrag_null ";
							continue;
						}
						if (box.Collider == null)
						{
							text += AlleKopfboxen[i] + "=ohneKollider ";
							continue;
						}
						Vector3 vector = box.Center - kopf;
						text += $"{AlleKopfboxen[i]}={vector.y * 100f:+0;-0;0}cm/{vector.magnitude * 100f:0}cm"
								+ (box.Collider.enabled ? " " : "(aus) ");
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
				return "Fehler: " + ex.Message;
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
				ProbeText = $"{mittel:+0;-0} cm ({grosse / Fehler.Count * 100f:0}% zu viel, n={Fehler.Count})";
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

		public string LetztesHindernis = "";

		public Player Aktuell => _gehalten;

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
				if (zonenRand)
				{
					grenze = Einstellungen.RadiusGrad(vector2.magnitude);
					if (Trefferzonen.KleinsterAbstand(item, position, forward, out var rand, out var zonenEntfernung))
					{
						num2 = rand;
						grenze = Einstellungen.RadiusGrad(zonenEntfernung);
					}
				}
				float num3 = num2 / num;
				if (NaechsterAbstand < 0f || num3 < NaechsterAbstand)
				{
					NaechsterAbstand = num3;
					NaechsterWinkel = num2;
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
				if (Einstellungen.NurSichtbare.Value
					&& !Sichtbar(position, vector, item)
					&& !Sichtbar(position, Zielpunkt.Koerper(item), item))
				{
					RausSicht++;
					continue;
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
				SoftAim.ZonenGrund = "Panzerung nicht lesbar (" + ex.GetType().Name + ")";
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
				seite = "vorn";
				rumpf = _rumpfVorn;
				becken = _beckenVorn;
			}
			else if (cosB < -0.5f)
			{
				seite = "hinten";
				rumpf = _rumpfHinten;
				becken = _beckenHinten;
			}
			else
			{
				seite = "seitlich";
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
							&& !boxname.EndsWith("(verdeckt)");
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
			bool kopfMoeglich = Einstellungen.Erlaubt(Knochen.Kopf) && eKopf >= 0f && dKopf > 0f;
			bool rumpfMoeglich = Einstellungen.Erlaubt(Knochen.Brust) && eRumpf >= 0f && rumpf > 0f;
			bool ausweich = !kopfMoeglich && !rumpfMoeglich;
			bool beckenMoeglich = eBecken >= 0f && becken > 0f;
			if (!Einstellungen.Erlaubt(Knochen.Kopf))
			{
				eKopf = -1f;
			}
			if (!Einstellungen.Erlaubt(Knochen.Brust))
			{
				eRumpf = -1f;
			}
			if (!Einstellungen.Erlaubt(Knochen.Becken) && !(ausweich && beckenMoeglich))
			{
				eBecken = -1f;
			}
			if (!Einstellungen.Erlaubt(Knochen.Beine) && !(ausweich && !beckenMoeglich && eBein >= 0f && dBein > 0f))
			{
				eBein = -1f;
			}
			AusweichAktiv = ausweich;
			Knochen wahl = Bestaendig(ziel, eKopf, eRumpf, eBecken, eBein);
			switch (wahl)
			{
			case Knochen.Kopf:
				punkt = boxmitte;
				break;
			case Knochen.Becken:
				punkt = beckenPunkt;
				break;
			case Knochen.Beine:
				punkt = beinPunkt;
				break;
			default:
				punkt = rumpfPunkt;
				break;
			}
			kopfZu = !kopfFrei;
			// Trefferanteile fuer die gewaehlte Zone festhalten (Diagnose).
			Erwartung(start, punkt, LetzteP);
			string grund = $"{wahl}" + (wahl == Knochen.Kopf ? $"/{boxname}" : "")
				+ $" [Erwartung Kopf {Text(eKopf)}, Rumpf {Text(eRumpf)}, Becken {Text(eBecken)}, Beine {Text(eBein)}; "
				+ $"geglaettet {Text(_glatt[0])}/{Text(_glatt[1])}/{Text(_glatt[2])}/{Text(_glatt[3])}"
				+ ((_kand >= 0) ? $", Wechsel zu {(Knochen)_kand} in {Mathf.Max(0f, Mindestzeit - (Time.time - _kandSeit)):0.00} s" : "")
				+ (AusweichAktiv ? ", AUSWEICH (Kopf und Rumpf nicht moeglich)" : "") + "; "
				+ $"P(Kopf/Rumpf/Becken/Beine/Arme) {LetzteP[0] * 100f:0}/{LetzteP[1] * 100f:0}/{LetzteP[2] * 100f:0}/{LetzteP[3] * 100f:0}/{LetzteP[4] * 100f:0} %; "
				+ $"Durchschlag Kopf {(kopfFrei ? $"{KopfDurch(boxname) * 100f:0}%" : "verdeckt")}, Rumpf {seite} {rumpf * 100f:0}%, Becken {becken * 100f:0}%; "
				+ $"Streuung {_streuung:0.000}°, Unruhe {SoftAim.Unruhe:0.000}°, Pen {_pen:0}, Schaden {_schaden:0}]";
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
			return (e < 0f) ? "verdeckt" : e.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
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
			case Knochen.Kopf:
				return Kopf;
			case Knochen.Brust:
				return Rumpf;
			case Knochen.Becken:
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

		// Mitdrehen: Zieltempo (geglaettet, aus dem Vorhalt), Feuerpunkt, geglaettete
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
		public static void Merken(Player ziel, Zone[] zonen, int anzahl, int wunsch, float entfernung, Vector3 zielTempo)
		{
			if ((object)ziel != _mitZiel)
			{
				// Neues Ziel: Glaettung der Drehraten neu beginnen - kein
				// Uebertrag der alten Drehrate, keine Spitze.
				_mitZiel = ziel;
				_mitHat = false;
				_zielRateG = Vector2.zero;
				// Zielwechsel: der naechste Eintritt in den Fangbereich loest den
				// Einfang-Schub aus, der Loese-Weg beginnt bei 0.
				SchubLoesenZuruecksetzen();
				_ruheHat = false;
			}
			_zielTempo = zielTempo;
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
					grund = "keine Schusswaffe";
				}
				else if (!(fc.Item is Weapon w))
				{
					grund = "keine Waffe";
				}
				else if (Einstellungen.Klasse(w) == Waffenklasse.Werfer)
				{
					grund = "Granatwerfer";
				}
				else if (!(fc.CurrentOperation is Player.FirearmController.Idling)
						 && !(fc.CurrentOperation is Player.FirearmController.FireOperationBase)
						 && !(fc.CurrentOperation is Player.FirearmController.RechamberOperation))
				{
					grund = fc.CurrentOperation?.GetType().Name ?? "keine Operation";
				}
				else if (fc.IsInInteraction())
				{
					grund = "Hand beschaeftigt";
				}
				else if (w.MalfState.State != Weapon.EMalfunctionState.None)
				{
					grund = "Stoerung " + w.MalfState.State;
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
		/// Geschwindigkeit (Zieltempo aus dem Vorhalt minus eigenes Tempo):
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
			return $"Waffe {w.Klasse}{(w.Eigen ? " (eigene Werte)" : "")}, "
				+ ((WaffeGrund.Length == 0) ? "bereit" : ("nicht bereit: " + WaffeGrund))
				+ (_ziel != null ? $", Feind {(feind ? "ja" : "nein")}" : "")
				+ $", Zonenwahl {ZonenGrund}, RCS {RcsGrad.x:0.000}/{RcsGrad.y:0.000}°"
				+ $", Mitdrehen {MitGrad.x:0.0}/{MitGrad.y:0.0}°/s (Ziellinie {ZielRate.x:0.0}/{ZielRate.y:0.0}°/s, eigene {_eigenRateG.x:0.0}/{_eigenRateG.y:0.0}°/s)"
				+ ((_schubBis > Time.time) ? $", Schub ja ({_schubBis - Time.time:0.00} s)" : ", Schub nein")
				+ $", Loese-Weg {_wegSumme:0.00}° {(_geloest ? "geloest" : "nicht geloest")}";
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
					if (_zonen[i].Name == "Brust")
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
			RohGrad = Vector2.zero;
			KorrGrad = Vector2.zero;
			_diagBild = Time.frameCount;
			TeilAnz = TeilReib = TeilMag = TeilSchub = TeilMit = Vector2.zero;
			TeilBild = Time.frameCount;
			Vector2 anzW = Vector2.zero, reibW = Vector2.zero;
			if (!LageBestimmen(ich, out var cos, out var kleinste))
			{
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

					Vector2 st = dRicht * l;
					// Reibung: Anteil quer oder weg von der fuehrenden Zone bremsen,
					// den Anteil zu ihr hin nie.
					if (reibungMax > 0f && (profW > 0f || profR > 0f))
					{
						bool wFuehrt = profW >= profR;
						Vector2 e = wFuehrt ? eW : eR;
						float f = reibungMax * (wFuehrt ? innenW : innenR);
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
				kamText = $" | Muendung vs. Kamera {Mathf.DeltaAngle(Rueckstoss.Gieren(blick), Rueckstoss.Gieren(richtung)):0.00}°/"
					+ $"{Mathf.DeltaAngle(Rueckstoss.Nicken(blick), Rueckstoss.Nicken(richtung)):0.00}°";
			}
			string zielText = !lage ? "kein Ziel"
				: $"{_entfernung:0.0} m, Zone {ZoneName}, Rand {Rand:0.000}°, Winkel zum Zielpunkt {Winkel:0.000}°, im Radius {(ImRadius ? "ja" : "nein")} (Radius {RadiusDiag:0.00}° = {RadiusMeterDiag:0.00} m, {Einstellungen.SoftRadiusArt.Value})"
				  + ((_wunsch >= 0) ? $", Wunschzone {_zonen[_wunsch].Name} Rand {RandWunsch:0.000}°" : "")
				  + $", Sicht {SichtGrund}"
				  + $", Fallausgleich {Zielpunkt.LetzterFall * 100f:0} cm = {Mathf.Atan2(Zielpunkt.LetzterFall, Mathf.Max(1f, Zielpunkt.LetzteEntfernung)) * 57.29578f:0.000}° auf {Zielpunkt.LetzteEntfernung:0} m"
				  + $", Streuung {Panzerung.Streuung:0.000}°, Unruhe {Unruhe:0.000}°";
			return $"[Zielhilfe] SCHUSS t={Time.time:0.000} bild={Time.frameCount} | {Kopfzeile(ich)} | {zielText}"
				+ $" | Maus roh {RohGrad.x:0.000}/{RohGrad.y:0.000}° -> korrigiert {KorrGrad.x:0.000}/{KorrGrad.y:0.000}° (vor {Time.frameCount - _diagBild} Bild)"
				+ $" | Magnet {MagnetGrad:0.0}°/s, Gewicht {Gewicht:0.00}" + kamText
				+ (ImAnschlag(ich) ? " | Anschlag" : " | Huefte");
		}

		public static string Diagnose()
		{
			if (Time.frameCount - _diagBild > 1)
			{
				return " | SOFT AIM MAUS: Eingabe nicht abgefragt | " + Kopfzeile(Spielzugriff.Ich);
			}
			if (!HatZiel)
			{
				return " | SOFT AIM MAUS: kein Ziel | " + Kopfzeile(Spielzugriff.Ich);
			}
			return $" | SOFT AIM MAUS: {Kopfzeile(Spielzugriff.Ich)} | {_entfernung:0.0} m, Zone {ZoneName}, Rand {Rand:0.000}° "
				+ ((RandWunsch >= 0f && _wunsch >= 0) ? $"(Wunschzone {_zonen[_wunsch].Name} {RandWunsch:0.000}°) " : "(Wunschzone fehlt) ")
				+ $"im Radius {(ImRadius ? "ja" : "nein")} (Radius {RadiusDiag:0.00}° = {RadiusMeterDiag:0.00} m ab Rand, {Einstellungen.SoftRadiusArt.Value}), "
				+ $"Winkel zum Zielpunkt {Winkel:0.000}°, Gewicht {Gewicht:0.00}, Magnet {MagnetGrad:0.0}°/s, "
				+ $"Maus roh {RohGrad.x:0.000}/{RohGrad.y:0.000}° -> korrigiert {KorrGrad.x:0.000}/{KorrGrad.y:0.000}°";
		}

		public static void FehlerMelden(Exception ex)
		{
			if (!_fehlerGemeldet)
			{
				_fehlerGemeldet = true;
				Protokoll?.LogWarning("[Zielhilfe] Soft Aim (Maus): " + ex);
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
				if (!Einstellungen.An.Value || Einstellungen.Art.Value != Zielmodus.SoftaimMaus)
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
					|| Einstellungen.Art.Value != Zielmodus.SoftaimMaus)
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
					Aufzeichnung.GetroffenWorden(ich, __instance, schuetze as Player);
					return;
				}
				if (damageInfo.Player == null || schuetze != (object)ich)
				{
					return;
				}
				Aufzeichnung.Treffer(ich, __instance, opfer);
				if (!Einstellungen.Diagnose.Value || Einstellungen.Art.Value != Zielmodus.SoftaimMaus)
				{
					return;
				}
				string platte = (__instance is ArmorPlateCollider ap) ? (" Platte " + ap.ArmorPlateColliderType) : "";
				SoftAim.Protokoll?.LogInfo($"[Zielhilfe] TREFFER t={Time.time:0.000} bild={Time.frameCount} | {__instance.BodyPartColliderType}{platte}"
					+ $" | an {(opfer != null ? opfer.Profile?.Nickname : "?")}"
					+ ((opfer != null) ? $" auf {Vector3.Distance(ich.Position, opfer.Position):0.0} m" : ""));
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
