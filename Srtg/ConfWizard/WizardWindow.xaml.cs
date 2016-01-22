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

// TODO : Calculate best refresh rate

namespace Srtg.ConfWizard {
    /// <summary>
    /// Logique d'interaction pour WizardWindow.xaml
    /// </summary>
    public partial class WizardWindow : Window {

        public DatasGathering.CollectorConfig Config { get; set; }

        private Page1 page1;

        public WizardWindow() {
            InitializeComponent();
            page1 = new Page1();
            page1.WizardFinished += Page1_WizardFinished;
            this.wizardFrame.Navigate(page1);
        }

        private void Page1_WizardFinished(DatasGathering.CollectorConfig conf) {
            this.Config = conf;
            this.Config.GatherInterval = 1000;
            this.DialogResult = true;
            this.Close();
        }

        private void Window_Closed(object sender, EventArgs e) {
            // Dispose eventual snmp socket
            if (page1 != null)
                page1.Dispose();
        }
    }
}
