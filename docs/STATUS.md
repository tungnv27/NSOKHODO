# STATUS — NSOKHODO

> **File này GHI ĐÈ mỗi session.** Đọc đầu tiên để biết "đang ở đâu / làm gì tiếp".

**Cập nhật lần cuối:** 2026-09-17 ~10h40 — **vòng 12 xong (D80–D85): gom đồ xếp chồng, cửa xả, log dễ đọc, lệnh khu
riêng chờ hàng trên Leader, cảnh báo khu chính, thú cưỡi.** Vòng 11 đã commit `a530a56`. Vòng 12: build Release sạch,
`tools/kiemtra/chay.ps1` PASS, soát lỗi độc lập (9 điểm, đã sửa), test sống đạt — **commit + push theo lời user
("thấy ổn thì commit + push rồi sửa tiếp")**.

## Đang chờ user

1. Xác nhận / bác **D52–D85** (bảng cuối `docs/TEST_2CHANG.md`). Thêm vào giao diện, cần duyệt: khung *Đơn đang soạn*
   (D75), ô *Khu giao* (D79), ô *Gom đồ* ở Cài đặt → Kho (D81), ô *Chi tiết (kỹ thuật)* trên khung log (D83).
2. Chạy app bản mới: mục **"Còn lại cho bạn"** trong `docs/TEST_2CHANG.md` (thêm: cửa xả bằng acc thật, log, gom).
3. Cho biết **lienminhfc** có phải user không (đưa 5 món 456 cho `tungkhodo9` lúc 16/09 23:50).
4. Ảnh "Sao k check ruong nhi?" (02:1x) có phải chụp trên **máy khác / VPS** không — nếu có: máy đó cần cài đặt kho
   đầy đủ (Chủ kho, kệ, gói…) hoặc chép `Data\Kho\Mặc định.txt` sang; không chạy hai máy cùng bộ acc.
5. **Lỗi "bay" ở NSOBAOTATL / NSOLITEPRO** (D77): chưa mang sang. Sửa NSOBAOTATL bị hệ thống chặn (repo khác, cần user
   cho phép rõ); NSOLITEPRO đang có ~1.200 dòng chưa commit của phiên khác (có chính `KeepAliveController.cs`) → chờ user
   quyết. Bản sửa: `Nudge` của NSOKHODO (bỏ nhánh `DangGiaoDich` vì hai tool kia không giao dịch).
6. Cân nhắc **cài Khu phụ** và **đổi khu chính khỏi khu 0** (M12, M20).

## Vòng 12 — đã làm (chi tiết `docs/WORKLOG.md` 17/09 9h30)

| Việc | Quyết định | Kiểm chứng |
|---|---|---|
| Cả kho đứng im khi khu chính −1 | D85 hộp thoại khi Chạy + ô đỏ | build + đọc code (hộp thoại chưa chụp) |
| Thú cưỡi trong rương có thể đọc lệch | D80 (byte đúng, khoá món bỏ cấp thú cưỡi) | đối chiếu client 251; KiemKho |
| Gom đồ xếp chồng về một nick | D81 | sống: 10 loại / 1 giao dịch; KiemKho |
| Xả nhiều đồ, Leader "không dọn" | D82 (cửa xả + 2 lỗi) | sống: tự mở, xoay vòng, `xa` / `xa xong`, Leader dọn khi Chủ kho đứng cạnh; KiemKho |
| Log khó đọc | D83 | sống (`[SK]`); ảnh khung log hai chế độ |
| Lệnh khu riêng, hàng trên Leader → "kho không có" | D84 | sống 20/20 trong 29 s; KiemKho |
| Gỡ kẹt (D78) với bản cuối | — | sống: tungkhodo8 kẹt thật → 11 s |

## Test sống — cách chạy lại

`powershell -ExecutionPolicy Bypass -File tools\kiemtra\song.ps1 -ChuanBi`, rồi `-Kho -Bo "tungkhodo9,tungnv2"` và
`-Nguoi -Acc tungkhodo9` (acc thứ hai: `-Nguoi -Acc tungkhodo8 -Lenh lenh-p2.txt` sau khi `nha tungkhodo8`).
**Luôn bỏ `tungnv2`** (Leader thật của user). Harness: `cfg ChuKho tungkhodo9` để acc người chơi ra lệnh chat
(cài đặt harness hiện đã lưu ChuKho = tungkhodo9). Lệnh harness: `CLAUDE.md` mục "Test sống".

Lúc dừng (10:31): mọi tiến trình test đã tắt; `tungkhodo9` rỗng túi; `tungkhodo8` đã nhận lại vào kho; Leader harness
(`tungkhodo`) còn 1 viên đá trong túi + 24 món trong rương (vẫn là đồ kho).

## Giới hạn đã biết (đã ghi trong `TEST_2CHANG.md`)

- Chưa đối chiếu túi trước/sau mỗi lần nạp (N9 "LECH").
- `lay … cho <tên có dấu cách>` không đọc được → dùng tool.
- `hex.log` chỉ chiều server → bot; `chat.log` chưa ghi tin khu của người khác.
- Cột "Hạn" chỉ tham khảo (D72); thú cưỡi không phân biệt cấp (D80). Đá không xếp chồng — mỗi viên một ô.
- Gom (D81) chỉ đồ xếp chồng; chờ khi có lệnh rút / cửa xả.
- Cửa xả: lượt đầu có thể chậm ~30 s (server khoá lời mời 31 s sau lần mời Leader bị từ chối).
- Tab Nhật ký đọc file trên luồng giao diện (chậm khi file log lớn) — chưa sửa.
- Phiên bị server huỷ sau khi hai bên đồng ý (M24) không có câu chữ → vẫn xếp là "đối phương huỷ".

## Việc sau (chưa làm, chưa ưu tiên)

- Dự phòng trả vai xong thì dọn đồ về "nhà".
- P5 auto mua bán (SPEC §13). Đề xuất `GiuLogNgay` sau khi đo dung lượng log một ngày.
