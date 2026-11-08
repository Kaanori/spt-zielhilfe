// ---------------------------------------------------------------
//  Ruhe im Zielpunkt (ab 1.4.1) - reine Logik ohne Unity, damit der
//  Offline-Test (Zielhilfe-QuickSetupTest/) sie per Reflexion pruefen kann.
//
//  Die Zielwahl ("Visible"-Kopfbox, Brust-Ausweichen) laeuft in jedem Bild
//  neu. Flackert die Sicht an einer Deckungskante, springt der Zielpunkt
//  zwischen zwei Stellen hin und her - die Schusslinie wandert mit, der
//  Feuerstoss wird abgebrochen oder trifft daneben. Zwei kleine Zustands-
//  halter schaffen Ruhe:
//
//    BoxHalter  - die einmal gewaehlte Kopfbox bleibt, solange sie frei ist;
//                 neu gewaehlt wird erst, wenn sie verdeckt ist.
//    Entprellung- Sichtzustand je Trefferzone (Kopf/Rumpf/Becken/Beine) fuer
//                 Soft Aim per Maus: eine Zone gilt erst als zu, wenn sie
//                 eine Weile zu war, und erst als wieder frei, wenn sie eine
//                 Weile frei war - sonst wechselt die Anziehung bei jedem
//                 Flackern zwischen Kopf und Rumpf.
//    ZonenRuhe  - Kopf <-> Brust (Kopf verdeckt): erst nach einer kurzen
//                 Zeit umschalten, nicht bei jedem Flackern. Waehrend eines
//                 Feuerstosses ist der Wechsel gesperrt.
//
//  Beide Halter gelten je Ziel: ein anderes Ziel setzt sie zurueck.
// ---------------------------------------------------------------
using System;

namespace Zielhilfe
{
	/// <summary>Merkt je Ziel die zuletzt gewaehlte freie Kopfbox (als int-Kennung).</summary>
	internal sealed class BoxHalter
	{
		internal static readonly BoxHalter Instanz = new BoxHalter();

		private object _ziel;

		private int _box;

		private bool _hat;

		internal void Vergessen()
		{
			_ziel = null;
			_hat = false;
		}

		/// <summary>Die gehaltene Box dieses Ziels, falls vorhanden.</summary>
		internal bool Gehalten(object ziel, out int box)
		{
			box = _box;
			return _hat && ReferenceEquals(_ziel, ziel);
		}

		internal void Merke(object ziel, int box)
		{
			_ziel = ziel;
			_box = box;
			_hat = true;
		}

		/// <summary>
		/// Waehlt die Box: die gehaltene, solange sie frei ist (und halten gilt),
		/// sonst die erste freie in der Reihenfolge. -1, wenn keine frei ist.
		/// Die Sichtpruefung kommt als Funktion, damit nur geprueft wird, was
		/// noetig ist (jede Pruefung ist ein Strahl).
		/// </summary>
		internal int Waehle(object ziel, int[] reihenfolge, Func<int, bool> frei, bool halten = true)
		{
			if (halten && Gehalten(ziel, out int h) && frei(h))
			{
				return h;
			}
			for (int i = 0; i < reihenfolge.Length; i++)
			{
				if (frei(reihenfolge[i]))
				{
					Merke(ziel, reihenfolge[i]);
					return reihenfolge[i];
				}
			}
			return -1;
		}
	}

	/// <summary>
	/// Kopf oder Brust, wenn der Kopf verdeckt ist. Zum Brustziel wechselt man
	/// erst, wenn der Kopf <see cref="Abwurf"/> Sekunden am Stueck verdeckt war,
	/// zurueck zum Kopf erst nach <see cref="Rueck"/> Sekunden am Stueck frei.
	/// Beim ersten Bild eines Ziels gilt sofort, was Sache ist.
	/// </summary>
	internal sealed class ZonenRuhe
	{
		internal static readonly ZonenRuhe Instanz = new ZonenRuhe();

		/// <summary>Kopf muss so lange verdeckt sein, bevor auf die Brust gewechselt wird.</summary>
		internal const float Abwurf = 0.2f;

		/// <summary>Kopf muss so lange frei sein, bevor von der Brust zurueck gewechselt wird.</summary>
		internal const float Rueck = 0.35f;

		/// <summary>Waehrend eines Feuerstosses nicht umschalten (Zielpunkt bleibt).</summary>
		internal bool Gesperrt;

		private object _ziel;

		private bool _brust;

		private bool _erzwungen;

		private bool _frei;

		private float _seit;

		internal void Vergessen()
		{
			_ziel = null;
			_brust = false;
			_erzwungen = false;
			Gesperrt = false;
		}

		private void Neu(object ziel, float jetzt, bool kopfFrei)
		{
			_ziel = ziel;
			_brust = !kopfFrei;
			_erzwungen = false;
			_frei = kopfFrei;
			_seit = jetzt;
		}

		/// <summary>
		/// Pro Bild. Liefert, ob auf die Brust gezielt werden soll.
		/// <paramref name="erlaubt"/> = die Einstellung "Fall back to chest" -
		/// ausser die AFK-Wache hat die Brust erzwungen (<see cref="Erzwinge"/>).
		/// </summary>
		internal bool Schritt(object ziel, float jetzt, bool kopfFrei, bool erlaubt)
		{
			if (!ReferenceEquals(_ziel, ziel))
			{
				Neu(ziel, jetzt, kopfFrei);
			}
			else if (!Gesperrt)
			{
				if (kopfFrei != _frei)
				{
					_frei = kopfFrei;
					_seit = jetzt;
				}
				float dauer = jetzt - _seit;
				if (!_brust && !kopfFrei && dauer >= Abwurf)
				{
					_brust = true;
				}
				else if (_brust && kopfFrei && dauer >= Rueck)
				{
					_brust = false;
					_erzwungen = false;
				}
			}
			return _brust && (erlaubt || _erzwungen);
		}

		/// <summary>
		/// Die Schussbahn zum Kopf ist nachweislich zu (AFK-Wache): ab jetzt Brust,
		/// auch wenn "Fall back to chest" aus ist, bis der Kopf wieder lange genug frei ist.
		/// </summary>
		internal void Erzwinge(object ziel, float jetzt)
		{
			if (Gesperrt)
			{
				return;
			}
			if (!ReferenceEquals(_ziel, ziel))
			{
				Neu(ziel, jetzt, false);
			}
			_brust = true;
			_erzwungen = true;
			_frei = false;
			_seit = jetzt;
		}
	}

	/// <summary>
	/// Zielwahl der AFK-Wache mit Zeitgedaechtnis, ohne Unity: wie lange war das
	/// aktuelle Ziel schon nicht mehr zu sehen? Reicht an <see cref="AfkVorrang"/> weiter.
	/// </summary>
	internal sealed class VerlustZaehler
	{
		private float _verloren;

		/// <summary>Sekunden am Stueck ohne Schussbahn zum aktuellen Ziel.</summary>
		internal float Verloren => _verloren;

		internal void Zuruecksetzen()
		{
			_verloren = 0f;
		}

		/// <summary>Pro Bild. Liefert, ob das Ziel noch gehalten wird (Verlust nicht laenger als haltezeit).</summary>
		internal bool Schritt(float dt, bool frei, float haltezeit)
		{
			_verloren = frei ? 0f : _verloren + Math.Max(0f, dt);
			return _verloren <= haltezeit;
		}
	}

	/// <summary>
	/// Entprellter Wahr/Falsch-Zustand je Ziel (z. B. "Zone frei"): schaltet erst
	/// um, wenn der rohe Wert die jeweilige Zeit am Stueck anders war. Beim ersten
	/// Bild eines Ziels gilt der rohe Wert sofort.
	/// </summary>
	internal sealed class Entprellung
	{
		private object _ziel;

		private bool _stand;

		private bool _roh;

		private float _seit;

		internal bool Stand => _stand;

		internal void Vergessen()
		{
			_ziel = null;
		}

		/// <param name="einschalt">Sekunden, die roh == wahr sein muss, bis wahr gilt.</param>
		/// <param name="ausschalt">Sekunden, die roh == falsch sein muss, bis falsch gilt.</param>
		internal bool Schritt(object ziel, float jetzt, bool roh, float einschalt, float ausschalt)
		{
			if (!ReferenceEquals(_ziel, ziel))
			{
				_ziel = ziel;
				_stand = roh;
				_roh = roh;
				_seit = jetzt;
				return _stand;
			}
			if (roh != _roh)
			{
				_roh = roh;
				_seit = jetzt;
			}
			if (roh != _stand && jetzt - _seit >= (roh ? einschalt : ausschalt))
			{
				_stand = roh;
			}
			return _stand;
		}
	}
}
