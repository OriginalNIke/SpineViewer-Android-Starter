using System.Text.Json;
namespace SpineViewer.Core;
public static class SpineVersionDetector {
 public static string? Detect(byte[] content, bool binary) {
  if(binary) return SpineBinaryInspector.Inspect(content).Version;
  using var document = JsonDocument.Parse(content);
  if(document.RootElement.TryGetProperty("skeleton", out var skeleton) && skeleton.TryGetProperty("spine", out var version) && version.ValueKind == JsonValueKind.String) return version.GetString();
  return null;
 }
}
