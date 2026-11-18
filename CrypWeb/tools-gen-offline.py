#!/usr/bin/env python3
"""Builds wwwroot/offline.json: the files that "Make available offline" can download, in groups, with their sizes.

The offline dialog (Components/OfflineModal.razor, wwwroot/offline.js) lets the user choose groups, so that the large
language data (up to 18 MB per language) is only downloaded for the languages that are needed. The app itself (runtime,
scripts, texts) is not listed: the service worker stores it after every start.

Groups:
  templates  the templates (.cwm, description .xml, picture) and the data files that templates read
  pictures   the component icons and the pictures that presentations show
  lang-xx    the dictionary and the n-gram statistics of one language

Run after tools-copy-templates.sh, tools-gen-vfs.py and tools-gen-presentations.py (tools-build-web.sh does).
"""
import json
import os
import re

WWWROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'CrypWeb.App', 'wwwroot')

# Dictionary_de.dic, de-3gram-nocs.gz, de-3gram-nocs-sp.gz
LANGUAGE_FILE = re.compile(r'^(?:Dictionary_([a-z]{2})\.dic|([a-z]{2})-\d+gram.*)$')


def files_below(folder):
    """[relative path, size] of every file below wwwroot/<folder>."""
    result = []
    for directory, _, names in os.walk(os.path.join(WWWROOT, folder)):
        for name in names:
            path = os.path.join(directory, name)
            result.append([os.path.relpath(path, WWWROOT).replace(os.sep, '/'), os.path.getsize(path)])
    return sorted(result)


def main():
    groups = {'templates': files_below('templates'), 'pictures': files_below('icons') + files_below('wpf-res')}
    with open(os.path.join(WWWROOT, 'vfs', 'manifest.json'), encoding='utf-8') as f:
        data_files = json.load(f)
    for entry in data_files:
        url = 'vfs' + entry['p']
        language = LANGUAGE_FILE.match(entry['p'].rsplit('/', 1)[-1]) if entry['p'].startswith('/LanguageStatistics/') else None
        if language:
            groups.setdefault('lang-' + (language.group(1) or language.group(2)), []).append([url, entry['s']])
        else:
            groups['templates'].append([url, entry['s']])  # data files of templates and plugins (small)
    result = [{'id': key, 'files': files} for key, files in groups.items()]
    with open(os.path.join(WWWROOT, 'offline.json'), 'w', encoding='utf-8') as f:
        json.dump({'groups': result}, f, separators=(',', ':'))
    for group in result:
        print('%-10s %5d files %7.1f MB' % (group['id'], len(group['files']), sum(s for _, s in group['files']) / 1e6))


if __name__ == '__main__':
    main()
