# OpenGL ES 3.0 — migração experimental

A visualização principal usa agora GLSurfaceView nativa Android com shaders GLSL ES 3.0, textura carregada na GPU e batching por página consecutiva do atlas. O runtime Spine 4.1 continua responsável pelos triângulos; Spine 4.2 ainda não possui renderização texturizada.

## Atenção
- Este é um primeiro protótipo de migração, **não** uma versão validada: não foi possível compilar o APK aqui.
- O código ainda aloca arrays e buffers na atualização/desenho; a meta de 60 FPS não foi verificada.
- O código não implementa clipping, cores por slot, blend modes Spine ou recuperação completa de contexto GL.
- Para desempenho real: reutilizar VBOs persistentes e atualizar com glBufferSubData, remover cópias de arrays, tratar contexto perdido, e usar sincronização VSync.
- O backend SkiaSharp original permanece no projeto como fallback de referência, mas não é exibido na interface.

## Teste
Execute o workflow GitHub Actions, envie erros de compilação caso ocorram e teste importação de pasta com char000296. Compare imagem, proporções, orientação e FPS com a versão SkiaSharp.
