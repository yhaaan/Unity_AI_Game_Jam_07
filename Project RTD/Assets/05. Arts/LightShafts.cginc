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

float ShaftMask (float2 world, float t)
{
    float top = _ShaftView.y;
    float drop = max(0, top - world.y);
    // trace the pixel back up the slant to where its beam leaves the ceiling
    float x0 = world.x - drop * _ShaftSlant - _ShaftView.x * _ShaftParallax;
    x0 += sin(t * 0.21) * 0.15;                               // the source sways a touch
    float beams = ShaftOctave(x0, _ShaftSpacing, 0, t) + 0.6 * ShaftOctave(x0, _ShaftSpacing * 0.47, 11.3, t * 1.3);
    float fall = saturate(1 - drop / max(0.01, _ShaftView.z * _ShaftReach));
    return saturate(beams) * fall * fall;
}

#endif
