using System;
using System.Collections.Generic;
using System.Threading;
using NSOKHODO.Client;

namespace NSOKHODO.Fleet
{
    /// <summary>
    /// Quan ly "ham" nhieu account: so huu tap NsoClient + vong doi (start/stop/find/dispose),
    /// staggered start (tranh dang nhap dong loat lam nghen server), gop log + dem online.
    ///
    /// Tach khoi MainForm de UI chi con lo grid; logic dieu phoi nhieu account nam o day.
    /// Thread-safe cho danh sach client; KHONG dung UI (khong cham DataGridView).
    /// </summary>
    public class FleetManager : IDisposable
    {
        private readonly List<NsoClient> _clients = new List<NsoClient>();
        private readonly object _lock = new object();

        // Staggered start
        private readonly Queue<AccountConfig> _startQueue = new Queue<AccountConfig>();
        private Timer _staggerTimer;
        private int _staggerDelayMs = 2000;
        private int _staggerBatch = 1;   // so account mo cung luc moi luot (1 = luong cu)

        /// <summary>Log gop tu tat ca client (da kem [username] prefix tu NsoClient.Log).</summary>
        public event Action<string> OnLog;

        public FleetManager()
        {
            // Dong bao "proxy X dang hong" la chuyen CUA CA HAM, khong thuoc acc nao - nen di thang
            // vao log gop chu khong mang tien to [username] cua mot acc xui xeo bat ky.
            ProxyHealth.OnLog = RaiseLog;
        }

        /// <summary>Bao mot client vua duoc tao + start (de UI cap nhat trang thai neu can).</summary>
        public event Action<NsoClient> OnClientStarted;

        public int Count { get { lock (_lock) return _clients.Count; } }

        public int OnlineCount
        {
            get
            {
                lock (_lock)
                {
                    int n = 0;
                    foreach (var c in _clients)
                        if (c.IsOnline) n++;
                    return n;
                }
            }
        }

        /// <summary>Snapshot danh sach client hien tai (an toan de duyet).</summary>
        public List<NsoClient> Clients
        {
            get { lock (_lock) return new List<NsoClient>(_clients); }
        }

        public NsoClient Find(AccountConfig acc)
        {
            lock (_lock)
            {
                foreach (var c in _clients)
                    if (c.Config == acc) return c;
            }
            return null;
        }

        /// <summary>
        /// Bang tra account -> client, dung 1 lan lay khoa.
        /// Dung cho vong lap duyet nhieu dong (UI refresh): goi Find() cho tung dong la O(N) x N
        /// - voi 600 account la 360.000 phep so sanh + 600 lan lay khoa MOI GIAY tren UI thread,
        /// va khoa nay dung chung voi StartAccount/StopAll nen con tranh khoa voi thread mang.
        /// AccountConfig khong override Equals/GetHashCode -> Dictionary so sanh theo tham chieu,
        /// dung ngu nghia voi Find() (c.Config == acc).
        /// </summary>
        public Dictionary<AccountConfig, NsoClient> SnapshotByAccount()
        {
            lock (_lock)
            {
                var map = new Dictionary<AccountConfig, NsoClient>(_clients.Count);
                foreach (var c in _clients)
                    if (c.Config != null) map[c.Config] = c;
                return map;
            }
        }

        /// <summary>
        /// Tim client dang chay theo TEN NHAN VAT — cho Kich Yen giao loi goi noi bo.
        ///
        /// <b>Bat buoc khop CA <paramref name="serverIndex"/>:</b> fleet chay duoc nhieu server
        /// cung luc, ma hai nhan vat KHAC SERVER hoan toan co the TRUNG TEN -> chi so ten se giao
        /// nham sang acc o server khac, noi con quai duoc bao khong ton tai.
        ///
        /// Ten doi chieu la <c>NsoClient.DisplayCharName</c> (uu tien ten THAT do server gan;
        /// <c>Config.CharName</c> chi la o user go va co the rong vinh vien).
        /// </summary>
        public NsoClient FindByCharName(int serverIndex, string charName)
        {
            if (string.IsNullOrEmpty(charName)) return null;
            string want = charName.Trim();
            lock (_lock)
            {
                foreach (var c in _clients)
                {
                    if (c.Config == null || c.Config.ServerIndex != serverIndex) continue;
                    if (string.Equals(c.DisplayCharName, want, StringComparison.OrdinalIgnoreCase)) return c;
                }
            }
            return null;
        }

        // ==================== START / STOP ====================

        /// <summary>
        /// Tao + start client cho 1 account. Neu da co client dang chay (khac Disconnected/Error)
        /// thi giu nguyen. Tra ve client (moi hoac dang ton tai).
        /// </summary>
        public NsoClient StartAccount(AccountConfig acc)
        {
            NsoClient existing = Find(acc);
            if (existing != null &&
                existing.State != ClientState.Disconnected &&
                existing.State != ClientState.Error)
                return existing;

            if (existing != null)
            {
                // Stop() truoc khi go: client cu co the dang XEP HANG o cong gioi han login theo IP
                // (State van la Disconnected nen loc o tren khong bat duoc). Go khong Stop thi timer
                // cho cua no van song, mot luc sau tu dong login -> hai client cung mot account.
                // Voi client Disconnected/Error thuc su thi Stop() la no-op vo hai.
                try { existing.Stop(); } catch { }
                lock (_lock) _clients.Remove(existing);
            }

            var client = new NsoClient(acc);
            client.Fleet = this;          // de client biet doi quan dang so huu no
            client.OnLog += RaiseLog;
            lock (_lock) _clients.Add(client);

            client.Start();

            var h = OnClientStarted;
            if (h != null) h(client);
            return client;
        }

        /// <summary>
        /// Start nhieu account voi staggered delay. Batch nho (&lt;=3) start ngay;
        /// batch lon start dan tung con cach nhau StaggerDelayMs de khong nghen server.
        ///
        /// <paramref name="batchSize"/> = so account mo CUNG LUC moi luot (mac dinh 1 = luong cu
        /// tung con mot). &gt;1 = tinh nang "Mo cung luc" tren UI: moi luot mo N con, cac luot van
        /// cach nhau delayMs.
        /// </summary>
        public void StartStaggered(IEnumerable<AccountConfig> accounts, int delayMs = 2000, int batchSize = 1)
        {
            var list = new List<AccountConfig>(accounts);
            if (list.Count == 0) return;

            _staggerDelayMs = delayMs > 0 ? delayMs : 2000;
            _staggerBatch = batchSize > 0 ? batchSize : 1;

            // Luong cu giu nguyen: batch = 1 va <=3 account -> mo ngay khong cho.
            if (list.Count <= _staggerBatch || (_staggerBatch == 1 && list.Count <= 3))
            {
                foreach (var acc in list) StartAccount(acc);
                return;
            }

            // Mo luot dau ngay (batch con), phan con lai day vao queue cho timer
            int first = Math.Min(_staggerBatch, list.Count);
            for (int i = 0; i < first; i++) StartAccount(list[i]);
            lock (_lock)
            {
                _startQueue.Clear();
                for (int i = first; i < list.Count; i++) _startQueue.Enqueue(list[i]);
            }
            RaiseLog(_staggerBatch > 1
                ? string.Format("[System] Starting {0} accounts ({1} cung luc / {2}s)...",
                    list.Count, _staggerBatch, _staggerDelayMs / 1000)
                : string.Format("[System] Starting {0} accounts ({1}s interval)...",
                    list.Count, _staggerDelayMs / 1000));

            EnsureStaggerTimer();
            _staggerTimer.Change(_staggerDelayMs, _staggerDelayMs);
        }

        private void EnsureStaggerTimer()
        {
            if (_staggerTimer == null)
                _staggerTimer = new Timer(StaggerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void StaggerTick(object state)
        {
            // Lay ca LUOT (batch) trong 1 lan khoa; batch = 1 la dung hanh vi cu (1 con/luot).
            var batch = new List<AccountConfig>();
            lock (_lock)
            {
                int take = _staggerBatch > 0 ? _staggerBatch : 1;
                while (batch.Count < take && _startQueue.Count > 0) batch.Add(_startQueue.Dequeue());
                if (_startQueue.Count == 0 && _staggerTimer != null)
                    _staggerTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            foreach (var acc in batch)
            {
                try { StartAccount(acc); }
                catch (Exception ex) { RaiseLog("[System] Stagger start loi: " + ex.Message); }
            }
        }

        public void StopStagger()
        {
            lock (_lock)
            {
                _startQueue.Clear();
                if (_staggerTimer != null)
                    _staggerTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>Stop + go client cua 1 account khoi ham.</summary>
        public void StopAccount(AccountConfig acc)
        {
            NsoClient client = Find(acc);
            if (client == null) return;
            client.Stop();
            lock (_lock) _clients.Remove(client);
        }

        public void StopAll()
        {
            StopStagger();
            List<NsoClient> snapshot;
            lock (_lock)
            {
                snapshot = new List<NsoClient>(_clients);
                _clients.Clear();
            }
            foreach (var client in snapshot) client.Stop();
        }

        private void RaiseLog(string msg)
        {
            var h = OnLog;
            if (h != null) h(msg);
        }

        public void Dispose()
        {
            StopStagger();
            List<NsoClient> snapshot;
            lock (_lock)
            {
                snapshot = new List<NsoClient>(_clients);
                _clients.Clear();
            }
            foreach (var client in snapshot)
            {
                try { client.Dispose(); } catch { }
            }
            if (_staggerTimer != null)
            {
                _staggerTimer.Dispose();
                _staggerTimer = null;
            }
        }
    }
}
