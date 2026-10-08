using BlendMode = SpineRuntime41.BlendMode;
using Microsoft.Maui.Handlers;
using Android.Opengl;
using Android.Graphics;
using Java.Nio;
using GLES30 = Android.Opengl.GLES30;
using SpineRuntime41;

namespace SpineViewer.Android;

// Native OpenGL ES 3 surface. All GL calls run on the GL thread.
public sealed class SpineGLView : Microsoft.Maui.Controls.View
{
    internal SpineGLSurface? Surface;
    public Func<IReadOnlyList<SpineTriangle>> GetTriangles { get; set; } = () => Array.Empty<SpineTriangle>();
    public SpineGLView() { HeightRequest = -1; }
    public void SetTexture(string name, byte[] png) => Surface?.SetTexture(name, png);
    public void InvalidateSurface() => Surface?.UpdateFrame(GetTriangles());
}

public sealed class SpineGLHandler : ViewHandler<SpineGLView, SpineGLSurface>
{
    public static readonly IPropertyMapper<SpineGLView, SpineGLHandler> Mapper = new PropertyMapper<SpineGLView, SpineGLHandler>(ViewMapper);
    public SpineGLHandler() : base(Mapper) { }
    protected override SpineGLSurface CreatePlatformView() => new(Context);
    protected override void ConnectHandler(SpineGLSurface platformView)
    {
        base.ConnectHandler(platformView);
        VirtualView.Surface = platformView;
    }
    protected override void DisconnectHandler(SpineGLSurface platformView)
    {
        VirtualView.Surface = null;
        platformView.OnPause();
        base.DisconnectHandler(platformView);
    }
}

public sealed class SpineGLSurface : GLSurfaceView
{
    readonly SpineGLRenderer renderer = new();
    readonly SpineTouch touch;
    public SpineGLSurface(global::Android.Content.Context context) : base(context)
    {
        touch = new SpineTouch(RequestRender);
        SetEGLContextClientVersion(3);
        SetRenderer(renderer);
        RenderMode = Rendermode.WhenDirty;
    }
    public override bool OnTouchEvent(global::Android.Views.MotionEvent? e) => touch.Handle(this, e);
    public void SetTexture(string name, byte[] png)
    {
        renderer.SetTexture(name, png);
        RequestRender();
    }
    public void UpdateFrame(IReadOnlyList<SpineTriangle> triangles)
    {
        renderer.UpdateFrame(triangles);
        RequestRender();
    }
}

internal sealed class SpineGLRenderer : Java.Lang.Object, GLSurfaceView.IRenderer
{
    readonly object sync = new();
    readonly Dictionary<string, byte[]> pngs = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> textureIds = new(StringComparer.OrdinalIgnoreCase);
    readonly List<(string page, BlendMode blend, float[] vertices)> batches = new();
    int program, posAttr, uvAttr, samplerUniform, sizeUniform, halfHeightUniform;
    int vertexBuffer;
    int backgroundVersion = -1;
    int width = 1, height = 1;
    float centerX, centerY, scale = 1;
    public void SetTexture(string name, byte[] bytes)
    {
        lock (sync) pngs[name] = (byte[])bytes.Clone();
    }
    private float modelWidth = 1f, modelHeight = 1f;
    public void UpdateFrame(IReadOnlyList<SpineTriangle> triangles)
    {
        lock (sync)
        {
            batches.Clear();
            if (triangles.Count == 0) return;
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            foreach(var t in triangles) for(int j=0;j<6;j+=2) {
                minX=Math.Min(minX,t.XY[j]); maxX=Math.Max(maxX,t.XY[j]);
                minY=Math.Min(minY,t.XY[j+1]); maxY=Math.Max(maxY,t.XY[j+1]);
            }
            centerX=(minX+maxX)*0.5f; centerY=(minY+maxY)*0.5f;
            modelWidth = Math.Max(1f, maxX-minX);
            modelHeight = Math.Max(1f, maxY-minY);
            int start=0;
            while(start<triangles.Count) {
                string page=triangles[start].Page;
                BlendMode blend=triangles[start].Blend;
                int end=start+1;
                while(end<triangles.Count && string.Equals(page,triangles[end].Page,StringComparison.OrdinalIgnoreCase) && blend == triangles[end].Blend) end++;
                var data=new float[(end-start)*3*4];
                int n=0;
                for(int i=start;i<end;i++) for(int j=0;j<3;j++) {
                    data[n++]=triangles[i].XY[j*2]; data[n++]=triangles[i].XY[j*2+1];
                    data[n++]=triangles[i].UV[j*2]; data[n++]=triangles[i].UV[j*2+1];
                }
                batches.Add((page,blend,data)); start=end;
            }
        }
    }
    const string VertexShader = "#version 300 es\nprecision highp float;\nin vec2 aPos; in vec2 aUV; uniform vec4 uView; uniform float uHalfHeight; out vec2 vUV; void main(){ vec2 p=(aPos-uView.xy)*uView.z; gl_Position=vec4(p.x/uView.w, p.y/uHalfHeight, 0.0,1.0); vUV=aUV; }";
    const string FragmentShader = "#version 300 es\nprecision mediump float; in vec2 vUV; uniform sampler2D uTexture; out vec4 frag; void main(){ vec4 c=texture(uTexture,vUV); if(c.a < 0.0039) discard; frag=c; }";
    static int Compile(int type,string source) {
        int shader=GLES30.GlCreateShader(type); GLES30.GlShaderSource(shader,source); GLES30.GlCompileShader(shader);
        int[] ok=new int[1]; GLES30.GlGetShaderiv(shader,GLES30.GlCompileStatus,ok,0);
        if(ok[0]==0) throw new InvalidOperationException(GLES30.GlGetShaderInfoLog(shader));
        return shader;
    }
    public void OnSurfaceCreated(Javax.Microedition.Khronos.Opengles.IGL10? gl, Javax.Microedition.Khronos.Egl.EGLConfig? config)
    {
        textureIds.Clear();
        backgroundVersion = -1;
        int[] vboIds = new int[1];
        GLES30.GlGenBuffers(1, vboIds, 0);
        vertexBuffer = vboIds[0];
        int vs=Compile(GLES30.GlVertexShader,VertexShader), fs=Compile(GLES30.GlFragmentShader,FragmentShader);
        program=GLES30.GlCreateProgram(); GLES30.GlAttachShader(program,vs); GLES30.GlAttachShader(program,fs); GLES30.GlLinkProgram(program);
        GLES30.GlDeleteShader(vs); GLES30.GlDeleteShader(fs);
        posAttr=GLES30.GlGetAttribLocation(program,"aPos"); uvAttr=GLES30.GlGetAttribLocation(program,"aUV");
        sizeUniform=GLES30.GlGetUniformLocation(program,"uView"); halfHeightUniform=GLES30.GlGetUniformLocation(program,"uHalfHeight"); samplerUniform=GLES30.GlGetUniformLocation(program,"uTexture");
        GLES30.GlEnable(GLES30.GlBlend); GLES30.GlBlendFunc(GLES30.GlSrcAlpha,GLES30.GlOneMinusSrcAlpha);
        GLES30.GlDisable(GLES30.GlDepthTest);
        GLES30.GlDisable(0x0B44); // GL_CULL_FACE // Spine mesh triangles can use either winding.
    }
    public void OnSurfaceChanged(Javax.Microedition.Khronos.Opengles.IGL10? gl,int w,int h)
    {
        width=Math.Max(1,w);height=Math.Max(1,h); GLES30.GlViewport(0,0,width,height);
    }
    int GetTexture(string name,byte[] png)
    {
        if(textureIds.TryGetValue(name,out int id)) return id;
        byte[] rgba = SpineAtlasPixels.Decode(png, out int textureWidth, out int textureHeight);
        int[] ids=new int[1]; GLES30.GlGenTextures(1,ids,0); id=ids[0];
        GLES30.GlBindTexture(GLES30.GlTexture2d,id);
        GLES30.GlTexParameteri(GLES30.GlTexture2d,GLES30.GlTextureMinFilter,GLES30.GlLinear);
        GLES30.GlTexParameteri(GLES30.GlTexture2d,GLES30.GlTextureMagFilter,GLES30.GlLinear);
        GLES30.GlTexParameteri(GLES30.GlTexture2d,GLES30.GlTextureWrapS,GLES30.GlClampToEdge);
        GLES30.GlTexParameteri(GLES30.GlTexture2d,GLES30.GlTextureWrapT,GLES30.GlClampToEdge);
        var pixels = ByteBuffer.AllocateDirect(rgba.Length);
        pixels.Put(rgba);
        pixels.Position(0);
        GLES30.GlTexImage2D(GLES30.GlTexture2d, 0, GLES30.GlRgba,
            textureWidth, textureHeight, 0, GLES30.GlRgba, GLES30.GlUnsignedByte, pixels);
        textureIds[name]=id; return id;
    }
    public void OnDrawFrame(Javax.Microedition.Khronos.Opengles.IGL10? gl)
    {
        GLES30.GlClearColor(SpineBackground.R, SpineBackground.G, SpineBackground.B, 1); GLES30.GlClear(GLES30.GlColorBufferBit);
        GLES30.GlUseProgram(program);
        lock(sync) {
            var camera = SpineCamera.Snapshot();
            // Recalculate a single uniform XY scale using the CURRENT surface dimensions.
            // The preview grid can resize when the options panel is expanded/collapsed.
            scale = Math.Clamp(Math.Min(Math.Max(1f,width*0.90f)/modelWidth, Math.Max(1f,height*0.90f)/modelHeight),0.01f,8f);
            float effectiveScale = scale * camera.Zoom;
            GLES30.GlUniform4f(sizeUniform,centerX-camera.PanX/effectiveScale,centerY+camera.PanY/effectiveScale,effectiveScale,Math.Max(1,width)*0.5f);
            GLES30.GlUniform1f(halfHeightUniform, Math.Max(1,height)*0.5f);
            // Image is rendered first, using the same textured pipeline as Spine.
            var bg = SpineBackgroundImage.Png;
            if (bg != null) {
                if (backgroundVersion != SpineBackgroundImage.Version) {
                    if (textureIds.Remove(SpineBackgroundImage.TextureKey, out int previous))
                        GLES30.GlDeleteTextures(1, new[] { previous }, 0);
                    backgroundVersion = SpineBackgroundImage.Version;
                }
                int bgId = GetTexture(SpineBackgroundImage.TextureKey, bg);
                if (bgId != 0) {
                    float cx = centerX-camera.PanX/effectiveScale;
                    float cy = centerY+camera.PanY/effectiveScale;
                    float[] quad = SpineBackgroundImage.Quad(cx,cy,effectiveScale,width,height);
                    var buffer=ByteBuffer.AllocateDirect(quad.Length*4).Order(ByteOrder.NativeOrder()).AsFloatBuffer();
                    buffer.Put(quad);buffer.Position(0);
                    GLES30.GlBlendFunc(GLES30.GlSrcAlpha,GLES30.GlOneMinusSrcAlpha);
                    GLES30.GlBindBuffer(GLES30.GlArrayBuffer,vertexBuffer);
                    GLES30.GlBufferData(GLES30.GlArrayBuffer,quad.Length*4,buffer,GLES30.GlStreamDraw);
                    GLES30.GlActiveTexture(GLES30.GlTexture0);GLES30.GlBindTexture(GLES30.GlTexture2d,bgId);
                    GLES30.GlUniform1i(samplerUniform,0);
                    GLES30.GlEnableVertexAttribArray(posAttr);GLES30.GlEnableVertexAttribArray(uvAttr);
                    GLES30.GlVertexAttribPointer(posAttr,2,GLES30.GlFloat,false,16,0);
                    GLES30.GlVertexAttribPointer(uvAttr,2,GLES30.GlFloat,false,16,8);
                    GLES30.GlDrawArrays(GLES30.GlTriangles,0,6);
                }
            }
            // Vertex shader handles viewport aspect ratio.
            foreach(var (page,blend,source) in batches) {
                switch (blend) {
                    case BlendMode.Additive: GLES30.GlBlendFunc(GLES30.GlSrcAlpha, GLES30.GlOne); break;
                    case BlendMode.Multiply: GLES30.GlBlendFunc(GLES30.GlDstColor, GLES30.GlOneMinusSrcAlpha); break;
                    case BlendMode.Screen: GLES30.GlBlendFunc(GLES30.GlOne, GLES30.GlOneMinusSrcColor); break;
                    default: GLES30.GlBlendFunc(GLES30.GlSrcAlpha, GLES30.GlOneMinusSrcAlpha); break;
                }
                if(!pngs.TryGetValue(page,out var png)) continue;
                int id=GetTexture(page,png); if(id==0) continue;
                // Upload interleaved XYUV data to a GPU VBO. Attribute offsets are
                // byte offsets (0 and 8), not positions in a Java FloatBuffer.
                var buffer=ByteBuffer.AllocateDirect(source.Length*4).Order(ByteOrder.NativeOrder()).AsFloatBuffer();
                buffer.Put(source); buffer.Position(0);
                GLES30.GlBindBuffer(GLES30.GlArrayBuffer,vertexBuffer);
                GLES30.GlBufferData(GLES30.GlArrayBuffer,source.Length*4,buffer,GLES30.GlStreamDraw);
                GLES30.GlActiveTexture(GLES30.GlTexture0); GLES30.GlBindTexture(GLES30.GlTexture2d,id);
                GLES30.GlUniform1i(samplerUniform,0);
                GLES30.GlEnableVertexAttribArray(posAttr); GLES30.GlEnableVertexAttribArray(uvAttr);
                GLES30.GlVertexAttribPointer(posAttr,2,GLES30.GlFloat,false,16,0);
                GLES30.GlVertexAttribPointer(uvAttr,2,GLES30.GlFloat,false,16,8);
                GLES30.GlDrawArrays(GLES30.GlTriangles,0,source.Length/4);
            }
        }
    }
}
