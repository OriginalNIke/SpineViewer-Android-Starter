# Spine 4.0 — recuperação experimental

- Revertidas as mudanças UV de Atlas.cs e MeshAttachment.cs introduzidas na versão UVBounds.
- Ao trocar animações 4.0, o track anterior é limpo e a pose inicial é restaurada antes de aplicar a nova animação. A transição é imediata, sem crossfade.
- Exceções durante troca/atualização de animação são exibidas no status e interrompem a reprodução em vez de propagarem ao loop de interface.
- Não resolve os glitches visuais; é necessária investigação do runtime Spine 4.0 com dados de referência.
- Não evita travamentos nativos ou loops infinitos internos.
- Não compilado ou executado em Android neste ambiente.
