using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    // After a transform, three manga panels frame the player — head upper-left, body right, legs lower-left — each a
    // slanted (diagonal-cut) paper panel with an ink border, the new part's art and a caption ("오리 머리!" "사자 몸통!"
    // "토끼 다리!"). They slam in from their own side one after another, hold, then fly back out. Drawn in the world so
    // they bend and get the CRT look with the stage.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerPartLoadout))]
    public sealed class PlayerPartComic : MonoBehaviour
    {
        const float StartDelay = 1.2f;      // after the combine cloud has burst (PlayerCombineFx.burstAt + a beat)
        const float Stagger = 0.18f, InTime = 0.26f, Hold = 1.5f, ExitTime = 0.3f;
        const float PanelW = 1.5f, PanelH = 1.25f, Slant = 0.26f, Border = 0.07f, BandH = 0.32f;
        const int Order = 90;

        static readonly Color Ink = new(0.043f, 0.102f, 0.227f, 1f), Paper = new(1f, 0.98f, 0.93f, 1f),
            Caption = new(1f, 0.784f, 0.078f, 1f);
        static Material spriteMaterial;

        // Zigzag around the player's middle: left, right, left. side = -1 left, +1 right.
        static readonly (float side, float x, float y, float tilt)[] Layout =
        {
            (-1f, 1.75f, 1.05f, -5f),
            (+1f, 1.8f, 0.4f, 4f),
            (-1f, 1.7f, -0.35f, -3f),
        };

        PlayerPartLoadout loadout;
        CapsuleCollider2D capsule;
        Transform group;
        readonly List<Transform> panels = new();
        float shownAt = -100f;
        string legend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        static void Attach()
        {
            foreach (var l in FindObjectsByType<PlayerPartLoadout>(FindObjectsSortMode.None))
                if (!l.GetComponent<PlayerPartComic>()) l.gameObject.AddComponent<PlayerPartComic>();
        }

        void Awake()
        {
            loadout = GetComponent<PlayerPartLoadout>();
            capsule = GetComponent<CapsuleCollider2D>();
        }

        void OnEnable() => loadout.Revealed += OnRevealed;

        void OnDisable()
        {
            loadout.Revealed -= OnRevealed;
            Clear();
        }

        void OnRevealed(PlayerPartBuild before, PlayerPartBuild next)
        {
            if (next == null) return;
            Clear();
            if (!spriteMaterial) spriteMaterial = DefaultSpriteMaterial();
            group = new GameObject("Part Comic").transform;
            SceneManager.MoveGameObjectToScene(group.gameObject, gameObject.scene);
            var parts = new[] { next.Head, next.Body, next.Legs };
            legend = next.Legend;
            for (int i = 0; i < 3; i++) panels.Add(BuildPanel(parts[i], i));
            shownAt = Time.time + StartDelay;
            Follow();
            foreach (var p in panels) p.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!group) return;
            float t = Time.time - shownAt;
            if (t > Stagger * 2f + InTime + Hold + ExitTime + 0.2f) { Clear(); return; }
            Follow();
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                var (side, _, _, tilt) = Layout[i];
                var home = Home(i);
                float k = t - i * Stagger;
                if (k < 0f) { p.gameObject.SetActive(false); continue; }
                p.gameObject.SetActive(true);
                float exitAt = Stagger * 2f + InTime + Hold + i * 0.07f;
                if (t > exitAt)
                {
                    // Fly back out the way it came, spinning a little.
                    float e = Mathf.Clamp01((t - exitAt) / ExitTime);
                    p.localPosition = home + new Vector3(side * 2.2f * e * e, 0.3f * e, 0f);
                    p.localRotation = Quaternion.Euler(0f, 0f, tilt + side * -25f * e);
                    p.localScale = Vector3.one * (1f - 0.4f * e);
                    continue;
                }
                // Slam in from its own side, overshoot past home, then settle with a wobble.
                float u = Mathf.Clamp01(k / InTime);
                float back = 1f + 2.4f * Mathf.Pow(u - 1f, 3f) + 1.4f * Mathf.Pow(u - 1f, 2f); // ease-out-back
                p.localPosition = home + new Vector3(side * 1.6f * (1f - back), 0f, 0f);
                float settle = u >= 1f ? Mathf.Sin((k - InTime) * 16f) * Mathf.Exp(-(k - InTime) * 5f) : 0f;
                p.localRotation = Quaternion.Euler(0f, 0f, tilt + side * 12f * (1f - u) + 3f * settle);
                float s = Mathf.Lerp(0.5f, 1f, u) + 0.04f * settle;
                p.localScale = new Vector3(s, s, 1f);
                // Glow breathes; rays turn slowly (opposite ways on each side) and flare as the panel lands.
                float flare = 1f + 0.5f * Mathf.Exp(-Mathf.Max(0f, k - InTime * 0.6f) * 6f);
                p.GetChild(0).localScale = Vector3.one * 3.6f * (0.94f + 0.06f * Mathf.Sin(t * 5f + i)) * flare;
                p.GetChild(1).localRotation = Quaternion.Euler(0f, 0f, side * t * 110f + i * 40f);
                p.GetChild(1).localScale = Vector3.one * flare;
            }
        }

        // The group sits on the player's middle; panels are placed around it, pulled back on screen if needed.
        void Follow()
        {
            float mid = capsule ? capsule.offset.y : 0.7f;
            group.position = transform.position + Vector3.up * mid;
        }

        Vector3 Home(int i)
        {
            var (side, x, y, _) = Layout[i];
            var local = new Vector3(side * x, y, 0f);
            var cam = Camera.main;
            if (!cam || !cam.orthographic) return local;
            float halfW = cam.orthographicSize * cam.aspect, halfH = cam.orthographicSize;
            var world = group.position + local;
            var c = cam.transform.position;
            float margin = PanelW * 0.5f + Slant + 0.35f;
            world.x = Mathf.Clamp(world.x, c.x - halfW + margin, c.x + halfW - margin);
            world.y = Mathf.Clamp(world.y, c.y - halfH + PanelH * 0.5f + 0.4f, c.y + halfH - PanelH * 0.5f - 0.6f);
            return world - group.position;
        }

        Transform BuildPanel(AnimalPartDefinition part, int index)
        {
            var root = new GameObject("Panel " + index).transform;
            root.SetParent(group, false);

            // Behind the panel: a soft warm glow and slowly turning light rays.
            var tint = index == 1 ? new Color(1f, 0.8f, 0.6f, 1f) : new Color(1f, 0.93f, 0.62f, 1f);
            Glow(root, tint, 3.6f, Order - 3);
            Rays(root, tint, 2.3f, 3, Order - 2); // three fanfare beams per part, spinning
            // No panel box: just the part itself over the glow and beams, with its name underneath.
            float bandY = -PanelH * 0.5f + BandH * 0.5f;
            if (part && part.Sprite)
            {
                var art = new GameObject("Art").AddComponent<SpriteRenderer>();
                art.transform.SetParent(root, false);
                art.sprite = part.Sprite;
                art.sortingOrder = Order + 3;
                // Part sheets carry a lot of empty canvas; fit the drawn shape, not the whole rect.
                var shape = VisibleBounds(part.Sprite);
                float room = PanelH - BandH;
                float fit = Mathf.Min(PanelW / Mathf.Max(shape.size.x, 0.01f), room / Mathf.Max(shape.size.y, 0.01f));
                art.transform.localScale = Vector3.one * fit;
                float artCenterY = bandY + BandH * 0.5f + room * 0.5f;
                art.transform.localPosition = new Vector3(-shape.center.x * fit, artCenterY - shape.center.y * fit, 0f);
            }

            // A soft dark haze behind the caption keeps the yellow lettering readable on any background.
            var shade = new GameObject("Caption Shade").AddComponent<SpriteRenderer>();
            shade.transform.SetParent(root, false);
            shade.transform.localPosition = new Vector3(0f, bandY, 0f);
            shade.transform.localScale = new Vector3(PanelW * 1.5f, BandH * 2.6f, 1f);
            shade.sprite = glowSprite;
            shade.color = new Color(0.03f, 0.06f, 0.16f, 0.85f);
            shade.sortingOrder = Order + 2;

            var text = new GameObject("Caption Text").AddComponent<TextMeshPro>();
            text.transform.SetParent(root, false);
            text.transform.localPosition = new Vector3(0f, bandY, 0f);
            text.rectTransform.sizeDelta = new Vector2(PanelW * 1.2f, BandH);
            text.text = part ? $"{legend ?? AnimalName(part.Animal)} {SlotName(part.Slot)}!" : "?";
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1f;
            text.fontSizeMax = 2.6f;
            text.color = Caption;
            text.fontStyle = FontStyles.Bold;
            text.sortingOrder = Order + 4;
            return root;
        }

        static readonly Dictionary<Sprite, Bounds> shapes = new();

        // Bounds of the opaque pixels, in sprite-local units. The part sheets are 512px canvases with a small drawing
        // and are not CPU-readable, so read a downscaled copy back from the GPU once per sprite.
        internal static Bounds VisibleBounds(Sprite sprite)
        {
            if (shapes.TryGetValue(sprite, out var b)) return b;
            b = sprite.bounds;
            var tex = sprite.texture;
            if (tex)
            {
                const int N = 128;
                var rt = RenderTexture.GetTemporary(N, N, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(N, N, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, N, N), 0, 0);
                copy.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                var px = copy.GetPixels32();
                Destroy(copy);
                var rect = sprite.rect;
                float sx = (float)N / tex.width, sy = (float)N / tex.height;
                int x0 = Mathf.FloorToInt(rect.xMin * sx), x1 = Mathf.CeilToInt(rect.xMax * sx), y0 = Mathf.FloorToInt(rect.yMin * sy), y1 = Mathf.CeilToInt(rect.yMax * sy);
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(N, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(N, x1); x++)
                    if (px[y * N + x].a > 24)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                if (maxX >= 0)
                {
                    float ppu = sprite.pixelsPerUnit;
                    Vector2 min = new((minX / sx - rect.xMin - sprite.pivot.x) / ppu, (minY / sy - rect.yMin - sprite.pivot.y) / ppu);
                    Vector2 max = new(((maxX + 1) / sx - rect.xMin - sprite.pivot.x) / ppu, ((maxY + 1) / sy - rect.yMin - sprite.pivot.y) / ppu);
                    b = new Bounds((min + max) * 0.5f, max - min);
                }
            }
            shapes[sprite] = b;
            return b;
        }

        static Sprite glowSprite;

        static void Glow(Transform parent, Color color, float size, int order)
        {
            if (!glowSprite)
            {
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                }
                tex.SetPixels32(px);
                tex.Apply();
                glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                glowSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            var sr = new GameObject("Glow").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localScale = Vector3.one * size;
            sr.sprite = glowSprite;
            color.a = 0.75f;
            sr.color = color;
            sr.sortingOrder = order;
        }

        // Light rays: soft wedges fanning out from the panel centre, bright at the root and fading to nothing.
        static void Rays(Transform parent, Color color, float radius, int count, int order)
        {
            var go = new GameObject("Rays");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Rays" };
            var verts = new Vector3[count * 3];
            var cols = new Color[count * 3];
            var tris = new int[count * 6];
            Color root = color, tip = color;
            root.a = 0.5f; tip.a = 0f;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                float half = 0.2f, len = radius;
                verts[i * 3] = Vector3.zero;
                verts[i * 3 + 1] = new Vector3(Mathf.Cos(a - half), Mathf.Sin(a - half)) * len;
                verts[i * 3 + 2] = new Vector3(Mathf.Cos(a + half), Mathf.Sin(a + half)) * len;
                cols[i * 3] = root; cols[i * 3 + 1] = cols[i * 3 + 2] = tip;
                tris[i * 6] = i * 3; tris[i * 6 + 1] = i * 3 + 1; tris[i * 6 + 2] = i * 3 + 2;
                tris[i * 6 + 3] = i * 3; tris[i * 6 + 4] = i * 3 + 2; tris[i * 6 + 5] = i * 3 + 1;
            }
            mesh.vertices = verts; mesh.colors = cols; mesh.triangles = tris;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = spriteMaterial;
            mr.sortingOrder = order;
            go.AddComponent<MeshCleanup>().mesh = mesh;
        }

        // A parallelogram: top edge shifted right by +slant/2, bottom edge left by -slant/2 (or the reverse).
        static void Quad(Transform parent, string name, float x, float y, float w, float h, float slant, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var mesh = new Mesh { name = name };
            float hw = w * 0.5f, hh = h * 0.5f, s = slant * 0.5f;
            mesh.vertices = new[] { new Vector3(-hw - s, -hh), new Vector3(-hw + s, hh), new Vector3(hw + s, hh), new Vector3(hw - s, -hh) };
            mesh.colors = new[] { color, color, color, color };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 }; // both windings
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = spriteMaterial;
            mr.sortingOrder = order;
            go.AddComponent<MeshCleanup>().mesh = mesh;
        }

        sealed class MeshCleanup : MonoBehaviour
        {
            public Mesh mesh;
            void OnDestroy() { if (mesh) Destroy(mesh); }
        }

        // The project's default sprite material (white texture × vertex colour), borrowed from a throwaway renderer.
        static Material DefaultSpriteMaterial()
        {
            var probe = new GameObject("probe").AddComponent<SpriteRenderer>();
            // Own copy with an explicit white texture: sharing the sprite material lets these meshes pick up whatever
            // texture the previous sprite drew with (the glow), which turned the paper into a soft blob.
            var material = new Material(probe.sharedMaterial) { name = "Comic Mesh", mainTexture = Texture2D.whiteTexture, hideFlags = HideFlags.DontUnloadUnusedAsset };
            DestroyImmediate(probe.gameObject);
            return material;
        }

        static string AnimalName(PartAnimal animal) => animal switch
        {
            PartAnimal.Rabbit => "토끼",
            PartAnimal.Bear => "곰",
            PartAnimal.Lion => "사자",
            PartAnimal.Axolotl => "우파루파",
            PartAnimal.Duck => "오리",
            PartAnimal.Chicken => "닭",
            PartAnimal.Dragon => "용",
            PartAnimal.Phoenix => "불사조",
            _ => animal.ToString()
        };

        static string SlotName(PartSlot slot) => slot switch
        {
            PartSlot.Head => "머리",
            PartSlot.Body => "몸통",
            _ => "다리"
        };

        void Clear()
        {
            panels.Clear();
            if (group) Destroy(group.gameObject);
            group = null;
        }
    }
}
