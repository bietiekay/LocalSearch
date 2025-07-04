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
        private ListBox listBoxIgnoreTypes;
        private Button buttonAddIgnoreType;
        private Button buttonRemoveIgnoreType;

        public event EventHandler SettingsSaved;
        public event EventHandler ReindexRequested;

        public SettingsDialog()
        {
            this.Text = "Konfiguration";
            this.Size = new System.Drawing.Size(700, 500);
            this.MinimumSize = new System.Drawing.Size(700, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            // Pfadverwaltung (links)
            listBoxPaths = new ListBox { Top = 20, Left = 20, Width = 350, Height = 320, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
            buttonAdd = new Button { Text = "Hinzufügen", Top = 350, Left = 20, Width = 110 };
            buttonRemove = new Button { Text = "Entfernen", Top = 350, Left = 140, Width = 110 };

            // Ignorierte Dateitypen (rechts oben)
            listBoxIgnoreTypes = new ListBox { Top = 20, Left = 400, Width = 120, Height = 180, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            buttonAddIgnoreType = new Button { Text = "Typ +", Top = 210, Left = 400, Width = 58 };
            buttonRemoveIgnoreType = new Button { Text = "Typ -", Top = 210, Left = 462, Width = 58 };

            // Intervall (rechts mittig)
            labelInterval = new Label { Text = "Indexierungsintervall (Minuten):", Top = 260, Left = 400, Width = 200, AutoSize = true };
            numericInterval = new NumericUpDown { Minimum = 1, Maximum = 1440, Value = 60, Top = 285, Left = 400, Width = 120 };

            // Hauptaktions-Buttons (unten)
            buttonSave = new Button { Text = "Speichern", Top = 400, Left = 20, Width = 120 };
            buttonCancel = new Button { Text = "Abbrechen", Top = 400, Left = 150, Width = 120 };
            buttonReindex = new Button { Text = "Neu indexieren", Top = 400, Left = 400, Width = 180 };

            openFileDialog = new OpenFileDialog { CheckFileExists = false, ValidateNames = false, FileName = "Ordner auswählen..." };
            folderBrowserDialog = new FolderBrowserDialog();

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
                var ignoreTypes = new System.Collections.Specialized.StringCollection();
                foreach (var item in listBoxIgnoreTypes.Items)
                    ignoreTypes.Add(item.ToString());
                Properties.Settings.Default.IgnoreFileTypes = ignoreTypes;
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
            buttonAddIgnoreType.Click += (s, e) => {
                using (var dlg = new InputBoxForm("Dateiendung (z.B. .tmp):", "."))
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        string input = dlg.InputText;
                        if (!string.IsNullOrWhiteSpace(input))
                        {
                            input = input.Trim().ToLowerInvariant();
                            if (!input.StartsWith(".")) input = "." + input;
                            if (!listBoxIgnoreTypes.Items.Contains(input))
                                listBoxIgnoreTypes.Items.Add(input);
                        }
                    }
                }
            };
            buttonRemoveIgnoreType.Click += (s, e) => {
                if (listBoxIgnoreTypes.SelectedItem != null)
                    listBoxIgnoreTypes.Items.Remove(listBoxIgnoreTypes.SelectedItem);
            };

            this.Controls.Add(listBoxPaths);
            this.Controls.Add(buttonAdd);
            this.Controls.Add(buttonRemove);
            this.Controls.Add(listBoxIgnoreTypes);
            this.Controls.Add(buttonAddIgnoreType);
            this.Controls.Add(buttonRemoveIgnoreType);
            this.Controls.Add(labelInterval);
            this.Controls.Add(numericInterval);
            this.Controls.Add(buttonSave);
            this.Controls.Add(buttonCancel);
            this.Controls.Add(buttonReindex);
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
            // Ignorierte Dateitypen laden
            listBoxIgnoreTypes.Items.Clear();
            var ignoreTypes = Properties.Settings.Default.IgnoreFileTypes;
            if (ignoreTypes != null)
            {
                foreach (var typ in ignoreTypes)
                    listBoxIgnoreTypes.Items.Add(typ);
            }
        }

        // Hilfsdialog für Eingabe
        private class InputBoxForm : Form
        {
            public string InputText => textBox.Text;
            private TextBox textBox;
            public InputBoxForm(string prompt, string defaultValue = "")
            {
                this.Text = "Eingabe";
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.Width = 350;
                this.Height = 140;
                var label = new Label { Text = prompt, Left = 10, Top = 10, Width = 320 };
                textBox = new TextBox { Left = 10, Top = 35, Width = 320, Text = defaultValue };
                var buttonOk = new Button { Text = "OK", Left = 170, Width = 75, Top = 70, DialogResult = DialogResult.OK };
                var buttonCancel = new Button { Text = "Abbrechen", Left = 255, Width = 75, Top = 70, DialogResult = DialogResult.Cancel };
                buttonOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                buttonCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
                this.Controls.Add(label);
                this.Controls.Add(textBox);
                this.Controls.Add(buttonOk);
                this.Controls.Add(buttonCancel);
                this.AcceptButton = buttonOk;
                this.CancelButton = buttonCancel;
            }
        }
    }
} 