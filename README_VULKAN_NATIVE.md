# Vulkan native foundation (not a complete renderer)

This package adds `SpineViewer.Android/Platforms/Android/VulkanNative/` with:

- Vulkan instance, Android native surface, physical device and graphics/present queue selection
- Logical device and FIFO swapchain creation, swapchain image retrieval, cleanup
- GLSL 450 vertex/fragment shaders **source** for later SPIR-V compilation
- CMake definition for Android NDK

## Not yet implemented

- Android `SurfaceView` -> JNI `ANativeWindow_fromSurface` adapter and MAUI handler
- SPIR-V compilation and runtime shader modules
- Image views, render pass, framebuffers, render pipelines for Normal/Additive/Multiply/Screen
- Atlas PNG decoding and VkImage upload, sampler and descriptor sets
- Vertex buffers, command buffers, acquire/submit/present, resize/recreate and synchronization
- Automatic Vulkan-to-OpenGL fallback after initialization failure

The **existing OpenGL ES 3.0 viewer remains active**. The native source is not wired into the `.csproj` or GitHub Actions yet, so this archive is a foundation, not a completed Vulkan migration. Do not advertise it as rendering via Vulkan.

To compile the native library separately on a machine with Android NDK:

```sh
cmake -S SpineViewer.Android/Platforms/Android/VulkanNative -B build-vulkan \
  -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-26
cmake --build build-vulkan
```

Shaders need `glslangValidator -V shaders/spine.vert -o spine.vert.spv` and the equivalent fragment shader command before Vulkan pipeline creation.
