using SpineViewer.Core;
namespace SpineViewer.Android;
public sealed class MainPage : ContentPage
{
    readonly Label status = new() { Text = "Selecione um JSON Spine para listar skins e animações.", TextColor = Colors.LightGray };
    readonly Label preview = new() { Text = "Prévia gráfica ainda não implementada", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
    readonly Picker skins = new() { Title = "Selecionar skin", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Picker animations = new() { Title = "Selecionar animação", TextColor = Colors.White, TitleColor = Colors.LightGray };
    readonly Button play = new() { Text = "▶ Reproduzir (em desenvolvimento)", IsEnabled = false };
    public MainPage()
    {
        Title = "SpineViewer Android";
        BackgroundColor = Color.FromArgb("#111827");
        var import = new Button { Text = "Importar JSON Spine" };
        import.Clicked += ImportJson;
        var atlas = new Button { Text = "Selecionar atlas / texturas (em desenvolvimento)", IsEnabled = false };
        var view = new Border { Stroke = Color.FromArgb("#374151"), BackgroundColor = Color.FromArgb("#1F2937"), Padding = 12, HeightRequest = 250, Content = preview };
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 24), Spacing = 16,
            Children = { new Label { Text = "SpineViewer Android", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }, import, atlas, view,
                new Label { Text = "Skins", TextColor = Colors.White }, skins,
                new Label { Text = "Animações", TextColor = Colors.White }, animations, play, status }
        }};
        skins.SelectedIndexChanged += (_, _) => status.Text = skins.SelectedItem is string skin ? $"Skin selecionada: {skin} (prévia pendente)" : status.Text;
        animations.SelectedIndexChanged += (_, _) => status.Text = animations.SelectedItem is string animation ? $"Animação selecionada: {animation} (reprodução pendente)" : status.Text;
    }
    async void ImportJson(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecione o arquivo JSON do Spine" });
            if (file is null) return;
            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            { await DisplayAlert("Formato", "Nesta versão inicial, selecione um arquivo .json exportado pelo Spine.", "OK"); return; }
            using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var catalog = SpineJsonCatalog.Read(await reader.ReadToEndAsync(), file.FileName);
            skins.ItemsSource = catalog.Skins.ToList();
            animations.ItemsSource = catalog.Animations.ToList();
            status.Text = $"{catalog.Name}: {catalog.Skins.Count} skins e {catalog.Animations.Count} animações encontradas.";
        }
        catch (Exception ex) { await DisplayAlert("Falha na importação", ex.Message, "OK"); }
    }
}
