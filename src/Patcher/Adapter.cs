using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Security.Cryptography;
using System.Text;
using PCLRestore.Adaptation;

namespace PclRestore.Patcher;

internal sealed class Adapter
{
    public const string InputHash="0db34e19d9eeb6d496d20fabe786ec16bbd36ba6b0ccdc8d29cb7e909c114397";
    private readonly ModuleDefMD module;
    private readonly TypeDef hooks;
    private readonly int revision;
    private readonly string version, sourceHash, sourceCommit;
    private readonly Dictionary<string,string> before=new();
    public Adapter(ModuleDefMD module,byte[] hooksImage,int revision,string sourceHash,string sourceCommit)
    {
        this.module=module;
        this.revision=revision;
        this.version=module.Assembly.Version.ToString(); this.sourceHash=sourceHash; this.sourceCommit=sourceCommit;
        using var library=ModuleDefMD.Load(hooksImage);
        hooks=TypeInjector.Inject(module,library.GetTypes().Single(t=>t.FullName=="PCLRestore.Hooks"));
    }
    private MethodDef Method(string type,string name) => module.GetTypes().Single(t=>t.FullName==type).Methods.Single(m=>m.Name==name);
    private MethodDef Hook(string name)=>hooks.Methods.Single(m=>m.Name==name);
    private void Track(MethodDef m)
    {
        string key=m.FullName;
        if(!before.ContainsKey(key)) before[key]=Hashing.Sha(Encoding.UTF8.GetBytes(string.Join("\n",m.Body.Instructions)));
        m.Body.SimplifyBranches();
    }
    private static void SetBody(MethodDef m,params Instruction[] instructions)
    {
        m.Body=new CilBody(); foreach(var i in instructions) m.Body.Instructions.Add(i);
    }
    private void AppendHook(MethodDef m,string name,bool instance=true)
    {
        Track(m);
        foreach(var ret in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())
        {
            int at=m.Body.Instructions.IndexOf(ret);
            ret.OpCode=instance?OpCodes.Ldarg_0:OpCodes.Call;
            ret.Operand=instance?null:Hook(name);
            if(instance) m.Body.Instructions.Insert(++at,Instruction.Create(OpCodes.Call,Hook(name)));
            m.Body.Instructions.Insert(++at,Instruction.Create(OpCodes.Ret));
        }
    }
    public void RestoreVanilla()
    {
        var page="PCL.PageDownloadInstall";
        var loader=Method(page,"LoaderInit"); Track(loader);
        var il=loader.Body.Instructions;
        var start=il.Single(i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand=="关闭下载");
        int a=il.IndexOf(start),b=il.ToList().FindIndex(a,i=>i.OpCode==OpCodes.Newobj && i.Operand is IMethod m && m.DeclaringType.FullName.StartsWith("PCL.ModLoader/LoaderTask`2"));
        var field=module.GetTypes().Single(t=>t.FullName=="PCL.ModDownload").Fields.Single(f=>f.Name=="DlClientListLoader");
        for(int n=a;n<=b;n++) { il[n].OpCode=OpCodes.Nop; il[n].Operand=null; }
        start.OpCode=OpCodes.Ldsfld; start.Operand=field;
        var nav=Method("PCL.PageDownloadLeft",".ctor"); Track(nav);
        for(int n=0;n<nav.Body.Instructions.Count;n++)
        {
            var i=nav.Body.Instructions[n];
            if(i.Operand is IMethod m && (m.Name=="get_ItemInstall" || m.Name=="get_LabResource"))
            { nav.Body.Instructions[n+1].OpCode=OpCodes.Ldc_I4_0; nav.Body.Instructions[n+1].Operand=null; }
            if(i.OpCode==OpCodes.Stfld && i.Operand is IField f && f.Name=="PageID")
            { nav.Body.Instructions[n-1].OpCode=OpCodes.Ldc_I4_0; nav.Body.Instructions[n-1].Operand=null; }
            if(i.Operand is IMethod target && target.Name=="get_ItemMod") i.Operand=Method("PCL.PageDownloadLeft","get_ItemInstall");
        }
        foreach(string name in new[]{"PrepareSelectPage","ReloadSelected","ReloadComponentLists"}) AppendHook(Method(page,name),"VanillaOnly");
        var install=Method(page,"BtnStart_Click"); Track(install);
        install.Body.Instructions.Insert(0,Instruction.Create(OpCodes.Ldarg_0));
        install.Body.Instructions.Insert(1,Instruction.Create(OpCodes.Call,Hook("VanillaOnly")));
    }
    public void PortablePaths()
    {
        var init=Method("PCL.ModBase",".cctor"); Track(init);
        var il=init.Body.Instructions;
        int end=il.ToList().FindIndex(i=>i.OpCode==OpCodes.Stsfld && i.Operand is IField f && f.Name=="PathTemp");
        int start=il.Take(end).ToList().FindLastIndex(i=>i.OpCode==OpCodes.Stsfld)+1;
        for(int n=start;n<end;n++) { il[n].OpCode=OpCodes.Nop; il[n].Operand=null; }
        il[start].OpCode=OpCodes.Call; il[start].Operand=Hook("PortableRoot");
        var pure=Method("PCL.ModBase/_Closure$__","_Lambda$__0-1"); Track(pure);
        SetBody(pure,Instruction.Create(OpCodes.Call,Hook("PureRoot")),Instruction.Create(OpCodes.Ret));
        var task=Method("PCL.ModMain","RequestTaskTempFolder"); Track(task);
        var clear=Method("PCL.ModMain","TryClearTaskTemp"); Track(clear);
        SetBody(task,Instruction.Create(OpCodes.Call,clear),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,Hook("TaskFolder")),Instruction.Create(OpCodes.Ret));
        var cleanup=clear.Body.Instructions;
        int cleanupStart=cleanup.ToList().FindIndex(i=>i.OpCode==OpCodes.Ldstr && i.Operand is string s && s.Contains("ProgramData\\PCL\\TaskTemp"));
        if(cleanupStart<0) throw new InvalidDataException("Legacy task cleanup anchor missing.");
        int cleanupEnd=cleanup.ToList().FindIndex(cleanupStart,i=>i.OpCode==OpCodes.Pop);
        for(int n=cleanupStart;n<=cleanupEnd;n++) {cleanup[n].OpCode=OpCodes.Nop;cleanup[n].Operand=null;}
        var startup=Method("PCL.Application","Application_Startup"); Track(startup);
        foreach(var i in startup.Body.Instructions.Where(i=>i.Operand is IMethod m && m.FullName=="System.String System.IO.Path::GetTempPath()").Take(2)) i.Operand=Hook("ExeDirectory");
        int coreReady=startup.Body.Instructions.ToList().FindIndex(i=>i.Operand is IMethod m && m.FullName=="System.Void MeloongCore.Wpf.Main::Init()");
        if(coreReady<0) throw new InvalidDataException("Core initialization anchor missing.");
        startup.Body.Instructions.Insert(coreReady+1,Instruction.Create(OpCodes.Call,Hook("PrepareEnvironment")));
        // The server cache and pack exporter must not materialize another PCL exe.
        foreach(var method in module.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody))
        foreach(var instruction in method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr && i.Operand is string s && s=="Latest.exe"))
        { Track(method); instruction.Operand="Latest.bin"; }
        var latest=Method("PCL.ModSecret","DownloadLatestPCL"); Track(latest);
        SetBody(latest,Instruction.Create(OpCodes.Call,Hook("CacheCurrent")),Instruction.Create(OpCodes.Ret));
    }
    private void RuntimeCall(MethodDef target,string name,bool initialize=false)
    {
        Track(target);
        var code=new List<Instruction>{Instruction.Create(OpCodes.Ldstr,name)};
        code.Add(Instruction.Create(OpCodes.Ldc_I4,initialize?3:0));
        code.Add(Instruction.Create(OpCodes.Newarr,module.CorLibTypes.Object.TypeDefOrRef));
        if(initialize)
        {
            var path=module.GetTypes().Single(t=>t.FullName=="PCL.ModBase").Fields.Single(f=>f.Name=="PathExe");
            code.AddRange(new[]{Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Ldsfld,path),Instruction.Create(OpCodes.Stelem_Ref),Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldc_I4_1),Instruction.Create(OpCodes.Ldstr,version),Instruction.Create(OpCodes.Stelem_Ref),Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldc_I4_2),Instruction.Create(OpCodes.Ldc_I4,revision),Instruction.Create(OpCodes.Box,module.CorLibTypes.Int32.TypeDefOrRef),Instruction.Create(OpCodes.Stelem_Ref)});
        }
        code.Add(Instruction.Create(OpCodes.Call,Hook("Call"))); code.Add(Instruction.Create(OpCodes.Pop));
        int at=0;
        if(initialize)
        {
            at=target.Body.Instructions.ToList().FindIndex(i=>i.Operand is IMethod m && m.Name=="PrepareEnvironment" && m.DeclaringType.FullName=="PCLRestore.Hooks")+1;
            if(at==0) throw new InvalidDataException("Core initialization anchor not found.");
        }
        foreach(var i in code) target.Body.Instructions.Insert(at++,i);
    }
    public void AddUpdater(byte[] runtimeImage)
    {
        module.Resources.Add(new EmbeddedResource("PCLRestore.Runtime.dll",runtimeImage,ManifestResourceAttributes.Private));
        RuntimeCall(Method("PCL.Application","Application_Startup"),"Initialize",true);
        var loaded=Method("PCL.FormMain","FormMain_Loaded");
        // Ready is reported after the original Loaded handler has returned successfully.
        Track(loaded);
        var settingGet=module.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodSpec>().First(m=>m.DeclaringType.FullName=="PCL.Settings" && m.Name=="Get" && m.GenericInstMethodSig.GenericArguments[0].ElementType==ElementType.I4);
        foreach(var ret in loaded.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())
        {
            int at=loaded.Body.Instructions.IndexOf(ret); ret.OpCode=OpCodes.Ldstr; ret.Operand="MarkReady";
            var done=Instruction.Create(OpCodes.Ret);
            foreach(var i in new[]{Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Newarr,module.CorLibTypes.Object.TypeDefOrRef),Instruction.Create(OpCodes.Call,Hook("Call")),Instruction.Create(OpCodes.Pop),Instruction.Create(OpCodes.Ldstr,"SystemSystemUpdate"),Instruction.Create(OpCodes.Ldnull),Instruction.Create(OpCodes.Call,settingGet),Instruction.Create(OpCodes.Ldc_I4_3),Instruction.Create(OpCodes.Bge,done),Instruction.Create(OpCodes.Ldstr,"CheckSilently"),Instruction.Create(OpCodes.Call,Hook("Dispatch")),done}) loaded.Body.Instructions.Insert(++at,i);
        }
        foreach(var pair in new[]{("UpdateCheckByButton","CheckUpdates"),("UpdateStart","CheckSilently"),("UpdateRestart","CheckUpdates"),("UpdateReplace","CheckUpdates")})
        {
            var target=Method("PCL.ModSecret",pair.Item1); Track(target);
            SetBody(target,Instruction.Create(OpCodes.Ldstr,pair.Item2),Instruction.Create(OpCodes.Call,Hook("Dispatch")),Instruction.Create(OpCodes.Ret));
        }
    }
    public void Finish(bool updater)
    {
        AppendHook(Method("PCL.FormMain","FormMain_Loaded"),"Brand");
        AppendHook(Method("PCL.PageOtherAbout","PageOtherAbout_Loaded"),"About");
        // Keep the original author/version and add a visible third-party suffix.
        foreach(var m in module.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody))
        foreach(var i in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr && i.Operand is string s && s=="正式版 "+version))
        { Track(m); i.Operand="正式版 "+version+" · 原版下载恢复版 r"+revision; }
        var profile=new AdaptationProfile{upstreamVersion=version,patchRevision=revision,vanillaDownload=true,portableTemp=true,updaterProtocol=updater?1:0,sourceCommit=sourceCommit,sourceExeSha256=sourceHash,methodFingerprints=before,officialAutoRestore=updater,adapterProtocol=1};
        module.Resources.Add(new EmbeddedResource("PCLRestore.Profile.json",JsonData.Write(profile),ManifestResourceAttributes.Public));
    }
}
