using SpineViewer.Core;
namespace SpineViewer.Android;
public sealed class MainPage : ContentPage
{
    readonly Label status = new() { Text = "Importe JSON, atlas e texturas para começar.", TextColor = Colors.LightGray };
    readonly Picker skins = new() { Title = "Selecionar skin", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Picker animations = new() { Title = "Selecionar animação", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Entry skinSearch = new() { Placeholder = "Pesquisar skin...", TextColor = Colors.White, PlaceholderColor = Colors.Gray, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
    readonly Entry animationSearch = new() { Placeholder = "Pesquisar animação...", TextColor = Colors.White, PlaceholderColor = Colors.Gray, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
    readonly Label skinCount = new() { Text = "Nenhuma skin", TextColor = Colors.LightGray, FontSize = 12 };
    readonly Label animationCount = new() { Text = "Nenhuma animação", TextColor = Colors.LightGray, FontSize = 12 };
    IReadOnlyList<string> allSkins = Array.Empty<string>();
    IReadOnlyList<string> allAnimations = Array.Empty<string>();
    bool updatingSelection;
    readonly Label atlasInfo = new() { Text = "Nenhum atlas importado", TextColor = Colors.LightGray };
    readonly Button play = new() { Text = "▶ Reproduzir (aguardando runtime)", IsEnabled = false };
    readonly List<string> selectedTextures = new();
    readonly Dictionary<string, byte[]> vulkanTextures = new(StringComparer.OrdinalIgnoreCase);
    readonly SpineGLView texturedView = new();
    readonly SpineVulkanView vulkanView = new() { IsVisible = false };
    readonly Label gpuInfo = new() { Text = "GPU: OpenGL ES 3.0", TextColor = Colors.LightGray };
    readonly Label fpsInfo = new() { Text = "FPS (atualizações): --", TextColor = Colors.LightGray };
    readonly Label performanceInfo = new() { Text = "Desempenho: aguardando quadros", TextColor = Colors.LightGreen };
    readonly Picker rendererChoice = new() { Title = "Renderizador", TextColor = Colors.White, TitleColor = Colors.LightGray };
    int frameSamples;
    long vulkanPresented;
    long fpsStart;
    AtlasCatalog? atlas;
    string? atlasText;
    byte[]? skeletonContent;
    bool skeletonBinary;
    readonly Spine42Session runtime = new();
    readonly Spine41Session runtime41 = new();
    readonly Spine40Session runtime40 = new();
    bool use40;
    bool use41;
    ISpinePlayback Active => use40 ? runtime40 : (use41 ? runtime41 : runtime);
    bool playing;
    IDispatcherTimer? playbackTimer;
    public MainPage()
    {
        Title = "SpineViewer Android";
        BackgroundColor = Color.FromArgb("#111827");
        var folderButton = new Button { Text = "📁 Selecionar pasta do personagem" };
        var checkVulkan = new Button { Text = "Verificar suporte Vulkan" };
        checkVulkan.Clicked += (_, _) => gpuInfo.Text = VulkanSupport.GetStatus();
        rendererChoice.ItemsSource = new List<string> { "OpenGL ES 3.0", "Vulkan (integrado)" };
        rendererChoice.SelectedIndex = 0;
        rendererChoice.SelectedIndexChanged += (_, _) => SelectRenderer();
        vulkanView.GetTriangles = () => texturedView.GetTriangles();
        vulkanView.GetTextures = () => vulkanTextures;
        vulkanView.OnFramePresented = () => System.Threading.Interlocked.Increment(ref vulkanPresented);
        vulkanView.OnStatus = message => MainThread.BeginInvokeOnMainThread(() => {
            gpuInfo.Text = message;
            if (rendererChoice.SelectedIndex == 1 &&
                (message.Contains("falha", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("erro", StringComparison.OrdinalIgnoreCase)))
                rendererChoice.SelectedIndex = 0;
        });
        folderButton.Clicked += ImportFolder;
        var json = new Button { Text = "Importar JSON Spine" };
        json.Clicked += ImportJson;
        var atlasButton = new Button { Text = "Importar .atlas" };
        atlasButton.Clicked += ImportAtlas;
        var imageButton = new Button { Text = "Importar textura PNG" };
        imageButton.Clicked += ImportTexture;
        var binaryButton = new Button { Text = "Inspecionar .skel" };
        binaryButton.Clicked += ImportSkel;
        texturedView.GetTriangles = () => use40 ? runtime40.TexturedTriangles() : (use41 ? runtime41.TexturedTriangles() : runtime.TexturedTriangles());
        var loadRuntime = new Button { Text = "Carregar runtime Spine 4.0 / 4.1 / 4.2" };
        loadRuntime.Clicked += LoadRuntime;
        var previousSkin = new Button { Text = "◀ Skin" };
        var nextSkin = new Button { Text = "Skin ▶" };
        var previousAnimation = new Button { Text = "◀ Animação" };
        var nextAnimation = new Button { Text = "Animação ▶" };
        previousSkin.Clicked += (_, _) => MoveSelection(skins, -1);
        nextSkin.Clicked += (_, _) => MoveSelection(skins, 1);
        previousAnimation.Clicked += (_, _) => MoveSelection(animations, -1);
        nextAnimation.Clicked += (_, _) => MoveSelection(animations, 1);
        skinSearch.TextChanged += (_, _) => FilterPicker(skins, allSkins, skinSearch.Text, skinCount, "skins");
        animationSearch.TextChanged += (_, _) => FilterPicker(animations, allAnimations, animationSearch.Text, animationCount, "animações");
        play.Text = "▶ Reproduzir";
        play.Clicked += (_, _) => { playing = !playing; play.Text = playing ? "⏸ Pausar" : "▶ Reproduzir"; };
        playbackTimer = Dispatcher.CreateTimer();
        playbackTimer.Interval = TimeSpan.FromMilliseconds(16); // alvo de 60 FPS; temporizador UI não garante sincronização com VSync
        var frameClock = System.Diagnostics.Stopwatch.StartNew();
        long lastFrame = frameClock.ElapsedTicks;
        fpsStart = lastFrame;
        playbackTimer.Tick += (_, _) =>
        {
            long now = frameClock.ElapsedTicks;
            float dt = Math.Clamp((float)((now - lastFrame) / (double)System.Diagnostics.Stopwatch.Frequency), 0f, 0.1f);
            lastFrame = now;
            if (!playing || !Active.IsLoaded) return;
            try { Active.Step(dt); }
            catch (Exception ex) {
                playing = false;
                play.Text = "▶ Reproduzir";
                status.Text = "Erro ao atualizar animação: " + ex.Message;
                return;
            }
            if (rendererChoice.SelectedIndex == 1) vulkanView.InvalidateSurface();
            else texturedView.InvalidateSurface();
            frameSamples++;
            if ((now - fpsStart) >= System.Diagnostics.Stopwatch.Frequency)
            {
                double seconds = (now - fpsStart) / (double)System.Diagnostics.Stopwatch.Frequency;
                long presented = System.Threading.Interlocked.Exchange(ref vulkanPresented, 0);
                double updates = frameSamples / seconds;
                double presentations = presented / seconds;
                fpsInfo.Text = rendererChoice.SelectedIndex == 1
                    ? $"Vulkan: {presentations:F1} apresentações/s | animação: {updates:F1}/s"
                    : $"OpenGL: {updates:F1} atualizações/s (não mede apresentação)";
                performanceInfo.Text = rendererChoice.SelectedIndex == 1
                    ? $"Vulkan: {presentations:F1} apresentações/s | {((presentations > 0) ? 1000.0 / presentations : 0):F1} ms/apresentação (média) | até 3 frames em voo"
                    : $"OpenGL: {updates:F1} atualizações/s | {((updates > 0) ? 1000.0 / updates : 0):F1} ms/atualização (média)";
                frameSamples = 0; fpsStart = now;
            }
        };
        playbackTimer.Start();
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 24), Spacing = 14,
            Children = { new Label { Text = "SpineViewer Android", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                folderButton, json, atlasButton, imageButton, binaryButton, loadRuntime, rendererChoice, checkVulkan, gpuInfo, fpsInfo, performanceInfo, texturedView, vulkanView,
                atlasInfo,
                new Label { Text = "Skins", TextColor = Colors.White, FontAttributes = FontAttributes.Bold }, skinSearch, skinCount, skins,
                new HorizontalStackLayout { Spacing = 8, Children = { previousSkin, nextSkin } },
                new Label { Text = "Animações", TextColor = Colors.White, FontAttributes = FontAttributes.Bold }, animationSearch, animationCount, animations,
                new HorizontalStackLayout { Spacing = 8, Children = { previousAnimation, nextAnimation } },
                play, status }
        }};
        skins.SelectedIndexChanged += (_, _) => { if (updatingSelection || skins.SelectedItem is not string s) return; Active.SetSkin(s); InvalidateActiveRenderer(); status.Text = $"Skin: {s}"; };
        animations.SelectedIndexChanged += (_, _) => { if (updatingSelection || animations.SelectedItem is not string a) return; try { Active.SetAnimation(a); InvalidateActiveRenderer(); status.Text = $"Animação: {a}"; }
            catch (Exception ex) { playing = false; play.Text = "▶ Reproduzir"; status.Text = "Erro ao trocar animação: " + ex.Message; } };
    }
    static void MoveSelection(Picker picker, int direction)
    {
        if (picker.Items.Count == 0) return;
        int current = picker.SelectedIndex;
        picker.SelectedIndex = current < 0 ? 0 : (current + direction + picker.Items.Count) % picker.Items.Count;
    }
    void FilterPicker(Picker picker, IReadOnlyList<string> source, string? query, Label count, string noun)
    {
        string? selected = picker.SelectedItem as string;
        var matches = string.IsNullOrWhiteSpace(query)
            ? source.ToList()
            : source.Where(s => s.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        updatingSelection = true;
        try
        {
            picker.ItemsSource = matches;
            picker.SelectedIndex = selected == null ? -1 : matches.IndexOf(selected);
        }
        finally { updatingSelection = false; }
        count.Text = $"{matches.Count} de {source.Count} {noun}";
    }
    void UpdateCatalogs(IReadOnlyList<string> availableSkins, IReadOnlyList<string> availableAnimations, bool selectFirst)
    {
        allSkins = availableSkins.ToArray();
        allAnimations = availableAnimations.ToArray();
        skinSearch.Text = string.Empty;
        animationSearch.Text = string.Empty;
        FilterPicker(skins, allSkins, null, skinCount, "skins");
        FilterPicker(animations, allAnimations, null, animationCount, "animações");
        if (selectFirst)
        {
            if (skins.Items.Count > 0) skins.SelectedIndex = 0;
            if (animations.Items.Count > 0) animations.SelectedIndex = 0;
        }
    }
    void SelectRenderer()
    {
        bool vulkan = rendererChoice.SelectedIndex == 1;
        texturedView.IsVisible = !vulkan;
        vulkanView.IsVisible = vulkan;
        gpuInfo.Text = vulkan ? "GPU: Vulkan (integrado, inicializando...)" : "GPU: OpenGL ES 3.0";
        InvalidateActiveRenderer();
    }
    void InvalidateActiveRenderer()
    {
        if (rendererChoice.SelectedIndex == 1) vulkanView.InvalidateSurface();
        else texturedView.InvalidateSurface();
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
            vulkanTextures.Clear();
            foreach (var png in pngs)
            {
                texturedView.SetTexture(png.Name, png.Content);
                vulkanTextures[png.Name] = png.Content;
                selectedTextures.Add(png.Name);
            }
            atlasInfo.Text = $"Atlas: {catalog.Pages.Count} página(s), {catalog.Pages.Sum(p => p.Regions.Count)} regiões.";
            if (!binary)
            {
                var data = SpineJsonCatalog.Read(System.Text.Encoding.UTF8.GetString(skeleton.Content), skeleton.Name);
                UpdateCatalogs(data.Skins.ToList(), data.Animations.ToList(), false);
            }
            await LoadRuntimeCoreAsync();
            status.Text = $"Pasta importada: {skeleton.Name}, {atlases[0].Name}, {pngs.Count} PNG(s). Spine {(use40 ? "4.0" : (use41 ? "4.1" : "4.2"))} carregado.";
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
            UpdateCatalogs(catalog.Skins.ToList(), catalog.Animations.ToList(), false);
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
            vulkanTextures[file.FileName] = bytes;
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
        string? version = SpineVersionDetector.Detect(skeletonContent, skeletonBinary);
        use40 = version?.StartsWith("4.0", StringComparison.Ordinal) == true;
        use41 = version?.StartsWith("4.1", StringComparison.Ordinal) == true;
        if (!use40 && !use41 && version?.StartsWith("4.2", StringComparison.Ordinal) != true)
            throw new NotSupportedException($"Versão Spine não identificada ou não suportada: {version ?? "desconhecida"}. Exporte em 4.0, 4.1 ou 4.2.");
        if (use40) runtime40.Load(atlasText, skeletonContent, skeletonBinary);
        else if (use41) runtime41.Load(atlasText, skeletonContent, skeletonBinary);
        else runtime.Load(atlasText, skeletonContent, skeletonBinary);
        UpdateCatalogs(Active.Skins, Active.Animations, true);
        playing = false;
        play.Text = "▶ Reproduzir";
        play.IsEnabled = Active.Animations.Count > 0;
        texturedView.InvalidateSurface();
        status.Text = $"Spine {(use40 ? "4.0" : (use41 ? "4.1" : "4.2"))} carregado: {Active.Skins.Count} skins, {Active.Animations.Count} animações.";
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
