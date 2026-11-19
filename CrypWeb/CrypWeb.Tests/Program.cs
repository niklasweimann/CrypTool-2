// Command line checks for CrypWeb.
//
//   dotnet run --project CrypWeb.Tests -- <template folder> [command] [arguments]
//
// Without a command the self test runs (editor operations, settings, .cwm round trip, AES, a few templates).
//
//   runall              run every template and render every presentation (the main regression check, takes minutes)
//   roundtrip           read -> write -> compare every template
//   snapall             write every presentation to CrypWeb.App/wwwroot/_snap/ (overview: _snap/index.html)
//   snap <tpl> [args]   write the presentations of one template; args: Element.Property=value | size:id=WxH | event:id:kind:v:x:y
//   probe <tpl>         run one template and print the last value on every connector (CT2_TRACE=1 prints exceptions)
//   settings <tpl>      print the stored settings of a template's components
//   runtype <type>      run every template containing the component type, with timings
//   palette <out.json>  write the palette entries found in the templates
//   derive <base.cwm> <out.cwm> <edits.json>   copy a template and change component settings (tools-gen-derived-templates.py)
//   kat                 known-answer tests of the components added for the function list (ComponentTests.cs)
//   functions [file]  coverage of the CrypTool function list (reference/functions.json, made by tools-gen-functions.py)
//   culture | bindtest | triggertest | present3d     checks of single features
using CrypWeb.Tests;

var templateFolder = args.Length > 0 ? args[0] : "../Templates";
// template data files (pictures, examples) are read from the template folder
CrypTool.Web.Core.TemplateFiles.Root = Path.GetFullPath(templateFolder);

var command = args.Length > 1 ? args[1] : "";
switch (command)
{
    case "culture":
        DebugCommands.Culture(templateFolder, args);
        break;
    case "roundtrip":
        RoundTripCommand.Run(templateFolder, args);
        break;
    case "runall":
        await RunAllCommand.Run(templateFolder, args);
        break;
    case "bindtest":
        DebugCommands.BindTest(templateFolder, args);
        break;
    case "triggertest":
        DebugCommands.TriggerTest(templateFolder, args);
        break;
    case "present3d":
        await DebugCommands.Present3D(templateFolder, args);
        break;
    case "probe" when args.Length > 2:
        await DebugCommands.Probe(templateFolder, args);
        break;
    case "settings" when args.Length > 2:
        DebugCommands.Settings(templateFolder, args);
        break;
    case "snapall":
        await SnapCommands.SnapAll(templateFolder, args);
        break;
    case "snap" when args.Length > 2:
        await SnapCommands.Snap(templateFolder, args);
        break;
    case "runtype" when args.Length > 2:
        await DebugCommands.RunType(templateFolder, args);
        break;
    case "derive" when args.Length > 4:
        Environment.ExitCode = DeriveCommand.Run(args) ? 0 : 1;
        break;
    case "api" when args.Length > 2:   // lists the public constructors and methods of BouncyCastle types whose name contains args[2]
        foreach (var type in typeof(Org.BouncyCastle.Crypto.IDigest).Assembly.GetTypes().Where(t => t.IsPublic && t.Name.Contains(args[2], StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(type.FullName);
            foreach (var f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) Console.WriteLine("   field " + f.FieldType.Name + " " + f.Name);
            foreach (var c in type.GetConstructors()) Console.WriteLine("   new(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
            foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
                Console.WriteLine("   " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
        }
        break;
    case "kat":
        Environment.ExitCode = ComponentTests.Run() ? 0 : 1;
        break;
    case "functions":
        Environment.ExitCode = FunctionsCommand.Run(templateFolder, args) ? 0 : 1;
        break;
    case "palette":
        DebugCommands.Palette(templateFolder, args[2]);
        break;
    default:
        await SelfTest.Run(templateFolder);
        break;
}
