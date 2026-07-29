// Prints the assembly references baked into a compiled DLL.
//
// This exists because of how the mod is built. Parity is compiled against
// hand-written reference assemblies (see refs/README.md) that stand in for
// assemblies generated from the game. The whole approach rests on one property:
// the emitted IL must reference those assemblies by the names the real ones
// carry at runtime. That is invisible in a build log - a green build looks
// identical whether the names are right or wrong - so CI checks it explicitly.
//
//     dotnet run --project tools/DumpRefs -- src/bin/Release/Parity.dll
//
// Optionally pass a comma-separated list of names that must be present:
//
//     dotnet run --project tools/DumpRefs -- Parity.dll MelonLoader,UnityEngine.CoreModule
//
// Exits non-zero if any expected name is missing.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: DumpRefs <assembly.dll> [expected,names,...]");
    return 2;
}

string path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"error: {path} does not exist");
    return 2;
}

using FileStream stream = File.OpenRead(path);
using PEReader pe = new PEReader(stream);
MetadataReader metadata = pe.GetMetadataReader();

AssemblyDefinition self = metadata.GetAssemblyDefinition();
Console.WriteLine($"{metadata.GetString(self.Name)} {self.Version} references:");

HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
{
    AssemblyReference reference = metadata.GetAssemblyReference(handle);
    string name = metadata.GetString(reference.Name);
    found.Add(name);
    Console.WriteLine($"  {name}, Version={reference.Version}");
}

if (args.Length < 2)
{
    return 0;
}

string[] expected = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
List<string> missing = expected
    .Select(name => name.Trim())
    .Where(name => name.Length > 0 && !found.Contains(name))
    .ToList();

if (missing.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("error: expected assembly references are missing: " + string.Join(", ", missing));
    Console.Error.WriteLine("The mod would fail to bind against the game's assemblies at runtime.");
    return 1;
}

Console.WriteLine();
Console.WriteLine($"All {expected.Length} expected references are present.");
return 0;
