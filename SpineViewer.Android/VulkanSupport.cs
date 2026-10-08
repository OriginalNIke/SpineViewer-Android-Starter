using System.Runtime.InteropServices;

namespace SpineViewer.Android;

/// <summary>
/// Vulkan capability probe. Does not render: the OpenGL ES renderer remains active.
/// A full Vulkan renderer needs an ANativeWindow-backed VkSurfaceKHR, swapchain,
/// pipelines, descriptors, texture upload and synchronized presentation.
/// </summary>
internal static class VulkanSupport
{
    [DllImport("libvulkan.so", EntryPoint = "vkEnumerateInstanceVersion")]
    private static extern int EnumerateInstanceVersion(out uint version);

    public static string GetStatus()
    {
        if (!OperatingSystem.IsAndroid()) return "Vulkan: verificação disponível somente no Android";
        try
        {
            int result = EnumerateInstanceVersion(out uint version);
            if (result != 0) return $"Vulkan: driver retornou VkResult {result}; OpenGL ES ativo";
            uint major = version >> 22;
            uint minor = (version >> 12) & 0x3ff;
            uint patch = version & 0xfff;
            return $"Vulkan loader {major}.{minor}.{patch} detectado; renderização atual: OpenGL ES 3.0";
        }
        catch (EntryPointNotFoundException)
        {
            // vkEnumerateInstanceVersion was added in Vulkan 1.1; older loaders
            // can still expose Vulkan 1.0 through vkCreateInstance.
            return "Vulkan loader antigo (possível Vulkan 1.0); renderização atual: OpenGL ES 3.0";
        }
        catch (DllNotFoundException)
        {
            return "Vulkan não disponível neste dispositivo; OpenGL ES 3.0 ativo";
        }
        catch (Exception e)
        {
            return $"Falha ao consultar Vulkan ({e.GetType().Name}); OpenGL ES 3.0 ativo";
        }
    }
}
