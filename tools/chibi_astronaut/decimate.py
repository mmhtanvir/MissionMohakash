# blender -b -P decimate.py -- in.glb out.glb target_tris
import bpy, sys
a = sys.argv[sys.argv.index("--") + 1:]
src, dst, target = a[0], a[1], int(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
total = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
ratio = min(1.0, target / max(1, total))
for o in meshes:
    if ratio < 1.0:
        m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = ratio
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=m.name)
for img in bpy.data.images:
    w, h = img.size
    if max(w, h) > 1024:
        f = 1024 / max(w, h); img.scale(max(1, int(w * f)), max(1, int(h * f)))
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_image_format='JPEG', export_jpeg_quality=85, export_animations=False)
print(f"DONE {src} tris {total} -> ratio {ratio:.4f}")
