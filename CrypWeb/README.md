# CrypTool 2 Web (prototype)

Browser/WebAssembly version of the CT2 workspace canvas (Blazor WASM).

- `CrypPluginBase.Portable` – original CrypPluginBase sources linked, against `CrypWeb.Wpf` (net10).
- `CrypWeb.Wpf` – the part of WPF that plugins and their presentations use, rendered to HTML/SVG (see below).
- `CrypWeb.Core` – WPF-free `.cwm` reader, workspace model, template `$placeholder$` resolution.
- `CrypWeb.App` – Blazor WebAssembly app: SVG canvas (pan/zoom/fit), template browser.
- `CrypWeb.Tests` – command-line checks over all templates (`dotnet run --project CrypWeb.Tests -- ../Templates runall`).

Run: `./tools-copy-templates.sh && dotnet run --project CrypWeb.App --urls http://localhost:5199`

Canvas features: move (multi/Shift-select, marquee, snap, arrow-key nudge), resize, pan (middle/right/Space), zoom at cursor, fit/100%,
connect ports by drag (type-checked, re-route by dragging an input), select/delete wires, add components (palette drag/double-click) and notes,
rename (double-click title), collapse, copy/paste/duplicate, undo/redo, open .cwm/.json, save .json.

Execution: `CrypWeb.Core/ExecutionEngine.cs` re-implements the CT2 workspace semantics (queued inputs, mandatory inputs,
back-pressure); plugin code runs on background threads as in CT2 (see "Threads" below).
`CrypWeb.Plugins` compiles the original plugin sources unchanged against the portable plugin base and `CrypWeb.Wpf`.
`plugins.exclude` lists the few plugin folders that are not built (standalone tutorials and desktop-shell parts).
TextInput/TextOutput/NumberInput/BooleanInput/BooleanOutput have browser-native replacements (`NativeComponent`).

Regenerate metadata/icons/compile list: `python3 tools-gen-plugins.py`; after changing `presentations.include`:
`python3 tools-gen-presentations.py`.

Not yet: control (IControl) connectors.

## Localization (en / de)

- **UI strings:** `CrypWeb.App/Localizer.cs` + `wwwroot/i18n/<lang>.json` (English is the fallback). Add a language by adding a JSON file and listing it in `Localizer.Languages`. The picker is in the ribbon, the choice is stored in `localStorage` (default: browser language).
- **Plugin metadata:** `tools-gen-plugins.py` reads `Resources.<lang>.resx` next to each `Resources.resx` and writes the differences into `plugins.json` (`loc.<lang>`: name, tooltip, port captions/tooltips). Use `PluginMeta.NameFor/TooltipFor/PortCaptionFor/PortTooltipFor(lang)` (`CrypWeb.Core/Localization.cs`), English is the fallback.
- **Templates:** `$placeholders$` and note texts come from the template's `.xml` (`TemplateStrings.Load(xml, lang)`). They are resolved when a template is loaded, so an unmodified template is reloaded when the language changes.
- **Engine:** `EngineHost.SetCulture` sets `CurrentUICulture` (and the default for background threads) to the selected language before a workspace is opened or run. Localized plugin resources (`Resources.<lang>.resx`) are embedded as satellite resources of `CrypWeb.Plugins` and loaded with `loadAllSatelliteResources` (`Blazor.start` in `index.html`), so plugin log messages and exceptions use the selected language.
- **Not localized yet:** component names given in a saved workspace (user-chosen), tooltips of the ribbon buttons, error messages of the app itself, plugin setting captions (the settings modal reuses the same lookup).

## File compatibility with CrypTool 2

Save writes a normal `.cwm` (gzip + CT2 "XMLSerialization" object graph) that the desktop version opens; Open reads `.cwm` (and the internal `.json`).
`CwmReader` keeps every serialized field it does not model (`Extra`), `CwmWriter` replays them, so unmodelled data survives a round trip.
Verified with `dotnet run --project CrypWeb.Tests -- ../Templates roundtrip` (read -> write -> compare field by field): all 281 templates; 19 differ only
because the writer drops orphaned objects that the original files carry (connections/connectors of already deleted components) and de-duplicates doubled list entries.
Known limits: text boxes keep their formatting only while the text is not edited (edited notes are written as plain text), `$placeholders$` of templates are
already replaced when a template is loaded (so a saved template contains the localized texts), wire routes are re-routed by the desktop when components were moved,
settings of types other than string/int/bool/double/enum cannot be restored by the desktop loader. Not tested against a real CrypTool 2 installation.

## Running the original plugins unchanged (how the gaps are closed)

The plugin sources in `CrypPlugins/` are edited only to fix bugs that also affect the desktop (e.g. `BigIntegerHelper.IsProbablePrime` for small numbers, a cooperative stop for `ADFGVXAnalyzer`, which relied on `Thread.Abort`). Everything the browser lacks is provided on the build side:

| Problem | Solution |
|---|---|
| WPF types in plugin code and presentations | `CrypWeb.Wpf` (see "Plugin presentations" below); plugin-base parts in `CrypPluginBase.Portable/Shims/` (e.g. `CrypAnalysisViewControl`) |
| Plugins with identical type names (e.g. several `Properties.Resources`) | each gets its own assembly (`plugins.isolate`, generated projects in `CrypWeb.Plugins.Isolated/`) |
| `System.Drawing.Common` (Windows only) | `CrypWeb.Drawing`: managed `Bitmap/Graphics/Pen/Brush/Font/...` on a pixel buffer, codecs by StbImageSharp/StbImageWriteSharp. Text is laid out but not drawn. |
| OS crypto (`Aes`, `RijndaelManaged`, DES, 3DES, RC2) | `CrypWeb.Plugins/ManagedCrypto.cs`: same names in the global namespace (found before `using System.Security.Cryptography`), BouncyCastle inside; verified against .NET (`CrypWeb.Tests`) |
| `NativeCryptography` (C++/CLI) | `CrypWeb.Plugins/NativeCryptoShim.cs` |
| Data files read from disk | lazy files in the runtime's file system, downloaded on first open (`tools-gen-vfs.py`, `wwwroot/vfs.js`) |
| WPF-only input/output components | browser-native components (`NativeComponent`: text/number/boolean in-/output, picture output) |
| Sockets (`NetworkSender`/`NetworkReceiver`) | `CrypWeb.Plugins/LoopbackNetwork.cs`: in-memory `UdpClient`/`TcpClient`/`TcpListener`; the templates send and receive within one workspace |
| `ThreadPool.SetMaxThreads` (EnigmaAnalyzer) | `CrypWeb.Plugins/ThreadPoolShim.cs`: the pool is shared by the whole app, resizing it is ignored |
| Emgu CV / OpenCV (ImageProcessor, ImageHash, Transcriptor, Webcam) | `CrypWeb.Plugins/EmguCV.cs`: managed `Image<TColor,TDepth>` with the operations the plugins use (OpenCV formulas); `Capture` is the browser camera |
| ZXing for `System.Drawing` (VisualEncoder/Decoder) | `CrypWeb.Plugins/ZXingBitmap.cs`: `BarcodeReader`/`BarcodeWriter` on top of the ZXing.Net package |
| NAudio, OxyPlot (AudioInput/Output) | `CrypWeb.Plugins/NAudio.cs`, `OxyPlot.cs`: microphone and speakers of the page, a line plot drawn with `OnRender` |
| ISAP commitment (C++ with GMP/MPFR) | `CrypWeb.Plugins/ISAPCommitmentScheme.cs`: managed port (BigInteger, exact fractions) |
| CrypCloud / VoluntLib (KeySearcher) | `StandIns/KeySearcher/`: the distributed search reports that it is not available; the local search is the original (`SOURCE_SUBSTITUTES` in `tools-gen-plugins.py`) |
| Camera, microphone, speakers | `System.Windows.Hosting.BrowserDevices` (CrypWeb.Wpf) <-> `CrypWeb.App/Engine/MediaBridge.cs` <-> `wwwroot/media.js` (getUserMedia, Web Audio) |
| Template data files (`Templates\Codes\QR_Code.png`) | in the virtual file system under `/Templates`, setting values are mapped by `TemplateFiles` |

Workflow after changing shims or plugin sources: build the app (`dotnet build CrypWeb.App`); a missing type or member is a
compile error that names what to add.

Status (headless smoke test over all templates, `dotnet run --project CrypWeb.Tests -- ../Templates runall`): all 281 templates run;
278 without errors (the others are bugs of the original plugins: DCAKeyRecovery, CramerShoupKeyGenerator), and all 188 components build.
Further checks: `probe <template>` (values on every connector, `CT2_TRACE=1` prints exceptions plugins swallow), `present3d`
(the PRESENT 3D animation: frames and render time).
Not done: text drawing in `CrypWeb.Drawing`.

### Plugin presentations: WPF for the web (`CrypWeb.Wpf`)

The original presentations run unchanged: their `.xaml` is loaded at run time and their `.xaml.cs` code-behind is compiled
against `CrypWeb.Wpf`, an implementation of the part of WPF that the plugins use. See `CrypWeb.Wpf/README.md`.

- **Runtime** (`CrypWeb.Wpf`): dependency properties, elements/panels/controls/shapes, brushes, transforms, geometries, bindings,
  styles, resources, animations and timers (`AnimationScheduler`), XAML loader (`Markup/`), HTML/SVG renderer (`Rendering/`),
  `PresentationHost` (change detection, input events).
- **Build**: `presentations.include` lists the plugin folders whose presentation is compiled for real;
  `python3 tools-gen-presentations.py` embeds the XAML, adds the code-behind, generates `InitializeComponent()` and copies pictures
  to `wwwroot/wpf-res/`. A missing WPF type or member shows up as a compile error: add it to `CrypWeb.Wpf`.
- **Threads, as on the desktop**: the app runs on the multi-threaded .NET runtime (`WasmEnableThreads`). The engine and the
  presentations live on the UI thread (`CrypWeb.App/Engine/EngineHost.cs`); plugin code (`Execute`) runs on background threads
  (`ExecutionEngine.ExecuteInBackground`), several components at once, as in CT2. So presentations stay live while a component
  computes (analyzers update their views, DES/AES/Keccak wait in `Execute` for the user to step through their visualization),
  and Stop reaches a running component (`Stop()` is called at once). `Dispatcher.Invoke` from a plugin thread runs on the UI
  thread and waits (`CrypWeb.Wpf/Core/Dispatcher.cs`). Plugins that never check their stop flag (they relied on `Thread.Abort`)
  finish in the background while the page stays usable.
- **Hosting requirement**: threads need `SharedArrayBuffer`, i.e. cross-origin isolation (`Cross-Origin-Opener-Policy:
  same-origin`, `Cross-Origin-Embedder-Policy: require-corp`). Cloudflare gets them from `wwwroot/_headers`; the dev server from
  `--ApplyCopHeaders true` (`.claude/launch.json`: `dotnet run --project CrypWeb/CrypWeb.App --no-launch-profile --
  --ApplyCopHeaders true`). Data files are downloaded synchronously on first open on the UI thread, which serves the file
  system for all threads (`wwwroot/vfs.js`).
- **Run**: the engine renders a presentation to HTML whenever it changed (`EngineHost.TakeHtml`, sent as `html` with the
  state of the component; shown by `Components/PresentationView.razor`); `wpf.js` merges it
  into the DOM, scales view boxes and shapes, and sends clicks/text/selection back (`EngineHost.PresEvent`). Component views are
  plain HTML above the canvas (`.nviews`), not SVG `foreignObject` (WebKit misplaces scaled content there).
- **Live before and after a run**: when a workspace is opened or changed, the app gives it to the engine (`EngineHost.Open`,
  `ExecutionEngine.Prepare`), which creates the components, so presentations are shown and usable before Play, as in CT2.
  Components are kept between runs. Settings a component changes itself (e.g. in its presentation) are reported back and
  stored in the workspace.
- **Drag and drop**: `wpf.js` performs the gesture and reports it once (`drag`); the host replays mouse down/move on the
  source, and `DragDrop.DoDragDrop` delivers the drop synchronously (`CrypWeb.Wpf/Input/DragDrop.cs`).
- **3D** (`Viewport3D`, PRESENT): models, materials, lights, cameras and 3D transforms are implemented
  (`CrypWeb.Wpf/Media/Media3D/`). `Rendering/Scene3D.cs` projects the scene (perspective, back faces, painter's algorithm,
  Lambert shading as in WPF, quads merged from triangle pairs); the renderer writes the faces as a compact list on a canvas
  (`data-scene`), which `wpf.js` (`draw3d`) draws with Canvas 2D, pictures and text of `ImageBrush`/`VisualBrush` mapped onto
  their faces. Thousands of textured cubes would be too many SVG elements to update for every animation step.
- **Bitmaps**: `RenderTargetBitmap`/`WriteableBitmap` keep the drawing rendered into them and are shown as SVG (ECC curve);
  `BitmapSource` has real pixels where plugins read them (`CopyPixels`, `BitmapSource.Create`, `CroppedBitmap`, PNG/BMP
  encoders; `Media/PixelBitmaps.cs`).
- **Tests**: `CrypWeb.Tests ... runall` runs plugin code on background threads as the app does; it reports plugins that are
  still executing when stopped and renders every presentation.
- **Checking a view**: `dotnet run --project CrypWeb.Tests -- ../Templates snap <template.cwm> [Element.Property=value]
  [size:id=WxH] [event:id:kind:value:x:y]` writes each rendered presentation to `CrypWeb.App/wwwroot/_snap/<Plugin>.html`
  (open it via the dev server). The page collects the sizes the browser measures in `window.snapSizes`; pass them back as
  `size:` arguments for views that scale themselves (SizeChanged).

- **Control templates**: a control with a `Template` is drawn as the template's elements (`Control.TemplateRoot`);
  `ContentPresenter` shows the control's content, `{TemplateBinding}` reads the control's properties. Controls the browser
  draws natively (ScrollViewer, text boxes, sliders, lists) keep their standard look. The template's triggers work:
  setters with `TargetName` change named parts of the template, conditions read the control (`IsMouseOver`,
  `IsPressed`, `IsSelected`, `IsEnabled`, data triggers on its data context).
- **Hover and press**: elements with `MouseEnter`/`MouseLeave` handlers or `IsMouseOver`/`IsPressed` triggers are
  marked (`data-h`, `data-p`); `wpf.js` reports the mouse entering, leaving and pressing (`hover`/`press` events).
  `CrypWeb.Tests ... triggertest` checks the trigger states.
- **Data grids** look as in WPF: black grid lines in the directions of `GridLinesVisibility`, column widths in pixels
  or shares (`2*`), row and header heights, row backgrounds, alignment from the columns' `ElementStyle`/`HeaderStyle`.
- **Rotated layout**: a `LayoutTransform` that turns by 90 or 270 degrees uses CSS vertical writing, so the parent
  makes room for the rotated size (DCA side bars).
- **Visual and drawing brushes**: a `VisualBrush` fills a shape with its element (HTML in an SVG `foreignObject`,
  clipped to the outline and stretched; the element's size is estimated); a `DrawingBrush` with one
  `GeometryDrawing` paints with that drawing's brush.
- **Unlimited space**: inside a scrolling `ScrollViewer`, a `Canvas` or along a `StackPanel` WPF measures a grid
  without limits, so its star rows/columns size like `Auto` (HomophonicSubstitutionAnalyzer's symbol grid);
  a text box that can scroll or has several lines shows its text from the top left (a `textarea`).
- **All views at once**: `CrypWeb.Tests ... snapall` writes every presentation and an overview page
  `CrypWeb.App/wwwroot/_snap/index.html` (open it via the dev server) to look through them side by side.
- **Closer to WPF** (found by comparing with screenshots in the plugin documentation): bindings on brushes and gradient
  stops use the owning element's data context; `DropShadowBitmapEffect` is drawn; named elements and elements whose
  `ActualWidth`/`ActualHeight` is read report their size (and are drawn again when it changes); a uniform
  `LayoutTransform` is CSS `zoom`; a Viewbox measures its child without limits; flow documents have tables and lists.

Not supported yet (no presentation in the templates needs them): `VisualBrush` as the background of panels and
controls other than a plain shape, `DrawingBrush` with several drawings, editing in `RichTextBox` (all presentations use
read-only ones), custom layout (`MeasureOverride`/`ArrangeOverride`, only in KeySearcher's cloud statistics and Enigma's
drag adorner), keyboard focus states.

## Hosting on Cloudflare Workers

The app is static (Blazor WASM, multi-threaded), so it is deployed as Workers *static assets* (`wrangler.jsonc`, SPA fallback to
`index.html`); `wwwroot/_headers` adds the cross-origin isolation headers the threads need.

```sh
npm install
npm run build            # ./tools-build-web.sh: templates + dotnet publish -> dist/
npm run dev              # local Workers runtime at http://localhost:8787
npx wrangler login       # once
npm run deploy           # build + wrangler deploy
```

Limits: 25 MiB per file / 20,000 files per version (currently ~1,700 files, largest ~9 MB). `.github/workflows/crypweb-deploy.yml` deploys manually
(needs the repo secrets `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID`).
