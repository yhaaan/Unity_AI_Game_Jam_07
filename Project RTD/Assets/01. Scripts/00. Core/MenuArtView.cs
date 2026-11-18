using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTD.Core
{
    internal static class MenuArtView
    {
        // Positions are fractions of the parent so the original canvas scaler remains authoritative.
        internal static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x,y);
            rect.anchorMax = new Vector2(x+w,y+h);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static RawImage Picture(RectTransform rect, Texture2D texture, bool fit = true)
        {
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            if (fit && texture)
            {
                var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)texture.width / texture.height;
            }
            return image;
        }

        internal static RawImage Art(Transform parent, Texture2D tex, float x, float y, float w, float h, bool fit = true)
        {
            var box = Box(parent, tex.name, x,y,w,h);
            return Picture(Box(box, "Image", 0,0,1,1), tex, fit);
        }

        internal static Button Action(Transform parent, Texture2D tex, string label, float x,float y,float w,float h, UnityEngine.Events.UnityAction click)
        {
            var rect = Box(parent, label, x,y,w,h);
            var picture = Art(rect, tex, 0,0,1,1);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = picture;
            button.onClick.AddListener(click);
            return button;
        }

        internal static void Label(Transform parent, string caption)
        {
            var label = Box(parent, caption, .06f,.18f,.88f,.64f).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = caption;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMax = 42;
            label.raycastTarget = false;
            label.color = Color.white;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
        }
    }
}
