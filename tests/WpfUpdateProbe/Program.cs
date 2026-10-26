using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
class Program
{
    static string Hash(string file) { using(var h=SHA256.Create()) using(var f=File.OpenRead(file)) return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
    static void Main(string[] args)
    {
        string exe=Path.GetFullPath(args[0]), candidateSource=Path.GetFullPath(args[1]);
        if(!exe.Contains(Path.DirectorySeparatorChar+"artifacts"+Path.DirectorySeparatorChar)) throw new Exception("Integration probe is restricted to artifacts.");
        string root=Path.Combine(Path.GetDirectoryName(exe),"PCL","Cache","Update");
        Directory.CreateDirectory(root);
        string candidate=Path.Combine(root,"candidate.bin"); File.Copy(candidateSource,candidate,true);
        string session=Guid.NewGuid().ToString("N"),expected=Hash(candidate);
        var asm=typeof(PCLRestore.Runtime.Entry).Assembly;
        var transactionType=asm.GetType("PCLRestore.Runtime.Transaction");var transaction=Activator.CreateInstance(transactionType,true);
        Action<string,object> set=(name,value)=>transactionType.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(transaction,value);
        set("Directory",root);set("Exe",exe);set("Candidate",candidate);set("Backup",Path.Combine(root,"previous.bak"));set("Session",session);set("ExpectedSha",expected);set("OriginalSha",Hash(exe));set("Pid",int.Parse(args[2]));
        string script=(string)asm.GetType("PCLRestore.Runtime.ReplacementScript").GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{transaction});
        File.WriteAllText(Path.Combine(root,"replace.ps1"),script,new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(root,"pending.json"),new JavaScriptSerializer().Serialize(new{session=session,expectedSha256=expected,helperPid=0}));
        Console.WriteLine(root);
    }
}
