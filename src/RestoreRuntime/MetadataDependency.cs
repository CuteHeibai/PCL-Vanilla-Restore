using System;
using System.IO;
using System.Reflection;

namespace PCLRestore.Runtime {
 internal static class MetadataDependency {
  static readonly object Gate=new object();
  static Assembly library;
  internal static void EnsureLoaded() {
   lock(Gate) {
    if(library!=null)return;
    using(var stream=typeof(Entry).Assembly.GetManifestResourceStream("PCLRestore.dnlib.dll")) {
     if(stream==null)throw new InvalidDataException("更新元数据组件缺失。");
     using(var buffer=new MemoryStream()){stream.CopyTo(buffer);library=Assembly.Load(buffer.ToArray());}
    }
    AppDomain.CurrentDomain.AssemblyResolve+=new ResolveEventHandler(Resolve);
   }
  }
  static Assembly Resolve(object sender,ResolveEventArgs args) {
   return library!=null&&args.Name==library.FullName?library:null;
  }
 }
}
