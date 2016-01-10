using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Srtg.DatasGathering {
    public class SnmpHelper {

        const int SNMP_TIMEOUT = 3000;
        const int SNMP_RETRY = 2;

        private string _host;
        private uint _port;
        private string _community;
        private SnmpSharpNet.SnmpVersion _ver;
        private SnmpSharpNet.SimpleSnmp _snmp;

        public SnmpHelper(string host, string community, SnmpSharpNet.SnmpVersion ver = SnmpSharpNet.SnmpVersion.Ver2, uint port = 161) {
            this._host = host;
            this._ver = ver;
            this._port = port;
            this._community = community;

            System.Net.IPAddress ip = null;
            if (!System.Net.IPAddress.TryParse(host, out ip))
                ip = System.Net.Dns.GetHostAddresses(host).FirstOrDefault();

            _snmp = new SnmpSharpNet.SimpleSnmp(ip.ToString(), (int)port, community, SNMP_TIMEOUT, SNMP_RETRY);
            _snmp.SuppressExceptions = false;
            if (!_snmp.Valid)
                throw new Exception("Invalid SNMP parameters");
        }

        public struct WellKnownOids {
            public const string OID_IF_INDEX = ".1.3.6.1.2.1.2.2.1.1";
            public const string OID_IF_DESCRIPTION = ".1.3.6.1.2.1.2.2.1.2";
            public const string OID_IF_SPEED = ".1.3.6.1.2.1.2.2.1.5";
            public const string OID_IF_MAC = ".1.3.6.1.2.1.2.2.1.6";
            public const string OID_IF_OUT_OCTET = ".1.3.6.1.2.1.2.2.1.16";
            public const string OID_IF_IN_OCTET = ".1.3.6.1.2.1.2.2.1.10";
            public const string OID_IF_IP = ".1.3.6.1.2.1.4.20.1.2";
            public const string OID_SYSNAME = ".1.3.6.1.2.1.1.5.0";
            public const string OID_SYSDESCR = ".1.3.6.1.2.1.1.1.0";
            public const string OID_IF_ALIAS = ".1.3.6.1.2.1.31.1.1.1.18";
        }

        public async Task<UInt64[]> GetCounters(int ifIndex, CancellationToken cancelToken) {

            var oid_in = string.Format("{0}.{1}", WellKnownOids.OID_IF_IN_OCTET, ifIndex);
            var oid_out = string.Format("{0}.{1}", WellKnownOids.OID_IF_OUT_OCTET, ifIndex);

            return await GetCounters(oid_in, oid_out);

        }
        public async Task<UInt64[]> GetCounters(int ifIndex) {
            return await GetCounters(ifIndex, new CancellationToken());
        }

        public async Task<UInt64[]> GetCounters(string oidIn, string oidOut) {
            return await GetCounters(oidIn, oidOut, new CancellationToken());
        }

        public async Task<UInt64[]> GetCounters(string oidIn, string oidOut, CancellationToken cancelToken) {
            Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> results = null;

            results = await Task.Run(() => _snmp.Get(_ver, new[] { oidIn, oidOut }), cancelToken);
            cancelToken.ThrowIfCancellationRequested();

            if (results == null)
                throw new Exception("Network error");

            foreach (var result in results)
                if (result.Value is SnmpSharpNet.V2Error)
                    throw new Exception(((SnmpSharpNet.V2Error)result.Value).ToString());

            return new UInt64[] { UInt64.Parse(results[new SnmpSharpNet.Oid(oidIn)].ToString()), UInt64.Parse(results[new SnmpSharpNet.Oid(oidOut)].ToString()) };

        }
        
        // Remove unreadable chars form data and convert hexa-encoded strings 
        // (seen on Windows SNMP service)
        private string FilterReceivedString(string data) {
            // Remove unreadable chars
            var datachars = data.Where(c => c >= 32).ToArray();
            data = string.Join("", datachars);

            // Convert hexa
            var mc = Regex.Match(data, @"^(?<code>[0-9A-F]{2}( |$))+$");
            if (mc.Success) {
                var sb = new StringBuilder();
                foreach (Capture hex in mc.Groups["code"].Captures)
                    sb.Append((char)Convert.ToUInt32(hex.Value.Trim(), 16));
                data = sb.ToString();
            }

            return data;
        }

        public async Task<InterfaceInfos[]> GetInterfacesInfos() {
            var descs = await Task.Run(() => _snmp.Walk(_ver, WellKnownOids.OID_IF_DESCRIPTION));
            var ips = await Task.Run(() => _snmp.Walk(_ver, WellKnownOids.OID_IF_IP));
            var aliases = await Task.Run(() => _snmp.Walk(_ver, WellKnownOids.OID_IF_ALIAS));

            var result = descs.Select(d => new InterfaceInfos() {
                Index = d.Key.Last(),
                Description = FilterReceivedString(d.Value.ToString()),
                Ips = new List<string>()
            }).ToArray();

            foreach (var ip in ips) {
                var itf = result.FirstOrDefault(r => r.Index == uint.Parse(ip.Value.ToString()));
                if (itf != null)
                    itf.Ips.Add(OidToIp(ip.Key));
            }

            foreach (var al in aliases) {
                var itf = result.FirstOrDefault(r => r.Index == al.Key.Last());
                if (itf != null)
                    itf.Alias = FilterReceivedString(al.Value.ToString());
            }                
            
            return result;
        }

        public string OidToIp(SnmpSharpNet.Oid oid) {
            var match = Regex.Match(oid.ToString(), @"((\.[0-9]+){4})$");
            if (!match.Success)
                return "";
            return match.Groups[1].Value.Trim('.');
        }

        public class InterfaceInfos {
            public uint Index { get; set; }
            public string Description { get; set; }
            public List<string> Ips { get; set; }
            public string Alias { get; set; }

            public override string ToString() {
                return string.Format("[{0}] {2}{3}{4} : {1}", 
                    this.Index, 
                    this.Ips.Count > 0 ? string.Join(", ", this.Ips) :  "<no ip>", 
                    string.IsNullOrWhiteSpace(this.Description) ? "no_name" : 
                    this.Description, string.IsNullOrWhiteSpace(this.Alias) ? "" : " - ", 
                    this.Alias).Trim();
            }
        } 
    }
}
