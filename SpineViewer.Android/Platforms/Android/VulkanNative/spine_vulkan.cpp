#include <vulkan/vulkan.h>
#include <android/native_window.h>
#include <android/native_window_jni.h>
#include <android/log.h>
#include <algorithm>
#include <cstdint>
#include <vector>
#include <cstring>
#include <jni.h>

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
    VkRenderPass renderPass = VK_NULL_HANDLE;
    VkCommandPool commandPool = VK_NULL_HANDLE;
    VkCommandBuffer commandBuffer = VK_NULL_HANDLE;
    VkSemaphore imageAvailable = VK_NULL_HANDLE;
    std::vector<VkImageView> views;
    std::vector<VkFramebuffer> framebuffers;

    void shutdown() {
        if (device) {
            vkDeviceWaitIdle(device);
            if (imageAvailable) vkDestroySemaphore(device, imageAvailable, nullptr);
            if (commandPool) vkDestroyCommandPool(device, commandPool, nullptr);
            for (auto fb : framebuffers) vkDestroyFramebuffer(device, fb, nullptr);
            if (renderPass) vkDestroyRenderPass(device, renderPass, nullptr);
            for (auto view : views) vkDestroyImageView(device, view, nullptr);
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
        return createSwapchain() && createFrameResources();
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
    bool createFrameResources() {
        for (auto image : images) {
            VkImageViewCreateInfo info{VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO};
            info.image = image; info.viewType = VK_IMAGE_VIEW_TYPE_2D; info.format = format;
            info.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
            info.subresourceRange.levelCount = 1; info.subresourceRange.layerCount = 1;
            VkImageView view = VK_NULL_HANDLE;
            if (vkCreateImageView(device, &info, nullptr, &view) != VK_SUCCESS) return false;
            views.push_back(view);
        }
        VkAttachmentDescription attachment{};
        attachment.format = format; attachment.samples = VK_SAMPLE_COUNT_1_BIT;
        attachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR; attachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
        attachment.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
        attachment.finalLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR;
        VkAttachmentReference ref{0, VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL};
        VkSubpassDescription subpass{};
        subpass.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS;
        subpass.colorAttachmentCount = 1; subpass.pColorAttachments = &ref;
        VkSubpassDependency dependency{};
        dependency.srcSubpass = VK_SUBPASS_EXTERNAL; dependency.dstSubpass = 0;
        dependency.srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
        dependency.dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
        dependency.dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
        VkRenderPassCreateInfo pass{VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO};
        pass.attachmentCount = 1; pass.pAttachments = &attachment;
        pass.subpassCount = 1; pass.pSubpasses = &subpass;
        pass.dependencyCount = 1; pass.pDependencies = &dependency;
        if (vkCreateRenderPass(device, &pass, nullptr, &renderPass) != VK_SUCCESS) return false;
        for (auto view : views) {
            VkFramebufferCreateInfo info{VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO};
            info.renderPass = renderPass; info.attachmentCount = 1; info.pAttachments = &view;
            info.width = extent.width; info.height = extent.height; info.layers = 1;
            VkFramebuffer fb = VK_NULL_HANDLE;
            if (vkCreateFramebuffer(device, &info, nullptr, &fb) != VK_SUCCESS) return false;
            framebuffers.push_back(fb);
        }
        VkCommandPoolCreateInfo pool{VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO};
        pool.queueFamilyIndex = queueFamily;
        pool.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
        if (vkCreateCommandPool(device, &pool, nullptr, &commandPool) != VK_SUCCESS) return false;
        VkCommandBufferAllocateInfo alloc{VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO};
        alloc.commandPool = commandPool; alloc.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
        alloc.commandBufferCount = 1;
        if (vkAllocateCommandBuffers(device, &alloc, &commandBuffer) != VK_SUCCESS) return false;
        VkSemaphoreCreateInfo sem{VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO};
        return vkCreateSemaphore(device, &sem, nullptr, &imageAvailable) == VK_SUCCESS;
    }
    bool drawClearFrame() {
        if (!device || !swapchain) return false;
        uint32_t index = 0;
        VkResult result = vkAcquireNextImageKHR(device, swapchain, UINT64_MAX, imageAvailable, VK_NULL_HANDLE, &index);
        if (result != VK_SUCCESS && result != VK_SUBOPTIMAL_KHR) return false;
        if (vkResetCommandBuffer(commandBuffer, 0) != VK_SUCCESS) return false;
        VkCommandBufferBeginInfo begin{VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO};
        if (vkBeginCommandBuffer(commandBuffer, &begin) != VK_SUCCESS) return false;
        VkClearValue clear{};
        clear.color.float32[0] = 0.07f; clear.color.float32[1] = 0.11f;
        clear.color.float32[2] = 0.17f; clear.color.float32[3] = 1.f;
        VkRenderPassBeginInfo render{VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO};
        render.renderPass = renderPass; render.framebuffer = framebuffers[index];
        render.renderArea.extent = extent; render.clearValueCount = 1; render.pClearValues = &clear;
        vkCmdBeginRenderPass(commandBuffer, &render, VK_SUBPASS_CONTENTS_INLINE);
        vkCmdEndRenderPass(commandBuffer);
        if (vkEndCommandBuffer(commandBuffer) != VK_SUCCESS) return false;
        VkPipelineStageFlags waitStage = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
        VkSubmitInfo submit{VK_STRUCTURE_TYPE_SUBMIT_INFO};
        submit.waitSemaphoreCount = 1; submit.pWaitSemaphores = &imageAvailable;
        submit.pWaitDstStageMask = &waitStage;
        submit.commandBufferCount = 1; submit.pCommandBuffers = &commandBuffer;
        if (vkQueueSubmit(queue, 1, &submit, VK_NULL_HANDLE) != VK_SUCCESS) return false;
        // Prototype: queue idle avoids reuse races. Replace with per-frame fences for 60 FPS.
        if (vkQueueWaitIdle(queue) != VK_SUCCESS) return false;
        VkPresentInfoKHR present{VK_STRUCTURE_TYPE_PRESENT_INFO_KHR};
        present.swapchainCount = 1; present.pSwapchains = &swapchain; present.pImageIndices = &index;
        result = vkQueuePresentKHR(queue, &present);
        return result == VK_SUCCESS || result == VK_SUBOPTIMAL_KHR;
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
void* spine_vk_create_surface(void* jniEnv, void* javaSurface) {
    auto* env = reinterpret_cast<JNIEnv*>(jniEnv);
    if (!env || !javaSurface) return nullptr;
    ANativeWindow* window = ANativeWindow_fromSurface(env, reinterpret_cast<jobject>(javaSurface));
    if (!window) return nullptr;
    void* handle = spine_vk_create(window);
    ANativeWindow_release(window);
    return handle;
}
int spine_vk_draw_clear(void* handle) {
    auto* r = static_cast<Renderer*>(handle);
    return r && r->drawClearFrame() ? 1 : 0;
}
void spine_vk_destroy(void* handle) {
    auto* r = static_cast<Renderer*>(handle);
    if (r) { r->shutdown(); delete r; }
}
}
