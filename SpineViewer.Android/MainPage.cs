using SpineViewer.Core;
namespace SpineViewer.Android;
public sealed class MainPage : ContentPage
{
    readonly Label status = new() { Text = "Importe JSON, atlas e texturas para começar.", TextColor = Colors.LightGray };
    readonly Label info = new() { Text = "Prévia da textura (renderização esquelética pendente)", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center };
    readonly Image texture = new() { Aspect = Aspect.AspectFit, HeightRequest = 260 };
    readonly Picker skins = new() { Title = "Selecionar skin", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Picker animations = new() { Title = "Selecionar animação", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Label atlasInfo = new() { Text = "Nenhum atlas importado", TextColor = Colors.LightGray };
    readonly Button play = new() { Text = "▶ Reproduzir (aguardando runtime)", IsEnabled = false };
    readonly List<string> selectedTextures = new();
    readonly SpineTexturedRenderer texturedView = new();
    AtlasCatalog? atlas;
    string? atlasText;
    byte[]? skeletonContent;
    bool skeletonBinary;
    readonly Spine42Session runtime = new();
    readonly Spine41Session runtime41 = new();
    bool use41;
    ISpinePlayback Active => use41 ? runtime41 : runtime;
    readonly SkeletonDebugDrawable debugDrawable = new();
    readonly GraphicsView skeletonView = new() { HeightRequest = 290 };
    bool playing;
    IDispatcherTimer? playbackTimer;
    public MainPage()
    {
        Title = "SpineViewer Android";
        BackgroundColor = Color.FromArgb("#111827");
        var json = new Button { Text = "Importar JSON Spine" };
        json.Clicked += ImportJson;
        var atlasButton = new Button { Text = "Importar .atlas" };
        atlasButton.Clicked += ImportAtlas;
        var imageButton = new Button { Text = "Importar textura PNG" };
        imageButton.Clicked += ImportTexture;
        var binaryButton = new Button { Text = "Inspecionar .skel" };
        binaryButton.Clicked += ImportSkel;
        debugDrawable.GetLines = () => Active.BoneLines();
        skeletonView.Drawable = debugDrawable;
        texturedView.GetTriangles = () => use41 ? runtime41.TexturedTriangles() : Array.Empty<SpineTriangle>();
        var loadRuntime = new Button { Text = "Carregar runtime Spine 4.1 / 4.2" };
        loadRuntime.Clicked += LoadRuntime;
        play.Text = "▶ Reproduzir";
        play.Clicked += (_, _) => { playing = !playing; play.Text = playing ? "⏸ Pausar" : "▶ Reproduzir"; };
        playbackTimer = Dispatcher.CreateTimer();
        playbackTimer.Interval = TimeSpan.FromMilliseconds(33);
        playbackTimer.Tick += (_, _) => { if (!playing || !Active.IsLoaded) return; Active.Step(0.033f); skeletonView.Invalidate(); texturedView.InvalidateSurface(); };
        playbackTimer.Start();
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 24), Spacing = 14,
            Children = { new Label { Text = "SpineViewer Android", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                json, atlasButton, imageButton, binaryButton, loadRuntime, texturedView, skeletonView,
                new Border { Stroke = Color.FromArgb("#374151"), BackgroundColor = Color.FromArgb("#1F2937"), Padding = 12,
                    Content = new VerticalStackLayout { Children = { texture, info } } },
                atlasInfo, new Label { Text = "Skins", TextColor = Colors.White }, skins,
                new Label { Text = "Animações", TextColor = Colors.White }, animations, play, status }
        }};
        skins.SelectedIndexChanged += (_, _) => { if (skins.SelectedItem is not string s) return; Active.SetSkin(s); skeletonView.Invalidate(); texturedView.InvalidateSurface(); status.Text = $"Skin: {s}"; };
        animations.SelectedIndexChanged += (_, _) => { if (animations.SelectedItem is not string a) return; Active.SetAnimation(a); skeletonView.Invalidate(); texturedView.InvalidateSurface(); status.Text = $"Animação: {a}"; };
    }
    async void ImportJson(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione JSON Spine" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Selecione um .json.");
            using var stream = await file.OpenReadAsync(); using var reader = new StreamReader(stream);
            var jsonText = await reader.ReadToEndAsync();
            skeletonContent = System.Text.Encoding.UTF8.GetBytes(jsonText); skeletonBinary = false;
            var catalog = SpineJsonCatalog.Read(jsonText, file.FileName);
            skins.ItemsSource = catalog.Skins.ToList(); animations.ItemsSource = catalog.Animations.ToList();
            status.Text = $"{catalog.Name}: {catalog.Skins.Count} skins e {catalog.Animations.Count} animações.";
        }
        catch (Exception ex) { await DisplayAlertAsync("Importação JSON", ex.Message, "OK"); }
    }
    async void ImportAtlas(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione o .atlas" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Selecione um .atlas.");
            using var stream = await file.OpenReadAsync(); using var reader = new StreamReader(stream);
            atlasText = await reader.ReadToEndAsync(); atlas = SpineAtlasCatalog.Read(atlasText);
            atlasInfo.Text = $"Atlas: {atlas.Pages.Count} página(s), {atlas.Pages.Sum(p => p.Regions.Count)} região(ões). Páginas: {string.Join(", ", atlas.Pages.Select(p => p.Name))}";
            UpdateTextureStatus();
        }
        catch (Exception ex) { await DisplayAlertAsync("Importação atlas", ex.Message, "OK"); }
    }
    async void ImportTexture(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione uma textura PNG" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Selecione um PNG.");
            using var input = await file.OpenReadAsync(); using var buffer = new MemoryStream(); await input.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            texture.Source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
            selectedTextures.Add(file.FileName);
            texturedView.SetTexture(file.FileName, bytes);
            UpdateTextureStatus();
        }
        catch (Exception ex) { await DisplayAlertAsync("Importação PNG", ex.Message, "OK"); }
    }
    void UpdateTextureStatus()
    {
        var matches = atlas?.Pages.Count(p => selectedTextures.Any(t => string.Equals(t, p.Name, StringComparison.OrdinalIgnoreCase))) ?? 0;
        info.Text = $"Texturas carregadas: {selectedTextures.Count}; páginas do atlas correspondentes: {matches}/{atlas?.Pages.Count ?? 0}. Exibindo PNG sem deformação esquelética.";
    }
    async void LoadRuntime(object? sender, EventArgs e) {
        try {
            if(atlasText == null || skeletonContent == null) throw new InvalidOperationException("Importe primeiro um .atlas e um .json ou .skel da versão 4.1 ou 4.2.");
            use41 = skeletonBinary && SpineBinaryInspector.Inspect(skeletonContent).Version?.StartsWith("4.1") == true;
            if (use41) runtime41.Load(atlasText,skeletonContent,skeletonBinary);
            else runtime.Load(atlasText,skeletonContent,skeletonBinary);
            skins.ItemsSource = Active.Skins.ToList(); animations.ItemsSource = Active.Animations.ToList();
            if(Active.Skins.Count>0) skins.SelectedIndex=0;
            if(Active.Animations.Count>0) animations.SelectedIndex=0;
            play.IsEnabled = Active.Animations.Count>0;
            skeletonView.Invalidate(); texturedView.InvalidateSurface();
            status.Text = $"Spine {(use41 ? "4.1" : "4.2")} carregado: {Active.Skins.Count} skins, {Active.Animations.Count} animações. Renderização texturizada disponível para Spine 4.1; 4.2 permanece em modo ossos.";
        } catch(Exception ex) { await DisplayAlertAsync("Runtime Spine 4.2",ex.Message,"OK"); }
    }
    async void ImportSkel(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione o .skel" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".skel", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Selecione um .skel.");
            using var stream = await file.OpenReadAsync(); using var buffer = new MemoryStream(); await stream.CopyToAsync(buffer);
            skeletonContent = buffer.ToArray(); skeletonBinary = true;
            var result = SpineBinaryInspector.Inspect(skeletonContent);
            status.Text = $"{file.FileName}: {result.ByteCount} bytes; versão provável: {result.Version ?? "não identificada"}. Pronto para carregar com o runtime compatível.";
        }
        catch (Exception ex) { await DisplayAlertAsync("Inspeção .skel", ex.Message, "OK"); }
    }
}
