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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ResxUnusedFinder.Properties;

namespace ResxUnusedFinder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            this.DataContext = new MainViewModel();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.MainWindowPlacement = this.GetPlacement();

            var vm = this.DataContext as MainViewModel;
            vm.OnClose();

            Settings.Default.KeyColumnWidth = this.keyColumn.ActualWidth;
            Settings.Default.ValueColumnWidth = this.valueColumn.ActualWidth;

            Settings.Default.Save();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.SetPlacement(Settings.Default.MainWindowPlacement);

            this.keyColumn.Width = Settings.Default.KeyColumnWidth;
            this.valueColumn.Width = Settings.Default.ValueColumnWidth;
        }
    }
}
