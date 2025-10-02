using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 3.5f;

    public InputActionAsset inputAction;

    private Rigidbody2D rb;
    private Vector2 playerVelocity;

    private InputAction moveAction;

    private void OnEnable()
    {
        inputAction.FindActionMap("Player").Enable();
    }

    private void OnDisable()
    {
        inputAction.FindActionMap("Player").Disable();
    }

    private void Awake()
    {
        moveAction = InputSystem.actions.FindAction("Move");

        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
  
    }

    void Update()
    {
        Move();
    }

    private void Move()
    {
        Vector2 _input = moveAction.ReadValue<Vector2>();
        rb.MovePosition(rb.position + _input * (speed * Time.deltaTime));
    }
}
