# OpenGL ES 3.0 geometry fix

Changes in `SpineGLView.cs`:
- Uses a GPU vertex buffer object (VBO) for interleaved XYUV vertex data.
- Specifies vertex attributes with byte offsets 0 and 8, and stride 16 bytes.
- Removes the per-frame CPU Y-coordinate adjustment; the vertex shader already applies aspect correction.

This is an experimental correction for the vertical-striping artifact. Build and device verification are still required. If artifacts persist, inspect the triangle geometry and atlas UVs and capture GL errors.
