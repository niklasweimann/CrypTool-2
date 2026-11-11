#!/usr/bin/env python3
"""Makes the original WPF presentations of plugins part of the web build.

For every plugin folder listed in `presentations.include` this script

  * embeds each XAML file that declares a class (x:Class) as resource `xaml/<Plugin>/<path>.xaml`,
  * adds the code-behind (`<file>.xaml.cs`) to the compilation,
  * generates the other half of the partial class (`<file>.g.cs`): a field for every named element and an
    `InitializeComponent()` that loads the XAML at run time (System.Windows.Markup.XamlLoader),
  * removes the generated stand-in class for that presentation (stubs.state.json).

Output: CrypWeb.Plugins/presentations.g.props and CrypWeb.Plugins/Presentations/**/*.g.cs.
Run it after changing `presentations.include`:  python3 tools-gen-presentations.py
"""
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
PLUGINS = os.path.normpath(os.path.join(HERE, "..", "CrypPlugins"))
INCLUDE_FILE = os.path.join(HERE, "presentations.include")
OUT_DIR = os.path.join(HERE, "CrypWeb.Plugins", "Presentations")
PROPS_FILE = os.path.join(HERE, "CrypWeb.Plugins", "presentations.g.props")
# plugins with their own assembly (plugins.isolate) get their presentation in that assembly
ISOLATED_DIR = os.path.join(HERE, "CrypWeb.Plugins.Isolated")
ISOLATE_FILE = os.path.join(HERE, "plugins.isolate")
STUB_STATE = os.path.join(HERE, "stubs.state.json")
RUNTIME_DIR = os.path.join(HERE, "CrypWeb.Wpf")
# Pictures of the plugins, served to the browser. The renderer asks for them lower-cased (HtmlRenderer.ResolveImage).
RESOURCE_DIR = os.path.join(HERE, "CrypWeb.App", "wwwroot", "wpf-res")
IMAGE_EXTENSIONS = (".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".ico")

XAML_NS = "http://schemas.microsoft.com/winfx/2006/xaml"
PRESENTATION_NS_MARK = "/xaml/presentation"

# Elements below these are instantiated later (templates) or are not objects with fields (styles).
NO_FIELDS_BELOW = {"DataTemplate", "ControlTemplate", "ItemsPanelTemplate", "HierarchicalDataTemplate", "Style"}

USINGS = ["System.Windows", "System.Windows.Controls", "System.Windows.Controls.Primitives", "System.Windows.Shapes",
          "System.Windows.Media", "System.Windows.Media.Animation", "System.Windows.Media.Imaging",
          "System.Windows.Documents", "System.Windows.Data"]


def read_include_list():
    if not os.path.exists(INCLUDE_FILE):
        return []
    with open(INCLUDE_FILE) as f:
        return [line.strip() for line in f if line.strip() and not line.startswith("#")]


def runtime_type_names():
    """Maps the simple name of every type the WPF runtime implements to its full name."""
    names = {}
    for path in glob.glob(os.path.join(RUNTIME_DIR, "**", "*.cs"), recursive=True):
        if os.sep + "obj" + os.sep in path or os.sep + "bin" + os.sep in path:
            continue
        namespace = None
        with open(path, encoding="utf-8") as f:
            for line in f:
                declared = re.match(r"\s*namespace\s+([\w.]+)", line)
                if declared:
                    namespace = declared.group(1)
                for name in re.findall(r"\b(?:class|struct|enum|interface)\s+(\w+)", line):
                    if namespace:
                        names.setdefault(name, "global::%s.%s" % (namespace, name))
    return names


def split_tag(tag):
    """'{namespace}Local' -> (namespace, local)"""
    if tag.startswith("{"):
        namespace, local = tag[1:].split("}", 1)
        return namespace, local
    return "", tag


def field_type(tag, known_types):
    """C# type of the field for a named element, or None if the element type is unknown to the web build."""
    namespace, local = split_tag(tag)
    if namespace.startswith("clr-namespace:"):
        clr_namespace = namespace[len("clr-namespace:"):].split(";")[0]
        return "global::" + clr_namespace + "." + local
    if PRESENTATION_NS_MARK in namespace:
        return known_types.get(local)
    return None


def named_elements(element, known_types, result, unknown):
    """Collects (name, C# type) of all named elements that become fields."""
    _, local = split_tag(element.tag)
    if local in NO_FIELDS_BELOW:
        return
    name = element.get("{%s}Name" % XAML_NS) or element.get("Name")
    if name and "." not in local:
        cs_type = field_type(element.tag, known_types)
        if cs_type is None:
            unknown.add(local)
            cs_type = "object"
        result.setdefault(name, cs_type)
    for child in element:
        named_elements(child, known_types, result, unknown)


def generate_partial_class(xaml_path, plugin, relative_path, known_types):
    """Returns (namespace, class name, C# source) for a XAML file with x:Class, or None."""
    root = ET.parse(xaml_path).getroot()
    full_class = root.get("{%s}Class" % XAML_NS)
    if not full_class:
        return None
    namespace, _, class_name = full_class.rpartition(".")

    fields, unknown = {}, set()
    for child in root:
        named_elements(child, known_types, fields, unknown)
    root_name = root.get("{%s}Name" % XAML_NS) or root.get("Name")
    fields.pop(root_name, None)     # the root is the object itself

    lines = ["// <auto-generated> by tools-gen-presentations.py from %s/%s - do not edit </auto-generated>" % (plugin, relative_path),
             "#pragma warning disable"]
    lines += ["using %s;" % u for u in USINGS]
    base_type = field_type(root.tag, known_types)
    declaration = "    public partial class %s" % class_name + (" : %s" % base_type if base_type else "")
    lines += ["", "namespace %s" % namespace, "{", declaration, "    {"]
    lines += ["        internal %s %s;" % (cs_type, name) for name, cs_type in sorted(fields.items())]
    lines += ["",
              "        private bool _contentLoaded;",
              "",
              "        /// <summary>Builds the element tree declared in the XAML file and assigns the fields above.</summary>",
              "        public void InitializeComponent()",
              "        {",
              "            if (_contentLoaded) return;",
              "            _contentLoaded = true;",
              '            System.Windows.Markup.XamlLoader.LoadComponent(this, "%s/%s");' % (plugin, relative_path),
              "        }", "    }", "}", ""]
    if unknown:
        print("  %s: element types not implemented yet: %s" % (relative_path, ", ".join(sorted(unknown))))
    return namespace, class_name, "\n".join(lines)


def copy_images(plugin):
    """Copies the pictures of a plugin to wwwroot/wpf-res/<plugin>/ with lower-case paths. Returns the count."""
    plugin_dir = os.path.join(PLUGINS, plugin)
    count = 0
    for folder, subfolders, files in os.walk(plugin_dir):
        subfolders[:] = [d for d in subfolders if d not in ("obj", "bin", "DetailedDescription")]
        for name in files:
            if name.lower().endswith(IMAGE_EXTENSIONS):
                relative = os.path.relpath(os.path.join(folder, name), plugin_dir).replace(os.sep, "/").lower()
                target = os.path.join(RESOURCE_DIR, plugin.lower(), relative)
                os.makedirs(os.path.dirname(target), exist_ok=True)
                shutil.copy(os.path.join(folder, name), target)
                count += 1
    return count


def assembly_name(plugin):
    """Assembly name of the desktop plugin (its WPF resources are called '<AssemblyName>.g')."""
    for project in glob.glob(os.path.join(PLUGINS, plugin, "*.csproj")):
        with open(project, encoding="utf-8-sig", errors="ignore") as f:
            declared = re.search(r"<AssemblyName>([^<]+)</AssemblyName>", f.read())
        return declared.group(1) if declared else os.path.splitext(os.path.basename(project))[0]
    return plugin


def collect_wpf_resources(plugin, resource_list, out_dir):
    """
    Plans '<Assembly>.g.resources' for a plugin: every picture as a stream resource under its lower-case path - what
    WPF builds from the <Resource> items and code-behind reads with a ResourceManager. Appends the entries to
    resource_list ("output|name|source") and returns the MSBuild item, or None when the plugin has no pictures.
    """
    plugin_dir = os.path.join(PLUGINS, plugin)
    name = assembly_name(plugin)
    output = os.path.join(out_dir, plugin, name + ".g.resources")
    found = False
    for folder, subfolders, files in os.walk(plugin_dir):
        subfolders[:] = [d for d in subfolders if d not in ("obj", "bin", "DetailedDescription")]
        for file_name in sorted(files):
            if file_name.lower().endswith(IMAGE_EXTENSIONS):
                path = os.path.join(folder, file_name)
                key = os.path.relpath(path, plugin_dir).replace(os.sep, "/").lower()
                resource_list.append("%s|%s|%s" % (output, key, path))
                found = True
    if not found:
        return None
    return ('    <EmbeddedResource Include="Presentations/%s/%s.g.resources" LogicalName="%s.g.resources" Type="Non-Resx" WithCulture="false" />'
            % (plugin, name, name))


def write_wpf_resources(resource_list):
    """Runs the WpfResources tool, which writes all planned .resources files."""
    if not resource_list:
        return
    os.makedirs(OUT_DIR, exist_ok=True)
    list_file = os.path.join(OUT_DIR, "wpf-resources.txt")
    with open(list_file, "w", encoding="utf-8") as f:
        f.write("\n".join(resource_list) + "\n")
    subprocess.run(["dotnet", "run", "--project", os.path.join(HERE, "tools", "WpfResources"), "--", list_file], check=True)
    os.remove(list_file)


def read_isolated():
    if not os.path.exists(ISOLATE_FILE):
        return set()
    with open(ISOLATE_FILE) as f:
        return {line.split()[0] for line in f if line.strip() and not line.startswith("#")}


def write_props(path, items):
    with open(path, "w", encoding="utf-8") as f:
        f.write("<Project>\n  <!-- generated by tools-gen-presentations.py: presentations compiled from their original XAML and code-behind -->\n"
                "  <ItemGroup>\n%s\n  </ItemGroup>\n</Project>\n" % "\n".join(items))


def main():
    plugins = read_include_list()
    isolated = read_isolated()
    known_types = runtime_type_names()
    shutil.rmtree(OUT_DIR, ignore_errors=True)
    shutil.rmtree(RESOURCE_DIR, ignore_errors=True)

    items = []                 # items of the shared plugin assembly
    isolated_items = {}        # plugin -> items of its own assembly
    resource_list = []
    real_classes = set()
    for plugin in plugins:
        plugin_dir = os.path.join(PLUGINS, plugin)
        if not os.path.isdir(plugin_dir):
            sys.exit("presentations.include: no such plugin folder: " + plugin)
        print("%s (%d pictures)" % (plugin, copy_images(plugin)))
        own_project = plugin in isolated
        project_dir = os.path.join(ISOLATED_DIR, plugin) if own_project else os.path.join(HERE, "CrypWeb.Plugins")
        out_dir = os.path.join(project_dir, "Presentations")
        plugin_items = isolated_items.setdefault(plugin, []) if own_project else items
        resources = collect_wpf_resources(plugin, resource_list, out_dir)
        if resources:
            plugin_items.append(resources)
        for xaml_path in sorted(glob.glob(os.path.join(plugin_dir, "**", "*.xaml"), recursive=True)):
            if os.sep + "obj" + os.sep in xaml_path:
                continue
            relative_path = os.path.relpath(xaml_path, plugin_dir).replace(os.sep, "/")
            source_base = os.path.relpath(xaml_path, project_dir).replace(os.sep, "/")
            generated = generate_partial_class(xaml_path, plugin, relative_path, known_types)
            if generated is None:
                # XAML without a class (a resource dictionary that others merge with Source="..."): embed it only
                plugin_items.append('    <EmbeddedResource Include="%s" LogicalName="xaml/%s/%s" Link="Presentations/%s/%s" />'
                                    % (source_base, plugin, relative_path, plugin, relative_path))
                continue
            namespace, class_name, source = generated
            real_classes.add((namespace, class_name))

            target = os.path.join(out_dir, plugin, relative_path[:-len(".xaml")] + ".g.cs")
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with open(target, "w", encoding="utf-8") as f:
                f.write(source)

            plugin_items.append('    <EmbeddedResource Include="%s" LogicalName="xaml/%s/%s" Link="Presentations/%s/%s" />'
                                % (source_base, plugin, relative_path, plugin, relative_path))
            if os.path.exists(xaml_path + ".cs"):
                plugin_items.append('    <Compile Include="%s.cs" Link="Presentations/%s/%s.cs" />' % (source_base, plugin, relative_path))
            plugin_items.append('    <Compile Include="Presentations/%s/%s.g.cs" />' % (plugin, relative_path[:-len(".xaml")]))

    write_wpf_resources(resource_list)
    write_props(PROPS_FILE, items)
    for plugin, plugin_items in isolated_items.items():
        write_props(os.path.join(ISOLATED_DIR, plugin, "presentations.g.props"), plugin_items)

    # the generated stand-ins of these classes would now be duplicates
    if os.path.exists(STUB_STATE):
        with open(STUB_STATE) as f:
            state = json.load(f)
        # A stand-in with the same simple name in another namespace (the plugin's own) would shadow the real
        # class that the plugin imports with a using directive, so those go as well.
        real_names = {name for _, name in real_classes}
        removed = [key for key in state if tuple(key.split("|")) in real_classes
                   or (key.split("|")[1] in real_names and not key.startswith("System."))]
        for key in removed:
            del state[key]
        with open(STUB_STATE, "w") as f:
            json.dump(state, f)
        subprocess.run([sys.executable, os.path.join(HERE, "tools-prune-plugins.py"), "--write-stubs"], check=True)
        print("removed %d stand-in classes" % len(removed))
    print("%d presentation classes from %d plugins" % (len(real_classes), len(plugins)))


if __name__ == "__main__":
    main()
