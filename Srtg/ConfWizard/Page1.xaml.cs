using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
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
    /// Logique d'interaction pour Page1.xaml
    /// </summary>
    public partial class Page1 : Page, INotifyPropertyChanged, IDisposable {

        NavigationService _nav;
        public List<ValidationError> _validationErrors = new List<ValidationError>();
        DatasGathering.SnmpHelper snmp;

        public DatasGathering.CollectorConfig Config { get; set; }
        public event Action<DatasGathering.CollectorConfig> WizardFinished;
        public event PropertyChangedEventHandler PropertyChanged;

        public Page1() {
            InitializeComponent();
            this.iVersion.Items.Add(SnmpSharpNet.SnmpVersion.Ver1);
            this.iVersion.Items.Add(SnmpSharpNet.SnmpVersion.Ver2);
        }

        private void ShowErrorInfo(string msg) {
            MessageBox.Show(msg, "An error occured", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void btNext_Click(object sender, RoutedEventArgs e) {

            grdLoading.Visibility = Visibility.Visible;

            DatasGathering.SnmpHelper.InterfaceInfos[] ints = null;
            string error = null;
            try {
                ints = await RetrieveInterfacesInfos();
            }
            catch (System.Net.Sockets.SocketException ex) { error = ex.Message; }
            catch (SnmpSharpNet.SnmpException ex2) { error = ex2.Message; }
            catch (OperationCanceledException) { return; }

            this.grdLoading.Visibility = Visibility.Hidden;

            if (error != null) {
                ShowErrorInfo(error);
                return;
            }

            var page2 = new Page2(ints, Config);
            page2.WizardFinished += this.WizardFinished;

            this._nav.Navigate(page2);
            
        }

        private async Task<DatasGathering.SnmpHelper.InterfaceInfos[]> RetrieveInterfacesInfos() {

            snmp = new DatasGathering.SnmpHelper(
                Config.TargetHost, 
                Config.TargetCommunity,
                Config.SnmpVersion,
                Config.SnmpPort 
                );
            return await snmp.GetInterfacesInfos();           

        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            this._nav = NavigationService.GetNavigationService(this);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs("Config"));
        }

        private void Grid_ValidationError(object sender, ValidationErrorEventArgs e) {
            if (e.Action == ValidationErrorEventAction.Added)
                _validationErrors.Add(e.Error);
            else
                _validationErrors.Remove(e.Error);
            btNext.IsEnabled = _validationErrors.Count == 0;
        }

        public void Dispose() {
            if (this.snmp != null)
                this.snmp.Dispose();
        }
    }
}
