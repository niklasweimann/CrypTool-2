#!/usr/bin/env python3
"""Generates the dark variant of the user interface: CrypWeb.App/wwwroot/css/dark.css (build step; the result is committed).

The rules of app.css and docs.css are read; for every rule that sets a light color of the interface (background, text,
border) a counterpart under "html.ui-dark" is written with the lightness turned around: near-white surfaces become dark,
dark text becomes light, saturated accent colors stay. The workspace (components, wires, ports, notes, minimap) is left
alone: its look comes from the design (Designs.cs), not from the light/dark mode of the interface.

Run it after changing colors in app.css or docs.css:   python3 tools-gen-dark.py
"""
import colorsys
import os
import re

CSS = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'CrypWeb.App', 'wwwroot', 'css')
SOURCES = ['app.css', 'docs.css']
OUT = os.path.join(CSS, 'dark.css')
PREFIX = 'html.ui-dark'

# classes of the workspace: they keep the colors of the design
SKIP = re.compile(r'\.(node|canvas|canvas-over|nview|nviews|nclip|nat|native|wire|wire-hit|wire-seg|wire-under|wiregrp|port|'
                  r'pbg|pfill|cnt|cnttxt|ptxt|uname|tbb|tbhit|tbt|ti|warn|wtxt|handle|marquee|note-bg|note-g|note-view|note|'
                  r'timing|timingtxt|ring|bub|title|mintxt|viewbtn|cicon|minimap|mm-\w+|comp-\w+|cv-dark|splice|faded|dim)(?![\w-])')

BG = re.compile(r'^background(-color|-image)?$')
TEXT = re.compile(r'^(color|fill|caret-color|text-decoration-color)$')
BORDER = re.compile(r'^(border(-top|-right|-bottom|-left)?(-color)?|outline(-color)?|stroke)$')
COLOR = re.compile(r'#[0-9a-fA-F]{8}\b|#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b|rgba?\([^)]*\)|\bwhite\b|\bblack\b')


def parse_color(text):
    t = text.strip().lower()
    if t == 'white':
        return 255, 255, 255, 1.0
    if t == 'black':
        return 0, 0, 0, 1.0
    if t.startswith('#'):
        h = t[1:]
        if len(h) == 3:
            h = ''.join(c * 2 for c in h)
        a = int(h[6:8], 16) / 255 if len(h) == 8 else 1.0
        return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a
    nums = [float(x.strip().rstrip('%')) for x in re.findall(r'[-\d.]+%?', t[t.index('(') + 1:t.rindex(')')])]
    a = nums[3] if len(nums) > 3 else 1.0
    return nums[0], nums[1], nums[2], a


def fmt(r, g, b, a):
    if a < 1:
        return 'rgba(%d, %d, %d, %s)' % (round(r), round(g), round(b), ('%.2f' % a).rstrip('0').rstrip('.'))
    return '#%02x%02x%02x' % (round(r), round(g), round(b))


def dark(color, role):
    """The dark counterpart of one color; role is 'bg', 'text' or 'border'."""
    r, g, b, a = parse_color(color)
    h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
    if s >= 0.45 and 0.3 <= l <= 0.75:
        return None  # an accent or status color
    if role == 'bg' and l < 0.35:
        return None  # already dark
    if role == 'text' and l > 0.5:
        return None  # light text sits on dark or colored surfaces
    if role == 'border' and l < 0.25:
        return None
    nl = 0.09 + (1 - l) * 0.83
    if s < 0.1 and nl < 0.4:
        h, s = 222 / 360, 0.12  # neutral surfaces get a slight blue, like the light ones
    else:
        s *= 0.75
    r, g, b = colorsys.hls_to_rgb(h, nl, s)
    return fmt(r * 255, g * 255, b * 255, a)


def split_top(text, sep):
    """Splits at sep outside of parentheses."""
    parts, depth, cur = [], 0, ''
    for ch in text:
        if ch == '(':
            depth += 1
        elif ch == ')':
            depth -= 1
        if ch == sep and depth == 0:
            parts.append(cur)
            cur = ''
        else:
            cur += ch
    parts.append(cur)
    return parts


def blocks(text):
    """(prelude, body) of the top-level blocks."""
    out, depth, start, prelude_start = [], 0, 0, 0
    for i, ch in enumerate(text):
        if ch == '{':
            if depth == 0:
                start = i
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                out.append((text[prelude_start:start].strip(), text[start + 1:i]))
                prelude_start = i + 1
    return out


def transform_declarations(body):
    out = []
    for decl in split_top(body, ';'):
        if ':' not in decl:
            continue
        prop, value = decl.split(':', 1)
        prop = prop.strip()
        role = 'bg' if BG.match(prop) else 'text' if TEXT.match(prop) else 'border' if BORDER.match(prop) else None
        if role is None or 'var(' in value:
            continue
        changed = False

        def swap(m):
            nonlocal changed
            new = dark(m.group(0), role)
            if new is None:
                return m.group(0)
            changed = True
            return new

        new_value = COLOR.sub(swap, value)
        if changed:
            out.append('%s: %s' % (prop, new_value.strip()))
    return out


def prefixed(selector):
    selector = selector.strip()
    if SKIP.search(selector) or selector.startswith(':root'):
        return None
    if selector in ('html', 'html, body'):
        return PREFIX
    if selector == 'body':
        return PREFIX + ' body'
    if selector.startswith('html'):
        return PREFIX + selector[4:]
    return PREFIX + ' ' + selector


def rules(text, indent=''):
    out = []
    for prelude, body in blocks(text):
        if prelude.startswith('@media') or prelude.startswith('@supports'):
            inner = rules(body, indent + '  ')
            if inner:
                out.append('%s%s {\n%s\n%s}' % (indent, prelude, '\n'.join(inner), indent))
        elif prelude.startswith('@'):
            continue
        else:
            declarations = transform_declarations(body)
            selectors = [p for p in (prefixed(s) for s in split_top(prelude, ',')) if p]
            if declarations and selectors:
                out.append('%s%s {\n%s\n%s}' % (indent, ',\n'.join(indent + s for s in selectors),
                                                ''.join('%s    %s;\n' % (indent, d) for d in declarations).rstrip('\n'), indent))
    return out


def root_variables(text):
    """The colors of :root, turned around the same way."""
    lines = []
    for prelude, body in blocks(text):
        if prelude.strip() != ':root':
            continue
        for decl in split_top(body, ';'):
            if ':' not in decl:
                continue
            name, value = (x.strip() for x in decl.split(':', 1))
            if not name.startswith('--') or not COLOR.fullmatch(value):
                continue
            role = 'text' if name in ('--muted',) else 'border' if name == '--line' else 'bg'
            new = dark(value, role)
            if new:
                lines.append('    %s: %s;' % (name, new))
    return lines


EXTRA = """
html.ui-dark {
    color-scheme: dark;
}

/* the workspace keeps its look: native fields inside the components stay light */
html.ui-dark .nviews, html.ui-dark .minimap {
    color-scheme: light;
}

html.ui-dark .nviews .nview, html.ui-dark .nviews .nat {
    color: #1f2430;
}

/* pictures of the documentation come with a light background of their own */
html.ui-dark .dc-article img {
    background: #fff;
    border-radius: 4px;
}
"""


def main():
    css = ''
    for name in SOURCES:
        with open(os.path.join(CSS, name), encoding='utf-8') as f:
            css += re.sub(r'/\*.*?\*/', '', f.read(), flags=re.S) + '\n'
    text = ('/* Generated by tools-gen-dark.py from app.css and docs.css: the dark variant of the interface. Do not edit. */\n'
            'html.ui-dark {\n' + '\n'.join(root_variables(css)) + '\n}\n' + EXTRA + '\n' + '\n'.join(rules(css)) + '\n')
    with open(OUT, 'w', encoding='utf-8') as f:
        f.write(text)
    print('dark.css:', text.count('{'), 'rules')


if __name__ == '__main__':
    main()
