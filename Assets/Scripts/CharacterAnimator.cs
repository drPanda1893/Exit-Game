using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    private Transform idleModel;
    private Transform runningModel;
    private bool isRunning = false;

    void Start()
    {
        idleModel    = transform.Find("IdleModel");
        runningModel = transform.Find("RunningModel");

        if (idleModel == null || runningModel == null)
        {
            Debug.LogError("IdleModel oder RunningModel nicht gefunden!");
            return;
        }

        ShowIdle();
    }

    public void SetMoving(bool moving)
    {
        if (moving == isRunning) return;
        isRunning = moving;

        if (moving) ShowRunning();
        else ShowIdle();
    }

    private void ShowIdle()
    {
        idleModel?.gameObject.SetActive(true);
        runningModel?.gameObject.SetActive(false);
    }

    private void ShowRunning()
    {
        idleModel?.gameObject.SetActive(false);
        runningModel?.gameObject.SetActive(true);
    }
}
