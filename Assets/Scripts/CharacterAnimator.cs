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
        if (runningModel) { runningModel.localPosition = Vector3.zero; runningModel.localRotation = Quaternion.identity; runningModel.gameObject.SetActive(false); }
        if (idleModel)    { idleModel.localPosition    = Vector3.zero; idleModel.localRotation    = Quaternion.identity; idleModel.gameObject.SetActive(true); }
    }

    private void ShowRunning()
    {
        if (idleModel)    { idleModel.localPosition    = Vector3.zero; idleModel.localRotation    = Quaternion.identity; idleModel.gameObject.SetActive(false); }
        if (runningModel) { runningModel.localPosition = Vector3.zero; runningModel.localRotation = Quaternion.identity; runningModel.gameObject.SetActive(true); }
    }
}
