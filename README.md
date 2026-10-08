# SpineViewer Android — etapa de importação e prévia de texturas

Base MAUI .NET 10 com importação de Spine JSON (catálogo de skins/animações), leitura de `.atlas` (páginas/regiões), seleção de PNG e prévia estática, inspeção preliminar do cabeçalho `.skel`.

**Ainda NÃO há reprodução gráfica de animações Spine, troca visual de skins, recorte de regiões atlas ou decodificação de esqueletos binários.** O botão Reproduzir permanece desabilitado intencionalmente. A leitura de atlas é preliminar e pode não aceitar todos os formatos. A detecção da versão `.skel` é apenas heurística.

## Próxima etapa técnica

Integrar um runtime Spine licenciado/compatível com Android e a versão exportada do arquivo; fornecer carregamento de SkeletonData (JSON/binário), AtlasAttachmentLoader, AnimationState, cálculo de world transforms, clipping, mesh e renderizador GPU para atlas. Verificar as licenças antes de redistribuir código/runtime do SpineViewer original.

## Compilação

Enviar o conteúdo da pasta raiz ao GitHub e executar `.github/workflows/android-apk.yml`. SDK e workload .NET 10 Android são necessários. Não foi possível compilar neste ambiente.
