# Spine 4.1: clipping e modos de mistura (experimental)

- SkeletonClipping do runtime Spine 4.1 recorta triângulos e interpola UVs antes de enviar à GPU.
- O clipping é iniciado por ClippingAttachment e encerrado conforme o slot final definido no runtime.
- Os triângulos carregam BlendMode do slot. O batching preserva a ordem e separa lotes por textura e modo.
- OpenGL ES aplica Normal, Additive, Multiply e Screen por lote.
- Não foi executada compilação .NET/Android neste ambiente. Validar no GitHub Actions e com char000296.
- Atenção: mistura com alpha premultiplicado, tint de slots e clipping de outros runtimes ainda requerem validação; este estágio cobre somente Spine 4.1.
