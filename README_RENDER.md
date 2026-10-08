# SpineViewer Android — renderização texturizada experimental (Spine 4.1)

Esta etapa adiciona um renderizador SkiaSharp de triângulos texturizados para o runtime Spine 4.1. A cada quadro, os attachments `RegionAttachment` e `MeshAttachment` são transformados em coordenadas mundiais, incluindo os pesos/deformações calculados pelo runtime, e desenhados em ordem de slots. O carregamento de PNG agora guarda a textura por nome de página do atlas.

## Teste

Importe `char000296.atlas`, `char000296.png` e `char000296.skel`, toque em carregar runtime, selecione uma animação e reproduza. O visualizador texturizado aparece acima da prévia de ossos. Para atlas com múltiplas páginas, importe cada PNG.

## Limitações conhecidas

- **Experimental e não compilado neste ambiente**: a integração SkiaSharp e sua API precisam ser verificadas no GitHub Actions.
- Somente o runtime **4.1** alimenta a geometria texturizada; 4.2 ainda mostra ossos.
- Clipping attachments, blend modes, tinting e PMA não são tratados; alguns personagens podem exibir artefatos.
- Renderização atual cria vértices por triângulo por frame, priorizando simplicidade, não desempenho.
- O pacote SkiaSharp.Views.Maui.Controls 3.119.1 depende de restauração NuGet no GitHub.
- A licença dos Spine Runtimes deve ser respeitada ao redistribuir o app.

## Build

`dotnet publish SpineViewer.Android/SpineViewer.Android.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk -p:AndroidKeyStore=false`
