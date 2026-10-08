# Pesquisa e navegação de skins/animações

- Pesquisa sem diferenciar maiúsculas/minúsculas, com contagem de resultados.
- Botões anterior/próximo para navegar na lista filtrada, com retorno ao início/fim.
- Ao filtrar, mantém a seleção atual quando ela ainda existe nos resultados; não altera a animação do runtime ao apenas digitar.
- O projeto conserva o renderer Vulkan/OpenGL e as correções de borda/gestos anteriores.

## Limitação
O Spine 4.2 ainda não tem geração de triângulos texturizados integrada aos renderizadores; esta alteração é apenas de interface. Não há SDK dotnet no ambiente para validar a compilação Android; execute o workflow GitHub Actions.
