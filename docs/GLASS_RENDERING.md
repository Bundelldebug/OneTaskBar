# UI 9.5 live optical material

Reference source, reviewed on 2026-10-27:

- am-glass `AdaptiveGlass.kt`: live backdrop, blur/lens stacking, restrained surface film. Apache-2.0. https://github.com/dmitthedazed/am-glass
- nkzw-tech/liquid-glass `shader-utils.tsx` and `index.tsx`: rounded edge displacement, separated center/edge passes, optional channel splitting. MIT. https://github.com/nkzw-tech/liquid-glass

The Windows port uses its existing Composition host backdrop. The Android Compose and browser SVG/React implementations are not runtime dependencies. No screen bitmap capture/readback loop or new framework is used.

Live contour refraction was removed in 9.5.4. It painted a magnified copy of the host backdrop along every tile edge through a composition visual surface, and that copy could differ from the blur beneath it, which drew dark rings around tiles over bright windows. Nothing in the app can read those pixels back to detect it.

`GlassOptics` adds only a thin directional highlight. It does not paint a second broad inner ring. Ink and media controls remain outside the optical sampling; filled sliders retain their established geometry. Reduced motion freezes pointer lighting; blur-off and UI 8.5/9.0 keep their existing rendering.

Verify with `glass95-probe`, `nowbar9-probe`, `panel-theme-probe`, and `memory-lifecycle-probe`. The glass fixture uses a changing colored source window, tests both palettes and 20 widget sizes, then closes all hosts. A WPF-only RenderTargetBitmap cannot include the separate GPU backdrop; use the computer-use capture for visual review.
