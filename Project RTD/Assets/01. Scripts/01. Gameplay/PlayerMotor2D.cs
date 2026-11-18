using System.Collections.Generic;
using ProjectRTD.Core;
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

        private bool IsScareLocked { get; set; }

        internal void SetScareLocked(bool locked)
        {
            if (IsScareLocked == locked) return;
            IsScareLocked = locked;
            if (!locked) return;
            body.linearVelocityX = 0f;
            jumpBufferTimer = 0f;
            input.ConsumeJumpPressed();
        }


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
        [Header("Glide")]
        [SerializeField, Min(0.01f)] private float glideDescentSpeed = 1.5f;
        private const float GlideDuration = 2f;
        private float glideRemaining = GlideDuration;
        internal bool IsGliding { get; private set; }
        private float airborneSeconds;
        private TravelCadence gait;
        private bool rightFoot, jumpHeldLastTick;

        private void OnDisable()
        {
            StopClimbing();
            gait = default;
            jumpHeldLastTick = false;
        }
        private float bobPhase;
        private bool leavingWater;
        public bool IsFloating { get; private set; }
        public bool IsClimbing => ladder;
        internal Collider2D GroundCollider { get; private set; }

        private readonly List<RaycastHit2D> groundHits = new(8);

        private Rigidbody2D body;
        private Collider2D playerCollider;
        private PlayerPartLoadout loadout;
        private ContactFilter2D waterFilter;
        private readonly List<Collider2D> waters = new(2);
        private readonly List<Collider2D> ladderOverlaps = new(4);
        private StageLadder2D ladder;
        private float gravityBeforeClimbing;
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
            if (input.ConsumeJumpPressed() && !IsScareLocked)
            {
                jumpBufferTimer = jumpBufferTime;
            }
        }

        private void FixedUpdate()
        {
            IsGliding = false;
            var stage = StageRuntime.Instance;
            if (stage && stage.Player == transform && (stage.IsRespawning || stage.IsAssembling))
            {
                body.linearVelocityX = 0f;
                jumpBufferTimer = 0f;
                return;
            }

            if (UpdateClimbing()) return;
            bool jumpHeld = input.JumpHeld;
            bool freshHold = jumpHeld && !jumpHeldLastTick;
            jumpHeldLastTick = jumpHeld;
            bool wasFloating = IsFloating;
            bool wasGrounded = IsGrounded;
            IsFloating = ApplyFlotation();
            IsGrounded = CheckGrounded();
            if (IsFloating && !wasFloating) SoundManager.Play(Sfx.Splash);
            else if (IsGrounded && !wasGrounded && airborneSeconds > .08f && !IsFloating)
                SoundManager.Play(Sfx.Land, Mathf.Lerp(.48f, .92f, Mathf.InverseLerp(.08f, .8f, airborneSeconds)));
            airborneSeconds = IsGrounded || IsFloating ? 0f : airborneSeconds + Time.fixedDeltaTime;
            if (!IsFloating) bobPhase = 0f;

            if (IsGrounded || IsFloating)
            {
                coyoteTimer = coyoteTime;
                glideRemaining = GlideDuration;
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
            }

            if (IsScareLocked)
            {
                body.linearVelocityX = 0f;
                gait = default;
                LimitFallSpeed();
                return;
            }

            ApplyHorizontalMovement();
            ApplyJump();
            IsGliding = !IsFloating && !IsGrounded && body.linearVelocityY <= 0.0001f && jumpHeld &&
                SpendGlideTime(loadout?.Current?.HasEffect(PartEffect.Glide) == true, ref glideRemaining, Time.fixedDeltaTime);
            // A held jump can transition to glide silently. Only a new press on descent cues the wings.
            if (IsGliding && freshHold) SoundManager.Play(Sfx.Glide, .32f);
            UpdateTravelSound(wasGrounded, wasFloating);
            if (IsGliding)
            {

                body.linearVelocityY = -glideDescentSpeed;
                body.AddForce(Vector2.up * (-Physics2D.gravity.y * body.gravityScale * body.mass));
            }
            LimitFallSpeed();

            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.fixedDeltaTime);
        }

        private bool UpdateClimbing()
        {
            const float climbSpeed = 4.75f;
            Vector2 move = input.Move;
            float feetOffset = playerCollider.bounds.min.y - transform.position.y;
            float feet = body.position.y + feetOffset;
            if (!ladder)
            {
                if (IsScareLocked || Mathf.Abs(move.y) < .25f) return false;
                playerCollider.Overlap(new ContactFilter2D { useTriggers = true }, ladderOverlaps);
                foreach (Collider2D overlap in ladderOverlaps)
                {
                    if (!overlap.TryGetComponent(out StageLadder2D candidate) ||
                        !candidate.CanGrab(overlap.bounds, body.position.x, feet))
                        continue;
                    ladder = candidate;
                    gravityBeforeClimbing = body.gravityScale;
                    body.gravityScale = 0f;
                    ladder.SetClimbing(playerCollider, true);
                    break;
                }
                if (!ladder) return false;
            }

            float step = climbSpeed * Time.fixedDeltaTime;
            if (Mathf.Abs(move.x) >= .25f && Mathf.Abs(move.y) < .25f &&
                ladder.TryGetLanding(feet, .25f, out float landing))
            {
                body.position = new Vector2(ladder.CenterX, landing - feetOffset + .01f);
                body.linearVelocity = Vector2.zero;
                StopClimbing();
                jumpBufferTimer = 0f;
                return false;
            }

            float nextFeet = Mathf.Clamp(feet + move.y * step, ladder.LowestSurface, ladder.HighestSurface);
            body.linearVelocity = Vector2.zero;
            body.MovePosition(new Vector2(ladder.CenterX, nextFeet - feetOffset));
            IsGrounded = false;
            GroundCollider = null;
            IsFloating = false;
            airborneSeconds = 0f;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            gait = default;
            return true;
        }

        private void StopClimbing()
        {
            if (!ladder) return;
            ladder.SetClimbing(playerCollider, false);
            body.gravityScale = gravityBeforeClimbing;
            ladder = null;
        }

        internal void ResetAfterTeleport()
        {
            StopClimbing();
            IsGliding = IsGrounded = IsFloating = leavingWater = false;
            GroundCollider = null;
            coyoteTimer = jumpBufferTimer = airborneSeconds = bobPhase = 0f;
            glideRemaining = GlideDuration;
            input.ConsumeJumpPressed();
            gait = default;
        }

        private void UpdateTravelSound(bool wasGrounded, bool wasFloating)
        {
            // Do not count the contact that already produced Land/Splash, or a takeoff.
            bool movingOnSurface = IsFloating ? wasFloating : IsGrounded && wasGrounded && !wasFloating;
            bool walking = movingOnSurface && Mathf.Abs(input.Move.x) > .01f;
            if (!gait.Tick(body.position.x, walking, IsFloating, Mathf.Max(.5f, moveSpeed * Time.fixedDeltaTime * 2f))) return;
            if (IsFloating) { SoundManager.Play(Sfx.Paddle, .4f); return; }

            float weight = Mathf.InverseLerp(-6f, 6f, loadout?.Current?.Weight ?? 0);
            SoundManager.Play(rightFoot ? Sfx.StepRight : Sfx.StepLeft, Mathf.Lerp(.33f, .57f, weight));
            rightFoot = !rightFoot;
        }

        internal static bool SpendGlideTime(bool hasFly, ref float remaining, float step)
        {
            if (!hasFly || remaining + .00001f < step) return false;
            remaining = Mathf.Max(0f, remaining - step);
            return true;
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
                SoundManager.Play(Sfx.Jump);
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
            GroundCollider = null;
            ConfigureGroundFilter();
            groundHits.Clear();

            int hitCount = body.Cast(Vector2.down, groundFilter, groundHits, groundCheckDistance);

            for (int index = 0; index < hitCount; index++)
            {
                if (groundHits[index].normal.y >= minimumGroundNormalY)
                {
                    GroundCollider = groundHits[index].collider;
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
