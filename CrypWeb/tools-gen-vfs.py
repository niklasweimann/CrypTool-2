#!/usr/bin/env python3
"""Builds wwwroot/vfs/ and its manifest.json: the data files that plugins read from disk on the desktop.

In the browser they are registered as lazy files in the runtime's virtual file system (wwwroot/vfs.js) and
downloaded on first read. manifest.json lists each file as {"p": virtual path, "s": size in bytes}.
"""
import glob
import json
import os
import re
import shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.join(ROOT, 'CrypWeb', 'CrypWeb.App', 'wwwroot', 'vfs')

# files that plugin projects copy next to the executable but that are code or configuration, not data
CODE_FILE_PATTERN = re.compile(r'\.(exe|dll|pdb|cs|xaml|resx|csproj|config)$', re.I)

# <Content|None|EmbeddedResource Include="..."> with <CopyToOutputDirectory>Always|PreserveNewest</CopyToOutputDirectory>
COPIED_FILE_PATTERN = re.compile(
    r'<(?:Content|None|EmbeddedResource)\s+Include="([^"]+)"[^>]*>\s*(?:<[^>]*>\s*)*?'
    r'<CopyToOutputDirectory>(?:Always|PreserveNewest)</CopyToOutputDirectory>')


class Vfs:
    """Collects the files: copies each to the output folder and remembers its size."""

    def __init__(self):
        self.sizes = {}  # virtual path -> size

    def add(self, virtual_path, source):
        target = os.path.join(OUT, virtual_path.lstrip('/'))
        os.makedirs(os.path.dirname(target), exist_ok=True)
        shutil.copy2(source, target)
        self.sizes[virtual_path] = os.path.getsize(source)

    def add_language_statistics(self):
        """N-grams and dictionaries live next to the executable on the desktop."""
        for path in glob.glob(os.path.join(ROOT, 'LanguageStatistics', '*')):
            if os.path.isfile(path):
                self.add('/LanguageStatistics/' + os.path.basename(path), path)

    def add_plugin_files(self):
        """Files that the plugin projects copy to the output directory (CrypPlugins/<relative path>)."""
        for project in glob.glob(os.path.join(ROOT, 'CrypPlugins', '*', '*.csproj')):
            folder = os.path.dirname(project)
            with open(project, encoding='utf-8-sig', errors='ignore') as f:
                text = f.read()
            for match in COPIED_FILE_PATTERN.finditer(text):
                relative = match.group(1).replace('\\', '/')
                source = os.path.join(folder, relative)
                if os.path.isfile(source) and not CODE_FILE_PATTERN.search(relative):
                    self.add('/CrypPlugins/' + relative, source)

    def add_template_data(self):
        """Data files of the templates (pictures, examples) that template settings name as "Templates\\<folder>\\<file>".

        The template files themselves and their preview pictures/descriptions (<name>.png/.xml next to <name>.cwm)
        are not needed here.
        """
        templates = os.path.join(ROOT, 'Templates')
        for path in glob.glob(os.path.join(templates, '**', '*'), recursive=True):
            base, extension = os.path.splitext(path)
            is_template_file = extension.lower() == '.cwm'
            belongs_to_template = os.path.exists(base + '.cwm')
            if not os.path.isfile(path) or is_template_file or belongs_to_template or os.path.basename(base) == 'dir':
                continue
            self.add('/Templates/' + os.path.relpath(path, templates).replace(os.sep, '/'), path)

    def write_manifest(self):
        entries = [{'p': path, 's': size} for path, size in sorted(self.sizes.items())]
        with open(os.path.join(OUT, 'manifest.json'), 'w') as f:
            json.dump(entries, f)


def main():
    shutil.rmtree(OUT, ignore_errors=True)
    os.makedirs(OUT)
    vfs = Vfs()
    vfs.add_language_statistics()
    vfs.add_plugin_files()
    vfs.add_template_data()
    vfs.write_manifest()
    print(len(vfs.sizes), 'files,', round(sum(vfs.sizes.values()) / 1e6), 'MB')


if __name__ == '__main__':
    main()
