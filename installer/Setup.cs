using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace FoxFriendSetup {
 static class Package {
  public const string Id="FoxFriend.Desktop.2";
  public static string TestRoot;
  public static string UninstallKey {get{return TestRoot==null?@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FoxFriend":@"Software\FoxFriend.SetupTest\Uninstall";}}
  public static string RunKey {get{return TestRoot==null?@"Software\Microsoft\Windows\CurrentVersion\Run":@"Software\FoxFriend.SetupTest\Run";}}
  public static readonly string[] Files={"FoxFriend.exe","assets/fox-atlas.png","assets/fox-icon.png","assets/fox.ico","README.md","SHA256SUMS.txt"};
  public static string Executable {get{return Assembly.GetExecutingAssembly().Location;}}
  public static string Receipt(string folder){return Path.Combine(folder,"FoxFriend.install");}
  public static string Validate(string folder){string full=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);if(full.Length<4||full==Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar))throw new IOException("Выберите отдельную папку для FoxFriend.");return full;}
  public static void Extract(string folder){
   folder=Validate(folder);Directory.CreateDirectory(folder);
   using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("FoxFriend.payload.zip"))using(var zip=new ZipArchive(source,ZipArchiveMode.Read)){
    if(zip.Entries.Count!=Files.Length)throw new InvalidDataException("Неверный состав установочного пакета.");
    foreach(var entry in zip.Entries){if(!Files.Contains(entry.FullName))throw new InvalidDataException("Неизвестный файл в пакете.");string dest=Path.GetFullPath(Path.Combine(folder,entry.FullName));if(!dest.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Некорректный путь в пакете.");Directory.CreateDirectory(Path.GetDirectoryName(dest));using(var input=entry.Open())using(var output=File.Create(dest))input.CopyTo(output);}
   }
   foreach(string line in File.ReadAllLines(Path.Combine(folder,"SHA256SUMS.txt"))){var fields=line.Split('\t');if(fields.Length!=2||!Files.Contains(fields[1])||fields[1]=="SHA256SUMS.txt")throw new InvalidDataException("Неверная контрольная сумма.");using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(folder,fields[1]))){string hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");if(!hash.Equals(fields[0],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Файл повреждён: "+fields[1]);}}
  }
  public static void Shortcut(string file,string target,string icon){Directory.CreateDirectory(Path.GetDirectoryName(file));dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic link=shell.CreateShortcut(file);link.TargetPath=target;link.WorkingDirectory=Path.GetDirectoryName(target);link.IconLocation=icon;link.Description="FoxFriend — твой настольный друг";link.Save();}
  public static string DesktopLink {get{return TestRoot==null?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"FoxFriend.lnk"):Path.Combine(TestRoot,"desktop","FoxFriend.lnk");}}
  public static string MenuLink {get{return TestRoot==null?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"FoxFriend","FoxFriend.lnk"):Path.Combine(TestRoot,"menu","FoxFriend.lnk");}}
  public static void StopCompanion(){try{using(var stop=EventWaitHandle.OpenExisting("Local\\FoxFriend.Close"))stop.Set();}catch(WaitHandleCannotBeOpenedException){} }
  public static void Install(string folder,bool desktop,bool startup){
   folder=Validate(folder);bool owned=File.Exists(Receipt(folder))&&File.ReadAllText(Receipt(folder)).Trim()==Id;
   if(!owned&&Files.Concat(new[]{"Uninstall.exe","FoxFriend.install"}).Any(f=>File.Exists(Path.Combine(folder,f))))throw new IOException("В этой папке уже есть файлы с такими именами. Выберите новую папку, чтобы ничего не перезаписать.");
   string stage=Path.Combine(Path.GetTempPath(),"FoxFriend-stage-"+Guid.NewGuid().ToString("N"));Extract(stage);
   try{
    Directory.CreateDirectory(folder);
    foreach(string file in Files){string destination=Path.Combine(folder,file);Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(Path.Combine(stage,file),destination,true);}
    File.Copy(Executable,Path.Combine(folder,"Uninstall.exe"),true);File.WriteAllText(Receipt(folder),Id);
    string app=Path.Combine(folder,"FoxFriend.exe");if(desktop)Shortcut(DesktopLink,app,Path.Combine(folder,"assets","fox.ico"));Shortcut(MenuLink,app,Path.Combine(folder,"assets","fox.ico"));
    using(var k=Registry.CurrentUser.CreateSubKey(UninstallKey)){k.SetValue("DisplayName","FoxFriend");k.SetValue("DisplayVersion","2.1.1");k.SetValue("Publisher","FoxFriend");k.SetValue("InstallLocation",folder);k.SetValue("DisplayIcon",Path.Combine(folder,"assets","fox.ico"));k.SetValue("UninstallString","\""+Path.Combine(folder,"Uninstall.exe")+"\" --uninstall");k.SetValue("NoModify",1);k.SetValue("NoRepair",1);}
    using(var k=Registry.CurrentUser.CreateSubKey(RunKey)){if(startup)k.SetValue("Lisichka","\""+app+"\"");else k.DeleteValue("Lisichka",false);}
   }finally{foreach(string f in Files){string p=Path.Combine(stage,f);if(File.Exists(p))File.Delete(p);}string assets=Path.Combine(stage,"assets");if(Directory.Exists(assets)&&!Directory.EnumerateFileSystemEntries(assets).Any())Directory.Delete(assets);if(Directory.Exists(stage)&&!Directory.EnumerateFileSystemEntries(stage).Any())Directory.Delete(stage);}
  }
  static void RemoveShortcut(string file,string target){if(!File.Exists(file))return;dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic link=shell.CreateShortcut(file);if(string.Equals((string)link.TargetPath,target,StringComparison.OrdinalIgnoreCase))File.Delete(file);}
  public static void Remove(string folder,int parent){
   folder=Validate(folder);if(!File.Exists(Receipt(folder))||File.ReadAllText(Receipt(folder)).Trim()!=Id)throw new IOException("Папка не принадлежит установке FoxFriend.");
   try{using(var p=Process.GetProcessById(parent)){if(!p.WaitForExit(15000))throw new IOException("Закройте установщик и повторите удаление.");}}catch(ArgumentException){}
   if(TestRoot==null){StopCompanion();Thread.Sleep(800);}
   string app=Path.Combine(folder,"FoxFriend.exe");RemoveShortcut(DesktopLink,app);RemoveShortcut(MenuLink,app);
   using(var run=Registry.CurrentUser.OpenSubKey(RunKey,true)){if(run!=null&&Convert.ToString(run.GetValue("Lisichka"))=="\""+app+"\"")run.DeleteValue("Lisichka",false);}
   using(var key=Registry.CurrentUser.OpenSubKey(UninstallKey)){if(key!=null&&Convert.ToString(key.GetValue("InstallLocation"))==folder){key.Close();Registry.CurrentUser.DeleteSubKeyTree(UninstallKey,false);}}
   foreach(string file in Files.Concat(new[]{"Uninstall.exe","FoxFriend.install"})){string path=Path.Combine(folder,file);if(File.Exists(path))File.Delete(path);}
   string assets=Path.Combine(folder,"assets");if(Directory.Exists(assets)&&!Directory.EnumerateFileSystemEntries(assets).Any())Directory.Delete(assets);if(Directory.Exists(folder)&&!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);
  }
  public static void SelfTest(string root){TestRoot=Validate(root);Directory.CreateDirectory(TestRoot);string target=Path.Combine(TestRoot,"Установка с пробелом");
   Install(target,false,false);if(!File.Exists(Path.Combine(target,"FoxFriend.exe"))||File.Exists(DesktopLink)||!File.Exists(MenuLink))throw new Exception("Install without desktop shortcut failed");
   Install(target,true,true);if(!File.Exists(DesktopLink))throw new Exception("Desktop shortcut not created");using(var k=Registry.CurrentUser.OpenSubKey(RunKey)){if(k==null||Convert.ToString(k.GetValue("Lisichka"))!="\""+Path.Combine(target,"FoxFriend.exe")+"\"")throw new Exception("Autostart failed");}
   File.WriteAllText(Path.Combine(target,"user-keeps.txt"),"preserve");Remove(target,-1);if(File.Exists(Path.Combine(target,"FoxFriend.exe"))||File.Exists(DesktopLink)||File.Exists(MenuLink)||!File.Exists(Path.Combine(target,"user-keeps.txt")))throw new Exception("Uninstall file isolation failed");
   using(var k=Registry.CurrentUser.OpenSubKey(UninstallKey)){if(k!=null)throw new Exception("Uninstall entry remained");}
   string collision=Path.Combine(TestRoot,"collision");Directory.CreateDirectory(collision);File.WriteAllText(Path.Combine(collision,"FoxFriend.exe"),"do not overwrite");bool rejected=false;try{Install(collision,false,false);}catch(IOException){rejected=true;}if(!rejected||File.ReadAllText(Path.Combine(collision,"FoxFriend.exe"))!="do not overwrite")throw new Exception("Collision protection failed");
   Registry.CurrentUser.DeleteSubKeyTree(@"Software\FoxFriend.SetupTest",false);File.WriteAllText(Path.Combine(TestRoot,"setup-test-result.txt"),"PASS: embedded checksums; install to Cyrillic path with spaces; optional desktop shortcut; Start menu shortcut; autostart in isolated registry; upgrade; uninstall preserves unrelated files; no overwrite of foreign files. Production registration untouched.");
  }
 }
 public sealed class RemoveWindow:Window {
  readonly TextBlock status;readonly Button action;bool working;
  public RemoveWindow(string target,int parent,bool worker){
   Title="FoxFriend · удаление";Width=510;Height=360;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=(Brush)new BrushConverter().ConvertFromString("#FFF9F0");Icon=BitmapFrame.Create(Assembly.GetExecutingAssembly().GetManifestResourceStream("FoxFriend.icon.png"));
   var panel=new StackPanel{Margin=new Thickness(28)};Content=panel;panel.Children.Add(new TextBlock{Text="Удаление FoxFriend",FontSize=26,Foreground=Brushes.SaddleBrown,Margin=new Thickness(0,0,0,18)});panel.Children.Add(new TextBlock{Text="Программа, её ярлыки и автозапуск будут удалены. Имя и личные настройки сохранятся.",TextWrapping=TextWrapping.Wrap,FontSize=14});panel.Children.Add(new TextBox{Text=target,IsReadOnly=true,Margin=new Thickness(0,18,0,18),Padding=new Thickness(8)});
   status=new TextBlock{Text=worker?"Удаляю программу…":"Нажмите «Удалить», чтобы продолжить.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)};panel.Children.Add(status);action=new Button{Content=worker?"Подождите…":"Удалить",IsEnabled=!worker,Padding=new Thickness(12,8,12,8)};panel.Children.Add(action);
   action.Click+=delegate{if(worker){Close();return;}try{string copy=Path.Combine(Path.GetTempPath(),"FoxFriend-uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Package.Executable,copy);Process.Start(new ProcessStartInfo(copy,"--remove-installed \""+target+"\" "+Process.GetCurrentProcess().Id){UseShellExecute=true});Close();}catch(Exception e){status.Text=e.Message;}};
   Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(working)e.Cancel=true;};
   if(worker)Loaded+=async delegate{working=true;try{await Task.Run(()=>Package.Remove(target,parent));status.Text="FoxFriend удалён. Личные настройки сохранены.";}catch(Exception e){status.Text="Удаление не завершено: "+e.Message;}finally{working=false;action.Content="Закрыть";action.IsEnabled=true;}};
  }
 }
 public sealed class SetupWindow:Window {
  TextBox folder;CheckBox desktop,startup,launch;Button install;TextBlock status;bool working;
  public SetupWindow(string updateFolder=null){
   Title="FoxFriend · установка";Width=540;Height=535;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=(Brush)new BrushConverter().ConvertFromString("#FFF9F0");FontFamily=new FontFamily("Segoe UI");Icon=BitmapFrame.Create(Assembly.GetExecutingAssembly().GetManifestResourceStream("FoxFriend.icon.png"));
   var panel=new StackPanel{Margin=new Thickness(30)};Content=panel;
   var header=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,18)};header.Children.Add(new Image{Source=Icon,Width=78,Height=78});var titles=new StackPanel{Margin=new Thickness(18,10,0,0)};titles.Children.Add(new TextBlock{Text="FoxFriend",FontSize=30,FontWeight=FontWeights.SemiBold,Foreground=Brushes.SaddleBrown});titles.Children.Add(new TextBlock{Text="Твой маленький рыжий друг",FontSize=14,Foreground=Brushes.Sienna});header.Children.Add(titles);panel.Children.Add(header);
   panel.Children.Add(new TextBlock{Text="Лисичка поселится на рабочем столе: общение, ласка, сон и маленькие тёплые моменты. Всё основное работает без интернета.",TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,0,0,20)});
   panel.Children.Add(new TextBlock{Text="Папка установки",Margin=new Thickness(0,0,0,6)});
   var location=new DockPanel{Margin=new Thickness(0,0,0,18)};var browse=new Button{Content="Обзор…",Margin=new Thickness(8,0,0,0)};browse.Click+=delegate{using(var dialog=new Forms.FolderBrowserDialog{Description="Выберите папку для FoxFriend",SelectedPath=folder.Text,ShowNewFolderButton=true}){if(dialog.ShowDialog()==Forms.DialogResult.OK)folder.Text=dialog.SelectedPath;}};DockPanel.SetDock(browse,Dock.Right);location.Children.Add(browse);folder=new TextBox{Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","FoxFriend"),Padding=new Thickness(8)};location.Children.Add(folder);panel.Children.Add(location);
   desktop=new CheckBox{Content="Создать ярлык на рабочем столе",IsChecked=true,Margin=new Thickness(0,0,0,12)};startup=new CheckBox{Content="Запускать вместе с Windows",IsChecked=true,Margin=new Thickness(0,0,0,12)};launch=new CheckBox{Content="Открыть подарок после установки",IsChecked=true,Margin=new Thickness(0,0,0,20)};panel.Children.Add(desktop);panel.Children.Add(startup);panel.Children.Add(launch);
   status=new TextBlock{Text="Установка для текущего пользователя. Права администратора не нужны для папок пользователя.",TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=Brushes.Sienna,Margin=new Thickness(0,0,0,16)};panel.Children.Add(status);
   if(updateFolder!=null&&File.Exists(Package.Receipt(updateFolder))&&File.ReadAllText(Package.Receipt(updateFolder)).Trim()==Package.Id){folder.Text=Package.Validate(updateFolder);desktop.IsChecked=File.Exists(Package.DesktopLink);using(var k=Registry.CurrentUser.OpenSubKey(Package.RunKey))startup.IsChecked=k!=null&&Convert.ToString(k.GetValue("Lisichka"))=="\""+Path.Combine(folder.Text,"FoxFriend.exe")+"\"";launch.Content="Запустить лисичку после обновления";status.Text="Обновление существующей установки. Имя, размеры и настройки сохранятся.";Title="FoxFriend · обновление";}
   install=new Button{Content=updateFolder==null?"Установить FoxFriend":"Обновить FoxFriend",FontSize=15,Padding=new Thickness(15,10,15,10)};install.Click+=async delegate{await Install();};panel.Children.Add(install);Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(working)e.Cancel=true;};
  }
  async Task Install(){if(working)return;working=true;install.IsEnabled=false;folder.IsEnabled=false;try{string target=Package.Validate(folder.Text);bool link=desktop.IsChecked==true,auto=startup.IsChecked==true;status.Text="Закрываю прежнюю лисичку и распаковываю подарок…";Package.StopCompanion();await Task.Delay(700);Package.Install(target,link,auto);status.Text="FoxFriend установлен. До встречи с лисичкой!";working=false;if(launch.IsChecked==true)Process.Start(new ProcessStartInfo(Path.Combine(target,"FoxFriend.exe")){WorkingDirectory=target,UseShellExecute=true});Close();}catch(Exception e){status.Text="Не удалось установить: "+e.Message;working=false;install.IsEnabled=true;folder.IsEnabled=true;}}
 }
 static class Program {
  [STAThread]static void Main(string[] args){try{
   if(args.Length==2&&args[0]=="--verify"){Package.Extract(args[1]);File.WriteAllText(Path.Combine(args[1],"verify-result.txt"),"PASS: embedded payload extracted, allowlisted paths, SHA256 verified.");return;}
   if(args.Length==2&&args[0]=="--self-test"){Package.SelfTest(args[1]);return;}
   var app=new Application();using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("FoxFriend.Theme.xaml"))app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
   bool worker=args.Length==3&&args[0]=="--remove-installed";bool remove=worker||args.Contains("--uninstall")||args.Contains("--render-uninstall")||Path.GetFileNameWithoutExtension(Package.Executable).Equals("Uninstall",StringComparison.OrdinalIgnoreCase);
   Window window=remove?(Window)new RemoveWindow(worker?args[1]:Path.GetDirectoryName(Package.Executable),worker?int.Parse(args[2]):-1,worker):new SetupWindow(args.Length==2&&args[0]=="--update"?args[1]:null);
   if(args.Length==2&&(args[0]=="--render"||args[0]=="--render-uninstall")){window.Loaded+=delegate{window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(args[1]))encoder.Save(f);window.Close();};}app.Run(window);
  }catch(Exception e){Environment.ExitCode=1;MessageBox.Show(e.Message,"FoxFriend — ошибка установки");}}
 }
}
