# Batching experimental (Spine 4.1)

- Agrupa triângulos consecutivos por página do atlas, preservando a ordem de desenho.
- Reutiliza buffers de geometria de triângulos e coordenadas calculadas.
- Elimina arrays temporários por triângulo.
- Limite de timer alterado de 33 ms para 16 ms (alvo, não garantia de 60 FPS).

## Limitações

SkiaSharp SKVertices.CreateCopy exige arrays com tamanho exato; ainda há duas alocações por lote e um objeto nativo por lote/quadro. O cálculo dos limites continua por quadro. Sem GPU surface e VSync, 60 FPS sustentados não estão garantidos. O agrupamento por página não pode atravessar páginas intercaladas sem mudar a ordem de sobreposição. Clipping e blending Spine ainda precisam de suporte. Não foi possível compilar o APK neste ambiente.
