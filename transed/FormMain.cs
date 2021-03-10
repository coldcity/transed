using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Transed {
    public partial class MainForm : Form {
        UndoRedoStack undoRedoStack = new UndoRedoStack();      // Double stack to track undo/redo steps
        string filename;                                        // Current filename
        string fileLocation;                                    // Current full path filename
        bool isDirty;                                           // Flag to indicate file has changed without save
        bool isNewfile;                                         // Flag to indicate file has never been saved
        bool captureChangesToUndoStack = true;                  // Flag to gate whether undo/redo stacks are capturing
        Timer undoCaptureTimer = new Timer();                   // Timer to fire state-capture for undo/redo

        // Substitution dictionary
        Dictionary<string, string> subs = new Dictionary<string, string>() {
            // Replace
            { "E", "Ꜣ" }, { "e", "ꜣ" },
            { "I", "Ỉ" }, { "i", "ỉ" },
            //{ "A", "Ꜥ" }, { "a", "ꜥ" },
            { "A", "Ꜥ" }, { "a", "Ꜥ" },
            { "H.", "Ḥ" }, { "h.", "ḥ" },
            { "HU", "Ḫ" }, { "hu", "ḫ" }, { "Hu", "Ḫ" }, { "hU", "ḫ" }, { "X", "Ḫ" }, { "x", "ḫ" },
            { "H_", "H̱" }, { "h_", "ẖ" },
            { "SV", "Š" }, { "sv", "š" }, { "Sv", "Š" }, { "sV", "š" }, { "S.", "Š" }, { "s.", "š" },
            { "K.", "Ḳ" }, { "k.", "ḳ" }, { "Q", "Ḳ" }, { "q", "ḳ" },
            { "T_", "Ṯ" }, { "t_", "ṯ" },
            { "D_", "Ḏ" }, { "d_", "ḏ" }, { "J", "Ḏ" }, { "j", "ḏ" },

            // Superscript numbers
            { "0", "⁰" }, { "1", "¹" }, { "2", "²" }, { "3", "³" }, { "4", "⁴" }, { "5", "⁵" }, { "6", "⁶" }, { "7", "⁷" }, { "8", "⁸" }, { "9", "⁹" },

            // Strip
            { "C", "" }, { "c", "" },
            { "L", "" }, { "l", "" },
            { "O", "" }, { "o", "" },
            { "U", "" }, { "u", "" },
            { "V", "" }, { "v", "" },
            { "Z", "" }, { "z", "" }
        };

        // Constructor
        public MainForm() {
            InitializeComponent();
            NewFile();
            undoCaptureTimer.Tick += undoCaptureTimer_Tick;
            undoCaptureTimer.Interval = 500;
        }

        // Update window title and menuitem enable states
        private void UpdateView() {
            this.Text = isDirty ? filename + "*" : filename;
            this.Text += " | Egyptian Hieroglyph Transliteration Pad";
            undoEditMenu.Enabled = undoRedoStack.CanUndo() ? true : false;
            redoEditMenu.Enabled = undoRedoStack.CanRedo() ? true : false;
        }

        // Init a new file
        private void NewFile() {
            txtArea.Text = "";
            filename = "Untitled.txt";
            isDirty = false;
            isNewfile = true;
            UpdateView();
        }

        // Read given file from disk, and update fileLocation
        private string ReadFile(string newFileLocation) {
            string content;
            fileLocation = newFileLocation;
            Stream stream = File.Open(fileLocation, FileMode.Open, FileAccess.ReadWrite);
            using (StreamReader streamReader = new StreamReader(stream))
                content = streamReader.ReadToEnd();
            UpdateFileStatus();
            return content;
        }

        // Write file to disk, and update fileLocation
        private void WriteFile(string newFileLocation, string[] lines) {
            fileLocation = newFileLocation;
            Stream stream = File.Open(fileLocation, FileMode.OpenOrCreate, FileAccess.Write);
            using (StreamWriter streamwriter = new StreamWriter(stream))
                foreach (string line in lines)
                    streamwriter.WriteLine(line);
            UpdateFileStatus();
            UpdateView();
        }

        // Save File, or divert to Save As for a new file
        private void SaveFile() {
            if (isNewfile)
                SaveFileAs();
            else
                WriteFile(fileLocation, txtArea.Lines);
        }

        // Save File As
        private void SaveFileAs() {
            SaveFileDialog fileSave = new SaveFileDialog();
            fileSave.Filter = "Text(*.txt)|*.txt";
            if (fileSave.ShowDialog() == DialogResult.OK)
                WriteFile(fileSave.FileName, txtArea.Lines);
        }

        // Allow user to save or cancel before a buffer-nuking action
        private bool OKToProceedWhileDirty() {
            if (!isDirty) return true;                  // Not dirty? Always fine

            DialogResult result = MessageBox.Show("Save changes to " + filename + " first?", "Transed", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)          // Cancelled? Not fine
                return false;
            else if (result == DialogResult.Yes)        // Save, then fine
                SaveFile();

            return true;
        }

        // Update filename and flags after a read/write
        private void UpdateFileStatus() {
            filename = fileLocation.Substring(fileLocation.LastIndexOf("\\") + 1);
            isDirty = false;
            isNewfile = false;
        }

        // Update statusbar
        private void UpdateStatus() {
            int pos = txtArea.SelectionStart;
            int line = txtArea.GetLineFromCharIndex(pos) + 1;
            int col = pos - txtArea.GetFirstCharIndexOfCurrentLine() + 1;

            status.Text = "Ln " + line + ", Col " + col;
        }

        // ****************************** Event Handlers ******************************

        private void undoCaptureTimer_Tick(object sender, EventArgs e) {
            undoCaptureTimer.Stop();
            undoRedoStack.AddItem(txtArea.Text);
            UpdateView();
        }

        private void txtArea_TextChanged(object sender, EventArgs e) {
            isDirty = true;

            string t = txtArea.Text;
            int cursorPos = txtArea.SelectionStart;

            foreach (KeyValuePair<string, string> sub in subs)  // Do substitutions
                t = t.Replace(sub.Key, sub.Value);

            txtArea.TextChanged -= txtArea_TextChanged;         // Don't fire handler on this text change
            txtArea.Text = t;
            txtArea.TextChanged += txtArea_TextChanged;         // Reconnect it
            txtArea.SelectionStart = cursorPos;                 // Restore state

            if (captureChangesToUndoStack)
                undoCaptureTimer.Start();
            UpdateView();
        }

        private void newFileMenu_Click(object sender, System.EventArgs e) {
            if (!OKToProceedWhileDirty()) return;

            NewFile();
        }

        private void openFileMenu_Click(object sender, EventArgs e) {
            if (!OKToProceedWhileDirty()) return;

            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Text(*.txt)|*.txt";
            openFile.InitialDirectory = "D:";
            openFile.Title = "Open File";
            if (openFile.ShowDialog() == DialogResult.OK) {
                //txtArea.TextChanged -= txtArea_TextChanged;     // Disconnect...
                txtArea.Text = ReadFile(openFile.FileName);
                //txtArea.TextChanged += txtArea_TextChanged;     // ...reconnect
                UpdateView();
            }
        }

        private void saveFileMenu_Click(object sender, EventArgs e) {
            if (!isDirty) return;
            SaveFile();
        }

        private void saveAsFileMenuItem_Click(object sender, EventArgs e) {
            SaveFileAs();
        }

        private void exitFileMenu_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void editMenu_Click(object sender, EventArgs e) {
            cutEditMenu.Enabled = txtArea.SelectedText.Length > 0 ? true : false;
            copyEditMenu.Enabled = txtArea.SelectedText.Length > 0 ? true : false;
            pasteEditMenu.Enabled = Clipboard.GetDataObject().GetDataPresent(DataFormats.Text);
        }

        private void editMenu_MouseEnter(object sender, EventArgs e) {
            editMenu_Click(sender, e);
        }

        private void cutEditMenu_Click(object sender, EventArgs e) {
            txtArea.Cut();
            pasteEditMenu.Enabled = true;
        }

        private void copyEditMenu_Click(object sender, EventArgs e) {
            txtArea.Copy();
            pasteEditMenu.Enabled = true;
        }

        private void pasteEditMenu_Click(object sender, EventArgs e) {
            if (Clipboard.GetDataObject().GetDataPresent(DataFormats.Text))
                txtArea.Paste();
        }

        private void selectallEditMenu_Click(object sender, EventArgs e) {
            txtArea.SelectAll();
        }

        private void undoEditMenu_Click(object sender, EventArgs e) {
            captureChangesToUndoStack = false;
            txtArea.Text = undoRedoStack.Undo();
            UpdateView();
        }

        private void redoEditMenu_Click(object sender, EventArgs e) {
            captureChangesToUndoStack = false;
            txtArea.Text = undoRedoStack.Redo();
            UpdateView();
        }

        private void MainForm_Load(object sender, EventArgs e) {
            wordWrapViewMenu.Checked = (bool)Properties.Settings.Default["wordWrap"];
            statusBarViewMenu.Checked = (bool)Properties.Settings.Default["statusBar"];
            darkModeViewMenu.Checked = (bool)Properties.Settings.Default["darkMode"];

            wordWrapViewMenu_CheckedChanged(null, null);
            statusBarViewMenu_CheckedChanged(null, null);
            darkModeViewMenu_CheckedChanged(null, null);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            Properties.Settings.Default["wordWrap"] = wordWrapViewMenu.Checked;
            Properties.Settings.Default["statusBar"] = statusBarViewMenu.Checked;
            Properties.Settings.Default["darkMode"] = darkModeViewMenu.Checked;
            Properties.Settings.Default.Save();

            Console.WriteLine("Closing");
        }

        private void wordWrapViewMenu_CheckedChanged(object sender, EventArgs e) {
            txtArea.WordWrap = wordWrapViewMenu.Checked;
        }

        private void statusBarViewMenu_CheckedChanged(object sender, EventArgs e) {
            statusContent.Visible = statusBarViewMenu.Checked;
        }

        private void darkModeViewMenu_CheckedChanged(object sender, EventArgs e) {
            txtArea.TextChanged -= txtArea_TextChanged;         // Don't fire handler!
            if (darkModeViewMenu.Checked) {
                txtArea.ForeColor = System.Drawing.Color.FromArgb(255,255,255,255);
                txtArea.BackColor = System.Drawing.Color.FromArgb(255,32,32,32);
            } else {
                txtArea.ForeColor = System.Drawing.Color.FromArgb(255,0,0,0);
                txtArea.BackColor = System.Drawing.Color.FromArgb(255,255,255,255);
            }
            txtArea.TextChanged += txtArea_TextChanged;         // Reconnect it
        }

        private void txtArea_SelectionChanged(object sender, EventArgs e) {
            UpdateStatus();
        }

        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e) {
            HelpForm f = new HelpForm();
            f.Show();                                           // Show this one non-modal; it's a handy reference
        }

        private void aboutHelpMenuItem_Click(object sender, EventArgs e) {
            AboutForm f = new AboutForm();
            f.ShowDialog();
        }
    }
}
