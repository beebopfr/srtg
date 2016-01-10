using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Srtg {
    
    public partial class App : Application {

        private void TextBox_GotKeyboardFocus(Object sender, KeyboardFocusChangedEventArgs e) {
            ((TextBox)sender).SelectAll();
        }

        private void Application_Startup(object sender, StartupEventArgs e) {

            var hasParam = e.Args.Length > 0;
            var main = new MainWindow(!hasParam);
            

            main.Show();

            if (hasParam)
                main.LoadIniFile(e.Args[0]);
        }
    }
}
