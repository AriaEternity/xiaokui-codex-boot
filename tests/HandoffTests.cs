using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace XiaokuiOS {
  internal static class HandoffTests {
    [STAThread] public static int Main(string[] args) {
      double delay=args.Length>0?Double.Parse(args[0],System.Globalization.CultureInfo.InvariantCulture):5.5;
      string report=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.GetDirectoryName(report));
      var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
      Native.Rect work=Native.LaunchWorkArea(),expected=Native.Centered(work);
      bool geometry=CheckCenter(work)&&CheckCenter(new Native.Rect{Left=-1920,Top=-300,Right=0,Bottom=740})&&CheckCenter(new Native.Rect{Left=2560,Top=120,Right=3640,Bottom=2000});
      var watch=Stopwatch.StartNew();
      IntPtr fixtureHandle=IntPtr.Zero;
      var fixture=new Window{Title="小葵 OS · 窗口交接测试目标",Width=980,Height=640,Left=150,Top=90,WindowStartupLocation=WindowStartupLocation.Manual,Background=new SolidColorBrush(Color.FromRgb(24,24,32))};
      bool initialMaximized=args.Length>2&&args[2]=="maximized";
      if(initialMaximized)fixture.WindowState=WindowState.Maximized;
      fixture.Content=new TextBlock{Text="窗口交接验证\n此窗口用于测试，不会关闭或重启 Codex。",Foreground=Brushes.White,FontSize=24,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,TextAlignment=TextAlignment.Center};
      var boot=new BootWindow(false,false,false,delegate{return fixtureHandle;},expected);
      bool shown=false,started=false,earlyVisible=false,earlyStarted=false,finished=false,minimizeVerified=false;
      bool centerVerified=false,preTargetCentered=true,exercise=delay==0;
      int phase=0,samples=0,mismatches=0,minimizeSamples=0,minimizeVisibleSamples=0,zSamples=0,zMismatches=0,foregroundActivations=0,exit=2;
      double shownAt=-1,startedAt=-1,lastChange=-100,lastActivation=-100,previousProgress=0,firstCompleteAt=-1;
      bool monotonicProgress=true;
      var verified=new HashSet<int>();
      var pulse=new DispatcherTimer(DispatcherPriority.ApplicationIdle){Interval=TimeSpan.FromMilliseconds(45)};
      pulse.Tick+=delegate {
        double now=watch.Elapsed.TotalSeconds;
        if(!shown&&now>=delay){fixture.Show();fixtureHandle=new WindowInteropHelper(fixture).Handle;shown=true;shownAt=now;lastChange=now;}
        if(shown&&Native.Presentable(fixtureHandle)&&now-lastActivation>.35) {
          Native.SetWindowPos(fixtureHandle,IntPtr.Zero,0,0,0,0,0x213);Native.SetForegroundWindow(fixtureHandle);
          lastActivation=now;foregroundActivations++;
        }
        if(boot.HasStarted&&Native.Presentable(boot.Handle)&&boot.Opacity>.001) {
          zSamples++;
          if(!Native.IsTopmost(boot.Handle)||(shown&&Native.Presentable(fixtureHandle)&&!Native.Above(boot.Handle,fixtureHandle)))zMismatches++;
        }
        if(boot.HasStarted){double progress=boot.Progress;monotonicProgress&=progress>=previousProgress;previousProgress=progress;if(progress>=1&&firstCompleteAt<0)firstCompleteAt=boot.SceneTime;}
        if(!shown){
          earlyVisible|=boot.Opacity>.001;earlyStarted|=boot.HasStarted;
          Native.Rect before;Native.GetWindowRect(boot.Handle,out before);preTargetCentered&=Native.Equal(before,expected,1);

        }
        if(boot.HasStarted&&!started){started=true;startedAt=now;}
        if(shown){
          double since=now-shownAt;
          if(phase==0&&boot.InitialCenterVerified&&Native.Equal(Native.Bounds(fixtureHandle),expected,1))centerVerified=true;
          if(exercise){
          if(phase==0&&since>1.4){Native.SetWindowPos(fixtureHandle,IntPtr.Zero,120,100,1040,660,0x14);phase=1;lastChange=now;}
          else if(phase==1&&since>2.4){Native.SetWindowPos(fixtureHandle,IntPtr.Zero,380,160,850,600,0x14);phase=2;lastChange=now;}
          else if(phase==2&&since>3.3){fixture.WindowState=WindowState.Maximized;phase=3;lastChange=now;}
          else if(phase==3&&since>4.3){fixture.WindowState=WindowState.Normal;phase=4;lastChange=now;}
          else if(phase==4&&since>5.4){Native.SetWindowPos(fixtureHandle,IntPtr.Zero,100,80,1200,760,0x14);phase=5;lastChange=now;}
          else if(phase==5&&since>6.2){fixture.WindowState=WindowState.Minimized;phase=6;lastChange=now;}
          else if(phase==6&&since>7.2){fixture.WindowState=WindowState.Normal;phase=7;lastChange=now;}
          }
          if(started&&boot.InitialCenterVerified&&now-lastChange>(phase==0?.3:.17)){
            if(Native.Presentable(fixtureHandle)) {
              Native.Rect actual;Native.GetWindowRect(boot.Handle,out actual);samples++;
              if(!Native.Equal(actual,Native.Bounds(fixtureHandle),1))mismatches++;
              else verified.Add(phase);
            }else if(phase==6){
              minimizeSamples++;
              bool hidden=boot.Opacity<.001||!Native.IsWindowVisible(boot.Handle)||Native.IsIconic(boot.Handle);
              minimizeVerified|=hidden;if(!hidden)minimizeVisibleSamples++;
            }
          }
        }
        if(now>delay+25){pulse.Stop();File.WriteAllText(report,"{\"passed\":false,\"error\":\"test-timeout\"}");boot.Close();}
      };
      boot.Finished+=delegate(bool skipped) {
        finished=true;
        bool absent=delay>SakuraScene.Duration;
        bool parallel=delay==0||earlyVisible&&earlyStarted&&(absent?shownAt<0:startedAt<shownAt);
        bool movements=!exercise||verified.Count>=6&&phase==7&&minimizeVerified&&minimizeVisibleSamples==0;
        bool timing=boot.AnimationSeconds>=12.5&&boot.AnimationSeconds<12.7;
        bool fade=boot.FirstFadeSeconds>=10.5&&boot.FirstFadeSeconds<10.7&&boot.FadeFrames>=20&&boot.LayeredTransition;
        bool windowChecks=absent?!shown: centerVerified&&samples>=3&&boot.CompletedWithAlignedTarget&&foregroundActivations>=1;
        bool progressChecks=monotonicProgress&&boot.Progress==1&&firstCompleteAt>=12.2&&firstCompleteAt<12.4;
        bool pass=!skipped&&geometry&&parallel&&preTargetCentered&&windowChecks&&started&&boot.FirstFrameWallSeconds<.5&&mismatches==0&&movements&&timing&&boot.AlignmentFailures==0&&zSamples>=30&&zMismatches==0&&boot.TopmostFailures==0&&boot.NonRectCoverVerified&&Native.CoverRegionReady(boot.Handle)&&fade&&progressChecks&&CheckTimeline();
        exit=pass?0:2;
        var result=new Dictionary<string,object>{
          {"passed",pass},{"launcherVersion","1.5.0.0"},{"targetDelaySeconds",delay},{"targetShownAtSeconds",shownAt},{"animationStartedAtSeconds",startedAt},
          {"zOrderSamples",zSamples},{"zOrderMismatches",zMismatches},{"foregroundActivations",foregroundActivations},{"topmostFailures",boot.TopmostFailures},
          {"nonRectCoverVerified",boot.NonRectCoverVerified},{"fadeFrames",boot.FadeFrames},{"firstFadeSeconds",boot.FirstFadeSeconds},{"fadeTargetAgeSeconds",boot.FadeTargetAgeSeconds},
          {"animationVisibleBeforeTarget",earlyVisible},{"animationStartedBeforeTarget",earlyStarted},{"nativeBoundsSamples",samples},
          {"preTargetCentered",preTargetCentered},{"initialClientCentered",centerVerified},{"launchBounds",Native.Describe(expected)},
          {"firstFrameWallSeconds",boot.FirstFrameWallSeconds},{"totalSeconds",watch.Elapsed.TotalSeconds},{"fixedDurationVerified",timing},{"noTargetAtDeadline",absent&&!shown},{"monotonicProgress",monotonicProgress},{"first100PercentSeconds",firstCompleteAt},{"finalProgress",boot.Progress},{"layeredTransition",boot.LayeredTransition},{"timelineContract",CheckTimeline()},
          {"monitorGeometryChecks",geometry},{"movementRegression",exercise},
          {"initiallyMaximized",initialMaximized},
          {"nativeBoundsMismatches",mismatches},{"geometryPhasesVerified",verified.Count},{"minimizeHideVerified",minimizeVerified},
          {"minimizeSamples",minimizeSamples},{"minimizeVisibleSamples",minimizeVisibleSamples},
          {"alignmentUpdates",boot.AlignmentUpdates},{"alignmentFailures",boot.AlignmentFailures},{"finalBoundsMatch",boot.CompletedWithAlignedTarget},
          {"animationSeconds",boot.AnimationSeconds},{"physicalDpi",Native.GetDpiForWindow(fixtureHandle)},
          {"realCodexTouched",false}
        };
        File.WriteAllText(report,new JavaScriptSerializer().Serialize(result),new UTF8Encoding(false));
      };
      boot.Closed+=delegate{pulse.Stop();fixture.Close();if(!finished&&exit!=0)exit=2;app.Shutdown(exit);};
      pulse.Start();return app.Run(boot);
    }
    private static bool CheckTimeline() {
      return SakuraScene.Duration==12.5&&SakuraScene.StageAt(1.25)==0&&SakuraScene.StageAt(3.75)==1&&SakuraScene.StageAt(6.25)==2&&SakuraScene.StageAt(9)==3&&
        SakuraScene.ProgressAt(6.25)>.50&&SakuraScene.ProgressAt(6.25)<.52&&SakuraScene.ProgressAt(12.19)<1&&SakuraScene.ProgressAt(12.2)==1&&
        SakuraScene.CaptionAlphaAt(10.95)==0&&SakuraScene.DecorationAlphaAt(11.1)==0&&SakuraScene.LogoAlphaAt(11.61)==0&&
        SakuraScene.BackgroundAlphaAt(11.6)>0&&SakuraScene.BackgroundAlphaAt(11.6)<1&&SakuraScene.ProgressAlphaAt(12.2)==1&&SakuraScene.ProgressAlphaAt(12.5)==0;
    }
    private static bool CheckCenter(Native.Rect work) {
      Native.Rect b=Native.Centered(work);
      return b.Left>=work.Left&&b.Top>=work.Top&&b.Right<=work.Right&&b.Bottom<=work.Bottom&&
        Math.Abs(b.Left+b.Right-work.Left-work.Right)<=1&&Math.Abs(b.Top+b.Bottom-work.Top-work.Bottom)<=1&&Math.Abs((double)b.Width/b.Height-1.6)<.01;
    }
  }
}
