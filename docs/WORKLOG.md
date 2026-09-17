# WORKLOG — NSOKHODO (append-only)

## 2026-09-16 — spec, test tay, code một lượt P1–P4

**Bối cảnh.** User muốn một kho đồ chung: ~10 clone giữ đồ ở Làng Tone, một Leader làm đầu mối,
tool biết tổng kho và tự điều clone giao đồ. Server chính TeaMobi. Repo thứ ba của họ NSO.

**Spec.** 8 vòng với user (SPEC §1), chốt v1.2: D1–D51. Test tay T0–T14 xong (`TEST_TAY.md`).
Phát hiện quyết định nhất: **T0 — giao dịch không khoá đồ, chỉ dùng mới khoá** ⇒ kho khả thi, và bot
**tuyệt đối không được dùng món nào** (D38).

**Cách làm (D50).** Code trọn P1–P4 một lượt + bộ kiểm tra offline, rồi một buổi test 2 chặng.

**Đã làm.**
- Tách khung từ NSOBAOTATL `7715bcf`, gỡ phần báo TA/TL. Chi tiết: `NGUON_GOC.md`.
- Viết tầng giao dịch (`TradeService` / `TradeHandler` / `TradeState`), 12 file `Kho/`, `KhoMode`,
  giao diện bố cục A (6 file `MainForm*.cs`).
- Bộ kiểm tra `tools/kiemtra/KiemKho.cs`: ~250 ca, chạy offline bằng client giả + gói server giả lập.
- Kết quả: build Release 0 lỗi; `chay.ps1` **PASS cả 4 bộ**. **Chưa chạy trên server, chưa commit code.**

**Sai sót bắt được trước khi test — ghi lại để không lặp:**
1. **cmd 22 không phải bước tách chồng** mà là **tách trang bị** (phá món đã nâng cấp). Spec v1.2
   (R3/M8) và `GIAO_DICH.md` §6 đều ghi sai "22 rồi −85". Đã gỡ khỏi `ItemService`; tách chồng chỉ
   dùng `−28/−85` (D53).
2. **Lõi có đường tự dùng "Vô hạn khả di lệnh" để đổi khu** (`Navigator.FindKdlSlot`). Trong kho, món
   đó có thể là đồ gửi → dùng là khoá. Đã tắt cứng (D57), có ca kiểm tra.
3. **Leader và clone cùng đi về phía nhau** lúc dọn kho: mỗi bên nhảy tới chỗ CŨ của bên kia ⇒ đổi
   chỗ mãi. Sửa trước khi chạy: clone đứng yên, chỉ Leader đi (D60).
4. **Chia tin dài cắt đôi lệnh gợi ý:** "…tiep" | "#1, bo: huy #1". `KiemKho` bắt được; `ChiaTin`
   giờ không cắt ngay trước từ bắt đầu bằng `#`.
5. `MessageRouter.HandleMessage` đã có biến `raw` ở khối con → móc mới đặt tên `raw` là lỗi
   CS0136. Đổi thành `mocTho`.
6. `KhoConfig.Luu()` được gọi từ cả luồng giao diện lẫn luồng điều phối (lệnh chat `theo`, nhả clone):
   hai bên cùng ghi một file `.tmp` thì `File.Replace` của bên sau hỏng. Thêm khoá tĩnh.
7. Spec §11 nói Navigator lấy mốc hồi chiêu "lúc gửi lệnh" — sai: nó lấy **sau khi chờ khu đổi**
   (≤2 giây). Kho không dựa vào mốc đó mà dùng `VaoKhuLucUtc` (lúc nhận MAP_INFO).

**Bài học về bộ kiểm tra:**
- `csc.exe` của .NET Framework chỉ hiểu **C# 5**. Nó cũng không cho **tham số của anonymous method**
  trùng tên với biến khai báo sau đó ở khối ngoài (CS0136).
- Phải truyền `-codepage:65001`, nếu không csc có thể đọc sai chuỗi tiếng Việt trong file kiểm tra.
- Muốn gọi thẳng kiểu của `NSOKHODO.exe` (không qua reflection) thì biên dịch với `-r:` và nạp exe
  qua `AssemblyResolve`. Code dùng kiểu đó phải nằm trong hàm `NoInlining`, tách khỏi `Main`.

**Việc tiếp.** User chạy `TEST_2CHANG.md`. Chặng 1 hỏng → nhận `Logs/<danh sách>/<ngày>/` +
`Data/Kho/` để sửa lõi. Đạt → commit code, cập nhật `GIAO_DICH.md` §9 với M1/M2/M7b/M8/M9/M18.

## 2026-09-16 (tối) — test sống trên server chính, sửa theo số đo

**Bối cảnh.** User test nhận đồ (3 lượt nạp từ `tungdedo1`, đạt), rồi giao MINH tự test và hoàn thiện.
Sau lượt nạp, **kho đứng im**: không dọn kho, không rao, lệnh #1 không giao.

**Nguyên nhân.** `KhuPhu=-1` (user chưa cài khu phụ) → `KhoMode.Ranh` coi là "chưa cài khu" cho **mọi**
acc, kể cả Leader, và không ghi log. Sửa: D64 (chỉ khu chính bắt buộc, cảnh báo trong log).

**Cách test sống.** `tools/kiemtra/song.ps1`: `KhoSong` (kho không giao diện, y hệt `MainForm`) +
`NguoiChoi` (acc của user đóng vai người ngoài: nạp, nhận, chat, tách chồng, vào lại). Chạy trong
`tools/kiemtra/song/` (bản sao acc, bỏ qua git). `tungkhodo9` bỏ khỏi kho, `tungkhodo8` nhả ra làm
người chơi thứ hai. Mọi món chỉ đi giữa các acc của user.

**Đã chạy đạt trên server:**
- Leader về khu chính, cất 25 ô vào rương; dọn sang `tungkhodo1` (12 chồng) và `tungkhodo2` (món lẻ);
  clone tự cất rương. Tổng khớp từng món.
- Rút: `tungkhodo1` gộp lệnh #1 + #3 (10 Nham Thạch), xin rương → lấy → tách 10/106 → đổi khu →
  giao; `tungkhodo2` giao lệnh #2 (3 Đá cấp 7). ~5 giây/lệnh.
- Lệnh chat Chủ kho: `kho`, `tim`, `co`, `goi`, `huy`, sai cú pháp — trả lời < 0,3 giây, không dấu, có tem.
- Rao 5 giây/lần khi khu có người ngoài (đúng D31).

**Số đo mới** (chi tiết `GIAO_DICH.md` §9): M1, M2, M5, M7b (rương 30 ô), M8 (tách chồng chạy),
M9, M12, M13 (tự đánh không vũ khí → "Vũ khí không thích hợp"), M18 (nguyên văn), và ba mục mới
**M19** (server chặn lời mời tới người vào game < ~60 s), **M20** (server tự xếp khu lúc đăng nhập),
**M21** (toạ độ Y người khác thấy lệch −52).

**Lỗi bắt được khi chạy thật — ghi lại để không lặp:**
1. **Xin rương 1.537 lần trong 7 phút.** `DamBaoRuong` và `LayDuTuRuong` dùng chung biến chờ
   `_choBoxSeq`: tin rương về bị bước lấy món "ăn" mất, bước xin rương tưởng chưa có trả lời → xin
   lại mỗi tick. Chỉ lộ khi clone **vừa đăng nhập** (rương chưa có trong bộ nhớ) mà sổ kho vẫn biết
   hàng nằm trong rương. Sửa D69 + ca `KiemKho` "KhoMode: xin ruong…" (bản cũ trượt ca này).
   **Bài học:** mọi vòng gửi gói phải có chốt chặn số lần / nhịp, không chỉ dựa vào logic chờ.
2. **Lời mời đầu tiên sau khi mở kho luôn hỏng** ("tungkhodo1 do not accept."): M19 → D65, D66.
3. **Mở lại kho sau 5 phút → tạm dừng mọi lệnh** trước khi acc kịp đăng nhập → D68.
4. **3 lượt dọn cho 3 viên đá lẻ** (lượt chỉ gom một phía túi/rương) → D70.
5. Câu thiếu ô thật (ảnh user) không chứa chữ "hành trang" → nhận thêm "không đủ ô trống".
6. Chạy thử: Windows PowerShell 5.1 chuyển hướng `*>` ra **UTF-16** → grep không đọc được; chạy qua
   `cmd /c`. Harness nạp chung một EXE thì build lại bị khoá → mỗi vai một thư mục `bin\<vai>`.

**Phần sau cùng buổi tối (16/09):**
- User nhắn *"Làm thông minh 1 chút nhé. Mấy đồ gộp được thì bỏ vào cùng nick."* → **D71**: mỗi loại xếp chồng
  có "nhà" (clone giữ nhiều nhất), lượt dọn ưu tiên nhà, nhà bận thì chờ; đồ không chồng / cùng nhóm ưu tiên nick
  đang giữ cùng loại → "kệ tự nhiên". Đo được: server **tự gộp chồng** cả khi nhận qua giao dịch (gói `8` rồi
  các gói `9` cộng dồn) lẫn khi cất rương (`17` vào ô đang có chồng). Sau 4 vòng nạp / rút / dọn: mỗi loại một nick.
- **Thiếu ô bị xếp nhầm "đối phương huỷ"** khi người nhận đã khoá rỗng trước lúc server đóng phiên → thừa một
  lần mời. Giờ mọi bước của vai giao đều đọc câu server trước khi xếp loại lỗi (ca `KiemKho` 7a).
- User nạp thử **khung rỗng** (0 món, 0 xu) → Leader vẫn đồng ý, phiên "XONG" không có gì. Giờ Leader từ chối
  (`KiemHangNap`); **bên giao không được dùng hàm này** (người nhận luôn khoá rỗng — suýt làm hỏng mọi lượt giao).
- Leader dự phòng: tắt Leader → 61 giây sau dự phòng nhận vai, về đúng chỗ đứng, nhận nạp, tự cất và dọn về nhà;
  bật lại Leader → 16 giây sau trả vai. Nhả / nhận lại clone: đạt (server bắt chờ 20 giây mới cho vào lại).
- Số đo thêm: tách chồng của **người chơi** cũng qua `−85`; thoát rồi vào lại ngay → *"Bạn chỉ có thể vào lại
  game sau N giây nữa"* (N 1–22), lõi tự chờ đúng N.
- Chạy thử: `TaskStop` **không** giết được script Git-bash nền (vòng `sleep` sống tiếp, gửi lệnh vào kho / người
  chơi lúc không ngờ) → dừng bằng `Stop-Process` theo command line.

## 2026-09-16 khuya → 17/09 0h30 — lỗi user gặp khi dùng app thật (vòng 11)

**Bối cảnh.** User dùng app thật (Leader `tungnv2`, khu chính 0) và báo lỗi. Một: rút 30 Tử tinh thạch trung cấp thì
lỗi *"tui day, khong lay duoc do tu ruong"*, lệnh tạm dừng *"chi con 9/30"* dù bảng ghi còn 52. Hai: chỉ giao
được một ID mỗi lần. Ba: giao diện giật. Bốn: muốn giới hạn login như NSOLITEPRO. Năm: nhân vật "bay"
(y=216 mà người khác thấy 164). User tắt tool, MINH test sống bằng `song.ps1` (bỏ `tungnv2` khỏi kho).

**Nguyên nhân và sửa.**
1. **Rút 30 món hỏng:** clone chỉ đi giao khi đã lấy **đủ** món từ rương ra túi. Acc giữ 12 món trong túi (túi đầy)
   + 18 trong rương → hỏng ngay, bị tránh 10 phút; acc thứ hai giống hệt → còn 9 → tạm dừng. Sửa **D73** (giao
   theo lượt, cất tạm để lấy chỗ, `TUI_DAY` thay vì "thiếu hàng"). Câu "chi con" ghi thêm hàng nằm trên acc tạm
   không dùng. Test sống: 30/30 trong 26 s, 3 lượt 12/12/6 từ một acc.
2. **Cờ "có hạn" nhảy qua lại:** ảnh 23:22 có 456 "có hạn 14 / không hạn 47", sổ kho 23:26 ghi cả 61 có hạn. Đo
   được (M22): cùng một món, gói giao dịch / gói 8 ghi "không hạn", danh sách túi sau khi đăng nhập ghi "có hạn".
   Sửa **D72**: hạn không thuộc khoá món.
3. **Sổ kho giữ rương cũ** sau khi tắt app → **D74** (đọc lại rương một lần mỗi lần vào game).
4. **Đơn nhiều món** → **D75** (khung "Đơn đang soạn" + `KhoDieuPhoi.RutNhieuTuTool`). Test sống: đơn 4 dòng,
   tungkhodo7 giao 2 dòng qua 3 lượt (có cất tạm 4 món để lấy chỗ), tungkhodo2 gặp người nhận **thiếu ô** →
   tạm dừng đúng (Đá cấp 7 không xếp chồng: 17 ô — lỗi thiết kế ca test của MINH).
5. **Giao diện giật** (agent đọc code): mỗi giây bộ điều phối phát một bảng tồn **mới** → tab Tổng kho lọc + vẽ lại cả
   lưới, còn đổi dòng chọn thì ghi đè ô ID người đang gõ; không lưới nào bật vẽ đệm đôi; khung log mỗi giây tách
   toàn bộ chữ và gán lại; `DisconnectStats` ghi file (mutex chung, chờ tới 3 s) mỗi giây trên luồng giao diện.
   Sửa: chỉ phát bảng khi nội dung đổi (dấu vân tay), vẽ lại đúng dòng đang nhìn, giữ dòng chọn + chỗ cuộn,
   `DoubleBuffered`, nhớ đệm cột "Phân bố", khung log nối thêm + dựng lại một lần khi quá 600 dòng (tắt vẽ lúc
   dựng, nhịp riêng 300 ms, thu gọn thì không vẽ), thống kê mất kết nối ghi ≤ 15 s/lần. Hàng chờ: vẽ lại dòng
   đang nhìn; `CacClone` chép ra mảng trước khi đọc (luồng điều phối có thể đang thêm).
6. **Giới hạn login** → **D76**: `Fleet/LoginGate` đã có nguyên trong khung (giống NSOLITEPRO từng byte), chỉ thiếu
   giao diện. Test sống bật 4: 9 acc xếp hàng, mỗi giây một acc vào.
7. **Nhân vật bay** → đo bằng hai acc (lệnh mới `theo` / `nhay` / `nhayx` của `NguoiChoi`), **M23**; sửa **D77**
   (3 gói về chỗ cũ sau nhịp chống AFK). Leader bản sửa: 3/3 nhịp đứng đúng 216.

**Kiểm chứng.** Build Release 0 lỗi. `chay.ps1` PASS (KiemKho thêm ca túi đầy / cất tạm / TUI_DAY / tách khi túi
đầy / lấy thêm trước khi giao, D72). Ảnh giao diện dựng bằng harness `ChupUi` (thư mục tạm, danh sách trống):
khung Điều phối + đơn 2 dòng, mục Đăng nhập — hiển thị đúng. **Chưa** chạy GUI thật với acc.

**Sự cố trong lúc test.** Harness người chơi bật tự nhận → một người chơi thật (**lienminhfc**) mời `tungkhodo9` và
đưa **5 món 456** (16/09 23:50:45). MINH đã nạp cả 5 vào kho cùng hàng test (kho giờ có 66 món 456). Chưa rõ đó có phải
user không.

**Bài học.**
- Harness người chơi cũng chạy nhịp chống AFK → đo toạ độ phải trừ các lần nhịp đó chen vào.
- `[IO.File]::ReadAllText('tên tương đối')` trong PowerShell đọc theo thư mục hiện hành **của .NET** (khác
  `Set-Location`) → lệnh hỏng, còn `WriteAllText` tạo nhầm file rỗng ở gốc repo (đã xoá). Dùng đường dẫn tuyệt đối
  hoặc tool Edit.
- UIA không thấy ô số (`NumericUpDown`) của WinForms → kiểm giao diện bằng harness nạp `MainForm` rồi `DrawToBitmap`.

## 2026-09-17 9h30 → 10h35 — commit vòng 11, gom đồ, cửa xả, log dễ đọc (D80–D85)

**Bối cảnh.** User: sửa các lỗi mức cao / vừa còn lại; gom đồ xếp chồng về một nick (nick trung gian khi nick giữ hàng
chật — server đòi đủ một ô cho từng chồng); xả nhiều đồ không phải đợi Leader ("nhiều lúc còn không dọn"); log dễ đọc cho
người dùng thường (user dặn không cần sửa log kỹ thuật). Cho phép commit + push khi thấy ổn. Trước đó 02:1x user gặp cả kho
"chưa đọc rương" vì khu chính −1 (ảnh có thanh cuộn kiểu Windows cũ + thêm `1khodo1–5` → nhiều khả năng máy khác).

**Làm.**
- Commit + push vòng 11 (`a530a56`) sau khi build + `chay.ps1` PASS.
- **D85** cảnh báo khu chính −1. **D80** thú cưỡi: đối chiếu `251/Controller.cs` — gói 31 / 8 / 45 không có byte cấp thú
  cưỡi, sub 115 có → byte đọc đúng, nhưng khoá món lệch giữa túi và rương → bỏ cấp khỏi khoá cho thú cưỡi.
- **#9 (lỗi "bay" ở NSOBAOTATL / NSOLITEPRO): KHÔNG làm.** Sửa file NSOBAOTATL bị hệ thống chặn (repo khác); NSOLITEPRO
  đang có ~1.200 dòng chưa commit của phiên khác, sửa chen vào dễ lẫn → báo user.
- **D81 gom**, **D82 cửa xả + hai lỗi Leader không dọn** (lời mời bị từ chối vẫn đặt mốc "sắp giao dịch"; Chủ kho trong khu
  chặn dọn cả khi Leader đầy), **D83 log dễ đọc**, **D84** lệnh khu riêng chờ hàng trên Leader (kiểm kỹ #2: bản cũ huỷ
  "kho không có hàng").
- Ca kiểm offline mới: `KiemGomVaXa` + mở rộng `KiemGoKetVaKhu`, `KiemSoKho`, `KiemHangCho`.

**Soát lỗi độc lập (agent) — 9 điểm, đã sửa 8, điểm 7 sửa một phần:**
1. (cao) Clone ở cửa xả không bao giờ nhường lệnh rút; cửa xả kéo clone đang giữ hàng → lệnh quá hạn bị huỷ. → nhường; bỏ clone giữ hàng.
2. (cao) Lệnh khu riêng chờ Leader bị đóng "XONG 5/10" khi clone giao xong phần đã lập. → chưa xong khi còn chờ.
3. (vừa) Clone cửa xả không vào được khu chính thì kẹt mãi; tin "mời GD" gửi trước khi clone tới. → bỏ sau 90 s; báo "đang tới" rồi "đã tới".
4. (vừa) Đợt gom đếm cả nick không xét được → ghim 60 phút, lặp lại. → chỉ đếm nick xét được; nghỉ 2 phút sau mỗi đợt.
5. (thấp) Ước lượng lượt gom bỏ qua sức chứa túi nick giao → tính thêm; điểm = giao dịch + số lần đích cất rương.
6. (thấp) Ngưỡng vào / ra cửa xả 6 ô gây xoay vòng → 12 ô (trọn một lượt).
7. (thấp) Phân loại `NHAN_GOM` sai thứ tự báo cáo → theo việc nhận; cửa xả chỉ gia hạn khi đúng Chủ kho.
8. (thấp) Khung log dễ đọc không vẽ lại sau khi mở lại cửa sổ → vẽ lại đầu mỗi nhịp.
9. (thấp) Thú cưỡi `+cấp` bị bỏ qua không báo → báo Chủ kho + log.

**Test sống** (bảng trong `TEST_2CHANG.md`): gom 10 loại / 1 giao dịch; cửa xả tự mở, xoay vòng, `xa` / `xa xong`; Leader dọn
khi Chủ kho đứng cạnh; lệnh khu 7 chia clone + Leader 20/20 trong 29 s; gỡ kẹt bản cuối (dựng tungkhodo8 kẹt bằng harness
người chơi thứ hai) 11 s. Ảnh giao diện: khung log hai chế độ, ô Gom đồ. Nạp trả hết đồ test.

**Bài học.**
- Heredoc Bash với chuỗi C# có ngoặc lồng / gạch chéo ngược vỡ → ghi script Python ra file (Write) rồi chạy.
- `grep` đếm phiên theo tên nick khớp cả phiên cũ → đếm theo số dòng trước / sau.
- Điều kiện kiểm "trạng thái = chờ" sai khi lệnh được giao ngay trong cùng nhịp → kiểm "đang mở, không tạm dừng".

## 2026-09-17 0h35 → 1h30 — clone kẹt cứng, khu giao riêng (D78, D79), soát lỗi độc lập

**Bối cảnh.** User rút 17 Tử tinh thạch cao cấp → tạm dừng *"chi con 0/17 (17 nam tren acc tam khong dung)"*. Sổ kho
của user: tungkhodo6 **túi 30/30 (toàn đá) + rương 30/30** (17 món 457 trong rương). Không còn ô nào để lấy món ra, cũng
không cất bớt được → `TUI_DAY` → tránh acc 10 phút → tạm dừng. User cũng đề xuất *"chọn được cả khu giao"* và dặn
*"Kiểm tra thật kỹ nhé. Đừng để lỗi gì."*

**Sửa.**
- **D78:** chừa 1 ô túi mỗi clone khi dọn kho (kẹt cứng không còn tự sinh ra); clone kẹt → lượt **trả bớt** (clone → Leader,
  ngược chiều dọn kho, `MucDich = tra`); lệnh rút chờ gỡ kẹt thay vì tạm dừng; không gỡ được → tạm dừng có lý do.
- **D79:** khu giao cho từng lệnh (UI, chat, hàng chờ lưu cột mới — file cũ vẫn đọc được).
- Tên lệnh chat quá dài bị cắt đôi tin → rút gọn câu hướng dẫn (≤ 90 ký tự).
- `chay.ps1 -Exe <đường dẫn>`: kiểm bản build ở chỗ khác khi app user đang chạy khoá `bin\Release`.

**Soát lỗi độc lập (agent đọc code) — 10 điểm, đã sửa cả 10:**
1. Nhận diện "kẹt" của bộ điều phối lệch với luật đổi chỗ của mode (chồng lớn hơn số cần; gộp chồng khác cờ hạn) → giao
   lại ngay, 3 lần hỏng → tạm dừng. Sửa: mode vừa báo `TUI_DAY` = tin là kẹt 10 phút; chỉ đếm chồng ≤ số còn giao.
2. Chờ gỡ kẹt cả khi không gỡ được → lệnh bị **huỷ** sau 10 phút với lý do sai. Sửa: `CoTheTraBot` + tạm dừng có lý do.
3. Leader đang chờ clone trả bớt vẫn nhận lời mời người chơi → lời mời clone bị lỡ; `HuyPhienCua` huỷ nhầm phiên nạp của
   người chơi. Sửa: Leader từ chối trong lúc chờ; chỉ huỷ phiên có đối phương đúng là bot của lượt.
4. Lệnh khu "mù" chặn cả hàng chờ + spam chat. Sửa: hai lượt (lệnh thấy người nhận trước), không đặt lại cờ đã báo, giới hạn
   tần suất câu "se moi", lệnh mới không vượt mốc thử lại của lệnh cũ, chuyến tìm không huỷ việc nội bộ.
5. Lệnh khu riêng kéo Leader khỏi khu chính. Sửa: không lập kế hoạch lên Leader cho khu riêng.
6. Chặn nhường việc khi có lượt trả bớt quá rộng → chỉ acc thuộc lượt đó.
7. Xoá cờ "cần đọc rương" trước khi đọc xong → mất cờ nếu việc bị nhường. Sửa: chỉ xoá khi rương đã về.
8. Khu chính đầy tạm thời → tạm dừng ngay. Sửa: tính một lần hỏng.
9. `ThieuTrongTui` trừ túi hai lần khi hai dòng trùng khoá (chưa xảy ra) → sửa.
10. Nhịp chống AFK có thể kéo server về chỗ cũ khi mode vừa di chuyển → bỏ nhịp khi `MyChar` đổi (trừ các bước của chính nhịp).

**Test sống (tungnv2 không đụng tới).** Dựng lại đúng tình huống kẹt: rút 12 viên đá cho tungkhodo9, chạy tungkhodo6 như
người chơi nhận 12 viên → túi 30/30 + rương 30/30. Bật kho, rút 17 × 457: lần giao đầu `TUI_DAY` (rương chưa đọc lại)
→ không tạm dừng → 69 s sau (chờ Leader qua mốc 90 s) trả 12 viên (4 s) → giao đủ 17 (12 s). Khu 5: lần đầu **hỏng** vì
mời 1,5 s sau khi vào khu (M24) → thêm chờ 2,5 s → bản sau đạt ngay. Khu 7 / 9 (người nhận tới sau) đạt; chat
`lay 456 1 khu 7` đạt. Nạp trả hết, tổng từng loại khớp. Leader bản cuối: nhịp chống AFK vẫn về 216.
**Chưa chạy sống** với bản cuối: lượt trả bớt (logic đổi sau review, có ca offline), lệnh khu riêng mà hàng chỉ nằm trên Leader.

**Bài học.**
- Nhịp điều phối xử lý báo cáo việc **trước** khi chụp lại sổ kho → ca kiểm đổi túi ngay trước lúc báo phải chạy một nhịp.
- `XetLoiMoi` có tác dụng phụ (đánh dấu Leader "vừa có lời mời" 10 s) → ca kiểm gọi nó phải đặt lại mốc.
- Soát lỗi bằng một agent đọc code độc lập bắt được 10 điểm mà ca kiểm của chính người viết bỏ sót (ca kiểm viết theo đúng
  giả định của code).

## 2026-09-17 10h45 → 11h45 — xả nhanh qua Leader (D86), nhận rồi huỷ (D87)

**Bối cảnh.** Sau vòng 12 user hỏi *"Nếu người dùng chỉ gđ vào leader thì sao?"* — MINH đọc code: cửa xả giữ 3 clone
trống nhất, dọn kho của Leader không dùng được chúng. User: *"Clone nên tự lấy đồ từ leader thì nhanh hơn. Vì gd chỉ mất
2-3s… Thay vì bắt người chơi gd vào clone."* User đang dùng app lúc đầu (MINH chỉ làm offline, build ra thư mục tạm),
11:00 user tắt app, cho test.

**Đo trước khi chốt (M27–M29).** Ba acc harness: mời người đang giao dịch → không khoá, 3 s sau mời lại được. Leader
từ chối → người mời khoá 30 s (tin "moi lai sau ~5 giay" của bản cửa xả là sai); phiên mở rồi huỷ → mời lại ngay.
Log cũ: bot↔bot 12 món 2–3 s.

**Đã làm.** D86 thay D82a: clone không nhận người chơi; đợt xả gọi 2 clone đứng sát Leader, Leader chuyển tiếp từ túi
ngay sau mỗi lượt nạp (`Viec.ChiTui`: không ra Thủ khố, không tách chồng). D87: Leader đầy → nhận rồi huỷ
(`HuyKhiMo`, mã `HET_CHO`); phiên giao gặp câu "đang chờ hoàn thành…" → mời lại sau 3 s. Ca kiểm offline mới cho cả
hai (KiemKho: xả nhanh, ChiTui, HuyKhiMo, chỗ đứng theo ô đất, mời lại 3 s).

**Soát lỗi độc lập lần 1 — 7 điểm, đã sửa cả 7:** túi Leader toàn đồ Rác thì đợt xả chặn dọn kho mãi (nặng); đợt xả chặn
lệnh D84 chờ hàng trong rương Leader và gỡ kẹt; `nap` huỷ lượt chuyển và bị đếm hỏng; chồng gộp → KHONG_CO_MON bị đếm
hỏng; hạn việc clone có thể nằm trong quá khứ; chỉ nhìn Chủ kho qua mắt Leader; danh sách clone giữ mục chết / đóng đợt
làm Leader chờ 20 s.

**Test sống — ba lần chạy.**
1. Bản đầu: nạp 3 lượt liền → lượt 3 bị từ chối, **khoá 31 s** (tổng 38 s); chuyển sang clone ở 395 đạt, clone ở 335
   "quá xa" 4 lần.
2. Nhận rồi huỷ: 3 lần mời cách 3 s đều tới (không khoá). Vẫn "quá xa" với clone ở 335 dù Leader dùng toạ độ chính nó
   thấy → log có `cmd52: (335,216) -> (335,288)`: **x = 335 là mép tầng** (M29), clone rơi xuống tầng dưới.
3. Chỗ đứng xét cờ đất của ô bản đồ (`TileEngine.HasFlag(x, y, 2)`): 29 món / 3 lượt nạp liền trong **17,3 s**, không
   lần khoá nào, 3 lượt chuyển đều đạt, `xa xong` đạt. Dọn sạch.
4. Bản cuối (sau soát lỗi lần 2, build 11:52): **16,8 s**, 0 lần khoá, 3 lượt chuyển đạt, không "quá xa"; log dễ đọc chỉ
   một dòng cho lần nhận-rồi-huỷ. Dọn sạch (tungkhodo9 rỗng túi, túi Leader harness trống).

**Soát lỗi độc lập lần 2 — 5 điểm, đã sửa cả 5 (có ca kiểm):**
1. (nặng) Leader vào lại game giữa đợt không thấy clone đứng sẵn (danh sách chỉ nạp từ cmd 3) → đợt kẹt mãi. Sửa: clone
   sẵn sàng mà Leader không thấy quá 10 s → đổi clone khác (tránh 2 phút).
2. Mã `BI_HUY` của **phiên** (đối phương / server huỷ, M24) bị coi là trung tính → thử mãi cùng clone. Sửa: chỉ trung
   tính khi bộ điều phối huỷ việc.
3. `nap` (giữ cửa) + Leader hết chỗ → giữ cửa chặn cả chuyển tiếp lẫn dọn kho tới hết giờ. Sửa: bỏ giữ cửa.
4. Mỗi lần nhận-rồi-huỷ thêm một dòng log dễ đọc; tin "mời lại sau ~3 giây" lúc clone còn đang tới. Sửa: 1 dòng / phút;
   tin "Clone dang toi canh Leader, moi lai sau ~10 giay" khi chưa clone nào đứng xong.
5. Leader đang chờ clone trả bớt (D78) vẫn **từ chối** Chủ kho → khoá 30 s. Sửa: nhận rồi huỷ (người lạ vẫn từ chối).

**Bài học.**
- Hỏi "nếu người dùng làm khác thì sao" lộ ra điểm yếu mà ca kiểm theo kịch bản của người viết không thấy.
- Câu chữ báo người chơi ("mời lại sau ~5 giây") phải đo luật server trước — sai là người chơi bị khoá 30 s.
- "Quá xa" chưa chắc do khoảng cách ngang: chỗ đứng chọn theo toạ độ phải xét tầng đất của bản đồ.

## 2026-09-17 chiều — một ô giao dịch tối đa 29.999 (D88, M30)

**User nhắc:** *"vật phẩm gom tối đa gd chỉ được là 29999 thôi 30000 là game k cho gd. Nhưng vật phẩm xếp trồng lại gộp
được 32000 là tối đa"*. Client 251 không có luật này → server chặn. Bản cũ chọn **nguyên chồng** vừa đủ số cần giao, nên
chồng 32.000 (túi / rương tự gộp) sẽ bị đem đi giao nguyên → hỏng lặp lại.

**Sửa (D88):** hằng `TradeHandler.MAX_SO_LUONG = 29999`.
- `KhoMode`: `ChonO` bỏ ô > 29.999; chọn ô và tách chồng dùng chung một phép tính "chồng lớn trước" (`CanTach`) — nếu
  không, phần lẻ 2.001 ở ô đầu bị chọn thay chồng 29.999 vừa tách → thêm lượt và thêm lần tách.
- Điều phối: xả nhanh bỏ qua chồng > 29.999 (lượt chuyển không tách, dọn kho thường tách); dọn kho tính chồng lớn là
  2 ô; gỡ kẹt không trả chồng lớn; gom giữ ngưỡng tổng ≤ 29.999.

**Soát lỗi độc lập — 8 điểm, đã sửa cả 8 (có ca kiểm); điểm 9 là harness `NguoiChoi nap`, để nguyên (dùng để đo):**
1. (vừa) `ChoGoKet` coi chồng 32.000 là "giao ngay" → clone kẹt cứng báo TUI_DAY mãi, không bao giờ gỡ kẹt. Sửa: cùng
   luật `ChonO`.
2. `CoTheTraBot` chưa lọc chồng lớn như `TaoTraBot` → chờ gỡ kẹt vô ích.
3. (vừa) Món đầu tiên là chồng lớn (2 ô) mà nick nhà chỉ nhận 1 ô → lượt dọn rỗng, 30 s sau chọn lại đúng cặp đó → dọn
   kho kẹt. Sửa: lượt rỗng → nick nghỉ 2 phút.
4. Chồng lớn trong túi Leader cũng cần một ô trống để tách → túi Leader hết ô thì lượt không mang chồng đó.
5. Cần 40.000, túi chỉ có chồng 32.000, rương hết → bản sửa đầu báo thiếu ngay (bản cũ giao nguyên 32.000). Sửa: tách
   và giao phần đang có.
6. Gom: nick đọc rương sau lúc ghim đợt có thể mang chồng lớn → lọc.
7. Lệnh khu riêng (D84) chờ chồng lớn trong **túi** Leader lúc đang xả nhanh → chờ tới hết đợt. Sửa: coi như hàng "gấp"
   xả không chuyển được, dọn thẳng từ túi.
8. Tách xong chỉ nhìn mảnh mới: nếu gói cập nhật ô nguồn về sau thì chồng 32.000 bị tách thêm 2.001 từ ô chỉ còn 2.001
   → hỏng. Sửa: chờ ô nguồn giảm đúng số, tối đa 1,5 s (quá thì coi là xong như cũ — thứ tự gói chưa đo).

**Kiểm chứng:** build Release sạch; `tools/kiemtra/chay.ps1` PASS (482 ca, thêm 21). **Chưa test sống**: sổ kho (harness
và bản `bin/Release`) không có chồng nào quá 146 món — cần một chồng ≥ 30.000 để thử; server báo câu gì khi đặt ô 30.000
cũng chưa đo.

**Bài học.** Đổi luật chọn ô thì mọi nơi "đoán trước" mode sẽ chọn gì (gỡ kẹt, dọn kho, xả nhanh, gom) phải đổi theo —
agent soát lỗi tìm ra 7 chỗ như vậy mà ca kiểm theo kịch bản chính không chạm tới.
