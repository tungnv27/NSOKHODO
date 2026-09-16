# Test tay trên server chính — làm TRƯỚC khi code (P0a)

> **Vì sao test tay:** các câu hỏi dưới đây trả lời được bằng **2 client game thường**, không cần bot.
> Biết kết quả trước thì không viết code sai hướng. Riêng các ca có thể **mất đồ** (T3) thì test
> tay bằng đồ rác vẫn an toàn hơn để bot thử.
>
> **Chuẩn bị:** 2 acc — **A** (bên giao) và **B** (bên nhận) — cùng máy chủ, cùng Làng Tone (map 22),
> cùng khu. **Chỉ dùng đồ rác.** T10, T11 nên dùng acc phụ, vì có thể bị khoá chat thật.
>
> **Ghi kết quả:** điền vào cột "Kết quả", hoặc nhắn MINH. Server báo câu chữ gì thì **chép
> nguyên văn**, vì bot sẽ bắt đúng câu đó.
> Kết quả chốt được chép tiếp sang `GIAO_DICH.md` §9.

## Ưu tiên cao nhất

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T0** (M17) | A giao cho B **1 món không khoá** → B mở thông tin món. **Món có bị chuyển thành "Đã khóa" không?** Nếu không, B giao ngược lại cho A (khứ hồi). | ⚠ **Sống còn.** Đồ bị khoá sau giao dịch thì đã vào kho là **không rút ra được**, cả thiết kế phải làm lại. | |
| **T3** (M4) | B chỉ để trống **đúng 1 ô** túi. A đặt **2 món rác khác loại** → cả hai khoá → cả hai Đồng ý. | Server báo gì? Món đi đâu: A còn giữ cả hai / B nhận 1 món / có món biến mất? | |
| **T2** (M3) | A đặt **1 món có hạn sử dụng** vào khung. | Client có cho đặt không? Giao xong B có nhận được không, hạn còn giữ nguyên không? | |
| **T1** (M11) | Dùng **acc cấp thấp** (cấp 1–10) mời, và nhận lời mời. | Có giao dịch được không? Nếu không: cấp tối thiểu, câu báo lỗi. | |

## Luật mời và giao dịch

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T4** (M6) | A mời B, B **không bấm gì**. A mời lại sau 1 giây, 3 giây, 10 giây. Lần khác thì B bấm Đồng ý sau 30 giây, rồi sau 60 giây. | Mời lại có bị chặn không, câu báo gì? Lời mời còn hiệu lực bao lâu? | |
| **T5** | A mời B trong lúc B **đang giao dịch với người khác**. | Server báo gì cho A? | |
| **T6** | Hai bên đã khoá, **A rời khu** hoặc **thoát game**. | Phiên có tự huỷ không? Đồ có còn nguyên ở cả hai bên không? | |
| **T7** | B khoá khung **rỗng** rồi Đồng ý; A đặt đồ + khoá + Đồng ý. | Giao dịch một chiều (B không đưa gì) có thành công không? | |

## Làng Tone — NPC, khu, rương

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T8** (M7) | Tìm NPC **mở rương** ở Làng Tone. Mở rương. | Tên NPC? Rương có **bao nhiêu ô**? Mở rương xong đi ra xa NPC thì còn cất/lấy đồ được không? | |
| **T9** (M15, M12) | Tìm NPC **đổi khu** ở Làng Tone. Mở danh sách khu. | Có NPC đổi khu không, tên gì? Danh sách có hiện **số người mỗi khu** không? Khu tối đa bao nhiêu người? | |
| **T12** | Tìm NPC **bán hàng / Tạp hoá** ở Làng Tone. Bán 1 món rác. | Có NPC không? Bán được bao nhiêu yên? Món nào **không bán được**? (cho ý tưởng Thùng rác) | |
| **T13** | Tìm món **mở rộng túi** (túi vải…) và món mở rộng rương nếu có. | **Tên chính xác**, cấp cần để dùng, có phải dùng theo thứ tự không, mỗi món thêm bao nhiêu ô, tối đa bao nhiêu ô. (cho ý tưởng Túi vải) | |
| **T14** (M16) | Xem xu tối đa một nhân vật được giữ (nếu bạn đã biết). | Trần xu? Giao xu làm vượt trần thì sao? | |

## Chat — dùng ACC PHỤ

| Mã | Làm gì | Cần biết | Kết quả |
|---|---|---|---|
| **T10** (M10) | Nhắn riêng cho một acc **chưa kết bạn**. Gõ tin thật dài. Gửi **cùng một nội dung** 5–10 lần liên tiếp. | Có cần kết bạn không? Ô nhập giới hạn bao nhiêu ký tự? Lặp nội dung thì bị khoá sau mấy lần, khoá bao lâu, câu báo gì? | |
| **T11** (M14) | Chat khu **5 giây/lần** trong 3 phút, nội dung khác nhau mỗi lần (thêm số ở đầu). | Có bị chặn hoặc cảnh báo không? | |

## Không test tay được (bot sẽ đo ở P0b)

- **M1:** thứ tự và khuôn các gói tin.
- **M2:** server có gửi gói xoá ô cho bên giao không.
- **M5:** khoảng cách mời vượt ngưỡng của client (client chặn menu khi cách hơn 60 px).
- **M8:** gói tách chồng.
- **M9:** gửi Đồng ý sau 1,5 giây (client ép đủ 5 giây).
- **M12:** 12 acc cùng một khu.
- **M13:** tự đánh ở làng.
