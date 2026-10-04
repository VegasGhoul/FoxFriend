using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Automation;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace Lisichka {
    public class Settings {
        public double Left=-1, Top=-1; public bool Quiet=false, LocalAI=false; public string Model="qwen2.5:3b";
        public double PetScale=1;public SendMode SendBy=SendMode.Both;public string UserName="",PetName="";public bool TenderNames=true,IntroComplete=false;
        public double CloudWidth=330,CloudHeight=190;public bool CheckUpdates=true;
        public static string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lisichka");
        public static Settings Load() { try { var s=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder,"settings.json")))??new Settings();s.PetScale=InputPolicy.Scale(s.PetScale);s.UserName=Names.Clean(s.UserName);if(!Enum.IsDefined(typeof(SendMode),s.SendBy))s.SendBy=SendMode.Both;return s; } catch { return new Settings(); } }
        public void Save() { try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder,"settings.json"),new JavaScriptSerializer().Serialize(this),Encoding.UTF8); } catch { } }
    }
    public static class Startup {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool Enabled { get { using(var k=Registry.CurrentUser.OpenSubKey(Key)) return k!=null && k.GetValue("Lisichka")!=null; } }
        public static void Set(bool value) { using(var k=Registry.CurrentUser.CreateSubKey(Key)) { if(value) k.SetValue("Lisichka","\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\""); else k.DeleteValue("Lisichka",false); } }
    }
    public class ChatLine { public string role; public string content; public ChatLine(string r,string c){role=r;content=c;} }
    public class LocalBrain : IDisposable {
        readonly HttpClient client=new HttpClient(new HttpClientHandler { UseProxy=false }) { Timeout=TimeSpan.FromSeconds(45) };
        public async Task<Reply> Ask(string model,List<ChatLine> history) {
            var messages=new List<ChatLine> { new ChatLine("system", "Ты Лисичка, доброжелательный виртуальный настольный питомец. Отвечай по-русски, тепло, 1–3 короткими предложениями, иногда с 🧡. Помни контекст беседы. Не утверждай, что обладаешь сознанием, чувствами или являешься человеком. Не выдумывай факты о пользователе. Не ставь диагнозов, не давай медицинских назначений. При грусти бережно выслушай, уточни, что нужно; не обесценивай, не обещай всё исправить. При радости раздели её. Не создавай зависимости и не отговаривай от общения с людьми. Распознавай просьбы изменить СВОЮ позу, включая перефразировки: sit, lie, sleep; отрицания не исполняй. Желание пользователя самому спать не команда. Верни только JSON: {\"text\":\"ответ\",\"pose\":\"none|sit|lie|sleep\"}.") };
            messages.AddRange(history.Skip(Math.Max(0,history.Count-16)));
            var json=new JavaScriptSerializer();
            string body=json.Serialize(new { model=model, messages=messages, stream=false, format="json", options=new { temperature=0.7, num_predict=220 } });
            using(var response=await client.PostAsync("http://127.0.0.1:11434/api/chat",new StringContent(body,Encoding.UTF8,"application/json"))) {
                response.EnsureSuccessStatusCode();
                var outer=json.Deserialize<Dictionary<string,object>>(await response.Content.ReadAsStringAsync());
                var msg=(Dictionary<string,object>)outer["message"];
                var result=json.Deserialize<Dictionary<string,object>>(Convert.ToString(msg["content"]));
                string text=result.ContainsKey("text")?Convert.ToString(result["text"]):"";
                if(string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Пустой ответ модели");
                string pose=result.ContainsKey("pose")?Convert.ToString(result["pose"]):"none";
                return new Reply(text.Substring(0,Math.Min(1600,text.Length)),pose=="sit"?(Pose?)Pose.Sit:pose=="lie"?(Pose?)Pose.Lie:pose=="sleep"?(Pose?)Pose.Sleep:null);
            }
        }
        public void Dispose(){client.Dispose();}
    }
    public class FoxWindow : Window {
        readonly Settings settings=Settings.Load(); readonly Companion companion=new Companion(); readonly LocalBrain brain=new LocalBrain();
        readonly Random random=new Random(); readonly List<ChatLine> history=new List<ChatLine>(); readonly List<DispatcherTimer> timers=new List<DispatcherTimer>();
        readonly BitmapSource[] sprites=new BitmapSource[6];
        SpriteBlend spriteBlend;
        Image front,back; Grid pet,stage; StackPanel messages,cloudStack; ScrollViewer scroll; Border cloud; TextBox input; Button send; TextBlock status,zzz,heart; Forms.NotifyIcon tray;DockPanel cloudHeader;Canvas effects;bool compact;
        public bool DiagnosticMode;
        Pose pose=Pose.Sit; int frame=-1, animationVersion,addressCounter; bool busy,closed,autoSleeping,initialized,awaitingName; DateTime lastActivity=DateTime.Now,nextQuestion,nextAffection,awakeUntil=DateTime.MinValue;
        readonly DragSession drag=new DragSession();Window settingsWindow;
        readonly ScaleTransform breath=new ScaleTransform(1,1); readonly RotateTransform sway=new RotateTransform();
        readonly TranslateTransform lift=new TranslateTransform();
        public FoxWindow(bool testing=false) {
            Title="FoxFriend — Лисичка"; Width=350; Height=650; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; ShowActivated=false; FontFamily=new FontFamily("Segoe UI");Icon=new BitmapImage(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","fox-icon.png")));
            LoadSprites(); BuildUI();Opacity=settings.IntroComplete?1:0;if(testing){Opacity=1;return;} CreateTray();IsVisibleChanged+=delegate{spriteBlend.SetTailActive(IsVisible);};
            Loaded+=async delegate {if(initialized)return;initialized=true;await InitializeCompanion();};
            Closing+=delegate { closed=true;if(settingsWindow!=null)settingsWindow.Close(); if(!DiagnosticMode){settings.Left=Left; settings.Top=Top; settings.Save();} foreach(var t in timers)t.Stop(); tray.Visible=false;tray.Dispose(); brain.Dispose(); SystemEvents.DisplaySettingsChanged-=DisplayChanged; SystemEvents.PowerModeChanged-=PowerChanged; };
            SystemEvents.DisplaySettingsChanged+=DisplayChanged; SystemEvents.PowerModeChanged+=PowerChanged;
            Timer(3,delegate { Blink(); }); Timer(5,delegate { Tick(); });
            nextQuestion=DateTime.Now.AddMinutes(random.Next(25,41)); nextAffection=DateTime.Now.AddMinutes(random.Next(15,26));
        }
        async Task InitializeCompanion(){
            Place();companion.SetName(settings.UserName);if(DiagnosticMode||settings.IntroComplete)Opacity=1;
            if(!DiagnosticMode&&!settings.IntroComplete){Hide();var gift=new GiftWindow(sprites[0]){Icon=Icon,Owner=this};if(gift.ShowDialog()!=true){if(!closed)Close();return;}if(closed)return;settings.IntroComplete=true;settings.Save();
                var area=SystemParameters.WorkArea;double x=area.Right-Width-18,y=area.Bottom-Height-8;Left=gift.Left+gift.Width/2-Width/2;Top=gift.Top+338-Height+5;cloudStack.Opacity=0;Show();BeginAnimation(OpacityProperty,Anim(0,1,.3));BeginAnimation(LeftProperty,Anim(Left,x,1.4));BeginAnimation(TopProperty,Anim(Top,y,1.4));await Task.Delay(1450);if(closed)return;
                if(!drag.Active){double currentX=Left,currentY=Top;BeginAnimation(LeftProperty,null);BeginAnimation(TopProperty,null);Left=currentX;Top=currentY;}
                cloudStack.BeginAnimation(OpacityProperty,Anim(0,1,.4));settings.Left=Left;settings.Top=Top;settings.Save();
            }
            if(closed)return;awaitingName=string.IsNullOrEmpty(settings.UserName);
            Say(Companion.Greeting(DateTime.Now)+(awaitingName?" Я твоя Лисичка. Как тебя зовут?":" "+settings.UserName+", я рядом 🧡"));
            if(Companion.Night(DateTime.Now)){autoSleeping=true;SetPose(Pose.Sleep,false);}StartAnimations();if(!DiagnosticMode&&settings.CheckUpdates)CheckUpdatesInBackground();
        }
        async void CheckUpdatesInBackground(){try{await Task.Delay(5000);if(closed)return;using(var updates=new UpdateService()){var release=await updates.Check(CancellationToken.None);if(closed||release==null)return;Say("Для меня вышло обновление "+release.tag_name+" 🧡 Открой настройки → «Проверить обновления», и я помогу его установить.");tray.ShowBalloonTip(7000,"FoxFriend · обновление","Доступна версия "+release.tag_name+". Нажмите, чтобы открыть обновления.",Forms.ToolTipIcon.Info);tray.BalloonTipClicked+=delegate{if(!closed)new UpdateWindow{Icon=Icon}.Show();};}}catch{/* Offline startup remains quiet. Manual checking explains errors. */}}
        void HideCompanion(){EndDrag(false);if(settingsWindow!=null)settingsWindow.Close();Hide();}
        void SetCloudCompact(bool value){compact=value;cloudHeader.Visibility=value?Visibility.Collapsed:Visibility.Visible;status.Visibility=value?Visibility.Collapsed:Visibility.Visible;scroll.Visibility=value?Visibility.Collapsed:Visibility.Visible;cloud.Padding=value?new Thickness(10,2,10,10):new Thickness(14,10,14,12);}
        public void ShowCompanion(){Show();EnsureReachable();}
        Point CursorInDesktop(MouseEventArgs e){var physical=PointToScreen(e.GetPosition(this));var source=PresentationSource.FromVisual(this);return source!=null?source.CompositionTarget.TransformFromDevice.Transform(physical):physical;}
        void StartDrag(MouseButtonEventArgs e){double x=Left,y=Top;BeginAnimation(LeftProperty,null);BeginAnimation(TopProperty,null);Left=x;Top=y;var p=CursorInDesktop(e);drag.Start(p.X,p.Y,Left,Top);pet.CaptureMouse();e.Handled=true;}
        void MoveDrag(MouseEventArgs e){if(!drag.Active)return;var p=CursorInDesktop(e);bool wasMoving=drag.Moved;double x,y;if(drag.Update(p.X,p.Y,e.LeftButton==MouseButtonState.Pressed,out x,out y)){Left=x;Top=y;if(!wasMoving){var hop=Anim(0,-5,.16);hop.AutoReverse=true;hop.RepeatBehavior=RepeatBehavior.Forever;lift.BeginAnimation(TranslateTransform.YProperty,hop);var trot=Anim(-2,2,.18);trot.AutoReverse=true;trot.RepeatBehavior=RepeatBehavior.Forever;sway.BeginAnimation(RotateTransform.AngleProperty,trot);}}else if(e.LeftButton!=MouseButtonState.Pressed)EndDrag(false);}
        void EndDrag(bool allowPet){bool click=drag.Active&&!drag.Moved;bool moved=drag.Moved;drag.Finish();pet.ReleaseMouseCapture();if(moved){lift.BeginAnimation(TranslateTransform.YProperty,Anim(lift.Y,0,.2));StartAnimations();settings.Left=Left;settings.Top=Top;if(!DiagnosticMode)settings.Save();}if(allowPet&&click)Pet();}
        double StageHeight {get{return (pose==Pose.Sit?295:pose==Pose.Lie?245:230)*settings.PetScale;}}
        void ApplySize(){settings.PetScale=InputPolicy.Scale(settings.PetScale);settings.CloudWidth=double.IsNaN(settings.CloudWidth)?330:Math.Max(240,Math.Min(520,settings.CloudWidth));settings.CloudHeight=double.IsNaN(settings.CloudHeight)?190:Math.Max(100,Math.Min(360,settings.CloudHeight));double bottom=Top+Height;Width=Math.Max(settings.CloudWidth+20,280*settings.PetScale+20);Height=Math.Min(SystemParameters.WorkArea.Height-12,settings.CloudHeight+165+295*settings.PetScale);cloudStack.Width=settings.CloudWidth;pet.LayoutTransform=new ScaleTransform(settings.PetScale,settings.PetScale);stage.BeginAnimation(HeightProperty,null);stage.Height=StageHeight;scroll.MaxHeight=Math.Min(settings.CloudHeight,Math.Max(55,Height-StageHeight-145));if(initialized&&!double.IsNaN(bottom))Top=bottom-Height;}
        void ApplySendMode(){send.Visibility=InputPolicy.ArrowSends(settings.SendBy)?Visibility.Visible:Visibility.Collapsed;input.ToolTip=settings.SendBy==SendMode.Arrow?"Стрелка — отправить; Enter — новая строка":"Enter — отправить; Shift+Enter — новая строка";}
        public void RenderChecks(string folder) {
            Directory.CreateDirectory(folder);
            spriteBlend.VerifyBlink(folder);
            spriteBlend.VerifyTail(folder);
            var root=(Grid)Content;
            Say("Доброе утро! 🧡 Я рядом. Погладь меня или напиши что-нибудь.");
            foreach(var p in new[]{Pose.Sit,Pose.Lie,Pose.Sleep}){
                pose=p;SetFrame(BaseFrame,0);front.BeginAnimation(OpacityProperty,null);front.Opacity=1;back.BeginAnimation(OpacityProperty,null);back.Opacity=0;zzz.Opacity=p==Pose.Sleep?1:0;
                root.Measure(new Size(330,645));root.Arrange(new Rect(0,0,330,645));root.UpdateLayout();
                var render=new RenderTargetBitmap(330,645,96,96,PixelFormats.Pbgra32);render.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(render));using(var file=File.Create(Path.Combine(folder,p.ToString()+".png")))encoder.Save(file);
            }
            for(int i=0;i<12;i++)AddLine("Это проверка длинной переписки: облачко растёт в отведённой области, а лисичка остаётся внизу и никогда не перекрывается текстом. "+i,i%2==0);
            root.Measure(new Size(330,645));root.Arrange(new Rect(0,0,330,645));root.UpdateLayout();
            var cloudBottom=cloudStack.TransformToAncestor(root).Transform(new Point(0,cloudStack.ActualHeight)).Y;
            var petTop=stage.TransformToAncestor(root).Transform(new Point(0,0)).Y;
            if(cloudBottom>petTop)throw new InvalidOperationException("Cloud overlaps pet");
            var longRender=new RenderTargetBitmap(330,645,96,96,PixelFormats.Pbgra32);longRender.Render(root);var longEncoder=new PngBitmapEncoder();longEncoder.Frames.Add(BitmapFrame.Create(longRender));using(var file=File.Create(Path.Combine(folder,"LongChat.png")))longEncoder.Save(file);
            File.WriteAllText(Path.Combine(folder,"layout-result.txt"),"PASS: cloud bottom="+cloudBottom+", pet region top="+petTop+". Transparent WPF canvas. Six alpha sprites loaded.");
            brain.Dispose();
        }
        public async void SmokeCheck() {
            var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tests","renders");Directory.CreateDirectory(folder);
            try {
                await Task.Delay(700);settings.LocalAI=false;
                foreach(var entry in new[]{new KeyValuePair<string,Pose>("Присядь рядом",Pose.Sit),new KeyValuePair<string,Pose>("Полежи пожалуйста",Pose.Lie),new KeyValuePair<string,Pose>("Ложись спать",Pose.Sleep)}) {
                    input.Text=entry.Key;await Submit();await Task.Delay(600);
                    if(pose!=entry.Value)throw new Exception("Wrong pose: "+entry.Key);
                    if(!send.IsEnabled)throw new Exception("Composer stayed disabled");
                    SaveRender(Path.Combine(folder,"Live-"+entry.Value+".png"));
                }
                Pet();await Task.Delay(650);if(frame!=2||pose!=Pose.Sit)throw new Exception("Affection failed");SaveRender(Path.Combine(folder,"Live-Pet.png"));await Task.Delay(1700);
                Blink();await Task.Delay(95);if(frame!=1)throw new Exception("Blink did not close eyes");await Task.Delay(220);if(frame!=0)throw new Exception("Blink did not reopen eyes");
                input.Text="Я тебя люблю";await Submit();await Task.Delay(300);if(!history[history.Count-1].content.Contains("люб"))throw new Exception("Love reply missing");SaveRender(Path.Combine(folder,"Live-Love.png"));
                foreach(Pose tailPose in new[]{Pose.Sit,Pose.Lie,Pose.Sleep}){SetPose(tailPose);input.Text="Повеляй хвостиком";await Submit();await Task.Delay(500);if(spriteBlend.WagBoost<.9)throw new Exception("Tail command did not animate");SaveRender(Path.Combine(folder,"Wag-"+tailPose+".png"));}spriteBlend.WagTail(false);await Task.Delay(500);if(spriteBlend.WagBoost>.01)throw new Exception("Tail command did not stop");SetPose(Pose.Sit);
                input.Text="Сегодня мне грустно и тяжело";await Submit();
                awaitingName=true;input.Text="Кирилл";await Submit();if(settings.UserName!="Кирилл")throw new Exception("Name onboarding failed");var profileJson=new JavaScriptSerializer();var restored=profileJson.Deserialize<Settings>(profileJson.Serialize(settings));if(restored.UserName!="Кирилл"||restored.SendBy!=settings.SendBy)throw new Exception("Profile persistence serialization failed");
                foreach(SendMode mode in Enum.GetValues(typeof(SendMode))){settings.SendBy=mode;ApplySendMode();input.Text="Сядь";int before=history.Count;var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};input.RaiseEvent(key);await Task.Delay(80);if(key.Handled!=InputPolicy.EnterSends(mode,false))throw new Exception("Enter routing failed "+mode);if(mode==SendMode.Arrow){send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);if(input.Text.Length!=0)throw new Exception("Arrow routing failed");}}
                settings.SendBy=SendMode.Both;ApplySendMode();
                foreach(double size in new[]{.6,1.0,1.4}){settings.PetScale=size;ApplySize();await Task.Delay(100);SaveRender(Path.Combine(folder,"Size-"+((int)(size*100))+".png"));var rootCheck=(Grid)Content;if(cloudStack.TransformToAncestor(rootCheck).Transform(new Point(0,cloudStack.ActualHeight)).Y>stage.TransformToAncestor(rootCheck).Transform(new Point(0,0)).Y)throw new Exception("Scaled cloud overlaps pet");}
                settings.PetScale=1;ApplySize();SetCloudCompact(true);await Task.Delay(50);if(!IsVisible||!input.IsVisible||scroll.IsVisible)throw new Exception("Compact cloud hides pet or composer");SaveRender(Path.Combine(folder,"Compact.png"));input.Text="Привет";await Submit();if(compact)throw new Exception("Reply did not reopen chat");
                foreach(double width in new[]{240.0,520.0})foreach(double height in new[]{100.0,360.0}){settings.CloudWidth=width;settings.CloudHeight=height;ApplySize();await Task.Delay(70);var checkRoot=(Grid)Content;if(cloudStack.TransformToAncestor(checkRoot).Transform(new Point(0,cloudStack.ActualHeight)).Y>stage.TransformToAncestor(checkRoot).Transform(new Point(0,0)).Y)throw new Exception("Cloud size overlaps pet");}settings.CloudWidth=330;settings.CloudHeight=190;ApplySize();
                HideCompanion();if(IsVisible)throw new Exception("Hide did not hide whole companion");ShowCompanion();OpenSettings();await Task.Delay(150);SaveWindowRender(settingsWindow,Path.Combine(folder,"Settings.png"));settingsWindow.Close();
                var gift=new GiftWindow(sprites[0]);gift.Loaded+=async delegate{await Task.Delay(100);SaveWindowRender(gift,Path.Combine(folder,"Gift-closed.png"));await gift.Tap();await Task.Delay(420);if(!gift.IsVisible||gift.ClickCount!=1)throw new Exception("Gift first tap");await gift.Tap();await Task.Delay(420);if(!gift.IsVisible||gift.ClickCount!=2)throw new Exception("Gift second tap");Task opening=gift.Tap();await Task.Delay(1050);SaveWindowRender(gift,Path.Combine(folder,"Gift-opening.png"));await opening;};if(gift.ShowDialog()!=true||gift.ClickCount!=3)throw new Exception("Gift did not open after third tap");
                input.Text="Предложи тему разговора";await Submit();if(!history.Last().content.Contains("1. Музыка"))throw new Exception("Topic menu missing");input.Text="1";await Submit();input.Text="Мне нравится рок";await Submit();if(history.Last().content.IndexOf("В роке",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Topic conversation lost context");SaveRender(Path.Combine(folder,"Topic-chat.png"));
                for(int i=0;i<15;i++)AddLine(new string('я',2000),i%2==0);
                await Task.Delay(400);SaveRender(Path.Combine(folder,"Live-LongChat.png"));
                var root=(Grid)Content;double bottom=cloudStack.TransformToAncestor(root).Transform(new Point(0,cloudStack.ActualHeight)).Y;double top=stage.TransformToAncestor(root).Transform(new Point(0,0)).Y;
                if(bottom>top)throw new Exception("Long chat overlap");
                File.WriteAllText(Path.Combine(folder,"smoke-result.txt"),"PASS: live WPF window, commands, blink, love response, name onboarding, Enter/Arrow/Both routed input, 60/100/140% sizes, compact composer, cloud 240-520 x 100-360 without overlap, hide/restore, settings, three-tap gift animation, long chat separation. Visible="+IsVisible+"; cloud bottom="+bottom+"; pet top="+top);
            } catch(Exception ex){Environment.ExitCode=1;File.WriteAllText(Path.Combine(folder,"smoke-result.txt"),"FAIL: "+ex);}
            finally{Close();}
        }
        void SaveRender(string path){SaveWindowRender(this,path);}
        static void SaveWindowRender(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(path))png.Save(f);}
        static SolidColorBrush Color(string hex){return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);}
        void LoadSprites() {
            var atlas=new BitmapImage(); atlas.BeginInit(); atlas.UriSource=new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","fox-atlas.png")); atlas.CacheOption=BitmapCacheOption.OnLoad; atlas.EndInit(); atlas.Freeze();
            // Atlas cells have different proportions; trim transparent margins without altering painted pixels.
            int[,] rects={{0,0,548,562},{548,0,488,562},{1036,0,500,562},{0,565,548,459},{548,565,488,459},{1036,565,500,459}};
            int[,] origins=new int[6,2];
            for(int i=0;i<6;i++) {
                var crop=new CroppedBitmap(atlas,new Int32Rect(rects[i,0],rects[i,1],rects[i,2],rects[i,3]));
                var rgba=new FormatConvertedBitmap(crop,PixelFormats.Bgra32,null,0); int w=rgba.PixelWidth,h=rgba.PixelHeight,stride=w*4; byte[] px=new byte[stride*h]; rgba.CopyPixels(px,stride,0);
                int left=w,top=h,right=0,bottom=0; for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(px[y*stride+x*4+3]>24){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                var trimmed=new CroppedBitmap(crop,new Int32Rect(Math.Max(0,left-2),Math.Max(0,top-2),Math.Min(w-1,right+2)-Math.Max(0,left-2)+1,Math.Min(h-1,bottom+2)-Math.Max(0,top-2)+1)); trimmed.Freeze(); sprites[i]=trimmed;origins[i,0]=rects[i,0]+Math.Max(0,left-2);origins[i,1]=rects[i,1]+Math.Max(0,top-2);
            }
            sprites[1]=SpriteBlend.EyesOnly(sprites[0],atlas,origins[0,0],origins[0,1],new double[,]{{197,225,38,41,469,5},{316,246,43,43,470,6}});
            sprites[4]=SpriteBlend.EyesOnly(sprites[3],atlas,origins[3,0],origins[3,1],new double[,]{{144,787,40,41,502,5},{267,815,43,43,481,8}});
            spriteBlend=new SpriteBlend(sprites);
        }
        Button SmallButton(string text,string tip,RoutedEventHandler action) {
            var b=new Button { Content=text,ToolTip=tip,Background=Brushes.Transparent,Foreground=Color("#95634E"),BorderThickness=new Thickness(0),Padding=new Thickness(7,3,7,3),Cursor=Cursors.Hand,FontSize=15,Focusable=false }; b.Click+=action; AutomationProperties.SetName(b,tip); return b;
        }
        void BuildUI() {
            var root=new Grid { Margin=new Thickness(10,0,10,5) }; root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto}); Content=root;
            cloudStack=new StackPanel {Width=330,HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Bottom, Margin=new Thickness(0,0,0,8) };
            cloud=new Border { Background=Color("#FFF9F0"),BorderBrush=Color("#EDCEAF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Padding=new Thickness(14,10,14,12) };
            var contents=new StackPanel(); cloud.Child=contents;
            var header=new DockPanel();cloudHeader=header;
            var controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            controls.Children.Add(SmallButton("☷","Предложить темы разговора",delegate {SetCloudCompact(false);Say(companion.SuggestTopics());}));
            controls.Children.Add(SmallButton("−","Оставить только поле ответа",delegate {SetCloudCompact(true);}));
            controls.Children.Add(SmallButton("⚙","Настройки",delegate {OpenSettings();}));
            controls.Children.Add(SmallButton("×","Закрыть переписку, оставить поле ответа",delegate {SetCloudCompact(true);})); DockPanel.SetDock(controls,Dock.Right); header.Children.Add(controls);
            header.Children.Add(new TextBlock {Text="Лисичка",FontWeight=FontWeights.SemiBold,FontSize=15,Foreground=Color("#704432"),VerticalAlignment=VerticalAlignment.Center}); contents.Children.Add(header);
            status=new TextBlock {Text="рядом с тобой",Foreground=Color("#A77A62"),FontSize=11,Margin=new Thickness(0,1,0,7)}; contents.Children.Add(status);
            messages=new StackPanel(); scroll=new ScrollViewer {Content=messages,MaxHeight=190,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false}; contents.Children.Add(scroll);
            var composer=new Grid {Margin=new Thickness(0,9,0,0)}; composer.ColumnDefinitions.Add(new ColumnDefinition()); composer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var inputBorder=new Border {CornerRadius=new CornerRadius(12),BorderBrush=Color("#EDD9C5"),BorderThickness=new Thickness(1),Background=Brushes.White,Padding=new Thickness(9,7,9,7)};
            input=new TextBox {FontSize=13,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Color("#654B40"),MaxLength=30000,MinHeight=20,MaxHeight=90,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,ToolTip="Напиши мне… До 30 000 символов; Shift+Enter — новая строка"}; AutomationProperties.SetName(input,"Сообщение лисичке");
            var inputGrid=new Grid();var hint=new TextBlock{Text="Напиши мне…",Foreground=Color("#B49B89"),FontSize=13,IsHitTestVisible=false,Margin=new Thickness(2,1,0,0)};inputGrid.Children.Add(hint);inputGrid.Children.Add(input);inputBorder.Child=inputGrid;input.TextChanged+=delegate{hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;};
            input.PreviewKeyDown+=async delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Enter && InputPolicy.EnterSends(settings.SendBy,(Keyboard.Modifiers&ModifierKeys.Shift)!=0)){e.Handled=true; await Submit();} };
            composer.Children.Add(inputBorder); send=SmallButton("➜","Отправить сообщение",async delegate {if(InputPolicy.ArrowSends(settings.SendBy))await Submit();}); Grid.SetColumn(send,1); composer.Children.Add(send); contents.Children.Add(composer);
            cloudStack.Children.Add(cloud); root.Children.Add(cloudStack);
            stage=new Grid {Height=295,ClipToBounds=true}; Grid.SetRow(stage,1); root.Children.Add(stage);
            pet=new Grid {Width=280,Height=283,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Cursor=Cursors.Hand,ToolTip="Нажми — погладить; потяни — перенести; правая кнопка — меню",RenderTransformOrigin=new Point(.5,.95)};
            var transforms=new TransformGroup();transforms.Children.Add(breath);transforms.Children.Add(sway);transforms.Children.Add(lift);pet.RenderTransform=transforms;
            back=new Image {Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Width=270,Opacity=0};
            front=new Image {Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Width=270};
            RenderOptions.SetBitmapScalingMode(front,BitmapScalingMode.HighQuality); RenderOptions.SetBitmapScalingMode(back,BitmapScalingMode.HighQuality); pet.Children.Add(back);pet.Children.Add(front);stage.Children.Add(pet);
            AutomationProperties.SetName(pet,"Лисичка — нажмите, чтобы погладить");
            pet.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){StartDrag(e);};
            pet.MouseMove+=delegate(object sender,MouseEventArgs e){MoveDrag(e);};
            pet.MouseLeftButtonUp+=delegate {EndDrag(true);};
            pet.LostMouseCapture+=delegate{if(drag.Active)EndDrag(false);};
            pet.MouseRightButtonUp+=delegate {OpenMenu();};
            zzz=new TextBlock {Text="Z z z",Foreground=Color("#987BB1"),FontSize=24,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(40,0,0,165),Opacity=0,IsHitTestVisible=false,RenderTransform=new TranslateTransform()};stage.Children.Add(zzz);
            heart=new TextBlock {Text="♥",Foreground=Color("#F28A3D"),FontSize=32,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Opacity=0,IsHitTestVisible=false,RenderTransform=new TranslateTransform()};stage.Children.Add(heart);
            effects=new Canvas{IsHitTestVisible=false};Grid.SetRowSpan(effects,2);root.Children.Add(effects);
            SetFrame(0,0);
            ApplySendMode();
        }
        void Timer(double seconds,Action action){var t=new DispatcherTimer {Interval=TimeSpan.FromSeconds(seconds)};t.Tick+=delegate{action();};timers.Add(t);t.Start();}
        static DoubleAnimation Anim(double from,double to,double seconds){return new DoubleAnimation(from,to,TimeSpan.FromSeconds(seconds)){EasingFunction=new SineEase {EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.HoldEnd};}
        void StartAnimations(){var a=Anim(.991,1.009,2.1);a.AutoReverse=true;a.RepeatBehavior=RepeatBehavior.Forever;breath.BeginAnimation(ScaleTransform.ScaleYProperty,a);var s=Anim(-.55,.55,3.3);s.AutoReverse=true;s.RepeatBehavior=RepeatBehavior.Forever;sway.BeginAnimation(RotateTransform.AngleProperty,s);}
        void SetFrame(int index,double duration) {
            if(frame==index)return;frame=index;spriteBlend.Transition(index,duration);front.Source=spriteBlend.Bitmap;front.Width=SpriteBlend.Width;front.Height=SpriteBlend.Height;front.Opacity=1;back.Opacity=0;
        }
        int BaseFrame {get{return pose==Pose.Sleep?5:pose==Pose.Lie?3:0;}}
        async void Blink(){if(closed||!IsVisible||pose==Pose.Sleep||frame==2||drag.Active)return;int version=++animationVersion;SetFrame(pose==Pose.Lie?4:1,.085);await Task.Delay(160);if(!closed&&version==animationVersion)SetFrame(BaseFrame,.1);}
        void SetPose(Pose value,bool manual=true){animationVersion++;pose=value;if(manual){autoSleeping=false;if(value!=Pose.Sleep&&Companion.Night(DateTime.Now))awakeUntil=DateTime.Today.AddHours(7);}SetFrame(BaseFrame,.48);stage.BeginAnimation(HeightProperty,Anim(stage.ActualHeight>0?stage.ActualHeight:295,StageHeight,.48));scroll.MaxHeight=Math.Min(settings.CloudHeight,Math.Max(55,Height-StageHeight-145));status.Text=value==Pose.Sleep?"спит · разбуди, когда захочешь":value==Pose.Lie?"уютно устроилась рядом":"рядом с тобой";AnimateSleep();}
        void AnimateSleep(){if(pose==Pose.Sleep){var a=Anim(0,.9,1.8);a.AutoReverse=true;a.RepeatBehavior=RepeatBehavior.Forever;zzz.BeginAnimation(OpacityProperty,a);var t=Anim(8,-12,3.6);t.RepeatBehavior=RepeatBehavior.Forever;((TranslateTransform)zzz.RenderTransform).BeginAnimation(TranslateTransform.YProperty,t);}else {zzz.BeginAnimation(OpacityProperty,Anim(zzz.Opacity,0,.3));}}
        async void Pet(){lastActivity=DateTime.Now;animationVersion++;int v=animationVersion;if(pose!=Pose.Sit){SetPose(Pose.Sit);v=animationVersion;}SetFrame(2,.2);Say("Мр-р… Как тепло от твоей ласки 🧡");ShowHeart();await Task.Delay(2100);if(!closed&&v==animationVersion)SetFrame(BaseFrame,.35);}
        void ShowHeart(){ShowHearts(3);}
        void ShowHearts(int count){count=Math.Max(1,Math.Min(12,count));while(effects.Children.Count>24-count)effects.Children.RemoveAt(0);var root=(Grid)Content;Point head=pet.TransformToAncestor(root).Transform(new Point(140,pose==Pose.Sit?24:pose==Pose.Lie?104:144));for(int i=0;i<count;i++){var h=new TextBlock{Text="♥",FontSize=random.Next(15,27),Foreground=Color(i%2==0?"#EF9142":"#EAB075"),Opacity=0};effects.Children.Add(h);double x=head.X-10+random.Next(-12,13),y=head.Y-12,life=1.7+random.NextDouble()*.7;Canvas.SetLeft(h,x);Canvas.SetTop(h,y);var fade=new DoubleAnimationUsingKeyFrames{BeginTime=TimeSpan.FromSeconds(i*.07),Duration=TimeSpan.FromSeconds(life)};fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(0)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(.95,KeyTime.FromPercent(.18)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(.9,KeyTime.FromPercent(.6)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(1)));fade.Completed+=delegate{effects.Children.Remove(h);};h.BeginAnimation(OpacityProperty,fade);var up=Anim(y,y-random.Next(55,95),life);up.BeginTime=TimeSpan.FromSeconds(i*.07);h.BeginAnimation(Canvas.TopProperty,up);var side=Anim(x,x+random.Next(-65,66),life);side.BeginTime=TimeSpan.FromSeconds(i*.07);h.BeginAnimation(Canvas.LeftProperty,side);}}
        void AddLine(string text,bool user){
            history.Add(new ChatLine(user?"user":"assistant",text));if(history.Count>32)history.RemoveAt(0);
            var block=new TextBlock {TextWrapping=TextWrapping.Wrap,FontSize=13,LineHeight=19,Foreground=Color("#684D3E")};
            var pieces=text.Split(new[]{"🧡"},StringSplitOptions.None);for(int i=0;i<pieces.Length;i++){if(i>0)block.Inlines.Add(new Run("♥"){Foreground=Color("#ED893E"),FontSize=16});block.Inlines.Add(new Run(pieces[i]));}
            var bubble=new Border {Child=block,Background=Color(user?"#F5E4D5":"#FFFFFF"),CornerRadius=new CornerRadius(12),Padding=new Thickness(10,7,10,7),Margin=new Thickness(user?25:0,0,user?0:14,6)};
            messages.Children.Add(bubble);while(messages.Children.Count>24)messages.Children.RemoveAt(0);
            bubble.BeginAnimation(OpacityProperty,Anim(0,1,.45));var entry=new TranslateTransform(0,7);bubble.RenderTransform=entry;entry.BeginAnimation(TranslateTransform.YProperty,Anim(7,0,.45));scroll.Dispatcher.BeginInvoke(new Action(delegate{scroll.ScrollToEnd();}));
        }
        void Say(string text){AddLine(text,false);}
        async Task Submit(){
            string text=input.Text.Trim();if(text.Length==0||busy)return;lastActivity=DateTime.Now;input.Clear();SetCloudCompact(false);AddLine(text,true);busy=true;send.IsEnabled=false;
            try {
                string userName=Names.Read(text,awaitingName);if(userName.Length>0){settings.UserName=userName;companion.SetName(userName);awaitingName=false;if(!DiagnosticMode)settings.Save();Say("Очень приятно, "+userName+"! Запомнила твоё имя 🧡 Изменить имя и ласковое обращение можно в настройках.");return;}
                if(awaitingName&&(Intent.Normalize(text)=="пропустить"||Intent.Normalize(text)=="не хочу")){awaitingName=false;Say("Хорошо, познакомимся, когда захочешь 🧡");return;}
                if(System.Text.RegularExpressions.Regex.IsMatch(Intent.Normalize(text),@"не называй меня ласково|без ласковых имен")){settings.TenderNames=false;if(!DiagnosticMode)settings.Save();Say("Хорошо, буду использовать обычное имя 🧡");return;}
                Reply result;Pose? direct=Intent.Command(text);
                if(settings.LocalAI&&!direct.HasValue){status.Text="слушаю и думаю…";try{result=await brain.Ask(settings.Model,new List<ChatLine>(history));}catch(Exception){result=companion.Respond(text);result.Text="Локальная модель недоступна — отвечаю в простом режиме.\n"+result.Text;}}
                else result=companion.Respond(text);
                if(closed)return;if(result.TailAction!=0)spriteBlend.WagTail(result.TailAction>0);if(result.Action.HasValue)SetPose(result.Action.Value);ShowHearts(ConversationAnalyzer.HeartCount(text,result.Affection));if(result.Affection){if(pose!=Pose.Sit)SetPose(Pose.Sit);SetFrame(2,.25);int v=++animationVersion;await Task.Delay(800);if(!closed&&v==animationVersion)SetFrame(BaseFrame,.3);}if(!closed){addressCounter++;if(settings.UserName.Length>0&&!result.Text.Contains(settings.UserName)&&(result.Affection||Intent.Negative(text)||addressCounter%3==0))result.Text=Names.Address(result.Text,Names.Form(settings.UserName,settings.PetName,settings.TenderNames&&(result.Affection||Intent.Negative(text))));Say(result.Text);}
            } finally {busy=false;if(!closed){send.IsEnabled=true;status.Text=pose==Pose.Sleep?"спит · разбуди, когда захочешь":settings.LocalAI?"локальный ИИ · рядом с тобой":"рядом с тобой";}}
        }
        void Tick(){
            if(!IsVisible)return;
            DateTime now=DateTime.Now;
            if(Companion.Night(now)&&now>=awakeUntil&&pose!=Pose.Sleep){autoSleeping=true;SetPose(Pose.Sleep,false);Say("Уже ночь… Сворачиваюсь клубочком. Если ты ещё занят, пусть всё идёт спокойно 🧡");}
            else if(autoSleeping&&!Companion.Night(now)){autoSleeping=false;SetPose(Pose.Sit,false);Say(Companion.Greeting(now));}
            if(settings.Quiet||pose==Pose.Sleep||busy||!IsVisible||(now-lastActivity).TotalMinutes<8)return;
            if(now>=nextQuestion){Say(companion.CheckIn());nextQuestion=now.AddMinutes(random.Next(30,46));nextAffection=now.AddMinutes(random.Next(16,25));}
            else if(now>=nextAffection){Say("Хочется немного ласки… Погладишь меня? 🧡");nextAffection=now.AddMinutes(random.Next(25,41));}
        }
        void Place(){ApplySize();if(settings.Left==-1){Left=SystemParameters.WorkArea.Right-Width-18;Top=SystemParameters.WorkArea.Bottom-Height-8;}else{Left=settings.Left;Top=settings.Top;}EnsureReachable();}
        void EnsureReachable(){var visible=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);if(double.IsNaN(Left)||double.IsNaN(Top)||!visible.IntersectsWith(new Rect(Left,Top,Width,Height))){Left=SystemParameters.WorkArea.Right-Width-18;Top=SystemParameters.WorkArea.Bottom-Height-8;}}
        void DisplayChanged(object sender,EventArgs e){Dispatcher.BeginInvoke(new Action(Place));}
        void PowerChanged(object sender,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Resume)Dispatcher.BeginInvoke(new Action(delegate{Tick();Say(Companion.Greeting(DateTime.Now));}));}
        void CreateTray(){tray=new Forms.NotifyIcon {Icon=new System.Drawing.Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","fox.ico")),Text="FoxFriend · Лисичка",Visible=true};var m=new Forms.ContextMenuStrip();m.Renderer=new Forms.ToolStripProfessionalRenderer(new TrayColors());m.Items.Add("Показать лисичку",null,delegate{Dispatcher.Invoke(new Action(ShowCompanion));});m.Items.Add("Настройки",null,delegate{Dispatcher.Invoke(new Action(OpenSettings));});m.Items.Add("Выход",null,delegate{Dispatcher.Invoke(new Action(Close));});tray.ContextMenuStrip=m;tray.DoubleClick+=delegate{ShowCompanion();};}
        void OpenMenu(){var menu=new ContextMenu();var chat=new MenuItem{Header=compact?"Показать переписку":"Оставить только поле ответа"};chat.Click+=delegate{SetCloudCompact(!compact);};menu.Items.Add(chat);var hide=new MenuItem{Header="Скрыть лисичку"};hide.Click+=delegate{HideCompanion();};menu.Items.Add(hide);foreach(var p in new[]{Pose.Sit,Pose.Lie,Pose.Sleep}){Pose choice=p;var item=new MenuItem {Header=p==Pose.Sit?"Сядь":p==Pose.Lie?"Полежи":"Ложись спать"};item.Click+=delegate{SetPose(choice);};menu.Items.Add(item);}var settingsItem=new MenuItem{Header="Настройки…"};settingsItem.Click+=delegate{OpenSettings();};menu.Items.Add(settingsItem);var exit=new MenuItem{Header="Выход"};exit.Click+=delegate{Close();};menu.Items.Add(exit);menu.PlacementTarget=pet;menu.IsOpen=true;}
        void OpenSettings(){
            if(settingsWindow!=null){settingsWindow.Activate();return;}
            var w=new Window {Title="FoxFriend · настройки",Icon=Icon,Width=450,Height=Math.Min(750,SystemParameters.WorkArea.Height-30),ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Topmost=true,Background=Color("#FFF9F0"),FontFamily=FontFamily};settingsWindow=w;
            var panel=new StackPanel{Margin=new Thickness(24)};w.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            panel.Children.Add(new TextBlock {Text="Твой маленький компаньон",FontSize=19,FontWeight=FontWeights.SemiBold,Foreground=Color("#704432"),Margin=new Thickness(0,0,0,16)});
            var checkUpdates=new CheckBox{Content="Проверять обновления при запуске",IsChecked=settings.CheckUpdates,Margin=new Thickness(0,0,0,10)};panel.Children.Add(checkUpdates);var updateButton=new Button{Content="Проверить обновления · v"+UpdateService.Current.ToString(3),Margin=new Thickness(0,0,0,18)};updateButton.Click+=delegate{new UpdateWindow{Owner=w,Icon=Icon}.ShowDialog();};panel.Children.Add(updateButton);
            panel.Children.Add(new TextBlock{Text="Как тебя зовут?",Margin=new Thickness(0,0,0,5)});var name=new TextBox{Text=settings.UserName,Padding=new Thickness(8),MaxLength=32,Margin=new Thickness(0,0,0,10)};panel.Children.Add(name);
            panel.Children.Add(new TextBlock{Text="Ласковое имя (можно оставить пустым)",Margin=new Thickness(0,0,0,5)});var petName=new TextBox{Text=settings.PetName,Padding=new Thickness(8),MaxLength=32,Margin=new Thickness(0,0,0,10)};panel.Children.Add(petName);
            var tender=new CheckBox{Content="Иногда обращаться ласково",IsChecked=settings.TenderNames,Margin=new Thickness(0,0,0,15)};panel.Children.Add(tender);
            var sizeLabel=new TextBlock{Text="Размер лисички: "+Math.Round(settings.PetScale*100)+"%"};panel.Children.Add(sizeLabel);double originalScale=settings.PetScale,originalWidth=settings.CloudWidth,originalHeight=settings.CloudHeight;bool saved=false;
            var size=new Slider{Minimum=60,Maximum=140,Value=settings.PetScale*100,TickFrequency=5,IsSnapToTickEnabled=true,Margin=new Thickness(0,5,0,15)};size.ValueChanged+=delegate{settings.PetScale=size.Value/100;sizeLabel.Text="Размер лисички: "+Math.Round(size.Value)+"%";ApplySize();};panel.Children.Add(size);
            var widthLabel=new TextBlock{Text="Ширина облачка: "+settings.CloudWidth};panel.Children.Add(widthLabel);var cloudWidth=new Slider{Minimum=240,Maximum=520,Value=settings.CloudWidth,TickFrequency=10,IsSnapToTickEnabled=true,Margin=new Thickness(0,5,0,15)};cloudWidth.ValueChanged+=delegate{settings.CloudWidth=cloudWidth.Value;widthLabel.Text="Ширина облачка: "+Math.Round(cloudWidth.Value);ApplySize();};panel.Children.Add(cloudWidth);
            var heightLabel=new TextBlock{Text="Высота переписки: "+settings.CloudHeight};panel.Children.Add(heightLabel);var cloudHeight=new Slider{Minimum=100,Maximum=360,Value=settings.CloudHeight,TickFrequency=10,IsSnapToTickEnabled=true,Margin=new Thickness(0,5,0,15)};cloudHeight.ValueChanged+=delegate{settings.CloudHeight=cloudHeight.Value;heightLabel.Text="Высота переписки: "+Math.Round(cloudHeight.Value);ApplySize();};panel.Children.Add(cloudHeight);
            panel.Children.Add(new TextBlock{Text="Отправка сообщений",Margin=new Thickness(0,0,0,8)});var modes=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,18)};var enter=new RadioButton{Content="Enter",IsChecked=settings.SendBy==SendMode.Enter,Margin=new Thickness(0,0,12,0)};var arrow=new RadioButton{Content="Стрелка",IsChecked=settings.SendBy==SendMode.Arrow,Margin=new Thickness(0,0,12,0)};var both=new RadioButton{Content="Enter и Стрелка",IsChecked=settings.SendBy==SendMode.Both};modes.Children.Add(enter);modes.Children.Add(arrow);modes.Children.Add(both);panel.Children.Add(modes);
            var startup=new CheckBox{Content="Появляться при входе в Windows",IsChecked=Startup.Enabled,Margin=new Thickness(0,0,0,12)};panel.Children.Add(startup);
            var quiet=new CheckBox{Content="Тихий режим: не начинать разговор самой",IsChecked=settings.Quiet,Margin=new Thickness(0,0,0,12)};panel.Children.Add(quiet);
            var ai=new CheckBox{Content="Свободное общение через локальный Ollama",IsChecked=settings.LocalAI,Margin=new Thickness(0,0,0,8)};panel.Children.Add(ai);
            panel.Children.Add(new TextBlock{Text="Имя уже установленной локальной модели",FontSize=12,Margin=new Thickness(0,0,0,4)});var model=new TextBox{Text=settings.Model,Padding=new Thickness(8),Margin=new Thickness(0,0,0,10)};panel.Children.Add(model);
            panel.Children.Add(new TextBlock{Text="FoxMind работает без интернета. Имя и настройки сохраняются на этом компьютере; переписка — только до выхода. Shift+Enter всегда добавляет новую строку. Кнопки − и × оставляют только поле ответа; лисичка остаётся рядом. Скрыть её можно правой кнопкой мыши. Вернуть её можно через значок лисы возле часов.\n\nСон: 00:00–07:00 по часам компьютера. Ollama — необязательный внешний режим; подключение только к 127.0.0.1:11434.",TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=Color("#806757"),Margin=new Thickness(0,0,0,18)});
            var save=new Button {Content="Сохранить",Padding=new Thickness(12,8,12,8)};save.Click+=delegate{try{string clean=Names.Clean(name.Text),petClean=Names.Clean(petName.Text);if((name.Text.Trim().Length>0&&clean.Length==0)||(petName.Text.Trim().Length>0&&petClean.Length==0)){MessageBox.Show(w,"Укажите имя буквами, от 2 до 32 символов.");return;}Startup.Set(startup.IsChecked==true);settings.UserName=clean;settings.PetName=petClean;companion.SetName(clean);awaitingName=clean.Length==0;settings.TenderNames=tender.IsChecked==true;settings.SendBy=enter.IsChecked==true?SendMode.Enter:arrow.IsChecked==true?SendMode.Arrow:SendMode.Both;ApplySendMode();settings.CheckUpdates=checkUpdates.IsChecked==true;settings.Quiet=quiet.IsChecked==true;settings.LocalAI=ai.IsChecked==true;settings.Model=string.IsNullOrWhiteSpace(model.Text)?"qwen2.5:3b":model.Text.Trim();settings.Save();saved=true;w.Close();}catch(Exception ex){MessageBox.Show(w,"Не удалось сохранить настройки: "+ex.Message);}};panel.Children.Add(save);w.Closed+=delegate{settingsWindow=null;if(!saved){settings.PetScale=originalScale;settings.CloudWidth=originalWidth;settings.CloudHeight=originalHeight;ApplySize();}};w.Show();
        }
    }
    sealed class TrayColors : Forms.ProfessionalColorTable {
        public override System.Drawing.Color MenuItemSelected {get{return System.Drawing.Color.FromArgb(248,232,217);}}
        public override System.Drawing.Color MenuItemBorder {get{return System.Drawing.Color.FromArgb(232,198,167);}}
        public override System.Drawing.Color ToolStripDropDownBackground {get{return System.Drawing.Color.FromArgb(255,249,240);}}
        public override System.Drawing.Color ImageMarginGradientBegin {get{return ToolStripDropDownBackground;}}
        public override System.Drawing.Color ImageMarginGradientMiddle {get{return ToolStripDropDownBackground;}}
        public override System.Drawing.Color ImageMarginGradientEnd {get{return ToolStripDropDownBackground;}}
    }
    static class Program {
        [STAThread] static void Main(string[] args){
            if(args.Contains("--render-check")){new FoxWindow(true).RenderChecks(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tests","renders"));return;}
            if(args.Contains("--enable-startup")){Startup.Set(true);return;}
            if(args.Contains("--disable-startup")){Startup.Set(false);return;}
            string suffix=args.Contains("--smoke")?".Smoke":"";bool first;using(var mutex=new Mutex(true,"Local\\Lisichka.DesktopPet"+suffix,out first)){
                if(!first){if(suffix.Length>0){Environment.ExitCode=1;return;}try{using(var show=EventWaitHandle.OpenExisting("Local\\FoxFriend.Show"))show.Set();}catch(WaitHandleCannotBeOpenedException){}return;}
                try{var app=new Application();using(var theme=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("FoxFriend.Theme.xaml"))app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(theme));var window=new FoxWindow();if(args.Contains("--smoke")){window.DiagnosticMode=true;window.ShowInTaskbar=true;window.Loaded+=delegate{window.SmokeCheck();};}
                    using(var show=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\FoxFriend.Show"+suffix))using(var stop=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\FoxFriend.Close"+suffix)){
                        bool alive=true;ThreadPool.QueueUserWorkItem(delegate{try{while(alive){int signal=WaitHandle.WaitAny(new WaitHandle[]{show,stop},500);if(signal==WaitHandle.WaitTimeout)continue;if(!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(new Action(delegate{if(signal==0)window.ShowCompanion();else window.Close();}));}}catch(ObjectDisposedException){}});app.Run(window);alive=false;
                    }
                }
                catch(Exception e){Directory.CreateDirectory(Settings.Folder);File.WriteAllText(Path.Combine(Settings.Folder,"error.log"),e.ToString());MessageBox.Show("Не удалось открыть лисичку. Подробности: "+Path.Combine(Settings.Folder,"error.log"),"Лисичка");}
            }
        }
    }
}
