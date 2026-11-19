using System;
using UnityEngine;

namespace ChimeraGroove.Plants
{
    /// <summary>
    /// Jump wall art (Resources/Walls/lab-barrier or mossy-cliff) at any height: the cap and the riveted
    /// ends keep their size, only the plain middle stretches. Position = bottom centre on the ground.
    /// Something landing on top makes the cap squash and the whole pillar settle (watch, or Land()).
    /// Purely visual: put it on / next to the wall's own collider.
    /// Also drawn in edit mode (the built pieces are never saved), so the wall shows in the Scene view
    /// and follows width / height / style edits in the Inspector.
    /// </summary>
    [ExecuteAlways, SelectionBase]
    public class JumpWall : MonoBehaviour
    {
        public const float PPU = 100f;
        public enum Style { LabBarrier, MossyCliff }

        public Style style = Style.LabBarrier;
        [Tooltip("world units")] public float width = 1.3f;
        [Tooltip("world units, ground to top")] public float height = 1.5f;
        public string sortingLayerName = "Default";
        public int sortingOrder = 2;
        [Tooltip("lands are detected when this transform's feet come to rest on top")] public Transform watch;

#pragma warning disable 0649
        [Serializable] class Slice { public string id, file; public float h; public bool stretch; }
        [Serializable] class Rig { public float width, natural_height; public Slice[] slices; }
#pragma warning restore 0649

        Rig rig;
        Transform root, cap;
        SpriteRenderer[] parts;
        float landAt = -10f, time, lastY = float.NaN, lastVy;
        Collider2D watchCol;

        string Folder => style == Style.LabBarrier ? "Walls/lab-barrier" : "Walls/mossy-cliff";

        bool dirty;

        void Start() { Rebuild(); }

        void OnValidate() { dirty = true; }

        void OnDisable()
        {
            if (!Application.isPlaying && root != null) { DestroyImmediate(root.gameObject); root = null; }
        }

        void OnEnable() { if (!Application.isPlaying && root == null) dirty = true; }

        public void Rebuild()
        {
            dirty = false;
            if (root != null)
            {
                if (Application.isPlaying) Destroy(root.gameObject);
                else DestroyImmediate(root.gameObject);
            }
            var text = Resources.Load<TextAsset>(Folder + "/rig");
            if (text == null) { Debug.LogError("JumpWall: missing Resources/" + Folder + "/rig.json"); return; }
            rig = JsonUtility.FromJson<Rig>(text.text);
            root = new GameObject("Wall Art").transform;
            root.SetParent(transform, false);
            var edit = !Application.isPlaying;
            if (edit) root.gameObject.hideFlags = HideFlags.DontSave;
            float k = width / (rig.width / PPU);                     // png px → world, horizontally and for fixed slices
            float fixedH = 0f; foreach (var s in rig.slices) if (!s.stretch) fixedH += s.h / PPU * k;
            float stretchH = Mathf.Max(.05f, height - fixedH);
            float stretchPx = 0f; foreach (var s in rig.slices) if (s.stretch) stretchPx += s.h;
            parts = new SpriteRenderer[rig.slices.Length];
            float y = height;                                         // build from the top down
            for (int i = 0; i < rig.slices.Length; i++)
            {
                var s = rig.slices[i];
                float h = s.stretch ? stretchH * (s.h / stretchPx) : s.h / PPU * k;
                var sr = new GameObject(s.id).AddComponent<SpriteRenderer>();
                if (edit) sr.gameObject.hideFlags = HideFlags.DontSave;
                sr.transform.SetParent(root, false);
                sr.sprite = Resources.Load<Sprite>(Folder + "/" + s.id);
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder + (i == 0 ? 1 : 0);    // cap over the body
                sr.transform.localPosition = new Vector3(0f, y - h / 2f, 0f);
                sr.transform.localScale = new Vector3(k, h / (s.h / PPU), 1f);
                parts[i] = sr;
                y -= h;
            }
            // the cap gets its own pivot (its bottom) so it can squash onto the body
            var capPivot = new GameObject("cap pivot").transform;
            if (edit) capPivot.gameObject.hideFlags = HideFlags.DontSave;
            capPivot.SetParent(root, false);
            float capBottom = parts[0].transform.localPosition.y - parts[0].transform.localScale.y * rig.slices[0].h / PPU / 2f;
            capPivot.localPosition = new Vector3(0f, capBottom, 0f);
            parts[0].transform.SetParent(capPivot, true);
            cap = capPivot;
            if (watch != null) watch.TryGetComponent(out watchCol);
        }

        /// <summary>Plays the landing squash (call it from gameplay if you have a better landing event).</summary>
        public void Land(float strength = 1f) { landAt = time; landStrength = Mathf.Clamp(strength, .3f, 1.5f); }
        float landStrength = 1f;

        void Update()
        {
            if (!Application.isPlaying)
            {
                if (dirty) Rebuild();
                return;
            }
            if (root == null) return;
            time += Time.deltaTime;
            if (watch != null) WatchLanding();
            float a = time - landAt, sq = 0f;
            if (a >= 0f && a < .6f) sq = landStrength * Mathf.Exp(-7f * a) * Mathf.Cos(2f * Mathf.PI * a * 3.2f);
            cap.localScale = new Vector3(1f + .12f * sq, 1f - .2f * sq, 1f);
            root.localScale = new Vector3(1f + .02f * sq, 1f - .03f * sq, 1f);
        }

        void WatchLanding()
        {
            float feet = watchCol != null ? watchCol.bounds.min.y : watch.position.y;
            float vy = float.IsNaN(lastY) ? 0f : (feet - lastY) / Mathf.Max(Time.deltaTime, 1e-4f);
            float top = transform.position.y + height * transform.lossyScale.y;
            bool over = Mathf.Abs(watch.position.x - transform.position.x) < width * .5f * transform.lossyScale.x + .2f;
            if (over && lastVy < -2f && Mathf.Abs(vy) < .5f && Mathf.Abs(feet - top) < .25f) Land(Mathf.Clamp(-lastVy / 8f, .4f, 1.4f));
            lastVy = vy; lastY = feet;
        }
    }
}
