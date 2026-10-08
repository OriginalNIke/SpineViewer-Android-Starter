# Correção CS0104 - BlendMode

Os arquivos `SpineViewer.Android/SpineGLView.cs` e `SpineViewer.Android/SpineTexturedRenderer.cs` agora declaram `using BlendMode = SpineRuntime41.BlendMode;`, evitando conflito com os enums de Android.Graphics e Microsoft.Maui.Graphics. A compilação Android deve ser validada no GitHub Actions; este pacote não foi compilado localmente.
