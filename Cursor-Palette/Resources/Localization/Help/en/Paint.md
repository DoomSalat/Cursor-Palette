# 🖌️ Paint editor

Move the cursor image within its canvas, resize the canvas, paint pixels, or pan and zoom around it.

🧰 Tools (top bar):
✥ Move — drag the image directly, or use the arrows/snap grid.
✋ Hand — hold the left mouse button to pan. The middle mouse button pans from any tool.
{img:PencilIcon48} Brush — hold the left mouse button to paint pixels. A white outline shows the pixel under the cursor. Hold Shift and click to draw a straight line from your last point; add Ctrl to snap it to 45° steps. Pick a color on the right — the ◐/■ switch (top-right of the picker) toggles between a color wheel and a Photoshop-style square, adjust hue/brightness and alpha, or type/paste a hex code directly.
{img:EraseIcon32} Eraser — hold the left mouse button to erase pixels (make them transparent). Shift and Shift+Ctrl work the same as with Brush, for straight erased lines.
{img:FillIcon32} Fill — click on an area of one color to fill it with the selected color. Uses the same color wheel as Brush.
{img:EyedropperIcon48} Eyedropper — click the button above the color picker, or hold Alt and click while using Brush/Fill, to sample a color from anywhere on screen. The button turns blue and the cursor changes while it's active; Esc cancels.
⛶ Canvas — drag the handles on the canvas edges/corners to resize it, then "Apply" to confirm. Switching tools without applying reverts the size.
🎯 Hotspot — drag the marker on the canvas, or click the desired spot; the 9 quick-set buttons jump to typical positions (corners, edges, center).
{img:ImageRefIcon32} Reference — show a reference image behind the cursor sprite for tracing. Adjust opacity, margin, offset and bilinear filtering, or drag-and-drop your own image. "Hide main image" temporarily hides the drawing, leaving only the reference visible. If the reference is animated, in linked mode its frame follows the active timeline frame; enable "Manual reference control" to browse its frames independently with ◀/▶/⟲. Not saved into the cursor.
↶ Undo — revert the last change (Ctrl+Z).
↷ Redo — re-apply a reverted change (Ctrl+Y or Ctrl+Shift+Z).

🎞️ Animation timeline (bar below the canvas) — "+"/"−" add and remove frames, the frame numbers switch the active one. ▶/⏹ plays/stops the preview; the "ms" field sets the active frame's duration. "Use for all" applies that duration to every frame at once; unchecking it restores the previous per-frame values. "Manual reference control" detaches the reference from the timeline for independent browsing. Limit — 60 frames, minimum 17 ms per frame (.ani format limit).
{img:DownloadIcon32} ".gif" (next to ".png") — appears once there's more than one frame, exports the animation as a GIF to Downloads.

🕹️ Arrows (Move tool) — nudge the image by 1 pixel; disabled once the image reaches that edge.
⚡ Snap grid (Move tool) — 9 buttons to press the image against an edge/corner, or center it.
📐 "Canvas size" (top-right) — set an exact width/height, pick a preset, and choose an anchor for how the canvas grows or shrinks.
{img:DownloadIcon32} "Load image" (top-right) — load a .png/.jpg/.bmp/.gif or .cur/.ani (first frame; an animated GIF becomes the whole frame timeline) via the file picker, or by dragging it onto the button/canvas; choose "Over" to composite it onto the current sprite (canvas grows to fit) or "Replace" to replace the canvas contents entirely.

🔍 Zoom — Ctrl + mouse wheel or the −/+ buttons, centered on the cursor. A separate −/+ pair scales the editor's interface.
☑️ "Sprite bounds" — outline the image's opaque pixels.
{img:DownloadIcon32} ".png" — export the canvas as a PNG to Downloads (named by preset + role + size).

Coordinates shown at bottom-left.
💾 "Save" — apply the changes.

🔄 Your last tool, zoom, pan position, color and picker mode are saved between sessions. The right tool panel's width is saved too — drag the splitter to resize it.