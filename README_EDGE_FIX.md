# Correção experimental de bordas e arraste vertical

- Vulkan: sinal de PanY alinhado ao OpenGL, de modo que arrastar para cima mova o personagem para cima.
- Vulkan e OpenGL: agora recebem os mesmos pixels RGBA com alpha não pré-multiplicado, evitando diferenças entre `GLUtils.TexImage2D` e o upload nativo.
- RGB de texels completamente transparentes é expandido até quatro pixels vizinhos para reduzir halos causados por filtragem linear no atlas. A transparência e os pixels visíveis são preservados.
- Não modifica shaders, clipping, animações, triple buffering ou o zoom.

**Atenção:** o efeito de borda pode também vir de imagens do atlas, UVs ou dados de attachment. Este patch corrige uma causa comum, mas precisa de validação visual no aparelho. Não foi compilado no Android neste ambiente.
