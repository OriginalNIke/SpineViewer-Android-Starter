# Carregamento automático Spine 4.1 / 4.2

- Removido o botão manual de carregamento de runtime.
- Ao selecionar uma pasta, o primeiro personagem é carregado automaticamente; ao mudar no seletor, a sessão é recarregada com o esqueleto, atlas e PNGs correspondentes.
- Em importações individuais, o runtime carrega automaticamente assim que esqueleto, atlas e todas as páginas de textura estiverem disponíveis.
- Arquivos binários com versão detectada diferente de 4.1 ou 4.2 são rejeitados com mensagem clara.
- Evita eventos de troca simultâneos durante o carregamento.

Não compilado/testado no Android neste ambiente.
