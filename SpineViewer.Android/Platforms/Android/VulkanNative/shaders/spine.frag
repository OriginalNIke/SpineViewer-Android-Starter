#version 450
layout(set=0,binding=0) uniform sampler2D atlasPage;
layout(location=0) in vec2 uv;
layout(location=1) in vec4 tint;
layout(location=0) out vec4 outColor;
void main() {
    outColor = texture(atlasPage, uv) * tint;
    if (outColor.a < 0.0039) discard;
}
