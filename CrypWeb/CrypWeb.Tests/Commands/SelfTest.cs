using System.Text.Json;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>The default command: parses all templates, then checks editor operations, settings schema, .cwm round trip, AES and runs a few templates.</summary>
public static class SelfTest
{
    public static async Task Run(string root)
    {
        int ok = 0, fail = 0;
        foreach (var f in Directory.EnumerateFiles(root, "*.cwm", SearchOption.AllDirectories))
        {
            try
            {
                CwmReader.Read(File.ReadAllBytes(f));
                ok++;
            }
            catch (Exception e)
            {
                fail++;
                Console.WriteLine($"FAIL {f}: {e.Message}");
            }
        }
        Console.WriteLine($"parsed ok={ok} fail={fail}");
        // editor smoke test
        var ed = new WorkspaceEditor();
        ed.Load(CwmReader.Read(File.ReadAllBytes(Path.Combine(root, "Cryptography/Classic/AffineCipher.cwm"))));
        int n0 = ed.Ws.Plugins.Count, w0 = ed.Ws.Wires.Count;
        ed.Checkpoint();
        ed.Selection.Add(ed.Ws.Plugins[2].Id);
        ed.DeleteSelection();
        Check(ed.Ws.Plugins.Count == n0 - 1 && ed.Ws.Wires.Count < w0, "delete removes plugin and wires");
        ed.Undo();
        Check(ed.Ws.Plugins.Count == n0 && ed.Ws.Wires.Count == w0, "undo restores");
        ed.Redo();
        Check(ed.Ws.Plugins.Count == n0 - 1, "redo");
        ed.Undo();
        ed.SelectAll();
        ed.Copy();
        ed.Paste();
        Check(ed.Ws.Plugins.Count == 2 * n0 && ed.Ws.Wires.Count == 2 * w0,
            "copy/paste duplicates wires");
        var ws2 = WorkspaceEditor.FromJson(ed.ToJson());
        Check(ws2.Plugins.Count == 2 * n0 && ws2.Wires.Count == 2 * w0, "json roundtrip");
        {
            var e2 = new WorkspaceEditor();
            e2.Load(new Workspace());
            var dict = e2.AddPlugin(new PaletteEntry { TypeName = "d",
                Outputs = { new PalettePort("OutputList", "System.String[]", "", false) } },
                0,
                0);
            var t9 = e2.AddPlugin(new PaletteEntry { TypeName = "t",
                Inputs = { new PalettePort("DictionaryContent", "System.Array", "", false),
                new PalettePort("S", "System.Int32", "", false) } },
                300,
                0);
            Check(e2.CanConnect(dict.Outputs[0], t9.Inputs[0]), "string[] -> Array (T9 dictionary) connects");
            Check(!e2.CanConnect(dict.Outputs[0], t9.Inputs[1]), "string[] -> Int32 is rejected");
        }
        foreach (var tn in new[] { "CrypTool.Caesar.Caesar", "CrypTool.Plugins.AffineCipher.AffineCipher" })
        {
            var fields = SettingsSchema.Build(tn, new Dictionary<string, string>());
            Check(fields.Count > 0,
                $"settings schema for {tn}: " + string.Join("; ",
                fields.Select(f => $"{f.Caption}[{f.Control}/{f.ValueType}={f.Value}{(f.Options.Count > 0 ? " opts=" + string.Join("|", f.Options) : "")}]")));
        }
        {
            // a workspace built in the editor (new components, a connection, a note, a changed setting) survives save -> load
            var e3 = new WorkspaceEditor();
            e3.Load(new Workspace());
            var a = e3.AddPlugin(new PaletteEntry { TypeName = "CrypTool.Caesar.Caesar",
                AssemblyName = "Caesar",
                Name = "Caesar",
                Inputs = { new PalettePort("InputString", "System.String", "", true),
                new PalettePort("ShiftKey", "System.Int32", "", false) },
                Outputs = { new PalettePort("OutputString", "System.String", "", false) } },
                100,
                50);
            var b = e3.AddPlugin(new PaletteEntry { TypeName = "CrypTool.Caesar.Caesar",
                AssemblyName = "Caesar",
                Name = "Caesar 2",
                Inputs = { new PalettePort("InputString", "System.String", "", true) } },
                400,
                50);
            e3.Connect(a.Outputs[0], b.Inputs[0]);
            a.Settings["Key"] = "7";
            a.SettingTypes["Key"] = "System.Int32";
            var note = e3.AddText(10, 10);
            note.Text = "Hello\nWorld";
            var r3 = CwmReader.Read(CwmWriter.Write(e3.Ws));
            Check(r3.Plugins.Count == 2 && r3.Wires.Count == 1 && r3.Connectors.Count == 4,
                "editor-built workspace round-trips through .cwm");
            Check(r3.Plugins[0].Settings.GetValueOrDefault("Key") == "7" && r3.Plugins[0].AssemblyName == "Caesar",
                "setting + assembly name are saved");
            Check(r3.Texts.Count == 1 && r3.Texts[0].Text == "Hello\nWorld", "edited note text survives (generated XamlPackage)");
            Check(r3.Connectors.All(c => c.DataTypeAssembly == "mscorlib"), "connector types get their assembly");
        }
        {
            // the managed block ciphers (used in the browser) must agree with the .NET implementation
            var rnd = new Random(7);
            byte[] R(int n)
            {
                var b = new byte[n];
                rnd.NextBytes(b);
                return b;
            }
            bool same = true;
            string why = "";
            foreach (var mode in new[] { System.Security.Cryptography.CipherMode.ECB,
                System.Security.Cryptography.CipherMode.CBC,
                System.Security.Cryptography.CipherMode.CFB })
            foreach (var pad in new[] { System.Security.Cryptography.PaddingMode.PKCS7,
                System.Security.Cryptography.PaddingMode.Zeros,
                System.Security.Cryptography.PaddingMode.None,
                System.Security.Cryptography.PaddingMode.ANSIX923 })
            foreach (var len in new[] { 16, 32, 37 })
            {
                if (pad == System.Security.Cryptography.PaddingMode.None
                    && len % 16 != 0
                    && mode != System.Security.Cryptography.CipherMode.CFB) continue;
                if (mode == System.Security.Cryptography.CipherMode.CFB
                    && pad == System.Security.Cryptography.PaddingMode.None
                    && len % 16 != 0) continue;
                var key = R(16);
                var iv = R(16);
                var data = R(len);
                try
                {
                    using var ours = new AesCryptoServiceProvider { Mode = mode, Padding = pad };
                    using var theirs = System.Security.Cryptography.Aes.Create();
                    theirs.Mode = mode;
                    theirs.Padding = pad;
                    var a = ours.CreateEncryptor(key, iv);
                    var b = theirs.CreateEncryptor(key, iv);
                    var ea = a.TransformFinalBlock(data, 0, data.Length);
                    var eb = b.TransformFinalBlock(data, 0, data.Length);
                    if (!ea.SequenceEqual(eb))
                    {
                        same = false;
                        why += $" enc {mode}/{pad}/{len};";
                    }
                    var da = ours.CreateDecryptor(key, iv).TransformFinalBlock(eb, 0, eb.Length);
                    var db = theirs.CreateDecryptor(key, iv).TransformFinalBlock(eb, 0, eb.Length);
                    if (!da.SequenceEqual(db))
                    {
                        same = false;
                        why += $" dec {mode}/{pad}/{len};";
                    }
                }
                catch (Exception e)
                {
                    same = false;
                    why += $" {mode}/{pad}/{len}: {e.Message};";
                }
            }
            Check(same, "managed AES == .NET AES (ECB/CBC/CFB x paddings)" + why);
            var kat = new AesCryptoServiceProvider { Mode = System.Security.Cryptography.CipherMode.ECB,
                Padding = System.Security.Cryptography.PaddingMode.None }.CreateEncryptor(Convert.FromHexString("000102030405060708090a0b0c0d0e0f"),
                null).TransformFinalBlock(Convert.FromHexString("00112233445566778899aabbccddeeff"), 0, 16);
            Check(Convert.ToHexString(kat).ToLower() == "69c4e0d86a7b0430d8cdb78070b4c55a", "AES-128 known answer (FIPS-197)");
        }
        Console.WriteLine("editor tests done");
        foreach (var tpl in new[] { "Cryptography/Classic/AffineCipher.cwm",
            "Cryptography/Classic/Caesar.cwm",
            "Cryptography/Classic/Vigenere.cwm",
            "Codes/T9Code.cwm" })
        {
            var path = Path.Combine(root, tpl);
            if (!File.Exists(path))
            {
                Console.WriteLine("skip " + tpl);
                continue;
            }
            var ws = CwmReader.Read(File.ReadAllBytes(path));
            var eng = new ExecutionEngine();
            var run = eng.RunAsync(ws);
            await Task.Delay(2500);
            eng.Stop();
            await run;
            foreach (var n in eng.Nodes.Values) Console.WriteLine($"  {tpl} [{n.Model.TypeName.Split('.').Last()}] {(n.Supported ? n.State.ToString() : "unsupported")} display='{n.Display?.Replace("\n", " ")[..Math.Min(50, n.Display?.Length ?? 0)]}' logs={string.Join("|", n.Logs.Select(l => l.Message))}");
        }
    }

    static void Check(bool c, string m) => Console.WriteLine((c ? "PASS " : "FAIL ") + m);
}
