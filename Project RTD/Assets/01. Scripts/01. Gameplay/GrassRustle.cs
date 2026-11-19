using System.Collections.Generic;
using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// Plays a soft rustle while the player wades through the front grass verge that StageDressing plants:
    /// one rustle per stride while moving through it, and a fuller one when landing in it.
    /// </summary>
    public sealed class GrassRustle : MonoBehaviour
    {
        const float Stride = .55f;          // world units walked between rustles
        const float MinSpeed = .6f;         // slower than this is standing still

        Transform player;
        Rigidbody2D body;
        readonly List<Vector3> tufts = new();   // x-min, x-max, ground y
        float travelled, lastX;
        bool wasInGrass;

        public void Init(Transform player, List<Vector3> grass)
        {
            this.player = player;
            body = player.GetComponent<Rigidbody2D>();
            tufts.AddRange(grass);
            lastX = player.position.x;
        }

        void Update()
        {
            if (!player) return;
            Vector2 p = player.position;
            bool inGrass = InGrass(p);
            float vx = body ? body.linearVelocity.x : (p.x - lastX) / Mathf.Max(Time.deltaTime, 1e-4f);
            float vy = body ? body.linearVelocity.y : 0f;

            if (inGrass && !wasInGrass && vy < -2f)
            {
                SoundManager.Play(Sfx.Rustle, .7f);   // dropped into the grass
                travelled = 0f;
            }
            else if (inGrass && Mathf.Abs(vx) > MinSpeed)
            {
                travelled += Mathf.Abs(p.x - lastX);
                if (travelled >= Stride)
                {
                    travelled = 0f;
                    SoundManager.Play(Sfx.Rustle, Mathf.Lerp(.3f, .55f, Mathf.InverseLerp(1f, 6f, Mathf.Abs(vx))));
                }
            }
            else if (!inGrass) travelled = Stride * .6f;   // first stride into grass rustles quickly

            wasInGrass = inGrass;
            lastX = p.x;
        }

        bool InGrass(Vector2 p)
        {
            foreach (var t in tufts)
                // the player's pivot sits about a body-height above its feet
                if (p.x >= t.x && p.x <= t.y && p.y > t.z - .2f && p.y < t.z + 1.3f) return true;
            return false;
        }
    }
}
