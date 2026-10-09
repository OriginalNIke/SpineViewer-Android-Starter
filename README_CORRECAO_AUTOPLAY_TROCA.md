# Correção: animação não inicia após trocar personagem

A interface selecionava a primeira animação no Picker, mas o `AnimationState` recém-criado não recebia `SetAnimation` porque os eventos de seleção eram suprimidos durante a atualização dos catálogos. O timer ficava ativo, porém a trilha de animação estava vazia.

Agora `LoadRuntimeCoreAsync` atribui explicitamente a animação inicial, aplica o primeiro quadro (`Step(0f)`) e invalida o renderizador ativo (Vulkan ou OpenGL). A mudança vale para Spine 4.1 e 4.2. Nenhuma alteração foi feita no código nativo dos renderizadores.

Compilação Android e testes em aparelho ainda são necessários.
