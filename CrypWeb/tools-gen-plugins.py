#!/usr/bin/env python3
"""Scans CrypPlugins/* and Experimental/CrypPlugins/*, then generates what the browser build needs:

  CrypWeb.App/wwwroot/plugins.json                 metadata of all components (name, ports, localized texts, icon, ...)
  CrypWeb.App/wwwroot/icons/*.png                  the plugin icons
  CrypWeb.Plugins/plugins.g.props                  compile / resource items of the shared plugin assembly
  CrypWeb.Plugins/isolated.g.props                 references to the isolated plugin projects
  CrypWeb.Plugins/libraries.g.json                 LibSource libraries that are compiled into the plugin assembly
  CrypWeb.Plugins.Isolated/<Plugin>/*              one small project per isolated plugin
  CrypWeb.Core/IsolatedAssemblies.g.cs             names of the isolated assemblies

Inputs besides the plugin sources: plugins.exclude (folders that are not built) and plugins.isolate
(folders that get their own assembly, optionally followed by "private"). See ARCHITECTURE.md.

Experimental components (Experimental/CrypPlugins, not part of the CT2 release) are marked "experimental" in
plugins.json; the app hides them until they are switched on under View. An experimental folder that also exists
in CrypPlugins is an older copy and is skipped.
"""
import glob
import json
import os
import re
import shutil
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
PLUGINS_DIR = os.path.join(ROOT, 'CrypPlugins')
EXPERIMENTAL_DIR = os.path.join(ROOT, 'Experimental', 'CrypPlugins')

# where each plugin folder lives (CrypPlugins or Experimental/CrypPlugins); filled by scan_plugins
PLUGIN_DIRS = {}
CRYPWEB = os.path.join(ROOT, 'CrypWeb')
OUT = os.path.join(CRYPWEB, 'CrypWeb.App', 'wwwroot')
PLUGIN_ASSEMBLY_DIR = os.path.join(CRYPWEB, 'CrypWeb.Plugins')
ISOLATED_DIR = os.path.join(CRYPWEB, 'CrypWeb.Plugins.Isolated')
LIB_SOURCE = os.path.join(ROOT, 'LibSource')

# ---------------------------------------------------------------------------------------------------------------------
# configuration
# ---------------------------------------------------------------------------------------------------------------------

# "private" plugins see the shared plugin assembly only under an alias, so that the namespaces of the other plugins
# (e.g. "Transposition") do not hide the plugin's own types of the same name.
PRIVATE_ALIAS = ' Aliases="plugins"'

ISOLATED_CSPROJ = '''<Project Sdk="Microsoft.NET.Sdk">
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
    <PackageReference Include="System.Resources.Extensions" Version="10.0.14" />
    <PackageReference Include="System.Configuration.ConfigurationManager" Version="10.0.14" />
    <PackageReference Include="BouncyCastle.Cryptography" Version="2.7.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
    <ProjectReference Include="..\\..\\CrypWeb.Plugins\\CrypWeb.Plugins.csproj"@ALIAS@ />
  </ItemGroup>
</Project>
'''

# Sources of a plugin that are replaced in the browser build: 'drop' lists files of the plugin folder that are left
# out, 'add' lists files (relative to the repository) that are compiled with the plugin instead.
SOURCE_SUBSTITUTES = {
    # the distributed key search (CrypCloud, VoluntLib2) is not needed in the browser; the local search stays
    'KeySearcher': {
        'drop': ['CrypCloud/CloudKeySearcher.cs', 'CrypCloud/calculation/Worker.cs', 'CrypCloud/calculation/CalculationTemplate.cs'],
        'add': ['CrypCloud/CrypCloudCore/CloudComponent/ACloudCompatible.cs', 'CrypWeb/StandIns/KeySearcher/CloudStandIns.cs'],
    },
}

# Libraries from LibSource that plugins reference (<ProjectReference ..\..\LibSource\...>) are compiled into the
# plugin assembly, with their resources. Libraries the web build covers otherwise are skipped.
LIBRARY_SKIP = {
    'CrypAnalysisViewControl',      # CrypPluginBase.Portable/Shims/CrypAnalysisViewControl.cs
    'LanguageStatisticsLib',        # LanguageStatistics.Portable
    'VoluntLib2', 'CrypCloud',      # distributed / cloud key search (not needed in the browser)
}

# Directions of [PropertyInfo] that become connectors (the others are settings or internal)
INPUT_DIRECTIONS = ('InputData', 'ControlSlave')
OUTPUT_DIRECTIONS = ('OutputData', 'ControlMaster')

PLUGIN_CLASS_PATTERN = re.compile(
    r'^[ \t]*(?:public\s+|internal\s+)?(?:sealed\s+|partial\s+|static\s+)*class\s+(\w+)\s*'
    r'(?::[^{]*\bICrypComponent\b|:[^{]*\bACloudCompatible\b)', re.M)

# [PluginInfo("resourceAssembly", "NameKey", "TooltipKey", "doc.xml", new[] {icons})] and the variant that starts with typeof(...)
PLUGIN_INFO_PATTERNS = (
    re.compile(r'PluginInfo\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*'
               r'(?:,\s*(?:new\s*(?:string)?\s*\[\s*\]\s*)?\{([^}]*)\})?'),
    re.compile(r'PluginInfo\(\s*typeof\([^)]*\)\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*'
               r'(?:,\s*(?:new\s*(?:string)?\s*\[\s*\]\s*)?\{([^}]*)\})?'),
)

PORT_PATTERN = re.compile(
    r'\[PropertyInfo\(\s*Direction\.(\w+)\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"(?:\s*,\s*(true|false))?[^\]]*\)\]\s*'
    r'(?:public\s+)(?:virtual\s+)?([\w<>\[\].?]+)\s+(\w+)')


# ---------------------------------------------------------------------------------------------------------------------
# small helpers
# ---------------------------------------------------------------------------------------------------------------------

def read_text(path):
    """Source files may start with a byte order mark and contain odd bytes; read leniently."""
    with open(path, encoding='utf-8-sig', errors='ignore') as f:
        return f.read()


def read_list(path):
    """The non-empty, non-comment lines of a list file (missing file: empty list)."""
    if not os.path.exists(path):
        return []
    with open(path) as f:
        return [line.strip() for line in f if line.strip() and not line.startswith('#')]


def walk_sources(folder):
    """os.walk without build output folders."""
    for root, dirs, files in os.walk(folder):
        dirs[:] = [d for d in dirs if d not in ('obj', 'bin')]
        yield root, files


def parse_resx(path):
    """name -> text of the string entries of a .resx file."""
    values = {}
    try:
        for entry in ET.parse(path).getroot().findall('data'):
            value = entry.find('value')
            if value is not None and entry.get('type') is None:
                values[entry.get('name')] = (value.text or '').strip()
    except Exception:
        pass  # an unreadable resource file just provides no texts
    return values


def write_props(path, lines):
    with open(path, 'w') as f:
        f.write('<Project>\n  <ItemGroup>\n' + '\n'.join(lines) + '\n  </ItemGroup>\n</Project>\n')


def item_lines(items, base_dir):
    """MSBuild item lines for (kind, path, link, logical name) tuples, with paths relative to base_dir."""
    lines = []
    for kind, path, link, logical_name in items:
        logical = 'LogicalName="%s" ' % logical_name if logical_name else ''
        lines.append('    <%s Include="%s" %sLink="%s" />' % (kind, os.path.relpath(path, base_dir), logical, link))
    return lines


def resource_manager_name(designer_path):
    """The resource base name a Resources.Designer.cs asks the ResourceManager for (or None)."""
    if not os.path.exists(designer_path):
        return None
    match = re.search(r'new\s+(?:global::)?(?:System\.Resources\.)?ResourceManager\("([^"]+)"', read_text(designer_path))
    return match.group(1) if match else None


# ---------------------------------------------------------------------------------------------------------------------
# one plugin folder
# ---------------------------------------------------------------------------------------------------------------------

def find_sources(plugin_dir, folder):
    """The .cs files that belong to the plugin (as listed by its project when that lists sources)."""
    sources = []
    for root, files in walk_sources(plugin_dir):
        for f in files:
            is_designer_of_language = re.search(r'\.[a-z]{2}(-[A-Za-z]+)?\.Designer\.cs$', f)
            if f.endswith('.cs') and not f.endswith('.xaml.cs') and f != 'AssemblyInfo.cs' and not is_designer_of_language:
                sources.append(os.path.join(root, f))

    # An old-style project lists its sources (<Compile Include="...">); files it does not list are not part of the
    # plugin (leftovers) and are left out here too.
    listed = set()
    for project in [f for f in os.listdir(plugin_dir) if f.endswith('.csproj')]:
        for include in re.findall(r'<Compile\s+Include="([^"*]+)"', read_text(os.path.join(plugin_dir, project))):
            listed.add(os.path.normpath(os.path.join(plugin_dir, include.replace('\\', os.sep))).lower())
    if listed:
        sources = [s for s in sources if os.path.normpath(s).lower() in listed]

    # Plugins whose sources depend on parts the browser does not have get stand-ins (see SOURCE_SUBSTITUTES)
    if folder in SOURCE_SUBSTITUTES:
        substitute = SOURCE_SUBSTITUTES[folder]
        dropped = set(os.path.normpath(os.path.join(plugin_dir, x)) for x in substitute.get('drop', []))
        sources = [s for s in sources if os.path.normpath(s) not in dropped]
        sources += [os.path.normpath(os.path.join(ROOT, x)) for x in substitute.get('add', [])]
    return sources


def find_resources(plugin_dir):
    """Returns (resources, localized): the Resources.resx files with their texts, and per language the
    Resources.<lang>.resx files next to them."""
    resources = {}
    localized = {}   # lang -> {path: values}
    for root, files in walk_sources(plugin_dir):
        for f in files:
            path = os.path.join(root, f)
            if f.endswith('.resx') and f.count('.') == 1:
                resources[path] = parse_resx(path)
            else:
                match = re.match(r'^(.*)\.([a-z]{2})(?:-[A-Za-z]+)?\.resx$', f)
                if match:
                    localized.setdefault(match.group(2).lower(), {})[path] = parse_resx(path)
    return resources, localized


def attribute_block(text_before_class):
    """The [attributes] directly above a class declaration (walks back over the lines)."""
    block = []
    depth = 0
    for line in reversed(text_before_class.split('\n')):
        stripped = line.strip()
        if not stripped or stripped.startswith('//') or stripped.startswith('///'):
            if depth == 0 and not block:
                continue
            if depth == 0:
                break
        block.append(line)
        depth += stripped.count(']') - stripped.count('[')
        if depth == 0 and not stripped.startswith(']') and not stripped.startswith('[') \
                and not stripped.endswith(')]') and not stripped.endswith(']'):
            block.pop()
            break
    return '\n'.join(reversed(block))


def parse_plugin_info(attributes):
    """(name key, tooltip key, icon list text) of the [PluginInfo] attribute, or None."""
    match = PLUGIN_INFO_PATTERNS[0].search(attributes) or PLUGIN_INFO_PATTERNS[1].search(attributes)
    if not match:
        return None
    groups = match.groups()
    if len(groups) == 5:
        _resource_assembly, name_key, tooltip_key, _doc, icons = groups
    else:
        name_key, tooltip_key, _doc, icons = groups
    icons = icons or ''
    if not icons:
        # icons may also follow as plain arguments: PluginInfo(..., "doc.xml", "Plugin/icon.png", ...)
        info = re.search(r'PluginInfo\((.*?)\)\s*\]', attributes, re.S)
        images = re.findall(r'"([^"]+\.(?:png|jpg|jpeg|gif|ico|bmp))"', info.group(1) if info else '', re.I)
        icons = ','.join('"%s"' % x for x in images)
    return name_key, tooltip_key, icons


def texts_for(resources, key):
    """The texts of the first resource file that has the key (merged like the original lookup)."""
    texts = {}
    for path, values in resources.items():
        if os.path.exists(path) and key in values:
            texts.update(values)
    return texts


def localized_texts(localized, name_key):
    """language -> merged texts of its resource files (only files that look like the plugin's resources)."""
    result = {}
    for lang, files in localized.items():
        merged = {}
        for values in files.values():
            if name_key in values or any(k.endswith('Caption') for k in values):
                merged.update(values)
        if merged:
            result[lang] = merged
    return result


def copy_icon(plugin_dir, icon_list, full_name):
    """Copies the first icon of the plugin to wwwroot/icons; returns its URL or None."""
    if not icon_list:
        return None
    parts = icon_list[0].split('/', 1)
    candidate = os.path.join(plugin_dir, parts[1]) if len(parts) == 2 else None
    if not (candidate and os.path.exists(candidate)):
        # not where the attribute says: look for the file name anywhere in the plugin folder
        name = os.path.basename(icon_list[0])
        candidate = None
        for root, _dirs, files in os.walk(plugin_dir):
            if name in files:
                candidate = os.path.join(root, name)
                break
    if not (candidate and os.path.exists(candidate)):
        return None
    icon = 'icons/%s.png' % full_name
    try:
        shutil.copy(candidate, os.path.join(OUT, icon))
    except Exception:
        return None
    return icon


def parse_ports(body, texts):
    """The connectors of a plugin class: name, direction, C# type, caption, tooltip, mandatory, control."""
    ports = []
    for match in PORT_PATTERN.finditer(body):
        direction, caption_key, tooltip_key, mandatory, type_name, name = match.groups()
        if direction not in INPUT_DIRECTIONS + OUTPUT_DIRECTIONS:
            continue
        ports.append(dict(
            name=name,
            dir='out' if direction in OUTPUT_DIRECTIONS else 'in',
            type=type_name,
            caption=texts.get(caption_key, caption_key),
            tooltip=texts.get(tooltip_key, tooltip_key),
            mandatory=(mandatory == 'true'),
            control=direction.startswith('Control'),
        ))
    return ports


def localize(localized_by_lang, texts, name_key, tooltip_key, ports, body):
    """Per language: the plugin name/tooltip and port captions that differ from the English ones."""
    result = {}
    for lang, lang_texts in localized_by_lang.items():
        entry = {}
        if lang_texts.get(name_key) and lang_texts.get(name_key) != texts.get(name_key):
            entry['name'] = lang_texts[name_key]
        if lang_texts.get(tooltip_key) and lang_texts.get(tooltip_key) != texts.get(tooltip_key):
            entry['tooltip'] = lang_texts[tooltip_key]

        port_texts = {}
        for port in ports:
            declaration = re.search(
                r'\[PropertyInfo\(\s*Direction\.\w+\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"[^\]]*\)\]\s*public\s+(?:virtual\s+)?'
                r'[\w<>\[\].?]+\s+' + port['name'] + r'\b', body)
            if not declaration:
                continue
            caption_key, port_tooltip_key = declaration.group(1), declaration.group(2)
            port_entry = {}
            if lang_texts.get(caption_key) and lang_texts.get(caption_key) != texts.get(caption_key):
                port_entry['caption'] = lang_texts[caption_key]
            if lang_texts.get(port_tooltip_key) and lang_texts.get(port_tooltip_key) != texts.get(port_tooltip_key):
                port_entry['tooltip'] = lang_texts[port_tooltip_key]
            if port_entry:
                port_texts[port['name']] = port_entry
        if port_texts:
            entry['ports'] = port_texts
        if entry:
            result[lang] = entry
    return result


def has_no_presentation(body):
    """A component without a visualization returns null as its Presentation (CT2 then offers no presentation view)."""
    # \b: "QuickWatchPresentation => null" (the old quick-watch view, null almost everywhere) is a different property
    if re.search(r'\bPresentation\s*(?:=>\s*null|\{\s*get\s*(?:=>\s*null|\{\s*return\s+null\s*;))', body):
        return True
    # an auto-property that is never assigned stays null too: Presentation { get; private set; }
    return bool(re.search(r'\bPresentation\s*\{\s*get\s*;\s*(?:private\s+)?set\s*;\s*\}', body)
                and not re.search(r'\bPresentation\s*=\s*(?!null\b)', body))


def extract_components(folder, plugin_dir, sources, resources, localized, excluded):
    """Metadata of every ICrypComponent class found in the sources of one plugin folder."""
    components = {}
    for source in sources:
        try:
            text = read_text(source)
        except Exception:
            continue
        for match in PLUGIN_CLASS_PATTERN.finditer(text):
            class_name = match.group(1)
            attributes = attribute_block(text[:match.start()])
            info = parse_plugin_info(attributes)
            if not info:
                continue
            name_key, tooltip_key, icons = info

            namespaces = re.findall(r'namespace\s+([\w.]+)', text[:match.start()])
            full_name = (namespaces[-1] + '.' if namespaces else '') + class_name
            category = re.search(r'ComponentCategory\(ComponentCategory\.(\w+)\)', attributes)

            texts = texts_for(resources, name_key)
            icon_list = [i.strip().strip('"') for i in icons.split(',') if i.strip()]
            body = text[match.end():]
            ports = parse_ports(body, texts)

            components[full_name] = dict(
                loc=localize(localized_texts(localized, name_key), texts, name_key, tooltip_key, ports, body),
                type=full_name,
                dir=folder,
                name=texts.get(name_key, class_name),
                tooltip=texts.get(tooltip_key, ''),
                category=category.group(1) if category else 'Undefined',
                icon=copy_icon(plugin_dir, icon_list, full_name),
                ports=ports,
                supported=folder not in excluded,
                presentation=not has_no_presentation(body),
            )
    return components


def build_items(folder, plugin_dir, sources, resources, localized):
    """(kind, path, link, logical name) for the MSBuild items of one plugin folder."""
    items = []
    for source in sources:
        inside = os.path.normpath(source).startswith(os.path.normpath(plugin_dir) + os.sep)
        link = os.path.relpath(source, plugin_dir) if inside else os.path.join('StandIns', os.path.basename(source))
        items.append(('Compile', source, folder + '/' + link.replace(os.sep, '/'), None))

    for resx_path in resources:
        base_name = resource_manager_name(resx_path[:-5] + '.Designer.cs')
        if not base_name:
            continue
        items.append(('EmbeddedResource', resx_path, folder + '/' + os.path.relpath(resx_path, plugin_dir).replace(os.sep, '/'),
                      base_name + '.resources'))
        # localized variants (Resources.<lang>.resx) are embedded the same way
        for lang, files in localized.items():
            for path in [p for p in files if p.startswith(resx_path[:-5] + '.' + lang)]:
                items.append(('EmbeddedResource', path, folder + '/' + os.path.relpath(path, plugin_dir).replace(os.sep, '/'),
                              '%s.%s.resources' % (base_name, lang)))
    return items


# ---------------------------------------------------------------------------------------------------------------------
# libraries from LibSource
# ---------------------------------------------------------------------------------------------------------------------

def library_sources(lib_dir, csproj):
    """The .cs files of a library: the ones its project lists (old-style), else all of them."""
    text = read_text(csproj)
    listed = [os.path.normpath(os.path.join(lib_dir, include.replace('\\', os.sep)))
              for include in re.findall(r'<Compile\s+Include="([^"*]+)"', text)]
    files = listed if listed else glob.glob(os.path.join(lib_dir, '**', '*.cs'), recursive=True)
    return sorted(f for f in files if os.path.exists(f) and os.sep + 'obj' + os.sep not in f
                  and os.sep + 'bin' + os.sep not in f and not f.endswith('AssemblyInfo.cs'))


def find_libraries(plugin_folders, isolated):
    """library project -> its folder, for the libraries the (non-isolated) plugins reference."""
    libraries = {}
    for folder in sorted(plugin_folders):
        if folder in isolated:
            continue  # isolated plugins would need the library in their own assembly
        plugin_dir = PLUGIN_DIRS.get(folder, os.path.join(PLUGINS_DIR, folder))
        for project in glob.glob(os.path.join(plugin_dir, '*.csproj')):
            references = re.findall(r'<ProjectReference\s+Include="([^"]*LibSource[^"]*\.csproj)"', read_text(project))
            for reference in references:
                library_project = os.path.normpath(os.path.join(plugin_dir, reference.replace('\\', os.sep)))
                name = os.path.relpath(os.path.dirname(library_project), LIB_SOURCE).split(os.sep)[0]
                if name not in LIBRARY_SKIP and os.path.exists(library_project):
                    libraries[library_project] = os.path.dirname(library_project)
    return libraries


def library_items(libraries):
    """Compile and resource items (as lines for the plugin assembly) of the libraries."""
    lines = []
    for project, lib_dir in sorted(libraries.items()):
        rel = os.path.relpath(lib_dir, LIB_SOURCE).replace(os.sep, '/')
        for f in library_sources(lib_dir, project):
            lines += item_lines([('Compile', f, 'LibSource/' + rel + '/' + os.path.relpath(f, lib_dir), None)], PLUGIN_ASSEMBLY_DIR)
        # the library's resources, under the name its Resources.Designer.cs asks for
        for resx_path in sorted(glob.glob(os.path.join(lib_dir, '**', 'Resources.resx'), recursive=True)):
            designer = resx_path[:-5] + '.Designer.cs'
            match = re.search(r'ResourceManager\("([^"]+)"', read_text(designer)) if os.path.exists(designer) else None
            if match:
                lines += item_lines([('EmbeddedResource', resx_path, 'LibSource/' + rel + '/' + os.path.relpath(resx_path, lib_dir),
                                      match.group(1) + '.resources')], PLUGIN_ASSEMBLY_DIR)
    return lines


# ---------------------------------------------------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------------------------------------------------

def plugin_folders():
    """(folder name, path, experimental) of every plugin folder: CrypPlugins first, then the experimental ones
    that CrypPlugins does not have."""
    folders = []
    for root, experimental in ((PLUGINS_DIR, False), (EXPERIMENTAL_DIR, True)):
        if not os.path.isdir(root):
            continue
        for folder in sorted(os.listdir(root)):
            path = os.path.join(root, folder)
            if os.path.isdir(path) and folder not in PLUGIN_DIRS:
                PLUGIN_DIRS[folder] = path
                folders.append((folder, path, experimental))
    return folders


def scan_plugins(excluded):
    """Returns (metadata by type name, MSBuild items by plugin folder)."""
    metadata = {}
    items_by_folder = {}
    os.makedirs(os.path.join(OUT, 'icons'), exist_ok=True)
    for folder, plugin_dir, experimental in plugin_folders():
        sources = find_sources(plugin_dir, folder)
        resources, localized = find_resources(plugin_dir)
        components = extract_components(folder, plugin_dir, sources, resources, localized, excluded)
        for component in components.values():
            component['experimental'] = experimental
        metadata.update(components)
        if folder not in excluded and sources:
            items_by_folder[folder] = build_items(folder, plugin_dir, sources, resources, localized)
    return metadata, items_by_folder


def write_isolated_projects(items_by_folder, isolated, private):
    """One project per isolated plugin. Returns (project reference lines, assembly names)."""
    # folders of plugins that are no longer isolated are removed; the others keep what tools-gen-presentations.py
    # generated there (Presentations/, presentations.g.props), so the two generators can run in any order
    os.makedirs(ISOLATED_DIR, exist_ok=True)
    for entry in os.listdir(ISOLATED_DIR):
        if entry not in isolated or entry not in items_by_folder:
            shutil.rmtree(os.path.join(ISOLATED_DIR, entry), ignore_errors=True)

    references, names = [], []
    for folder, items in items_by_folder.items():
        if folder not in isolated:
            continue
        project_dir = os.path.join(ISOLATED_DIR, folder)
        os.makedirs(project_dir, exist_ok=True)
        write_props(os.path.join(project_dir, 'plugins.g.props'), item_lines(items, project_dir))
        csproj = ISOLATED_CSPROJ.replace('@ASM@', 'CrypWeb.Plugin.' + folder).replace('@ALIAS@', PRIVATE_ALIAS if folder in private else '')
        with open(os.path.join(project_dir, 'CrypWeb.Plugin.%s.csproj' % folder), 'w') as f:
            f.write(csproj)
        references.append('    <ProjectReference Include="$(MSBuildThisFileDirectory)..\\CrypWeb.Plugins.Isolated\\%s\\CrypWeb.Plugin.%s.csproj" />'
                          % (folder, folder))
        names.append('CrypWeb.Plugin.' + folder)
    return references, names


def shared_assembly_items(items_by_folder, isolated):
    lines = []
    for folder, items in items_by_folder.items():
        if folder not in isolated:
            lines += item_lines(items, PLUGIN_ASSEMBLY_DIR)
    # KeySearcher itself is a WPF/cloud component, but the plugins that support key searching (AES, DES, RC2, RC4, ...)
    # use its pure-logic helpers
    if 'KeySearcher' not in items_by_folder:
        for sub in ('KeyPattern', 'KeyTranslators'):
            for f in sorted(glob.glob(os.path.join(PLUGINS_DIR, 'KeySearcher', sub, '*.cs'))):
                lines += item_lines([('Compile', f, 'KeySearcher/' + sub + '/' + os.path.basename(f), None)], PLUGIN_ASSEMBLY_DIR)
    return lines


def main():
    excluded = set(read_list(os.path.join(CRYPWEB, 'plugins.exclude')))
    isolate_lines = [line.split() for line in read_list(os.path.join(CRYPWEB, 'plugins.isolate'))]
    isolated = set(parts[0] for parts in isolate_lines)
    private = set(parts[0] for parts in isolate_lines if 'private' in parts[1:])

    metadata, items_by_folder = scan_plugins(excluded)

    isolated_references, isolated_names = write_isolated_projects(items_by_folder, isolated, private)
    main_lines = shared_assembly_items(items_by_folder, isolated)

    libraries = find_libraries(items_by_folder, isolated)
    main_lines += library_items(libraries)
    print('libraries:', ', '.join(os.path.basename(p)[:-7] for p in sorted(libraries)))
    # the presentations generator processes the XAML of these libraries too (name -> folder)
    os.makedirs(PLUGIN_ASSEMBLY_DIR, exist_ok=True)
    with open(os.path.join(PLUGIN_ASSEMBLY_DIR, 'libraries.g.json'), 'w') as f:
        json.dump({os.path.basename(p)[:-len('.csproj')]: d for p, d in sorted(libraries.items())}, f, indent=1)

    write_props(os.path.join(PLUGIN_ASSEMBLY_DIR, 'plugins.g.props'), main_lines)
    write_props(os.path.join(PLUGIN_ASSEMBLY_DIR, 'isolated.g.props'), isolated_references)
    with open(os.path.join(CRYPWEB, 'CrypWeb.Core', 'IsolatedAssemblies.g.cs'), 'w') as f:
        f.write('// generated by tools-gen-plugins.py\nnamespace CrypTool.Web.Core;\n'
                'public static class IsolatedAssemblies { public static readonly string[] Names = { %s }; }\n'
                % ', '.join('"%s"' % n for n in isolated_names))
    with open(os.path.join(OUT, 'plugins.json'), 'w') as f:
        json.dump(list(metadata.values()), f)

    print(len(metadata), 'components;', sum(1 for m in metadata.values() if m['supported']), 'enabled for build;',
          sum(1 for m in metadata.values() if m['icon']), 'icons;',
          sum(1 for m in metadata.values() if m['experimental'] and m['supported']), 'experimental enabled')


if __name__ == '__main__':
    main()
