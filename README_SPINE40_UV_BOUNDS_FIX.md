# Spine 4.0 — correção de bounds UV do atlas

O atlas `c040_00.atlas` usa `bounds` com regiões giradas 90/180/270 graus. Em `Runtime40/Atlas.cs`, os limites UV de regiões 90/270 eram calculados com a largura e a altura trocadas, causando amostragem de pixels de outras regiões. Agora os limites são calculados a partir do retângulo `bounds` real. Em `MeshAttachment.cs`, o tamanho da página é lido de `region.page` em vez de inferido a partir da região.

Alterações restritas ao runtime 4.0. Não houve alteração no blending global ou nos runtimes 4.1/4.2. Ainda requer compilação Android e teste visual com `c040` em OpenGL e Vulkan. O atlas `pma:true` não foi alterado nesta etapa: o decoder Android entrega pixels RGBA não pré-multiplicados para o pipeline existente.
