using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Tooltip("The speed at which the player moves.")]
    [SerializeField]
    private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    private void Awake()
    {
        // Get the Rigidbody2D component attached to this GameObject.
        rb = GetComponent<Rigidbody2D>();

        // Find the "Move" action from the default input actions.
        // Using "Player/Move" is more specific in case you have other "Move" actions in other maps.
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    private void OnEnable()
    {
        // Enable the move action when this component is enabled.
        moveAction.Enable();
    }

    private void OnDisable()
    {
        // Disable the move action when this component is disabled to prevent errors.
        moveAction.Disable();
    }

    private void Update()
    {
        // Read the input value from the "Move" action.
        // This returns a Vector2 with values from -1 to 1 for X and Y.
        moveInput = moveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // Apply the movement to the Rigidbody2D in FixedUpdate for smooth physics.
        // We multiply the normalized input by the move speed.
        rb.linearVelocity = moveInput * moveSpeed;
    }
}
