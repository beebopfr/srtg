using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Srtg.ChartRendering {
    public class ChartRenderer {

        private double _maxBps = 5000;
        /// <summary>
        /// Get or set the maximum bps value on the X axis
        /// </summary>
        public double MaxBits {
            get { return _maxBps; }
            set { _maxBps = value; }
        }

        public bool MaxBitsAutoAdjust { get; set; }

        private double _chartLength = 300000;
        /// <summary>
        /// Get or set the maximum visible time interval on the chart in seconds)
        /// </summary>
        public double ChartLength {
            get { return _chartLength / 1000; }
            set { _chartLength = value * 1000; }
        }

        private bool _RemoveOldDatas = true;
        /// <summary>
        /// Get or set if datas older than the chart length (out of the visible area) must be
        /// removed from the collection after each render
        /// </summary>
        public bool RemoveOldDatas {
            get {
                return _RemoveOldDatas;
            }

            set {
                _RemoveOldDatas = value;
            }
        }



        // The canvas element to render the chart into
        private Canvas _Canvas;
        // The current chart datas collection
        private List<ChartData> _Datas;
        // Path element for the blue input line
        private Path _LineBlue;
        // Path element for the green output shape
        private Path _LineGreen;
        // Line element for the current cursor position
        private Line _LineCurrentPos;
        // Line element for the X axis
        private Line _lineX;
        // Line element for the Y axis
        private Line _lineY;

        private IFormatProvider US_NUMBERS_FORMAT = CultureInfo.GetCultureInfo("en-US").NumberFormat;
        private double _marginLeft = 60;
        private double _marginBottom = 30;
        private double _marginTop = 30;

        /// <summary>
        /// Returns the chart starting time
        /// </summary>
        public DateTime GraphStartTime {
            get {
                // No datas, return the current datetime as start point
                if (_Datas == null || _Datas.Count() == 0)
                    return DateTime.Now;

                return _Datas.First().TimeStamp;
            }
        }

        // Calculate the actual inner chart width (without margins)
        private double ChartInnertWidth {
            get {
                return this._Canvas.ActualWidth - this._marginLeft;
            }
        }

        // Calculate the actual inner chart height (without margins)
        private double CharInnertHeight {
            get {
                return this._Canvas.ActualHeight - this._marginBottom - this._marginTop;
            }
        }

        // Helper that returns the inner bottom y point, just above the X/times axis line
        private double ChartInnerBottomY {
            get {
                return Math.Round(this._Canvas.ActualHeight - this._marginBottom);
            }
        }

        
       
        // Keep a list of elements that have to be removed from the canvas at each rendering
        private List<UIElement> _temporaryElements = new List<UIElement>();

        // Elements colors
        private Brush GreenValueBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
        private Brush ErrorFillBrush = new SolidColorBrush(Color.FromRgb(255, 224, 224));

        /// <summary>
        /// Create a new chart renderer
        /// </summary>
        /// <param name="canvas">The canvas where to draw the chart</param>
        /// <param name="datas"The datas collection to bind the renderer to</param>
        public ChartRenderer(Canvas canvas, List<ChartData> datas) {

            if (canvas == null)
                throw new ArgumentNullException("canvas", "canvas parameter cannot be null");

            if (datas == null)
                throw new ArgumentNullException("datas", "datas parameter cannot be null");

            this.MaxBitsAutoAdjust = true; // Default value

            this._Canvas = canvas;
            this._Datas = datas;

            // Clear the parent canvas
            this._Canvas.Children.Clear();

            // Create all the permanent elements
            _LineBlue = new Path() { Stroke = Brushes.Blue, StrokeThickness = 1.5 };
            _LineGreen = new Path() { Stroke = GreenValueBrush, Fill = GreenValueBrush, StrokeThickness = 1.5 };
            _LineCurrentPos = new Line() { Stroke = Brushes.Red, StrokeThickness = 2 };
            _lineX = new Line() { Stroke = Brushes.Black, StrokeThickness = 2 };
            _lineY = new Line() { Stroke = Brushes.Black, StrokeThickness = 2 };

            // All them all as children of the parent canvas
            this._Canvas.Children.Add(_LineGreen);
            this._Canvas.Children.Add(_LineBlue);
            this._Canvas.Children.Add(_LineCurrentPos);
            this._Canvas.Children.Add(_lineX);
            this._Canvas.Children.Add(_lineY);
        }

        private double GetOverflowOffset() {

            // Calculer un offset négatif en cas de dépassement horizontal pour avoir toujour
            // le curseur de position à la limite droite du chart
            double overflowOffset = 0;
            var lasttime = _Datas.Count() == 0 ? default(DateTime) : _Datas.Last().TimeStamp;
            var pxTotalDatasWidth = (lasttime - this.GraphStartTime).TotalMilliseconds * this.ChartInnertWidth / this._chartLength;
            if (pxTotalDatasWidth > this.ChartInnertWidth)
                overflowOffset = pxTotalDatasWidth - this.ChartInnertWidth + 2;
            return overflowOffset;

        }

        private DateTime GetMinVisibleTime() {
            if (this._Datas == null)
                return DateTime.MinValue;
            // = startTime + offset milliseconds
            var mintime = this.GraphStartTime.AddMilliseconds(this.GetOverflowOffset() / (ChartInnertWidth / _chartLength));
            // return time of the element before the first visible
            var el = this._Datas.LastOrDefault(dt => dt.TimeStamp < mintime);
            return el == null ? DateTime.MinValue : el.TimeStamp;
        }

        private void UpdateXYAxis() {
            // X axis line
            _lineX.X1 = 0;
            _lineX.Y1 = _lineX.Y2 = CharInnertHeight + _marginTop + 1;
            _lineX.X2 = ChartInnertWidth + _marginLeft;

            // Y axis line
            _lineY.X1 = _lineY.X2 = _marginLeft;
            _lineY.Y1 = _marginTop;
            _lineY.Y2 = CharInnertHeight + _marginTop + 1;
        }

        private void UpdateTimePlots() {

            var offset = GetOverflowOffset();
            var pixelsPerSecond = ChartInnertWidth * 1000d / _chartLength;
            var overflowOffsetSeconds = offset / pixelsPerSecond;
            var stepSeconds = 1;
            var step = 0d;
            var minstep = 70;

            // Find the step in seconds corresponding to the minimal desired minstep pixel space
            while (step < minstep) {
                step = pixelsPerSecond * stepSeconds;
                stepSeconds += 1;
            }

            // loop step by step from 0 to chartlength
            for (var timePos = 0; timePos <= (_chartLength / 1000) + overflowOffsetSeconds; timePos += stepSeconds) {

                var x = (timePos * pixelsPerSecond) + _marginLeft - offset;

                // Add the plot line as a temporary element
                var line = new Line() { Stroke = Brushes.Black, X1 = x, X2 = x, Y1 = CharInnertHeight + _marginTop, Y2 = CharInnertHeight + 4 + _marginTop };
                _Canvas.Children.Add(line);
                _temporaryElements.Add(line);

                // If we are in the chart inner visible space, draw a vertical dotted line
                // and add it as a temporary element
                if (x > _marginLeft) {
                    var dottedLine = new Line() { Stroke = Brushes.LightGray, StrokeDashArray = { 1, 1 }, X1 = x, X2 = x, Y1 = ChartInnerBottomY, Y2 = _marginTop };
                    _Canvas.Children.Add(dottedLine);
                    _temporaryElements.Add(dottedLine);
                }

                var timeToPrint = GraphStartTime.AddSeconds(timePos).ToString("HH:mm:ss");

                // draw the time text 
                // and add it as a temporary element
                var text = new TextBlock() { Text = timeToPrint, FontSize = 10 };
                var isFirst = timePos == 0 && offset == 0;
                if (isFirst)
                    text.FontWeight = FontWeights.Bold;
                _Canvas.Children.Add(text);
                _temporaryElements.Add(text);

                // center the text beside the plot
                text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textWidth = text.DesiredSize.Width;
                text.Margin = new Thickness(x - (textWidth / 2), isFirst ? CharInnertHeight + _marginTop + 4 : CharInnertHeight + _marginTop + 6, 0, 0);

                
            }

        }

        /// <summary>
        /// Format a speed expressed in bits to a human readable speed with unit
        /// </summary>
        /// <param name="speed">The input speed un bits</param>
        /// <param name="ShortUnitsText">Set to true to get units in short form ('M') rather than long ('Mbps')</param>
        /// <returns></returns>
        public static string FormatSpeed(double speed, bool ShortUnitsText = false) {

            var units = new string[] { "bps", "kbps", "Mbps", "Gbps" };
            var shortUnits = new string[] { "b", "k", "M", "G" };

            var u = 0;
            var s = Math.Round(speed, 2);
            while (u < units.Length - 1 && s >= 1000) {
                s = Math.Round(s/1000, 2);
                u++;
            }

            var unit = ShortUnitsText ? shortUnits[u] : units[u];
            return string.Format("{0:0.0#} {1}", s, unit);

        }

        private void UpdateYPlots() {

            var diviser = 1d;
            var step = double.MaxValue;
            var nextstep = double.MaxValue;
            var minstep = 50;

            while (nextstep >= minstep) {
                step = nextstep;
                diviser *= 2;
                nextstep = CharInnertHeight / diviser;
            }

            for (var y = CharInnertHeight - step; y >= -6; y -= step) {

                var line = new Line() { Stroke = Brushes.Black, X1 = _marginLeft - 5, X2 = _marginLeft, Y1 = y + _marginTop, Y2 = y + _marginTop };
                _Canvas.Children.Add(line);
                _temporaryElements.Add(line);

                var dottedLine = new Line() { Stroke = Brushes.DarkGray, StrokeDashArray = { 1, 2 }, X1 = _marginLeft, X2 = ChartInnertWidth + _marginLeft, Y1 = y + _marginTop, Y2 = y + _marginTop };
                _Canvas.Children.Add(dottedLine);
                _temporaryElements.Add(dottedLine);

                var speedToPrint = FormatSpeed((CharInnertHeight - y) * _maxBps / CharInnertHeight, true);

                var text = new TextBlock() { Text = speedToPrint, FontSize = 9, FontWeight = FontWeights.Bold };
                _Canvas.Children.Add(text);
                text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textHeight = text.DesiredSize.Height;
                var textWidth = text.DesiredSize.Width;
                text.Margin = new Thickness(_marginLeft - textWidth - 10, y + _marginTop - textHeight / 2 - 1, 0, 0);
                _temporaryElements.Add(text);
            }

        }

        private double TimeStampToX(DateTime timestamp, bool roundedToPixel = true) {
            var x = (timestamp - this.GraphStartTime).TotalMilliseconds * this.ChartInnertWidth / this._chartLength + this._marginLeft - GetOverflowOffset();
            return roundedToPixel ? Math.Round(x) : x;
        }

        private void UpdateDataLines() {
            // Just return if no data
            if (_Datas == null || _Datas.Count() == 0)
                return;

            // Clip data lines in the inner visible chart part
            var clipRect = new RectangleGeometry(new Rect(_marginLeft, _marginTop, ChartInnertWidth, CharInnertHeight));
            _LineGreen.Clip =
            _LineBlue.Clip = clipRect;
                


            // Stringbuilders for green and blue geomerties
            var sbG = new StringBuilder("M");
            var sbB = new StringBuilder("M");

            var x = double.NaN;
            var prevX = double.NaN;
            var prevYG = double.NaN;
            var prevYB = double.NaN;

            foreach (var data in _Datas) {

                // Calculate x coordinate for data timestamp
                x = TimeStampToX(data.TimeStamp);

                // Calculate in and out y coordinates
                var yG = CharInnertHeight - (data.InBitsSpeed * CharInnertHeight / _maxBps) + _marginTop;
                var yB = CharInnertHeight - (data.OutBitsSpeed * CharInnertHeight / _maxBps) + _marginTop;

                // skip this point if out of view
                if (x < this._marginLeft - 1) {
                    prevX = x;
                    prevYG = yG;
                    prevYB = yB;
                    continue;
                }

                // if this is the first and the only point, set previous values to the same of current values
                if (double.IsNaN(prevX)) {
                    prevX = x;
                    prevYG = yG;
                    prevYB = yB;
                }

                // format and add the paths datas
                sbG.AppendFormat(US_NUMBERS_FORMAT, " {0:0.00},{1:0.00} {2:0.00},{3:0.00}", prevX, prevYG, x, yG);
                sbB.AppendFormat(US_NUMBERS_FORMAT, " {0:0.00},{1:0.00} {2:0.00},{3:0.00}", prevX, prevYB, x, yB);

                // insert error or pause rectangle ?
                if (data.IsError || data.IsPause) {
                    // Create rect
                    var rect = new Path() { Data = Geometry.Parse(string.Format(US_NUMBERS_FORMAT, "M {0:0.00},{3:0.00} {0:0.00},{2:0.00} {1:0.00},{2:0.00} {1:0.00},{3:0.00} {0:0.00},{3:0.00}", prevX, x, ChartInnerBottomY, _marginTop)), Fill = data.IsError ? ErrorFillBrush : Brushes.LightGray };
                    // Clip to inner chart zone
                    rect.Clip = clipRect;
                    // add as temp
                    _Canvas.Children.Add(rect);
                    _temporaryElements.Add(rect);
                }

                prevX = x;
                prevYB = yB;
                prevYG = yG;

            }

            // Close the green path
            sbG.AppendFormat(US_NUMBERS_FORMAT, " {0:0.00},{1:0.00} {2:0.00},{1:0.00}", x, ChartInnerBottomY, _marginLeft);

            // Set the final green and blue paths datas
            _LineGreen.Data = Geometry.Parse(sbG.ToString());
            _LineBlue.Data = Geometry.Parse(sbB.ToString());

            // Update the position cursor datas
            _LineCurrentPos.X1 = _LineCurrentPos.X2 = x + 1;
            _LineCurrentPos.Y1 = 0;
            _LineCurrentPos.Y2 = ChartInnerBottomY;

        }

        // Adjust the maxbits value to the higher unit round value
        private void AutoAdjustMaxBits(double max) {

            // Divide to higher unit
            var multiplicator = 1;
            while (multiplicator <= Math.Pow(1000, 3) && max / 1000 > 1) {
                max /= 1000;
                multiplicator *= 1000;
            }

            // Go to 10% upper margin
            max = max / 0.9;

            // Round to upper 0.5
            max = Math.Ceiling(max * 2);
            max /= 2;
                
            // Re-multiply to bits
            this.MaxBits = max * multiplicator;
        }

        public void Render(bool showCursor = true, bool fast = false) {

            // Remove temporary elements
            foreach (var el in _temporaryElements)
                _Canvas.Children.Remove(el);
            _temporaryElements.Clear();

            var minVisibleTime = GetMinVisibleTime();
            var visibledatas = this._Datas.Where(dt => dt.TimeStamp >= minVisibleTime);

            // Auto adjust MaxBits if needed
            if (!fast && MaxBitsAutoAdjust && this._Datas != null && visibledatas.Any(dt => !dt.IsError)) {
                    var datasmax = visibledatas.Max(d => d.InBitsSpeed > d.OutBitsSpeed ? d.InBitsSpeed : d.OutBitsSpeed);
                    //if (datasmax > this.MaxBits)
                    AutoAdjustMaxBits(datasmax);
            }

            _LineCurrentPos.Visibility = showCursor ? Visibility.Visible : Visibility.Hidden;

            UpdateXYAxis();
            UpdateYPlots();
            UpdateTimePlots();
            UpdateDataLines();

            // Remove old datas if requested            
            if (!fast && this.RemoveOldDatas && this._Datas.Count() > 0 && minVisibleTime > this._Datas.Min(dt => dt.TimeStamp)) {
                var removed = this._Datas.RemoveAll(dt => dt.TimeStamp < minVisibleTime);
                Debug.WriteLine("Removed {0} old data", removed);
            }
            

        }
    }
}
