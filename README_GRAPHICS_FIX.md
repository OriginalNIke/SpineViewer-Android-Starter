# OpenGL graphics corrections

- Corrected Spine Y-up to OpenGL clip-space Y-up (removed the erroneous negative sign).
- Fragment shader discards nearly transparent texels to reduce colored outlines around small details.
- Explicitly disables face culling for two-sided Spine meshes.

**Limitations:** Eye artifacts may also be caused by missing Spine clipping attachments, per-slot blend modes, atlas padding, or premultiplied alpha. Those require separate investigation. Not compiled or device-tested.
