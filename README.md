# SpineViewer Android — projeto inicial

## Estado real

**Protótipo de interface, não um visualizador Spine funcional ainda.**

Implementado:
- App Android .NET MAUI com tela touchscreen.
- Importação de arquivo `.json` Spine via seletor de arquivos Android.
- Leitura de nomes de skins e animações em JSON Spine (skins em objeto ou array).
- Pickers para selecionar nomes de skins e animações.
- Workflow GitHub Actions para tentar compilar um APK não assinado.

Pendente:
- Runtime Spine compatível com Android e tratamento de `.skel`.
- Importação e resolução de `.atlas` e texturas PNG.
- Renderização de malhas, attachments e texturas via OpenGL ES.
- Reprodução, atualização de skeleton e troca visual de skins.
- Testes de build, testes em dispositivos e assinatura de APK para distribuição.

## Compilar

1. Publique **o conteúdo desta pasta** em um repositório GitHub (não somente o ZIP).
2. Abra **Actions > Android APK (starter) > Run workflow**.
3. Se a compilação passar, baixe o artefato `SpineViewer-Android-unsigned`.
4. Alternativamente, use .NET SDK 8 com workload `maui-android` e execute:

```sh
dotnet publish SpineViewer.Android/SpineViewer.Android.csproj -f net8.0-android -c Release -p:AndroidPackageFormat=apk
```

O ambiente usado para preparar este ZIP não possui `dotnet`, então **o build ainda não foi verificado**. Dependências do Android SDK/workload podem precisar de ajustes no runner.

## Arquitetura planejada

- `SpineViewer.Core`: catálogo e contratos independentes da interface.
- `SpineViewer.Android`: app e acesso a arquivos.
- Próxima fase: `SpineViewer.Rendering` e `SpineViewer.Runtimes` (não implementados).

## Código original

O ZIP original SpineViewer utiliza WPF/SFML e referências x64. Não foram copiadas as implementações originais nesta primeira base, porque a integração exige remover dependências gráficas Windows, analisar licenças e adaptar o runtime. Preserve o ZIP original para a próxima etapa.
