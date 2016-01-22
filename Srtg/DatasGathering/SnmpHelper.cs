using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Srtg.DatasGathering {
    public class SnmpHelper : SnmpSharpNet.SimpleSnmp, IDisposable {

        const int SNMP_TIMEOUT = 3000;
        const int SNMP_RETRY = 2;

        private SnmpSharpNet.SnmpVersion _ver;

        public SnmpHelper(string host, string community, SnmpSharpNet.SnmpVersion ver = SnmpSharpNet.SnmpVersion.Ver2, int port = 161)
            :base(host, port, community, SNMP_TIMEOUT, SNMP_RETRY){
            this._ver = ver;
            this._suppressExceptions = false;
            System.Net.IPAddress ip = null;
            if (!System.Net.IPAddress.TryParse(host, out ip))
                ip = System.Net.Dns.GetHostAddresses(host).FirstOrDefault();
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

            results = await Task.Run(() => this.Get(_ver, new[] { oidIn, oidOut }), cancelToken);
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

            Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> descs;
            Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> ips;
            Dictionary<SnmpSharpNet.Oid, SnmpSharpNet.AsnType> aliases;

            try {
                descs = await Task.Run(() => this.Walk(_ver, WellKnownOids.OID_IF_DESCRIPTION));
                ips = await Task.Run(() => this.Walk(_ver, WellKnownOids.OID_IF_IP));
                aliases = await Task.Run(() => this.Walk(_ver, WellKnownOids.OID_IF_ALIAS));
            }
            catch(System.NullReferenceException) {
                // Operation aborted
                throw new OperationCanceledException();
            }
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

        public void Dispose() {
            if (this._target != null)
                this._target.Dispose();
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
