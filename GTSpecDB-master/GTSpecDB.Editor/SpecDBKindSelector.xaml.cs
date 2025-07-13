using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Humanizer;

using PDTools.SpecDB.Core;
namespace GTSpecDB.Editor
{
    /// <summary>
    /// Interaction logic for SpecDBKindSelector.xaml
    /// </summary>
    public partial class SpecDBKindSelector : Window
    {
        public SpecDBFolder SelectedType { get; set; }
        public bool HasSelected { get; set; }
        public SpecDBKindSelector()
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri(@"Resources/StringResource." + Properties.Settings.Default.Language + @".xaml", UriKind.Relative);
            this.Resources.MergedDictionaries.Add(dictionary);

            InitializeComponent();

            foreach (var type in (SpecDBFolder[])Enum.GetValues(typeof(SpecDBFolder)))
            {
                string lb_Types_str = type.Humanize().ToString();
                if (Properties.Settings.Default.Language == "ja")
                {
                    lb_Types_str = lb_Types_str.Replace("[", "【");
                    lb_Types_str = lb_Types_str.Replace("]", "】");
                    lb_Types_str = lb_Types_str.Replace("Gran Turismo ", "グランツーリスモ");
                    lb_Types_str = lb_Types_str.Replace("Prologue", "プロローグ");
                    lb_Types_str = lb_Types_str.Replace("Special Edition 2004 Geneva Version", "スペシャルエディション 2004 ジュネーブバージョン");
                    lb_Types_str = lb_Types_str.Replace("/Toyota ", " / トヨタ");
                    lb_Types_str = lb_Types_str.Replace("Demo", "デモ");
                    lb_Types_str = lb_Types_str.Replace("Pre-Release", "プレリリース");
                    lb_Types_str = lb_Types_str.Replace("Online Test Version", "オンライン実験バージョン");
                    lb_Types_str = lb_Types_str.Replace("Tourist Trophy", "ツーリストトロフィー");
                    lb_Types_str = lb_Types_str.Replace(" Concept", "コンセプト");
                    lb_Types_str = lb_Types_str.Replace("Time Trial Challenge", "タイムトライアルチャレンジ");
                    lb_Types_str = lb_Types_str.Replace("Kiosk ", "キオスク");
                    lb_Types_str = lb_Types_str.Replace("Default DB", "デフォルトDB");
                    lb_Types_str = lb_Types_str.Replace("Preview DB", "プレビューDB");
                }
                lb_Types.Items.Add(lb_Types_str);
            }

        }

        private void lb_Types_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btn_PickSpecDBType.IsEnabled = lb_Types.SelectedIndex != -1;
        }

        private void btn_PickSpecDBType_Click(object sender, RoutedEventArgs e)
        {
            if (lb_Types.SelectedIndex != -1)
            {
                SelectedType = (SpecDBFolder)lb_Types.SelectedIndex;
                HasSelected = true;
                Close();
            }
        }
    }
}
