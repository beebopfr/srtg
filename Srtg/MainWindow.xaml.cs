using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Srtg {

    public partial class MainWindow : Window {

        DatasGathering.DatasCollector _collector;
        ChartRendering.ChartRenderer _chart;

        DatasGathering.CollectorState _currentCollectorState;
        int _currentCollectorRemaining;
        string _currFileName;

        private string FileDialogFilter {
            get {
                return "SRTG Configuration files (*.srtg)|*.srtg|Tous les fichiers (*.*)|*.*";
            }
        }

        public MainWindow() {
            InitializeComponent();
        }
        public MainWindow(bool showStartupScreen) : this() {
            grdNoConfiguration.Visibility = showStartupScreen ? Visibility.Visible : Visibility.Hidden;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
        }

        private void InitCollector(DatasGathering.CollectorConfig conf) {

            _currFileName = null;

            if (_collector != null)
                _collector.Dispose();

            _collector = new DatasGathering.DatasCollector(conf);
            _collector.DatasChanged += _collector_DatasChanged;
            _collector.StateChanged += _collector_StateChanged;
            _collector.AwaitTick += _collector_AwaitTick;
            _chart = new ChartRendering.ChartRenderer(cnvChart, _collector.Datas);            
            _chart.Render();

            lblError.Text = "";
            imgError.Visibility = Visibility.Hidden;

            var t = _collector.Start();

            grdNoConfiguration.Visibility = Visibility.Hidden;
            mnSave.IsEnabled = true;
        }

        private void _collector_AwaitTick(int remaining) {
            _currentCollectorRemaining = remaining;
            UpdateStateStatus();
        }

        private void SetWindowInfos() {
            if (_collector == null || _collector.Config == null) {
                this.Title = "SRTG : empty configuration";
            }
            else {
                this.Title = string.Format("SRTG : {0}:{1} ({2})", _collector.Config.TargetHost, _collector.Config.SnmpPort, _collector.Config.TargetCommunity);
                if (this._currFileName != null)
                    this.Title += string.Format(" [{0}]", System.IO.Path.GetFileName(this._currFileName));
            }
        }

        private void _collector_StateChanged(DatasGathering.CollectorState st) {
            _currentCollectorState = st;
            if (st != DatasGathering.CollectorState.Awaiting)
                _currentCollectorRemaining = 0;
            UpdateStateStatus();
        }

        private void UpdateStateStatus() {
            var stateString = _currentCollectorState.ToString();
            if (_currentCollectorRemaining > 0)
                stateString = string.Format("{0} ({1})", stateString, Math.Ceiling(_currentCollectorRemaining / 1000d));

            try {
                Dispatcher.Invoke(() => this.lblCollectorStatus.Text = stateString);
            }
            catch (OperationCanceledException) { };
        }

        private void _collector_DatasChanged() {
            Dispatcher.Invoke(() => imgError.Visibility = Visibility.Hidden);
            Dispatcher.Invoke(() => _chart.Render());
            if (_collector.Datas != null && _collector.Datas.Count > 0) {
                var last = _collector.Datas.Last();
                Dispatcher.Invoke(() => {
                    lblLastIn.Text = ChartRendering.ChartRenderer.FormatSpeed(last.InBitsSpeed);
                    lblLastOut.Text = ChartRendering.ChartRenderer.FormatSpeed(last.OutBitsSpeed);
                    lblMaxIn.Text = ChartRendering.ChartRenderer.FormatSpeed(_collector.Datas.Max(dt => dt.InBitsSpeed));
                    lblMaxOut.Text = ChartRendering.ChartRenderer.FormatSpeed(_collector.Datas.Max(dt => dt.OutBitsSpeed));
                    lblError.Text = last.IsError ? string.Format("{0}", last.Error.Message) : "";
                    imgError.Visibility = last.IsError ? Visibility.Visible : Visibility.Hidden;
                    pnlLastSpeeds.Visibility = last.IsError ? Visibility.Collapsed : Visibility.Visible;
                });
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
           if (_chart != null)
              _chart.Render(fast: true);
        }

        private void OpenSettings(DatasGathering.CollectorConfig initialConf = null) {
            var oldconf = _collector == null ? DatasGathering.CollectorConfig.Default() : _collector.Config;
            var initial = initialConf ?? (DatasGathering.CollectorConfig)oldconf.Clone();

            var win = new SettingsWindow(initial) { Owner = this };

            if (true == win.ShowDialog()) {
                var conf = win.Config;
                var reset = _collector == null ||
                    conf.SnmpPort != oldconf.SnmpPort || conf.TargetCommunity != oldconf.TargetCommunity ||
                                conf.TargetHost != oldconf.TargetHost || conf.TargetInterfaceIndex != oldconf.TargetInterfaceIndex;
                if (reset)
                    InitCollector(conf);
                else
                    _collector.Config = conf;
            }

            SetWindowInfos();
        }

        private void btSettings_Click(object sender, RoutedEventArgs e) {
            OpenSettings();
        }

        private void btWizard_Click(object sender, RoutedEventArgs e) {
            var wiz = new ConfWizard.WizardWindow() { Owner = this };
            if (true == wiz.ShowDialog()) {
                OpenSettings(wiz.Config);
            }
        }

        private void linkOpenSettings_Click(object sender, RoutedEventArgs e) {
            btSettings_Click(this, null);
        }

        private void linkOpenWizard_Click(object sender, RoutedEventArgs e) {
            btWizard_Click(this, null);
        }

        private void btSaveConfig_Click(object sender, RoutedEventArgs e) {
            if (this._collector == null || this._collector.Config == null)
                return;
            var conf = this._collector.Config;
            var ini = conf.ToIniText();
            var fd = new SaveFileDialog();
            fd.DefaultExt = ".srtg";
            fd.Filter = this.FileDialogFilter;
            fd.FileName = string.Format("{0}_{1}.srtg", conf.TargetHost, conf.TargetInterfaceIndex);
            fd.InitialDirectory = Properties.Settings.Default.fdpath_config;

            if (true == fd.ShowDialog(this)) {
                Properties.Settings.Default.fdpath_config = Path.GetDirectoryName(fd.FileName);
                Properties.Settings.Default.Save();

                var outfile = this._currFileName = fd.FileName;
                System.IO.File.WriteAllText(outfile, ini, System.Text.Encoding.ASCII);
                SetWindowInfos();
            }
        }

        internal void LoadIniFile(string path) {
            string ini = string.Empty;
            try {
                ini = System.IO.File.ReadAllText(path);
            }
            catch (System.IO.IOException e) {
                MessageBox.Show(string.Format("Cannot load file : {0}", e.Message), "An error occured", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var conf = DatasGathering.CollectorConfig.FromIniText(ini);
            InitCollector(conf);            
            this._currFileName = path;
            SetWindowInfos();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (_collector != null)
                _collector.Dispose();
        }

        private void btOpen_Click(object sender, RoutedEventArgs e) {
            var fd = new OpenFileDialog();
            fd.Filter = this.FileDialogFilter;
            fd.InitialDirectory = Properties.Settings.Default.fdpath_config;

            if (true == fd.ShowDialog(this)) {
                Properties.Settings.Default.fdpath_config = Path.GetDirectoryName(fd.FileName);
                Properties.Settings.Default.Save();

                var infile = this._currFileName = fd.FileName;
                LoadIniFile(infile);                
            }
        }

        private void linkOpenFile_Click(object sender, RoutedEventArgs e) {
            btOpen_Click(this, null);
        }

        private void btSaveImage_Click(object sender, RoutedEventArgs e) {

            if (_collector == null)
                return;

            // Create new canvas and renderer
            var cnv = new Canvas() { Margin = cnvChart.Margin, Width = cnvChart.ActualWidth, Height = cnvChart.ActualHeight, Background = System.Windows.Media.Brushes.White };            
            var ch = new ChartRendering.ChartRenderer(cnv, _collector.Datas);
            cnv.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            cnv.Arrange(new Rect(0, 0, cnv.DesiredSize.Width, cnv.DesiredSize.Height));
            var cwidth = (int)cnv.RenderSize.Width;
            var cheight = (int)(cnv.RenderSize.Height + cnv.Margin.Top * 2);
            var rtb = new RenderTargetBitmap(cwidth, cheight, 96d, 96d, PixelFormats.Default);

            // Generate a white background
            var dv = new DrawingVisual();
            var dvct = dv.RenderOpen();
            dvct.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, rtb.Width, rtb.Height));
            dvct.Close();
            rtb.Render(dv);

            // Render chart and create Bitmap
            ch.Render(false);
            // Add additional informations overlays            
            AddChartImageInfos(cnv);
            cnv.UpdateLayout();
            rtb.Render(cnv);

            // Open file dialog and save image if OK
            var fd = new SaveFileDialog();
            fd.Filter = "PNG Images|*.png";
            fd.DefaultExt = ".png";
            fd.InitialDirectory = Properties.Settings.Default.fdpath_img;

            if (true == fd.ShowDialog(this)) {
                Properties.Settings.Default.fdpath_img = Path.GetDirectoryName(fd.FileName);
                Properties.Settings.Default.Save();

                var bf = BitmapFrame.Create(rtb);
                var pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(bf);                
                using (var st = System.IO.File.OpenWrite(fd.FileName)) {
                    pngEncoder.Save(st);
                }

                var result = MessageBox.Show("Show generated file in Explorer ?", "Image successfully generated", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start("explorer.exe", "/select, " + fd.FileName);
            }
            
        }

        private void AddChartImageInfos(Canvas cnv) {

            var grid = new Grid() { Background = System.Windows.Media.Brushes.Transparent, Width = cnv.ActualWidth, Height = cnv.ActualHeight + 2*cnvChart.Margin.Top };
            grid.Margin = new Thickness(0, 0 - cnvChart.Margin.Top, 0, 0);
            cnv.Children.Add(grid);

            var txtTitle = new TextBlock() { Text = this.Title, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment=HorizontalAlignment.Right };
            txtTitle.Margin = new Thickness(0, 8, 8, 0);
            grid.Children.Add(txtTitle);

            var txtFooter = new TextBlock() { Text = this.Title, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 9 };
            txtFooter.Margin = new Thickness(0, 0, 6, 6);
            txtFooter.Foreground = System.Windows.Media.Brushes.Gray;
            var maxIn = ChartRendering.ChartRenderer.FormatSpeed(_collector.Datas.Max(dt => dt.InBitsSpeed));
            var maxOut = ChartRendering.ChartRenderer.FormatSpeed(_collector.Datas.Max(dt => dt.OutBitsSpeed));
            txtFooter.Text = string.Format("generated : {0} - max in : {1} - max out : {2}", DateTime.Now, maxIn, maxOut);
            grid.Children.Add(txtFooter);

        }

        private void btAbout_Click(object sender, RoutedEventArgs e) {
            new AboutWindow() { Owner = this }.ShowDialog();
        }
        
    }
}
