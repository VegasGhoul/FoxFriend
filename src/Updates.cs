using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Lisichka {
 public sealed class ReleaseAsset {public string name, browser_download_url,digest;public long size;}
 public sealed class ReleaseInfo {public string tag_name,body;public bool draft,prerelease;public ReleaseAsset[] assets;}
 public sealed class UpdateService : IDisposable {
  public const string Repository="VegasGhoul/FoxFriend";
  public static readonly Version Current=typeof(UpdateService).Assembly.GetName().Version;
  readonly HttpClient client;
  public UpdateService(HttpMessageHandler handler=null){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;client=handler==null?new HttpClient():new HttpClient(handler);client.Timeout=TimeSpan.FromMinutes(5);client.DefaultRequestHeaders.UserAgent.ParseAdd("FoxFriend/"+Current);client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");}
  public static bool Newer(string tag){Version v;return Version.TryParse((tag??"").TrimStart('v','V'),out v)&&v>new Version(Current.Major,Current.Minor,Current.Build);}
  public static bool TrustedAsset(string url){Uri u;return Uri.TryCreate(url,UriKind.Absolute,out u)&&u.Scheme=="https"&&u.Host=="github.com"&&u.Port==443&&u.UserInfo.Length==0&&u.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal);}
  public async Task<ReleaseInfo> Check(CancellationToken token){using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(TimeSpan.FromSeconds(18));using(var r=await client.GetAsync("https://api.github.com/repos/"+Repository+"/releases/latest",timeout.Token)){if(r.StatusCode==HttpStatusCode.NotFound)throw new InvalidOperationException("Выпуски FoxFriend ещё не опубликованы или недоступны.");r.EnsureSuccessStatusCode();var info=new JavaScriptSerializer().Deserialize<ReleaseInfo>(await r.Content.ReadAsStringAsync());if(info==null||info.draft||info.prerelease||!Newer(info.tag_name))return null;Asset(info);return info;}}}
  static ReleaseAsset Asset(ReleaseInfo info){var a=(info.assets??new ReleaseAsset[0]).FirstOrDefault(x=>x.name=="FoxFriend.exe");if(a==null||!TrustedAsset(a.browser_download_url)||a.size<1024||a.size>150*1024*1024)throw new InvalidDataException("В выпуске нет подходящего установщика FoxFriend.");return a;}
  public async Task<string> Download(ReleaseInfo info,IProgress<int> progress,CancellationToken token){var asset=Asset(info);string hash=asset.digest??"";if(hash.StartsWith("sha256:"))hash=hash.Substring(7);else{var checksum=info.assets.FirstOrDefault(x=>x.name=="FoxFriend.exe.sha256");if(checksum==null||!TrustedAsset(checksum.browser_download_url))throw new InvalidDataException("В выпуске отсутствует контрольная сумма.");using(var r=await client.GetAsync(checksum.browser_download_url,token)){r.EnsureSuccessStatusCode();hash=(await r.Content.ReadAsStringAsync()).Trim().Split(' ','\t','\r','\n')[0];}}
   if(!Regex.IsMatch(hash,@"\A[0-9a-fA-F]{64}\z"))throw new InvalidDataException("Неверная контрольная сумма выпуска.");
   string folder=Path.Combine(Path.GetTempPath(),"FoxFriend-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string partial=Path.Combine(folder,"FoxFriend.download"),complete=Path.Combine(folder,"FoxFriend.exe");
   try{using(var response=await client.GetAsync(asset.browser_download_url,HttpCompletionOption.ResponseHeadersRead,token)){response.EnsureSuccessStatusCode();using(var source=await response.Content.ReadAsStreamAsync())using(var file=File.Create(partial)){byte[] bytes=new byte[65536];long total=0;int count;while((count=await source.ReadAsync(bytes,0,bytes.Length,token))>0){total+=count;if(total>asset.size)throw new InvalidDataException("Размер обновления не совпадает.");await file.WriteAsync(bytes,0,count,token);if(progress!=null)progress.Report((int)(total*100/asset.size));}if(total!=asset.size)throw new InvalidDataException("Обновление загрузилось не полностью.");}}
    token.ThrowIfCancellationRequested();using(var sha=SHA256.Create())using(var f=File.OpenRead(partial)){string actual=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");if(!actual.Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Файл обновления повреждён. Попробуйте снова.");}File.Move(partial,complete);return complete;
   }catch{if(File.Exists(partial))File.Delete(partial);if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);throw;}
  }
  public void Dispose(){client.Dispose();}
 }
 public sealed class UpdateWindow:Window {
  readonly UpdateService service=new UpdateService();readonly CancellationTokenSource cancel=new CancellationTokenSource();readonly TextBlock status;readonly Button button;readonly ProgressBar progress;ReleaseInfo release;bool downloading,finished;
  public UpdateWindow(){Title="FoxFriend · обновления";Width=440;Height=305;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=(Brush)new BrushConverter().ConvertFromString("#FFF9F0");var panel=new StackPanel{Margin=new Thickness(26)};Content=panel;panel.Children.Add(new TextBlock{Text="FoxFriend "+UpdateService.Current.ToString(3),FontSize=24,Foreground=Brushes.SaddleBrown,Margin=new Thickness(0,0,0,16)});status=new TextBlock{Text="Проверяю обновления…",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)};panel.Children.Add(status);progress=new ProgressBar{Minimum=0,Maximum=100,Height=8,IsIndeterminate=true,Foreground=Brushes.Peru,Margin=new Thickness(0,0,0,20)};panel.Children.Add(progress);button=new Button{Content="Проверить ещё раз",IsEnabled=false,Padding=new Thickness(12,8,12,8)};panel.Children.Add(button);button.Click+=async delegate{if(release==null)await Check();else await Download();};Loaded+=async delegate{await Check();};Closed+=delegate{finished=true;cancel.Cancel();service.Dispose();};}
  async Task Check(){button.IsEnabled=false;progress.IsIndeterminate=true;status.Text="Проверяю обновления…";try{release=await service.Check(cancel.Token);if(finished)return;status.Text=release==null?"У вас последняя опубликованная версия 🧡":"Есть новая версия "+release.tag_name+" 🧡\nМожно скачать и установить её. Имя и настройки сохранятся.";button.Content=release==null?"Проверить ещё раз":"Скачать и установить";}catch(Exception e){if(!finished)status.Text="Проверка не завершена: "+(e is OperationCanceledException?"нет ответа от сервера.":e.Message);}finally{if(!finished){progress.IsIndeterminate=false;button.IsEnabled=true;}}}
  async Task Download(){if(downloading)return;downloading=true;button.IsEnabled=false;progress.Value=0;status.Text="Загружаю обновление… Окно можно закрыть для отмены.";try{string file=await service.Download(release,new Progress<int>(x=>{if(!finished){progress.Value=x;status.Text="Загрузка обновления: "+x+"%";}}),cancel.Token);if(finished)return;string folder=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);Process.Start(new ProcessStartInfo(file,"--update \""+folder+"\""){UseShellExecute=true});Close();}catch(Exception e){if(!finished)status.Text="Загрузка не завершена: "+e.Message;}finally{downloading=false;if(!finished)button.IsEnabled=true;}}
 }
}
