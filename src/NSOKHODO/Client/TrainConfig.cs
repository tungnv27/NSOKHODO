namespace NSOKHODO.Client
{
    /// <summary>
    /// Cau hinh rieng cho che do Train (tan sat). Tach khoi AccountConfig de Train
    /// mo rong tu do ma KHONG dung cac nhom khac (Login/AFK/Party).
    ///
    /// Duoc serialize thanh 1 field JSON o cuoi accounts.txt (xem ConfigManager),
    /// nen them field moi o day KHONG lam vo format file cu (default ap dung khi thieu).
    /// </summary>
    public class TrainConfig
    {
        /// <summary>Chu ky combat loop (ms).</summary>
        public int TickMs { get; set; }

        /// <summary>Tam danh truc X (px) - "Ngang" tren UI. 0 = tu dong theo skill.Dx.</summary>
        public int AttackRangeX { get; set; }

        /// <summary>Tam danh truc Y (px) - "Cao" tren UI. 0 = tu dong theo skill.Dy.</summary>
        public int AttackRangeY { get; set; }

        /// <summary>Ban kinh nhat item (px).</summary>
        public int PickRange { get; set; }

        /// <summary>Sau khi chet bao lau (phut) thi bo qua viec quay lai diem chet.</summary>
        public int DeathSpotTimeoutMin { get; set; }

        // ---- Loc loai quai (clone MODGAME Auto.a(levelBoss): 0=thuong, 1=tinh anh,
        // 2=thu linh; mob isBoss that tinh nhu thu linh). Day la "CHO PHEP danh" khi gap. ----
        public bool HitNormal { get; set; }
        public bool HitTinhAnh { get; set; }
        public bool HitThuLinh { get; set; }

        /// <summary>
        /// "Săn TATL" (ri[26], MODGAME Char.ea) = CHU DONG bay toi danh Tinh Anh (levelBoss 1, neu
        /// HitTinhAnh) / Thu Linh (levelBoss 2, neu HitThuLinh) tren CA map, uu tien hon quai thuong.
        /// Khong bat thi cluster-selection it khi chon trung Tinh Anh/Thu Linh le -> "tich ma khong danh".
        /// </summary>
        public bool HuntTatl { get; set; }

        /// <summary>Sau revive co tu quay lai diem chet de tan sat tiep khong.</summary>
        public bool ReturnToDeathSpot { get; set; }

        // ---- Tham so port tu logic train Java (Auto.c(int,int)) ----
        // Thay cho du lieu skill (dx/dy/coolDown/maxFight) ma C# chua parse tu DataSync.

        /// <summary>Cooldown giua 2 lan gui packet danh (ms) - thay skill.coolDown.</summary>
        public int AttackCooldownMs { get; set; }

        /// <summary>
        /// Bien CONG THEM vao cong nhip danh (ms). Cong that = skill.CoolDown + gia tri nay.
        ///
        /// 100 (mac dinh) = giu nguyen hanh vi cu, clone NSOTRUNGDUC `Class_ad.java:1018`
        /// (`skill.e + 100L`). 0 = dung nhip ZangVPS (`skill.o - fK`, fK=0 khi khong bat o giam toc).
        ///
        /// Do duoc 2026-09-06 tren CUNG acc `barbig01` / CUNG map 36 khu 8: Zang 350 ms/don
        /// (= dung cooldown chieu), ta 500 ms/don => cham hon 42,9%. Chi tiet + bang so:
        /// `docs/features/NANG_CAP_TAN_SAT_ZANG.md`.
        ///
        /// Ha xuong = danh day hon = nhieu yen hon, doi lai rui ro server tu choi "thao tac qua nhanh"
        /// (va an 900s). KHONG can canh tay: TrainMode co bo do tu lui - thay server keu "qua nhanh"
        /// ngay sau mot don thi tu cong them 50 ms (tran 300) va ghi log.
        /// </summary>
        public int AttackGatePadMs { get; set; }

        /// <summary>
        /// "Toc do NextMap" - nghi bao nhieu ms GIUA hai lan doi map lien tiep khi di theo lo trinh
        /// nhieu chang (Navigator.DoGmNavigation). Clone o cau hinh cung ten cua ZangVPS (ho de 1500;
        /// ben ho la hang `dj_0.hS`, nap tu config index 46, ngu tai `av_0.java:1111` sau moi hop).
        ///
        /// LICH SU: truoc day la `AccountConfig.MapDelay` - CO that, `Navigator` CO doc that, nhung
        /// (a) khong co o nhap nao tren giao dien va (b) `ConfigManager` khong he serialize no
        /// => vinh vien ket o 500 ms, khong ai chinh duoc. Chuyen sang day de duoc luu theo dang
        /// key=value cua TrainConfig, KHONG phai dong vao format pipe cua accounts.txt.
        ///
        /// MAC DINH 1500 = BANG ZangVPS, va DAY LA THAY DOI CO ANH HUONG HANH VI (cu la 500).
        /// User chot 2026-09-06: *"vay de mac dinh 1500 di. neu muon nhanh hon toi tu chinh"*.
        /// DUNG "sua nguoc" ve 500 o phien sau voi ly do "them o cau hinh thi khong duoc doi hanh vi"
        /// - o day la co chu dich: moi hop cham them 1 giay nhung giam ap luc goi tin luc chuyen map
        /// (xem features/DISCONNECT_ZANGVPS.md SS0.bis "khuech dai 2" - chinh ZangVPS gan nhan
        /// "Fix 900s, Ban IP" cho nhom tuy chon ham toc cua ho).
        /// </summary>
        public int NextMapDelayMs { get; set; }

        /// <summary>
        /// "Dong bang quai" - clone ZangVPS `cbDongBang` (`Z:1866`): khi dang giu muc tieu VA da o
        /// trong tam, moi 750 ms gui mot goi move +/-5 px luan phien tren truc X.
        ///
        /// Vi sao dang thu (2026-09-06): day la thu DUY NHAT Zang bat ma ta khong co, do tren cung
        /// acc/map.
        ///
        /// ⚠️ DINH CHINH (doc lai ma nguon Zang 2026-09-06, vong 2): ban dau toi ghi o day rang
        /// "Zang khong can meo nay vi no co client that, biet toa do quai theo thoi gian thuc".
        /// SAI. Duong danh cua Zang cung doc `bf.ex/ey` = toa do SPAWN (`bf.java:2687-2692`, dung o
        /// `Z:1972/1989/2008`), y het ta - no CO toa do hoat anh (`bf.y/cf`) nhung KHONG dung cho
        /// cong tam. Tuc la ca hai ben deu nham vao diem spawn.
        ///
        /// Gia thuyet CON LAI (van chua kiem chung): quai co di chuyen THAT phia server; goi move
        /// +/-5 px giu no khong troi khoi diem spawn, nho vay diem spawn van la noi danh trung.
        /// Neu dung thi no giup CA HAI ben nhu nhau - tuc day KHONG phai cho ta thua Zang, ma la
        /// mot khoan cong them. PHAI A/B moi biet. Mac dinh TAT.
        /// </summary>
        public bool DongBangQuai { get; set; }

        /// <summary>
        /// "KC tan sat" - ban kinh (px) do tu DIEM NEO BAI: mob nam ngoai ban kinh nay thi KHONG
        /// duoc chon lam muc tieu. <c>0 = khong gioi han</c> (y het hanh vi cu).
        ///
        /// Clone ZangVPS <c>dj_0.z</c> (dat bang lenh chat `kts N`), bo loc cung thu 7 trong
        /// <c>Z.java:2630</c>: <c>dj_0.z != -1 &amp;&amp; ba.a(dj_0.A, dj_0.B, mob.ex, mob.ey) &lt;= dj_0.z</c>.
        /// Diem quan trong nhat cua ban goc: khoang cach do tu MOT DIEM NEO CO DINH, khong phai tu
        /// nhan vat - do tu nhan vat thi moi lan duoi la neo tu day ra, bot troi di vo han.
        ///
        /// Vi sao them (do tren log cua chinh ta, 2026-09-16 ts1):
        /// - `nsolite-120dotay.log` (86 acc dang tan sat): cac dong `[Flood]` luc DANG DANH co dang
        ///   `cmd 1 (di chuyen) x25-30, cmd 60 (danh quai) x1-2` - tuc moi giay bot bo ra 25-30 goi
        ///   DI CHUYEN cho 1-2 don danh.
        /// - `nsolite.log` 2026-09-10: 2976 lan `[Burst] Lao NHAY TANG` / 18 acc / 18 phut = 9
        ///   lan/phut/acc, phan lon la nhung cu lao 1400-1700 px sang dau kia map.
        ///
        /// ⚠️ AP CHO CA "San TATL": duong san Tinh Anh/Thu Linh (<c>FindEliteTarget</c>) von CO Y
        /// bo qua o neo de "bay" khap map - do chinh la duong de ra nhung cu lao xa nhat. Neu KC
        /// nay chi ap cho quai thuong thi no khong cat duoc gi ca.
        /// </summary>
        public int KcTanSat { get; set; }

        /// <summary>
        /// "Han che 900s" (tab Train) - di cham lai + giu luu luong hai chieu, CLONE bot Java Auto30
        /// (NINJA_LITE_AUTO_30 `ninja2.jar`, giai ma 2026-09-13). User chot 2026-09-13. Khi bat:
        /// <list type="number">
        /// <item>Buoc burst = speed x 8 px, cu (int)(speed*1.5) goi nghi 300 ms (`Class_cz.java:1770-1790`)
        ///   - <c>Navigator.CharBurstMove</c>, MOI noi goi (tan sat, doi map, luu toa do, PK, buff).</item>
        /// <item>Duoi quai: cach &lt;= 20 px thi khong gui lenh di (`Class_cw.java:262`) - <c>TrainMode.MoveToMob</c>.</item>
        /// <item>San 150 ms giua hai tick train (`Class_cw.java:38/93/158`) - <c>TrainMode.SanNhipTickMs</c>.</item>
        /// <item>Dung yen &gt; 1 s thi gui lai vi tri da gui (`Class_cz.java:311-313`) - <c>KeepAliveController</c>.</item>
        /// <item>Cmd 93 xem thong tin CHINH MINH moi 15 s (`Class_cv.java:151-153`) - <c>KeepAliveController</c>.</item>
        /// </list>
        /// Vi sao: bot ta gui 30-104 goi/giay, Auto30 uoc ~3-10; 72% lan dut khi dang choi (log 2026-09-11)
        /// la read-timeout luc dung im. CHUA do duoc la giam dis - phai A/B. Mac dinh TAT.
        /// </summary>
        public bool HanCheBan { get; set; }

        /// <summary>So mob toi da gom trong 1 packet danh AoE - thay skill.maxFight.</summary>
        public int MaxAoeMobs { get; set; }

        /// <summary>Giu focus mob bao lau (ms) neu khong danh duoc thi bo - giong Java this.v timeout 5s.</summary>
        public int FocusTimeoutMs { get; set; }

        /// <summary>
        /// TEMPLATE id cua skill danh (gia tri gui trong SELECT_SKILL cmd 41 - giong
        /// Service.selectSkill(skillTemplateId)). -1 = tu chon skill tan cong hop le
        /// (template.type==1, point>0) tu du lieu DataSync, uu tien skill nhieu point nhat.
        /// </summary>
        public int AttackSkillId { get; set; }

        /// <summary>
        /// CAP HOC (level nhan vat toi thieu de hoc duoc chieu) cua skill danh user chon.
        /// -1 = TU DONG: chon chieu co cap hoc cao nhat ma nhan vat dat >= MIN_POINT diem,
        /// khong co thi lay chieu nhieu diem nhat (xem AttackSkillSelector.Pick).
        /// <para><b>Vi sao luu CAP chu khong luu template id:</b> template id la duy nhat TOAN CUC
        /// theo lop (lop 1 = 1..9, lop 2 = 10..18...) nen copy config sang acc KHAC PHAI se tro
        /// nham sang chieu cua lop khac. Cap hoc thi lop nao cung co. Xem
        /// docs/features/CHON_CHIEU_THEO_CAP.md.</para>
        /// <para><b>Uu tien:</b> AttackSkillLv >= 0 -> dung cap. Nguoc lai AttackSkillId >= 0 ->
        /// dung template id (file cu, KHONG duoc lam mat cai dat cua user). Ca hai am -> tu dong.</para>
        /// </summary>
        public int AttackSkillLv { get; set; }

        // ---- Tab Auto (clone NSOTool/MODGAME Char.update) ----

        /// <summary>Tu dong uong binh HP (item type 16) khi HP% &lt; HpPercent.</summary>
        public bool UseHpPotion { get; set; }
        public int HpPercent { get; set; }

        /// <summary>Tu dong uong binh MP (item type 17) khi MP% &lt; MpPercent.</summary>
        public bool UseMpPotion { get; set; }
        public int MpPercent { get; set; }

        /// <summary>Tu dong an thuc an (item type 18) dung cap FoodLevel, dinh ky.</summary>
        public bool UseFood { get; set; }
        public int FoodLevel { get; set; }

        /// <summary>Tu dong dung chieu ho tro (skill template.type==2, tru 67-72) khi het cooldown.</summary>
        public bool UseBuff { get; set; }

        /// <summary>Tu dong dung phan than (skill template id 67-72) khi het cooldown.</summary>
        public bool UseClone { get; set; }

        /// <summary>Tu di toi / quay lai map muc tieu khi dang o map khac (ReMap).</summary>
        public bool ReMap { get; set; }

        // ===== Cum DOI KHU TU DONG - dac ta docs/features/DOI_KHU.md =====
        // Ba cong tac long nhau, cung quyet dinh MOT hanh dong: "het quai thi di dau".

        /// <summary>
        /// "Tan sat map trong" (MODGAME Char.dv / NSOCHIP ch.E_1). Chi co tac dung khi
        /// Khu = "Moi khu" (ANY_ZONE): thay vi dung nguyen khu server tha vao, chu dong
        /// chuyen sang KHU IT NGUOI NHAT luc moi toi map.
        /// </summary>
        public bool TanSatMapTrong { get; set; }

        /// <summary>
        /// "Chuyen Map Het Boss" (MODGAME Char.dz / NSOCHIP ch.I_1). Het muc tieu hop le
        /// trong khu -> NHAY SANG KHU KHAC. Luu y: ten nhan noi doi - ban goc cung chi doi KHU
        /// chu khong doi map, va dieu kien KHONG he nhac toi boss (chi la "khong con muc tieu
        /// hop le"), nen phu thuoc thang vao 3 o Danh Thuong / Tinh Anh / Thu Linh.
        /// </summary>
        public bool ChuyenMapHetBoss { get; set; }

        /// <summary>
        /// "Danh chuyen khu" (MODGAME NSOT_MOB.p "Danh CK" / NSOCHIP al.c_fld_2). Chi co nghia
        /// khi <see cref="ChuyenMapHetBoss"/> bat: nhay theo DANH SACH khu user nhap
        /// (<see cref="DanhChuyenKhuList"/>) thay vi chon khu vang nhat.
        /// </summary>
        public bool DanhChuyenKhu { get; set; }

        /// <summary>
        /// Dai khu cho "Danh chuyen khu": <c>0-29</c> · <c>1,3,5</c> · <c>0-9,20-29</c>.
        /// Doc bang <see cref="ZoneRange.Parse"/> (dung chung PC/Android).
        /// Rong hoac sai cu phap -> tu roi ve "khu vang nhat", KHONG duoc nem loi
        /// (ban goc NSOCHIP chia 0 o `% list.length` -> ArithmeticException trong auto-thread).
        /// CAM ky tu ';' - do la dau ngan field cua chinh dinh dang nay.
        /// </summary>
        public string DanhChuyenKhuList { get; set; }

        /// <summary>
        /// "Di map nhiem vu (NPC 25)": thay vi toi map co dinh (Train.Map), bot lay map nhiem vu
        /// hang ngay (DailyQuestMapId, server doi moi ngay) va di tat qua NPC 25 o Truong (1/27/72)
        /// -> teleport thang toi do roi tan sat. Clone flow MODGAME/NSOTRUNGDUC (canh Truong->TaskOrder.mapId).
        /// </summary>
        public bool GoToQuestMap { get; set; }

        /// <summary>
        /// "TS khi het MP" (nhan nguyen van ri[23] MODGAME) = tu sat (thuc su chet, KHONG de quai
        /// danh) khi server bao "Khong du MP de su dung" luc danh, de hoi sinh day MP. Clone Auto.j()
        /// / NSOT_MOB.o(): nhay xuong day map (TileMap.d = MapHeightPx) -> roi xuong vuc -> chet.
        /// Khong co nguong % - bat/tat thuan (giong toggle goc).
        /// </summary>
        public bool SuicideOnMp { get; set; }

        /// <summary>
        /// "Hoi sinh tai cho (ton luong)" - clone MODGAME lenh chat `hsl` (Auto.java:224-237).
        /// Chet -> gui cmd -10 wakeUpFromDead thay vi -9 ve lang: char dung day NGAY TAI BAI,
        /// khong co pha chay lai 6 map. Toi da 4 lan/1 lan chet, cach nhau 1000ms; het luot hoac
        /// het luong thi tu roi ve luong cu (ve lang).
        /// MAC DINH TAT: moi lan ton 1 LUONG. Voi vong "het MP -> tu sat" (~18 giay/lan) thi
        /// tuong duong ~200 luong/gio/account - phai la lua chon co y thuc cua user.
        /// </summary>
        public bool ReviveInPlace { get; set; }

        /// <summary>
        /// "Auto Mua Thức Ăn" (ri[22], MODGAME Char.dw): khi o map lang co shop (NPC template 4) ma
        /// trong tui het thuc an cap FoodLevel -> toi NPC 4, openMenu + menu(4,0,0) + buyItem_food
        /// (cmd 13: byte 9, byte tier=FoodLevel/10 [50->7], short qty). Chi mua duoc thuc an cap &lt;= 50.
        /// </summary>
        public bool AutoBuyFood { get; set; }

        /// <summary>
        /// "Cộng tiềm năng" (ri[30], MODGAME Char.ee) - HE NEN tu cong diem tiem nang (NSOT_MOB.java:789-801).
        /// Chia theo 4 o <see cref="UpPotSucManh"/>..<see cref="UpPotChakra"/>; ca 4 = 0 thi y ban goc: lop 2/4/6
        /// don Chakra, con lai Suc manh, ton &gt;= 100 thi 40 The luc + 60 chi so chinh. Lop 0 khong cong.
        /// MAC DINH TAT: lenh KHONG HOAN TAC. Xem docs/features/CONG_DIEM.md.
        /// </summary>
        public bool AutoUpPotential { get; set; }

        /// <summary>
        /// Chia tiem nang TU CHON cho "Cộng tiềm năng": so diem MOI DOT vao Suc manh / Than phap / The luc /
        /// Chakra (clone ZangVPS numSucManh..numChakra, cI.java:3089-3092). Ton &gt;= tong 4 so thi cong mot dot,
        /// chua du thi de danh. CA 4 = 0 (MAC DINH, user chot) -&gt; tu chia theo lop nhu MODGAME. 0..999.
        /// </summary>
        public int UpPotSucManh { get; set; }
        public int UpPotThanPhap { get; set; }
        public int UpPotTheLuc { get; set; }
        public int UpPotChakra { get; set; }

        /// <summary>
        /// "Cộng kĩ năng" (ri[31], MODGAME Char.ef) - HE NEN tu nang CHIEU DANG DANH (NSOT_MOB.java:772-787)
        /// het muc cap nhan vat cho phep. Chieu do da toi da thi DUNG, khong chuyen chieu (user chot Q1, y ban
        /// goc). MAC DINH TAT: lenh KHONG HOAN TAC. Xem docs/features/CONG_DIEM.md.
        /// </summary>
        public bool AutoUpSkill { get; set; }

        // ---- Danh theo nhom (clone NSOTRUNGDUC lenh "tsn": chat nhom "ts <map> <khu> <mob>") ----
        /// <summary>
        /// "Danh theo nhom" (MAC DINH BAT): THANH VIEN nghe chat nhom tu TRUONG, hieu 4 lenh nhu
        /// NSOTRUNGDUC: "ts &lt;map&gt; &lt;khu&gt; [mob]" (mob -1 = tan sat all) / "tsa &lt;map&gt; &lt;khu&gt;"
        /// (tuong thich cu) / "khu &lt;n&gt;" / "map &lt;n&gt;" -> ghi lenh RUNTIME
        /// (GameState.GroupOrderMapId/ZoneId, uu tien hon map/khu user cai qua
        /// NsoClient.EffectiveTargetMapId) - KHONG ghi de TargetMapId/Zone trong config.
        /// </summary>
        public bool AttackByGroup { get; set; }
        /// <summary>
        /// "Goi thanh vien nhom": TRUONG chat nhom "ts &lt;map&gt; &lt;khu&gt; -1" (NSOTRUNGDUC "tsn",
        /// -1 = tan sat all). KHONG phat dinh ky - chi phat khi lenh doi (map/khu) hoac co thanh vien
        /// moi vao nhom.
        /// </summary>
        public bool CallGroupMembers { get; set; }

        /// <summary>
        /// "Bam nhom truong" (MAC DINH TAT) - clone V9_X1 (`dc.java`: bo loc quai theo khoang cach toi
        /// TRUONG &lt;= 1000px, `f.java:1587`: danh dung con truong dang danh).
        /// THANH VIEN (khong phai truong) lay VI TRI SONG cua truong lam diem neo farm thay cho
        /// TargetX/TargetY user cai -> chi danh quai quanh truong, het quai thi di ve phia truong.
        /// TRUONG thi KHONG doi gi (van "ts" nhu thuong).
        /// Uu tien: Kich yen &gt; bam truong &gt; neo user cai.
        /// Mat dau truong (khac map/khu, hoac vi tri oi) -> ROI VE hanh vi cu, khong dung im.
        /// </summary>
        public bool FollowLeader { get; set; }
        /// <summary>
        /// Ban kinh o farm quanh truong (px), truc X. Truc Y dung <c>FollowRadius / 3</c> vi ban do NSO
        /// la NEN TANG - lech Y quan trong hon lech X rat nhieu (xem features/DONG_BANG_VI_TRI.md).
        /// V9_X1 dung Euclid &lt;= 1000; ta dung O CHU NHAT cho khop may moc neo san co.
        /// </summary>
        public int FollowRadius { get; set; }

        // ---- Loc nhat do (clone MODGAME NSOT_MOB.a(ItemTemplate)) ----
        // Khong khop filter nao + PickAll=false -> bo qua item do.

        /// <summary>Nhat yen (item type 19).</summary>
        public bool PickYen { get; set; }

        /// <summary>Nhat binh HP/MP (type 16/17) co level &gt;= PickPotionLv.</summary>
        public bool PickPotion { get; set; }
        public int PickPotionLv { get; set; }

        /// <summary>Nhat da/ngoc (type 26) co level &gt;= PickDaLv (level 0 = luon nhat).</summary>
        public bool PickDa { get; set; }
        public int PickDaLv { get; set; }

        /// <summary>
        /// "Luyen da Max" (MODGAME Char.dn): khi tui gan day (&lt;10 o trong) tu GOP 4 vien da
        /// cung cap (template type 26, cap = template.id + 1) thanh 1 vien cap+1 qua cmd 20
        /// (crystalCollectLock - da ket qua bi KHOA). Da dat cap tran (LuyenDaLv) thi ky gui
        /// vao ruong (ITEM_BAG_TO_BOX) de giai phong tui. Clone NSOT_MOB.java:703-790.
        /// </summary>
        public bool LuyenDaMax { get; set; }

        /// <summary>Cap tran luyen da (MODGAME Char.ep, clamp 4-12, mac dinh 7). Da cap &lt; LV gop len;
        /// da cham cap LV thi ky gui ruong.</summary>
        public int LuyenDaLv { get; set; }

        /// <summary>Nhat trang bi (type 0-15) co level &gt;= PickTrangBiLv.</summary>
        public bool PickTrangBi { get; set; }
        public int PickTrangBiLv { get; set; }

        /// <summary>Nhat VP nhiem vu (template type 23-25, giong ItemTemplate.b() cua MODGAME).</summary>
        public bool PickVpNhiemVu { get; set; }

        /// <summary>Nhat VP su kien (type 27, description bat dau "Vật phẩm sự kiện").</summary>
        public bool PickVpSuKien { get; set; }

        /// <summary>Nhat sach vo cong (type 27, name bat dau "Sách võ công").</summary>
        public bool PickSvc { get; set; }

        /// <summary>Nhat tat ca item con lai (fallback giong Char.dr cua MODGAME).</summary>
        public bool PickAll { get; set; }

        /// <summary>Khong nhat gi ca - de len tat ca cac co nhat do.</summary>
        public bool PickNothing { get; set; }

        /// <summary>
        /// "Nhat nhanh" (clone mod Tungvz NSOTRUNGDUC = co `pickOnce`, doc pickup-once.md).
        /// Hanh vi user chot: "GIU DI TOI item, chi BO vong retry" (KHONG phai nhat-tai-cho).
        /// Bat -> moi item van di toi noi nhung chi bam nhat 1 lan (thay vi 4) roi ve train ngay
        /// -> nhanh, do "giat", nhung DE BO SOT hon (char chua toi kip la lo, cho ~12s thu lai).
        /// Tat -> retry 4 lan (mac dinh). Chi tac dung khi AutoPickItem (cong tac tong) bat.
        /// </summary>
        public bool QuickPick { get; set; }

        // ---- Auto PK Am (clone MODGAME PK_AM_PANEL "Cai PK Am Tungvz" + AutoPkAm) ----
        // Ten field = nguyen van nhan tren panel MODGAME. PK Am co map/toa do RIENG (khong dung Target* cua Train).

        /// <summary>"Khu cho pk" (nst_khuCho) - khu DANH nguoi (pha cPk &lt;= 10). Mac dinh 5.</summary>
        public int PkAmKhuCho { get; set; }

        /// <summary>"Khu danh pk" (nst_khuDanh) - khu HAP diem khi karma cao (pha cPk &gt; 10). Mac dinh 6.</summary>
        public int PkAmKhuDanh { get; set; }

        /// <summary>"ID map PK" (nst_idMap). Mac dinh 6.</summary>
        public int PkAmIdMap { get; set; }

        /// <summary>"Toa do X" (nst_x). Mac dinh 354.</summary>
        public int PkAmX { get; set; }

        /// <summary>"Toa do Y" (nst_y). Mac dinh 120.</summary>
        public int PkAmY { get; set; }

        /// <summary>"Bao nhieu % thi pk :" (PK_AM_PANEL.c) - exp% trong cap dat nguong nay thi bat dau PK. Mac dinh 50.</summary>
        public int PkAmPercent { get; set; }

        /// <summary>"Dung PK khi am % :" - no exp vuot nguong nay (% cap) thi dung PK ve train.
        /// MODGAME hardcode 15 (AutoPkAm.java:19); o day cho cau hinh de tang/giam. Mac dinh 15.</summary>
        public int PkAmStopPercent { get; set; }

        /// <summary>"Giu level :" (THEM, MODGAME khong co - user chot 2026-09-15) - nhan vat
        /// chua toi cap nay thi KHONG mo phien PK Am. 0 = khong gioi han (mac dinh, config cu).</summary>
        public int PkAmMinLevel { get; set; }

        /// <summary>"Hien thong tin up" (isShow) - headless khong co overlay nen chi luu, khong tac dung.</summary>
        public bool PkAmShowInfo { get; set; }

        // ==================== AUTO DANH VONG (clone MODGAME AutoDanhVongPanel) ====================
        // 21 field. Auto nay KHONG phai mot "dang auto" trong dropdown Che do: no duoc day XEN NGANG
        // vao stack mode bang nut "Adv" hoac bo hen gio, xong thi tra ve dang dang chay.
        // ⚠️ Ben MODGAME toan bo cac gia tri nay la STATIC (1 account/1 JVM). Ben ta la config
        // theo TUNG account, nen 50 acc chay 50 cau hinh khac nhau duoc.

        /// <summary>"Đối thủ là" - TEN NHAN VAT acc phu de danh loi dai. So sanh PHAN BIET hoa/thuong.</summary>
        public string DvDoiThu { get; set; }

        /// <summary>"Map lôi đài" - PHAI la map Truong (1/27/72). Mac dinh 72.</summary>
        public int DvMapLoiDai { get; set; }

        /// <summary>"Khu lôi đài". Mac dinh 22. Acc chinh va acc phu PHAI cung map+khu moi thay nhau.</summary>
        public int DvKhuLoiDai { get; set; }

        /// <summary>"Map danh vọng" - map danh quai NV. &lt;= 0 = lay tu nhiem vu hang ngay (NPC 25).</summary>
        public int DvMapDanhVong { get; set; }

        /// <summary>"Khu danh vọng". &lt; 0 = KHONG doi khu.</summary>
        public int DvKhuDanhVong { get; set; }

        /// <summary>"Xu cược lôi đài". Mac dinh 1000. ⚠️ Hai ben lech thi ban goc KHONG canh bao.</summary>
        public int DvXuCuoc { get; set; }

        /// <summary>"Giờ Auto DV" (0..23). -1 = chua dat. Dung GIO VIET NAM.</summary>
        public int DvGio { get; set; }

        /// <summary>"Phút Auto DV" (0..59). -1 = chua dat.</summary>
        public int DvPhut { get; set; }

        /// <summary>"Tự đi làm DV" - bat bo hen gio. Moi ngay chay DUNG MOT lan.</summary>
        public bool DvTuDiLam { get; set; }

        /// <summary>"Map LTĐ (nv đánh quái)" - map bam "noi tro ve". Phai la Truong HOAC Lang, khong thi ep -1.</summary>
        public int DvMapLtd { get; set; }

        /// <summary>"Auto mua đồ?": 0 Ko tu mua · 1 Duoi 4X · 2 Duoi 5X · 3 Duoi 6X · 4 Duoi 7X · 5 Mua tat ca.</summary>
        public int DvAutoMuaDo { get; set; }

        /// <summary>"NV nâng cấp TB?": 0 Huy nhiem vu · 1 Dung auto · 2 Nhan va lam.</summary>
        public int DvNvNangCap { get; set; }

        /// <summary>"Nâng cấp max" - chi hien khi DvNvNangCap = 2. Mac dinh 5.</summary>
        public int DvNangCapMax { get; set; }

        /// <summary>"Nếu thiếu item thì?": 0 Dung auto · 1 Huy nhiem vu.</summary>
        public int DvThieuItem { get; set; }

        /// <summary>"Chỉ đập đồ đang + &lt;=" - chi hien khi DvNvNangCap = 2. Mac dinh 7.</summary>
        public int DvChiDapDoDang { get; set; }

        /// <summary>"Nhiệm vụ đánh quái?": 0 Bo qua boss · 1 Bem het.
        /// ⚠️ "Bem het" bat co San TATL va ban goc KHONG BAO GIO tat lai (anh huong vinh vien len tan sat).</summary>
        public int DvNvDanhQuai { get; set; }

        /// <summary>"Acc phụ thua LĐ - Tự thoát ra vào lại".
        /// ⚠️ O nay KHONG CO TAC DUNG - ban goc khong noi nao dat co <c>daChet</c> = true (§O.8.2).
        /// Giu tren UI cho dung ban goc, user chot 2026-09-08 clone y het.</summary>
        public bool DvAccPhuThuaLd { get; set; }

        /// <summary>"Auto danh vọng phù" - so lan dung phu/ngay, kep &lt;= 6. Item 705, gia 5 luong.</summary>
        public int DvDanhVongPhu { get; set; }

        /// <summary>"Huỷ nv đánh TA" (tinh anh). Mac dinh TAT.</summary>
        public bool DvHuyTa { get; set; }

        /// <summary>"Huỷ nv đánh TL" (thu linh). Mac dinh <b>BAT</b> - dung ban goc.</summary>
        public bool DvHuyTl { get; set; }

        /// <summary>"Huỷ nv lôi đài". Mac dinh TAT.</summary>
        public bool DvHuyLoiDai { get; set; }

        // ==================== DAP DO (nang cap trang bi dang mac) ====================
        // Clone panel "Dap do" cua MODGAME (AutoNangCapPanel) - dung 4 num. Xem
        // docs/features/DAP_DO_MODGAME.md muc F/G.

        /// <summary>"Bat dap do dang mac" (RMS goc: ncBat). Mac dinh TAT.</summary>
        public bool DapDo { get; set; }

        /// <summary>"Cap dich" +4..+16 (ncTarget). Mac dinh +8.</summary>
        public int DapDoCapDich { get; set; }

        /// <summary>
        /// "Da cho chang +7 -> +8" (ncDa8): CHI nhan 5 hoac 6 = tier 0-based cua da
        /// (5 = "Da 6", 6 = "Da 7"). O cap +7 sàn == trần == gia tri nay. Mac dinh 5.
        /// </summary>
        public int DapDoDaCho7 { get; set; }

        /// <summary>"Ghi log debug dap do" (ncLog). Mac dinh TAT.</summary>
        public bool DapDoLog { get; set; }

        // ==================== KICH YEN ====================
        // Clone NSOTRUNGDUC Class_gt (7 truong dau) + phan GOI ACC CHINH la nang cap rieng.
        // Xem docs/features/KICH_YEN.md.

        /// <summary>"Bat kich yen" (Class_gt.d). Mac dinh TAT.</summary>
        public bool KichYen { get; set; }

        /// <summary>
        /// "Chi dung im (khong danh, chi bao)" - LOAI TRU voi <see cref="KichYen"/>.
        ///
        /// Clone di toi map+khu nhu binh thuong roi DUNG YEN tai cho vua toi: khong chon muc tieu,
        /// khong danh, khong nhat do, khong ve neo, khong "Danh chuyen khu" - chi quet mang mob va
        /// bao Tinh anh/Thu linh cho acc chinh.
        ///
        /// Vi sao KHONG lam thanh mot <c>AutoModeKind</c> rieng (user chot 2026-09-06): lam vay phai
        /// dong bo 5 cho (enum, clamp doc config, hai mang ten mode cung o PC/Android, factory tao
        /// mode) ma 4 trong so do hong LANG LE - config se roi ve Tan sat khong bao loi. Mot co trong
        /// TrainConfig thi mode van la TanSat, khong cho nao phai biet den no ngoai cong chan.
        ///
        /// Chong kick idle da co san: <c>KeepAliveController</c> gui goi "dung nguyen cho" 60s/lan
        /// va duoc bat vo dieu kien trong <c>NsoClient.StartAutoSystems</c>.
        /// </summary>
        public bool KyDungIm { get; set; }

        /// <summary>Ap kich cho quai thuong (Class_gt.a).</summary>
        public bool KyNormal { get; set; }
        /// <summary>Ap kich cho Tinh Anh (Class_gt.b).</summary>
        public bool KyTinhAnh { get; set; }
        /// <summary>Ap kich cho Thu Linh (Class_gt.c).</summary>
        public bool KyThuLinh { get; set; }

        /// <summary>Nguong % HP de yen quai thuong (Class_gt.e). 0 = danh chet, 100 = khong danh.</summary>
        public int KyPctNormal { get; set; }
        /// <summary>Nguong % HP de yen Tinh Anh (Class_gt.f).</summary>
        public int KyPctTinhAnh { get; set; }
        /// <summary>Nguong % HP de yen Thu Linh (Class_gt.g).</summary>
        public int KyPctThuLinh { get; set; }

        /// <summary>Vai tro: 0 = khong dung, 1 = acc chinh, 2 = acc phu.</summary>
        public int KyRole { get; set; }

        /// <summary>
        /// Acc chinh: danh sach TEN NHAN VAT duoc phep goi minh (whitelist).
        /// Acc phu: ten acc chinh de goi (cho phep nhieu ten - bao cho tat ca).
        /// LUU bang dau ',' - TUYET DOI KHONG dung ';' vi do la dau ngan FIELD cua
        /// TrainConfig.Serialize/Parse (dung ';' se lam mat du lieu am tham, va no ngay
        /// khi bam Copy config vi ConfigCopy.Snapshot roundtrip qua chinh 2 ham do).
        /// UI van cho user go ';' rot chuan hoa ve ',' luc luu.
        /// </summary>
        public string KyPeers { get; set; }

        /// <summary>"Bat goi" - bat/tat rieng phan goi acc chinh.</summary>
        public bool KyCall { get; set; }
        /// <summary>Goi acc chinh khi thay Tinh Anh.</summary>
        public bool KyCallTinhAnh { get; set; }
        /// <summary>Goi acc chinh khi thay Thu Linh.</summary>
        public bool KyCallThuLinh { get; set; }

        /// <summary>Kenh goi: 0 = tu dong, 1 = chi noi bo (trong tool), 2 = chi chat rieng.</summary>
        public int KyChannel { get; set; }

        /// <summary>Acc chinh keo ca thanh vien nhom di cung (giong "tsn").</summary>
        public bool KyCallGroup { get; set; }

        /// <summary>Loi goi het han sau bao nhieu giay (elite co the da chet truoc khi toi noi).</summary>
        public int KyTtlSec { get; set; }

        /// <summary>San elite qua bao nhieu giay khong xong thi bo cuoc, lay viec ke.</summary>
        public int KyHuntTimeoutSec { get; set; }

        // ==================== LOC DO (docs/features/LOC_DO.md) ====================

        /// <summary>
        /// Che do LOC DO: <c>0</c> Tat (mac dinh) · <c>1</c> Chi danh sach · <c>2</c> Vip (ban &gt;5
        /// yen / vut &lt;=5) · <c>3</c> Vut do khong du chi so.
        ///
        /// <para>Ban goc co 4 mode (0 vut tai cho / 1 ban o lang / 2 tat / 3 vip); ta GOP 0+1 lam
        /// mot vi "vut hay ban" nay nam o TUNG DONG danh sach (<see cref="LocList"/>) chu khong con
        /// la thuoc tinh cua ca che do. Xem LOC_DO.md §A.</para>
        /// </summary>
        public int LocMode { get; set; }

        /// <summary>
        /// Danh sach loc - cac dong ngan bang <c>,</c>, moi dong <c>id:scope:action</c>.
        /// Doc/ghi bang <see cref="LocDoRule"/>. Vd <c>4:L:S,799:A:K,512:A:D</c>.
        /// </summary>
        public string LocList { get; set; }

        /// <summary>
        /// Tu VE LANG BAN khi tui gan day. Tat (mac dinh) = mon cho ban nam im trong tui cho toi
        /// khi co viec khac keo nhan vat ve lang (vd chuyen dap do).
        ///
        /// <para>⚠️ Bat len la them MOT duong TU SAT nua vao vong tan sat (chuyen ve lang di bang
        /// cach nhay vuc, y het chuyen dap do). Vi vay mac dinh TAT.</para>
        /// </summary>
        public bool LocVeLang { get; set; }

        /// <summary>
        /// Nguong mo chuyen: hanh trang con &lt;= bao nhieu O TRONG thi di ban. Mac dinh 4 =
        /// <c>LOC_ORAM</c> cua MODGAME. Chi co nghia khi <see cref="LocVeLang"/> bat.
        /// </summary>
        public int LocORam { get; set; }

        /// <summary>
        /// Nhip vong lap auto (ms). 100 = dung nhip NSOTRUNGDUC chay that (Class_bw.java:1110:
        /// <c>Thread.sleep(elapsed &lt; 100 ? 100 - elapsed : 10)</c>).
        /// DUNG CHUNG cho ca default lan buoc nang cap config cu - dung ghi cung so o hai noi.
        /// </summary>
        public const int DEFAULT_TICK_MS = 100;

        /// <summary>
        /// Mac dinh cua <see cref="AttackGatePadMs"/>. Giu 100 = KHONG doi hanh vi cua bat ky acc
        /// nao dang chay; ai muon nhanh bang Zang thi tu ha ve 0 o tab Train.
        /// </summary>
        public const int DEFAULT_ATTACK_GATE_PAD_MS = 100;

        /// <summary>
        /// Mac dinh o "Toc do NextMap" = 1500 ms, bang ZangVPS (`dj_0.hS`, `dj_0.java:3918`).
        /// Xem chu thich cua <see cref="NextMapDelayMs"/> ve ly do KHONG giu 500 nhu ban cu.
        /// </summary>
        public const int DEFAULT_NEXT_MAP_DELAY_MS = 1500;

        /// <summary>
        /// Muc pad ĐÃ CHẠY THẬT LÂU DÀI mà server chua bao gio che "qua nhanh" (chinh la hang
        /// NSOTRUNGDUC cu). Duoi muc nay = dang di vao vung chua kiem chung => TrainMode vu trang
        /// bo do "qua nhanh" (<c>CheckAttackTooFast</c>).
        ///
        /// CO Y TACH KHOI <see cref="DEFAULT_ATTACK_GATE_PAD_MS"/> du hai so dang bang nhau:
        /// neu sau nay ha mac dinh ve 0 cho bang ZangVPS ma bo do van do theo "mac dinh" thi dieu
        /// kien thanh `0 >= 0` = LUON DUNG => bo do tu tat dung luc can nhat. Hai so nay tra loi hai
        /// cau hoi khac nhau: "acc moi chay bao nhieu" va "bao nhieu thi coi la da kiem chung".
        /// </summary>
        public const int SAFE_ATTACK_GATE_PAD_MS = 100;

        /// <summary>
        /// Mac dinh cua <see cref="KcTanSat"/>. <c>0 = khong gioi han</c> = KHONG doi hanh vi cua
        /// bat ky acc nao dang chay. Ai muon cat bot cu lao xa thi tu dat o tab Train.
        /// </summary>
        public const int DEFAULT_KC_TAN_SAT = 0;

        public TrainConfig()
        {
            TickMs = DEFAULT_TICK_MS;
            AttackRangeX = 0;  // 0 = tu dong theo skill.Dx
            AttackRangeY = 0;  // 0 = tu dong theo skill.Dy
            PickRange = 150;
            DeathSpotTimeoutMin = 5;
            // Mac dinh (user chot 2026-06-15): danh ca thuong + tinh anh + thu linh + san TATL.
            HitNormal = true;
            HitTinhAnh = true;
            HitThuLinh = true;
            HuntTatl = true;
            ReturnToDeathSpot = true;
            AttackCooldownMs = 400;
            AttackGatePadMs = DEFAULT_ATTACK_GATE_PAD_MS;
            NextMapDelayMs = DEFAULT_NEXT_MAP_DELAY_MS;
            DongBangQuai = false;   // clone Zang cbDongBang - mac dinh False ben ho cung vay
            KcTanSat = DEFAULT_KC_TAN_SAT;   // 0 = khong gioi han (giu nguyen hanh vi cu)
            HanCheBan = false;      // "Han che 900s" - TAT de acc cu giu nguyen hanh vi
            MaxAoeMobs = 5;
            FocusTimeoutMs = 5000;
            AttackSkillId = -1;
            AttackSkillLv = -1;

            UseHpPotion = false;  // user chot: KHONG tu dung HP
            HpPercent = 20;
            UseMpPotion = false;
            MpPercent = 20;
            UseFood = true;       // an thuc an cap 50
            FoodLevel = 50;
            UseBuff = false;
            UseClone = false;
            ReMap = true;
            // Ca 3 MAC DINH TAT: bat san la doi hanh vi cua fleet dang chay on.
            TanSatMapTrong = false;
            ChuyenMapHetBoss = false;
            DanhChuyenKhu = false;
            DanhChuyenKhuList = "";
            GoToQuestMap = true;  // MAC DINH BAT (giong cac ban mod): tu di map nhiem vu qua NPC 25
            SuicideOnMp = true;   // user chot: TS khi het MP
            ReviveInPlace = false; // ton luong -> mac dinh TAT (giong NSOT_MOB.hsl cua MODGAME)
            AutoBuyFood = true;   // tu mua thuc an khi het
            AutoUpPotential = false; // lenh cong diem KHONG HOAN TAC -> phai la lua chon co y thuc
            AutoUpSkill = false;
            UpPotSucManh = 0; UpPotThanPhap = 0; UpPotTheLuc = 0; UpPotChakra = 0;   // ca 4 = 0: chia theo lop (user chot)
            AttackByGroup = true;    // MAC DINH BAT: thanh vien nghe trong nhom
            CallGroupMembers = false; // truong tu goi nhom (bat khi can)
            FollowLeader = false;     // user chot: MAC DINH TAT
            FollowRadius = 1000;      // so cua V9_X1 (`dc.java`), truc Y = /3

            // Mac dinh: chi nhat Yen + HP/MP (user chot 2026-06-15: bo Trang Bi).
            PickYen = true;
            PickPotion = true;
            PickPotionLv = 1;
            PickDa = false;
            PickDaLv = 1;
            LuyenDaMax = false;
            LuyenDaLv = 7;     // giong default MODGAME Char.ep = 7
            PickTrangBi = false;
            PickTrangBiLv = 1;
            PickVpNhiemVu = false;
            PickVpSuKien = false;
            PickSvc = false;
            PickAll = false;
            PickNothing = false;
            QuickPick = false;

            // Mac dinh PK Am (user chot 2026-06-15): 20/23/2/100/216/80.
            PkAmKhuCho = 20;
            PkAmKhuDanh = 23;
            PkAmIdMap = 2;
            PkAmX = 100;
            PkAmY = 216;
            PkAmPercent = 80;
            PkAmStopPercent = 15;
            PkAmMinLevel = 0;
            PkAmShowInfo = true;

            // ---- Auto Danh Vong: mac dinh CHEP NGUYEN tu AutoDanhVongPanel cua MODGAME ----
            DvDoiThu = "";
            DvMapLoiDai = 72;
            DvKhuLoiDai = 22;
            DvMapDanhVong = -1;
            DvKhuDanhVong = -1;
            DvXuCuoc = 1000;
            DvGio = -1;
            DvPhut = -1;
            DvTuDiLam = false;
            DvMapLtd = -1;
            DvAutoMuaDo = 2;      // "Duoi 5X"
            DvNvNangCap = 0;      // "Huy nhiem vu"
            DvNangCapMax = 5;
            DvThieuItem = 0;      // "Dung auto"
            DvChiDapDoDang = 7;
            DvNvDanhQuai = 0;     // "Bo qua boss"
            DvAccPhuThuaLd = false;
            DvDanhVongPhu = 0;
            DvHuyTa = false;
            DvHuyTl = true;       // ⚠️ mac dinh BAT - dung ban goc
            DvHuyLoiDai = false;
            DapDo = false;
            DapDoCapDich = 8;    // giong mac dinh panel MODGAME (+8)
            DapDoDaCho7 = 5;     // 5 = "Da 6"
            DapDoLog = false;

            // Kich yen: MAC DINH TAT toan bo (bat len la doi hanh vi danh quai -> phai co chu dich).
            KichYen = false;
            KyDungIm = false;
            KyNormal = false;
            KyTinhAnh = true;    // 2 loai nay moi la ly do ton tai cua tinh nang
            KyThuLinh = true;
            KyPctNormal = 30;
            KyPctTinhAnh = 30;
            KyPctThuLinh = 30;
            KyRole = 0;          // khong dung
            KyPeers = "";
            KyCall = false;
            KyCallTinhAnh = true;
            KyCallThuLinh = true;
            KyChannel = 0;       // tu dong: noi bo neu cung tool + cung server, khong thi chat rieng
            KyCallGroup = true;
            KyTtlSec = 120;
            KyHuntTimeoutSec = 90;

            // LOC DO: MAC DINH TAT hoan toan. Bat len la acc bat dau VUT/BAN do that trong tui,
            // khong hoan tac duoc -> phai do user co chu dich bat (LOC_DO.md §E.2).
            LocMode = 0;
            LocList = "";
            LocVeLang = false;   // them mot duong tu sat -> phai do user co chu dich bat
            LocORam = 4;         // = LOC_ORAM cua MODGAME
        }

        // ==================== KICH YEN - helper ====================

        /// <summary>
        /// Tach <see cref="KyPeers"/> thanh danh sach ten. Nhan ca ',' va ';' (user quen go ';')
        /// va khoang trang thua; bo phan tu rong. Dung cho ca whitelist (acc chinh) lan
        /// danh sach acc chinh can goi (acc phu).
        /// </summary>
        public static string[] SplitPeers(string s)
        {
            if (string.IsNullOrEmpty(s)) return new string[0];
            var raw = s.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            var list = new System.Collections.Generic.List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string t = raw[i].Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>Chuan hoa chuoi user go ve dang luu duoc: ngan bang ',' (xem <see cref="KyPeers"/>).</summary>
        public static string NormalizePeers(string s)
        {
            return string.Join(",", SplitPeers(s));
        }

        /// <summary>
        /// Chuan hoa o "Dai khu" truoc khi luu. GIU NGUYEN chuoi user go (ke ca dang "0-9,20-29")
        /// de mo lai UI van thay dung cai minh nhap - CHI loai 3 ky tu se pha dinh dang luu tru:
        /// ';' (ngan field cua TrainConfig), '|' (ngan cot cua accounts.txt), '=' (ngan key/value).
        /// Cu phap sai thi de nguyen: <see cref="ZoneRange.Parse"/> tra danh sach rong va cum
        /// "Danh chuyen khu" tu roi ve "khu vang nhat" - khong ai duoc nem loi vi chuoi nay.
        /// </summary>
        public static string NormalizeZoneList(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                if (c != ';' && c != '|' && c != '=') sb.Append(c);
            return sb.ToString().Trim();
        }

        // ==================== SERIALIZE (key=value;...) ====================
        // Dung dau ';' va '=' (KHONG dung '|') de nhet vua 1 field trong accounts.txt pipe-format.
        // Them field moi: them 1 dong o Serialize + 1 case o Parse -> tu dong tuong thich nguoc
        // (file cu thieu key -> giu default; key la -> bo qua).

        public string Serialize()
        {
            return string.Join(";", new[]
            {
                "TickMs=" + TickMs,
                "AttackRangeX=" + AttackRangeX,
                "AttackRangeY=" + AttackRangeY,
                "PickRange=" + PickRange,
                "DeathSpotTimeoutMin=" + DeathSpotTimeoutMin,
                "HitNormal=" + (HitNormal ? "1" : "0"),
                "HitTinhAnh=" + (HitTinhAnh ? "1" : "0"),
                "HitThuLinh=" + (HitThuLinh ? "1" : "0"),
                "HuntTatl=" + (HuntTatl ? "1" : "0"),
                "ReturnToDeathSpot=" + (ReturnToDeathSpot ? "1" : "0"),
                "AttackCooldownMs=" + AttackCooldownMs,
                "MaxAoeMobs=" + MaxAoeMobs,
                "FocusTimeoutMs=" + FocusTimeoutMs,
                "AttackSkillId=" + AttackSkillId,
                "AttackSkillLv=" + AttackSkillLv,
                "UseHpPotion=" + (UseHpPotion ? "1" : "0"),
                "HpPercent=" + HpPercent,
                "UseMpPotion=" + (UseMpPotion ? "1" : "0"),
                "MpPercent=" + MpPercent,
                "UseFood=" + (UseFood ? "1" : "0"),
                "FoodLevel=" + FoodLevel,
                "UseBuff=" + (UseBuff ? "1" : "0"),
                "UseClone=" + (UseClone ? "1" : "0"),
                "ReMap=" + (ReMap ? "1" : "0"),
                "TanSatMapTrong=" + (TanSatMapTrong ? "1" : "0"),
                "ChuyenMapHetBoss=" + (ChuyenMapHetBoss ? "1" : "0"),
                "DanhChuyenKhu=" + (DanhChuyenKhu ? "1" : "0"),
                "DanhChuyenKhuList=" + NormalizeZoneList(DanhChuyenKhuList),
                "GoToQuestMap=" + (GoToQuestMap ? "1" : "0"),
                "SuicideOnMp=" + (SuicideOnMp ? "1" : "0"),
                "ReviveInPlace=" + (ReviveInPlace ? "1" : "0"),
                "AutoBuyFood=" + (AutoBuyFood ? "1" : "0"),
                "AutoUpPotential=" + (AutoUpPotential ? "1" : "0"),
                "AutoUpSkill=" + (AutoUpSkill ? "1" : "0"),
                "UpPotSucManh=" + UpPotSucManh,
                "UpPotThanPhap=" + UpPotThanPhap,
                "UpPotTheLuc=" + UpPotTheLuc,
                "UpPotChakra=" + UpPotChakra,
                "AttackByGroup=" + (AttackByGroup ? "1" : "0"),
                "CallGroupMembers=" + (CallGroupMembers ? "1" : "0"),
                "PickYen=" + (PickYen ? "1" : "0"),
                "PickPotion=" + (PickPotion ? "1" : "0"),
                "PickPotionLv=" + PickPotionLv,
                "PickDa=" + (PickDa ? "1" : "0"),
                "PickDaLv=" + PickDaLv,
                "LuyenDaMax=" + (LuyenDaMax ? "1" : "0"),
                "LuyenDaLv=" + LuyenDaLv,
                "PickTrangBi=" + (PickTrangBi ? "1" : "0"),
                "PickTrangBiLv=" + PickTrangBiLv,
                "PickVpNhiemVu=" + (PickVpNhiemVu ? "1" : "0"),
                "PickVpSuKien=" + (PickVpSuKien ? "1" : "0"),
                "PickSvc=" + (PickSvc ? "1" : "0"),
                "PickAll=" + (PickAll ? "1" : "0"),
                "PickNothing=" + (PickNothing ? "1" : "0"),
                "QuickPick=" + (QuickPick ? "1" : "0"),
                "PkAmKhuCho=" + PkAmKhuCho,
                "PkAmKhuDanh=" + PkAmKhuDanh,
                "PkAmIdMap=" + PkAmIdMap,
                "PkAmX=" + PkAmX,
                "PkAmY=" + PkAmY,
                "PkAmPercent=" + PkAmPercent,
                "PkAmStopPercent=" + PkAmStopPercent,
                "PkAmShowInfo=" + (PkAmShowInfo ? "1" : "0"),
                "DapDo=" + (DapDo ? "1" : "0"),
                "DapDoCapDich=" + DapDoCapDich,
                "DapDoDaCho7=" + DapDoDaCho7,
                "DapDoLog=" + (DapDoLog ? "1" : "0"),

                "KichYen=" + (KichYen ? "1" : "0"),
                "KyDungIm=" + (KyDungIm ? "1" : "0"),
                "KyNormal=" + (KyNormal ? "1" : "0"),
                "KyTinhAnh=" + (KyTinhAnh ? "1" : "0"),
                "KyThuLinh=" + (KyThuLinh ? "1" : "0"),
                "KyPctNormal=" + KyPctNormal,
                "KyPctTinhAnh=" + KyPctTinhAnh,
                "KyPctThuLinh=" + KyPctThuLinh,
                "KyRole=" + KyRole,
                // Da chuan hoa ve dau ',' - neu con sot ';' thi no se cat vo format nen chan lai o day.
                "KyPeers=" + NormalizePeers(KyPeers),
                "KyCall=" + (KyCall ? "1" : "0"),
                "KyCallTinhAnh=" + (KyCallTinhAnh ? "1" : "0"),
                "KyCallThuLinh=" + (KyCallThuLinh ? "1" : "0"),
                "KyChannel=" + KyChannel,
                "KyCallGroup=" + (KyCallGroup ? "1" : "0"),
                "KyTtlSec=" + KyTtlSec,
                "KyHuntTimeoutSec=" + KyHuntTimeoutSec,
                // Field moi APPEND O CUOI (quy uoc CONVENTIONS.md) - ban cu thieu 2 dong nay van
                // parse duoc, chi la nhan gia tri mac dinh.
                "AttackGatePadMs=" + AttackGatePadMs,
                "DongBangQuai=" + (DongBangQuai ? "1" : "0"),
                "NextMapDelayMs=" + NextMapDelayMs,
                "FollowLeader=" + (FollowLeader ? "1" : "0"),
                "FollowRadius=" + FollowRadius,

                // ---- Auto Danh Vong (21 field, append o CUOI cho tuong thich nguoc) ----
                "DvDoiThu=" + NormalizeZoneList(DvDoiThu),
                "DvMapLoiDai=" + DvMapLoiDai,
                "DvKhuLoiDai=" + DvKhuLoiDai,
                "DvMapDanhVong=" + DvMapDanhVong,
                "DvKhuDanhVong=" + DvKhuDanhVong,
                "DvXuCuoc=" + DvXuCuoc,
                "DvGio=" + DvGio,
                "DvPhut=" + DvPhut,
                "DvTuDiLam=" + (DvTuDiLam ? "1" : "0"),
                "DvMapLtd=" + DvMapLtd,
                "DvAutoMuaDo=" + DvAutoMuaDo,
                "DvNvNangCap=" + DvNvNangCap,
                "DvNangCapMax=" + DvNangCapMax,
                "DvThieuItem=" + DvThieuItem,
                "DvChiDapDoDang=" + DvChiDapDoDang,
                "DvNvDanhQuai=" + DvNvDanhQuai,
                "DvAccPhuThuaLd=" + (DvAccPhuThuaLd ? "1" : "0"),
                "DvDanhVongPhu=" + DvDanhVongPhu,
                "DvHuyTa=" + (DvHuyTa ? "1" : "0"),
                "DvHuyTl=" + (DvHuyTl ? "1" : "0"),
                "DvHuyLoiDai=" + (DvHuyLoiDai ? "1" : "0"),

                // ---- Loc do (append o CUOI cho tuong thich nguoc) ----
                "LocMode=" + LocMode,
                // NormalizeZoneList = loc 3 ky tu pha dinh dang `; | =`. Dinh dang LocList von chi
                // dung `,` va `:` nen day chi la luoi an toan cho chuoi user sua tay.
                "LocList=" + NormalizeZoneList(LocList),
                "LocVeLang=" + (LocVeLang ? "1" : "0"),
                "LocORam=" + LocORam,

                // ---- Han che 900s (append o CUOI cho tuong thich nguoc) ----
                "HanCheBan=" + (HanCheBan ? "1" : "0"),

                // ---- PK Am "Giu level" (append o CUOI cho tuong thich nguoc) ----
                "PkAmMinLevel=" + PkAmMinLevel,

                // ---- KC tan sat (append o CUOI cho tuong thich nguoc) ----
                "KcTanSat=" + KcTanSat,
            });
        }

        public static TrainConfig Parse(string s)
        {
            var cfg = new TrainConfig();
            if (string.IsNullOrEmpty(s)) return cfg;

            foreach (string pair in s.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq);
                string val = pair.Substring(eq + 1);
                int iv;
                bool isInt = int.TryParse(val, out iv);

                switch (key)
                {
                    // 200/50/100 deu la gia tri auto da tung lam default (khong co UI nen chua tung chinh tay)
                    // -> dua het ve DEFAULT_TICK_MS hien tai. Gia tri khac giu nguyen (neu sau nay them UI).
                    // Truoc day ve day ghi cung so 50: khi default doi 150->100 (commit f2219ae) cho nay
                    // khong doi theo, nen MOI acc doc tu accounts.txt van chay 50ms = GAP DOI nhip that
                    // cua NSOTRUNGDUC (Class_bw.java:1110 sleep 100ms) -> 20 tick/giay, dinh nguong
                    // chong flood 25 goi/giay (do duoc 26-54 goi/giay trong log 2026-09-05).
                    case "TickMs": if (isInt) cfg.TickMs = (iv == 200 || iv == 50 || iv == 100) ? DEFAULT_TICK_MS : iv; break;
                    case "AttackRangeX": if (isInt) cfg.AttackRangeX = iv; break;
                    case "AttackRangeY": if (isInt) cfg.AttackRangeY = iv; break;
                    case "PickRange": if (isInt) cfg.PickRange = iv; break;
                    case "DeathSpotTimeoutMin": if (isInt) cfg.DeathSpotTimeoutMin = iv; break;
                    // SkipBoss cu: 0 = danh ca boss -> bat het cac loai; key moi (neu co,
                    // nam sau trong chuoi) se ghi de lai.
                    case "SkipBoss":
                        if (val == "0") { cfg.HitTinhAnh = true; cfg.HitThuLinh = true; cfg.HuntTatl = true; }
                        break;
                    case "HitNormal": cfg.HitNormal = val == "1"; break;
                    case "HitTinhAnh": cfg.HitTinhAnh = val == "1"; break;
                    case "HitThuLinh": cfg.HitThuLinh = val == "1"; break;
                    case "HuntTatl": cfg.HuntTatl = val == "1"; break;
                    case "HitTatl": cfg.HuntTatl = val == "1"; break; // legacy key -> doi sang HuntTatl
                    case "ReturnToDeathSpot": cfg.ReturnToDeathSpot = val == "1"; break;
                    case "AttackCooldownMs": if (isInt) cfg.AttackCooldownMs = iv; break;
                    case "MaxAoeMobs": if (isInt) cfg.MaxAoeMobs = iv; break;
                    case "FocusTimeoutMs": if (isInt) cfg.FocusTimeoutMs = iv; break;
                    case "AttackSkillId": if (isInt) cfg.AttackSkillId = iv; break;
                    case "AttackSkillLv": if (isInt) cfg.AttackSkillLv = iv; break;
                    case "UseHpPotion": cfg.UseHpPotion = val == "1"; break;
                    case "HpPercent": if (isInt) cfg.HpPercent = iv; break;
                    case "UseMpPotion": cfg.UseMpPotion = val == "1"; break;
                    case "MpPercent": if (isInt) cfg.MpPercent = iv; break;
                    case "UseFood": cfg.UseFood = val == "1"; break;
                    case "FoodLevel": if (isInt) cfg.FoodLevel = iv; break;
                    case "UseBuff": cfg.UseBuff = val == "1"; break;
                    case "UseClone": cfg.UseClone = val == "1"; break;
                    case "ReMap": cfg.ReMap = val == "1"; break;
                    case "TanSatMapTrong": cfg.TanSatMapTrong = val == "1"; break;
                    case "ChuyenMapHetBoss": cfg.ChuyenMapHetBoss = val == "1"; break;
                    case "DanhChuyenKhu": cfg.DanhChuyenKhu = val == "1"; break;
                    case "DanhChuyenKhuList": cfg.DanhChuyenKhuList = NormalizeZoneList(val); break;
                    case "GoToQuestMap": cfg.GoToQuestMap = val == "1"; break;
                    case "SuicideOnMp": cfg.SuicideOnMp = val == "1"; break;
                    case "ReviveInPlace": cfg.ReviveInPlace = val == "1"; break;
                    case "AutoBuyFood": cfg.AutoBuyFood = val == "1"; break;
                    case "AutoUpPotential": cfg.AutoUpPotential = val == "1"; break;
                    case "AutoUpSkill": cfg.AutoUpSkill = val == "1"; break;
                    case "UpPotSucManh": if (isInt) cfg.UpPotSucManh = iv; break;
                    case "UpPotThanPhap": if (isInt) cfg.UpPotThanPhap = iv; break;
                    case "UpPotTheLuc": if (isInt) cfg.UpPotTheLuc = iv; break;
                    case "UpPotChakra": if (isInt) cfg.UpPotChakra = iv; break;
                    case "AttackByGroup": cfg.AttackByGroup = val == "1"; break;
                    case "CallGroupMembers": cfg.CallGroupMembers = val == "1"; break;
                    case "PickYen": cfg.PickYen = val == "1"; break;
                    case "PickPotion": cfg.PickPotion = val == "1"; break;
                    case "PickPotionLv": if (isInt) cfg.PickPotionLv = iv; break;
                    case "PickDa": cfg.PickDa = val == "1"; break;
                    case "PickDaLv": if (isInt) cfg.PickDaLv = iv; break;
                    case "LuyenDaMax": cfg.LuyenDaMax = val == "1"; break;
                    case "LuyenDaLv": if (isInt) cfg.LuyenDaLv = iv; break;
                    case "PickTrangBi": cfg.PickTrangBi = val == "1"; break;
                    case "PickTrangBiLv": if (isInt) cfg.PickTrangBiLv = iv; break;
                    case "PickVpNhiemVu": cfg.PickVpNhiemVu = val == "1"; break;
                    case "PickVpSuKien": cfg.PickVpSuKien = val == "1"; break;
                    case "PickSvc": cfg.PickSvc = val == "1"; break;
                    case "PickAll": cfg.PickAll = val == "1"; break;
                    case "PickNothing": cfg.PickNothing = val == "1"; break;
                    case "QuickPick": cfg.QuickPick = val == "1"; break;
                    case "PkAmKhuCho": if (isInt) cfg.PkAmKhuCho = iv; break;
                    case "PkAmKhuDanh": if (isInt) cfg.PkAmKhuDanh = iv; break;
                    case "PkAmIdMap": if (isInt) cfg.PkAmIdMap = iv; break;
                    case "PkAmX": if (isInt) cfg.PkAmX = iv; break;
                    case "PkAmY": if (isInt) cfg.PkAmY = iv; break;
                    case "PkAmPercent": if (isInt) cfg.PkAmPercent = iv; break;
                    case "PkAmStopPercent": if (isInt) cfg.PkAmStopPercent = iv; break;
                    case "PkAmMinLevel": if (isInt) cfg.PkAmMinLevel = iv; break;
                    case "PkAmShowInfo": cfg.PkAmShowInfo = val == "1"; break;
                    case "DapDo": cfg.DapDo = val == "1"; break;
                    case "DapDoCapDich": if (isInt && iv >= 4 && iv <= 16) cfg.DapDoCapDich = iv; break;
                    case "DapDoDaCho7": if (isInt && (iv == 5 || iv == 6)) cfg.DapDoDaCho7 = iv; break;
                    case "DapDoLog": cfg.DapDoLog = val == "1"; break;

                    case "KichYen": cfg.KichYen = val == "1"; break;
                    case "KyDungIm": cfg.KyDungIm = val == "1"; break;
                    case "KyNormal": cfg.KyNormal = val == "1"; break;
                    case "KyTinhAnh": cfg.KyTinhAnh = val == "1"; break;
                    case "KyThuLinh": cfg.KyThuLinh = val == "1"; break;
                    case "KyPctNormal": if (isInt && iv >= 0 && iv <= 100) cfg.KyPctNormal = iv; break;
                    case "KyPctTinhAnh": if (isInt && iv >= 0 && iv <= 100) cfg.KyPctTinhAnh = iv; break;
                    case "KyPctThuLinh": if (isInt && iv >= 0 && iv <= 100) cfg.KyPctThuLinh = iv; break;
                    case "KyRole": if (isInt && iv >= 0 && iv <= 2) cfg.KyRole = iv; break;
                    case "KyPeers": cfg.KyPeers = NormalizePeers(val); break;
                    case "KyCall": cfg.KyCall = val == "1"; break;
                    case "KyCallTinhAnh": cfg.KyCallTinhAnh = val == "1"; break;
                    case "KyCallThuLinh": cfg.KyCallThuLinh = val == "1"; break;
                    case "KyChannel": if (isInt && iv >= 0 && iv <= 2) cfg.KyChannel = iv; break;
                    case "KyCallGroup": cfg.KyCallGroup = val == "1"; break;
                    case "KyTtlSec": if (isInt && iv >= 10 && iv <= 3600) cfg.KyTtlSec = iv; break;
                    case "KyHuntTimeoutSec": if (isInt && iv >= 10 && iv <= 3600) cfg.KyHuntTimeoutSec = iv; break;
                    // Tran 0..1000: am la vo nghia (danh nhanh hon cooldown thi server bo qua don do
                    // chu khong lam gi khac), tren 1000 thi acc gan nhu dung yen - chan ca hai dau
                    // ngay o day de mot dong accounts.txt hong khong lam acc ngung farm ma khong ai biet.
                    case "AttackGatePadMs": if (isInt && iv >= 0 && iv <= 1000) cfg.AttackGatePadMs = iv; break;
                    case "DongBangQuai": cfg.DongBangQuai = val == "1"; break;
                    // Tran 0..5000: 0 = tat (khong gioi han); duoi ~100 px thi o san hep hon ca
                    // tam chieu (acc se dung im) nen chan luon o 100; tren 5000 thi rong hon moi
                    // map cua game, dat cung nhu tat. Chan ca hai dau ngay day de mot dong
                    // accounts.txt hong khong lam acc ngung farm ma khong ai biet.
                    case "KcTanSat":
                        if (isInt && iv >= 0 && iv <= 5000) cfg.KcTanSat = (iv > 0 && iv < 100) ? 100 : iv;
                        break;
                    // Tran 0..10000: 0 = khong nghi them (chi cho su kien map nap), tren 10 giay thi
                    // mot tuyen 4 hop mat hon 40 giay - chan ca hai dau ngay day de mot dong
                    // accounts.txt hong khong lam acc bo ra hang phut moi lan di toi bai.
                    case "NextMapDelayMs": if (isInt && iv >= 0 && iv <= 10000) cfg.NextMapDelayMs = iv; break;
                    case "FollowLeader": cfg.FollowLeader = val == "1"; break;
                    // Tran 100..5000: duoi 100px thi o farm hep hon ca tam danh (thanh vien khong bao
                    // gio chon duoc quai nao -> dung im), tren 5000 thi rong hon moi map cua game nen
                    // "bam" khong con y nghia. Chan ca hai dau ngay day.
                    case "FollowRadius": if (isInt && iv >= 100 && iv <= 5000) cfg.FollowRadius = iv; break;

                    // ---- Auto Danh Vong ----
                    case "DvDoiThu": cfg.DvDoiThu = NormalizeZoneList(val); break;
                    case "DvMapLoiDai": if (isInt) cfg.DvMapLoiDai = iv; break;
                    case "DvKhuLoiDai": if (isInt) cfg.DvKhuLoiDai = iv; break;
                    case "DvMapDanhVong": if (isInt) cfg.DvMapDanhVong = iv; break;
                    case "DvKhuDanhVong": if (isInt) cfg.DvKhuDanhVong = iv; break;
                    case "DvXuCuoc": if (isInt && iv >= 0) cfg.DvXuCuoc = iv; break;
                    case "DvGio": if (isInt && iv >= -1 && iv <= 23) cfg.DvGio = iv; break;
                    case "DvPhut": if (isInt && iv >= -1 && iv <= 59) cfg.DvPhut = iv; break;
                    case "DvTuDiLam": cfg.DvTuDiLam = val == "1"; break;
                    case "DvMapLtd": if (isInt) cfg.DvMapLtd = iv; break;
                    case "DvAutoMuaDo": if (isInt && iv >= 0 && iv <= 5) cfg.DvAutoMuaDo = iv; break;
                    case "DvNvNangCap": if (isInt && iv >= 0 && iv <= 2) cfg.DvNvNangCap = iv; break;
                    case "DvNangCapMax": if (isInt && iv >= 0) cfg.DvNangCapMax = iv; break;
                    case "DvThieuItem": if (isInt && iv >= 0 && iv <= 1) cfg.DvThieuItem = iv; break;
                    case "DvChiDapDoDang": if (isInt && iv >= 0) cfg.DvChiDapDoDang = iv; break;
                    case "DvNvDanhQuai": if (isInt && iv >= 0 && iv <= 1) cfg.DvNvDanhQuai = iv; break;
                    case "DvAccPhuThuaLd": cfg.DvAccPhuThuaLd = val == "1"; break;
                    // Kep <= 6: ban goc cung kep o buoc Save cua form.
                    case "DvDanhVongPhu": if (isInt && iv >= 0) cfg.DvDanhVongPhu = iv > 6 ? 6 : iv; break;
                    case "DvHuyTa": cfg.DvHuyTa = val == "1"; break;
                    case "DvHuyTl": cfg.DvHuyTl = val == "1"; break;
                    case "DvHuyLoiDai": cfg.DvHuyLoiDai = val == "1"; break;

                    // ---- Loc do ----
                    // Mode la ngoai 0..3 (file hong / ban sau ha bot mode) -> ve 0 = TAT, khong
                    // phai giu nguyen so la: mot con so khong hieu duoc KHONG duoc phep tro thanh
                    // "che do nao do" roi tu vut do cua user.
                    case "LocMode": cfg.LocMode = (isInt && iv >= 0 && iv <= 3) ? iv : 0; break;
                    // Ban nay khong co Loc do -> giu NGUYEN VAN chuoi de accounts.txt quay ve
                    // NSOLITEPRO khong bi mat cau hinh (ben do moi co LocDoRule de chuan hoa).
                    case "LocList": cfg.LocList = val; break;
                    case "LocVeLang": cfg.LocVeLang = val == "1"; break;
                    // Tran 1..20: 0 o trong = tui DA day cung, cho toi luc do moi di la muon
                    // (khong con o de nhat); tren 20 thi gan nhu luc nao cung dat nguong -> acc
                    // di ban lien tuc. Chan ca hai dau ngay day de mot dong accounts.txt hong
                    // khong lam acc bo bai train.
                    case "LocORam": if (isInt && iv >= 1 && iv <= 20) cfg.LocORam = iv; break;

                    // ---- Han che 900s ----
                    case "HanCheBan": cfg.HanCheBan = val == "1"; break;
                }
            }

            // CHUAN HOA loai tru "Bat kich yen" <-> "Chi dung im". Giao dien da chan bang tay o ca hai
            // ban PC/Android, nhung file accounts.txt con den tu: user sua tay, ban cu, va "Cai dat
            // nhanh kich yen" (KichYenQuick.Apply). Vao day duoc CA HAI cung bat thi giao dien se hien
            // hai o cung tick - dung trang thai ma chinh no tuyen bo la khong cho phep.
            // Chot KyDungIm THANG vi do la thu tu THAT luc chay: cong "dung im" o TrainMode.CombatTick
            // dat truoc toan bo phan danh, nen KichYen coi nhu khong ton tai.
            if (cfg.KyDungIm) cfg.KichYen = false;
            return cfg;
        }
    }
}
