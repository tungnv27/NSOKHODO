using System;
using System.Threading;
using NSOKHODO.Client;
using NSOKHODO.Models;

namespace NSOKHODO.Auto
{
    /// <summary>
    /// Base cho cac che do tu dong: quan ly vong doi thread + vong lap tick + bat loi.
    /// Subclass chi can implement <see cref="Tick"/> (lam 1 buoc, tra ve so ms ngu truoc tick ke).
    /// </summary>
    public abstract class AutoModeBase : IAutoMode
    {
        protected readonly NsoClient Client;
        protected volatile bool Stopped;
        private Thread _thread;

        // NSOKHODO: KHONG co "uong binh HP/MP" (BinhMauRunner cua NSOBAOTATL da bo). Bot kho khong
        // bao gio dung mon nao - binh trong tui co the la DO GUI KHO, va do chi bi khoa khi dem dung
        // (test tay T0, SPEC D38). Nhan vat dung o lang nen khong bi quai danh.

        protected AutoModeBase(NsoClient client)
        {
            Client = client;
            StartupDelayOverrideMs = -1;
        }

        public abstract string Name { get; }

        /// <summary>
        /// Nhan "dang dung cho, san sang". Dung chung cho mode kho (SetActivity) va bo dem o dong
        /// status (<c>NsoClient.IsFarming</c>) - doi chu o day thi ca hai cung doi, khong le nhau.
        /// </summary>
        public const string ACT_DUNG_CANH = "Sẵn sàng";

        /// <summary>Nhan hoat dong hien tai cho cot Trang thai. Subclass set qua <see cref="SetActivity"/>.</summary>
        public string Activity { get; private set; }

        /// <summary>Cap nhat nhan hoat dong (thread-safe du chi la gan tham chieu string).</summary>
        protected void SetActivity(string activity) { Activity = activity; }

        /// <summary>Ten thread (de debug).</summary>
        protected virtual string ThreadName { get { return "NSO-" + Name; } }

        /// <summary>Delay truoc tick dau tien (cho game settle sau InGame).</summary>
        protected virtual int StartupDelayMs { get { return 2000; } }

        /// <summary>
        /// San nhip giua HAI LAN BAT DAU tick (ms) - 0 = khong san. Ton tai cho "Han che 900s"
        /// (<c>TrainConfig.HanCheBan</c>): bot Java Auto30 chi cho moi buoc train cach nhau &gt;= 150 ms
        /// (`Class_cw.java:38/93/158`). Dat o vong lap (mot cho) thay vi sua hang chuc lenh return
        /// trong TrainMode.Tick.
        /// </summary>
        protected virtual int SanNhipTickMs { get { return 0; } }

        /// <summary>
        /// Ghi de <see cref="StartupDelayMs"/> cho DUNG mot lan Start (-1 = dung mac dinh).
        /// Phai dat TRUOC khi goi <see cref="Start"/>.
        /// </summary>
        /// <remarks>
        /// 2000ms mac dinh la de CHO DU LIEU VE sau khi vua vao game: map/skill/hanh trang deu
        /// den bang packet roi rac sau MAP_INFO dau tien. Sau HOI SINH thi khong con gi de cho -
        /// mode chi bi huy va tao lai (NsoClient.StopAutoSystems/StartAutoSystems), du lieu van
        /// nam nguyen trong GameStateManager - nen cho them 2 giay la thoi gian chet thuan tuy.
        ///
        /// Do duoc tren log 2026-09-05: chet -> "[TanSat] Di toi map" trung vi 3 giay, TOI THIEU
        /// 2 giay; nghia la 2/3 quang do la dong SafeSleep nay. NSOTRUNGDUC khong co khoang nay:
        /// vong auto cua no KHONG bi huy khi chet, tick ke tiep (100ms sau) da di ve bai.
        /// </remarks>
        public int StartupDelayOverrideMs { get; set; }

        public virtual void Start()
        {
            Stopped = false;
            _lastHeartbeatAt = DateTime.UtcNow; // bat dau dem tu luc mode start (khong danh ngay khi vao)
            // Stack 256 KB - cung ly do voi hai luong socket o NsoSession: moi account mot luong,
            // chay 1.800 account thi phan giu cho vung dia chi moi la thu cham tran truoc tien.
            // Tick() cua mode kho khong de quy, nhanh sau nhat la Navigator (vai chuc khung).
            _thread = new Thread(Run, 256 * 1024) { IsBackground = true, Name = ThreadName };
            _thread.Start();
        }

        public virtual void Stop() { Stopped = true; }

        /// <summary>
        /// Mac dinh FALSE = "mode nay luon co viec de lam", tuc bo do im lang duoc phep ket luan.
        /// Mode nao dung im la HOP LE (Thua loi dai, Cho PK, Buff, Danh Vong) thi ghi de thanh true -
        /// xem <see cref="IAutoMode.DungImHopLe"/>.
        /// </summary>
        public virtual bool DungImHopLe { get { return false; } }

        public virtual void OnBeforeDeath() { }

        // ---- Heartbeat giu ket noi (clone MODGAME Class_bw.p @2090) ----
        // MODGAME: moi 60s gui 1 goi ATTACK voi target = CHINH MINH (Class_ba.f()) -> tao luu luong
        // client->server de server KHONG kick idle khi dung im.
        // Tren server nay danh nguoi = cmd 61 (SendAttackChar) voi list int charId -> tu danh minh =
        // SendAttackChar([myCharId]). KHONG gay sat thuong, KHONG dung skill, KHONG bat co PK (day la
        // goi ATTACK, khong phai changePk). Type=2 cua MODGAME chi la fire-type, khong lien quan PK.
        //
        // Truoc 2026-09-13 nam o PkModeBase (chi Cho PK / Danh PK co). Dua len base de Buff va
        // "Chi dung im" cua Kich yen (cong KyDungIm trong TrainMode) goi chung MOT ban.
        // Gui xong LastAttackAt tuoi lai - vo hai: TickImLang + TickSongGia deu return som khi
        // NsoClient.DungImHopLe, ma ca 4 noi goi deu bat co do.
        private const int HEARTBEAT_INTERVAL_MS = 60000;
        private DateTime _lastHeartbeatAt = DateTime.MinValue;

        /// <summary>
        /// Giu ket noi khi dung im: moi 60s tu "danh chinh minh" (cmd 61 voi charId ban than) de server
        /// khong kick idle. Goi o DAU Tick (sau guard song/trong-game) cua mode dung im: Cho PK, Danh PK,
        /// Buff, "Chi dung im" (Kich yen). Tu danh KHONG gay sat thuong / KHONG bat co PK.
        /// </summary>
        protected void Heartbeat(CharacterState mc)
        {
            if (mc == null || mc.CharId <= 0) return;
            if ((DateTime.UtcNow - _lastHeartbeatAt).TotalMilliseconds < HEARTBEAT_INTERVAL_MS) return;
            Client.Combat.SendAttackChar(new int[] { mc.CharId });
            _lastHeartbeatAt = DateTime.UtcNow;
        }

        private void Run()
        {
            SafeSleep(StartupDelayOverrideMs >= 0 ? StartupDelayOverrideMs : StartupDelayMs);
            while (!Stopped && Client.State == ClientState.InGame)
            {
                int sleepMs;
                // Nhip = KHOANG CACH giua 2 tick, khong phai "ngu them sau khi lam xong".
                // Truoc day ngu tron sleepMs SAU khi Tick() da ton 20-40ms (nhat do co Sleep(40)/mon,
                // burst move, quet mob) -> TickMs=50 ma nhip that troi ve 70-90ms (~12-14 Hz thay vi 20 Hz).
                // Clone MODGAME NSOT_MOB.java:1047: sleep(50 - elapsed) - tru thoi gian xu ly.
                var startedAt = DateTime.UtcNow;
                try
                {
                    sleepMs = Tick();

                    int san = SanNhipTickMs;
                    if (sleepMs < san) sleepMs = san;
                }
                catch (Exception ex)
                {
                    Client.Log("[" + Name + "] Loi: " + ex.Message);
                    sleepMs = 5000;
                }
                double elapsed = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                int remain = sleepMs - (int)elapsed;
                SafeSleep(remain > 0 ? remain : 0);
            }
        }

        /// <summary>Mot buoc cong viec. Tra ve so ms can ngu truoc khi tick lai (0 = tick ngay).</summary>
        protected abstract int Tick();

        protected void SafeSleep(int ms)
        {
            int s = 0;
            while (s < ms && !Stopped) { Thread.Sleep(Math.Min(100, ms - s)); s += 100; }
        }
    }
}
