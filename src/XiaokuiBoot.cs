using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Media;

[assembly: AssemblyTitle("小葵 OS · Codex 启动动画")]
[assembly: AssemblyDescription("Xiaokui OS — Sakura Boot. An independent local startup animation.")]
[assembly: AssemblyVersion("1.5.0.0")]

namespace XiaokuiOS {
  internal static class Native {
    internal delegate bool EnumProc(IntPtr window, IntPtr param);
    internal delegate void EventProc(IntPtr hook,uint eventType,IntPtr window,int objectId,int childId,uint thread,uint time);
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; public int Width {get{return Right-Left;}} public int Height {get{return Bottom-Top;}} }
    [StructLayout(LayoutKind.Sequential)] internal struct ScreenPoint { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size;public Rect Monitor,Work;public uint Flags; }
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out ScreenPoint point);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(ScreenPoint point,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback,IntPtr param);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr window,int mode);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window,uint kind);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("gdi32.dll")] private static extern IntPtr CreatePolygonRgn([In] ScreenPoint[] points,int count,int mode);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region,int x,int y);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window,IntPtr region);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,EventProc callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
    [DllImport("kernel32.dll")] internal static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out Rect rect,int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmGetInt(IntPtr window,int attribute,out int value,int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    internal static string ImagePath(IntPtr window) {
      uint pid; GetWindowThreadProcessId(window,out pid);
      IntPtr handle=OpenProcess(0x1000,false,pid); if(handle==IntPtr.Zero)return "";
      try { var buffer=new StringBuilder(4096);int size=buffer.Capacity;return QueryFullProcessImageName(handle,0,buffer,ref size)?buffer.ToString():""; }
      finally { CloseHandle(handle); }
    }
    internal static bool Official(string path) {
      return path.IndexOf(@"\WindowsApps\OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)>=0 && path.EndsWith(@"\app\ChatGPT.exe",StringComparison.OrdinalIgnoreCase);
    }
    internal static IntPtr FindClient() {
      IntPtr found=IntPtr.Zero,foreground=GetForegroundWindow();
      EnumWindows(delegate(IntPtr h,IntPtr unused) {
        if(!IsWindowVisible(h)||GetWindow(h,4)!=IntPtr.Zero)return true;
        Rect bounds;if(!GetWindowRect(h,out bounds)||bounds.Width<400||bounds.Height<300)return true;
        if(!Official(ImagePath(h)))return true;
        if(found==IntPtr.Zero)found=h;
        if(h==foreground){found=h;return false;}return true;
      },IntPtr.Zero);return found;
    }
    internal static Rect Bounds(IntPtr window) {
      Rect rect;if(DwmGetWindowAttribute(window,9,out rect,Marshal.SizeOf(typeof(Rect)))!=0)GetWindowRect(window,out rect);return rect;
    }
    internal static bool Responsive(IntPtr window) {
      IntPtr ignored;return IsWindow(window)&&IsWindowVisible(window)&&SendMessageTimeout(window,0,IntPtr.Zero,IntPtr.Zero,2,120,out ignored)!=IntPtr.Zero;
    }
    internal static bool Presentable(IntPtr window) {
      if(window==IntPtr.Zero||!IsWindow(window)||!IsWindowVisible(window)||IsIconic(window))return false;
      int cloaked;if(DwmGetInt(window,14,out cloaked,4)==0&&cloaked!=0)return false;
      Rect b=Bounds(window);return b.Width>=400&&b.Height>=300;
    }
    internal static bool Equal(Rect a,Rect b,int tolerance=0){return Math.Abs(a.Left-b.Left)<=tolerance&&Math.Abs(a.Top-b.Top)<=tolerance&&Math.Abs(a.Right-b.Right)<=tolerance&&Math.Abs(a.Bottom-b.Bottom)<=tolerance;}
    internal static string Describe(Rect b){return b.Left+","+b.Top+","+b.Width+","+b.Height;}
    internal static bool IsTopmost(IntPtr window){return (GetWindowLongPtr(window,-20).ToInt64()&8)!=0;}
    internal static bool Above(IntPtr front,IntPtr back) {
      if(front==IntPtr.Zero||back==IntPtr.Zero||front==back)return false;
      IntPtr previous=GetWindow(back,3);
      for(int i=0;previous!=IntPtr.Zero&&i<512;i++,previous=GetWindow(previous,3))if(previous==front)return true;
      return false;
    }
    internal static bool RaiseCover(IntPtr window){return SetWindowPos(window,new IntPtr(-1),0,0,0,0,0x213);}
    internal static bool PrepareCoverRegion(IntPtr window) {
      Rect bounds;if(!GetWindowRect(window,out bounds)||bounds.Width<10||bounds.Height<10)return false;
      // Leave one physical pixel out of the corner. A nonrectangular cover lets
      // Chromium continue painting while it is behind the startup animation.
      int w=bounds.Width,h=bounds.Height;
      ScreenPoint[] outline={new ScreenPoint{X=1,Y=0},new ScreenPoint{X=w,Y=0},new ScreenPoint{X=w,Y=h},new ScreenPoint{X=0,Y=h},new ScreenPoint{X=0,Y=1},new ScreenPoint{X=1,Y=1}};
      IntPtr region=CreatePolygonRgn(outline,outline.Length,2);
      if(region==IntPtr.Zero)return false;
      if(SetWindowRgn(window,region,true)==0){DeleteObject(region);return false;}
      // USER32 owns the region after SetWindowRgn succeeds.
      return CoverRegionReady(window);
    }
    internal static bool CoverRegionReady(IntPtr window) {
      IntPtr query=CreateRectRgn(0,0,0,0);if(query==IntPtr.Zero)return false;
      try{return GetWindowRgn(window,query)==3&&!PtInRegion(query,0,0)&&PtInRegion(query,1,0)&&PtInRegion(query,0,1)&&PtInRegion(query,6,6);}
      finally{DeleteObject(query);}
    }
    internal static Rect Centered(Rect work) {
      // A shared physical-pixel rectangle exists before the client creates a
      // window. Keep the composition's aspect ratio and leave desktop margins.
      double scale=Math.Min(1,Math.Min(work.Width*.86/1600,work.Height*.86/1000));
      int width=(int)(1600*scale),height=(int)(1000*scale);
      int left=work.Left+(work.Width-width)/2,top=work.Top+(work.Height-height)/2;
      return new Rect{Left=left,Top=top,Right=left+width,Bottom=top+height};
    }
    internal static Rect LaunchWorkArea() {
      ScreenPoint point;GetCursorPos(out point);
      var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
      if(!GetMonitorInfo(MonitorFromPoint(point,2),ref info))throw new InvalidOperationException("无法读取当前屏幕的可用区域。");
      return info.Work;
    }
    internal static bool PlaceVisible(IntPtr window,Rect wanted) {
      Rect outer;if(!GetWindowRect(window,out outer))return false;
      Rect visible=Bounds(window);
      // GetWindowRect includes invisible resize borders; match the DWM frame.
      return SetWindowPos(window,IntPtr.Zero,wanted.Left-(visible.Left-outer.Left),wanted.Top-(visible.Top-outer.Top),
        wanted.Width+outer.Width-visible.Width,wanted.Height+outer.Height-visible.Height,0x4014);
    }
    internal static void Activate(IntPtr window) { if(window==IntPtr.Zero||!IsWindow(window))return;if(IsIconic(window))ShowWindowAsync(window,9);SetForegroundWindow(window); }
  }

  internal static class Assets {
    internal static byte[] Read(string name) {
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
        if(stream==null)throw new IOException("缺少内置素材："+name);
        using(var data=new MemoryStream()){stream.CopyTo(data);return data.ToArray();}
      }
    }
    internal static BitmapSource Logo() {
      var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=new MemoryStream(Read("logo.png"));image.EndInit();image.Freeze();return image;
    }
  }

  internal sealed class Audio : IDisposable {
    private readonly Dictionary<string,SoundPlayer> players=new Dictionary<string,SoundPlayer>();
    internal bool Enabled=true;
    internal Audio() { foreach(string name in new[]{"boot","tick","ready","exit"}) { try {var player=new SoundPlayer(new MemoryStream(Assets.Read(name+".wav")));player.Load();players.Add(name,player);}catch{} } }
    internal void Play(string name) { if(!Enabled)return;try{players[name].Play();}catch{} }
    internal void Toggle(){Enabled=!Enabled;if(!Enabled)Stop();}
    internal void Stop(){foreach(var player in players.Values)try{player.Stop();}catch{}}
    public void Dispose(){Stop();foreach(var player in players.Values)player.Dispose();}
  }

  internal sealed class Tile { internal double X,Y,DX,DY,Delay,Size;internal Brush Brush; }
  internal sealed class SakuraScene : FrameworkElement {
    internal double Time;
    internal int RenderedFrames;
    internal bool Muted,Reduced;
    private readonly BitmapSource logo=Assets.Logo();
    private readonly List<Tile> tiles=new List<Tile>();
    private readonly Dictionary<string,FormattedText> textCache=new Dictionary<string,FormattedText>();
    private readonly Brush pink=Brush("#F49ABD"),bright=Brush("#FFD4E4"),white=Brush("#F3EDF7"),muted=Brush("#807889");
    private readonly Random random=new Random(318);
    private readonly double[] stars=new double[180];
    // 所有视觉节点共享同一时间轴；12.2 秒显示完成，12.5 秒覆盖层无条件退出。
    internal const double Duration=12.5,FadeStart=10.5,ProgressComplete=12.2;
    internal static double Smooth(double x){x=Clamp(x);return x*x*(3-2*x);}
    internal static double ProgressAt(double t){return Clamp(t/ProgressComplete);}
    internal static int StageAt(double t){return t<2.5?0:t<5?1:t<7.5?2:3;}
    internal static double BackgroundAlphaAt(double t){return 1-Smooth((t-10.95)/1.55);}
    internal static double DecorationAlphaAt(double t){return 1-Smooth((t-FadeStart)/.55);}
    internal static double LogoAlphaAt(double t){return 1-Smooth((t-10.8)/.8);}
    internal static double ProgressAlphaAt(double t){return 1-Smooth((t-ProgressComplete)/.3);}
    internal static double CaptionAlphaAt(double t){
      int stage=StageAt(t);double start=stage*2.5,end=stage<3?(stage+1)*2.5:FadeStart;
      return Smooth((t-start)/.22)*(stage<3?Smooth((end-t)/.18):1-Smooth((t-FadeStart)/.4));
    }
    internal SakuraScene() {
      Focusable=true;ClipToBounds=true;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
      for(int i=0;i<stars.Length;i++)stars[i]=random.NextDouble();
      var converted=new FormatConvertedBitmap(logo,PixelFormats.Bgra32,null,0);int stride=converted.PixelWidth*4;var data=new byte[stride*converted.PixelHeight];converted.CopyPixels(data,stride,0);
      int step=29;
      for(int y=step/2;y<converted.PixelHeight;y+=step)for(int x=step/2;x<converted.PixelWidth;x+=step){
        int p=y*stride+x*4;if(data[p+3]<100)continue;
        var color=Color.FromArgb((byte)Math.Min(215,(int)data[p+3]),data[p+2],data[p+1],data[p]);var brush=new SolidColorBrush(color);brush.Freeze();
        tiles.Add(new Tile{X=(double)x/converted.PixelWidth,Y=(double)y/converted.PixelHeight,DX=(random.NextDouble()-.5)*1250,DY=(random.NextDouble()-.5)*750,Delay=random.NextDouble()*.32,Size=step*1000.0/converted.PixelWidth,Brush=brush});
      }
    }
    internal static Brush Brush(string hex){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();return b;}
    internal static double Clamp(double x){return Math.Max(0,Math.Min(1,x));}
    internal static double Ease(double x){x=Clamp(x);return 1-Math.Pow(1-x,3);}
    private static Pen Pen(Brush brush,double width){var p=new Pen(brush,width);p.Freeze();return p;}
    private void Line(DrawingContext dc,Brush brush,double width,double x1,double y1,double x2,double y2){dc.DrawLine(Pen(brush,width),new Point(x1,y1),new Point(x2,y2));}
    private FormattedText Text(string value,double size,Brush color,bool mono,bool bold) {
      string key=value+"|"+size+"|"+color+"|"+mono+"|"+bold;FormattedText result;
      if(!textCache.TryGetValue(key,out result)){
        result=new FormattedText(value,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily(mono?"Cascadia Mono, Consolas":"Microsoft YaHei UI, Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,color,1.0);
        textCache.Add(key,result);
      }return result;
    }
    private void Label(DrawingContext dc,string value,double x,double y,double size,Brush color,bool center=false,bool mono=false,bool bold=false) {
      var text=Text(value,size,color,mono,bold);dc.DrawText(text,new Point(center?x-text.Width/2:x,y));
    }
    private void Arc(DrawingContext dc,double radius,double start,double length,Brush color,double thickness,double cx=800,double cy=438) {
      var geometry=new StreamGeometry();using(var g=geometry.Open()){
        double a=start*Math.PI/180,b=(start+length)*Math.PI/180;
        g.BeginFigure(new Point(cx+radius*Math.Cos(a),cy+radius*Math.Sin(a)),false,false);
        g.ArcTo(new Point(cx+radius*Math.Cos(b),cy+radius*Math.Sin(b)),new Size(radius,radius),0,Math.Abs(length)>180,length>0?SweepDirection.Clockwise:SweepDirection.Counterclockwise,true,false);
      }geometry.Freeze();dc.DrawGeometry(null,Pen(color,thickness),geometry);
    }
    private void Flower(DrawingContext dc,double x,double y,double scale,double alpha) {
      dc.PushOpacity(alpha);dc.PushTransform(new TranslateTransform(x,y));dc.PushTransform(new ScaleTransform(scale,scale));
      for(int i=0;i<5;i++){dc.PushTransform(new RotateTransform(i*72));dc.DrawRectangle(pink,null,new Rect(-3,-12,6,7));dc.DrawRectangle(pink,null,new Rect(-5,-10,10,5));dc.Pop();}
      dc.DrawRectangle(bright,null,new Rect(-2,-2,4,4));dc.Pop();dc.Pop();dc.Pop();
    }
    protected override void OnRender(DrawingContext dc) {
      base.OnRender(dc);RenderedFrames++;double t=Time,scale=Math.Min(ActualWidth/1600,ActualHeight/1000);if(scale<=0)return;
      dc.PushOpacity(BackgroundAlphaAt(t));
      dc.DrawRectangle(Brush("#080810"),null,new Rect(0,0,ActualWidth,ActualHeight));
      dc.Pop();
      dc.PushTransform(new TranslateTransform((ActualWidth-1600*scale)/2,(ActualHeight-1000*scale)/2));dc.PushTransform(new ScaleTransform(scale,scale));
      dc.PushOpacity(BackgroundAlphaAt(t));
      var glow=new RadialGradientBrush();glow.GradientOrigin=new Point(.5,.44);glow.Center=new Point(.5,.44);glow.RadiusX=.55;glow.RadiusY=.65;glow.GradientStops.Add(new GradientStop(Color.FromRgb(31,20,39),0));glow.GradientStops.Add(new GradientStop(Color.FromRgb(8,8,16),1));dc.DrawRectangle(glow,null,new Rect(0,0,1600,1000));
      dc.Pop();
      double awaken=Ease((t-.18)/1.2),structure=Ease((t-.9)/1.2);
      dc.PushOpacity(DecorationAlphaAt(t));
      dc.PushOpacity(.18*awaken);
      for(int x=64;x<1550;x+=56)Line(dc,Brush("#403449"),.55,x,128,x,880);
      for(int y=152;y<900;y+=56)Line(dc,Brush("#403449"),.55,46,y,1554,y);
      dc.Pop();
      for(int i=0;i<60;i++){
        double x=50+stars[i*3]*1500,y=130+stars[i*3+1]*720;
        double a=(.08+.15*(.5+.5*Math.Sin(t*.6+i)))*awaken;
        dc.PushOpacity(a);dc.DrawRectangle(i%8==0?pink:muted,null,new Rect(x,y,2+i%2,2+i%2));dc.Pop();
      }
      dc.PushOpacity(structure);
      Flower(dc,65,68,1,1);Label(dc,"X I A O K U I   O S",91,49,18,white,false,true,true);
      Label(dc,"PERSONAL OPERATING SYSTEM",91,78,9,muted,false,true);
      Label(dc,"SAKURA / BOOT 01",1375,55,11,pink,false,true);
      Label(dc,"PERSONAL WORKSPACE",1375,76,8,muted,false,true);
      var edge=Brush("#4E354E");
      for(int n=0;n<4;n++){double x=n%2==0?42:1558,y=n<2?131:894,dx=n%2==0?1:-1,dy=n<2?1:-1;
        Line(dc,edge,1,x,y,x+dx*24,y);Line(dc,edge,1,x,y,x,y+dy*24);dc.DrawRectangle(pink,null,new Rect(x-1,y-1,3,3));}
      Line(dc,Brush("#33273C"),.8,72,913,1528,913);
      Label(dc,"小葵 OS  /  樱花启动序列",67,940,11,muted);
      Label(dc,"A LITTLE SAKURA. A NEW BEGINNING.",1211,942,9,muted,false,true);
      dc.Pop();
      dc.Pop();
      if(t<1.4){
        double seed=1-Ease((t-.9)/.5);Flower(dc,800,438,1.15+.15*Math.Sin(t*5),seed);
        dc.PushOpacity(.6*seed);Label(dc,"S A K U R A   C O R E",800,484,10,pink,true,true);dc.Pop();
        double length=320*Ease((t-.25)/.65);dc.PushOpacity(seed*.65);Line(dc,pink,1,800-length,545,800+length,545);dc.Pop();
      }
      double visible=Ease((t-1.1)/2.05),settled=Ease((t-2.75)/.55),logoWidth=1000,logoHeight=logoWidth*logo.PixelHeight/logo.PixelWidth;
      double lx=800-logoWidth/2,ly=438-logoHeight/2;
      if(t>=1.05&&t<3.4&&!Reduced){
        dc.PushOpacity((1-settled)*.92);
        foreach(var tile in tiles){double p=Ease((t-1.05-tile.Delay)/1.9);if(p<=0)continue;double inv=1-p;
          double x=lx+tile.X*logoWidth+tile.DX*inv*inv,y=ly+tile.Y*logoHeight+tile.DY*inv*inv;
          double s=tile.Size*(.35+.65*p);dc.DrawRectangle(tile.Brush,null,new Rect(x-s/2,y-s/2,s+.3,s+.3));
        }dc.Pop();
      }
      double ring=Ease((t-2.5)/.8)*(1-Ease((t-8.5)/.65))*DecorationAlphaAt(t);
      dc.PushOpacity(ring*.55);
      Arc(dc,287,-150+t*8,58,Brush("#704A69"),.8);Arc(dc,287,30+t*8,58,Brush("#704A69"),.8);
      Arc(dc,302,-32-t*6,22,pink,1.6);Arc(dc,302,148-t*6,22,pink,1.6);
      for(int i=0;i<48;i++){double a=i*Math.PI/24;double r1=i%4==0?309:313,r2=318;
        Line(dc,i%4==0?pink:edge,.7,800+r1*Math.Cos(a),438+r1*Math.Sin(a),800+r2*Math.Cos(a),438+r2*Math.Sin(a));}
      dc.Pop();
      if(t>=1.2){
        dc.PushOpacity((Reduced?Ease((t-1)/.6):Ease((t-2.2)/1.05))*LogoAlphaAt(t));
        dc.PushTransform(new ScaleTransform(1+.018*(1-settled)-.025*Smooth((t-10.8)/.8),1+.018*(1-settled)-.025*Smooth((t-10.8)/.8),800,438));dc.DrawImage(logo,new Rect(lx,ly,logoWidth,logoHeight));dc.Pop();dc.Pop();
      }
      if(t>2.5&&t<5&&!Reduced){
        double scan=Clamp((t-2.5)/2.3),x=lx+logoWidth*scan;
        dc.PushOpacity(Math.Sin(scan*Math.PI)*.55);Line(dc,pink,1,x,ly-12,x,ly+logoHeight+12);
        var wash=new LinearGradientBrush();wash.StartPoint=new Point(0,0);wash.EndPoint=new Point(1,0);wash.GradientStops.Add(new GradientStop(Colors.Transparent,0));wash.GradientStops.Add(new GradientStop(Color.FromArgb(25,255,160,204),1));
        dc.DrawRectangle(wash,null,new Rect(x-90,ly,90,logoHeight));dc.Pop();
      }
      double detail=Ease((t-2.4)/.6);
      dc.PushOpacity(detail*DecorationAlphaAt(t));
      double bracket=Ease((t-2.5)/.8);
      double bx=lx-25,by=ly-14,bw=logoWidth+50,bh=logoHeight+28;
      for(int n=0;n<4;n++){double x=n%2==0?bx:bx+bw,y=n<2?by:by+bh,dx=n%2==0?1:-1,dy=n<2?1:-1;
        Line(dc,Brush("#815168"),1,x,y,x+dx*32*bracket,y);Line(dc,Brush("#815168"),1,x,y,x,y+dy*20*bracket);}
      Label(dc,"IDENTITY / XIAOKUI",bx,by-26,9,muted,false,true);
      Label(dc,"PX · 001",bx+bw-65,by-26,9,muted,false,true);
      dc.Pop();
      {
        int stage=StageAt(t);
        string[] zh={"小葵，正在醒来。","初始化界面","准备工作空间","欢迎回来"};
        string[] en={"SAKURA CORE / AWAKENING","INITIALIZING INTERFACE","PREPARING WORKSPACE","WELCOME BACK, XIAOKUI"};
        string[] note={"一片樱花，一个新的开始。","像素正在亮起。","为你留好熟悉的位置。","樱花已绽放，开始创造。"};
        double a=CaptionAlphaAt(t);dc.PushOpacity(a);double offset=(1-a)*4;
        Label(dc,zh[stage],800,719+offset,stage==3?28:25,stage==3?bright:white,true,false,true);
        Label(dc,en[stage],800,762+offset,10,pink,true,true);
        Label(dc,note[stage],800,795+offset,11,muted,true);
        dc.Pop();
      }
      double progress=ProgressAt(t);
      dc.PushOpacity(Ease(t/.45)*ProgressAlphaAt(t));
      double py=858;for(int i=0;i<40;i++){
        Brush color=i<progress*40?pink:Brush("#2F2537");dc.DrawRectangle(color,null,new Rect(530+i*13.7,py,i%5==0?11:9,2));}
      Label(dc,progress>=1?"STARTUP SEQUENCE COMPLETE":"S A K U R A   B O O T",530,875,9,muted,false,true);
      Label(dc,((int)(progress*100)).ToString("D3")+" %",1020,873,10,pink,false,true);dc.Pop();
      if(t>7.5&&!Reduced)for(int i=0;i<9;i++){
        double p=Clamp((t-7.5-i*.07)/2),a=Math.Sin(p*Math.PI)*.48;double angle=i*2.399;
        Flower(dc,800+Math.Cos(angle)*(375+p*110),438+Math.Sin(angle)*(230+p*90),.32+i%3*.09,a);
      }
      dc.PushOpacity(.6*structure*DecorationAlphaAt(t));Label(dc,Muted?"♪ 静音":"♪ 音效",1350,875,10,muted);Label(dc,"ESC  跳过",1451,875,10,muted);dc.Pop();
      dc.Pop();dc.Pop();
    }
    internal void Export(string path,double seconds,int width,int height) {
      Time=seconds;InvalidateVisual();Measure(new Size(width,height));Arrange(new Rect(0,0,width,height));UpdateLayout();
      var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(this);
      var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    }
  }

  [ComImport,Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  internal interface IApplicationActivationManager {
    [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string appId,[MarshalAs(UnmanagedType.LPWStr)]string arguments,uint options,out uint processId);
  }

  internal static class Program {
    private static readonly string Root=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    private static readonly object logGate=new object();
    private static int openRequested;
    internal static uint ActivatedProcessId;
    internal static void Log(string text){lock(logGate){try{string dir=Path.Combine(Root,"logs");Directory.CreateDirectory(dir);string file=Path.Combine(dir,"startup.log");if(File.Exists(file)&&new FileInfo(file).Length>65536)File.WriteAllText(file,"");File.AppendAllText(file,DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}catch{}}}
    internal static void OpenOfficial() {
      if(Interlocked.Exchange(ref openRequested,1)!=0)return;
      object manager=null;
      try {
        manager=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
        uint pid;int hr=((IApplicationActivationManager)manager).ActivateApplication("OpenAI.Codex_2p2nqsd0c76g0!App",String.Empty,0,out pid);
        Marshal.ThrowExceptionForHR(hr);ActivatedProcessId=pid;
        Log("official-activation-confirmed pid="+pid+"; parallel-with-animation");
      }catch(Exception error){
        Log("activation-manager-unavailable "+error.GetType().Name+"; using-normal-shell-activation");
        try{Process.Start(new ProcessStartInfo("explorer.exe",@"shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App"){UseShellExecute=true});}
        catch(Exception fallback){Log("official-activation-failed "+fallback.GetType().Name);}
      }finally{if(manager!=null&&Marshal.IsComObject(manager))Marshal.FinalReleaseComObject(manager);}
    }
    [STAThread] public static int Main(string[] args) {
      bool preview=args.Contains("--preview")||Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location).Contains("预览");
      bool render=args.Contains("--render"),mock=args.Contains("--mock");
      try {
        if(args.Contains("--check-activation")){OpenOfficial();return ActivatedProcessId!=0?0:1;}
        if(render){int index=Array.IndexOf(args,"--render");string dir=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(dir);var scene=new SakuraScene();double[] samples={1.25,3.75,6.25,9,10.75,11.5,12.2,12.35,12.5};foreach(double t in samples)scene.Export(Path.Combine(dir,"frame-"+t.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png"),t,1600,1000);return 0;}
        using(var mutex=new Mutex(false,preview?@"Local\XiaokuiOS.Preview.v1":@"Local\XiaokuiOS.CodexBoot.v1")){
          bool acquired;try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
          if(!acquired){if(!preview)Native.Activate(Native.FindClient());return 0;}
          try{
            Native.Rect work=Native.LaunchWorkArea(),placement=Native.Centered(work);
            Log("launch-entry version=1.5 preview="+preview+"; workArea="+Native.Describe(work)+"; centeredBounds="+Native.Describe(placement));
            // 独立线程同步启动官方客户端；先记录鼠标所在屏幕的居中矩形。
            if(!preview&&!mock) {
              var activation=new Thread(new ThreadStart(OpenOfficial)){IsBackground=true,Name="Codex activation"};
              activation.SetApartmentState(ApartmentState.STA);activation.Start();
              Log("official-activation-dispatched; animation-does-not-wait");
            }
            var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};app.Run(new BootWindow(preview,mock,args.Contains("--loop"),null,placement));return 0;
          }
          finally{mutex.ReleaseMutex();}
        }
      }catch(Exception error){Log("Failure: "+error);if(!preview&&!render&&!mock)OpenOfficial();else MessageBox.Show(error.Message,"小葵 OS",MessageBoxButton.OK,MessageBoxImage.Information);return 1;}
    }
  }
}
