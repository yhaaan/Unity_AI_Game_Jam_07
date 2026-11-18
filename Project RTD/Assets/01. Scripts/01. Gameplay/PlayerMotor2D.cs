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
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0f)] private float maxFallSpeed = 20f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.65f;

        [Header("Flotation")]
        [SerializeField, Range(0.2f, 0.8f)] private float submergedFraction = 0.5f;
        [SerializeField, Range(0f, 0.2f)] private float bobHeightFraction = 0.10f;
        [SerializeField, Min(0.5f)] private float bobPeriod = 2.2f;
        private float bobPhase;
        private bool leavingWater;
        public bool IsFloating { get; private set; }

        private readonly List<RaycastHit2D> groundHits = new(8);

        private Rigidbody2D body;
        private Collider2D playerCollider;
        private PlayerPartLoadout loadout;
        private ContactFilter2D waterFilter;
        private readonly List<Collider2D> waters = new(2);
        private PlayerInputReader input;
        private ContactFilter2D groundFilter;
        private float coyoteTimer;
        private float jumpBufferTimer;

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
            playerCollider = GetComponent<Collider2D>();
            loadout = GetComponent<PlayerPartLoadout>();
            waterFilter.SetLayerMask(LayerMask.GetMask("Water"));
            waterFilter.useTriggers = true;
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
        }

        private void FixedUpdate()
        {
            IsFloating = ApplyFlotation();
            IsGrounded = CheckGrounded();
            if (!IsFloating) bobPhase = 0f;

            if (IsGrounded || IsFloating)
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

        private bool ApplyFlotation()
        {
            if (loadout == null || loadout.Current?.HasEffect(PartEffect.Float) != true)
                return false;

            int count = playerCollider.Overlap(waterFilter, waters);
            float surface = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (waters[i].isTrigger) surface = Mathf.Max(surface, waters[i].bounds.max.y);
            if (float.IsNegativeInfinity(surface)) { leavingWater = false; return false; }
            Vector2 velocity = body.linearVelocity;
            if (leavingWater && velocity.y > 0f) return false;
            leavingWater = false;

            // Follow a moving waterline with a damped spring instead of snapping
            // to a height. Use collider proportions so different sizes sit alike.
            Bounds bounds = playerCollider.bounds;
            float dt = Time.fixedDeltaTime;
            float omega = 2f * Mathf.PI / Mathf.Max(0.5f, bobPeriod);
            bobPhase = Mathf.Repeat(bobPhase + omega * dt, 2f * Mathf.PI);
            float amplitude = bounds.size.y * bobHeightFraction;
            float targetFeet = surface - bounds.size.y * submergedFraction + amplitude * Mathf.Sin(bobPhase);
            float waveSpeed = amplitude * omega * Mathf.Cos(bobPhase);
            float acceleration = 36f * (targetFeet - bounds.min.y) + 11f * (waveSpeed - velocity.y);
            velocity.y += (acceleration - Physics2D.gravity.y * body.gravityScale) * dt;
            body.linearVelocity = velocity;
            return true;
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

                leavingWater = IsFloating;
                IsFloating = false;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                IsGrounded = false;
            }
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
