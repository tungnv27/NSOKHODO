# CLAUDE.md

Hướng dẫn cho Claude Code khi làm việc trong repo này.

# NSOKHODO

Bot **headless** cho Ninja School Online (**chỉ server chính TeaMobi**): **kho đồ chung**. Khoảng 10
clone online 24/7 ở Làng Tone giữ đồ; người chơi chỉ nhắn tin và giao dịch với **một acc Leader**.
Tool biết tổng kho, nhận lệnh rút (tool hoặc chat), tự chọn acc đang giữ món sang khu chính, tách
chồng rồi giao thẳng cho người nhận.

> **Đầu mỗi session:** đọc `docs/STATUS.md` trước, rồi tóm tắt ngắn cho user "đang ở đâu / làm gì
> tiếp" trước khi viết code. Thiết kế đầy đủ: `docs/SPEC.md` (quyết định D1–D85).

## Phạm vi — ranh giới cứng

**CÓ:** giao dịch hai vai (nhận / giao) · Leader nhận đồ + xu · cất / lấy rương ở Thủ khố · dọn kho
Leader → clone theo kệ · dồn xu · sổ kho (túi + rương) · hàng chờ rút + giữ chỗ · tách chồng · lệnh
chat của Chủ kho · Leader rao + báo sự kiện · kệ Rác + nhả / nhận lại clone · Leader dự phòng ·
theo dõi món + báo cáo ngày · log đầy đủ theo ngày · tự đánh chống rớt · nhiều acc, proxy, tự đăng
nhập lại.

**KHÔNG CÓ, và đừng thêm vào nếu chưa bàn lại với user:**
đánh quái · nhặt đồ · PK · đập đồ · **dùng / mặc / bán / vứt bất kỳ món nào** (D38) · tự mở rộng túi
(D44) · rút xu · clone đi tìm người ở map khác · chat thế giới · auto mua bán (P5, chưa ưu tiên).
Nhóm ý tưởng user **đã loại** (đừng đề xuất lại): nhiều kho trên một máy, kho bang hội, báo qua Telegram.

⚠️ **KHÔNG học / tra cứu gì từ `E:\srcnso`** — đó là mã nguồn server lậu. User chỉ chơi bản chính.

## ⛔ Món trong kho là tài sản của người khác

Đồ **chỉ bị khoá khi đem ra dùng** (test tay T0). Bot lỡ dùng / mặc một món = món đó khoá vĩnh viễn,
không giao được nữa. Vì vậy:

- `Service/ItemService.cs` **không có** lệnh dùng / mặc / bán / vứt / nâng cấp. **Đừng thêm lại.**
  `tools/kiemtra/KiemLoi` kiểm đúng danh sách hàm còn lại — thêm hàm mới thì phải sửa ca đó và
  giải thích vì sao an toàn.
- **cmd 22 là "tách TRANG BỊ"** (phá món đã nâng cấp), **không phải** tách chồng. Tách chồng chỉ
  dùng `-28/-85` (D53).
- `Navigator.FindKdlSlot()` luôn −1: **không** đổi khu bằng "Khả di lệnh" (D57).
- Mọi thay đổi đụng tới túi / rương thật → **thêm ca vào `tools/kiemtra/KiemKho.cs` trước**.

## Quan hệ với NSOBAOTATL / NSOLITEPRO

Lõi **chép từ NSOBAOTATL `7715bcf` rồi ĐÓNG BĂNG** (bản lõi thứ ba của họ NSO). Chi tiết lấy gì / sửa
gì: `docs/NGUON_GOC.md` — **cập nhật file đó mỗi lần sửa lõi hoặc lấy thêm code**. Hỏng đăng nhập mà
NSOLITEPRO vẫn chạy → **so lõi trước** (lệnh `diff` ở NGUON_GOC).

⚠️ **KHÔNG đổi `clientType` / version khai lúc đăng nhập** (`1` / `"1.8.0"`). Server trả khuôn gói
khác nhau theo phiên bản.

Thứ tự tra cứu khi hai nguồn mâu thuẫn: `SERVER_FACTS → NINJAPC/251 → MODGAME → NSOTRUNGDUC / NSOCHIP`.

## Tech Stack

- C# / **.NET Framework 4.5.2** (`net452`, csproj kiểu SDK, `LangVersion latest`). Chạy VPS Windows
  Server 2012. `App.config` khai `sku v4.5.2` (D49) — thiếu runtime thì cài .NET 4.8.
- WinForms, **UI tiếng Việt có dấu**. Raw TCP + XOR. **Không thư viện ngoài, không JSON** — dữ liệu
  pipe-delimited / `key=value`.
- csproj **tự glob** `*.cs`.

## Commands

- Build: `dotnet build NSOKHODO.sln -c Release`
- Chạy: `src/NSOKHODO/bin/Release/net452/NSOKHODO.exe` — `--list=<tên>` mở danh sách cụ thể;
  `--chay` tự mở mọi acc (trừ clone đang nhả) khi cửa sổ hiện.
- Kiểm tra: `powershell -ExecutionPolicy Bypass -File tools\kiemtra\chay.ps1`
  (app user đang chạy khoá `bin\Release` → `dotnet build src\NSOKHODO\NSOKHODO.csproj -c Release -o <thư mục tạm>`
  rồi `chay.ps1 -Exe <thư mục tạm>\NSOKHODO.exe`)

## Kiểm tra

Không có test framework. `tools/kiemtra/` là bộ **harness** nạp thẳng `NSOKHODO.exe`:

| File | Kiểm gì |
|---|---|
| `KiemLoi` | dải khu, đếm mất kết nối, tên map, **danh sách lệnh túi đồ còn lại (D38)** |
| `KiemHp` | nhánh sửa "HP đã đầy" của lõi |
| `KiemHopThoai` | dựng `MapPickerForm` + **`MainForm` bố cục A** trong thư mục tạm, mở đủ tab, đóng → ghi cài đặt |
| `KiemKho` | **toàn bộ phần kho, offline**: bỏ dấu / tem, lệnh chat, cài đặt, sổ kho, hàng chờ + giữ chỗ, phiên giao dịch (giả lập gói server qua `TradeState`), log theo ngày, kênh chat, bộ điều phối (vai, lời mời, lệnh chat, rút, dọn kho, dồn xu, nhả clone, Leader dự phòng) |

- `KiemKho` biên dịch với `-r:NSOKHODO.exe`; **csc.exe của .NET Framework chỉ hiểu C# 5** (không `?.`,
  `$""`, `nameof`, `out var`, `=>` cho thuộc tính). Chạy mất ~20 giây vì có chờ thật (1,5 s trước 46…).
- Client giả: `new NsoClient(cfg)` **không Start**, gán `MyChar` / `State` / khu bằng reflection;
  chèn vào `FleetManager._clients`. Bộ điều phối được gọi từng nhịp qua `Nhip()` (không chạy luồng).
  `KhoMode.Tick()` cũng gọi thẳng được (đặt `KhoDieuPhoi.HienTai` bằng reflection) — dùng cho mọi
  vòng gửi gói (rương, tách chồng).

### Test sống trên server — `tools/kiemtra/song.ps1` (KHÔNG nằm trong `chay.ps1`)

| File | Vai |
|---|---|
| `KhoSong` | kho **không giao diện**, dựng y như `MainForm` (fleet + điều phối + nhật ký). Lệnh qua `song\lenh-kho.txt` (`rut`, `rutn <người nhận> tpl:cap:sl,…` = đơn nhiều món, `rutk <khu> <tpl> <cap> <sl> <người nhận>` = giao ở khu riêng, `tiep`, `huy`, `nha`, `nhanlai`, `dungacc`, `chayacc`, `cfg <Khoá> <giá trị>` (vd `cfg ChuKho tungkhodo9` để acc người chơi ra lệnh chat, `cfg NguongNhan 31` để Leader "đầy"), `dung`); trạng thái ra `song\trangthai.txt` mỗi 5 giây; dòng `[SK]` = log dễ đọc (D83) |
| `NguoiChoi` | một acc **của user** đóng vai người ngoài (tiến trình riêng → không có điều phối → mode kho đứng im): đi khu, nạp, tự nhận, mời, chat riêng, tách chồng, vào lại, `tui` (in túi kèm cờ hạn), `theo <tên> <giây>` (in toạ độ người khác mỗi lần đổi), `nhay`/`nhayx <d,d,..> <gap> [cuoi]` (thử gói di chuyển — M23) |

- Acc người chơi **tự nhận mọi lời mời** khi `nhan on` → người lạ có thể đưa đồ vào (17/09 đã gặp). Xong việc thì `nhan off`.
- Acc người chơi cũng chạy **nhịp chống AFK 60 giây** → khi đo toạ độ, nhớ trừ các lần nhịp chen vào.
- Kiểm giao diện không cần acc: nạp `MainForm` trong một harness nhỏ (danh sách trống, thư mục tạm) rồi
  `DrawToBitmap` — UIA không thấy ô số `NumericUpDown`.

- `-ChuanBi` tạo `tools/kiemtra/song/` (đã ignore) với **bản sao** acc + cài đặt kho, biên dịch hai harness.
  Mỗi vai chạy từ `song\bin\<vai>\` (bản chép EXE riêng) → build lại không bị khoá.
- Acc người chơi phải nằm ngoài kho (`-Bo`) hoặc đang **nhả**. Món chỉ đi giữa các acc của user.
- ⚠️ Vòng nào gửi gói lặp lại phải có **chốt chặn số lần / nhịp** (bài học: xin rương 1.537 lần, D69).
- ⚠️ `TaskStop` không giết được script Git-bash chạy nền (vòng `sleep` vẫn chạy và gửi lệnh) →
  dừng bằng `Stop-Process` theo command line.

⚠️ **"Build xanh" KHÔNG có nghĩa là EXE đã mới** (bài học NSOBAOTATL): tắt app đang chạy trước khi
build, và soát `LastWriteTime` của EXE — `chay.ps1` in ra dòng đó.

## Code Style

- .NET 4.5.2 — `Thread`/`ThreadPool`, hạn chế async. **KHÔNG gọi API mới hơn 4.5.2**
  (`Array.Empty`, `Task.CompletedTask`, `DateTimeOffset.ToUnixTime*`…) và không dùng tuple `(a, b)`.
- Event: `public event Action<T> OnXxx;` + null-check. Service = **gửi**, Controller/Handler = **nhận**.
- Tên lớp / hàm / biến trong phần kho viết **tiếng Việt không dấu** (`SoKho`, `LapKeHoach`…) theo
  khung NSOBAOTATL. Comment và log do bot tự viết: **không dấu**. Chữ lấy từ server giữ nguyên.
- **Mọi tin gửi vào game:** tem `@NNN` (D18) + **không dấu** (D46) + ≤100 ký tự — đi qua
  `KenhChat` / `ChuVan.ChuanBiTinGui`, **không gọi thẳng** `ChatService`.
- Comment giải thích **vì sao**. Chỗ nào từng trả giá thì ghi cái giá đó.

## Kiến trúc

```
MainForm (bố cục A) ──view──> FleetManager ──owns──> N × NsoClient
      │                                                   │
      │ Lam(...)                                    KhoMode (mode duy nhất)
      ▼                                               ├─ PhienGiaoDich (1 phiên, 2 vai)
KhoDieuPhoi (1 luồng, nhịp 1 s) <── LayViec / XetLoiMoi / TimNguoi ──┤
  ├─ SoKho · HangCho · KenhChat · KhoConfig                ├─ Navigator (đi map, đổi khu NPC 13)
  └─ NhatKy (Logs/<danh sách>/<ngày>/)  <── BaoViec / BaoPhien ───────┘  + Heartbeat 60 s + KeepAlive
```

- **Một luồng quyết định.** Mode của từng acc chỉ hỏi (`LayViec`, `XetLoiMoi`, `TimNguoi`) và **báo**
  (`BaoViec`, `BaoPhien` — chỉ xếp hàng). Giao diện gọi `KhoDieuPhoi.Lam(...)`. Đừng để luồng khác
  sửa hàng chờ / việc / đơn dọn trực tiếp.
- `KhoMode.Tick()`: sống/chết → **phiên đang chạy** → **lời mời** → **việc** → **rảnh** (về khu, chỗ đứng).
  Mỗi bước làm một chút rồi trả về, để lời mời (sống 31 giây) luôn được xét kịp.
- Mỗi acc làm **tối đa một việc** (`Viec`): `DocRuong`, `CatRuong`, `GiaoMon`, `DoiNhan`.

## Important Rules

- **CHỈ commit sau khi user xác nhận đã test đúng.** Build sạch + `chay.ps1` PASS → báo user cách test
  (`docs/TEST_2CHANG.md`) → chờ xác nhận → mới commit.
- **KHÔNG commit `accounts.txt`**, `Data/`, `Logs/` (đã ignore).
- **KHÔNG tự ý TẮT / THU HẸP tính năng đã có**, kể cả "cho an toàn". Buộc phải giới hạn → giới hạn
  đúng chỗ gây hại, ghi lý do tại chỗ, **nói với user**. Mọi tính năng lớn đã có công tắc trong
  Cài đặt → Kho (D50).
- **Sửa SPEC sau mốc chốt:** thêm một vòng ở §1 + dòng quyết định ở §2. **Không sửa ngầm.**
- **Đổi BỐ CỤC giao diện → trình user duyệt TRƯỚC** (bố cục A đã duyệt, D51).
- **`accounts.txt` giữ nguyên 23 cột** — đi lại được giữa ba tool.
- **Thread safety:** `GameStateManager` bị nhiều luồng truy cập — chụp mảng trước khi lặp
  (`OtherPlayers.ToArray()` trong `try`).
- ⚠️ `SplitContainer`: không đặt `SplitterDistance` / `Panel*MinSize` trong constructor — đặt trong `Shown`.
- ⚠️ `RadioButton`: mỗi cụm một `Panel` riêng.

## Tài liệu

| File | Nội dung |
|---|---|
| `docs/STATUS.md` | **ảnh chụp trạng thái — ĐỌC ĐẦU TIÊN** |
| `docs/SPEC.md` | thiết kế đã chốt, quyết định D1–D85, lộ trình, rủi ro |
| `docs/GIAO_DICH.md` | hợp đồng giao thức giao dịch / rương / tách chồng + bảng đo M1–M26 |
| `docs/TEST_TAY.md` | kết quả test tay trên server chính (T0–T14) |
| `docs/TEST_2CHANG.md` | checklist buổi test 2 chặng |
| `docs/NGUON_GOC.md` | lấy gì từ đâu, sửa gì trong lõi |
| `docs/WORKLOG.md` | nhật ký (append-only) |
