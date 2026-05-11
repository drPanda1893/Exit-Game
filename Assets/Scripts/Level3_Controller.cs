using UnityEngine;
using System;

/// <summary>
/// Level 3 – Bibliothek: Phasen-Controller.
/// Orchestriert drei Phasen:
///   A) Buch-Auswahl  (Level3_BookSelection)
///   B) Farbcode       (Level3_ColorPuzzle)
///   C) Generator      (TBD)
///
/// Phase A übergibt die Farbsequenz via NotifyBookSolved(names, colors).
/// Der Controller leitet sie an Level3_ColorPuzzle.SetSolution() weiter.
/// </summary>
public class Level3_Controller : MonoBehaviour
{
    [Header("Phasen-Panels")]
    [SerializeField] private GameObject phaseA_BookSelection;
    [SerializeField] private GameObject phaseB_ColorCode;
    [SerializeField] private GameObject phaseC_Generator;

    // Trägt die Farbsequenz aus Phase A, damit Phase B sie nicht hardcoden muss
    public static event Action<string[], Color[]> OnBookSolved;
    public static event Action OnColorCodeSolved;

    private enum Phase { BookSelection, ColorCode, Generator }
    private Phase currentPhase;

    private Level3_ColorPuzzle colorPuzzleScript;

    void Awake()
    {
        if (phaseB_ColorCode != null)
            colorPuzzleScript = phaseB_ColorCode.GetComponent<Level3_ColorPuzzle>();
    }

    void OnEnable()
    {
        OnBookSolved      += AdvanceToColorCode;
        OnColorCodeSolved += AdvanceToGenerator;
        SetPhase(Phase.BookSelection);
    }

    void OnDisable()
    {
        OnBookSolved      -= AdvanceToColorCode;
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

    void AdvanceToColorCode(string[] names, Color[] colors)
    {
        colorPuzzleScript?.SetSolution(names, colors);
        SetPhase(Phase.ColorCode);
    }

    void AdvanceToGenerator()
    {
        if (phaseC_Generator != null)
            SetPhase(Phase.Generator);
        else
            GameManager.Instance.CompleteCurrentLevel();
    }

    // ── Statische API für Child-Skripte ───────────────────────

    /// <summary>Phase A abgeschlossen: Bibel gelesen, Lösung bekannt.</summary>
    public static void NotifyBookSolved(string[] names, Color[] colors) => OnBookSolved?.Invoke(names, colors);

    /// <summary>Phase B abgeschlossen: Farbsequenz korrekt eingegeben.</summary>
    public static void NotifyColorSolved() => OnColorCodeSolved?.Invoke();

    /// <summary>Alias für NotifyColorSolved – direkter Aufruf aus Level3_ColorPuzzle.</summary>
    public static void NextPhase() => OnColorCodeSolved?.Invoke();
}
