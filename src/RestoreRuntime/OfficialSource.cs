using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PCLRestore.Runtime {
 internal sealed class OfficialRelease {internal string Commit,ArchiveUrl,Message;}
 internal static class OfficialSource {
  internal const string CommitApiUrl="https://api.github.com/repos/Meloong-Git/PCL/commits?path=%E6%9C%80%E6%96%B0%E6%AD%A3%E5%BC%8F%E7%89%88.zip&per_page=1";
  const string ArchiveName="%E6%9C%80%E6%96%B0%E6%AD%A3%E5%BC%8F%E7%89%88.zip",ExeName="Plain Craft Launcher 2.exe";
  const long MaximumImage=128L*1024*1024;
  internal static OfficialRelease ParseCommitList(string json) {
   try {
    var list=new JavaScriptSerializer{MaxJsonLength=1048576}.DeserializeObject(json) as object[];
    if(list==null||list.Length==0)throw new InvalidDataException("官方更新信息为空。");
    var first=list[0] as Dictionary<string,object>;object value;
    if(first==null||!first.TryGetValue("sha",out value)||!(value is string)||!Regex.IsMatch((string)value,@"\A[0-9a-fA-F]{40}\z"))throw new InvalidDataException("官方更新提交无效。");
    string commit=((string)value).ToLowerInvariant(),message="";
    if(first.TryGetValue("commit",out value)&&value is Dictionary<string,object>) {
     var details=(Dictionary<string,object>)value;
     if(details.TryGetValue("message",out value)&&value is string) {
      string firstLine=((string)value).Split(new[]{'\r','\n'})[0];var safe=new StringBuilder();
      foreach(char c in firstLine)if(!Char.IsControl(c)&&safe.Length<200)safe.Append(c);message=safe.ToString();
     }
    }
    return new OfficialRelease{Commit=commit,ArchiveUrl="https://raw.githubusercontent.com/Meloong-Git/PCL/"+commit+"/"+ArchiveName,Message=message};
   }catch(InvalidDataException){throw;}catch(Exception e){throw new InvalidDataException("官方更新信息格式无效。",e);}
  }
  internal static void ExtractOriginal(string zipPath,string outputPath) {
   string input=Path.GetFullPath(zipPath),output=Path.GetFullPath(outputPath);
   if(String.Equals(input,output,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("官方更新原包不能原地覆盖。");
   string temporary=output+"."+Guid.NewGuid().ToString("N")+".bin";
   try {
    using(var zip=ZipFile.OpenRead(input)) {
     ZipArchiveEntry selected=null;
     foreach(var entry in zip.Entries)if(String.Equals(entry.FullName,ExeName,StringComparison.OrdinalIgnoreCase)){if(selected!=null)throw new InvalidDataException("官方更新包中的程序不唯一。");selected=entry;}
     if(selected==null||selected.Length<=0||selected.Length>MaximumImage)throw new InvalidDataException("官方更新包没有符合大小要求的根目录程序。");
     using(var source=selected.Open())using(var target=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
      var buffer=new byte[81920];long total=0;int read;
      while((read=source.Read(buffer,0,buffer.Length))>0){total+=read;if(total>MaximumImage||total>selected.Length)throw new InvalidDataException("官方程序解压数据超过限制。");target.Write(buffer,0,read);}
      if(total!=selected.Length)throw new InvalidDataException("官方程序解压数据不完整。");
     }
    }
    if(File.Exists(output))File.Replace(temporary,output,null);else File.Move(temporary,output);
   }catch(InvalidDataException){throw;}catch(Exception e){throw new InvalidDataException("无法读取官方更新原包。",e);}
   finally {if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
}
