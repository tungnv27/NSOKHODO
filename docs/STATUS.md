# STATUS — NSOKHODO

> **File này GHI ĐÈ mỗi session.** Đọc đầu tiên để biết "đang ở đâu / làm gì tiếp".

**Cập nhật lần cuối:** 2026-09-17 ~16h20 — **vòng 14 xong (D88): một ô giao dịch tối đa 29.999, chồng lớn tách
trước.** Build Release sạch, `tools/kiemtra/chay.ps1` PASS (482 ca), soát lỗi độc lập (8 điểm, đã sửa cả 8). **Chưa test
sống** (kho không có chồng nào quá 146 món). Vòng 13 (D86–D87, xả nhanh) đã commit `24c7a8d`.

## Đang chờ user

1. Xác nhận / bác **D52–D88** (bảng cuối `docs/TEST_2CHANG.md`). D82a (người chơi giao thẳng vào clone) đã bỏ, thay bằng
   D86. Thêm vào giao diện, cần duyệt: khung *Đơn đang soạn* (D75), ô *Khu giao* (D79), ô *Gom đồ* ở Cài đặt → Kho (D81),
   ô *Chi tiết (kỹ thuật)* trên khung log (D83).
2. **Rút xu** (đề xuất D89, việc TroLyAI `f41331`): chốt 3 điểm — xác nhận lệnh từ 100 triệu, cho phép `cho <tên>`,
   lệnh xu tách riêng lệnh đồ. Nhận xu và dồn xu (D45) **chưa từng test sống** → test trước khi làm.
3. Chạy app bản mới: mục **"Còn lại cho bạn"** trong `docs/TEST_2CHANG.md` (xả nhanh bằng acc thật, log, gom).
4. Cho biết **lienminhfc** có phải user không (đưa 5 món 456 cho `tungkhodo9` lúc 16/09 23:50).
5. Ảnh "Sao k check ruong nhi?" (02:1x) có phải chụp trên **máy khác / VPS** không — nếu có: máy đó cần cài đặt kho
   đầy đủ hoặc chép `Data\Kho\Mặc định.txt` sang; không chạy hai máy cùng bộ acc.
6. **Lỗi "bay" ở NSOBAOTATL / NSOLITEPRO** (D77): chưa mang sang (NSOBAOTATL: hệ thống chặn sửa repo khác, cần user cho
   phép rõ; NSOLITEPRO: ~1.200 dòng chưa commit của phiên khác, có chính `KeepAliveController.cs`).
7. Cân nhắc **cài Khu phụ** và **đổi khu chính khỏi khu 0** (M12, M20).

## Vòng 14 — đã làm (chi tiết `docs/WORKLOG.md` 17/09 chiều)

| Việc | Quyết định | Kiểm chứng |
|---|---|---|
| User nhắc: ô giao dịch tối đa 29.999, chồng gộp tới 32.000 (M30) | D88 tách chồng lớn trước; xả nhanh / gỡ kẹt bỏ qua chồng lớn; dọn kho tính 2 ô | KiemKho (21 ca mới); soát lỗi độc lập 8 điểm; **chưa test sống** |

## Vòng 13 — đã làm (chi tiết `docs/WORKLOG.md` 17/09 10h45)

| Việc | Quyết định | Kiểm chứng |
|---|---|---|
| Người chơi chỉ giao cho Leader thì sao; clone nên tự lấy từ Leader | D86 xả nhanh (thay D82a) | sống: 29 món / 3 lượt liền 17,3 s; KiemKho |
| Leader đầy từ chối → người chơi khoá 30 s (đo M28) | D87 nhận rồi huỷ | sống: 0 lần khoá; KiemKho |
| Mời trúng người đang giao dịch → trước chờ 31 s | mời lại sau 3 s (M27) | sống (harness); KiemKho |
| Clone đứng mép tầng (x = 335 Làng Tone) → "quá xa" | chỗ đứng theo cờ đất ô bản đồ (M29) | sống: lần 3 không còn "quá xa"; KiemKho |

## Test sống — cách chạy lại

`powershell -ExecutionPolicy Bypass -File tools\kiemtra\song.ps1 -ChuanBi`, rồi `-Kho -Bo "tungkhodo9,tungnv2"` và
`-Nguoi -Acc tungkhodo9 -Lenh lenh-a.txt`. **Kiểm app của user có đang chạy không trước khi đăng nhập acc** (dùng chung
bộ acc). Harness: `cfg ChuKho tungkhodo9` (đã lưu), `cfg BatGomChong 0` khi test xả. Nạp liên tục: script
`nap_lien.py` trong scratchpad phiên 17/09 (gửi `nap tungkhodo het`, chờ `PHIEN KET THUC`, bị huỷ thì mời lại sau 3 s).

Lúc dừng (11:56): mọi tiến trình test đã tắt; `tungkhodo9` rỗng túi; túi Leader harness (`tungkhodo`) trống.

## Giới hạn đã biết (đã ghi trong `TEST_2CHANG.md`)

- Xả nhanh: lượt nạp đầu mở đợt, clone cần ~4 s để tới → nạp dồn dập thì lượt thứ 3 thường bị "nhận rồi huỷ" một lần.
  Đồ xả vào clone đứng cạnh không theo kệ (Gom gộp đồ xếp chồng sau); đồ Rác không chuyển tiếp.
- Chưa đối chiếu túi trước/sau mỗi lần nạp (N9 "LECH").
- Chồng ≥ 30.000 (D88) mới kiểm offline; server báo gì khi đặt ô 30.000 chưa đo; thứ tự gói cập nhật túi sau khi tách
  chưa đo (bot chờ ô nguồn giảm tối đa 1,5 s).
- `lay … cho <tên có dấu cách>` không đọc được → dùng tool.
- `hex.log` chỉ chiều server → bot; `chat.log` chưa ghi tin khu của người khác.
- Cột "Hạn" chỉ tham khảo (D72); thú cưỡi không phân biệt cấp (D80). Đá không xếp chồng — mỗi viên một ô.
- Gom (D81) chỉ đồ xếp chồng; chờ khi có lệnh rút / đợt xả.
- Watchdog báo "DONG BANG: server keo ve (420,216)" khi clone vào khu hai lần ở cùng điểm — báo nhầm, chỉ là log
  (không đăng nhập lại vì clone không tàn sát). Chưa sửa (ngoài phạm vi).
- Tab Nhật ký đọc file trên luồng giao diện (chậm khi file log lớn) — chưa sửa.

## Việc sau (chưa làm, chưa ưu tiên)

- Dự phòng trả vai xong thì dọn đồ về "nhà".
- P5 auto mua bán (SPEC §13). Đề xuất `GiuLogNgay` sau khi đo dung lượng log một ngày.
