using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Level 5 – Werkstatt.
/// State-Machine: Idle → Dialog → WaitingPickup → Done
///
/// Spieler betritt die Werkstatt, bekommt einen Dialog,
/// findet den Bunsenbrenner auf dem Werktisch, drückt [E] → Level 6.
/// </summary>
public class Level5_Werkstatt : MonoBehaviour
{
    public static Level5_Werkstatt Instance { get; private set; }

    [Header("3D Trigger")]
    [SerializeField] private DustyWallSpot brennerSpot;

    [Header("UI")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private GameObject pickupFlash;

    private enum State { Idle, WaitingPickup, Done }
    private State state = State.Idle;

    void Awake() => Instance = this;

    void OnEnable()
    {
        state = State.Idle;
        if (interactionPrompt) interactionPrompt.SetActive(false);
        if (pickupFlash)       pickupFlash.SetActive(false);
    }

    void Start() => StartDialog();

    void Update()
    {
        if (state != State.WaitingPickup) return;

        if (interactionPrompt)
            interactionPrompt.SetActive(brennerSpot != null && brennerSpot.PlayerNearby);

        if (brennerSpot == null || !brennerSpot.PlayerNearby) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (interactionPrompt) interactionPrompt.SetActive(false);
            StartCoroutine(PickupBrenner());
        }
    }

    void StartDialog()
    {
        if (BigYahuDialogSystem.Instance == null) { state = State.WaitingPickup; return; }

        BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Joshi: Das hier ist die Werkstatt. Riechst du das? Oel und Metall.",
            "Joshi: Ich hab mal gehoert, dass jemand den Bunsenbrenner hier zurueckgelassen hat.",
            "Joshi: Mit dem koennen wir das Schloss am Tor schmelzen. Such ihn!"
        }, () =>
        {
            BigYahuDialogSystem.Instance.ResetSpeaker();
            state = State.WaitingPickup;
        });
    }

    IEnumerator PickupBrenner()
    {
        state = State.Done;

        if (pickupFlash) pickupFlash.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        if (pickupFlash) pickupFlash.SetActive(false);

        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
            BigYahuDialogSystem.Instance.ShowDialog(
                "Joshi: Perfekt! Mit dem Brenner koennen wir das Tor aufschmelzen. Los zum Ausgang!",
                () =>
                {
                    BigYahuDialogSystem.Instance.ResetSpeaker();
                    GameManager.Instance?.CompleteCurrentLevel();
                });
        }
        else
        {
            GameManager.Instance?.CompleteCurrentLevel();
        }
    }
}
