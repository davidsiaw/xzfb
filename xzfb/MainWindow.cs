using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace xzfb
{
    public partial class MainWindow : Form
    {
        /// <summary>
        /// Process Specific Access Mode.
        /// http://msdn.microsoft.com/en-us/library/ms684880(VS.85).aspx
        /// </summary>
        [Flags]
        public enum ProcessSpecificAccess : uint
        {
            PROCESS_VM_READ = 0x0010,
            PROCESS_VM_WRITE = 0x0020
        }
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        /*
            Includes some P/Invoked Methods();
        */

        /// <summary>
        /// Retrieves the fully-qualified path for the file containing the specified module.
        /// http://msdn.microsoft.com/en-us/library/ms683198(VS.85).aspx
        /// </summary>
        /// <param name="hProcess"></param>
        /// <param name="hModule"></param>
        /// <param name="lpBaseName"></param>
        /// <param name="nSize"></param>
        /// <returns></returns>
        [DllImport("psapi.dll")] //Supported under Windows Vista and Windows Server 2008.
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName,
        [In] [MarshalAs(UnmanagedType.U4)] int nSize);


        /// <summary>
        /// Retrieves the identifier of the thread that created the specified window and, optionally, the identifier of the process that created the window.
        /// http://msdn.microsoft.com/en-us/library/ms633522(VS.85).aspx
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="processId"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, uint processId);

        /// <summary>
        /// Retrieves the Path of a running process.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        string GetProcessPath(IntPtr hwnd)
        {
            try
            {
                uint pid = 0;
                GetWindowThreadProcessId(hwnd, out pid);

                IntPtr hProcess = OpenProcess(ProcessAccessFlags.QueryInformation, true, pid);
                StringBuilder sb = new StringBuilder(256);
                GetModuleFileNameEx(hProcess, IntPtr.Zero, sb, sb.Capacity);
                CloseHandle(hProcess);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }

        Timer t = new Timer();
        IntPtr hwnd;
        ScreenCapture sc = new ScreenCapture();
        public MainWindow()
        {
            InitializeComponent();
            this.DoubleBuffered = true;

            foreach (KeyValuePair<IntPtr, string> window in OpenWindowGetter.GetOpenWindows())
            {
                IntPtr handle = window.Key;
                string title = window.Value;
                string path = GetProcessPath(handle);

                if(title.IndexOf("Yosuga") != -1 && path.IndexOf("mpc") != -1)
                {
                    hwnd = handle;
                }
                Console.WriteLine("{0}: {1} {2}", handle, title, path);
            }
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            t.Interval = 20;
            t.Tick += refreshWindow;
            t.Start();
        }

        private void refreshWindow(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        private Image capture()
        {
            if(hwnd.ToInt64() == 0)
            {
                return sc.CaptureScreen();
            }
            return sc.CaptureWindow(hwnd);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.FillRectangle(new SolidBrush(Color.FromArgb(255,0x85,0xe3,0xff)), 0, 0, this.Width, this.Height);

            using (Image img = capture())
            {
                Point tlmargin = new Point(9, 40);
                Point brmargin = new Point(9, 9);

                Rectangle srcrect = new Rectangle(tlmargin.X, tlmargin.Y,
                    img.Width - tlmargin.X - brmargin.X,
                    img.Height - tlmargin.Y - brmargin.Y);

                Rectangle dstrect = new Rectangle(0, 0, srcrect.Width, srcrect.Height);

                using (Bitmap b = new Bitmap(dstrect.Width, dstrect.Height))
                {
                    using (Graphics bg = Graphics.FromImage(b))
                    {
                        bg.DrawImage(img, dstrect, srcrect, GraphicsUnit.Pixel);
                    }

                    using (Bitmap b2 = new Bitmap(dstrect.Width, dstrect.Height))
                    {
                        Rectangle rect = new Rectangle(0, 0, b.Width, b.Height);

                        System.Drawing.Imaging.BitmapData srcData =
                            b.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                            b.PixelFormat);

                        System.Drawing.Imaging.BitmapData bmpData =
                            b2.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                            b2.PixelFormat);

                        // Get the address of the first line.
                        IntPtr ptr = bmpData.Scan0;
                        IntPtr sptr = srcData.Scan0;

                        unsafe
                        {
                            uint* bmp = (uint*)ptr;
                            uint* src = (uint*)sptr;

                            int w = b.Width;
                            int h = b.Height;
                            int s = bmpData.Stride / 4;
                            
                            for (int x = 0; x < w; x++)
                            {
                                for (int y = 0; y < h; y++)
                                {
                                    double scl = 1;
                                    double x0 = ((2 * (double)x / (double)w) - 1) * scl;
                                    double y0 = ((2 * (double)y / (double)h) - 1) * scl;

                                    double m0 = Math.Sqrt(x0 * x0 + y0 * y0);
                                    double t0 = Math.Atan2(y0, x0);

                                    double m1 = Math.Sqrt(m0);
                                    double t1 = t0 / 2;

                                    Complex c0 = new Complex(x0, y0);
                                    Complex c1 = c0 * c0;

                                    double x1 = c1.Real;
                                    double y1 = c1.Imaginary;

                                    //double x1 = m1 * Math.Cos(t1);
                                    //double y1 = m1 * Math.Sin(t1);

                                    int xx = (int)((double)w / 2 * (x1 + 1) / scl);
                                    int yy = (int)((double)h / 2 * (y1 + 1) / scl);

                                    xx = (w + xx) % w;
                                    yy = (h + yy) % h;

                                    if (xx < 0 || yy < 0 || xx >= w || yy >= h)
                                    {
                                        bmp[x + y * s] = (uint)xx + (uint)0xff000000;
                                    }
                                    else
                                    {
                                        bmp[x + y * s] = src[xx + yy * s];
                                    }
                                }
                            }
                        }

                        // Unlock the bits.
                        b.UnlockBits(srcData);
                        b2.UnlockBits(bmpData);

                        g.DrawImage(b2, new Point(0, 0));
                    }
                }
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }
    }
}
