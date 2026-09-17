using System;
using System.Collections.Generic;
using NSOKHODO.Client;

namespace NSOKHODO.Kho
{
    /// <summary>
    /// KENH CHAT RA (SPEC §9). Moi tin gui di: bo dau (D46) + tem @NNN (D18) + cat 100 ky tu.
    ///
    /// <list type="bullet">
    /// <item>Chat rieng: moi bot toi da 1 tin / 3 giay (san cua KichYenCaller NSOLITEPRO).</item>
    /// <item>Chat cong dong: chi Leader; su kien chen truoc tin rao; hai tin cach nhau &gt;= RaoNhipGiay.</item>
    /// <item>Rao thong minh (D31): 5 giay/lan khi co nguoi ngoai hoac so lieu vua doi, con lai 300 giay/lan.</item>
    /// </list>
    /// Moi ham goi duoc tu moi luong; viec GUI that chi xay ra trong <see cref="Xa"/> (luong dieu phoi).
    /// </summary>
    public sealed class KenhChat
    {
        public const int SAN_RIENG_MS = 3000;
        public const int DO_DAI_TOI_DA = 100;
        private const int SAN_CONG_DONG_TOI_THIEU_MS = 3000;

        private sealed class TinRieng { public string Den; public string NoiDung; }

        private readonly object _lk = new object();
        private readonly Dictionary<NsoClient, Queue<TinRieng>> _rieng = new Dictionary<NsoClient, Queue<TinRieng>>();
        private readonly Dictionary<NsoClient, DateTime> _riengLuc = new Dictionary<NsoClient, DateTime>();
        private readonly Queue<string> _suKien = new Queue<string>();
        private readonly Dictionary<string, DateTime> _hanChe = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private DateTime _congDongLuc = DateTime.MinValue;
        private DateTime _raoLuc = DateTime.MinValue;

        public int SoTinRao { get; private set; }

        /// <summary>
        /// Xep hang mot tin rieng. <paramref name="khoaHanChe"/> khac null thi cung khoa chi gui toi da
        /// mot lan moi <paramref name="hanCheGiay"/> giay (vd "dang don kho" cho cung mot nguoi).
        /// Tra false neu bi han che.
        /// </summary>
        public bool GuiRieng(NsoClient bot, string den, string noiDung, string khoaHanChe = null, int hanCheGiay = 0)
        {
            if (bot == null || string.IsNullOrEmpty(den) || string.IsNullOrEmpty(noiDung)) return false;
            lock (_lk)
            {
                if (!QuaHanChe(khoaHanChe == null ? null : den + "|" + khoaHanChe, hanCheGiay)) return false;
                Queue<TinRieng> q;
                if (!_rieng.TryGetValue(bot, out q)) { q = new Queue<TinRieng>(); _rieng[bot] = q; }
                if (q.Count > 50) q.Dequeue();   // bot mat ket noi lau: dung de hang phinh
                q.Enqueue(new TinRieng { Den = den, NoiDung = noiDung });
                return true;
            }
        }

        /// <summary>Xep hang mot tin su kien cho chat cong dong (Leader se gui).</summary>
        public void GuiCongDong(string noiDung, string khoaHanChe = null, int hanCheGiay = 0)
        {
            if (string.IsNullOrEmpty(noiDung)) return;
            lock (_lk)
            {
                if (!QuaHanChe(khoaHanChe == null ? null : "*|" + khoaHanChe, hanCheGiay)) return;
                if (_suKien.Count > 30) _suKien.Dequeue();
                _suKien.Enqueue(noiDung);
            }
        }

        public void XoaHangCua(NsoClient bot)
        {
            lock (_lk) { _rieng.Remove(bot); _riengLuc.Remove(bot); }
        }

        /// <summary>
        /// Gui nhung tin den han. <paramref name="noiDungRao"/> null = khong rao lan nay.
        /// <paramref name="coNguoiNgoai"/>/<paramref name="soLieuVuaDoi"/>: dieu kien rao thong minh.
        /// </summary>
        public void Xa(NsoClient leader, bool leaderOKhuChinh, KhoConfig cfg, string noiDungRao,
                       bool coNguoiNgoai, bool soLieuVuaDoi, DateTime bayGio)
        {
            // ---- rieng ----
            var gui = new List<KeyValuePair<NsoClient, TinRieng>>();
            lock (_lk)
            {
                foreach (var kv in _rieng)
                {
                    if (kv.Value.Count == 0) continue;
                    var bot = kv.Key;
                    if (bot.State != ClientState.InGame) continue;
                    DateTime luc;
                    if (_riengLuc.TryGetValue(bot, out luc) && (bayGio - luc).TotalMilliseconds < SAN_RIENG_MS) continue;
                    gui.Add(new KeyValuePair<NsoClient, TinRieng>(bot, kv.Value.Dequeue()));
                    _riengLuc[bot] = bayGio;
                }
            }
            foreach (var g in gui)
            {
                string tin = ChuVan.ChuanBiTinGui(g.Value.NoiDung, DO_DAI_TOI_DA);
                try
                {
                    g.Key.Chat.SendPrivateChat(g.Value.Den, tin);
                    NhatKy.Chat(g.Key.Config.Username, false, false, g.Value.Den, tin);
                }
                catch (Exception ex) { NhatKy.App(g.Key.Config.Username, "Chat", "Gui chat rieng loi: " + ex.Message); }
            }

            // ---- cong dong (chi Leader, chi khi dang o khu chinh) ----
            if (leader == null || !leaderOKhuChinh || leader.State != ClientState.InGame) return;
            int gap = Math.Max(SAN_CONG_DONG_TOI_THIEU_MS, cfg.RaoNhipGiay * 1000);
            string guiCd = null;
            lock (_lk)
            {
                if ((bayGio - _congDongLuc).TotalMilliseconds < gap) return;
                if (_suKien.Count > 0)
                {
                    guiCd = _suKien.Dequeue();
                }
                else if (cfg.RaoBat && !string.IsNullOrEmpty(noiDungRao))
                {
                    int nhip = cfg.RaoCheDo == CheDoRao.Deu || coNguoiNgoai || soLieuVuaDoi
                        ? Math.Max(1, cfg.RaoNhipGiay) : Math.Max(cfg.RaoNhipGiay, cfg.RaoVangGiay);
                    if ((bayGio - _raoLuc).TotalSeconds >= nhip)
                    {
                        guiCd = noiDungRao;
                        _raoLuc = bayGio;
                        SoTinRao++;
                    }
                }
                if (guiCd != null) _congDongLuc = bayGio;
            }
            if (guiCd == null) return;
            string t = ChuVan.ChuanBiTinGui(guiCd, DO_DAI_TOI_DA);
            try
            {
                leader.Chat.SendPublicChat(t);
                NhatKy.Chat(leader.Config.Username, true, false, "", t);
            }
            catch (Exception ex) { NhatKy.App(leader.Config.Username, "Chat", "Gui chat cong dong loi: " + ex.Message); }
        }

        private bool QuaHanChe(string khoa, int giay)
        {
            if (khoa == null || giay <= 0) return true;
            DateTime luc;
            var now = DateTime.UtcNow;
            if (_hanChe.TryGetValue(khoa, out luc) && (now - luc).TotalSeconds < giay) return false;
            _hanChe[khoa] = now;
            if (_hanChe.Count > 2000)
            {
                var bo = new List<string>();
                foreach (var kv in _hanChe) if ((now - kv.Value).TotalHours > 1) bo.Add(kv.Key);
                foreach (var k in bo) _hanChe.Remove(k);
            }
            return true;
        }
    }
}
