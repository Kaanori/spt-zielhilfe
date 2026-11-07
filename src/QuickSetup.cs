// ---------------------------------------------------------------
//  "0 Quick Setup" (ab 1.3.0) - die wenigen Einstellungen, die fast alle
//  brauchen, ganz oben im F12-Menue:
//
//    Enabled           Hauptschalter (frueher "1 General" / Enabled)
//    Strength          Light / Medium / Strong / Custom
//    Aim at            Smart / Head / Body / Custom
//    Enemy ESP         ESP an/aus (frueher "6 ESP" / Enabled)
//    Show all settings aus = nur diese Sektion + ein paar Wichtige
//
//  Strength und Aim at schreiben nur in BESTEHENDE Einstellungen (1b, 1c-1h,
//  2 Target, 1b Zonen) - es gibt keine zweite Wahrheit. Aendert der Nutzer
//  danach einen dieser Werte von Hand, springt die Wahl auf das, was die Werte
//  gerade ergeben (ein passendes Preset oder Custom). Die Wahl "Custom" selbst
//  aendert nichts.
//
//  BESTEHENDE NUTZER: Beim ersten Start nach dem Update (Strength fehlt in der
//  cfg) werden KEINE Werte angefasst. Es wird nur gelesen, was die Werte
//  ergeben: passt alles genau zu Light/Medium/Strong, wird das eingetragen,
//  sonst Custom. "Show all settings" steht dort auf an, damit nichts
//  verschwindet; bei einer frischen Installation auf aus.
//
//  Die Preset-Tabelle und die Erkennung haengen nur an den Einstellungs-
//  Eintraegen - der Offline-Test (Zielhilfe-QuickSetupTest/) laedt die gebaute DLL und
//  ruft sie ueber Reflexion auf.
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace Zielhilfe
{
	internal enum Staerke
	{
		Light,
		Medium,
		Strong,
		Custom
	}

	internal enum Zielwahl
	{
		Smart,
		Head,
		Body,
		Custom
	}

	/// <summary>
	/// Alle Binds laufen hierueber: Der Eintrag bekommt immer ein
	/// ConfigurationManagerAttributes-Etikett, damit "Show all settings" ihn
	/// ueber das Feld Browsable aus- und einblenden kann.
	/// </summary>
	internal static class Eintrag
	{
		internal static readonly List<KeyValuePair<ConfigEntryBase, ConfigurationManagerAttributes>> Alle =
			new List<KeyValuePair<ConfigEntryBase, ConfigurationManagerAttributes>>();

		public static ConfigEntry<T> B<T>(ConfigFile c, string sektion, string key, T defaultValue, ConfigDescription d)
		{
			ConfigurationManagerAttributes attr = null;
			object[] tags = d?.Tags ?? new object[0];
			foreach (object t in tags)
			{
				attr = t as ConfigurationManagerAttributes;
				if (attr != null)
				{
					break;
				}
			}
			if (attr == null)
			{
				attr = new ConfigurationManagerAttributes();
				d = new ConfigDescription(d?.Description ?? "", d?.AcceptableValues, tags.Concat(new object[] { attr }).ToArray());
			}
			ConfigEntry<T> e = c.Bind(sektion, key, defaultValue, d);
			Alle.Add(new KeyValuePair<ConfigEntryBase, ConfigurationManagerAttributes>(e, attr));
			return e;
		}

		public static ConfigEntry<T> B<T>(ConfigFile c, string sektion, string key, T defaultValue, string beschreibung)
		{
			return B(c, sektion, key, defaultValue, new ConfigDescription(beschreibung));
		}
	}

	internal static class QuickSetup
	{
		public const string Sektion = "0 Quick Setup";

		public static ConfigEntry<Staerke> StaerkeWahl;
		public static ConfigEntry<Zielwahl> ZielWahl;
		public static ConfigEntry<bool> AlleZeigen;

		private static ConfigFile _datei;
		private static bool _sperre;
		private static bool _neuBauen;
		private static List<ConfigEntryBase> _gesteuertStaerke;
		private static List<ConfigEntryBase> _gesteuertZiel;

		// ---- Presets ------------------------------------------------------
		// Je Zeile: Light, Medium, Strong.
		// 1b: Radius in m, Anziehung, Reibung, Magnet (Grad/s), Mitdrehen-Anteil,
		//     Einfang-Schub max (Grad/s), Korrektur-Deckel (Grad/s).
		// Medium = die Werksvorgaben (so war das Mod bis 1.2.0 eingestellt).
		internal static readonly float[][] Allgemein =
		{
			new[] { 0.50f, 0.30f, 0.15f,  4f, 0.4f, 20f,  60f },
			new[] { 0.75f, 0.50f, 0.30f, 10f, 0.7f, 40f, 120f },
			new[] { 1.00f, 0.75f, 0.45f, 14f, 0.9f, 60f, 200f }
		};

		// DMR/Scharfschuetze und Schrot haben ab Werk eigene Werte (1f/1g, "Use own
		// values" an). Je Zeile: Radius, Anziehung, Reibung, Magnet.
		internal static readonly float[][] Dmr =
		{
			new[] { 0.35f, 0.30f, 0.20f,  4f },
			new[] { 0.50f, 0.50f, 0.40f, 10f },
			new[] { 0.70f, 0.75f, 0.55f, 14f }
		};

		internal static readonly float[][] Schrot =
		{
			new[] { 0.70f, 0.30f, 0.15f,  4f },
			new[] { 1.00f, 0.50f, 0.30f, 10f },
			new[] { 1.40f, 0.75f, 0.45f, 14f }
		};

		private static void Fuer(List<KeyValuePair<ConfigEntryBase, object>> l, ConfigEntryBase e, object wert)
		{
			l.Add(new KeyValuePair<ConfigEntryBase, object>(e, wert));
		}

		/// <summary>Alle Eintraege samt Sollwert, die ein Preset festlegt.</summary>
		internal static List<KeyValuePair<ConfigEntryBase, object>> StaerkeWerte(Staerke s)
		{
			int n = (int)s;
			float[] a = Allgemein[n];
			List<KeyValuePair<ConfigEntryBase, object>> l = new List<KeyValuePair<ConfigEntryBase, object>>();
			Fuer(l, Einstellungen.SoftRadiusMeter, a[0]);
			Fuer(l, Einstellungen.SoftStaerke, a[1]);
			Fuer(l, Einstellungen.SoftReibung, a[2]);
			Fuer(l, Einstellungen.SoftMagnet, a[3]);
			Fuer(l, Einstellungen.SoftMitAnteil, a[4]);
			Fuer(l, Einstellungen.SoftSchubMax, a[5]);
			Fuer(l, Einstellungen.SoftDeckel, a[6]);
			foreach (Waffenklasse k in new[] { Waffenklasse.Pistole, Waffenklasse.MP, Waffenklasse.Sturmgewehr, Waffenklasse.MG })
			{
				Fuer(l, Einstellungen.KlasseEigen[(int)k], false);
			}
			foreach (KeyValuePair<Waffenklasse, float[]> k in new[]
			{
				new KeyValuePair<Waffenklasse, float[]>(Waffenklasse.Praezision, Dmr[n]),
				new KeyValuePair<Waffenklasse, float[]>(Waffenklasse.Schrot, Schrot[n])
			})
			{
				int i = (int)k.Key;
				Fuer(l, Einstellungen.KlasseEigen[i], true);
				Fuer(l, Einstellungen.KlasseMeter[i], k.Value[0]);
				Fuer(l, Einstellungen.KlasseStaerke[i], k.Value[1]);
				Fuer(l, Einstellungen.KlasseReibung[i], k.Value[2]);
				Fuer(l, Einstellungen.KlasseMagnet[i], k.Value[3]);
				// Mitdrehen und Schub wie allgemein, damit die Klasse nicht aus der Reihe laeuft.
				Fuer(l, Einstellungen.KlasseMitAnteil[i], a[4]);
				Fuer(l, Einstellungen.KlasseSchubMax[i], a[5]);
			}
			return l;
		}

		/// <summary>Smart = nach Panzerung waehlen (Werk); Head/Body = feste Stelle.</summary>
		internal static List<KeyValuePair<ConfigEntryBase, object>> ZielWerte(Zielwahl z)
		{
			List<KeyValuePair<ConfigEntryBase, object>> l = new List<KeyValuePair<ConfigEntryBase, object>>();
			bool kopf = z != Zielwahl.Body;
			Fuer(l, Einstellungen.ZoneNachPanzerung, z == Zielwahl.Smart);
			Fuer(l, Einstellungen.Ziel, kopf ? Knochen.Head : Knochen.Chest);
			Fuer(l, Einstellungen.ZoneKopf, kopf);
			Fuer(l, Einstellungen.ZoneRumpf, true);
			Fuer(l, Einstellungen.ZoneBecken, false);
			Fuer(l, Einstellungen.ZoneBeine, false);
			return l;
		}

		private static bool Gleich(ConfigEntryBase e, object soll)
		{
			object ist = e.BoxedValue;
			if (ist is float f && soll is float g)
			{
				return Math.Abs(f - g) < 1E-4f;
			}
			return Equals(ist, soll);
		}

		private static bool Passt(List<KeyValuePair<ConfigEntryBase, object>> l)
		{
			foreach (KeyValuePair<ConfigEntryBase, object> p in l)
			{
				if (!Gleich(p.Key, p.Value))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Welches Preset ergeben die Werte gerade? Custom, wenn keins passt.</summary>
		internal static Staerke ErkenneStaerke()
		{
			foreach (Staerke s in new[] { Staerke.Medium, Staerke.Light, Staerke.Strong })
			{
				if (Passt(StaerkeWerte(s)))
				{
					return s;
				}
			}
			return Staerke.Custom;
		}

		internal static Zielwahl ErkenneZiel()
		{
			foreach (Zielwahl z in new[] { Zielwahl.Smart, Zielwahl.Head, Zielwahl.Body })
			{
				if (Passt(ZielWerte(z)))
				{
					return z;
				}
			}
			return Zielwahl.Custom;
		}

		private static void Schreibe(List<KeyValuePair<ConfigEntryBase, object>> l)
		{
			bool alt = _datei.SaveOnConfigSet;
			_datei.SaveOnConfigSet = false;
			_sperre = true;
			try
			{
				foreach (KeyValuePair<ConfigEntryBase, object> p in l)
				{
					p.Key.BoxedValue = p.Value;
				}
			}
			finally
			{
				_sperre = false;
				_datei.SaveOnConfigSet = alt;
			}
			_datei.Save();
		}

		// ---- Anlegen ------------------------------------------------------

		private static ConfigDescription Reihe(string text, int platz)
		{
			return new ConfigDescription(text, null, new ConfigurationManagerAttributes { Order = platz });
		}

		/// <summary>Zeigen die Eintraege, die auch bei "Show all settings" = aus sichtbar bleiben.</summary>
		private static readonly string[] Wichtig =
		{
			"1 General|Hotkey",
			"1b Soft Aim (mouse)|When active",
			"1b Soft Aim (mouse)|Only when aiming down sights (ADS)",
			"1b Soft Aim (mouse)|Recoil compensation horizontal (%)",
			"1b Soft Aim (mouse)|Recoil compensation vertical (%)",
			"2 Target|Range in meters",
			"3 Exclusions|Scavs",
			"3 Exclusions|PMCs",
			"3 Exclusions|Bosses and guards",
			"6 ESP|Toggle hotkey",
			"6 ESP|Maximum distance (m)",
			"7 AFK Guard|Toggle hotkey"
		};

		/// <summary>VOR NachUebernahme: legt die drei neuen Eintraege an (Enabled und Enemy ESP legen Zielhilfe.cs / Esp.cs an).</summary>
		public static void Anlegen(ConfigFile c, HashSet<ConfigDefinition> vorher)
		{
			_datei = c;
			StaerkeWahl = Eintrag.B(c, Sektion, "Strength", Staerke.Medium, Reihe(
				"How strong the aim assist is. Picks a matching set of values in \"1b\" and \"1c\" to \"1h\" "
				+ "(pull radius, pull strength, friction, magnet, target tracking, capture boost, correction cap):\n"
				+ "Light = a gentle nudge.\n"
				+ "Medium = the default (what the mod always shipped with).\n"
				+ "Strong = clearly stronger. Still driven by your mouse: it never aims for you.\n"
				+ "Custom = your own values. This is set automatically as soon as you change one of those "
				+ "values by hand; choosing Custom yourself changes nothing.\n"
				+ "Choosing Light/Medium/Strong overwrites the values listed above (also the per-class values).", 95));
			ZielWahl = Eintrag.B(c, Sektion, "Aim at", Zielwahl.Smart, Reihe(
				"Where the assist pulls to.\n"
				+ "Smart = head or torso, whichever is likely to hurt more given the enemy's armor (default).\n"
				+ "Head = the head; the torso only helps when the head is covered.\n"
				+ "Body = the torso.\n"
				+ "Custom = your own zone settings (\"Choose zone by armor\", \"Allowed zone\", \"Body part\"); set "
				+ "automatically when you change those by hand; choosing Custom yourself changes nothing.", 90));
			AlleZeigen = Eintrag.B(c, Sektion, "Show all settings", false, Reihe(
				"Off = F12 shows only this section plus a few essentials (hotkey, when active, ADS only, recoil "
				+ "compensation, range, exclusions, ESP hotkey and distance, AFK guard hotkey). On = every setting of the mod. "
				+ "Your values are kept either way; this only hides entries. Existing installations start with "
				+ "this ON so nothing disappears. The list refreshes when you tick it; if it does not, close "
				+ "and reopen F12.", 10));
			_gesteuertStaerke = StaerkeWerte(Staerke.Medium).Select(p => p.Key).ToList();
			_gesteuertZiel = ZielWerte(Zielwahl.Smart).Select(p => p.Key).ToList();
		}

		/// <summary>
		/// NACH der Einstellungsuebernahme (Migration.cs): jetzt stehen die Werte
		/// des Nutzers in den Eintraegen. Hier wird ERKANNT, nie ueberschrieben.
		/// </summary>
		public static void NachUebernahme(ConfigFile c, HashSet<ConfigDefinition> vorher)
		{
			bool erste = !vorher.Contains(new ConfigDefinition(Sektion, "Strength"));
			bool bestehend = erste && vorher.Count > 0;
			bool alt = c.SaveOnConfigSet;
			c.SaveOnConfigSet = false;
			_sperre = true;
			try
			{
				if (erste)
				{
					StaerkeWahl.Value = ErkenneStaerke();
					ZielWahl.Value = ErkenneZiel();
					if (bestehend)
					{
						AlleZeigen.Value = true;
					}
				}
				else
				{
					// Normaler Start: stimmt die gespeicherte Wahl nicht mehr zu den Werten
					// (cfg von Hand bearbeitet), zieht die WAHL nach - die Werte bleiben.
					if (StaerkeWahl.Value != Staerke.Custom && StaerkeWahl.Value != ErkenneStaerke())
					{
						StaerkeWahl.Value = ErkenneStaerke();
					}
					if (ZielWahl.Value != Zielwahl.Custom && ZielWahl.Value != ErkenneZiel())
					{
						ZielWahl.Value = ErkenneZiel();
					}
				}
			}
			finally
			{
				_sperre = false;
				c.SaveOnConfigSet = alt;
			}
			if (erste)
			{
				c.Save();
			}
			Sichtbarkeit();
			c.SettingChanged += Geaendert;
		}

		// ---- Laufzeit -----------------------------------------------------

		private static void Geaendert(object sender, SettingChangedEventArgs e)
		{
			if (_sperre)
			{
				return;
			}
			ConfigEntryBase s = e.ChangedSetting;
			if (s == StaerkeWahl)
			{
				if (StaerkeWahl.Value != Staerke.Custom)
				{
					Schreibe(StaerkeWerte(StaerkeWahl.Value));
				}
			}
			else if (s == ZielWahl)
			{
				if (ZielWahl.Value != Zielwahl.Custom)
				{
					Schreibe(ZielWerte(ZielWahl.Value));
				}
			}
			else if (s == AlleZeigen)
			{
				Sichtbarkeit();
				_neuBauen = true;
			}
			else if (_gesteuertStaerke.Contains(s))
			{
				SetzeWahl(StaerkeWahl, ErkenneStaerke());
			}
			else if (_gesteuertZiel.Contains(s))
			{
				SetzeWahl(ZielWahl, ErkenneZiel());
			}
		}

		private static void SetzeWahl<T>(ConfigEntry<T> e, T wert)
		{
			if (Equals(e.Value, wert))
			{
				return;
			}
			_sperre = true;
			try
			{
				e.Value = wert;
			}
			finally
			{
				_sperre = false;
			}
		}

		/// <summary>Blendet Eintraege ein/aus (Feld Browsable; der ConfigurationManager liest es beim Aufbau der Liste).</summary>
		internal static void Sichtbarkeit()
		{
			bool alle = AlleZeigen.Value;
			foreach (KeyValuePair<ConfigEntryBase, ConfigurationManagerAttributes> p in Eintrag.Alle)
			{
				ConfigDefinition d = p.Key.Definition;
				bool sichtbar = alle || d.Section == Sektion || Array.IndexOf(Wichtig, d.Section + "|" + d.Key) >= 0;
				p.Value.Browsable = sichtbar ? (bool?)null : false;
			}
		}

		/// <summary>
		/// Pro Bild aufgerufen: ist F12 offen, wenn "Show all settings" umgeschaltet wurde,
		/// laesst die Liste neu aufbauen (nicht mitten im Zeichnen des Menues).
		/// </summary>
		public static void Tick()
		{
			if (!_neuBauen)
			{
				return;
			}
			_neuBauen = false;
			try
			{
				foreach (KeyValuePair<string, BepInEx.PluginInfo> p in BepInEx.Bootstrap.Chainloader.PluginInfos)
				{
					if (p.Key.IndexOf("configurationmanager", StringComparison.OrdinalIgnoreCase) < 0 || p.Value.Instance == null)
					{
						continue;
					}
					Type t = p.Value.Instance.GetType();
					PropertyInfo offen = t.GetProperty("DisplayingWindow");
					MethodInfo bauen = t.GetMethod("BuildSettingList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (offen != null && bauen != null && (bool)offen.GetValue(p.Value.Instance, null))
					{
						bauen.Invoke(p.Value.Instance, null);
					}
				}
			}
			catch
			{
				// Kein Drama: dann zeigt F12 die Aenderung beim naechsten Oeffnen.
			}
		}
	}
}
