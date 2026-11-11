#!/usr/bin/env python3
"""Scans CrypPlugins/*, writes wwwroot/plugins.json (metadata for all components),
copies icons, and generates CrypWeb.Plugins/plugins.g.props (compile items for the browser build)."""
import glob, os, re, json, shutil, sys, xml.etree.ElementTree as ET
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
PL = os.path.join(ROOT, 'CrypPlugins')
OUT = os.path.join(ROOT, 'CrypWeb', 'CrypWeb.App', 'wwwroot')
EXCL = set(l.strip() for l in open(os.path.join(ROOT, 'CrypWeb', 'plugins.exclude')) if l.strip() and not l.startswith('#')) if os.path.exists(os.path.join(ROOT, 'CrypWeb', 'plugins.exclude')) else set()

def resx(path):
    d = {}
    try:
        for e in ET.parse(path).getroot().findall('data'):
            v = e.find('value')
            if v is not None and e.get('type') is None: d[e.get('name')] = (v.text or '').strip()
    except Exception: pass
    return d

# plugins.isolate: one plugin folder per line, optionally followed by "private" (see ISO_CSPROJ)
_ISOLATE_LINES = [l.split() for l in open(os.path.join(ROOT, 'CrypWeb', 'plugins.isolate')) if l.strip() and not l.startswith('#')] if os.path.exists(os.path.join(ROOT, 'CrypWeb', 'plugins.isolate')) else []
ISOLATE = set(parts[0] for parts in _ISOLATE_LINES)
ISOLATE_PRIVATE = set(parts[0] for parts in _ISOLATE_LINES if 'private' in parts[1:])
items_by_dir = {}
# "private" plugins see the shared plugin assembly only under an alias, so that the namespaces of the other plugins
# (e.g. "Transposition") do not hide the plugin's own types of the same name.
PRIVATE_ALIAS = ' Aliases="plugins"'
ISO_CSPROJ = '''<Project Sdk="Microsoft.NET.Sdk">
  <!-- generated: one plugin directory in its own assembly (its type names collide with another plugin's in a shared assembly) -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>@ASM@</AssemblyName>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <GenerateResourceUsePreserializedResources>true</GenerateResourceUsePreserializedResources>
    <DefineConstants>$(DefineConstants);CT2_PORTABLE</DefineConstants>
    <NoWarn>$(NoWarn);CS0108;CS0109;CS0114;CS0168;CS0169;CS0219;CS0414;CS0649;CS0618;CS1591;CS8981;CA1416;CS0067;CS0105;SYSLIB0011;SYSLIB0021;SYSLIB0022;SYSLIB0023;SYSLIB0045;SYSLIB0050;SYSLIB0051</NoWarn>
  </PropertyGroup>
  <Import Project="plugins.g.props" />
  <!-- the plugin's presentation compiled from its XAML (tools-gen-presentations.py) -->
  <Import Project="presentations.g.props" Condition="Exists('presentations.g.props')" />
  <ItemGroup>
    <!-- "Thread" means the cooperative stand-in, as in the shared plugin assembly -->
    <Compile Include="..\\..\\CrypWeb.Plugins\\ThreadAlias.cs" />
    <PackageReference Include="System.Resources.Extensions" Version="10.0.14" />
    <PackageReference Include="System.Configuration.ConfigurationManager" Version="10.0.14" />
    <PackageReference Include="BouncyCastle.Cryptography" Version="2.7.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
    <ProjectReference Include="..\\..\\CrypWeb.Plugins\\CrypWeb.Plugins.csproj"@ALIAS@ />
  </ItemGroup>
</Project>
'''
meta = {}; props = []
os.makedirs(os.path.join(OUT, 'icons'), exist_ok=True)
for d in sorted(os.listdir(PL)):
    pd = os.path.join(PL, d)
    if not os.path.isdir(pd): continue
    srcs = []
    asm_name = d
    for cp in [f for f in os.listdir(pd) if f.endswith('.csproj')]:
        mm_ = re.search(r'<AssemblyName>([^<]+)</AssemblyName>', open(os.path.join(pd, cp), encoding='utf-8-sig', errors='ignore').read())
        if mm_: asm_name = mm_.group(1)
    for r, ds, fs in os.walk(pd):
        ds[:] = [x for x in ds if x not in ('obj', 'bin')]
        for f in fs:
            if f.endswith('.cs') and not f.endswith('.xaml.cs') and f != 'AssemblyInfo.cs' and not re.search(r'\.[a-z]{2}(-[A-Za-z]+)?\.Designer\.cs$', f): srcs.append(os.path.join(r, f))
    # resources
    res_files = {}
    loc_files = {}   # lang -> {path: values}  (Resources.<lang>.resx next to Resources.resx)
    for r, ds, fs in os.walk(pd):
        ds[:] = [x for x in ds if x not in ('obj', 'bin')]
        for f in fs:
            if f.endswith('.resx') and f.count('.') == 1: res_files[os.path.join(r, f)] = resx(os.path.join(r, f))
            else:
                lm = re.match(r'^(.*)\.([a-z]{2})(?:-[A-Za-z]+)?\.resx$', f)
                if lm: loc_files.setdefault(lm.group(2).lower(), {})[os.path.join(r, f)] = resx(os.path.join(r, f))
    for f in list(srcs):
        try: text = open(f, encoding='utf-8-sig', errors='ignore').read()
        except Exception: continue
        for cm in re.finditer(r'^[ \t]*(?:public\s+)?(?:sealed\s+|partial\s+|static\s+)*class\s+(\w+)\s*(?::[^{]*ICrypComponent\b|:[^{]*\bICrypComponent)', text, re.M):
            cls = cm.group(1)
            # walk back over the attribute block
            lines = text[:cm.start()].split('\n')
            blk = []; depth = 0
            for ln in reversed(lines):
                t = ln.strip()
                if not t or t.startswith('//') or t.startswith('///'):
                    if depth == 0 and not blk: continue
                    if depth == 0: break
                blk.append(ln); depth += t.count(']') - t.count('[')
                if depth == 0 and not t.startswith(']') and not t.startswith('[') and not t.endswith(')]') and not t.endswith(']'):
                    blk.pop(); break
            attrs = '\n'.join(reversed(blk))
            pm_ = re.search(r'PluginInfo\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*(?:,\s*(?:new\s*(?:string)?\s*\[\s*\]\s*)?\{([^}]*)\})?', attrs) or re.search(r'PluginInfo\(\s*typeof\([^)]*\)\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*(?:,\s*(?:new\s*(?:string)?\s*\[\s*\]\s*)?\{([^}]*)\})?', attrs)
            if not pm_: continue
            g = pm_.groups()
            if len(g) == 5: resname, ck, tk, doc, icons = g
            else: ck, tk, doc, icons = g; resname = ''
            icons = icons or ''
            ns = re.findall(r'namespace\s+([\w.]+)', text[:cm.start()])
            full = (ns[-1] + '.' if ns else '') + cls
            cat = re.search(r'ComponentCategory\(ComponentCategory\.(\w+)\)', attrs)
            m = type('M', (), {'end': lambda self: cm.end()})()
            # find resx whose designer names this resource
            res = {}
            for rf, vals in res_files.items():
                res.update(vals) if os.path.exists(rf) and ck in vals else None
            locres = {}
            for lang, files in loc_files.items():
                d_ = {}
                for rf, vals in files.items():
                    if ck in vals or any(k.endswith('Caption') for k in vals): d_.update(vals)
                if d_: locres[lang] = d_
            icon_list = [i.strip().strip('"') for i in icons.split(',') if i.strip()]
            icon = None
            if icon_list:
                p = icon_list[0].split('/', 1)
                cand = os.path.join(pd, p[1]) if len(p) == 2 else None
                if not (cand and os.path.exists(cand)):
                    base = os.path.basename(icon_list[0]); cand = None
                    for r_, ds_, fs_ in os.walk(pd):
                        if base in fs_: cand = os.path.join(r_, base); break
                if cand and os.path.exists(cand):
                    icon = 'icons/%s.png' % full
                    try: shutil.copy(cand, os.path.join(OUT, icon))
                    except Exception: icon = None
            # ports
            body = text[cm.end():]
            ports = []
            for pm in re.finditer(r'\[PropertyInfo\(\s*Direction\.(\w+)\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"(?:\s*,\s*(true|false))?[^\]]*\)\]\s*(?:public\s+)(?:virtual\s+)?([\w<>\[\].?]+)\s+(\w+)', body):
                dr, cap, tip, mand, typ, name = pm.groups()
                if dr not in ('InputData', 'OutputData', 'ControlMaster', 'ControlSlave'): continue
                ports.append(dict(name=name, dir='out' if dr in ('OutputData', 'ControlMaster') else 'in', type=typ, caption=res.get(cap, cap), tooltip=res.get(tip, tip), mandatory=(mand == 'true'), control=dr.startswith('Control')))
            loc = {}
            for lang, lr in locres.items():
                entry = {}
                if lr.get(ck) and lr.get(ck) != res.get(ck): entry['name'] = lr[ck]
                if lr.get(tk) and lr.get(tk) != res.get(tk): entry['tooltip'] = lr[tk]
                lp = {}
                for pt in ports:
                    pm2 = [x for x in re.finditer(r'\[PropertyInfo\(\s*Direction\.\w+\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"[^\]]*\)\]\s*public\s+(?:virtual\s+)?[\w<>\[\].?]+\s+' + pt['name'] + r'\b', body)]
                    if not pm2: continue
                    ck2, tk2 = pm2[0].group(1), pm2[0].group(2)
                    e2 = {}
                    if lr.get(ck2) and lr.get(ck2) != res.get(ck2): e2['caption'] = lr[ck2]
                    if lr.get(tk2) and lr.get(tk2) != res.get(tk2): e2['tooltip'] = lr[tk2]
                    if e2: lp[pt['name']] = e2
                if lp: entry['ports'] = lp
                if entry: loc[lang] = entry
            meta[full] = dict(loc=loc, type=full, dir=d, name=res.get(ck, cls), tooltip=res.get(tk, ''), category=cat.group(1) if cat else 'Undefined', icon=icon, ports=ports, supported=d not in EXCL)
    if d not in EXCL and srcs:
        its = []
        for s_ in srcs: its.append(('Compile', s_, d + '/' + os.path.relpath(s_, pd).replace(os.sep, '/'), None))
        for rf in res_files:
            des = rf[:-5] + '.Designer.cs'
            ln = None
            if os.path.exists(des):
                mm = re.search(r'new\s+(?:global::)?System\.Resources\.ResourceManager\("([^"]+)"', open(des, encoding='utf-8-sig', errors='ignore').read())
                if mm: ln = mm.group(1)
            if ln:
                its.append(('EmbeddedResource', rf, d + '/' + os.path.relpath(rf, pd).replace(os.sep, '/'), ln + '.resources'))
                # localized variants (Resources.<lang>.resx) are embedded the same way
                for lang, files in loc_files.items():
                    for x in [x for x in files if x.startswith(rf[:-5] + '.' + lang)]:
                        its.append(('EmbeddedResource', x, d + '/' + os.path.relpath(x, pd).replace(os.sep, '/'), '%s.%s.resources' % (ln, lang)))
        items_by_dir[d] = its

def lines(its, base):
    out = []
    for kind, path, link, logical in its:
        rp = os.path.relpath(path, base)
        out.append('    <%s Include="%s" %sLink="%s" />' % (kind, rp, ('LogicalName="%s" ' % logical) if logical else '', link))
    return out

PLUG = os.path.join(ROOT, 'CrypWeb', 'CrypWeb.Plugins')
ISO = os.path.join(ROOT, 'CrypWeb', 'CrypWeb.Plugins.Isolated')
import shutil as _sh
_sh.rmtree(ISO, ignore_errors=True)
main_lines = []; iso_refs = []; iso_names = []
for d, its in items_by_dir.items():
    if d in ISOLATE:
        pdir = os.path.join(ISO, d); os.makedirs(pdir, exist_ok=True)
        open(os.path.join(pdir, 'plugins.g.props'), 'w').write('<Project>\n  <ItemGroup>\n' + '\n'.join(lines(its, pdir)) + '\n  </ItemGroup>\n</Project>\n')
        open(os.path.join(pdir, 'CrypWeb.Plugin.%s.csproj' % d), 'w').write(ISO_CSPROJ.replace('@ASM@', 'CrypWeb.Plugin.' + d).replace('@ALIAS@', PRIVATE_ALIAS if d in ISOLATE_PRIVATE else ''))
        iso_refs.append('    <ProjectReference Include="$(MSBuildThisFileDirectory)..\\CrypWeb.Plugins.Isolated\\%s\\CrypWeb.Plugin.%s.csproj" />' % (d, d)); iso_names.append('CrypWeb.Plugin.' + d)
    else:
        main_lines += lines(its, PLUG)
# KeySearcher itself is a WPF/cloud component, but the plugins that support key searching (AES, DES, RC2, RC4, ...) use its pure-logic helpers
if 'KeySearcher' not in items_by_dir:
    for sub in ('KeyPattern', 'KeyTranslators'):
        for f in sorted(glob.glob(os.path.join(PL, 'KeySearcher', sub, '*.cs'))):
            main_lines += lines([('Compile', f, 'KeySearcher/' + sub + '/' + os.path.basename(f), None)], PLUG)
os.makedirs(PLUG, exist_ok=True)
open(os.path.join(PLUG, 'plugins.g.props'), 'w').write('<Project>\n  <ItemGroup>\n' + '\n'.join(main_lines) + '\n  </ItemGroup>\n</Project>\n')
open(os.path.join(PLUG, 'isolated.g.props'), 'w').write('<Project>\n  <ItemGroup>\n' + '\n'.join(iso_refs) + '\n  </ItemGroup>\n</Project>\n')
open(os.path.join(ROOT, 'CrypWeb', 'CrypWeb.Core', 'IsolatedAssemblies.g.cs'), 'w').write('// generated by tools-gen-plugins.py\nnamespace CrypTool.Web.Core;\npublic static class IsolatedAssemblies { public static readonly string[] Names = { %s }; }\n' % ', '.join('"%s"' % n for n in iso_names))
json.dump(list(meta.values()), open(os.path.join(OUT, 'plugins.json'), 'w'))
print(len(meta), 'components;', sum(1 for m in meta.values() if m['supported']), 'enabled for build;', sum(1 for m in meta.values() if m['icon']), 'icons')
