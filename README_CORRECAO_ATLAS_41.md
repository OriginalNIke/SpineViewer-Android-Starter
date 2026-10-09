# Correção atlas Spine 4.1 — char001207

O atlas enviado usa regiões com `rotate:90`, `rotate:180` e `rotate:270`.

- Atlas.cs: aplica o mesmo tratamento de dimensões e UVs para 270° e 90°.
- RegionAttachment.cs: aplica mapeamento de UVs para 90°, 180° e 270° e dimensões ajustadas para regiões de 270°.
- Os renderizadores Vulkan e OpenGL usam a mesma geometria do runtime 4.1.

Atenção: correção focada nas regiões giradas. Não foi possível validar a aparência do personagem em um dispositivo Android nem garantir que todo flicker de malhas esteja resolvido.
