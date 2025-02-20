﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Forms;
using System.Xml;
using XmlContentTranslator.Translator;

namespace XmlContentTranslator
{
    public partial class Main : Form
    {
        class ComboBoxItem
        {
            private string Text { get; }
            public string Value { get; }

            public ComboBoxItem(string text, string value)
            {
                if (text.Length > 1)
                {
                    text = text.Substring(0, 1).ToUpper() + text.Substring(1).ToLower();
                }

                Text = text;
                Value = value;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        Hashtable _treeNodesHashtable = new Hashtable();
        Hashtable _listViewItemHashtable = new Hashtable();
        XmlDocument _originalDocument;
        string _secondLanguageFileName;
        bool _change;
        private Find _formFind;
        private Download _formDownload;
        private bool _scada = false;

        public Main()
        {
            InitializeComponent();
            toolStripStatusLabel1.Text = string.Empty;
            toolStripStatusLabel2.Text = string.Empty;

            FillComboWithLanguages(comboBoxFrom);
            FillComboWithLanguages(comboBoxTo);
        }

        private void OpenToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_change && listViewLanguageTags.Columns.Count == 3 &&
                MessageBox.Show("Changes will be lost. Continue?", "Continue", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            openFileDialog1.FileName = string.Empty;
            openFileDialog1.DefaultExt = ".xml";
            openFileDialog1.Filter = "Xml files|*.xml" + "|All files|*.*";
            openFileDialog1.Title = "Open language master file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                MakeNew();
                if (OpenFirstFile(openFileDialog1.FileName))
                {
                    OpenSecondFile();
                }
            }
        }

        private bool OpenFirstFile(string fileName)
        {
            toolStripStatusLabel1.Text = "Opening " + fileName + "...";
            var doc = new XmlDocument();
            try
            {
                doc.Load(fileName);
            }
            catch
            {
                MessageBox.Show("Not a valid xml file: " + fileName);
                return false;
            }

            return OpenFirstXmlDocument(doc);
        }

        private bool OpenFirstXmlDocument(XmlDocument doc)
        {
            if (doc.DocumentElement != null
                && doc.DocumentElement.Name.EndsWith("dictionaries", StringComparison.OrdinalIgnoreCase) ) {
                    if (doc.DocumentElement.SelectSingleNode("Dictionary/Phrase").Attributes != null
                            && doc.DocumentElement.SelectSingleNode("Dictionary/Phrase").Attributes["key"] != null) {
                        _scada = true;
                    } else {
                        _scada = false;
                    }
            } else {
                _scada = false;
            }

            listViewLanguageTags.Columns.Add("Tag", 150);
            TryGetLanguageNameAttribute(doc, comboBoxFrom);

            AddAttributes(doc.DocumentElement);
            if (doc.DocumentElement != null)
            {
                foreach (XmlNode childNode in doc.DocumentElement.ChildNodes)
                {
                    if (childNode.NodeType != XmlNodeType.Attribute)
                    {
                        var treeNode = new TreeNode(childNode.Name);
                        if (childNode.Attributes != null && childNode.Attributes["key"] != null && _scada) {
                            treeNode.Text = childNode.Attributes["key"].InnerText;
                        }
                        treeNode.Tag = childNode;
                        treeView1.Nodes.Add(treeNode);
                        if (childNode.ChildNodes.Count > 0 && !XmlUtils.IsTextNode(childNode))
                        {
                            ExpandNode(treeNode, childNode);
                        }
                        else
                        {
                            _treeNodesHashtable.Add(treeNode, childNode);
                            AddListViewItem(childNode);
                        }
                    }
                }
            }
            _originalDocument = doc;
            toolStripStatusLabel1.Text = "Done reading " + openFileDialog1.FileName;
            return true;
        }

        private int SetComboBox(ComboBox comboBox, string culture) {
            int index = 0;
            comboBox.SelectedIndex = -1;
            int defaultIndex = -1;
            bool isDefaultSet = false;
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Value == culture)
                {
                    comboBox.SelectedIndex = index;
                    return -1;
                }
                if (isDefaultSet == false && item.Value == "en")
                {
                    defaultIndex = index;
                    isDefaultSet = true;
                }
                index++;
            }

            culture = culture.Substring(0, 2);
            index = 0;
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Value == culture)
                {
                    comboBox.SelectedIndex = index;
                    return -1;
                }
                index++;
            }
            return defaultIndex;
        }

        private void SetLanguage(ComboBox comboBox, XmlDocument doc)
        {
            comboBox.SelectedIndex = -1;
            int defaultIndex = -1;
            if (doc != null && doc.DocumentElement != null && doc.DocumentElement.SelectSingleNode("General/CultureName") != null)
            {
                defaultIndex = SetComboBox(comboBox, doc.DocumentElement.SelectSingleNode("General/CultureName").InnerText);
            } else if (doc != null && doc.DocumentElement != null && doc.DocumentElement.Attributes != null
                    && doc.DocumentElement.Attributes["culture"] != null) {
                defaultIndex = SetComboBox(comboBox, doc.DocumentElement.Attributes["culture"].InnerText);
            }
            if (defaultIndex >= 0)
                comboBox.SelectedIndex = defaultIndex;
        }

        private void OpenSecondFile()
        {
            _secondLanguageFileName = string.Empty;
            Text = "XML Content Translator - New";
            openFileDialog1.Title = "Open file to translate/correct";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                OpenSecondFile(openFileDialog1.FileName);
            }
            else
            {
                listViewLanguageTags.Columns.Add("Language 2", 200);
                SetLanguage(comboBoxTo, null);
                CreateEmptyLanguage();
            }
            HighLightLinesWithSameText();
        }

        private void OpenSecondFile(string fileName)
        {
            toolStripStatusLabel1.Text = "Opening " + fileName + "...";
            _secondLanguageFileName = fileName;
            Text = "XML Content Translator - " + _secondLanguageFileName;

            Cursor = Cursors.WaitCursor;
            listViewLanguageTags.BeginUpdate();
            var doc = new XmlDocument();
            try
            {
                doc.Load(_secondLanguageFileName);
            }
            catch
            {
                MessageBox.Show("Not a valid xml file: " + _secondLanguageFileName);
            }

            TryGetLanguageNameAttribute(doc, comboBoxTo);

            AddAttributes(doc.DocumentElement);
            if (doc.DocumentElement != null)
            {
                foreach (XmlNode childNode in doc.DocumentElement.ChildNodes)
                {
                    if (childNode.ChildNodes.Count > 0 && !XmlUtils.IsTextNode(childNode))
                    {
                        ExpandNode(null, childNode);
                    }
                    else
                    {
                        AddListViewItem(childNode);
                        AddAttributes(doc.DocumentElement);
                    }
                }
            }

            CreateEmptyLanguage();

            listViewLanguageTags.EndUpdate();
            Cursor = Cursors.Default;
            toolStripStatusLabel1.Text = "Done reading " + _secondLanguageFileName;
        }

        private void TryGetLanguageNameAttribute(XmlDocument doc, ComboBox cb)
        {
            if (doc.DocumentElement != null && doc.DocumentElement.Attributes["Name"] != null)
            {
                listViewLanguageTags.Columns.Add(doc.DocumentElement.Attributes["Name"].InnerText, 200);
            }
            else if (doc.DocumentElement != null && doc.DocumentElement.Attributes["name"] != null)
            {
                listViewLanguageTags.Columns.Add(doc.DocumentElement.Attributes["name"].InnerText, 200);
            }
            else if (doc.DocumentElement != null && doc.DocumentElement.Attributes["lang"] != null)
            {
                listViewLanguageTags.Columns.Add(doc.DocumentElement.Attributes["lang"].InnerText, 200);
            }
            else
            {
                string language = "Language1";
                if (cb.Name == "comboBoxTo")
                {
                    language = "Language2";
                }
                listViewLanguageTags.Columns.Add(language, 200);
            }
            SetLanguage(cb, doc);
        }

        private void CreateEmptyLanguage()
        {
            foreach (ListViewItem lvi in listViewLanguageTags.Items)
            {
                if (lvi.SubItems.Count == 2)
                {
                    lvi.SubItems.Add(string.Empty);
                }
            }
        }

        private void AddListViewItem(XmlNode node)
        {
            if (listViewLanguageTags.Columns.Count == 2)
            {
                if (node.NodeType != XmlNodeType.Comment && node.NodeType != XmlNodeType.CDATA)
                {
                    var item = new ListViewItem(node.Name);
                    if (node.NodeType == XmlNodeType.Attribute)
                    {
                        if (item.Text.Contains("key", StringComparison.OrdinalIgnoreCase) && _scada) {
                            return;
                        } else {
                            item.Text = "@" + item.Text;
                            item.SubItems.Add(node.InnerText);
                        }
                    }
                    else if (XmlUtils.ContainsText(node))
                    {
                        item = new ListViewItem(node.Name);
                        if (node.Attributes != null && node.Attributes["key"] != null && _scada) {
                            item.Text = node.Attributes["key"].InnerText;
                        }
//                        item = new ListViewItem("#" + node.Name);
                        item.SubItems.Add(node.InnerXml);
                    }
                    else
                    {
                        item = new ListViewItem(node.Name);
                        item.SubItems.Add(node.InnerText);
                    }


                    item.Tag = node;
                    listViewLanguageTags.Items.Add(item);
                    _listViewItemHashtable.Add(XmlUtils.BuildNodePath(node), item); // fails on some attributes!!
                }
            }
            else if (listViewLanguageTags.Columns.Count == 3)
            {
                var item = _listViewItemHashtable[XmlUtils.BuildNodePath(node)] as ListViewItem;

                if (XmlUtils.ContainsText(node))
                {
                    item?.SubItems.Add(node.InnerXml);
                }
                else
                {
                    item?.SubItems.Add(node.InnerText);
                }
            }
        }

        private void MakeNew()
        {
            _treeNodesHashtable = new Hashtable();
            _listViewItemHashtable = new Hashtable();
            treeView1.Nodes.Clear();
            listViewLanguageTags.Items.Clear();
            listViewLanguageTags.Clear();

            _secondLanguageFileName = string.Empty;
            Text = "XML Content Translator";

            _change = false;
        }

        private void ExpandNode(TreeNode parentNode, XmlNode node)
        {
            if (listViewLanguageTags.Columns.Count == 2)
            {
                AddAttributes(node);
                foreach (XmlNode childNode in node.ChildNodes)
                {
                    var treeNode = new TreeNode(childNode.Name);
                    if (childNode.Attributes != null && childNode.Attributes["key"] != null && _scada) {
                        treeNode.Text = childNode.Attributes["key"].InnerText;
                    }
                    treeNode.Tag = childNode;
                    if (parentNode == null)
                    {
                        treeView1.Nodes.Add(treeNode);
                    }
                    else
                    {
                        parentNode.Nodes.Add(treeNode);
                    }

                    if (XmlUtils.IsParentElement(childNode))
                    {
                        ExpandNode(treeNode, childNode);
                    }
                    else
                    {
                        _treeNodesHashtable.Add(treeNode, childNode);
                        AddListViewItem(childNode);
                        AddAttributes(childNode);
                    }
                }
            }
            else if (listViewLanguageTags.Columns.Count == 3)
            {
                AddAttributes(node);
                foreach (XmlNode childNode in node.ChildNodes)
                {
                    if (XmlUtils.IsParentElement(childNode))
                    {
                        ExpandNode(null, childNode);
                    }
                    else
                    {
                        AddListViewItem(childNode);
                        AddAttributes(childNode);
                    }
                }
            }
        }

        private void AddAttributes(XmlNode node)
        {
            if (node.Attributes == null || node.Attributes.Count == 0)
            {
                return;
            }

            foreach (XmlNode childNode in node.Attributes)
            {
                AddListViewItem(childNode);
            }
        }

        private void ExitToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_change && listViewLanguageTags.Columns.Count == 3 &&
                MessageBox.Show("Changes will be lost. Continue?", "Continue", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            _change = false;
            Close();
        }

        private void TreeView1AfterSelect(object sender, TreeViewEventArgs e)
        {
            var node = _treeNodesHashtable[e.Node] as XmlNode;
            if (node != null)
            {
                DeSelectListViewItems();

                var item = _listViewItemHashtable[XmlUtils.BuildNodePath(node)] as ListViewItem;
                if (item != null)
                {
                    item.Selected = true;
                    listViewLanguageTags.EnsureVisible(item.Index);
                }
            }
        }

        private void DeSelectListViewItems()
        {
            var selectedItems = new List<ListViewItem>();
            foreach (ListViewItem lvi in listViewLanguageTags.SelectedItems)
            {
                selectedItems.Add(lvi);
            }
            foreach (ListViewItem lvi in selectedItems)
            {
                lvi.Selected = false;
            }
        }

        private void ListViewLanguageTagsSelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewLanguageTags.SelectedItems.Count == 1 && listViewLanguageTags.SelectedItems[0].SubItems.Count > 2)
            {
                textBoxCurrentText.Enabled = true;
                textBoxCurrentText.Text = listViewLanguageTags.SelectedItems[0].SubItems[2].Text;

                if (listViewLanguageTags.SelectedItems[0].Tag is XmlNode node)
                {
                    toolStripStatusLabel2.Text = $"{XmlUtils.BuildNodePath(node).Replace("#document/", "")}     {listViewLanguageTags.SelectedItems[0].Index + 1} / {listViewLanguageTags.Items.Count}";
                }
                else
                {
                    toolStripStatusLabel2.Text = $"{listViewLanguageTags.SelectedItems[0].Index + 1} / {listViewLanguageTags.Items.Count}";
                }
            }
            else
            {
                textBoxCurrentText.Text = string.Empty;
                textBoxCurrentText.Enabled = false;
                toolStripStatusLabel2.Text = $"{listViewLanguageTags.SelectedItems.Count} items selected";
            }
            HighLightLinesWithSameText();
        }

        private void TextBoxCurrentTextTextChanged(object sender, EventArgs e)
        {
            if (listViewLanguageTags.SelectedItems.Count == 1)
            {
                listViewLanguageTags.SelectedItems[0].SubItems[2].Text = textBoxCurrentText.Text;
            }
        }

        private void FillOriginalDocumentFromSecondLanguage()
        {
            FillAttributes(_originalDocument.DocumentElement);
            if (_originalDocument.DocumentElement != null)
            {
                foreach (XmlNode childNode in _originalDocument.DocumentElement.ChildNodes)
                {
                    if (childNode.ChildNodes.Count > 0 && !XmlUtils.IsTextNode(childNode))
                    {
                        FillOriginalDocumentExpandNode(childNode);
                    }
                    else
                    {
                        var item = _listViewItemHashtable[XmlUtils.BuildNodePath(childNode)] as ListViewItem;
                        if (item != null)
                        {
                            childNode.InnerText = item.SubItems[2].Text;
                        }
                        FillAttributes(_originalDocument.DocumentElement);
                    }
                }
            }
        }

        private void FillOriginalDocumentExpandNode(XmlNode node)
        {
            FillAttributes(node);
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.ChildNodes.Count > 0 && !XmlUtils.IsTextNode(childNode))
                {
                    FillOriginalDocumentExpandNode(childNode);
                }
                else
                {
                    if (_listViewItemHashtable[XmlUtils.BuildNodePath(childNode)] is ListViewItem item)
                    {
                        if (XmlUtils.ContainsText(childNode))
                        {
                            try
                            {
                                childNode.InnerXml = item.SubItems[2].Text;
                            }
                            catch 
                            {
                                childNode.InnerText = item.SubItems[2].Text;
                            }
                        }
                        else
                        {
                            childNode.InnerText = item.SubItems[2].Text;
                        }
                    }
                    FillAttributes(childNode);
                }
            }
        }

        private void FillAttributes(XmlNode node)
        {
            if (node.Attributes == null)
                return;

            foreach (XmlNode attribute in node.Attributes)
            {
                var item = _listViewItemHashtable[XmlUtils.BuildNodePath(attribute)] as ListViewItem;
                if (item != null)
                {
                    attribute.InnerText = item.SubItems[2].Text;
                }
            }
        }

        private void Form1KeyDown(object sender, KeyEventArgs e)
        {
            if (listViewLanguageTags.Items.Count == 0)
                return;

            if (e.Control && e.KeyCode == Keys.Down)
            {
                if (listViewLanguageTags.SelectedItems.Count == 0)
                    listViewLanguageTags.Items[0].Selected = true;

                int index = listViewLanguageTags.SelectedItems[0].Index + 1;
                if (index < listViewLanguageTags.Items.Count)
                {
                    DeSelectListViewItems();
                    listViewLanguageTags.Items[index].Selected = true;
                    listViewLanguageTags.EnsureVisible(index);
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                ActiveControl = textBoxCurrentText;
            }
            else if (e.Control && e.KeyCode == Keys.Up)
            {
                if (listViewLanguageTags.SelectedItems.Count == 0)
                    listViewLanguageTags.Items[0].Selected = true;

                int index = listViewLanguageTags.SelectedItems[0].Index - 1;
                if (index >= 0)
                {
                    DeSelectListViewItems();
                    listViewLanguageTags.Items[index].Selected = true;
                    listViewLanguageTags.EnsureVisible(index);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                ActiveControl = textBoxCurrentText;
            }
            else if (e.KeyCode == Keys.F6)
            {
                ButtonGoToNextBlankLineClick(null, null);
            }
            else if (e.KeyCode == Keys.F3 && !e.Control && !e.Alt)
            {
                if (_formFind == null || _formFind.SearchText.Length == 0)
                {
                    findToolStripMenuItem_Click(this, null);
                }
                else
                {
                    FindNext();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void GoogleTranslateSelectedLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            GoogleTranslateSelectedLines();
        }

        /// <summary>
        /// Translate Text using Google Translate API's
        /// Google URL - https://www.google.com/translate_t?hl=en&ie=UTF8&text={0}&langpair={1}
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="languagePair">2 letter Language Pair, delimited by "|".
        /// E.g. "ar|en" language pair means to translate from Arabic to English</param>
        /// <returns>Translated to String</returns>
        public static string TranslateTextViaScreenScraping(string input, string languagePair)
        {
            input = input.Replace(Environment.NewLine, "<br/>").Trim();
            input = input.Replace("'", "&apos;");

            //string url = String.Format("https://www.google.com/translate_t?hl=en&ie=UTF8&text={0}&langpair={1}", HttpUtility.UrlEncode(input), languagePair);
            var url = $"https://translate.google.com/?hl=en&eotf=1&sl={languagePair.Substring(0, 2)}&tl={languagePair.Substring(3)}&q={HttpUtility.UrlEncode(input)}";

            var webClient = new WebClient { Encoding = Encoding.Default };
            var result = webClient.DownloadString(url);
            var startIndex = result.IndexOf("<span id=result_box", StringComparison.Ordinal);
            var sb = new StringBuilder();
            if (startIndex > 0)
            {
                startIndex = result.IndexOf("<span title=", startIndex, StringComparison.Ordinal);
                while (startIndex > 0)
                {
                    startIndex = result.IndexOf(">", startIndex, StringComparison.Ordinal);
                    if (startIndex > 0)
                    {
                        startIndex++;
                        int endIndex = result.IndexOf("</span>", startIndex, StringComparison.Ordinal);
                        string translatedText = result.Substring(startIndex, endIndex - startIndex);
                        string test = HttpUtility.HtmlDecode(translatedText);
                        sb.Append(test);
                        startIndex = result.IndexOf("<span title=", startIndex, StringComparison.Ordinal);
                    }
                }
            }
            var res = sb.ToString();
            res = res.Replace("<BR/>", Environment.NewLine);
            res = res.Replace("<BR />", Environment.NewLine);
            res = res.Replace("< BR />", Environment.NewLine);
            res = res.Replace(" <br/>", Environment.NewLine);
            res = res.Replace("<br/>", Environment.NewLine);
            res = res.Replace("<br />", Environment.NewLine);
            return res.Trim();
        }

        private void GoogleTranslateSelectedLines()
        {
            if (string.IsNullOrEmpty(_secondLanguageFileName))
                return;

            if (comboBoxFrom.SelectedItem == null || comboBoxTo.SelectedItem == null)
            {
                MessageBox.Show("From/to language not selected");
                return;
            }

            int skipped = 0;
            int translated = 0;
            string oldText = string.Empty;
            string newText = string.Empty;

            toolStripStatusLabel1.Text = "Translating via Google Translate. Please wait...";
            Refresh();

            var translator = new GoogleTranslator1();
            Cursor = Cursors.WaitCursor;
            var sb = new StringBuilder();
            var res = new StringBuilder();
            var oldLines = new List<string>();
            var list = new List<string>();
            foreach (ListViewItem item in listViewLanguageTags.SelectedItems)
            {
                oldText = item.SubItems[1].Text;
                oldText = oldText.Replace(System.Environment.NewLine, "<br/>").Trim();
                oldText = string.Join(Environment.NewLine, oldText.SplitToLines());
                oldLines.Add(oldText);
                var urlEncode = HttpUtility.UrlEncode(sb + newText);
                if (urlEncode.Length >= 1000)
                {
                    res.Append(TranslateTextViaScreenScraping(sb.ToString(), (comboBoxFrom.SelectedItem as ComboBoxItem).Value + "|" + (comboBoxTo.SelectedItem as ComboBoxItem).Value));
                    sb = new StringBuilder();
                }
                list.Add(oldText);
                sb.Append("== " + oldText + " ");
            }
            var log = new StringBuilder();
            var lines = translator.Translate(((ComboBoxItem)comboBoxFrom.SelectedItem).Value, ((ComboBoxItem)comboBoxTo.SelectedItem).Value, list, log).ToList();
            if (listViewLanguageTags.SelectedItems.Count != lines.Count)
            {
                MessageBox.Show("Error getting/decoding translation from google!");
                Cursor = Cursors.Default;
                return;
            }

            int index = 0;
            foreach (ListViewItem item in listViewLanguageTags.SelectedItems)
            {
                string s = lines[index];
                string cleanText = s.Replace("</p>", string.Empty).Trim();
                cleanText = cleanText.Replace(" ...", "...");
                cleanText = cleanText.Replace("<br>", Environment.NewLine);
                cleanText = cleanText.Replace("<br/>", Environment.NewLine);
                cleanText = cleanText.Replace("<br />", Environment.NewLine);
                cleanText = cleanText.Replace(Environment.NewLine + " ", Environment.NewLine);
                newText = cleanText;

                oldText = oldLines[index];
                if (oldText.Contains("{0:"))
                {
                    newText = oldText;
                }
                else
                {
                    if (!oldText.Contains(" / "))
                        newText = newText.Replace(" / ", "/");

                    if (!oldText.Contains(" ..."))
                        newText = newText.Replace(" ...", "...");

                    if (!oldText.Contains("& "))
                        newText = newText.Replace("& ", "&");

                    if (!oldText.Contains("# "))
                        newText = newText.Replace("# ", "#");

                    if (!oldText.Contains("@ "))
                        newText = newText.Replace("@ ", "@");

                    if (oldText.Contains("{0}"))
                    {
                        for (int i = 0; i < 50; i++)
                            newText = newText.Replace("(" + i + ")", "{" + i + "}");
                    }
                    translated++;
                }
                item.SubItems[2].Text = newText;
                _change = true;
                index++;
            }


            Cursor = Cursors.Default;
            if (translated == 1 && skipped == 0)
            {
                toolStripStatusLabel1.Text = "One line translated: '" + StringUtils.Max50(oldText) + "' => '" + StringUtils.Max50(newText) + "'";
            }
            else
            {
                if (translated == 1)
                    toolStripStatusLabel1.Text = "One line translated";
                else
                    toolStripStatusLabel1.Text = translated + " lines translated";
                if (skipped > 0)
                    toolStripStatusLabel1.Text += ", " + skipped + " line(s) skipped";
            }
            ListViewLanguageTagsSelectedIndexChanged(null, null);
        }

        private void translateViaGoogleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoogleTranslateSelectedLines();
        }

        private void setValueFromMasterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_secondLanguageFileName))
            {
                return;
            }

            var transferred = 0;
            var oldText = string.Empty;
            var newText = string.Empty;
            foreach (ListViewItem item in listViewLanguageTags.SelectedItems)
            {
                oldText = item.SubItems[2].Text;
                newText = item.SubItems[1].Text;
                transferred++;
                item.SubItems[2].Text = newText;
                _change = true;
            }

            if (transferred == 1)
            {
                toolStripStatusLabel1.Text = "One line transfered from master: '" + oldText + "' => '" + newText + "'";
            }
            else
            {
                toolStripStatusLabel1.Text = transferred + " line(s) transfered from master";
            }

            ListViewLanguageTagsSelectedIndexChanged(null, null);
        }

        public static void FillComboWithLanguages(ComboBox comboBox)
        {
            foreach (var pair in new GoogleTranslator1().GetTranslationPairs())
            {
                comboBox.Items.Add(new ComboBoxItem(pair.Name, pair.Code));
            }
        }

        private void ToolStripMenuItem1Click(object sender, EventArgs e)
        {
            if (_change && listViewLanguageTags.Columns.Count == 3 &&
                MessageBox.Show("Changes will be lost. Continue?", "Continue", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            MakeNew();
            toolStripStatusLabel1.Text = "New";
        }

        private void SaveAsToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_originalDocument == null)
            {
                return;
            }

            saveFileDialog1.Title = "Save language file as...";
            saveFileDialog1.DefaultExt = ".xml";
            saveFileDialog1.Filter = "Xml files|*.xml" + "|All files|*.*";
            saveFileDialog1.Title = "Open language master file";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                _secondLanguageFileName = saveFileDialog1.FileName;
                FillOriginalDocumentFromSecondLanguage();
                _originalDocument.Save(saveFileDialog1.FileName);
                _change = false;
                toolStripStatusLabel1.Text = "File saved as " + _secondLanguageFileName;
            }
        }

        private void TextBoxCurrentTextKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control && !e.Alt)
            {
                _change = true;
            }
        }

        private void SaveToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_secondLanguageFileName))
            {
                SaveAsToolStripMenuItemClick(null, null);
            }
            else
            {
                FillOriginalDocumentFromSecondLanguage();

                using (var sw = new StringWriter())
                {
                    using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
                    {
                        _originalDocument.Save(xw);
                        var s = sw.ToString();
                        if (s.Contains("Subtitle Edit"))
                        {
                            s = s.Replace("<HelpFile></HelpFile>", "<HelpFile />");
                        }
                        s = s.Replace("encoding=\"utf-16\"?", "encoding=\"utf-8\"?");
                        File.WriteAllText(_secondLanguageFileName, s, Encoding.UTF8);
                    }
                }
                _change = false;
                toolStripStatusLabel1.Text = "File saved - " + _secondLanguageFileName;
            }
        }

        private void Form1FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_change && listViewLanguageTags.Columns.Count == 3 &&
                MessageBox.Show("Changes will be lost. Continue?", "Continue", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        private void ListViewLanguageTagsDragEnter(object sender, DragEventArgs e)
        { // make sure they're actually dropping files (not text or anything else)
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                e.Effect = DragDropEffects.All;
            }
        }

        private void ListViewLanguageTagsDragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (!string.IsNullOrEmpty(_secondLanguageFileName))
            {
                MessageBox.Show("Two files already loaded");
                return;
            }

            if (files.Length == 1)
            {

                var fileName = files[0];
                var fi = new FileInfo(fileName);
                if (fi.Length < 1024 * 1024 * 20) // max 20 mb
                {
                    if (treeView1.Nodes.Count == 0)
                    {
                        OpenFirstFile(fileName);
                    }
                    else
                    {
                        OpenSecondFile(fileName);
                    }
                }
                else
                {
                    MessageBox.Show(fileName + " is too large (max 20 mb)");
                }
            }
            else
            {
                MessageBox.Show("Only file drop supported");
            }

        }

        private void Form1Load(object sender, EventArgs e)
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                if (File.Exists(args[1]))
                {
                    OpenFirstFile(args[1]);
                }

                if (args.Length > 2 && File.Exists(args[2]))
                {
                    OpenSecondFile(args[2]);
                }
            }

        }

        private void ButtonGoToNextBlankLineClick(object sender, EventArgs e)
        {
            var index = 0;
            if (listViewLanguageTags.SelectedItems.Count > 0)
            {
                index = listViewLanguageTags.SelectedItems[0].Index + 1;
            }

            for (; index < listViewLanguageTags.Items.Count; index++)
            {
                if (listViewLanguageTags.Items[index].SubItems.Count > 1 && string.IsNullOrEmpty(listViewLanguageTags.Items[index].SubItems[2].Text))
                {
                    foreach (ListViewItem item in listViewLanguageTags.SelectedItems)
                    {
                        item.Selected = false;
                    }

                    listViewLanguageTags.Items[index].Selected = true;
                    listViewLanguageTags.Items[index].EnsureVisible();
                    return;
                }
            }
        }

        private void HighLightLinesWithSameText()
        {
            foreach (ListViewItem item in listViewLanguageTags.Items)
            {
                if (item.SubItems.Count == 3)
                {
                    if (item.SubItems[1].Text.Trim() == item.SubItems[2].Text.Trim())
                    {
                        item.BackColor = Color.LightYellow;
                        item.UseItemStyleForSubItems = true;
                    }
                    else if (item.SubItems[2].Text.Trim().Length == 0)
                    {
                        item.BackColor = Color.LightPink;
                        item.UseItemStyleForSubItems = true;
                    }
                    else
                    {
                        item.BackColor = listViewLanguageTags.BackColor;
                        item.UseItemStyleForSubItems = true;
                    }
                }
            }
        }

        private void OpenOnlineFile(string url)
        {
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.DefaultExt = ".xml";
            openFileDialog1.Filter = "Xml files|*.xml" + "|All files|*.*";
            openFileDialog1.Title = "Open English translation base file";

            var doc = new XmlDocument();
            try
            {
                var wc = new WebClient();
                var xml = wc.DownloadString(url);
                MakeNew();
                doc.LoadXml(xml);
                OpenFirstXmlDocument(doc);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                return;
            }

            OpenSecondFile();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            OpenOnlineFile("https://raw.githubusercontent.com/SubtitleEdit/subtitleedit/main/LanguageBaseEnglish.xml");
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_formFind == null)
            {
                _formFind = new Find();
            }

            if (_formFind.ShowDialog(this) == DialogResult.OK)
            {
                FindNext();
            }
        }

        private void FindNext()
        {
            if (_formFind.SearchText.Length == 0)
                return;

            var index = 0;
            if (listViewLanguageTags.SelectedItems.Count > 0)
            {
                index = listViewLanguageTags.SelectedItems[0].Index + 1;
            }
            while (index < listViewLanguageTags.Items.Count)
            {
                if (_formFind.SearchTags)
                {
                    if (listViewLanguageTags.Items[index].Text.ToLower().Contains(_formFind.SearchText.ToLower()))
                    {
                        SelectOnlyThis(index);
                        return;
                    }
                }
                else
                {
                    if (listViewLanguageTags.Items[index].SubItems[0].Text.ToLower().Contains(_formFind.SearchText) ||
                        listViewLanguageTags.Items[index].SubItems[1].Text.ToLower().Contains(_formFind.SearchText))
                    {
                        SelectOnlyThis(index);
                        return;
                    }
                }
                index++;
            }
        }

        private void DownloadFile() {
            if (_formDownload.DownloadText.Length == 0)
                return;

            OpenOnlineFile(_formDownload.DownloadText);
        }

        private void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_formDownload == null)
            {
                _formDownload = new Download();
            }

            if (_formDownload.ShowDialog(this) == DialogResult.OK)
            {
                DownloadFile();
            }
        }

        private void SelectOnlyThis(int index)
        {
            foreach (ListViewItem selectedItem in listViewLanguageTags.SelectedItems)
            {
                selectedItem.Selected = false;
            }
            listViewLanguageTags.Items[index].Selected = true;
            listViewLanguageTags.Items[index].EnsureVisible();
            listViewLanguageTags.Items[index].Focused = true;
        }

        private void listViewLanguageTags_DoubleClick(object sender, EventArgs e)
        {
            if (listViewLanguageTags.SelectedItems.Count != 1)
            {
                return;
            }

            var node = listViewLanguageTags.SelectedItems[0].Tag as XmlNode;
            if (node == null)
            {
                return;
            }

            foreach (TreeNode treeNode in treeView1.Nodes)
            {
                if (treeNode.Tag == node)
                {
                    treeView1.SelectedNode = treeNode;
                    return;
                }
                foreach (TreeNode subTreeNode in treeNode.Nodes)
                {
                    if (subTreeNode.Tag == node)
                    {
                        treeView1.SelectedNode = subTreeNode;
                        return;
                    }
                }
            }
        }

        private void Main_Resize(object sender, EventArgs e)
        {
            Main_ResizeEnd(null, null);
        }

        private void Main_ResizeEnd(object sender, EventArgs e)
        {
            if (listViewLanguageTags.Columns.Count > 0)
            {
                var w = 0;
                for (int i = 0; i < listViewLanguageTags.Columns.Count - 1; i++)
                {
                    w += listViewLanguageTags.Columns[i].Width;
                }

                listViewLanguageTags.Columns[listViewLanguageTags.Columns.Count - 1].Width = listViewLanguageTags.Width - 25 - w;
            }
        }
    }
}