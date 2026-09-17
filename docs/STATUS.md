# STATUS — NSOKHODO

> **File này GHI ĐÈ mỗi session.** Đọc đầu tiên để biết "đang ở đâu / làm gì tiếp".

**Cập nhật lần cuối:** 2026-09-17 ~2h — **vòng 11 xong (D72–D79): sửa các lỗi user gặp khi dùng app thật, thêm
đơn nhiều món, khu giao, gỡ kẹt clone. Soát lỗi độc lập 10 điểm đã sửa. Build Release sạch (01:14:57),
`tools/kiemtra/chay.ps1` PASS. CHƯA COMMIT CODE — chờ user xác nhận.** User tạm dừng (“sau quay lại”) — đã báo đủ
kết quả vòng 11 + danh sách còn lại + cú pháp lệnh chat.

## Thứ tự đã đề xuất cho user

1. **Bật "Ghi log"** (nút trên thanh công cụ) trong lúc test — đang tắt, xem mục dưới.
2. User test bản mới → 3. commit + push → 4. chuyển bản sửa "bay" (D77) sang NSOBAOTATL / NSOLITEPRO (user chưa
   đồng ý — hỏi trước).

## Đang chờ user

1. Xác nhận / bác **D52–D79** (bảng cuối `docs/TEST_2CHANG.md`). **D75** (khung *Đơn đang soạn*) và ô **Khu giao**
   (D79) là thêm vào bố cục A → cần user duyệt giao diện.
2. Chạy app bản mới (01:14:57) với acc thật: phần **"Còn lại cho bạn"** trong `docs/TEST_2CHANG.md`.
3. Cho biết **lienminhfc** có phải user không (đưa 5 món 456 cho `tungkhodo9` lúc 16/09 23:50, đã nạp vào kho).
4. Cân nhắc **cài Khu phụ** và **đổi khu chính khỏi khu 0** (M12, M20).
5. Đạt → commit một lần, push `origin master`.

## 17/09 ~02:12 — "Sao không check rương?"

Ảnh user: mọi acc "Rương trống: chưa đọc", "Trạng thái kho: Chưa cài khu chính" (khu chính = −1 → `KhoMode.Ranh`
đứng im, không đọc rương). User đã cài lại khu chính → tool chạy. File `bin\Release\Data\Kho\Mặc định.txt` máy này
luôn `KhuChinh=0` (lưu 01:50, 02:03, 02:13) → ảnh nhiều khả năng từ **máy khác** (thanh cuộn kiểu Windows cũ, có
thêm `1khodo1–5`) mới chép exe, chưa có file cài đặt kho → mặc định −1. **Chưa xác nhận với user.** App máy này
tắt lúc 02:13:30. Đề xuất (chờ duyệt): bấm ▶ Chạy khi khu chính −1 thì hiện cảnh báo.

## App thật của user lúc 17/09 02:00 (chỉ đọc, không sửa gì)

- Chạy bản 01:14:57 từ 01:43 (`bin\Release`, PID 28828). Hàng chờ trống (`SoKe=26`). Sổ kho cập nhật 01:59, dùng
  262/666 ô.
- **`LogFile=0`** trong `Data/Kho/Mặc định.txt` → không có `Logs/.../2026-09-17/`; log cuối 16/09 22:32. User báo lỗi
  lúc này thì không có log để lần.
- Cài đặt kho: Leader `tungnv2`, dự phòng `tungkhodo`, **Chủ kho `barbigkid`** (lệnh chat phải nhắn từ nick này),
  `BatLenhChat=1`, `CheDoNhan=TatCa`, khu chính 0, không khu phụ.
- Đồ dồn lệch: rương đầy 30/30 ở `tungkhodo1` (túi 7), `tungkhodo2` (túi **29/30** — chỉ còn ô chừa D78),
  `tungkhodo4` (túi 24), `tungkhodo6` (túi 4); `tungkhodo`, `tungkhodo9` trống hẳn; `tungkhodo5` rương 9,
  `tungkhodo7` rương 16, `tungkhodo3` / `tungkhodo8` rương 25; `tungnv2` rương 3. Kẹt thì D78 tự gỡ; gom lại cho
  đều là việc "Gom lại đồ…" ở mục Việc sau.

## Vòng 11 — đã làm (chi tiết `docs/WORKLOG.md` 17/09)

| Lỗi / yêu cầu | Sửa | Kiểm chứng |
|---|---|---|
| Rút 30 món: "tui day…", "chi con 9/30" | D73 giao theo lượt + cất tạm | sống 30/30; KiemKho |
| Cùng món nhảy dòng có hạn / không hạn | D72 | sống (M22); KiemKho |
| Sổ kho giữ rương cũ | D74 | sống |
| Chỉ giao 1 ID | D75 đơn nhiều món | sống; ảnh giao diện |
| Giao diện giật | vẽ lại theo thay đổi, đệm đôi, log nối thêm… | build + ảnh; **chưa đo trên app thật** |
| Giới hạn login | D76 | sống |
| Nhân vật "bay" | D77 | sống (M23), 4/4 nhịp |
| "Chỉ còn 0/17" (clone túi + rương đầy) | D78 gỡ kẹt + chừa ô | sống (dựng lại đúng tình huống); KiemKho |
| Chọn khu giao | D79 | sống khu 5 / 7 / 9 + chat; KiemKho |

**Chưa chạy sống với bản cuối:** lượt trả bớt (logic đổi sau soát lỗi — có ca offline), lệnh khu riêng mà hàng chỉ
nằm trên Leader (có ca offline).

## Test sống — cách chạy lại

`powershell -ExecutionPolicy Bypass -File tools\kiemtra\song.ps1 -ChuanBi`, rồi
`-Kho -Bo "tungkhodo9,tungnv2"` và `-Nguoi -Acc tungkhodo9`. **Luôn bỏ `tungnv2`** (Leader thật của user).
Lệnh harness: `CLAUDE.md` mục "Test sống". `song\Data\settings.txt` đang bật giới hạn login 4.
App user đang chạy khoá `bin\Release` → build ra thư mục khác + `chay.ps1 -Exe` (CLAUDE.md "Commands").

Lúc dừng (01:29): mọi tiến trình test đã tắt; `tungkhodo9` rỗng túi; tổng từng loại khớp; túi Leader (tungkhodo) trống.

## Giới hạn đã biết (đã ghi trong `TEST_2CHANG.md`)

- Chưa đối chiếu túi trước/sau mỗi lần nạp (N9 "LECH").
- `lay … cho <tên có dấu cách>` không đọc được → dùng tool.
- `hex.log` chỉ chiều server → bot; `chat.log` chưa ghi tin khu của người khác.
- Cột "Hạn" chỉ tham khảo (D72). Đá không xếp chồng — mỗi viên một ô bên người nhận.
- Đồ đã rải nhiều nick từ trước không tự gom (dọn kho có thể rải một loại ra vài nick khi nick "nhà" hết chỗ).
- Nhân vật không cầm vũ khí → "Vũ khí không thích hợp" mỗi 60 giây (vô hại).
- Tab Nhật ký đọc file trên luồng giao diện (chậm khi file log lớn) — chưa sửa.
- Byte cấp món: danh sách túi dùng `HasUpgrade` (gồm thú cưỡi), rương / gói 8 thì không → **thú cưỡi trong rương có
  thể đọc lệch**. Chưa kiểm.
- Lỗi "bay" (M23) nhiều khả năng cũng có ở NSOBAOTATL / NSOLITEPRO (cùng hàm) — chưa báo / sửa bên đó.
- Phiên bị server huỷ sau khi hai bên đồng ý (M24) không có câu chữ → vẫn xếp là "đối phương huỷ".

## Việc sau (chưa làm, chưa ưu tiên)

- Gom lại đồ xếp chồng đã rải (clone → clone).
- Dự phòng trả vai xong thì dọn đồ về "nhà".
- P5 auto mua bán (SPEC §13). Đề xuất `GiuLogNgay` sau khi đo dung lượng log một ngày.
