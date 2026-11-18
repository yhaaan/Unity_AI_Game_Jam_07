using System;
using ChimeraGroove.Prologue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectRTD.Gameplay
{
    // Renders the original cut behind a UI that uses the live loadout.
    internal sealed class ChimeraRollPresentation : IDisposable
    {
        private const int PreviewLayer = 2; // Ignore Raycast, isolated by position as well.
        private static readonly string[] ThumbnailLayers = { "head-thumbnail", "body-thumbnail", "legs-thumbnail" };
        private static readonly string[] NameLayers = { "axolotl-label", "bear-label", "duck-label" };

        private readonly GameObject renderRoot;
        private readonly PrologueCut cut;
        private readonly Camera camera;
        private readonly RenderTexture surface;
        private readonly RectTransform frame;
        private readonly RectTransform reroll;
        private readonly ChimeraLayerLibrary library;
        private readonly SpriteRenderer buttonArt;
        private readonly SpriteRenderer[] icons = new SpriteRenderer[3];
        private readonly SpriteRenderer[] heroes = new SpriteRenderer[3];
        private readonly SpriteRenderer[] namePositions = new SpriteRenderer[3];
        private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[3];
        private readonly Vector3[] iconPositions = new Vector3[3];
        private readonly Vector2[] iconSizes = new Vector2[3];

        internal bool Finished => cut.IntroDone;

        internal ChimeraRollPresentation(RectTransform parent, RectTransform rerollButton, TMP_FontAsset font,
            ChimeraLayerLibrary library, Material material)
        {
            this.library = library;
            renderRoot = new GameObject("Chimera Roll Render");
            renderRoot.transform.position = new Vector3(2048f, 0f, 0f);
            cut = renderRoot.AddComponent<PrologueCut>();
            cut.cutName = "cut5-chimera-roll";
            cut.playOnStart = false;
            cut.Build();

            var cameraObject = new GameObject("Chimera Roll Camera");
            cameraObject.transform.SetParent(renderRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << PreviewLayer;
            camera.orthographic = true;
            camera.orthographicSize = cut.SizePx.y / (2f * PrologueCut.PPU);
            camera.aspect = cut.SizePx.x / cut.SizePx.y;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(16, 23, 38, 255);
            surface = new RenderTexture((int)cut.SizePx.x, (int)cut.SizePx.y, 24) { name = "Chimera Roll Surface" };
            camera.targetTexture = surface;

            foreach (var sprite in renderRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.gameObject.layer = PreviewLayer;
                sprite.sharedMaterial = material;
            }

            var region = new GameObject("Chimera Roll Region", typeof(RectTransform)).GetComponent<RectTransform>();
            region.SetParent(parent, false);
            region.SetAsFirstSibling();
            region.anchorMin = new Vector2(.07f, .22f);
            region.anchorMax = new Vector2(.93f, .92f);
            region.offsetMin = region.offsetMax = Vector2.zero;
            var picture = new GameObject("Chimera Roll", typeof(RectTransform), typeof(RawImage),
                typeof(AspectRatioFitter), typeof(RectMask2D));
            frame = (RectTransform)picture.transform;
            frame.SetParent(region, false);
            var raw = picture.GetComponent<RawImage>();
            raw.texture = surface;
            raw.raycastTarget = false;
            var fit = picture.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = camera.aspect;

            buttonArt = cut.GetLayerRenderer("reroll-button");
            reroll = rerollButton;
            reroll.SetParent(frame, false);
            var button = reroll.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (button.targetGraphic)
            {
                button.targetGraphic.color = Color.clear;
                button.targetGraphic.canvasRenderer.cullTransparentMesh = false;
            }
            var oldLabel = reroll.GetComponentInChildren<TMP_Text>(true);
            if (oldLabel) oldLabel.gameObject.SetActive(false);

            var originalHero = cut.GetLayerRenderer("bear-core");
            var heroPivot = originalHero.transform.parent;
            foreach (var sprite in heroPivot.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.gameObject.SetActive(false);

            for (int i = 0; i < icons.Length; i++)
            {
                var icon = icons[i] = cut.GetLayerRenderer(ThumbnailLayers[i]);
                iconPositions[i] = icon.transform.localPosition;
                iconSizes[i] = icon.sprite.bounds.size;
                namePositions[i] = cut.GetLayerRenderer(NameLayers[i]);
                namePositions[i].gameObject.SetActive(false);
                var text = new GameObject("Rolled Part " + i, typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(frame, false);
                var label = names[i] = text.GetComponent<TextMeshProUGUI>();
                label.font = font;
                label.enableAutoSizing = true;
                label.fontSizeMin = 8f;
                label.fontSizeMax = 44f;
                label.color = new Color32(30, 25, 32, 255);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.rectTransform.pivot = new Vector2(0f, .5f);

                var hero = new GameObject("Preview " + i, typeof(SpriteRenderer));
                hero.layer = PreviewLayer;
                hero.transform.SetParent(heroPivot, false);
                heroes[i] = hero.GetComponent<SpriteRenderer>();
                heroes[i].sharedMaterial = material;
                heroes[i].sortingOrder = originalHero.sortingOrder + heroes.Length - 1 - i;
            }
        }

        internal void Play(PlayerPartBuild build)
        {
            var parts = new[] { build?.Head, build?.Body, build?.Legs };
            var combined = new Bounds();
            bool hasCombined = false;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var sprite = part ? part.Sprite : null;
                var icon = icons[i];
                icon.sprite = heroes[i].sprite = sprite;
                names[i].text = part ? part.Animal.ToString() : "Unavailable";
                if (!sprite) continue;
                Bounds art = PartBounds(part);
                if (hasCombined) combined.Encapsulate(art);
                else { combined = art; hasCombined = true; }
                float scale = Mathf.Min(iconSizes[i].x / art.size.x, iconSizes[i].y / art.size.y);
                icon.transform.localScale = new Vector3(scale, scale, 1f);
                icon.transform.localPosition = iconPositions[i] - art.center * scale;
            }

            if (hasCombined)
            {
                float scale = Mathf.Min(4.65f / combined.size.x, 4.1f / combined.size.y);
                float ground = build.Legs.GroundYpx > 0 ? (build.Legs.GroundYpx - 256f) / 100f : -combined.min.y;
                var origin = new Vector3(-.55f - combined.center.x * scale, .19f + ground * scale, 0f);
                foreach (var hero in heroes)
                {
                    hero.transform.localScale = new Vector3(scale, scale, 1f);
                    hero.transform.localPosition = origin;
                }
            }
            cut.Play();
            UpdateLayout();
        }

        private Bounds PartBounds(AnimalPartDefinition part)
        {
            var layers = library ? library.Find(part)?.layers : null;
            if (layers == null || layers.Length == 0) return part.Sprite.bounds;
            Bounds bounds = LayerBounds(layers[0]);
            for (int i = 1; i < layers.Length; i++) bounds.Encapsulate(LayerBounds(layers[i]));
            return bounds;
        }

        private static Bounds LayerBounds(ChimeraLayerLibrary.Layer layer)
        {
            Bounds bounds = layer.sprite.bounds;
            bounds.center += ChimeraPartRig.Canvas(layer.pivot[0], layer.pivot[1]);
            return bounds;
        }

        internal void UpdateLayout()
        {
            Vector2 size = frame.rect.size;
            for (int i = 0; i < names.Length; i++)
            {
                var reference = namePositions[i];
                var point = reference.transform.TransformPoint(new Vector3(reference.sprite.bounds.min.x, 0f));
                Anchor(names[i].rectTransform, camera.WorldToViewportPoint(point), new Vector2(.195f * size.x, .064f * size.y));
                names[i].alpha = reference.color.a;
            }
            Bounds button = buttonArt.bounds;
            Vector2 a = camera.WorldToViewportPoint(button.min);
            Vector2 b = camera.WorldToViewportPoint(button.max);
            Anchor(reroll, (a + b) * .5f, Vector2.Scale(size, b - a));
        }

        private static void Anchor(RectTransform rect, Vector2 point, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = point;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        public void Dispose()
        {
            renderRoot.SetActive(false);
            camera.targetTexture = null;
            surface.Release();
            Object.Destroy(surface);
            Object.Destroy(renderRoot);
        }
    }
}
