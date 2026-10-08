# Vulkan: redução de alocações por quadro (experimental)

Esta versão mantém o triple buffering nativo existente. Em `VulkanPreviewCallback.Render`, os arrays gerenciados de vértices, contagens, modos de blend e ponteiros de páginas passam a crescer conforme necessário e são reutilizados. Os nomes UTF-8 das páginas são alocados uma única vez por superfície e liberados em `Release`.

O formato JNI e a ordem dos batches não mudaram. Não foi adicionada sincronização nova nem alterado o backend OpenGL. A atualização não elimina as alocações que possam ocorrer na geração de `SpineTriangle` pelo runtime; essa é uma próxima área de análise.

Atenção: ainda não compilado/testado no dispositivo. Compile com o workflow atual, preserve o APK anterior e compare as mesmas animações antes/depois. O indicador Vulkan conta apresentações aceitas, não necessariamente quadros efetivamente exibidos pelo compositor.
