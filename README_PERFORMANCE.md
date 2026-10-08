# SpineViewer Android — correção de desempenho

- Remove a visualização azul dos ossos e a prévia estática do PNG.
- Mostra apenas a renderização texturizada Spine 4.1.
- Pausa o trabalho de animação e redesenho quando a reprodução está parada.
- Usa tempo real entre quadros e limite aproximado de 30 FPS.
- Reutiliza shaders e SKPaint entre quadros, evitando sua criação por triângulo.
- Libera texturas e shaders quando a view é desconectada.

## Ainda pendente

A extração de geometria ainda aloca triângulos a cada quadro. Uma otimização maior exigirá buffers reutilizáveis e desenho por lotes (batching) por atlas sem alterar a ordem dos slots. O renderizador 4.2 ainda não está integrado.

A compilação do APK deve ser validada no GitHub Actions.
