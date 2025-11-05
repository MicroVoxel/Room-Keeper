using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    #region Animator Parameters
    private static readonly int StrIsWalk = Animator.StringToHash("IsWalk");
    private static readonly int StrXInput = Animator.StringToHash("XInput");
    private static readonly int StrYInput = Animator.StringToHash("YInput");
    #endregion

    #region Inspector
    [Header("Player Settings")]
    [SerializeField, Min(0.1f)] private float speed = 1f;
    [SerializeField] private InputActionAsset inputActions;
    #endregion

    #region Singleton
    public static PlayerController PlayerInstance;
    #endregion

    #region Fields
    private Animator _animator;
    private Rigidbody2D _rigidbody;
    private InputAction _moveAction;

    private Vector2 _moveInput;
    private bool _canMove = true;
    #endregion

    #region Unity Methods
    private void Awake()
    {
        if (PlayerInstance != null && PlayerInstance != this)
        {
            Destroy(gameObject);
            return;
        }
        PlayerInstance = this;

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
        if (!_canMove)
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
        if (!_canMove) return;

        Vector2 velocity = _moveInput.normalized * speed;
        _rigidbody.MovePosition(_rigidbody.position + velocity * Time.fixedDeltaTime);
    }
    #endregion

    #region Animation Handling
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
    #endregion

    #region Public Methods
    public void SetMovement(bool active)
    {
        _canMove = active;

        if (!active)
        {
            // หยุดอนิเมชันและความเร็ว
            _moveInput = Vector2.zero;
            _animator.SetBool(StrIsWalk, false);
        }
    }
    #endregion
}
