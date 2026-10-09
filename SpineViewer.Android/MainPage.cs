using SpineViewer.Core;
namespace SpineViewer.Android;
public sealed class MainPage : ContentPage
{
    readonly Label status = new() { Text = "Selecione a pasta do personagem para começar.", TextColor = Colors.LightGray };
    readonly Picker skins = new() { Title = "Selecionar skin", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Picker animations = new() { Title = "Selecionar animação", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Label skinCount = new() { Text = "Nenhuma skin", TextColor = Colors.LightGray, FontSize = 12 };
    readonly Label animationCount = new() { Text = "Nenhuma animação", TextColor = Colors.LightGray, FontSize = 12 };
    readonly Picker characters = new() { Title = "Selecionar personagem", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Label characterCount = new() { Text = "Nenhum personagem", TextColor = Colors.LightGray, FontSize = 12 };
    IReadOnlyList<FolderImporter.Entry>? folderFiles;
    bool updatingCharacter;
    bool loadingCharacter;
    string? pendingCharacter;
    string? loadedCharacter;
    readonly Dictionary<string, (string Text, AtlasCatalog Catalog)> atlasCache = new(StringComparer.OrdinalIgnoreCase);
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
    bool use41;
    ISpinePlayback Active => use41 ? runtime41 : runtime;
    bool playing;
    IDispatcherTimer? playbackTimer;
    public MainPage()
    {
        Title = "SpineViewer Android";
        BackgroundColor = Color.FromArgb("#111827");
        var folderButton = new Button { Text = "📁 Selecionar pasta do personagem" };
        rendererChoice.ItemsSource = new List<string> { "OpenGL ES 3.0", "Vulkan (integrado)" };
        // Restaurar o renderizador escolhido na última execução.
        rendererChoice.SelectedIndex = Preferences.Default.Get("preferred_renderer", 0) == 1 ? 1 : 0;
        rendererChoice.SelectedIndexChanged += (_, _) => SelectRenderer();
        texturedView.IsVisible = rendererChoice.SelectedIndex != 1;
        vulkanView.IsVisible = rendererChoice.SelectedIndex == 1;
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
        texturedView.GetTriangles = () => use41 ? runtime41.TexturedTriangles() : runtime.TexturedTriangles();
        var previousSkin = new Button { Text = "◀ Skin" };
        var nextSkin = new Button { Text = "Skin ▶" };
        var previousAnimation = new Button { Text = "◀ Animação" };
        var nextAnimation = new Button { Text = "Animação ▶" };
        previousSkin.Clicked += (_, _) => MoveSelection(skins, -1);
        nextSkin.Clicked += (_, _) => MoveSelection(skins, 1);
        previousAnimation.Clicked += (_, _) => MoveSelection(animations, -1);
        nextAnimation.Clicked += (_, _) => MoveSelection(animations, 1);
        var previousCharacter = new Button { Text = "◀ Personagem" };
        var nextCharacter = new Button { Text = "Personagem ▶" };
        previousCharacter.Clicked += (_, _) => MoveSelection(characters, -1);
        nextCharacter.Clicked += (_, _) => MoveSelection(characters, 1);
        characters.SelectedIndexChanged += async (_, _) =>
        {
            if (updatingCharacter || folderFiles is null || characters.SelectedItem is not string name) return;
            pendingCharacter = name;
            if (loadingCharacter) return;
            loadingCharacter = true;
            try
            {
                while (pendingCharacter is { } next)
                {
                    pendingCharacter = null;
                    if (next == loadedCharacter) continue;
                    try { await LoadCharacterFromFolderAsync(folderFiles, next); loadedCharacter = next; }
                    catch (Exception ex) { await DisplayAlertAsync("Trocar personagem", ex.Message, "OK"); }
                }
            }
            finally { loadingCharacter = false; }
        };
        play.Text = "▶ Reproduzir";
        play.Clicked += (_, _) => { playing = !playing; play.Text = playing ? "⏸ Pausar" : "▶ Reproduzir"; };
        playbackTimer = Dispatcher.CreateTimer();
        playbackTimer.Interval = TimeSpan.FromMilliseconds(16); // alvo de 60 FPS; temporizador UI não garante sincronização com VSync
        var frameClock = System.Diagnostics.Stopwatch.StartNew();
        long lastFrame = frameClock.ElapsedTicks;
        fpsStart = lastFrame;
        bool optionsExpanded = false;
        long lastVulkanPresentation = 0;
        playbackTimer.Tick += (_, _) =>
        {
            long now = frameClock.ElapsedTicks;
            float dt = Math.Clamp((float)((now - lastFrame) / (double)System.Diagnostics.Stopwatch.Frequency), 0f, 0.1f);
            lastFrame = now;
            if (!playing || !Active.IsLoaded) return;
            Active.Step(dt);
            if (rendererChoice.SelectedIndex == 1)
            {
                // Vulkan presents synchronously. While the options panel is visible,
                // avoid saturating the UI thread with blocking swapchain presents.
                // Keep animation time advancing at the normal rate.
                long interval = System.Diagnostics.Stopwatch.Frequency / (optionsExpanded ? 20 : 60);
                if (now - lastVulkanPresentation >= interval)
                {
                    lastVulkanPresentation = now;
                    vulkanView.InvalidateSurface();
                }
            }
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
        // Fundo salvo independentemente do renderizador selecionado.
        var backgroundPresets = new (string Name, int Rgb)[]
        {
            ("Azul escuro (padrão)", 0x111827), ("Preto", 0x000000),
            ("Cinza escuro", 0x303030), ("Cinza claro", 0xD0D0D0),
            ("Branco", 0xFFFFFF), ("Verde", 0x00FF00), ("Azul", 0x0000FF),
            ("Personalizado", -1)
        };
        int savedBackground = Preferences.Default.Get("background_rgb", 0x111827);
        SpineBackground.Set(savedBackground);
        var backgroundPicker = new Picker { Title = "Cor do fundo", TextColor = Colors.White, TitleColor = Colors.LightGray };
        foreach (var preset in backgroundPresets) backgroundPicker.Items.Add(preset.Name);
        var customBackground = new Entry { Placeholder = "Hexadecimal: #RRGGBB", TextColor = Colors.White,
            Keyboard = Keyboard.Text, IsVisible = false, MaxLength = 7 };
        var applyBackground = new Button { Text = "Aplicar cor", IsVisible = false };
        void SetBackground(int rgb)
        {
            SpineBackground.Set(rgb);
            Preferences.Default.Set("background_rgb", rgb);
            InvalidateActiveRenderer();
        }
        applyBackground.Clicked += async (_, _) =>
        {
            string hex = (customBackground.Text ?? "").Trim().TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int rgb))
            {
                await DisplayAlertAsync("Cor inválida", "Digite uma cor no formato #RRGGBB.", "OK");
                return;
            }
            SetBackground(rgb);
        };
        backgroundPicker.SelectedIndexChanged += (_, _) =>
        {
            int index = backgroundPicker.SelectedIndex;
            if (index < 0) return;
            bool custom = backgroundPresets[index].Rgb < 0;
            customBackground.IsVisible = applyBackground.IsVisible = custom;
            if (custom)
                customBackground.Text = $"#{SpineBackground.Current:X6}";
            else
                SetBackground(backgroundPresets[index].Rgb);
        };
        int selectedPreset = Array.FindIndex(backgroundPresets, p => p.Rgb == savedBackground);
        backgroundPicker.SelectedIndex = selectedPreset >= 0 ? selectedPreset : backgroundPresets.Length - 1;
        // Persist image in app storage; never rely on temporary picker URIs.
        string backgroundFile = Path.Combine(FileSystem.AppDataDirectory, "viewer_background.img");
        if (File.Exists(backgroundFile)) {
            try { SpineBackgroundImage.Set(File.ReadAllBytes(backgroundFile)); }
            catch { try { File.Delete(backgroundFile); } catch { } }
        }
        var selectBackgroundImage = new Button { Text = "🖼 Escolher imagem de fundo (PNG/JPG/WebP)" };
        var clearBackgroundImage = new Button { Text = "Remover imagem de fundo" };
        selectBackgroundImage.Clicked += async (_, _) => {
            try {
                var file = await FilePicker.Default.PickAsync(new PickOptions {
                    PickerTitle = "Escolha a imagem de fundo",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                        { DevicePlatform.Android, new[] { "image/png", "image/jpeg", "image/webp" } }
                    }) });
                if (file == null) return;
                await using var input = await file.OpenReadAsync();
                using var memory = new MemoryStream();
                await input.CopyToAsync(memory);
                if (memory.Length > 20*1024*1024) throw new InvalidOperationException("Imagem maior que 20 MB.");
                var bytes = memory.ToArray();
                SpineBackgroundImage.Set(bytes);
                await File.WriteAllBytesAsync(backgroundFile, bytes);
                InvalidateActiveRenderer();
            } catch (Exception ex) { await DisplayAlertAsync("Imagem de fundo", ex.Message, "OK"); }
        };
        clearBackgroundImage.Clicked += (_, _) => {
            SpineBackgroundImage.Set(null);
            if (File.Exists(backgroundFile)) File.Delete(backgroundFile);
            InvalidateActiveRenderer();
        };
        // Menu recolhível: controles de importação e diagnóstico não ocupam a área do personagem.
        var menuButton = new Button { Text = "☰  Opções  ▾", HorizontalOptions = LayoutOptions.Fill };
        var optionsPanel = new VerticalStackLayout { Spacing = 12, IsVisible = false,
            Children = { folderButton, json, rendererChoice, new Label { Text = "Fundo da animação", TextColor = Colors.White },
                backgroundPicker, customBackground, applyBackground, selectBackgroundImage, clearBackgroundImage } };
        menuButton.Clicked += (_, _) =>
        {
            optionsPanel.IsVisible = !optionsPanel.IsVisible;
            optionsExpanded = optionsPanel.IsVisible;
            menuButton.Text = optionsPanel.IsVisible ? "✕  Fechar opções  ▴" : "☰  Opções  ▾";
        };
        // Layout responsivo: o visualizador recebe o espaço restante da tela.
        // Os três seletores permanecem visíveis sem rolagem da página inteira.
        var layout = new Grid
        {
            Padding = new Thickness(8, 4, 8, 8),
            RowSpacing = 4,
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        layout.Add(menuButton);
        Grid.SetRow(menuButton, 0);
        var optionsScroll = new ScrollView { Content = optionsPanel, MaximumHeightRequest = 210, IsVisible = false };
        menuButton.Clicked += (_, _) => optionsScroll.IsVisible = optionsPanel.IsVisible;
        layout.Add(optionsScroll);
        Grid.SetRow(optionsScroll, 1);
        var preview = new Grid();
        preview.Add(texturedView);
        preview.Add(vulkanView);
        // Monitoramento visível sobre a tela do personagem, independente do menu.
        // InputTransparent evita bloquear zoom, arraste e toques no visualizador.
        var monitoring = new VerticalStackLayout
        {
            Spacing = 1,
            Padding = new Thickness(6, 4),
            BackgroundColor = Color.FromArgb("#B0111827"),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true,
            MaximumWidthRequest = 330,
            Children = { gpuInfo, fpsInfo, performanceInfo, status }
        };
        gpuInfo.FontSize = 10;
        fpsInfo.FontSize = 10;
        performanceInfo.FontSize = 10;
        status.FontSize = 10;
        preview.Add(monitoring);
        texturedView.HeightRequest = -1;
        vulkanView.HeightRequest = -1;
        texturedView.MinimumHeightRequest = 100;
        vulkanView.MinimumHeightRequest = 100;
        layout.Add(preview);
        Grid.SetRow(preview, 2);

        var selectors = new Grid
        {
            RowSpacing = 2,
            ColumnSpacing = 4,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(new GridLength(76)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(42)),
                new ColumnDefinition(new GridLength(42))
            },
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        void AddSelector(int row, string title, Picker picker, Button previous, Button next)
        {
            var label = new Label { Text = title, TextColor = Colors.White, FontSize = 12,
                VerticalTextAlignment = TextAlignment.Center };
            previous.Text = "◀";
            next.Text = "▶";
            previous.Padding = new Thickness(0);
            next.Padding = new Thickness(0);
            previous.FontSize = 13;
            next.FontSize = 13;
            picker.FontSize = 13;
            selectors.Add(label); Grid.SetRow(label, row); Grid.SetColumn(label, 0);
            selectors.Add(picker); Grid.SetRow(picker, row); Grid.SetColumn(picker, 1);
            selectors.Add(previous); Grid.SetRow(previous, row); Grid.SetColumn(previous, 2);
            selectors.Add(next); Grid.SetRow(next, row); Grid.SetColumn(next, 3);
        }
        AddSelector(0, "Personagem", characters, previousCharacter, nextCharacter);
        AddSelector(1, "Skin", skins, previousSkin, nextSkin);
        AddSelector(2, "Animação", animations, previousAnimation, nextAnimation);
        play.FontSize = 13;
        play.Padding = new Thickness(8, 5);
        var fitButton = new Button { Text = "⊡ Centralizar / Ajustar", FontSize = 13,
            Padding = new Thickness(8, 5) };
        fitButton.Clicked += (_, _) =>
        {
            SpineCamera.Reset();
            InvalidateActiveRenderer();
        };
        var playbackControls = new Grid
        {
            ColumnSpacing = 4,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        playbackControls.Add(play);
        playbackControls.Add(fitButton);
        Grid.SetColumn(fitButton, 1);
        var controls = new VerticalStackLayout { Spacing = 2,
            Children = { selectors, playbackControls } };
        layout.Add(controls);
        Grid.SetRow(controls, 3);
        // Contagens ficam nas opções; monitoramento em tempo real aparece na tela.
        optionsPanel.Children.Add(characterCount);
        optionsPanel.Children.Add(skinCount);
        optionsPanel.Children.Add(animationCount);
        Content = layout;
        skins.SelectedIndexChanged += (_, _) => { if (updatingSelection || skins.SelectedItem is not string s) return; Active.SetSkin(s); InvalidateActiveRenderer(); status.Text = $"Skin: {s}"; };
        animations.SelectedIndexChanged += (_, _) => { if (updatingSelection || animations.SelectedItem is not string a) return; Active.SetAnimation(a); InvalidateActiveRenderer(); status.Text = $"Animação: {a}"; };
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
        Preferences.Default.Set("preferred_renderer", vulkan ? 1 : 0);
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
            if (skeletons.Count == 0 || !files.Any(f => f.Name.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Nenhum conjunto Spine (.skel/.json e .atlas) encontrado na pasta ou subpastas.");
            folderFiles = files;
            atlasCache.Clear();
            loadedCharacter = null;
            pendingCharacter = null;
            updatingCharacter = true;
            try
            {
                characters.ItemsSource = skeletons.Select(f => f.Name).ToList();
                characterCount.Text = $"{skeletons.Count} personagem(ns)";
                characters.SelectedIndex = 0;
            }
            finally { updatingCharacter = false; }
            loadingCharacter = true;
            try { await LoadCharacterFromFolderAsync(files, skeletons[0].Name); loadedCharacter = skeletons[0].Name; }
            finally { loadingCharacter = false; }
        }
        catch (Exception ex) { await DisplayAlertAsync("Importar pasta", ex.Message, "OK"); }
    }

    async Task LoadCharacterFromFolderAsync(IReadOnlyList<FolderImporter.Entry> files, string characterPath)
    {
        var skeleton = files.First(f => f.Name.Equals(characterPath, StringComparison.Ordinal));
        var atlases = files.Where(f => f.Name.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)).ToList();
        if (atlases.Count == 0) throw new InvalidDataException("Nenhum atlas encontrado.");
        {
            bool binary = skeleton.Name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase);
            string stem = Path.GetFileNameWithoutExtension(skeleton.FileName);
            // Prefer an atlas beside the skeleton with the same basename.
            var rankedAtlases = atlases.OrderByDescending(f => f.Directory.Equals(skeleton.Directory, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(f => Path.GetFileNameWithoutExtension(f.FileName).Equals(stem, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var selectedAtlas = rankedAtlases[0];
            if (rankedAtlases.Count > 1 &&
                rankedAtlases[0].Directory.Equals(rankedAtlases[1].Directory, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileNameWithoutExtension(rankedAtlases[0].FileName).Equals(stem, StringComparison.OrdinalIgnoreCase) ==
                Path.GetFileNameWithoutExtension(rankedAtlases[1].FileName).Equals(stem, StringComparison.OrdinalIgnoreCase))
            {
                var selection = await DisplayActionSheetAsync("Escolha o atlas", "Cancelar", null,
                    rankedAtlases.Select(f => f.Name).ToArray());
                if (string.IsNullOrEmpty(selection) || selection == "Cancelar") return;
                selectedAtlas = rankedAtlases.First(f => f.Name == selection);
            }
            if (!atlasCache.TryGetValue(selectedAtlas.Name, out var cachedAtlas))
            {
                string text = System.Text.Encoding.UTF8.GetString(selectedAtlas.Content);
                cachedAtlas = (text, SpineAtlasCatalog.Read(text));
                atlasCache[selectedAtlas.Name] = cachedAtlas;
            }
            var atlasString = cachedAtlas.Text;
            var catalog = cachedAtlas.Catalog;
            var pngs = files.Where(f => f.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList();
            var textures = new List<(string page, FolderImporter.Entry file)>();
            foreach (var page in catalog.Pages)
            {
                string relativePage = page.Name.Replace('\\', '/').TrimStart('/');
                string expected = string.IsNullOrEmpty(selectedAtlas.Directory) ? relativePage : selectedAtlas.Directory + "/" + relativePage;
                var match = pngs.FirstOrDefault(p => p.Name.Equals(expected, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    var candidates = pngs.Where(p => p.FileName.Equals(Path.GetFileName(relativePage), StringComparison.OrdinalIgnoreCase)).ToList();
                    if (candidates.Count == 1) match = candidates[0];
                    else if (candidates.Count > 1)
                        throw new InvalidDataException($"Textura ambígua '{page.Name}': {string.Join(", ", candidates.Select(c => c.Name))}. Coloque a PNG ao lado do atlas ou no caminho indicado nele.");
                }
                if (match is null) throw new InvalidDataException($"Textura ausente: {expected}");
                textures.Add((page.Name, match));
            }
            // Validar o conjunto antes de alterar a sessão carregada.
            playing = false;
            skeletonContent = skeleton.Content;
            skeletonBinary = binary;
            atlasText = atlasString;
            atlas = catalog;
            selectedTextures.Clear();
            vulkanTextures.Clear();
            foreach (var (page, png) in textures)
            {
                texturedView.SetTexture(page, png.Content);
                vulkanTextures[page] = png.Content;
                selectedTextures.Add(page);
            }
            atlasInfo.Text = $"Atlas: {catalog.Pages.Count} página(s), {catalog.Pages.Sum(p => p.Regions.Count)} regiões.";
            if (!binary)
            {
                var data = SpineJsonCatalog.Read(System.Text.Encoding.UTF8.GetString(skeleton.Content), skeleton.Name);
                UpdateCatalogs(data.Skins.ToList(), data.Animations.ToList(), false);
            }
            await LoadRuntimeCoreAsync(autoPlay: true);
            status.Text = $"Pasta importada: {skeleton.Name}, {selectedAtlas.Name}, {textures.Count} textura(s). Spine {(use41 ? "4.1" : "4.2")} carregado.";
        }
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
            TryLoadImportedRuntime();
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
            TryLoadImportedRuntime();
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
            TryLoadImportedRuntime();
        }
        catch (Exception ex) { await DisplayAlertAsync("Importação PNG", ex.Message, "OK"); }
    }
    void UpdateTextureStatus()
    {
        var matches = atlas?.Pages.Count(p => selectedTextures.Any(t => string.Equals(t, p.Name, StringComparison.OrdinalIgnoreCase))) ?? 0;
        status.Text = $"Texturas: {selectedTextures.Count}; páginas do atlas correspondentes: {matches}/{atlas?.Pages.Count ?? 0}.";
    }
    void TryLoadImportedRuntime()
    {
        // Importações individuais: carregar assim que esqueleto, atlas e todas as páginas estiverem disponíveis.
        if (atlas is null || atlasText is null || skeletonContent is null) return;
        if (!atlas.Pages.All(p => selectedTextures.Any(t => string.Equals(t, p.Name, StringComparison.OrdinalIgnoreCase)))) return;
        LoadRuntimeCoreAsync().GetAwaiter().GetResult();
    }

    Task LoadRuntimeCoreAsync(bool autoPlay = false)
    {
        if (atlasText == null || skeletonContent == null)
            throw new InvalidOperationException("Importe primeiro um .atlas e um .json ou .skel.");
        var detectedVersion = skeletonBinary ? SpineBinaryInspector.Inspect(skeletonContent).Version : null;
        if (skeletonBinary && detectedVersion is not null &&
            !detectedVersion.StartsWith("4.1") && !detectedVersion.StartsWith("4.2"))
            throw new NotSupportedException($"Spine {detectedVersion} não suportado. Use arquivos 4.1 ou 4.2.");
        use41 = skeletonBinary && detectedVersion?.StartsWith("4.1") == true;
        if (use41) runtime41.Load(atlasText, skeletonContent, skeletonBinary);
        else runtime.Load(atlasText, skeletonContent, skeletonBinary);
        UpdateCatalogs(Active.Skins, Active.Animations, true);
        // Picker selection is updated while selection events are suppressed.
        // Explicitly assign the first animation to the newly created AnimationState.
        // Otherwise the UI shows "idle" and "Pausar", but the track is empty.
        if (Active.Animations.Count > 0)
        {
            string initialAnimation = animations.SelectedItem as string ?? Active.Animations[0];
            Active.SetAnimation(initialAnimation);
            Active.Step(0f); // Apply first pose before first render (OpenGL and Vulkan).
        }
        play.IsEnabled = Active.Animations.Count > 0;
        SpineCamera.ResetCharacterFrame(); // Reinicia o enquadramento estável para o novo personagem.
        playing = autoPlay && play.IsEnabled;
        play.Text = playing ? "⏸ Pausar" : "▶ Reproduzir";
        InvalidateActiveRenderer();
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
            status.Text = $"{file.FileName}: {result.ByteCount} bytes; versão provável: {result.Version ?? "não identificada"}.";
            TryLoadImportedRuntime();
        }
        catch (Exception ex) { await DisplayAlertAsync("Inspeção .skel", ex.Message, "OK"); }
    }
}
