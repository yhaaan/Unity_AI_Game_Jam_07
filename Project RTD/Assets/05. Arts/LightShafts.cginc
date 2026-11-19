// Shared beam mask for the diagonal ceiling light: RTD/Light Shafts draws the beams,
// RTD/Shaft Dust uses the same mask so dust only sparkles where the light falls.
// StageAtmosphere sets _ShaftView every frame: (camera x, view top y, view height, unused).
#ifndef RTD_LIGHT_SHAFTS_INCLUDED
#define RTD_LIGHT_SHAFTS_INCLUDED

float4 _ShaftView;
float _ShaftSlant;      // horizontal drift per unit of drop (beams lean down-right)
float _ShaftSpacing;    // average gap between beams, world units
float _ShaftParallax;   // 0 = beams fixed to the room, 1 = fixed to the screen
float _ShaftReach;      // fraction of the view height the light travels before fading out

float ShaftHash (float n) { return frac(sin(n * 91.345) * 47453.5453); }

// one octave of beams: each cell holds at most one beam with its own width, offset and strength
float ShaftOctave (float x0, float spacing, float seed, float t)
{
    float cellPos = x0 / spacing;
    float cell = floor(cellPos);
    float h = ShaftHash(cell + seed);
    float h2 = ShaftHash(cell * 1.7 + seed + 3.1);
    if (h < 0.3) return 0;                                   // some cells stay dark: irregular rhythm
    float center = 0.25 + 0.5 * h2;
    float width = lerp(0.05, 0.2, ShaftHash(cell + seed + 7.7));
    float d = abs(frac(cellPos) - center);
    float beam = 1 - smoothstep(width * 0.35, width, d);    // soft-edged band
    float breathe = 0.75 + 0.25 * sin(t * (0.35 + h * 0.4) + h2 * 6.283);
    return beam * lerp(0.45, 1.0, h) * breathe;
}

// beam strength at a point, ignoring how far it is below the ceiling; xShift slides the sample sideways
float ShaftBeams (float2 world, float t, float xShift)
{
    float drop = max(0, _ShaftView.y - world.y);
    // trace the pixel back up the slant to where its beam leaves the ceiling
    float x0 = world.x + xShift - drop * _ShaftSlant - _ShaftView.x * _ShaftParallax;
    x0 += sin(t * 0.21) * 0.15;                               // the source sways a touch
    return saturate(ShaftOctave(x0, _ShaftSpacing, 0, t) + 0.6 * ShaftOctave(x0, _ShaftSpacing * 0.47, 11.3, t * 1.3));
}

// 1 right under the ceiling, 0 once the light has travelled its reach
float ShaftFall (float2 world)
{
    float drop = max(0, _ShaftView.y - world.y);
    return saturate(1 - drop / max(0.01, _ShaftView.z * _ShaftReach));
}

float ShaftMask (float2 world, float t)
{
    float fall = ShaftFall(world);
    return ShaftBeams(world, t, 0) * fall * fall;
}

#endif
