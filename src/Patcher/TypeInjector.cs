using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace PclRestore.Patcher;

internal static class TypeInjector
{
    public static TypeDef Inject(ModuleDef module, TypeDef source)
    {
        var importer = new Importer(module, ImporterOptions.TryToUseTypeDefs);
        var type = new TypeDefUser(source.Namespace, source.Name, importer.Import(source.BaseType)) { Attributes=source.Attributes };
        module.Types.Add(type);
        var fields = new Dictionary<FieldDef, FieldDef>();
        var methods = new Dictionary<MethodDef, MethodDef>();
        foreach(var f in source.Fields)
        {
            var copy=new FieldDefUser(f.Name, importer.Import(f.FieldSig),f.Attributes);
            if(f.HasConstant) copy.Constant=new ConstantUser(f.Constant.Value);
            type.Fields.Add(copy); fields[f]=copy;
        }
        foreach(var m in source.Methods)
        {
            var copy=new MethodDefUser(m.Name,importer.Import(m.MethodSig),m.ImplAttributes,m.Attributes);
            type.Methods.Add(copy); methods[m]=copy;
        }
        foreach(var entry in methods)
        {
            var src=entry.Key; var dst=entry.Value;
            if(!src.HasBody) continue;
            dst.Body=new CilBody { InitLocals=src.Body.InitLocals };
            var locals=new Dictionary<Local,Local>();
            foreach(var v in src.Body.Variables) { var copy=new Local(importer.Import(v.Type)); locals[v]=copy; dst.Body.Variables.Add(copy); }
            var instructions=src.Body.Instructions.ToDictionary(x=>x,x=>new Instruction(x.OpCode));
            object? Import(object? value) => value switch
            {
                null=>null,
                Instruction x=>instructions[x],
                IList<Instruction> xs=>xs.Select(x=>instructions[x]).ToArray(),
                Local x=>locals[x],
                Parameter x=>dst.Parameters[x.Index],
                MethodDef x when methods.ContainsKey(x)=>methods[x],
                FieldDef x when fields.ContainsKey(x)=>fields[x],
                TypeDef x when x==source=>type,
                ITypeDefOrRef x=>importer.Import(x),
                IMethod x=>importer.Import(x),
                IField x=>importer.Import(x),
                _=>value
            };
            foreach(var i in src.Body.Instructions) { instructions[i].Operand=Import(i.Operand); dst.Body.Instructions.Add(instructions[i]); }
            foreach(var e in src.Body.ExceptionHandlers)
                dst.Body.ExceptionHandlers.Add(new ExceptionHandler(e.HandlerType) {
                    TryStart=(Instruction?)Import(e.TryStart),TryEnd=(Instruction?)Import(e.TryEnd),
                    HandlerStart=(Instruction?)Import(e.HandlerStart),HandlerEnd=(Instruction?)Import(e.HandlerEnd),
                    FilterStart=(Instruction?)Import(e.FilterStart),CatchType=e.CatchType==null?null:importer.Import(e.CatchType)
                });
        }
        return type;
    }
}
