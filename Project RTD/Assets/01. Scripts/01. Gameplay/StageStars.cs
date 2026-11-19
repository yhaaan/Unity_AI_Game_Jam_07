using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Stage clear rating: up to three stars for how few rerolls the stage took (3 per obstacle or fewer = ★★★,
    // 6 per obstacle or fewer = ★★, otherwise ★). Pops up in the middle of the screen when the exit is reached,
    // on the curved HUD canvas. The best result per stage is kept in PlayerPrefs ("rtd.stars.<scene>").
    public sealed class StageStars : MonoBehaviour
    {
        const float Duration = 1.9f;
        static readonly Color Yellow = new(1f, 0.784f, 0.078f, 1f), Empty = new(0.1f, 0.14f, 0.28f, 0.9f), Ink = new(0.043f, 0.102f, 0.227f, 1f);

        int stars, rolls;

        internal static int Rate(int rolls, int obstacles) =>
            rolls <= 3 * obstacles ? 3 : rolls <= 6 * obstacles ? 2 : 1;

        public static int Best(string sceneName) => PlayerPrefs.GetInt("rtd.stars." + sceneName, 0);

        /// <summary>Shows the rating and returns how long to hold for it (0 when this stage is not rated).</summary>
        internal static float Show(StageRuntime stage)
        {
            if (!stage || TutorialGuide.Instance) return 0f; // the tutorial room's rerolls are free
            var host = PlayerPhoneHud.CurvedCanvas();
            if (!host) return 0f;
            int obstacles = StageSections.Instance ? Mathf.Max(1, StageSections.Instance.sections.Length) : 1;
            var go = new GameObject("Stage Stars", typeof(RectTransform));
            go.layer = 0;
            go.transform.SetParent(host, false);
            var view = go.AddComponent<StageStars>();
            view.rolls = stage.ManualRolls;
            view.stars = Rate(view.rolls, obstacles);
            string key = "rtd.stars." + stage.gameObject.scene.name;
            if (view.stars > PlayerPrefs.GetInt(key, 0)) { PlayerPrefs.SetInt(key, view.stars); PlayerPrefs.Save(); }
            return Duration;
        }

        IEnumerator Start()
        {
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 70f);
            root.sizeDelta = new Vector2(900f, 420f);
            root.SetAsLastSibling();
            var group = gameObject.AddComponent<CanvasGroup>();

            var art = Resources.Load<Sprite>("PhoneHud/phone_star");
            var title = Label("CLEAR!", new Vector2(0f, 96f), 54f, Yellow);
            const float Size = 82f, Step = 104f;
            var slots = new RectTransform[3];
            var glows = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector2((i - 1) * Step, i == 1 ? 10f : 0f);
                // Soft golden glow behind the star, lit when it is earned.
                glows[i] = Star(root, StageObjectiveBanner.SoftSprite(), pos, Size * 2.6f, new Color(1f, 0.85f, 0.3f, 0f)).GetComponent<Image>();
                // Ink outline, then the empty socket; the gold star is added when it is earned.
                foreach (var o in new[] { new Vector2(4, 0), new Vector2(-4, 0), new Vector2(0, 4), new Vector2(0, -4) })
                    Star(root, art, pos + o, Size, Ink);
                Star(root, art, pos, Size, Empty);
                slots[i] = Star(root, art, pos, Size, Yellow);
                slots[i].localScale = Vector3.zero;
            }
            var count = Label($"합성 {rolls}회", new Vector2(0f, -84f), 28f, Color.white);

            // Title drops in.
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
            {
                group.alpha = t;
                title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, t);
                yield return null;
            }
            group.alpha = 1f;
            title.rectTransform.localScale = Vector3.one;

            // Stars slam in one after another.
            for (int i = 0; i < stars; i++)
            {
                ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Confirm);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f)
                {
                    float s = t < 0.6f ? Mathf.Lerp(0f, 1.45f, t / 0.6f) : Mathf.Lerp(1.45f, 1f, (t - 0.6f) / 0.4f);
                    slots[i].localScale = Vector3.one * s;
                    slots[i].localRotation = Quaternion.Euler(0f, 0f, (1f - t) * -40f);
                    glows[i].color = new Color(1f, 0.85f, 0.3f, 0.75f * t);
                    glows[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, t); // flare, then settle
                    yield return null;
                }
                slots[i].localScale = Vector3.one;
                slots[i].localRotation = Quaternion.identity;
                for (float d = 0f; d < 0.08f; d += Time.unscaledDeltaTime) yield return null;
            }

            // Hold, with the earned stars breathing a little.
            float until = Time.unscaledTime + Mathf.Max(0.3f, Duration - 0.22f - stars * 0.28f - 0.25f);
            while (Time.unscaledTime < until)
            {
                for (int i = 0; i < stars; i++)
                {
                    float pulse = Mathf.Sin(Time.unscaledTime * 7f + i);
                    slots[i].localScale = Vector3.one * (1f + 0.05f * pulse);
                    glows[i].color = new Color(1f, 0.85f, 0.3f, 0.6f + 0.15f * pulse);
                }
                yield return null;
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f) { group.alpha = 1f - t; yield return null; }
            Destroy(gameObject);
        }

        static RectTransform Star(RectTransform parent, Sprite art, Vector2 position, float size, Color color)
        {
            var rect = new GameObject("Star", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = art;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        TMP_Text Label(string content, Vector2 position, float size, Color color)
        {
            var rect = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = gameObject.layer;
            rect.SetParent(transform, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(900f, size * 1.4f);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
