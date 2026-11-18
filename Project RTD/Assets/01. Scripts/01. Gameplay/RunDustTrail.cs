using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Six reusable clouds cover the 0.55 s lifetime at four footfalls per beat.
    internal sealed class RunDustTrail
    {
        private const float Life = .55f;
        private readonly SpriteRenderer[] clouds = new SpriteRenderer[6];
        private readonly Vector3[] origins = new Vector3[6];
        private readonly float[] born = new float[6], directions = new float[6];
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        private int next, lastStep = -1;

        public RunDustTrail(Transform owner, SpriteRenderer reference)
        {
            const int w = 40, h = 24;
            texture = new Texture2D(w,h,TextureFormat.RGBA32,false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w*h];
            for (int y=0; y<h; ++y) for (int x=0; x<w; ++x)
            {
                float Disc(float cx,float cy,float radius) => radius - Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));
                float edge = Mathf.Max(Disc(10,9,8), Mathf.Max(Disc(20,12,10),Disc(30,9,7)));
                pixels[y*w+x] = new Color32(255,255,255,(byte)(255*Mathf.Clamp01(edge+.5f)));
            }
            texture.SetPixels32(pixels); texture.Apply(false,true);
            sprite = Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.2f),100,0,SpriteMeshType.FullRect);
            for (int i=0; i<clouds.Length; ++i)
            {
                var r = new GameObject("Run dust").AddComponent<SpriteRenderer>();
                r.transform.SetParent(owner,false); r.sprite = sprite; r.sharedMaterial = reference.sharedMaterial;
                r.sortingLayerID = reference.sortingLayerID; r.sortingOrder = reference.sortingOrder-1;
                clouds[i] = r;
            }
            Clear();
        }

        public void Tick(bool running, int step, Bounds feet, float vx)
        {
            if (running && lastStep != step)
            {
                float back = -Mathf.Sign(vx);
                origins[next] = new Vector3(feet.center.x + back * feet.extents.x*.8f,feet.min.y+.02f,feet.center.z);
                directions[next] = back; born[next] = Time.time; clouds[next].enabled = true;
                next = (next+1)%clouds.Length;
            }
            lastStep = running ? step : -1;
            for (int i=0; i<clouds.Length; ++i)
            {
                var r = clouds[i]; if (!r.enabled) continue;
                float t = (Time.time-born[i])/Life;
                if (t>=1) { r.enabled=false; continue; }
                r.transform.position = origins[i] + new Vector3(directions[i]*.35f,.18f,0)*t*(2-t);
                r.transform.localScale = Vector3.one*Mathf.Lerp(.45f,1.15f,t);
                r.color = new Color(.95f,.92f,.85f,.85f*(1-t));
            }
        }
        public void Clear() { lastStep = -1; foreach (var r in clouds) r.enabled = false; }
        public void Dispose()
        {
            foreach (var r in clouds) if (r) Object.Destroy(r.gameObject);
            Object.Destroy(sprite); Object.Destroy(texture);
        }
    }
}
