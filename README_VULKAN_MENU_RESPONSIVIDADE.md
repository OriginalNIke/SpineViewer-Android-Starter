# Responsividade do menu no Vulkan

O Vulkan apresenta quadros sincronicamente no temporizador da interface, ao contrário do OpenGL, que usa thread de renderização. Enquanto o menu está aberto, a taxa de apresentações Vulkan fica limitada a 20 FPS para liberar tempo da thread UI. A animação continua atualizando com o tempo real; ao fechar o menu, o limite volta a 60 FPS.

Esta é uma mitigação de responsividade, não a solução definitiva. A solução definitiva exige separar o ciclo de apresentação Vulkan da thread da interface e sincronizar corretamente o estado Spine e o ciclo de vida da superfície.

Preservados os caminhos de transparência Spine 4.1, câmera compartilhada, Vulkan/OpenGL e Spine 4.2. Não compilado/testado em dispositivo.
