# Vulkan Triple Buffering + Painel de Desempenho (experimental)

- Três conjuntos independentes de command buffer, fence, semáforo de aquisição e buffer de vértices.
- Um semáforo de apresentação por imagem da swapchain; `VkPresentInfoKHR` aguarda a sinalização de renderização.
- Espera pela fence do slot antes de regravar seus recursos; espera pela fence da imagem antes de reutilizá-la.
- O upload de texturas continua serializado usando `vkDeviceWaitIdle` (apenas ao importar textura).
- Painel com apresentações por segundo e tempo médio calculado de apresentações; NÃO é GPU frame time.
- OpenGL preservado. O timer MAUI ainda roda na thread de interface; portanto 60 FPS não são garantidos.

ATENÇÃO: não compilado nem testado em Android neste ambiente. Faça backup da versão anterior.
