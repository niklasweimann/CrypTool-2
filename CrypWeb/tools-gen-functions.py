#!/usr/bin/env python3
"""Function coverage of CrypWeb against the CrypTool function list (https://www.cryptool.org/de/functions/).

  python3 tools-gen-functions.py [saved-page.html]

With a saved copy of the page, reference/function-list.json (the snapshot of the list) is rewritten first.
Then every entry that CT2 offers is matched against what CrypWeb has (components in plugins.json, templates under
../Templates) and reference/functions.json is written. The summary printed at the end is what the
`functions` command of CrypWeb.Tests checks.

Only function names and where they are offered are taken from the list; no code of CT1 or JCrypTool is used.
Status of an entry:
  done     CT2 offers it and every component/template named for it exists in CrypWeb (parts that
           reference/function-aliases.json lists as not part of the browser version are not counted)
  partial  some of the named components/templates were not found
           or CT2 does not mark it, but a template listed in reference/function-covers.json covers it
  open     CT2 does not offer it (only CT1 / JCrypTool / CrypTool-Online do) and no template covers it: to be built
"""
import html
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

HERE = Path(__file__).resolve().parent
REFERENCE = HERE / "reference"
TEMPLATES = HERE.parent / "Templates"
PLUGINS = HERE / "CrypWeb.App/wwwroot/plugins.json"
PLUGIN_SOURCES = HERE.parent / "CrypPlugins"
EXCLUDE = HERE / "plugins.exclude"
ALIASES = REFERENCE / "function-aliases.json"
COVERS = REFERENCE / "function-covers.json"

TAGS = {"Analyse": "analysis", "Visualisierung": "visualization", "Angriff": "attack", "PRNG": "prng", "MPC": "mpc"}
SECTIONS = {
    "Links im CT2-Workspace als Komponente verfügbar:": "components",
    "In fertigem Beispiel-Template demonstriert:": "templates",
    "Teil der Wizards:": "wizards",
}


def text(fragment):
    fragment = re.sub(r"<[^>]+>", " ", fragment)
    return re.sub(r"\s+", " ", html.unescape(fragment)).strip()


def cells_of(row):
    return re.findall(r"<t[dh][^>]*>(.*?)</t[dh]>", row, re.S)


def offered(cell):
    """The columns show a green or a red icon (green icons are 448 wide, red ones 384)."""
    return 'color="green"' in cell


def list_items(cell):
    return [text(li) for li in re.findall(r"<li[^>]*>(.*?)</li>", cell, re.S)]


def sections_of(cell):
    """Splits a CT2 cell into its labelled lists: components, templates, wizards."""
    result = {"components": [], "templates": [], "wizards": []}
    parts = re.split("(" + "|".join(re.escape(s) for s in SECTIONS) + ")", re.sub(r"<style.*?</style>", "", cell, flags=re.S))
    for label, body in zip(parts[1::2], parts[2::2]):
        result[SECTIONS[label]] = list_items(body)
    return result


def split_name(raw):
    """The name cell contains the explanation of its badges ("Analyse Analyse = ..."): cut it off, keep the kinds."""
    kinds = []
    for tag, kind in TAGS.items():
        if re.search(rf"\b{tag} {tag} =", raw):
            kinds.append(kind)
    name = re.split(r"\s*\(?\s*\b(?:" + "|".join(TAGS) + r") (?:" + "|".join(TAGS) + r") =", raw)[0]
    return name.strip(" ("), kinds


def parse_page(path):
    page = re.sub(r"<style[^>]*>.*?</style>", "", Path(path).read_text(encoding="utf8"), flags=re.S)
    entries = []
    for row in re.findall(r"<tr[^>]*>(.*?)</tr>", page, re.S)[1:]:
        cells = cells_of(row)
        if len(cells) < 5:
            continue
        name, kinds = split_name(text(cells[0]))
        ct2 = sections_of(cells[2])
        entries.append({
            "name": name,
            "kinds": kinds,
            "ct1": offered(cells[1]),
            "ct2": offered(cells[2]),
            "jct": offered(cells[3]),
            "cto": offered(cells[4]),
            "ct2Components": ct2["components"],
            "ct2Templates": ct2["templates"],
            "wizards": ct2["wizards"],
            "ct1Where": list_items(cells[1]),
            "jctWhere": list_items(cells[3]),
        })
    return entries


def norm(s):
    return re.sub(r"[^0-9a-zäöüß]+", "", s.lower())


def component_names():
    """normalized names -> full type name of the components CrypWeb builds: display names in plugins.json, plus the
    class names found in CrypPlugins (plugins.json does not list every component of a folder)"""
    excluded = {l.strip() for l in EXCLUDE.read_text(encoding="utf8").splitlines() if l.strip() and not l.startswith("#")}
    names = {}
    for p in json.loads(PLUGINS.read_text(encoding="utf8")):
        if not p.get("supported", True) or p.get("experimental"):
            continue
        for n in (p.get("name"), p.get("loc", {}).get("de", {}).get("name"), p.get("dir")):
            if n:
                names[norm(n)] = p["type"]
    for folder in PLUGIN_SOURCES.iterdir():
        if not folder.is_dir() or folder.name in excluded:
            continue
        for cs in folder.rglob("*.cs"):
            if "/obj/" in str(cs):
                continue
            source = cs.read_text(encoding="utf8", errors="ignore")
            namespace = re.search(r"namespace\s+([\w.]+)", source)
            # a component is a class with a [PluginInfo] attribute, or that names ICrypComponent as its base
            declared = re.findall(r"class\s+(\w+)\s*:[^{]*ICrypComponent", source)
            declared += [m.group(1) for i in re.finditer(r"\[PluginInfo\(", source)
                         if (m := re.compile(r"class\s+(\w+)").search(source, i.end()))]
            for cls in declared:
                names.setdefault(norm(cls), (namespace.group(1) + "." if namespace else "") + cls)
    return names


def template_titles():
    """normalized German and English titles of the templates (from the .xml next to each .cwm)"""
    titles = {}
    for xml_file in TEMPLATES.rglob("*.xml"):
        if not xml_file.with_suffix(".cwm").exists():
            continue
        try:
            root = ET.parse(xml_file).getroot()
        except ET.ParseError:
            continue
        for t in root.findall("title"):
            titles[norm(t.text or "")] = str(xml_file.with_suffix(".cwm").relative_to(TEMPLATES))
    return titles


def last(path):
    return path.split(">")[-1].strip()


def main():
    REFERENCE.mkdir(exist_ok=True)
    snapshot = REFERENCE / "function-list.json"
    if len(sys.argv) > 1:
        entries = parse_page(sys.argv[1])
        snapshot.write_text(json.dumps(entries, ensure_ascii=False, indent=1) + "\n", encoding="utf8")
        print(f"{len(entries)} entries read from {sys.argv[1]}")
    entries = json.loads(snapshot.read_text(encoding="utf8"))

    components, templates = component_names(), template_titles()
    aliases = {norm(k): v for k, v in json.loads(ALIASES.read_text(encoding="utf8")).items() if not k.startswith("_")}
    covers = {k: v for k, v in json.loads(COVERS.read_text(encoding="utf8")).items() if not k.startswith("_")}
    reasons = {}

    def find(index, path):
        """what the entry's component or template resolves to: a name, "oos" or None"""
        key = norm(last(path))
        alias = aliases.get(key)
        if alias and "oos" in alias:
            reasons[path] = alias["oos"]
            return "oos"
        if alias and index is components:   # a target names a component; templates are found by their title
            return components.get(norm(alias["target"]))
        return index.get(key)

    out = []
    for e in entries:
        item = {k: e[k] for k in ("name", "kinds", "ct1", "ct2", "jct", "cto")}
        if e["ct2"]:
            comp = {c: find(components, c) for c in e["ct2Components"]}
            tmpl = {t: find(templates, t) for t in e["ct2Templates"]}
            item["components"] = {k: v for k, v in comp.items() if v and v != "oos"}
            item["templates"] = {k: v for k, v in tmpl.items() if v and v != "oos"}
            item["outOfScope"] = {k: reasons[k] for k, v in {**comp, **tmpl}.items() if v == "oos"}
            item["missing"] = [k for k, v in {**comp, **tmpl}.items() if not v]
            item["status"] = "partial" if item["missing"] else "done"
        else:
            template = covers.get(e["name"] + "|" + ",".join(e["kinds"]))
            if template and (TEMPLATES / template).exists():
                item["components"] = {}
                item["templates"] = {template: template}
                item["status"] = "done"
                item["coveredBy"] = template
            else:
                item["status"] = "open"
        out.append(item)
    (REFERENCE / "functions.json").write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n", encoding="utf8")

    count = lambda s: sum(1 for i in out if i["status"] == s)
    print(f"{len(out)} entries: done {count('done')}, partial {count('partial')}, open {count('open')}")
    for i in out:
        if i["status"] == "partial":
            print("  partial:", i["name"], "->", "; ".join(i["missing"]))
    sources = {}
    for i in out:
        if i["status"] == "open":
            key = "+".join(s for s, f in (("CT1", i["ct1"]), ("JCT", i["jct"]), ("CTO", i["cto"])) if f) or "none"
            sources[key] = sources.get(key, 0) + 1
    print("open by source:", sources)


if __name__ == "__main__":
    main()
