// ---------------------------------------------------------------
//  Gegner-ESP (ab 1.2.0) - Rahmen, Namen, Entfernung und Lebensbalken
//  der Gegner im Bild. Ab Werk AUS.
//
//  WIE GEZEICHNET WIRD (aus der Erfahrung mit QuestItemESP, siehe dort
//  MarkenKamera.cs / Anzeigen.cs):
//   * Kamera: die Kamera, die das Bild wirklich zeichnet (Camera.main,
//     "FPS Camera"), NICHT CameraManager.Instance.Camera - die kann die
//     Waffenkamera mit anderem Blickwinkel sein.
//   * Umrechnung in 0..1 (Viewport) statt in Pixel der Kamera: Mit
//     Aufloesungsskalierung / FSR 4 (Render 2560x1440 -> Ausgabe 3840x2160)
//     liefert WorldToScreenPoint Pixel der Render-Aufloesung, OnGUI zeichnet
//     aber in Fensteraufloesung. 0..1 mal Screen.width/height passt immer.
//     Gerechnet wird mit nonJitteredProjectionMatrix, damit das
//     Sub-Pixel-Zittern des Hochskalierers die Rahmen nicht wackeln laesst.
//   * Gezeichnet wird mit IMGUI AUF das fertige Bild: kein Tiefentest, keine
//     Occlusion-Culling-Probleme (Rahmen sind durch Waende sichtbar).
//   * Wann: nicht im Inventar/Menue, nicht nach dem Tod, nicht in der
//     Verfolgerperspektive, nicht beim Zielen durch ein vergroesserndes
//     Visier (dessen Linsenbild hat eigene Kamera/Blickwinkel; abschaltbar
//     per Einstellung, dann stimmt die Lage aber nicht).
//
//  LEISTUNG: keine Zuweisungen je Bild (Eintraege, Strings und Strahlpuffer
//  werden wiederverwendet), Sichtpruefungen gedrosselt (Standard 8 Hz je
//  Gegner, hoechstens 6 Gegner je Bild), bei ausgeschaltetem ESP laeuft
//  nur eine Tastenabfrage.
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.UI.Screens;
using UnityEngine;

namespace Zielhilfe
{
	internal enum EspStil
	{
		Box,
		CornerBox,
		Marker
	}

	internal enum EspTyp
	{
		Usec,
		Bear,
		Scav,
		Boss,
		Guard,
		Raider,
		Rogue,
		Cultist,
		Other
	}

	internal static class EspEinstellungen
	{
		public static ConfigEntry<bool> An;
		public static ConfigEntry<KeyCode> Taste;
		public static ConfigEntry<float> Reichweite;
		public static ConfigEntry<bool> NurFeinde;
		public static ConfigEntry<bool> NurMitHilfe;
		public static ConfigEntry<bool> DurchOptik;
		public static ConfigEntry<float> SichtTakt;
		public static readonly ConfigEntry<bool>[] Zeigen = new ConfigEntry<bool>[9];
		public static readonly ConfigEntry<Color>[] Farbe = new ConfigEntry<Color>[9];

		public static ConfigEntry<EspStil> Stil;
		public static ConfigEntry<bool> ZeigeTyp;
		public static ConfigEntry<bool> ZeigeBossName;
		public static ConfigEntry<bool> ZeigeEntfernung;
		public static ConfigEntry<bool> ZeigeLeben;
		public static ConfigEntry<bool> ZeigeKopf;
		public static ConfigEntry<bool> ZeigeVerdeckte;
		public static ConfigEntry<float> AlphaSichtbar;
		public static ConfigEntry<float> AlphaVerdeckt;
		public static ConfigEntry<float> FernAb;
		public static ConfigEntry<float> SchriftProzent;
		public static ConfigEntry<float> Linie;
		public static ConfigEntry<float> MarkerProzent;

		internal static readonly string[] TypName =
			{ "PMC USEC", "PMC BEAR", "Scav", "Boss", "Guard / Follower", "Raider", "Rogue", "Cultist", "Other" };

		private static ConfigDescription D(string text, int platz, AcceptableValueBase bereich = null)
		{
			return new ConfigDescription(text, bereich, new ConfigurationManagerAttributes { Order = platz });
		}

		public static void Anlegen(ConfigFile c)
		{
			const string s = "6 ESP";
			An = c.Bind(s, "Enabled", false, D("Draws boxes, labels, distance and health bars for enemies on your screen, "
				+ "also through walls. OFF by default. Single-player (offline) only - like the rest of this mod it "
				+ "is a cheat-like feature.", 100));
			Taste = c.Bind(s, "Toggle hotkey", KeyCode.Insert, D("Switches the ESP on and off during the raid "
				+ "(changes \"Enabled\"). Set to None to disable the hotkey.", 99));
			Reichweite = c.Bind(s, "Maximum distance (m)", 300f, D("Enemies farther away are not drawn.", 98,
				new AcceptableValueRange<float>(10f, 1500f)));
			NurFeinde = c.Bind(s, "Hostile enemies only", true, D("On = only bots the game considers hostile to you "
				+ "(same logic as the aim assist; friendly Scavs and allies are left out). Off = every living bot.", 97));
			NurMitHilfe = c.Bind(s, "Only while the aim assist is active", false, D("On = the ESP appears only while the "
				+ "aim assist is active (with \"When active\" = HoldKey this means: only while you hold the hotkey).", 96));
			DurchOptik = c.Bind(s, "Draw while aiming through a scope", false, D("Off = hidden while aiming through a magnifying "
				+ "scope. A scope draws its picture with its own camera and field of view, so markers computed against the main "
				+ "camera would not line up with the lens image. On = draw anyway (positions will be wrong in the lens). "
				+ "Iron sights and red dots are not affected.", 95));
			SichtTakt = c.Bind(s, "Visibility checks per second", 8f, D("How often the line of sight to each enemy is "
				+ "re-checked (visible vs. occluded). Higher = more responsive colour change, more CPU. 0 = never check "
				+ "(everything counts as visible).", 94, new AcceptableValueRange<float>(0f, 30f)));
			bool[] standard = { true, true, true, true, true, true, true, true, true };
			for (int i = 0; i < 9; i++)
			{
				Zeigen[i] = c.Bind(s, "Show: " + TypName[i], standard[i], D("Draw " + TypName[i] + " enemies.", 90 - i));
			}

			const string a = "6b ESP Appearance";
			Stil = c.Bind(a, "Draw style", EspStil.CornerBox, D("Box = full rectangle. CornerBox = only the corners. "
				+ "Marker = a small marker above the head.", 100));
			ZeigeTyp = c.Bind(a, "Show type label", true, D("Writes the enemy type (USEC, BEAR, Scav, ...) above the box.", 99));
			ZeigeBossName = c.Bind(a, "Show boss names", true, D("Bosses (and their guards' leaders) are labelled with their name "
				+ "instead of the type.", 98));
			ZeigeEntfernung = c.Bind(a, "Show distance", true, D("Writes the distance in meters below the box.", 97));
			ZeigeLeben = c.Bind(a, "Show health bar", true, D("Vertical health bar left of the box (total health).", 96));
			ZeigeKopf = c.Bind(a, "Show head dot", false, D("Small dot on the head.", 95));
			ZeigeVerdeckte = c.Bind(a, "Draw occluded enemies", true, D("Off = only enemies you have a clear line of sight to "
				+ "are drawn (then the ESP does not show through walls).", 94));
			AlphaSichtbar = c.Bind(a, "Opacity when visible", 1f, D("Opacity of the drawing for enemies in line of sight.", 93,
				new AcceptableValueRange<float>(0.05f, 1f)));
			AlphaVerdeckt = c.Bind(a, "Opacity when occluded", 0.45f, D("Opacity for enemies behind cover. Lower than the "
				+ "visible value makes them easy to tell apart.", 92, new AcceptableValueRange<float>(0.05f, 1f)));
			FernAb = c.Bind(a, "Simplify beyond (m)", 150f, D("Enemies farther than this get only a small marker and the "
				+ "distance - no box, type label or health bar. 0 = never simplify.", 91, new AcceptableValueRange<float>(0f, 1500f)));
			SchriftProzent = c.Bind(a, "Label size (% of screen height)", 1.5f, D("Font size relative to the screen height, so "
				+ "it looks the same at 1080p and 4K.", 90, new AcceptableValueRange<float>(0.8f, 4f)));
			Linie = c.Bind(a, "Line thickness (pixels at 1080p)", 1.5f, D("Scaled with the screen height.", 89,
				new AcceptableValueRange<float>(1f, 6f)));
			MarkerProzent = c.Bind(a, "Marker size (% of screen height)", 0.9f, D("Size of the small marker / head dot.", 88,
				new AcceptableValueRange<float>(0.3f, 3f)));
			Color[] farben =
			{
				new Color(0.30f, 0.60f, 1.00f), new Color(1.00f, 0.35f, 0.35f), new Color(1.00f, 0.85f, 0.20f),
				new Color(1.00f, 0.15f, 0.85f), new Color(1.00f, 0.55f, 0.15f), new Color(0.20f, 1.00f, 0.90f),
				new Color(0.70f, 0.45f, 1.00f), new Color(0.65f, 1.00f, 0.25f), new Color(0.85f, 0.85f, 0.85f)
			};
			for (int i = 0; i < 9; i++)
			{
				Farbe[i] = c.Bind(a, "Color: " + TypName[i], farben[i], D("Color for " + TypName[i] + ".", 80 - i));
			}
		}
	}

	/// <summary>Gemeinsamer Zustand zwischen Rechner (Update) und Zeichner (OnGUI).</summary>
	internal sealed class EspEintrag
	{
		public Player Spieler;
		public EspTyp Typ;
		public string Name = "";
		public bool Mark;
		public bool Sichtbar = true;
		public float NaechsterTest;
		public float Leben = 1f;
		public float NaechstesLeben;
	}

	internal static class EspDaten
	{
		public static readonly Dictionary<Player, EspEintrag> Karte = new Dictionary<Player, EspEintrag>(64);
		public static EspEintrag[] Aktiv = new EspEintrag[64];
		public static int Anzahl;
		public static Camera Kamera;
		public static bool Zeichnen;
		public static Player Ich;

		// "123 m" - je ganzer Meter nur einmal erzeugt.
		private static readonly string[] _meterText = new string[1601];

		public static string Meter(float m)
		{
			int i = Mathf.Clamp((int)(m + 0.5f), 0, 1600);
			return _meterText[i] ?? (_meterText[i] = i.ToString() + " m");
		}
	}

	/// <summary>Rechner: Tastenabfrage, Gegnerliste (4x je Sekunde), gedrosselte Sichtpruefung, Lebenswerte.</summary>
	internal sealed class EnemyEsp : MonoBehaviour
	{
		private EspZeichner _zeichner;
		private float _naechsteListe;
		private int _kameraFrame = -1000;
		private bool _kameraGemeldet;
		private bool _gateGemeldet;
		private bool _lebenGemeldet;
		private bool _war;
		private readonly List<Player> _weg = new List<Player>(16);
		private readonly Dictionary<Collider, bool> _durch = new Dictionary<Collider, bool>(256);
		private static readonly RaycastHit[] Strahl = new RaycastHit[24];

		private void Awake()
		{
			_zeichner = gameObject.AddComponent<EspZeichner>();
			_zeichner.useGUILayout = false;
			_zeichner.enabled = false;
		}

		private void Update()
		{
			if (EspEinstellungen.An == null)
			{
				return;
			}
			KeyCode taste = EspEinstellungen.Taste.Value;
			if (taste != KeyCode.None && Input.GetKeyDown(taste))
			{
				EspEinstellungen.An.Value = !EspEinstellungen.An.Value;
			}
			if (!EspEinstellungen.An.Value || !Einstellungen.An.Value)
			{
				Aus();
				return;
			}
			Player ich = Spielzugriff.ImRaid ? Spielzugriff.Ich : null;
			if (ich == null || !Erlaubt(ich) || (EspEinstellungen.NurMitHilfe.Value && !MainPlugin.HilfeAktiv))
			{
				Aus();
				return;
			}
			Camera kam = KameraHolen();
			if (kam == null)
			{
				Aus();
				return;
			}
			if (!_war)
			{
				_war = true;
				_durch.Clear();
			}
			EspDaten.Kamera = kam;
			EspDaten.Ich = ich;
			float jetzt = Time.time;
			if (jetzt >= _naechsteListe)
			{
				_naechsteListe = jetzt + 0.25f;
				ListeErneuern(ich);
			}
			Pruefen(ich, kam, jetzt);
			EspDaten.Zeichnen = true;
			_zeichner.enabled = true;
		}

		private void Aus()
		{
			EspDaten.Zeichnen = false;
			if (_zeichner != null && _zeichner.enabled)
			{
				_zeichner.enabled = false;
			}
			if (_war)
			{
				_war = false;
				EspDaten.Karte.Clear();
				EspDaten.Anzahl = 0;
				_durch.Clear();
			}
		}

		/// <summary>Camera.main ("FPS Camera") - die Kamera, die das Bild zeichnet.</summary>
		private Camera KameraHolen()
		{
			Camera k = EspDaten.Kamera;
			if (k == null || !k.isActiveAndEnabled || Time.frameCount - _kameraFrame > 120)
			{
				_kameraFrame = Time.frameCount;
				k = Camera.main;
				if (k == null || !k.isActiveAndEnabled)
				{
					k = Spielzugriff.Kamera;
				}
				if (k != null && !_kameraGemeldet)
				{
					_kameraGemeldet = true;
					SoftAim.Protokoll?.LogInfo("[Zielhilfe] ESP draws against camera \"" + k.name + "\" (FOV " + k.fieldOfView.ToString("0.0") + ")");
				}
			}
			return k;
		}

		// Wann darf gezeichnet werden (nach QuestItemESP/Anzeigen.cs).
		private bool Erlaubt(Player ich)
		{
			try
			{
				if (ich.PointOfView != EPointOfView.FirstPerson)
				{
					return false;
				}
				if (ich.HealthController == null || !ich.HealthController.IsAlive)
				{
					return false;
				}
				if (!EftScreenManager.Instance.CheckCurrentScreen(EEftScreenType.BattleUI))
				{
					return false;
				}
				if (!EspEinstellungen.DurchOptik.Value && DurchOptikZielend(ich))
				{
					return false;
				}
			}
			catch (Exception ex)
			{
				if (!_gateGemeldet)
				{
					_gateGemeldet = true;
					SoftAim.Protokoll?.LogWarning("[Zielhilfe] ESP gate check: " + ex.GetType().Name + " - " + ex.Message + " (drawing continues)");
				}
			}
			return true;
		}

		private static bool DurchOptikZielend(Player ich)
		{
			try
			{
				if (!(ich.HandsController is Player.FirearmController waffe) || !waffe.IsAiming)
				{
					return false;
				}
				var visier = ich.ProceduralWeaponAnimation?.CurrentScope;
				return visier != null && visier.IsOptic;
			}
			catch
			{
				return false;
			}
		}

		internal static EspTyp Einordnen(Player p)
		{
			string rolle = Spielzugriff.RolleKlein(p);
			if (rolle.Contains("pmcbot"))
			{
				return EspTyp.Raider;
			}
			if (rolle.Contains("exusec"))
			{
				return EspTyp.Rogue;
			}
			if (rolle.Contains("sectantpriest"))
			{
				return EspTyp.Boss;
			}
			if (rolle.Contains("sectant"))
			{
				return EspTyp.Cultist;
			}
			if (rolle.Contains("follower"))
			{
				return EspTyp.Guard;
			}
			if (Spielzugriff.IstBoss(p))
			{
				return EspTyp.Boss;
			}
			ProfileInfo info = p?.Profile?.Info;
			if (info != null)
			{
				if (info.Side == EPlayerSide.Usec)
				{
					return EspTyp.Usec;
				}
				if (info.Side == EPlayerSide.Bear)
				{
					return EspTyp.Bear;
				}
			}
			return Spielzugriff.IstScav(p) ? EspTyp.Scav : EspTyp.Other;
		}

		private void ListeErneuern(Player ich)
		{
			List<Player> alle = Spielzugriff.Welt?.AllAlivePlayersList;
			if (alle == null)
			{
				EspDaten.Anzahl = 0;
				return;
			}
			foreach (EspEintrag e in EspDaten.Karte.Values)
			{
				e.Mark = false;
			}
			float maxQuadrat = EspEinstellungen.Reichweite.Value * EspEinstellungen.Reichweite.Value * 1.5f;
			Vector3 mein = ich.Position;
			int n = 0;
			for (int i = 0; i < alle.Count; i++)
			{
				Player p = alle[i];
				if (p == null || p == ich || p.HealthController == null || !p.HealthController.IsAlive)
				{
					continue;
				}
				if ((p.Position - mein).sqrMagnitude > maxQuadrat)
				{
					continue;
				}
				if (EspEinstellungen.NurFeinde.Value && !Spielzugriff.IstFeind(p, ich))
				{
					continue;
				}
				if (!EspDaten.Karte.TryGetValue(p, out EspEintrag e))
				{
					e = new EspEintrag { Spieler = p, NaechsterTest = Time.time + (i % 8) * 0.02f };
					e.Typ = Einordnen(p);
					string nick = "";
					try
					{
						nick = p.Profile?.Info?.Nickname ?? "";
					}
					catch
					{
					}
					e.Name = nick;
					EspDaten.Karte[p] = e;
				}
				e.Mark = true;
				if (!EspEinstellungen.Zeigen[(int)e.Typ].Value)
				{
					continue;
				}
				if (n >= EspDaten.Aktiv.Length)
				{
					Array.Resize(ref EspDaten.Aktiv, n * 2);
				}
				EspDaten.Aktiv[n++] = e;
			}
			EspDaten.Anzahl = n;
			_weg.Clear();
			foreach (KeyValuePair<Player, EspEintrag> kv in EspDaten.Karte)
			{
				if (!kv.Value.Mark)
				{
					_weg.Add(kv.Key);
				}
			}
			for (int i = 0; i < _weg.Count; i++)
			{
				EspDaten.Karte.Remove(_weg[i]);
			}
		}

		// Sichtpruefung und Lebenswert, gedrosselt.
		private void Pruefen(Player ich, Camera kam, float jetzt)
		{
			float takt = EspEinstellungen.SichtTakt.Value;
			Zielpunkt.EigeneMerken(ich);
			Vector3 auge = kam.transform.position;
			float abstand = (takt > 0f) ? 1f / takt : 0f;
			int budget = 6;
			for (int i = 0; i < EspDaten.Anzahl; i++)
			{
				EspEintrag e = EspDaten.Aktiv[i];
				Player p = e.Spieler;
				if (p == null)
				{
					continue;
				}
				if (jetzt >= e.NaechstesLeben && EspEinstellungen.ZeigeLeben.Value)
				{
					e.NaechstesLeben = jetzt + 0.2f;
					e.Leben = LebenHolen(p, e.Leben);
				}
				if (takt <= 0f)
				{
					e.Sichtbar = true;
					continue;
				}
				if (jetzt < e.NaechsterTest || budget <= 0)
				{
					continue;
				}
				budget--;
				e.NaechsterTest = jetzt + abstand;
				e.Sichtbar = SichtFrei(p, auge);
			}
		}

		private bool SichtFrei(Player p, Vector3 auge)
		{
			HashSet<Collider> zielK = null;
			Transform wurzel = null;
			Vector3 kopf;
			try
			{
				zielK = p.PlayerBones?.BodyPartCollidersHashSet;
				wurzel = p.Transform?.Original;
				kopf = p.PlayerBones != null && p.PlayerBones.Head != null ? p.PlayerBones.Head.position : p.Position + Vector3.up * 1.6f;
			}
			catch
			{
				kopf = p.Position + Vector3.up * 1.6f;
			}
			if (Frei(auge, kopf, zielK, wurzel))
			{
				return true;
			}
			return Frei(auge, p.Position + Vector3.up * 1.1f, zielK, wurzel);
		}

		private bool Frei(Vector3 start, Vector3 punkt, HashSet<Collider> zielK, Transform wurzel)
		{
			Vector3 weg = punkt - start;
			float laenge = weg.magnitude;
			if (laenge < 0.05f)
			{
				return true;
			}
			int n = Physics.RaycastNonAlloc(start, weg / laenge, Strahl, laenge, Durchschuss.Ebenenmaske(), QueryTriggerInteraction.Ignore);
			for (int i = 0; i < n; i++)
			{
				Collider c = Strahl[i].collider;
				if (c == null || Zielpunkt.IstEigener(c) || Zielpunkt.ZumZiel(c, zielK, wurzel))
				{
					continue;
				}
				// Durchschiessbares (Glas, Gitter ...) einmal je Kollider
				// bewerten und merken - die Bewertung erzeugt Texte.
				if (!_durch.TryGetValue(c, out bool durch))
				{
					durch = Durchschuss.Durchlaessig(c, out _);
					_durch[c] = durch;
				}
				if (!durch)
				{
					return false;
				}
			}
			return true;
		}

		private float LebenHolen(Player p, float alt)
		{
			try
			{
				var w = p.HealthController.GetBodyPartHealth(EBodyPart.Common, false);
				if (w.Maximum > 0.01f)
				{
					return Mathf.Clamp01(w.Current / w.Maximum);
				}
			}
			catch (Exception ex)
			{
				if (!_lebenGemeldet)
				{
					_lebenGemeldet = true;
					SoftAim.Protokoll?.LogWarning("[Zielhilfe] ESP health read failed: " + ex.GetType().Name + " - " + ex.Message);
				}
			}
			return alt;
		}
	}

	/// <summary>Zeichner (OnGUI). Nur aktiv, solange der Rechner zeichnen will.</summary>
	internal sealed class EspZeichner : MonoBehaviour
	{
		private GUIStyle _stil;
		private int _stilGroesse = -1;
		private Texture2D _weiss;

		private void OnGUI()
		{
			if (Event.current.type != EventType.Repaint || !EspDaten.Zeichnen)
			{
				return;
			}
			Camera kam = EspDaten.Kamera;
			if (kam == null)
			{
				return;
			}
			if (_stil == null)
			{
				_stil = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow, wordWrap = false };
				_weiss = Texture2D.whiteTexture;
			}
			float sw = Screen.width;
			float sh = Screen.height;
			Rect kr = kam.rect;
			// Ohne Zittern des Hochskalierers (FSR/TAA): nicht verwackelte Projektion.
			Matrix4x4 m = kam.nonJitteredProjectionMatrix * kam.worldToCameraMatrix;
			float pxMeter = m.m11 * 0.5f * sh * kr.height;
			float schrift = Mathf.Max(8f, sh * EspEinstellungen.SchriftProzent.Value * 0.01f);
			int fs = (int)(schrift + 0.5f);
			if (fs != _stilGroesse)
			{
				_stilGroesse = fs;
				_stil.fontSize = fs;
			}
			float dick = Mathf.Max(1f, sh / 1080f * EspEinstellungen.Linie.Value);
			float marker = Mathf.Max(3f, sh * EspEinstellungen.MarkerProzent.Value * 0.01f);
			float maxM = EspEinstellungen.Reichweite.Value;
			float fern = EspEinstellungen.FernAb.Value;
			EspStil stil = EspEinstellungen.Stil.Value;
			bool zeigeVerdeckte = EspEinstellungen.ZeigeVerdeckte.Value;
			float aS = EspEinstellungen.AlphaSichtbar.Value;
			float aV = EspEinstellungen.AlphaVerdeckt.Value;
			bool typ = EspEinstellungen.ZeigeTyp.Value;
			bool boss = EspEinstellungen.ZeigeBossName.Value;
			bool dist = EspEinstellungen.ZeigeEntfernung.Value;
			bool leben = EspEinstellungen.ZeigeLeben.Value;
			bool kopfPunkt = EspEinstellungen.ZeigeKopf.Value;
			Color vorher = GUI.color;
			Vector3 auge = kam.transform.position;

			for (int i = 0; i < EspDaten.Anzahl; i++)
			{
				EspEintrag e = EspDaten.Aktiv[i];
				Player p = e.Spieler;
				if (p == null)
				{
					continue;
				}
				Vector3 fuss = p.Position;
				float d = (fuss - auge).magnitude;
				if (d > maxM)
				{
					continue;
				}
				if (!e.Sichtbar && !zeigeVerdeckte)
				{
					continue;
				}
				Vector3 kopf3 = fuss + Vector3.up * 1.7f;
				try
				{
					if (p.PlayerBones != null && p.PlayerBones.Head != null)
					{
						kopf3 = p.PlayerBones.Head.position;
					}
				}
				catch
				{
				}
				Vector3 oben = kopf3 + Vector3.up * 0.18f;
				if (!Proj(ref m, kr, fuss, sw, sh, out float fx, out float fy, out float tiefe)
					|| !Proj(ref m, kr, oben, sw, sh, out float ox, out float oy, out _))
				{
					continue;
				}
				float pxM = pxMeter / tiefe;
				float pad = 0.32f * pxM;
				float links = Mathf.Min(fx, ox) - pad;
				float rechts = Mathf.Max(fx, ox) + pad;
				float obenY = Mathf.Min(fy, oy);
				float untenY = Mathf.Max(fy, oy);
				if (rechts < 0f || links > sw || untenY < 0f || obenY > sh)
				{
					continue;
				}

				Color c = EspEinstellungen.Farbe[(int)e.Typ].Value;
				c.a *= e.Sichtbar ? aS : aV;
				bool nah = fern <= 0f || d <= fern;
				float mitteX = (links + rechts) * 0.5f;

				if (!nah || stil == EspStil.Marker)
				{
					Rechteck(mitteX - marker * 0.5f, obenY - marker * 1.6f, marker, marker, c);
				}
				else if (stil == EspStil.Box)
				{
					Rahmen(links, obenY, rechts - links, untenY - obenY, dick, c);
				}
				else
				{
					Ecken(links, obenY, rechts - links, untenY - obenY, dick, c);
				}

				if (kopfPunkt && nah)
				{
					if (Proj(ref m, kr, kopf3, sw, sh, out float kx, out float ky, out _))
					{
						Rechteck(kx - marker * 0.5f, ky - marker * 0.5f, marker, marker, c);
					}
				}

				if (nah && leben && stil != EspStil.Marker)
				{
					float bh = untenY - obenY;
					float bx = links - dick * 3f;
					Color hint = new Color(0f, 0f, 0f, 0.55f * c.a);
					Rechteck(bx - dick, obenY - dick, dick * 2f, bh + dick * 2f, hint);
					Color fuell = Color.Lerp(new Color(1f, 0.15f, 0.1f, c.a), new Color(0.2f, 1f, 0.2f, c.a), e.Leben);
					float fh = bh * e.Leben;
					Rechteck(bx - dick * 0.5f, untenY - fh, dick, fh, fuell);
				}

				if (nah && typ && stil != EspStil.Marker)
				{
					string text = null;
					if (e.Typ == EspTyp.Boss && boss && e.Name.Length > 0)
					{
						text = e.Name;
					}
					else
					{
						text = EspEinstellungen.TypName[(int)e.Typ];
					}
					Text(mitteX, obenY - schrift * 0.75f, text, c);
				}
				if (dist)
				{
					float ty = (nah && stil != EspStil.Marker) ? untenY + schrift * 0.75f : obenY - marker * 1.6f - schrift * 0.8f;
					Text(mitteX, ty, EspDaten.Meter(d), c);
				}
			}
			GUI.color = vorher;
		}

		private static bool Proj(ref Matrix4x4 m, Rect kr, Vector3 p, float sw, float sh, out float x, out float y, out float tiefe)
		{
			tiefe = m.m30 * p.x + m.m31 * p.y + m.m32 * p.z + m.m33;
			x = 0f;
			y = 0f;
			if (tiefe <= 0.05f)
			{
				return false;
			}
			float cx = (m.m00 * p.x + m.m01 * p.y + m.m02 * p.z + m.m03) / tiefe;
			float cy = (m.m10 * p.x + m.m11 * p.y + m.m12 * p.z + m.m13) / tiefe;
			float vx = cx * 0.5f + 0.5f;
			float vy = cy * 0.5f + 0.5f;
			x = (kr.x + vx * kr.width) * sw;
			y = (1f - (kr.y + vy * kr.height)) * sh;
			return true;
		}

		private void Rechteck(float x, float y, float b, float h, Color c)
		{
			GUI.color = c;
			GUI.DrawTexture(new Rect(x, y, b, h), _weiss);
		}

		private void Rahmen(float x, float y, float b, float h, float t, Color c)
		{
			Rechteck(x, y, b, t, c);
			Rechteck(x, y + h - t, b, t, c);
			Rechteck(x, y, t, h, c);
			Rechteck(x + b - t, y, t, h, c);
		}

		private void Ecken(float x, float y, float b, float h, float t, Color c)
		{
			float lb = b * 0.28f;
			float lh = h * 0.2f;
			Rechteck(x, y, lb, t, c);
			Rechteck(x + b - lb, y, lb, t, c);
			Rechteck(x, y + h - t, lb, t, c);
			Rechteck(x + b - lb, y + h - t, lb, t, c);
			Rechteck(x, y, t, lh, c);
			Rechteck(x + b - t, y, t, lh, c);
			Rechteck(x, y + h - lh, t, lh, c);
			Rechteck(x + b - t, y + h - lh, t, lh, c);
		}

		private void Text(float mx, float my, string text, Color c)
		{
			float h = _stilGroesse * 1.4f;
			Rect r = new Rect(mx - 150f, my - h * 0.5f, 300f, h);
			GUI.color = new Color(0f, 0f, 0f, 0.8f * c.a);
			Rect s = new Rect(r.x + 1f, r.y + 1f, r.width, r.height);
			GUI.Label(s, text, _stil);
			GUI.color = c;
			GUI.Label(r, text, _stil);
		}
	}
}
