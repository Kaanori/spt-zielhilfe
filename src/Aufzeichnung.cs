// ---------------------------------------------------------------
//  Kampf-Aufzeichnung der Zielhilfe.
//
//  Schreibt jedes Gefecht bildgenau als JSON Lines (gzip) mit, damit eine
//  Simulation Kaans echte Kaempfe mit anderen Einstellungen nachspielen
//  kann. Format: AUFZEICHNUNG-FORMAT.md neben dem Quelltext.
//
//  Leistung: je Bild wird EINE Zeile in einen wiederverwendeten
//  StringBuilder formatiert (Zahlen ohne ToString), in einen
//  wiederverwendeten char-Puffer kopiert und in Byte-Bloecke aus einem
//  Vorrat kodiert. Gzip und Datei laufen in einem Hintergrund-Faden.
//  Ausserhalb eines Gefechts landet die Zeile in einem Ringpuffer
//  (1 s Vorlauf). Gegner, die nicht das Ziel sind, nur mit 10 Hz.
// ---------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace Zielhilfe
{
	internal static class Aufzeichnung
	{
		internal const int FormatVersion = 1;

		internal static BepInEx.Logging.ManualLogSource Protokoll;

		private const float Vorlauf = 1f;

		private const float Nachlauf = 4f;

		private const float GegnerTakt = 0.1f;

		private const int MaxGegner = 8;

		// --- Maus aus dem Abgriff (je Bild) ---------------------------------
		private static float _rohX, _rohY, _fertigX, _fertigY;

		private static int _sx = 1, _sy = 1, _rohBild = -99, _fertigBild = -99;

		public static void MausRoh(float a2, float a3, int sx, int sy)
		{
			_rohX = a2;
			_rohY = a3;
			_sx = sx;
			_sy = sy;
			_rohBild = Time.frameCount;
		}

		public static void MausFertig(float a2, float a3)
		{
			_fertigX = a2;
			_fertigY = a3;
			_fertigBild = Time.frameCount;
		}

		// --- Zustand Raid / Gefecht -----------------------------------------
		private static bool _raid;

		private static string _raidOrdner;

		private static bool _gefecht;

		private static float _letzteAktivitaet = -99f;

		private static bool _aktivDiesesBild;

		private static int _gefechtNr;

		private static bool _voll;

		private static Player _letztesZiel;

		private static readonly List<string> _ereignisse = new List<string>(8);

		private static double _msGlatt;

		private static bool _fehlerGemeldet;

		private static readonly StringBuilder _sb = new StringBuilder(16384);

		private static char[] _zeichen = new char[16384];

		// Ringpuffer fuer den Vorlauf.
		private sealed class RingZeile
		{
			public char[] Text = new char[4096];

			public int Laenge;

			public float Zeit;
		}

		private static readonly RingZeile[] _ring = new RingZeile[512];

		private static int _ringPos;

		// Gegner-Merkzettel (je Player).
		private sealed class GegnerInfo
		{
			public string Id = "", Name = "", Rolle = "";

			public float Zuletzt = -99f;

			public readonly float[] Durch = new float[13];

			public float DurchZeit = -99f;

			public Component Sain;

			public bool SainGeprueft;
		}

		private static readonly Dictionary<Player, GegnerInfo> _gegner = new Dictionary<Player, GegnerInfo>();

		private static readonly Player[] _wahl = new Player[MaxGegner];

		private static readonly float[] _wahlAbstand = new float[MaxGegner];

		// --- Schreiber im Hintergrund ---------------------------------------
		private struct Auftrag
		{
			public int Art; // 0 oeffnen, 1 Daten, 2 schliessen, 3 alte Raids aufraeumen

			public string Pfad;

			public byte[] Daten;

			public int Laenge;
		}

		private const int BlockGroesse = 65536;

		private static readonly BlockingCollection<Auftrag> _auftraege = new BlockingCollection<Auftrag>(new ConcurrentQueue<Auftrag>(), 4096);

		private static readonly ConcurrentBag<byte[]> _vorrat = new ConcurrentBag<byte[]>();

		private static Thread _faden;

		private static long _rohBytes;

		private static long _dateiBytesFertig;

		private static long _dateiBytesOffen;

		private static int _verloren;

		private static byte[] _block;

		private static int _blockPos;

		private static void FadenStarten()
		{
			if (_faden != null)
			{
				return;
			}
			_faden = new Thread(Schreiben) { IsBackground = true, Name = "Zielhilfe-Aufzeichnung" };
			_faden.Start();
		}

		private static void Schreiben()
		{
			FileStream datei = null;
			GZipStream gz = null;
			foreach (Auftrag a in _auftraege.GetConsumingEnumerable())
			{
				try
				{
					switch (a.Art)
					{
					case 0:
						SchliessenIntern(ref datei, ref gz);
						Directory.CreateDirectory(Path.GetDirectoryName(a.Pfad));
						datei = new FileStream(a.Pfad, FileMode.Create, FileAccess.Write, FileShare.Read, 65536);
						gz = new GZipStream(datei, CompressionMode.Compress);
						break;
					case 1:
						if (gz != null)
						{
							gz.Write(a.Daten, 0, a.Laenge);
							Interlocked.Exchange(ref _dateiBytesOffen, datei.Position);
						}
						if (a.Daten.Length == BlockGroesse)
						{
							_vorrat.Add(a.Daten);
						}
						break;
					case 2:
						SchliessenIntern(ref datei, ref gz);
						break;
					case 3:
						AlteRaidsLoeschen(a.Pfad, a.Laenge);
						break;
					}
				}
				catch (Exception ex)
				{
					Protokoll?.LogWarning("[Zielhilfe] Aufzeichnung schreiben: " + ex.Message);
				}
			}
		}

		private static void SchliessenIntern(ref FileStream datei, ref GZipStream gz)
		{
			if (gz != null)
			{
				gz.Dispose();
				gz = null;
			}
			if (datei != null)
			{
				long n = 0;
				try
				{
					n = new FileInfo(datei.Name).Length;
				}
				catch
				{
				}
				datei.Dispose();
				datei = null;
				Interlocked.Add(ref _dateiBytesFertig, n);
				Interlocked.Exchange(ref _dateiBytesOffen, 0);
			}
		}

		private static void AlteRaidsLoeschen(string wurzel, int behalten)
		{
			if (!Directory.Exists(wurzel))
			{
				return;
			}
			string[] ordner = Directory.GetDirectories(wurzel);
			Array.Sort(ordner, StringComparer.Ordinal); // Name beginnt mit Datum-Uhrzeit
			for (int i = 0; i < ordner.Length - behalten; i++)
			{
				try
				{
					Directory.Delete(ordner[i], recursive: true);
				}
				catch
				{
				}
			}
		}

		private static void Einreihen(Auftrag a)
		{
			if (!_auftraege.TryAdd(a))
			{
				_verloren++;
			}
		}

		private static void BlockAbgeben()
		{
			if (_block != null && _blockPos > 0)
			{
				Einreihen(new Auftrag { Art = 1, Daten = _block, Laenge = _blockPos });
				_block = null;
				_blockPos = 0;
			}
		}

		private static void Ausgeben(char[] text, int laenge)
		{
			int max = laenge * 3 + 1;
			if (_block != null && _blockPos + max > _block.Length)
			{
				BlockAbgeben();
			}
			if (_block == null)
			{
				if (max > BlockGroesse)
				{
					_block = new byte[max];
				}
				else if (!_vorrat.TryTake(out _block))
				{
					_block = new byte[BlockGroesse];
				}
				_blockPos = 0;
			}
			_blockPos += Encoding.UTF8.GetBytes(text, 0, laenge, _block, _blockPos);
			_block[_blockPos++] = (byte)'\n';
			_rohBytes += laenge + 1;
		}

		// --- Oeffentliche Ereignisse ----------------------------------------
		private static readonly StringBuilder _eb = new StringBuilder(512);

		private static void PatroneText(StringBuilder s, string feld, AmmoTemplate p, Weapon w)
		{
			s.Append(",\"").Append(feld).Append("\":");
			if (p == null)
			{
				s.Append("null");
				return;
			}
			s.Append("{\"tpl\":");
			Str(s, p._id);
			s.Append(",\"pen\":").Append(p.PenetrationPower);
			s.Append(",\"v0\":");
			Zahl(s, p.InitialSpeed * ((w != null) ? w.SpeedFactor : 1f), 1);
			s.Append('}');
		}

		/// <summary>Fertiges Ereignis (JSON-Objekt) fuer die naechste Bildzeile, z. B. "ergebnis" aus Einschlag.cs.</summary>
		internal static void EreignisDazu(string json)
		{
			if (!Einstellungen.KampfAufzeichnung.Value)
			{
				return;
			}
			_aktivDiesesBild = true;
			_ereignisse.Add(json);
		}

		public static void Kugelstart(Vector3 start, Vector3 richtung, Ammo ammo)
		{
			if (!Einstellungen.KampfAufzeichnung.Value)
			{
				return;
			}
			_eb.Length = 0;
			_eb.Append("{\"typ\":\"kugel\",\"p\":");
			V3(_eb, start, 3);
			_eb.Append(",\"dir\":");
			V3(_eb, richtung.normalized, 6);
			_eb.Append(",\"tpl\":");
			Str(_eb, (ammo?.Template as AmmoTemplate)?._id ?? "");
			_eb.Append('}');
			_ereignisse.Add(_eb.ToString());
		}

		public static void Schuss(Player ich, Weapon w, AmmoTemplate gefeuert, AmmoTemplate benutzt, AmmoTemplate vorhergesagt, int nr)
		{
			if (!Einstellungen.KampfAufzeichnung.Value)
			{
				return;
			}
			_aktivDiesesBild = true;
			_eb.Length = 0;
			_eb.Append("{\"typ\":\"schuss\"");
			if (Rueckstoss.Schusslinie(ich, out var start, out var richtung))
			{
				_eb.Append(",\"fp\":");
				V3(_eb, start, 3);
				_eb.Append(",\"sd\":");
				V3(_eb, richtung, 5);
			}
			SoftAim.SchussLage(ich);
			_eb.Append(",\"zone\":");
			Str(_eb, SoftAim.ZoneName);
			_eb.Append(",\"rand\":");
			Zahl(_eb, SoftAim.Rand, 4);
			_eb.Append(",\"win\":");
			Zahl(_eb, SoftAim.Winkel, 4);
			_eb.Append(",\"imRadius\":");
			_eb.Append(SoftAim.ImRadius ? '1' : '0');
			PatroneText(_eb, "gefeuert", gefeuert, w);
			PatroneText(_eb, "benutzt", benutzt, w);
			PatroneText(_eb, "vorhergesagt", vorhergesagt, w);
			_eb.Append(",\"fallCm\":");
			Zahl(_eb, Zielpunkt.LetzterFall * 100f, 1);
			_eb.Append(",\"entf\":");
			Zahl(_eb, Zielpunkt.LetzteEntfernung, 1);
			Panzerung.StreuungAuffrischen(ich);
			_eb.Append(",\"streuung\":");
			Zahl(_eb, Panzerung.Streuung, 4);
			Einschlaege.SchussFelder(_eb, nr);
			_eb.Append('}');
			_ereignisse.Add(_eb.ToString());
		}

		public static void Treffer(Player ich, BodyPartCollider kollider, Player opfer)
		{
			if (!Einstellungen.KampfAufzeichnung.Value)
			{
				return;
			}
			_aktivDiesesBild = true;
			_eb.Length = 0;
			_eb.Append("{\"typ\":\"treffer\",\"kol\":");
			Str(_eb, kollider.BodyPartColliderType.ToString());
			if (kollider is ArmorPlateCollider ap)
			{
				_eb.Append(",\"platte\":");
				Str(_eb, ap.ArmorPlateColliderType.ToString());
			}
			_eb.Append(",\"opfer\":");
			Str(_eb, opfer?.Profile?.ProfileId ?? "");
			bool tot = opfer != null && opfer.HealthController != null && !opfer.HealthController.IsAlive;
			_eb.Append(",\"kill\":");
			_eb.Append(tot ? '1' : '0');
			_eb.Append('}');
			_ereignisse.Add(_eb.ToString());
		}

		public static void Einschlag(string kollider, string material, bool durch, bool abgeprallt, Vector3 punkt, float entfernung, float vorZiel)
		{
			_aktivDiesesBild = true;
			_eb.Length = 0;
			_eb.Append("{\"typ\":\"einschlag\",\"kol\":");
			Str(_eb, kollider);
			_eb.Append(",\"mat\":");
			Str(_eb, material);
			_eb.Append(",\"durch\":").Append(durch ? '1' : '0');
			_eb.Append(",\"abpr\":").Append(abgeprallt ? '1' : '0');
			_eb.Append(",\"p\":");
			V3(_eb, punkt, 3);
			_eb.Append(",\"entf\":");
			Zahl(_eb, entfernung, 2);
			_eb.Append(",\"vorZiel\":");
			Zahl(_eb, vorZiel, 2);
			_eb.Append('}');
			_ereignisse.Add(_eb.ToString());
		}

		public static void GetroffenWorden(Player ich, BodyPartCollider kollider, Player schuetze)
		{
			if (!Einstellungen.KampfAufzeichnung.Value)
			{
				return;
			}
			_aktivDiesesBild = true;
			_eb.Length = 0;
			_eb.Append("{\"typ\":\"getroffen\",\"kol\":");
			Str(_eb, kollider.BodyPartColliderType.ToString());
			_eb.Append(",\"von\":");
			Str(_eb, schuetze?.Profile?.ProfileId ?? "");
			_eb.Append('}');
			_ereignisse.Add(_eb.ToString());
		}

		// --- Je Bild --------------------------------------------------------
		public static void Bild(Player ich, Camera kamera)
		{
			if (ich == null)
			{
				return;
			}
			long t0 = Stopwatch.GetTimestamp();
			FadenStarten();
			float jetzt = Time.time;
			if (!_raid)
			{
				RaidBeginnen();
			}

			Player ziel = (SoftAim.TeilBild >= Time.frameCount - 1) ? SoftAim.AktuellesZiel : null;
			if ((object)ziel != _letztesZiel)
			{
				_eb.Length = 0;
				_eb.Append("{\"typ\":\"zielwechsel\",\"neu\":");
				Str(_eb, ziel?.Profile?.ProfileId ?? "");
				_eb.Append('}');
				_ereignisse.Add(_eb.ToString());
				_letztesZiel = ziel;
			}
			bool zielImBereich = ziel != null && SoftAim.HatZiel && SoftAim.ImRadius;

			_sb.Length = 0;
			bool siehtMich = ZeileSchreiben(ich, kamera, ziel, jetzt);

			bool aktiv = _aktivDiesesBild || zielImBereich || siehtMich;
			_aktivDiesesBild = false;
			if (aktiv)
			{
				_letzteAktivitaet = jetzt;
			}
			int n = _sb.Length;
			if (_zeichen.Length < n)
			{
				_zeichen = new char[n * 2];
			}
			_sb.CopyTo(0, _zeichen, 0, n);

			long grenze = (long)Einstellungen.AufzMaxMB.Value * 1000000L;
			if (!_voll && Interlocked.Read(ref _dateiBytesFertig) + Interlocked.Read(ref _dateiBytesOffen) >= grenze)
			{
				_voll = true;
				GefechtBeenden();
				Protokoll?.LogWarning($"[Zielhilfe] Aufzeichnung: Grenze {Einstellungen.AufzMaxMB.Value} MB fuer diesen Raid erreicht, keine weiteren Gefechte.");
			}

			if (!_gefecht && aktiv && !_voll)
			{
				GefechtBeginnen(ich, jetzt);
			}
			if (_gefecht)
			{
				Ausgeben(_zeichen, n);
				if (jetzt - _letzteAktivitaet > Nachlauf)
				{
					GefechtBeenden();
				}
			}
			else
			{
				RingZeile r = _ring[_ringPos] ?? (_ring[_ringPos] = new RingZeile());
				if (r.Text.Length < n)
				{
					r.Text = new char[n * 2];
				}
				Array.Copy(_zeichen, r.Text, n);
				r.Laenge = n;
				r.Zeit = jetzt;
				_ringPos = (_ringPos + 1) % _ring.Length;
			}
			_ereignisse.Clear();

			double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
			_msGlatt += (ms - _msGlatt) * 0.05;
		}

		private static void RaidBeginnen()
		{
			_raid = true;
			_voll = false;
			_gefechtNr = 0;
			_gegner.Clear();
			Interlocked.Exchange(ref _dateiBytesFertig, 0);
			Interlocked.Exchange(ref _dateiBytesOffen, 0);
			string karte = Sauber(Spielzugriff.Welt?.LocationId ?? "unbekannt");
			string wurzel = Path.Combine(BepInEx.Paths.BepInExRootPath, "Zielhilfe-Aufzeichnung");
			_raidOrdner = Path.Combine(wurzel, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + karte);
			// Aelteste Ordner entfernen, so dass mit diesem hoechstens N bleiben.
			Einreihen(new Auftrag { Art = 3, Pfad = wurzel, Laenge = Math.Max(0, Einstellungen.AufzMaxRaids.Value - 1) });
			for (int i = 0; i < _ring.Length; i++)
			{
				if (_ring[i] != null)
				{
					_ring[i].Laenge = 0;
				}
			}
		}

		public static void RaidVorbei()
		{
			if (!_raid)
			{
				return;
			}
			GefechtBeenden();
			_raid = false;
			_letztesZiel = null;
		}

		private static void GefechtBeginnen(Player ich, float jetzt)
		{
			_gefechtNr++;
			string pfad = Path.Combine(_raidOrdner, $"gefecht-{_gefechtNr:000}-{DateTime.Now:HHmmss}.jsonl.gz");
			Einreihen(new Auftrag { Art = 0, Pfad = pfad });
			_gefecht = true;
			// Kopfzeile.
			StringBuilder k = new StringBuilder(8192);
			KopfSchreiben(k, ich, jetzt);
			char[] kz = new char[k.Length];
			k.CopyTo(0, kz, 0, k.Length);
			Ausgeben(kz, kz.Length);
			// Vorlauf aus dem Ring, in zeitlicher Reihenfolge.
			for (int i = 0; i < _ring.Length; i++)
			{
				RingZeile r = _ring[(_ringPos + i) % _ring.Length];
				if (r != null && r.Laenge > 0 && jetzt - r.Zeit <= Vorlauf)
				{
					Ausgeben(r.Text, r.Laenge);
					r.Laenge = 0;
				}
			}
		}

		private static void GefechtBeenden()
		{
			if (!_gefecht)
			{
				return;
			}
			BlockAbgeben();
			Einreihen(new Auftrag { Art = 2 });
			_gefecht = false;
		}

		private static void KopfSchreiben(StringBuilder k, Player ich, float jetzt)
		{
			k.Append("{\"kopf\":{\"format\":").Append(FormatVersion);
			k.Append(",\"plugin\":\"1.0.0\",\"karte\":");
			Str(k, Spielzugriff.Welt?.LocationId ?? "");
			k.Append(",\"datum\":");
			Str(k, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
			k.Append(",\"zeit\":");
			Zahl(k, jetzt, 4);
			k.Append(",\"gefecht\":").Append(_gefechtNr);
			k.Append(",\"ich\":");
			Str(k, ich.Profile?.ProfileId ?? "");
			k.Append(",\"einstellungen\":{");
			bool erst = true;
			ConfigFile c = Einstellungen.Datei;
			if (c != null)
			{
				foreach (ConfigDefinition def in c.Keys)
				{
					ConfigEntryBase e = c[def];
					if (!erst)
					{
						k.Append(',');
					}
					erst = false;
					Str(k, def.Section + "/" + def.Key);
					k.Append(':');
					Str(k, e.GetSerializedValue());
				}
			}
			k.Append("},\"ausruestung\":[");
			erst = true;
			try
			{
				foreach (Slot sl in ich.Inventory.Equipment.Slots)
				{
					Item it = sl.ContainedItem;
					if (it == null)
					{
						continue;
					}
					if (!erst)
					{
						k.Append(',');
					}
					erst = false;
					k.Append("{\"slot\":");
					Str(k, sl.ID);
					k.Append(",\"tpl\":");
					Str(k, it.TemplateId.ToString());
					k.Append(",\"name\":");
					Str(k, it.Template?._name ?? "");
					k.Append('}');
				}
			}
			catch
			{
			}
			k.Append("]}}");
		}

		/// <summary>Formatiert die Bildzeile in _sb. Liefert, ob ein Feind mich sieht (Gefecht-Ausloeser).</summary>
		private static bool ZeileSchreiben(Player ich, Camera kamera, Player ziel, float jetzt)
		{
			StringBuilder s = _sb;
			int bild = Time.frameCount;
			s.Append("{\"t\":");
			Zahl(s, jetzt, 4);
			s.Append(",\"dt\":");
			Zahl(s, Time.deltaTime, 5);
			s.Append(",\"f\":").Append(bild);

			// --- ich ---
			s.Append(",\"ich\":{\"p\":");
			V3(s, ich.Position, 3);
			s.Append(",\"v\":");
			V3(s, ich.Velocity, 2);
			s.Append(",\"r\":");
			V2(s, ich.Rotation, 3);
			if (Rueckstoss.Schusslinie(ich, out var start, out var richtung))
			{
				s.Append(",\"fp\":");
				V3(s, start, 3);
				s.Append(",\"sd\":");
				V3(s, richtung, 5);
			}
			if (kamera != null)
			{
				s.Append(",\"kam\":");
				V3(s, kamera.transform.position, 3);
				s.Append(",\"kamVor\":");
				V3(s, kamera.transform.forward, 4);
				s.Append(",\"fov\":");
				Zahl(s, kamera.fieldOfView, 2);
			}
			MovementContext mc = ich.MovementContext;
			s.Append(",\"ads\":").Append(SoftAim.ImAnschlag(ich) ? '1' : '0');
			if (mc != null)
			{
				s.Append(",\"pose\":");
				Zahl(s, mc.PoseLevel, 2);
				s.Append(",\"liegt\":").Append(mc.IsInPronePose ? '1' : '0');
				s.Append(",\"sprint\":").Append(mc.IsSprintEnabled ? '1' : '0');
				s.Append(",\"lehnen\":");
				Zahl(s, mc.Tilt, 2);
			}
			MovementState zustand = ich.CurrentManagedState;
			float k = ich.GetRotationMultiplier() * ((zustand != null) ? zustand.StateSensitivity : 1f);
			s.Append(",\"k\":");
			Zahl(s, k, 5);
			Einstellungen.Werte w = Einstellungen.Aktiv();
			s.Append(",\"w\":{\"kl\":");
			Str(s, w.Klasse.ToString());
			s.Append(",\"eigen\":").Append(w.Eigen ? '1' : '0');
			bool bereit = SoftAim.WaffeBereit(ich, out var grund);
			s.Append(",\"bereit\":").Append(bereit ? '1' : '0');
			if (!bereit)
			{
				s.Append(",\"grund\":");
				Str(s, grund);
			}
			if (ich.HandsController?.Item is Weapon waffe)
			{
				s.Append(",\"tpl\":");
				Str(s, TemplateText(waffe));
				AmmoTemplate pat = Spielzugriff.Patrone(ich, out _);
				if (pat != null)
				{
					s.Append(",\"pen\":").Append(pat.PenetrationPower);
					s.Append(",\"v0\":");
					Zahl(s, pat.InitialSpeed * waffe.SpeedFactor, 1);
					s.Append(",\"patrone\":");
					Str(s, pat._id);
				}
				try
				{
					SightComponent glas = ich.ProceduralWeaponAnimation?.CurrentAimingMod;
					if (glas != null)
					{
						s.Append(",\"visier\":");
						Str(s, TemplateText(glas.Item));
						s.Append(",\"zoom\":");
						Zahl(s, glas.GetCurrentOpticZoom(), 2);
						s.Append(",\"nullung\":").Append(glas.GetCurrentOpticCalibrationDistance());
					}
				}
				catch
				{
				}
			}
			s.Append("}}");

			// --- Maus ---
			s.Append(",\"maus\":{\"eingabe\":").Append(_rohBild == bild ? '1' : '0').Append(',');
			if (_rohBild == bild)
			{
				s.Append("\"roh\":");
				V2(s, new Vector2(_rohX, _rohY), 5);
				s.Append(",\"inv\":[").Append(_sx).Append(',').Append(_sy).Append(']');
				s.Append(",\"fertig\":");
				if (_fertigBild == bild)
				{
					V2(s, new Vector2(_fertigX, _fertigY), 5);
				}
				else
				{
					V2(s, new Vector2(_rohX, _rohY), 5);
				}
				s.Append(',');
			}
			bool teile = SoftAim.TeilBild == bild;
			s.Append("\"teile\":").Append(teile ? '1' : '0');
			if (teile)
			{
				s.Append(",\"anz\":");
				V2(s, SoftAim.TeilAnz, 4);
				s.Append(",\"reib\":");
				V2(s, SoftAim.TeilReib, 4);
				s.Append(",\"mag\":");
				V2(s, SoftAim.TeilMag, 4);
				s.Append(",\"schub\":");
				V2(s, SoftAim.TeilSchub, 4);
				s.Append(",\"mit\":");
				V2(s, SoftAim.TeilMit, 4);
			}
			s.Append(",\"rcs\":");
			V2(s, SoftAim.RcsGrad, 4);
			s.Append(",\"weg\":");
			Zahl(s, SoftAim.WegSumme, 3);
			s.Append(",\"gel\":").Append(SoftAim.Geloest ? '1' : '0');
			s.Append(",\"schubAktiv\":").Append(SoftAim.SchubAktiv ? '1' : '0');
			s.Append(",\"schubRest\":");
			Zahl(s, SoftAim.SchubRest, 3);
			s.Append('}');

			// --- Ziel der Hilfe ---
			if (ziel != null && SoftAim.HatZiel)
			{
				s.Append(",\"hilfe\":{\"ziel\":");
				Str(s, Info(ziel).Id);
				s.Append(",\"zone\":");
				Str(s, SoftAim.ZoneName);
				s.Append(",\"rand\":");
				Zahl(s, SoftAim.Rand, 4);
				s.Append(",\"win\":");
				Zahl(s, SoftAim.Winkel, 4);
				s.Append(",\"radius\":");
				Zahl(s, SoftAim.RadiusDiag, 3);
				s.Append(",\"imRadius\":").Append(SoftAim.ImRadius ? '1' : '0');
				s.Append(",\"zielRate\":");
				V2(s, SoftAim.ZielRate, 2);
				s.Append(",\"wahl\":");
				Str(s, SoftAim.ZonenGrund);
				s.Append(",\"sicht\":");
				Str(s, SoftAim.SichtGrund);
				s.Append(",\"sichtZonen\":[");
				for (int i = 0; i < 4; i++)
				{
					if (i > 0)
					{
						s.Append(',');
					}
					s.Append(SoftAim.SichtZonen[i]);
				}
				s.Append(']');
				s.Append(",\"fallCm\":");
				Zahl(s, Zielpunkt.LetzterFall * 100f, 1);
				s.Append(",\"entf\":");
				Zahl(s, Zielpunkt.LetzteEntfernung, 1);
				// Seit 03.11. unabhaengig von "Zone nach Panzerung" (vorher immer 0).
				Panzerung.StreuungAuffrischen(ich);
				s.Append(",\"streuung\":");
				Zahl(s, Panzerung.Streuung, 4);
				s.Append(",\"unruhe\":");
				Zahl(s, SoftAim.Unruhe, 4);
				s.Append(",\"p\":[");
				for (int i = 0; i < 5; i++)
				{
					if (i > 0)
					{
						s.Append(',');
					}
					Zahl(s, Panzerung.LetzteP[i], 3);
				}
				s.Append("]}");
			}

			// --- Gegner ---
			bool siehtMich = GegnerSchreiben(s, ich, kamera, ziel, jetzt);

			// --- Ereignisse ---
			if (_ereignisse.Count > 0)
			{
				s.Append(",\"ev\":[");
				for (int i = 0; i < _ereignisse.Count; i++)
				{
					if (i > 0)
					{
						s.Append(',');
					}
					s.Append(_ereignisse[i]);
				}
				s.Append(']');
			}
			s.Append('}');
			return siehtMich;
		}

		private static bool GegnerSchreiben(StringBuilder s, Player ich, Camera kamera, Player ziel, float jetzt)
		{
			List<Player> liste = Spielzugriff.Welt?.AllAlivePlayersList;
			bool siehtMich = false;
			int anzahl = 0;
			for (int i = 0; i < MaxGegner; i++)
			{
				_wahl[i] = null;
				_wahlAbstand[i] = float.MaxValue;
			}
			if (liste != null)
			{
				float reichweite = Einstellungen.Reichweite.Value;
				float sicht = Einstellungen.AufzSichtMeter.Value;
				Vector3 kp = (kamera != null) ? kamera.transform.position : ich.Position;
				Vector3 kv = (kamera != null) ? kamera.transform.forward : Vector3.forward;
				float halb = (kamera != null) ? (0.5f * kamera.fieldOfView * Mathf.Max(1f, kamera.aspect)) : 45f;
				for (int i = 0; i < liste.Count; i++)
				{
					Player p = liste[i];
					if (p == null || (object)p == ich || p.HealthController == null || !p.HealthController.IsAlive)
					{
						continue;
					}
					float abstand = Vector3.Distance(ich.Position, p.Position);
					if (abstand > reichweite || !Spielzugriff.IstFeind(p, ich))
					{
						continue;
					}
					bool hatMich = HatMichImBlick(p, ich);
					if (hatMich && abstand <= sicht)
					{
						siehtMich = true;
					}
					bool istZiel = (object)p == ziel;
					bool imBlick = Vector3.Angle(kv, p.Position + Vector3.up * 1.2f - kp) <= halb;
					if (!istZiel && !imBlick && !hatMich)
					{
						continue;
					}
					// Ziel immer vorn, sonst die naechsten.
					float rang = istZiel ? -1f : abstand;
					for (int j = 0; j < MaxGegner; j++)
					{
						if (rang < _wahlAbstand[j])
						{
							for (int m = MaxGegner - 1; m > j; m--)
							{
								_wahl[m] = _wahl[m - 1];
								_wahlAbstand[m] = _wahlAbstand[m - 1];
							}
							_wahl[j] = p;
							_wahlAbstand[j] = rang;
							break;
						}
					}
				}
			}
			s.Append(",\"geg\":[");
			for (int j = 0; j < MaxGegner; j++)
			{
				Player p = _wahl[j];
				if (p == null)
				{
					continue;
				}
				GegnerInfo info = Info(p);
				bool istZiel = (object)p == ziel;
				if (!istZiel && jetzt - info.Zuletzt < GegnerTakt)
				{
					continue;
				}
				info.Zuletzt = jetzt;
				if (anzahl > 0)
				{
					s.Append(',');
				}
				anzahl++;
				GegnerEintrag(s, p, info, ich, istZiel, jetzt);
			}
			s.Append(']');
			return siehtMich;
		}

		private static bool HatMichImBlick(Player p, Player ich)
		{
			try
			{
				EnemyInfo ge = p.AIData?.BotOwner?.Memory?.GoalEnemy;
				return ge != null && (object)ge.Person == ich && ge.IsVisible;
			}
			catch
			{
				return false;
			}
		}

		private static GegnerInfo Info(Player p)
		{
			if (!_gegner.TryGetValue(p, out var info))
			{
				info = new GegnerInfo();
				try
				{
					info.Id = p.Profile?.ProfileId ?? "";
					info.Name = p.Profile?.Nickname ?? "";
					info.Rolle = p.Profile?.Info?.Settings != null ? p.Profile.Info.Settings.Role.ToString() : "";
				}
				catch
				{
				}
				_gegner[p] = info;
			}
			return info;
		}

		private static readonly Dictionary<Item, string> _tplText = new Dictionary<Item, string>();

		private static string TemplateText(Item it)
		{
			if (!_tplText.TryGetValue(it, out var t))
			{
				t = it.TemplateId.ToString();
				_tplText[it] = t;
			}
			return t;
		}

		private static void GegnerEintrag(StringBuilder s, Player p, GegnerInfo info, Player ich, bool istZiel, float jetzt)
		{
			s.Append("{\"id\":");
			Str(s, info.Id);
			s.Append(",\"n\":");
			Str(s, info.Name);
			s.Append(",\"rolle\":");
			Str(s, info.Rolle);
			s.Append(",\"ziel\":").Append(istZiel ? '1' : '0');
			s.Append(",\"p\":");
			V3(s, p.Position, 3);
			s.Append(",\"v\":");
			V3(s, p.Velocity, 2);
			s.Append(",\"r\":");
			V2(s, p.Rotation, 2);
			s.Append(",\"blick\":");
			V3(s, p.LookDirection, 3);
			MovementContext mc = p.MovementContext;
			if (mc != null)
			{
				s.Append(",\"pose\":");
				Zahl(s, mc.PoseLevel, 2);
				s.Append(",\"liegt\":").Append(mc.IsInPronePose ? '1' : '0');
				s.Append(",\"sprint\":").Append(mc.IsSprintEnabled ? '1' : '0');
			}
			// Trefferzonen (Huellen) und Kopfboxen einzeln (Form).
			s.Append(",\"zonen\":{");
			ZoneHuelle(s, p, "Kopf", Trefferzonen.Kopf, true);
			ZoneHuelle(s, p, "Rumpf", Trefferzonen.Rumpf, false);
			ZoneHuelle(s, p, "Becken", Trefferzonen.Becken, false);
			ZoneHuelle(s, p, "Beine", Trefferzonen.Beine, false);
			ZoneHuelle(s, p, "Arme", Trefferzonen.Arme, false);
			s.Append('}');
			s.Append(",\"kopfboxen\":{");
			bool erst = true;
			Dictionary<EBodyPartColliderType, BodyPartCollider> karte = p.PlayerBones?.BodyPartCollidersDictionary;
			EBodyPartColliderType[] boxen = Zielpunkt.AlleKopfboxen;
			if (karte != null)
			{
				for (int i = 0; i < boxen.Length; i++)
				{
					if (!karte.TryGetValue(boxen[i], out var box) || box == null
						|| !Trefferzonen.Form(box.Collider, out var m, out var a1, out var a2, out var a3))
					{
						continue;
					}
					if (!erst)
					{
						s.Append(',');
					}
					erst = false;
					s.Append('"').Append(KopfboxName(i)).Append("\":[");
					Zahl(s, m.x, 3).Append(',');
					Zahl(s, m.y, 3).Append(',');
					Zahl(s, m.z, 3).Append(',');
					Zahl(s, a1.x, 3).Append(',');
					Zahl(s, a1.y, 3).Append(',');
					Zahl(s, a1.z, 3).Append(',');
					Zahl(s, a2.x, 3).Append(',');
					Zahl(s, a2.y, 3).Append(',');
					Zahl(s, a2.z, 3).Append(',');
					Zahl(s, a3.x, 3).Append(',');
					Zahl(s, a3.y, 3).Append(',');
					Zahl(s, a3.z, 3).Append(']');
				}
			}
			s.Append('}');
			// Panzerung gegen die eigene Patrone (je Gegner hoechstens 1x je Sekunde gerechnet).
			if (jetzt - info.DurchZeit >= 1f)
			{
				info.DurchZeit = jetzt;
				try
				{
					float pen = Spielzugriff.Patrone(ich, out _)?.PenetrationPower ?? 30;
					Panzerung.Auswerten(p, pen, info.Durch);
				}
				catch
				{
					for (int i = 0; i < info.Durch.Length; i++)
					{
						info.Durch[i] = -1f;
					}
				}
			}
			s.Append(",\"durch\":[");
			for (int i = 0; i < info.Durch.Length; i++)
			{
				if (i > 0)
				{
					s.Append(',');
				}
				Zahl(s, info.Durch[i], 3);
			}
			s.Append(']');
			KiEintrag(s, p, info, ich);
			s.Append('}');
		}

		private static readonly string[] _kopfboxNamen = new string[8];

		private static string KopfboxName(int i)
		{
			return _kopfboxNamen[i] ?? (_kopfboxNamen[i] = Zielpunkt.AlleKopfboxen[i].ToString());
		}

		private static void ZoneHuelle(StringBuilder s, Player p, string name, EBodyPartColliderType[] typen, bool erst)
		{
			if (!Trefferzonen.Huelle(p, typen, out var b))
			{
				return;
			}
			if (s[s.Length - 1] != '{')
			{
				s.Append(',');
			}
			s.Append('"').Append(name).Append("\":[");
			Zahl(s, b.center.x, 3).Append(',');
			Zahl(s, b.center.y, 3).Append(',');
			Zahl(s, b.center.z, 3).Append(',');
			Zahl(s, b.extents.x, 3).Append(',');
			Zahl(s, b.extents.y, 3).Append(',');
			Zahl(s, b.extents.z, 3).Append(']');
		}

		// --- KI: wohin will er? ---------------------------------------------
		private static void KiEintrag(StringBuilder s, Player p, GegnerInfo info, Player ich)
		{
			BotOwner bo = null;
			try
			{
				bo = p.AIData?.BotOwner;
			}
			catch
			{
			}
			if (bo == null)
			{
				return;
			}
			s.Append(",\"ki\":{");
			bool erst = true;
			try
			{
				StandartBotBrain brain = bo.Brain;
				if (brain != null)
				{
					Feld(s, ref erst, "schicht");
					Str(s, brain.ActiveLayerName() ?? "");
					Feld(s, ref erst, "knoten");
					Str(s, brain.GetLastNode() ?? "");
					BotLogicDecision? ent = brain.LastDecision;
					if (ent.HasValue)
					{
						Feld(s, ref erst, "entscheidung");
						Str(s, ent.Value.ToString());
					}
				}
			}
			catch
			{
			}
			try
			{
				BotMover mover = bo.Mover;
				if (mover != null)
				{
					Vector3? endziel = mover.TargetPoint;
					if (endziel.HasValue)
					{
						Feld(s, ref erst, "endziel");
						V3(s, endziel.Value, 2);
					}
					if (mover.HasPathAndNoComplete)
					{
						Feld(s, ref erst, "ecke");
						V3(s, mover.CurrentCornerPoint, 2);
						Feld(s, ref erst, "rest");
						Zahl(s, mover.DistDestination, 1);
					}
					Feld(s, ref erst, "bewegt");
					s.Append(mover.IsMoving ? '1' : '0');
				}
			}
			catch
			{
			}
			try
			{
				BotMemory mem = bo.Memory;
				if (mem != null)
				{
					EnemyInfo ge = mem.GoalEnemy;
					Feld(s, ref erst, "hatZiel");
					s.Append(ge != null ? '1' : '0');
					if (ge != null)
					{
						bool ichBins = (object)ge.Person == ich;
						Feld(s, ref erst, "zielIchBin");
						s.Append(ichBins ? '1' : '0');
						Feld(s, ref erst, "siehtZiel");
						s.Append(ge.IsVisible ? '1' : '0');
						Feld(s, ref erst, "kannSchiessen");
						s.Append(ge.CanShoot ? '1' : '0');
					}
					Feld(s, ref erst, "inDeckung");
					s.Append(mem.IsInCover ? '1' : '0');
					CustomNavigationPoint dp = mem.CurCustomCoverPoint;
					if (dp != null)
					{
						Feld(s, ref erst, "deckung");
						V3(s, dp.BasePosition, 2);
					}
				}
			}
			catch
			{
			}
			SainEintrag(s, ref erst, bo, info);
			s.Append('}');
		}

		// --- SAIN per Reflexion (keine Bau-Abhaengigkeit) ---------------------
		// Beleg SAIN.dll: SAIN.Components.BotComponent haengt als Komponente am
		// BotOwner-Objekt (SAIN selbst: GetComponent<BotComponent>()); Eigenschaften
		// Decision (SAINDecisionClass: CurrentCombatDecision, CurrentSquadDecision,
		// CurrentSelfDecision), EnemyController (SAINEnemyController: GoalEnemy ->
		// Enemy: EnemyProfileId, IsVisible, InLineOfSight), SAINLayersActive.
		private static bool _sainGeprueft;

		private static Type _sainTyp;

		private static PropertyInfo _pDecision, _pEnemyCtrl, _pLayers, _pKampf, _pTrupp, _pSelbst, _pGoal, _pFeindId, _pFeindSieht, _pFeindLos;

		private static void SainVorbereiten()
		{
			_sainGeprueft = true;
			try
			{
				_sainTyp = AccessTools.TypeByName("SAIN.Components.BotComponent");
				if (_sainTyp == null)
				{
					return;
				}
				_pDecision = _sainTyp.GetProperty("Decision");
				_pEnemyCtrl = _sainTyp.GetProperty("EnemyController");
				_pLayers = _sainTyp.GetProperty("SAINLayersActive");
				Type dt = _pDecision?.PropertyType;
				_pKampf = dt?.GetProperty("CurrentCombatDecision");
				_pTrupp = dt?.GetProperty("CurrentSquadDecision");
				_pSelbst = dt?.GetProperty("CurrentSelfDecision");
				Type et = _pEnemyCtrl?.PropertyType;
				_pGoal = et?.GetProperty("GoalEnemy");
				Type ft = _pGoal?.PropertyType;
				_pFeindId = ft?.GetProperty("EnemyProfileId");
				_pFeindSieht = ft?.GetProperty("IsVisible");
				_pFeindLos = ft?.GetProperty("InLineOfSight");
			}
			catch (Exception ex)
			{
				_sainTyp = null;
				Protokoll?.LogInfo("[Zielhilfe] Aufzeichnung: SAIN nicht lesbar (" + ex.Message + ")");
			}
		}

		private static void SainEintrag(StringBuilder s, ref bool erst, BotOwner bo, GegnerInfo info)
		{
			if (!_sainGeprueft)
			{
				SainVorbereiten();
			}
			if (_sainTyp == null)
			{
				return;
			}
			try
			{
				if (!info.SainGeprueft)
				{
					info.SainGeprueft = true;
					info.Sain = bo.GetComponent(_sainTyp);
				}
				Component c = info.Sain;
				if (c == null)
				{
					return;
				}
				Feld(s, ref erst, "sain");
				s.Append('{');
				bool e2 = true;
				object lay = _pLayers?.GetValue(c, null);
				if (lay is bool la)
				{
					Feld(s, ref e2, "aktiv");
					s.Append(la ? '1' : '0');
				}
				object dec = _pDecision?.GetValue(c, null);
				if (dec != null)
				{
					Feld(s, ref e2, "kampf");
					Str(s, _pKampf?.GetValue(dec, null)?.ToString() ?? "");
					Feld(s, ref e2, "trupp");
					Str(s, _pTrupp?.GetValue(dec, null)?.ToString() ?? "");
					Feld(s, ref e2, "selbst");
					Str(s, _pSelbst?.GetValue(dec, null)?.ToString() ?? "");
				}
				object ctrl = _pEnemyCtrl?.GetValue(c, null);
				object goal = (ctrl != null) ? _pGoal?.GetValue(ctrl, null) : null;
				if (goal != null)
				{
					Feld(s, ref e2, "feind");
					Str(s, _pFeindId?.GetValue(goal, null) as string ?? "");
					if (_pFeindSieht?.GetValue(goal, null) is bool sieht)
					{
						Feld(s, ref e2, "sieht");
						s.Append(sieht ? '1' : '0');
					}
					if (_pFeindLos?.GetValue(goal, null) is bool los)
					{
						Feld(s, ref e2, "sichtlinie");
						s.Append(los ? '1' : '0');
					}
				}
				s.Append('}');
			}
			catch
			{
				// SAIN-Zustand in diesem Bild nicht lesbar - Eintrag faellt weg.
			}
		}

		// --- Diagnose -------------------------------------------------------
		public static string Diagnose()
		{
			if (!Einstellungen.KampfAufzeichnung.Value)
			{
				return " | Aufzeichnung aus";
			}
			long bytes = Interlocked.Read(ref _dateiBytesFertig) + Interlocked.Read(ref _dateiBytesOffen);
			return $" | Aufzeichnung {(_gefecht ? $"GEFECHT {_gefechtNr}" : "bereit")}{(_voll ? " (voll)" : "")}, {_msGlatt:0.000} ms/Bild, "
				+ $"{bytes / 1000000.0:0.0} MB gepackt / {_rohBytes / 1000000.0:0.0} MB roh" + ((_verloren > 0) ? $", {_verloren} Bloecke verloren" : "");
		}

		public static void FehlerMelden(Exception ex)
		{
			if (!_fehlerGemeldet)
			{
				_fehlerGemeldet = true;
				Protokoll?.LogWarning("[Zielhilfe] Aufzeichnung: " + ex);
			}
		}

		// --- JSON-Werkzeuge ohne Speicheranforderung ------------------------
		private static void Feld(StringBuilder s, ref bool erst, string name)
		{
			if (!erst)
			{
				s.Append(',');
			}
			erst = false;
			s.Append('"').Append(name).Append("\":");
		}

		internal static void Str(StringBuilder s, string t)
		{
			s.Append('"');
			if (t != null)
			{
				for (int i = 0; i < t.Length; i++)
				{
					char c = t[i];
					if (c == '"' || c == '\\')
					{
						s.Append('\\').Append(c);
					}
					else if (c < ' ')
					{
						s.Append(' ');
					}
					else
					{
						s.Append(c);
					}
				}
			}
			s.Append('"');
		}

		internal static void V3(StringBuilder s, Vector3 v, int stellen)
		{
			s.Append('[');
			Zahl(s, v.x, stellen).Append(',');
			Zahl(s, v.y, stellen).Append(',');
			Zahl(s, v.z, stellen).Append(']');
		}

		internal static void V2(StringBuilder s, Vector2 v, int stellen)
		{
			s.Append('[');
			Zahl(s, v.x, stellen).Append(',');
			Zahl(s, v.y, stellen).Append(']');
		}

		private static readonly long[] _zehner = { 1, 10, 100, 1000, 10000, 100000, 1000000 };

		/// <summary>Festkomma-Zahl mit Punkt, ohne ToString (keine Speicheranforderung).</summary>
		internal static StringBuilder Zahl(StringBuilder s, float v, int stellen)
		{
			if (float.IsNaN(v) || float.IsInfinity(v))
			{
				return s.Append('0');
			}
			stellen = Mathf.Clamp(stellen, 0, 6);
			long f = _zehner[stellen];
			double d = v;
			long ganz = (long)Math.Round(Math.Abs(d) * f, MidpointRounding.AwayFromZero);
			if (ganz == 0)
			{
				return s.Append('0');
			}
			if (d < 0)
			{
				s.Append('-');
			}
			long vor = ganz / f;
			long nach = ganz % f;
			s.Append(vor);
			if (stellen > 0 && nach != 0)
			{
				s.Append('.');
				// fuehrende Nullen der Nachkommastellen, nachfolgende Nullen weglassen
				long teiler = f / 10;
				while (nach != 0 && teiler > 0)
				{
					long ziffer = nach / teiler;
					s.Append((char)('0' + ziffer));
					nach -= ziffer * teiler;
					teiler /= 10;
				}
			}
			return s;
		}

		private static string Sauber(string t)
		{
			StringBuilder b = new StringBuilder(t.Length);
			foreach (char c in t)
			{
				b.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
			}
			return b.ToString();
		}
	}
}
