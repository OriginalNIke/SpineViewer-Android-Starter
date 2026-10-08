# Personalização do fundo

No menu Opções > Fundo da animação escolha azul escuro, preto, cinza escuro, cinza claro, branco, verde, azul ou Personalizado. Para uma cor personalizada, digite #RRGGBB e toque Aplicar cor. A preferência é persistida e restaurada ao reiniciar. OpenGL limpa a superfície com a cor escolhida; Vulkan passa a cor ao renderizador nativo por `spine_vk_set_background` e a usa no `VkClearValue` do render pass. Mantidos Spine 4.1/4.2, OpenGL ES 3.0 e Vulkan. Necessário recompilar a biblioteca nativa Vulkan com o projeto. Não testado em dispositivo.
