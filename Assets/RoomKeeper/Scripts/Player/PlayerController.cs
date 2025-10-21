using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private static readonly int AnimWalking = Animator.StringToHash("Walk");
    private static readonly int AnimDirection = Animator.StringToHash("Direction");
    
    [Header("Player Settings")]
    [Min(0.1f), SerializeField] private float speed = 1f;

    private Animator _animator;
    private Rigidbody2D _rigidbody;
    private Vector2 _playerVelocity;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        _playerVelocity = Vector2.zero;

        if (Gamepad.current == null) return;
        _playerVelocity = Gamepad.current.leftStick.ReadValue();
        
        if (_playerVelocity.magnitude < 0.1f)
        {
            _animator.SetBool(AnimWalking, false);
            return;
        }
        
        float angle = Mathf.Atan2(_playerVelocity.y, _playerVelocity.x) * Mathf.Rad2Deg;
        int direction = 0;
        
        if (angle >= 45f && angle < 135f)
            direction = 2;
        else if (angle >= -135f && angle < -45f)
            direction = 0;
        else if (angle >= -45f && angle < 45f)
            direction = 1;
        else
            direction = 3;
        
        _animator.SetBool(AnimWalking, true);
        _animator.SetInteger(AnimDirection, direction);
    }

    private void FixedUpdate()
    {
        Vector2 velocity = _playerVelocity.normalized * speed;
        _rigidbody.MovePosition(_rigidbody.position + velocity *  Time.fixedDeltaTime);
    }
}
