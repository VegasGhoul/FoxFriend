using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Lisichka {
    // Interpolate PREMULTIPLIED color and alpha in one surface. Fading two
    // layered Images independently makes opaque fur 75% opaque at midpoint.
    public sealed class SpriteBlend : Animatable {
        public const int Width=280, Height=283;
        readonly byte[][] frames;
        byte[] start,target,output,warped;int currentFrame;bool tailActive;readonly double[][] tailWeights=new double[3][];
        public WriteableBitmap Bitmap {get;private set;}
        public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register("Progress",typeof(double),typeof(SpriteBlend),new PropertyMetadata(0.0,OnProgressChanged));
        public double Progress {get{return (double)GetValue(ProgressProperty);}set{SetValue(ProgressProperty,value);}}
        public static readonly DependencyProperty TailPhaseProperty=DependencyProperty.Register("TailPhase",typeof(double),typeof(SpriteBlend),new PropertyMetadata(0.0,OnTailChanged));
        public double TailPhase {get{return (double)GetValue(TailPhaseProperty);}set{SetValue(TailPhaseProperty,value);}}
        static void OnTailChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){var s=(SpriteBlend)d;s.Render(s.Progress);}
        static void OnProgressChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){((SpriteBlend)d).Render((double)e.NewValue);}
        protected override Freezable CreateInstanceCore(){throw new NotSupportedException("SpriteBlend is a live renderer");}
        public SpriteBlend(BitmapSource[] sources) {
            frames=new byte[sources.Length][];
            for(int i=0;i<sources.Length;i++) {
                double w=i<3?248:270,h=w*sources[i].PixelHeight/sources[i].PixelWidth;
                if(h>Height){w*=Height/h;h=Height;}
                var visual=new DrawingVisual();using(var draw=visual.RenderOpen())draw.DrawImage(sources[i],new Rect((Width-w)/2,Height-h,w,h));
                var canvas=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32);canvas.Render(visual);
                frames[i]=new byte[Width*Height*4];canvas.CopyPixels(frames[i],Width*4,0);
            }
            for(int pose=0;pose<3;pose++){tailWeights[pose]=new double[Width*Height];for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){double side=Smooth((x-(pose==2?142:176))/44.0),height=pose==2?Smooth((y-174)/45.0):Smooth((274-y)/65.0)*Smooth((y-70)/35.0);tailWeights[pose][y*Width+x]=side*height;}}
            output=(byte[])frames[0].Clone();warped=new byte[output.Length];start=(byte[])output.Clone();target=frames[0];Bitmap=new WriteableBitmap(Width,Height,96,96,PixelFormats.Pbgra32,null);Render(1);
        }
        static double Smooth(double x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        int TailPose {get{return currentFrame<3?0:currentFrame<5?1:2;}}
        public void SetTailActive(bool active){tailActive=active;BeginAnimation(TailPhaseProperty,null);if(!active)return;var wave=new DoubleAnimation(0,Math.PI*2,TimeSpan.FromSeconds(TailPose==0?1.8:TailPose==1?3.2:6.5)){RepeatBehavior=RepeatBehavior.Forever};Timeline.SetDesiredFrameRate(wave,30);BeginAnimation(TailPhaseProperty,wave);}
        public void Transition(int frame,double seconds) {
            var snapshot=(byte[])output.Clone();BeginAnimation(ProgressProperty,null);start=snapshot;target=frames[frame];int oldPose=TailPose;currentFrame=frame;if(oldPose!=TailPose&&tailActive)SetTailActive(true);
            if(seconds<=0){Progress=1;Render(1);return;}
            Progress=0;Render(0);
            BeginAnimation(ProgressProperty,new DoubleAnimation(0,1,TimeSpan.FromSeconds(seconds)){EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.HoldEnd});
        }
        void Render(double amount) {if(output==null||Bitmap==null)return;double t=Math.Max(0,Math.Min(1,amount));if(t>=1)Array.Copy(target,output,output.Length);else Mix(start,target,output,t);if(tailActive){WarpTail(output,warped,TailPose,Math.Sin(TailPhase));Bitmap.WritePixels(new Int32Rect(0,0,Width,Height),warped,Width*4,0);}else Bitmap.WritePixels(new Int32Rect(0,0,Width,Height),output,Width*4,0);}
        void WarpTail(byte[] source,byte[] destination,int pose,double wave){Array.Copy(source,destination,source.Length);double amplitude=pose==0?4.0:pose==1?2.2:.65;for(int y=70;y<Height;y++)for(int x=142;x<Width;x++){double weight=tailWeights[pose][y*Width+x];if(weight<=0)continue;double sx=x-wave*amplitude*weight;if(sx<0||sx>=Width-1)continue;int a=(int)sx;double f=sx-a;int to=(y*Width+x)*4,from=(y*Width+a)*4;for(int c=0;c<4;c++)destination[to+c]=(byte)Math.Round(source[from+c]*(1-f)+source[from+4+c]*f);}}
        public void VerifyTail(string folder){int[] indices={0,3,5};for(int pose=0;pose<3;pose++){var a=new byte[output.Length];var b=new byte[output.Length];WarpTail(frames[indices[pose]],a,pose,-1);WarpTail(frames[indices[pose]],b,pose,1);int changed=0;for(int p=0;p<a.Length;p+=4){if(a[p]!=b[p]||a[p+3]!=b[p+3])changed++;int x=(p/4)%Width;if(x<142&&a[p]!=frames[indices[pose]][p])throw new Exception("Tail changed face/body region");}if(changed<50)throw new Exception("Tail has no visible motion");foreach(int phase in new[]{0,1}){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(Width,Height,96,96,PixelFormats.Pbgra32,null,phase==0?a:b,Width*4)));using(var f=File.Create(Path.Combine(folder,"Tail-"+pose+"-"+phase+".png")))png.Save(f);}}File.WriteAllText(Path.Combine(folder,"tail-result.txt"),"PASS: isolated tail displacement in all three poses; playful 4 px / 1.8 s, resting 2.2 px / 3.2 s, sleeping 0.65 px / 6.5 s.");}
        static void Mix(byte[] a,byte[] b,byte[] result,double t){for(int i=0;i<result.Length;i++)result[i]=(byte)Math.Round(a[i]+(b[i]-a[i])*t);}
        public void VerifyBlink(string folder) {
            foreach(int open in new[]{0,3}){
                byte[] a=frames[open],b=frames[open+1],middle=new byte[a.Length];
                for(int i=3;i<a.Length;i+=4)if(a[i]!=b[i])throw new Exception("Blink changed silhouette alpha");
                foreach(double t in new[]{0.0,.25,.5,.75,1.0}){
                    Mix(a,b,middle,t);for(int i=3;i<a.Length;i+=4)if(middle[i]!=a[i])throw new Exception("Blink transparency dip");
                    var bitmap=BitmapSource.Create(Width,Height,96,96,PixelFormats.Pbgra32,null,middle,Width*4);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(folder,"Blink-"+open+"-"+((int)(t*100))+".png")))png.Save(f);
                }
            }
            File.WriteAllText(Path.Combine(folder,"blink-result.txt"),"PASS: all 79240 pixels preserve alpha at 0%, 25%, 50%, 75%, 100% for seated and lying blink. Single premultiplied surface, no fading background.");
        }
        // Only the eyelids come from the closed-eye artwork. Keep the original
        // body, outline, scale and alpha byte-for-byte, with feathered patch edges.
        public static BitmapSource EyesOnly(BitmapSource original,BitmapSource atlas,int originX,int originY,double[,] eyes) {
            var baseImage=new FormatConvertedBitmap(original,PixelFormats.Pbgra32,null,0);var sheet=new FormatConvertedBitmap(atlas,PixelFormats.Pbgra32,null,0);
            int w=baseImage.PixelWidth,h=baseImage.PixelHeight,aw=sheet.PixelWidth;var pixels=new byte[w*h*4];var source=new byte[aw*sheet.PixelHeight*4];baseImage.CopyPixels(pixels,w*4,0);sheet.CopyPixels(source,aw*4,0);
            for(int eye=0;eye<eyes.GetLength(0);eye++)for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                double gx=x+originX,gy=y+originY,dx=(gx-eyes[eye,0])/eyes[eye,2],dy=(gy-eyes[eye,1])/eyes[eye,3];double distance=Math.Sqrt(dx*dx+dy*dy);if(distance>=1)continue;
                double weight=Math.Min(1,(1-distance)/.16);weight=weight*weight*(3-2*weight);
                int sx=(int)Math.Round(gx+eyes[eye,4]),sy=(int)Math.Round(gy+eyes[eye,5]);if(sx<0||sy<0||sx>=aw||sy>=sheet.PixelHeight)continue;
                int p=(y*w+x)*4,s=(sy*aw+sx)*4;if(pixels[p+3]==0||source[s+3]==0)continue;
                for(int c=0;c<3;c++){double color=source[s+c]*(double)pixels[p+3]/source[s+3];pixels[p+c]=(byte)Math.Round(pixels[p+c]*(1-weight)+color*weight);}
            }
            var result=BitmapSource.Create(w,h,96,96,PixelFormats.Pbgra32,null,pixels,w*4);result.Freeze();return result;
        }
    }
}
