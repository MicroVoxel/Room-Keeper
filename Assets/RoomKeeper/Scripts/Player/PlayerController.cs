using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    #region Animator Parameters
    private string StrIsWalk = "IsWalk";
    private string StrXInput = "XInput";
    private string StrYInput = "YInput";
    #endregion

    [Header("Player Settings")]
    [SerializeField, Min(0.1f)] private float speed = 1f;
    [SerializeField] private InputActionAsset inputActions;

    public static PlayerController playerInstance;

    private Animator _animator;
    private Rigidbody2D _rigidbody;
    private InputAction _moveAction;

    private Vector2 _moveInput;
    private bool canMove = true;

    private void Awake()
    {
        if (playerInstance == null)
        {
            playerInstance = this;
        }

        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();

        var map = inputActions.FindActionMap("Player");
        _moveAction = map.FindAction("Move");
    }

    private void OnEnable()
    {
        _moveAction.Enable();
    }

    private void OnDisable()
    {
        _moveAction.Disable();
    }

    private void Update()
    {
        if (!canMove)
        {
            _moveInput = Vector2.zero;
            HandleAnimation(Vector2.zero);
            return;
        }

        _moveInput = _moveAction.ReadValue<Vector2>();
        HandleAnimation(_moveInput);
    }

    private void FixedUpdate()
    {
        if (!canMove) return;

        Vector2 velocity = _moveInput.normalized * speed;
        _rigidbody.MovePosition(_rigidbody.position + velocity * Time.fixedDeltaTime);
    }

    private void HandleAnimation(Vector2 input)
    {
        if (input.magnitude < 0.1f)
        {
            _animator.SetBool(StrIsWalk, false);
            return;
        }

        _animator.SetBool(StrIsWalk, true);
        _animator.SetFloat(StrXInput, input.x);
        _animator.SetFloat(StrYInput, input.y);
    }

    public void SetMovement(bool active)
    {
        canMove = active;

        if (!active)
        {
            // หยุดอนิเมชันและความเร็ว
            _moveInput = Vector2.zero;
            _animator.SetBool(StrIsWalk, false);
        }
    }
}
