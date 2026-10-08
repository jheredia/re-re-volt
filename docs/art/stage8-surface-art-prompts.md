# Stage 8D surface art prompts

All three source images were generated with the built-in image-generation tool. They are original project assets and were not derived from reference images. Runtime copies are resized under `Assets/Resources/Art/Stage8`; these source files remain unchanged:

- `stage8-cutting-mat-road-source.png` — 1254×1254 RGB
- `stage8-workbench-surface-source.png` — 1254×1254 RGB
- `stage8-surface-decals-source.png` — 1774×887 RGBA

## Cutting-mat road

```text
Use case: stylized-concept
Asset type: tileable game texture for a Unity RC racing circuit surface
Primary request: a perfectly seamless square cutting-mat road surface, viewed straight down, designed for repeated tiling under toy-scale RC cars
Scene/backdrop: full-frame material swatch only, no environment and no perspective
Subject: deep muted teal-green self-healing cutting mat with extremely subtle fine scratches, faint graphite rub, tiny paint specks, soft rubber wheel haze, and understated workshop wear distributed evenly
Style/medium: believable hand-authored game texture with restrained stylized realism; diffuse/albedo appearance with broad even illumination
Composition/framing: orthographic top-down square; texture fills every pixel; all four edges must wrap seamlessly; no dominant focal mark
Lighting/mood: flat neutral ambient light, no cast shadows, no vignette, no baked directional highlights
Color palette: dark desaturated teal and green with low-contrast gray wear
Materials/textures: matte self-healing cutting mat, fine micro-scratches and subtle scuffing at toy scale
Constraints: seamless/tileable on both axes; preserve even density at every edge; no grid, no lane lines, no arrows, no text, no numbers, no flags, no national emblems, no brands, no logos, no copied racing liveries, no watermark
Avoid: obvious repeating clusters; large cracks; dramatic stains; photoreal camera lighting; perspective; borders; high-contrast focal elements
```

## Workbench surface

```text
Use case: stylized-concept
Asset type: tileable game texture for a Unity RC racing workshop floor
Primary request: a perfectly seamless square workbench surface, viewed straight down, designed for gentle repeated tiling around a toy-scale race circuit
Scene/backdrop: full-frame material swatch only, no environment and no perspective
Subject: warm medium-brown laminated wood workbench with quiet linear grain, tiny softened scratches, faint sanding arcs, a few pinhead paint flecks, and subtle handling wear distributed evenly
Style/medium: believable hand-authored game texture with restrained stylized realism; diffuse/albedo appearance; intentionally quieter and lower contrast than the teal cutting-mat race surface
Composition/framing: orthographic top-down square; texture fills every pixel; all four edges must wrap seamlessly; no dominant knot or focal stain
Lighting/mood: flat neutral ambient light, no cast shadows, no vignette, no baked directional highlights
Color palette: muted walnut brown, warm umber, soft tan; low contrast
Materials/textures: matte sealed plywood or laminated workbench, fine grain and understated craft-room wear
Constraints: seamless/tileable on both axes; preserve even density at every edge; no planks or hard seams; no objects; no text, no numbers, no flags, no national emblems, no brands, no logos, no copied designs, no watermark
Avoid: high-gloss varnish; large knots; dramatic stains; perspective; borders; high-contrast focal elements; noisy grain
```

## Transparent decal atlas

```text
Use case: stylized-concept
Asset type: transparent 4-by-2 decal atlas for a Unity RC racing circuit
Primary request: eight original workshop-racing surface decals arranged as a precise 4-column by 2-row atlas, one isolated decal centered in each equal square cell
Scene/backdrop: genuinely transparent background across the entire sheet, with generous transparent padding inside every cell
Subject: top row from left to right: crossed strips of worn mustard repair tape; overlapping torn blue painter-tape patches; small coral-red dry-brush paint scuff; paired curved charcoal RC tire rub arcs. Bottom row from left to right: an open asymmetric scatter of three cyan triangles, two small circles, and one short bar, clearly decorative paint rather than a symbol; an open asymmetric arrangement of two warm-orange diamonds, three small dots, and two offset bars, clearly decorative paint rather than a symbol; pale cream broken lane-wear strips with chipped edges; a compact soft black twin-tire skid mark
Style/medium: clean hand-painted game decal sprites with subtle material texture, readable from a top-down racing camera, restrained stylized realism
Composition/framing: orthographic 2:1 atlas; exactly 4 equal columns and 2 equal rows; every motif fully contained inside its own square cell with at least 12 percent transparent padding; no overlaps across cell boundaries; consistent visual weight
Lighting/mood: diffuse albedo only, no scene lighting, no cast shadows
Color palette: workshop mustard, painter blue, coral red, charcoal, cyan, warm orange, pale cream, soft black
Materials/textures: fibrous tape edges, dry paint, rubber rub, chipped painted shapes
Constraints: output must have a true transparent alpha background, not white or checkerboard; the two geometric arrangements must remain open, asymmetric, disconnected, non-symbolic, and must not resemble badges, crests, seals, insignia, or emblems; no enclosed circular compositions; no central-eye imagery; no text, letters, numbers, arrows, flags, national emblems, brands, logos, copied racing liveries, protected symbols, borders, watermarks, or sheet labels
Avoid: symmetry; badge-like framing; drop shadows; glow; mockup surface; perspective; cell dividers; repeating logos; recognizable trademark or icon
```

The built-in generator selected a 2:1 canvas for the requested 4×2 atlas. The equal cells remain square, so runtime UVs divide it into four columns and two rows without cropping.

## Generation limitations and runtime mitigation

The generated cutting-mat and workbench sources are not pixel-periodic. At the 1024×1024 runtime size, direct opposite-edge RGB comparisons measured:

- Cutting mat: left/right RMS 9.702, top/bottom RMS 10.778.
- Workbench: left/right RMS 9.479, top/bottom RMS 11.801.

The retained importers therefore use `TextureWrapMode.Mirror` on all axes rather than claiming the source pixels match. The existing 4×4 road and 8×8 workbench tiling remains, while mirrored sampling makes each runtime tile boundary color-continuous by reflecting the same edge texels across that boundary.

The original decal prompt requested at least 12 percent transparent padding inside every cell, but the generated output did not meet that request. At 1024×512, counting every pixel with alpha greater than zero, the minimum padding for cells 0–7 was respectively 0, 0, 0, 0, 2, 0, 0, and 0 pixels. The runtime importer therefore disables mipmaps, and each mesh cell insets its UV rectangle by exactly one runtime pixel on all four sides. This prevents filtering from sampling neighboring atlas cells; it does not represent the generated padding as compliant.

## Runtime presentation budget

- Three retained textures: two mirrored 1024×1024 surface maps and one clamped, single-mip 1024×512 RGBA decal atlas.
- Exactly eight decal renderers and eight small owned quad meshes, one per atlas cell.
- Exactly one circuit-owned transparent decal material, shared by every decal renderer and preserved across visual-style changes.
- Every decal quad uses a one-runtime-pixel UV inset on all four cell edges to prevent cross-cell filtering.
- Zero decal colliders, zero outline objects, and zero drivable-layer, checkpoint, or grid additions.
- A fixed 0.015-unit lift above the receiving surface prevents z-fighting; road decals remain offset from the route center lines.
