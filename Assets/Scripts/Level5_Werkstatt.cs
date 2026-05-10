using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Level 5 – Schuppen / Werkstatt.
/// State-Machine: Idle → WaitingBreadboard → SolvingBreadboard → WaitingPickup → Done
///
/// Kein Dialog. Spieler:
///   1) Geht zum Breadboard am Werktisch → [E] → Puzzle
///   2) Loest 3 Verbindungen (1-6, 3-5, 4-8) → Tuer zur Werkstatt oeffnet sich
///   3) Geht in die Werkstatt → [E] am Brenner → Level 6
/// </summary>
public class Level5_Werkstatt : MonoBehaviour
{
    public static Level5_Werkstatt Instance { get; private set; }

    [Header("Breadboard-Puzzle")]
    [SerializeField] private DustyWallSpot breadboardSpot;
    [SerializeField] private GameObject    breadboardPanel;
    [SerializeField] private GameObject    breadboardPrompt;
    [SerializeField] private Button[]      nodeButtons;      // 8 Buttons, Index 0-7 = Node 1-8

    [Header("Tuer zur Werkstatt")]
    [SerializeField] private GameObject doorObject;          // wird bei Erfolg deaktiviert

    [Header("Brenner-Pickup")]
    [SerializeField] private DustyWallSpot brennerSpot;
    [SerializeField] private GameObject    interactionPrompt;
    [SerializeField] private GameObject    pickupFlash;

    private enum State { Idle, WaitingBreadboard, SolvingBreadboard, WaitingPickup, Done }
    private State state = State.Idle;

    // Puzzle-Zustand
    private int    selectedNode     = -1;
    private bool[] nodeUsed         = new bool[8];
    private int    correctConnected = 0;

    // Loesung (0-basiert): Nodes 1-6, 3-5, 4-8
    private static readonly (int a, int b)[] Solution = { (0, 5), (2, 4), (3, 7) };

    static readonly Color ColIdle     = new Color(0.15f, 0.45f, 0.15f);
    static readonly Color ColSelected = new Color(0.90f, 0.75f, 0.10f);
    static readonly Color ColCorrect  = new Color(0.10f, 0.78f, 0.20f);
    static readonly Color ColWrong    = new Color(0.85f, 0.10f, 0.10f);

    void Awake() => Instance = this;

    void OnEnable()
    {
        state = State.Idle;
        if (breadboardPanel)   breadboardPanel.SetActive(false);
        if (breadboardPrompt)  breadboardPrompt.SetActive(false);
        if (interactionPrompt) interactionPrompt.SetActive(false);
        if (pickupFlash)       pickupFlash.SetActive(false);
        if (doorObject)        doorObject.SetActive(true);
    }

    void Start()
    {
        if (nodeButtons != null)
            for (int i = 0; i < nodeButtons.Length; i++)
            {
                int idx = i;
                if (nodeButtons[i]) nodeButtons[i].onClick.AddListener(() => OnNodeClicked(idx));
            }
        state = State.WaitingBreadboard;
    }

    void Update()
    {
        switch (state)
        {
            case State.WaitingBreadboard:
                bool nearBoard = breadboardSpot != null && breadboardSpot.PlayerNearby;
                if (breadboardPrompt) breadboardPrompt.SetActive(nearBoard);
                if (nearBoard && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    OpenBreadboard();
                break;

            case State.WaitingPickup:
                bool nearBrenner = brennerSpot != null && brennerSpot.PlayerNearby;
                if (interactionPrompt) interactionPrompt.SetActive(nearBrenner);
                if (nearBrenner && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    if (interactionPrompt) interactionPrompt.SetActive(false);
                    StartCoroutine(PickupBrenner());
                }
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Breadboard-Puzzle
    // -------------------------------------------------------------------------

    void OpenBreadboard()
    {
        state = State.SolvingBreadboard;
        if (breadboardPrompt) breadboardPrompt.SetActive(false);
        if (breadboardPanel)  breadboardPanel.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(null);
        ResetPuzzle();
    }

    void ResetPuzzle()
    {
        selectedNode     = -1;
        correctConnected = 0;
        nodeUsed         = new bool[8];
        if (nodeButtons == null) return;
        foreach (var btn in nodeButtons)
            if (btn) btn.GetComponent<Image>().color = ColIdle;
    }

    void OnNodeClicked(int idx)
    {
        if (state != State.SolvingBreadboard) return;
        if (nodeUsed[idx]) return;

        if (selectedNode == -1)
        {
            selectedNode = idx;
            if (nodeButtons[idx]) nodeButtons[idx].GetComponent<Image>().color = ColSelected;
        }
        else
        {
            int a = selectedNode;
            int b = idx;
            selectedNode = -1;

            if (IsCorrectPair(a, b))
            {
                nodeUsed[a] = nodeUsed[b] = true;
                if (nodeButtons[a]) nodeButtons[a].GetComponent<Image>().color = ColCorrect;
                if (nodeButtons[b]) nodeButtons[b].GetComponent<Image>().color = ColCorrect;
                correctConnected++;
                if (correctConnected >= Solution.Length)
                    StartCoroutine(BreadboardSolved());
            }
            else
            {
                StartCoroutine(WrongConnection(a, b));
            }
        }
    }

    bool IsCorrectPair(int a, int b)
    {
        foreach (var (sa, sb) in Solution)
            if ((a == sa && b == sb) || (a == sb && b == sa)) return true;
        return false;
    }

    IEnumerator WrongConnection(int a, int b)
    {
        if (nodeButtons[a]) nodeButtons[a].GetComponent<Image>().color = ColWrong;
        if (nodeButtons[b]) nodeButtons[b].GetComponent<Image>().color = ColWrong;
        yield return new WaitForSeconds(0.55f);
        if (!nodeUsed[a] && nodeButtons[a]) nodeButtons[a].GetComponent<Image>().color = ColIdle;
        if (!nodeUsed[b] && nodeButtons[b]) nodeButtons[b].GetComponent<Image>().color = ColIdle;
    }

    IEnumerator BreadboardSolved()
    {
        yield return new WaitForSeconds(0.45f);
        if (breadboardPanel) breadboardPanel.SetActive(false);
        if (doorObject)      doorObject.SetActive(false);   // Tuer oeffnet sich
        state = State.WaitingPickup;
    }

    // -------------------------------------------------------------------------
    // Brenner-Pickup
    // -------------------------------------------------------------------------

    IEnumerator PickupBrenner()
    {
        state = State.Done;
        if (pickupFlash) pickupFlash.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        if (pickupFlash) pickupFlash.SetActive(false);
        GameManager.Instance?.CompleteCurrentLevel();
    }
}
