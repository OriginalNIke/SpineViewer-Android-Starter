# Seleção de renderizador (etapa incremental)

- Adicionado seletor OpenGL/Vulkan na tela principal.
- Vulkan continua em SurfaceView dentro de diálogo, não substitui ainda o controle OpenGL embutido.
- Falha de inicialização Vulkan retorna o seletor ao OpenGL.
- Indicador mede a taxa de atualização da animação na thread UI; **não** mede FPS real da GPU.
- Mantidos o backend Vulkan, pipelines e runtime originais.

## Pendente

Incorporar a SurfaceView Vulkan ao layout MAUI e sincronizar a troca de renderizadores com o ciclo de vida da superfície; instrumentar timestamps reais de apresentação e reduzir alocações por quadro.

A compilação Android não foi validada neste ambiente.
