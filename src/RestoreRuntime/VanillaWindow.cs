using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace PCLRestore.Runtime {
 internal sealed class VanillaWindow:Window {
  static VanillaWindow active;
  readonly VanillaInstaller installer;
  readonly ListBox list=new ListBox();readonly TextBox search=new TextBox(),folder=new TextBox(),versionName=new TextBox();
  readonly ComboBox category=new ComboBox();readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap};readonly ProgressBar progress=new ProgressBar{Height=8};
  readonly Button install=new Button{Content="下载并安装",Padding=new Thickness(18,7,18,7)},cancel=new Button{Content="取消任务",Padding=new Thickness(14,7,14,7),IsEnabled=false};
  List<VanillaVersion> versions=new List<VanillaVersion>();CancellationTokenSource task;bool installing;
  internal static void Attach(Window host){
   var panel=host.Content as Panel;if(panel==null)return;if(panel.Children.OfType<Button>().Any(b=>Equals(b.Tag,"PCLRestore.Vanilla")))return;
   var button=new Button{Content="独立原版下载",Tag="PCLRestore.Vanilla",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,16,12),Padding=new Thickness(14,7,14,7),ToolTip="使用内置独立引擎安装纯原版 Minecraft"};
   Grid.SetRowSpan(button,100);Grid.SetColumnSpan(button,100);Panel.SetZIndex(button,1000);button.Click+=(s,e)=>{e.Handled=true;Open(host);};panel.Children.Add(button);
  }
  internal static void Open(Window host){if(active!=null){active.Activate();return;}active=new VanillaWindow(host);active.Closed+=(s,e)=>active=null;active.Show();}
  VanillaWindow(Window host){
   Title="独立原版下载 · PCL 恢复版";Width=780;Height=650;MinWidth=660;MinHeight=540;Owner=host;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(245,247,250));
   string exe=Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);installer=new VanillaInstaller(Path.Combine(exe,"PCL","Cache","Vanilla"));
   var root=new Grid{Margin=new Thickness(22)};for(int i=0;i<7;i++)root.RowDefinitions.Add(new RowDefinition{Height=i==2?new GridLength(1,GridUnitType.Star):GridLength.Auto});Content=root;
   var heading=new StackPanel();heading.Children.Add(new TextBlock{Text="安装纯原版 Minecraft",FontSize=23,FontWeight=FontWeights.SemiBold});heading.Children.Add(new TextBlock{Text="独立读取官方清单并校验游戏文件；安装后使用 PCL 登录和启动。",Margin=new Thickness(0,6,0,16),TextWrapping=TextWrapping.Wrap});Add(root,heading,0);
   var filters=new DockPanel{Margin=new Thickness(0,0,0,10)};category.ItemsSource=new[]{"正式版","全部版本","预览版","远古版本"};category.SelectedIndex=0;category.Width=125;DockPanel.SetDock(category,Dock.Right);filters.Children.Add(category);search.Margin=new Thickness(0,0,10,0);search.ToolTip="搜索版本号";filters.Children.Add(search);Add(root,filters,1);Add(root,list,2);
   var fields=new Grid{Margin=new Thickness(0,14,0,10)};fields.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});fields.ColumnDefinitions.Add(new ColumnDefinition());fields.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});fields.RowDefinitions.Add(new RowDefinition());fields.RowDefinitions.Add(new RowDefinition());
   Field(fields,"游戏目录",folder,0);Field(fields,"版本名称",versionName,1);folder.Text=Path.Combine(exe,".minecraft");var choose=new Button{Content="选择…",Margin=new Thickness(8,0,0,7),Padding=new Thickness(12,5,12,5)};Grid.SetColumn(choose,2);fields.Children.Add(choose);choose.Click+=(s,e)=>{using(var dialog=new System.Windows.Forms.FolderBrowserDialog{Description="选择游戏目录（包含 versions、assets、libraries 的文件夹）",SelectedPath=folder.Text})if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)folder.Text=dialog.SelectedPath;};Add(root,fields,3);
   Add(root,progress,4);status.Text="正在获取官方版本列表…";status.Margin=new Thickness(0,8,0,10);Add(root,status,5);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};install.IsEnabled=false;actions.Children.Add(cancel);install.Margin=new Thickness(10,0,0,0);actions.Children.Add(install);Add(root,actions,6);
   search.TextChanged+=(s,e)=>Filter();category.SelectionChanged+=(s,e)=>Filter();list.SelectionChanged+=(s,e)=>{if(list.SelectedItem is VanillaVersion)versionName.Text=((VanillaVersion)list.SelectedItem).Id;};cancel.Click+=(s,e)=>{if(task!=null)task.Cancel();};install.Click+=async(s,e)=>await Install();
   Closing+=(s,e)=>{if(installing){e.Cancel=true;status.Text="请先取消任务，等待停止后再关闭窗口。";}else if(task!=null)task.Cancel();};Loaded+=async(s,e)=>await LoadCatalog();
  }
  static void Add(Grid grid,UIElement control,int row){Grid.SetRow(control,row);grid.Children.Add(control);}
  static void Field(Grid grid,string label,TextBox input,int row){var text=new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,7)};Grid.SetRow(text,row);grid.Children.Add(text);input.Margin=new Thickness(0,0,0,7);input.Padding=new Thickness(6,5,6,5);Grid.SetRow(input,row);Grid.SetColumn(input,1);grid.Children.Add(input);}
  void Filter(){string query=search.Text.Trim();int type=category.SelectedIndex;list.ItemsSource=versions.Where(v=>(query.Length==0||v.Id.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)&&(type==1||type==0&&v.Type=="release"||type==2&&v.Type=="snapshot"||type==3&&(v.Type=="old_alpha"||v.Type=="old_beta"))).ToList();}
  async Task LoadCatalog(){task=new CancellationTokenSource();try{versions=await Task.Run(()=>installer.Catalog(task.Token));Filter();install.IsEnabled=true;status.Text="请选择版本与游戏目录。";}catch(Exception e){if(!(e is OperationCanceledException))status.Text="获取版本列表失败："+e.Message;}finally{task.Dispose();task=null;}}
  async Task Install(){
   var selected=list.SelectedItem as VanillaVersion;if(selected==null){status.Text="请先选择版本。";return;}string directory=folder.Text,name=versionName.Text;try{VanillaModel.Name(name);directory=Path.GetFullPath(directory);}catch(Exception e){status.Text=e.Message;return;}
   if(MessageBox.Show(this,"将安装 "+selected.Id+" 到：\n"+Path.Combine(directory,"versions",name)+"\n已有同名版本不会被覆盖。","确认原版安装",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
   installing=true;install.IsEnabled=false;cancel.IsEnabled=true;folder.IsEnabled=versionName.IsEnabled=list.IsEnabled=false;task=new CancellationTokenSource();
   var updates=new Progress<VanillaProgress>(p=>{status.Text=p.Message;progress.IsIndeterminate=p.Total==0;if(p.Total>0){progress.Maximum=p.Total;progress.Value=p.Done;}});
   try{await Task.Run(()=>installer.Install(selected,directory,name,task.Token,p=>((IProgress<VanillaProgress>)updates).Report(p)));status.Text="安装完成。返回 PCL 启动页刷新版本列表，即可选择此版本登录并启动。";}
   catch(OperationCanceledException){status.Text="任务已取消，未发布未完成的版本；已校验依赖可供下次复用。";}
   catch(Exception e){status.Text="安装未完成："+e.Message;}
   finally{task.Dispose();task=null;installing=false;install.IsEnabled=true;cancel.IsEnabled=false;folder.IsEnabled=versionName.IsEnabled=list.IsEnabled=true;progress.IsIndeterminate=false;}
  }
 }
}
