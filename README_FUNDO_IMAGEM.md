# Fundo com imagem

Selecionar imagem PNG/JPG/WebP (máximo 20 MB, 4096 x 4096), armazenada em FileSystem.AppDataDirectory e restaurada ao iniciar. Ajuste: preencher a tela com corte central, preservando proporção. Remover imagem restaura fundo de cor sólida. OpenGL desenha quad texturizado antes dos personagens; Vulkan adiciona batch de fundo antes dos batches Spine com textura nativa. Zoom/pan não afetam a imagem de fundo. Spine 4.1/4.2 e renderizadores preservados. Ainda requer compilação e teste no Android.
