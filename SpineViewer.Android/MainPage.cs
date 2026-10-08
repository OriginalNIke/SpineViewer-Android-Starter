using SpineViewer.Core;
namespace SpineViewer.Android;
public sealed class MainPage : ContentPage
{
    readonly Label status = new() { Text = "Importe JSON, atlas e texturas para começar.", TextColor = Colors.LightGray };
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
    bool playing;
    IDispatcherTimer? playbackTimer;
    public MainPage()
    {
        Title = "SpineViewer Android";
        BackgroundColor = Color.FromArgb("#111827");
        var folderButton = new Button { Text = "📁 Selecionar pasta do personagem" };
        folderButton.Clicked += ImportFolder;
        var json = new Button { Text = "Importar JSON Spine" };
        json.Clicked += ImportJson;
        var atlasButton = new Button { Text = "Importar .atlas" };
        atlasButton.Clicked += ImportAtlas;
        var imageButton = new Button { Text = "Importar textura PNG" };
        imageButton.Clicked += ImportTexture;
        var binaryButton = new Button { Text = "Inspecionar .skel" };
        binaryButton.Clicked += ImportSkel;
        texturedView.GetTriangles = () => use41 ? runtime41.TexturedTriangles() : Array.Empty<SpineTriangle>();
        var loadRuntime = new Button { Text = "Carregar runtime Spine 4.1 / 4.2" };
        loadRuntime.Clicked += LoadRuntime;
        play.Text = "▶ Reproduzir";
        play.Clicked += (_, _) => { playing = !playing; play.Text = playing ? "⏸ Pausar" : "▶ Reproduzir"; };
        playbackTimer = Dispatcher.CreateTimer();
        playbackTimer.Interval = TimeSpan.FromMilliseconds(16); // alvo de 60 FPS; temporizador UI não garante sincronização com VSync
        var frameClock = System.Diagnostics.Stopwatch.StartNew();
        long lastFrame = frameClock.ElapsedTicks;
        playbackTimer.Tick += (_, _) =>
        {
            long now = frameClock.ElapsedTicks;
            float dt = Math.Clamp((float)((now - lastFrame) / (double)System.Diagnostics.Stopwatch.Frequency), 0f, 0.1f);
            lastFrame = now;
            if (!playing || !Active.IsLoaded || !use41) return;
            Active.Step(dt);
            texturedView.InvalidateSurface();
        };
        playbackTimer.Start();
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 24), Spacing = 14,
            Children = { new Label { Text = "SpineViewer Android", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                folderButton, json, atlasButton, imageButton, binaryButton, loadRuntime, texturedView,
                atlasInfo, new Label { Text = "Skins", TextColor = Colors.White }, skins,
                new Label { Text = "Animações", TextColor = Colors.White }, animations, play, status }
        }};
        skins.SelectedIndexChanged += (_, _) => { if (skins.SelectedItem is not string s) return; Active.SetSkin(s); texturedView.InvalidateSurface(); status.Text = $"Skin: {s}"; };
        animations.SelectedIndexChanged += (_, _) => { if (animations.SelectedItem is not string a) return; Active.SetAnimation(a); texturedView.InvalidateSurface(); status.Text = $"Animação: {a}"; };
    }
    async void ImportFolder(object? sender, EventArgs e)
    {
        try
        {
            var files = await FolderImporter.SelectAndReadAsync();
            if (files is null) return;
            var skeletons = files.Where(f => f.Name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase) ||
                                             f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();
            var atlases = files.Where(f => f.Name.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)).ToList();
            if (skeletons.Count != 1 || atlases.Count != 1)
                throw new InvalidDataException($"A pasta deve conter exatamente um .skel ou .json e um .atlas. Encontrados: {skeletons.Count} esqueletos e {atlases.Count} atlas.");
            var skeleton = skeletons[0];
            bool binary = skeleton.Name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase);
            var atlasString = System.Text.Encoding.UTF8.GetString(atlases[0].Content);
            var catalog = SpineAtlasCatalog.Read(atlasString);
            var pngs = files.Where(f => f.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList();
            var missing = catalog.Pages.Where(page => !pngs.Any(p => string.Equals(p.Name, page.Name, StringComparison.OrdinalIgnoreCase)))
                                       .Select(page => page.Name).ToArray();
            if (missing.Length > 0) throw new InvalidDataException("Texturas ausentes: " + string.Join(", ", missing));
            // Validar o conjunto antes de alterar a sessão carregada.
            playing = false;
            skeletonContent = skeleton.Content;
            skeletonBinary = binary;
            atlasText = atlasString;
            atlas = catalog;
            selectedTextures.Clear();
            foreach (var png in pngs)
            {
                texturedView.SetTexture(png.Name, png.Content);
                selectedTextures.Add(png.Name);
            }
            atlasInfo.Text = $"Atlas: {catalog.Pages.Count} página(s), {catalog.Pages.Sum(p => p.Regions.Count)} regiões.";
            if (!binary)
            {
                var data = SpineJsonCatalog.Read(System.Text.Encoding.UTF8.GetString(skeleton.Content), skeleton.Name);
                skins.ItemsSource = data.Skins.ToList();
                animations.ItemsSource = data.Animations.ToList();
            }
            await LoadRuntimeCoreAsync();
            status.Text = $"Pasta importada: {skeleton.Name}, {atlases[0].Name}, {pngs.Count} PNG(s). Spine {(use41 ? "4.1" : "4.2")} carregado.";
        }
        catch (Exception ex) { await DisplayAlertAsync("Importar pasta", ex.Message, "OK"); }
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
            selectedTextures.Add(file.FileName);
            texturedView.SetTexture(file.FileName, bytes);
            UpdateTextureStatus();
        }
        catch (Exception ex) { await DisplayAlertAsync("Importação PNG", ex.Message, "OK"); }
    }
    void UpdateTextureStatus()
    {
        var matches = atlas?.Pages.Count(p => selectedTextures.Any(t => string.Equals(t, p.Name, StringComparison.OrdinalIgnoreCase))) ?? 0;
        status.Text = $"Texturas: {selectedTextures.Count}; páginas do atlas correspondentes: {matches}/{atlas?.Pages.Count ?? 0}.";
    }
    async void LoadRuntime(object? sender, EventArgs e)
    {
        try { await LoadRuntimeCoreAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("Runtime Spine", ex.Message, "OK"); }
    }
    Task LoadRuntimeCoreAsync()
    {
        if (atlasText == null || skeletonContent == null)
            throw new InvalidOperationException("Importe primeiro um .atlas e um .json ou .skel.");
        use41 = skeletonBinary && SpineBinaryInspector.Inspect(skeletonContent).Version?.StartsWith("4.1") == true;
        if (use41) runtime41.Load(atlasText, skeletonContent, skeletonBinary);
        else runtime.Load(atlasText, skeletonContent, skeletonBinary);
        skins.ItemsSource = Active.Skins.ToList();
        animations.ItemsSource = Active.Animations.ToList();
        if (Active.Skins.Count > 0) skins.SelectedIndex = 0;
        if (Active.Animations.Count > 0) animations.SelectedIndex = 0;
        playing = false;
        play.Text = "▶ Reproduzir";
        play.IsEnabled = use41 && Active.Animations.Count > 0;
        texturedView.InvalidateSurface();
        status.Text = $"Spine {(use41 ? "4.1" : "4.2")} carregado: {Active.Skins.Count} skins, {Active.Animations.Count} animações.";
        return Task.CompletedTask;
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
