using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;

namespace LocalSearch
{
    internal static class Program
    {
        // Für Single Instance
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        private const int SW_RESTORE = 9;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "LocalSearch_SingleInstanceMutex", out createdNew))
            {
                if (createdNew)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1());
                }
                else
                {
                    // Bereits laufende Instanz suchen und in den Vordergrund bringen
                    IntPtr hWnd = FindWindow(null, "LocalSearch");
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE); // Falls minimiert
                        SetForegroundWindow(hWnd);
                    }
                    MessageBox.Show("LocalSearch läuft bereits und wurde in den Vordergrund gebracht.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
