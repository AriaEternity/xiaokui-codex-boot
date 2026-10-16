using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace XiaokuiOS {
  // 启动时立即居中；客户端出现后匹配同一矩形，播放期间持续跟随。
  internal sealed class BootWindow : Window {
    private readonly SakuraScene scene=new SakuraScene();
    private readonly Stopwatch wall=new Stopwatch(),animation=new Stopwatch();
    private readonly DispatcherTimer heartbeat=new DispatcherTimer(DispatcherPriority.Render);
    private readonly DispatcherTimer deadline=new DispatcherTimer(DispatcherPriority.Send);
    private readonly Audio audio=new Audio();
    private readonly bool preview,mock,loop;
    private readonly Func<IntPtr> findClient;
    private readonly Native.Rect launchBounds;
    private Native.EventProc locationCallback;
    private IntPtr locationHook,target,own;
    private bool done,started,switchedAway,placed,restoreRequested,fadeStarted,targetRaised;
    private int cue=-1,probeBusy;
    private double lastProbe=-1,centeredAt,lastRaise=-10,lastPlacement=-10,lastRenderAt=-1;
    private int regionWidth,regionHeight;
    private uint targetPid;
    internal int AlignmentUpdates {get;private set;}
    internal int AlignmentFailures {get;private set;}
    internal bool InitialCenterVerified {get;private set;}
    internal bool NonRectCoverVerified {get;private set;}
    internal int TopmostReassertions {get;private set;}
    internal int TopmostFailures {get;private set;}
    internal int FadeFrames {get;private set;}
    internal bool TargetResponsive {get;private set;}
    internal double FirstFadeSeconds {get;private set;}
    internal double FadeTargetAgeSeconds {get;private set;}
    internal Native.Rect LaunchBounds {get{return launchBounds;}}
    internal bool HasStarted {get{return started;}}
    internal bool CompletedWithAlignedTarget {get;private set;}
    internal double FirstFrameWallSeconds {get;private set;}
    internal IntPtr Handle {get{return own;}}
    internal double AnimationSeconds {get{return animation.Elapsed.TotalSeconds;}}
    internal double Progress {get{return SakuraScene.ProgressAt(scene.Time);}}
    internal double SceneTime {get{return scene.Time;}}
    internal bool LayeredTransition {get{return AllowsTransparency&&Background==Brushes.Transparent;}}
    internal event Action<bool> Finished;

    internal BootWindow(bool onlyPreview,bool mockTarget,bool loopPreview,Func<IntPtr> resolver=null,Native.Rect? placement=null) {
      preview=onlyPreview;mock=mockTarget;loop=preview&&loopPreview;findClient=resolver??Native.FindClient;
      launchBounds=placement??Native.Centered(Native.LaunchWorkArea());
      Title=preview?"小葵 OS · 动画预览":"小葵 OS · Codex";
      WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
      Width=1280;Height=800;WindowStartupLocation=WindowStartupLocation.Manual;
      ShowInTaskbar=preview;Topmost=true;ShowActivated=true;
      Opacity=0;IsHitTestVisible=true;
      Content=scene;scene.Reduced=!SystemParameters.ClientAreaAnimation;
      SourceInitialized+=delegate {
        own=new WindowInteropHelper(this).Handle;
        int rounded=2,disabled=1;
        try{Native.DwmSetWindowAttribute(own,33,ref rounded,4);Native.DwmSetWindowAttribute(own,3,ref disabled,4);}catch{}
        PlaceCoverAtLaunch();
      };
      Loaded+=delegate {
        wall.Start();heartbeat.Start();CompositionTarget.Rendering+=OnFrame;
        PlaceCoverAtLaunch();BeginPlayback();Probe(0);
      };
      SizeChanged+=delegate {PrepareCover();};
      heartbeat.Interval=TimeSpan.FromMilliseconds(25);heartbeat.Tick+=delegate{Update();};
      deadline.Interval=TimeSpan.FromSeconds(SakuraScene.Duration);
      deadline.Tick+=delegate{
        deadline.Stop();double remaining=SakuraScene.Duration-animation.Elapsed.TotalSeconds;
        // Windows 定时器可能提前合并到相邻节拍；再次核对单调时钟，避免提前退出。
        if(remaining>0){deadline.Interval=TimeSpan.FromMilliseconds(Math.Max(1,remaining*1000));deadline.Start();return;}
        scene.Time=SakuraScene.Duration;Finish(false);
      };
      PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
        if(e.Key==Key.Escape||e.Key==Key.Enter||e.Key==Key.Space){e.Handled=true;Finish(true);}
        else if(e.Key==Key.M){audio.Toggle();scene.Muted=!audio.Enabled;}
      };
      MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){
        var p=e.GetPosition(scene);double s=Math.Min(scene.ActualWidth/1600,scene.ActualHeight/1000);if(s<=0)return;
        double x=(p.X-(scene.ActualWidth-1600*s)/2)/s,y=(p.Y-(scene.ActualHeight-1000*s)/2)/s;
        if(y>857&&y<907&&x>1330&&x<1440){audio.Toggle();scene.Muted=!audio.Enabled;}
        else if(y>857&&y<907&&x>=1440)Finish(true);
      };
      Deactivated+=delegate {
        if(!started||done)return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate {
          IntPtr foreground=Native.GetForegroundWindow();
          if(!done&&foreground!=own&&foreground!=target)switchedAway=true;
        }));
      };
      Closed+=delegate {done=true;deadline.Stop();heartbeat.Stop();CompositionTarget.Rendering-=OnFrame;DropHook();audio.Dispose();};
    }

    private void DropHook(){if(locationHook!=IntPtr.Zero){Native.UnhookWinEvent(locationHook);locationHook=IntPtr.Zero;}}
    private void PlaceCoverAtLaunch() {
      if(own!=IntPtr.Zero)Native.SetWindowPos(own,IntPtr.Zero,launchBounds.Left,launchBounds.Top,launchBounds.Width,launchBounds.Height,0x14);
      PrepareCover();KeepAbove(wall.Elapsed.TotalSeconds,true);
    }
    private void PrepareCover() {
      Native.Rect bounds;if(own==IntPtr.Zero||!Native.GetWindowRect(own,out bounds))return;
      if(bounds.Width==regionWidth&&bounds.Height==regionHeight&&NonRectCoverVerified)return;
      NonRectCoverVerified=Native.PrepareCoverRegion(own);
      regionWidth=bounds.Width;regionHeight=bounds.Height;
    }
    private void KeepAbove(double now,bool force=false) {
      if(own==IntPtr.Zero||done)return;
      bool intact=Native.IsTopmost(own)&&(target==IntPtr.Zero||!Native.Presentable(target)||Native.Above(own,target));
      if(!force&&intact&&now-lastRaise<.12)return;
      lastRaise=now;TopmostReassertions++;
      if(!Native.RaiseCover(own))TopmostFailures++;
    }
    private void Bind(IntPtr candidate) {
      if(candidate==target)return;
      DropHook();target=candidate;TargetResponsive=false;placed=false;restoreRequested=false;targetRaised=false;lastPlacement=-10;
      if(target==IntPtr.Zero)return;
      Native.GetWindowThreadProcessId(target,out targetPid);
      locationCallback=delegate(IntPtr hook,uint type,IntPtr window,int objectId,int childId,uint thread,uint time) {
        if(done||window!=target||objectId!=0)return;
        Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(delegate{if(!done)Synchronize();}));
      };
      locationHook=Native.SetWinEventHook(0x800B,0x800B,IntPtr.Zero,locationCallback,targetPid,0,0);
      // 所有者关系维持最小化联动；置顶覆盖保留到动画退出。
      try{new WindowInteropHelper(this).Owner=target;}catch(Exception error){Program.Log("owner-link-failed "+error.GetType().Name);}
      KeepAbove(wall.Elapsed.TotalSeconds,true);
      Program.Log("target-bound atMs="+wall.ElapsedMilliseconds+" bounds="+Native.Describe(Native.Bounds(target)));
    }

    private bool Synchronize() {
      if(preview||mock)return true;
      uint pid;Native.GetWindowThreadProcessId(target,out pid);
      if(own==IntPtr.Zero||pid!=targetPid||!Native.Presentable(target))return false;
      if(!placed) {
        if(Native.IsZoomed(target)) {
          if(!restoreRequested){Native.ShowWindowAsync(target,9);restoreRequested=true;}
          return false;
        }
        if(!Native.Equal(Native.Bounds(target),launchBounds,1)) {
          if(wall.Elapsed.TotalSeconds-lastPlacement<.12)return false;
          lastPlacement=wall.Elapsed.TotalSeconds;
          if(!Native.PlaceVisible(target,launchBounds)){AlignmentFailures++;return false;}
          return false; // 等待异步窗口定位及桌面合成更新。
        }
        placed=true;InitialCenterVerified=true;centeredAt=wall.Elapsed.TotalSeconds;
        Program.Log("client-centered atMs="+wall.ElapsedMilliseconds+" bounds="+Native.Describe(Native.Bounds(target)));
      }
      Native.Rect wanted=Native.Bounds(target),actual;
      if(wanted.Width<400||wanted.Height<300||!Native.GetWindowRect(own,out actual))return false;
      if(!Native.Equal(actual,wanted)) {
        if(!Native.SetWindowPos(own,IntPtr.Zero,wanted.Left,wanted.Top,wanted.Width,wanted.Height,0x14)){AlignmentFailures++;return false;}
        AlignmentUpdates++;
      }
      if(!Native.GetWindowRect(own,out actual)||!Native.Equal(actual,wanted,1)){AlignmentFailures++;return false;}
      PrepareCover();
      return true;
    }

    private void Probe(double now) {
      if(preview||mock||now-lastProbe<.18||Interlocked.CompareExchange(ref probeBusy,1,0)!=0)return;
      lastProbe=now;IntPtr pinned=target;
      ThreadPool.QueueUserWorkItem(delegate {
        try {
          IntPtr candidate=Native.Presentable(pinned)?pinned:findClient();
          bool ok=Native.Presentable(candidate)&&Native.Responsive(candidate);
          Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(delegate {
            if(done)return;
            if(candidate!=IntPtr.Zero&&candidate!=target)Bind(candidate);
            TargetResponsive=ok&&candidate==target;
            Update();
          }));
        }catch(Exception error){Program.Log("window-probe-failed "+error.GetType().Name);}
        finally{Interlocked.Exchange(ref probeBusy,0);}
      });
    }

    private void BeginPlayback() {
      if(started||done)return;
      started=true;FirstFrameWallSeconds=wall.Elapsed.TotalSeconds;
      IsHitTestVisible=true;
      Opacity=NonRectCoverVerified?1:.999;
      animation.Start();deadline.Start();scene.Time=0;scene.InvalidateVisual();audio.Play("boot");
      Activate();Focus();
      Program.Log("animation-start atMs="+wall.ElapsedMilliseconds+" targetPresent="+(target!=IntPtr.Zero)+" bounds="+Native.Describe(launchBounds));
    }

    private void OnFrame(object sender,EventArgs e){Update();}
    private void Update() {
      if(done||!wall.IsRunning||!started)return;
      double t=animation.Elapsed.TotalSeconds;
      // 截止时间优先于窗口探测、位置与响应检查；任何目标状态均不延长播放。
      if(t>=SakuraScene.Duration){scene.Time=SakuraScene.Duration;Finish(false);return;}
      double now=wall.Elapsed.TotalSeconds;KeepAbove(now);Probe(now);
      bool aligned=Synchronize();
      // 最小化时仅隐藏画面，计时照常继续，到时退出。
      Opacity=target!=IntPtr.Zero&&Native.IsIconic(target)?0:1;
      if(!(preview||mock)&&aligned&&!targetRaised&&t>=SakuraScene.FadeStart-.35) {
        Native.SetWindowPos(target,IntPtr.Zero,0,0,0,0,0x4213);targetRaised=true;KeepAbove(now,true);
      }
      scene.Time=t;scene.Muted=!audio.Enabled;
      if(t>=SakuraScene.FadeStart){
        if(!fadeStarted){fadeStarted=true;FirstFadeSeconds=t;FadeTargetAgeSeconds=placed?now-centeredAt:0;Program.Log("handoff-start atMs="+wall.ElapsedMilliseconds+" topmost="+Native.IsTopmost(own)+" targetPresent="+Native.Presentable(target));}
        FadeFrames++;
      }
      // 各图层直接绘制自己的透明度，窗口及客户端不缩放，进度条保留到最后。
      scene.RenderTransform=Transform.Identity;
      int current=t<2.5?0:t<5?1:t<7.5?2:t<11.85?3:4;
      if(current!=cue){cue=current;if(current==1||current==2)audio.Play("tick");if(current==3)audio.Play("ready");if(current==4)audio.Play("exit");}
      if(now-lastRenderAt>=1.0/60){lastRenderAt=now;scene.InvalidateVisual();}
    }

    private void Finish(bool skipped) {
      if(preview&&loop&&!skipped){animation.Restart();deadline.Stop();deadline.Interval=TimeSpan.FromSeconds(SakuraScene.Duration);deadline.Start();cue=-1;fadeStarted=false;Opacity=1;scene.RenderTransform=Transform.Identity;audio.Play("boot");return;}
      if(done)return;
      CompletedWithAlignedTarget=!(preview||mock)&&Synchronize();
      done=true;deadline.Stop();animation.Stop();heartbeat.Stop();CompositionTarget.Rendering-=OnFrame;DropHook();audio.Stop();
      IntPtr foreground=Native.GetForegroundWindow();bool mayFocus=!switchedAway&&(foreground==own||foreground==target||IsActive);
      if(Finished!=null)Finished(skipped);
      Close();
      if(!preview&&!mock&&mayFocus&&Native.Presentable(target))Native.Activate(target);
      Program.Log((preview?"preview":"startup")+" complete version=1.5; skipped="+skipped+"; target="+(target!=IntPtr.Zero)+"; centered="+InitialCenterVerified+"; aligned="+CompletedWithAlignedTarget+"; alignmentUpdates="+AlignmentUpdates+"; alignmentFailures="+AlignmentFailures+"; topmostFailures="+TopmostFailures+"; topmostReassertions="+TopmostReassertions+"; nonRect="+NonRectCoverVerified+"; fadeFrames="+FadeFrames+"; frames="+scene.RenderedFrames+"; fixedDuration=True; progress="+Progress.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+"; layered="+LayeredTransition+"; animationMs="+animation.ElapsedMilliseconds+"; totalMs="+wall.ElapsedMilliseconds);
    }
  }
}
