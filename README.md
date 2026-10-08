# SpineViewer Android — Runtime 4.2 / Stage 2

## Implementado
- Incluído o código original SpineRuntime42 (4.2.74) proveniente do ZIP fornecido, em projeto .NET 10 sem dependência x64.
- Leitura pelo runtime 4.2 de JSON ou `.skel` **com** `.atlas` (não somente inspeção do cabeçalho).
- `Skeleton`, `AnimationState`, troca de skins e reprodução por timer de ~30 FPS.
- Visualização gráfica **de depuração dos ossos animados** com `GraphicsView` (não mostra o personagem texturizado).
- Importação PNG anterior preservada como prévia separada.

## Limitações importantes
- **Ainda não há renderização de sprites/meshes texturizados**, nem upload de texturas para GPU. `DeferredTextureLoader` apenas fornece metadados do atlas para o runtime.
- O runtime 4.2 exige assets compatíveis 4.2; versões antigas não são aceitas automaticamente.
- Arquivos `.atlas` e `.skel` podem falhar se forem incompatíveis, incompletos ou tiverem formatos diferentes.
- Sem SDK Android no ambiente de criação: **compilação não verificada**. Execute o workflow GitHub Actions e forneça o log.
- Antes de redistribuir o APK, revise a licença Spine Runtimes incluída nos arquivos `.cs` e as condições do Spine Editor.

## Uso
Importe `.atlas` e `.json`/`.skel` versão 4.2; toque em **Carregar runtime Spine 4.2**, escolha animação/skin e toque em **Reproduzir**. A área com linhas ciano é a prévia esquelética.

## Próxima etapa
Renderizador Android com textura, recortes UV, malhas, ordem de desenho e blending dos slots.
