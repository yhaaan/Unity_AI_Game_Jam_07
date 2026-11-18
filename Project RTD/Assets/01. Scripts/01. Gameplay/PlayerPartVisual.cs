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
        private ChimeraPartRig legRig, bodyRig, headRig;
        private PlayerMotor2D motor;
        private Rigidbody2D physicsBody;

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
            legRig.Pose(beat,run,air,ascent);
            bodyRig.Pose(beat,run,air,ascent);
            headRig.Pose(beat,run,air,ascent);
            var hip = ChimeraPartRig.Canvas(262,331);
            var neck = ChimeraPartRig.Canvas(185,250);
            var bodyT = bodyRig.Transform;
            bodyT.localScale = new Vector3(1+.03f*wave,1-.04f*wave,1);
            bodyT.localRotation = Quaternion.Euler(0,0,air ? -4*ascent : run ? 5+2*wave : 0);
            bodyT.localPosition = hip - bodyT.localRotation * Vector3.Scale(bodyT.localScale, hip);
            var headT = headRig.Transform;
            headT.localRotation = Quaternion.Euler(0,0,air ? -5*ascent : 3*wave);
            headT.position = bodyT.TransformPoint(neck) - headT.TransformVector(neck);
        }

        private void LateUpdate()
        {
            if (legRig != null) Animate();
            float move = input.Move.x;
            if (Mathf.Abs(move) < 0.01f) return;
            var scale = transform.localScale;
            scale.x = move > 0 ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
    }
}
