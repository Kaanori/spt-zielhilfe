// ---------------------------------------------------------------
//  "7 AFK Guard" (ab 1.4.0) - die Notfall-Pause ("kurz aufs Klo").
//
//  Eine Taste (Standard Pause/Untbr) schaltet die Wache ein und wieder aus.
//  Solange sie laeuft, bleibt der Spieler stehen, wo er ist (die Wache
//  bewegt ihn nicht), und verteidigt sich selbst:
//
//    a) wer mich TRIFFT (BodyPartCollider.ApplyHit, TrefferAbgriff) oder
//       knapp an mir VORBEISCHIESST (Kugelstart InitiateShot, KugelAbgriff:
//       Flugbahn geht naeher als 1,2 m am Brustkorb vorbei), gilt eine
//       Zeit lang als Angreifer - egal ob feindlich eingestuft oder in
//       "3 Exclusions" ausgenommen; er hat ja geschossen,
//    b) jeder feindliche, lebende Bot innerhalb der Reichweite (25 m) mit
//       freier Schussbahn - feindlich nach derselben Spiellogik wie die
//       Zielhilfe (Spielzugriff.IstFeind) und mit den Ausnahmen aus "3".
//
//  Vorrang: Angreifer vor allen anderen, innerhalb einer Gruppe der naechste.
//  Das Ziel ist KLEBRIG (ab 1.4.1, AfkVorrang): es bleibt, solange es lebt und
//  (Angreifer oder in Reichweite) ist; ein Angreifer wird nie gegen einen
//  Nicht-Angreifer getauscht. Verloren gilt es erst, wenn die Schussbahn
//  laenger als "Keep target while hidden" zu ist. Gewechselt wird nur, wenn
//  ein NEUER Angreifer auftaucht (waehrend das Ziel keiner ist) oder ein
//  anderes in der gleichen Gruppe viel naeher ist (unter der halben Entfernung).
//
//  Zielen: derselbe Punkt wie bei der Zielhilfe (MainPlugin.PunktWaehlen:
//  Zone nach Panzerung bzw. freie Kopfbox, dann Zielpunkt.MitVorhalt mit
//  Vorhalt und Kugelfall fuer die SCHUSSLINIE). Gedreht wird ueber
//  Player.Rotation wie im alten Festhalte-Modus, aber gedeckelt (Grad/s).
//  Gemessen wird an der echten Schusslinie (Rueckstoss.Schusslinie, nach
//  AdjustShotVectors) - der Unterschied Waffe/Blick (Huefte, Nullung,
//  Rueckstoss) wird geglaettet herausgerechnet.
//
//  Schiessen: Player.FirearmController.SetTriggerPressed - derselbe Aufruf,
//  den die eigene Maustaste ausloest (FirearmHandsInputTranslator,
//  ECommand.ToggleShooting/EndShooting). Vollautomatisch in Feuerstoessen,
//  sonst Einzelschuesse (Feuerstoss). Nur, wenn die Schusslinie nah genug
//  am Punkt liegt. Leeres Magazin: Nachladen ueber genau den Weg der
//  R-Taste (FirearmHandsInputTranslator.Reload), sonst Hinweis.
//
//  Der Abzug wird losgelassen beim Ausschalten, ohne Ziel, bei Tod,
//  Raid-Ende, Pause (Zeit steht) und sobald ein Menue/Inventar offen ist.
//  Tod und Raid-Ende schalten die Wache ganz aus.
//
//  Die reine Logik (Vorrang, Feuerstoss) haengt nicht an Unity - der
//  Offline-Test (Zielhilfe-QuickSetupTest/) ruft sie per Reflexion auf.
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI.Screens;
using UnityEngine;

namespace Zielhilfe
{
	internal static class AfkEinstellungen
	{
		public const string Sektion = "7 AFK Guard";

		public static ConfigEntry<KeyCode> Taste;

		public static ConfigEntry<float> Reichweite;

		public static ConfigEntry<float> Merken;

		public static ConfigEntry<float> Haltezeit;

		public static ConfigEntry<float> Drehrate;

		public static ConfigEntry<float> Toleranz;

		public static ConfigEntry<float> Stoss;

		public static ConfigEntry<float> Pause;

		public static ConfigEntry<bool> Anvisieren;

		public static ConfigEntry<bool> Nachladen;

		public static ConfigEntry<bool> Hinweise;

		private static ConfigDescription D(string text, int platz, AcceptableValueBase werte = null)
		{
			return new ConfigDescription(text, werte, new ConfigurationManagerAttributes { Order = platz });
		}

		public static void Anlegen(ConfigFile c)
		{
			string s = Sektion;
			Taste = Eintrag.B(c, s, "Toggle hotkey", KeyCode.Pause, D(
				"Emergency break (\"AFK guard\"): press once in a raid to switch it ON, press again to switch it OFF. "
				+ "This key is the only off switch (it also turns off when you die or the raid ends).\n"
				+ "While ON you stay where you are and the mod defends you on its own: it shoots back at "
				+ "anyone who hit you or shot close past you, and at every hostile bot that comes within "
				+ "\"Range\" with a clear line of fire. Attackers first, then the nearest.\n"
				+ "Default Pause (Pause/Break key) - not used by the game or by the other installed mods. "
				+ "None = disabled. Single player against bots only.", 100));
			Reichweite = Eintrag.B(c, s, "Range (m)", 25f, D(
				"Hostile bots closer than this are engaged even if they have not attacked you yet. "
				+ "Attackers (they hit you or shot close past you) are engaged at any distance up to "
				+ "\"2 Target / Range in meters\". Exclusions from \"3 Exclusions\" apply here, not to attackers.",
				90, new AcceptableValueRange<float>(5f, 150f)));
			Merken = Eintrag.B(c, s, "Remember attacker (s)", 30f, D(
				"How long someone who hit you or shot close past you stays an attacker. "
				+ "Hits are remembered even while the guard is off, so switching it on right after "
				+ "being shot still knows the shooter.",
				80, new AcceptableValueRange<float>(0f, 300f)));
			Haltezeit = Eintrag.B(c, s, "Keep target while hidden (s)", 0.75f, D(
				"The current target is kept as long as it lives and is an attacker or within range - even if "
				+ "it ducks behind cover for a moment. Only after the line of fire has been blocked this long "
				+ "the guard looks for another target. An attacker is never swapped for a non-attacker while "
				+ "it is still shootable.",
				75, new AcceptableValueRange<float>(0f, 3f)));
			Drehrate = Eintrag.B(c, s, "Max turn rate (deg/s)", 360f, D(
				"How fast the view may turn towards a target. Lower = smoother, but slower on the first shot.",
				70, new AcceptableValueRange<float>(30f, 1080f)));
			Toleranz = Eintrag.B(c, s, "Aim tolerance (cm)", 10f, D(
				"Fires only while the real bullet line (including lead and bullet drop) is closer than this "
				+ "to the chosen point on the target, measured at the target's distance. "
				+ "A head is about 20 cm wide. A full-auto burst is cut short when the line drifts off "
				+ "more than three times this (at least 1.5 degrees).",
				60, new AcceptableValueRange<float>(2f, 50f)));
			Stoss = Eintrag.B(c, s, "Burst length (s)", 0.3f, D(
				"Full-auto weapons: how long the trigger is held per burst. Semi-auto/single-shot weapons fire "
				+ "single shots instead.",
				50, new AcceptableValueRange<float>(0.05f, 2f)));
			Pause = Eintrag.B(c, s, "Burst pause (s)", 0.25f, D(
				"Pause after a burst (full-auto) or between two single shots (semi-auto), so the weapon "
				+ "settles again.",
				40, new AcceptableValueRange<float>(0.05f, 2f)));
			Anvisieren = Eintrag.B(c, s, "Aim down sights", true, D(
				"On = aims down sights while engaging (more accurate) and lowers the weapon again "
				+ "3 seconds after the last target. Off = shoots from the hip.", 30));
			Nachladen = Eintrag.B(c, s, "Auto reload", true, D(
				"On = reloads by itself when the weapon is empty, exactly like pressing the reload key "
				+ "(takes a magazine from your rig/pockets). If nothing can be loaded it stops shooting "
				+ "and shows \"out of ammo\". Off = only shows the notice.", 20));
			Hinweise = Eintrag.B(c, s, "Show notice", true, D(
				"On = game notification when the guard switches on/off or needs you (out of ammo, "
				+ "malfunction, no firearm), plus a small status line at the top of the screen while it is on. "
				+ "The BepInEx log gets a line either way.", 10));
		}
	}

	/// <summary>
	/// Zielwahl der AFK-Wache, ohne Unity (Offline-Test): nur freie Kandidaten
	/// kommen als NEUES Ziel in Frage, Angreifer vor allen anderen, innerhalb der
	/// Gruppe der naechste. Das bisherige Ziel ist klebrig:
	///   - es bleibt, solange es frei ist ODER (aktuellGehalten) noch nicht lange
	///     genug verdeckt war;
	///   - ein Nicht-Angreifer weicht einem NEUEN Angreifer; ein Angreifer weicht
	///     nie einem Nicht-Angreifer;
	///   - innerhalb der gleichen Gruppe wird nur gewechselt, wenn der beste
	///     Kandidat naeher als wechselAnteil x Entfernung des bisherigen ist.
	/// </summary>
	internal static class AfkVorrang
	{
		/// <param name="aktuell">Index des bisherigen Ziels oder -1 (es darf frei[aktuell] == false sein).</param>
		/// <param name="wechselAnteil">z. B. 0,5: erst unter der halben Entfernung wird getauscht.</param>
		/// <param name="aktuellGehalten">Das bisherige Ziel ist zwar nicht frei, aber noch innerhalb der Haltezeit.</param>
		/// <returns>Index des Ziels oder -1.</returns>
		internal static int Waehle(bool[] angreifer, float[] abstand, bool[] frei, int anzahl, int aktuell, float wechselAnteil, bool aktuellGehalten)
		{
			int best = -1;
			for (int i = 0; i < anzahl; i++)
			{
				if (!frei[i])
				{
					continue;
				}
				if (best < 0
					|| (angreifer[i] && !angreifer[best])
					|| (angreifer[i] == angreifer[best] && abstand[i] < abstand[best]))
				{
					best = i;
				}
			}
			bool hat = aktuell >= 0 && aktuell < anzahl;
			if (hat && (frei[aktuell] || aktuellGehalten))
			{
				if (best < 0 || best == aktuell)
				{
					return aktuell;
				}
				if (!angreifer[aktuell] && angreifer[best])
				{
					// Neuer Angreifer: der geht vor.
					return best;
				}
				if (angreifer[aktuell] == angreifer[best] && abstand[best] < wechselAnteil * abstand[aktuell])
				{
					// Gleiche Gruppe, aber der andere ist viel naeher.
					return best;
				}
				return aktuell;
			}
			return best;
		}
	}

	/// <summary>
	/// Abzug im Takt, ohne Unity (Offline-Test). Je Bild ein Schritt; liefert,
	/// ob der Abzug jetzt gedrueckt sein soll.
	///   Vollauto: druecken, wenn bereit und die Pause um ist; halten bis
	///             Stosslaenge erreicht oder abgebrochen wird.
	///   Sonst:    kurz druecken (EinzelDruck), dann mindestens die Pause
	///             loslassen - jede neue Betaetigung ist ein Schuss.
	/// </summary>
	internal sealed class Feuerstoss
	{
		internal const float EinzelDruck = 0.05f;

		internal bool Gedrueckt;

		internal float Zeit = 999f;

		internal int Stoesse;

		internal void Zuruecksetzen()
		{
			Gedrueckt = false;
			Zeit = 999f;
		}

		/// <summary>Von aussen losgelassen (Ziel weg, Menue ...): die Pause zaehlt ab jetzt.</summary>
		internal void Losgelassen()
		{
			if (Gedrueckt)
			{
				Gedrueckt = false;
				Zeit = 0f;
			}
		}

		/// <param name="bereit">Schusslinie im Toleranzbereich, Waffe bereit, Munition da.</param>
		/// <param name="abbrechen">Ziel weg, Waffe nicht bereit oder Schusslinie weit daneben.</param>
		internal bool Schritt(float dt, bool bereit, bool abbrechen, bool vollauto, float stoss, float pause)
		{
			Zeit += Math.Max(0f, dt);
			if (Gedrueckt)
			{
				float halten = vollauto ? stoss : EinzelDruck;
				if (abbrechen || Zeit >= halten)
				{
					Gedrueckt = false;
					Zeit = 0f;
				}
			}
			else if (bereit && !abbrechen && Zeit >= pause)
			{
				Gedrueckt = true;
				Zeit = 0f;
				Stoesse++;
			}
			return Gedrueckt;
		}
	}

	internal sealed class AfkWache : MonoBehaviour
	{
		/// <summary>Laeuft die Wache? Die normale Zielhilfe ruht dann (MainPlugin.Update, MausEingabe).</summary>
		public static bool Aktiv;

		// Angreifer -> Zeitpunkt des letzten Treffers/Beinahetreffers (Time.time).
		private static readonly Dictionary<Player, float> _angreifer = new Dictionary<Player, float>();

		// Fuer das Protokoll: wer schon gemeldet wurde (nicht bei jeder Kugel).
		private static readonly HashSet<Player> _gemeldet = new HashSet<Player>();

		private const float Beinahe = 1.2f;

		// Tausch innerhalb einer Gruppe (beide Angreifer / beide nicht) erst, wenn der
		// andere unter dieser Entfernung relativ zum jetzigen Ziel liegt.
		private const float WechselAnteil = 0.5f;

		private readonly Feuerstoss _stoss = new Feuerstoss();

		private readonly List<Player> _kandidaten = new List<Player>();

		private bool[] _kAngreifer = new bool[32];

		private float[] _kAbstand = new float[32];

		private bool[] _kFrei = new bool[32];

		private Player _ziel;

		private float _naechsteAuswahl;

		private readonly VerlustZaehler _verlust = new VerlustZaehler();

		private Player.FirearmController _abzugAn;

		private bool _adsGesetzt;

		private float _naechsterAds;

		private float _letztesZiel;

		private Vector2 _versatz;

		private bool _versatzHat;

		private float _letzteRate;

		private float _naechstesNachladen;

		private int _nachladeVersuche;

		private string _zustand = "";

		private string _zeile = "";

		private GUIStyle _stil;

		// ---- Eingaenge aus den Harmony-Abgriffen ---------------------------

		/// <summary>TrefferAbgriff: ich wurde getroffen. Gilt auch bei ausgeschalteter Wache.</summary>
		public static void Getroffen(Player schuetze)
		{
			Merke(schuetze, "hit you");
		}

		/// <summary>KugelAbgriff: eine fremde Kugel startet - geht sie knapp an mir vorbei?</summary>
		public static void FremdeKugel(Player.FirearmController fc, Vector3 start, Vector3 richtung)
		{
			Player ich = Spielzugriff.Ich;
			Player schuetze = fc?._player;
			if (ich == null || schuetze == null || (object)schuetze == ich || richtung.sqrMagnitude < 0.0001f)
			{
				return;
			}
			Vector3 r = richtung.normalized;
			Vector3 v = Zielpunkt.Koerper(ich) - start;
			float t = Vector3.Dot(v, r);
			if (t <= 0f || t > 1500f)
			{
				return;
			}
			if ((v - r * t).sqrMagnitude <= Beinahe * Beinahe)
			{
				Merke(schuetze, "shot close past you");
			}
		}

		private static void Merke(Player schuetze, string was)
		{
			Player ich = Spielzugriff.Ich;
			if (schuetze == null || ich == null || (object)schuetze == ich)
			{
				return;
			}
			_angreifer[schuetze] = Time.time;
			if (Aktiv && _gemeldet.Add(schuetze))
			{
				SoftAim.Protokoll?.LogInfo("[Zielhilfe] AFK guard: " + Name(schuetze) + " " + was
					+ $" ({Vector3.Distance(ich.Position, schuetze.Position):0} m) - marked as attacker");
			}
		}

		private static bool IstAngreifer(Player p)
		{
			return _angreifer.TryGetValue(p, out float t) && Time.time - t <= AfkEinstellungen.Merken.Value;
		}

		// ---- Ablauf ---------------------------------------------------------

		private void Update()
		{
			if (AfkEinstellungen.Taste == null)
			{
				return;
			}
			bool imRaid = Spielzugriff.ImRaid;
			if (!imRaid && _angreifer.Count > 0)
			{
				_angreifer.Clear();
				_gemeldet.Clear();
			}
			KeyCode taste = AfkEinstellungen.Taste.Value;
			if (taste != KeyCode.None && Input.GetKeyDown(taste) && imRaid)
			{
				if (Aktiv)
				{
					Aus("switched off", true);
				}
				else if (!Einstellungen.An.Value)
				{
					Hinweis("AFK guard: Zielhilfe is disabled (\"Enabled\" in 0 Quick Setup).", true);
				}
				else
				{
					Ein();
				}
			}
			if (!Aktiv)
			{
				return;
			}
			try
			{
				if (!Einstellungen.An.Value)
				{
					Aus("Zielhilfe was disabled", true);
					return;
				}
				if (!imRaid)
				{
					Aus("raid ended", false);
					return;
				}
				Player ich = Spielzugriff.Ich;
				if (ich == null || ich.HealthController == null || !ich.HealthController.IsAlive)
				{
					Aus("you died", false);
					return;
				}
				if (Time.deltaTime <= 0f || !KampfBildschirm())
				{
					// Spiel angehalten oder Inventar/Menue offen: nichts tun, aber an bleiben.
					Loslassen();
					Status("paused (menu open)");
					return;
				}
				Schritt(ich, Time.deltaTime);
			}
			catch (Exception ex)
			{
				Loslassen();
				SoftAim.FehlerMelden(ex);
			}
		}

		private void Ein()
		{
			Aktiv = true;
			_ziel = null;
			_naechsteAuswahl = 0f;
			_verlust.Zuruecksetzen();
			_versatzHat = false;
			_letzteRate = 0f;
			_nachladeVersuche = 0;
			_naechstesNachladen = 0f;
			_letztesZiel = Time.time;
			_stoss.Zuruecksetzen();
			_gemeldet.Clear();
			_zustand = "";
			Status("watching");
			string taste = AfkEinstellungen.Taste.Value.ToString();
			Hinweis($"AFK guard ON - shoots back at attackers and at enemies within {AfkEinstellungen.Reichweite.Value:0} m. Press {taste} to stop.", false);
			SoftAim.Protokoll?.LogInfo($"[Zielhilfe] AFK guard ON (range {AfkEinstellungen.Reichweite.Value:0} m, "
				+ $"remember attackers {AfkEinstellungen.Merken.Value:0} s, hotkey {taste})");
		}

		private void Aus(string grund, bool melden)
		{
			Loslassen();
			AdsAus();
			Aktiv = false;
			ZonenRuhe.Instanz.Vergessen();
			_ziel = null;
			_zeile = "";
			if (melden || grund == "you died")
			{
				Hinweis("AFK guard OFF (" + grund + ")", false);
			}
			SoftAim.Protokoll?.LogInfo($"[Zielhilfe] AFK guard OFF ({grund}), {_stoss.Stoesse} trigger pulls");
			_stoss.Stoesse = 0;
		}

		private void Schritt(Player ich, float dt)
		{
			Camera kamera = Spielzugriff.Kamera;
			if (kamera == null)
			{
				Loslassen();
				return;
			}
			Zielpunkt.EigeneMerken(ich);
			if (!(ich.HandsController is Player.FirearmController fc) || !(fc.Item is Weapon w))
			{
				Loslassen();
				_ziel = null;
				Zustand("no firearm in hands");
				return;
			}
			if (_abzugAn != null && (object)_abzugAn != fc)
			{
				// Waffe gewechselt: den alten Abzug sicher loslassen.
				Loslassen();
			}
			bool bereit = SoftAim.WaffeBereit(ich, out string grund);
			bool leer = Leer(w, fc);
			if (leer)
			{
				Loslassen();
				Nachladen(ich, fc);
			}
			else
			{
				_nachladeVersuche = 0;
				if (!bereit && grund.StartsWith("malfunction"))
				{
					Zustand("weapon malfunction - fix it yourself");
				}
				else if (bereit)
				{
					Zustand("");
				}
			}

			if (_ziel != null && (_ziel.HealthController == null || !_ziel.HealthController.IsAlive))
			{
				_ziel = null;
			}
			if (_ziel == null || Time.time >= _naechsteAuswahl)
			{
				_naechsteAuswahl = Time.time + 0.2f;
				Player neu = Auswaehlen(ich, kamera);
				if ((object)neu != _ziel)
				{
					_versatzHat = _versatzHat && neu != null;
					_verlust.Zuruecksetzen();
					if (neu != null)
					{
						SoftAim.Protokoll?.LogInfo($"[Zielhilfe] AFK guard target: {Name(neu)}"
							+ $" at {Vector3.Distance(ich.Position, neu.Position):0} m"
							+ (IstAngreifer(neu) ? " (attacker)" : " (in range)"));
					}
				}
				_ziel = neu;
			}
			if (_ziel == null)
			{
				Loslassen();
				if (Time.time - _letztesZiel > 3f)
				{
					AdsAus();
				}
				Status(leer ? "watching - EMPTY" : "watching");
				return;
			}

			// Punkt auf dem Ziel: dieselbe Wahl wie die Zielhilfe. Waehrend eines
			// Feuerstosses bleibt die Zonenwahl (Kopf/Brust) stehen - der Stoss geht
			// auf denselben Punkt.
			ZonenRuhe.Instanz.Gesperrt = _stoss.Gedrueckt;
			Vector3 punkt = Zielpunkt.Fuer(_ziel);
			MainPlugin.PunktWaehlen(ich, _ziel, kamera, Einstellungen.ZoneNachPanzerung.Value, ref punkt, out _);
			Vector3 start = Zielpunkt.SichtStart(ich, kamera);
			bool frei = Zielpunkt.Sichtlinie(start, punkt, _ziel, out _, out _, out _);
			if (!frei)
			{
				Vector3 rumpf = Zielpunkt.Koerper(_ziel);
				if (Zielpunkt.Sichtlinie(start, rumpf, _ziel, out _, out _, out _))
				{
					punkt = rumpf;
					frei = true;
					// Kopf ist zu: ab jetzt Brust halten, bis der Kopf wieder eine
					// Weile frei ist - nicht bildweise zwischen beiden springen.
					ZonenRuhe.Instanz.Erzwinge(_ziel, Time.time);
				}
			}
			// Kurz verdeckt (Bot geht hinter eine Kante): das Ziel bleibt bis
			// "Keep target while hidden" erhalten, der Abzug ist solange los.
			bool gehalten = _verlust.Schritt(dt, frei, AfkEinstellungen.Haltezeit.Value);
			if (!frei)
			{
				Loslassen();
				if (!gehalten)
				{
					_ziel = null;
					_naechsteAuswahl = 0f;
				}
				return;
			}
			_letztesZiel = Time.time;
			// Soft-Aim-Bedeutung: die SCHUSSLINIE liegt auf dem Punkt, also
			// traegt der Punkt den ganzen Kugelfall (siehe MainPlugin.Update).
			Rueckstoss.AktiverAnteil = 1f;
			Vector3 soll = Zielpunkt.MitVorhalt(ich, _ziel, punkt);
			float fehler = Zielen(ich, kamera, soll, dt);

			if (AfkEinstellungen.Anvisieren.Value && bereit && !fc.IsAiming && Time.time >= _naechsterAds)
			{
				_naechsterAds = Time.time + 0.5f;
				fc.SetAim(true);
				_adsGesetzt = true;
			}

			float entfernung = Mathf.Max(0.5f, Vector3.Distance(start, soll));
			float tolGrad = Mathf.Max(0.05f, Mathf.Atan(AfkEinstellungen.Toleranz.Value * 0.01f / entfernung) * 57.29578f);
			float abbruchGrad = Mathf.Max(3f * tolGrad, 1.5f);
			// Feuerstoss-Modus (burst/doublet) wie Vollauto halten - das Spiel bricht den Stoss selbst ab.
			Weapon.EFireMode modus = w.SelectedFireMode;
			bool vollauto = modus == Weapon.EFireMode.fullauto || modus == Weapon.EFireMode.burst || modus == Weapon.EFireMode.doublet;
			bool druecken = _stoss.Schritt(dt, bereit && !leer && fehler <= tolGrad, !bereit || leer || fehler > abbruchGrad,
				vollauto, AfkEinstellungen.Stoss.Value, AfkEinstellungen.Pause.Value);
			Abzug(fc, druecken);
			Status($"{(IstAngreifer(_ziel) ? "attacker" : "enemy")} {Name(_ziel)} {Vector3.Distance(ich.Position, _ziel.Position):0} m, off by {fehler:0.0}°"
				+ (druecken ? " - FIRING" : "") + (leer ? " - EMPTY" : ""));
		}

		/// <summary>Alle Kandidaten bewerten und nach AfkVorrang waehlen.</summary>
		private Player Auswaehlen(Player ich, Camera kamera)
		{
			List<Player> alle = Spielzugriff.Welt?.AllAlivePlayersList;
			_kandidaten.Clear();
			if (alle == null)
			{
				return null;
			}
			Vector3 start = Zielpunkt.SichtStart(ich, kamera);
			float nah = AfkEinstellungen.Reichweite.Value;
			float fern = Einstellungen.Reichweite.Value;
			bool gehalten = _ziel != null && _verlust.Verloren <= AfkEinstellungen.Haltezeit.Value;
			int aktuell = -1;
			foreach (Player p in alle)
			{
				if (p == null || (object)p == ich || p.HealthController == null || !p.HealthController.IsAlive)
				{
					continue;
				}
				bool angreifer = IstAngreifer(p);
				bool istZiel = (object)p == _ziel;
				float d = Vector3.Distance(ich.Position, p.Position);
				if (angreifer)
				{
					if (d > fern)
					{
						continue;
					}
				}
				else
				{
					// Das bisherige Ziel darf etwas ueber die Reichweite hinaus laufen,
					// sonst fliegt es an der Grenze bildweise raus und rein.
					if (d > (istZiel ? nah * 1.1f : nah) || !Spielzugriff.IstFeind(p, ich))
					{
						continue;
					}
					if ((Einstellungen.ScavsAuslassen.Value && Spielzugriff.IstScav(p))
						|| (Einstellungen.PmcsAuslassen.Value && Spielzugriff.IstPmc(p))
						|| (Einstellungen.BosseAuslassen.Value && Spielzugriff.IstBoss(p)))
					{
						continue;
					}
				}
				bool frei = Zielpunkt.Sichtlinie(start, Zielpunkt.Fuer(p), p, out _, out _, out _)
					|| Zielpunkt.Sichtlinie(start, Zielpunkt.Koerper(p), p, out _, out _, out _);
				if (!frei && istZiel && _verlust.Verloren <= 0f)
				{
					// Die Schusslinie zum Ziel war im letzten Bild frei (Schritt prueft
					// den echten Zielpunkt) - ein einzelner Fehlwurf zaehlt nicht.
					frei = true;
				}
				if (!frei && !(istZiel && gehalten))
				{
					continue;
				}
				int i = _kandidaten.Count;
				if (i >= _kAbstand.Length)
				{
					Array.Resize(ref _kAngreifer, i * 2);
					Array.Resize(ref _kAbstand, i * 2);
					Array.Resize(ref _kFrei, i * 2);
				}
				_kandidaten.Add(p);
				_kAngreifer[i] = angreifer;
				_kAbstand[i] = d;
				_kFrei[i] = frei;
				if (istZiel)
				{
					aktuell = i;
				}
			}
			int wahl = AfkVorrang.Waehle(_kAngreifer, _kAbstand, _kFrei, _kandidaten.Count, aktuell, WechselAnteil, gehalten);
			return wahl < 0 ? null : _kandidaten[wahl];
		}

		/// <summary>
		/// Dreht die Blickrichtung so, dass die SCHUSSLINIE auf soll zeigt.
		/// Der Versatz Waffe gegen Blick (Huefte, Nullung, Rueckstoss) wird
		/// geglaettet abgezogen - gedreht wird der Blick, nicht die Waffe; so
		/// schwingt die Regelung nicht mit dem Nachlaufen der Waffe. Liefert
		/// den Fehler der Schusslinie VOR dieser Drehung in Grad.
		/// </summary>
		private float Zielen(Player ich, Camera kamera, Vector3 soll, float dt)
		{
			if (!Rueckstoss.Schusslinie(ich, out Vector3 start, out Vector3 richtung))
			{
				if (kamera == null)
				{
					return 180f;
				}
				start = kamera.transform.position;
				richtung = kamera.transform.forward;
			}
			Vector3 weg = soll - start;
			if (weg.sqrMagnitude < 0.0001f)
			{
				return 0f;
			}
			Vector3 wunsch = weg.normalized;
			float fehler = Vector3.Angle(richtung, wunsch);
			Vector2 rot = ich.Rotation;
			Vector2 ist = new Vector2(Mathf.DeltaAngle(rot.x, Rueckstoss.Gieren(richtung)),
									  Mathf.DeltaAngle(rot.y, Rueckstoss.Nicken(richtung)));
			if (!_versatzHat)
			{
				_versatz = ist;
				_versatzHat = true;
			}
			else if (_letzteRate < 90f)
			{
				// Nur bei ruhigem Blick nachfuehren: beim schnellen Schwenk laeuft
				// die Waffe hinterher, das ist kein bleibender Versatz.
				_versatz = Vector2.Lerp(_versatz, ist, 1f - Mathf.Exp(-dt / 0.15f));
			}
			_versatz.x = Mathf.Clamp(_versatz.x, -15f, 15f);
			_versatz.y = Mathf.Clamp(_versatz.y, -15f, 15f);
			float zielG = Rueckstoss.Gieren(wunsch) - _versatz.x;
			float zielN = Rueckstoss.Nicken(wunsch) - _versatz.y;
			Vector2 d = new Vector2(Mathf.DeltaAngle(rot.x, zielG), Mathf.DeltaAngle(rot.y, zielN));
			Vector2 schritt = d * (1f - Mathf.Exp(-dt * 20f));
			float max = AfkEinstellungen.Drehrate.Value * dt;
			if (schritt.magnitude > max)
			{
				schritt = schritt.normalized * max;
			}
			_letzteRate = (dt > 0.0001f) ? schritt.magnitude / dt : 0f;
			ich.Rotation = rot + schritt;
			return fehler;
		}

		private void Abzug(Player.FirearmController fc, bool an)
		{
			if (!an)
			{
				Loslassen();
				return;
			}
			if ((object)_abzugAn == fc)
			{
				return;
			}
			fc.SetTriggerPressed(true);
			_abzugAn = fc;
		}

		/// <summary>Abzug loslassen, falls die Wache ihn gedrueckt hat. Immer sicher aufrufbar.</summary>
		private void Loslassen()
		{
			_stoss.Losgelassen();
			if (_abzugAn == null)
			{
				return;
			}
			try
			{
				_abzugAn.SetTriggerPressed(false);
			}
			catch
			{
				// Controller schon zerstoert (Waffenwechsel, Tod) - dann gibt es nichts mehr zu druecken.
			}
			_abzugAn = null;
		}

		private void AdsAus()
		{
			if (!_adsGesetzt)
			{
				return;
			}
			_adsGesetzt = false;
			try
			{
				if (Spielzugriff.Ich?.HandsController is Player.FirearmController fc && fc.IsAiming)
				{
					fc.SetAim(false);
				}
			}
			catch
			{
			}
		}

		/// <summary>
		/// Kein Schuss mehr in der Waffe? Mit Lager: das Lager ist leer und die
		/// Waffe ruht (Idling). Mitten im Feuern/Repetieren ist das Lager kurz leer
		/// (Huelse raus, naechste Patrone kommt) - dann zaehlt nur das Magazin.
		/// Ohne Lager (Trommel): das Magazin.
		/// </summary>
		private static bool Leer(Weapon w, Player.FirearmController fc)
		{
			int magazin = w.GetCurrentMagazineCount();
			if (!w.HasChambers)
			{
				return magazin == 0;
			}
			if (w.ChamberAmmoCount > 0)
			{
				return false;
			}
			return (fc.CurrentOperation is Player.FirearmController.Idling) || magazin == 0;
		}

		/// <summary>
		/// Nachladen ueber denselben Weg wie die R-Taste. Laeuft schon eine
		/// Operation (z. B. das Nachladen selbst), wird gewartet. Bleibt die Waffe
		/// nach zwei Versuchen leer, gibt es nichts mehr zum Laden.
		/// </summary>
		private void Nachladen(Player ich, Player.FirearmController fc)
		{
			if (!(fc.CurrentOperation is Player.FirearmController.Idling))
			{
				// Laeuft schon etwas (meist das Nachladen selbst) - abwarten.
				return;
			}
			if (!AfkEinstellungen.Nachladen.Value)
			{
				Zustand("out of ammo");
				return;
			}
			if (Time.time < _naechstesNachladen)
			{
				return;
			}
			_nachladeVersuche++;
			if (_nachladeVersuche > 2)
			{
				Zustand("out of ammo");
				// Weiter selten probieren - vielleicht legt der Spieler etwas nach.
				_naechstesNachladen = Time.time + 15f;
				return;
			}
			_naechstesNachladen = Time.time + 4f;
			AdsAus();
			SoftAim.Protokoll?.LogInfo("[Zielhilfe] AFK guard: weapon empty, reloading");
			new FirearmHandsInputTranslator(ich, fc).Reload();
		}

		private static bool KampfBildschirm()
		{
			try
			{
				return EftScreenManager.Instance.CheckCurrentScreen(EEftScreenType.BattleUI);
			}
			catch
			{
				return true;
			}
		}

		private static string Name(Player p)
		{
			try
			{
				string n = p?.Profile?.Nickname;
				return string.IsNullOrEmpty(n) ? "?" : n;
			}
			catch
			{
				return "?";
			}
		}

		// ---- Anzeige --------------------------------------------------------

		/// <summary>Meldung, die den Spieler braucht - nur beim Wechsel einmal.</summary>
		private void Zustand(string z)
		{
			if (z == _zustand)
			{
				return;
			}
			_zustand = z;
			if (z.Length == 0)
			{
				return;
			}
			SoftAim.Protokoll?.LogInfo("[Zielhilfe] AFK guard: " + z);
			Hinweis("AFK guard: " + z, true);
		}

		private void Status(string s)
		{
			string z = "AFK GUARD ON - " + AfkEinstellungen.Taste.Value + " to stop | " + s
				+ (_zustand.Length > 0 ? " | " + _zustand.ToUpperInvariant() : "");
			if (z != _zeile)
			{
				_zeile = z;
			}
		}

		private static void Hinweis(string text, bool warnung)
		{
			if (!AfkEinstellungen.Hinweise.Value)
			{
				return;
			}
			try
			{
				NotificationManager.DisplayMessageNotification(text, ENotificationDurationType.Default,
					warnung ? ENotificationIconType.Alert : ENotificationIconType.Default);
			}
			catch
			{
				// Ohne Benachrichtigung geht es auch - die Zeile oben und das Protokoll bleiben.
			}
		}

		private void OnGUI()
		{
			if (!Aktiv || !AfkEinstellungen.Hinweise.Value || _zeile.Length == 0)
			{
				return;
			}
			if (_stil == null)
			{
				_stil = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
			}
			_stil.fontSize = Mathf.Max(12, Screen.height / 60);
			Rect r = new Rect(0f, Screen.height * 0.04f, Screen.width, _stil.fontSize * 2f);
			Rect s = r;
			s.x += 1f;
			s.y += 1f;
			_stil.normal.textColor = Color.black;
			GUI.Label(s, _zeile, _stil);
			_stil.normal.textColor = new Color(1f, 0.55f, 0.1f);
			GUI.Label(r, _zeile, _stil);
		}
	}
}
