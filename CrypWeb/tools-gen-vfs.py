#!/usr/bin/env python3
"""Builds wwwroot/vfs/ + manifest.json: data files that plugins read from disk on the desktop.
In the engine worker they are registered as lazy files in the virtual file system (fetched on first read)."""
import os, re, json, shutil, glob
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.join(ROOT, 'CrypWeb', 'CrypWeb.App', 'wwwroot', 'vfs')
shutil.rmtree(OUT, ignore_errors=True); os.makedirs(OUT)
entries = {}
def add(vpath, src):
    dst = os.path.join(OUT, vpath.lstrip('/'))
    os.makedirs(os.path.dirname(dst), exist_ok=True); shutil.copy2(src, dst)
    entries[vpath] = os.path.getsize(src)
# language statistics (n-grams, dictionaries) live next to the executable
for f in glob.glob(os.path.join(ROOT, 'LanguageStatistics', '*')):
    if os.path.isfile(f): add('/LanguageStatistics/' + os.path.basename(f), f)
# files that the plugin projects copy to the output directory (CrypPlugins/<relative path>)
skip = re.compile(r'\.(exe|dll|pdb|cs|xaml|resx|csproj|config)$', re.I)
for proj in glob.glob(os.path.join(ROOT, 'CrypPlugins', '*', '*.csproj')):
    d = os.path.dirname(proj); text = open(proj, encoding='utf-8-sig', errors='ignore').read()
    for m in re.finditer(r'<(?:Content|None|EmbeddedResource)\s+Include="([^"]+)"[^>]*>\s*(?:<[^>]*>\s*)*?<CopyToOutputDirectory>(?:Always|PreserveNewest)</CopyToOutputDirectory>', text):
        rel = m.group(1).replace('\\', '/')
        src = os.path.join(d, rel)
        if os.path.isfile(src) and not skip.search(rel): add('/CrypPlugins/' + rel, src)
# data files of the templates (pictures, examples) that template settings name as "Templates\\<folder>\\<file>";
# the template files themselves and their preview pictures/descriptions (<name>.png/.xml next to <name>.cwm) are not
# needed here
TEMPLATES = os.path.join(ROOT, 'Templates')
for f in glob.glob(os.path.join(TEMPLATES, '**', '*'), recursive=True):
    base, ext = os.path.splitext(f)
    if not os.path.isfile(f) or ext.lower() == '.cwm' or os.path.exists(base + '.cwm') or os.path.basename(base) == 'dir':
        continue
    add('/Templates/' + os.path.relpath(f, TEMPLATES).replace(os.sep, '/'), f)
json.dump([{'p': k, 's': v} for k, v in sorted(entries.items())], open(os.path.join(OUT, 'manifest.json'), 'w'))
print(len(entries), 'files,', round(sum(entries.values()) / 1e6), 'MB')
