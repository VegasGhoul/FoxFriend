using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
class IconBuilder {
 [STAThread] static void Main(string[] args){var source=new BitmapImage(new Uri(Path.GetFullPath(args[0])));int[] sizes={16,24,32,48,64,128,256};var chunks=new List<byte[]>();foreach(int size in sizes){var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){double scale=Math.Min((double)size/source.PixelWidth,(double)size/source.PixelHeight);double w=source.PixelWidth*scale,h=source.PixelHeight*scale;dc.DrawImage(source,new Rect((size-w)/2,(size-h)/2,w,h));}var render=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);render.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(render));using(var stream=new MemoryStream()){png.Save(stream);chunks.Add(stream.ToArray());}}using(var w=new BinaryWriter(File.Create(args[1]))){w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(chunks[i].Length);w.Write(offset);offset+=chunks[i].Length;}foreach(var chunk in chunks)w.Write(chunk);}}
}
