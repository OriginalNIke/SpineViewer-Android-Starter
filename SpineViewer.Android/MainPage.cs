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
    AtlasCatalog? atlas;
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
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 24), Spacing = 14,
            Children = { new Label { Text = "SpineViewer Android", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                json, atlasButton, imageButton, binaryButton,
                new Border { Stroke = Color.FromArgb("#374151"), BackgroundColor = Color.FromArgb("#1F2937"), Padding = 12,
                    Content = new VerticalStackLayout { Children = { texture, info } } },
                atlasInfo, new Label { Text = "Skins", TextColor = Colors.White }, skins,
                new Label { Text = "Animações", TextColor = Colors.White }, animations, play, status }
        }};
        skins.SelectedIndexChanged += (_, _) => { if (skins.SelectedItem is string s) status.Text = $"Skin selecionada: {s}. Aplicação visual requer runtime."; };
        animations.SelectedIndexChanged += (_, _) => { if (animations.SelectedItem is string a) status.Text = $"Animação selecionada: {a}. Reprodução requer runtime."; };
    }
    async void ImportJson(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione JSON Spine" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Selecione um .json.");
            using var stream = await file.OpenReadAsync(); using var reader = new StreamReader(stream);
            var catalog = SpineJsonCatalog.Read(await reader.ReadToEndAsync(), file.FileName);
            skins.ItemsSource = catalog.Skins.ToList(); animations.ItemsSource = catalog.Animations.ToList();
            status.Text = $"{catalog.Name}: {catalog.Skins.Count} skins e {catalog.Animations.Count} animações.";
        }
        catch (Exception ex) { await DisplayAlert("Importação JSON", ex.Message, "OK"); }
    }
    async void ImportAtlas(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione o .atlas" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Selecione um .atlas.");
            using var stream = await file.OpenReadAsync(); using var reader = new StreamReader(stream);
            atlas = SpineAtlasCatalog.Read(await reader.ReadToEndAsync());
            atlasInfo.Text = $"Atlas: {atlas.Pages.Count} página(s), {atlas.Pages.Sum(p => p.Regions.Count)} região(ões). Páginas: {string.Join(", ", atlas.Pages.Select(p => p.Name))}";
            UpdateTextureStatus();
        }
        catch (Exception ex) { await DisplayAlert("Importação atlas", ex.Message, "OK"); }
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
            UpdateTextureStatus();
        }
        catch (Exception ex) { await DisplayAlert("Importação PNG", ex.Message, "OK"); }
    }
    void UpdateTextureStatus()
    {
        var matches = atlas?.Pages.Count(p => selectedTextures.Any(t => string.Equals(t, p.Name, StringComparison.OrdinalIgnoreCase))) ?? 0;
        info.Text = $"Texturas carregadas: {selectedTextures.Count}; páginas do atlas correspondentes: {matches}/{atlas?.Pages.Count ?? 0}. Exibindo PNG sem deformação esquelética.";
    }
    async void ImportSkel(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione o .skel" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".skel", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Selecione um .skel.");
            using var stream = await file.OpenReadAsync(); using var buffer = new MemoryStream(); await stream.CopyToAsync(buffer);
            var result = SpineBinaryInspector.Inspect(buffer.ToArray());
            status.Text = $"{file.FileName}: {result.ByteCount} bytes; versão provável: {result.Version ?? "não identificada"}. Decodificação .skel ainda pendente.";
        }
        catch (Exception ex) { await DisplayAlert("Inspeção .skel", ex.Message, "OK"); }
    }
}
