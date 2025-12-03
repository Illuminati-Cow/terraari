sampler uImage0 : register(s0); 
sampler uImage1 : register(s1); 
float uTime; 

static const int COLOR_COUNT = 4;
static const float4 colors[COLOR_COUNT] = {
    float4(254.0/255.0, 157.0/255.0, 253.0/255.0, 1.0),
    float4(249.0/255.0, 241.0/255.0, 234.0/255.0, 1.0),
    float4(195.0/255.0, 253.0/255.0, 254.0/255.0, 1.0),
    float4(205.0/255.0, 197.0/255.0, 254.0/255.0, 1.0)
};

float4 lerp(float4 a, float4 b, float t) {
    return a + (b - a) * t;
}

bool isGreen(float4 c) {
    return c.r < 0.05 && c.g > 0.7 && c.b < 0.05;
}

float4 ArmorBasic(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0 { 
    // Get specific index
    // Get position within index
    // Set color to lerp

    float4 colorSample = tex2D(uImage0, coords);

    if (!isGreen(colorSample)) {
        return colorSample;
    }

    float gradientScale = 3.0;
    float gradientSpeed = 1.0/3.0;

    float sectionSize = 1.0/COLOR_COUNT;
    float currentSection = fmod((coords.x/gradientScale + uTime*gradientSpeed)/sectionSize, COLOR_COUNT);
    float sectionProgress = fmod(currentSection, 1.0);
    float4 color = lerp(colors[int(currentSection)], colors[int(currentSection+1.0)%COLOR_COUNT], sectionProgress);
    return color;
} 
     
technique Technique1 
{ 
    pass ArmorBasic 
    { 
        PixelShader = compile ps_2_0 ArmorBasic(); 
    } 
}

/*
// float4 texColor = tex2D(uImage0, coords);
    // float2 centered = (coords - 0.5) * 2.0;
    // float dist = length(centered);
    
    // Use x coordinate for horizontal gradient (change to uv.y for vertical)
    float t = coords.x;
    
    // Calculate which color segment we're in
    float segmentSize = 1.0 / float(COLOR_COUNT);
    int segmentIndex = int(floor(t / segmentSize));
    
    // Clamp to valid range
    segmentIndex = clamp(segmentIndex, 0, COLOR_COUNT - 1);
    
    // Calculate local t within this segment (0 to 1)
    float localT = (t - float(segmentIndex) * segmentSize) / segmentSize;
    
    // Interpolate between the two colors
    float4 color = lerp(colors[segmentIndex%COLOR_COUNT], colors[(segmentIndex + 1)%COLOR_COUNT], localT);

    // if (coords.y < 0.8) {
    //     int offset = int(coords.y*10.0);
    //     return colors[(segmentIndex + offset)%COLOR_COUNT];
    // } else if (coords.y < 0.9) {
    //     return colors[8%COLOR_COUNT];
    // } else {
    //     return float4(0.0, 1.0, 0.0, 1.0);
    // }

    int xidx = int(coords.x/(1.0/4.0));
    // return colors[(xidx + yidx) % COLOR_COUNT];

    if (coords.y < 0.1) {
        return colors[(xidx + 0) % COLOR_COUNT];
    } else if (coords.y < 0.2) {
        return colors[(xidx + 1) % COLOR_COUNT];
    } else if (coords.y < 0.3) {
        return colors[(xidx + 2) % COLOR_COUNT];
    } else if (coords.y < 0.8) {
        return colors[1%COLOR_COUNT];
    } else {
        return float4(1.0, 0, 0, 1.0);
    }
    
    return color;


     /*
    return float4(colors[(segmentIndex + 2)%COLOR_COUNT], 1.0);
    return float4(colors[(segmentIndex + 2)%COLOR_COUNT], 1.0);
     */

    // if (dist > 1.0)
    //     return float4(0, 0, 0, 0);
    
    // if (dist > 0.8)
    //     return float4(1, 0, 0, 1);

    // return tex2D(uImage1, float2(0.9, 0.9));
