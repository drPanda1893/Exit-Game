using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;

    private CharacterAnimator characterAnimator;
    private Rigidbody rb;
    private Vector3 moveDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        characterAnimator = GetComponent<CharacterAnimator>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f, v = 0f;
        if (keyboard.leftArrowKey.isPressed  || keyboard.aKey.isPressed) h = -1f;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) h =  1f;
        if (keyboard.upArrowKey.isPressed    || keyboard.wKey.isPressed) v =  1f;
        if (keyboard.downArrowKey.isPressed  || keyboard.sKey.isPressed) v = -1f;

        moveDirection = new Vector3(h, 0f, v);
        bool isMoving = moveDirection.sqrMagnitude > 0f;

        if (isMoving)
        {
            moveDirection.Normalize();
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDirection),
                20f * Time.deltaTime);
        }

        characterAnimator?.SetMoving(isMoving);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (moveDirection.sqrMagnitude > 0f)
        {
            rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);
        }
        else
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
