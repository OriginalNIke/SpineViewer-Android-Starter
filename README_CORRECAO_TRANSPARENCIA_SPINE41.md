# Correção de cores e transparência Spine 4.1

- Propaga cor e alfa combinados do esqueleto, slot e attachment por vértice.
- Aplica a cor e a transparência nos shaders OpenGL ES 3.0 e Vulkan.
- Mantém o desenho dos triângulos na ordem original de slots.
- Usa fatores de blend separados para RGB e alfa no OpenGL, em paridade com Vulkan.
- Imagem de fundo mantém cor branca e alfa 1 para não receber a modulação do personagem.
- Shader SPIR-V é recompilado pelo workflow GitHub Actions (glslangValidator).

Não altera o runtime 4.2 nem garante, isoladamente, a correção da faixa branca; requer teste em dispositivo.
