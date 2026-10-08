# Correção experimental Spine 4.0 — regiões rotacionadas

A amostra `c040_00.atlas` (Spine 4.0.47) contém regiões rotacionadas em 90, 180 e 270 graus. O runtime derivado do 4.1 calculava UVs de RegionAttachment apenas para 0/90 graus e não trocava as dimensões do atlas para 270 graus.

Mudanças **apenas no SpineViewer.Runtime40**:
- Atlas.cs: tratamento da rotação 270° como região com dimensões trocadas, assim como 90°.
- Attachments/RegionAttachment.cs: UVs para 180° e 270° e ajuste de geometria da região para 270°.

O arquivo c040 fornecido foi usado para identificar os ângulos e PMA; o pacote de personagem não foi incluído no ZIP do código.

Ainda não há compilação .NET/Android nem teste visual neste ambiente. Testar a mesma animação em Vulkan e OpenGL e manter backup da versão anterior. O runtime 4.0 continua experimental, derivado do 4.1, e outras incompatibilidades podem persistir.
