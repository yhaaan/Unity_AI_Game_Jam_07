using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Tutorial room (Stage00): obstacles in a row, each with a hint card recommending the parts that beat it. Pressing
    // R in front of an obstacle rolls a build that passes it, so doing what the card says always works.
    // The cards share the phone HUD's look: ink outline, lilac shell, navy screen with a cyan line, yellow icons.
    [DisallowMultipleComponent]
    public sealed class TutorialGuide : MonoBehaviour
    {
        public enum Need { HighJump, LowCeiling, Swim }

        [Serializable]
        public struct Obstacle
        {
            public Need need;
            [Tooltip("World x of the obstacle's near edge; the card floats just before it.")] public float x;
            public float signY;
        }

        [SerializeField] internal Obstacle[] obstacles = Array.Empty<Obstacle>();

        internal static TutorialGuide Instance { get; private set; }

        static Color Hex(int rgb) => new((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1f);
        // Flat dark tooltip: slate panel, thin pale border, a lighter stat bar, white text with coloured keywords.
        static readonly Color Edge = Hex(0x12141C), Line = Hex(0x5C6378), Fill = new(0.15f, 0.16f, 0.21f, 0.96f),
            Bar = Hex(0x3A3E4D), Yellow = Hex(0xFFC814);
        const string Good = "#7CFF8A", Bad = "#FF5A4D";
        const float W = 3.5f, H = 2.2f;
        const int Order = -20; // behind the player, floor and gimmicks; in front of the background props (-79)
        static Sprite white;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (!white)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                var px = new Color32[16];
                for (int k = 0; k < 16; k++) px[k] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                white.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            var player = StageRuntime.Instance ? StageRuntime.Instance.Player : null;
            var catalog = player && player.TryGetComponent(out PlayerPartLoadout loadout) ? loadout.Catalog : null;
            foreach (var o in obstacles) Card(o, catalog);
        }

        internal static bool Passes(Need need, PlayerPartBuild build) => build != null && need switch
        {
            Need.HighJump => build.Jump >= 3,
            Need.LowCeiling => build.Height is >= 0 and <= 2,
            Need.Swim => build.HasEffect(PartEffect.Float),
            _ => true
        };

        // The next obstacle at or ahead of the player; null once they're past the last one.
        internal Func<PlayerPartBuild, bool> AcceptFor(float playerX)
        {
            foreach (var o in obstacles)
                if (o.x > playerX - 0.6f)
                {
                    var need = o.need;
                    return build => Passes(need, build);
                }
            return null;
        }

        static (string title, string icon, bool flip, string stat, string change, string body, PartSlot slot, PartAnimal[] picks) Info(Need need) => need switch
        {
            Need.HighJump => ("높이 뛰기", "phone_jump", false, "점프 레벨:", $"<color={Bad}>2</color> → <color={Good}>3</color>",
                $"몸이 <color={Good}>가벼울수록</color> 높이 뛰어요.\n<color={Good}>우파루파 · 토끼</color> 파츠를 모아보세요.",
                PartSlot.Head, new[] { PartAnimal.Axolotl, PartAnimal.Rabbit }),
            Need.LowCeiling => ("낮은 천장", "phone_jump", true, "키:", $"<color={Bad}>3+</color> → <color={Good}>2 이하</color>",
                $"<color={Good}>작은 머리와 다리</color>만 지나갈 수 있어요.\n<color={Good}>우파루파 · 토끼 · 오리</color> 추천.",
                PartSlot.Head, new[] { PartAnimal.Axolotl, PartAnimal.Rabbit, PartAnimal.Duck }),
            _ => ("물 건너기", "phone_swim", false, "수영:", $"<color={Bad}>불가</color> → <color={Good}>가능</color>",
                $"<color={Good}>물에 뜨는 몸통</color>이 필요해요.\n<color={Good}>오리 · 우파루파</color> 몸통 추천.",
                PartSlot.Body, new[] { PartAnimal.Duck, PartAnimal.Axolotl }),
        };

        void Card(Obstacle o, PartCatalog catalog)
        {
            var info = Info(o.need);
            var root = new GameObject("Tutorial Card " + o.need).transform;
            root.SetParent(transform, false);
            // Jump and pool cards sit over their obstacle; the roof card stands just before the roof (which would cover it).
            float cardX = o.need switch { Need.HighJump => o.x + 0.63f, Need.LowCeiling => o.x - 1.45f, _ => o.x + 2.15f };
            root.position = new Vector3(cardX, o.signY + 0.55f, 0f);
            root.localScale = Vector3.one * 0.8f;

            // Panel: dark edge, thin pale line, slate fill. Square corners.
            Box(root, "Edge", Vector2.zero, new Vector2(W, H), Edge, 0);
            Box(root, "Line", Vector2.zero, new Vector2(W - 0.08f, H - 0.08f), Line, 1);
            Box(root, "Fill", Vector2.zero, new Vector2(W - 0.14f, H - 0.14f), Fill, 2);

            float left = -W * 0.5f + 0.28f, right = W * 0.5f - 0.28f, inner = right - left;

            // Header: icon + title.
            float y = H * 0.5f - 0.42f;
            var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(root, false);
            icon.sprite = Resources.Load<Sprite>("PhoneHud/" + info.icon);
            icon.color = Yellow;
            icon.sortingOrder = Order + 4;
            if (icon.sprite) icon.transform.localScale = Vector3.one * (0.36f / Mathf.Max(icon.sprite.bounds.size.x, icon.sprite.bounds.size.y));
            icon.transform.localPosition = new Vector3(left + 0.2f, y, 0f);
            if (info.flip) icon.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Label(root, info.title, new Vector2(left + 0.5f, y), new Vector2(inner - 0.5f, 0.42f), 2.7f, Color.white, TextAlignmentOptions.MidlineLeft, true);

            // Stat bar: what the obstacle needs, before → after.
            y -= 0.46f;
            Box(root, "Bar", new Vector2(0f, y), new Vector2(inner, 0.32f), Bar, 3);
            Label(root, info.stat, new Vector2(left + 0.12f, y), new Vector2(inner * 0.5f, 0.3f), 1.7f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            Label(root, info.change, new Vector2(left + inner * 0.5f - 0.12f, y), new Vector2(inner * 0.5f, 0.3f), 1.7f, Color.white, TextAlignmentOptions.MidlineRight, true);

            // Body.
            y -= 0.5f;
            Label(root, info.body, new Vector2(left, y), new Vector2(inner, 0.6f), 1.55f, Color.white, TextAlignmentOptions.MidlineLeft, false);

            // Bottom row: [R] key hint on the left, the recommended part art on the right.
            y = -H * 0.5f + 0.34f;
            Box(root, "Key Line", new Vector2(left + 0.16f, y), new Vector2(0.32f, 0.32f), Line, 3);
            Box(root, "Key", new Vector2(left + 0.16f, y), new Vector2(0.26f, 0.26f), Edge, 4);
            Label(root, "R", new Vector2(left + 0.03f, y), new Vector2(0.26f, 0.26f), 1.6f, Color.white, TextAlignmentOptions.Center, true);
            Label(root, "다시 조합", new Vector2(left + 0.42f, y), new Vector2(1.2f, 0.3f), 1.45f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineLeft, false);
            for (int i = 0; i < info.picks.Length; i++)
            {
                var part = catalog ? catalog.ForSlot(info.slot).FirstOrDefault(p => p.Animal == info.picks[i]) : null;
                if (!part || !part.Sprite) continue;
                float x = right - 0.22f - (info.picks.Length - 1 - i) * 0.48f;
                var art = new GameObject("Part").AddComponent<SpriteRenderer>();
                art.transform.SetParent(root, false);
                art.sprite = part.Sprite;
                art.sortingOrder = Order + 5;
                var shape = PlayerPartComic.VisibleBounds(part.Sprite);
                float fit = 0.42f / Mathf.Max(shape.size.x, shape.size.y, 0.01f);
                art.transform.localScale = Vector3.one * fit;
                art.transform.localPosition = new Vector3(x - shape.center.x * fit, y - shape.center.y * fit, 0f);
            }

            root.gameObject.AddComponent<CardFloat>();
        }

        static void Box(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = position;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.sprite = white;
            sr.color = color;
            sr.sortingOrder = Order + order;
        }

        // position = left-middle of the box for left/right aligned text, centre for centred text.
        static void Label(Transform parent, string content, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions align, bool bold)
        {
            var text = new GameObject("Text").AddComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.rectTransform.pivot = align == TextAlignmentOptions.Center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            text.rectTransform.sizeDelta = size;
            text.transform.localPosition = align == TextAlignmentOptions.Center ? new Vector3(position.x + size.x * 0.5f, position.y, 0f) : (Vector3)position;
            text.text = content;
            text.alignment = align;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.8f;
            text.fontSizeMax = fontSize;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.sortingOrder = Order + 6;
        }

        // A very slight hover so the card feels alive without wobbling.
        sealed class CardFloat : MonoBehaviour
        {
            Vector3 home; float phase;
            void Start() { home = transform.localPosition; phase = UnityEngine.Random.value * 6f; }
            void Update() => transform.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * 1.4f + phase) * 0.04f);
        }
    }
}
