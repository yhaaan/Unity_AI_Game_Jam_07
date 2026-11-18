using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeraGroove.Plants
{
    /// <summary>
    /// Lab shutter door (Resources/LabDoor: frame.png, panel.png, rig.json).
    /// The GameObject's position is the door's bottom centre on the floor.
    ///   Closed  – shutter down, amber lamp, blocker collider on
    ///   Opening – small dip, rolls up into the header with an overshoot, dust puffs, lamp blinks
    ///   Open    – green lamp, blocker off
    ///   Closing – drops with gravity, slams (squash + shake + dust), back to Closed
    /// Open()/Close()/Toggle(); optional auto-open when a tagged body walks into the trigger zone.
    /// Same maths as web/plant-anim.js (LabDoor).
    /// </summary>
    public class LabDoor : MonoBehaviour
    {
        public const float PPU = 100f;
        public enum State { Closed, Opening, Open, Closing }

        public string resourcesRoot = "LabDoor";
        public string sortingLayerName = "Default";
        public int sortingOrder = 0;
        public bool startOpen = false;
        [Header("Auto open")]
        public bool autoOpen = true;
        public string playerTag = "Player";
        [Tooltip("trigger zone width in door widths")] public float triggerWidth = 1.6f;
        public float closeDelay = .6f;
        [Tooltip("collider-free alternative to autoOpen: opens while this transform is within openDistance")]
        public Transform watch;
        public float openDistance = 3f;
        [Header("Feel")]
        public float openSeconds = .75f;
        public float closeSeconds = .5f;
        public bool blockWhenClosed = true;
        public Color lampClosed = new Color32(0xe5, 0xb7, 0x46, 0xff);
        public Color lampMoving = new Color32(0xf0, 0x8a, 0x3c, 0xff);
        public Color lampOpen = new Color32(0x7c, 0xc4, 0x7a, 0xff);
        public Color dustColor = new Color32(0xef, 0xe4, 0xcf, 0xff);

        public event Action<State> StateChanged;
        public State Current { get; private set; }
        public bool IsOpen => Current == State.Open || Current == State.Opening;

#pragma warning disable 0649
        [Serializable] class Opening { public float x0, x1, top, floor, post_l, post_r; }
        [Serializable] class Lamp { public float x, y, r; }
        [Serializable] class PanelInfo { public string file; public float[] size; }
        [Serializable] class Rig { public int[] size; public PanelInfo panel; public Opening opening; public Lamp lamp; }
#pragma warning restore 0649

        class Puff { public float t0, life, x, y, vx, vy, r; public SpriteRenderer sr; }

        Rig rig;
        float W, pw, ph, closedTop, travel, lift, from, t0, time, slamAt = -1f, exitAt = -1f;
        bool puffedThisLift;
        Transform panelT, root;
        SpriteRenderer lampSr, shineSr;
        BoxCollider2D blocker;
        readonly List<Puff> puffs = new List<Puff>();
        int bodiesInside;
        static Sprite disc, square;

        void Start() { Build(); if (startOpen) { lift = 1f; SetState(State.Open); } else SetState(State.Closed); Apply(); }

        static Sprite Disc
        {
            get
            {
                if (disc != null) return disc;
                const int N = 64; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = x + .5f - N / 2f, dy = y + .5f - N / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(N / 2f - d)));
                }
                tex.SetPixels32(px); tex.Apply();
                return disc = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N); // 1 unit wide
            }
        }
        static Sprite Square
        {
            get
            {
                if (square != null) return square;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px); tex.Apply();
                return square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
            }
        }

        Vector3 P(float px, float py) => new Vector3((px - W / 2f) / PPU, (rig.opening.floor - py) / PPU, 0f);

        SpriteRenderer Sr(string name, Sprite s, int order, Transform parent = null)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent != null ? parent : root, false);
            sr.sprite = s; sr.sortingLayerName = sortingLayerName; sr.sortingOrder = sortingOrder + order;
            return sr;
        }

        void Build()
        {
            var text = Resources.Load<TextAsset>(resourcesRoot + "/rig");
            if (text == null) { Debug.LogError("LabDoor: missing Resources/" + resourcesRoot + "/rig.json"); return; }
            rig = JsonUtility.FromJson<Rig>(text.text);
            var o = rig.opening;
            W = rig.size[0];
            pw = (o.x1 - o.x0) + 2f * 38f;
            float ps = pw / rig.panel.size[0];
            ph = rig.panel.size[1] * ps;
            closedTop = o.floor - 6f - ph;
            travel = (o.floor - 6f) - (o.top - 14f);

            root = new GameObject("Door").transform;
            root.SetParent(transform, false);

            // shutter behind the frame, clipped to the doorway
            var mask = new GameObject("doorway mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(root, false);
            mask.sprite = Square;
            float mx0 = o.post_l + 20f, mx1 = o.post_r - 20f, my0 = o.top - 60f, my1 = o.floor;
            mask.transform.localPosition = P((mx0 + mx1) / 2f, (my0 + my1) / 2f);
            mask.transform.localScale = new Vector3((mx1 - mx0) / PPU, (my1 - my0) / PPU, 1f);
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = sortingOrder + 0; mask.frontSortingOrder = sortingOrder + 1;

            panelT = new GameObject("shutter").transform;      // pivot = shutter bottom centre (squash point)
            panelT.SetParent(root, false);
            var panel = Sr("panel", Resources.Load<Sprite>(resourcesRoot + "/panel"), 1, panelT);
            panel.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            panel.transform.localPosition = new Vector3(0f, ph / 2f / PPU, 0f);
            panel.transform.localScale = new Vector3(ps, ps, 1f);

            var frame = Sr("frame", Resources.Load<Sprite>(resourcesRoot + "/frame"), 2);
            frame.transform.localPosition = P(W / 2f, rig.size[1] / 2f);

            lampSr = Sr("lamp", Disc, 3);
            lampSr.transform.localPosition = P(rig.lamp.x, rig.lamp.y);
            lampSr.transform.localScale = Vector3.one * rig.lamp.r * 1.64f / PPU;
            shineSr = Sr("lamp shine", Disc, 4);
            shineSr.color = new Color(1f, 1f, 1f, .55f);
            shineSr.transform.localPosition = P(rig.lamp.x - rig.lamp.r * .3f, rig.lamp.y - rig.lamp.r * .32f);
            shineSr.transform.localScale = Vector3.one * rig.lamp.r * .44f / PPU;

            if (blockWhenClosed)
            {
                blocker = gameObject.AddComponent<BoxCollider2D>();
                blocker.size = new Vector2((o.x1 - o.x0) / PPU, (o.floor - o.top) / PPU);
                blocker.offset = new Vector2(((o.x0 + o.x1) / 2f - W / 2f) / PPU, (o.floor - o.top) / 2f / PPU);
            }
            if (autoOpen && watch == null)
            {
                var zone = gameObject.AddComponent<BoxCollider2D>();
                zone.isTrigger = true;
                zone.size = new Vector2(W * triggerWidth / PPU, (o.floor - o.top) / PPU);
                zone.offset = new Vector2(0f, (o.floor - o.top) / 2f / PPU);
            }
        }

        public void Open() { if (IsOpen || rig == null) return; from = lift; t0 = time; puffedThisLift = false; SetState(State.Opening); }
        public void Close() { if (!IsOpen || rig == null) return; from = lift; t0 = time; SetState(State.Closing); }
        public void Toggle() { if (IsOpen) Close(); else Open(); }

        void SetState(State s)
        {
            Current = s;
            if (blocker != null) blocker.enabled = s != State.Open;
            StateChanged?.Invoke(s);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!autoOpen || other.isTrigger || !other.CompareTag(playerTag)) return;
            bodiesInside++; exitAt = -1f; Open();
        }
        void OnTriggerExit2D(Collider2D other)
        {
            if (!autoOpen || other.isTrigger || !other.CompareTag(playerTag)) return;
            bodiesInside = Mathf.Max(0, bodiesInside - 1);
            if (bodiesInside == 0) exitAt = time;
        }

        void Update()
        {
            if (rig == null) return;
            time += Time.deltaTime;
            if (watch != null)
            {
                Vector3 d = watch.position - transform.position;
                bool near = Mathf.Abs(d.x) < openDistance && d.y > -1f && d.y < openDistance;
                if (near) { exitAt = -1f; Open(); }
                else if (IsOpen && exitAt < 0f) exitAt = time;
            }
            if (exitAt >= 0f && time - exitAt > closeDelay) { exitAt = -1f; Close(); }
            Step();
            Apply();
        }

        static float Back(float u) { const float c1 = 1.7f, c3 = c1 + 1f; float v = u - 1f; return 1f + c3 * v * v * v + c1 * v * v; }

        float squash = 1f, shake;
        void Step()
        {
            float a = time - t0;
            squash = 1f;
            if (Current == State.Opening)
            {
                float u = Mathf.Clamp01(a / openSeconds), dip = .12f;
                if (a < dip) lift = from - 10f / travel * Mathf.Sin(Mathf.PI * a / dip);
                else
                {
                    float v = Mathf.Clamp01((a - dip) / (openSeconds - dip));
                    lift = from + (1f - from) * Back(v);
                    if (!puffedThisLift) { puffedThisLift = true; Dust(.7f); }
                }
                if (u >= 1f) { lift = 1f; SetState(State.Open); }
            }
            else if (Current == State.Closing)
            {
                float u = Mathf.Clamp01(a / closeSeconds);
                lift = from * (1f - u * u * u);
                if (u >= 1f) { lift = 0f; SetState(State.Closed); Dust(1.4f); slamAt = time; }
            }
            shake = 0f;
            if (slamAt >= 0f)
            {
                float s = time - slamAt;
                if (s < .45f)
                {
                    squash = 1f - .09f * Mathf.Exp(-7f * s) * Mathf.Cos(2f * Mathf.PI * s * 3.2f);
                    shake = 4f * Mathf.Exp(-9f * s) * Mathf.Sin(2f * Mathf.PI * s * 11f);
                }
                else slamAt = -1f;
            }
        }

        void Dust(float power)
        {
            var o = rig.opening;
            for (int i = 0; i < 9; i++)
            {
                float side = i % 2 == 1 ? 1f : -1f, k = i / 9f;
                var pf = new Puff
                {
                    t0 = time + k * .05f, life = .55f + k * .3f,
                    x = (side < 0 ? o.x0 + 30f : o.x1 - 30f) + side * k * 20f, y = o.floor - 10f,
                    vx = side * (90f + 160f * k) * power, vy = -(40f + 60f * k) * power,
                    r = (16f + 18f * (1f - k)) * Mathf.Min(1.2f, power),
                };
                pf.sr = Sr("dust", Disc, 5);
                pf.sr.color = dustColor;
                puffs.Add(pf);
            }
        }

        void Apply()
        {
            if (rig == null) return;
            root.localPosition = new Vector3(shake / PPU, 0f, 0f);
            float top = closedTop - lift * travel, bottom = top + ph;
            panelT.localPosition = P(W / 2f + ((rig.opening.x0 + rig.opening.x1) / 2f - W / 2f), bottom);
            panelT.localScale = new Vector3(1f, squash, 1f);

            bool moving = Current == State.Opening || Current == State.Closing;
            var col = Current == State.Open ? lampOpen : lampClosed;
            if (moving) col = Mathf.FloorToInt((time - t0) * 10f) % 2 == 1 ? lampMoving : lampClosed;
            lampSr.color = col;

            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i]; float a = time - p.t0;
                if (a > p.life) { Destroy(p.sr.gameObject); puffs.RemoveAt(i); continue; }
                if (a < 0f) { p.sr.enabled = false; continue; }
                p.sr.enabled = true;
                float u = a / p.life;
                float r = p.r * (u < .25f ? 1f - (1f - u / .25f) * (1f - u / .25f) : 1f - Smooth((u - .25f) / .75f));
                p.sr.transform.localPosition = P(p.x + p.vx * a * (1f - u * .5f), p.y + p.vy * a * (1f - u * .6f));
                p.sr.transform.localScale = Vector3.one * Mathf.Max(0f, r) * 2f / PPU;
            }
        }

        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
    }
}
