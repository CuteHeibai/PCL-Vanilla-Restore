using System;
using PCLRestore;
static class NativeDialogTests {
 internal static void Run(Action<string,Action> test){
  test("native update confirmation uses PCL buttons and return code",()=>{
   var method=typeof(Hooks).GetMethod("NativeMessage");if(method==null)throw new Exception("native PCL dialog bridge missing");
   PCL.ModMain.Result=2;
   int result=(int)method.Invoke(null,new object[]{"Update body","Check update","下载并更新","取消",false,typeof(PCL.ModMain).Assembly});
   if(result!=2||PCL.ModMain.Caption!="Update body"||PCL.ModMain.Title!="Check update"||PCL.ModMain.First!="下载并更新"||PCL.ModMain.Second!="取消"||!PCL.ModMain.Wait||PCL.ModMain.Warning)throw new Exception("native dialog arguments/result changed");
   PCL.ModMain.Result=1;if((int)method.Invoke(null,new object[]{"Update body","Check update","下载并更新","取消",false,typeof(PCL.ModMain).Assembly})!=1)throw new Exception("native confirmation result lost");
  });
  test("native update notice and failure use PCL single button",()=>{
   var method=typeof(Hooks).GetMethod("NativeMessage");if(method==null)throw new Exception("native PCL dialog bridge missing");
   method.Invoke(null,new object[]{"Failed","Update","确定","",true,typeof(PCL.ModMain).Assembly});
   if(PCL.ModMain.Second!=""||!PCL.ModMain.Wait||!PCL.ModMain.Warning)throw new Exception("native error dialog style changed");
  });
 }
}
namespace PCL {
 public static class ModMain {
  public static string Caption,Title,First,Second;public static bool Wait,Warning;public static int Result;
  public static int MyMsgBox(string caption,string title,string first,string second,string third,bool warning,bool highlight,bool wait,Action one,Action two,Action three){Caption=caption;Title=title;First=first;Second=second;Wait=wait;Warning=warning;return Result;}
 }
}
