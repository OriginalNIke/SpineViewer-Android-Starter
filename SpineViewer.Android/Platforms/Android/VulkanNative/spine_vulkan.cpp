#ifndef VK_USE_PLATFORM_ANDROID_KHR
#define VK_USE_PLATFORM_ANDROID_KHR 1
#endif
#include <vulkan/vulkan.h>
#include <vulkan/vulkan_android.h>
#include <android/native_window.h>
#include <android/native_window_jni.h>
#include <android/log.h>
#include <algorithm>
#include <cstdint>
#include <vector>
#include <cstring>
#include <jni.h>
#include <unordered_map>
#include <string>
#include <array>

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
    // Vulkan textured Spine renderer resources.
    VkDescriptorSetLayout descriptorLayout = VK_NULL_HANDLE;
    VkPipelineLayout pipelineLayout = VK_NULL_HANDLE;
    VkDescriptorPool descriptorPool = VK_NULL_HANDLE;
    VkSampler sampler = VK_NULL_HANDLE;
    std::array<VkPipeline, 4> pipelines{};
    struct Texture {
        VkImage image = VK_NULL_HANDLE;
        VkDeviceMemory memory = VK_NULL_HANDLE;
        VkImageView view = VK_NULL_HANDLE;
        VkDescriptorSet descriptor = VK_NULL_HANDLE;
    };
    std::unordered_map<std::string, Texture> textures;
    struct Batch { std::string page; int blend; std::vector<float> xyuv; };
    std::vector<Batch> batches;
    VkBuffer vertexBuffer = VK_NULL_HANDLE;
    VkDeviceMemory vertexMemory = VK_NULL_HANDLE;
    VkDeviceSize vertexCapacity = 0;
    std::vector<uint8_t> vertSpv, fragSpv;
    float cx=0, cy=0, zoom=1;

    uint32_t memoryType(uint32_t bits, VkMemoryPropertyFlags flags) {
        VkPhysicalDeviceMemoryProperties props{};
        vkGetPhysicalDeviceMemoryProperties(physical, &props);
        for(uint32_t i=0;i<props.memoryTypeCount;++i)
            if ((bits & (1u<<i)) && (props.memoryTypes[i].propertyFlags & flags)==flags) return i;
        return UINT32_MAX;
    }
    bool makeBuffer(VkDeviceSize bytes, VkBufferUsageFlags usage, VkBuffer& buf, VkDeviceMemory& mem) {
        VkBufferCreateInfo info{VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO};
        info.size=bytes; info.usage=usage; info.sharingMode=VK_SHARING_MODE_EXCLUSIVE;
        if(vkCreateBuffer(device,&info,nullptr,&buf)!=VK_SUCCESS) return false;
        VkMemoryRequirements req{}; vkGetBufferMemoryRequirements(device,buf,&req);
        auto type=memoryType(req.memoryTypeBits,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT|VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
        if(type==UINT32_MAX) return false;
        VkMemoryAllocateInfo alloc{VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO};
        alloc.allocationSize=req.size; alloc.memoryTypeIndex=type;
        if(vkAllocateMemory(device,&alloc,nullptr,&mem)!=VK_SUCCESS) return false;
        return vkBindBufferMemory(device,buf,mem,0)==VK_SUCCESS;
    }
    void releasePipelines() {
        for(auto& p:pipelines) { if(p) vkDestroyPipeline(device,p,nullptr); p=VK_NULL_HANDLE; }
        if(pipelineLayout) vkDestroyPipelineLayout(device,pipelineLayout,nullptr);
        pipelineLayout=VK_NULL_HANDLE;
    }
    void releaseTexture(Texture& t) {
        if(t.view) vkDestroyImageView(device,t.view,nullptr);
        if(t.image) vkDestroyImage(device,t.image,nullptr);
        if(t.memory) vkFreeMemory(device,t.memory,nullptr);
        t={};
    }
    bool setupPipelines() {
        if(vertSpv.empty() || fragSpv.empty()) return false;
        VkShaderModuleCreateInfo sm{VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO};
        sm.codeSize=vertSpv.size(); sm.pCode=reinterpret_cast<const uint32_t*>(vertSpv.data());
        VkShaderModule vs=VK_NULL_HANDLE, fs=VK_NULL_HANDLE;
        if(vkCreateShaderModule(device,&sm,nullptr,&vs)!=VK_SUCCESS) return false;
        sm.codeSize=fragSpv.size(); sm.pCode=reinterpret_cast<const uint32_t*>(fragSpv.data());
        if(vkCreateShaderModule(device,&sm,nullptr,&fs)!=VK_SUCCESS) { vkDestroyShaderModule(device,vs,nullptr); return false; }
        VkDescriptorSetLayoutBinding binding{};
        binding.binding=0; binding.descriptorType=VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
        binding.descriptorCount=1; binding.stageFlags=VK_SHADER_STAGE_FRAGMENT_BIT;
        VkDescriptorSetLayoutCreateInfo dl{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO};
        dl.bindingCount=1; dl.pBindings=&binding;
        if(vkCreateDescriptorSetLayout(device,&dl,nullptr,&descriptorLayout)!=VK_SUCCESS) { vkDestroyShaderModule(device,vs,nullptr); vkDestroyShaderModule(device,fs,nullptr); return false; }
        VkPushConstantRange push{}; push.stageFlags=VK_SHADER_STAGE_VERTEX_BIT; push.size=sizeof(float)*5;
        VkPipelineLayoutCreateInfo pl{VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO};
        pl.setLayoutCount=1; pl.pSetLayouts=&descriptorLayout;
        pl.pushConstantRangeCount=1; pl.pPushConstantRanges=&push;
        bool ok=vkCreatePipelineLayout(device,&pl,nullptr,&pipelineLayout)==VK_SUCCESS;
        VkPipelineShaderStageCreateInfo stages[2]{};
        stages[0].sType=VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
        stages[0].stage=VK_SHADER_STAGE_VERTEX_BIT; stages[0].module=vs; stages[0].pName="main";
        stages[1].sType=VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
        stages[1].stage=VK_SHADER_STAGE_FRAGMENT_BIT; stages[1].module=fs; stages[1].pName="main";
        VkVertexInputBindingDescription vb{0,16,VK_VERTEX_INPUT_RATE_VERTEX};
        VkVertexInputAttributeDescription attrs[2]={{0,0,VK_FORMAT_R32G32_SFLOAT,0},{1,0,VK_FORMAT_R32G32_SFLOAT,8}};
        VkPipelineVertexInputStateCreateInfo vi{VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO};
        vi.vertexBindingDescriptionCount=1; vi.pVertexBindingDescriptions=&vb;
        vi.vertexAttributeDescriptionCount=2; vi.pVertexAttributeDescriptions=attrs;
        VkPipelineInputAssemblyStateCreateInfo ia{VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO};
        ia.topology=VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
        VkPipelineViewportStateCreateInfo vp{VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO};
        vp.viewportCount=1; vp.scissorCount=1;
        VkPipelineRasterizationStateCreateInfo rs{VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO};
        rs.polygonMode=VK_POLYGON_MODE_FILL; rs.cullMode=VK_CULL_MODE_NONE;
        rs.frontFace=VK_FRONT_FACE_COUNTER_CLOCKWISE; rs.lineWidth=1;
        VkPipelineMultisampleStateCreateInfo ms{VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO};
        ms.rasterizationSamples=VK_SAMPLE_COUNT_1_BIT;
        VkPipelineColorBlendAttachmentState blend{}; blend.colorWriteMask=0xf; blend.blendEnable=VK_TRUE;
        VkPipelineColorBlendStateCreateInfo cb{VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO};
        cb.attachmentCount=1; cb.pAttachments=&blend;
        VkDynamicState dynamic[]={VK_DYNAMIC_STATE_VIEWPORT,VK_DYNAMIC_STATE_SCISSOR};
        VkPipelineDynamicStateCreateInfo ds{VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO};
        ds.dynamicStateCount=2; ds.pDynamicStates=dynamic;
        VkGraphicsPipelineCreateInfo gp{VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO};
        gp.stageCount=2; gp.pStages=stages; gp.pVertexInputState=&vi;
        gp.pInputAssemblyState=&ia; gp.pViewportState=&vp;
        gp.pRasterizationState=&rs; gp.pMultisampleState=&ms;
        gp.pColorBlendState=&cb; gp.pDynamicState=&ds;
        gp.layout=pipelineLayout; gp.renderPass=renderPass;
        for(int mode=0;ok && mode<4;++mode) {
            // Straight alpha textures: normal, additive, multiply, screen.
            blend.srcColorBlendFactor=VK_BLEND_FACTOR_SRC_ALPHA;
            blend.dstColorBlendFactor=VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
            if(mode==1) blend.dstColorBlendFactor=VK_BLEND_FACTOR_ONE;
            if(mode==2) {blend.srcColorBlendFactor=VK_BLEND_FACTOR_DST_COLOR;blend.dstColorBlendFactor=VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;}
            if(mode==3) {blend.srcColorBlendFactor=VK_BLEND_FACTOR_ONE;blend.dstColorBlendFactor=VK_BLEND_FACTOR_ONE_MINUS_SRC_COLOR;}
            blend.colorBlendOp=VK_BLEND_OP_ADD;
            blend.srcAlphaBlendFactor=VK_BLEND_FACTOR_ONE;
            blend.dstAlphaBlendFactor=VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
            blend.alphaBlendOp=VK_BLEND_OP_ADD;
            ok=vkCreateGraphicsPipelines(device,VK_NULL_HANDLE,1,&gp,nullptr,&pipelines[mode])==VK_SUCCESS;
        }
        vkDestroyShaderModule(device,vs,nullptr); vkDestroyShaderModule(device,fs,nullptr);
        if(!ok) releasePipelines();
        if(!ok) return false;
        VkSamplerCreateInfo si{VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO};
        si.magFilter=VK_FILTER_LINEAR; si.minFilter=VK_FILTER_LINEAR;
        si.addressModeU=si.addressModeV=si.addressModeW=VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE;
        si.maxLod=1;
        if(vkCreateSampler(device,&si,nullptr,&sampler)!=VK_SUCCESS) return false;
        VkDescriptorPoolSize poolSize{VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,256};
        VkDescriptorPoolCreateInfo pi{VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO};
        pi.maxSets=256; pi.poolSizeCount=1; pi.pPoolSizes=&poolSize;
        return vkCreateDescriptorPool(device,&pi,nullptr,&descriptorPool)==VK_SUCCESS;
    }
    bool setShaders(const uint8_t* v, int nv, const uint8_t* f, int nf) {
        if(!v||!f||nv<4||nf<4||nv%4||nf%4||device==VK_NULL_HANDLE) return false;
        vkDeviceWaitIdle(device);
        for(auto& kv:textures) releaseTexture(kv.second);
        textures.clear();
        if(descriptorPool) vkDestroyDescriptorPool(device,descriptorPool,nullptr);
        descriptorPool=VK_NULL_HANDLE;
        if(sampler) vkDestroySampler(device,sampler,nullptr);
        sampler=VK_NULL_HANDLE;
        releasePipelines();
        if(descriptorLayout) vkDestroyDescriptorSetLayout(device,descriptorLayout,nullptr);
        descriptorLayout=VK_NULL_HANDLE;
        vertSpv.assign(v,v+nv); fragSpv.assign(f,f+nf);
        return setupPipelines();
    }
    bool uploadTexture(const char* name, const uint8_t* pixels, int w, int h) {
        if(!name||!pixels||w<=0||h<=0||!descriptorPool||w>8192||h>8192) return false;
        vkDeviceWaitIdle(device);
        auto it=textures.find(name);
        if(it!=textures.end()) { releaseTexture(it->second); textures.erase(it); }
        VkDeviceSize size=VkDeviceSize(w)*h*4;
        VkBuffer staging=VK_NULL_HANDLE; VkDeviceMemory stageMemory=VK_NULL_HANDLE;
        if(!makeBuffer(size,VK_BUFFER_USAGE_TRANSFER_SRC_BIT,staging,stageMemory)) return false;
        void* mapped=nullptr;
        if(vkMapMemory(device,stageMemory,0,size,0,&mapped)!=VK_SUCCESS) return false;
        memcpy(mapped,pixels,size); vkUnmapMemory(device,stageMemory);
        Texture tex{};
        VkImageCreateInfo ci{VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO};
        ci.imageType=VK_IMAGE_TYPE_2D; ci.format=VK_FORMAT_R8G8B8A8_UNORM;
        ci.extent={uint32_t(w),uint32_t(h),1}; ci.mipLevels=1; ci.arrayLayers=1;
        ci.samples=VK_SAMPLE_COUNT_1_BIT; ci.tiling=VK_IMAGE_TILING_OPTIMAL;
        ci.usage=VK_IMAGE_USAGE_TRANSFER_DST_BIT|VK_IMAGE_USAGE_SAMPLED_BIT;
        ci.initialLayout=VK_IMAGE_LAYOUT_UNDEFINED;
        bool ok=vkCreateImage(device,&ci,nullptr,&tex.image)==VK_SUCCESS;
        VkMemoryRequirements req{};
        if(ok) vkGetImageMemoryRequirements(device,tex.image,&req);
        uint32_t type=ok?memoryType(req.memoryTypeBits,VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT):UINT32_MAX;
        VkMemoryAllocateInfo alloc{VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO};
        alloc.allocationSize=req.size; alloc.memoryTypeIndex=type;
        ok=ok && type!=UINT32_MAX && vkAllocateMemory(device,&alloc,nullptr,&tex.memory)==VK_SUCCESS;
        ok=ok && vkBindImageMemory(device,tex.image,tex.memory,0)==VK_SUCCESS;
        if(ok) {
            vkResetCommandBuffer(commandBuffer,0);
            VkCommandBufferBeginInfo begin{VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO};
            begin.flags=VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
            ok=vkBeginCommandBuffer(commandBuffer,&begin)==VK_SUCCESS;
            VkImageMemoryBarrier barrier{VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER};
            barrier.srcQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED;
            barrier.dstQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED;
            barrier.oldLayout=VK_IMAGE_LAYOUT_UNDEFINED;
            barrier.newLayout=VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
            barrier.dstAccessMask=VK_ACCESS_TRANSFER_WRITE_BIT;
            barrier.image=tex.image;
            barrier.subresourceRange.aspectMask=VK_IMAGE_ASPECT_COLOR_BIT;
            barrier.subresourceRange.levelCount=1; barrier.subresourceRange.layerCount=1;
            if(ok) vkCmdPipelineBarrier(commandBuffer,VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,VK_PIPELINE_STAGE_TRANSFER_BIT,0,0,nullptr,0,nullptr,1,&barrier);
            VkBufferImageCopy copy{}; copy.imageSubresource.aspectMask=VK_IMAGE_ASPECT_COLOR_BIT;
            copy.imageSubresource.layerCount=1; copy.imageExtent={uint32_t(w),uint32_t(h),1};
            if(ok) vkCmdCopyBufferToImage(commandBuffer,staging,tex.image,VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,1,&copy);
            barrier.oldLayout=VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
            barrier.newLayout=VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            barrier.srcAccessMask=VK_ACCESS_TRANSFER_WRITE_BIT;
            barrier.dstAccessMask=VK_ACCESS_SHADER_READ_BIT;
            if(ok) vkCmdPipelineBarrier(commandBuffer,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT,0,0,nullptr,0,nullptr,1,&barrier);
            ok=ok && vkEndCommandBuffer(commandBuffer)==VK_SUCCESS;
            VkSubmitInfo submit{VK_STRUCTURE_TYPE_SUBMIT_INFO};
            submit.commandBufferCount=1; submit.pCommandBuffers=&commandBuffer;
            ok=ok && vkQueueSubmit(queue,1,&submit,VK_NULL_HANDLE)==VK_SUCCESS;
            if(ok) vkQueueWaitIdle(queue);
        }
        if(staging) vkDestroyBuffer(device,staging,nullptr);
        if(stageMemory) vkFreeMemory(device,stageMemory,nullptr);
        if(ok) {
            VkImageViewCreateInfo iv{VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO};
            iv.image=tex.image; iv.viewType=VK_IMAGE_VIEW_TYPE_2D; iv.format=VK_FORMAT_R8G8B8A8_UNORM;
            iv.subresourceRange.aspectMask=VK_IMAGE_ASPECT_COLOR_BIT;
            iv.subresourceRange.levelCount=1; iv.subresourceRange.layerCount=1;
            ok=vkCreateImageView(device,&iv,nullptr,&tex.view)==VK_SUCCESS;
        }
        if(ok) {
            VkDescriptorSetAllocateInfo da{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO};
            da.descriptorPool=descriptorPool; da.descriptorSetCount=1; da.pSetLayouts=&descriptorLayout;
            ok=vkAllocateDescriptorSets(device,&da,&tex.descriptor)==VK_SUCCESS;
            if(ok) {
                VkDescriptorImageInfo image{};
                image.sampler=sampler; image.imageView=tex.view;
                image.imageLayout=VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                VkWriteDescriptorSet write{VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET};
                write.dstSet=tex.descriptor; write.dstBinding=0;
                write.descriptorType=VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
                write.descriptorCount=1; write.pImageInfo=&image;
                vkUpdateDescriptorSets(device,1,&write,0,nullptr);
            }
        }
        if(ok) textures.emplace(name,tex);
        else releaseTexture(tex);
        return ok;
    }
    bool setFrame(const float* xyuv, int floatCount, const int* counts, const int* modes, const char* const* pages, int batchCount, float x, float y, float z) {
        if(!device||batchCount<0||floatCount<0||!xyuv||!counts||!modes||!pages) return false;
        std::vector<Batch> next;
        int offset=0;
        for(int i=0;i<batchCount;++i) {
            int n=counts[i];
            if(n<0||n%12||n>floatCount-offset||!pages[i]) return false;
            Batch b; b.page=pages[i]; b.blend=std::clamp(modes[i],0,3);
            b.xyuv.assign(xyuv+offset,xyuv+offset+n);
            next.push_back(std::move(b)); offset+=n;
        }
        if(offset!=floatCount) return false;
        batches=std::move(next); cx=x;cy=y;zoom=z;
        return true;
    }
    bool updateVertices() {
        VkDeviceSize bytes=0;
        for(auto& b:batches) bytes+=b.xyuv.size()*sizeof(float);
        if(bytes==0) return true;
        if(bytes>vertexCapacity) {
            if(vertexBuffer) vkDestroyBuffer(device,vertexBuffer,nullptr);
            if(vertexMemory) vkFreeMemory(device,vertexMemory,nullptr);
            vertexBuffer=VK_NULL_HANDLE;vertexMemory=VK_NULL_HANDLE;
            vertexCapacity=std::max(bytes,vertexCapacity*2);
            if(!makeBuffer(vertexCapacity,VK_BUFFER_USAGE_VERTEX_BUFFER_BIT,vertexBuffer,vertexMemory)) return false;
        }
        void* dst=nullptr;
        if(vkMapMemory(device,vertexMemory,0,bytes,0,&dst)!=VK_SUCCESS) return false;
        auto* ptr=static_cast<uint8_t*>(dst);
        for(auto& b:batches) { memcpy(ptr,b.xyuv.data(),b.xyuv.size()*sizeof(float));ptr+=b.xyuv.size()*sizeof(float); }
        vkUnmapMemory(device,vertexMemory);
        return true;
    }


    void shutdown() {
        if (device) {
            vkDeviceWaitIdle(device);
            for(auto& kv:textures) releaseTexture(kv.second);
            textures.clear();
            if(vertexBuffer) vkDestroyBuffer(device,vertexBuffer,nullptr);
            if(vertexMemory) vkFreeMemory(device,vertexMemory,nullptr);
            if(descriptorPool) vkDestroyDescriptorPool(device,descriptorPool,nullptr);
            if(sampler) vkDestroySampler(device,sampler,nullptr);
            releasePipelines();
            if(descriptorLayout) vkDestroyDescriptorSetLayout(device,descriptorLayout,nullptr);
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
        if (!updateVertices()) return false;
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
        if(pipelineLayout && vertexBuffer) {
            VkViewport viewport{0,0,float(extent.width),float(extent.height),0,1};
            VkRect2D scissor{{0,0},extent};
            vkCmdSetViewport(commandBuffer,0,1,&viewport);
            vkCmdSetScissor(commandBuffer,0,1,&scissor);
            VkDeviceSize offset=0;
            vkCmdBindVertexBuffers(commandBuffer,0,1,&vertexBuffer,&offset);
            float view[5]={cx,cy,float(extent.width)*0.5f,float(extent.height)*0.5f,zoom};
            vkCmdPushConstants(commandBuffer,pipelineLayout,VK_SHADER_STAGE_VERTEX_BIT,0,sizeof(view),view);
            uint32_t first=0;
            for(auto& batch:batches) {
                auto it=textures.find(batch.page);
                if(it!=textures.end() && pipelines[batch.blend]) {
                    vkCmdBindPipeline(commandBuffer,VK_PIPELINE_BIND_POINT_GRAPHICS,pipelines[batch.blend]);
                    vkCmdBindDescriptorSets(commandBuffer,VK_PIPELINE_BIND_POINT_GRAPHICS,pipelineLayout,0,1,&it->second.descriptor,0,nullptr);
                    vkCmdDraw(commandBuffer,uint32_t(batch.xyuv.size()/4),1,first,0);
                }
                first+=uint32_t(batch.xyuv.size()/4);
            }
        }
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
int spine_vk_set_shaders(void* handle,const uint8_t* vs,int nvs,const uint8_t* fs,int nfs) {
    auto* r=static_cast<Renderer*>(handle);
    return r && r->setShaders(vs,nvs,fs,nfs) ? 1 : 0;
}
int spine_vk_set_texture_rgba(void* handle,const char* name,const uint8_t* rgba,int width,int height) {
    auto* r=static_cast<Renderer*>(handle);
    return r && r->uploadTexture(name,rgba,width,height) ? 1 : 0;
}
int spine_vk_set_frame(void* handle,const float* xyuv,int floats,const int* counts,const int* modes,const char* const* pages,int batches,float cx,float cy,float zoom) {
    auto* r=static_cast<Renderer*>(handle);
    return r && r->setFrame(xyuv,floats,counts,modes,pages,batches,cx,cy,zoom) ? 1 : 0;
}
void spine_vk_destroy(void* handle) {
    auto* r = static_cast<Renderer*>(handle);
    if (r) { r->shutdown(); delete r; }
}
}
