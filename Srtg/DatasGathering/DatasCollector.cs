using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Srtg.DatasGathering {    

    class DatasCollector : IDisposable {

        public List<ChartRendering.ChartData> Datas { get; private set; }
        public CollectorConfig Config { get; set; }
        public CollectorState State { get; private set; }

        public event Action DatasChanged;
        public event Action<CollectorState> StateChanged;
        public event Action<int> AwaitTick;

        Timer _watchTimer;
        Timer _tickTimer = new Timer(1000) { AutoReset = false };
        TimeSpan _waitStartedTime;
        SnmpHelper snmp;
        System.Threading.CancellationTokenSource GetDatasCancelTokenSource = new System.Threading.CancellationTokenSource();

        public DatasCollector(CollectorConfig config) {
            Datas = new List<ChartRendering.ChartData>();
            SetState(CollectorState.Stopped);

            this.Config = config;
            _watchTimer = new Timer(config.GatherInterval) { AutoReset = false };
            _watchTimer.Elapsed += _watchTimer_Elapsed;
            _tickTimer.Elapsed += _tickTimer_Elapsed;

        }

        private void _tickTimer_Elapsed(object sender, ElapsedEventArgs e) {
            if (_watchTimer == null || _watchTimer.Interval <= 1000)
                return;

            var remaining = _watchTimer.Interval - (DateTime.Now.TimeOfDay - _waitStartedTime).TotalMilliseconds;
            if (this.AwaitTick != null)
                this.AwaitTick((int)remaining);

            if (remaining > 1000 && this.State != CollectorState.Stopped)
                _tickTimer.Start();
        }

        public async Task Start() {
            if (this.State != CollectorState.Stopped)
                return;
            await Task.Run(() => _watchTimer_Elapsed(this, null));
        }

        private void CalculateSpeed(ChartRendering.ChartData _currDatas) {
            if (Datas.Count == 0)
                throw new InvalidOperationException("No available data to calculate time delta");
            var last = Datas.Last(dt => !dt.IsError);
            var ellapsed = _currDatas.TimeStamp.Subtract(last.TimeStamp);

            var diffIn = _currDatas.InCounter - last.InCounter;
            var diffOut = _currDatas.OutCounter - last.OutCounter;

            // Counter loop handling
            if (_currDatas.InCounter < last.InCounter)
                diffIn = (Config._Counters64.GetValueOrDefault() ? UInt64.MaxValue : UInt32.MaxValue) - last.InCounter + _currDatas.InCounter;
            if (_currDatas.OutCounter < last.OutCounter)
                diffOut = (Config._Counters64.GetValueOrDefault() ? UInt64.MaxValue : UInt32.MaxValue) - last.OutCounter + _currDatas.OutCounter;

            _currDatas.InSpeed = (double)diffIn / ellapsed.TotalSeconds;
            _currDatas.OutSpeed = (double)diffOut / ellapsed.TotalSeconds;
            Debug.Print("In : {0}b/s, Out {1}b/s", _currDatas.InBitsSpeed, _currDatas.OutBitsSpeed);
        }

        private void TestCounter64() {
            if (Config._Counters64.HasValue)
                return;

            Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> r = null;
            try {
                r = new SnmpSharpNet.SimpleSnmp(Config.TargetHost, Config.SnmpPort, Config.TargetCommunity, 2000, 2)
                    .Get(Config.SnmpVersion, new [] { SnmpHelper.WellKnownOids.OID_IF_IN_OCTET_64 + "." + Config.TargetInterfaceIndex.ToString() });
            }
            catch {
                Config._Counters64 = false;
            }

            Config._Counters64 = r != null;
        }

        private async Task GetDatas() {                 
            SetState(CollectorState.Querying);

            if (this.State == CollectorState.Stopped)
                return;

            snmp = new SnmpHelper(Config.TargetHost, Config.TargetCommunity, Config.SnmpVersion, Config.SnmpPort);

            ulong[] values = null;
            Exception error = null;

            //TestCounter64();

            try {
                values = await snmp.GetCounters((int)Config.TargetInterfaceIndex, Config._Counters64 ?? false, this.GetDatasCancelTokenSource.Token);
            }
            catch (OperationCanceledException) {
                return;
            }
            catch (Exception ex) {
                error = ex.GetBaseException();
            }

            this.GetDatasCancelTokenSource.Token.ThrowIfCancellationRequested();

            var data = new ChartRendering.ChartData() {
                TimeStamp = DateTime.Now,
                Error = error
            };

            if (data.IsError && Datas.Count > 0) {
                var last = Datas.Last();
                data.InSpeed = last.InSpeed;
                data.OutSpeed = last.OutSpeed;
            }


            if (values != null && values.Length == 2) { // No error while getting data values
                data.InCounter = values[0];
                data.OutCounter = values[1];
                if (Datas.Count > 0 && Datas.Any(dt => !dt.IsError))
                    CalculateSpeed(data);
                if (Datas.Count == 1) {
                    Datas[0].InSpeed = data.InSpeed;
                    Datas[0].OutSpeed = data.OutSpeed;
                }
            }

            Datas.Add(data);
            if (DatasChanged != null)
                DatasChanged();

            _watchTimer.Interval = Config.GatherInterval;
            _waitStartedTime = DateTime.Now.TimeOfDay;
            if (this.State != CollectorState.Stopped)
                _watchTimer.Start();

            SetState(CollectorState.Awaiting);
            if (_watchTimer.Interval >= 1000)
                _tickTimer_Elapsed(this, null);
                
        }

        private void _watchTimer_Elapsed(object sender, ElapsedEventArgs e) {
            try {
                var t = GetDatas();
                t.Wait();
            }
            catch (TaskCanceledException) { }
            catch (AggregateException ex) {
                if (ex.GetBaseException() is TaskCanceledException || ex.GetBaseException() is OperationCanceledException)
                    return;
                throw;
            }
        }

        private void SetState(CollectorState state) {
            this.State = state;
            if (this.StateChanged != null)
                StateChanged(state);
        }

        public void Dispose() {
            this.State = CollectorState.Stopped;
            this._watchTimer.Dispose();
            this._tickTimer.Dispose();

            this.GetDatasCancelTokenSource.Cancel();
            if (this.snmp != null)
                this.snmp.Dispose();
            
            this.StateChanged = null;
            this.DatasChanged = null;
            this.AwaitTick = null;            
        }
    }

    enum CollectorState {
        Stopped,
        Awaiting,
        Paused,
        Querying
    }
}
