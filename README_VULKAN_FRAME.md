# Vulkan first-frame preview (experimental)

The GitHub Actions workflow now builds `libspine_vulkan.so` using the Android NDK and bundles it for arm64-v8a. The app has a **Vulkan: testar primeiro quadro** button opening an Android SurfaceView preview. Native code creates Vulkan instance/device/swapchain, image views, render pass, framebuffers, command buffer, semaphore, and submits a clear-color frame via vkQueuePresentKHR. OpenGL remains the active Spine renderer.

**Not yet done:** Vulkan Spine geometry, SPIR-V pipeline, textures, blend modes, resize/recreate, threaded render loop, synchronization optimization. Preview only renders a solid background; it does not show characters. Build and device testing have not been performed here. The native Android interop bindings and CI toolchain require validation. Vulkan fallback is effectively OpenGL remaining the main renderer.
