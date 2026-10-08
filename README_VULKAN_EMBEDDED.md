# Vulkan integrado à tela principal (experimental)

- `SpineVulkanView` e `SpineVulkanHandler` criam um `SurfaceView` dentro do layout MAUI.
- O seletor alterna a visibilidade das superfícies Vulkan e OpenGL, sem trocar o runtime Spine carregado.
- `VulkanPreviewCallback` reutiliza o backend Vulkan e envia as malhas e texturas já existentes.
- Em falhas reportadas de inicialização, o seletor retorna ao OpenGL.
- Não há mais janela Dialog de pré-visualização.

## Limitações

- A compilação .NET/NDK e o comportamento de ciclo de vida da superfície devem ser testados no Android.
- Ainda não há contador de frames realmente apresentados pela GPU.
- Falhas nativas abruptas não podem ser recuperadas pelo fallback gerenciado.
- O pipeline Vulkan e sua sincronização continuam experimentais.
