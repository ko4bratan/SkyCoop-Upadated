using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

static object Inspect(string path)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    var assembly = reader.GetAssemblyDefinition();
    var refs = reader.AssemblyReferences.Select(h => reader.GetAssemblyReference(h))
        .Select(r => new { name = reader.GetString(r.Name), version = r.Version.ToString() }).ToArray();
    var methods = new List<object>();
    foreach (var handle in reader.TypeDefinitions)
    {
        var type = reader.GetTypeDefinition(handle);
        string name = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        if (name == "UnityEngine.Application" || name == "MelonLoader.MelonMod")
            methods.Add(new { type = name, methods = type.GetMethods().Select(h => reader.GetString(reader.GetMethodDefinition(h).Name)).ToArray() });
    }
    var types = reader.TypeDefinitions.Select(h => reader.GetTypeDefinition(h))
        .Select(t => reader.GetString(t.Namespace) + "." + reader.GetString(t.Name)).ToArray();
    var typeRefs = reader.TypeReferences.Select(h => reader.GetTypeReference(h))
        .Where(t => t.ResolutionScope.Kind == HandleKind.AssemblyReference)
        .Select(t => new { name = reader.GetString(t.Namespace) + "." + reader.GetString(t.Name),
                          assembly = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)t.ResolutionScope).Name) }).ToArray();
    return new { file = Path.GetFileName(path), name = reader.GetString(assembly.Name), version = assembly.Version.ToString(), references = refs, types, type_references = typeRefs, selected_methods = methods };
}

try
{
    foreach (string file in args) Console.WriteLine(JsonSerializer.Serialize(Inspect(file)));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
