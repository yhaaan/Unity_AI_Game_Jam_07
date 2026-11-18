using UnityEngine;

namespace ProjectRTD.Gameplay
{
    static class StageArt
    {
        /// <summary>
        /// A placeholder renderer the stage art replaces. In Play it is only switched off; when the layout is
        /// baked in the editor it is deleted (with its object, if the renderer was all that object held),
        /// so the saved scene holds no unused pieces.
        /// </summary>
        public static void Retire(Renderer renderer)
        {
            if (!renderer) return;
            if (Application.isPlaying || IsPrefabPart(renderer)) { renderer.enabled = false; return; }
            var go = renderer.gameObject;
            if (go.GetComponent<TMPro.TMP_Text>() || OnlyHolds(go, renderer)) Object.DestroyImmediate(go);
            else Object.DestroyImmediate(renderer);
        }

        /// <summary>Deletes an unused object (Play: Destroy, editor bake: DestroyImmediate).</summary>
        public static void Remove(GameObject go)
        {
            if (!go) return;
            if (Application.isPlaying) Object.Destroy(go);
            else if (IsPrefabPart(go)) go.SetActive(false);
            else Object.DestroyImmediate(go);
        }

        static bool OnlyHolds(GameObject go, Renderer renderer)
        {
            if (go.transform.childCount > 0) return false;
            foreach (var c in go.GetComponents<Component>())
                if (c is not Transform && c != renderer) return false;
            return true;
        }

        static bool IsPrefabPart(Object o)
        {
#if UNITY_EDITOR
            return UnityEditor.PrefabUtility.IsPartOfPrefabInstance(o);
#else
            return false;
#endif
        }
    }
}
