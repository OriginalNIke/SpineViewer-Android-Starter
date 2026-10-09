using SpineRuntime41;
namespace SpineViewer.Android;
public sealed class Spine41Session : ISpinePlayback {
 sealed class DeferredTextureLoader : TextureLoader {
  public void Load(AtlasPage page, string path) { }
  public void Unload(object texture) { }
 }
 Skeleton? skeleton;
 AnimationState? state;
 public bool IsLoaded => skeleton != null;
 public IReadOnlyList<SpineTriangle> TexturedTriangles() => Spine41Geometry.Extract(skeleton);
 public IReadOnlyList<string> Skins {get;private set;} = Array.Empty<string>();
 public IReadOnlyList<string> Animations {get;private set;} = Array.Empty<string>();
 public void Load(string atlasText, byte[] bytes, bool binary) {
  using var atlasReader = new StringReader(atlasText);
  var atlas = new Atlas(atlasReader, "", new DeferredTextureLoader());
  SkeletonData data;
  if(binary) {using var stream = new MemoryStream(bytes); data = new SkeletonBinary(atlas).ReadSkeletonData(stream);}
  else {using var reader = new StreamReader(new MemoryStream(bytes)); data = new SkeletonJson(atlas).ReadSkeletonData(reader);}
  // Publish a fully constructed skeleton and state together, avoiding partial loads.
  var nextSkeleton = new Skeleton(data);
  var nextState = new AnimationState(new AnimationStateData(data));
  nextSkeleton.UpdateWorldTransform();
  skeleton = nextSkeleton;
  state = nextState;
  Skins = data.Skins.Select(s=>s.Name).ToArray();
  Animations = data.Animations.Select(a=>a.Name).ToArray();
 }
 public void SetSkin(string name) {
  if (skeleton == null || string.IsNullOrWhiteSpace(name)) return;
  if (!Skins.Contains(name, StringComparer.Ordinal)) return;
  skeleton.SetSkin(name);
  skeleton.SetSlotsToSetupPose();
  skeleton.UpdateWorldTransform();
 }
 public void SetAnimation(string name) {
  if (state == null || string.IsNullOrWhiteSpace(name)) return;
  if (!Animations.Contains(name, StringComparer.Ordinal)) return;
  state.SetAnimation(0, name, true);
 }
 public void Step(float dt) {if(skeleton==null||state==null)return;state.Update(Math.Clamp(dt,0,0.1f));state.Apply(skeleton);skeleton.UpdateWorldTransform();}
 public IReadOnlyList<(float x,float y,float px,float py)> BoneLines() {
  if(skeleton==null)return Array.Empty<(float,float,float,float)>();
  var result=new List<(float,float,float,float)>();
  foreach(var b in skeleton.Bones)if(b.Parent!=null)result.Add((b.WorldX,b.WorldY,b.Parent.WorldX,b.Parent.WorldY));
  return result;
 }
}
