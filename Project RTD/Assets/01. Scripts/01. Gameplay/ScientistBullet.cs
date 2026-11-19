using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // A scientist's tranquilliser shot: launches with a cartoon "boing" (stretch, squash, wobble), flies straight with a
    // pulsing glow, and pops on whatever it hits. Hitting the player sends them back to the stage start.
    [DisallowMultipleComponent]
    public sealed class ScientistBullet : MonoBehaviour
    {
        const float Speed = 6.5f, Life = 3f, Radius = 0.12f;
        const int Order = 60;

        static readonly Color Core = new(1f, 0.95f, 0.55f, 1f), GlowColor = new(1f, 0.55f, 0.15f, 1f),
            Ink = new(0.043f, 0.102f, 0.227f, 1f);
        static Sprite disc, soft;

        Vector2 velocityDir; float dir, born;
        Transform art;
        SpriteRenderer glow;
        bool popped;

        internal static void Fire(Vector3 muzzle, float direction) => Fire(muzzle, new Vector2(Mathf.Sign(direction), 0f));

        // Aimed shot in any direction (drones fire down at the player).
        internal static void Fire(Vector3 muzzle, Vector2 aim)
        {
            float direction = aim.x >= 0f ? 1f : -1f;
            if (!disc) disc = MakeDisc(false);
            if (!soft) soft = MakeDisc(true);
            var go = new GameObject("Scientist Bullet");
            go.transform.position = muzzle;
            var bullet = go.AddComponent<ScientistBullet>();
            bullet.dir = direction;
            bullet.velocityDir = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right * direction;
            bullet.Build();
            go.transform.rotation = Quaternion.FromToRotation(Vector3.right, bullet.velocityDir); // stretch along travel
            MuzzleFlash(muzzle, direction);
        }

        void Build()
        {
            born = Time.time;
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            var hit = gameObject.AddComponent<CircleCollider2D>();
            hit.isTrigger = true;
            hit.radius = Radius;

            art = new GameObject("Art").transform;
            art.SetParent(transform, false);
            // No outline: a small hot core inside two layers of soft glow.
            glow = Disc(art, soft, GlowColor, 0.95f, Order);
            Disc(art, soft, Core, 0.5f, Order + 1);
            Disc(art, disc, Core, 0.2f, Order + 2);
            Disc(art, disc, Color.white, 0.11f, Order + 3);
        }

        void Update()
        {
            if (popped) return;
            float t = Time.time - born;
            if (t > Life) { Pop(); return; }
            transform.position += (Vector3)(velocityDir * (Speed * Time.deltaTime));

            // Boing: shoot out stretched, squash back, then wobble down to round.
            float stretch = t < 0.08f ? Mathf.Lerp(0.3f, 1.7f, t / 0.08f)
                : 1f + 0.55f * Mathf.Exp(-(t - 0.08f) * 7f) * Mathf.Cos((t - 0.08f) * 26f);
            art.localScale = new Vector3(stretch, 1f / Mathf.Sqrt(Mathf.Max(0.3f, stretch)), 1f);
            float pulse = 0.85f + 0.25f * Mathf.Sin(t * 22f);
            glow.transform.localScale = Vector3.one * 0.95f * pulse;
            var c = glow.color; c.a = 0.5f + 0.25f * Mathf.Sin(t * 22f); glow.color = c;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (popped) return;
            if (other.attachedRigidbody && other.attachedRigidbody.TryGetComponent(out PlayerPartLoadout player))
            {
                var stage = StageRuntime.Instance;
                if (stage && !stage.IsRespawning && !stage.InDoorway && stage.Player == player.transform)
                {
                    player.GetComponentInChildren<PlayerPartVisual>()?.PlayScare();
                    stage.ReturnToStart();
                }
                Pop();
                return;
            }
            // Walls and floors stop it; other triggers (pickups, zones) and enemies don't.
            if (!other.isTrigger && !other.GetComponent<PlatformEnemy>()) Pop();
        }

        void Pop()
        {
            popped = true;
            MuzzleFlash(transform.position, dir, 1.4f);
            Destroy(gameObject);
        }

        // A quick ink-ringed starburst that blooms and vanishes.
        static void MuzzleFlash(Vector3 at, float direction, float size = 1f)
        {
            var go = new GameObject("Shot Flash");
            go.transform.position = at;
            Disc(go.transform, soft, GlowColor, 0.7f * size, Order);
            Disc(go.transform, soft, Core, 0.35f * size, Order + 1);
            go.AddComponent<Bloom>();
        }

        sealed class Bloom : MonoBehaviour
        {
            float born;
            void Start() => born = Time.time;
            void Update()
            {
                float k = (Time.time - born) / 0.18f;
                if (k >= 1f) { Destroy(gameObject); return; }
                transform.localScale = Vector3.one * (0.6f + 0.9f * k);
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                {
                    var c = sr.color; c.a = 1f - k; sr.color = c;
                }
            }
        }

        static SpriteRenderer Disc(Transform parent, Sprite sprite, Color color, float size, int order)
        {
            var sr = new GameObject("Disc").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localScale = Vector3.one * size;
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // 1-unit disc; soft = radial glow falloff.
        static Sprite MakeDisc(bool softEdge)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = softEdge ? Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f) : Mathf.Clamp01((1f - d) * n * 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
