// Nectar Taskbar Safety Watch v0.1. Windows-only; no admin or network.
// Changes taskbar ONLY after valid crash marker and 4 consecutive absent-Nectar polls.
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class NectarTaskbarSafetyWatch
{
  const string LockName = @"Local\NectarTaskbarSafetyWatch_01";
  const string StopName = @"Local\NectarTaskbarSafetyWatch_Stop_01";
  const uint ABM_GETTASKBARPOS=5, ABM_SETSTATE=10;
  const int GWL_EXSTYLE=-20, WS_EX_LAYERED=0x80000, WS_EX_TRANSPARENT=0x20;
  const uint SWP_NOZORDER=4, SWP_NOACTIVATE=0x10, SWP_SHOWWINDOW=0x40;
  static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"com.kua8.nectar"); } }
  static string Marker { get { return Path.Combine(Dir,"taskbar_hidden.flag"); } }
  static string LogPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NectarSafetyWatch","watchdog.log"); } }

  [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] struct APPBARDATA {
    public uint cbSize; public IntPtr hWnd; public uint uCallbackMessage,uEdge; public RECT rc; public IntPtr lParam;
  }
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int cmd);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h,int index,int value);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RECT rect);
  [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint msg,ref APPBARDATA data);

  static APPBARDATA BarData(IntPtr h) {
    APPBARDATA a=new APPBARDATA();a.cbSize=(uint)Marshal.SizeOf(typeof(APPBARDATA));a.hWnd=h;return a;
  }
  static void Log(string message) {
    try {
      string folder=Path.GetDirectoryName(LogPath);Directory.CreateDirectory(folder);
      if(File.Exists(LogPath)&&new FileInfo(LogPath).Length>262144) {
        string prev=LogPath+".previous";if(File.Exists(prev))File.Delete(prev);File.Move(LogPath,prev);
      }
      File.AppendAllText(LogPath,DateTime.Now.ToString("s")+" "+message+Environment.NewLine);
    }catch {}
  }
  static bool HasProcess(string name) {
    Process[] p=Process.GetProcessesByName(name);
    try{return p.Length>0;}finally{foreach(Process i in p)i.Dispose();}
  }
  static bool ValidMarker(out int value) {
    value=-1;
    try {
      if(!File.Exists(Marker)||new FileInfo(Marker).Length>40)return false;
      string s=File.ReadAllText(Marker).Trim();
      if(!s.StartsWith("abstate=",StringComparison.Ordinal))return false;
      int n;
      if(!int.TryParse(s.Substring(8),out n)||n<0||n>3)return false;
      value=n;return true;
    }catch{return false;}
  }
  static bool OnScreen(RECT rect) {
    if(rect.Right<=rect.Left||rect.Bottom<=rect.Top)return false;
    foreach(Screen s in Screen.AllScreens) {
      System.Drawing.Rectangle b=s.Bounds;
      if(rect.Left>=b.Left&&rect.Top>=b.Top&&rect.Right<=b.Right&&rect.Bottom<=b.Bottom)return true;
    }
    return false;
  }
  static bool ShouldRestore(bool marker,bool nectar,bool explorer,bool tray,int missingChecks) {
    return marker&&!nectar&&explorer&&tray&&missingChecks>=4;
  }
  static bool Restore(int original) {
    if(HasProcess("nectar")||!HasProcess("explorer"))return false;
    IntPtr h=FindWindow("Shell_TrayWnd",null);
    if(h==IntPtr.Zero)return false;
    APPBARDATA query=BarData(h);
    bool hasRect=SHAppBarMessage(ABM_GETTASKBARPOS,ref query).ToUInt64()!=0&&OnScreen(query.rc);
    RECT rect=query.rc;
    if(!hasRect) {
      // Never guess taskbar geometry with multiple monitors.
      if(Screen.AllScreens.Length!=1){Log("Unknown multimonitor geometry, recovery deferred.");return false;}
      System.Drawing.Rectangle screen=Screen.PrimaryScreen.Bounds;
      RECT current;int thickness=48;
      if(GetWindowRect(h,out current)) {
        int estimate=current.Bottom-current.Top;
        if(estimate>=25&&estimate<=120)thickness=estimate;
      }
      rect.Left=screen.Left;rect.Right=screen.Right;
      rect.Top=screen.Bottom-thickness;rect.Bottom=screen.Bottom;
    }
    if(HasProcess("nectar"))return false; // restart race protection
    APPBARDATA state=BarData(h);
    state.lParam=new IntPtr(original);
    SHAppBarMessage(ABM_SETSTATE,ref state);
    int ex=GetWindowLong(h,GWL_EXSTYLE);
    int cleaned=ex&~WS_EX_LAYERED&~WS_EX_TRANSPARENT;
    if(ex!=cleaned)SetWindowLong(h,GWL_EXSTYLE,cleaned);
    ShowWindow(h,5);
    SetWindowPos(h,IntPtr.Zero,rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top,SWP_NOZORDER|SWP_NOACTIVATE|SWP_SHOWWINDOW);
    RECT after;
    if(!IsWindowVisible(h)||!GetWindowRect(h,out after)||!OnScreen(after)) {
      Log("Taskbar restore not confirmed; marker retained.");return false;
    }
    try{File.Delete(Marker);}catch{Log("Recovered taskbar but marker removal failed.");}
    Log("RECOVERED: Nectar absent for 8s; native taskbar restored.");
    return true;
  }
  static int SelfTest() {
    bool[] tests={
      !ShouldRestore(false,false,true,true,4),
      !ShouldRestore(true,true,true,true,4),
      !ShouldRestore(true,false,false,true,4),
      !ShouldRestore(true,false,true,false,4),
      !ShouldRestore(true,false,true,true,1),
      !ShouldRestore(true,false,true,true,3),
      ShouldRestore(true,false,true,true,4),
      ShouldRestore(true,false,true,true,8)
    };
    int passes=0;foreach(bool test in tests)if(test)passes++;
    Console.WriteLine("DECISION_TESTS="+passes+"/"+tests.Length);
    Console.WriteLine("RESULT="+(passes==tests.Length?"PASS":"FAIL"));
    int state;Console.WriteLine("MARKER_VALID="+ValidMarker(out state));
    Console.WriteLine("NECTAR_RUNNING="+HasProcess("nectar"));
    Console.WriteLine("EXPLORER_RUNNING="+HasProcess("explorer"));
    IntPtr h=FindWindow("Shell_TrayWnd",null);
    Console.WriteLine("TASKBAR_EXISTS="+(h!=IntPtr.Zero));
    Console.WriteLine("TASKBAR_VISIBLE="+(h!=IntPtr.Zero&&IsWindowVisible(h)));
    APPBARDATA d=BarData(h);
    Console.WriteLine("TASKBAR_POSITION_VALID="+(h!=IntPtr.Zero&&SHAppBarMessage(ABM_GETTASKBARPOS,ref d).ToUInt64()!=0&&OnScreen(d.rc)));
    return passes==tests.Length?0:1;
  }
  static int Watch() {
    bool newMutex;
    using(Mutex m=new Mutex(true,LockName,out newMutex)) {
      if(!newMutex)return 0;
      using(EventWaitHandle stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopName)) {
        int misses=0;Log("Safety Watch started in marker-gated recovery mode.");
        while(!stop.WaitOne(2000)) {
          try {
            int state;bool valid=ValidMarker(out state);
            if(!valid||HasProcess("nectar")){misses=0;continue;}
            misses++;
            if(ShouldRestore(valid,false,HasProcess("explorer"),FindWindow("Shell_TrayWnd",null)!=IntPtr.Zero,misses))
              if(Restore(state))misses=0;
          }catch(Exception e){Log("Watch error "+e.GetType().Name);misses=0;}
        }
        Log("Safety Watch stopped normally.");
      }
    }
    return 0;
  }
  [STAThread]
  static int Main(string[] args) {
    if(args.Length>0&&args[0]=="--self-test")return SelfTest();
    if(args.Length>0&&args[0]=="--stop") {
      bool created;
      using(EventWaitHandle e=new EventWaitHandle(false,EventResetMode.ManualReset,StopName,out created)) {
        if(!created)e.Set();
      }
      return 0;
    }
    if(args.Length==0||(args.Length==1&&args[0]=="--watch"))return Watch();
    Console.WriteLine("Usage: --self-test, --watch, --stop");return 2;
  }
}
