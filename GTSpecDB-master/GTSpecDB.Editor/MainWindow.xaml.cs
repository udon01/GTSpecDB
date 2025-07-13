using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Humanizer;
using Humanizer.Localisation.TimeToClockNotation;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell;
using PDTools.SpecDB.Core;
using PDTools.SpecDB.Core.Mapping;
using PDTools.SpecDB.Core.Mapping.Tables;
using PDTools.SpecDB.Core.Mapping.Types;
using PDTools.Utils;

namespace GTSpecDB.Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public const string WindowTitle = "Gran Turismo Spec Database Editor";

        public int copycount = 1;
        public string BeforeRowName = "";
        public string NewRowName = "";
        public string before = "";
        public string after = "";
        public int loopcount = 1;

        public SpecDB CurrentDatabase { get; set; }
        public PDTools.SpecDB.Core.Table CurrentTable { get; set; }
        public string SpecDBDirectory { get; set; }

        private string _filterString;
        public string FilterString
        {
            get => _filterString;
            set
            {
                _filterString = value;
                NotifyPropertyChanged("FilterString");
                FilterCollection();
            }
        }

        private ICollectionView _dataGridCollection;
        private void FilterCollection()
        {
            if (_dataGridCollection != null)
            {
                _dataGridCollection.Refresh();
            }
        }

        public MainWindow()
        {
            if (Properties.Settings.Default.Language == "")
            {
                switch (CultureInfo.CurrentCulture.IetfLanguageTag)
                {
                    case "ja-JP":
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja");
                        Properties.Settings.Default.Language = "ja";
                        Properties.Settings.Default.Save();
                        break;
                    default:
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("");
                        Properties.Settings.Default.Language = "en";
                        Properties.Settings.Default.Save();
                        break;
                }
            }
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri(@"Resources/StringResource." + Properties.Settings.Default.Language + @".xaml", UriKind.Relative);
            this.Resources.MergedDictionaries.Add(dictionary);

            InitializeComponent();

            dg_Rows.Columns.Add(new DataGridTextColumn
            {
                Header = "ID",
                Binding = new Binding("ID"),
            });

            dg_Rows.Columns.Add(new DataGridTextColumn
            {
                Header = "Label",
                Binding = new Binding("Label"),
            });

            cb_FilterColumnType.Items.Add("ID");
            cb_FilterColumnType.Items.Add("Label");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string property)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        private void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 1)
                        return;

                    if (!Directory.Exists(files[0]))
                        return;

                    var specType = SpecDB.DetectSpecDBType(Path.GetFileName(files[0]));
                    if (specType is null)
                    {
                        var window = new SpecDBKindSelector();
                        window.ShowDialog();
                        if (!window.HasSelected || window.SelectedType == SpecDBFolder.NONE)
                            return;

                        specType = window.SelectedType;
                    }

                    CurrentDatabase?.Dispose();

                    CurrentDatabase = SpecDB.LoadFromSpecDBFolder(files[0], specType.Value, false);
                    SpecDBDirectory = files[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Resource.Load_Failed_specDB_M + $"{ex.Message}", Resource.Load_Failed_specDB_T, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ProcessNewlyLoadedSpecDB();
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            var toolBar = sender as ToolBar;
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
                overflowGrid.Visibility = Visibility.Collapsed;

            var mainPanelBorder = toolBar.Template.FindName("MainPanelBorder", toolBar) as FrameworkElement;
            if (mainPanelBorder != null)
                mainPanelBorder.Margin = new Thickness(0);
        }

        private void mi_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Top Menu
        private void OpenSpecDB_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog(Resource.Open_SpecDB);
            dlg.EnsurePathExists = true;
            dlg.EnsureFileExists = true;
            dlg.IsFolderPicker = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    var specType = SpecDB.DetectSpecDBType(Path.GetFileName(dlg.FileName));
                    if (specType is null)
                    {
                        var window = new SpecDBKindSelector();
                        window.ShowDialog();
                        if (!window.HasSelected || window.SelectedType == SpecDBFolder.NONE)
                            return;
                        specType = window.SelectedType;
                    }

                    CurrentDatabase?.Dispose();

                    CurrentDatabase = SpecDB.LoadFromSpecDBFolder(dlg.FileName, specType.Value, false);
                    SpecDBDirectory = dlg.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Resource.Load_Failed_specDB_M + $"{ex.Message}", Resource.Load_Failed_specDB_T, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // バックアップ処理
                string backupfolderpath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + Resource.Backup;
                if (!Directory.Exists(backupfolderpath))
                    Directory.CreateDirectory(backupfolderpath);
                string[] files = Directory.GetFiles(dlg.FileName, "*");
                string localtime = DateTime.Now.ToString();
                localtime = localtime.Replace("/", "-").Replace(":", ",");
                backupfolderpath = backupfolderpath + @"\" + localtime + " " + Path.GetFileNameWithoutExtension(dlg.FileName);
                if (!Directory.Exists(backupfolderpath))
                    Directory.CreateDirectory(backupfolderpath);
                backupfolderpath = backupfolderpath + @"\" + Path.GetFileNameWithoutExtension(dlg.FileName);
                Directory.CreateDirectory(backupfolderpath);

                for (int i = 0; i < files.Count(); i++)
                {
                    try
                    {
                        File.Copy(files[i], backupfolderpath + @"\" + Path.GetFileName(files[i]), true);
                    }
                    catch(UnauthorizedAccessException)
                    {
                        MessageBox.Show(Resource.Save_Failed_Backup_M, Resource.Save_Failed_Backup_T, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                ProcessNewlyLoadedSpecDB();
            }
        }

        private async void SavePartsInfo_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog(Resource.SavePartsInfo_dlg);
            dlg.EnsurePathExists = true;
            dlg.EnsureFileExists = true;
            dlg.IsFolderPicker = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (!CurrentDatabase.Tables.TryGetValue("GENERIC_CAR", out PDTools.SpecDB.Core.Table genericCar))
                {
                    MessageBox.Show(Resource.Save_Failed_PartsInfo_M, Resource.Save_Failed_PartsInfo_T, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!genericCar.IsLoaded)
                    genericCar.LoadAllRows(CurrentDatabase);

                var progressWindow = new ProgressWindow();
                progressWindow.Title = Resource.SavePartsInfo_PW_Title;
                progressWindow.progressBar.Maximum = genericCar.Rows.Count;
                var progress = new Progress<(int Index, string CarLabel)>(prog =>
                {
                    progressWindow.lbl_progress.Content = $"{prog.Index} of {progressWindow.progressBar.Maximum}";
                    progressWindow.currentElement.Content = prog.CarLabel;
                    progressWindow.progressBar.Value = prog.Index;
                });

                var task = SavePartsInfoFileAsync(progressWindow, progress, true, dlg.FileName);
                progressWindow.ShowDialog();
                await task;

                if (Properties.Settings.Default.Language == "ja")
                    statusName.Text = "PartsInfo.tbi/tbdを" + $"{dlg.FileName}" + "に保存しました";
                else if (Properties.Settings.Default.Language == "en")
                    statusName.Text = "PartsInfo.tbi/tbd saved to " + $"{dlg.FileName}" + ".";
            }
        }

        async Task SavePartsInfoFileAsync(ProgressWindow progressWindow, Progress<(int, string)> progress, bool tbdFile, string fileName)
        {
            try
            {
                await Task.Run(() => CurrentDatabase.SavePartsInfoFile(progress, tbdFile, fileName));
            }
            finally
            {
                progressWindow.Close();
            }
        }

        /// <summary>
        /// Parts Table saving
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SaveCarsParts_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog(Resource.SaveCarsParts_dlg);
            dlg.EnsurePathExists = true;
            dlg.EnsureFileExists = true;
            dlg.IsFolderPicker = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (!CurrentDatabase.Tables.TryGetValue("GENERIC_CAR", out PDTools.SpecDB.Core.Table genericCar))
                {
                    MessageBox.Show(Resource.Save_Failed_CarsParts_M, Resource.Save_Failed_CarsParts_T, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!genericCar.IsLoaded)
                    genericCar.LoadAllRows(CurrentDatabase);

                var progressWindow = new ProgressWindow();
                progressWindow.Title = Resource.SaveCarsParts_PW_Title;
                progressWindow.progressBar.Maximum = genericCar.Rows.Count;
                var progress = new Progress<(int Index, string CarLabel)>(prog =>
                {
                    progressWindow.lbl_progress.Content = $"{prog.Index} of {progressWindow.progressBar.Maximum}";
                    progressWindow.currentElement.Content = prog.CarLabel;
                    progressWindow.progressBar.Value = prog.Index;
                });

                var task = SavePartsInfoFileAsync(progressWindow, progress, false, dlg.FileName);
                progressWindow.ShowDialog();
                await task;

                if (Properties.Settings.Default.Language == "ja")
                    statusName.Text = "CarPartsを" + $"{dlg.FileName}" + "に保存しました";
                else if (Properties.Settings.Default.Language == "en")
                    statusName.Text = "Car parts saved to " + $"{dlg.FileName}" + ".";
            }
        }

        private void SaveCurrentTable_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog(Resource.SaveCurrentTable_dlg);
            dlg.EnsurePathExists = true;
            dlg.EnsureFileExists = true;
            dlg.IsFolderPicker = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                CurrentTable.SaveTable(CurrentDatabase, dlg.FileName);

                if (Properties.Settings.Default.Language == "ja")
                    statusName.Text = "テーブルを" + $"{dlg.FileName}" + "に保存しました";
                else if (Properties.Settings.Default.Language == "en")
                    statusName.Text = "Table saved to " + $"{dlg.FileName}" + ".";
            }
        }

        private void ExportCurrentTable_Click(object sender, RoutedEventArgs e)
        {
            CommonSaveFileDialog dlg = new CommonSaveFileDialog(Resource.ExportCurrentTable_dlg);
            dlg.EnsurePathExists = true;
            dlg.EnsureFileExists = true;
            dlg.DefaultFileName = $"{CurrentTable.TableName}.txt";

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                CurrentTable.ExportTableText(((ShellFile)dlg.FileAsShellObject).Path);
        }

        private void ExportCurrentTableCSV_Click(object sender, RoutedEventArgs e)
        {
            CommonSaveFileDialog dlg = new CommonSaveFileDialog(Resource.ExportCurrentTableCSV_dlg);
            dlg.EnsurePathExists = true;
            dlg.EnsureFileExists = true;
            dlg.DefaultFileName = $"{CurrentTable.TableName}.csv";
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                CurrentTable.ExportTableCSV(((ShellFile)dlg.FileAsShellObject).Path);
        }

        private async void ExportCurrentTableSQLite_Click(object sender, RoutedEventArgs e)
        {
            
        }
        #endregion

        #region Toolbar
        private void btn_AddRow_Click(object sender, RoutedEventArgs e)
        {
            var newRow = new RowData();
            newRow.ID = ++CurrentTable.LastID;
            CurrentTable.Rows.Add(newRow);

            foreach (var colMeta in CurrentTable.TableMetadata.Columns)
            {
                switch (colMeta.ColumnType)
                {
                    case DBColumnType.Bool:
                        newRow.ColumnData.Add(new DBBool(false)); break;
                    case DBColumnType.Byte:
                        newRow.ColumnData.Add(new DBByte(0)); break;
                    case DBColumnType.SByte:
                        newRow.ColumnData.Add(new DBSByte(0)); break;
                    case DBColumnType.Short:
                        newRow.ColumnData.Add(new DBShort(0)); break;
                    case DBColumnType.UShort:
                        newRow.ColumnData.Add(new DBUShort(0)); break;
                    case DBColumnType.Int:
                        newRow.ColumnData.Add(new DBInt(0)); break;
                    case DBColumnType.UInt:
                        newRow.ColumnData.Add(new DBUInt(0)); break;
                    case DBColumnType.Long:
                        newRow.ColumnData.Add(new DBLong(0)); break;
                    case DBColumnType.Float:
                        newRow.ColumnData.Add(new DBFloat(0)); break;
                    case DBColumnType.String:
                        newRow.ColumnData.Add(new DBString(0, colMeta.StringFileName)); break;
                    default:
                        break;
                }
            }

            dg_Rows.ScrollIntoView(newRow);
        }

        private void btn_DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (dg_Rows.SelectedIndex == -1 || !dg_Rows.CurrentCell.IsValid)
                return;

            CurrentTable.Rows.Remove(dg_Rows.CurrentCell.Item as RowData);
            CurrentTable.LastID = CurrentTable.Rows.Max(row => row.ID);

            if (Properties.Settings.Default.Language == "ja")
                statusName.Text = "選択された行を削除しました";
            else if (Properties.Settings.Default.Language == "en")
                statusName.Text = "Row deleted.";
        }

        private void btn_CopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (dg_Rows.SelectedIndex == -1 || !dg_Rows.CurrentCell.IsValid)
                return;

            var selectedRow = dg_Rows.CurrentCell.Item as RowData;

            var newRow = new RowData();
            newRow.ID = ++CurrentTable.LastID;
            newRow.Label = $"{selectedRow.Label}_copy" + copycount;
            CurrentTable.Rows.Add(newRow);

            for (int i = 0; i < CurrentTable.TableMetadata.Columns.Count; i++)
            {
                ColumnMetadata colMeta = CurrentTable.TableMetadata.Columns[i];
                switch (colMeta.ColumnType)
                {
                    case DBColumnType.Bool:
                        newRow.ColumnData.Add(new DBBool(((DBBool)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.Byte:
                        newRow.ColumnData.Add(new DBByte(((DBByte)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.SByte:
                        newRow.ColumnData.Add(new DBSByte(((DBSByte)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.Short:
                        newRow.ColumnData.Add(new DBShort(((DBShort)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.UShort:
                        newRow.ColumnData.Add(new DBUShort(((DBUShort)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.Int:
                        newRow.ColumnData.Add(new DBInt(((DBInt)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.UInt:
                        newRow.ColumnData.Add(new DBUInt(((DBUInt)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.Float:
                        newRow.ColumnData.Add(new DBFloat(((DBFloat)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.Long:
                        newRow.ColumnData.Add(new DBLong(((DBLong)selectedRow.ColumnData[i]).Value)); break;
                    case DBColumnType.String:
                        var str = new DBString(((DBString)selectedRow.ColumnData[i]).StringIndex, colMeta.StringFileName);
                        str.Value = CurrentDatabase.StringDatabases[colMeta.StringFileName].Strings[str.StringIndex];
                        newRow.ColumnData.Add(str);
                        break;
                    default:
                        break;
                }
            }

            dg_Rows.ScrollIntoView(newRow);
            copycount += 1;
        }

        private void btn_Save_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(CurrentTable.TableName);
            CurrentTable.SaveTable(CurrentDatabase, SpecDBDirectory);

            if (Properties.Settings.Default.Language == "ja")
                statusName.Text = "テーブルを" + $"{SpecDBDirectory}" + "に保存しました";
            else if (Properties.Settings.Default.Language == "en")
                statusName.Text = "Table saved to " + $"{SpecDBDirectory}" + ".";
        }

        private void btn_SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentTable_Click(sender, e);
        }

        private void tb_ColumnFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tb_ColumnFilter.Text != null && (tb_ColumnFilter.Text.Length > 3 || tb_ColumnFilter.Text.Length == 0))
            {
                // Apparently only twice works, so lol
                dg_Rows.CancelEdit();
                dg_Rows.CancelEdit();
                FilterString = tb_ColumnFilter.Text;
            }
        }
        #endregion

        #region Datagrid Events
        private void dg_Rows_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!(e.EditingElement is TextBox tb))
                return;

            var currentRow = e.Row.Item as RowData;
            string newInput = tb.Text;
            if (dg_Rows.Columns[0] == e.Column) // Editing ID column
            {
                if (int.TryParse(newInput, out int newValue))
                {
                    if (CurrentTable.Rows.Any(row => row.ID == newValue && row != currentRow))
                    {
                        var res = MessageBox.Show(Resource.ID_Duplicate_M, Resource.ID_Duplicate_T, MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (res != MessageBoxResult.Yes)
                        {
                            currentRow.ID = CurrentTable.LastID + 1;
                            e.Cancel = true;
                            return;
                        }
                    }

                    var nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID >= newValue);
                    currentRow.ID = newValue;

                    // Put it to the last of said id if it conflicts
                    if (nextRow != null && nextRow.ID == newValue)
                        nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID > newValue);

                    if (nextRow is null) // End of list?
                    {
                        CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(currentRow), CurrentTable.Rows.Count - 1);
                        return; 
                    }

                    var nextRowIndex = CurrentTable.Rows.IndexOf(nextRow);
                    if (nextRowIndex > CurrentTable.Rows.IndexOf(currentRow)) // If the row is being moved backwards
                        nextRowIndex--;

                    CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(currentRow), nextRowIndex);
                    dg_Rows.ScrollIntoView(currentRow);
                }
                else
                {
                    currentRow.ID = CurrentTable.LastID + 1;
                    e.Cancel = true;
                }
            }
            else if (dg_Rows.Columns[1] == e.Column) // Editing Label Column
            {
                if (CurrentTable.Rows.Any(row => row.Label != null && row.Label.Equals(newInput) && row != currentRow))
                {
                    var res = MessageBox.Show(Resource.Label_Duplicate_M, Resource.Label_Duplicate_T, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes)
                    {
                        currentRow.Label = string.Empty;
                        e.Cancel = true;
                        return;
                    }
                }

                if (!newInput.All(c => char.IsLetterOrDigit(c) || c.Equals('_')))
                {
                    MessageBox.Show(Resource.Label_chara_error_M, Resource.Label_chara_error_T);
                    currentRow.Label = string.Empty;
                    e.Cancel = true;
                }

                currentRow.Label = newInput;
            }
            else
            {
                // Perform regular validation
                ColumnMetadata dataCol = CurrentTable.TableMetadata.Columns[dg_Rows.Columns.IndexOf(e.Column) - 2];
                if (dataCol.ColumnType == DBColumnType.Int)
                {
                    if (!int.TryParse(newInput, out int res))
                    {
                        MessageBox.Show(Resource.Out_of_range_int_M1 + $"{int.MinValue}" + Resource.Out_of_range_M2 + $"{int.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
                else if (dataCol.ColumnType == DBColumnType.UInt)
                {
                    if (!uint.TryParse(newInput, out uint res))
                    {
                        MessageBox.Show(Resource.Out_of_range_uint_M1 + $"{uint.MinValue}" + Resource.Out_of_range_M2 + $"{uint.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
                else if (dataCol.ColumnType == DBColumnType.Short)
                {
                    if (!short.TryParse(newInput, out short res))
                    {
                        MessageBox.Show(Resource.Out_of_range_short_M1 + $"{short.MinValue}" + Resource.Out_of_range_M2 + $"{short.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
                else if (dataCol.ColumnType == DBColumnType.Short)
                {
                    if (!ushort.TryParse(newInput, out ushort res))
                    {
                        MessageBox.Show(Resource.Out_of_range_ushort_M1 + $"{ushort.MinValue}" + Resource.Out_of_range_M2 + $"{ushort.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
                else if (dataCol.ColumnType == DBColumnType.Byte)
                {
                    if (!byte.TryParse(newInput, out byte res))
                    {
                        MessageBox.Show(Resource.Out_of_range_byte_M1 + $"{byte.MinValue}" + Resource.Out_of_range_M2 + $"{byte.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
                else if (dataCol.ColumnType == DBColumnType.SByte)
                {
                    if (!sbyte.TryParse(newInput, out sbyte res))
                    {
                        MessageBox.Show(Resource.Out_of_range_sbyte_M1 + $"{sbyte.MinValue}" + Resource.Out_of_range_M2 + $"{sbyte.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
                else if (dataCol.ColumnType == DBColumnType.Float)
                {
                    if (!float.TryParse(newInput, out float res))
                    {
                        MessageBox.Show(Resource.Out_of_range_float_M1 + $"{float.MinValue}" + Resource.Out_of_range_M2 + $"{float.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
                else if (dataCol.ColumnType == DBColumnType.Long)
                {
                    if (!long.TryParse(newInput, out long res))
                    {
                        MessageBox.Show(Resource.Out_of_range_long_M1 + $"{long.MinValue}" + Resource.Out_of_range_M2 + $"{long.MaxValue}" + Resource.Out_of_range_M3, Resource.Out_of_range_T,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        e.Cancel = true;
                    }
                }
            }
        }

        private void dg_Rows_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is DataGridCell cell))
                return;

            var colIndex = dg_Rows.Columns.IndexOf(cell.Column);
            if (colIndex == 0 || colIndex == 1)
                return;

            ColumnMetadata dataCol = CurrentTable.TableMetadata.Columns[colIndex - 2];
            if (dataCol is null)
                return;

            if (dataCol.ColumnType == DBColumnType.String)
            {
                var strDb = CurrentDatabase.StringDatabases[dataCol.StringFileName];

                // Find column index to apply our row data to
                var row = cell.DataContext as RowData;
                int columnIndex = CurrentTable.TableMetadata.Columns.IndexOf(dataCol);

                var str = row.ColumnData[columnIndex] as DBString;
                int index = strDb.Strings.IndexOf(str.Value);

                var strWindow = new StringDatabaseManager(strDb, index);
                strWindow.ShowDialog();
                if (strWindow.HasSelected)
                {
                    // Apply string change
                    str.StringIndex = strWindow.SelectedString.index;
                    str.Value = strWindow.SelectedString.selectedString;
                }
            }
        }

        private void dg_Rows_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Clipboard.ContainsText())
            {
                string clipText = Clipboard.GetText();
                string[] textSpl = clipText.Split('\t');
                if (textSpl.Length > 2 + CurrentTable.TableMetadata.Columns.Count)
                {
                    e.Handled = true;
                    return;
                }

                // Verify ID and Label first
                if (!int.TryParse(textSpl[0], out int id))
                {
                    e.Handled = true;
                    return;
                }

                if (CurrentTable.Rows.FirstOrDefault(row => row.ID == id || (row.Label != null && row.Label.Equals(textSpl[1]))) != null)
                {
                    var res = MessageBox.Show(Resource.ID_or_Label_Duplicate_M, Resource.ID_or_Label_Duplicate_T, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes)
                    {
                        e.Handled = true;
                        return;
                    }
                }

                var dbRow = dg_Rows.SelectedItem as RowData;
                dbRow.ID = id;

                // Reorder
                var nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID >= id);

                // Put it to the last of said id if it conflicts
                if (nextRow.ID == id)
                    nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID > id);

                if (nextRow is null) // End of list?
                    CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(dbRow), CurrentTable.Rows.Count - 1);
                else
                {
                    var nextRowIndex = CurrentTable.Rows.IndexOf(nextRow);
                    if (nextRowIndex > CurrentTable.Rows.IndexOf(dbRow)) // If the row is being moved backwards
                        nextRowIndex--;

                    CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(dbRow), nextRowIndex);
                }

                dbRow.Label = textSpl[1].TrimEnd();

                textSpl[textSpl.Length - 1].TrimEnd();
                for (int i = 2; i < textSpl.Length; i++)
                {
                    IDBType colData = dbRow.ColumnData[i - 2];
                    textSpl[i] = textSpl[i].TrimEnd();
                    switch (colData)
                    {
                        case DBByte @byte:
                            if (byte.TryParse(textSpl[i], out byte vByte)) @byte.Value = vByte;
                            break;
                        case DBSByte @sbyte:
                            if (sbyte.TryParse(textSpl[i], out sbyte vSbyte)) @sbyte.Value = vSbyte;
                            break;
                        case DBFloat @float:
                            if (float.TryParse(textSpl[i], out float vFloat)) @float.Value = vFloat;
                            break;
                        case DBInt @int:
                            if (int.TryParse(textSpl[i], out int vInt)) @int.Value = vInt;
                            break;
                        case DBUInt @uint:
                            if (uint.TryParse(textSpl[i], out uint vUInt)) @uint.Value = vUInt;
                            break;
                        case DBLong @long:
                            if (long.TryParse(textSpl[i], out long vLong)) @long.Value = vLong;
                            break;
                        case DBShort @short:
                            if (short.TryParse(textSpl[i], out short vShort)) @short.Value = vShort;
                            break;
                        case DBUShort @ushort:
                            if (ushort.TryParse(textSpl[i], out ushort vUShort)) @ushort.Value = vUShort;
                            break;
                        case DBBool @bool:
                            if (bool.TryParse(textSpl[i], out bool vBool)) @bool.Value = vBool;
                            break;
                        case DBString @str:
                            var strDb = CurrentDatabase.StringDatabases[@str.FileName];
                            @str.StringIndex = strDb.GetOrCreate(textSpl[i]);
                            @str.Value = textSpl[i];
                            break;

                    }
                }
            }
            else if (e.Key == Key.Delete)
            {

            }

        }

        private void dg_Rows_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            dg_cm_CopyCell.IsEnabled = lb_Tables.SelectedIndex != -1 && dg_Rows.SelectedIndex != -1;
            dg_cm_ViewRaceEntries.IsEnabled = lb_Tables.SelectedIndex != -1 && dg_Rows.SelectedIndex != -1 && CurrentTable.TableName == "RACE";
        }

        private void dg_Rows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!dg_Rows.CurrentCell.IsValid)
                return;

            var row = dg_Rows.CurrentCell.Item as RowData;

            tb_CurrentId.Text = row.ID.ToString();
            tb_CurrentLabel.Text = row.Label;
        }

        private void dg_cm_CopyCell_Click(object sender, RoutedEventArgs e)
        {
            if (!dg_Rows.CurrentCell.IsValid)
                return;

            var colIndex = dg_Rows.Columns.IndexOf(dg_Rows.CurrentCell.Column);
            var row = dg_Rows.CurrentCell.Item as RowData;

            string output;
            if (colIndex == 0)
                output = row.ID.ToString();
            else if (colIndex == 1)
                output = row.Label;
            else
            {
                var columnData = row.ColumnData[colIndex - 2];
                if (columnData is DBString strData)
                    output = CurrentDatabase.StringDatabases[strData.FileName].Strings[strData.StringIndex];
                else
                    output = row.ColumnData[colIndex].ToString();

                if (string.IsNullOrEmpty(output))
                    output = "";
                Clipboard.SetText(output);
            }

            if (string.IsNullOrEmpty(output))
                output = "";
            Clipboard.SetText(output);

            if (Properties.Settings.Default.Language == "ja")
                statusName.Text = $"{output}" + "がコピーされました";
            else if (Properties.Settings.Default.Language == "en")
                statusName.Text = $"Copied cell '{output}'";
        }

        private void dg_cm_CopyLabels_Click(object sender, RoutedEventArgs e)
        {
            this.IsEnabled = false;
            CopyLabels sw = new CopyLabels();
            sw.Owner = this;

            sw.TextBoxValueChanged += async (s, val) =>
            {
                this.IsEnabled = false;

                string[] parts = val.Split('|');
                before = parts[0];
                after = parts[1];
                int[] df_pt_ID = new int[11];
                int[] df_pt_ID_FTire_bef = new int[11];
                int[] df_pt_ID_RTire_bef = new int[11];
                int[] df_pt_ID_FTire_aft = new int[11];
                int[] df_pt_ID_RTire_aft = new int[11];
                string[] save_Tables = new string[0];

                foreach (var item in lb_Tables.Items)
                {
                    var table = CurrentDatabase.Tables[(string)item];

                    if (!table.IsLoaded)
                    {
                        progressBar.IsEnabled = true;
                        progressBar.IsIndeterminate = true;
                        try
                        {
                            var loadTask = Task.Run(() => table.LoadAllRows(CurrentDatabase));
                            await loadTask;

                            if (!table.IsTableProperlyMapped)
                                goto label_copys_finish;
                        }
                        catch (Exception ex)
                        {
                            goto label_copys_finish;
                        }
                    }

                    this.IsEnabled = false;
                    if (table.TableName == "DEFAULT_PARTS" || table.TableName == "GENERIC_CAR")
                        goto label_copys_finish;

                    CurrentTable = table;

                    for (int i = cb_FilterColumnType.Items.Count - 1; i >= 2; i--)
                        cb_FilterColumnType.Items.RemoveAt(i);

                    PopulateTableColumns();
                    SetNoProgress_copy();

                    dg_Rows.ItemsSource = CurrentTable.Rows;

                    SetupFilters();
                    ToggleToolbarControls(true);

                    //入力されたLabelを検索ボックスに入力
                    //tb_ColumnFilter.Text = before;

                    loopcount = 1;
                    int[] rows_copied = new int[0];
                    bool save_tables_bool = true;

                    for (int j = 0; j < loopcount; j++)
                    {
                        BeforeRowName = before;
                        NewRowName = after;
                        Table_Labelname.SetRowNamesBasedOnTableName(CurrentTable.TableName, before, after, loopcount,
                            out loopcount, out BeforeRowName, out NewRowName);

                        int beforerows_count = 0;
                        if (CurrentTable.TableName != "VARIATION" && CurrentTable.TableName != "WHEEL")
                        {
                            for (int k = 0; k < CurrentTable.Rows.Count; k++)
                            {
                                if (CurrentTable.Rows[k].Label != BeforeRowName)
                                    beforerows_count++;
                                else
                                    break;
                            }
                        }
                        else
                        {
                            while (true)
                            {
                                int k = 0;
                                int VarOrder = 2147483647;
                                for (k = 0; k < CurrentTable.Rows.Count; k++)
                                {
                                    if (CurrentTable.Rows[k].Label == BeforeRowName)
                                    {
                                        bool k_rows = false;
                                        for (int m = 0; m < rows_copied.Count(); m++)
                                        {
                                            if (k == rows_copied[m])
                                                k_rows = true;
                                        }
                                        if (k_rows == false)
                                        {
                                            int VarOrder_Search = 3;
                                            if (CurrentTable.TableName == "WHEEL")
                                                VarOrder_Search = 4;

                                            // VarOrder取得
                                            var row = dg_Rows.ItemContainerGenerator.ContainerFromIndex(k) as DataGridRow;
                                            if (row == null)
                                            {
                                                dg_Rows.UpdateLayout();
                                                dg_Rows.ScrollIntoView(dg_Rows.Items[k]);
                                                row = dg_Rows.ItemContainerGenerator.ContainerFromIndex(k) as DataGridRow;
                                            }

                                            var cell = dg_Rows.Columns[VarOrder_Search].GetCellContent(row);
                                            if (cell == null)
                                            {
                                                dg_Rows.UpdateLayout();
                                                dg_Rows.ScrollIntoView(dg_Rows.Columns[VarOrder_Search]);
                                                cell = dg_Rows.Columns[VarOrder_Search].GetCellContent(row);
                                            }

                                            /*
                                            var cellt = dg_Rows.Columns[4].GetCellContent(row);
                                            if (cellt == null)
                                            {
                                                dg_Rows.UpdateLayout();
                                                dg_Rows.ScrollIntoView(dg_Rows.Columns[4]);
                                                cellt = dg_Rows.Columns[4].GetCellContent(row);
                                            }
                                            */

                                            var textBlock = cell as TextBlock;
                                            //var textBlockt = cellt as TextBlock;
                                            VarOrder = int.Parse(textBlock.Text);
                                            if (CurrentTable.TableName == "VARIATION")
                                                VarOrder--;
                                        }
                                    }

                                    // カウントと一致したら抜ける
                                    if (VarOrder == j)
                                    {
                                        loopcount++;
                                        beforerows_count = k;
                                        Array.Resize(ref rows_copied, rows_copied.Count() + 1);
                                        rows_copied[j] = beforerows_count;
                                        break;
                                    }

                                    // 一致しない場合は探し直す
                                }

                                if (VarOrder == 2147483647)
                                {
                                    beforerows_count = k;
                                    break;
                                }
                                if (VarOrder == j)
                                    break;
                            }
                        }

                        // 入力されたLabelと完全一致したらコピーする
                        if (CurrentTable.Rows.Count != beforerows_count)
                        {
                            // カーソルを合わせる処理
                            dg_Rows.Focus();
                            DataGridCellInfo cellInfo = new DataGridCellInfo(dg_Rows.Items[beforerows_count], dg_Rows.Columns[1]);
                            dg_Rows.CurrentCell = cellInfo;
                            dg_Rows.SelectedIndex = beforerows_count;

                            if (dg_Rows.SelectedIndex == -1 || !dg_Rows.CurrentCell.IsValid)
                                return;

                            var selectedRow = dg_Rows.CurrentCell.Item as RowData;

                            if (CurrentTable.TableName == "FRONTTIRE")
                                df_pt_ID_FTire_bef[j] = CurrentTable.Rows[beforerows_count].ID;
                            if (CurrentTable.TableName == "REARTIRE")
                                df_pt_ID_RTire_bef[j] = CurrentTable.Rows[beforerows_count].ID;

                            var newRow = new RowData();
                            int newRow_ID = ++CurrentTable.LastID;

                            int newRow_ID_set = 0;
                            switch (CurrentTable.TableName)
                            {
                                case "CAR_CUSTOM_INFO":
                                case "CAR_NAME_ALPHABET":
                                case "CAR_NAME_american":
                                case "CAR_NAME_big5":
                                case "CAR_NAME_british":
                                case "CAR_NAME_french":
                                case "CAR_NAME_german":
                                case "CAR_NAME_italian":
                                case "CAR_NAME_JAPAN":
                                case "CAR_NAME_japanese":
                                case "CAR_NAME_korean":
                                case "CAR_NAME_spanish":
                                case "VARIATION":
                                case "WHEEL":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_1; break;
                                case "AIR_CLEANER":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_2; break;
                                case "BRAKE":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_3; break;
                                case "CATALYST":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_4; break;
                                case "CHASSIS":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_5; break;
                                case "CLUTCH":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_6; break;
                                case "COMPUTER":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_7; break;
                                case "DRIVETRAIN":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_8; break;
                                case "ENGINE":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_9; break;
                                case "EXHAUST_MANIFOLD":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_10; break;
                                case "FLYWHEEL":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_11; break;
                                case "FRONTTIRE":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_12; break;
                                case "GEAR":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_13; break;
                                case "INTAKE_MANIFOLD":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_14; break;
                                case "LIGHTWEIGHT":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_15; break;
                                case "LSD":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_16; break;
                                case "MUFFLER":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_17; break;
                                case "NATUNE":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_18; break;
                                case "PROPELLERSHAFT":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_19; break;
                                case "RACINGMODIFY":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_20; break;
                                case "REARTIRE":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_21; break;
                                case "STEER":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_22; break;
                                case "SUPERCHARGER":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_23; break;
                                case "SUSPENSION":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_24; break;
                                case "TURBINEKIT":
                                    newRow_ID_set = Properties.Settings.Default.Multi_Copy_ID_25; break;
                                default: break;
                            }

                            if (newRow_ID_set != 0)
                            {
                                for (int k = 0; k < CurrentTable.Rows.Count; k++)
                                {
                                    if (CurrentTable.Rows[k].ID < newRow_ID_set)
                                    {
                                        newRow_ID = CurrentTable.Rows[k].ID;
                                        newRow_ID++;
                                    }
                                    else
                                        break;
                                }
                            }

                            newRow.ID = newRow_ID;

                            if ((CurrentTable.TableName == "VARIATION" && j > 0) || (CurrentTable.TableName == "WHEEL" && j > 0))
                                newRow.ID--;

                            newRow.Label = NewRowName;
                            CurrentTable.Rows.Add(newRow);

                            for (int i = 0; i < CurrentTable.TableMetadata.Columns.Count; i++)
                            {
                                ColumnMetadata colMeta = CurrentTable.TableMetadata.Columns[i];
                                switch (colMeta.ColumnType)
                                {
                                    case DBColumnType.Bool:
                                        newRow.ColumnData.Add(new DBBool(((DBBool)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.Byte:
                                        newRow.ColumnData.Add(new DBByte(((DBByte)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.SByte:
                                        newRow.ColumnData.Add(new DBSByte(((DBSByte)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.Short:
                                        newRow.ColumnData.Add(new DBShort(((DBShort)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.UShort:
                                        newRow.ColumnData.Add(new DBUShort(((DBUShort)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.Int:
                                        newRow.ColumnData.Add(new DBInt(((DBInt)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.UInt:
                                        newRow.ColumnData.Add(new DBUInt(((DBUInt)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.Float:
                                        newRow.ColumnData.Add(new DBFloat(((DBFloat)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.Long:
                                        newRow.ColumnData.Add(new DBLong(((DBLong)selectedRow.ColumnData[i]).Value)); break;
                                    case DBColumnType.String:
                                        var str = new DBString(((DBString)selectedRow.ColumnData[i]).StringIndex, colMeta.StringFileName);
                                        str.Value = CurrentDatabase.StringDatabases[colMeta.StringFileName].Strings[str.StringIndex];
                                        newRow.ColumnData.Add(str);
                                        break;
                                    default:
                                        break;
                                }
                            }

                            dg_Rows.ScrollIntoView(newRow);
                            copycount += 1;

                            if (j == 0)
                            {
                                switch (CurrentTable.TableName)
                                {
                                    case "BRAKE":
                                        df_pt_ID[0] = newRow.ID; break;
                                    case "SUSPENSION":
                                        df_pt_ID[1] = newRow.ID; break;
                                    case "CHASSIS":
                                        df_pt_ID[2] = newRow.ID; break;
                                    case "RACINGMODIFY":
                                        df_pt_ID[3] = newRow.ID; break;
                                    case "STEER":
                                        df_pt_ID[4] = newRow.ID; break;
                                    case "DRIVETRAIN":
                                        df_pt_ID[5] = newRow.ID; break;
                                    case "GEAR":
                                        df_pt_ID[6] = newRow.ID; break;
                                    case "ENGINE":
                                        df_pt_ID[7] = newRow.ID; break;
                                    case "TURBINEKIT":
                                        df_pt_ID[8] = newRow.ID; break;
                                    case "MUFFLER":
                                        df_pt_ID[9] = newRow.ID; break;
                                    case "LSD":
                                        df_pt_ID[10] = newRow.ID; break;
                                    default: break;
                                }
                            }
                            if (CurrentTable.TableName == "FRONTTIRE")
                                df_pt_ID_FTire_aft[j] = newRow.ID;
                            if (CurrentTable.TableName == "REARTIRE")
                                df_pt_ID_RTire_aft[j] = newRow.ID;

                            if (newRow_ID != CurrentTable.LastID)
                            {
                                string newInput = newRow.ID.ToString();
                                if (int.TryParse(newInput, out int newValue))
                                {
                                    var nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID >= newValue);
                                    newRow.ID = newValue;

                                    // Put it to the last of said id if it conflicts
                                    if (nextRow != null && nextRow.ID == newValue)
                                        nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID > newValue);

                                    if (nextRow is null) // End of list?
                                    {
                                        CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(newRow), CurrentTable.Rows.Count - 1);
                                        return;
                                    }

                                    var nextRowIndex = CurrentTable.Rows.IndexOf(nextRow);
                                    if (nextRowIndex > CurrentTable.Rows.IndexOf(newRow)) // If the row is being moved backwards
                                        nextRowIndex--;

                                    CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(newRow), nextRowIndex);
                                    dg_Rows.ScrollIntoView(newRow);
                                }
                            }
                            
                            if (save_tables_bool == true)
                            {
                                Array.Resize(ref save_Tables, save_Tables.Length + 1);
                                save_Tables[save_Tables.Length - 1] = CurrentTable.TableName;
                                save_tables_bool = false;
                            }
                        }
                    }

                label_copys_finish:;
                }
                loopcount = 1;

                // DEFAULT_PARTS
                int newRow_ID_df = 0;
                var df_table = CurrentDatabase.Tables["DEFAULT_PARTS"];

                if (!df_table.IsLoaded)
                {
                    progressBar.IsEnabled = true;
                    progressBar.IsIndeterminate = true;
                    try
                    {
                        var loadTask = Task.Run(() => df_table.LoadAllRows(CurrentDatabase));
                        await loadTask;

                        //if (!df_table.IsTableProperlyMapped)
                        //    goto label_copys_finish;
                    }
                    catch (Exception ex)
                    {
                        //goto label_copys_finish;
                    }
                }
                this.IsEnabled = false;

                CurrentTable = df_table;

                for (int i = cb_FilterColumnType.Items.Count - 1; i >= 2; i--)
                    cb_FilterColumnType.Items.RemoveAt(i);

                PopulateTableColumns();
                SetNoProgress_copy();

                dg_Rows.ItemsSource = CurrentTable.Rows;

                SetupFilters();
                ToggleToolbarControls(true);

                dg_Rows.Focus();

                BeforeRowName = before;
                NewRowName = after;
                Table_Labelname.SetRowNamesBasedOnTableName(CurrentTable.TableName, before, after, loopcount,
                    out loopcount, out BeforeRowName, out NewRowName);

                int beforerows_count_df = 0;
                for (int k = 0; k < CurrentTable.Rows.Count; k++)
                {
                    if (CurrentTable.Rows[k].Label != BeforeRowName)
                        beforerows_count_df++;
                    else
                        break;
                }

                int[] df_Tire_bef = new int[2] { 47, 49 };

                for (int k = 0; k < 2; k++)
                {
                    // VarOrder取得
                    var row = dg_Rows.ItemContainerGenerator.ContainerFromIndex(beforerows_count_df) as DataGridRow;
                    if (row == null)
                    {
                        dg_Rows.UpdateLayout();
                        dg_Rows.ScrollIntoView(dg_Rows.Items[beforerows_count_df]);
                        row = dg_Rows.ItemContainerGenerator.ContainerFromIndex(beforerows_count_df) as DataGridRow;
                    }

                    var cell = dg_Rows.Columns[df_Tire_bef[k]].GetCellContent(row);
                    if (cell == null)
                    {
                        dg_Rows.UpdateLayout();
                        dg_Rows.ScrollIntoView(dg_Rows.Columns[df_Tire_bef[k]]);
                        cell = dg_Rows.Columns[df_Tire_bef[k]].GetCellContent(row);
                    }

                    var textBlock = cell as TextBlock;
                    df_Tire_bef[k] = int.Parse(textBlock.Text);
                }

                if (CurrentTable.Rows.Count != beforerows_count_df)
                {
                    // カーソルを合わせる処理
                    dg_Rows.Focus();
                    DataGridCellInfo cellInfo = new DataGridCellInfo(dg_Rows.Items[beforerows_count_df], dg_Rows.Columns[1]);
                    dg_Rows.CurrentCell = cellInfo;
                    dg_Rows.SelectedIndex = beforerows_count_df;

                    if (dg_Rows.SelectedIndex == -1 || !dg_Rows.CurrentCell.IsValid)
                        return;

                    var selectedRow = dg_Rows.CurrentCell.Item as RowData;

                    var newRow = new RowData();
                    newRow_ID_df = ++CurrentTable.LastID;
                    if (Properties.Settings.Default.Multi_Copy_ID_26 != 0)
                    {
                        for (int k = 0; k < CurrentTable.Rows.Count; k++)
                        {
                            if (CurrentTable.Rows[k].ID < Properties.Settings.Default.Multi_Copy_ID_26)
                            {
                                newRow_ID_df = CurrentTable.Rows[k].ID;
                                newRow_ID_df++;
                            }
                            else
                                break;
                        }
                    }
                    newRow.ID = newRow_ID_df;

                    newRow.Label = NewRowName;
                    CurrentTable.Rows.Add(newRow);

                    int df_pt_ID_count = 0;

                    for (int i = 0; i < CurrentTable.TableMetadata.Columns.Count; i++)
                    {
                        ColumnMetadata colMeta = CurrentTable.TableMetadata.Columns[i];
                        bool df_pt_ID_input = false;
                        if (i == 1 || i == 5 || i == 11 || i == 13 || i == 17 || i == 19 || i == 21 || i == 23 || i == 27 || i == 35 || i == 43 || i == 45 || i == 47)
                        {
                            if (i == 45)
                            {
                                int Tire_aft_count = 2147483647;
                                for (int m = 0; m < df_pt_ID_FTire_bef.Length; m++)
                                {
                                    if (df_Tire_bef[0] == df_pt_ID_FTire_bef[m])
                                        Tire_aft_count = m;
                                }
                                if (Tire_aft_count != 2147483647)
                                {
                                    switch (colMeta.ColumnType)
                                    {
                                        case DBColumnType.Byte:
                                            newRow.ColumnData.Add(new DBByte((byte)df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.SByte:
                                            newRow.ColumnData.Add(new DBSByte((sbyte)df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Short:
                                            newRow.ColumnData.Add(new DBShort((short)df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.UShort:
                                            newRow.ColumnData.Add(new DBUShort((ushort)df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Int:
                                            newRow.ColumnData.Add(new DBInt(df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.UInt:
                                            newRow.ColumnData.Add(new DBUInt((uint)df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Float:
                                            newRow.ColumnData.Add(new DBFloat(df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Long:
                                            newRow.ColumnData.Add(new DBLong(df_pt_ID_FTire_aft[Tire_aft_count])); break;
                                        default:
                                            break;
                                    }
                                    df_pt_ID_input = true;
                                }
                            }
                            else if (i == 47)
                            {
                                int Tire_aft_count = 2147483647;
                                for (int m = 0; m < df_pt_ID_RTire_bef.Length; m++)
                                {
                                    if (df_Tire_bef[1] == df_pt_ID_RTire_bef[m])
                                        Tire_aft_count = m;
                                }
                                if (Tire_aft_count != 2147483647)
                                {
                                    switch (colMeta.ColumnType)
                                    {
                                        case DBColumnType.Byte:
                                            newRow.ColumnData.Add(new DBByte((byte)df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.SByte:
                                            newRow.ColumnData.Add(new DBSByte((sbyte)df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Short:
                                            newRow.ColumnData.Add(new DBShort((short)df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.UShort:
                                            newRow.ColumnData.Add(new DBUShort((ushort)df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Int:
                                            newRow.ColumnData.Add(new DBInt(df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.UInt:
                                            newRow.ColumnData.Add(new DBUInt((uint)df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Float:
                                            newRow.ColumnData.Add(new DBFloat(df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        case DBColumnType.Long:
                                            newRow.ColumnData.Add(new DBLong(df_pt_ID_RTire_aft[Tire_aft_count])); break;
                                        default:
                                            break;
                                    }
                                    df_pt_ID_input = true;
                                }
                            }
                            else if (df_pt_ID[df_pt_ID_count] > 0)
                            {
                                switch (colMeta.ColumnType)
                                {
                                    case DBColumnType.Byte:
                                        newRow.ColumnData.Add(new DBByte((byte)df_pt_ID[df_pt_ID_count])); break;
                                    case DBColumnType.SByte:
                                        newRow.ColumnData.Add(new DBSByte((sbyte)df_pt_ID[df_pt_ID_count])); break;
                                    case DBColumnType.Short:
                                        newRow.ColumnData.Add(new DBShort((short)df_pt_ID[df_pt_ID_count])); break;
                                    case DBColumnType.UShort:
                                        newRow.ColumnData.Add(new DBUShort((ushort)df_pt_ID[df_pt_ID_count])); break;
                                    case DBColumnType.Int:
                                        newRow.ColumnData.Add(new DBInt(df_pt_ID[df_pt_ID_count])); break;
                                    case DBColumnType.UInt:
                                        newRow.ColumnData.Add(new DBUInt((uint)df_pt_ID[df_pt_ID_count])); break;
                                    case DBColumnType.Float:
                                        newRow.ColumnData.Add(new DBFloat(df_pt_ID[df_pt_ID_count])); break;
                                    case DBColumnType.Long:
                                        newRow.ColumnData.Add(new DBLong(df_pt_ID[df_pt_ID_count])); break;
                                    default:
                                        break;
                                }
                                df_pt_ID_input = true;
                            }
                            df_pt_ID_count++;
                        }
                        if (df_pt_ID_input == false)
                        {
                            switch (colMeta.ColumnType)
                            {
                                case DBColumnType.Bool:
                                    newRow.ColumnData.Add(new DBBool(((DBBool)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Byte:
                                    newRow.ColumnData.Add(new DBByte(((DBByte)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.SByte:
                                    newRow.ColumnData.Add(new DBSByte(((DBSByte)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Short:
                                    newRow.ColumnData.Add(new DBShort(((DBShort)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.UShort:
                                    newRow.ColumnData.Add(new DBUShort(((DBUShort)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Int:
                                    newRow.ColumnData.Add(new DBInt(((DBInt)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.UInt:
                                    newRow.ColumnData.Add(new DBUInt(((DBUInt)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Float:
                                    newRow.ColumnData.Add(new DBFloat(((DBFloat)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Long:
                                    newRow.ColumnData.Add(new DBLong(((DBLong)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.String:
                                    var str = new DBString(((DBString)selectedRow.ColumnData[i]).StringIndex, colMeta.StringFileName);
                                    str.Value = CurrentDatabase.StringDatabases[colMeta.StringFileName].Strings[str.StringIndex];
                                    newRow.ColumnData.Add(str);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    if (newRow_ID_df != CurrentTable.LastID)
                    {
                        string newInput = newRow.ID.ToString();
                        if (int.TryParse(newInput, out int newValue))
                        {
                            var nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID >= newValue);
                            newRow.ID = newValue;

                            // Put it to the last of said id if it conflicts
                            if (nextRow != null && nextRow.ID == newValue)
                                nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID > newValue);

                            if (nextRow is null) // End of list?
                            {
                                CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(newRow), CurrentTable.Rows.Count - 1);
                                return;
                            }

                            var nextRowIndex = CurrentTable.Rows.IndexOf(nextRow);
                            if (nextRowIndex > CurrentTable.Rows.IndexOf(newRow)) // If the row is being moved backwards
                                nextRowIndex--;

                            CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(newRow), nextRowIndex);
                            dg_Rows.ScrollIntoView(newRow);
                        }
                    }

                    dg_Rows.ScrollIntoView(newRow);
                }

                // GENERIC_CAR
                var ge_table = CurrentDatabase.Tables["GENERIC_CAR"];

                if (!ge_table.IsLoaded)
                {
                    progressBar.IsEnabled = true;
                    progressBar.IsIndeterminate = true;
                    try
                    {
                        var loadTask = Task.Run(() => ge_table.LoadAllRows(CurrentDatabase));
                        await loadTask;

                        //if (!ge_table.IsTableProperlyMapped)
                        //    goto label_copys_finish;
                    }
                    catch (Exception ex)
                    {
                        //goto label_copys_finish;
                    }
                }
                this.IsEnabled = false;

                CurrentTable = ge_table;

                for (int i = cb_FilterColumnType.Items.Count - 1; i >= 2; i--)
                    cb_FilterColumnType.Items.RemoveAt(i);

                PopulateTableColumns();
                SetNoProgress_copy();

                dg_Rows.ItemsSource = CurrentTable.Rows;

                SetupFilters();
                ToggleToolbarControls(true);

                dg_Rows.Focus();

                BeforeRowName = before;
                NewRowName = after;
                Table_Labelname.SetRowNamesBasedOnTableName(CurrentTable.TableName, before, after, loopcount,
                    out loopcount, out BeforeRowName, out NewRowName);

                int beforerows_count_ge = 0;
                for (int k = 0; k < CurrentTable.Rows.Count; k++)
                {
                    if (CurrentTable.Rows[k].Label != BeforeRowName)
                        beforerows_count_ge++;
                    else
                        break;
                }

                if (CurrentTable.Rows.Count != beforerows_count_ge)
                {
                    // カーソルを合わせる処理
                    dg_Rows.Focus();
                    DataGridCellInfo cellInfo = new DataGridCellInfo(dg_Rows.Items[beforerows_count_ge], dg_Rows.Columns[1]);
                    dg_Rows.CurrentCell = cellInfo;
                    dg_Rows.SelectedIndex = beforerows_count_ge;

                    if (dg_Rows.SelectedIndex == -1 || !dg_Rows.CurrentCell.IsValid)
                        return;

                    var selectedRow = dg_Rows.CurrentCell.Item as RowData;

                    var newRow = new RowData();
                    int newRow_ID_ge = ++CurrentTable.LastID;
                    if (Properties.Settings.Default.Multi_Copy_ID_1 != 0)
                    {
                        for (int k = 0; k < CurrentTable.Rows.Count; k++)
                        {
                            if (CurrentTable.Rows[k].ID < Properties.Settings.Default.Multi_Copy_ID_1)
                            {
                                newRow_ID_ge = CurrentTable.Rows[k].ID;
                                newRow_ID_ge++;
                            }
                            else
                                break;
                        }
                    }
                    newRow.ID = newRow_ID_ge;

                    newRow.Label = NewRowName;
                    CurrentTable.Rows.Add(newRow);

                    for (int i = 0; i < CurrentTable.TableMetadata.Columns.Count; i++)
                    {
                        ColumnMetadata colMeta = CurrentTable.TableMetadata.Columns[i];
                        bool df_pt_ID_input = false;
                        if (i == 1)
                        {
                            switch (colMeta.ColumnType)
                            {
                                case DBColumnType.Byte:
                                    newRow.ColumnData.Add(new DBByte((byte)newRow_ID_df)); break;
                                case DBColumnType.SByte:
                                    newRow.ColumnData.Add(new DBSByte((sbyte)newRow_ID_df)); break;
                                case DBColumnType.Short:
                                    newRow.ColumnData.Add(new DBShort((short)newRow_ID_df)); break;
                                case DBColumnType.UShort:
                                    newRow.ColumnData.Add(new DBUShort((ushort)newRow_ID_df)); break;
                                case DBColumnType.Int:
                                    newRow.ColumnData.Add(new DBInt(newRow_ID_df)); break;
                                case DBColumnType.UInt:
                                    newRow.ColumnData.Add(new DBUInt((uint)newRow_ID_df)); break;
                                case DBColumnType.Float:
                                    newRow.ColumnData.Add(new DBFloat(newRow_ID_df)); break;
                                case DBColumnType.Long:
                                    newRow.ColumnData.Add(new DBLong(newRow_ID_df)); break;
                                default:
                                    break;
                            }
                            df_pt_ID_input = true;
                        }
                        if (df_pt_ID_input == false)
                        {
                            switch (colMeta.ColumnType)
                            {
                                case DBColumnType.Bool:
                                    newRow.ColumnData.Add(new DBBool(((DBBool)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Byte:
                                    newRow.ColumnData.Add(new DBByte(((DBByte)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.SByte:
                                    newRow.ColumnData.Add(new DBSByte(((DBSByte)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Short:
                                    newRow.ColumnData.Add(new DBShort(((DBShort)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.UShort:
                                    newRow.ColumnData.Add(new DBUShort(((DBUShort)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Int:
                                    newRow.ColumnData.Add(new DBInt(((DBInt)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.UInt:
                                    newRow.ColumnData.Add(new DBUInt(((DBUInt)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Float:
                                    newRow.ColumnData.Add(new DBFloat(((DBFloat)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.Long:
                                    newRow.ColumnData.Add(new DBLong(((DBLong)selectedRow.ColumnData[i]).Value)); break;
                                case DBColumnType.String:
                                    var str = new DBString(((DBString)selectedRow.ColumnData[i]).StringIndex, colMeta.StringFileName);
                                    str.Value = CurrentDatabase.StringDatabases[colMeta.StringFileName].Strings[str.StringIndex];
                                    newRow.ColumnData.Add(str);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    if (newRow_ID_ge != CurrentTable.LastID)
                    {
                        string newInput = newRow.ID.ToString();
                        if (int.TryParse(newInput, out int newValue))
                        {
                            var nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID >= newValue);
                            newRow.ID = newValue;

                            // Put it to the last of said id if it conflicts
                            if (nextRow != null && nextRow.ID == newValue)
                                nextRow = CurrentTable.Rows.FirstOrDefault(r => r.ID > newValue);

                            if (nextRow is null) // End of list?
                            {
                                CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(newRow), CurrentTable.Rows.Count - 1);
                                return;
                            }

                            var nextRowIndex = CurrentTable.Rows.IndexOf(nextRow);
                            if (nextRowIndex > CurrentTable.Rows.IndexOf(newRow)) // If the row is being moved backwards
                                nextRowIndex--;

                            CurrentTable.Rows.Move(CurrentTable.Rows.IndexOf(newRow), nextRowIndex);
                            dg_Rows.ScrollIntoView(newRow);
                        }
                    }

                    dg_Rows.ScrollIntoView(newRow);
                }

                for (int m = 0; m < save_Tables.Length; m++)
                {
                    var table = CurrentDatabase.Tables[save_Tables[m]];

                    if (!table.IsLoaded)
                    {
                        progressBar.IsEnabled = true;
                        progressBar.IsIndeterminate = true;
                        try
                        {
                            var loadTask = Task.Run(() => table.LoadAllRows(CurrentDatabase));
                            await loadTask;

                            if (!table.IsTableProperlyMapped)
                                goto label_copys_finish2;
                        }
                        catch (Exception ex)
                        {
                            goto label_copys_finish2;
                        }
                    }
                    this.IsEnabled = false;

                    CurrentTable = table;

                    for (int i = cb_FilterColumnType.Items.Count - 1; i >= 2; i--)
                        cb_FilterColumnType.Items.RemoveAt(i);

                    PopulateTableColumns();
                    SetNoProgress_copy();

                    dg_Rows.ItemsSource = CurrentTable.Rows;

                    SetupFilters();
                    ToggleToolbarControls(true);

                    dg_Rows.Focus();
                    CurrentTable.SaveTable(CurrentDatabase, SpecDBDirectory);

                label_copys_finish2:;
                }

                var df_table_save = CurrentDatabase.Tables["DEFAULT_PARTS"];

                if (!df_table_save.IsLoaded)
                {
                    progressBar.IsEnabled = true;
                    progressBar.IsIndeterminate = true;
                    try
                    {
                        var loadTask = Task.Run(() => df_table_save.LoadAllRows(CurrentDatabase));
                        await loadTask;

                        if (!df_table_save.IsTableProperlyMapped)
                            goto label_copys_finish3;
                    }
                    catch (Exception ex)
                    {
                        goto label_copys_finish3;
                    }
                }
                this.IsEnabled = false;

                CurrentTable = df_table_save;

                for (int i = cb_FilterColumnType.Items.Count - 1; i >= 2; i--)
                    cb_FilterColumnType.Items.RemoveAt(i);

                PopulateTableColumns();
                SetNoProgress_copy();

                dg_Rows.ItemsSource = CurrentTable.Rows;

                SetupFilters();
                ToggleToolbarControls(true);

                dg_Rows.Focus();
                CurrentTable.SaveTable(CurrentDatabase, SpecDBDirectory);

            label_copys_finish3:;

                var ge_table_save = CurrentDatabase.Tables["GENERIC_CAR"];

                if (!ge_table_save.IsLoaded)
                {
                    progressBar.IsEnabled = true;
                    progressBar.IsIndeterminate = true;
                    try
                    {
                        var loadTask = Task.Run(() => ge_table_save.LoadAllRows(CurrentDatabase));
                        await loadTask;

                        if (!ge_table_save.IsTableProperlyMapped)
                            goto label_copys_finish4;
                    }
                    catch (Exception ex)
                    {
                        goto label_copys_finish4;
                    }
                }
                this.IsEnabled = false;

                CurrentTable = ge_table_save;

                for (int i = cb_FilterColumnType.Items.Count - 1; i >= 2; i--)
                    cb_FilterColumnType.Items.RemoveAt(i);

                PopulateTableColumns();
                SetNoProgress_copy();

                dg_Rows.ItemsSource = CurrentTable.Rows;

                SetupFilters();

                mi_SaveTable.IsEnabled = true;
                mi_ExportTable.IsEnabled = true;
                mi_ExportTableCSV.IsEnabled = true;

                ToggleToolbarControls(true);

                dg_Rows.Focus();
                CurrentTable.SaveTable(CurrentDatabase, SpecDBDirectory);

            label_copys_finish4:;

                if (Properties.Settings.Default.Language == "ja")
                    statusName.Text = $"コピーが完了しました";
                else if (Properties.Settings.Default.Language == "en")
                    statusName.Text = "Copying is complete.";

                this.IsEnabled = true;
            };
            sw.ShowDialog();
        }

        private void dg_cm_ViewRaceEntries_Click(object sender, RoutedEventArgs e)
        {
            int tableId = CurrentTable.TableID;

            ;
        }
        #endregion

        #region Table Listing
        private async void lb_Tables_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (lb_Tables.SelectedIndex == -1)
                return;

            // Ensure to cancel the edit to properly allow filtering reset
            dg_Rows.CancelEdit();
            dg_Rows.CancelEdit();

            var table = CurrentDatabase.Tables[(string)lb_Tables.SelectedItem];

            if (!table.IsLoaded)
            {
                if (Properties.Settings.Default.Language == "ja")
                    statusName.Text = $"{table.TableName}" + "をロード中...";
                else if (Properties.Settings.Default.Language == "en")
                    statusName.Text = "Loading " + $"{table.TableName}" + "...";
                progressBar.IsEnabled = true;
                progressBar.IsIndeterminate = true;
                try
                {
                    var loadTask = Task.Run(() => table.LoadAllRows(CurrentDatabase));
                    await loadTask;

                    if (!table.IsTableProperlyMapped)
                        MessageBox.Show(Resource.IsTableProperlyMapped_M, Resource.IsTableProperlyMapped_T, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Resource.Load_Failed_Table_M + $"{ex.Message}", Resource.Load_Failed_Table_T, MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetNoProgress();
                    return;
                }
            }

            CurrentTable = table;

            for (int i = cb_FilterColumnType.Items.Count - 1; i >= 2; i--)
                cb_FilterColumnType.Items.RemoveAt(i);

            PopulateTableColumns();
            SetNoProgress();

            dg_Rows.ItemsSource = CurrentTable.Rows;

            SetupFilters();

            mi_SaveTable.IsEnabled = true;
            mi_ExportTable.IsEnabled = true;
            mi_ExportTableCSV.IsEnabled = true;

            ToggleToolbarControls(true);

            if (Properties.Settings.Default.Language == "ja")
                statusName.Text = $"{CurrentTable.Rows.Count}行の{table.TableName}を読み込みました";
            else if (Properties.Settings.Default.Language == "en")
                statusName.Text = $"Loaded '{table.TableName}' with {CurrentTable.Rows.Count} rows.";
        }

        private void cm_DumpTable_Click(object sender, RoutedEventArgs e)
        {
            if (lb_Tables.SelectedIndex == -1)
                return;

            var dlg = new SaveFileDialog();
            if (dlg.ShowDialog() == true)
            {
                var table = CurrentDatabase.Tables[(string)lb_Tables.SelectedItem];
                int rows = table.DumpTable(dlg.FileName);

                if (Properties.Settings.Default.Language == "ja")
                    MessageBox.Show($"{dlg.FileName}に{rows}行のテーブルをダンプしました", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                else if (Properties.Settings.Default.Language == "en")
                    MessageBox.Show($"Dumped table with {rows} rows at {dlg.FileName}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void cm_DumpDebugTable_Click(object sender, RoutedEventArgs e)
        {
            if (lb_Tables.SelectedIndex == -1)
                return;

            var debug = new SpecDBDebugPrinter();
            debug.Load(Path.Combine(CurrentDatabase.FolderName, CurrentTable.TableName) + ".dbt");
            debug.Print();
        }

        private void lb_Tables_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (lb_Tables.SelectedIndex == -1)
                return;

            var table = CurrentDatabase.Tables[(string)lb_Tables.SelectedItem];
            cm_TableIndex.Header = Resource.cm_TableIndex_Header + $"{table.IDI.TableIndex}";
        }
        #endregion

        #region Non-Events
        public void SetupFilters()
        {
            foreach (var col in CurrentTable.TableMetadata.Columns)
                cb_FilterColumnType.Items.Add(col.ColumnName);

            if (cb_FilterColumnType.SelectedIndex == -1)
            {
                tb_ColumnFilter.Text = _filterString = string.Empty;
                cb_FilterColumnType.SelectedIndex = 1; // Reset to label
            }

            _dataGridCollection = CollectionViewSource.GetDefaultView(dg_Rows.ItemsSource);
            if (_dataGridCollection != null)
                _dataGridCollection.Filter = FilterTask;
                
        }

        public bool FilterTask(object value)
        {
            if (string.IsNullOrEmpty(FilterString) || FilterString.Length < 3)
                return true;

            if (value is RowData row && row.ColumnData.Count != 0)
            {

                if (cb_FilterColumnType.SelectedIndex == 0)
                    return row.ID.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                else if (cb_FilterColumnType.SelectedIndex == 1)
                    return row.Label.Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                else
                {
                    var colData = row.ColumnData[cb_FilterColumnType.SelectedIndex - 2];
                    switch (colData)
                    {
                        case DBByte @byte:
                            return @byte.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBSByte @sbyte:
                            return @sbyte.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBFloat @float:
                            return @float.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBInt @int:
                            return @int.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBUInt @uint:
                            return @uint.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBLong @long:
                            return @long.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBShort @short:
                            return @short.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBUShort @ushort:
                            return @ushort.Value.ToString().Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                        case DBString @str:
                            return @str.Value.Contains(FilterString, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            return false;
        }

        private void PopulateTableColumns()
        {
            dg_Rows.ItemsSource = null;
            for (int i = dg_Rows.Columns.Count - 1; i >= 2; i--)
                dg_Rows.Columns.Remove(dg_Rows.Columns[i]);

            for (int i = 0; i < CurrentTable.TableMetadata.Columns.Count; i++)
            {
                ColumnMetadata column = CurrentTable.TableMetadata.Columns[i];
                var style = new Style(typeof(DataGridColumnHeader));
                style.Setters.Add(new Setter(ToolTipService.ToolTipProperty, $"Type: {column.ColumnType}"));

                if (column.ColumnType == DBColumnType.Bool)
                {
                    dg_Rows.Columns.Add(new DataGridCheckBoxColumn
                    {
                        HeaderStyle = style,
                        Header = column.ColumnName,
                        Binding = new Binding($"ColumnData[{i}].Value"),
                    });
                }
                else
                {
                    dg_Rows.Columns.Add(new DataGridTextColumn
                    {
                        HeaderStyle = style,
                        Header = column.ColumnName,
                        Binding = new Binding($"ColumnData[{i}].Value"),
                        IsReadOnly = column.ColumnType == DBColumnType.String,
                    });
                }
            }
        }

        private void SetNoProgress()
        {
            if (Properties.Settings.Default.Language == "ja")
                statusName.Text = $"準備完了";
            else if (Properties.Settings.Default.Language == "en")
                statusName.Text = $"Ready";
            progressBar.IsEnabled = false;
            progressBar.IsIndeterminate = false;
        }

        private void SetNoProgress_copy()
        {
            if (Properties.Settings.Default.Language == "ja")
                statusName.Text = $"コピー中...";
            else if (Properties.Settings.Default.Language == "en")
                statusName.Text = $"Copying...";
            progressBar.IsEnabled = false;
            progressBar.IsIndeterminate = false;
        }

        private void ToggleToolbarControls(bool enabled)
        {
            cb_FilterColumnType.IsEnabled = enabled;
            btn_AddRow.IsEnabled = enabled;
            btn_DeleteRow.IsEnabled = enabled;
            btn_CopyRow.IsEnabled = enabled;
            tb_ColumnFilter.IsEnabled = enabled;

            btn_SaveAs.IsEnabled = enabled;
            btn_Save.IsEnabled = enabled;
        }

        private void ProcessNewlyLoadedSpecDB()
        {
            CurrentTable = null;
            dg_Rows.ItemsSource = null;
            tb_ColumnFilter.Text = "";
            FilterString = "";

            mi_SavePartsInfo.IsEnabled = CurrentDatabase.SpecDBFolderType >= SpecDBFolder.GT5_JP3009;
            mi_SaveCarsParts.IsEnabled = CurrentDatabase.SpecDBFolderType <= SpecDBFolder.GT5_TRIAL_JP2704;

            mi_ExportTableSQLite.IsEnabled = true;
            mi_cm_CopyLabels.IsEnabled = true;
            lb_Tables.Items.Clear();

            foreach (var table in CurrentDatabase.Tables)
                lb_Tables.Items.Add($"{table.Key}");

            string Window_Title = $"[{CurrentDatabase.SpecDBFolderType.Humanize()}]";
            if (Properties.Settings.Default.Language == "ja")
            {
                Window_Title = Window_Title.Replace("[", "【");
                Window_Title = Window_Title.Replace("]", "】");
                Window_Title = Window_Title.Replace("Gran Turismo ", "グランツーリスモ");
                Window_Title = Window_Title.Replace("Prologue", "プロローグ");
                Window_Title = Window_Title.Replace("Special Edition 2004 Geneva Version", "スペシャルエディション 2004 ジュネーブバージョン");
                Window_Title = Window_Title.Replace("/Toyota ", " / トヨタ");
                Window_Title = Window_Title.Replace("Demo", "デモ");
                Window_Title = Window_Title.Replace("Pre-Release", "プレリリース");
                Window_Title = Window_Title.Replace("Online Test Version", "オンライン実験バージョン");
                Window_Title = Window_Title.Replace("Tourist Trophy", "ツーリストトロフィー");
                Window_Title = Window_Title.Replace(" Concept", "コンセプト");
                Window_Title = Window_Title.Replace("Time Trial Challenge", "タイムトライアルチャレンジ");
                Window_Title = Window_Title.Replace("Kiosk ", "キオスク");
                Window_Title = Window_Title.Replace("Default DB", "デフォルトDB");
                Window_Title = Window_Title.Replace("Preview DB", "プレビューDB");
            }
            this.Title = $"{WindowTitle} - {CurrentDatabase.SpecDBFolderType} " + Window_Title;

            ToggleToolbarControls(false);
        }

        #endregion

        private void Language_Japanese_Click(object sender, RoutedEventArgs e)
        {
            MenuItem_Japanese.IsChecked = true;
            MenuItem_English.IsChecked = false;
            Properties.Settings.Default.Language = "ja";
            Properties.Settings.Default.Save();
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri(@"Resources/StringResource." + Properties.Settings.Default.Language + @".xaml", UriKind.Relative);
            this.Resources.MergedDictionaries.Add(dictionary);
        }

        private void Language_English_Click(object sender, RoutedEventArgs e)
        {
            MenuItem_Japanese.IsChecked = false;
            MenuItem_English.IsChecked = true;
            Properties.Settings.Default.Language = "en";
            Properties.Settings.Default.Save();
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri(@"Resources/StringResource." + Properties.Settings.Default.Language + @".xaml", UriKind.Relative);
            this.Resources.MergedDictionaries.Add(dictionary);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.Language == "ja")
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja");
                MenuItem_Japanese.IsChecked = true;
                MenuItem_English.IsChecked = false;
            }
            else if(Properties.Settings.Default.Language == "en")
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("");
                MenuItem_Japanese.IsChecked = false;
                MenuItem_English.IsChecked = true;
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            Setting sw = new Setting();
            sw.Owner = this;
            sw.ShowDialog();
        }
    }
}
