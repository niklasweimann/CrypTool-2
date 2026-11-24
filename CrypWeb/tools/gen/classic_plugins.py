#!/usr/bin/env python3
"""Writes one component for each cipher of classic_specs.PLUGINS into CrypPlugins/<Folder>: component, settings, resources (English, German),
documentation, project file and icon, and adds the projects to "CrypTool 2.sln". The algorithms are in LibSource/ClassicCipherLib.

  python3 tools/gen/classic_plugins.py

The plugins are ordinary CrypTool 2 plugins (they build on the desktop too). The documentation text of a component is the description of its template
(tools/gen/aca_templates.py); the list of inputs and settings is made from the specification. Afterwards run tools/gen/plugins.py.
"""
import importlib
import re
import shutil
import uuid
from pathlib import Path
from xml.sax.saxutils import escape

from classic_specs import PLUGINS

ROOT = Path(__file__).resolve().parents[3]
PLUGINS_DIR = ROOT / "CrypPlugins"
SOURCE = PLUGINS_DIR / "ClassicVariants"        # the files whose form is copied
LIB_GUID = "{7C2A91E4-5B3D-4F6A-8E10-3D9B7A4C2F55}"
OLD_FOLDER = "AcaClassic"                       # the first, combined component (removed)
templates = importlib.import_module("aca_templates")
import classic_docs as docs


def guid(name):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, "https://cryptool.org/classic-ciphers/" + name)).upper()


def list_prefix(setting):
    return "RouteList" if setting["name"] in ("WriteRoute", "ReadRoute") else setting["name"] + "List"


# ---------------------------------------------------------------------------------------------------------------- resources
def strings(spec, description, lang):
    de = lang == 1
    items = {
        "PluginCaption": spec["name"][lang],
        "PluginTooltip": description[lang],
        "OutputTextCaption": "Ausgabe" if de else "Output",
        "OutputTextTooltip": "Der ver- oder entschlüsselte Text" if de else "The encrypted or decrypted text",
    }
    for port in spec["inputs"]:
        items[port["name"] + "Caption"] = port["cap"][lang]
        items[port["name"] + "Tooltip"] = port["tip"][lang]
    for setting in spec["settings"]:
        items[setting["name"] + "Caption"] = setting["cap"][lang]
        items[setting["name"] + "Tooltip"] = setting["tip"][lang]
        if setting["kind"] == "combo":
            for number, item in enumerate(setting["items"], 1):
                items[f"{list_prefix(setting)}{number}"] = item[lang]
    return items


def write_resx(folder, spec, description):
    for lang, name in ((0, "Resources.resx"), (1, "Resources.de.resx")):
        template = (SOURCE / "Properties" / name).read_text(encoding="utf-8-sig")
        head = template[:template.index('  <data name="ActionCaption"')]
        body = "".join('  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n' % (k, escape(v)) for k, v in sorted(strings(spec, description, lang).items()))
        (PLUGINS_DIR / folder / "Properties" / name).write_text(head + body + "</root>", encoding="utf-8-sig")
    designer = (SOURCE / "Properties/Resources.Designer.cs").read_text(encoding="utf-8-sig")
    head = designer[:designer.index("        /// <summary>\n        ///   Looks up a localized string")].replace("ClassicVariants", folder)
    props = "".join('''        /// <summary>
        ///   Looks up a localized string, similar to %s.
        /// </summary>
        internal static string %s {
            get {
                return ResourceManager.GetString("%s", resourceCulture);
            }
        }
        
''' % (k, k, k) for k in sorted(strings(spec, description, 0)))
    (PLUGINS_DIR / folder / "Properties/Resources.Designer.cs").write_text(head + props.rstrip() + "\n    }\n}\n", encoding="utf-8-sig")
    (PLUGINS_DIR / folder / "Properties/Resources.de.Designer.cs").write_text("", encoding="utf-8")


# ---------------------------------------------------------------------------------------------------------------- code
def settings_code(folder, spec):
    lines, order = [], 0
    fields, properties = [], []
    for setting in spec["settings"]:
        order += 1
        name, kind = setting["name"], setting["kind"]
        field = "_" + name[0].lower() + name[1:]
        if kind == "combo":
            fields.append(f"        private int {field}{' = %d' % setting['default'] if setting.get('default') else ''};")
            items = ", ".join('"%s%d"' % (list_prefix(setting), n) for n in range(1, len(setting["items"]) + 1))
            attribute = f'[TaskPane("{name}Caption", "{name}Tooltip", null, {order}, false, ControlType.ComboBox, new string[] {{ {items} }})]'
            type_ = "int"
        elif kind == "int":
            fields.append(f"        private int {field} = {setting['default']};")
            attribute = f'[TaskPane("{name}Caption", "{name}Tooltip", null, {order}, false, ControlType.NumericUpDown, ValidationType.RangeInteger, {setting["min"]}, {setting["max"]})]'
            type_ = "int"
        elif kind == "bool":
            fields.append(f"        private bool {field} = {'true' if setting['default'] else 'false'};")
            attribute = f'[TaskPane("{name}Caption", "{name}Tooltip", null, {order}, false, ControlType.CheckBox)]'
            type_ = "bool"
        else:
            fields.append(f'        private string {field} = "{setting["default"]}";')
            attribute = f'[TaskPane("{name}Caption", "{name}Tooltip", null, {order}, false, ControlType.TextBox, null)]'
            type_ = "string"
        properties.append(f'''        {attribute}
        public {type_} {name}
        {{
            get => {field};
            set {{ if (value != {field}) {{ {field} = value; OnPropertyChanged("{name}"); }} }}
        }}
''')
        if name == "Under":
            properties.append('''        /// <summary>The first letter of the setting, A if it is empty.</summary>
        public char UnderLetter => string.IsNullOrEmpty(_under) ? 'A' : _under[0];
''')
    return f'''using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System.ComponentModel;

namespace {folder}
{{
    public class {folder}Settings : ISettings
    {{
{chr(10).join(fields)}

{chr(10).join(properties)}
        public event TaskPaneAttributeChangedHandler TaskPaneAttributeChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Initialize()
        {{
        }}

        protected void OnPropertyChanged(string name)
        {{
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }}
    }}
}}
'''


def component_code(folder, spec, description):
    inputs = [p for p in spec["inputs"]]
    ports = "".join(f'''        [PropertyInfo(Direction.InputData, "{p["name"]}Caption", "{p["name"]}Tooltip", {"true" if p["mandatory"] else "false"})]
        public string {p["name"]} {{ get; set; }}

''' for p in inputs)
    names = " = ".join([p["name"] for p in inputs] + ["OutputText"])
    decrypt = "            bool decrypt = _settings.Action == 1;\n" if any(s["name"] == "Action" for s in spec["settings"]) else ""
    call = spec["call"]
    trace = spec["trace"]
    if not decrypt:
        call = call.replace("decrypt", "false")
        trace = trace.replace("decrypt", "false")
    summary = escape(description[0])
    decrypt_name = "decrypt" if decrypt else "false"
    L = "ClassicCiphers."
    return f'''using ClassicCiphers;
using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace {folder}
{{
    /// <summary>{summary} The algorithm and the picture are in LibSource/ClassicCipherLib.</summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("{folder}.Properties.Resources", "PluginCaption", "PluginTooltip", "{folder}/DetailedDescription/doc.xml")]
    [ComponentCategory(ComponentCategory.CiphersClassic)]
    public class {folder} : ICrypComponent
    {{
        private {folder}Settings _settings = new {folder}Settings();

{ports}        [PropertyInfo(Direction.OutputData, "OutputTextCaption", "OutputTextTooltip", false)]
        public string OutputText {{ get; set; }}

        public ISettings Settings
        {{
            get => _settings;
            set => _settings = ({folder}Settings)value;
        }}

        private ClassicCipherView _view;

        /// <summary>The picture of the calculation: the key tables and the steps (made by the library, shown by <see cref="ClassicCipherView"/>).</summary>
        public UserControl Presentation => _view ?? (_view = new ClassicCipherView());

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {{
            if (InputText == null)
            {{
                GuiLogMessage("There is no input text.", NotificationLevel.Error);
                return;
            }}
{decrypt}            string title = new System.Resources.ResourceManager("{folder}.Properties.Resources", typeof({folder}).Assembly).GetString("PluginCaption");
            string lang = TraceBuilder.Language();
            try
            {{
                OutputText = {call};
                OnPropertyChanged("OutputText");
            }}
            catch (ArgumentException ex)
            {{
                GuiLogMessage(ex.Message, NotificationLevel.Error);
                _view?.ShowMessage(title, ex.Message);
                return;
            }}
            try
            {{
                ((ClassicCipherView)Presentation).Show({trace});
            }}
            catch (Exception)
            {{
                ((ClassicCipherView)Presentation).Show({L}TraceBuilder.Generic(title, InputText, OutputText, {decrypt_name}, lang));
            }}
        }}

        public void Stop()
        {{
        }}

        public void Initialize()
        {{
        }}

        public void PreExecution()
        {{
            Dispose();
        }}

        public void PostExecution()
        {{
            Dispose();
        }}

        public void Dispose()
        {{
            {names} = null;
        }}

        private void OnPropertyChanged(string name)
        {{
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }}

        private void GuiLogMessage(string message, NotificationLevel level)
        {{
            EventsHelper.GuiLogMessage(OnGuiLogNotificationOccured, this, message, level);
        }}
    }}
}}
'''


def markup(text):
    """Text of the documentation: special characters escaped, lines separated by <newline />, {\\b ...} (the note of a template) as bold."""
    import re
    text = escape(text.replace("{\\\\b ", "{\\b "))
    text = re.sub(r"\{\\b ([^}]*)\}", r"<b>\1</b>", text)
    return "<newline />\n    ".join(line for line in text.split("\n"))


def doc_xml(number, spec, description, memo, text):
    """The documentation: background and procedure, the inputs, the settings, an example (the template) and the presentation."""
    def inputs(lang):
        return "<newline />\n    ".join("<b>%s</b>: %s" % (escape(p["cap"][lang]), escape(p["tip"][lang])) for p in spec["inputs"])

    def settings(lang):
        return "<newline />\n    ".join("<b>%s</b>: %s" % (escape(x["cap"][lang]), escape(x["tip"][lang])) for x in spec["settings"] if x["name"] != "Action")

    def presentation(lang):
        picture = number in templates.TRACES
        if lang == 0:
            return ("The presentation shows the key tables made from the keys (squares, alphabets, the box or grid), with the places that the text uses marked in yellow, and the steps letter by letter with the results in green."
                    if picture else "The presentation shows the plain text and the result letter by letter.")
        return ("Die Präsentation zeigt die aus den Schlüsseln gebildeten Tabellen (Quadrate, Alphabete, den Kasten oder das Raster), in denen die vom Text benutzten Stellen gelb markiert sind, und die Schritte Buchstabe für Buchstabe mit den Ergebnissen in Grün."
                if picture else "Die Präsentation zeigt den Klartext und das Ergebnis Buchstabe für Buchstabe.")

    sections = []
    for lang, code in ((0, "en"), (1, "de-DE")):
        de = lang == 1
        usage = ("<section headline=\"%s\">\n    %s\n    </section>" % ("Eingänge" if de else "Inputs", inputs(lang)))
        if settings(lang):
            usage += "<section headline=\"%s\">\n    %s\n    </section>" % ("Einstellungen" if de else "Settings", settings(lang))
        usage += "<section headline=\"%s\">\n    %s\n    </section>" % ("Beispiel" if de else "Example", markup(memo[lang]))
        sections.append((code, markup(text[lang]), usage, escape(presentation(lang))))
    return '''<?xml version="1.0" encoding="utf-8"?>
<documentation>
  <language culture="en" />
  <language culture="de-DE" />
  <introduction lang="en">
    %s
  </introduction>
  <introduction lang="de-DE">
    %s
  </introduction>
  <usage lang="en">
    %s
  </usage>
  <usage lang="de-DE">
    %s
  </usage>
  <presentation lang="en">
    %s
  </presentation>
  <presentation lang="de-DE">
    %s
  </presentation>
</documentation>
''' % (sections[0][1], sections[1][1], sections[0][2], sections[1][2], sections[0][3], sections[1][3])


def write_project(folder):
    text = (SOURCE / "ClassicVariants.csproj").read_text(encoding="utf-8-sig")
    text = text.replace("ClassicVariants", folder).replace("{09B99D7F-2791-4719-948E-68701D85897C}", "{" + guid(folder) + "}")
    text = text.replace(f'    <Compile Include="{folder}.cs" />\n    <Compile Include="{folder}Settings.cs" />\n', f'    <Compile Include="{folder}.cs" />\n    <Compile Include="{folder}Settings.cs" />\n')
    reference = f'''    <ProjectReference Include="..\\..\\LibSource\\ClassicCipherLib\\ClassicCipherLib.csproj">
      <Project>{LIB_GUID}</Project>
      <Name>ClassicCipherLib</Name>
      <Private>False</Private>
    </ProjectReference>
'''
    icon_group = '  <ItemGroup>\n    <Resource Include="icon.png" />\n  </ItemGroup>\n'
    assert icon_group in text
    text = text.replace(icon_group, "", 1)   # no icon of their own: tools/gen/plugins.py makes one for the web edition, the desktop uses the default
    marker = '  <ItemGroup>\n    <EmbeddedResource Include="Properties\\Resources.de.resx">'
    assert marker in text
    text = text.replace(marker, "  <ItemGroup>\n" + reference.replace("    <ProjectReference", "    <ProjectReference") + "  </ItemGroup>\n" + marker, 1)
    (PLUGINS_DIR / folder / f"{folder}.csproj").write_text(text, encoding="utf-8-sig")
    info = (SOURCE / "Properties/AssemblyInfo.cs").read_text(encoding="utf-8-sig").replace("ClassicVariants", folder).replace("701fee78-cff9-47e4-90a7-2656fac8c064", guid(folder + "/assembly").lower())
    (PLUGINS_DIR / folder / "Properties/AssemblyInfo.cs").write_text(info, encoding="utf-8-sig")


# ---------------------------------------------------------------------------------------------------------------- solution
def update_solution(folders):
    path = ROOT / "CrypTool 2.sln"
    raw = path.read_text(encoding="utf-8", newline="")
    nl = "\r\n" if "\r\n" in raw else "\n"
    lines = raw.split(nl)
    guids = {OLD_FOLDER: "{3E1C6A52-7B1D-4C0F-9A31-5D8E24F1A6B7}", "GrilleCipher": "{" + guid("GrilleCipher") + "}"}   # components that are gone
    wanted = {f: "{" + guid(f) + "}" for f in folders}
    wanted["ClassicCipherLib"] = LIB_GUID
    # drop every line of the old component and of the entries that are written again
    drop = set(guids.values()) | set(wanted.values())
    out, skip_end = [], False
    for line in lines:
        if skip_end:
            skip_end = line != "EndProject"
            continue
        if line.startswith("Project(") and any(g in line for g in drop):
            skip_end = True
            continue
        if any(g in line for g in drop) and line.strip().startswith("{"):
            continue
        out.append(line)
    result = []
    for line in out:
        result.append(line)
        if line.startswith('Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassicVariants"'):
            result.append("EndProject")
            for folder in list(folders) + ["ClassicCipherLib"]:
                sub = "LibSource" if folder == "ClassicCipherLib" else "CrypPlugins"
                result.append(f'Project("{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}") = "{folder}", "{sub}\\{folder}\\{folder}.csproj", "{wanted[folder]}"')
                result.append("EndProject")
            # the line "EndProject" that belongs to ClassicVariants was written above; the original one follows and is dropped
            result.append("__SKIP_ONE_ENDPROJECT__")
    final, skip = [], False
    for line in result:
        if line == "__SKIP_ONE_ENDPROJECT__":
            skip = True
            continue
        if skip and line == "EndProject":
            skip = False
            continue
        final.append(line)
    text = nl.join(final)
    # configurations and nesting (the Debug|x64 and Release|x64 lines of ClassicVariants are the model)
    old = "{09B99D7F-2791-4719-948E-68701D85897C}"
    new_config, new_nested = [], []
    for folder in list(folders) + ["ClassicCipherLib"]:
        g = wanted[folder]
        for configuration in ("Debug", "Release"):
            new_config.append(f"\t\t{g}.{configuration}|x64.ActiveCfg = {configuration}|x64")
            new_config.append(f"\t\t{g}.{configuration}|x64.Build.0 = {configuration}|x64")
        new_nested.append(f"\t\t{g} = {{88B851F6-9E3D-484F-A317-ECED5B98C618}}")
    anchor = f"\t\t{old}.Release|x64.Build.0 = Release|x64"
    text = text.replace(anchor, anchor + nl + nl.join(new_config), 1)
    nested_anchor = f"\t\t{old} = {{88B851F6-9E3D-484F-A317-ECED5B98C618}}"
    text = text.replace(nested_anchor, nested_anchor + nl + nl.join(new_nested), 1)
    path.write_text(text, encoding="utf-8", newline="")


def main():
    for gone in (OLD_FOLDER, "GrilleCipher"):
        if (PLUGINS_DIR / gone).exists():
            shutil.rmtree(PLUGINS_DIR / gone)
    folders = []
    for number, spec in sorted(PLUGINS.items()):
        folder = spec["folder"]
        description = templates.RECORDS[number]["description"]
        directory = PLUGINS_DIR / folder
        (directory / "Properties").mkdir(parents=True, exist_ok=True)
        (directory / "DetailedDescription").mkdir(exist_ok=True)
        (directory / f"{folder}.cs").write_text(component_code(folder, spec, description), encoding="utf-8-sig")
        (directory / f"{folder}Settings.cs").write_text(settings_code(folder, spec), encoding="utf-8-sig")
        (directory / "DetailedDescription/doc.xml").write_text(doc_xml(number, spec, description, templates.RECORDS[number]["memo"], (docs.DOCS[number]["en"], docs.DOCS[number]["de"])), encoding="utf-8-sig")
        write_resx(folder, spec, description)
        write_project(folder)
        folders.append(folder)
    update_solution(folders)
    print(f"{len(folders)} components written")


if __name__ == "__main__":
    main()
