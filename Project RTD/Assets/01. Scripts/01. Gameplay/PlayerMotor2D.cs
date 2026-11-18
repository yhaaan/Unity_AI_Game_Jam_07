using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [Header("Horizontal Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float groundAcceleration = 55f;
        [SerializeField, Min(0f)] private float groundDeceleration = 70f;
        [SerializeField, Min(0f)] private float airAcceleration = 35f;

        [Header("Jump")]
        [SerializeField, Min(0f)] private float jumpVelocity = 10f;
        [SerializeField, Range(0f, 1f)] private float jumpReleaseMultiplier = 0.45f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0f)] private float maxFallSpeed = 20f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.65f;

        private readonly List<RaycastHit2D> groundHits = new(8);

        private Rigidbody2D body;
        private PlayerInputReader input;
        private ContactFilter2D groundFilter;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private bool jumpReleaseQueued;

        public bool IsGrounded { get; private set; }

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public float JumpVelocity
        {
            get => jumpVelocity;
            set => jumpVelocity = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputReader>();
            ConfigureGroundFilter();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            jumpVelocity = Mathf.Max(0f, jumpVelocity);
            maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
            groundCheckDistance = Mathf.Max(0.001f, groundCheckDistance);
            ConfigureGroundFilter();
        }

        private void Update()
        {
            if (input.ConsumeJumpPressed())
            {
                jumpBufferTimer = jumpBufferTime;
            }

            if (input.ConsumeJumpReleased())
            {
                jumpReleaseQueued = true;
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();

            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
            }

            ApplyHorizontalMovement();
            ApplyJump();
            LimitFallSpeed();

            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.fixedDeltaTime);
        }

        private void ApplyHorizontalMovement()
        {
            float horizontalInput = input.Move.x;
            float targetSpeed = horizontalInput * moveSpeed;
            float acceleration = IsGrounded
                ? (Mathf.Abs(horizontalInput) > 0.01f ? groundAcceleration : groundDeceleration)
                : airAcceleration;

            Vector2 velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
        }

        private void ApplyJump()
        {
            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                Vector2 velocity = body.linearVelocity;
                velocity.y = jumpVelocity;
                body.linearVelocity = velocity;

                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                IsGrounded = false;
            }

            if (!jumpReleaseQueued)
            {
                return;
            }

            jumpReleaseQueued = false;

            if (body.linearVelocity.y <= 0f)
            {
                return;
            }

            Vector2 releasedVelocity = body.linearVelocity;
            releasedVelocity.y *= jumpReleaseMultiplier;
            body.linearVelocity = releasedVelocity;
        }

        private void LimitFallSpeed()
        {
            Vector2 velocity = body.linearVelocity;

            if (velocity.y >= -maxFallSpeed)
            {
                return;
            }

            velocity.y = -maxFallSpeed;
            body.linearVelocity = velocity;
        }

        private bool CheckGrounded()
        {
            ConfigureGroundFilter();
            groundHits.Clear();

            int hitCount = body.Cast(Vector2.down, groundFilter, groundHits, groundCheckDistance);

            for (int index = 0; index < hitCount; index++)
            {
                if (groundHits[index].normal.y >= minimumGroundNormalY)
                {
                    return true;
                }
            }

            return false;
        }

        private void ConfigureGroundFilter()
        {
            groundFilter.useLayerMask = true;
            groundFilter.SetLayerMask(groundLayers);
            groundFilter.useTriggers = false;
        }
    }
}
