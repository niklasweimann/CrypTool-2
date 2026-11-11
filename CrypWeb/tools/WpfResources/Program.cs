// Writes .resources files that contain files as stream resources, like the "<Assembly>.g.resources" WPF builds from
// the <Resource> items of a project. Code-behind reads them with a ResourceManager and casts the values to Stream.
//
// Usage: WpfResources <list file>
// Each line of the list file is  <output .resources file>|<resource name>|<source file>.
using System.Resources;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: WpfResources <list file>");
    return 1;
}

var entriesByOutput = File.ReadAllLines(args[0])
    .Where(line => line.Trim().Length > 0)
    .Select(line => line.Split('|'))
    .GroupBy(parts => parts[0]);

foreach (var group in entriesByOutput)
{
    Directory.CreateDirectory(Path.GetDirectoryName(group.Key)!);
    using var writer = new ResourceWriter(group.Key);
    foreach (string[] parts in group)
    {
        // closeAfterWrite: the writer reads the stream when the file is generated
        writer.AddResource(parts[1], new MemoryStream(File.ReadAllBytes(parts[2])), closeAfterWrite: true);
    }
    writer.Generate();
}
return 0;
