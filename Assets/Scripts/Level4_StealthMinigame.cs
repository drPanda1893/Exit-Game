using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Level 4 – Wärter-Minigame (Top-Down UI).
/// Spieler-Icon bewegt sich per WASD durch ein UI-Spielfeld.
/// Wärter-Blöcke patrouillieren. Kollision = Erwischt!
/// Ziel: "Schuppen"-Icon erreichen.
///
/// Alle Objekte sind UI-RectTransforms innerhalb eines Panel-Containers.
/// Pivot und Anchor aller beweglichen Elemente: Center/Center.
/// </summary>
public class Level4_StealthMinigame : MonoBehaviour
{
    [Header("Spieler")]
    [SerializeField] private RectTransform player;
    [SerializeField] private float playerSpeed = 160f;

    [Header("Wärter (beliebig viele)")]
    [SerializeField] private List<RectTransform> guards;
    [SerializeField] private float guardSpeed = 90f;

    [Header("Ziel")]
    [SerializeField] private RectTransform goal;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private RectTransform playArea; // Der Container, auf dem dieses Script liegt

    private Vector2[] guardDirections;
    private Vector2[] guardStartPositions;
    private bool active;

    void OnEnable()
    {
        active = false;
        statusText.text = string.Empty;
        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Big Yahu: Vorsicht! Wärter patrouillieren den Hof!",
            "Big Yahu: Nutze WASD, weiche den Wächtern aus und erreiche den Schuppen!"
        }, () => {
            ResetPositions();
            active = true;
        });
    }

    void Start()
    {
        if (playArea == null) playArea = GetComponent<RectTransform>();

        guardDirections = new Vector2[guards.Count];
        guardStartPositions = new Vector2[guards.Count];
        for (int i = 0; i < guards.Count; i++)
        {
            guardStartPositions[i] = guards[i].anchoredPosition;
            // Abwechselnd horizontal und vertikal patrouillieren
            guardDirections[i] = (i % 2 == 0) ? Vector2.right : Vector2.up;
        }
    }

    void Update()
    {
        if (!active) return;
        MovePlayer();
        MoveGuards();
        CheckCaught();
        CheckGoal();
    }

    void MovePlayer()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;

        player.anchoredPosition += new Vector2(h, v).normalized * playerSpeed * Time.deltaTime;
        Clamp(player);
    }

    void MoveGuards()
    {
        Rect area = playArea.rect;
        for (int i = 0; i < guards.Count; i++)
        {
            guards[i].anchoredPosition += guardDirections[i] * guardSpeed * Time.deltaTime;
            Vector2 pos = guards[i].anchoredPosition;

            // An Rändern umkehren
            if (pos.x < area.xMin || pos.x > area.xMax) guardDirections[i].x *= -1f;
            if (pos.y < area.yMin || pos.y > area.yMax) guardDirections[i].y *= -1f;
            guards[i].anchoredPosition = pos;
        }
    }

    void Clamp(RectTransform rt)
    {
        Rect area = playArea.rect;
        Vector2 pos = rt.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, area.xMin + rt.sizeDelta.x * 0.5f, area.xMax - rt.sizeDelta.x * 0.5f);
        pos.y = Mathf.Clamp(pos.y, area.yMin + rt.sizeDelta.y * 0.5f, area.yMax - rt.sizeDelta.y * 0.5f);
        rt.anchoredPosition = pos;
    }

    bool Overlaps(RectTransform a, RectTransform b)
    {
        Rect ra = new Rect(a.anchoredPosition - a.sizeDelta * 0.5f, a.sizeDelta);
        Rect rb = new Rect(b.anchoredPosition - b.sizeDelta * 0.5f, b.sizeDelta);
        return ra.Overlaps(rb);
    }

    void CheckCaught()
    {
        foreach (var guard in guards)
        {
            if (Overlaps(player, guard))
            {
                StartCoroutine(Caught());
                return;
            }
        }
    }

    void CheckGoal()
    {
        if (Overlaps(player, goal))
        {
            active = false;
            statusText.text = "✓ Schuppen erreicht!";
            StartCoroutine(DelayedComplete());
        }
    }

    IEnumerator Caught()
    {
        active = false;
        statusText.text = "✗ Erwischt! Neustart...";
        BigYahuDialogSystem.Instance.ShowDialog("Big Yahu: Autsch! Pass besser auf!");
        yield return new WaitForSeconds(1.8f);
        statusText.text = string.Empty;
        ResetPositions();
        active = true;
    }

    IEnumerator DelayedComplete()
    {
        yield return new WaitForSeconds(0.6f);
        GameManager.Instance.CompleteCurrentLevel();
    }

    void ResetPositions()
    {
        player.anchoredPosition = new Vector2(-300f, -180f);
        for (int i = 0; i < guards.Count; i++)
            guards[i].anchoredPosition = guardStartPositions[i];
    }
}
