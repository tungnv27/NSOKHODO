# Test tay trên server chính (P0a) — ✅ ĐÃ LÀM 2026-09-16

> **Vì sao test tay:** các câu hỏi dưới đây trả lời được bằng **2 client game thường**, không cần bot.
> Biết kết quả trước thì không viết code sai hướng.
>
> **Người làm:** user, ngày 2026-09-16. Cột "Kết quả" ghi theo lời user (nguyên văn trong ngoặc kép),
> kèm quyết định thiết kế tương ứng trong `SPEC.md`.
> Kết quả đã chép sang `GIAO_DICH.md` §9.
>
> **Chuẩn bị (khi test lại):** 2 acc — **A** (bên giao) và **B** (bên nhận) — cùng máy chủ, cùng Làng Tone
> (map 22), cùng khu. **Chỉ dùng đồ rác.** T10, T11 nên dùng acc phụ.

## Ưu tiên cao nhất

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T0** (M17) | A giao cho B **1 món không khoá** → B mở thông tin món. | Món có bị chuyển thành "Đã khóa" không? | ✅ **Không.** *"Chỉ khóa khi sử dụng."* → kho khả thi; **bot không bao giờ dùng hay mặc đồ** (D38). |
| **T3** (M4) | B chỉ để trống **đúng 1 ô**. A đặt **2 món rác** → khoá → Đồng ý. | Server báo gì? Món đi đâu? | ✅ *"Ngay khi A khoá vượt ô trống, server sẽ đóng giao dịch ngay. Có popup (lúc có lúc không): đối phương không đủ hành trang."* → **không mất đồ** (D39). Câu chữ chính xác: chưa có (M18). |
| **T2** (M3) | A đặt **1 món có hạn sử dụng**. | Có cho đặt / giao không? | ✅ *"Có gd được đồ có hạn."* |
| **T1** (M11) | Acc **cấp thấp** mời và nhận lời mời. | Có giao dịch được không? | ✅ *"Lv1 là giao dịch được."* → clone không cần luyện cấp (D47). |

## Luật mời và giao dịch

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T4** (M6) | A mời B nhiều lần liên tiếp; B đồng ý muộn. | Mời lại có bị chặn không? Lời mời còn hiệu lực bao lâu? | ✅ *"Mời cố định 31s. Nhưng nếu đồng ý thì có thể gd lại luôn, không mất thời gian chờ."* → mời lại cách 31 giây; xong phiên thì mời lượt sau ngay (D40). |
| **T5** | A mời B trong lúc B **đang giao dịch với người khác**. | Server báo gì cho A? | ✅ *"Đối phương đang có giao dịch khác."* → Chủ kho chen ngang bằng chat `nap`, không bằng lời mời (D41). |
| **T6** | Hai bên đã khoá, A rời khu hoặc thoát game. | Phiên có tự huỷ, đồ có còn nguyên không? | ✅ *"Có."* |
| **T7** | B khoá khung **rỗng** rồi Đồng ý; A đặt đồ + khoá + Đồng ý. | Giao dịch một chiều có thành công không? | ✅ *"Có."* |

## Làng Tone — NPC, khu, rương

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T8** (M7) | Tìm NPC **mở rương** ở Làng Tone. | Tên NPC? Bao nhiêu ô? Đi xa NPC còn cất được không? | ◐ *"Tìm theo chức năng thủ khố trên các phiên bản. Đi xa không cất được, phải sát NPC."* → MINH tra MODGAME: mục "Thủ khố" = **NPC 5**, đứng ≤ 22 px, mở bằng `-30 {-103, 4}` (D42). **Số ô rương: chưa có** → bot đọc ở P0b (M7b). |
| **T9** (M15, M12) | Tìm NPC **đổi khu**. | Có không? Số người tối đa mỗi khu? | ◐ *"Có NPC id 13."* **Số người tối đa mỗi khu: chưa có** → M12. |
| **T12** | Tìm NPC **bán hàng**, bán 1 món rác. | Có NPC không? Bán được gì? | ✅ *"Không bán được gì cả. Cần thì gd vào clone, tôi tự dọn con clone đó."* → kệ Rác dọn tay (D43). |
| **T13** | Tìm món **mở rộng túi**. | Tên, điều kiện dùng. | ✅ *"Không tự mở rộng."* → **bỏ** tính năng tự dùng túi vải (D44). |
| **T14** (M16) | Xu tối đa một nhân vật. | Trần xu? | ✅ *"Tối đa giữ được 2 tỷ xu."* → kiểm trần và dồn xu (D45). |

## Chat

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T10** (M10) | Nhắn riêng cho acc **chưa kết bạn**; tin dài; lặp nội dung. | Cần kết bạn không? Giới hạn độ dài? Bị khoá khi lặp không? | ◐ *"Có nhận được. Nhưng phải gửi tiếng Việt không dấu."* → bỏ dấu mọi tin gửi đi (D46). **Độ dài tối đa: chưa có** → M10b. |
| **T11** (M14) | Chat khu **5 giây/lần** trong 3 phút, có số ở đầu. | Có bị chặn không? | ✅ *"Nếu thêm @[000-999] thì không bị."* Mới thử ngắn; chạy dài vẫn dùng rao thông minh mặc định (D31). |

## Còn lại cho bot đo (P0b — `SPEC.md` §13)

| Mã | Nội dung |
|---|---|
| M1 | Thứ tự và khuôn các gói tin |
| M2 | Server có gửi gói xoá ô cho bên giao không |
| M5 | Khoảng cách mời vượt ngưỡng của client (client chặn menu khi cách hơn 60 px) |
| M7b | Số ô rương; mở và cất rương bằng bot |
| M8 | Gói tách chồng |
| M9 | Gửi Đồng ý sau 1,5 giây (client ép đủ 5 giây) |
| M10b | Độ dài tối đa của tin chat riêng |
| M12 | 12 acc cùng một khu |
| M13 | Tự đánh ở làng |
| M18 | Nguyên văn các câu server báo (T3, T4, T5) |
