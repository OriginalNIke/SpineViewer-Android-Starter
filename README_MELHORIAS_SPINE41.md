# Melhorias Spine 4.1

- Validação de triângulos de malha (índices, UV, valores não finitos e triângulos degenerados) antes de enviá-los ao renderizador; aplica-se a OpenGL e Vulkan, que consomem a mesma geometria.
- Carregamento transacional do esqueleto 4.1: o novo esqueleto e AnimationState são preparados antes de substituir a sessão anterior.
- Validação das seleções de skin e animação contra os nomes disponíveis no personagem carregado.
- Runtime 4.2, renderizadores Vulkan/OpenGL, fundos de imagem e interface preservados.

Não foi possível executar compilação Android neste ambiente; validar com assets reais Spine 4.1.
