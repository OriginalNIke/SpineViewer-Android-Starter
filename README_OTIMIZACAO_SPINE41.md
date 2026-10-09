# Primeira etapa de otimização Spine 4.1

- OpenGL ES 3.0: buffers de vértices de cada lote são reaproveitados, com crescimento sob demanda, sem alocação de float[] por lote e quadro.
- Upload para GPU: ByteBuffer/FloatBuffer direto é reutilizado na thread GL; cresce apenas quando um quadro exige mais vértices.
- Fundo: reutiliza o vetor de 48 floats ao desenhar a imagem de fundo.
- Mantida a ordem de desenho e o agrupamento por página de atlas e blend mode; o comprimento efetivo dos lotes é registrado separadamente da capacidade alocada.
- Vulkan e runtime Spine 4.2 não foram modificados nesta etapa.

Validação sugerida: alternar personagens Spine 4.1, skins e animações com fundos em ambos os backends; comparar FPS e travadinhas no OpenGL. O projeto não foi compilado/testado em Android neste ambiente.
