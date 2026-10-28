using dnlib.DotNet;
using PclRestore.Patcher;
using var launcher=ModuleDefMD.Load(args[0]);using var probe=ModuleDefMD.Load(args[1]);
var injected=TypeInjector.Inject(launcher,probe.GetTypes().Single(t=>t.FullName=="PclNativeProbe"));launcher.EntryPoint=injected.Methods.Single(m=>m.Name=="Main");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);launcher.Write(args[2]);Console.WriteLine("Prepared isolated native PCL download probe");
