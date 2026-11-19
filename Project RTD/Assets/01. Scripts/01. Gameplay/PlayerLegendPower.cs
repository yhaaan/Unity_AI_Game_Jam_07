using System.Collections.Generic;
using ProjectRTD.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    // The hidden power of a legendary build, on T:
    //  불사조 — flame nova: everything hostile around the player burns away (scientists, drones, bullets).
    //           Once per stage it also rises from its own ashes instead of being sent back to the start.
    //  용     — breath: a long jet of fire in the direction it faces that clears whatever stands in it.
    // Burnt enemies come back when the stage resets (player respawn). The phone HUD shows the cooldown.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerPartLoadout))]
    public sealed class PlayerLegendPower : MonoBehaviour
    {
        const float Cooldown = 6f, NovaRadius = 4.6f, BreathLength = 6.5f, BreathHeight = 2.2f, BreathTime = 0.55f;
        const int Order = 70;

        static readonly Color Hot = new(1f, 0.95f, 0.6f, 1f), Fire = new(1f, 0.55f, 0.15f, 1f), Ember = new(0.9f, 0.2f, 0.1f, 1f),
            Jade = new(0.5f, 1f, 0.65f, 1f), Violet = new(0.7f, 0.5f, 1f, 1f);
        static Sprite soft;

        internal static PlayerLegendPower Instance { get; private set; }
        /// <summary>null when the current build is not a legend; otherwise 0 (just used) .. 1 (ready).</summary>
        internal static float? Charge => Instance && Instance.loadout.Current?.Legend != null
            ? Mathf.Clamp01((Time.time - Instance.usedAt) / Cooldown) : null;

        PlayerPartLoadout loadout;
        PlayerInputReader input;
        float usedAt = -100f, facing = 1f, breathUntil;
        bool reviveReady = true;
        readonly List<GameObject> burnt = new();
        readonly List<Puff> puffs = new();
        StageRuntime stage;

        struct Puff { public SpriteRenderer sr; public Vector2 velocity; public float born, life, from, to; public Color a, b; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        static void Attach()
        {
            foreach (var l in FindObjectsByType<PlayerPartLoadout>(FindObjectsSortMode.None))
                if (!l.GetComponent<PlayerLegendPower>()) l.gameObject.AddComponent<PlayerLegendPower>();
        }

        void Awake()
        {
            Instance = this;
            loadout = GetComponent<PlayerPartLoadout>();
            input = GetComponent<PlayerInputReader>();
        }

        void Start()
        {
            stage = StageRuntime.Instance;
            if (stage) stage.ResetMechanics += RestoreBurnt;
            loadout.Changed += OnChanged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (stage) stage.ResetMechanics -= RestoreBurnt;
            if (loadout) loadout.Changed -= OnChanged;
        }

        // A freshly rolled legend arrives charged, with its rebirth.
        void OnChanged(PlayerPartBuild build)
        {
            if (build?.Legend == null) return;
            usedAt = -100f;
            reviveReady = true;
        }

        void RestoreBurnt()
        {
            foreach (var go in burnt) if (go) go.SetActive(true);
            burnt.Clear();
        }

        void Update()
        {
            if (input && Mathf.Abs(input.Move.x) > 0.1f) facing = Mathf.Sign(input.Move.x);
            TickPuffs();
            string legend = loadout.Current?.Legend;
            if (legend == null) return;
            if (Time.time < breathUntil) Burn(BreathArea());

            var k = Keyboard.current;
            if (k == null || !k.tKey.wasPressedThisFrame || Time.timeScale <= 0f || Time.time - usedAt < Cooldown) return;
            if (stage && (stage.IsRespawning || stage.InDoorway)) return;
            usedAt = Time.time;
            if (legend == "불사조") Nova();
            else Breath();
        }

        Vector2 Centre => (Vector2)transform.position + Vector2.up * 0.75f;

        // ---------- 불사조 ----------

        void Nova()
        {
            SoundManager.Play(Sfx.Goal);
            Burn(new Bounds(Centre, Vector3.one * NovaRadius * 2f), NovaRadius);
            Spawn(Centre, Vector2.zero, 0.5f, 1f, NovaRadius * 2.4f, Hot, new Color(Fire.r, Fire.g, Fire.b, 0f));      // shockwave
            Spawn(Centre, Vector2.zero, 0.7f, 2f, NovaRadius * 1.5f, Fire, new Color(Ember.r, Ember.g, Ember.b, 0f));
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2f + Random.value * 0.3f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Spawn(Centre, dir * Random.Range(4f, 8.5f) + Vector2.up * 1.5f, Random.Range(0.45f, 0.8f), Random.Range(0.5f, 1.1f), 0.1f,
                    i % 2 == 0 ? Hot : Fire, new Color(Ember.r, Ember.g, Ember.b, 0f));
            }
        }

        /// <summary>불사조 only, once per stage: burn everything nearby and stay where it stands instead of respawning.</summary>
        internal static bool TryRevive()
        {
            var p = Instance;
            if (!p || p.loadout.Current?.Legend != "불사조" || !p.reviveReady) return false;
            p.reviveReady = false;
            p.Nova();
            return true;
        }

        // ---------- 용 ----------

        void Breath()
        {
            SoundManager.Play(Sfx.Summon);
            breathUntil = Time.time + BreathTime;
            var mouth = Centre + new Vector2(facing * 0.5f, 0.25f);
            // A thick jet: big overlapping puffs, bright at the mouth and spreading as they go.
            const int count = 44;
            for (int i = 0; i < count; i++)
            {
                float speed = BreathLength / 0.42f * Random.Range(0.8f, 1.1f);
                var velocity = new Vector2(facing * speed, Random.Range(-1.1f, 1.1f));
                var puff = Spawn(mouth, velocity, 0.5f, Random.Range(0.8f, 1.2f), Random.Range(2.4f, 3.4f), i % 3 == 0 ? Hot : Jade, new Color(Violet.r, Violet.g, Violet.b, 0f));
                // Stagger the stream so it pours out over the breath instead of all at once.
                var p = puffs[puff]; p.born += i / (float)count * (BreathTime - 0.1f); puffs[puff] = p;
            }
        }

        Bounds BreathArea() => new Bounds(Centre + new Vector2(facing * (BreathLength * 0.5f + 0.3f), 0.2f), new Vector3(BreathLength, BreathHeight, 1f));

        // ---------- burning ----------

        void Burn(Bounds area, float radius = 0f)
        {
            foreach (var enemy in FindObjectsByType<PlatformEnemy>(FindObjectsSortMode.None)) TryBurn(enemy.gameObject, enemy.transform.position + Vector3.up * 0.8f, area, radius);
            foreach (var drone in FindObjectsByType<DroneSentry>(FindObjectsSortMode.None)) TryBurn(drone.gameObject, drone.transform.position, area, radius);
            foreach (var bullet in FindObjectsByType<ScientistBullet>(FindObjectsSortMode.None))
                if (Inside(bullet.transform.position, area, radius)) Destroy(bullet.gameObject);
        }

        void TryBurn(GameObject target, Vector3 at, Bounds area, float radius)
        {
            if (!target.activeInHierarchy || !Inside(at, area, radius)) return;
            target.SetActive(false);
            burnt.Add(target);
            // A puff of flame and smoke where it stood.
            Spawn(at, Vector2.zero, 0.45f, 0.6f, 2.6f, Hot, new Color(Fire.r, Fire.g, Fire.b, 0f));
            for (int i = 0; i < 8; i++)
                Spawn(at, Random.insideUnitCircle * 3.5f + Vector2.up * 2f, Random.Range(0.4f, 0.7f), 0.7f, 0.1f, Fire, new Color(0.2f, 0.2f, 0.25f, 0f));
        }

        static bool Inside(Vector3 at, Bounds area, float radius)
        {
            at.z = area.center.z;
            return radius > 0f ? Vector2.Distance(at, area.center) <= radius : area.Contains(at);
        }

        // ---------- effect particles ----------

        int Spawn(Vector2 at, Vector2 velocity, float life, float from, float to, Color a, Color b)
        {
            if (!soft) soft = MakeSoft();
            var sr = new GameObject("Legend Fx").AddComponent<SpriteRenderer>();
            sr.transform.position = at;
            sr.sprite = soft;
            sr.color = a;
            sr.sortingOrder = Order;
            sr.transform.localScale = Vector3.one * from;
            puffs.Add(new Puff { sr = sr, velocity = velocity, born = Time.time, life = life, from = from, to = to, a = a, b = b });
            return puffs.Count - 1;
        }

        void TickPuffs()
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i];
                if (!p.sr) { puffs.RemoveAt(i); continue; }
                float k = (Time.time - p.born) / p.life;
                if (k < 0f) { p.sr.enabled = false; continue; }
                if (k >= 1f) { Destroy(p.sr.gameObject); puffs.RemoveAt(i); continue; }
                p.sr.enabled = true;
                p.sr.transform.position += (Vector3)(p.velocity * Time.deltaTime);
                p.velocity *= Mathf.Exp(-2.2f * Time.deltaTime);
                p.sr.transform.localScale = Vector3.one * Mathf.Lerp(p.from, p.to, 1f - (1f - k) * (1f - k));
                p.sr.color = Color.Lerp(p.a, p.b, k * k);
                puffs[i] = p;
            }
        }

        static Sprite MakeSoft()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f)));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
