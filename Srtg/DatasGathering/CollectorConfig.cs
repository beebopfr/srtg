using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SnmpSharpNet;

namespace Srtg.DatasGathering {
    public class CollectorConfig : INotifyPropertyChanged, IDataErrorInfo, ICloneable {

        private SnmpSharpNet.SnmpVersion _snmpVersion;
        private uint _snmpPort;
        private string _targetHost;
        private string _targetCommunity;
        private uint _targetInterfaceIndex;
        private int _gatherInterval;

        public SnmpVersion SnmpVersion {
            get {
                return _snmpVersion;
            }

            set {
                _snmpVersion = value;
                OnPropertyChanged("SnmpVersion");
            }
        }
        public uint SnmpPort {
            get {
                return _snmpPort;
            }

            set {
                _snmpPort = value;
                OnPropertyChanged("SnmpPort");
            }
        }
        public string TargetHost {
            get {
                return _targetHost;
            }

            set {
                _targetHost = value;
                OnPropertyChanged("TargetHost");
            }
        }
        public string TargetCommunity {
            get {
                return _targetCommunity;
            }

            set {
                _targetCommunity = value;
                OnPropertyChanged("TargetCommunity");
            }
        }
        public uint TargetInterfaceIndex {
            get {
                return _targetInterfaceIndex;
            }

            set {
                _targetInterfaceIndex = value;
                OnPropertyChanged("TargetInterfaceIndex");
            }
        }
        public int GatherInterval {
            get {
                return _gatherInterval;
            }

            set {
                _gatherInterval = value;
                OnPropertyChanged("GatherInterval");
            }
        }

        public int GatherIntervalSeconds {
            get {
                return GatherInterval / 1000;
            }
            set {
                GatherInterval = value * 1000;
                OnPropertyChanged("GatherIntervalSeconds");
            }
        }

        string IDataErrorInfo.Error {
            get {
                return string.Empty;
            }
        }

        string IDataErrorInfo.this[string columnName] {
            get {
                var error = string.Empty;
                switch (columnName) {
                    case "TargetCommunity":
                        if (string.IsNullOrWhiteSpace(this.TargetCommunity))
                            error = "SNMP community cannot be empty";
                        break;
                    case "TargetHost":
                        if (string.IsNullOrWhiteSpace(this.TargetHost))
                            error = "Target host cannot be empty";
                        break;
                    case "GatherInterval":
                    case "GatherIntervalSeconds":
                        if (this.GatherInterval < 1000 || this.GatherInterval > 60000)
                            error = "Gather interval must be between 1 and 60 sec.";
                        break;
                }
                return error;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propName) {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        public static CollectorConfig Default() {
            return new CollectorConfig() {
                GatherInterval = 1000,
                SnmpVersion = SnmpSharpNet.SnmpVersion.Ver2,
                TargetCommunity = "public",
                SnmpPort = 161,
                TargetHost = "localhost",
                TargetInterfaceIndex = 1
            };
        }

        public object Clone() {
            return new CollectorConfig() {
                _gatherInterval = this._gatherInterval,
                _snmpPort = this._snmpPort,
                _snmpVersion = this._snmpVersion,
                _targetCommunity = this._targetCommunity,
                _targetHost = this._targetHost,
                _targetInterfaceIndex = this._targetInterfaceIndex
            };
        }

        public string ToIniText() {

            var sb = new StringBuilder(
                string.Format("# SRTG configuration file\n" +
                              "# Generated {0}\n",
                              DateTime.Now.ToString()));

            Action<string, object> addLine = (prop, val) => {
                sb.AppendFormat("{0}={1}\n", prop, val);
            };

            addLine("SnmpVersion", this.SnmpVersion);
            addLine("SnmpPort", this.SnmpPort);
            addLine("TargetHost", this.TargetHost);
            addLine("TargetCommunity", this.TargetCommunity);
            addLine("TargetInterfaceIndex", this.TargetInterfaceIndex);
            addLine("GatherInterval", this.GatherInterval);

            return sb.ToString();

        }

        public static CollectorConfig FromIniText(string ini) {

            var result = CollectorConfig.Default();
            var lineindex = 1;
            foreach (var line in ini.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) {
                    lineindex++;
                    continue;
                }

                var parts = line.Split('=');
                if (parts.Length < 2)
                    throw new FormatException("Bad line at #" + lineindex.ToString());

                var prop = parts[0].Trim();
                var value = string.Join("=", parts.Skip(1).ToArray()).Trim();

                switch (prop) {
                    case "SnmpVersion":
                        result.SnmpVersion = value == "Ver2" ? SnmpVersion.Ver2 : SnmpVersion.Ver1;
                        break;
                    case "SnmpPort":
                        result.SnmpPort = uint.Parse(value);
                        break;
                    case "TargetHost":
                        result.TargetHost = value;
                        break;
                    case "TargetCommunity":
                        result.TargetCommunity = value;
                        break;
                    case "TargetInterfaceIndex":
                        result.TargetInterfaceIndex = uint.Parse(value);
                        break;
                    case "GatherInterval":
                        result.GatherInterval = int.Parse(value);
                        break;
                }

                lineindex++;
            }

            return result;
        }
    }
}
