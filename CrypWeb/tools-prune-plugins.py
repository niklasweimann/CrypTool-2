#!/usr/bin/env python3
"""Builds CrypWeb.Plugins repeatedly.
 * Missing WPF types and plugin presentation classes are replaced by generated stand-ins (stubs.g.cs): members that the plugin code
   uses are discovered from the compiler errors and added as `dynamic` members (calls are no-ops in the browser).
 * Every plugin directory that still has compile errors afterwards is added to plugins.exclude."""
import subprocess, re, os, sys, json
here = os.path.dirname(os.path.abspath(__file__))
ex = os.path.join(here, 'plugins.exclude')
stubs_path = os.path.join(here, 'CrypWeb.Plugins', 'stubs.g.cs')
STATE = os.path.join(here, 'stubs.state.json')
excl = set(l.strip() for l in open(ex)) if os.path.exists(ex) else set()
iso_path = os.path.join(here, 'plugins.isolate')
iso = set(l.split()[0] for l in open(iso_path) if l.strip() and not l.startswith('#')) if os.path.exists(iso_path) else set()

def names(ns, s): return {n: ns for n in s.split()}
KNOWN = {}
KNOWN.update(names('System.Windows.Controls', 'TextBox TextBlock Button Label ContentControl Window ScrollViewer ItemsControl ContentPresenter DataGrid ComboBoxItem TextChangedEventArgs DataGridAutoGeneratingColumnEventArgs ComboBox CheckBox ListBox ListView Image Slider TabControl TabItem Border'))
KNOWN.update(names('System.Windows.Controls', 'Canvas Grid StackPanel DockPanel WrapPanel UniformGrid'))
KNOWN.update(names('System.Windows.Controls', 'RowDefinition ColumnDefinition'))
KNOWN.update(names('System.Windows.Controls.Primitives', 'Thumb DragDeltaEventArgs ScrollBar'))
KNOWN.update(names('System.Windows.Shapes', 'Shape Line Path Ellipse Polygon Polyline Rectangle'))
KNOWN.update(names('System.Windows.Media', 'Geometry PathGeometry PathFigure PolyLineSegment StreamGeometry StreamGeometryContext PointCollection Transform GeneralTransform Visual Pen DrawingContext Freezable HitTestResult HitTestResultBehavior TranslateTransform ScaleTransform RotateTransform TransformGroup LineGeometry RectangleGeometry EllipseGeometry GradientStopCollection GradientStop LinearGradientBrush RadialGradientBrush VisualTreeHelper FontFamily Typeface FormattedText'))
KNOWN.update(names('System.Windows.Media', 'ImageBrush'))
KNOWN.update(names('System.Windows.Media.Imaging', 'BitmapSource WriteableBitmap BitmapFrame RenderTargetBitmap'))
KNOWN.update(names('System.Windows.Media.Animation', 'DoubleAnimation DoubleAnimationBase ColorAnimation Storyboard AnimationClock PointAnimation'))
KNOWN.update(names('System.Windows.Media.Media3D', 'GeometryModel3D Point3D Model3DGroup Model3DCollection Material Transform3DCollection MeshGeometry3D Vector3D DiffuseMaterial PerspectiveCamera Viewport3D ModelVisual3D Point3DCollection'))
KNOWN.update(names('System.Windows.Input', 'MouseButtonEventArgs KeyEventArgs MouseEventArgs MouseWheelEventArgs TextCompositionEventArgs ExecutedRoutedEventArgs CanExecuteRoutedEventArgs RoutedUICommand Cursor Key MouseButtonEventHandler KeyEventHandler MouseEventHandler MouseWheelEventHandler Cursors ICommand RoutedCommand CommandBinding'))
KNOWN.update(names('System.Windows', 'DataTemplate Style DragEventArgs SizeChangedEventArgs DataObjectPastingEventArgs DataFormats GridUnitType LengthConverter GridLength DragEventHandler SizeChangedEventHandler Window Clipboard DataObject DataFormat TextAlignment FontWeights FontStyles ResourceDictionary'))
KNOWN.update(names('System.Windows.Data', 'Binding MultiBinding BindingOperations'))
KNOWN.update(names('System.Windows.Documents', 'TextRange FlowDocument Paragraph Run Inline Block AdornerLayer Hyperlink Span Bold LineBreak TextPointer'))
KNOWN.update(names('System.Windows.Threading', 'DispatcherOperation DispatcherTimer'))
KNOWN.update(names('CrypTool.CrypAnalysisViewControl', 'CrypAnalysisViewControl'))
ENUMS = {'Key', 'GridUnitType', 'HitTestResultBehavior', 'TextAlignment'}
PANELS = {'Canvas', 'Grid', 'StackPanel', 'DockPanel', 'WrapPanel', 'UniformGrid'}
PRES = re.compile(r'(Presentation|Pres|Visualization|Visualisation|PresentationFrame)$')

stubs = {}   # (namespace, name) -> {'kind','base','m','p','s','e','ev'}
if os.path.exists(STATE):
    for k, v in json.load(open(STATE)).items():
        ns, n = k.split('|'); v['m'] = set(v['m']); v['p'] = set(v['p']); v['s'] = set(v['s']); v['e'] = set(v['e']); v['ev'] = set(v.get('ev', [])); stubs[(ns, n)] = v

def base_for(name, ns):
    if name in ENUMS: return None
    if name in PANELS: return 'System.Windows.Controls.Panel'
    if name.endswith('EventArgs'): return 'System.Windows.StubEventArgs'
    if name in ('ImageBrush',): return 'System.Windows.Media.Brush'
    if name == 'BitmapSource': return 'System.Windows.Media.ImageSource'
    if ns in ('System.Windows.Shapes',): return 'System.Windows.FrameworkElement'
    if ns == 'System.Windows.Controls' or ns == 'System.Windows.Controls.Primitives': return 'System.Windows.Controls.Control'
    if name.endswith('Presentation') or PRES.search(name): return 'System.Windows.Controls.UserControl'
    if name == 'CrypAnalysisViewControl': return 'System.Windows.Controls.UserControl'
    return 'System.Windows.DependencyObject'

for (_ns, _n), _v in stubs.items(): _v['base'] = base_for(_n, _ns)  # refresh bases of stubs loaded from an older state

def add_stub(ns, name, kind=None):
    if (ns, name) in stubs: return False
    k = kind or ('enum' if name in ENUMS else 'delegate' if name.endswith('Handler') else 'class')
    stubs[(ns, name)] = {'kind': k, 'base': base_for(name, ns), 'm': set(), 'p': set(), 's': set(), 'e': set(), 'ev': set()}
    return True

def write_stubs():
    json.dump({'%s|%s' % k: {kk: (sorted(vv) if isinstance(vv, set) else vv) for kk, vv in v.items()} for k, v in stubs.items()}, open(STATE, 'w'))
    with open(stubs_path, 'w') as f:
        f.write('// generated by tools-prune-plugins.py: stand-ins for WPF types / presentation classes (members are discovered from the compiler errors; calls are no-ops)\n#pragma warning disable\n')
        for (ns, n), v in sorted(stubs.items()):
            if v['kind'] == 'enum':
                mem = sorted(v['e'] | v['s'])
                f.write('namespace %s { public enum %s { %s } }\n' % (ns, n, ', '.join(mem) if mem else '_'))
            elif v['kind'] == 'delegate':
                arg = re.sub(r'Handler$', 'Args', n)
                f.write('namespace %s { public delegate void %s(object sender, %s e); }\n' % (ns, n, 'System.Windows.RoutedEventArgs' if not any(k[1] == arg for k in stubs) else next(k[0] for k in stubs if k[1] == arg) + '.' + arg))
            else:
                base = v['base'] or 'object'
                body = ' public %s() { } public %s(params object[] a) { }' % (n, n)
                body += ''.join(' public dynamic %s(params object[] a) { __Record("%s", a); return System.Windows.StubValue.Instance; }' % (m, m) for m in sorted(v['m'] - v['p']))  # a member used as property wins over one used as call
                body += ''.join(' public dynamic %s { get => __Get("%s"); set => __Set("%s", value); }' % (q, q, q) for q in sorted(v['p'] - v['ev']))
                body += ''.join(' public System.Windows.StubEvent %s { get; set; } = new System.Windows.StubEvent();' % q for q in sorted(v['ev']))
                for q in sorted(v['s']):
                    if (q[2:] if q.startswith('()') else q) in (v['m'] | v['p']): continue  # an instance member of that name wins over a static one
                    if q.startswith('()'): body += ' public static dynamic %s(params object[] a) => System.Windows.StubValue.Instance;' % q[2:]
                    else: body += ' public static dynamic %s { get => System.Windows.StubValue.Instance; set { } }' % q
                f.write('namespace %s { public class %s : %s {%s } }\n' % (ns, n, base, body))
write_stubs()
if '--write-stubs' in sys.argv: sys.exit(0)

def src_line(path, line):
    try: return open(path, encoding='utf-8-sig', errors='ignore').read().split('\n')[int(line) - 1]
    except Exception: return ''

def file_ns(path):
    ns = re.findall(r'^\s*namespace\s+([\w.]+)', open(path, encoding='utf-8-sig', errors='ignore').read(), re.M)
    return ns[0] if ns else None

for it in range(40):
    subprocess.run([sys.executable, os.path.join(here, 'tools-gen-plugins.py')], capture_output=True)
    r = subprocess.run(['dotnet', 'build', os.path.join(here, 'CrypWeb.Plugins.All'), '-nologo', '-v:q'], capture_output=True, text=True)
    out = r.stdout + r.stderr
    errs = set(re.findall(r'(/[^()\n]*/CrypPlugins/([^/]+)/[^(]*)\((\d+),\d+\): error (CS\d+): ([^\[\n]*)', out))
    if not errs and (r.returncode != 0 and ' error ' in out):
        print(out[-2500:]); break
    new = 0
    for path, d, line, code, msg in errs:
        m = re.search(r"type or namespace name '(\w+)' could not be found", msg)
        if code == 'CS0246' and m and path.endswith('.cs'):
            n = m.group(1)
            if n in KNOWN: new += add_stub(KNOWN[n], n)
            elif PRES.search(n):
                ns = file_ns(path)
                if ns: new += add_stub(ns, n)
        m2 = re.search(r"'(\w+)' does not contain a definition for '(\w+)'", msg)
        if code in ('CS1061', 'CS0117') and m2:
            tname, mem = m2.group(1), m2.group(2); text = src_line(path, line)
            for key, v in stubs.items():
                if key[1] != tname: continue
                if v['kind'] == 'enum': 
                    if mem not in v['e']: v['e'].add(mem); new += 1
                elif code == 'CS0117':
                    tag = ('()' + mem) if re.search(r'\.' + re.escape(mem) + r'\s*\(', text) else mem
                    if tag not in v['s']: v['s'].add(tag); new += 1
                else:
                    kind = 'm' if re.search(r'\.' + re.escape(mem) + r'\s*\(', text) else 'p'
                    if mem not in v[kind]: v[kind].add(mem); new += 1
    # `presentation.Member += handler` on a dynamic stand-in member: make the member an event stand-in
    for path, d, line, code, msg in errs:
        if code == 'CS0019' and "'+='" in msg or code == 'CS0019' and "'-='" in msg:
            mm = re.search(r'(\w+)\s*(\+=|-=)', src_line(path, line))
            if mm:
                for key, v in stubs.items():
                    if mm.group(1) in v['p'] and mm.group(1) not in v['ev']: v['ev'].add(mm.group(1)); new += 1
    # identical type names in two plugin directories cannot share an assembly: give those directories their own
    dup = set(d for _, d, _, code, _ in errs if code in ('CS0101', 'CS0102', 'CS0111', 'CS0579') and d not in iso)
    if dup:
        iso |= dup
        private = set(l.split()[0] for l in open(iso_path) if 'private' in l.split()[1:]) if os.path.exists(iso_path) else set()
        open(iso_path, 'w').write('\n'.join(d + (' private' if d in private else '') for d in sorted(iso)) + '\n'); new += len(dup)
    print('iteration', it, 'errors', len(errs), 'stub/isolation changes', new, 'stubs', len(stubs), 'isolated', len(iso), flush=True)
    if new: write_stubs(); continue
    bad = set(d for _, d, _, _, _ in errs)
    if not bad: break
    excl |= bad
    open(ex, 'w').write('\n'.join(sorted(excl)) + '\n')
print('excluded', len(excl), 'stubs', len(stubs))
