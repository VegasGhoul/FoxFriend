using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Lisichka;
class FakeHttp:HttpMessageHandler {
 public string Json; public byte[] Bytes=new byte[2048];public HttpStatusCode Status=HttpStatusCode.OK;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(Status){Content=request.RequestUri.Host=="api.github.com"?(HttpContent)new StringContent(Json):new ByteArrayContent(Bytes)});}
}
class UpdateTests {
 static int count;static void Check(bool test,string why){if(!test)throw new Exception(why);count++;}
 static void Main(){Run().GetAwaiter().GetResult();}
 static async Task Run(){
  Check(!UpdateService.Newer("v2.1.2"),"equal version");Check(UpdateService.Newer("v2.1.3"),"patch version");Check(!UpdateService.Newer("v2.0.9"),"downgrade");Check(!UpdateService.Newer("v2.2-beta"),"prerelease parse");
  string url="https://github.com/VegasGhoul/FoxFriend/releases/download/v2.1.3/FoxFriend.exe";
  Check(UpdateService.TrustedAsset(url),"release URL");Check(!UpdateService.TrustedAsset(url.Replace("https:","http:")),"no HTTP");Check(!UpdateService.TrustedAsset(url.Replace("github.com","github.com.evil.example")),"no other host");Check(!UpdateService.TrustedAsset(url.Replace("FoxFriend/releases","Other/releases")),"no other repository");
  var fake=new FakeHttp();string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(fake.Bytes)).Replace("-","");
  fake.Json="{\"tag_name\":\"v2.1.3\",\"assets\":[{\"name\":\"FoxFriend.exe\",\"size\":2048,\"browser_download_url\":\""+url+"\",\"digest\":\"sha256:"+hash+"\"}]}";
  using(var service=new UpdateService(fake)){var release=await service.Check(CancellationToken.None);Check(release!=null,"new release found");string path=await service.Download(release,null,CancellationToken.None);Check(File.Exists(path)&&new FileInfo(path).Length==2048,"complete checked download");File.Delete(path);Directory.Delete(Path.GetDirectoryName(path));
   fake.Bytes[0]=1;bool rejected=false;try{await service.Download(release,null,CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected,"corrupt download rejected");
   fake.Bytes=new byte[1024];rejected=false;try{await service.Download(release,null,CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected,"truncated download rejected");
   var cancel=new CancellationTokenSource();cancel.Cancel();rejected=false;try{await service.Check(cancel.Token);}catch(OperationCanceledException){rejected=true;}Check(rejected,"cancellation");
   fake.Status=HttpStatusCode.NotFound;rejected=false;try{await service.Check(CancellationToken.None);}catch(InvalidOperationException){rejected=true;}Check(rejected,"missing release is not up-to-date");
  }
  Console.WriteLine("PASS: "+count+" update checks (version, origin, checksum, truncation, cancellation, missing release)");
 }
}
