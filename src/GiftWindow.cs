using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace Lisichka {
    public sealed class GiftWindow : Window {
        readonly Grid box; readonly Canvas lid; readonly Image fox; readonly TextBlock label; readonly Button tap;
        readonly RotateTransform shake=new RotateTransform();readonly TranslateTransform rise=new TranslateTransform(0,80);readonly ScaleTransform growth=new ScaleTransform(.6,.6);
        int clicks;bool opening;
        public int ClickCount {get{return clicks;}}
        public GiftWindow(BitmapSource sprite){
            Title="FoxFriend · маленький подарок";Width=380;Height=410;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=true;WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var root=new Grid{ClipToBounds=true};Content=root;
            var card=new Border{CornerRadius=new CornerRadius(25),Background=Brush("#FFF9F0"),BorderBrush=Brush("#EDD0B5"),BorderThickness=new Thickness(1),Margin=new Thickness(12)};root.Children.Add(card);
            var panel=new Grid{Margin=new Thickness(24)};root.Children.Add(panel);
            label=new TextBlock{Text="Внутри — маленький друг 🧡\nНажмите на подарок 3 раза",TextAlignment=TextAlignment.Center,FontSize=18,Foreground=Brush("#80503C"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,10,0,0)};panel.Children.Add(label);
            var viewport=new Canvas{Width=332,Height=338,Clip=new RectangleGeometry(new Rect(55,65,225,249)),VerticalAlignment=VerticalAlignment.Top};panel.Children.Add(viewport);
            fox=new Image{Source=sprite,Width=180,Height=200,Stretch=Stretch.Uniform,Opacity=0,RenderTransformOrigin=new Point(.5,1)};Canvas.SetLeft(fox,76);Canvas.SetTop(fox,140);
            var transform=new TransformGroup();transform.Children.Add(growth);transform.Children.Add(rise);fox.RenderTransform=transform;viewport.Children.Add(fox);
            box=new Grid{Width=180,Height=170,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,48),RenderTransform=shake,RenderTransformOrigin=new Point(.5,1)};
            var body=new Border{Height=116,CornerRadius=new CornerRadius(5,5,16,16),Background=new LinearGradientBrush(Colors.PeachPuff,Color.FromRgb(235,150,99),90),VerticalAlignment=VerticalAlignment.Bottom};box.Children.Add(body);
            box.Children.Add(new Border{Width=26,Height=116,Background=Brush("#A7CAC1"),VerticalAlignment=VerticalAlignment.Bottom});
            lid=new Canvas{Width=200,Height=70,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(-10,32,-10,0),RenderTransform=new TransformGroup()};
            var left=new Ellipse{Width=65,Height=32,Stroke=Brush("#74A99D"),StrokeThickness=9,RenderTransform=new RotateTransform(20)};Canvas.SetLeft(left,45);Canvas.SetTop(left,0);lid.Children.Add(left);
            var right=new Ellipse{Width=65,Height=32,Stroke=Brush("#74A99D"),StrokeThickness=9,RenderTransform=new RotateTransform(-20)};Canvas.SetLeft(right,97);Canvas.SetTop(right,20);lid.Children.Add(right);
            var top=new Border{Width=200,Height=32,CornerRadius=new CornerRadius(7),Background=Brush("#F6B989")};Canvas.SetTop(top,32);lid.Children.Add(top);
            var ribbon=new Border{Width=28,Height=32,Background=Brush("#A7CAC1")};Canvas.SetLeft(ribbon,86);Canvas.SetTop(ribbon,32);lid.Children.Add(ribbon);box.Children.Add(lid);panel.Children.Add(box);
            tap=new Button{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Width=210,Height=185,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,43),Cursor=System.Windows.Input.Cursors.Hand,ToolTip="Открыть подарок"};
            var hitTemplate=new ControlTemplate(typeof(Button));var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);hitTemplate.VisualTree=hit;tap.Template=hitTemplate;tap.Click+=async delegate{await Tap();};panel.Children.Add(tap);
            var skip=new Button{Content="Пропустить знакомство",Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=11,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom};skip.Click+=delegate{DialogResult=true;};panel.Children.Add(skip);
        }
        static SolidColorBrush Brush(string color){return (SolidColorBrush)new BrushConverter().ConvertFromString(color);}
        static DoubleAnimation Anim(double from,double to,double time){return new DoubleAnimation(from,to,TimeSpan.FromSeconds(time)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}};}
        public async Task Tap(){if(opening)return;clicks++;label.Text=clicks<3?"Кто-то шуршит внутри…\nЕщё "+(3-clicks)+(clicks==1?" нажатия":" нажатие"):"Привет! Я твоя Лисичка 🧡";
            var a=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromSeconds(.4)};a.KeyFrames.Add(new EasingDoubleKeyFrame(-7,KeyTime.FromPercent(.15)));a.KeyFrames.Add(new EasingDoubleKeyFrame(7,KeyTime.FromPercent(.4)));a.KeyFrames.Add(new EasingDoubleKeyFrame(-4,KeyTime.FromPercent(.7)));a.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromPercent(1)));shake.BeginAnimation(RotateTransform.AngleProperty,a);
            if(clicks<3)return;opening=true;tap.IsEnabled=false;await Task.Delay(350);if(!IsVisible)return;
            var lidTransform=new TransformGroup();var up=new TranslateTransform();var turn=new RotateTransform(0,100,50);lidTransform.Children.Add(turn);lidTransform.Children.Add(up);lid.RenderTransform=lidTransform;up.BeginAnimation(TranslateTransform.YProperty,Anim(0,-100,.65));turn.BeginAnimation(RotateTransform.AngleProperty,Anim(0,-22,.65));lid.BeginAnimation(OpacityProperty,Anim(1,0,.8));
            fox.BeginAnimation(OpacityProperty,Anim(0,1,.3));rise.BeginAnimation(TranslateTransform.YProperty,Anim(80,-26,1));growth.BeginAnimation(ScaleTransform.ScaleXProperty,Anim(.6,1,1));growth.BeginAnimation(ScaleTransform.ScaleYProperty,Anim(.6,1,1));await Task.Delay(1200);if(!IsVisible)return;
            box.BeginAnimation(OpacityProperty,Anim(1,0,.45));await Task.Delay(600);if(!IsVisible)return;BeginAnimation(OpacityProperty,Anim(1,0,.45));await Task.Delay(450);if(IsVisible)DialogResult=true;
        }
    }
}
