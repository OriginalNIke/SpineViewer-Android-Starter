# Importação por pasta (Android)

Use **Selecionar pasta do personagem** e conceda acesso à pasta pelo seletor nativo Android (Storage Access Framework). O aplicativo procura um `.skel` **ou** `.json`, um `.atlas` e os PNGs correspondentes na pasta selecionada (nível atual, sem subpastas). Após validar os nomes das páginas do atlas, carrega o runtime e preenche skins/animações automaticamente.

A pasta deve conter apenas um conjunto de personagem. Os botões de importação individual continuam disponíveis.

O código ainda não foi compilado no Android; valide no GitHub Actions. O renderizador texturizado continua limitado ao Spine 4.1.
