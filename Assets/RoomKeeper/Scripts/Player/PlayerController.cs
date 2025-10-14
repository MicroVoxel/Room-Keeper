using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Player Settings")]
    [Min(0.1f), SerializeField] private float speed = 1f;

    private Rigidbody2D _rigidbody;
    private Vector2 _playerVelocity;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        _playerVelocity = Vector2.zero;

        if (Gamepad.current == null) return;
        _playerVelocity = Gamepad.current.leftStick.ReadValue();
    }

    private void FixedUpdate()
    {
        Vector2 velocity = _playerVelocity.normalized * speed;
        _rigidbody.MovePosition(_rigidbody.position + velocity *  Time.fixedDeltaTime);
    }
}
