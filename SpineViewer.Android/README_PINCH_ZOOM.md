# Zoom e movimento com gestos (experimental)

- Dois dedos: zoom de 0,25x a 5x com foco no ponto entre os dedos.
- Um dedo: arrastar o personagem.
- Dois toques: restaurar câmera.
- Estado de câmera compartilhado entre Vulkan e OpenGL ES 3.0.
- Gestos solicitam redesenho mesmo com animação pausada.

Nota: o enquadramento automático ainda usa os limites da animação a cada quadro.
A câmera adiciona zoom e deslocamento a esse enquadramento.
Compilação Android e teste físico pendentes.
