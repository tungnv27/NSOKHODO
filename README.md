# NSOKHODO

Kho đồ chung cho Ninja School Online (server chính TeaMobi): khoảng 10 acc clone online 24/7 ở
Làng Tone giữ đồ hộ. Người chơi chỉ nhắn tin và giao dịch với **một acc Leader**. Tool biết tổng kho
có gì, và khi cần thì tự chọn clone giao đồ cho người nhận.

**Trạng thái (2026-09-17 10h30):** spec v1.3 + vòng 11 · test tay xong · **đã code P1–P4 và test sống
trên server chính** (`docs/TEST_2CHANG.md` đầu file) · đã sửa các lỗi user gặp khi dùng thật (giao 30 món, đơn nhiều
ID, giao diện giật, giới hạn login, nhân vật "bay", clone kẹt túi + rương, chọn khu giao) · **vòng 12–13:** gom đồ xếp chồng về một nick, xả nhanh (Leader chuyển ngay sang clone
đứng cạnh), log dễ đọc · còn phần giao diện cho user.

## Chạy

```
dotnet build NSOKHODO.sln -c Release
src\NSOKHODO\bin\Release\net452\NSOKHODO.exe          (--list=<tên> · --chay)
powershell -ExecutionPolicy Bypass -File tools\kiemtra\chay.ps1
powershell -ExecutionPolicy Bypass -File tools\kiemtra\song.ps1 -ChuanBi   (test sống, xem CLAUDE.md)
```

- Triển khai: chép `NSOKHODO.exe` (một file, ~1,2 MB). `Data/` và `Logs/` tự tạo cạnh exe.
- **Một kho = một tiến trình = một danh sách acc.** Cài đặt kho, sổ kho, hàng chờ, log đều tách theo
  tên danh sách: `Data/Kho/<danh sách>.*`, `Logs/<danh sách>/<ngày>/`.
- Mọi acc trong danh sách phải **cùng máy chủ** với Leader.

## Dùng nhanh

1. Thêm acc → tab **Acc**, chuột phải → *Đặt làm Leader* (và *dự phòng*).
2. **Cài đặt → Kho:** khu chính, khu phụ, tên **Chủ kho**. **▶ Chạy**.
3. Nạp: mời Leader giao dịch — nạp nhiều lượt liền cũng được, Leader chuyển ngay sang clone đứng cạnh (Leader đầy
   thì khung mở rồi tắt, mời lại sau ~3 giây). Khung log dưới cùng mặc định
   chỉ hiện dòng dễ đọc "ai → ai: món"; tick **Chi tiết** để xem log kỹ thuật.
   Rút: tab **Tổng kho** → Điều phối → **Giao ngay** (một món) hoặc
   **+ Vào đơn** nhiều món rồi **Giao đơn** (ô **Khu giao**: −1 = khu chính), hoặc nhắn riêng Leader:

| Lệnh (chỉ Chủ kho) | Việc |
|---|---|
| `kho` | tóm tắt kho |
| `nap` | chen ngang + giữ cửa 60 giây để nạp ngay |
| `tim <từ khoá>` · `co <id>` | tìm món · kho có bao nhiêu |
| `lay <id> [sl\|het] [+cấp] [khu N] [cho <tên>]` | rút đồ (mặc định giao cho chính người nhắn, ở khu chính) |
| `lay goi <tên> [khu N] [cho <tên>]` · `goi` | rút cả gói · liệt kê gói |
| `tiep [#số]` · `huy [#số]` | chạy tiếp lệnh tạm dừng · huỷ lệnh của mình |
| `theo <id> [N]` · `botheo <id>` | theo dõi món |
| `xa` · `xa xong` | **xả nhanh**: gọi sẵn 2 clone đứng cạnh Leader / báo đã nạp xong (không nhắn thì lần nạp đầu cũng tự gọi) |

## Tài liệu

| File | Nội dung |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | Luật làm việc trong repo, kiến trúc, cách kiểm tra |
| [`docs/STATUS.md`](docs/STATUS.md) | Đang ở đâu, làm gì tiếp |
| [`docs/SPEC.md`](docs/SPEC.md) | Quyết định đã chốt (D1–D87), các luồng nạp / dọn / rút, chat, log, kiến trúc, lộ trình, rủi ro |
| [`docs/GIAO_DICH.md`](docs/GIAO_DICH.md) | Hợp đồng giao thức giao dịch (gói 37, 43–46, 56–58), rương, tách chồng — kèm nguồn và mức chắc chắn |
| [`docs/TEST_TAY.md`](docs/TEST_TAY.md) | Kết quả test tay trên server chính (P0a) |
| [`docs/TEST_2CHANG.md`](docs/TEST_2CHANG.md) | Checklist buổi test 2 chặng |
| [`docs/NGUON_GOC.md`](docs/NGUON_GOC.md) | Khung tách từ NSOBAOTATL `7715bcf`; sửa gì trong lõi |
| [`docs/WORKLOG.md`](docs/WORKLOG.md) | Nhật ký |

## Công nghệ

C# · .NET Framework 4.5.2 (`net452`) · WinForms — chạy trên Windows Server 2012.
Máy cần **.NET Framework 4.5.2 trở lên**; mở app mà Windows báo thiếu thì cài **.NET Framework 4.8**.
Khung tách từ [NSOBAOTATL](https://github.com/tungnv27/NSOBAOTATL) `7715bcf`.
