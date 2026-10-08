# SpineViewer Android — Spine 4.1 + 4.2 (experimental)

Esta atualização inclui o runtime 4.1 do SpineViewer original e seleção automática entre os runtimes 4.1 e 4.2 para arquivos `.skel` com versão identificável. Para o exemplo `char000296.skel`, a versão encontrada é **4.1.11**.

## Como testar
1. Importe `char000296.atlas` e `char000296.skel` usando os botões.
2. Importe `char000296.png` para a prévia de textura.
3. Toque em **Carregar runtime Spine 4.1 / 4.2**.
4. Se o runtime conseguir decodificar o esqueleto, escolha skin e animação; toque em Reproduzir para visualizar os **ossos**.

## Limitações importantes
- **Ainda não desenha o personagem texturizado**, apenas o esqueleto em linhas; a imagem PNG é uma prévia separada.
- A importação de múltiplos arquivos de uma só vez e o carregamento de ZIP dentro do aplicativo ainda não estão implementados.
- A detecção automática nesta etapa se concentra no cabeçalho `.skel`; JSON segue o runtime 4.2.
- A compilação Android não foi executada neste ambiente; validar no GitHub Actions.
- Verifique as condições de licença do Spine Runtime antes da redistribuição.
