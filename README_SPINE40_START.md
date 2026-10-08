# Início do suporte Spine 4.0 (experimental)

- Runtime 4.0 isolado em `SpineViewer.Runtime40`, inicialmente derivado do runtime 4.1. **Não é ainda uma implementação oficial fiel ao formato 4.0.**
- Detecta a versão de arquivos JSON (`skeleton.spine`) e `.skel` (cabeçalho) e escolhe runtime 4.0, 4.1 ou 4.2.
- Sessão 4.0 e extração de geometria são independentes; compartilham a interface de triângulos Vulkan/OpenGL.
- Próxima etapa obrigatória: adaptar as diferenças reais de parser JSON/binário e timelines do 4.0 usando amostras reais, além de compilar e testar.
- Mantenha backup do APK anterior.
