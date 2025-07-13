using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace GTSpecDB.Editor
{
    /// <summary>
    /// Setting.xaml の相互作用ロジック
    /// </summary>

    public delegate void TextBoxValueChangedEventHandler2(object sender, string value);

    public partial class Setting : Window
    {
        public Setting()
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri(@"Resources/StringResource." + Properties.Settings.Default.Language + @".xaml", UriKind.Relative);
            this.Resources.MergedDictionaries.Add(dictionary);

            InitializeComponent();
        }

        public event TextBoxValueChangedEventHandler2 TextBoxValueChanged2;

        private void Multi_Copy_ID_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");

            int pos_before = tb.SelectionStart;
            string tb_before = tb.Text;
            tb.Text = Regex.Replace(tb.Text, @"[^0-9]", "");
            if (tb.Text.Length < tb_before.Length)
            {
                int pos_after = pos_before - (tb_before.Length - tb.Text.Length);
                if (pos_after < 0)
                    pos_after = 0;
                tb.SelectionStart = pos_after;
            }
        }

        private void Multi_Copy_ID_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var tb = sender as TextBox;
            string tb_before = tb.Text;
            int pos_before = tb.SelectionStart;
            if (e.Command == ApplicationCommands.Paste)
            {
                string pastedText = Clipboard.GetText();
                pastedText = Regex.Replace(pastedText, @"[^0-9]", "");
                e.Handled = true;
                tb.Text = tb.Text + pastedText;
                tb.SelectionStart = pos_before + pastedText.Length;
            }
        }

        private void AssociatedObject_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            for (int i = 0; i < 26; i++)
            {
                string tbName = $"Multi_Copy_ID_{i + 1}";
                string settingName = $"Multi_Copy_ID_{i + 1}";

                var tb = this.FindName(tbName) as TextBox;
                var prop = Properties.Settings.Default.GetType().GetProperty(settingName);

                if (tb != null && prop != null)
                {
                    int value = int.TryParse(tb.Text, out int parsed) ? parsed : 0;
                    prop.SetValue(Properties.Settings.Default, value);
                }
            }
            Properties.Settings.Default.Save();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 26; i++)
            {
                string tbName = $"Multi_Copy_ID_{i + 1}";
                string settingName = $"Multi_Copy_ID_{i + 1}";

                var tb = this.FindName(tbName) as TextBox;
                var prop = Properties.Settings.Default.GetType().GetProperty(settingName);

                if (tb != null && prop != null)
                {
                    var value = prop.GetValue(Properties.Settings.Default)?.ToString();
                    tb.Text = value;
                }
            }
        }
    }
}
