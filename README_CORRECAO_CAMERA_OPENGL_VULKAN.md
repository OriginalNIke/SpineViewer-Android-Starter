# Correção de enquadramento OpenGL / Vulkan

- Cálculo de ajuste de escala compartilhado (`SpineFit`) para os dois renderizadores.
- Vulkan consulta a extensão real da swapchain, que é também a usada pelo shader nativo.
- Ao receber uma mudança de tamanho da superfície Android, o Vulkan recria seu renderizador/swapchain e reenvia as texturas no quadro seguinte.
- Mantidos o zoom e deslocamento da câmera, sem reiniciar o personagem nem a animação na troca de renderizador.
- Não altera a correção de texturas, tint e transparência do Spine 4.1.

Teste no dispositivo: alternar OpenGL/Vulkan com o personagem carregado, abrir/recolher Opções, girar/minimizar/restaurar e observar se o tamanho permanece consistente. Não foi possível executar build Android neste ambiente.
