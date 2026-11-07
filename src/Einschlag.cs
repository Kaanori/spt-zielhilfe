// ---------------------------------------------------------------
//  Einschlag je eigenem Schuss (Diagnose, keine Wirkung auf die Hilfe).
//
//  Frage aus dem Raid Customs 03.11.: 150-300 m, 27 Schuesse, 8 Treffer,
//  obwohl die Schusslinie bei jedem Schuss <= 8 cm am Zielpunkt lag. Zu tief
//  (Fallausgleich)? Seitlich (Ausweichen, Vorhalt in die falsche Richtung)?
//  Kopf bewegt (Haltung)? Oder ein Hindernis (Bewuchs) auf der Linie?
//
//  Belege im Spielcode (Assembly-CSharp 4.1.6):
//  - Player.FirearmController.Shot -> AdjustShotVectors -> InitiateShot ->
//    BallisticsCalculator.Shoot (CreateShot mit _fireIndex++) -> Shot.
//    Shot.MasterOrigin = shotPosition aus InitiateShot (Shot.method_1), bei
//    Splittern die des Elternteils; FireIndex wird an Splitter vererbt.
//  - BallisticsCalculator.UpdateShots -> Shot.Tick -> Update -> CheckCollision
//    -> HandleCollision: setzt HitPoint/HitCollider/HittedBallisticCollider,
//    zieht von TimeSinceShot den Rest des Schritts ab (= Flugzeit genau bis
//    zum Treffpunkt) und entscheidet in CreateFragments ueber den Ausgang
//    (BulletState StopHit/RicochetHit/DeviationHit/FragmentationHit).
//    Gilt fuer JEDE Oberflaeche, auch Koerper (BodyPartCollider) - der
//    Schaden kommt erst danach ueber _shotDelegate -> ApplyHit.
//  - DeviationHit (Durchschuss): der Splitter fliegt mit
//    GetDeviatedTrajectory weiter, d. h. Richtung + Zufall *
//    HittedBallisticCollider.TrajectoryDeviation - Bewuchs/Zaun KANN die
//    Kugel also ablenken.
//  - Ende: RemovePredicate (IsShotAndParentFinished) -> _shotsToRelease ->
//    Shot.Release -> Shot.ReturnShotToPool -> PrepareToPool (loescht
//    PlayerProfileID, PositionHistory, Fragments). Ein Prefix auf
//    ReturnShotToPool sieht die Hauptkugel also ein letztes Mal vollstaendig -
//    auch die, die nie etwas getroffen hat (IsFlyingOutOfTime: TimeSinceShot >
//    AmmoLifeTime, M995: 5 s).
//  - AppEnvironment.Config.UseSpiritPlayer setzt BaseLocalGame offline auf
//    false: Spirit-Kollider spielen im Offline-Raid keine Rolle.
//
//  Je Schuss ein Akten-Platz (16 feste Plaetze, keine Speicheranforderung je
//  Bild). Je Bild laeuft nur Tick() fuer offene Akten: ein Kollider-Lesen des
//  Ziels bis die Kugel die Zieltiefe erreicht hat (~0,2-0,4 s je Schuss).
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using EFT;
using EFT.Ballistics;
using UnityEngine;

namespace Zielhilfe
{
	internal static class Einschlaege
	{
		private const int Plaetze = 16;

		private const int MaxFolge = 4;

		/// <summary>Abschluss ohne Freigabe nach so vielen s (AmmoLifeTime M995 = 5 s).</summary>
		private const float Hoechstdauer = 8f;

		private sealed class Akte
		{
			public bool Offen;
			public int Nr;
			public float TSchuss;
			public int BildSchuss;
			public bool HatLinie;
			public Vector3 Fp, Sd;
			public bool HatKugel;
			public Vector3 KugelP, KugelDir;
			public bool Gebunden;
			public int FireIndex;

			public Player Ziel;
			public string ZielId = "", ZielName = "", ZielQuelle = "";
			public bool HatKopfS, HatRumpfS;
			public Vector3 KopfS, RumpfS, PosS, TempoS;
			public float PoseS;
			public bool LiegtS;
			public float EntfS;
			public float FlugzeitZiel;

			public bool HatVorhalt;
			public Vector3 Vorhalt, TempoGl, TempoRoh, VorhaltBasis;
			public float VorhaltFlugzeit, FallM;

			// Kopf in dem Bild, in dem die Kugel die Zieltiefe erreicht (geschaetzt).
			public bool HatKopfD;
			public Vector3 KopfD, RumpfD;
			public float TKopfD, BesterAbstandD;

			// Ziel im Moment des ERSTEN Einschlags.
			public bool HatKopfE, HatRumpfE;
			public Vector3 KopfE, KopfEPhys, RumpfE, PosE, TempoE;
			public float PoseE;
			public bool LiegtE;

			public int Anzahl;
			public readonly Vector3[] Punkt = new Vector3[MaxFolge];
			public readonly string[] Kol = new string[MaxFolge];
			public readonly string[] Mat = new string[MaxFolge];
			public readonly string[] Ebene = new string[MaxFolge];
			public readonly float[] Abl = new float[MaxFolge];
			public readonly float[] AblChance = new float[MaxFolge];
			public readonly string[] Zustand = new string[MaxFolge];
			public readonly string[] Koerper = new string[MaxFolge];
			public readonly string[] Opfer = new string[MaxFolge];
			public readonly bool[] Splitter = new bool[MaxFolge];
			public readonly float[] Flug = new float[MaxFolge];
			public readonly float[] T = new float[MaxFolge];
		}

		private static readonly Akte[] _akten = new Akte[Plaetze];

		private static int _offen;

		private static int _nr;

		private static string _ichId = "";

		private static Player _ichFuer;

		internal static int LetzteNr => _nr;

		private static bool An => Einstellungen.Diagnose.Value || Einstellungen.KampfAufzeichnung.Value;

		private static bool IstIch(EFT.Ballistics.Shot s)
		{
			Player ich = Spielzugriff.Ich;
			if (ich == null)
			{
				return false;
			}
			if ((object)_ichFuer != ich)
			{
				_ichFuer = ich;
				_ichId = ich.Profile?.ProfileId ?? "";
			}
			return _ichId.Length > 0 && string.Equals(s.PlayerProfileID, _ichId);
		}

		internal static void RaidVorbei()
		{
			if (_offen == 0 && _nr == 0 && _ichFuer == null)
			{
				return;
			}
			for (int i = 0; i < Plaetze; i++)
			{
				if (_akten[i] != null)
				{
					_akten[i].Offen = false;
					_akten[i].Ziel = null;
				}
			}
			_offen = 0;
			_nr = 0;
			_ichFuer = null;
			_ichId = "";
		}

		// --- Schuss ---------------------------------------------------------

		/// <summary>Aus dem Schuss-Abgriff (Prefix auf FirearmController.Shot). Liefert die Schussnummer.</summary>
		internal static int Schuss(Player ich)
		{
			if (!An || ich == null)
			{
				return 0;
			}
			Akte a = FreierPlatz();
			_nr++;
			a.Offen = true;
			_offen++;
			a.Nr = _nr;
			a.TSchuss = Time.time;
			a.BildSchuss = Time.frameCount;
			a.HatLinie = Rueckstoss.Schusslinie(ich, out a.Fp, out a.Sd);
			if (!a.HatLinie)
			{
				a.Fp = ich.Position + Vector3.up * 1.5f;
				a.Sd = ich.LookDirection;
			}
			a.HatKugel = false;
			a.Gebunden = false;
			a.FireIndex = -1;
			a.Anzahl = 0;
			a.HatKopfD = false;
			a.BesterAbstandD = float.MaxValue;
			a.HatKopfE = false;
			a.HatRumpfE = false;

			// Ziel: das der Hilfe (gleiche Bedingung wie die Aufzeichnung), sonst
			// der Spieler, der der Schusslinie am naechsten ist.
			Player ziel = (SoftAim.TeilBild >= Time.frameCount - 1 && SoftAim.HatZiel) ? SoftAim.AktuellesZiel : null;
			a.ZielQuelle = "hilfe";
			if (ziel == null)
			{
				ziel = AmStrahl(ich, a.Fp, a.Sd);
				a.ZielQuelle = (ziel != null) ? "strahl" : "";
			}
			a.Ziel = ziel;
			a.ZielId = ziel?.Profile?.ProfileId ?? "";
			a.ZielName = ziel?.Profile?.Nickname ?? "";
			a.HatKopfS = false;
			a.HatRumpfS = false;
			a.HatVorhalt = false;
			a.EntfS = 0f;
			a.FlugzeitZiel = 0f;
			if (ziel != null)
			{
				a.HatKopfS = Kopf(ziel, out a.KopfS, out _);
				a.HatRumpfS = Rumpf(ziel, out a.RumpfS);
				a.PosS = ziel.Position;
				a.TempoS = ziel.Velocity;
				a.PoseS = ziel.MovementContext != null ? ziel.MovementContext.PoseLevel : 1f;
				a.LiegtS = ziel.MovementContext != null && ziel.MovementContext.IsInPronePose;
				Vector3 bezug = a.HatKopfS ? a.KopfS : (a.HatRumpfS ? a.RumpfS : ziel.Position);
				a.EntfS = Vector3.Distance(a.Fp, bezug);
				try
				{
					a.FlugzeitZiel = Zielpunkt.Flugzeit(ich, a.EntfS);
				}
				catch
				{
					a.FlugzeitZiel = a.EntfS / 850f;
				}
				if ((object)Zielpunkt.VorhaltZiel == ziel && Zielpunkt.VorhaltBild >= Time.frameCount - 1)
				{
					a.HatVorhalt = true;
					a.Vorhalt = Zielpunkt.VorhaltVektor;
					a.TempoGl = Zielpunkt.VorhaltTempoGl;
					a.TempoRoh = Zielpunkt.VorhaltTempoRoh;
					a.VorhaltFlugzeit = Zielpunkt.VorhaltFlugzeit;
					a.FallM = Zielpunkt.VorhaltFall;
					a.VorhaltBasis = Zielpunkt.VorhaltBasis;
				}
			}
			return a.Nr;
		}

		/// <summary>Text fuer die SCHUSS-Zeile und das schuss-Ereignis: angewandter Vorhalt.</summary>
		internal static string VorhaltText(int nr)
		{
			Akte a = Finden(nr);
			if (a == null)
			{
				return FormattableString.Invariant($" | schuss={nr}");
			}
			if (!a.HatVorhalt)
			{
				return FormattableString.Invariant($" | schuss={nr} | Vorhalt: nicht gerechnet (Ziel {(a.ZielName.Length > 0 ? a.ZielName : "keins")}, Quelle {a.ZielQuelle})");
			}
			Vector2 vb = Bildebene(a.Fp, a.VorhaltBasis, a.Vorhalt);
			Vector3 gl = a.TempoGl;
			Vector3 roh = a.TempoRoh;
			Vector2 glB = Bildebene(a.Fp, a.VorhaltBasis, gl) / 100f;
			Vector2 rohB = Bildebene(a.Fp, a.VorhaltBasis, roh) / 100f;
			return FormattableString.Invariant($" | schuss={nr} | Vorhalt quer {vb.x:+0.0;-0.0;0} cm, hoch {vb.y:+0.0;-0.0;0} cm ({a.Vorhalt.magnitude * 100f:0.0} cm), Flugzeit {a.VorhaltFlugzeit:0.000} s")
				+ FormattableString.Invariant($" | Tempo geglaettet quer {glB.x:+0.00;-0.00;0} m/s ({new Vector3(gl.x, 0f, gl.z).magnitude:0.00}), momentan quer {rohB.x:+0.00;-0.00;0} m/s ({new Vector3(roh.x, 0f, roh.z).magnitude:0.00})")
				+ FormattableString.Invariant($" | Fallausgleich {a.FallM * 100f:0.0} cm");
		}

		/// <summary>Felder fuer das schuss-Ereignis der Aufzeichnung.</summary>
		internal static void SchussFelder(StringBuilder s, int nr)
		{
			s.Append(",\"nr\":").Append(nr);
			Akte a = Finden(nr);
			if (a == null)
			{
				return;
			}
			s.Append(",\"ziel\":");
			Aufzeichnung.Str(s, a.ZielId);
			s.Append(",\"zielQuelle\":");
			Aufzeichnung.Str(s, a.ZielQuelle);
			if (!a.HatVorhalt)
			{
				return;
			}
			s.Append(",\"vorhalt\":{\"cm\":");
			Aufzeichnung.V3(s, a.Vorhalt * 100f, 1);
			s.Append(",\"bild\":");
			Aufzeichnung.V2(s, Bildebene(a.Fp, a.VorhaltBasis, a.Vorhalt), 1);
			s.Append(",\"tempoGl\":");
			Aufzeichnung.V3(s, a.TempoGl, 2);
			s.Append(",\"tempoRoh\":");
			Aufzeichnung.V3(s, a.TempoRoh, 2);
			s.Append(",\"tempoGlBild\":");
			Aufzeichnung.V2(s, Bildebene(a.Fp, a.VorhaltBasis, a.TempoGl) / 100f, 2);
			s.Append(",\"tempoRohBild\":");
			Aufzeichnung.V2(s, Bildebene(a.Fp, a.VorhaltBasis, a.TempoRoh) / 100f, 2);
			s.Append(",\"flugzeit\":");
			Aufzeichnung.Zahl(s, a.VorhaltFlugzeit, 4);
			s.Append(",\"fallCm\":");
			Aufzeichnung.Zahl(s, a.FallM * 100f, 1);
			s.Append('}');
		}

		/// <summary>Aus dem Kugel-Abgriff (InitiateShot): echter Start und Richtung MIT Streuung.</summary>
		internal static void Kugel(Vector3 start, Vector3 richtung)
		{
			if (_offen == 0)
			{
				return;
			}
			Akte a = Finden(_nr);
			if (a == null || a.HatKugel || a.BildSchuss != Time.frameCount)
			{
				return;
			}
			a.HatKugel = true;
			a.KugelP = start;
			a.KugelDir = richtung.normalized;
		}

		// --- Kugel unterwegs ------------------------------------------------

		/// <summary>Aus dem Postfix auf Shot.HandleCollision: jeder Einschlag, auch Splitter und Koerper.</summary>
		internal static void Kollision(EFT.Ballistics.Shot s)
		{
			if (_offen == 0 || s == null || !IstIch(s))
			{
				return;
			}
			Akte a = Zuordnen(s);
			if (a == null)
			{
				return;
			}
			int i = a.Anzahl;
			a.Anzahl++;
			if (i >= MaxFolge)
			{
				return;
			}
			BallisticCollider bc = s.HittedBallisticCollider;
			Collider kol = s.HitCollider;
			a.Punkt[i] = s.HitPoint;
			a.Kol[i] = (kol != null) ? kol.name : "?";
			a.Mat[i] = (bc != null) ? bc.TypeOfMaterial.ToString() : "?";
			// Ebene des Kolliders: die Kugel trifft auf Water, Terrain,
			// HighPolyCollider, TransparentCollider, Deadbody, HitCollider
			// (BallisticsCalculatorConstants.HitMask) - die Sichtpruefung der Hilfe
			// (Durchschuss.Ebenenmaske) kennt TransparentCollider und Water nicht.
			a.Ebene[i] = (kol != null) ? LayerMask.LayerToName(kol.gameObject.layer) : "?";
			// Ablenkung beim Durchschuss: Shot.GetDeviatedTrajectory, Zufall *
			// TrajectoryDeviation mit Chance TrajectoryDeviationChance.
			a.Abl[i] = (bc != null) ? bc.TrajectoryDeviation : 0f;
			a.AblChance[i] = (bc != null) ? bc.TrajectoryDeviationChance : 0f;
			a.Zustand[i] = s.BulletState.ToString();
			a.Splitter[i] = s.Parent != null;
			float flug = 0f;
			for (EFT.Ballistics.Shot k = s; k != null; k = k.Parent)
			{
				flug += k.TimeSinceShot;
			}
			a.Flug[i] = flug;
			a.T[i] = Time.time;
			a.Koerper[i] = "";
			a.Opfer[i] = "";
			if (bc is BodyPartCollider bp)
			{
				a.Koerper[i] = bp.BodyPartColliderType.ToString()
					+ ((bp is ArmorPlateCollider ap) ? ("/" + ap.ArmorPlateColliderType) : "");
				a.Opfer[i] = (bp.playerBridge?.iPlayer as Player)?.Profile?.ProfileId ?? "";
			}
			if (i == 0 && a.Ziel != null)
			{
				Player z = a.Ziel;
				a.HatKopfE = Kopf(z, out a.KopfE, out a.KopfEPhys);
				a.HatRumpfE = Rumpf(z, out a.RumpfE);
				a.PosE = z.Position;
				a.TempoE = z.Velocity;
				a.PoseE = z.MovementContext != null ? z.MovementContext.PoseLevel : 1f;
				a.LiegtE = z.MovementContext != null && z.MovementContext.IsInPronePose;
			}
		}

		/// <summary>Aus dem Prefix auf Shot.ReturnShotToPool: Hauptkugel fertig (alle Splitter auch).</summary>
		internal static void Freigabe(EFT.Ballistics.Shot s)
		{
			if (_offen == 0 || s == null || s.Parent != null || !IstIch(s))
			{
				return;
			}
			Akte a = Zuordnen(s);
			if (a == null)
			{
				return;
			}
			bool hatD = false;
			Vector3 durchgang = Vector3.zero;
			if (a.Ziel != null && a.EntfS > 1f)
			{
				Vector3 bezug = a.HatKopfS ? a.KopfS : a.RumpfS;
				hatD = Durchgang(s, a.Fp, (bezug - a.Fp).normalized, a.EntfS, out durchgang);
			}
			string grund = (a.Anzahl > 0) ? "einschlag"
				: (s.IsFlyingOutOfTime ? "verfallen" : "ohne Einschlag freigegeben");
			Abschliessen(a, grund, hatD, durchgang, s.CurrentPosition);
		}

		/// <summary>Je Bild (LateUpdate): Kopf zur geschaetzten Durchgangszeit lesen, alte Akten schliessen.</summary>
		internal static void Tick()
		{
			if (_offen == 0)
			{
				return;
			}
			float jetzt = Time.time;
			for (int i = 0; i < Plaetze; i++)
			{
				Akte a = _akten[i];
				if (a == null || !a.Offen)
				{
					continue;
				}
				if (jetzt - a.TSchuss > Hoechstdauer)
				{
					Abschliessen(a, "keine Freigabe binnen 8 s", false, Vector3.zero, Vector3.zero);
					continue;
				}
				if (a.Ziel == null)
				{
					continue;
				}
				float soll = a.TSchuss + a.FlugzeitZiel;
				float abstand = Mathf.Abs(jetzt - soll);
				if (abstand < a.BesterAbstandD)
				{
					if (Kopf(a.Ziel, out var k, out _))
					{
						a.KopfD = k;
						a.HatKopfD = true;
						a.TKopfD = jetzt;
						a.BesterAbstandD = abstand;
						if (Rumpf(a.Ziel, out var r))
						{
							a.RumpfD = r;
						}
					}
				}
			}
		}

		// --- Abschluss ------------------------------------------------------

		private static readonly StringBuilder _sb = new StringBuilder(2048);

		private static void Abschliessen(Akte a, string grund, bool hatD, Vector3 durchgang, Vector3 letzterOrt)
		{
			a.Offen = false;
			_offen = Mathf.Max(0, _offen - 1);
			try
			{
				Schreiben(a, grund, hatD, durchgang, letzterOrt);
			}
			catch (Exception ex)
			{
				SoftAim.FehlerMelden(ex);
			}
			a.Ziel = null;
		}

		private static void Schreiben(Akte a, string grund, bool hatD, Vector3 durchgang, Vector3 letzterOrt)
		{
			bool diag = Einstellungen.Diagnose.Value;
			bool aufz = Einstellungen.KampfAufzeichnung.Value;
			if (!diag && !aufz)
			{
				return;
			}
			Vector3 fp = a.Fp;
			Vector3 start = a.HatKugel ? a.KugelP : fp;
			Vector3 kdir = a.HatKugel ? a.KugelDir : a.Sd;
			bool hatEinschlag = a.Anzahl > 0;
			Vector3 p0 = hatEinschlag ? a.Punkt[0] : (hatD ? durchgang : letzterOrt);

			// Bezugspunkt in Zieltiefe und der Kopf in diesem Moment.
			bool hatZiel = a.Ziel != null && a.HatKopfS;
			string bezug = "";
			Vector3 pz = p0;
			Vector3 kopfRef = a.KopfS;
			Vector3 rumpfRef = a.RumpfS;
			float vorZiel = 0f, abstandZiel = -1f;
			bool bewuchs = false;
			Vector2 ab = Vector2.zero, bb = Vector2.zero, cb = Vector2.zero, lin = Vector2.zero, streu = Vector2.zero;
			string zone = "";
			bool zielGetroffen = false;
			// Erster Koerpertreffer der Kette (auch nach Durchschuss eines
			// Hindernisses durch den Splitter).
			int kt = -1;
			for (int i = 0; i < Mathf.Min(a.Anzahl, MaxFolge); i++)
			{
				if (a.Koerper[i].Length > 0)
				{
					kt = i;
					break;
				}
			}
			if (kt >= 0)
			{
				zone = ZoneVon(a.Koerper[kt]);
				zielGetroffen = a.Opfer[kt].Length > 0 && a.Opfer[kt] == a.ZielId;
			}
			if (hatZiel)
			{
				Vector3 achse = (a.KopfS - fp).normalized;
				float tiefe0 = Vector3.Dot(p0 - fp, achse);
				if (kt >= 0)
				{
					// Koerpertreffer: genau dort, Kopf im Moment des (ersten) Einschlags.
					bezug = (kt == 0) ? "koerper" : "koerper-nach-hindernis";
					pz = a.Punkt[kt];
					if (a.HatKopfE)
					{
						kopfRef = a.KopfE;
						rumpfRef = a.HatRumpfE ? a.RumpfE : a.RumpfS;
					}
				}
				else if (hatD)
				{
					// Kugel (oder ihr Splitter nach einem Hindernis) flog durch die
					// Zieltiefe: Durchgangspunkt gegen den Kopf zur Durchgangszeit.
					bezug = (hatEinschlag && tiefe0 < a.EntfS - 0.3f) ? "durchgang-nach-hindernis" : "durchgang";
					pz = durchgang;
					if (a.HatKopfD)
					{
						kopfRef = a.KopfD;
						rumpfRef = a.RumpfD;
					}
				}
				else if (hatEinschlag && tiefe0 <= a.EntfS + 1.5f)
				{
					// Einschlag vor, im oder knapp hinter dem Ziel: Kopf beim Einschlag.
					bezug = "einschlag";
					pz = p0;
					if (a.HatKopfE)
					{
						kopfRef = a.KopfE;
						rumpfRef = a.HatRumpfE ? a.RumpfE : a.RumpfS;
					}
				}
				else
				{
					bezug = hatEinschlag ? "einschlag-hinten" : "letzter-ort";
					if (a.HatKopfE)
					{
						kopfRef = a.KopfE;
					}
				}
				ab = Abw(fp, kopfRef, pz);
				bb = Abw(fp, a.KopfS, pz);
				cb = a.HatVorhalt ? (ab - Bildebene(fp, kopfRef, a.Vorhalt)) : ab;
				lin = Abw(fp, a.KopfS, fp + a.Sd * a.EntfS);
				streu = Abw(fp, a.KopfS, start + kdir * a.EntfS) - lin;
				if (hatEinschlag)
				{
					vorZiel = a.EntfS - tiefe0;
					Vector3 kE = a.HatKopfE ? a.KopfE : a.KopfS;
					Vector3 rE = a.HatRumpfE ? a.RumpfE : a.RumpfS;
					abstandZiel = Mathf.Min(Vector3.Distance(p0, kE), a.HatRumpfS ? Vector3.Distance(p0, rE) : float.MaxValue);
					bewuchs = a.Koerper[0].Length == 0 && vorZiel > 0.3f && Durchlaessig(a.Mat[0]);
				}
			}

			if (diag)
			{
				_sb.Length = 0;
				_sb.Append(FormattableString.Invariant($"[Zielhilfe] EINSCHLAG t={Time.time:0.000} schuss={a.Nr} | Punkt {p0.x:0.00},{p0.y:0.00},{p0.z:0.00} | "));
				if (hatEinschlag)
				{
					_sb.Append(a.Kol[0]).Append(" [").Append(a.Mat[0]).Append(", ").Append(a.Zustand[0]).Append(", Ebene ").Append(a.Ebene[0]);
					if (a.AblChance[0] > 0f)
					{
						_sb.Append(FormattableString.Invariant($", Ablenkung {a.Abl[0]:0.###} mit Chance {a.AblChance[0]:0.##}"));
					}
					_sb.Append(']');
					if (a.Koerper[0].Length > 0)
					{
						_sb.Append(" Koerper ").Append(a.Koerper[0]).Append(zielGetroffen ? " am Ziel" : " an anderem");
					}
					_sb.Append(FormattableString.Invariant($" | Flugzeit {a.Flug[0]:0.000} s"));
				}
				else
				{
					_sb.Append("kein Einschlag (").Append(grund).Append(')');
				}
				if (hatZiel)
				{
					_sb.Append(FormattableString.Invariant($" | Ziel {a.ZielName} ({a.ZielQuelle}) {a.EntfS:0.0} m, "));
					_sb.Append(zone.Length > 0 ? ("Treffer " + zone + (zielGetroffen ? "" : " (anderer)")) : "daneben");
					_sb.Append(FormattableString.Invariant($" | Ziel-Punkt {pz.x:0.00},{pz.y:0.00},{pz.z:0.00}"));
					_sb.Append(FormattableString.Invariant($" | Bezug {bezug} | (a) Kopf jetzt quer {ab.x:+0.0;-0.0;0} hoch {ab.y:+0.0;-0.0;0} cm"));
					_sb.Append(FormattableString.Invariant($" | (b) Kopf beim Schuss quer {bb.x:+0.0;-0.0;0} hoch {bb.y:+0.0;-0.0;0} cm"));
					_sb.Append(FormattableString.Invariant($" | (c) ohne Vorhalt quer {cb.x:+0.0;-0.0;0} hoch {cb.y:+0.0;-0.0;0} cm"));
					_sb.Append(FormattableString.Invariant($" | Linie quer {lin.x:+0.0;-0.0;0} hoch {lin.y:+0.0;-0.0;0} cm, Streuung quer {streu.x:+0.0;-0.0;0} hoch {streu.y:+0.0;-0.0;0} cm"));
					Vector3 weg = (kopfRef - a.KopfS) * 100f;
					_sb.Append(FormattableString.Invariant($" | Kopf bewegt {weg.magnitude:0.0} cm (hoch {weg.y:+0.0;-0.0;0}), Haltung {a.PoseS:0.00}->{(a.HatKopfE ? a.PoseE : a.PoseS):0.00}, Tempo {new Vector3(a.TempoS.x, 0f, a.TempoS.z).magnitude:0.00} m/s"));
					if (a.HatVorhalt)
					{
						Vector2 vb = Bildebene(fp, kopfRef, a.Vorhalt);
						_sb.Append(FormattableString.Invariant($" | Vorhalt quer {vb.x:+0.0;-0.0;0} hoch {vb.y:+0.0;-0.0;0} cm, Fall {a.FallM * 100f:0.0} cm"));
					}
					if (hatEinschlag && a.Koerper[0].Length == 0)
					{
						_sb.Append(FormattableString.Invariant($" | {vorZiel:0.0} m vor dem Ziel, {abstandZiel:0.00} m vom Ziel"));
						if (bewuchs)
						{
							_sb.Append(" (DURCHLAESSIG: ").Append(a.Mat[0]).Append(')');
						}
					}
				}
				else
				{
					_sb.Append(" | kein Ziel");
				}
				int n = Mathf.Min(a.Anzahl, MaxFolge);
				for (int i = 1; i < n; i++)
				{
					_sb.Append(FormattableString.Invariant($" | Folge {i}: {a.Kol[i]} [{a.Mat[i]}, {a.Zustand[i]}, Ebene {a.Ebene[i]}]"));
					if (a.Koerper[i].Length > 0)
					{
						_sb.Append(" Koerper ").Append(a.Koerper[i]).Append(a.Opfer[i] == a.ZielId ? " am Ziel" : " an anderem");
					}
					_sb.Append(FormattableString.Invariant($" {a.Flug[i]:0.000} s"));
				}
				SoftAim.Protokoll?.LogInfo(_sb.ToString());
			}

			if (aufz)
			{
				StringBuilder s = _sb;
				s.Length = 0;
				s.Append("{\"typ\":\"ergebnis\",\"nr\":").Append(a.Nr);
				s.Append(",\"tSchuss\":");
				Aufzeichnung.Zahl(s, a.TSchuss, 4);
				s.Append(",\"grund\":");
				Aufzeichnung.Str(s, grund);
				s.Append(",\"fp\":");
				Aufzeichnung.V3(s, fp, 3);
				s.Append(",\"sd\":");
				Aufzeichnung.V3(s, a.Sd, 6);
				if (a.HatKugel)
				{
					s.Append(",\"kdir\":");
					Aufzeichnung.V3(s, kdir, 6);
				}
				s.Append(",\"einschlaege\":[");
				int n = Mathf.Min(a.Anzahl, MaxFolge);
				for (int i = 0; i < n; i++)
				{
					if (i > 0)
					{
						s.Append(',');
					}
					s.Append("{\"p\":");
					Aufzeichnung.V3(s, a.Punkt[i], 3);
					s.Append(",\"kol\":");
					Aufzeichnung.Str(s, a.Kol[i]);
					s.Append(",\"mat\":");
					Aufzeichnung.Str(s, a.Mat[i]);
					s.Append(",\"zust\":");
					Aufzeichnung.Str(s, a.Zustand[i]);
					s.Append(",\"ebene\":");
					Aufzeichnung.Str(s, a.Ebene[i]);
					if (a.AblChance[i] > 0f)
					{
						s.Append(",\"abl\":");
						Aufzeichnung.Zahl(s, a.Abl[i], 4);
						s.Append(",\"ablChance\":");
						Aufzeichnung.Zahl(s, a.AblChance[i], 3);
					}
					s.Append(",\"splitter\":").Append(a.Splitter[i] ? '1' : '0');
					s.Append(",\"flug\":");
					Aufzeichnung.Zahl(s, a.Flug[i], 4);
					s.Append(",\"t\":");
					Aufzeichnung.Zahl(s, a.T[i], 4);
					if (a.Koerper[i].Length > 0)
					{
						s.Append(",\"koerper\":");
						Aufzeichnung.Str(s, a.Koerper[i]);
						s.Append(",\"opfer\":");
						Aufzeichnung.Str(s, a.Opfer[i]);
					}
					s.Append('}');
				}
				s.Append(']');
				s.Append(",\"anzahl\":").Append(a.Anzahl);
				if (!hatEinschlag)
				{
					s.Append(",\"letzterOrt\":");
					Aufzeichnung.V3(s, letzterOrt, 2);
				}
				if (hatD)
				{
					s.Append(",\"durchgang\":");
					Aufzeichnung.V3(s, durchgang, 3);
				}
				if (a.Ziel != null || a.ZielId.Length > 0)
				{
					s.Append(",\"ziel\":");
					Aufzeichnung.Str(s, a.ZielId);
					s.Append(",\"zielN\":");
					Aufzeichnung.Str(s, a.ZielName);
					s.Append(",\"zielQuelle\":");
					Aufzeichnung.Str(s, a.ZielQuelle);
				}
				if (hatZiel)
				{
					s.Append(",\"entf\":");
					Aufzeichnung.Zahl(s, a.EntfS, 2);
					s.Append(",\"flugzeitZiel\":");
					Aufzeichnung.Zahl(s, a.FlugzeitZiel, 4);
					s.Append(",\"beimSchuss\":{\"kopf\":");
					Aufzeichnung.V3(s, a.KopfS, 3);
					if (a.HatRumpfS)
					{
						s.Append(",\"rumpf\":");
						Aufzeichnung.V3(s, a.RumpfS, 3);
					}
					s.Append(",\"p\":");
					Aufzeichnung.V3(s, a.PosS, 3);
					s.Append(",\"v\":");
					Aufzeichnung.V3(s, a.TempoS, 2);
					s.Append(",\"pose\":");
					Aufzeichnung.Zahl(s, a.PoseS, 2);
					s.Append(",\"liegt\":").Append(a.LiegtS ? '1' : '0');
					s.Append('}');
					if (a.HatKopfE)
					{
						s.Append(",\"beimEinschlag\":{\"kopf\":");
						Aufzeichnung.V3(s, a.KopfE, 3);
						s.Append(",\"kopfPhys\":");
						Aufzeichnung.V3(s, a.KopfEPhys, 3);
						if (a.HatRumpfE)
						{
							s.Append(",\"rumpf\":");
							Aufzeichnung.V3(s, a.RumpfE, 3);
						}
						s.Append(",\"p\":");
						Aufzeichnung.V3(s, a.PosE, 3);
						s.Append(",\"v\":");
						Aufzeichnung.V3(s, a.TempoE, 2);
						s.Append(",\"pose\":");
						Aufzeichnung.Zahl(s, a.PoseE, 2);
						s.Append(",\"liegt\":").Append(a.LiegtE ? '1' : '0');
						s.Append('}');
					}
					if (a.HatKopfD)
					{
						s.Append(",\"beimDurchgang\":{\"kopf\":");
						Aufzeichnung.V3(s, a.KopfD, 3);
						s.Append(",\"rumpf\":");
						Aufzeichnung.V3(s, a.RumpfD, 3);
						s.Append(",\"t\":");
						Aufzeichnung.Zahl(s, a.TKopfD, 4);
						s.Append('}');
					}
					if (a.HatVorhalt)
					{
						s.Append(",\"vorhalt\":{\"cm\":");
						Aufzeichnung.V3(s, a.Vorhalt * 100f, 1);
						s.Append(",\"bild\":");
						Aufzeichnung.V2(s, Bildebene(fp, kopfRef, a.Vorhalt), 1);
						s.Append(",\"tempoGl\":");
						Aufzeichnung.V3(s, a.TempoGl, 2);
						s.Append(",\"tempoRoh\":");
						Aufzeichnung.V3(s, a.TempoRoh, 2);
						s.Append(",\"flugzeit\":");
						Aufzeichnung.Zahl(s, a.VorhaltFlugzeit, 4);
						s.Append(",\"fallCm\":");
						Aufzeichnung.Zahl(s, a.FallM * 100f, 1);
						s.Append('}');
					}
					s.Append(",\"bezug\":");
					Aufzeichnung.Str(s, bezug);
					s.Append(",\"pz\":");
					Aufzeichnung.V3(s, pz, 3);
					s.Append(",\"a\":");
					Aufzeichnung.V2(s, ab, 1);
					s.Append(",\"b\":");
					Aufzeichnung.V2(s, bb, 1);
					s.Append(",\"c\":");
					Aufzeichnung.V2(s, cb, 1);
					s.Append(",\"linie\":");
					Aufzeichnung.V2(s, lin, 1);
					s.Append(",\"streu\":");
					Aufzeichnung.V2(s, streu, 1);
					s.Append(",\"kopfWeg\":");
					Aufzeichnung.V3(s, (kopfRef - a.KopfS) * 100f, 1);
					if (hatEinschlag)
					{
						s.Append(",\"vorZiel\":");
						Aufzeichnung.Zahl(s, vorZiel, 2);
						s.Append(",\"abstandZiel\":");
						Aufzeichnung.Zahl(s, abstandZiel, 2);
						s.Append(",\"durchlaessig\":").Append(bewuchs ? '1' : '0');
					}
				}
				s.Append(",\"treffer\":").Append(zone.Length > 0 ? '1' : '0');
				if (zone.Length > 0)
				{
					s.Append(",\"zone\":");
					Aufzeichnung.Str(s, zone);
					s.Append(",\"zielGetroffen\":").Append(zielGetroffen ? '1' : '0');
				}
				s.Append('}');
				Aufzeichnung.EreignisDazu(s.ToString());
			}
		}

		// --- Hilfen ---------------------------------------------------------

		private static Akte FreierPlatz()
		{
			Akte aelteste = null;
			for (int i = 0; i < Plaetze; i++)
			{
				Akte a = _akten[i] ?? (_akten[i] = new Akte());
				if (!a.Offen)
				{
					return a;
				}
				if (aelteste == null || a.TSchuss < aelteste.TSchuss)
				{
					aelteste = a;
				}
			}
			// Alle 16 offen (Dauerfeuer in die Ferne): die aelteste abschliessen.
			Abschliessen(aelteste, "verdraengt", false, Vector3.zero, Vector3.zero);
			return aelteste;
		}

		private static Akte Finden(int nr)
		{
			for (int i = 0; i < Plaetze; i++)
			{
				Akte a = _akten[i];
				if (a != null && a.Offen && a.Nr == nr)
				{
					return a;
				}
			}
			return null;
		}

		/// <summary>
		/// Akte zur Kugel: ueber den FireIndex, sonst (erste Begegnung) ueber den
		/// Startpunkt - MasterOrigin ist genau das shotPosition aus InitiateShot.
		/// </summary>
		private static Akte Zuordnen(EFT.Ballistics.Shot s)
		{
			int fi = s.FireIndex;
			for (int i = 0; i < Plaetze; i++)
			{
				Akte a = _akten[i];
				if (a != null && a.Offen && a.Gebunden && a.FireIndex == fi)
				{
					return a;
				}
			}
			Vector3 ursprung = s.MasterOrigin;
			Akte beste = null;
			float bester = 1E-04f;
			for (int i = 0; i < Plaetze; i++)
			{
				Akte a = _akten[i];
				if (a == null || !a.Offen || a.Gebunden || !a.HatKugel)
				{
					continue;
				}
				float d = (a.KugelP - ursprung).sqrMagnitude;
				if (d < bester)
				{
					bester = d;
					beste = a;
				}
			}
			if (beste != null)
			{
				beste.Gebunden = true;
				beste.FireIndex = fi;
			}
			return beste;
		}

		/// <summary>Kopfmitte = Mitte der HeadCommon-Box (Transform) und dieselbe aus Collider.bounds (Physik-Stand).</summary>
		private static bool Kopf(Player p, out Vector3 mitte, out Vector3 phys)
		{
			mitte = phys = Vector3.zero;
			Dictionary<EBodyPartColliderType, BodyPartCollider> karte = p?.PlayerBones?.BodyPartCollidersDictionary;
			if (karte == null || !karte.TryGetValue(EBodyPartColliderType.HeadCommon, out var box) || box == null || box.Collider == null)
			{
				return false;
			}
			if (!Trefferzonen.Form(box.Collider, out mitte, out _, out _, out _))
			{
				return false;
			}
			phys = box.Collider.bounds.center;
			return true;
		}

		/// <summary>Rumpfmitte = Mitte der Huelle aller Rumpf-Kollider (wie unruhe in der Aufzeichnung).</summary>
		private static bool Rumpf(Player p, out Vector3 mitte)
		{
			mitte = Vector3.zero;
			if (!Trefferzonen.Huelle(p, Trefferzonen.Rumpf, out var b))
			{
				return false;
			}
			mitte = b.center;
			return true;
		}

		/// <summary>Spieler, der der Schusslinie am naechsten ist (hoechstens 2,5 m daneben).</summary>
		private static Player AmStrahl(Player ich, Vector3 fp, Vector3 sd)
		{
			List<Player> liste = Spielzugriff.Welt?.AllAlivePlayersList;
			if (liste == null)
			{
				return null;
			}
			Player beste = null;
			float bester = 2.5f;
			for (int i = 0; i < liste.Count; i++)
			{
				Player p = liste[i];
				if (p == null || (object)p == ich)
				{
					continue;
				}
				Vector3 v = p.Position + Vector3.up * 1.2f - fp;
				float entlang = Vector3.Dot(v, sd);
				if (entlang < 2f || entlang > 1200f)
				{
					continue;
				}
				float quer = (v - sd * entlang).magnitude;
				if (quer < bester)
				{
					bester = quer;
					beste = p;
				}
			}
			return beste;
		}

		/// <summary>
		/// Wo die Kugel die Zieltiefe (Abstand entlang der Achse) durchflog: aus
		/// PositionHistory der Hauptkugel und ihrer Splitter (Punkt je Flugschritt,
		/// beim Einschlag durch den Treffpunkt ersetzt), linear zwischen zwei Schritten.
		/// </summary>
		private static bool Durchgang(EFT.Ballistics.Shot wurzel, Vector3 fp, Vector3 achse, float tiefe, out Vector3 punkt)
		{
			punkt = Vector3.zero;
			EFT.Ballistics.Shot s = wurzel;
			int schutz = 0;
			while (s != null && schutz++ < 8)
			{
				List<Vector3> h = s.PositionHistory;
				for (int i = 1; i < h.Count; i++)
				{
					float a0 = Vector3.Dot(h[i - 1] - fp, achse);
					float a1 = Vector3.Dot(h[i] - fp, achse);
					if (a0 <= tiefe && a1 >= tiefe && a1 > a0)
					{
						punkt = Vector3.Lerp(h[i - 1], h[i], (tiefe - a0) / (a1 - a0));
						return true;
					}
				}
				s = (s.Fragments.Count > 0) ? s.Fragments[0] : null;
			}
			return false;
		}

		/// <summary>
		/// Abweichung von p gegenueber bezug in der Bildebene des Schuetzen (cm),
		/// x = rechts, y = hoch, auf die Tiefe des Bezugs umgerechnet (Winkel x
		/// Entfernung) - ein Einschlag 10 m davor zaehlt so, wie er im Glas laege.
		/// </summary>
		internal static Vector2 Abw(Vector3 fp, Vector3 bezug, Vector3 p)
		{
			Vector3 achse = bezug - fp;
			float d = achse.magnitude;
			if (d < 0.01f)
			{
				return Vector2.zero;
			}
			achse /= d;
			Achsen(achse, out var rechts, out var hoch);
			Vector3 v = p - fp;
			float entlang = Vector3.Dot(v, achse);
			float k = (entlang > 0.5f) ? (d / entlang) : 1f;
			return new Vector2(Vector3.Dot(v, rechts) * k * 100f, Vector3.Dot(v, hoch) * k * 100f);
		}

		/// <summary>Ein Weltvektor (m) in der Bildebene um die Linie fp->bezug, in cm (x rechts, y hoch).</summary>
		internal static Vector2 Bildebene(Vector3 fp, Vector3 bezug, Vector3 w)
		{
			Vector3 achse = bezug - fp;
			if (achse.sqrMagnitude < 1E-04f)
			{
				return Vector2.zero;
			}
			Achsen(achse.normalized, out var rechts, out var hoch);
			return new Vector2(Vector3.Dot(w, rechts) * 100f, Vector3.Dot(w, hoch) * 100f);
		}

		private static void Achsen(Vector3 achse, out Vector3 rechts, out Vector3 hoch)
		{
			// Unity ist linkshaendig: Cross(oben, vorn) zeigt nach rechts.
			rechts = Vector3.Cross(Vector3.up, achse);
			if (rechts.sqrMagnitude < 1E-06f)
			{
				rechts = Vector3.right;
			}
			rechts.Normalize();
			hoch = Vector3.Cross(achse, rechts);
		}

		private static string ZoneVon(string koerper)
		{
			int schraeg = koerper.IndexOf('/');
			string typ = (schraeg >= 0) ? koerper.Substring(0, schraeg) : koerper;
			if (In(Trefferzonen.Kopf, typ))
			{
				return "Kopf";
			}
			if (In(Trefferzonen.Rumpf, typ))
			{
				return "Rumpf";
			}
			if (In(Trefferzonen.Becken, typ))
			{
				return "Becken";
			}
			if (In(Trefferzonen.Beine, typ))
			{
				return "Beine";
			}
			if (In(Trefferzonen.Arme, typ))
			{
				return "Arme";
			}
			// Platten ohne Kollidertyp (None) sitzen am Rumpf.
			return "Rumpf";
		}

		private static bool In(EBodyPartColliderType[] typen, string name)
		{
			for (int i = 0; i < typen.Length; i++)
			{
				if (typen[i].ToString() == name)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Weiche/durchschiessbare Materialien (Bewuchs, Zaun, Stoff, Pappe, Glas).</summary>
		private static bool Durchlaessig(string mat)
		{
			switch (mat)
			{
			case "GrassHigh":
			case "GrassLow":
			case "WoodThin":
			case "Chainfence":
			case "GenericSoft":
			case "Fabric":
			case "Cardboard":
			case "GarbagePaper":
			case "Plastic":
			case "Glass":
			case "GlassShattered":
			case "Grate":
				return true;
			default:
				return false;
			}
		}
	}
}
