using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace PCL.Core.Utils;
    public class WpfDragHelper
    {


        public event EventHandler DragDrop;
        public string[] DropFilePaths { get; private set; }
        public POINT DropPoint { get; private set; }
        public HwndSource HwndIntPtrSource { get; set; }

        public void AddHook()
        {
            this.RemoveDragHook();
            this.HwndIntPtrSource.AddHook(WndProc);
            IntPtr handle = this.HwndIntPtrSource.Handle;
            if (IsUserAnAdmin) RevokeDragDrop(handle);
            DragAcceptFiles(handle, true);
            ChangeMessageFilter(handle);
        }

        public void RemoveDragHook()
        {
            this.HwndIntPtrSource.RemoveHook(WndProc);
            DragAcceptFiles(this.HwndIntPtrSource.Handle, false);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (TryGetDropInfo(msg, wParam, out string[] filePaths, out POINT point))
            {
                DropPoint = point; // 正确计算窗口坐标
                DropFilePaths = filePaths;
                DragDrop?.Invoke(this, null);
                handled = true;
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, ref CHANGEFILTERSTRUCT pChangeFilterStruct);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilter(uint msg, uint flags);

        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(IntPtr hWnd, uint iFile, StringBuilder lpszFile, int cch);

        [DllImport("shell32.dll")]
        private static extern bool DragQueryPoint(IntPtr hDrop, out POINT lppt);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr hDrop);

        [DllImport("ole32.dll")]
        private static extern int RevokeDragDrop(IntPtr hWnd);

        [DllImport("shell32.dll")]
        private static extern bool IsUserAnAdminn();

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CHANGEFILTERSTRUCT
        {
            public uint cbSize;
            public uint ExtStatus;
        }

        const uint WM_COPYGLOBALDATA = 0x0049;
        const uint WM_COPYDATA = 0x004A;
        const uint WM_DROPFILES = 0x0233;
        const uint MSGFLT_ALLOW = 1;
        const uint MSGFLT_ADD = 1;
        const int MAX_PATH = 260;

        private static void ChangeMessageFilter(IntPtr handle)
        {
            Version ver = Environment.OSVersion.Version;
            bool isVistaOrHigher = ver >= new Version(6, 0);
            bool isNt61OrHiger = ver >= new Version(6, 1);
            if (isVistaOrHigher)
            {
                var status = new CHANGEFILTERSTRUCT { cbSize = 8 };
                foreach (uint msg in new[] { WM_DROPFILES, WM_COPYGLOBALDATA, WM_COPYDATA })
                {
                    bool error = false;
                    if (isNt61OrHiger) error = !ChangeWindowMessageFilterEx(handle, msg, MSGFLT_ALLOW, ref status);
                    else error = !ChangeWindowMessageFilter(msg, MSGFLT_ADD);
                    if (error) throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private static bool TryGetDropInfo(int msg, IntPtr wParam, out string[] dropFilePaths, out POINT dropPoint)
        {
            dropFilePaths = null;
            dropPoint = new POINT();
            if (msg != WM_DROPFILES) return false;
            uint fileCount = DragQueryFile(wParam, uint.MaxValue, null, 0);
            dropFilePaths = new string[fileCount];
            for (uint i = 0; i < fileCount; i++)
            {
                var sb = new StringBuilder(MAX_PATH);
                uint result = DragQueryFile(wParam, i, sb, sb.Capacity);
                if (result > 0) dropFilePaths[i] = sb.ToString();
            }
            DragQueryPoint(wParam, out dropPoint);
            DragFinish(wParam);
            return true;
        }
    }