# Vulkan texturizado — etapa experimental

## O que foi adicionado

- GLSL 450 -> SPIR-V via glslangValidator no GitHub Actions.
- Carregamento dos módulos SPIR-V pela camada Android/MAUI.
- Pipelines Vulkan para Normal, Additive, Multiply e Screen, usando alpha não premultiplicado.
- Upload de páginas PNG decodificadas em RGBA8888 para imagens Vulkan, com staging buffer, transições de layout, sampler linear e descriptor sets.
- Upload de vértices XYUV agrupados por página do atlas e modo de mistura, mantendo a ordem dos triângulos e o clipping já aplicado pela extração Spine 4.1.
- Comandos vkCmdBindPipeline, vkCmdBindDescriptorSets, vkCmdBindVertexBuffers, vkCmdDraw e apresentação na swapchain.
- Preview Vulkan do personagem (janela de teste) e OpenGL ES 3.0 preservado na visualização principal.

## Restrições e testes necessários

- **Experimental, compilação e execução não verificadas neste ambiente**. O GitHub Actions precisa validar tanto o NDK quanto o .NET 10 Android.
- O preview Vulkan usa a animação Spine 4.1. O runtime 4.2 não fornece triângulos texturizados nessa versão do projeto.
- A janela Vulkan é de teste; a visualização principal permanece OpenGL. Não há seleção automática/fallback transparente ainda.
- O backend sincroniza a GPU por vkQueueWaitIdle, ainda não usa fences por quadro, staging assíncrono nem recriação de swapchain ao redimensionar; não é otimizado para 60 FPS.
- Não há recuperação automática de VK_ERROR_OUT_OF_DATE_KHR, VK_SUBOPTIMAL_KHR ou perda de dispositivo.
- Páginas de atlas acima de 8192px são rejeitadas e o pool tem limite de 256 descritores; texturas atualizadas exigem reiniciar o preview.
- Transparência é straight alpha. Assets com alpha premultiplicado podem apresentar halos nas bordas.

## Teste

1. Suba todos os arquivos do ZIP para o repositório GitHub.
2. Execute Android APK (.NET 10).
3. Instale o APK arm64-v8a e carregue uma pasta Spine 4.1 com `.skel`, `.atlas` e PNG.
4. Clique em **Vulkan: testar primeiro quadro**. Deve abrir uma janela com personagem texturizado, caso a GPU e o driver aceitem os pipelines.
5. Compare a animação Vulkan com a janela principal OpenGL e envie o log em caso de erro.
