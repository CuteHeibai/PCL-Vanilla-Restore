using System;
using System.IO;
using System.Reflection;

namespace PCLRestore.Runtime {
 internal static class MetadataDependency {
  static readonly object Gate=new object();
  static Assembly library,adapter;
  internal static void EnsureLoaded() {
   lock(Gate) {
    if(library==null) {library=Load("PCLRestore.dnlib.dll");AppDomain.CurrentDomain.AssemblyResolve+=new ResolveEventHandler(Resolve);}
    if(adapter==null)adapter=Load("PCLRestore.Adaptation.dll");
   }
  }
  static Assembly Resolve(object sender,ResolveEventArgs args) {
   if(library!=null&&args.Name==library.FullName)return library;
   return adapter!=null&&args.Name==adapter.FullName?adapter:null;
  }
  static Assembly Load(string name) {using(var stream=typeof(Entry).Assembly.GetManifestResourceStream(name)){if(stream==null)throw new InvalidDataException("内嵌更新组件缺失："+name);using(var buffer=new MemoryStream()){stream.CopyTo(buffer);return Assembly.Load(buffer.ToArray());}}}
 }
}
