﻿using Be.Windows.Forms;
using BrightIdeasSoftware;
using IpsLibNet;
using IpsPeek.IpsLibNet.Patching;
using IpsPeek.Reporting;
using IpsPeek.Utils;
using NAppUpdate;
using NAppUpdate.Framework;
using NAppUpdate.Framework.Sources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Jot;
namespace IpsPeek
{
    public partial class FormMain : Form
    {
        private long _patchFileSize = 0;
        private int _patchCount = 0;
        private string _patchName;
        private string _fileName;
        private long _fileSize = 0;
        private int _modified = 0;
        private HighlightTextRenderer _highlighter = new HighlightTextRenderer();
        private FindHexBoxDialog _findDialog;
        private GoToHexBoxDialog _goToOffsetDialog;
        private byte[] _fileData;
        private List<IpsElement> _patches;
        private readonly ApplicationConfiguration _applicationConfiguration = new ApplicationConfiguration();

        #region "Helpers"

        private void ClosePatch()
        {
            fastObjectListViewRows.ClearObjects();
            hexBoxData.ByteProvider = null;
            this.Text = Application.ProductName;

            this.closeToolStripMenuItem.Enabled = false;
            this.closeToolStripButton.Enabled = false;

            exportToolStripButton.Enabled = false;
            exportToolStripMenuItem.Enabled = false;

            goToRowToolStripMenuItem.Enabled = false;
            toolStripButtonGoToRow.Enabled = false;

            toolStripStatusLabelRows.Text = string.Empty;
            ToolStripStatusLabelPatchCount.Text = string.Empty;
            toolStripStatusLabelPatchFileSize.Text = string.Empty;
            toolStripStatusLabelModified.Text = string.Empty;
            toolStripStatusLabelPatchFileSizeSeparator.Visible = false;
            this.olvColumnNumber.Tag = 0;
            _patches = null;
            UpdateLinkedFileDateView();
        }

        private void SetStrings()
        {
            olvColumnEnd.Text = Strings.End;
            olvColumnIpsEnd.Text = Strings.IpsEndHeader;
            olvColumnIpsOffset.Text = Strings.IpsOffsetHeader;
            olvColumnIpsSize.Text = Strings.IpsSizeHeader;
            olvColumnIpsSizeHex.Text = Strings.IpsSizeHexHeader;
            olvColumnOffset.Text = Strings.OffsetHeader;
            olvColumnSize.Text = Strings.SizeHeader;
            olvColumnSizeHex.Text = Strings.SizeHexHeader;
            olvColumnType.Text = Strings.TypeHeader;

            fileToolStripMenuItem.Text = Strings.File;
            openPatchToolStripMenuItem.Text = Strings.OpenPatch;
            closeToolStripMenuItem.Text = Strings.ClosePatch;
            exportToolStripMenuItem.Text = Strings.Export;
            exitToolStripMenuItem.Text = Strings.Exit;

            viewToolStripMenuItem.Text = Strings.View;
            toolbarToolStripMenuItem.Text = Strings.Toolbar;
            dataViewToolStripMenuItem.Text = Strings.DataView;

            helpContentsToolStripMenuItem.Text = Strings.Help;
            helpContentsToolStripMenuItem.Text = Strings.HelpContents;
            iPSPeekHomeToolStripMenuItem.Text = Strings.ApplicationHome;
            officialForumToolStripMenuItem.Text = Strings.OfficialForum;
            aboutIPSPeekToolStripMenuItem.Text = Strings.About;

            openPatchToolStripButton.Text = Strings.OpenPatch;
            closeToolStripButton.Text = Strings.ClosePatch;
            exportToolStripButton.Text = Strings.Export;

            goToRowToolStripMenuItem.Text = Strings.GoToRow;
            toolStripButtonGoToRow.Text = Strings.GoToRow;

            copyRowToolStripMenuItem.Text = Strings.CopyRow;
            toolStripButtonCopyRow.Text = Strings.CopyRow;

            toolStripStatusLabelLine.Text = string.Empty;
            toolStripStatusLabelColumn.Text = string.Empty;
            toolStripStatusLabelRows.Text = string.Empty;
            ToolStripStatusLabelPatchCount.Text = string.Empty;
            toolStripStatusLabelModified.Text = string.Empty;
            toolStripStatusLabelPatchFileSize.Text = string.Empty;

            // Data View.
            toolStripButtonLinkFile.Text = Strings.OpenFile;
            toolStripButtonUnlinkFile.Text = Strings.CloseFile;
            toolStripButtonGoToOffset.Text = Strings.GoToOffsetEllipses;
            toolStripButtonSelectAll.Text = Strings.SelectAll;
            toolStripButtonCopy.Text = Strings.Copy;
            toolStripButtonStart.Text = Strings.RunEmulator;
            toolStripButtonSelectEmulator.Text = Strings.SelectEmulator;
            toolStripMenuItemCopyHex.Text = Strings.CopyHex;
            toolStripButtonFind.Text = Strings.FindEllipses;
            findNextToolStripMenuItem.Text = Strings.FindNext;
            findPreviousToolStripMenuItem.Text = Strings.FindPrevious;
            toolStripButtonStringView.Text = Strings.StringView;
            toolStripStatusLabelFile.Text = string.Empty;
            toolStripStatusLabelFileSize.Text = string.Empty;

            // Data View Context Menu.
            toolStripMenuItemCopy.Text = Strings.Copy;
            copyHexToolStripMenuItem.Text = Strings.CopyHex;
            toolStripMenuItemSelectAll.Text = Strings.SelectAll;

            UpdateOffsetStatus();
        }

        private void OpenPatch()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = Strings.FilterIpsFiles;

                if (dialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    LoadPatch(dialog.FileName);
                }
            }
        }

        private DialogResult OpenFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                // TODO: Change filter to All Files (*.*).
                // dialog.Filter = Strings.FilterIpsFiles;
                DialogResult result;
                if ((result = dialog.ShowDialog(this)) == System.Windows.Forms.DialogResult.OK)
                {
                    LoadFile(dialog.FileName);
                    // filterToolStripTextBox.Clear();
                }
                return result;
            }
        }

        private void CloseFile()
        {
            _fileData = null;
            hexBoxData.ByteProvider = null;
            toolStripStatusLabelFileSeparator.Visible = false;
            toolStripButtonStart.Enabled = false;
        }

        private void LoadFile(string file)
        {
            _fileSize = new FileInfo(file).Length;
            _fileData = File.ReadAllBytes(file);
            _fileName = Path.GetFileName(file);
            toolStripStatusLabelFileSeparator.Visible = true;
            toolStripButtonStart.Enabled = true;
        }

        private void UpdateLinkedFileDateView()
        {
            hexBoxData.Highlights.Clear();
            List<Highlight> highlights = new List<Highlight>();
            if (_fileData != null && _patches != null)
            {
                // DynamicByteProvider provider = new DynamicByteProvider(_fileData);
                UpateDataViewToolStrip(true);

                long fileLength = _fileData.Count();
                using (MemoryStream file = new MemoryStream())
                {
                    file.Write(_fileData, 0, _fileData.Length);
                    foreach (IpsElement patch in _patches.Where(p => p is IAvailability && ((IAvailability)p).Enabled))
                    {
                        if (patch is IpsPatchElement)
                        {
                            if (((IpsPatchElement)patch).Offset >= file.Length)
                            {
                                long diff = ((IpsPatchElement)patch).Offset - file.Length;
                                hexBoxData.Highlights.Add(new Highlight(Color.White, Color.Red, file.Length, diff));
                            }

                            file.Seek(((IpsPatchElement)patch).Offset, SeekOrigin.Begin);
                            file.Write(((IpsPatchElement)patch).GetData(), 0, ((IpsPatchElement)patch).Size);
                            hexBoxData.Highlights.Add(new Highlight(hexBoxData.ForeColor, Color.Yellow, ((IpsPatchElement)patch).Offset, ((IpsPatchElement)patch).Size));

                            /* if (patch.Offset >= file.Length)
                             {
                              //   long diff = patch.Offset - provider.Length;
                                 provider.InsertBytes(provider.Length - 1, new byte[diff]);
                                 hexBoxData.Highlights.Add(new Highlight(Color.White, Color.Red, provider.Length - 1, diff));
                             }
                             /* if (patch.Offset >= provider.Length)
                               {
                                   long diff = patch.Offset - provider.Length;
                                   provider.InsertBytes(provider.Length - 1, new byte[diff]);
                                   hexBoxData.Highlights.Add(new Highlight(Color.White, Color.Red, provider.Length - 1, diff));
                               }
                               if (patch.End >= provider.Length)
                               {
                                   long diff = patch.End - provider.Length + 1;
                                   provider.InsertBytes(provider.Length - 1, new byte[diff]);
                               }
                               byte[] data = patch.GetData();

                               for (int i = 0; i < patch.Size; i++)
                               {
                                   provider.WriteByte(patch.Offset + i, data[i]);
                               }

                               // provider.Bytes.InsertRange(patch.Offset, new List<byte>(patch.GetData()));
                               hexBoxData.Highlights.Add(new Highlight(Color.White, Color.Yellow, patch.Offset, patch.Size));
                              * */
                        }
                        //   hexBoxData.Highlights.AddRange(highlights.ToArray());
                        /* long diff = file.Length - fileLength;
                        if (diff > 0)
                        {
                            hexBoxData.Highlights.Add(new Highlight(hexBoxData.ForeColor, Color.Red, fileLength, diff));
                        } */
                        else if (((IpsResizeValueElement)patch).GetIntValue() < file.Length)
                        {
                            long diff = file.Length - ((IpsResizeValueElement)patch).GetIntValue();
                            hexBoxData.Highlights.Add(new Highlight(hexBoxData.ForeColor, Color.LightGray, file.Length - diff, diff));
                        }
                    }
                    hexBoxData.ByteProvider = new DynamicByteProvider(file.ToArray());
                    hexBoxData.LineInfoOffset = 0;
                    hexBoxData.Refresh();
                }
            }
            else if (_fileData != null)
            {
                UpateDataViewToolStrip(true);

                hexBoxData.ByteProvider = new DynamicByteProvider(_fileData);
            }
            else
            {
                UpateDataViewToolStrip(false);
            }
        }

        private void OpenPage(string url)
        {
            Process.Start(url);
        }

        private string GetDisplayName(Type element)
        {
            if (element == typeof(IpsEndOfFileValueElement))
            {
                return "EOF";
            }
            else if (element == typeof(IpsIdValueElement))
            {
                return "ID";
            }
            else if (element == typeof(IpsPatchElement))
            {
                return "PAT";
            }
            else if (element == typeof(IpsResizeValueElement))
            {
                return "CHS";
            }
            else if (element == typeof(IpsRlePatchElement))
            {
                return "RLE";
            }
            else
            {
                return string.Empty;
            }
        }

        private void ExportFile()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = Strings.FilterTextFiles;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    using (ITableWriter writer = new TableStreamWriter(dialog.OpenFile()))
                    {
                        try
                        {
                            var sr = ((StreamWriter)writer);
                            sr.WriteLine(Strings.ApplicationInformation, Application.ProductName, Application.ProductVersion.ToString());
                            sr.WriteLine();
                            sr.WriteLine(Strings.FileInformation, _patchName);
                            sr.WriteLine();

                            List<Cell> row = new List<Cell>();
                            List<OLVColumn> columns = fastObjectListViewRows.AllColumns.Where((c) => c.IsVisible && !string.IsNullOrEmpty(c.Text)).OrderBy((c) => c.DisplayIndex).ToList();
                            foreach (OLVColumn column in columns)
                            {
                                var cell = new Cell(column.Text, (int)column.Tag);
                                cell.Padding = 1;

                                row.Add(cell);
                            }
                            writer.WriteRow(row.ToArray());
                            row.Clear();

                            foreach (var patch in fastObjectListViewRows.Objects)
                            {
                                foreach (OLVColumn column in columns)
                                {
                                    string text = column.AspectGetter(patch).ToString();
                                    if (string.IsNullOrEmpty(text)) text = text.PadLeft((int)column.Tag, '-');

                                    var cell = new Cell(text, Math.Max(column.Text.Length, (int)column.Tag));
                                    cell.Padding = 1;
                                    row.Add(cell);
                                }
                                writer.WriteRow(row.ToArray());
                                row.Clear();
                            }
                            sr.WriteLine(Strings.Footer, fastObjectListViewRows.GetItemCount(), _patchCount, _modified);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private void LoadPatch(string file)
        {
            try
            {
                var scanner = new IpsScanner();
                _patches = scanner.Scan(file);
                _patchCount = _patches.Where((element) => (element is IpsPatchElement)).Count();
                _patchFileSize = new FileInfo(file).Length;
                _modified = _patches.Where((element) => (element is IpsPatchElement)).Sum(x => ((IpsPatchElement)x).Size);
                _patches.Where((e) => e is IAvailability).ToList().ForEach((e) => ((IAvailability)e).Enabled = true);
                try
                {
                    _modified += ((IpsResizeValueElement)_patches.Where((element) => (element is IpsResizeValueElement)).First()).GetIntValue();
                }
                catch
                {
                }
                fastObjectListViewRows.SetObjects(_patches);
                fastObjectListViewRows.SelectedIndex = 0;
                this.Text = string.Format("{0} - {1}", Application.ProductName, Path.GetFileName(file));

                this.closeToolStripMenuItem.Enabled = true;
                this.closeToolStripButton.Enabled = true;

                exportToolStripButton.Enabled = true;
                exportToolStripMenuItem.Enabled = true;

                goToRowToolStripMenuItem.Enabled = true;
                toolStripButtonGoToRow.Enabled = true;

                toolStripStatusLabelModified.Text = string.Format(Strings.Modified, _modified);
                toolStripStatusLabelPatchFileSize.Text = string.Format(Strings.PatchFileSize, _patchFileSize);
                ToolStripStatusLabelPatchCount.Text = string.Format(Strings.Patches, _patchCount);
                toolStripStatusLabelPatchFileSizeSeparator.Visible = true;
            }
            catch (Exception)
            {
                MessageBox.Show(string.Format(Strings.ErrorFileLoadFailed, file));
            }

            _patchName = Path.GetFileName(file);
            filterToolStripTextBox.Clear();
            UpdateLinkedFileDateView();
        }

        private void LoadSettings()
        {
            Services.Tracker.Configure(this).Apply();
            Services.Tracker.Configure(_applicationConfiguration).Apply();

            toolbarToolStripMenuItem.Checked = _applicationConfiguration.ToolBarVisible;
            dataViewToolStripMenuItem.Checked = _applicationConfiguration.DataViewVisible;
            toolStripButtonStringView.Checked = _applicationConfiguration.StringViewVisible;
            splitContainer1.SplitterDistance = _applicationConfiguration.PanelHeight;
            _findDialog.TextItems = _applicationConfiguration.TextItems.ToArray();

            if (_applicationConfiguration.VerticalLayout)
            {
                SetVerticalLayout();
            }
            else
            {
                SetHorizontalLayout();
            }

            if (_applicationConfiguration.ListViewState != null)
            {
                try
                {
                    this.fastObjectListViewRows.RestoreState(_applicationConfiguration.ListViewState);
                }
                catch
                {
                }
            }
        }

        private void SaveSettings()
        {
            _applicationConfiguration.DataViewVisible = dataViewToolStripMenuItem.Checked;
            _applicationConfiguration.StringViewVisible = toolStripButtonStringView.Checked;
            _applicationConfiguration.ToolBarVisible = toolbarToolStripMenuItem.Checked;
            _applicationConfiguration.PanelHeight = splitContainer1.SplitterDistance;
            _applicationConfiguration.ListViewState = this.fastObjectListViewRows.SaveState();
            _applicationConfiguration.VerticalLayout = (this.verticalLayoutToolStripMenuItem.CheckState == CheckState.Indeterminate);
        }

        private void GoToRow()
        {
            using (NumericUpDown control = new NumericUpDown())
            {
                control.Maximum = fastObjectListViewRows.Items.Count;
                control.Minimum = 1;
                control.Value = fastObjectListViewRows.SelectedIndex + 1;
                if (ControlInputBox.Show(this, Strings.GoToRowTitle, Strings.GoToRowDescription, control) == System.Windows.Forms.DialogResult.OK)
                {
                    int row;
                    string result = control.Value.ToString();
                    if (int.TryParse(result, out row))
                    {
                        row--;
                        fastObjectListViewRows.SelectedIndex = row;
                        fastObjectListViewRows.TopItemIndex = row;
                    }
                }
            }
        }

        private void CopyRow()
        {
            fastObjectListViewRows.IncludeColumnHeadersInCopy = true;
            fastObjectListViewRows.CopySelectionToClipboard();
        }

        #endregion "Helpers"

        public FormMain()
        {
            InitializeComponent();

            SetStrings();
            fastObjectListViewRows.CheckBoxes = true;
            fastObjectListViewRows.TriStateCheckBoxes = true;
            fastObjectListViewRows.CheckedAspectName = "Enabled";
            //fastObjectListViewRows.CheckedAspectName = delegate(object row)
            //{
            //    if (row is IAvailability)
            //    {
            //        var element = row as IAvailability;

            //        if (element.Enabled)
            //        {
            //            return 1;
            //        }
            //        else
            //        {
            //            return 0;
            //        }
            //    }
            //    else
            //    {
            //        return -1;
            //    }
            //};

            this.olvColumnEnd.AspectGetter = delegate(object row)
            {
                var value = row as IpsPatchElement;
                if (value != null)
                {
                    return string.Format("{0:X6}", value.End);
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnEnd.Tag = 6;

            this.olvColumnIpsOffset.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {
                    return string.Format("{0:X8}", value.IpsOffset);
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnIpsOffset.Tag = 8;

            this.olvColumnIpsEnd.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {
                    return string.Format("{0:X8}", value.IpsEnd);
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnIpsEnd.Tag = 8;

            this.olvColumnIpsSizeHex.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {
                    return string.Format("{0:X}", value.IpsSize);
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnIpsSizeHex.Tag = 5;

            this.olvColumnIpsSize.AspectGetter = delegate(object row)
            {
                var value = row as IpsElement;
                if (value != null)
                {
                    return value.IpsSize;
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnIpsSize.Tag = 8;

            this.olvColumnOffset.AspectGetter = delegate(object row)
            {
                if (row is IpsResizeValueElement)
                {
                    return string.Format("{0:X6}", ((IpsResizeValueElement)row).GetIntValue());
                }
                else if (row is IpsPatchElement)
                {
                    return string.Format("{0:X6}", ((IpsPatchElement)row).Offset);
                }
                else
                { return string.Empty; }
            };
            this.olvColumnOffset.Tag = 6;

            this.olvColumnSizeHex.AspectGetter = delegate(object row)
            {
                var value = row as IpsPatchElement;
                if (value != null)
                {
                    return string.Format("{0:X}", value.Size);
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnSizeHex.Tag = 4;

            this.olvColumnSize.AspectGetter = delegate(object row)
            {
                var value = row as IpsPatchElement;
                if (value != null)
                {
                    return value.Size;
                }
                else
                {
                    return string.Empty;
                }
            };
            this.olvColumnSize.Tag = 5;

            this.olvColumnType.AspectGetter = delegate(object row)
            {
                string name = string.Empty;
                try
                {
                    name = GetDisplayName(row.GetType());
                }
                catch
                {
                }
                return name;
            };
            this.olvColumnType.Tag = 3;

            this.olvColumnNumber.AspectGetter = delegate(object row)
            {
                string index = (fastObjectListViewRows.IndexOf(row) + 1).ToString();
                this.olvColumnNumber.Tag = Math.Max((int)this.olvColumnNumber.Tag, index.Length);
                return index;
            };
            this.olvColumnNumber.Tag = 0;
            this.fastObjectListViewRows.UseFiltering = true;
            this.closeToolStripMenuItem.Enabled = false;
            this.closeToolStripButton.Enabled = false;
            hexBoxData.LineInfoVisible = true;
            hexBoxData.ColumnInfoVisible = true;
            hexBoxData.VScrollBarVisible = true;
            hexBoxData.UseFixedBytesPerLine = false;
            hexBoxData.LineInfoVisible = true;

            toolbarToolStripMenuItem.Checked = true;

            dataViewToolStripMenuItem.Checked = true;

            copyRowToolStripMenuItem.Enabled = false;
            toolStripButtonCopyRow.Enabled = false;

            goToRowToolStripMenuItem.Enabled = false;
            toolStripButtonGoToRow.Enabled = false;

            this.StartPosition = FormStartPosition.Manual;

            exportToolStripButton.Enabled = false;
            exportToolStripMenuItem.Enabled = false;

            fastObjectListViewRows.DefaultRenderer = _highlighter;

            toolStripButtonUnlinkFile.Enabled = false;

            _findDialog = new FindHexBoxDialog();
            _findDialog.StartPosition = FormStartPosition.CenterParent;
            _findDialog.SetHexEditor(hexBoxData);

            _goToOffsetDialog = new GoToHexBoxDialog();

            _goToOffsetDialog.StartPosition = FormStartPosition.CenterParent;

            horizontalLayoutToolStripMenuItem.CheckState = CheckState.Indeterminate;

            toolStripStatusLabelPatchFileSizeSeparator.Visible = false;
            toolStripStatusLabelFileSeparator.Visible = false;

            toolStripButtonStart.Enabled = false;

            UpateDataViewToolStrip(false);

            // Try to load a file from the command line (such as a file that was dropped onto the icon).
            try
            {
                string file = Environment.GetCommandLineArgs()[1];
                LoadPatch(file);
            }
            catch
            {
            }

            LoadSettings();

            UpdateCheck();
        }

        private void UpdateCheck()
        {
            UpdateManager.Instance.UpdateSource = new SimpleWebSource("http://update.codeisle.com/ips-peek/feed.xml");
            UpdateManager.Instance.ReinstateIfRestarted();
            UpdateManager.Instance.CleanUp();

            try
            {
                UpdateManager.Instance.CheckForUpdates();
            }
            catch (NAppUpdateException ex)
            {
            }
            catch (Exception ex)
            {
            }
            updateNowToolStripMenuItem.Visible = (UpdateManager.Instance.UpdatesAvailable != 0);
        }

        private void UpateDataViewToolStrip(bool enable)
        {
            toolStripButtonGoToOffset.Enabled = enable;
            toolStripButtonSelectAll.Enabled = enable;
            toolStripButtonCopy.Enabled = enable;
            toolStripButtonFind.Enabled = enable;
        }

        private void openPatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPatch();
        }

        private void objectListView1_SelectionChanged(object sender, EventArgs e)
        {
            SelectPatch((IpsElement)fastObjectListViewRows.SelectedObject);
        }

        private void SelectPatch(IpsElement element)
        {
            if (_fileData != null)
            {
                long offset = 0;
                long size = 0;

                if (element is IpsPatchElement)
                {
                    offset = (long)((IpsPatchElement)element).Offset;
                    size = (long)((IpsPatchElement)element).Size;
                    hexBoxData.SelectionStart = 0;
                    hexBoxData.ScrollByteIntoView(offset + size);
                    hexBoxData.SelectionStart = offset;
                    hexBoxData.SelectionLength = size;
                }

                try
                {
                    toolStripStatusLabelRows.Text = string.Format(Strings.Row, fastObjectListViewRows.SelectedIndex + 1, fastObjectListViewRows.Items.Count, size);
                }
                catch
                {
                    toolStripStatusLabelRows.Text = string.Empty;
                }
            }
            else if (element is IpsPatchElement)
            {
                int size = 0;

                hexBoxData.LineInfoOffset = (long)((IpsPatchElement)element).Offset;
                hexBoxData.ByteProvider = new DynamicByteProvider(((IpsPatchElement)element).GetData());

                size = ((IpsPatchElement)element).Size;
                UpateDataViewToolStrip(true);

                UpdateOffsetStatus();

                try
                {
                    toolStripStatusLabelRows.Text = string.Format(Strings.Row, fastObjectListViewRows.SelectedIndex + 1, fastObjectListViewRows.Items.Count, size);
                }
                catch
                {
                    toolStripStatusLabelRows.Text = string.Empty;
                }
            }
            else
            {
                copyRowToolStripMenuItem.Enabled = false;
                toolStripButtonCopyRow.Enabled = false;
                toolStripStatusLabelRows.Text = string.Empty;
                hexBoxData.ByteProvider = null;
                UpdateOffsetStatus();
                UpateDataViewToolStrip(false);
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClosePatch();
        }

        private void openPatchToolStripButton_Click(object sender, EventArgs e)
        {
            OpenPatch();
        }

        private void closeToolStripButton_Click(object sender, EventArgs e)
        {
            ClosePatch();
        }

        private void exportToolStripButton_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            ExportFile();
            this.Enabled = true;
        }

        private void toolbarToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            toolStrip1.Visible = toolbarToolStripMenuItem.Checked;
        }

        private void dataViewToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            splitContainer1.Panel2Collapsed = !dataViewToolStripMenuItem.Checked;
        }

        private void FormMain_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                Array data = (Array)e.Data.GetData(DataFormats.FileDrop);
                if ((data != null))
                {
                    var file = data.GetValue(0).ToString();

                    this.BeginInvoke((Action<string>)((string value) => { LoadPatch(value); }), new object[] { file });

                    this.Activate();
                }
            }
            catch (Exception)
            {
            }
        }

        private void FormMain_DragEnter(object sender, DragEventArgs e)
        {
            if ((e.Data.GetDataPresent(DataFormats.FileDrop)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void filterToolStripTextBox_TextChanged(object sender, EventArgs e)
        {
            if (filterToolStripTextBox.TextLength == 0)
            {
                var filter = TextMatchFilter.Contains(this.fastObjectListViewRows, string.Empty);
                _highlighter.Filter = filter;
                fastObjectListViewRows.ModelFilter = filter;
                fastObjectListViewRows.Refresh();
            }
        }

        private void filterToolStripTextBox_Enter(object sender, EventArgs e)
        {
            // Kick off SelectAll asyncronously so that it occurs after Click
            BeginInvoke((Action)delegate
            {
                filterToolStripTextBox.SelectAll();
            });
        }

        private void officialForumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://www.codeisle.com/forum/product/ips-peek/");
        }

        private void iPSPeekHomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://www.codeisle.com/");
        }

        private void helpContentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://help.codeisle.com/ips-peek/");
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            ExportFile();
            this.Enabled = true;
        }

        private void aboutIPSPeekToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FormAbout about = new FormAbout())
            {
                about.StartPosition = FormStartPosition.CenterParent;
                about.ShowDialog(this);
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.olvColumnIpsOffset.AspectGetter = null;
            this.olvColumnIpsEnd.AspectGetter = null;
            this.olvColumnIpsSizeHex.AspectGetter = null;
            this.olvColumnOffset.AspectGetter = null;

            SaveSettings();
            DeleteTempFiles();
        }

        private void DeleteTempFiles()
        {
            foreach (string file in tempFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }

        private void filterToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var filter = TextMatchFilter.Contains(this.fastObjectListViewRows, filterToolStripTextBox.Text);
                _highlighter.Filter = filter;
                fastObjectListViewRows.ModelFilter = filter;
                fastObjectListViewRows.Refresh();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void toolStripButtonGoToRow_Click(object sender, EventArgs e)
        {
            GoToRow();
        }

        private void toolStripButtonCopyRow_Click(object sender, EventArgs e)
        {
            CopyRow();
        }

        private void goToRowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoToRow();
        }

        private void copyRowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyRow();
        }

        private void toolStripButtonStringView_CheckStateChanged(object sender, EventArgs e)
        {
            hexBoxData.StringViewVisible = toolStripButtonStringView.Checked;
        }

        private void toolStripButtonSelectAll_Click(object sender, EventArgs e)
        {
            hexBoxData.SelectAll();
        }

        private void toolStripButtonGoToOffset_Click(object sender, EventArgs e)
        {
            GoToOffset();
        }

        private void GoToOffset()
        {
            _goToOffsetDialog.Minimum = hexBoxData.LineInfoOffset;
            _goToOffsetDialog.Maximum = hexBoxData.LineInfoOffset + ((DynamicByteProvider)hexBoxData.ByteProvider).Length - 1;
            _goToOffsetDialog.Value = hexBoxData.LineInfoOffset + hexBoxData.SelectionStart;

            if (_goToOffsetDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                hexBoxData.Focus();
                hexBoxData.SelectionStart = _goToOffsetDialog.Value - hexBoxData.LineInfoOffset;
            }
        }

        private void toolStripButtonFind_ButtonClick(object sender, EventArgs e)
        {
            _findDialog.FindOptions.Direction = FindDirection.Beginning;
            ShowFindDialog();
        }

        private void ShowFindDialog()
        {
            if (_findDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                _findDialog.FindOptions.IsValid = true;
                Find();
            }
        }

        private void findNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _findDialog.FindOptions.Direction = FindDirection.Forward;
            Find();
        }

        private void Find()
        {
            if (!_findDialog.FindOptions.IsValid)
            {
                ShowFindDialog();
                return;
            }
            if (_findDialog.Find() < 0)
            {
                if (_findDialog.FindOptions.Type == FindType.Hex)
                {
                    MessageBox.Show(string.Format("The following data was not found: \"{0}\"", BitConverter.ToString(_findDialog.FindOptions.Hex)));
                }
                else
                {
                    MessageBox.Show(string.Format("The following text was not found: \"{0}\"", _findDialog.FindOptions.Text));
                }
            }
        }

        private void UpdateOffsetStatus()
        {
            toolStripButtonCopy.Enabled = (hexBoxData.SelectionLength > 0);
            toolStripMenuItemCopy.Enabled = (hexBoxData.SelectionLength > 0);
            toolStripMenuItemCopyHex.Enabled = (hexBoxData.SelectionLength > 0);

            toolStripStatusLabelLength.Text = string.Empty;
            toolStripStatusLabelLine.Text = string.Empty;
            toolStripStatusLabelColumn.Text = string.Empty;
            toolStripStatusLabelOffset.Text = string.Empty;

            if (hexBoxData.ByteProvider != null)
            {
                toolStripStatusLabelLine.Text = string.Format(Strings.Line, hexBoxData.CurrentLine);
                toolStripStatusLabelColumn.Text = string.Format(Strings.Column, hexBoxData.CurrentPositionInLine);
            }

            if (hexBoxData.SelectionLength > 0)
            {
                toolStripStatusLabelLength.Text = string.Format(Strings.Length, hexBoxData.SelectionLength);
            }
            if (hexBoxData.SelectionLength > 1)
            {
                toolStripStatusLabelOffset.Text = string.Format(Strings.Block, hexBoxData.LineInfoOffset + hexBoxData.SelectionStart, hexBoxData.LineInfoOffset + hexBoxData.SelectionStart + hexBoxData.SelectionLength - 1);
            }
            else if (hexBoxData.SelectionStart >= 0)
            {
                toolStripStatusLabelOffset.Text = string.Format(Strings.OffsetStatus, hexBoxData.LineInfoOffset + hexBoxData.SelectionStart);
            }
        }

        private void toolStripButtonCopy_ButtonClick(object sender, EventArgs e)
        {
            hexBoxData.Copy();
        }

        private void copyHexToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hexBoxData.CopyHex();
        }

        private void findPreviousToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _findDialog.FindOptions.Direction = FindDirection.Backward;
            Find();
        }

        private void toolStripMenuItemCopy_Click(object sender, EventArgs e)
        {
            hexBoxData.Copy();
        }

        private void toolStripMenuItemCopyHex_Click(object sender, EventArgs e)
        {
            hexBoxData.CopyHex();
        }

        private void toolStripMenuItemSelectAll_Click(object sender, EventArgs e)
        {
            hexBoxData.SelectAll();
        }

        // TODO: Refactor hexBoxData_SelectionStartChanged.
        private void hexBoxData_SelectionStartChanged(object sender, EventArgs e)
        {
            UpdateOffsetStatus();
        }

        private void hexBoxData_SelectionLengthChanged(object sender, EventArgs e)
        {
            UpdateOffsetStatus();
        }

        private void toolStripButtonLinkFile_Click(object sender, EventArgs e)
        {
            if (OpenFile() == System.Windows.Forms.DialogResult.OK)
            {
                toolStripButtonUnlinkFile.Enabled = true;
                toolStripStatusLabelFile.Text = string.Format("File: {0}", _fileName);
                toolStripStatusLabelFileSize.Text = string.Format(Strings.FileSize, _fileSize);
                UpdateLinkedFileDateView();

                SelectPatch((IpsElement)fastObjectListViewRows.SelectedObject);
            }
        }

        private void toolStripButtonUnlinkFile_Click(object sender, EventArgs e)
        {
            toolStripButtonUnlinkFile.Enabled = false;
            toolStripButtonLinkFile.Enabled = true;
            CloseFile();
            SelectPatch((IpsElement)fastObjectListViewRows.SelectedObject);
            toolStripStatusLabelFile.Text = string.Empty;
            toolStripStatusLabelFileSize.Text = string.Empty;
            UpdateLinkedFileDateView();
        }

        private void horizontalLayoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (horizontalLayoutToolStripMenuItem.CheckState == CheckState.Unchecked)
            {
                SetHorizontalLayout();
            }
        }

        private void SetHorizontalLayout()
        {
            horizontalLayoutToolStripMenuItem.CheckState = CheckState.Indeterminate;
            splitContainer1.Orientation = Orientation.Vertical;
            verticalLayoutToolStripMenuItem.CheckState = CheckState.Unchecked;
        }

        private void verticalLayoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (verticalLayoutToolStripMenuItem.CheckState == CheckState.Unchecked)
            {
                SetVerticalLayout();
            }
        }

        private void SetVerticalLayout()
        {
            verticalLayoutToolStripMenuItem.CheckState = CheckState.Indeterminate;
            splitContainer1.Orientation = Orientation.Horizontal;
            horizontalLayoutToolStripMenuItem.CheckState = CheckState.Unchecked;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SettingsDialog dialog = new SettingsDialog())
            {
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ShowDialog(this);
            }
        }

        private void toolStripButtonSelectEmulator_Click(object sender, EventArgs e)
        {
            SelectEmulator();
        }

        public bool SelectEmulator()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = Strings.EmulatorFilter;
                if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK) return false;

                _applicationConfiguration.Emulator = dialog.FileName;

                return true;
            }
        }

        private void toolStripButtonStart_Click(object sender, EventArgs e)
        {
            if (!File.Exists(_applicationConfiguration.Emulator))
            {
                if (MessageBox.Show(this, Strings.MessageSetEmulator, Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.Yes)
                {
                    if (SelectEmulator())
                    {
                        RunPatchedGame();
                    }
                }
            }
            else
            {
                RunPatchedGame();
            }
        }

        private void RunPatchedGame()
        {
            string extension = Path.GetExtension(_fileName);
            string tempFile = GetTempFileName() + extension;
            tempFiles.Add(tempFile);

            using (FileStream stream = File.Create(tempFile))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.Write(_fileData, 0, _fileData.Length);

                if (_patches != null)
                {
                    foreach (IpsElement element in _patches)
                    {
                        if (element is IWritable)
                        {
                            ((IWritable)element).Write(stream);
                        }
                    }
                }
            }
            try
            {
                Process.Start(_applicationConfiguration.Emulator, tempFile);
            }
            catch
            {
                MessageBox.Show(this, string.Format(Strings.ErrorEmulatorFailed), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private List<string> tempFiles = new List<string>();

        private string GetTempFileName()
        {
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        private void fastObjectListViewRows_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            Point scrollPosition = hexBoxData.AutoScrollOffset;
            UpdateLinkedFileDateView();
            hexBoxData.AutoScrollOffset = scrollPosition;
            SelectPatch((IpsElement)fastObjectListViewRows.SelectedObject);
        }

        private void updateNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Update the application now?", Application.ProductName, MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                UpdateManager.Instance.PrepareUpdates();
                UpdateManager.Instance.ApplyUpdates();
            }
        }
    }
}