using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // TAB opens the parts book: every part fans in along a curved carousel (the selected one big in the middle, the
    // rest shrinking and dipping away to both sides), each framed with a yellow line. Left/right (A/D or arrows) moves
    // the selection, the panel at the bottom centre describes it, and parts never worn yet are greyed out.
    // Lives on the curved HUD canvas, so it bends and takes the CRT look like the rest of the HUD. Pauses the game.
    [DisallowMultipleComponent]
    public sealed class PartGallery : MonoBehaviour
    {
        const string SaveKey = "rtd.parts-seen";
        const float Tile = 150f, Spacing = 200f;

        static readonly Color Yellow = new(1f, 0.784f, 0.078f, 1f), Slate = new(0.15f, 0.16f, 0.21f, 0.97f),
            Edge = new(0.07f, 0.08f, 0.11f, 1f), Line = new(0.36f, 0.39f, 0.47f, 1f), Grey = new(0.33f, 0.35f, 0.4f, 1f);
        const string Good = "#7CFF8A";

        static HashSet<string> seen;

        sealed class Card
        {
            public AnimalPartDefinition part;
            public RectTransform rect;
            public CanvasGroup group;
            public Image art;
            public Image[] outline;
            public bool known;
            public string legend;   // "불사조" / "용": a legendary build, shown with a tinted stand-in part
            public Color tint = Color.white;
        }

        readonly List<Card> cards = new();
        PlayerPartLoadout loadout;
        RectTransform panel;
        CanvasGroup panelGroup;
        TMP_Text title, nameText, statText, bodyText;
        RectTransform arrowL, arrowR;
        bool open;
        int selected;
        float position, velocity, punch, kick, openedAt, previousTimeScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        static void Attach()
        {
            if (!PlayerPhoneHud.IsStage() || FindFirstObjectByType<PartGallery>(FindObjectsInactive.Include)) return;
            var host = PlayerPhoneHud.CurvedCanvas();
            if (!host) return;
            var go = new GameObject("Part Gallery", typeof(RectTransform));
            go.layer = 0;
            go.transform.SetParent(host, false);
            go.AddComponent<PartGallery>();
        }

        static void LoadSeen()
        {
            if (seen != null) return;
            seen = new HashSet<string>(PlayerPrefs.GetString(SaveKey, "").Split(',').Where(s => s.Length > 0));
        }

        void Start()
        {
            LoadSeen();
            loadout = FindFirstObjectByType<PlayerPartLoadout>();
            if (loadout)
            {
                loadout.Changed += Remember;
                Remember(loadout.Current);
            }
        }

        void OnDestroy()
        {
            if (loadout) loadout.Changed -= Remember;
            if (open) Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        void Remember(PlayerPartBuild build)
        {
            if (build == null) return;
            bool added = false;
            foreach (var p in new[] { build.Head, build.Body, build.Legs })
                if (p) added |= seen.Add(p.name);
            if (build.Legend != null) added |= seen.Add("legend:" + build.Legend);
            if (!added) return;
            PlayerPrefs.SetString(SaveKey, string.Join(",", seen));
            PlayerPrefs.Save();
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.tabKey.wasPressedThisFrame)
            {
                if (open) Close();
                else if (Time.timeScale > 0f && !StageTutorial.Active && loadout) Open();
            }
            if (!open) return;

            int step = (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame ? 1 : 0)
                     - (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame ? 1 : 0);
            if (step != 0)
            {
                int before = selected;
                selected = Mathf.Clamp(selected + step, 0, cards.Count - 1);
                // Clunk: the rail lurches, the new card slams in oversized, and a bump at the ends when there is no more.
                punch = 1f;
                kick = step;
                if (selected != before) { velocity += step * 9f; ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.PlateDown); }
                else velocity += step * 5f; // end of the rail: bounce off it
                Describe();
            }
            Layout();
        }

        void Open()
        {
            if (!panel) Build();
            foreach (var c in cards) Paint(c);
            // Start on the part being worn right now.
            var current = loadout.Current;
            int at = current != null ? cards.FindIndex(c => c.legend == null && c.part == current.Head) : -1;
            selected = at >= 0 ? at : 0;
            position = selected;
            velocity = punch = 0f;
            open = true;
            openedAt = Time.unscaledTime;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            panel.gameObject.SetActive(true);
            transform.SetAsLastSibling(); // above the phone and the other HUD pieces
            title.text = $"파츠 도감   <color=#FFC814>{cards.Count(c => c.known)}</color> / {cards.Count}";
            Describe();
            Layout();
        }

        void Close()
        {
            open = false;
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            panel.gameObject.SetActive(false);
            var player = StageRuntime.Instance ? StageRuntime.Instance.Player : null;
            if (player && player.TryGetComponent<PlayerInputReader>(out var input)) input.ConsumeJumpPressed();
        }

        // ---------- layout ----------

        void Layout()
        {
            float t = Time.unscaledTime - openedAt;
            // Stiff, slightly under-damped spring: the rail overshoots the slot and snaps back into it.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            velocity += (selected - position) * 260f * dt;
            velocity *= Mathf.Exp(-17f * dt);
            position += velocity * dt;
            punch = Mathf.MoveTowards(punch, 0f, dt * 4.5f);
            float slam = punch * punch;
            float now = Time.unscaledTime;
            panelGroup.alpha = Mathf.Clamp01(t / 0.15f);
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                float d = i - position, a = Mathf.Abs(d);
                // Arc: neighbours shrink, dip and lean away, like cards on a curved rail.
                float x = d * Spacing * (1f - 0.035f * Mathf.Min(a, 5f));
                float y = 60f - a * a * 13f;
                float scale = Mathf.Lerp(1.4f, 0.78f, Mathf.Clamp01(a / 2.2f));
                // Deal-in: each card whips in from the right a beat after the one before it.
                float k = Mathf.Clamp01((t - 0.02f * Mathf.Abs(i - selected)) / 0.28f);
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                x += (1f - ease) * 1500f;
                float tilt = -d * 5f + (1f - ease) * -30f;
                // The whole rail jolts sideways on a switch.
                x += kick * slam * 26f * Mathf.Cos(punch * 9f);
                if (i == selected)
                {
                    // Slam in big and tilted, then idle: a gentle bob, sway and breathing while it is being looked at.
                    scale *= 1f + 0.28f * slam;
                    tilt += kick * -14f * slam;
                    float calm = 1f - slam;
                    y += Mathf.Sin(now * 2.6f) * 9f * calm;
                    tilt += Mathf.Sin(now * 1.9f) * 4f * calm;
                    scale *= 1f + 0.03f * Mathf.Sin(now * 3.4f) * calm;
                }
                c.rect.anchoredPosition = new Vector2(x, y);
                c.rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
                c.rect.localScale = Vector3.one * scale;
                c.group.alpha = Mathf.Clamp01(5.2f - a) * ease;
                bool focus = i == selected;
                var line = !c.known ? Grey : focus ? Color.Lerp(Yellow, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f)) : Yellow;
                foreach (var o in c.outline) o.color = line;
            }
            // Draw order: the selected card on top, the far ones underneath.
            foreach (var c in cards.OrderByDescending(c => Mathf.Abs(cards.IndexOf(c) - position))) c.rect.SetAsLastSibling();

            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 10f;
            arrowL.anchoredPosition = new Vector2(-560f - bob, 60f);
            arrowR.anchoredPosition = new Vector2(560f + bob, 60f);
            arrowL.gameObject.SetActive(selected > 0);
            arrowR.gameObject.SetActive(selected < cards.Count - 1);
        }

        void Describe()
        {
            var c = cards[selected];
            var p = c.part;
            if (c.legend != null)
            {
                nameText.text = c.known ? $"<color=#FFC814>{c.legend}</color>  <size=60%>전설 조합</size>" : "<color=#8A8F9C>???</color>  <size=60%>전설 조합</size>";
                statText.text = "리롤 1,000번에 한 번     점프 <color=" + Good + ">3</color>     힘 <color=" + Good + ">3</color>";
                bodyText.text = c.known
                    ? "능력: <color=#FFC814>날기 · 수영 · 과학자 통과</color>\n<color=#FFC814>T</color> " + (c.legend == "불사조"
                        ? "화염 폭발로 주변 적을 태워요. 스테이지마다 한 번 부활!"
                        : "브레스를 뿜어 앞의 적을 쓸어버려요.")
                    : "<color=#8A8F9C>아주 낮은 확률로 나타나는 전설의 조합… 모든 능력을 가졌다고 해요.</color>";
                return;
            }
            if (!c.known)
            {
                nameText.text = "<color=#8A8F9C>???</color>";
                statText.text = $"{SlotName(p.Slot)}";
                bodyText.text = "<color=#8A8F9C>아직 얻지 못한 파츠예요. R로 조합을 바꿔 찾아보세요!</color>";
                return;
            }
            nameText.text = string.IsNullOrWhiteSpace(p.DisplayName) ? $"{p.Animal} {SlotName(p.Slot)}" : p.DisplayName;
            statText.text = $"{SlotName(p.Slot)}     무게 <color={Good}>{Signed(p.WeightDelta)}</color>"
                + (p.Slot == PartSlot.Body ? "" : $"     키 <color={Good}>{Signed(p.HeightDelta)}</color>");
            var skills = p.EffectIds.Select(SkillName).Where(s => s != null).ToArray();
            string text = !string.IsNullOrWhiteSpace(p.Description) ? p.Description
                : p.WeightDelta < 0 ? "가벼워서 점프가 높아져요." : p.WeightDelta >= 2 ? "묵직해서 힘이 세져요." : "균형 잡힌 파츠예요.";
            bodyText.text = skills.Length > 0 ? $"능력: <color=#FFC814>{string.Join(" · ", skills)}</color>\n{text}" : text;
        }

        // Full-width plus: the display font's own "+" glyph is unreadable, this one comes from the Korean fallback.
        static string Signed(int v) => v > 0 ? "＋" + v : v.ToString();
        static string SlotName(PartSlot slot) => slot == PartSlot.Head ? "머리" : slot == PartSlot.Body ? "몸통" : "다리";
        static string SkillName(PartEffect e) => e switch
        {
            PartEffect.Glide => "날기",
            PartEffect.Float => "수영",
            PartEffect.CanPass => "과학자 통과",
            _ => null
        };

        // ---------- construction ----------

        void Build()
        {
            // This object stays active to listen for TAB; the visible book is a child that is switched on and off.
            var self = (RectTransform)transform;
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.offsetMin = self.offsetMax = Vector2.zero;
            panel = new GameObject("Book", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(self, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            var dim = Box("Dim", panel, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.03f, 0.82f));
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;

            title = Text("Title", panel, new Vector2(0f, 330f), new Vector2(900f, 60f), 40f, TextAlignmentOptions.Center);
            Text("Hint", panel, new Vector2(0f, -400f), new Vector2(900f, 34f), 20f, TextAlignmentOptions.Center).text =
                "<color=#FFFFFF99>A / D  또는  ← →  이동        TAB  닫기</color>";

            var rail = new GameObject("Rail", typeof(RectTransform)).GetComponent<RectTransform>();
            rail.SetParent(panel, false);
            foreach (var slotGroup in loadout.Catalog.ForSlot(PartSlot.Head).Select(h => h.Animal).Distinct())
                foreach (var slot in new[] { PartSlot.Head, PartSlot.Body, PartSlot.Legs })
                {
                    var part = loadout.Catalog.ForSlot(slot).FirstOrDefault(p => p.Animal == slotGroup);
                    if (part) cards.Add(MakeCard(rail, part));
                }

            // Legendary builds at the end of the rail. They have no art of their own yet, so a tinted part stands in.
            foreach (var (legend, animal, tint) in new[] { ("불사조", PartAnimal.Chicken, new Color(1f, 0.62f, 0.3f)), ("용", PartAnimal.Lion, new Color(0.5f, 1f, 0.65f)) })
            {
                var stand = loadout.Catalog.ForSlot(PartSlot.Head).FirstOrDefault(p => p.Animal == animal);
                if (!stand) continue;
                var card = MakeCard(rail, stand);
                card.legend = legend;
                card.tint = tint;
                cards.Add(card);
            }

            arrowL = Arrow(panel, -1f);
            arrowR = Arrow(panel, 1f);

            // Description: flat dark tooltip at the bottom centre.
            var info = Box("Info Edge", panel, new Vector2(0f, -270f), new Vector2(780f, 190f), Edge);
            Box("Info Line", info, Vector2.zero, new Vector2(774f, 184f), Line);
            Box("Info Fill", info, Vector2.zero, new Vector2(768f, 178f), Slate);
            nameText = Text("Name", info, new Vector2(0f, 56f), new Vector2(720f, 50f), 36f, TextAlignmentOptions.MidlineLeft);
            nameText.fontStyle = FontStyles.Bold;
            Box("Bar", info, new Vector2(0f, 8f), new Vector2(720f, 36f), new Color(0.23f, 0.24f, 0.3f, 1f));
            statText = Text("Stats", info, new Vector2(0f, 8f), new Vector2(690f, 34f), 22f, TextAlignmentOptions.MidlineLeft);
            bodyText = Text("Body", info, new Vector2(0f, -48f), new Vector2(720f, 64f), 22f, TextAlignmentOptions.MidlineLeft);
        }

        Card MakeCard(RectTransform rail, AnimalPartDefinition part)
        {
            var root = new GameObject(part.name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(rail, false);
            root.sizeDelta = new Vector2(Tile, Tile);
            var card = new Card { part = part, rect = root, group = root.gameObject.AddComponent<CanvasGroup>() };
            // No box: the part floats on its own with a yellow outline hugging its shape — eight copies of the art,
            // drawn in flat colour and nudged outward, behind the real one.
            if (!flat)
            {
                var shader = Shader.Find("GUI/Text Shader"); // colour × the sprite's alpha only
                if (shader) flat = new Material(shader) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            }
            Vector2 size = new(Tile, Tile), offset = Vector2.zero;
            if (part.Sprite)
            {
                // The sheets are mostly empty canvas: scale so the drawn part fills the tile, and centre it.
                var shape = PlayerPartComic.VisibleBounds(part.Sprite);
                var full = part.Sprite.bounds;
                float k = (Tile - 14f) / Mathf.Max(shape.size.x, shape.size.y, 0.01f);
                size = new Vector2(full.size.x, full.size.y) * k;
                offset = -(Vector2)(shape.center - full.center) * k;
            }
            const float w = 5f;
            var nudges = new[] { new Vector2(w, 0), new Vector2(-w, 0), new Vector2(0, w), new Vector2(0, -w),
                                 new Vector2(w, w) * 0.7f, new Vector2(-w, w) * 0.7f, new Vector2(w, -w) * 0.7f, new Vector2(-w, -w) * 0.7f };
            card.outline = new Image[nudges.Length];
            for (int i = 0; i < nudges.Length; i++)
            {
                var o = Box("Outline", root, offset + nudges[i], size, Yellow).GetComponent<Image>();
                o.sprite = part.Sprite;
                if (flat) o.material = flat;
                card.outline[i] = o;
            }
            card.art = Box("Art", root, offset, size, Color.white).GetComponent<Image>();
            card.art.sprite = part.Sprite;
            return card;
        }

        static Material flat;

        void Paint(Card c)
        {
            c.known = seen.Contains(c.legend != null ? "legend:" + c.legend : c.part.name);
            // Never worn: a flat grey silhouette (the flat material ignores the art's own colours).
            c.art.material = c.known || !flat ? null : flat;
            // Never worn: a dark grey silhouette.
            c.art.color = c.known ? c.tint : new Color(0.32f, 0.34f, 0.4f, 0.9f); // grey silhouette
        }

        static Sprite pill, soft;

        // A bare chevron: two rounded yellow bars with a soft yellow glow behind. No box, no dark edge.
        RectTransform Arrow(RectTransform parent, float side)
        {
            if (!pill) pill = PlayerPhoneHud.MakeRound(64, 32, false);
            if (!soft)
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                }
                tex.SetPixels32(px);
                tex.Apply();
                soft = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                soft.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            var holder = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.gameObject.layer = parent.gameObject.layer;
            holder.SetParent(parent, false);
            holder.anchoredPosition = new Vector2(side * 560f, 60f);
            holder.sizeDelta = new Vector2(64f, 64f);
            var glow = Box("Glow", holder, Vector2.zero, new Vector2(190f, 190f), new Color(1f, 0.82f, 0.2f, 0.55f)).GetComponent<Image>();
            glow.sprite = soft;
            foreach (float tilt in new[] { 40f, -40f })
            {
                var bar = Box("Bar", holder, new Vector2(side * 3f, tilt > 0 ? 14f : -14f), new Vector2(48f, 13f), Yellow);
                var image = bar.GetComponent<Image>();
                image.sprite = pill;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 32f / 6.5f; // fully rounded ends
                bar.localRotation = Quaternion.Euler(0f, 0f, -side * tilt);
            }
            return holder;
        }

        static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
