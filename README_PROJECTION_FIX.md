# OpenGL ES 3.0 projection fix

The vertex shader previously normalized X by half the viewport width, but did not normalize Y by half the viewport height. That caused severe vertical stretching and screen-spanning bands.

The updated shader uses `uHalfHeight` for Y normalization, set from the current GLSurfaceView height.

Build and device test are still required. If artifacts persist, inspect UV coordinates, blend mode and mesh geometry.
