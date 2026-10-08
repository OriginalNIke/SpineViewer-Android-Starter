# Importação recursiva de subpastas (Spine 4.1 / 4.2)

- O seletor Android SAF percorre pastas e subpastas (até 24 níveis), sem permissão de armazenamento irrestrita.
- Caminhos relativos são preservados para evitar colisões entre arquivos homônimos.
- Com múltiplos esqueletos, escolha qual carregar; um esqueleto é exibido por vez.
- O atlas é escolhido por proximidade ao esqueleto e nome-base; em caso de empate, o usuário escolhe.
- Cada página do atlas busca primeiro a PNG relativa à pasta do atlas; apenas um nome de arquivo globalmente único pode servir como fallback. Duplicatas ambíguas causam erro explicativo.
- OpenGL e Vulkan recebem somente as texturas do atlas selecionado, com as chaves de página originais.
- Não modifica runtimes 4.1/4.2 nem os backends gráficos.

**Limitações:** Não combina múltiplos esqueletos em um único personagem. Não altera nomes internos de skins e animações. A versão ainda precisa ser compilada e testada no dispositivo.
