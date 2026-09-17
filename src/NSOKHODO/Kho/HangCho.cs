using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NSOKHODO.Config;

namespace NSOKHODO.Kho
{
    public enum TrangThaiLenh { ChoCoMat = 0, DangGiao = 1, TamDung = 2, Xong = 3, Huy = 4 }

    /// <summary>Mot dong cua lenh rut: template, cap (-1 = bat ky), so xin (-1 = het).</summary>
    public sealed class DongLenh
    {
        public short Tpl;
        public int Cap = -1;
        public int SoXin = 1;
        public int DaGiao;
        public int CapDaChot = -1;      // cap cu the sau khi lap ke hoach
        public int SoChot;              // so luong se giao (sau khi "het" duoc quy doi)
    }

    /// <summary>GIU CHO (D34): acc X phai giao SoLuong mon khoa K cho lenh nay.</summary>
    public sealed class PhanGiao
    {
        public string Acc;
        public KhoaMon Khoa;
        public int SoLuong;
        public int DaGiao;
        public int ConLai { get { return Math.Max(0, SoLuong - DaGiao); } }
    }

    public sealed class LenhRut
    {
        public int So;
        public string Nguon = "tool";           // "tool" hoac "chat"
        public string ChuKho;                   // Chu kho ra lenh (null neu tu tool)
        public string NguoiNhan;
        public string TenGoi;
        public List<DongLenh> Dong = new List<DongLenh>();
        public TrangThaiLenh TrangThai = TrangThaiLenh.ChoCoMat;
        public string LyDo = "";
        public DateTime TaoLuc = DateTime.Now;
        public DateTime ChoCoMatDen;
        public DateTime KetThucLuc;
        public List<PhanGiao> KeHoach = new List<PhanGiao>();
        public int HongLienTiep;
        // Chu kho da `tiep` mot lenh thieu hang: giao phan dang co roi ket thuc (SPEC R1).
        public bool ChapNhanThieu;
        public string CloneDangGiao;
        public bool DaBaoCho;
        public readonly List<string> CacClone = new List<string>();
        /// <summary>Khu giao (D79). -1 = khu chinh trong cai dat.</summary>
        public int Khu = -1;
        /// <summary>Khu giao khong co bot nao nhin thay: lan cu clone sang tim tiep theo (khong luu dia).</summary>
        public DateTime ThuTimSau;

        public bool DangMo { get { return TrangThai == TrangThaiLenh.ChoCoMat || TrangThai == TrangThaiLenh.DangGiao || TrangThai == TrangThaiLenh.TamDung; } }

        public int TongXin { get { int n = 0; foreach (var d in Dong) n += d.SoChot > 0 ? d.SoChot : Math.Max(0, d.SoXin); return n; } }
        // Lay tu tung DONG, khong tu KeHoach: lap lai ke hoach (lenh `tiep`) xoa sach cac phan giu cu.
        public int TongDaGiao { get { int n = 0; foreach (var d in Dong) n += d.DaGiao; return n; } }

        public string MoTaMon()
        {
            if (!string.IsNullOrEmpty(TenGoi)) return "goi " + TenGoi;
            var p = new List<string>();
            foreach (var d in Dong)
                p.Add((d.SoChot > 0 ? d.SoChot : d.SoXin) + " x " + BangMon.Ten(d.Tpl)
                      + (d.CapDaChot > 0 ? " +" + d.CapDaChot : (d.Cap > 0 ? " +" + d.Cap : "")));
            return string.Join(", ", p.ToArray());
        }
    }

    /// <summary>
    /// HANG CHO LENH RUT + GIU CHO (SPEC §7, D34, D35). Mot khoa cho moi thu.
    /// Luu cac lenh CHUA XONG ra <c>Data/Kho/&lt;danh sach&gt;.hangcho.txt</c> de khoi dong lai app
    /// khong mat cho da giu.
    /// </summary>
    public sealed class HangCho
    {
        private readonly object _lk = new object();
        private readonly List<LenhRut> _mo = new List<LenhRut>();
        private readonly List<LenhRut> _daXong = new List<LenhRut>();
        private int _soKe = 1;
        private bool _canLuu;

        public object Khoa { get { return _lk; } }

        public static string DuongDan
        {
            get { return Path.Combine(Path.Combine(AppPaths.DataDir, "Kho"), AppPaths.SafeName(AppPaths.ListName) + ".hangcho.txt"); }
        }

        public List<LenhRut> DangMo { get { lock (_lk) return new List<LenhRut>(_mo); } }
        public List<LenhRut> GanDay { get { lock (_lk) return new List<LenhRut>(_daXong); } }
        public int SoCho { get { lock (_lk) return _mo.Count; } }

        public LenhRut Tim(int so)
        {
            lock (_lk)
            {
                foreach (var l in _mo) if (l.So == so) return l;
                foreach (var l in _daXong) if (l.So == so) return l;
                return null;
            }
        }

        public void Them(LenhRut l)
        {
            lock (_lk)
            {
                l.So = _soKe++;
                _mo.Add(l);
                _canLuu = true;
            }
        }

        /// <summary>Dua lenh sang Xong/Huy va nha moi cho giu.</summary>
        public void KetThuc(LenhRut l, TrangThaiLenh tt, string lyDo)
        {
            lock (_lk)
            {
                l.TrangThai = tt;
                if (!string.IsNullOrEmpty(lyDo)) l.LyDo = lyDo;
                l.KetThucLuc = DateTime.Now;
                l.CloneDangGiao = null;
                _mo.Remove(l);
                _daXong.Insert(0, l);
                if (_daXong.Count > 200) _daXong.RemoveAt(_daXong.Count - 1);
                _canLuu = true;
            }
        }

        public void DanhDauDoi() { lock (_lk) _canLuu = true; }

        /// <summary>Tong so dang giu cho khoa <paramref name="k"/> (tren moi acc), bo qua lenh <paramref name="truLenh"/>.</summary>
        public int TongGiu(KhoaMon k, LenhRut truLenh)
        {
            lock (_lk)
            {
                int n = 0;
                foreach (var l in _mo)
                {
                    if (l == truLenh) continue;
                    foreach (var p in l.KeHoach) if (p.Khoa.Equals(k)) n += p.ConLai;
                }
                return n;
            }
        }

        /// <summary>So dang giu cho khoa <paramref name="k"/> tren acc <paramref name="acc"/>.</summary>
        public int GiuTrenAcc(KhoaMon k, string acc, LenhRut truLenh)
        {
            lock (_lk)
            {
                int n = 0;
                foreach (var l in _mo)
                {
                    if (l == truLenh) continue;
                    foreach (var p in l.KeHoach)
                        if (p.Khoa.Equals(k) && string.Equals(p.Acc, acc, StringComparison.OrdinalIgnoreCase)) n += p.ConLai;
                }
                return n;
            }
        }

        /// <summary>Tat ca khoa dang giu tren mot acc (de viec cat ruong khong dem cat mon sap phai giao).</summary>
        public HashSet<KhoaMon> KhoaDangGiuTren(string acc)
        {
            var r = new HashSet<KhoaMon>();
            lock (_lk)
            {
                foreach (var l in _mo)
                    foreach (var p in l.KeHoach)
                        if (p.ConLai > 0 && string.Equals(p.Acc, acc, StringComparison.OrdinalIgnoreCase)) r.Add(p.Khoa);
            }
            return r;
        }

        /// <summary>Ghi so dang giu vao bang tong kho (cot Giu / Kha dung).</summary>
        public void ApGiuCho(List<TonMon> bang)
        {
            foreach (var d in bang) d.Giu = TongGiu(d.Khoa, null);
        }

        /// <summary>
        /// LAP KE HOACH + GIU CHO cho mot lenh. Tra null neu du hang; nguoc lai tra mo ta phan thieu
        /// (lenh van giu phan da lap duoc). <paramref name="duocGiao"/>: acc nao duoc dung de giao
        /// (online, khong nha, khong phai du phong...).
        /// </summary>
        public string LapKeHoach(LenhRut l, SoKho so, Func<string, bool> duocGiao)
        {
            lock (_lk)
            {
                l.KeHoach.Clear();
                var thieu = new List<string>();
                var tuiAcc = so.TatCa();
                foreach (var d in l.Dong)
                {
                    int canGiao = d.SoXin < 0 ? int.MaxValue : Math.Max(0, d.SoXin - d.DaGiao);
                    if (canGiao == 0) continue;

                    // Cac cap dang co (chi mon khong khoa)
                    var cap = new SortedDictionary<int, int>();
                    foreach (var t in tuiAcc)
                    {
                        if (duocGiao != null && !duocGiao(t.Acc)) continue;
                        foreach (var m in t.Mon)
                        {
                            if (m.Khoa.Khoa || m.Khoa.Tpl != d.Tpl) continue;
                            if (d.Cap >= 0 && m.Khoa.Up != d.Cap) continue;
                            int co;
                            cap.TryGetValue(m.Khoa.Up, out co);
                            cap[m.Khoa.Up] = co + m.SoLuong;
                        }
                    }
                    if (d.Cap < 0 && cap.Count > 1)
                    {
                        var p = new List<string>();
                        foreach (var kv in cap) p.Add("+" + kv.Key + " x" + kv.Value);
                        thieu.Add(BangMon.Ten(d.Tpl) + " co nhieu cap (" + string.Join(", ", p.ToArray()) + ") - ghi ro +cap");
                        continue;
                    }
                    int up = d.Cap >= 0 ? d.Cap : (cap.Count == 1 ? FirstKey(cap) : 0);
                    d.CapDaChot = up;

                    // Kha dung theo tung acc — uu tien acc co nhieu nhat (it clone nhat). Co han KHONG
                    // con thuoc khoa (D72: server bao khong on dinh) nen khong con uu tien "mon co han truoc".
                    var ung = new List<KeyValuePair<string, KeyValuePair<KhoaMon, int>>>();
                    int tongKhaDung = 0;
                    foreach (var t in tuiAcc)
                    {
                        if (duocGiao != null && !duocGiao(t.Acc)) continue;
                        var k = new KhoaMon(d.Tpl, (byte)up, false, false);
                        int co = t.SoLuong(k) - GiuTrenAcc(k, t.Acc, l) - DaLapTrenAcc(l, k, t.Acc);
                        if (co <= 0) continue;
                        ung.Add(new KeyValuePair<string, KeyValuePair<KhoaMon, int>>(t.Acc, new KeyValuePair<KhoaMon, int>(k, co)));
                        tongKhaDung += co;
                    }
                    var tongTheoAcc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var u in ung)
                    {
                        int n; tongTheoAcc.TryGetValue(u.Key, out n);
                        tongTheoAcc[u.Key] = n + u.Value.Value;
                    }
                    ung.Sort((a, b) =>
                    {
                        int c = tongTheoAcc[b.Key].CompareTo(tongTheoAcc[a.Key]);
                        if (c != 0) return c;
                        return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                    });

                    if (d.SoXin < 0) canGiao = tongKhaDung;
                    d.SoChot = d.DaGiao + canGiao;
                    int conCan = canGiao;
                    foreach (var u in ung)
                    {
                        if (conCan <= 0) break;
                        int lay = Math.Min(conCan, u.Value.Value);
                        l.KeHoach.Add(new PhanGiao { Acc = u.Key, Khoa = u.Value.Key, SoLuong = lay });
                        conCan -= lay;
                    }
                    if (conCan > 0)
                    {
                        int dangGiu = TongGiuTpl(d.Tpl, (byte)up, l);
                        // Hang nam tren acc khong dung duoc luc nay (offline / vua hong voi lenh nay) - de nguoi
                        // doc khong tuong kho het hang (user 16/09: bang ghi con 52 ma lenh bao "chi con 9/30").
                        int ngoai = 0;
                        if (duocGiao != null)
                            foreach (var t in tuiAcc)
                            {
                                if (duocGiao(t.Acc)) continue;
                                foreach (var m in t.Mon)
                                    if (!m.Khoa.Khoa && m.Khoa.Tpl == d.Tpl && m.Khoa.Up == up) ngoai += m.SoLuong;
                            }
                        thieu.Add(string.Format("{0}{1}: chi con {2}/{3}{4}{5}", BangMon.Ten(d.Tpl), up > 0 ? " +" + up : "",
                            canGiao - conCan, canGiao, dangGiu > 0 ? " (" + dangGiu + " dang giu cho lenh khac)" : "",
                            ngoai > 0 ? " (" + ngoai + " nam tren acc tam khong dung: offline/nha/vua giao hong/Leader khi giao khu rieng)" : ""));
                    }
                    if (d.SoXin < 0 && canGiao == 0)
                        thieu.Add(BangMon.Ten(d.Tpl) + ": kho khong co");
                }
                _canLuu = true;
                return thieu.Count == 0 ? null : string.Join("; ", thieu.ToArray());
            }
        }

        /// <summary>Nha phan giu tren acc (acc bi nha / offline qua lau) de lap lai ke hoach.</summary>
        public void NhaGiuTrenAcc(LenhRut l, string acc)
        {
            lock (_lk)
            {
                l.KeHoach.RemoveAll(p => p.ConLai > 0 && p.DaGiao == 0 && string.Equals(p.Acc, acc, StringComparison.OrdinalIgnoreCase));
                foreach (var p in l.KeHoach)
                    if (string.Equals(p.Acc, acc, StringComparison.OrdinalIgnoreCase)) p.SoLuong = p.DaGiao;
                _canLuu = true;
            }
        }

        /// <summary>
        /// Ghi so da giao that. <paramref name="soMon"/> la so luong da sang tay nguoi nhan cho khoa
        /// <paramref name="k"/> tu acc <paramref name="acc"/>.
        /// </summary>
        public void GhiDaGiao(LenhRut l, string acc, KhoaMon k, int soMon)
        {
            lock (_lk)
            {
                int con = soMon;
                foreach (var p in l.KeHoach)
                {
                    if (con <= 0) break;
                    if (!p.Khoa.Equals(k) || !string.Equals(p.Acc, acc, StringComparison.OrdinalIgnoreCase)) continue;
                    int an = Math.Min(con, p.ConLai);
                    p.DaGiao += an;
                    con -= an;
                }
                foreach (var d in l.Dong)
                {
                    if (d.Tpl != k.Tpl || d.CapDaChot != k.Up) continue;
                    d.DaGiao += soMon;
                    break;
                }
                _canLuu = true;
            }
        }

        public bool DaGiaoDu(LenhRut l)
        {
            lock (_lk)
            {
                if (l.KeHoach.Count == 0) return false;
                foreach (var p in l.KeHoach) if (p.ConLai > 0) return false;
                return true;
            }
        }

        private int DaLapTrenAcc(LenhRut l, KhoaMon k, string acc)
        {
            int n = 0;
            foreach (var p in l.KeHoach)
                if (p.Khoa.Equals(k) && string.Equals(p.Acc, acc, StringComparison.OrdinalIgnoreCase)) n += p.ConLai;
            return n;
        }

        private int TongGiuTpl(short tpl, byte up, LenhRut truLenh)
        {
            int n = 0;
            foreach (var l in _mo)
            {
                if (l == truLenh) continue;
                foreach (var p in l.KeHoach)
                    if (p.Khoa.Tpl == tpl && p.Khoa.Up == up) n += p.ConLai;
            }
            return n;
        }

        private static int FirstKey(SortedDictionary<int, int> d)
        {
            foreach (var kv in d) return kv.Key;
            return 0;
        }

        // ================= luu / nap =================

        public void LuuNeuCan()
        {
            string noiDung;
            lock (_lk)
            {
                if (!_canLuu) return;
                _canLuu = false;
                var sb = new StringBuilder();
                sb.Append("# NSOKHODO hang cho lenh rut - chi cac lenh CHUA xong\r\n");
                sb.Append("SoKe=").Append(_soKe).Append("\r\n");
                foreach (var l in _mo)
                {
                    sb.Append("L|").Append(l.So).Append('|').Append(l.Nguon).Append('|').Append(Sach(l.ChuKho)).Append('|')
                      .Append(Sach(l.NguoiNhan)).Append('|').Append(Sach(l.TenGoi)).Append('|').Append((int)l.TrangThai).Append('|')
                      .Append(Sach(l.LyDo)).Append('|').Append(l.TaoLuc.Ticks).Append('|').Append(l.ChoCoMatDen.Ticks).Append('|')
                      .Append(l.HongLienTiep).Append('|').Append(l.ChapNhanThieu ? 1 : 0).Append('|').Append(l.Khu).Append("\r\n");
                    foreach (var d in l.Dong)
                        sb.Append("D|").Append(l.So).Append('|').Append(d.Tpl).Append('|').Append(d.Cap).Append('|')
                          .Append(d.SoXin).Append('|').Append(d.DaGiao).Append('|').Append(d.CapDaChot).Append('|').Append(d.SoChot).Append("\r\n");
                    foreach (var p in l.KeHoach)
                        sb.Append("P|").Append(l.So).Append('|').Append(p.Acc).Append('|').Append(p.Khoa.ToString()).Append('|')
                          .Append(p.SoLuong).Append('|').Append(p.DaGiao).Append("\r\n");
                }
                noiDung = sb.ToString();
            }
            try
            {
                string f = DuongDan;
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                string tmp = f + ".tmp";
                File.WriteAllText(tmp, noiDung, new UTF8Encoding(false));
                if (File.Exists(f)) File.Replace(tmp, f, null);
                else File.Move(tmp, f);
            }
            catch { lock (_lk) _canLuu = true; }
        }

        public void Nap()
        {
            try
            {
                if (!File.Exists(DuongDan)) return;
                var theoSo = new Dictionary<int, LenhRut>();
                lock (_lk)
                {
                    foreach (var dong in File.ReadAllLines(DuongDan, Encoding.UTF8))
                    {
                        if (dong.StartsWith("#")) continue;
                        if (dong.StartsWith("SoKe=")) { int.TryParse(dong.Substring(5), out _soKe); if (_soKe < 1) _soKe = 1; continue; }
                        var p = dong.Split('|');
                        int so;
                        if (p.Length < 2 || !int.TryParse(p[1], out so)) continue;
                        if (p[0] == "L" && p.Length >= 11)
                        {
                            var l = new LenhRut
                            {
                                So = so, Nguon = p[2], ChuKho = Rong(p[3]), NguoiNhan = Rong(p[4]), TenGoi = Rong(p[5]),
                                TrangThai = (TrangThaiLenh)Int(p[6]), LyDo = p[7],
                                TaoLuc = new DateTime(Long(p[8])), ChoCoMatDen = new DateTime(Long(p[9])), HongLienTiep = Int(p[10]),
                                ChapNhanThieu = p.Length > 11 && p[11] == "1",
                                Khu = p.Length > 12 ? IntHoac(p[12], -1) : -1,   // file cu (truoc D79): khu chinh
                            };
                            // Lenh dang giao do tu luc app tat -> quay ve cho co mat, clone se di lai tu dau.
                            if (l.TrangThai == TrangThaiLenh.DangGiao) l.TrangThai = TrangThaiLenh.ChoCoMat;
                            if (l.ChoCoMatDen < DateTime.Now) l.ChoCoMatDen = DateTime.Now.AddMinutes(10);
                            theoSo[so] = l;
                            _mo.Add(l);
                        }
                        else if (p[0] == "D" && p.Length >= 8 && theoSo.ContainsKey(so))
                        {
                            theoSo[so].Dong.Add(new DongLenh
                            {
                                Tpl = (short)Int(p[2]), Cap = Int(p[3]), SoXin = Int(p[4]), DaGiao = Int(p[5]),
                                CapDaChot = Int(p[6]), SoChot = Int(p[7]),
                            });
                        }
                        else if (p[0] == "P" && p.Length >= 6 && theoSo.ContainsKey(so))
                        {
                            KhoaMon k;
                            if (KhoaMon.TryParse(p[3], out k))
                                theoSo[so].KeHoach.Add(new PhanGiao { Acc = p[2], Khoa = k, SoLuong = Int(p[4]), DaGiao = Int(p[5]) });
                        }
                    }
                    foreach (var l in _mo) if (l.So >= _soKe) _soKe = l.So + 1;
                }
            }
            catch { }
        }

        private static string Sach(string s) { return (s ?? "").Replace('|', '/').Replace('\r', ' ').Replace('\n', ' '); }
        private static string Rong(string s) { return string.IsNullOrEmpty(s) ? null : s; }
        private static int Int(string s) { return IntHoac(s, 0); }
        private static int IntHoac(string s, int macDinh) { int n; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : macDinh; }
        private static long Long(string s) { long n; return long.TryParse(s, out n) ? n : 0; }
    }
}
