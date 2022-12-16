using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TRTCWPFDemo.Common;
using TRTCWPFDemo;

namespace TencentCloud_TRTC
{
    public static class Program
    {
        // 外部関数宣言
        [DllImport("User32.dll")]
        private static extern Int32 SetProcessDPIAware();

        /// <summary>
        ///アプリケーションのエントリーポイントです。
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public static void Main()
        {
            ManageLiteAV.CrashDump dump = new ManageLiteAV.CrashDump();
            dump.open();

            SetProcessDPIAware();   // SDKの記録エラーを回避するため、デフォルトで高DPIをオフにする

            Log.Open();
            // SDKのLocal configuration情報を初期化する
            DataManager.GetInstance().InitConfig();

            Process processes = Process.GetCurrentProcess();
            Log.I(String.Format("Progress <{0}, {1}>", processes.ProcessName, processes.Id));

            TencentCloud_TRTC.App app = new TencentCloud_TRTC.App();            
            app.InitializeComponent();
            app.Run();

            // プログラムを終了する前に、最新のLocal設定情報を書き込む。
            DataManager.GetInstance().Uninit();
            DataManager.GetInstance().Dispose();

            Log.Close();

            dump.close();
        }
    }
}
