using System;
using System.Windows.Forms;

namespace LocalSearch
{
    public class SettingsDialog : Form
    {
        private ListBox listBoxPaths;
        private Button buttonAdd;
        private Button buttonRemove;
        private Button buttonSave;
        private Button buttonCancel;
        private Button buttonReindex;
        private OpenFileDialog openFileDialog;
        private FolderBrowserDialog folderBrowserDialog;
        private NumericUpDown numericInterval;
        private Label labelInterval;

        public event EventHandler SettingsSaved;
        public event EventHandler ReindexRequested;

        public SettingsDialog()
        {
            this.Text = "Konfiguration";
            this.Size = new System.Drawing.Size(500, 400);

            listBoxPaths = new ListBox { Top = 10, Left = 10, Width = 350, Height = 300, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            buttonAdd = new Button { Text = "Hinzufügen", Top = 320, Left = 10, Width = 100 };
            buttonRemove = new Button { Text = "Entfernen", Top = 320, Left = 120, Width = 100 };
            buttonSave = new Button { Text = "Speichern", Top = 320, Left = 230, Width = 100 };
            buttonCancel = new Button { Text = "Abbrechen", Top = 320, Left = 340, Width = 100 };
            buttonReindex = new Button { Text = "Neu indexieren", Top = 360, Left = 10, Width = 220 };
            openFileDialog = new OpenFileDialog { CheckFileExists = false, ValidateNames = false, FileName = "Ordner auswählen..." };
            folderBrowserDialog = new FolderBrowserDialog();
            numericInterval = new NumericUpDown { Minimum = 1, Maximum = 1440, Value = 60, Top = 10, Left = 370, Width = 100 };
            labelInterval = new Label { Text = "Indexierungsintervall (Minuten):", Top = 12, Left = 370 + 30, Width = 180, AutoSize = true };

            buttonAdd.Click += (s, e) => {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    if (!listBoxPaths.Items.Contains(folderBrowserDialog.SelectedPath))
                        listBoxPaths.Items.Add(folderBrowserDialog.SelectedPath);
                }
            };
            buttonRemove.Click += (s, e) => {
                if (listBoxPaths.SelectedItem != null)
                    listBoxPaths.Items.Remove(listBoxPaths.SelectedItem);
            };
            buttonSave.Click += (s, e) => {
                var pfade = new System.Collections.Specialized.StringCollection();
                foreach (var item in listBoxPaths.Items)
                    pfade.Add(item.ToString());
                Properties.Settings.Default.Suchpfade = pfade;
                Properties.Settings.Default.IndexInterval = (int)numericInterval.Value;
                Properties.Settings.Default.Save();
                SettingsSaved?.Invoke(this, EventArgs.Empty);
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            buttonCancel.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            buttonReindex.Click += (s, e) => {
                ReindexRequested?.Invoke(this, EventArgs.Empty);
            };

            this.Controls.Add(listBoxPaths);
            this.Controls.Add(buttonAdd);
            this.Controls.Add(buttonRemove);
            this.Controls.Add(buttonSave);
            this.Controls.Add(buttonCancel);
            this.Controls.Add(buttonReindex);
            this.Controls.Add(numericInterval);
            this.Controls.Add(labelInterval);
        }

        public void LoadPathsFromSettings()
        {
            listBoxPaths.Items.Clear();
            var pfade = Properties.Settings.Default.Suchpfade;
            if (pfade != null)
            {
                foreach (var pfad in pfade)
                    listBoxPaths.Items.Add(pfad);
            }
            if (Properties.Settings.Default.Properties["IndexInterval"] != null)
                numericInterval.Value = Properties.Settings.Default.IndexInterval;
            else
                numericInterval.Value = 60;
        }
    }
} 