namespace NSOKHODO.Config
{
    /// <summary>
    /// Ba NUM VAN cua tang ket noi, de trong <c>Data/settings.txt</c> de DOI DUOC MA KHONG BUILD LAI.
    ///
    /// VI SAO PHAI CHINH DUOC (user 2026-09-12: <i>"nếu k oke thì quay lại được không?"</i>): ba tham so
    /// nay dong vao mot co che DA CHAY DUOC (cong <see cref="Fleet.ReloginGate"/>) va vao han gio bat
    /// tay proxy. Neu server bat dau tu choi hang loat thi user phai ha duoc trong 10 giay, khong phai
    /// cho mot lan build.
    ///
    /// <para><b>Quay ve dung hanh vi truoc 2026-09-12:</b>
    /// <c>ProxyHandshakeSec=0</c> · <c>ReloginMax=5</c> · <c>ReloginGapMs=1000</c> roi mo lai tool.</para>
    /// </summary>
    public static class NetOptions
    {
        // ==================== 1. HAN GIO BAT TAY PROXY ====================

        /// <summary>
        /// Han gio cho MOI lan doc trong pha bat tay proxy (ms). 0 = giu nguyen nhu truoc
        /// (dung <c>ReceiveTimeout</c> 120 giay cua ket noi game).
        ///
        /// <para><b>Goc re no chua:</b> <c>NsoConnection.Connect</c> dat ReceiveTimeout = 120s TRUOC
        /// khi bat tay proxy, ma bat tay SOCKS5/HTTP doc qua chinh stream do. Proxy nhan TCP roi im
        /// (qua tai / het slot / IP da xoay) = luong login nam cho DU 2 PHUT, trong luc do van giu
        /// slot cua ca hai cong dieu tiet -> 5 acc xui la ca tien trinh dung hinh.</para>
        ///
        /// <para>10 giay: proxy song thi bat tay xong trong vai tram ms; qua 10 giay coi nhu chet,
        /// tra loi som cho acc khac dung luot.</para>
        /// </summary>
        public static int ProxyHandshakeMs = 10000;

        // ==================== 2. CONG DIEU TIET LOGIN LAI ====================

        /// <summary>
        /// Tran so acc trong pha login lai cung luc (toan tien trinh).
        /// <b>-1 = TU DONG</b> (mac dinh): cong <see cref="Fleet.LoginGate"/> bat -> khong gioi han
        /// (cong do da ham theo tung MAY CHU roi); cong do tat -> quay ve 5 nhu cu, de khong bao gio
        /// co trang thai "khong con cai gi ham ca" tren duong login lai.
        /// <b>0 = khong gioi han</b> du cong login dang tat. <b>&gt;0 = tran cung.</b>
        ///
        /// <para>Co ba trang thai chu khong phai hai vi hai nhu cau khac nhau: mac dinh phai AN
        /// TOAN (co lop ham), con user thi phai co duong tat de bo tran ma khong buoc phai bat
        /// cong login - luc do chi can ghi <c>ReloginMax=0</c> vao settings.txt.</para>
        ///
        /// <para>⚠️ <b>Dat &gt;0 TRONG KHI cong login dang bat thi hai cong chong nhau:</b> o
        /// <c>ScheduleReconnect</c> acc xin cong login TRUOC, duoc slot may chu roi moi xin cong
        /// fleet - bi cong fleet tu choi thi no VAN OM slot may chu trong luc xep hang. Voi cau
        /// hinh mac dinh (-1) khong bao gio xay ra: cong login bat => tran fleet = 0 = khong tu
        /// choi ai. Muon bo tran fleet thi dung <c>ReloginMax=0</c>, dung dat 5.</para>
        /// </summary>
        public static int ReloginMax = -1;

        /// <summary>
        /// Hai lan cap slot login lai phai cach nhau bay nhieu ms (toan tien trinh). <b>0 = khong
        /// gian cach.</b>
        ///
        /// <para>-1 = TU DONG (mac dinh): cong login bat -> 0 (khong gian cach), cong login tat ->
        /// 1000ms nhu cu. 0 = bo gian cach du cong login dang tat.</para>
        ///
        /// <para>Vi sao bo khi cong login bat: voi 600 acc, luat 1 giay/slot la SAN CUNG 10 phut cho
        /// mot lan vao lai ca ham. Cong login da co gian cach 1 giay RIENG TUNG MAY CHU - do moi la
        /// cho that su can gian.</para>
        /// </summary>
        public static int ReloginGapMs = -1;

        // ==================== NAP / LUU ====================

        public static void Load()
        {
            var map = SettingsStore.LoadAll();

            int v = SettingsStore.GetInt(map, "ProxyHandshakeSec", 10);
            if (v >= 0 && v <= 300) ProxyHandshakeMs = v * 1000;

            v = SettingsStore.GetInt(map, "ReloginMax", -1);
            if (v >= -1 && v <= 1000) ReloginMax = v;

            v = SettingsStore.GetInt(map, "ReloginGapMs", -1);
            if (v >= -1 && v <= 60000) ReloginGapMs = v;
        }

        public static void Save()
        {
            SettingsStore.Set(
                "ProxyHandshakeSec", (ProxyHandshakeMs / 1000).ToString(),
                "ReloginMax", ReloginMax.ToString(),
                "ReloginGapMs", ReloginGapMs.ToString());
        }

        /// <summary>Mot dong tom tat cho log khoi dong - de doc log la biet tool dang chay tham so nao.</summary>
        public static string Describe()
        {
            return string.Format("[Net] Bat tay proxy: {0} · Tran login lai: {1} · Gian cach: {2}",
                ProxyHandshakeMs > 0 ? (ProxyHandshakeMs / 1000) + "s" : "nhu cu (120s)",
                ReloginMax < 0 ? "tu dong" : (ReloginMax == 0 ? "khong gioi han" : ReloginMax.ToString()),
                ReloginGapMs < 0 ? "tu dong" : (ReloginGapMs == 0 ? "khong" : ReloginGapMs + "ms"));
        }
    }
}
