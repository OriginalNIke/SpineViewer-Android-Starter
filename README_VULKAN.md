# Vulkan — etapa inicial, com OpenGL preservado

Esta versão **não implementa renderização Vulkan**. Ela acrescenta um diagnóstico real
para o loader Vulkan (`libvulkan.so`) via P/Invoke, acionado pelo botão
"Verificar suporte Vulkan". O renderizador continua OpenGL ES 3.0 e mantém
clipping e modos de mistura da versão anterior.

## O que falta para a migração efetiva

1. Criar uma SurfaceView dedicada e obter `ANativeWindow` com o NDK.
2. Inicializar `VkInstance`, extensões Android, `VkPhysicalDevice`, filas,
   `VkDevice` e `VkSurfaceKHR`.
3. Criar swapchain, render pass/dynamic rendering e sincronização de frames.
4. Compilar shaders SPIR-V e criar pipelines de blend Normal, Additive,
   Multiply e Screen.
5. Fazer upload de texturas do atlas, coordenadas UV e buffers XYUV por frame.
6. Ligar o clipping CPU já existente ao pipeline Vulkan.
7. Integrar escolha de backend com fallback automático OpenGL e tratamento
   de perda de superfície, rotação e retomada de atividade.
8. Testar compilação Android e renderização real em hardware.

O loader Vulkan encontrado não garante que o dispositivo ou uma fila gráfica
possam criar uma swapchain compatível. Não confundir esta etapa de preparação
com uma migração de renderização concluída.
