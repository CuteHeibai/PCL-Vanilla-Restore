using dnlib.DotNet;
using System.Text.Json.Nodes;

// QA-only stale provenance fixture. The updater/transaction has no test switch.
if(args.Length!=2)throw new ArgumentException("validated-exe isolated-QA-exe");
string input=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);
string artifacts=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
if(output.Equals(input,StringComparison.OrdinalIgnoreCase)||!output.StartsWith(artifacts,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Output must be a separate artifacts QA copy.");
using var module=ModuleDefMD.Load(input);
var resource=module.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="PCLRestore.Profile.json");
var profile=JsonNode.Parse(resource.CreateReader().ReadBytes((int)resource.Length))!.AsObject();
profile["sourceCommit"]=new string('0',40);profile["sourceExeSha256"]=new string('0',64);
module.Resources.Remove(resource);module.Resources.Add(new EmbeddedResource(resource.Name,System.Text.Encoding.UTF8.GetBytes(profile.ToJsonString()),resource.Attributes));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);module.Write(output);
Console.WriteLine("Prepared QA stale-provenance copy; production update logic unchanged.");
