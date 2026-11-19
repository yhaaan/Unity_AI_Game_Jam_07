using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Hovering security drone: patrols left-right, sweeping a searchlight cone down to the floor. If the player is
    // inside the cone with a clear line of sight (cover blocks it), the siren goes red, the cone locks onto the player
    // and the drone fires an aimed three-shot burst. Break line of sight or get out of the cone to shake it off.
    [DisallowMultipleComponent]
    public sealed class DroneSentry : MonoBehaviour
    {
        [SerializeField] private float patrolLeft = -3f, patrolRight = 3f;   // world x
        [SerializeField, Min(0f)] private float speed = 1.4f;
        [SerializeField, Min(0.5f)] private float beamLength = 4.6f;
        [SerializeField, Range(5f, 45f)] private float beamHalfAngle = 17f;
        [SerializeField, Range(0f, 45f)] private float sweep = 22f;
        [SerializeField, Min(0.1f)] private float sweepSpeed = 1.1f;
        [SerializeField, Min(0f)] private float lockOnDelay = 0.4f;
        [SerializeField, Min(1)] private int burst = 3;
        [SerializeField, Min(0.05f)] private float burstGap = 0.2f, cooldown = 1.4f;
        [SerializeField] private LayerMask sightBlockers = Physics2D.DefaultRaycastLayers;

        static readonly Color ScanColor = new(1f, 0.93f, 0.55f, 0.22f), AlertColor = new(1f, 0.2f, 0.18f, 0.34f);
        static Material spriteMaterial;

        ChimeraGroove.DroneAnimator art;
        MeshRenderer beamRenderer;
        Mesh beamMesh;
        Vector3 home;
        int direction = 1;
        float aimAngle;          // degrees from straight down; + leans right
        float spottedAt = -1f, nextBurst;
        bool alert;

        internal void Configure(float left, float right, float length)
        {
            patrolLeft = left; patrolRight = right; beamLength = length;
        }

        void Start()
        {
            home = transform.position;
            var artGo = new GameObject("Drone Art");
            artGo.transform.SetParent(transform, false);
            artGo.transform.localScale = Vector3.one * 0.55f;
            art = artGo.AddComponent<ChimeraGroove.DroneAnimator>();
            art.sortingOrderBase = 20;
            BuildBeam();
            if (StageRuntime.Instance) StageRuntime.Instance.ResetMechanics += ResetPatrol;
        }

        void OnDestroy()
        {
            if (StageRuntime.Instance) StageRuntime.Instance.ResetMechanics -= ResetPatrol;
            if (beamMesh) Destroy(beamMesh);
        }

        void ResetPatrol()
        {
            transform.position = home;
            direction = 1;
            alert = false;
            spottedAt = -1f;
        }

        void Update()
        {
            var stage = StageRuntime.Instance;
            bool paused = !stage || stage.IsRespawning || stage.InDoorway;
            var player = stage ? stage.Player : null;
            Vector2 eye = transform.position;
            Vector2 target = player ? (Vector2)player.position + Vector2.up * 0.7f : Vector2.zero;

            bool seen = !paused && player && InCone(eye, target) && ClearSight(eye, target, player);
            if (seen && !alert) { alert = true; spottedAt = Time.time; nextBurst = Time.time + lockOnDelay; }
            else if (!seen && alert && Time.time > nextBurst) alert = false;

            if (alert && player)
            {
                // Lock the cone on the player and open fire.
                var to = target - eye;
                aimAngle = Mathf.MoveTowards(aimAngle, Mathf.Clamp(Vector2.SignedAngle(Vector2.down, to), -60f, 60f), 240f * Time.deltaTime);
                if (seen && Time.time >= nextBurst) { StartCoroutine(Burst(player)); nextBurst = Time.time + burst * burstGap + cooldown; }
            }
            else if (!paused)
            {
                // Patrol and sweep.
                float x = transform.position.x + direction * speed * Time.deltaTime;
                if (x > patrolRight) { x = patrolRight; direction = -1; }
                if (x < patrolLeft) { x = patrolLeft; direction = 1; }
                transform.position = new Vector3(x, home.y + Mathf.Sin(Time.time * 2.3f) * 0.08f, home.z);
                aimAngle = Mathf.MoveTowards(aimAngle, Mathf.Sin(Time.time * sweepSpeed) * sweep, 120f * Time.deltaTime);
            }
            if (art) art.state = alert ? ChimeraGroove.DroneState.Alert : ChimeraGroove.DroneState.Scan;
            UpdateBeam();
        }

        System.Collections.IEnumerator Burst(Transform player)
        {
            for (int i = 0; i < burst; i++)
            {
                if (!player || !alert) yield break;
                Vector2 muzzle = (Vector2)transform.position + Vector2.down * 0.35f;
                Vector2 aim = (Vector2)player.position + Vector2.up * 0.7f - muzzle;
                ScientistBullet.Fire(muzzle, aim);
                yield return new WaitForSeconds(burstGap);
            }
        }

        bool InCone(Vector2 eye, Vector2 target)
        {
            var to = target - eye;
            if (to.magnitude > beamLength + 0.4f) return false;
            float angle = Vector2.SignedAngle(Vector2.down, to);
            return Mathf.Abs(Mathf.DeltaAngle(angle, aimAngle)) <= beamHalfAngle;
        }

        bool ClearSight(Vector2 eye, Vector2 target, Transform player)
        {
            var hits = Physics2D.LinecastAll(eye + (target - eye).normalized * 0.4f, target, sightBlockers);
            foreach (var hit in hits)
            {
                if (hit.collider.isTrigger) continue;
                if (hit.collider.attachedRigidbody && hit.collider.attachedRigidbody.transform == player) continue;
                return false; // cover in the way
            }
            return true;
        }

        // ---------- searchlight ----------

        void BuildBeam()
        {
            if (!spriteMaterial)
            {
                var probe = new GameObject("probe").AddComponent<SpriteRenderer>();
                spriteMaterial = new Material(probe.sharedMaterial) { name = "Searchlight", mainTexture = Texture2D.whiteTexture, hideFlags = HideFlags.DontUnloadUnusedAsset };
                Destroy(probe.gameObject);
            }
            var go = new GameObject("Searchlight");
            go.transform.SetParent(transform, false);
            beamMesh = new Mesh { name = "Searchlight" };
            go.AddComponent<MeshFilter>().sharedMesh = beamMesh;
            beamRenderer = go.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = spriteMaterial;
            beamRenderer.sortingOrder = 18;
        }

        void UpdateBeam()
        {
            // Cone from the drone's eye: bright near the drone, fading toward the floor, with a flicker when alert.
            var color = alert ? AlertColor : ScanColor;
            if (alert) color.a *= 0.8f + 0.2f * Mathf.Sin(Time.time * 30f);
            var dirL = Quaternion.Euler(0f, 0f, aimAngle - beamHalfAngle) * Vector3.down;
            var dirR = Quaternion.Euler(0f, 0f, aimAngle + beamHalfAngle) * Vector3.down;
            var origin = Vector3.down * 0.3f;
            var far = color; far.a *= 0.15f;
            beamMesh.Clear();
            beamMesh.vertices = new[] { origin, origin + dirL * beamLength, origin + dirR * beamLength };
            beamMesh.colors = new[] { color, far, far };
            beamMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one };
            beamMesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var y = transform.position.y;
            Gizmos.DrawLine(new Vector3(patrolLeft, y), new Vector3(patrolRight, y));
        }
    }
}
