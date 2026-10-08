# Vulkan: sincronização e contador de apresentações (experimental)

- Adicionado VkFence ao envio do quadro. A GPU conclui o uso do buffer antes que o próximo quadro o sobrescreva.
- Removido vkQueueWaitIdle do caminho de desenho por quadro; continua no upload de texturas e desligamento.
- O contador Vulkan agora contabiliza chamadas vkQueuePresentKHR com retorno de sucesso/suboptimal, por segundo. **Não é medição de frames efetivamente exibidos pelo compositor nem de tempo de GPU**.
- OpenGL mantém indicador de atualizações da animação, não FPS de apresentação.
- O buffer de vértices já era reutilizado por capacidade; preservado. Texturas continuam em cache no backend.

## Limitações

Esta é sincronização de **um frame em voo**. Double/triple buffering exigirá buffers e command buffers independentes, semáforos de renderização e controle de imagens da swapchain. Não se deve remover a espera pela fence sem isso.

A compilação Android/NDK e execução no aparelho não foram validadas neste ambiente.
