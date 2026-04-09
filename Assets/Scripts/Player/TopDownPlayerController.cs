using UnityEngine;

namespace TurnOnTheBass
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class TopDownPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 4.5f;

        private Rigidbody2D body;
        private Vector2 moveInput;
        private bool movementEnabled = true;

        public bool MovementEnabled
        {
            get => movementEnabled;
            set
            {
                movementEnabled = value;
                if (!movementEnabled && body != null)
                {
                    body.linearVelocity = Vector2.zero;
                }
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        private void Update()
        {
            moveInput = MovementEnabled ? InputRouter.GetMovement() : Vector2.zero;
        }

        private void FixedUpdate()
        {
            body.linearVelocity = moveInput * moveSpeed;
        }
    }
}
