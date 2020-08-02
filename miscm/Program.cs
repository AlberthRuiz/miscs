using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
namespace miscm {
    class Program {
        #region Init
        /// <summary>
        /// * NAME              : Popup_killer_tool
        /// * CREATION DATE     : 04/22/2020
        /// * AUTOR             : Christiam Alberth Mendoza Ruiz        
        /// * DESCRIPTION       : Utility to close pop-up windows in different processes, taking a config.xml file as a reference 
        /// </summary>
        protected struct process { public string name; public List<popup> popups; }
        protected struct popup { public string title; public IntPtr phWnd; public string contain; public string button; }
        private static List<process> lstProcesses;
        private static int timeSleep;
        private static string pathLog = Environment.CurrentDirectory;
        private static string logFileName = "logFile.txt";
        private static void initialize() {
            writeLog($@"{pathLog}\{logFileName}", string.Empty);
            XmlDocument doc = new XmlDocument();
            doc.Load("config.xml");
            lstProcesses = new List<process>();
            foreach (XmlNode node in doc.DocumentElement) {
                if (node.Name.Contains("timeSleep")) {
                    timeSleep = int.Parse(node.InnerText);
                } else {
                    process stProcess = new process() { name = node.Attributes[0].InnerText, popups = new List<popup>() };
                    foreach (XmlNode child in node.ChildNodes) {
                        stProcess.popups.Add(new popup() { title = child.Attributes[0].InnerText, contain = child.Attributes[1].InnerText, button = child.Attributes.Count > 2 ? child.Attributes[2].InnerText : "" });
                    }
                    lstProcesses.Add(stProcess);
                }
            }
        }
        #endregion

        #region Log
        static void writeLog(string path, string line) {
            if (!File.Exists(path)) {
                File.WriteAllText($@"{pathLog}\{logFileName}", string.Empty);
            }
            if (File.Exists(path) && line.Trim() != "") {
                File.AppendAllText($@"{pathLog}\{logFileName}", $"{line}{Environment.NewLine}");
            }

        }
        #endregion

        static string GetWindowCaption(IntPtr hwnd) {
            StringBuilder sb = new StringBuilder(256);
            GetWindowCaption(hwnd, sb, 256);
            return sb.ToString();
        }
        static IEnumerable<popup> GetWindowText(Process p) {
            List<popup> titles = new List<popup>();
            foreach (ProcessThread t in p.Threads) {
                EnumThreadWindows(t.Id, (hWnd, lParam) => {
                    StringBuilder text = new StringBuilder(200);
                    GetWindowText(hWnd, text, 200);
                    titles.Add(new popup() { phWnd = hWnd, title = text.ToString() });
                    return true;
                }, IntPtr.Zero);
            }
            return titles;
        }
        static List<IntPtr> GetAllChildrenWindowHandles(IntPtr hParent, int maxCount) {
            List<IntPtr> children = new List<IntPtr>();
            int ct = 0;
            IntPtr prevChild = IntPtr.Zero;
            IntPtr currChild = IntPtr.Zero;
            while (true && ct < maxCount) {
                currChild = FindWindowEx(hParent, prevChild, null, null);
                if (currChild == IntPtr.Zero) {
                    break;
                }

                children.Add(currChild);
                prevChild = currChild;
                ++ct;
            }
            return children;
        }
        static void Main(string[] args) {
            initialize();
            while (true) {
                Process[] processlist = null;
                List<popup> lsPopups = null;
                string line = "";
                foreach (process proc in lstProcesses) {
                    processlist = Process.GetProcessesByName(proc.name);
                    foreach (var item in processlist) {
                        lsPopups = GetWindowText(item).ToList();
                        foreach (popup popup in proc.popups) {
                            foreach (var children in lsPopups.Where(o => o.title.Contains(popup.title))) {
                                var nodes = GetAllChildrenWindowHandles(children.phWnd, 254);
                                foreach (var node in nodes) {
                                    string caption = GetWindowCaption(node);
                                    if (caption.Contains(popup.contain) || caption.Contains(popup.button)) {
                                        SendMessage(children.phWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                                        line = $"HOSTNAME: {Environment.MachineName} | PROCESS: {proc.name} |POPUP: {popup.title} | TEXT: {caption.Trim()} | TIMESPAM: {DateTime.Now} | STATE: Closed";
                                        Console.WriteLine(line);
                                        writeLog($@"{pathLog}\{logFileName}", line);

                                    }
                                }
                            }
                        }
                    }
                }
                GC.Collect();
                Thread.Sleep(timeSleep);

            }
        }
        #region External DLL
        const int WM_GETTEXT = 0x0D;
        const int WM_GETTEXTLENGTH = 0x0E;
        const UInt32 WM_CLOSE = 0x0010;
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private extern static bool EnumThreadWindows(int threadId, EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32", SetLastError = true, CharSet = CharSet.Auto)]
        private extern static int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "GetWindowText", CharSet = CharSet.Auto)]
        static extern IntPtr GetWindowCaption(IntPtr hwnd, StringBuilder lpString, int maxCount);
        #endregion
    }
}
