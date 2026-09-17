# Nguồn gốc — lấy gì từ đâu, bỏ gì, sửa gì

> **Cập nhật file này mỗi lần lấy thêm code từ NSOBAOTATL / NSOLITEPRO, và mỗi lần sửa phần lõi.**
> Không có nó thì vài tháng nữa không ai còn biết dòng nào là bản sao đã đóng băng, dòng nào là
> của riêng repo này.

## Mốc gốc

| | |
|---|---|
| Repo nguồn trực tiếp | `D:\10\NSOBAOTATL` (`github.com/tungnv27/NSOBAOTATL`) |
| Commit gốc | **`7715bcf`** (2026-09-16, nhãn "CHUA TEST") |
| Nguồn của nguồn | NSOBAOTATL lại tách từ NSOLITEPRO **`79d9d86`** — xem `NSOBAOTATL/docs/NGUON_GOC.md` |
| Ngày tách | 2026-09-16 |
| Cách tách | **chép một lần rồi ĐÓNG BĂNG.** Đổi chuỗi `NSOBAOTATL` → `NSOKHODO` theo byte (namespace, tên exe, `LogicalName` tài nguyên nhúng). GUID project mới trong `NSOKHODO.sln`. |

⇒ NSOKHODO là **bản lõi đóng băng thứ ba** của họ NSO (SPEC §12, rủi ro 6).

## ⚠️ Cái giá đã biết của việc đóng băng

NSOLITEPRO: **16 trên 61 commit** gần đây đụng `Core/` hoặc `Protocol/`. Server đổi giao thức →
NSOLITEPRO được sửa → **bên này không tự có**; triệu chứng là "tự nhiên không đăng nhập được".

**Khi hỏng mà bên kia chạy tốt, việc ĐẦU TIÊN là so lõi:**

```bash
diff -r -q --strip-trailing-cr D:/10/NSOBAOTATL/src/NSOBAOTATL/Core      D:/10/NSOKHODO/src/NSOKHODO/Core
diff -r -q --strip-trailing-cr D:/10/NSOBAOTATL/src/NSOBAOTATL/Protocol  D:/10/NSOKHODO/src/NSOKHODO/Protocol
diff -r -q --strip-trailing-cr D:/10/NSOLITEPRO/src/NSOLITEPRO/Core      D:/10/NSOKHODO/src/NSOKHODO/Core
```

Khác biệt được phép ở `Core/`: tên namespace. Ở `Protocol/`: tên namespace + các mã thêm ở §"Sửa có
chủ đích" dưới đây.

## Kế thừa nguyên từ NSOBAOTATL

Toàn bộ bảng "Chép NGUYÊN VẸN" và "Sửa có chủ đích" của `NSOBAOTATL/docs/NGUON_GOC.md` **vẫn đúng ở
đây** (stack 256 KB, comment proxy mẫu, `DangTanSat` = false, `DungImHopLe` = true, `accounts.txt`
giữ 23 cột, `TrainConfig` giữ mọi khoá…), trừ những dòng bị thay ở bảng dưới.

Giữ lại từ phần "viết mới" của NSOBAOTATL: `Fleet/DisconnectStats.cs` (đếm mất kết nối),
`KeepAliveController`, `AutoModeBase.Heartbeat` (tự đánh 60 giây — D23).

## Sửa có chủ đích so với NSOBAOTATL `7715bcf` (đừng "sửa lại cho giống")

| Chỗ | Sửa gì | Vì sao |
|---|---|---|
| `App.config` | `sku` `v4.5` → **`.NETFramework,Version=v4.5.2`** | D49: máy thiếu runtime thì từ chối mở kèm thông báo, không chết giữa chừng |
| `Service/ItemService.cs` | **viết lại, gỡ hết** lệnh dùng / mặc / tháo / bán / vứt / nâng cấp / luyện / mua / bùa và **cmd 22 (tách TRANG BỊ)**. Còn: 42 (xin chi tiết), `-28/-85` tách chồng (`SendSplitStack`), sắp xếp túi, 16, 17, `-30/-103` | D38: bot dùng / mặc món nào là món đó khoá vĩnh viễn. Gỡ ở tầng gửi gói ⇒ code nào lỡ gọi thì **không build được**. D53: cmd 22 phá đồ đã nâng cấp. `tools/kiemtra/KiemLoi` kiểm danh sách hàm còn lại |
| `Protocol/SubCommandCodes.cs` | thêm `NotMap.ITEM_SPLIT = -85` | tách chồng |
| `Protocol/CommandCodes.cs` | thêm `TRADE_OPEN_UI 37`, `TRADE_INVITE 43`, `TRADE_INVITE_ACCEPT 44`, `TRADE_LOCK_ITEM 45`, `TRADE_ACCEPT 46`, `TRADE_INVITE_CANCEL 56`, `TRADE_CANCEL 57`, `TRADE_OK 58` | giao dịch (`GIAO_DICH.md` §2) |
| `Controller/MessageRouter.cs` | ctor nhận `TradeState`; thêm móc `RawHook` (gọi đầu `HandleMessage`, biến cục bộ tên `mocTho` vì hàm đã có biến `raw`); route 37/43/45/46/57/58 → `TradeHandler`; chữ server −24/−26 → `TradeState.GhiTinServer` | nhận giao dịch; ghi hex; giữ câu chữ server để phân loại lý do huỷ (M18) |
| `Controller/ChatHandler.cs` | `LastRaw` gán cả cho −26 | nt |
| `Client/NsoClient.cs` | thêm `Trade` (`TradeState`, sống cả đời client), `TradeSvc`, `DangGiaoDich`, `VaoKhuLucUtc` (mốc nhận MAP_INFO), sự kiện `OnPrivateChatReceived` + `OnRawPacket`; `Trade.DatLaiKhiMatKetNoi()` khi rớt / `Stop` / `ForceRelogin` | kho |
| `Client/NsoClient.cs` | mode = **`Auto.KhoMode` cho MỌI acc**, **không xét `BatBao`** (cột 18 vẫn đọc/ghi để `accounts.txt` đi lại được) | D58: bố cục A không có nút bật/tắt từng acc; cho clone nghỉ = "Nhả clone" |
| `Auto/AutoModeBase.cs` | bỏ `BinhMauRunner`; `ACT_DUNG_CANH` = "Sẵn sàng" | D38: uống bình = tiêu hao đồ trong kho |
| `Auto/Navigation/Navigator.cs` | `FindKdlSlot()` **luôn −1** | D57: "Vô hạn khả di lệnh" trong túi có thể là đồ gửi kho; luôn đổi khu qua NPC 13. `KiemKho` kiểm |
| `Auto/AddOns/KeepAliveController.cs` | đang giao dịch thì **không nhích chống-AFK** (chỉ gửi lại vị trí hiện tại) | di chuyển giữa phiên có thể làm server huỷ phiên (T6) |
| `Auto/AddOns/KeepAliveController.cs` | `Nudge`: sau 4 bước, gửi **3 gói về chỗ cũ** kiểu `CharBurstMove`; `MyChar` đổi giữa nhịp (mode tự đi) → bỏ nhịp; vào giao dịch giữa nhịp → về chỗ cũ ngay (D77, 2026-09-17) | M23: bước cuối một gói bị server bỏ qua → người khác thấy nhân vật lơ lửng ở y − 52. **Lỗi này nhiều khả năng cũng có ở NSOBAOTATL / NSOLITEPRO** (cùng hàm) — chưa sửa bên đó || `Program.cs` | bỏ `BaoOptions.Load()` | `BaoOptions` đã bỏ |

## Bỏ khỏi khung NSOBAOTATL

`Auto/AddOns/BaoTaTl.cs` · `Auto/Modes/StandMode.cs` · `Auto/AddOns/BinhMauRunner.cs` ·
`Config/BaoOptions.cs` · `UI/ChiaKhuForm.cs` · `tools/kiemtra/KiemBinh.cs` · phần "chia khu" của
`tools/kiemtra/KiemLoi.cs`.

`UI/MainForm.cs` **viết lại hoàn toàn** theo bố cục A (tách 6 file `MainForm*.cs`), chỉ giữ các bẫy
đã biết: VirtualMode + `InvalidateRow`, `_anhChup`, log qua hàng đợi, `SplitContainer` đặt kích thước
trong `Shown`, `RadioButton` có `Panel` riêng.

## Viết mới hoàn toàn

| File | Việc |
|---|---|
| `Service/TradeService.cs` | chiều GỬI giao dịch: 43, 44, 45, 46, 56, 57 |
| `Controller/TradeHandler.cs` | chiều NHẬN: 37, 43, 45, 46, 57, 58; hằng `MAX_MON` (12 ô), `MAX_SO_LUONG` (29.999 / ô — D88) |
| `Client/TradeState.cs` | trạng thái giao dịch (luồng nhận ghi, luồng mode đọc ảnh chụp) |
| `Auto/Modes/KhoMode.cs` | mode duy nhất: phiên → lời mời → việc → rảnh (về đúng khu / chỗ đứng) |
| `Kho/PhienGiaoDich.cs` | máy trạng thái một phiên, hai vai (nhận / giao) |
| `Kho/KhoDieuPhoi.cs` | bộ điều phối (một luồng, nhịp 1 giây): Leader, lời mời, lệnh chat, hàng chờ rút, dọn kho, dồn xu, bảo trì clone, theo dõi, báo cáo, nhả clone, rao |
| `Kho/SoKho.cs` · `Kho/HangCho.cs` | sổ kho (túi + rương) · hàng chờ lệnh rút + giữ chỗ |
| `Kho/KhoConfig.cs` · `Kho/KeHang.cs` · `Kho/BangMon.cs` | cài đặt kho · kệ hàng · cache tên món |
| `Kho/LenhChat.cs` · `Kho/KenhChat.cs` · `Kho/ChuVan.cs` | đọc lệnh chat · hàng đợi chat ra + rao · bỏ dấu / tem |
| `Kho/NhatKy.cs` | log đầy đủ theo ngày (`Logs/<danh sách>/yyyy-MM-dd/`) |
| `Kho/Viec.cs` | việc bộ điều phối giao cho một acc |
| `UI/MainForm*.cs` · `UI/HopNhap.cs` | giao diện bố cục A · hộp nhập nhỏ |
| `tools/kiemtra/KiemKho.cs` | bộ kiểm tra offline toàn bộ phần kho |

## Nguồn tra cứu khi hai bên mâu thuẫn

```
SERVER_FACTS (NSOLITEPRO, đã đo hex) → NINJAPC / 251 → MODGAME (180) → NSOTRUNGDUC / NSOCHIP
```

**KHÔNG dùng `E:\srcnso`** — đó là mã nguồn server lậu; tool này chỉ nhắm server chính TeaMobi (SPEC D9).
