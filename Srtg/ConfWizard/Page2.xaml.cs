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

namespace Srtg.ConfWizard {
    /// <summary>
    /// Logique d'interaction pour Page2.xaml
    /// </summary>
    public partial class Page2 : Page {

        NavigationService _nav;
        DatasGathering.SnmpHelper.InterfaceInfos[] _ints;
        DatasGathering.CollectorConfig _conf;

        public event Action<DatasGathering.CollectorConfig> WizardFinished;
        
        public Page2(DatasGathering.SnmpHelper.InterfaceInfos[] ints, DatasGathering.CollectorConfig conf) {
            InitializeComponent();

            lstInts.SelectionChanged += LstInts_SelectionChanged;
            
            this._ints = ints;
            this._conf = conf;
            lstInts.ItemsSource = this._ints;
        }

        private void LstInts_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            btFinish.IsEnabled = lstInts.SelectedIndex != -1;
        }

        private void btBacky_Click(object sender, RoutedEventArgs e) {
            _nav.GoBack();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            _nav = NavigationService.GetNavigationService(this);
        }

        private void btFinish_Click(object sender, RoutedEventArgs e) {
            _conf.TargetInterfaceIndex = ((DatasGathering.SnmpHelper.InterfaceInfos)lstInts.SelectedItem).Index;
            if (this.WizardFinished != null)
                this.WizardFinished(_conf);
        }
    }
}
