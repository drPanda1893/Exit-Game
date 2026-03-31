using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Level 3 – Die Bibel.
/// Spieler muss die Farbsequenz Rot → Blau → Gelb → Grün eingeben.
/// 4 farbige Buttons, 4 Indikatoren zeigen den bisherigen Fortschritt.
/// </summary>
public class Level3_ColorCode : MonoBehaviour
{
    [Header("Farb-Buttons")]
    [SerializeField] private Button redButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button yellowButton;

    [Header("Sequenz-Anzeige (4 Indikatoren)")]
    [SerializeField] private Image[] sequenceIndicators;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    // Korrekte Sequenz
    private static readonly string[] Solution = { "Red", "Blue", "Yellow", "Green" };
    private static readonly Color[] SolutionColors =
    {
        Color.red,
        new Color(0.2f, 0.4f, 1f),
        Color.yellow,
        Color.green
    };

    private readonly List<string> playerInput = new();
    private bool locked;

    void OnEnable()
    {
        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Big Yahu: Die Bibel verbirgt einen Farbcode!",
            "Big Yahu: Rot, Blau, Gelb, Grün – genau in dieser Reihenfolge!"
        });
    }

    void Start()
    {
        redButton.onClick.AddListener(()    => Press("Red",    Color.red));
        blueButton.onClick.AddListener(()   => Press("Blue",   new Color(0.2f, 0.4f, 1f)));
        yellowButton.onClick.AddListener(() => Press("Yellow", Color.yellow));
        greenButton.onClick.AddListener(()  => Press("Green",  Color.green));

        ResetIndicators();
    }

    void Press(string color, Color uiColor)
    {
        if (locked || playerInput.Count >= 4) return;

        playerInput.Add(color);
        int idx = playerInput.Count - 1;
        if (sequenceIndicators != null && idx < sequenceIndicators.Length)
            sequenceIndicators[idx].color = uiColor;

        if (playerInput.Count == 4)
            StartCoroutine(Evaluate());
    }

    IEnumerator Evaluate()
    {
        locked = true;
        yield return new WaitForSeconds(0.4f);

        bool correct = true;
        for (int i = 0; i < Solution.Length; i++)
            if (playerInput[i] != Solution[i]) { correct = false; break; }

        if (correct)
        {
            feedbackText.text = "✓ Korrekte Sequenz!";
            yield return new WaitForSeconds(0.9f);
            GameManager.Instance.CompleteCurrentLevel();
        }
        else
        {
            feedbackText.text = "✗ Falsche Reihenfolge!";
            yield return new WaitForSeconds(1.1f);
            playerInput.Clear();
            ResetIndicators();
            feedbackText.text = string.Empty;
            locked = false;
        }
    }

    void ResetIndicators()
    {
        if (sequenceIndicators == null) return;
        foreach (var ind in sequenceIndicators)
            ind.color = new Color(0.2f, 0.2f, 0.2f);
    }
}
