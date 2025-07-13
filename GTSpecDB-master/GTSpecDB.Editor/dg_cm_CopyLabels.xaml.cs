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

namespace GTSpecDB.Editor
{
    /// <summary>
    /// dg_cm_CopyLabels.xaml の相互作用ロジック
    /// </summary>

    public delegate void TextBoxValueChangedEventHandler(object sender, string value);

    public partial class CopyLabels : Window
    {
        public CopyLabels()
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri(@"Resources/StringResource." + Properties.Settings.Default.Language + @".xaml", UriKind.Relative);
            this.Resources.MergedDictionaries.Add(dictionary);

            InitializeComponent();
        }

        public event TextBoxValueChangedEventHandler TextBoxValueChanged;

        private void btn_Search_and_copy_Click(object sender, RoutedEventArgs e)
        {
            string before = Textbox_before_Label.Text;
            string after = Textbox_after_Label.Text;
            string combined = $"{before}|{after}"; // 区切り付きで渡す（あとで分割する）

            TextBoxValueChanged?.Invoke(this, combined);
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (this.Owner != null)
                this.Owner.IsEnabled = true;
        }
    }
}
