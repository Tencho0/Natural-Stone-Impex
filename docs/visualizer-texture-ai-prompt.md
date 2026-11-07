# AI Prompt: Photo → Visualizer Texture

Turns a real top-down photo of laid stone into a visualizer texture with an AI image editor
(Gemini / Nano Banana Pro, or ChatGPT image editing) **without changing the product**.
Part of the [Visualizer Texture Standard](visualizer-texture-standard.md).

The [Texture Prep Kit](../tools/texture-prep-kit/texture-prep-kit.html) contains the same prompt with a
fill-in form, the fix-up prompts with copy buttons, and a checker for the AI result.

## Before the AI

1. Shoot as in the standard: laid sample, straight down, overcast, tape measures on two edges.
2. **Crop the photo yourself** to an exact square between two tape marks (e.g. 0–150 cm). AI models are
   not precise with scale; this crop fixes the real width.
3. Start a **new chat for every product** and attach the cropped photo.

## Main prompt

Replace the four `[...]` parts.

```text
Edit this photo into a flat texture for a paving visualizer.

The photo shows a real product that customers will buy: [grey split-face granite blocks], laid on the ground with [8 mm joints filled with grey sand], photographed from directly above. It covers [1.5 × 1.5 m]. The stones must stay exactly as they are.

Make only these changes:
1. Remove all lighting effects: cast shadows (trees, people, the camera, a pole), sunlit patches, glare and shiny reflections, and darker corners or edges. The result must have soft, even, shadowless light, like an overcast day, with the same brightness everywhere.
2. Set a neutral white balance. Keep the stones' real colours, colour variation, veins, spots and surface texture. Do not increase saturation, contrast, sharpness or clarity.
3. Remove anything that is not stone or joint (leaves, dirt, debris, tape measures, tools, feet) and fill those spots with the surrounding stone or joint material.
4. Make the image seamlessly tileable: the left edge must continue into the right edge, and the top edge into the bottom edge. Change only a narrow strip along the edges (at most 5% of the width) and continue the existing joints and stones there.

Do not:
- add, remove, move, resize, reshape or rearrange any stone;
- invent new stone patterns, veins or colours;
- change the joint width or joint colour;
- zoom, crop, rotate or change the framing; every stone stays where it is;
- add depth, 3D effects, new shadows, a border, text or a watermark;
- make the stone look newer, cleaner or more attractive than in the photo.

The result should look like a flat, evenly lit, top-down scan of the same surface.

Deliver the result as a downloadable image file, not only as a preview in the chat:
- File name: [NSI_84_bordyuri_1.5m_albedo.jpg]
- Format: JPG, quality 90 or higher, sRGB colour, no transparency
- Size: exactly 2048 × 2048 pixels (square). If you generate at a different size, resize to exactly 2048 × 2048 without cropping, stretching or adding new detail.
- File size: under 5 MB
- The image contains only the stone surface, edge to edge: no border, margin, frame, text or watermark.
```

## Fix-up prompts

Ask for one fix at a time in the same chat, with the original photo still attached. If two fixes in a
row fail, start a new chat with the main prompt.

| Problem | Prompt |
|---|---|
| Shadows or bright patches remain | Some shadows or bright patches are still visible [where]. Remove only those, so the light is even everywhere. Keep every stone, joint and colour exactly as in your last image. |
| Colours drifted | The stone colours changed compared with the original photo: they look [warmer / more saturated / darker]. Match the colours of the original photo I attached. Change nothing else. |
| Stones were changed | Some stones changed shape, position or pattern [where]. Restore that area exactly as in the original photo, keeping only the even lighting. Do not redraw any stones. |
| A seam shows when tiled | When the image is repeated, a visible seam appears at the [left/right/top/bottom] edge. Adjust only the outer 5% strip so the opposite edges continue into each other. Keep everything else identical. |
| Framing or size changed | The framing or size is wrong. Output the same content with exactly the framing of the original photo, as a downloadable JPG file of exactly 2048 × 2048 px (sRGB, quality 90 or higher, under 5 MB), with the same file name as before. |
| No file was given | Please give me the result as a downloadable JPG file named as requested: exactly 2048 × 2048 px, sRGB, quality 90 or higher, under 5 MB. Do not change the image content. |

## Check and upload

1. Open the Texture Prep Kit, drop the cropped original and the AI result, enter the real width.
   It flags changed stones (red map), colour drift, uneven light, seams (3 × 3 preview), size and framing.
   Everything runs in the browser; nothing is uploaded.
2. Compare the colours with a real stone held next to the screen in daylight.
3. Upload in the admin product form under «Текстура за визуализатора» and set
   «Реална ширина на текстурата (м)» to the width you cropped.
4. Keep the original photo next to the texture as proof of what the real product looks like.

**Tool notes:** ChatGPT usually returns a real file with the requested name and size. Gemini often shows
the image only in the chat; save it, rename it, and let the checker's Size line confirm 2048 × 2048.
