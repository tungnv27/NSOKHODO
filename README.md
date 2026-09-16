# NSOKHODO

Kho đồ chung cho Ninja School Online: khoảng 10 acc clone online 24/7 ở Làng Tone giữ đồ hộ.
Người chơi chỉ nhắn tin và giao dịch với **một acc Leader**. Tool biết tổng kho có gì, và khi cần
thì tự chọn clone giao đồ cho người nhận.

**Trạng thái:** spec đã chốt (v1, 2026-09-16) · test tay trên server chính xong · **chưa code**.

## Tài liệu

| File | Nội dung |
|---|---|
| [`docs/SPEC.md`](docs/SPEC.md) | **Đọc đầu tiên.** Quyết định đã chốt, các luồng nạp / dọn / rút, chat, log, kiến trúc, lộ trình, rủi ro |
| [`docs/GIAO_DICH.md`](docs/GIAO_DICH.md) | Hợp đồng giao thức giao dịch (gói 37, 43–46, 56–58), rương, tách chồng — kèm nguồn và mức chắc chắn |
| [`docs/TEST_TAY.md`](docs/TEST_TAY.md) | Kết quả test tay trên server chính (P0a) |

## Công nghệ

C# · .NET Framework 4.5.2 (`net452`) · WinForms — chạy trên Windows Server 2012, không cần cài runtime.
Khung sẽ tách từ [NSOBAOTATL](https://github.com/tungnv27/NSOBAOTATL) `7715bcf`.
