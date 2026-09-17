using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NSOKHODO.Client;
using NSOKHODO.Config;
using NSOKHODO.Models;

namespace NSOKHODO.Kho
{
    /// <summary>
    /// Khoa gop mon trong so kho: (template, cap nang, khoa). SPEC §8.
    /// Ghi theo NOI DUNG, khong giu tham chieu Item - sub 115 dung lai toan bo object ~20 giay/lan.
    /// <para><b>CO HAN (<see cref="Han"/>) KHONG thuoc khoa</b> (D72, test song 16/09): server bao co nay KHAC
    /// NHAU cho CUNG mot mon - khung giao dich + cmd 8 ghi 4/5 Tu tinh thach "khong han", dang nhap lai
    /// (danh sach tui) ghi ca 5 "co han". Tinh vao khoa thi mot mon nhay qua lai hai dong, ke hoach giao
    /// tro vao khoa clone khong con khop -> "khong du hang". Han chi con la thong tin hien thi.</para>
    /// <para><b>CAP CUA THU CUOI (type 29..33) cung KHONG thuoc khoa</b> (D80): client chinh chu 251 chi doc
    /// byte cap cua thu cuoi trong danh sach tui day du (sub 115); ruong (cmd 31), cmd 8 va khung giao dich
    /// (45) thi khong co byte nay -> cung mot con thu cuoi luc o tui la "tpl+N", cat vao ruong la "tpl".
    /// Cap luon ghi 0 cho thu cuoi.</para>
    /// </summary>
    public struct KhoaMon : IEquatable<KhoaMon>
    {
        public short Tpl;
        public byte Up;
        public bool Han;
        public bool Khoa;

        public KhoaMon(short tpl, byte up, bool han, bool khoa)
        {
            Tpl = tpl; Up = LaThuCuoi(tpl) ? (byte)0 : up; Han = han; Khoa = khoa;
        }

        public static KhoaMon Tu(Item it)
        {
            return new KhoaMon(it.TemplateId, it.Upgrade, it.IsExpires, it.IsLock);
        }

        public bool Khop(Item it)
        {
            return it != null && !it.IsEmpty && it.TemplateId == Tpl && it.IsLock == Khoa
                   && (it.Upgrade == Up || LaThuCuoi(Tpl));
        }

        /// <summary>Template chua biet (bang mon rong) -> coi nhu khong phai thu cuoi.</summary>
        public static bool LaThuCuoi(short tpl)
        {
            var t = BangMon.Lay(tpl);
            return t != null && t.IsTypeMount;
        }

        public bool Equals(KhoaMon o) { return Tpl == o.Tpl && Up == o.Up && Khoa == o.Khoa; }
        public override bool Equals(object obj) { return obj is KhoaMon && Equals((KhoaMon)obj); }
        public override int GetHashCode() { return (Tpl * 397) ^ (Up << 2) ^ (Khoa ? 2 : 0); }

        public override string ToString()
        {
            return Tpl.ToString(CultureInfo.InvariantCulture) + (Up > 0 ? "+" + Up : "") + (Han ? "h" : "") + (Khoa ? "k" : "");
        }

        public static bool TryParse(string s, out KhoaMon k)
        {
            k = new KhoaMon();
            if (string.IsNullOrEmpty(s)) return false;
            bool han = false, khoa = false;
            while (s.Length > 0 && (s[s.Length - 1] == 'h' || s[s.Length - 1] == 'k'))
            {
                if (s[s.Length - 1] == 'h') han = true; else khoa = true;
                s = s.Substring(0, s.Length - 1);
            }
            byte up = 0;
            int plus = s.IndexOf('+');
            if (plus > 0)
            {
                if (!byte.TryParse(s.Substring(plus + 1), out up)) return false;
                s = s.Substring(0, plus);
            }
            short tpl;
            if (!short.TryParse(s, out tpl)) return false;
            k = new KhoaMon(tpl, up, han, khoa);
            return true;
        }
    }

    /// <summary>Mot o chua do cua mot acc (tui hoac ruong).</summary>
    public sealed class OMon
    {
        public bool TrongRuong;
        public int Slot;
        public KhoaMon Khoa;
        public int SoLuong;
    }

    /// <summary>Anh chup kho do cua MOT acc.</summary>
    public sealed class TuiAcc
    {
        public string Acc;
        public string TenNV;
        public bool Online;
        public DateTime ThayLuc;
        public int Xu;
        public int SoOTui;
        public int SoORuong = -1;        // -1 = chua tung doc ruong
        public DateTime DocRuongLuc;
        public List<OMon> Mon = new List<OMon>();

        public int TuiDung
        {
            get { int n = 0; foreach (var m in Mon) if (!m.TrongRuong) n++; return n; }
        }

        public int RuongDung
        {
            get { int n = 0; foreach (var m in Mon) if (m.TrongRuong) n++; return n; }
        }

        public int TuiTrong { get { return Math.Max(0, SoOTui - TuiDung); } }
        public int RuongTrong { get { return SoORuong < 0 ? 0 : Math.Max(0, SoORuong - RuongDung); } }

        public int SoLuong(KhoaMon k)
        {
            int n = 0;
            foreach (var m in Mon) if (m.Khoa.Equals(k)) n += m.SoLuong;
            return n;
        }

        public int SoLuong(KhoaMon k, bool trongRuong)
        {
            int n = 0;
            foreach (var m in Mon) if (m.TrongRuong == trongRuong && m.Khoa.Equals(k)) n += m.SoLuong;
            return n;
        }

        public bool CoTpl(short tpl)
        {
            foreach (var m in Mon) if (m.Khoa.Tpl == tpl) return true;
            return false;
        }
    }

    /// <summary>Mot dong cua tong kho (da gop moi acc).</summary>
    public sealed class TonMon
    {
        public KhoaMon Khoa;
        public string Ten;
        public string Nhom;
        public int Tong;
        public int Giu;
        public int SoO;
        public bool LaRac;
        public bool TheoDoi;
        public bool TrenAccNha;          // co mot phan nam tren clone dang nha
        public readonly Dictionary<string, int[]> PhanBo = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase); // acc -> [tui, ruong]

        public int KhaDung { get { return Khoa.Khoa ? 0 : Math.Max(0, Tong - Giu); } }

        // Bo nho dem cot "Phan bo" cua giao dien - CHI luong UI doc/ghi (bang da phat hanh khong doi PhanBo).
        public string UiPhanBo;
        public bool UiPhanBoAnTen;
    }

    /// <summary>Suc chua cho cau rao "70/360" (SPEC §8): chi tinh CLONE, bo Leader, du phong va ke Rac.</summary>
    public sealed class SucChua
    {
        public int Tong;
        public int Dung;
        public bool ChuaDu;   // co clone chua doc ruong -> con so thieu, cau rao them dau "~"
        public int Trong { get { return Math.Max(0, Tong - Dung); } }
    }

    /// <summary>
    /// SO KHO — so do cua moi acc (online: doc song; offline: lan cuoi thay, luu dia).
    /// <c>Data/Kho/&lt;danh sach&gt;.sokho.txt</c>.
    /// </summary>
    public sealed class SoKho
    {
        private readonly object _lk = new object();
        private readonly Dictionary<string, TuiAcc> _acc = new Dictionary<string, TuiAcc>(StringComparer.OrdinalIgnoreCase);
        private bool _canLuu;

        public static string DuongDan
        {
            get { return Path.Combine(Path.Combine(AppPaths.DataDir, "Kho"), AppPaths.SafeName(AppPaths.ListName) + ".sokho.txt"); }
        }

        /// <summary>
        /// Chep kho do cua mot client dang online. Ruong null (chua xin sau lan dang nhap nay) thi GIU
        /// ruong da biet tu truoc - ruong khong doi khi acc offline.
        /// </summary>
        public void CapNhat(NsoClient c)
        {
            if (c == null || c.Config == null) return;
            var mc = c.GameState.MyChar;
            if (mc == null || string.IsNullOrEmpty(mc.Name)) return;
            var bag = mc.BagItems;
            var box = mc.BoxItems;

            var t = new TuiAcc
            {
                Acc = c.Config.Username,
                TenNV = mc.Name,
                Online = true,
                ThayLuc = DateTime.Now,
                Xu = mc.Xu,
                SoOTui = bag != null ? bag.Length : 0,
            };
            if (bag != null)
                for (int i = 0; i < bag.Length; i++)
                {
                    var it = bag[i];
                    if (it == null || it.IsEmpty) continue;
                    t.Mon.Add(new OMon { TrongRuong = false, Slot = i, Khoa = KhoaMon.Tu(it), SoLuong = Math.Max(1, (int)it.Quantity) });
                }

            lock (_lk)
            {
                TuiAcc cu;
                _acc.TryGetValue(t.Acc, out cu);
                if (box != null)
                {
                    t.SoORuong = box.Length;
                    t.DocRuongLuc = DateTime.Now;
                    for (int i = 0; i < box.Length; i++)
                    {
                        var it = box[i];
                        if (it == null || it.IsEmpty) continue;
                        t.Mon.Add(new OMon { TrongRuong = true, Slot = i, Khoa = KhoaMon.Tu(it), SoLuong = Math.Max(1, (int)it.Quantity) });
                    }
                }
                else if (cu != null && cu.SoORuong >= 0)
                {
                    t.SoORuong = cu.SoORuong;
                    t.DocRuongLuc = cu.DocRuongLuc;
                    foreach (var m in cu.Mon) if (m.TrongRuong) t.Mon.Add(m);
                }
                if (cu == null || !GiongNhau(cu, t)) _canLuu = true;
                _acc[t.Acc] = t;
            }
        }

        /// <summary>Acc khong con online: giu so lieu cu, chi doi co.</summary>
        public void DanhDauOffline(string acc)
        {
            lock (_lk)
            {
                TuiAcc t;
                if (_acc.TryGetValue(acc, out t)) t.Online = false;
            }
        }

        public void Xoa(string acc)
        {
            lock (_lk) { if (_acc.Remove(acc)) _canLuu = true; }
        }

        public TuiAcc Lay(string acc)
        {
            TuiAcc t;
            lock (_lk) return acc != null && _acc.TryGetValue(acc, out t) ? t : null;
        }

        public List<TuiAcc> TatCa()
        {
            lock (_lk) return new List<TuiAcc>(_acc.Values);
        }

        /// <summary>
        /// Gop thanh bang tong kho. <paramref name="laKho"/> loc acc nao duoc tinh (clone + leader);
        /// <paramref name="daNha"/> danh dau acc dang nha (hang tren do khong rut duoc).
        /// </summary>
        public List<TonMon> Gop(Func<string, bool> laKho, Func<string, bool> daNha, KhoConfig cfg)
        {
            var bang = new Dictionary<KhoaMon, TonMon>();
            foreach (var t in TatCa())
            {
                if (laKho != null && !laKho(t.Acc)) continue;
                bool nha = daNha != null && daNha(t.Acc);
                foreach (var m in t.Mon)
                {
                    TonMon d;
                    if (!bang.TryGetValue(m.Khoa, out d))
                    {
                        var tpl = BangMon.Lay(m.Khoa.Tpl);
                        d = new TonMon
                        {
                            Khoa = m.Khoa,
                            Ten = BangMon.Ten(m.Khoa.Tpl),
                            Nhom = KeHang.NhomCuaMon(m.Khoa.Tpl, tpl, cfg),
                            LaRac = cfg != null && cfg.LaRac(m.Khoa.Tpl),
                        };
                        bang[m.Khoa] = d;
                    }
                    // Cot "Han": co mon nao duoc bao co han thi hien "co" (co khong on dinh - xem KhoaMon).
                    if (m.Khoa.Han && !d.Khoa.Han) d.Khoa = new KhoaMon(d.Khoa.Tpl, d.Khoa.Up, true, d.Khoa.Khoa);
                    if (nha) { d.TrenAccNha = true; continue; }
                    d.Tong += m.SoLuong;
                    d.SoO++;
                    int[] pb;
                    if (!d.PhanBo.TryGetValue(t.Acc, out pb)) { pb = new int[2]; d.PhanBo[t.Acc] = pb; }
                    pb[m.TrongRuong ? 1 : 0] += m.SoLuong;
                }
            }
            var r = new List<TonMon>(bang.Values);
            r.Sort((a, b) =>
            {
                int c = a.Khoa.Tpl.CompareTo(b.Khoa.Tpl);
                if (c != 0) return c;
                c = a.Khoa.Up.CompareTo(b.Khoa.Up);
                if (c != 0) return c;
                return a.Khoa.Khoa.CompareTo(b.Khoa.Khoa);
            });
            return r;
        }

        /// <summary>Suc chua theo cac acc <paramref name="tinh"/> (clone chua bi loai).</summary>
        public SucChua TinhSucChua(Func<string, bool> tinh)
        {
            var s = new SucChua();
            foreach (var t in TatCa())
            {
                if (tinh != null && !tinh(t.Acc)) continue;
                s.Tong += t.SoOTui;
                s.Dung += t.TuiDung;
                if (t.SoORuong >= 0)
                {
                    s.Tong += t.SoORuong;
                    s.Dung += t.RuongDung;
                }
                else s.ChuaDu = true;
            }
            return s;
        }

        // ================= luu / nap =================

        public void LuuNeuCan()
        {
            List<TuiAcc> ds;
            lock (_lk)
            {
                if (!_canLuu) return;
                _canLuu = false;
                ds = new List<TuiAcc>(_acc.Values);
            }
            try
            {
                var sb = new StringBuilder();
                sb.Append("# acc|ten|thayLuc|xu|oTui|oRuong|docRuongLuc|mon (T/R:slot:khoa:sl,...)\r\n");
                foreach (var t in ds)
                {
                    sb.Append(t.Acc).Append('|').Append((t.TenNV ?? "").Replace('|', '/')).Append('|')
                      .Append(t.ThayLuc.Ticks).Append('|').Append(t.Xu).Append('|').Append(t.SoOTui).Append('|')
                      .Append(t.SoORuong).Append('|').Append(t.DocRuongLuc.Ticks).Append('|');
                    bool dau = true;
                    foreach (var m in t.Mon)
                    {
                        if (!dau) sb.Append(',');
                        dau = false;
                        sb.Append(m.TrongRuong ? 'R' : 'T').Append(':').Append(m.Slot).Append(':')
                          .Append(m.Khoa.ToString()).Append(':').Append(m.SoLuong);
                    }
                    sb.Append("\r\n");
                }
                string p = DuongDan;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                string tmp = p + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(p)) File.Replace(tmp, p, null);
                else File.Move(tmp, p);
            }
            catch { lock (_lk) _canLuu = true; }
        }

        public void Nap()
        {
            try
            {
                if (!File.Exists(DuongDan)) return;
                foreach (var dong in File.ReadAllLines(DuongDan, Encoding.UTF8))
                {
                    if (dong.StartsWith("#")) continue;
                    var p = dong.Split('|');
                    if (p.Length < 8) continue;
                    var t = new TuiAcc { Acc = p[0], TenNV = p[1], Online = false };
                    long ticks; int n;
                    if (long.TryParse(p[2], out ticks)) t.ThayLuc = new DateTime(ticks);
                    if (int.TryParse(p[3], out n)) t.Xu = n;
                    if (int.TryParse(p[4], out n)) t.SoOTui = n;
                    if (int.TryParse(p[5], out n)) t.SoORuong = n;
                    if (long.TryParse(p[6], out ticks)) t.DocRuongLuc = new DateTime(ticks);
                    foreach (var mm in p[7].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var q = mm.Split(':');
                        KhoaMon k; int slot, sl;
                        if (q.Length == 4 && int.TryParse(q[1], out slot) && KhoaMon.TryParse(q[2], out k)
                            && int.TryParse(q[3], out sl))
                            t.Mon.Add(new OMon { TrongRuong = q[0] == "R", Slot = slot, Khoa = k, SoLuong = sl });
                    }
                    lock (_lk) { if (!_acc.ContainsKey(t.Acc)) _acc[t.Acc] = t; }
                }
            }
            catch { }
        }

        private static bool GiongNhau(TuiAcc a, TuiAcc b)
        {
            if (a.SoOTui != b.SoOTui || a.SoORuong != b.SoORuong || a.Xu != b.Xu || a.Mon.Count != b.Mon.Count)
                return false;
            for (int i = 0; i < a.Mon.Count; i++)
            {
                var x = a.Mon[i]; var y = b.Mon[i];
                if (x.TrongRuong != y.TrongRuong || x.Slot != y.Slot || x.SoLuong != y.SoLuong || !x.Khoa.Equals(y.Khoa))
                    return false;
            }
            return true;
        }
    }
}
