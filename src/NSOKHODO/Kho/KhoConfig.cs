using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NSOKHODO.Config;

namespace NSOKHODO.Kho
{
    public enum CheDoNhan { TatCa = 0, ChiChuKho = 1 }
    public enum CheDoRao { ThongMinh = 0, Deu = 1 }

    /// <summary>Mot dong cua goi rut: template + so luong (-1 = het).</summary>
    public sealed class DongGoi
    {
        public short Tpl;
        public int SoLuong;
    }

    /// <summary>Mot muc theo doi: template, nguong (-1 = bao moi lan ve), Chu kho da dat.</summary>
    public sealed class MucTheoDoi
    {
        public short Tpl;
        public int Nguong = -1;
        public string ChuKho;
    }

    /// <summary>
    /// CAI DAT KHO — mot file cho moi danh sach acc: <c>Data/Kho/&lt;ten danh sach&gt;.txt</c>.
    /// Mot kho = mot tien trinh = mot danh sach acc (SPEC §11). Dinh dang <c>khoa=gia tri</c>
    /// (khong JSON, theo quy uoc ho NSO). Bang khoa + mac dinh: docs/SPEC.md §4.
    ///
    /// <para>Doc/ghi tu nhieu luong (UI + bo dieu phoi): moi truy cap danh sach qua khoa cua doi tuong.
    /// Gia tri don (int/bool/string) gan nguyen tu nen doc khong can khoa.</para>
    /// </summary>
    public sealed class KhoConfig
    {
        private readonly object _lk = new object();

        // ---- vi tri ----
        public int Map = 22;                 // Lang Tone
        public int KhuChinh = -1;            // -1 = chua cai -> mode dung cho, khong di dau
        public int KhuPhu = -1;
        public short LeaderX;                // 0/0 = trong (D48: dung dau cung duoc)
        public short LeaderY;

        // ---- vai ----
        public string Leader = "";           // username
        public string LeaderDuPhong = "";    // username
        private readonly List<string> _chuKho = new List<string>();
        // Clone dang NHA cho user dang nhap tay (SPEC §6.2) - nho qua lan khoi dong lai.
        private readonly HashSet<string> _daNha = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ---- nhan / cho ----
        public CheDoNhan CheDoNhan = CheDoNhan.TatCa;   // D16: mac dinh Tat ca
        public bool ChatVoiNguoiLa = false;               // D24
        public int NguongNhan = 12;
        public int ChoNguoiGiay = 120;
        public int ChoNguoiLaGiay = 45;
        public int ChoBotGiay = 20;
        public int MoiLaiGiay = 31;                       // D40 (test tay T4)
        public int GiuCuaGiay = 60;                       // D41
        public int ChoCoMatPhut = 10;
        public int XuTran = 2000000000;                   // D45 (test tay T14)
        public int XuNguong = 1000000000;

        // ---- chat / rao ----
        public bool RaoBat = true;
        public string RaoMau = "Kho do {dung}/{tong}";
        public CheDoRao RaoCheDo = CheDoRao.ThongMinh;    // D31
        public int RaoNhipGiay = 5;
        public int RaoVangGiay = 300;
        public bool BaoCongDong = true;

        // ---- ke hang / rac ----
        private readonly Dictionary<string, string> _keCuaAcc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<short, string> _keCuaMon = new Dictionary<short, string>();
        private readonly HashSet<short> _rac = new HashSet<short>();
        public int RacBaoNguong = 80;

        // ---- goi rut / theo doi / bao cao ----
        private readonly Dictionary<string, List<DongGoi>> _goi = new Dictionary<string, List<DongGoi>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MucTheoDoi> _theoDoi = new List<MucTheoDoi>();
        public string BaoCaoGio = "";                      // "" = tat, "22:00" = gio gui

        // ---- log ----
        public bool LogFile = true;                        // D26
        public int GiuLogNgay = 0;
        public bool LogHexGiaoDich = true;

        // ---- cong tac tung tinh nang (D50: de khoanh vung khi co loi) ----
        public bool BatNap = true;
        public bool BatRut = true;
        public bool BatDonKho = true;
        public bool BatCatRuong = true;
        public bool BatDonXu = true;
        public bool BatLenhChat = true;
        public bool BatTheoDoi = true;
        public bool BatBaoCao = true;

        // ================= truy cap danh sach (co khoa) =================

        public List<string> ChuKho { get { lock (_lk) return new List<string>(_chuKho); } }

        public void DatChuKho(IEnumerable<string> ds)
        {
            lock (_lk)
            {
                _chuKho.Clear();
                foreach (var s in ds)
                {
                    string t = (s ?? "").Trim();
                    if (t.Length > 0 && !_chuKho.Exists(x => ChuVan.CungTen(x, t))) _chuKho.Add(t);
                }
            }
        }

        public bool LaChuKho(string ten)
        {
            if (string.IsNullOrEmpty(ten)) return false;
            lock (_lk) return _chuKho.Exists(x => ChuVan.CungTen(x, ten));
        }

        public bool DaNha(string username)
        {
            if (string.IsNullOrEmpty(username)) return false;
            lock (_lk) return _daNha.Contains(username);
        }

        public void DatNha(string username, bool nha)
        {
            if (string.IsNullOrEmpty(username)) return;
            lock (_lk) { if (nha) _daNha.Add(username); else _daNha.Remove(username); }
        }

        public List<string> DanhSachNha { get { lock (_lk) return new List<string>(_daNha); } }

        public string KeCuaAcc(string username)
        {
            string k;
            lock (_lk) return username != null && _keCuaAcc.TryGetValue(username, out k) ? k : null;
        }

        public void DatKeCuaAcc(string username, string ke)
        {
            if (string.IsNullOrEmpty(username)) return;
            lock (_lk)
            {
                if (string.IsNullOrEmpty(ke)) _keCuaAcc.Remove(username);
                else _keCuaAcc[username] = ke;
            }
        }

        public string KeCuaMon(short tpl)
        {
            string k;
            lock (_lk) return _keCuaMon.TryGetValue(tpl, out k) ? k : null;
        }

        public void DatKeCuaMon(short tpl, string ke)
        {
            lock (_lk)
            {
                if (string.IsNullOrEmpty(ke)) _keCuaMon.Remove(tpl);
                else _keCuaMon[tpl] = ke;
            }
        }

        public Dictionary<short, string> ChupKeCuaMon()
        {
            lock (_lk) return new Dictionary<short, string>(_keCuaMon);
        }

        public bool LaRac(short tpl) { lock (_lk) return _rac.Contains(tpl); }
        public void DatRac(short tpl, bool la) { lock (_lk) { if (la) _rac.Add(tpl); else _rac.Remove(tpl); } }
        public List<short> DanhSachRac { get { lock (_lk) return new List<short>(_rac); } }

        public List<string> TenGoi
        {
            get { lock (_lk) { var r = new List<string>(_goi.Keys); r.Sort(StringComparer.OrdinalIgnoreCase); return r; } }
        }

        public List<DongGoi> LayGoi(string ten)
        {
            List<DongGoi> g;
            lock (_lk)
            {
                if (ten == null || !_goi.TryGetValue(ten.Trim(), out g)) return null;
                var r = new List<DongGoi>();
                foreach (var d in g) r.Add(new DongGoi { Tpl = d.Tpl, SoLuong = d.SoLuong });
                return r;
            }
        }

        public void DatGoi(string ten, List<DongGoi> dong)
        {
            if (string.IsNullOrEmpty(ten)) return;
            ten = ten.Trim();
            lock (_lk)
            {
                if (dong == null || dong.Count == 0) _goi.Remove(ten);
                else _goi[ten] = new List<DongGoi>(dong);
            }
        }

        public List<MucTheoDoi> TheoDoi
        {
            get
            {
                lock (_lk)
                {
                    var r = new List<MucTheoDoi>();
                    foreach (var m in _theoDoi) r.Add(new MucTheoDoi { Tpl = m.Tpl, Nguong = m.Nguong, ChuKho = m.ChuKho });
                    return r;
                }
            }
        }

        public void DatTheoDoi(short tpl, int nguong, string chuKho)
        {
            lock (_lk)
            {
                _theoDoi.RemoveAll(m => m.Tpl == tpl && ChuVan.CungTen(m.ChuKho, chuKho));
                _theoDoi.Add(new MucTheoDoi { Tpl = tpl, Nguong = nguong, ChuKho = chuKho });
            }
        }

        public bool BoTheoDoi(short tpl, string chuKho)
        {
            lock (_lk) return _theoDoi.RemoveAll(m => m.Tpl == tpl && (chuKho == null || ChuVan.CungTen(m.ChuKho, chuKho))) > 0;
        }

        // ================= FILE =================

        public static string DuongDan
        {
            get { return Path.Combine(Path.Combine(AppPaths.DataDir, "Kho"), AppPaths.SafeName(AppPaths.ListName) + ".txt"); }
        }

        public static KhoConfig Nap()
        {
            var c = new KhoConfig();
            try
            {
                if (File.Exists(DuongDan))
                    c.DocChu(File.ReadAllLines(DuongDan, Encoding.UTF8));
            }
            catch { }
            return c;
        }

        private static readonly object _luuLk = new object();

        /// <summary>
        /// Ghi nguyen tu (tmp + Replace). Loi ghi dia thi tra ve thong bao, khong nem.
        /// Khoa chung: giao dien va bo dieu phoi (lenh chat `theo`, nha clone) deu goi - hai ben cung
        /// ghi mot file .tmp thi File.Replace cua ben sau hong.
        /// </summary>
        public string Luu()
        {
            lock (_luuLk)
            {
                try
                {
                    string p = DuongDan;
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    string tmp = p + ".tmp";
                    File.WriteAllText(tmp, VietChu(), new UTF8Encoding(false));
                    if (File.Exists(p)) File.Replace(tmp, p, p + ".bak");
                    else File.Move(tmp, p);
                    return null;
                }
                catch (Exception ex) { return ex.Message; }
            }
        }

        public string VietChu()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# NSOKHODO - cai dat kho cua danh sach \"" + AppPaths.ListName + "\" (docs/SPEC.md §4)");
            Ghi(sb, "Map", Map);
            Ghi(sb, "KhuChinh", KhuChinh);
            Ghi(sb, "KhuPhu", KhuPhu);
            Ghi(sb, "LeaderX", LeaderX);
            Ghi(sb, "LeaderY", LeaderY);
            Ghi(sb, "Leader", Leader);
            Ghi(sb, "LeaderDuPhong", LeaderDuPhong);
            Ghi(sb, "ChuKho", string.Join(",", ChuKho.ToArray()));
            Ghi(sb, "CheDoNhan", CheDoNhan.ToString());
            Ghi(sb, "ChatVoiNguoiLa", ChatVoiNguoiLa);
            Ghi(sb, "NguongNhan", NguongNhan);
            Ghi(sb, "ChoNguoi", ChoNguoiGiay);
            Ghi(sb, "ChoNguoiLa", ChoNguoiLaGiay);
            Ghi(sb, "ChoBot", ChoBotGiay);
            Ghi(sb, "MoiLaiGiay", MoiLaiGiay);
            Ghi(sb, "GiuCuaGiay", GiuCuaGiay);
            Ghi(sb, "ChoCoMatPhut", ChoCoMatPhut);
            Ghi(sb, "XuTran", XuTran);
            Ghi(sb, "XuNguong", XuNguong);
            Ghi(sb, "RaoBat", RaoBat);
            Ghi(sb, "RaoMau", RaoMau);
            Ghi(sb, "RaoCheDo", RaoCheDo.ToString());
            Ghi(sb, "RaoNhipGiay", RaoNhipGiay);
            Ghi(sb, "RaoVangGiay", RaoVangGiay);
            Ghi(sb, "BaoCongDong", BaoCongDong);
            Ghi(sb, "RacBaoNguong", RacBaoNguong);
            Ghi(sb, "BaoCaoGio", BaoCaoGio);
            Ghi(sb, "LogFile", LogFile);
            Ghi(sb, "GiuLogNgay", GiuLogNgay);
            Ghi(sb, "LogHexGiaoDich", LogHexGiaoDich);
            Ghi(sb, "BatNap", BatNap);
            Ghi(sb, "BatRut", BatRut);
            Ghi(sb, "BatDonKho", BatDonKho);
            Ghi(sb, "BatCatRuong", BatCatRuong);
            Ghi(sb, "BatDonXu", BatDonXu);
            Ghi(sb, "BatLenhChat", BatLenhChat);
            Ghi(sb, "BatTheoDoi", BatTheoDoi);
            Ghi(sb, "BatBaoCao", BatBaoCao);
            lock (_lk)
            {
                var rac = new List<string>();
                foreach (var t in _rac) rac.Add(t.ToString(CultureInfo.InvariantCulture));
                Ghi(sb, "Rac", string.Join(",", rac.ToArray()));
                Ghi(sb, "DaNha", string.Join(",", new List<string>(_daNha).ToArray()));
                foreach (var kv in _keCuaAcc) Ghi(sb, "KeAcc." + kv.Key, kv.Value);
                foreach (var kv in _keCuaMon) Ghi(sb, "KeMon." + kv.Key.ToString(CultureInfo.InvariantCulture), kv.Value);
                foreach (var kv in _goi)
                {
                    var p = new List<string>();
                    foreach (var d in kv.Value) p.Add(d.Tpl + "x" + d.SoLuong);
                    Ghi(sb, "Goi." + kv.Key, string.Join(";", p.ToArray()));
                }
                var td = new List<string>();
                foreach (var m in _theoDoi) td.Add(m.Tpl + ":" + m.Nguong + ":" + (m.ChuKho ?? ""));
                Ghi(sb, "TheoDoi", string.Join(";", td.ToArray()));
            }
            return sb.ToString();
        }

        public void DocChu(IEnumerable<string> dong)
        {
            foreach (string raw in dong)
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith("#")) continue;
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string k = raw.Substring(0, eq).Trim();
                string v = raw.Substring(eq + 1).Trim();
                try { DocMotKhoa(k, v); } catch { }
            }
        }

        private void DocMotKhoa(string k, string v)
        {
            if (k.StartsWith("KeAcc.", StringComparison.OrdinalIgnoreCase)) { DatKeCuaAcc(k.Substring(6), v); return; }
            if (k.StartsWith("KeMon.", StringComparison.OrdinalIgnoreCase))
            {
                short t; if (short.TryParse(k.Substring(6), out t)) DatKeCuaMon(t, v);
                return;
            }
            if (k.StartsWith("Goi.", StringComparison.OrdinalIgnoreCase))
            {
                var ds = new List<DongGoi>();
                foreach (var part in v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var xy = part.Split('x');
                    short t; int n;
                    if (xy.Length == 2 && short.TryParse(xy[0].Trim(), out t) && int.TryParse(xy[1].Trim(), out n))
                        ds.Add(new DongGoi { Tpl = t, SoLuong = n });
                }
                DatGoi(k.Substring(4), ds);
                return;
            }
            switch (k)
            {
                case "Map": Map = Int(v, Map); break;
                case "KhuChinh": KhuChinh = Int(v, KhuChinh); break;
                case "KhuPhu": KhuPhu = Int(v, KhuPhu); break;
                case "LeaderX": LeaderX = (short)Int(v, LeaderX); break;
                case "LeaderY": LeaderY = (short)Int(v, LeaderY); break;
                case "Leader": Leader = v; break;
                case "LeaderDuPhong": LeaderDuPhong = v; break;
                case "ChuKho": DatChuKho(v.Split(',')); break;
                case "CheDoNhan": CheDoNhan = v.Equals("ChiChuKho", StringComparison.OrdinalIgnoreCase) ? CheDoNhan.ChiChuKho : CheDoNhan.TatCa; break;
                case "ChatVoiNguoiLa": ChatVoiNguoiLa = Bool(v); break;
                case "NguongNhan": NguongNhan = Int(v, NguongNhan); break;
                case "ChoNguoi": ChoNguoiGiay = Int(v, ChoNguoiGiay); break;
                case "ChoNguoiLa": ChoNguoiLaGiay = Int(v, ChoNguoiLaGiay); break;
                case "ChoBot": ChoBotGiay = Int(v, ChoBotGiay); break;
                case "MoiLaiGiay": MoiLaiGiay = Int(v, MoiLaiGiay); break;
                case "GiuCuaGiay": GiuCuaGiay = Int(v, GiuCuaGiay); break;
                case "ChoCoMatPhut": ChoCoMatPhut = Int(v, ChoCoMatPhut); break;
                case "XuTran": XuTran = Int(v, XuTran); break;
                case "XuNguong": XuNguong = Int(v, XuNguong); break;
                case "RaoBat": RaoBat = Bool(v); break;
                case "RaoMau": RaoMau = v; break;
                case "RaoCheDo": RaoCheDo = v.Equals("Deu", StringComparison.OrdinalIgnoreCase) ? CheDoRao.Deu : CheDoRao.ThongMinh; break;
                case "RaoNhipGiay": RaoNhipGiay = Int(v, RaoNhipGiay); break;
                case "RaoVangGiay": RaoVangGiay = Int(v, RaoVangGiay); break;
                case "BaoCongDong": BaoCongDong = Bool(v); break;
                case "RacBaoNguong": RacBaoNguong = Int(v, RacBaoNguong); break;
                case "BaoCaoGio": BaoCaoGio = v; break;
                case "LogFile": LogFile = Bool(v); break;
                case "GiuLogNgay": GiuLogNgay = Int(v, GiuLogNgay); break;
                case "LogHexGiaoDich": LogHexGiaoDich = Bool(v); break;
                case "BatNap": BatNap = Bool(v); break;
                case "BatRut": BatRut = Bool(v); break;
                case "BatDonKho": BatDonKho = Bool(v); break;
                case "BatCatRuong": BatCatRuong = Bool(v); break;
                case "BatDonXu": BatDonXu = Bool(v); break;
                case "BatLenhChat": BatLenhChat = Bool(v); break;
                case "BatTheoDoi": BatTheoDoi = Bool(v); break;
                case "BatBaoCao": BatBaoCao = Bool(v); break;
                case "DaNha":
                    lock (_lk)
                    {
                        _daNha.Clear();
                        foreach (var s in v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            if (s.Trim().Length > 0) _daNha.Add(s.Trim());
                    }
                    break;
                case "Rac":
                    lock (_lk)
                    {
                        _rac.Clear();
                        foreach (var s in v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            short t; if (short.TryParse(s.Trim(), out t)) _rac.Add(t);
                        }
                    }
                    break;
                case "TheoDoi":
                    lock (_lk)
                    {
                        _theoDoi.Clear();
                        foreach (var s in v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var p = s.Split(':');
                            short t; int n;
                            if (p.Length >= 1 && short.TryParse(p[0].Trim(), out t))
                                _theoDoi.Add(new MucTheoDoi
                                {
                                    Tpl = t,
                                    Nguong = p.Length > 1 && int.TryParse(p[1].Trim(), out n) ? n : -1,
                                    ChuKho = p.Length > 2 ? p[2].Trim() : "",
                                });
                        }
                    }
                    break;
            }
        }

        /// <summary>Gio bao cao ngay da cai chua; tra false khi de trong hoac sai cu phap.</summary>
        public bool LayGioBaoCao(out int gio, out int phut)
        {
            gio = 0; phut = 0;
            string s = (BaoCaoGio ?? "").Trim();
            if (s.Length == 0) return false;
            var p = s.Split(':');
            return p.Length == 2 && int.TryParse(p[0], out gio) && int.TryParse(p[1], out phut)
                   && gio >= 0 && gio < 24 && phut >= 0 && phut < 60;
        }

        private static void Ghi(StringBuilder sb, string k, object v)
        {
            string s = v is bool ? ((bool)v ? "1" : "0") : Convert.ToString(v, CultureInfo.InvariantCulture);
            sb.Append(k).Append('=').Append((s ?? "").Replace("\r", " ").Replace("\n", " ")).Append("\r\n");
        }

        private static int Int(string v, int def)
        {
            int n;
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : def;
        }

        private static bool Bool(string v)
        {
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
