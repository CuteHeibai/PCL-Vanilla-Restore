using dnlib.DotNet;
using System.Text.Json;
using System.Security.Cryptography;
using PCLRestore.Adaptation;

if (args.Length < 2) throw new ArgumentException("inspect <exe> [type::method] | rewrite <in> <out>");
using var module = ModuleDefMD.Load(args[1]);
if (args[0] == "inspect")
{
    foreach (var type in module.GetTypes())
    foreach (var method in type.Methods.Where(m => m.HasBody))
    {
        var key = type.FullName + "::" + method.Name;
        if (args.Length > 2 && !key.Contains(args[2], StringComparison.Ordinal)) continue;
        if (args.Length == 2 && !method.Body.Instructions.Any(i => i.Operand is string s && s.Contains("不再提供 MC"))) continue;
        Console.WriteLine(key);
        foreach(var instruction in method.Body.Instructions) Console.WriteLine("  " + instruction);
    }
}
else if (args[0] == "rewrite")
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
    module.Write(args[2]);
    Console.WriteLine(JsonSerializer.Serialize(new { file=args[2], version=module.Assembly.Version.ToString() }));
}
else if(args[0]=="contract")
{
    File.WriteAllBytes(args[2],SelfAdapter.CreateCompatibilityContract(File.ReadAllBytes(args[1])));
}
else if(args[0]=="adapt")
{
    if(String.Equals(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),StringComparison.OrdinalIgnoreCase)) throw new IOException("The official input must be preserved; choose a different output path.");
    string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))).ToLowerInvariant();
    if(hash!=SelfAdapter.KnownInputHash) throw new InvalidDataException("Unsupported official binary SHA256; no output was written.");
    int revision=args.Length>5?int.Parse(args[5]):1;
    if(revision<1) throw new ArgumentOutOfRangeException("revision");
    bool updater=args.Length>4;
    if(!updater)throw new ArgumentException("The embedded official-auto-update runtime is required.");
    var result=SelfAdapter.Adapt(File.ReadAllBytes(args[1]),File.ReadAllBytes(args[3]),File.ReadAllBytes(args[4]),revision,"93ede5d9e638c99fcec853a9ba1eecd91ceee7f5");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
    File.WriteAllBytes(args[2],result.Image);
    Console.WriteLine(JsonSerializer.Serialize(new{file=args[2],sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))).ToLowerInvariant(),updater}));
}
else throw new ArgumentException("Unknown command");
