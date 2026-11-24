#!/usr/bin/env python3
"""Generates the documentation of the web edition from the sources of CrypTool 2 (build step, replaces the
OnlineDocumentationGenerator project, which needs WPF and the desktop build).

Reads
  plugins.json                    which components exist and where their documentation file is ("doc", see tools/gen/plugins.py)
  CrypPlugins/*/<doc>.xml         the component documentation (<documentation> files, en/de)
  Templates/**/*.xml              title, summary, description and relevant components of every template
  OnlineDocumentationGenerator/StaticDocs/*.xml   the general articles (homomorphic ciphers, CrypTool book, ...)

Writes to CrypWeb.App/wwwroot/docs
  index.json                      what the documentation navigation needs: components, templates, articles (titles, summaries)
  c/<type>.json                   one component: introduction / usage / presentation per language, references
  a/<id>.json                     one general article
  img/<hash>.<ext>                the pictures the articles use

The texts are converted from the CrypTool markup (<b>, <newline/>, <docRef item="...">, <section headline="...">, ...)
to a small, safe subset of HTML. Links between articles are "?doc=component/<type>", "?doc=template/<path>" and
"?doc=article/<id>"; the app (Components/DocsPage.razor) shows what such a link names.
"""
import hashlib
import html
import json
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
CRYPWEB = os.path.join(ROOT, 'CrypWeb')
WWWROOT = os.path.join(CRYPWEB, 'CrypWeb.App', 'wwwroot')
OUT = os.path.join(WWWROOT, 'docs')
PLUGIN_ROOTS = [os.path.join(ROOT, 'CrypPlugins'), os.path.join(ROOT, 'Experimental', 'CrypPlugins')]
TEMPLATE_ROOTS = [(os.path.join(ROOT, 'Templates'), ''), (os.path.join(ROOT, 'Experimental', 'Templates'), 'Experimental/')]
STATIC_DOCS = os.path.join(ROOT, 'OnlineDocumentationGenerator', 'StaticDocs')
LANGUAGES = ['en', 'de']

# the general articles: id, XML file (the id is the name in the link "?doc=article/<id>")
ARTICLES = [
    ('book', 'CrypToolBook.xml'),
    ('homomorphic-ciphers', 'HomomorphicChiffres.xml'),
    ('prf-key-derivation', 'PseudoRandomFunction_based_KeyDerivationFunctions.xml'),
]

warnings = []


def warn(message):
    warnings.append(message)


# ---------------------------------------------------------------------------------------------------------------------
# small helpers
# ---------------------------------------------------------------------------------------------------------------------

def lang_of(value):
    """'de-DE' -> 'de'; no language means English."""
    return (value or 'en').split('-')[0].lower()


def load_xml(path):
    try:
        return ET.parse(path).getroot()
    except (ET.ParseError, OSError) as error:
        warn('%s: %s' % (os.path.relpath(path, ROOT), error))
        return None


def localized(root, tag):
    """{lang: element} of the children <tag lang="..."> of root (a child without language is English)."""
    result = {}
    for child in root.findall(tag):
        result.setdefault(lang_of(child.get('lang')), child)
    return result


def esc(text):
    """Escapes text for HTML content (quotes stay as they are)."""
    return html.escape(text, quote=False)


def pick(by_lang, lang):
    """The element of a language, else the English one, else None (elements are falsy when they have no children)."""
    element = by_lang.get(lang)
    return element if element is not None else by_lang.get('en')


def plain_text(element):
    """The text of an element without markup, whitespace collapsed."""
    if element is None:
        return ''
    return re.sub(r'\s+', ' ', ''.join(element.itertext())).strip()


def slug(text):
    return re.sub(r'[^a-z0-9]+', '-', text.lower()).strip('-') or 'section'


# ---------------------------------------------------------------------------------------------------------------------
# pictures
# ---------------------------------------------------------------------------------------------------------------------

class Pictures:
    """Copies the pictures an article uses to docs/img (named by content, so that two articles share one file)."""

    def __init__(self):
        self.copied = {}

    def publish(self, source):
        """The URL (relative to the app) of the picture named like a file under CrypPlugins or the repository, or None."""
        source = source.replace('\\', '/').lstrip('/')
        for base in PLUGIN_ROOTS + [ROOT]:
            path = os.path.join(base, source)
            if os.path.isfile(path):
                break
        else:
            return None
        if path not in self.copied:
            with open(path, 'rb') as f:
                digest = hashlib.sha1(f.read()).hexdigest()[:12]
            name = digest + os.path.splitext(path)[1].lower()
            os.makedirs(os.path.join(OUT, 'img'), exist_ok=True)
            shutil.copyfile(path, os.path.join(OUT, 'img', name))
            self.copied[path] = 'docs/img/' + name
        return self.copied[path]


# ---------------------------------------------------------------------------------------------------------------------
# CrypTool markup -> HTML
# ---------------------------------------------------------------------------------------------------------------------

BREAK_TAGS = {'newline', 'newinle', 'newoine', 'br'}            # (the typos exist in the sources)
BLOCK_TAGS = {'list', 'enum', 'ul', 'ol', 'table', 'section', 'subsection', 'subsubsection'}
SAFE_COLOR = re.compile(r'^(#[0-9a-fA-F]{3,8}|[a-zA-Z]{3,20})$')


class Converter:
    """Converts the content of one documentation element to HTML.

    `components` maps a component type name to True (the ones that have an article), `templates` the template names.
    `references` is the list of reference ids of the article, in order; [n] in the text is the position in that list.
    """

    def __init__(self, pictures, components, references, context):
        self.pictures = pictures
        self.components = components
        self.references = references
        self.context = context  # for warnings

    # --- inline / block structure: the result of children() is a list of ('inline'|'block'|'break', html) ---

    def children(self, element):
        parts = []
        if element.text:
            parts.append(('inline', esc(re.sub(r'\s+', ' ', element.text))))
        for child in element:
            parts.extend(self.node(child))
            if child.tail:
                parts.append(('inline', esc(re.sub(r'\s+', ' ', child.tail))))
        return parts

    def node(self, element):
        tag = element.tag.lower() if isinstance(element.tag, str) else ''
        if tag in BREAK_TAGS:
            return [('break', '')]
        if tag in ('b', 'i', 'u'):
            return [('inline', '<%s>%s</%s>' % (tag, self.inline(element), tag))]
        if tag == 'it':
            return [('inline', '<i>%s</i>' % self.inline(element))]
        if tag == 'font':
            color = element.get('color')
            style = ' style="color:%s"' % color if color and SAFE_COLOR.match(color) else ''
            return [('inline', '<span%s>%s</span>' % (style, self.inline(element)))]
        if tag == 'ref':
            return [('inline', self.citation(element.get('id')))]
        if tag == 'external':
            return [('inline', self.external(element))]
        if tag == 'internal':
            return [('inline', self.inline(element))]
        if tag == 'docref':
            return [('inline', self.doc_link(element))]
        if tag == 'img':
            return [('block', self.picture(element))]
        if tag in ('list', 'ul', 'enum', 'ol'):
            return [('block', self.list(element, 'ol' if tag in ('enum', 'ol') else 'ul'))]
        if tag == 'table':
            return [('block', self.table(element))]
        if tag in ('section', 'subsection', 'subsubsection'):
            return self.section(element, tag)
        if tag in ('li', 'item'):
            return [('block', '<ul><li>%s</li></ul>' % self.inline(element))]
        # unknown element: keep its content
        return self.children(element)

    def inline(self, element):
        """Content without paragraph structure (list items, table cells, bold text, ...): line breaks stay <br>."""
        out = []
        for kind, text in self.children(element):
            out.append('<br>' if kind == 'break' else text)
        return re.sub(r'(<br>\s*){3,}', '<br><br>', ''.join(out)).strip()

    def blocks(self, element):
        """The content as paragraphs: text runs separated by <newline/> become <p>, blocks stand for themselves."""
        return self.paragraphs(self.children(element))

    @staticmethod
    def paragraphs(parts):
        out, current = [], []

        def flush():
            text = ''.join(current).strip()
            if text:
                out.append('<p>%s</p>' % text)
            current.clear()

        for kind, text in parts:
            if kind == 'inline':
                current.append(text)
            elif kind == 'break':
                flush()
            else:
                flush()
                out.append(text)
        flush()
        return '\n'.join(out)

    # --- elements ---

    def citation(self, ref_id):
        if ref_id in self.references:
            number = self.references.index(ref_id) + 1
            return '<sup class="cite"><a href="#ref-%s">[%d]</a></sup>' % (html.escape(ref_id), number)
        return ''

    @staticmethod
    def external(element):
        url = (element.get('ref') or '').strip()
        text = Converter.inline_static(element) or html.escape(url)
        if url.lower().startswith(('http://', 'https://')):
            return '<a href="%s" target="_blank" rel="noopener noreferrer" class="ext">%s</a>' % (html.escape(url), text)
        return text

    @staticmethod
    def inline_static(element):
        return esc(plain_text(element))

    def doc_link(self, element):
        item = (element.get('item') or '').strip()
        text = self.inline(element)
        if item in self.components:
            return '<a href="?doc=component/%s" class="doc">%s</a>' % (html.escape(item), text or esc(self.components[item]))
        if item:
            warn('%s: docRef to unknown component "%s"' % (self.context, item))
        return '<b>%s</b>' % (text or esc(item.split('.')[-1]))

    def picture(self, element):
        source = (element.get('src') or '').strip()
        url = self.pictures.publish(source) if source else None
        if not url:
            warn('%s: picture "%s" not found' % (self.context, source))
            return ''
        caption = element.get('caption')
        figure = '<img src="%s" alt="%s" loading="lazy">' % (url, html.escape(caption or ''))
        if caption:
            figure += '<figcaption>%s</figcaption>' % esc(caption)
        return '<figure>%s</figure>' % figure

    def list(self, element, tag):
        items = [self.inline(item) for item in element if isinstance(item.tag, str) and item.tag.lower() in ('item', 'li')]
        return '<%s>%s</%s>' % (tag, ''.join('<li>%s</li>' % item for item in items), tag)

    def table(self, element):
        rows = []
        for row in element:
            if not isinstance(row.tag, str) or row.tag.lower() != 'tr':
                continue
            cells = ''.join('<%s>%s</%s>' % (cell.tag.lower(), self.inline(cell), cell.tag.lower())
                            for cell in row if isinstance(cell.tag, str) and cell.tag.lower() in ('td', 'th'))
            rows.append('<tr>%s</tr>' % cells)
        return '<div class="tablewrap"><table>%s</table></div>' % ''.join(rows)

    def section(self, element, tag):
        level = {'section': 2, 'subsection': 3, 'subsubsection': 4}[tag]
        headline = element.get('headline') or ''
        parts = [('block', '<h%d id="%s">%s</h%d>' % (level, slug(headline), esc(headline), level))] if headline else []
        # the content of a section is a block, so that the text around sections is split into paragraphs
        return parts + [('block', self.paragraphs(self.children(element)))]


# ---------------------------------------------------------------------------------------------------------------------
# references
# ---------------------------------------------------------------------------------------------------------------------

def read_references(documentation):
    """[{id, html: {lang: ...}}] of the <references> element."""
    result = []
    references = documentation.find('references')
    if references is None:
        return result
    for reference in references:
        ref_id = reference.get('id')
        if not ref_id or not isinstance(reference.tag, str):
            continue
        entry = {'id': ref_id, 'html': {}}
        if reference.tag == 'linkReference':
            links = localized(reference, 'link')
            captions = localized(reference, 'caption')
            for lang in LANGUAGES:
                link, caption = pick(links, lang), pick(captions, lang)
                if link is None:
                    continue
                url = (link.get('url') or '').strip()
                text = esc(plain_text(caption) or url)
                entry['html'][lang] = ('<a href="%s" target="_blank" rel="noopener noreferrer" class="ext">%s</a>' % (html.escape(url), text)
                                       if url.lower().startswith(('http://', 'https://')) else text)
        elif reference.tag == 'bookReference':
            fields = {name: localized(reference, name) for name in ('author', 'name', 'publisher', 'year')}
            for lang in LANGUAGES:
                def field(name):
                    return plain_text(pick(fields[name], lang))
                text = '%s. %s. <i>%s</i> (%s)' % tuple(esc(field(n)) for n in ('author', 'name', 'publisher', 'year'))
                entry['html'][lang] = text
        else:
            continue
        result.append(entry)
    return result


# ---------------------------------------------------------------------------------------------------------------------
# components
# ---------------------------------------------------------------------------------------------------------------------

def find_documentation(plugin):
    """The path of the documentation XML of a plugins.json entry, or None."""
    doc = plugin.get('doc')
    if not doc:
        return None
    for base in PLUGIN_ROOTS:
        path = os.path.join(base, doc)
        if os.path.isfile(path):
            return path
    # the first segment of the path names the assembly, which can differ from the folder
    path = os.path.join(PLUGIN_ROOTS[0], plugin['dir'], doc.split('/', 1)[-1])
    if os.path.isfile(path):
        return path
    for base in PLUGIN_ROOTS:
        path = os.path.join(base, plugin['dir'], doc.split('/', 1)[-1])
        if os.path.isfile(path):
            return path
    return None


def convert_document(documentation, converter_factory):
    """{lang: {introduction, usage, presentation}} of a <documentation> element."""
    languages = [lang_of(e.get('culture')) for e in documentation.findall('language')] or ['en']
    sections = {name: localized(documentation, name) for name in ('introduction', 'usage', 'presentation')}
    # typos in a few files
    for typo, name in (('introductionn', 'introduction'), ('presentationn', 'presentation')):
        for lang, element in localized(documentation, typo).items():
            sections[name].setdefault(lang, element)
    result = {}
    for lang in LANGUAGES:
        if lang not in languages and lang != 'en':
            continue
        converter = converter_factory(lang)
        entry = {}
        for name, by_lang in sections.items():
            element = by_lang.get(lang)
            if element is None:
                continue
            text = converter.blocks(element)
            if text:
                entry[name] = text
        if entry:
            result[lang] = entry
    return result


def write_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, separators=(',', ':'))


# ---------------------------------------------------------------------------------------------------------------------
# templates
# ---------------------------------------------------------------------------------------------------------------------

def read_templates():
    """[{id, title, summary, description, keywords, plugins}] of every template description (.xml next to the .cwm)."""
    result = []
    for base, prefix in TEMPLATE_ROOTS:
        for directory, _, names in os.walk(base):
            for name in sorted(names):
                if not name.endswith('.xml'):
                    continue
                cwm = os.path.join(directory, name[:-4] + '.cwm')
                if not os.path.isfile(cwm):
                    continue
                root = load_xml(os.path.join(directory, name))
                if root is None or root.tag != 'sample':
                    continue
                template_id = prefix + os.path.relpath(cwm, base).replace(os.sep, '/')
                entry = {'id': template_id, 'title': {}, 'summary': {}, 'description': {}, 'keywords': {}, 'plugins': []}
                for key in ('title', 'summary', 'description', 'keywords'):
                    for lang, element in localized(root, key).items():
                        entry[key][lang] = plain_text(element)
                entry['summary_html'] = {lang: inline_markup(element) for lang, element in localized(root, 'summary').items()}
                entry['plugins'] = [p.get('name') for p in root.findall('relevantPlugins/plugin') if p.get('name')]
                if not entry['title']:
                    continue
                result.append(entry)
    return sorted(result, key=lambda t: t['id'].lower())


def inline_markup(element):
    """Summaries use <i> and <b> only."""
    out = esc(re.sub(r'\s+', ' ', element.text or ''))
    for child in element:
        tag = child.tag.lower() if isinstance(child.tag, str) else ''
        inner = inline_markup(child)
        out += '<%s>%s</%s>' % (tag, inner, tag) if tag in ('b', 'i', 'u') else inner
        out += esc(re.sub(r'\s+', ' ', child.tail or ''))
    return out.strip()


# ---------------------------------------------------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------------------------------------------------

def main():
    with open(os.path.join(WWWROOT, 'plugins.json'), encoding='utf-8') as f:
        plugins = json.load(f)
    shutil.rmtree(OUT, ignore_errors=True)
    os.makedirs(OUT)

    pictures = Pictures()
    templates = read_templates()
    component_names = {p['type']: p['name'] for p in plugins}
    has_article = {}
    articles_of = {}   # component type -> documentation (only the components with a documentation file)

    # pass 1: which components have an article (so that docRefs can be resolved)
    for plugin in plugins:
        path = find_documentation(plugin)
        if not path:
            continue
        root = load_xml(path)
        if root is not None and root.tag == 'documentation':
            articles_of[plugin['type']] = (plugin, path, root)
            has_article[plugin['type']] = plugin['name']

    # which templates use which component ("relevantPlugins" names the class)
    by_class = {}
    for plugin in plugins:
        by_class.setdefault(plugin['type'].split('.')[-1], []).append(plugin['type'])
    used_in = {}
    missing = set()  # templates name components that are not part of the web edition
    for template in templates:
        for name in template['plugins']:
            for type_name in by_class.get(name, []):
                used_in.setdefault(type_name, []).append(template['id'])
            if name not in by_class:
                missing.add(name)
    for template in templates:
        template['plugins'] = [t for n in template['plugins'] for t in by_class.get(n, [])]

    # components
    index_components = []
    for plugin in plugins:
        entry = {'id': plugin['type'], 'name': plugin['name'], 'category': plugin['category'], 'icon': plugin.get('icon'),
                 'tooltip': plugin['tooltip'], 'experimental': bool(plugin.get('experimental')), 'article': False}
        for lang, loc in (plugin.get('loc') or {}).items():
            if lang in LANGUAGES:
                entry.setdefault('loc', {})[lang] = {'name': loc.get('name') or plugin['name'], 'tooltip': loc.get('tooltip') or plugin['tooltip']}
        if plugin['type'] in articles_of:
            _, path, root = articles_of[plugin['type']]
            references = read_references(root)
            ids = [r['id'] for r in references]
            body = convert_document(root, lambda lang: Converter(pictures, has_article, ids, plugin['type']))
            document = {'text': body, 'references': references, 'templates': used_in.get(plugin['type'], [])}
            if body or document['templates']:
                write_json(os.path.join(OUT, 'c', plugin['type'] + '.json'), document)
                entry['article'] = True
        elif used_in.get(plugin['type']):
            write_json(os.path.join(OUT, 'c', plugin['type'] + '.json'), {'text': {}, 'references': [], 'templates': used_in[plugin['type']]})
            entry['article'] = True
        index_components.append(entry)

    # general articles
    index_articles = []
    for article_id, file_name in ARTICLES:
        root = load_xml(os.path.join(STATIC_DOCS, file_name))
        if root is None:
            continue
        references = read_references(root)
        ids = [r['id'] for r in references]
        titles = {lang: plain_text(e) for lang, e in localized(root, 'name').items()}
        text = {}
        for lang in LANGUAGES:
            converter = Converter(pictures, has_article, ids, article_id)
            element = localized(root, 'description').get(lang)
            if element is not None:
                text[lang] = {'description': converter.blocks(element)}
        write_json(os.path.join(OUT, 'a', article_id + '.json'), {'text': text, 'references': references})
        index_articles.append({'id': article_id, 'title': titles})

    # templates (the article of a template is made from the index entry)
    index_templates = [{'id': t['id'], 'title': t['title'], 'summary': t['summary_html'], 'description': t['description'],
                        'keywords': t['keywords'], 'plugins': t['plugins']} for t in templates]

    write_json(os.path.join(OUT, 'index.json'), {'components': index_components, 'templates': index_templates, 'articles': index_articles})

    # summary
    total = sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk(OUT) for f in fs)
    print('docs: %d components (%d with article), %d templates, %d articles, %d pictures, %.1f MB' % (
        len(index_components), sum(1 for c in index_components if c['article']), len(index_templates), len(index_articles),
        len(pictures.copied), total / 1e6))
    if missing:
        print('  note: %d components named by templates are not part of the web edition (%s, ...)' % (len(missing), ', '.join(sorted(missing)[:5])))
    for message in sorted(set(warnings)):
        print('  warning:', message, file=sys.stderr)


if __name__ == '__main__':
    main()
