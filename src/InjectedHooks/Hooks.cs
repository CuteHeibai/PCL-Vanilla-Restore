using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Threading;

namespace PCLRestore
{
    public static class Hooks
    {
        public static Assembly RuntimeAssembly;
        public static string ExeDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;
        }
        public static string PortableRoot() { return ExeDirectory() + "PCL\\"; }
        public static void PrepareEnvironment()
        {
            string path = PortableRoot() + "Cache\\Temp\\";
            Directory.CreateDirectory(path);
            Environment.SetEnvironmentVariable("TEMP", path, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("TMP", path, EnvironmentVariableTarget.Process);
        }
        public static string PureRoot()
        {
            string path = PortableRoot();
            foreach (char c in path) if (c > 127) throw new IOException("该组件需要英文路径，请将启动器移动到英文目录。临时文件仍保存在启动器的 PCL 文件夹内。");
            Directory.CreateDirectory(path);
            return path;
        }
        public static string TaskFolder(bool requireNonSpace)
        {
            string path = PortableRoot() + "TaskTemp\\" + Guid.NewGuid().ToString("N") + "\\";
            if (requireNonSpace && path.Contains(" ")) throw new IOException("该组件需要无空格的路径，请移动启动器后重试。");
            Directory.CreateDirectory(path);
            return path;
        }
        public static object Call(string method, object[] args)
        {
            if (RuntimeAssembly == null)
            {
                Stream stream = typeof(Hooks).Assembly.GetManifestResourceStream("PCLRestore.Runtime.dll");
                if (stream == null) throw new InvalidDataException("更新组件缺失。");
                byte[] bytes = new BinaryReader(stream).ReadBytes(checked((int)stream.Length));
                stream.Dispose();
                RuntimeAssembly = Assembly.Load(bytes);
            }
            return RuntimeAssembly.GetType("PCLRestore.Runtime.Entry", true).GetMethod(method).Invoke(null, new object[] { args });
        }
        public static void Dispatch(string method) { ThreadPool.QueueUserWorkItem(new WaitCallback(Worker), method); }
        public static void Worker(object method)
        {
            try { Call((string)method, new object[0]); }
            catch (Exception)
            {
                if (Application.Current != null) Application.Current.Dispatcher.BeginInvoke(new Action(ReportUpdateFailure));
            }
        }
        public static void ReportUpdateFailure()
        {
            NativeMessage("暂时无法检查更新，请稍后重试。", "检查更新", "确定", "", true, Assembly.GetEntryAssembly());
        }
        public static int NativeMessage(string text, string title, string first, string second, bool warning, Assembly host)
        {
            Type owner = host.GetType("PCL.ModMain", true);
            Type[] signature = { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(Action), typeof(Action), typeof(Action) };
            MethodInfo dialog = owner.GetMethod("MyMsgBox", BindingFlags.Public | BindingFlags.Static, null, signature, null);
            if (dialog == null || dialog.ReturnType != typeof(int)) throw new InvalidDataException("PCL 原生提示窗口接口不兼容。");
            return (int)dialog.Invoke(null, new object[] { text, title, first, second, "", warning, true, true, null, null, null });
        }
        public static object Get(object obj, string name)
        {
            PropertyInfo p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return p == null ? null : p.GetValue(obj, null);
        }
        public static void VanillaOnly(object page)
        {
            if (page.GetType().FullName != "PCL.PageDownloadInstall") return;
            string[] cards = { "CardOptiFine", "CardLiteLoader", "CardForge", "CardNeoForge", "CardFabric", "CardFabricApi", "CardOptiFabric" };
            foreach (string name in cards)
            {
                UIElement control = Get(page, name) as UIElement;
                if (control != null) { control.Visibility = Visibility.Collapsed; control.IsHitTestVisible = false; }
            }
            string[] selections = { "SelectedOptiFine", "SelectedLiteLoader", "SelectedForge", "SelectedNeoForge", "SelectedFabric", "SelectedFabricApi", "SelectedOptiFabric" };
            foreach (string name in selections) page.GetType().GetField(name).SetValue(page, null);
        }
        public static void Brand(object window)
        {
            ((Window)window).Title = "PCL 原版下载恢复版";
            object logo = Get(window, "LabTitleLogo");
            if (logo != null)
            {
                logo.GetType().GetProperty("Text").SetValue(logo, "PCL 恢复版", null);
                ((UIElement)logo).Visibility = Visibility.Visible;
                UIElement shape = Get(window, "ShapeTitleLogo") as UIElement;
                if (shape != null) shape.Visibility = Visibility.Collapsed;
            }
        }
        public static void About(object page)
        {
            object item = Get(page, "ItemAboutPcl");
            if (item == null) return;
            PropertyInfo info = item.GetType().GetProperty("Info");
            string text = (string)info.GetValue(item, null);
            if (!text.Contains("第三方")) info.SetValue(item, text + "\n第三方独立修改：PCL 原版下载恢复版\n源码：https://github.com/CuteHeibai/PCL-Vanilla-Restore", null);
        }
        public static void CacheCurrent()
        {
            string destination = PortableRoot() + "Latest.bin";
            Directory.CreateDirectory(PortableRoot());
            File.Copy(Assembly.GetEntryAssembly().Location, destination, true);
        }
    }
}
