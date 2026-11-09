#!/usr/bin/env python3
"""Copies the plugins' presentation XAML (+ the images they reference and the plugin string resources) to wwwroot/xaml/
so the web app can draw the original layout. Writes wwwroot/xaml/index.json: {PluginFolder: {"files":[...], "res":{lang:{key:val}}}}."""
import os, re, json, shutil, glob, xml.etree.ElementTree as ET
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
PL = os.path.join(ROOT, 'CrypPlugins'); OUT = os.path.join(ROOT, 'CrypWeb', 'CrypWeb.App', 'wwwroot', 'xaml')
shutil.rmtree(OUT, ignore_errors=True); os.makedirs(OUT)
def resx(p):
    d = {}
    try:
        for e in ET.parse(p).getroot().findall('data'):
            v = e.find('value')
            if v is not None and e.get('type') is None: d[e.get('name')] = (v.text or '').strip()
    except Exception: pass
    return d
index = {}; nimg = 0
for d in sorted(os.listdir(PL)):
    base = os.path.join(PL, d)
    files = [f for f in glob.glob(base + '/**/*.xaml', recursive=True)
             if re.search(r'(Presentation|Pres|Visuali[sz]ation|QuickWatch|PresentationFrame|View)\w*\.xaml$', os.path.basename(f)) and '/obj/' not in f and 'Settings' not in f and d != 'WorkspaceManager'
             and not re.match(r'\s*(<\?xml[^>]*>\s*)?(<!--.*?-->\s*)*<(ResourceDictionary|Style)\b', open(f, encoding='utf-8-sig', errors='ignore').read(), re.S)]
    if not files: continue
    ent = {'files': [], 'res': {}, 'images': []}
    for f in files:
        rel = os.path.relpath(f, base); dst = os.path.join(OUT, d, rel); os.makedirs(os.path.dirname(dst), exist_ok=True); shutil.copy(f, dst); ent['files'].append(rel.replace(os.sep, '/'))
        txt = open(f, encoding='utf-8-sig', errors='ignore').read()
        for m in re.finditer(r'(?:Source|ImageSource|UriSource)="([^"{]+\.(?:png|jpg|jpeg|gif|svg|bmp))"', txt, re.I):
            s = m.group(1); s = re.sub(r'^pack://application:,,,/[^;]*;component/', '', s).lstrip('/')
            for cand in (os.path.join(os.path.dirname(f), s), os.path.join(base, s)):
                if os.path.isfile(cand):
                    t = os.path.join(OUT, d, os.path.relpath(cand, base)); os.makedirs(os.path.dirname(t), exist_ok=True); shutil.copy(cand, t); nimg += 1; ent['images'].append(os.path.relpath(cand, base).replace(os.sep, '/')); break
    # images the code-behind sets at runtime (not named in the XAML): every small image in the plugin's image folders
    for img in glob.glob(base + '/**/*', recursive=True):
        if re.search(r'/(images?|Images?|icons?)/', img) and img.lower().endswith(('.png', '.jpg', '.jpeg', '.gif', '.svg')) and '/obj/' not in img and os.path.getsize(img) < 200000:
            rel = os.path.relpath(img, base).replace(os.sep, '/')
            if rel not in ent['images']:
                t = os.path.join(OUT, d, rel); os.makedirs(os.path.dirname(t), exist_ok=True); shutil.copy(img, t); ent['images'].append(rel); nimg += 1
    for rx in glob.glob(base + '/**/Properties/Resources*.resx', recursive=True):
        m = re.match(r'^Resources(?:\.([a-z]{2})(?:-\w+)?)?\.resx$', os.path.basename(rx))
        if m: ent['res'].setdefault(m.group(1) or 'en', {}).update(resx(rx))
    index[d] = ent
json.dump(index, open(os.path.join(OUT, 'index.json'), 'w'), separators=(',', ':'))
print(len(index), 'plugins with presentation xaml,', nimg, 'images'); os.system('du -sh ' + OUT)
