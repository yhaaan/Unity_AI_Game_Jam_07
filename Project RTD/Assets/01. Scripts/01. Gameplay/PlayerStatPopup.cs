using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // On a reroll, every stat that changed pops above the player's head as [icon] +1 / -1:
    // a comic "boing" in, a short float up, then a fade. Gains are yellow, losses coral, both with ink outlines.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerPartLoadout))]
    public sealed class PlayerStatPopup : MonoBehaviour
    {
        [SerializeField] private Sprite weightIcon, speedIcon, healthIcon, jumpIcon;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color gain = new Color32(0xff, 0xd1, 0x66, 0xff);
        [SerializeField] private Color loss = new Color32(0xe0, 0x52, 0x63, 0xff);
        [SerializeField] private Color ink = new Color32(0x10, 0x17, 0x26, 0xff);
        [SerializeField] private float headroom = 0.45f, spacing = 0.95f, iconSize = 0.42f;
        [SerializeField] private float lifetime = 1.6f, rise = 0.7f;
        [SerializeField] private int sortingOrder = 80;
        [SerializeField, Tooltip("Wait for the combine cloud to burst before popping.")] private float startDelay = 0.6f;

        private PlayerPartLoadout loadout;
        private CapsuleCollider2D capsule;
        private readonly List<Popup> live = new();
        private Material outlinedFont;

        private sealed class Popup
        {
            public Transform root;
            public SpriteRenderer icon, iconShadow;
            public TextMeshPro text;
            public float born, delay;
            public Vector3 basePos;
        }

        private void Awake()
        {
            loadout = GetComponent<PlayerPartLoadout>();
            capsule = GetComponent<CapsuleCollider2D>();
        }

        private void OnEnable() { loadout.Revealed += OnRevealed; }
        private void OnDisable() { loadout.Revealed -= OnRevealed; }

        private void OnRevealed(PlayerPartBuild before, PlayerPartBuild next)
        {
            if (before == null || next == null) return; // first summon: nothing to compare against

            var changes = new List<(Sprite icon, int delta)>();
            Add(changes, weightIcon, next.Weight - before.Weight);
            Add(changes, speedIcon, next.Speed - before.Speed);
            Add(changes, healthIcon, next.Health - before.Health);
            Add(changes, jumpIcon, next.Jump - before.Jump);
            Clear();
            for (int i = 0; i < changes.Count; i++)
            {
                float x = (i - (changes.Count - 1) * 0.5f) * spacing;
                live.Add(Spawn(changes[i].icon, changes[i].delta, new Vector3(x, Top() + headroom, 0f), startDelay + i * 0.07f));
            }
        }

        private static void Add(List<(Sprite, int)> list, Sprite icon, int delta)
        {
            if (delta != 0) list.Add((icon, delta));
        }

        private float Top() => capsule ? capsule.offset.y + capsule.size.y * 0.5f : 1f;

        private Popup Spawn(Sprite iconSprite, int delta, Vector3 position, float delay)
        {
            var root = new GameObject("Stat Popup").transform;
            root.SetParent(transform, false); // the root never flips, so text always reads left to right
            root.localPosition = position;
            root.localScale = Vector3.zero;
            Color tint = delta > 0 ? gain : loss;

            var shadow = Icon(root, "Shadow", iconSprite, ink, sortingOrder);
            shadow.transform.localPosition = new Vector3(-0.2f + 0.035f, -0.035f, 0f);
            var icon = Icon(root, "Icon", iconSprite, tint, sortingOrder + 1);
            icon.transform.localPosition = new Vector3(-0.2f, 0f, 0f);

            var text = new GameObject("Value").AddComponent<TextMeshPro>();
            text.transform.SetParent(root, false);
            text.transform.localPosition = new Vector3(0.24f, 0f, 0f);
            if (font) text.font = font;
            text.fontSharedMaterial = OutlinedFont(text);
            text.text = (delta > 0 ? "+" : "") + delta;
            text.fontSize = 4.2f;
            text.fontStyle = FontStyles.Bold;
            text.color = tint;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(0.8f, 0.6f);
            text.sortingOrder = sortingOrder + 2;

            return new Popup { root = root, icon = icon, iconShadow = shadow, text = text, born = Time.time, delay = delay, basePos = position };
        }

        private SpriteRenderer Icon(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (sprite) sr.transform.localScale = Vector3.one * (iconSize / sprite.bounds.size.y);
            return sr;
        }

        private Material OutlinedFont(TextMeshPro text)
        {
            if (outlinedFont) return outlinedFont;
            outlinedFont = new Material(text.fontSharedMaterial) { name = "Stat Popup (outlined)" };
            outlinedFont.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            outlinedFont.SetColor(ShaderUtilities.ID_OutlineColor, ink);
            outlinedFont.EnableKeyword(ShaderUtilities.Keyword_Outline);
            return outlinedFont;
        }

        private void Update()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                float t = Time.time - p.born - p.delay;
                if (t < 0f) continue;
                if (t >= lifetime) { Destroy(p.root.gameObject); live.RemoveAt(i); continue; }
                float pop = Mathf.Min(1f, t / 0.28f);
                float scale = BackOut(pop) * (1f + 0.06f * Mathf.Sin(t * 18f) * Mathf.Exp(-t * 4f));
                float up = rise * (1f - Mathf.Pow(1f - Mathf.Min(1f, t / lifetime), 3f));
                float fade = Mathf.Clamp01((lifetime - t) / 0.45f);
                p.root.localScale = Vector3.one * scale;
                p.root.localPosition = p.basePos + Vector3.up * up;
                p.root.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(t * 9f) * Mathf.Exp(-t * 3f));
                SetAlpha(p.icon, fade); SetAlpha(p.iconShadow, fade);
                p.text.alpha = fade;
            }
        }

        private static float BackOut(float x)
        {
            const float c = 2.2f;
            float u = x - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        private static void SetAlpha(SpriteRenderer sr, float a)
        {
            var c = sr.color; c.a = a; sr.color = c;
        }

        private void Clear()
        {
            foreach (var p in live) if (p.root) Destroy(p.root.gameObject);
            live.Clear();
        }

        private void OnDestroy()
        {
            Clear();
            if (outlinedFont) Destroy(outlinedFont);
        }
    }
}
