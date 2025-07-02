using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using LocalSearch;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace LocalSearch
{
    public partial class Form1 : Form
    {
        // Hotkey-Konstanten
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_CONTROL = 0x2;
        private const int VK_SPACE = 0x20;
        private const int MOD_ALT = 0x1;
        private int hotkeyId = 1;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private System.Collections.Specialized.StringCollection suchPfade;
        private int indexInterval;
        private bool allowClose = false;
        private List<FileEntry> fileEntries = new List<FileEntry>();
        private System.Threading.Timer indexTimer;
        private DateTime nextIndexTime = DateTime.MinValue;
        private volatile bool isIndexing = false;
        private int sortColumn = -1;
        private SortOrder sortOrder = SortOrder.None;
        private ContextMenuStrip contextMenu;

        // Low-Level-Keyboard-Hook-Variablen
        private static IntPtr hookId = IntPtr.Zero;
        private static WinApi.LowLevelKeyboardProc hookCallback;

        public class FileEntry
        {
            public string FullPath { get; set; }
            public string Name { get; set; }
            public long Size { get; set; }
            public DateTime Created { get; set; }
            public DateTime LastModified { get; set; }
        }

        public Form1()
        {
            InitializeComponent();
            // OwnerDraw aktivieren
            this.listViewResults.OwnerDraw = true;
            this.listViewResults.DrawColumnHeader += listViewResults_DrawColumnHeader;
            this.listViewResults.DrawSubItem += listViewResults_DrawSubItem;

            // Kontextmenü erstellen
            contextMenu = new ContextMenuStrip();
            var pfadOeffnenItem = new ToolStripMenuItem("Pfad Öffnen");
            pfadOeffnenItem.Click += pfadOeffnenItem_Click;
            var uriKopierenItem = new ToolStripMenuItem("URI kopieren");
            uriKopierenItem.Click += uriKopierenItem_Click;
            var oeffnenItem = new ToolStripMenuItem("Öffnen");
            oeffnenItem.Click += oeffnenItem_Click;
            contextMenu.Items.AddRange(new ToolStripItem[] { oeffnenItem, pfadOeffnenItem, uriKopierenItem });
            this.listViewResults.ContextMenuStrip = contextMenu;
            this.listViewResults.MouseUp += listViewResults_MouseUp;
            this.listViewResults.MouseDown += listViewResults_MouseDown;
            this.listViewResults.ItemDrag += listViewResults_ItemDrag;
            this.listViewResults.MouseDoubleClick += listViewResults_MouseDoubleClick;
            this.beendenMenuItem.Click += (s, e) => {
                allowClose = true;
                Application.Exit();
            };
            this.buttonSettings.Click += (s, e) => {
                using (var dlg = new SettingsDialog())
                {
                    dlg.LoadPathsFromSettings();
                    dlg.SettingsSaved += (sender, args) => {
                        suchPfade = Properties.Settings.Default.Suchpfade;
                        indexInterval = Properties.Settings.Default.IndexInterval;
                        indexTimer.Change(0, indexInterval * 60 * 1000);
                        nextIndexTime = DateTime.Now.AddMinutes(indexInterval);
                        UpdateStats();
                    };
                    dlg.ReindexRequested += (sender, args) => {
                        // Sofortige Neuindexierung
                        ThreadPool.QueueUserWorkItem(IndexFiles);
                    };
                    dlg.ShowDialog();
                }
            };
            this.oeffnenMenuItem.Click += (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.BringToFront();
                this.Activate();
                this.textBoxSearch.Focus();
                if (!string.IsNullOrEmpty(textBoxSearch.Text))
                {
                    textBoxSearch.SelectAll();
                }
            };
            this.aboutMenuItem.Click += (s, e) => {
                using (var dlg = new AboutDialog())
                {
                    dlg.ShowDialog(this);
                }
            };
            suchPfade = Properties.Settings.Default.Suchpfade;
            indexInterval = Properties.Settings.Default.IndexInterval;
            // Timer für Indexierung mit indexInterval initialisieren
            indexTimer = new System.Threading.Timer(IndexFiles, null, 0, indexInterval * 60 * 1000);
            // Event-Handler für Live-Suche
            this.textBoxSearch.TextChanged += textBoxSearch_TextChanged;
            this.listViewResults.ColumnClick += listViewResults_ColumnClick;
            this.Resize += Form1_Resize; // Event-Handler für Resize
            this.Load += Form1_Load; // Event-Handler für Load
            this.FormClosing += Form1_FormClosing; // Event-Handler für FormClosing
            UpdateStatus("Bereit");
            UpdateStats();
            AdjustColumnWidths(); // Beim Start einmalig anpassen

            hookCallback = HookProc;
            hookId = WinApi.SetHook(hookCallback);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Fensterposition und -größe wiederherstellen
            if (Properties.Settings.Default.WindowLocation != null && !Properties.Settings.Default.WindowLocation.IsEmpty)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = Properties.Settings.Default.WindowLocation;
            }
            if (Properties.Settings.Default.WindowSize != null && !Properties.Settings.Default.WindowSize.IsEmpty)
            {
                this.Size = Properties.Settings.Default.WindowSize;
            }
            // Fenster verstecken und nur im Tray anzeigen
            this.Hide();
            this.ShowInTaskbar = false;
            this.notifyIcon1.Visible = true;
            AdjustColumnWidths(); // Auch beim Laden anpassen
        }

        protected override void WndProc(ref Message m)
        {
            // Umschalt-Logik (Toggle) für STRG+Leertaste
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == hotkeyId)
            {
                if (this.Visible)
                {
                    // Fenster verstecken
                    this.Hide();
                    this.ShowInTaskbar = false;
                }
                else
                {
                    // Fenster anzeigen und in den Vordergrund bringen
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.ShowInTaskbar = true;
                    this.BringToFront();
                    this.Activate();
                    this.textBoxSearch.Focus();
                }
                if (!string.IsNullOrEmpty(textBoxSearch.Text))
                {
                    textBoxSearch.SelectAll();
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Nur Escape schließt das Fenster
            if (keyData == Keys.Escape && this.Visible)
            {
                this.Hide();
                this.ShowInTaskbar = false;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
                return;
            }
            // Hook entfernen
            WinApi.UnhookWindowsHookEx(hookId);
            base.OnFormClosing(e);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
            AdjustColumnWidths(); // Spaltenbreiten anpassen
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (this.Visible)
            {
                this.textBoxSearch.Focus();
                if (!string.IsNullOrEmpty(textBoxSearch.Text))
                {
                    textBoxSearch.SelectAll();
                }
            }
        }

        private void IndexFiles(object state)
        {
            isIndexing = true;
            UpdateStatus("Indexierung läuft...");
            var newEntries = new List<FileEntry>();
            if (suchPfade != null)
            {
                foreach (string pfad in suchPfade)
                {
                    if (Directory.Exists(pfad))
                    {
                        try
                        {
                            foreach (var file in Directory.EnumerateFiles(pfad, "*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    var info = new FileInfo(file);
                                    newEntries.Add(new FileEntry
                                    {
                                        FullPath = info.FullName,
                                        Name = info.Name,
                                        Size = info.Length,
                                        Created = info.CreationTime,
                                        LastModified = info.LastWriteTime
                                    });
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
            }
            Interlocked.Exchange(ref fileEntries, newEntries);
            isIndexing = false;
            nextIndexTime = DateTime.Now.AddMinutes(indexInterval);
            UpdateStatus("Bereit");
            UpdateStats();
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            string query = textBoxSearch.Text.Trim();
            listViewResults.Items.Clear();
            if (string.IsNullOrEmpty(query))
                return;

            // Suchwörter extrahieren
            var suchwoerter = query
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant())
                .ToArray();

            // Sichtbare Zeilen berechnen
            int visibleCount = (int)Math.Floor((double)listViewResults.ClientSize.Height / listViewResults.Font.Height);

            var results = fileEntries
                .Where(f =>
                    suchwoerter.All(wort =>
                        f.Name.ToLowerInvariant().Contains(wort) ||
                        f.FullPath.ToLowerInvariant().Contains(wort)
                    )
                )
                .OrderByDescending(f => f.LastModified)
                .Take(visibleCount)
                .ToList();

            foreach (var entry in results)
            {
                var item = new ListViewItem(entry.Name);
                item.SubItems.Add(entry.LastModified.ToString());
                item.SubItems.Add(entry.Created.ToString());
                item.SubItems.Add(entry.Size.ToString());
                item.SubItems.Add(Path.GetExtension(entry.Name));
                item.ToolTipText = entry.FullPath;
                listViewResults.Items.Add(item);
            }
        }

        private void UpdateStatus(string status)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateStatus(status)));
                return;
            }
            toolStripStatusLabelStatus.Text = $"Status: {status}";
            if (isIndexing)
                toolStripStatusLabelStatus.Text += " (Indexierung läuft)";
        }

        private void UpdateStats()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(UpdateStats));
                return;
            }
            toolStripStatusLabelStats.Text = $"Indexierte Elemente: {fileEntries.Count} | Nächster Lauf: {(nextIndexTime > DateTime.MinValue ? nextIndexTime.ToString("HH:mm:ss") : "-")}";
        }

        private void listViewResults_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == sortColumn)
            {
                // Sortierreihenfolge umkehren
                sortOrder = (sortOrder == SortOrder.Ascending) ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                sortColumn = e.Column;
                sortOrder = SortOrder.Ascending;
            }
            listViewResults.ListViewItemSorter = new ListViewItemComparer(sortColumn, sortOrder);
            listViewResults.Sort();
        }

        private class ListViewItemComparer : System.Collections.IComparer
        {
            private int col;
            private SortOrder order;
            public ListViewItemComparer(int column, SortOrder order)
            {
                this.col = column;
                this.order = order;
            }
            public int Compare(object x, object y)
            {
                string xText = ((ListViewItem)x).SubItems[col].Text;
                string yText = ((ListViewItem)y).SubItems[col].Text;
                int result;
                // Für Spalte Größe numerisch, für Datum als DateTime, sonst als String vergleichen
                if (col == 3) // Größe
                {
                    long xVal = long.TryParse(xText, out var xv) ? xv : 0;
                    long yVal = long.TryParse(yText, out var yv) ? yv : 0;
                    result = xVal.CompareTo(yVal);
                }
                else if (col == 1 || col == 2) // LastModified oder Created
                {
                    DateTime xDate = DateTime.TryParse(xText, out var xd) ? xd : DateTime.MinValue;
                    DateTime yDate = DateTime.TryParse(yText, out var yd) ? yd : DateTime.MinValue;
                    result = xDate.CompareTo(yDate);
                }
                else
                {
                    result = string.Compare(xText, yText, StringComparison.CurrentCultureIgnoreCase);
                }
                return order == SortOrder.Descending ? -result : result;
            }
        }

        // OwnerDraw: Spaltenkopf zeichnen
        private void listViewResults_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        // OwnerDraw: SubItem zeichnen
        private void listViewResults_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            // Nur für die Namensspalte (0) hervorheben
            if (e.ColumnIndex == 0)
            {
                string suchText = textBoxSearch.Text.Trim();
                var suchwoerter = suchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant())
                    .ToArray();
                string name = e.SubItem.Text;
                Rectangle bounds = e.Bounds;
                using (var bg = new SolidBrush(e.Item.Selected ? SystemColors.Highlight : e.SubItem.BackColor))
                {
                    e.Graphics.FillRectangle(bg, bounds);
                }
                // Text zeichnen, Fundstellen fett
                int x = bounds.X + 2;
                int y = bounds.Y + 1;
                string rest = name;
                Font normalFont = e.Item.Selected ? new Font(e.SubItem.Font, FontStyle.Bold) : e.SubItem.Font;
                Font boldFont = new Font(e.SubItem.Font, FontStyle.Bold);
                Color textColor = e.Item.Selected ? SystemColors.HighlightText : e.SubItem.ForeColor;
                while (!string.IsNullOrEmpty(rest))
                {
                    int minIndex = -1;
                    int minLength = 0;
                    string foundWord = null;
                    foreach (var wort in suchwoerter)
                    {
                        if (string.IsNullOrEmpty(wort)) continue;
                        int idx = rest.ToLowerInvariant().IndexOf(wort);
                        if (idx >= 0 && (minIndex == -1 || idx < minIndex))
                        {
                            minIndex = idx;
                            minLength = wort.Length;
                            foundWord = rest.Substring(idx, wort.Length);
                        }
                    }
                    if (minIndex == -1)
                    {
                        // Kein Suchwort mehr, Rest normal zeichnen
                        TextRenderer.DrawText(e.Graphics, rest, normalFont, new Point(x, y), textColor);
                        break;
                    }
                    // Text vor dem Treffer normal zeichnen
                    if (minIndex > 0)
                    {
                        string vor = rest.Substring(0, minIndex);
                        TextRenderer.DrawText(e.Graphics, vor, normalFont, new Point(x, y), textColor);
                        x += TextRenderer.MeasureText(e.Graphics, vor, normalFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width - 1;
                    }
                    // Treffer fett zeichnen
                    TextRenderer.DrawText(e.Graphics, foundWord, boldFont, new Point(x, y), textColor);
                    x += TextRenderer.MeasureText(e.Graphics, foundWord, boldFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width - 1;
                    rest = rest.Substring(minIndex + minLength);
                }
                boldFont.Dispose();
            }
            else
            {
                e.DrawDefault = true;
            }
        }

        private void listViewResults_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var info = listViewResults.HitTest(e.Location);
                if (info.Item != null)
                {
                    info.Item.Selected = true;
                    contextMenu.Show(listViewResults, e.Location);
                }
            }
        }

        private void listViewResults_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var info = listViewResults.HitTest(e.Location);
                if (info.Item != null)
                {
                    info.Item.Selected = true;
                }
            }
        }

        private void listViewResults_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (listViewResults.SelectedItems.Count > 0)
            {
                var item = listViewResults.SelectedItems[0];
                var eintrag = fileEntries.FirstOrDefault(f => f.Name == item.Text && item.ToolTipText == f.FullPath);
                if (eintrag != null)
                {
                    string[] files = new string[] { eintrag.FullPath };
                    DataObject data = new DataObject(DataFormats.FileDrop, files);
                    listViewResults.DoDragDrop(data, DragDropEffects.Copy);
                }
            }
        }

        private void pfadOeffnenItem_Click(object sender, EventArgs e)
        {
            if (listViewResults.SelectedItems.Count > 0)
            {
                var item = listViewResults.SelectedItems[0];
                var eintrag = fileEntries.FirstOrDefault(f => f.Name == item.Text && item.ToolTipText == f.FullPath);
                if (eintrag != null)
                {
                    string argument = "/select,\"" + eintrag.FullPath + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
            }
        }

        private void uriKopierenItem_Click(object sender, EventArgs e)
        {
            if (listViewResults.SelectedItems.Count > 0)
            {
                var item = listViewResults.SelectedItems[0];
                var eintrag = fileEntries.FirstOrDefault(f => f.Name == item.Text && item.ToolTipText == f.FullPath);
                if (eintrag != null)
                {
                    // file:// URL erzeugen
                    string fileUrl = new Uri(eintrag.FullPath).AbsoluteUri;
                    Clipboard.SetText(fileUrl);
                }
            }
        }

        private void AdjustColumnWidths()
        {
            // Feste Breiten für die anderen Spalten
            int breiteLastModified = 120;
            int breiteCreated = 120;
            int breiteSize = 80;
            int breiteFiletype = 100;
            int padding = 5; // Optionaler Abstand

            // Gesamtbreite des ListViews
            int gesamtBreite = listViewResults.ClientSize.Width;

            // Breite für die Dateiname-Spalte berechnen
            int breiteFilename = gesamtBreite - (breiteLastModified + breiteCreated + breiteSize + breiteFiletype + padding);
            if (breiteFilename < 100) breiteFilename = 100; // Mindestbreite

            if (listViewResults.Columns.Count >= 5)
            {
                listViewResults.Columns[0].Width = breiteFilename;
                listViewResults.Columns[1].Width = breiteLastModified;
                listViewResults.Columns[2].Width = breiteCreated;
                listViewResults.Columns[3].Width = breiteSize;
                listViewResults.Columns[4].Width = breiteFiletype;
            }
        }

        // Low-Level-Keyboard-Hook-Callback
        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WinApi.WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                // STRG+Leertaste prüfen (ohne ALT)
                if (vkCode == VK_SPACE &&
                    (WinApi.GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0)
                {
                    // Toggle-Fenster
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (this.Visible)
                        {
                            this.Hide();
                            this.ShowInTaskbar = false;
                        }
                        else
                        {
                            this.Show();
                            this.WindowState = FormWindowState.Normal;
                            this.ShowInTaskbar = true;
                            this.BringToFront();
                            this.Activate();
                            this.textBoxSearch.Focus();
                        }
                    });
                    if (!string.IsNullOrEmpty(textBoxSearch.Text))
                    {
                        textBoxSearch.SelectAll();
                    }
                    return (IntPtr)1; // Event handled
                }
            }
            return WinApi.CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Fensterposition und -größe speichern
            if (this.WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.WindowLocation = this.Location;
                Properties.Settings.Default.WindowSize = this.Size;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                // Im Maximized-Fall die RestoreBounds speichern
                Properties.Settings.Default.WindowLocation = this.RestoreBounds.Location;
                Properties.Settings.Default.WindowSize = this.RestoreBounds.Size;
            }
            Properties.Settings.Default.Save();
        }

        private void listViewResults_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listViewResults.SelectedItems.Count > 0)
            {
                OpenSelectedFile();
            }
        }

        private void oeffnenItem_Click(object sender, EventArgs e)
        {
            OpenSelectedFile();
        }

        private void OpenSelectedFile()
        {
            if (listViewResults.SelectedItems.Count > 0)
            {
                var item = listViewResults.SelectedItems[0];
                var eintrag = fileEntries.FirstOrDefault(f => f.Name == item.Text && item.ToolTipText == f.FullPath);
                if (eintrag != null)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(eintrag.FullPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Datei konnte nicht geöffnet werden: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }

    // Hilfsklasse für WinAPI
    internal static class WinApi
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(Keys vKey);

        public static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            // Für globale Hooks in .NET: IntPtr.Zero als HINSTANCE
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, IntPtr.Zero, 0);
        }
    }
}
