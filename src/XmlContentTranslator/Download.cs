using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace XmlContentTranslator
{
    public partial class Download : Form
    {
        public string DownloadText { get; set; }

        public Download()
        {
            InitializeComponent();
            try
            {
                string[] path = new string[] {System.IO.Path.GetDirectoryName(Application.ExecutablePath), "scada.txt"};
                path = System.IO.File.ReadAllLines(Path.Combine(path));
                List<string> urls = new List<string>();
                foreach (string s in path)
                {
                    if (Uri.IsWellFormedUriString(s, UriKind.Absolute)) {
                        urls.Add(s);
                    }
                }
                comboBox1.DataSource = urls;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                return;
            }
        }

        private void Download_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                buttonDownload_Click(sender, e);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void buttonDownload_Click(object sender, EventArgs e)
        {
            DownloadText = comboBox1.Text;
            DialogResult = DialogResult.OK;
        }

        private void Download_Shown(object sender, EventArgs e)
        {
            comboBox1.Focus();
            comboBox1.SelectAll();
        }

        private void comboBox1_DropDown(object sender, EventArgs e)
        {
            int width = comboBox1.DropDownWidth;
            Graphics g = comboBox1.CreateGraphics();
            Font font = comboBox1.Font;
            int vertScrollBarWidth =
                (comboBox1.Items.Count > comboBox1.MaxDropDownItems)
                ? SystemInformation.VerticalScrollBarWidth : 0;

            int newWidth;
            foreach (string s in ((ComboBox)sender).Items)
            {
                newWidth = (int)g.MeasureString(s, font).Width
                    + vertScrollBarWidth;
                if (width < newWidth)
                {
                    width = newWidth;
                }
            }
            comboBox1.DropDownWidth = width;
        }
    }
}
