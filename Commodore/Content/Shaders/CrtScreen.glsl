#version 100

precision highp float;
precision mediump int;

uniform sampler2D texture;

varying vec4 color;
varying vec2 texCoord;

uniform vec2 screenSize;
uniform float scanlineDensity;
uniform float blurDistance;

vec4 blur5(sampler2D image, vec2 uv, vec2 resolution, vec2 direction) {
    vec4 color = vec4(0.0);
    vec2 off1 = vec2(1.3333333333333333) * direction;

    color += texture2D(image, uv) * 0.29411764705882354;
    color += texture2D(image, uv + (off1 / resolution)) * 0.35294117647058826;
    color += texture2D(image, uv - (off1 / resolution)) * 0.35294117647058826;
    
    return color;
}

void main()
{
    vec4 finalColor;
    vec2 coords = texCoord.xy;

    finalColor = blur5(texture, coords, screenSize, vec2(blurDistance, 0));
    finalColor += blur5(texture, coords, screenSize, vec2(-blurDistance, 0));

    float actualPixelY = coords.y * screenSize.y;
    int modulus = int(mod(actualPixelY, scanlineDensity));

    if(modulus == 0 && actualPixelY != 0.0)
    finalColor /= 1.9;
    finalColor /= 1.33;

    gl_FragColor = finalColor;
}