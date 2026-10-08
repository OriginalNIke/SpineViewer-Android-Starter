#version 450
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUV;
layout(push_constant) uniform View { vec2 center; vec2 halfViewport; float scale; } view;
layout(location=0) out vec2 uv;
void main() {
    vec2 clip = ((aPos - view.center) * view.scale) / view.halfViewport;
    gl_Position = vec4(clip, 0.0, 1.0);
    uv = aUV;
}
