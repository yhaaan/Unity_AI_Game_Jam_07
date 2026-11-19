using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// Dresses a stage as one closed lab room out of two pieces of art (Resources/Lab):
    ///   * lab_rail_* : a riveted rail used as the floor deck (and, flipped, as the ceiling)
    ///   * lab_pillar : a capped column, 9-sliced so it can be any height (jump walls, room walls, floor supports)
    /// Floors become rail decks carried by a row of pillars down out of frame; gaps between floors become
    /// dark shafts, pools get a floor at their bottom; both ends of the room are closed by full-height
    /// pillar walls and a rail ceiling runs over the top. Visual only: no colliders are added or changed.
    /// </summary>
    public static class LabRoomArt
    {
        public const float DeckHeight = .51f;      // the rail's native height at 100 ppu (so it tiles only sideways)
        public const float CeilingY = 5.6f;        // underside of the ceiling deck
        const float Bottom = -9f, Top = 14f;       // far enough past any camera framing

        // sorting: everything sits behind plants (3), the player (10-12) and the grass verge (16)
        const int BackingOrder = -2, DeckOrder = 0, WallOrder = 2;   // deck 0, its caps 1, contact shadows 1, standing pieces 2
        const float WallThickness = .9f;   // drawn wall; the boundary collider behind it is wider

        static readonly Color Backing = new(.17f, .13f, .27f, 1f);
        static readonly Color FloorMass = new(.36f, .3f, .5f, 1f);   // lighter than the shafts so pits read as holes
        static readonly Color ShaftDark = new(.06f, .04f, .1f, 1f);
        static readonly Color WallTint = new(.86f, .83f, .95f, 1f);

        static Sprite railLong, pillar, capLeft, capRight, white;

        static bool Load()
        {
            // sprites made at runtime die with the play session, while statics can outlive it
            // (no domain reload), so check every cached piece, not just the asset ones
            if (railLong && pillar && white && capLeft && capRight) return true;
            railLong = Resources.Load<Sprite>("Lab/lab_rail_long");
            pillar = Resources.Load<Sprite>("Lab/lab_pillar");
            var plain = Resources.Load<Sprite>("Lab/lab_rail_plain");
            if (plain && (!capLeft || !capRight))
            {
                // the long rail is a seamless middle strip; borrow the rounded lip ends of a short segment
                var r = plain.rect; float ppu = r.height / DeckHeight;
                capLeft = Sprite.Create(plain.texture, new Rect(r.x, r.y, 26, r.height), new Vector2(0f, .5f), ppu, 0, SpriteMeshType.FullRect);
                capRight = Sprite.Create(plain.texture, new Rect(r.xMax - 26, r.y, 26, r.height), new Vector2(1f, .5f), ppu, 0, SpriteMeshType.FullRect);
            }
            if (!white) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
            return railLong && pillar;
        }

        /// <summary>A walkable span: x from a to z, walking surface at y = top.</summary>
        public struct Span { public float a, z, top; public Span(float a, float z, float top) { this.a = a; this.z = z; this.top = top; } }

        /// <param name="floors">walkable floors, left to right</param>
        /// <param name="pools">liquid volumes sitting in gaps (their bounds)</param>
        public static void BuildRoom(Transform holder, IList<Span> floors, float left, float right, float wallWidth, IList<Bounds> pools)
        {
            if (!Load() || floors.Count == 0) return;
            var room = new GameObject("Lab Room").transform;
            room.SetParent(holder, false);

            foreach (var f in floors) Floor(room, f.a, f.z, f.top, true);

            // gaps between floors: a liquid basin, or an open shaft down into the dark
            for (int i = 0; i + 1 < floors.Count; i++)
            {
                float a = floors[i].z, z = floors[i + 1].a;
                if (z - a < .05f) continue;
                bool pooled = false;
                foreach (var p in pools)
                    if (p.min.x < z && p.max.x > a)
                    {
                        Floor(room, a, z, p.min.y, false);
                        pooled = true;
                    }
                if (!pooled) Quad(room, "Shaft", new Vector2(a, Bottom), new Vector2(z, floors[i].top - .02f), ShaftDark, BackingOrder);
            }

            // closing walls: slim full-height pillars whose inner face is the room edge
            Pillar(room, "Room Wall Left", new Vector2(left - WallThickness * .5f, Bottom), WallThickness, Top - Bottom, WallOrder, WallTint);
            Pillar(room, "Room Wall Right", new Vector2(right + WallThickness * .5f, Bottom), WallThickness, Top - Bottom, WallOrder, WallTint);

            // nothing of the lab beyond the walls: dark mass outside the room (seen when the camera zooms out)
            Quad(room, "Outside Left", new Vector2(left - 60f, Bottom), new Vector2(left - WallThickness * .5f, Top), Backing, BackingOrder);
            Quad(room, "Outside Right", new Vector2(right + WallThickness * .5f, Bottom), new Vector2(right + 60f, Top), Backing, BackingOrder);

            // ceiling: a backing slab with the rail flipped so its lip faces down
            Quad(room, "Ceiling Slab", new Vector2(left - wallWidth, CeilingY + .2f), new Vector2(right + wallWidth, Top), Backing, BackingOrder);
            // the rail ends inside the walls, so it never pokes out past them
            var ceiling = Deck(room, "Ceiling Rail", left - WallThickness * .5f, right + WallThickness * .5f, CeilingY + DeckHeight, false);
            ceiling.transform.localScale = new Vector3(1f, -1f, 1f);
            ceiling.transform.position = new Vector3(ceiling.transform.position.x, CeilingY + DeckHeight * .5f, 0f);
        }

        static readonly int SurfaceId = Shader.PropertyToID("_Surface"), BottomId = Shader.PropertyToID("_Bottom");
        const int LiquidOrder = 17;   // over the swimmer (10-12) and the grass verge (16): you wade *in* it

        /// <summary>Fills a pool volume with toon toxic sludge (RTD/Toxic Liquid), bubbling and popping.</summary>
        public static void ToxicPool(Transform holder, Bounds pool)
        {
            var mat = Resources.Load<Material>("Lab/ToxicLiquid");
            if (!mat || !Load()) return;
            var sr = new GameObject("Toxic Pool").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(holder, false);
            sr.sprite = white;
            sr.sharedMaterial = mat;
            sr.sortingOrder = LiquidOrder;
            // extend above the surface so blobs can swell and pop over it
            var min = new Vector2(pool.min.x, pool.min.y);
            var max = new Vector2(pool.max.x, pool.max.y + .45f);
            sr.transform.position = (Vector3)((min + max) * .5f);
            sr.transform.localScale = new Vector3(max.x - min.x, max.y - min.y, 1f);
            var block = new MaterialPropertyBlock();
            block.SetFloat(SurfaceId, pool.max.y - .05f);
            block.SetFloat(BottomId, pool.min.y);
            sr.SetPropertyBlock(block);
        }

        /// <summary>A floor slab: rail deck on top of a plain solid floor mass running down out of frame.</summary>
        static void Floor(Transform room, float a, float z, float top, bool capped)
        {
            Quad(room, "Floor Mass", new Vector2(a, Bottom), new Vector2(z, top - DeckHeight + .1f), FloorMass, BackingOrder);
            Deck(room, "Floor Rail", a, z, top, capped);
        }

        static SpriteRenderer Deck(Transform room, string name, float a, float z, float top, bool capped)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(room, false);
            sr.sprite = railLong;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(z - a, DeckHeight);
            sr.sortingOrder = DeckOrder;
            sr.transform.position = new Vector3((a + z) * .5f, top - DeckHeight * .5f, 0f);
            if (capped && capLeft && capRight)
            {
                Cap(sr.transform, capLeft, new Vector3(a - .02f, top - DeckHeight * .5f, 0f));
                Cap(sr.transform, capRight, new Vector3(z + .02f, top - DeckHeight * .5f, 0f));
            }
            return sr;
        }

        static void Cap(Transform deck, Sprite sprite, Vector3 at)
        {
            var sr = new GameObject("Rail Cap").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(deck, false);
            sr.transform.position = at;
            sr.sprite = sprite;
            sr.sortingOrder = DeckOrder + 1;
        }

        /// <summary>
        /// A lab pillar standing on <paramref name="baseCenter"/>: scaled to <paramref name="width"/>,
        /// 9-sliced so the base and cap keep their shape whatever the height.
        /// </summary>
        public const float SinkIntoDeck = .14f;   // how far a standing piece's base goes into the rail's top lip

        public static SpriteRenderer Pillar(Transform parent, string name, Vector2 baseCenter, float width, float height, int order, Color tint, bool contactShadow = false)
        {
            if (contactShadow && Load())
            {
                // a soft dark smear on the deck where the base meets it
                var shadow = new GameObject(name + " Shadow").AddComponent<SpriteRenderer>();
                shadow.transform.SetParent(parent, false);
                shadow.sprite = SoftDot();
                shadow.color = new Color(.08f, .05f, .14f, .55f);
                shadow.sortingOrder = order - 1;   // over the deck, under the piece standing on it
                shadow.transform.position = new Vector3(baseCenter.x, baseCenter.y + SinkIntoDeck - .02f, 0f);
                shadow.transform.localScale = new Vector3(width * 1.45f, .26f, 1f);
            }
            if (!Load()) return null;
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = pillar;
            sr.drawMode = SpriteDrawMode.Sliced;
            float scale = width / pillar.bounds.size.x;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            // never shorter than the base + cap themselves
            float minNative = (pillar.border.y + pillar.border.w) / pillar.pixelsPerUnit;
            sr.size = new Vector2(pillar.bounds.size.x, Mathf.Max(minNative, height / scale));
            sr.transform.position = new Vector3(baseCenter.x, baseCenter.y + sr.size.y * scale * .5f, 0f);
            sr.sortingOrder = order;
            sr.color = tint;
            return sr;
        }

        static Sprite softDot;

        /// <summary>A 1-unit soft round blob for contact shadows.</summary>
        static Sprite SoftDot()
        {
            if (softDot) return softDot;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2((x + .5f) / n * 2 - 1, (y + .5f) / n * 2 - 1).magnitude;
                    float a = Mathf.Clamp01(1 - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            softDot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            return softDot;
        }

        static void Quad(Transform room, string name, Vector2 min, Vector2 max, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(room, false);
            sr.sprite = white;
            sr.color = color;
            sr.sortingOrder = order;
            sr.transform.position = (Vector3)((min + max) * .5f);
            sr.transform.localScale = new Vector3(max.x - min.x, max.y - min.y, 1f);
        }
    }
}
