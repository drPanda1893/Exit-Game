using UnityEngine;
using System;

/// <summary>
/// Level 3 – Bibliothek: Phasen-Controller.
/// Orchestriert drei Phasen:
///   A) Buch-Auswahl  (Level3_BookSelection)
///   B) Farbcode       (Level3_ColorCode)
///   C) Generator      (TBD)
///
/// Jede Phase ist ein eigenes Child-Panel im Level-3-UI.
/// Der Controller aktiviert/deaktiviert Panels sequentiell.
/// </summary>
public class Level3_Controller : MonoBehaviour
{
    // ── Phase Panels ──────────────────────────────────────────
    [Header("Phasen-Panels")]
    [SerializeField] private GameObject phaseA_BookSelection;
    [SerializeField] private GameObject phaseB_ColorCode;
    [SerializeField] private GameObject phaseC_Generator;

    // ── Events (für lose Kopplung) ────────────────────────────
    /// <summary>Wird gefeuert, wenn Spieler die Bibel korrekt öffnet.</summary>
    public static event Action OnBookSolved;

    /// <summary>Wird gefeuert, wenn die Farbsequenz korrekt eingegeben wurde.</summary>
    public static event Action OnColorCodeSolved;

    // ── State ─────────────────────────────────────────────────
    private enum Phase { BookSelection, ColorCode, Generator }
    private Phase currentPhase;

    // ──────────────────────────────────────────────────────────
    void OnEnable()
    {
        OnBookSolved    += AdvanceToColorCode;
        OnColorCodeSolved += AdvanceToGenerator;

        SetPhase(Phase.BookSelection);
    }

    void OnDisable()
    {
        OnBookSolved    -= AdvanceToColorCode;
        OnColorCodeSolved -= AdvanceToGenerator;
    }

    // ── Phase Transitions ─────────────────────────────────────
    void SetPhase(Phase phase)
    {
        currentPhase = phase;

        phaseA_BookSelection.SetActive(phase == Phase.BookSelection);
        phaseB_ColorCode.SetActive(phase == Phase.ColorCode);

        if (phaseC_Generator != null)
            phaseC_Generator.SetActive(phase == Phase.Generator);
    }

    void AdvanceToColorCode()
    {
        SetPhase(Phase.ColorCode);
    }

    void AdvanceToGenerator()
    {
        // Phase C noch nicht implementiert → Level direkt abschließen.
        // Sobald Generator-Puzzle existiert: SetPhase(Phase.Generator);
        if (phaseC_Generator != null)
        {
            SetPhase(Phase.Generator);
        }
        else
        {
            GameManager.Instance.CompleteCurrentLevel();
        }
    }

    // ── Statische Helfer für Child-Skripte ────────────────────
    public static void NotifyBookSolved()   => OnBookSolved?.Invoke();
    public static void NotifyColorSolved()  => OnColorCodeSolved?.Invoke();
}
