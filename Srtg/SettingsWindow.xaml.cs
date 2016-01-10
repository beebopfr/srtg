using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Srtg {

    public partial class SettingsWindow : Window, INotifyPropertyChanged {

        public DatasGathering.CollectorConfig Config { get; set; }
        public List<ValidationError> _validationErrors = new List<ValidationError>();

        public event PropertyChangedEventHandler PropertyChanged;

        public SettingsWindow(DatasGathering.CollectorConfig _conf) {
            InitializeComponent();
            iVer.Items.Add(SnmpSharpNet.SnmpVersion.Ver1);
            iVer.Items.Add(SnmpSharpNet.SnmpVersion.Ver2);
            this.Config = _conf;
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs("Config"));                    
        }
        private void btSave_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
        }

        private void grdConfig_ValidationError(object sender, ValidationErrorEventArgs e) {
            if (e.Action == ValidationErrorEventAction.Added)
                _validationErrors.Add(e.Error);
            else
                _validationErrors.Remove(e.Error);
            btSave.IsEnabled = _validationErrors.Count == 0;
        }
    }
}
