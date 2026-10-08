# Spine 4.2 — Renderização texturizada experimental

- `Spine42Session.TexturedTriangles()` fornece regiões e malhas com UVs, clipping e blend modes para Vulkan e OpenGL existentes.
- O timer atualiza animações Spine 4.2, e o botão de reprodução passa a estar disponível.
- A interface de pesquisa de skins e animações foi mantida.
- Os mesmos recursos de câmera, zoom e correção de bordas continuam nos renderizadores.

## Limites de validação

Não foi possível compilar o APK neste ambiente. Teste primeiro um personagem Spine 4.2 com `.atlas`, `.png` e `.skel`/`.json` exportados na mesma versão. Confira animações, deformações, clipping, sequências e blending. Não há garantia de suporte integral a todos os recursos sem esses testes.
