using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // All three renderers share this transform: offsets and facing must never split the rig.
    public sealed class PlayerPartVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer legs, body, head;
        private PlayerPartLoadout loadout;
        private PlayerInputReader input;
        private CapsuleCollider2D capsule;
        [SerializeField] private ChimeraLayerLibrary animations;
        internal ChimeraLayerLibrary PartArt => animations;
        private ChimeraPartRig legRig, bodyRig, headRig;
        private PlayerMotor2D motor;
        private Rigidbody2D physicsBody;
        private RunDustTrail dust;
        private const float ScareDuration = .75f;
        private float scaredAt = float.NegativeInfinity;

        private CardTurn turn;

        private void Awake() => turn = new CardTurn(transform);

        public void PlayScare()
        {
            if (!isActiveAndEnabled || legRig == null || !float.IsNegativeInfinity(scaredAt)) return;
            ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Scare);
            scaredAt = Time.time;
            motor.SetScareLocked(true);
        }

        private void EndScare()
        {
            scaredAt = float.NegativeInfinity;
            if (motor) motor.SetScareLocked(false);
        }

        private void OnDestroy() => dust?.Dispose();

        private void OnEnable()
        {
            loadout = GetComponentInParent<PlayerPartLoadout>();
            input = loadout.GetComponent<PlayerInputReader>();
            capsule = loadout.GetComponent<CapsuleCollider2D>();
            motor = loadout.GetComponent<PlayerMotor2D>();
            physicsBody = loadout.GetComponent<Rigidbody2D>();
            loadout.Changed += Apply;
            if (loadout.Current != null) Apply(loadout.Current);
        }

        private void OnDisable()
        {
            EndScare();
            dust?.Clear();
            loadout.Changed -= Apply;
            ResetRig();
        }

        private void ResetRig()
        {
            legRig?.Dispose(); bodyRig?.Dispose(); headRig?.Dispose();
            legRig = bodyRig = headRig = null;
            legs.enabled = body.enabled = head.enabled = true;
        }

        private void Apply(PlayerPartBuild build)
        {
            head.sprite = build.Head.Sprite;
            body.sprite = build.Body.Sprite;
            legs.sprite = build.Legs.Sprite;
            ResetRig();
            if (!legs.sprite) return;
            var sprite = legs.sprite;
            int groundY = build.Legs.GroundYpx;
            var l = animations ? animations.Find(build.Legs) : null;
            var b = animations ? animations.Find(build.Body) : null;
            var h = animations ? animations.Find(build.Head) : null;
            if (l != null && b != null && h != null)
            {
                int order = legs.sortingOrder;
                legRig = new ChimeraPartRig(legs,l,order);
                bodyRig = new ChimeraPartRig(body,b,order + l.layers.Length);
                headRig = new ChimeraPartRig(head,h,order + l.layers.Length + b.layers.Length);
                legs.enabled = body.enabled = head.enabled = false;
                groundY = l.ground_y_px;
            }
            float belowPivot = (groundY - sprite.rect.height + sprite.pivot.y) / sprite.pixelsPerUnit;
            var position = transform.localPosition;
            position.y = capsule.offset.y - capsule.size.y * 0.5f + belowPivot * transform.localScale.y;
            transform.localPosition = position;
        }

        // Chimera's 112 BPM Idle/Run motion, with the neck following the moving torso.
        // In the air use velocity rather than the demo's repeating visual jump arc.
        private void Animate()
        {
            var velocity = physicsBody.linearVelocity;
            bool air = !motor.IsGrounded && !motor.IsFloating;
            bool run = motor.IsGrounded && !motor.IsFloating && Mathf.Abs(velocity.x) > .1f;
            float beat = Time.time * (112f/60f);
            float wave = Mathf.Sin(2*Mathf.PI*beat*(run ? 2 : .5f));
            float ascent = Mathf.Clamp(velocity.y / Mathf.Max(.01f,motor.JumpVelocity),-1,1);
            if (run && dust == null) dust = new RunDustTrail(loadout.transform,legs);
            dust?.Tick(run,Mathf.FloorToInt(beat*4),capsule.bounds,velocity.x);

            // Chimera Scare: crouch, spring up, shiver and settle. Visual only.
            float p = (Time.time-scaredAt)/ScareDuration;
            float crouch = 0, hold = 0, shake = 0, land = 0;
            if (p >= 0 && p < 1)
            {
                crouch = p < .12f ? Mathf.Sin(Mathf.PI*.5f*p/.12f) : 0;
                float u = Mathf.Clamp01((p-.12f)/.12f)-1;
                hold = (1+3.4f*u*u*u+2.4f*u*u)*Mathf.Clamp01((.85f-p)/.12f);
                shake = Mathf.Sin(p*110)*hold;
                land = p > .85f ? Mathf.Exp(-(p-.85f)*14)*Mathf.Cos((p-.85f)*30) : 0;
            }
            var lift = new Vector3(.03f*shake,.26f*hold,0);
            legRig.Transform.localPosition = lift;
            legRig.Pose(beat,run,air,ascent,hold);
            bodyRig.Pose(beat,run,air,ascent,hold);
            headRig.Pose(beat,run,air,ascent,hold);
            var hip = ChimeraPartRig.Canvas(262,331);
            var neck = ChimeraPartRig.Canvas(185,250);
            var bodyT = bodyRig.Transform;
            float stretch = .22f*hold - .14f*crouch - .12f*land;
            bodyT.localScale = new Vector3(1+.03f*wave-.5f*stretch,1-.04f*wave+stretch,1);
            bodyT.localRotation = Quaternion.Euler(0,0,air ? -4*ascent : run ? 5+2*wave : 0);
            bodyT.localPosition = lift + hip - bodyT.localRotation * Vector3.Scale(bodyT.localScale, hip);
            var headT = headRig.Transform;
            headT.localRotation = Quaternion.Euler(0,0,air ? -5*ascent : 3*wave);
            headT.position = bodyT.TransformPoint(neck) - headT.TransformVector(neck);
        }

        private void LateUpdate()
        {
            if (legRig != null) Animate();
            if (!float.IsNegativeInfinity(scaredAt))
            {
                // Animate has now applied this frame's pose. Unlock only once it is neutral again.
                if (legRig != null && (Time.time - scaredAt) / ScareDuration < 1f)
                {
                    UpdateFacing(0f);
                    return;
                }
                EndScare();
            }
            UpdateFacing(input.Move.x);
        }

        private void UpdateFacing(float move)
        {
            turn.Tick(move, Time.deltaTime);

        }
    }
}
