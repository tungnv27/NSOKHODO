# Buổi test 2 chặng — checklist

> Theo SPEC D50 / §13. **Chặng 1 hỏng ở bước nào → DỪNG buổi test**, gửi MINH log (mục "Khi hỏng").
> Qua hết chặng 1 mới sang chặng 2. Ghi kết quả ngay cạnh từng ô: `[x]` đạt · `[!]` hỏng + ghi chú.
>
> Bản build: `src/NSOKHODO/bin/Release/net452/NSOKHODO.exe` (build 2026-09-17 0h, bộ kiểm tra offline PASS).
> Code **chưa commit**: commit sau khi bạn xác nhận đạt.

## ✅ MINH đã chạy thật trên server (16/09 tối)

Bạn test nhận đồ xong, MINH tự chạy tiếp bằng kho không giao diện + `tungkhodo9` / `tungkhodo8` đóng vai
người chơi (`tools/kiemtra/song.ps1`). Món chỉ đi giữa các acc của bạn; cuối buổi **tồn kho khớp từng món**
với tổng đã nạp (kể cả 11 món bạn nạp bằng `barbigz999` / `barbigz998`), và **mỗi loại nằm trên một nick**.

| Mục | Kết quả |
|---|---|
| Chặng 1 bước 1 — nạp (M1, M9) | ✅ (bạn test + MINH) |
| Chặng 1 bước 2 — cất rương, dọn sang clone (M7b: **rương 30 ô**) | ✅ |
| Chặng 1 bước 3 — rút một phần chồng, tách `−85` (M8), bên giao không có gói xoá ô (M2) | ✅ |
| Chặng 1 bước 4 — người nhận thiếu ô → tạm dừng → `tiep` (M18: *"Đối phương không đủ ô trống…"*) | ✅ |
| Lệnh chat `kho` `tim` `co` `goi` `huy` `lay` `theo` `botheo` `nap`, sai cú pháp | ✅ |
| Gộp nhiều lệnh cùng người nhận vào một lượt giao | ✅ |
| Rao 5 giây khi có người ngoài; báo sự kiện ra chat khu | ✅ |
| Đồ xếp chồng về cùng nick (D71); server tự gộp chồng khi nhận **và** khi cất rương | ✅ |
| Lệnh quá hạn / tạm dừng khi mở lại kho; giữ chỗ khi mở lại (D68) | ✅ |
| Leader dự phòng: tắt Leader → 61 giây sau dự phòng nhận vai, nhận nạp, dọn về nhà; bật lại → 16 giây sau trả vai | ✅ |

**Vì vậy chặng 1 bạn KHÔNG cần làm lại.** Còn lại cho bạn: phần **giao diện** và những thứ cần người thật
(mục "Còn lại cho bạn" cuối file).

### Sau lỗi bạn gặp tối 16/09 (MINH chạy lại 17/09 0h, `tungnv2` không đụng tới)

| Mục | Kết quả |
|---|---|
| Rút **30** Tử tinh thạch trung cấp từ acc túi đầy (12 trong túi + 19 trong rương) | ✅ 30/30 trong 26 s, 3 lượt 12/12/6 (D73) |
| Đơn **nhiều ID** (4 dòng), nhiều clone lần lượt giao | ✅ tungkhodo7 giao 2 dòng qua 3 lượt (tự cất tạm 4 món lấy chỗ); tới người nhận **đầy túi** → tạm dừng đúng (D75) |
| Nạp trả rồi dọn về clone; tổng từng loại khớp | ✅ |
| Cờ "có hạn" gộp một dòng (D72); đọc lại rương sau đăng nhập (D74) | ✅ |
| Giới hạn login 4 acc / máy chủ (D76) | ✅ 9 acc xếp hàng, mỗi giây một acc |
| Nhân vật "bay" (D77): người khác nhìn Leader qua 3 nhịp chống AFK | ✅ luôn về 216 (bản cũ: kẹt 164) |
| Giao diện: khung Đơn đang soạn, mục Đăng nhập (dựng `MainForm` với danh sách trống, chụp ảnh) | ✅ hiển thị; **chưa** chạy với acc thật |

### Sau lỗi "chỉ còn 0/17" (MINH chạy lại 17/09 1h)

| Mục | Kết quả |
|---|---|
| Dựng lại đúng tình huống: tungkhodo6 túi 30/30 + rương 30/30, rút 17 × 457 | ✅ lệnh **không** tạm dừng; tự trả 12 viên đá cho Leader (4 s) rồi giao đủ 17/17 (12 s) — D78 |
| Dọn kho chừa 1 ô túi (tungkhodo2 dừng ở 1 ô trống, rương đầy) | ✅ |
| Giao ở **khu 5** (người nhận đứng xa điểm vào) | ✅ đạt ngay lần đầu sau khi thêm chờ 2,5 s (bản trước hỏng lần đầu — M24) |
| Khu 7 / khu 9 **chưa có người nhận** → người nhận tới sau | ✅ clone đợi 90 s, 60 s sau quay lại giao; không tính hỏng, không lặp câu báo — D79 |
| Chat `lay 456 1 khu 7`; sai cú pháp trả lời gọn một tin | ✅ |
| Nạp trả hết, tổng từng loại khớp; nhịp chống AFK bản cuối vẫn về 216 | ✅ |

### Gom đồ, cửa xả, log dễ đọc (MINH chạy 17/09 10:00–10:30, bản cuối 10:19 / 10:29, `tungnv2` không đụng tới)

| Mục | Kết quả |
|---|---|
| **Gom** (D81) trên dữ liệu thật: 10 loại xếp chồng nằm trên cả tungkhodo1 lẫn tungkhodo4 | ✅ 1 giao dịch (5 s) tungkhodo4 → tungkhodo1, 268 món; cất rương gộp vào chồng sẵn có; tungkhodo4 trống thêm 10 ô |
| **Cửa xả tự mở** khi Leader dưới ngưỡng mà Chủ kho mời (D82) | ✅ 3 clone sang khu chính, đứng thành hàng x = 310 / 420 / 475; tin nhắn liệt kê clone |
| Xả 2 lượt × 12 vào một clone → clone rời cửa đi cất rương, clone khác vào thay | ✅ tungkhodo5 → tungkhodo2; tin "Cua xa doi" |
| `xa xong`, `xa` | ✅ đóng / mở đúng, báo số món |
| Leader dọn khi Chủ kho **vẫn đứng cạnh** (túi Leader dưới ngưỡng) | ✅ 8 s sau lượt nạp thứ hai Leader tự cất rương (bản cũ đứng im) |
| **Lệnh khu 7** 20 viên: 10 trên clone, 12 trên Leader (D84) | ✅ clone giao 8 + 2, Leader dọn 12 sang tungkhodo3 (ưu tiên), tungkhodo3 giao 10 → **20/20 trong 29 s** |
| **Gỡ kẹt** bản cuối: tungkhodo8 túi 30/30 + rương 30/30, rút 2 món chỉ có trong rương nó | ✅ tự trả 12 ô cho Leader ngay khi đọc rương; lệnh chờ gỡ kẹt → giao đủ trong 11 s |
| Log dễ đọc (harness in dòng `[SK]`) + ảnh khung log hai chế độ, ô *Gom đồ* | ✅ `[GIAO] tungkhodo8 → tungkhodo9: 8 Đá cấp 7 (lệnh #42)`… |
| Nạp trả hết đồ test (tungkhodo9 rỗng túi) | ✅ |

### Xả nhanh qua Leader (MINH chạy 17/09 11:00–11:38, bản cuối 11:34, `tungnv2` không đụng tới)

| Mục | Kết quả |
|---|---|
| Đo: mời người đang giao dịch (M27) | ✅ câu *"Người chơi đang chờ hoàn thành một giao dịch khác."*, 3 s sau mời lại tới — không khoá |
| Đo: bị từ chối / nhận rồi huỷ (M28) | ✅ từ chối → khoá 30 s; nhận rồi huỷ → mời lại ngay |
| Người chơi nạp **3 lượt liền** (12 + 12 + 5), xong phiên là mời ngay | ✅ **17,3 s** / bản cuối (11:52) **16,8 s**, 0 lần khoá 30 s (bản đầu 37,9 s, 1 lần khoá) |
| Leader đầy (lượt 3) → nhận rồi huỷ + tin "moi lai sau ~3 giay"; mời trúng lúc Leader đang chuyển → tự mời lại sau 3 s | ✅ |
| Leader chuyển sang clone đứng cạnh (D86) | ✅ 3 lượt: 12 → tungkhodo5, 12 → tungkhodo5, 5 → tungkhodo3; mỗi lượt 2–3 s; tungkhodo5 đầy → đi cất rương, tungkhodo6 vào thay |
| Chỗ đứng cùng tầng đất | ✅ bản cuối: clone đứng 395 (Leader 365–371), không lần "quá xa"; bản đầu đứng 335 → rơi xuống y 288 (M29) |
| `xa xong` | ✅ đóng đợt, tin *"Xa xong (Chu kho bao xong): Leader da chuyen 29 mon sang clone"* |
| Log dễ đọc | ✅ `[NẠP] … → Leader`, `[CHUYỂN] Leader tungkhodo → tungkhodo5: 12 Đá cấp 7`, `[XẢ] Xong đợt xả …` |
| Dọn sạch | ✅ tungkhodo9 rỗng túi, túi Leader trống, clone rảnh |

## 0. Chuẩn bị (~10 phút)

**Người và đồ:**
- **Acc chính** (bạn, client thường) — làm **Chủ kho**.
- **Leader** + **2–3 clone** cho chặng 1 (chặng 2 dùng đủ ~10 clone + 1 dự phòng). Clone cấp 1 là được (T1).
- *(Chặng 2)* một **acc người lạ** (không nằm trong danh sách Chủ kho).
- Đồ rẻ: **một chồng ≥10** món xếp chồng được (đá, bình…) và **1–2 món không chồng** (không khoá).

**Cài đặt trên tool:**
1. Thêm acc (Leader + clone) như NSOBAOTATL. **Mọi acc phải cùng máy chủ** — acc khác máy chủ hiện "KHÁC MÁY CHỦ" và không tính vào kho.
2. Tab **Acc** → chuột phải acc Leader → **Đặt làm Leader** *(chặng 2: thêm "Đặt làm Leader dự phòng")*.
3. Tab **Cài đặt → Kho**:
   - Map `22` (Làng Tone); **Khu chính** = khu vắng bạn chọn; **Khu phụ** = khu khác (để trống cũng
     chạy — D64 — nhưng nên cài).
   - ⚠️ **Tránh khu 0 / khu thấp** làm khu chính: server xếp người mới đăng nhập vào khu thấp nhất còn
     chỗ, giờ cao điểm các khu thấp đầy (18:56 bot phải vào khu 21–22) → Leader có thể không vào được khu chính.
   - **Chủ kho**: tên nhân vật acc chính (mỗi dòng một tên).
   - Còn lại để mặc định.
4. Nút **Ghi log** trên thanh nút phải **đang bật** (mặc định bật).
5. **▶ Chạy** (không chọn dòng nào → hỏi "chạy tất cả" → Có).

**Kỳ vọng sau ~1 phút:**
- Leader đứng ở khu chính; clone về khu phụ.
- Mỗi clone tự đi tới **Thủ khố** một lần ("Đọc rương"); cột **Rương trống** trên tab Acc hiện `x/y`.
- Thanh trạng thái có `Kho a/b`; tab Nhật ký → "Mọi thứ (app.log)" có dòng `[Kho] Ruong: N o`.

> 📌 **Ghi lại số ô rương** (M7b): ______ ô.

**Log nằm ở:** `Logs/<tên danh sách>/<yyyy-MM-dd>/` cạnh file exe (nút "Mở thư mục ngày" ở tab Nhật ký).

---

## CHẶNG 1 — LÕI GIAO DỊCH (~15 phút)

### Bước 1 — Nạp 1 món cho Leader (M1)
1. Acc chính tới khu chính, mời Leader giao dịch (bấm tay).
2. Đặt **1 món không chồng**, khoá, đồng ý.

- [ ] Leader nhận lời, khoá khung rỗng, **~1,5 giây sau** mới đồng ý (M9); giao dịch xong.
- [ ] Tab **Tổng kho** hiện món đó.
- [ ] Chat khu: `@NNN Da nhan 1 x <ten mon> tu <ban>`. Tin riêng tới bạn cùng nội dung — **không dấu, có tem**.
- [ ] `giaodich.csv` có dòng `NAP … XONG`; `hex.log` có `cmd=43`, `cmd=37`, `cmd=45`, `cmd=58`.

### Bước 2 — Leader cất rương, rồi dọn sang clone (M7b)
Leader **chỉ dọn khi không có Chủ kho trong khu chính** (SPEC §6), nên:
1. Acc chính **đổi sang khu khác** (hoặc rời map).

- [ ] ~10 giây sau, Leader đi tới Thủ khố, trạng thái "Cất rương", rồi **về đúng chỗ cũ**.
- [ ] Cột Rương trống của Leader có số; `hex.log` có `cmd=31` rồi `cmd=17`.
- [ ] Sau đó Leader **gọi một clone** sang khu chính ("Chờ nhận từ …"), lấy món ra (`cmd=16`), đi tới clone và giao.
- [ ] `giaodich.csv` có dòng `DON … XONG` (Leader) và `NHAN_DON … XONG` (clone).
- [ ] Clone quay về khu phụ, tự "Cất rương". Tab Tổng kho: món nằm trên clone (cột Phân bố `ten 0/1`).

### Bước 3 — Rút một phần chồng (M8, M2, M9)
1. Nạp **cả chồng ≥10** (như bước 1), rồi rời khu chính, chờ Leader dọn sang clone.
2. Lấy ID món: nhắn riêng Leader `tim <tu khoa>` → bot trả `ID ten xSL`.
3. Quay lại khu chính, nhắn riêng Leader: `lay <ID> 3`.

- [ ] Leader trả: `Lenh #N: 3 x <ten> cho <ban>. Dang chuan bi`.
- [ ] Clone giữ món: lấy chồng ra khỏi rương → **tách 3** → đổi sang khu chính → đi sát bạn → mời.
- [ ] app.log có `Tach chong xong: o A -> o B x3` (**M8: −85 chạy thật**).
- [ ] Bạn nhận lời, khung hiện **đúng 3 món**; bạn khoá (không đặt gì) + đồng ý → xong.
- [ ] Bạn có **đúng 3**; kho còn **7** (tab Tổng kho). Tin `Xong lenh #N: da giao 3 mon…`.
- [ ] **M2:** trong `hex.log`, ngay sau `cmd=58` của clone có gói túi nào (`cmd=8`/`9`/`-30`…) không? Ghi lại: ______

### Bước 4 — Người nhận thiếu ô (M18)
1. Làm đầy túi acc chính (**0 ô trống**).
2. Nhắn `lay <ID> 1`.

- [ ] Clone mời, bạn nhận lời, server đóng phiên → Leader nhắn `… thieu o hanh trang … Don tui roi nhan: tiep #N`.
- [ ] Tab **Hàng chờ**: lệnh ở **TẠM DỪNG**, lý do "nguoi nhan thieu o hanh trang".
- [ ] Dọn 1 ô, nhắn `tiep #N` → giao lại được, lệnh **Xong**.
- [ ] **M18:** mở `giaodich.csv`, cột `TinServer` của phiên hỏng. Chép nguyên văn: ______

**✅ Chặng 1 đạt → sang chặng 2.**

### Khi hỏng
1. Ghi: bước nào, thấy gì, lúc mấy giờ.
2. Bấm **■ Dừng** hoặc đóng app (để log ghi nốt).
3. Nén gửi MINH:
   - thư mục `Logs/<danh sách>/<ngày>/`;
   - thư mục `Data/Kho/` (cài đặt, sổ kho, hàng chờ — **không có mật khẩu**).
   - **Đừng** gửi `Data/Accounts/`.

---

## CHẶNG 2 — MỌI TÍNH NĂNG (theo SPEC §13 P1–P4)

Không cần theo thứ tự. Mọi tính năng có công tắc ở **Cài đặt → Kho → Bật/tắt từng tính năng**, để khoanh vùng khi có lỗi.

### A. Nạp, chat, rao (P1)
- [ ] **Toạ độ Leader:** Cài đặt → Kho → *Lấy chỗ Leader đang đứng*. Leader đi Thủ khố xong **quay về đúng chỗ**, kể cả sau khi đăng nhập lại. *Xoá toạ độ* → Leader đứng yên chỗ đang đứng.
- [ ] Nạp tay **≥5 lượt**, tổng ~50 món + xu. Tổng kho + cột Xu khớp túi / rương thật.
- [ ] Nạp bằng **`gdvp`** (NSOTRUNGDUC) được.
- [ ] **Người lạ** nạp được (chế độ *Tất cả*) và **không nhận tin riêng nào**.
- [ ] Chế độ *Chỉ Chủ kho* → người lạ bị từ chối ngay khi mời.
- [ ] Người lạ đang giao dịch với Leader, bạn nhắn **`nap`**:
  - [ ] phiên người lạ bị huỷ;
  - [ ] bot trả `San sang nhan K mon`;
  - [ ] bạn mời được;
  - [ ] trong 60 giây người lạ mời lại thì bị từ chối.
- [ ] Người lạ đặt **quá số ô trống** của Leader → phiên bị huỷ, không mất đồ. Nếu người đó là bạn thì nhận tin `Khong nhan: …`. Lý do có thể là bot tự từ chối, hoặc server đóng phiên trước (T3). Ghi lại câu nhận được: ______
- [ ] Mọi tin bot gửi (xem `chat.log`) đều **có tem `@NNN` và không dấu**.
- [ ] **Rao:**
  - [ ] câu rao đổi số sau mỗi lượt nạp;
  - [ ] có người ngoài trong khu → ~5 giây/tin;
  - [ ] khu vắng → ~5 phút/tin (Cài đặt → Chat & rao có xem trước câu rao).
- [ ] **Lệnh chat:**
  - [ ] `kho` · `tim <tu>` · `co <id>` · `goi`;
  - [ ] `theo <id> 5` rồi rút xuống dưới 5 → nhận báo;
  - [ ] `botheo <id>`;
  - [ ] chat thường không phải lệnh → bot im.
- [ ] **M10b** (độ dài tối đa tin riêng): `tim` với từ khoá rộng → tin dài ~90 ký tự tới đủ, không bị cắt? ______
- [ ] **M12:** ~12 acc đứng cùng khu phụ có bị server đẩy sang khu khác không (cột Khu)? ______
- [ ] **M13:** chạy ≥2 giờ, cột **Mất KN** có tăng không? ______
- [ ] *(nếu chạy qua đêm)* sang ngày mới thì log sang thư mục ngày mới.

### B. Rút (P2)
- [ ] Rút **1 món** / **cả chồng** / **một phần chồng**.
- [ ] Món **rải trên 3 clone**: `lay <id> het` → từng clone lần lượt sang giao, lệnh Xong.
- [ ] Giao cho người **không phải Chủ kho**:
  - [ ] từ tool: tab Tổng kho → chọn món → Điều phối → gõ tên → **Giao**;
  - [ ] qua chat: `lay <id> 2 cho <ten>`.
  - Người đó không nhận tin riêng; bạn nhận tiến độ.
- [ ] Người nhận dùng **`nhan`** (NSOTRUNGDUC) thay vì bấm tay.
- [ ] **Hai Chủ kho** cùng xin món chỉ đủ một người → lệnh sau báo `… dang giu cho lenh khac`, tạm dừng.
- [ ] Món có **nhiều cấp +** mà không ghi cấp → lệnh bị huỷ, bot bảo ghi `+cap`. `lay <id> 1 +N` → giao đúng cấp.
- [ ] Người nhận **không nhận lời mời** 2 lần → lệnh tạm dừng (`… Lam lai: tiep #N`).
- [ ] Người nhận không tới khu chính trong 10 phút → lệnh tự huỷ.
- [ ] **Gói rút:** Cài đặt → Gói rút → tạo gói (vd `thu`: 2 món) → nhắn `lay goi thu` → giao đủ.
  - [ ] Gói có món kho không đủ → tạm dừng; `tiep` giao phần đang có.
- [ ] Tab **Hàng chờ**: nút **Tiếp** / **Huỷ** hoạt động; tên tab hiện số lệnh chờ.

### C. Dọn kho, kệ, rác, xu (P3)
- [ ] Nạp liên tục ~100 món nhiều lượt: không lượt nào bị từ chối quá 1 phút; dọn xong thì túi Leader về trống; Tổng kho khớp.
- [ ] **Kệ:** tab Acc → chuột phải clone → *Kệ của clone* (vd một clone "Trang bị"). Đồ mặc đi về đúng clone đó.
- [ ] Kệ đầy → đồ tràn sang kệ "Khác", app.log có `KE_TRAN`.
- [ ] **Rác:** tab Tổng kho → chuột phải món → *Đánh dấu rác*; gán một clone kệ "Rác".
  - [ ] Món đó chỉ vào clone kệ Rác.
  - [ ] Kệ Rác ≥80% → bot báo `Ke Rac day …`.
- [ ] **Nhả clone:** chọn clone kệ Rác → **Nhả clone**.
  - [ ] Bot đợi xong việc rồi đăng xuất; cột Vai = "Đã nhả".
  - [ ] Bạn đăng nhập tay dọn đồ rồi **thoát hẳn**; **Nhận lại** → bot vào lại, đọc lại rương.
  - [ ] app.log có `NHA_CLONE … truoc … | sau …`; sổ kho đúng; **không bị đá acc**.
- [ ] **Dồn xu:** Cài đặt → Kho → đặt *Leader vượt mức này thì dồn xu* thấp (vd 1.000.000). Nạp xu vượt mức → Leader gọi clone ít xu nhất sang nhận phần vượt. Không clone nào vượt trần.

### D. Hoàn thiện (P4)
- [ ] **Leader dự phòng:** tab Acc → chọn Leader chính → **■ Dừng**.
  - [ ] ~60 giây sau, dự phòng sang khu chính, chat khu `Leader moi: …`.
  - [ ] **▶ Chạy** lại Leader chính → ~10 giây sau (khi dự phòng rảnh) trả vai.
- [ ] **Báo cáo ngày:** Cài đặt → Theo dõi → giờ báo cáo = 2–3 phút tới → nhận tin `Hom nay: nhap … xuat …`.
- [ ] Tab **Nhật ký**: chọn ngày / loại, lọc theo tên → đúng dòng.
- [ ] Tắt từng công tắc tính năng → tính năng đó dừng, phần khác vẫn chạy.

---

## Quyết định MINH tự chốt lúc code — cần bạn xác nhận hoặc bác

Chi tiết ở SPEC §2 "Quyết định vòng 9". Tóm tắt:

| # | Nội dung | Đồng ý? |
|---|---|---|
| D52 | Người mời không có trong danh sách cùng khu → Leader **vẫn nhận lời**, kiểm tên khi khung mở | |
| D54 | Lệnh thiếu hàng → **tạm dừng**; `tiep` = giao phần đang có; tạm dừng quá 30 phút tự huỷ | |
| D55 | Món nhiều cấp mà không ghi `+cap` → huỷ lệnh, bắt ghi rõ | |
| D58 | **Mọi acc trong danh sách là thành viên kho** (không còn nút Bật/Tắt từng acc — cho nghỉ thì Nhả) | |
| D60 | Dọn kho: clone đứng yên, Leader đi tới; clone ở lại khu chính tới khi hết món cùng kệ | |
| D61 | Mỗi lúc **một** việc rút trong cả kho; Leader / dự phòng cũng giao nếu đang giữ hàng | |
| D62 | `nap` không huỷ phiên Leader đang **giao lệnh rút** | |
| D63 | Hỏng phía acc giao → đổi acc khác, 3 lần mới tạm dừng; hỏng phía người nhận → 2 lần tạm dừng | |

| D64 | **Khu phụ để trống được**; chỉ thiếu khu chính mới đứng im (có cảnh báo) | |
| D65 | Clone vào game chưa đủ 90 giây thì chưa cho nhận hàng (server chặn lời mời ~60 giây đầu) | |
| D66 | Server báo "do not accept" → mời lại sau 10 giây; "quá xa" → lại sát, mời lại (≤3 lần) | |
| D67 | Tới sát người nhận: lệch cao ≤60 px thì giữ độ cao của bot | |
| D68 | Mở kho: 5 phút đầu không nhả giữ chỗ của acc chưa kịp đăng nhập | |
| D69 | Chốt chặn gói rương (≤1 gói xin rương / 2 giây, ≤3 lần / việc…) | |
| D70 | Một lượt dọn gom cả túi lẫn rương của Leader | |
| D71 | **Đồ xếp chồng về cùng nick**; đồ cùng loại / cùng nhóm ưu tiên nick đang giữ | |
| — | Leader **từ chối phiên nạp rỗng** (khoá 0 món, 0 xu) | |
| D72 | **Cờ "có hạn" không tách dòng** (server báo cờ này lung tung cho cùng một món); rút không còn ưu tiên món có hạn | |
| D73 | **Giao theo lượt**: túi đầy vẫn giao được; túi đầy mà chưa có gì giao → cất tạm món khác vào rương lấy chỗ | |
| D74 | Mỗi lần acc vào game → đọc lại rương một lần | |
| D75 | **Đơn nhiều món** trên khung Điều phối (+ Vào đơn / Giao đơn) — **thêm vào bố cục A, cần bạn duyệt** | |
| D76 | Giới hạn login cùng lúc theo máy chủ (Cài đặt → Kho → Đăng nhập), mặc định tắt | |
| D77 | Nhịp chống AFK kết thúc bằng 3 gói về chỗ cũ (chữa lỗi "bay") | |
| D78 | **Gỡ kẹt**: clone túi + rương đầy tự trả bớt ≤ 12 món cho Leader; dọn kho luôn chừa 1 ô túi; lệnh chờ gỡ kẹt thay vì tạm dừng | |
| D79 | **Khu giao cho từng lệnh** (ô *Khu giao* trên Điều phối — **thêm vào bố cục, cần bạn duyệt**; chat `khu N`); Leader không đi giao khu riêng | |
| D80 | Cấp **thú cưỡi** không thuộc khoá món (rương / giao dịch không có byte cấp); `+cấp` cho thú cưỡi bị bỏ qua, có báo | |
| D81 | **Gom đồ xếp chồng** rải nhiều clone về một nick (nick trống làm trung gian khi nick giữ hàng chật) — ô mới *Cài đặt → Kho → Gom đồ…* **cần bạn duyệt** | |
| D82 | ~~Cửa xả: giao thẳng vào clone~~ (thay bằng D86); Leader dọn được cả khi bạn đứng chờ | |
| D83 | **Log dễ đọc** mặc định; ô *Chi tiết (kỹ thuật)* trên khung log — **cần bạn duyệt**; tab Nhật ký có mục "Dễ đọc" | |
| D84 | Lệnh khu riêng mà hàng đang trên Leader → chờ Leader dọn sang clone rồi giao (không huỷ "kho không có") | |
| D85 | Khu chính chưa cài → hộp thoại khi bấm Chạy + ô trạng thái đỏ | |
| D86 | **Xả nhanh**: bạn chỉ giao cho Leader; 2 clone đứng cạnh Leader, Leader chuyển sang ngay sau mỗi lượt (2–3 s). `xa` = gọi sẵn clone, `xa xong` = đã nạp xong | |
| D87 | Leader đầy → **nhận rồi huỷ** lời mời của bạn (không từ chối → không bị khoá 30 s), nhắn mời lại sau ~3 s | |

D53 (cmd 22 là tách **trang bị**) và D57 (không bao giờ dùng Khả di lệnh để đổi khu) là **sửa sai / chặn mất đồ**, không phải lựa chọn.

## Giới hạn đã biết của bản này

- **Chưa đối chiếu túi trước / sau** sau mỗi lần nạp (SPEC N9 "LECH"): cột `Lech` trong `giaodich.csv` để trống. Sổ kho cập nhật liên tục từ túi thật, nên lệch vẫn tự sửa ở giây sau.
- Tên người nhận **có dấu cách** không dùng được trong `lay … cho <ten>` → ra lệnh từ tool.
- `hex.log` chỉ ghi chiều **server → bot**; gói bot gửi đi thì xem thứ tự trong `app.log` (`[GD giao]`, `[GD nhan]`).
- `chat.log` ghi mọi tin riêng vào / ra và tin khu **bot gửi**; **chưa** ghi tin khu của người khác.
- Chưa có cảnh báo riêng cho đồ có hạn: chỉ có cột "Hạn" ở Tổng kho (D72: "có" nếu có món nào được server báo
  có hạn — cờ này không đáng tin). Khi rút **không** còn ưu tiên món có hạn.
- Người nhận phải đủ ô cho **từng lượt** (≤ 12 ô). Đá (vd. Đá cấp 7) **không xếp chồng** — 20 viên là 20 ô.
- Nhân vật **không cầm vũ khí**: nhịp tự đánh 60 giây làm server báo "Vũ khí không thích hợp" (chỉ là một dòng
  log; kết nối vẫn giữ). Bot không tự mặc vũ khí (D38).
- Gom (D81) chỉ gom đồ **xếp chồng**; đá / trang bị cùng loại mỗi món vẫn một ô nên không gom. Đợt gom chờ khi có
  lệnh rút hoặc cửa xả đang mở.
- Xả nhanh (D86): lượt nạp đầu mở đợt, clone cần ~4 s để tới đứng cạnh → nạp 3 lượt liền thì lượt 3 thường bị
  "nhận rồi huỷ" một lần; mời trúng lúc Leader đang chuyển thì server báo "đang chờ hoàn thành một giao dịch khác" —
  bấm lại sau vài giây. Đồ xả vào clone đứng cạnh **không theo kệ** (đồ xếp chồng thì Gom gộp lại sau). Đồ **Rác**
  không chuyển tiếp (dọn kho thường đưa về kệ Rác).
- Thú cưỡi (D80): kho không phân biệt cấp thú cưỡi.
- Đồ người chơi nạp cho **Leader dự phòng**: nó tự dọn về nick "nhà" khi còn giữ vai (test sống đạt). Nếu Leader
  chính quay lại giữa chừng thì phần chưa dọn nằm trong rương dự phòng (vẫn tính vào kho, rút được).

## Còn lại cho bạn (cần giao diện hoặc người thật)

- [ ] Mở app bản mới: 5 tab hiện đủ; thanh trạng thái; **Cài đặt → Kho** đặt **Khu phụ** (và cân nhắc đổi khu chính khỏi khu 0).
- [ ] Tab **Tổng kho**: thấy đủ món; Điều phối → **Giao ngay** cho một người; **+ Vào đơn** vài món → **Giao đơn**; **Rút gói**.
- [ ] Giao diện có còn giật không (để tab Tổng kho mở vài phút lúc kho đang dọn).
- [ ] **Khu giao**: đặt ô Khu giao = một khu khác, Giao ngay → tab Hàng chờ ghi "· khu N", clone sang đúng khu đó.
- [ ] **Cài đặt → Kho → Đăng nhập**: bật giới hạn, bấm Chạy — tab Acc có dòng "CHỜ SLOT" nền vàng.
- [ ] Tab **Hàng chờ**: nút **Tiếp** / **Huỷ**.
- [ ] Tab **Acc**: đặt Leader / dự phòng bằng chuột phải; **Nhả clone** → đăng nhập tay dọn → **Nhận lại**.
- [ ] Tab **Nhật ký**: chọn ngày / file, lọc.
- [ ] Chế độ **Chỉ Chủ kho**; `nap` chen ngang một người lạ đang giao dịch.
- [ ] `gdvp` / `nhan` của NSOTRUNGDUC.
- [ ] **Kệ** gán tay, **Rác**, **dồn xu** (hạ ngưỡng xu để thử), **báo cáo ngày**, công tắc từng tính năng.
- [ ] Chạy ≥2 giờ (M13): cột **Mất KN** có tăng không.
- [ ] **Xả nhanh** bằng acc thật: cứ mời **Leader** nạp liên tục nhiều lượt → log có `[CHUYỂN]`; Leader đầy thì khung
      mở rồi tắt ngay + tin "moi lai sau ~3 giay" → mời lại (không bị khoá 30 giây); thử `xa` (gọi sẵn clone) / `xa xong`.
- [ ] **Log**: khung log mặc định chỉ dòng dễ đọc; tick **Chi tiết** thấy lại dòng kỹ thuật; tab Nhật ký mục "Dễ đọc".
- [ ] **Gom đồ**: để kho rảnh vài phút, xem log `[GOM]` (tắt được ở Cài đặt → Kho).
- [ ] Bấm **▶ Chạy** khi khu chính = −1 → có hộp thoại nhắc.
