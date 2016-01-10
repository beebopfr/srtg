using System.Diagnostics;
using System.Windows;

namespace Srtg {


    public partial class AboutWindow : Window {
        public AboutWindow() {
            InitializeComponent();

            txtVersion.Text = typeof(AboutWindow).Assembly.GetName().Version.ToString(3);
        }

        private void lnkBeebop_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
