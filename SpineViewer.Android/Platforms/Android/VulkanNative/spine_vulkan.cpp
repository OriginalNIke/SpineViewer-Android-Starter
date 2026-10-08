#include <vulkan/vulkan.h>
#include <android/native_window.h>
#include <android/log.h>
#include <algorithm>
#include <cstdint>
#include <vector>
#include <cstring>

// Native Vulkan foundation. Not yet a complete Spine renderer: the shader
// modules, descriptor sets, vertex uploads, command buffers and presentation
// loop must be implemented before switching the MAUI view to this backend.
namespace {
struct Renderer {
    VkInstance instance = VK_NULL_HANDLE;
    VkPhysicalDevice physical = VK_NULL_HANDLE;
    VkDevice device = VK_NULL_HANDLE;
    VkSurfaceKHR surface = VK_NULL_HANDLE;
    VkQueue queue = VK_NULL_HANDLE;
    uint32_t queueFamily = UINT32_MAX;
    VkSwapchainKHR swapchain = VK_NULL_HANDLE;
    VkFormat format = VK_FORMAT_UNDEFINED;
    VkExtent2D extent{};
    std::vector<VkImage> images;
    ANativeWindow* window = nullptr;

    void shutdown() {
        if (device) {
            vkDeviceWaitIdle(device);
            if (swapchain) vkDestroySwapchainKHR(device, swapchain, nullptr);
            vkDestroyDevice(device, nullptr);
        }
        if (surface) vkDestroySurfaceKHR(instance, surface, nullptr);
        if (instance) vkDestroyInstance(instance, nullptr);
        if (window) ANativeWindow_release(window);
        *this = {};
    }
    bool init(ANativeWindow* nativeWindow) {
        if (!nativeWindow) return false;
        window = nativeWindow;
        ANativeWindow_acquire(window);
        const char* extensions[] = { VK_KHR_SURFACE_EXTENSION_NAME, VK_KHR_ANDROID_SURFACE_EXTENSION_NAME };
        VkApplicationInfo app{VK_STRUCTURE_TYPE_APPLICATION_INFO};
        app.pApplicationName = "SpineViewer";
        app.apiVersion = VK_API_VERSION_1_0;
        VkInstanceCreateInfo ici{VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO};
        ici.pApplicationInfo = &app;
        ici.enabledExtensionCount = 2;
        ici.ppEnabledExtensionNames = extensions;
        if (vkCreateInstance(&ici, nullptr, &instance) != VK_SUCCESS) return false;
        VkAndroidSurfaceCreateInfoKHR sci{VK_STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR};
        sci.window = window;
        if (vkCreateAndroidSurfaceKHR(instance, &sci, nullptr, &surface) != VK_SUCCESS) return false;
        uint32_t count = 0;
        if (vkEnumeratePhysicalDevices(instance, &count, nullptr) != VK_SUCCESS || !count) return false;
        std::vector<VkPhysicalDevice> devices(count);
        vkEnumeratePhysicalDevices(instance, &count, devices.data());
        for (auto candidate : devices) {
            uint32_t families = 0;
            vkGetPhysicalDeviceQueueFamilyProperties(candidate, &families, nullptr);
            std::vector<VkQueueFamilyProperties> props(families);
            vkGetPhysicalDeviceQueueFamilyProperties(candidate, &families, props.data());
            for (uint32_t i=0; i<families; ++i) {
                VkBool32 present = VK_FALSE;
                vkGetPhysicalDeviceSurfaceSupportKHR(candidate, i, surface, &present);
                if ((props[i].queueFlags & VK_QUEUE_GRAPHICS_BIT) && present) {
                    physical = candidate; queueFamily = i; break;
                }
            }
            if (physical) break;
        }
        if (!physical) return false;
        float priority = 1.f;
        VkDeviceQueueCreateInfo qci{VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO};
        qci.queueFamilyIndex = queueFamily;
        qci.queueCount = 1;
        qci.pQueuePriorities = &priority;
        const char* deviceExtensions[] = { VK_KHR_SWAPCHAIN_EXTENSION_NAME };
        VkDeviceCreateInfo dci{VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO};
        dci.queueCreateInfoCount = 1;
        dci.pQueueCreateInfos = &qci;
        dci.enabledExtensionCount = 1;
        dci.ppEnabledExtensionNames = deviceExtensions;
        if (vkCreateDevice(physical, &dci, nullptr, &device) != VK_SUCCESS) return false;
        vkGetDeviceQueue(device, queueFamily, 0, &queue);
        return createSwapchain();
    }
    bool createSwapchain() {
        VkSurfaceCapabilitiesKHR caps{};
        if (vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical, surface, &caps) != VK_SUCCESS) return false;
        uint32_t count = 0;
        vkGetPhysicalDeviceSurfaceFormatsKHR(physical, surface, &count, nullptr);
        if (!count) return false;
        std::vector<VkSurfaceFormatKHR> formats(count);
        vkGetPhysicalDeviceSurfaceFormatsKHR(physical, surface, &count, formats.data());
        VkSurfaceFormatKHR chosen = formats[0];
        for (auto f : formats) if (f.format == VK_FORMAT_R8G8B8A8_UNORM || f.format == VK_FORMAT_B8G8R8A8_UNORM) { chosen=f; break; }
        format = chosen.format;
        if (caps.currentExtent.width != UINT32_MAX) extent = caps.currentExtent;
        else {
            extent.width = std::clamp((uint32_t)ANativeWindow_getWidth(window), caps.minImageExtent.width, caps.maxImageExtent.width);
            extent.height = std::clamp((uint32_t)ANativeWindow_getHeight(window), caps.minImageExtent.height, caps.maxImageExtent.height);
        }
        uint32_t modesCount = 0;
        vkGetPhysicalDeviceSurfacePresentModesKHR(physical, surface, &modesCount, nullptr);
        if (!modesCount) return false;
        VkSwapchainCreateInfoKHR ci{VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR};
        ci.surface = surface;
        ci.minImageCount = std::min(std::max(caps.minImageCount + 1, 2u), caps.maxImageCount ? caps.maxImageCount : UINT32_MAX);
        ci.imageFormat = chosen.format;
        ci.imageColorSpace = chosen.colorSpace;
        ci.imageExtent = extent;
        ci.imageArrayLayers = 1;
        ci.imageUsage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT;
        ci.imageSharingMode = VK_SHARING_MODE_EXCLUSIVE;
        ci.preTransform = caps.currentTransform;
        ci.compositeAlpha = (caps.supportedCompositeAlpha & VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR) ? VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR : VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR;
        ci.presentMode = VK_PRESENT_MODE_FIFO_KHR;
        ci.clipped = VK_TRUE;
        if (vkCreateSwapchainKHR(device, &ci, nullptr, &swapchain) != VK_SUCCESS) return false;
        vkGetSwapchainImagesKHR(device, swapchain, &count, nullptr);
        images.resize(count);
        return vkGetSwapchainImagesKHR(device, swapchain, &count, images.data()) == VK_SUCCESS;
    }
};
}
extern "C" {
// JNI-independent C API: Android Java Surface must be converted with
// ANativeWindow_fromSurface(env, surface) by the future JNI adapter.
void* spine_vk_create(ANativeWindow* window) {
    auto* r = new Renderer;
    if (!r->init(window)) { r->shutdown(); delete r; return nullptr; }
    return r;
}
void spine_vk_destroy(void* handle) {
    auto* r = static_cast<Renderer*>(handle);
    if (r) { r->shutdown(); delete r; }
}
}
