# NSOKHODO — Kho đồ chung cho Ninja School Online

> **Trạng thái:** SPEC — ý tưởng chốt với user ngày **2026-09-16** (3 vòng). **CHƯA CODE.**
> **Hợp đồng giao thức giao dịch** (gói tin, nguồn, mức chắc chắn): `GIAO_DICH.md`. Đọc file đó trước khi code.

---

## 0. Tóm tắt

Bot **headless** giữ khoảng 10 acc clone online 24/7 ở **Làng Tone (map 22)**.

- **Leader** đứng ở **khu chính**, tại toạ độ user cài. Đây là acc **duy nhất** người chơi nhắn tin và nạp đồ vào.
- **Clone** đứng ở **khu phụ** (cho đỡ rối). Chỉ khi cần giao nhận chúng mới sang khu chính.
- Leader nhận đồ rồi tự dọn đi chỗ khác (rương của nó, các clone).
- Tool biết **tổng kho có gì, nằm ở đâu, còn bao nhiêu chỗ**.
- Khi có lệnh rút, tool tự chọn clone đang giữ món. Clone đó sang khu chính, tách chồng nếu cần, rồi **giao thẳng** cho người nhận.
- Leader báo tình hình qua **chat riêng** và **chat cộng đồng**, đồng thời tự rao trạng thái kho **5 giây một lần**.

## 1. Ý tưởng gốc (user, nguyên văn)

**Vòng 1:**
> Mình tạo khoảng 10 con clone đi. Làm kho chung. Xong mình sẽ vứt rác tùm lum gì cũng vứt vào đó.
> Mấy con đó sẽ onl 24/7. NV của tool là: Tổng kho đang có những cái gì. Khi nào mình muốn lấy item,
> tool sẽ phải tự phân phối acc giao cho mình, thay vì phải thủ công lọ mọ phân chia vào từng acc.

> Cần 1 con acc làm key để chat với người dùng. Con key đó sẽ làm nhiệm vụ leader. Nhận item, nó
> cất đâu kệ nó. Điều clone giao ai kệ nó. Người chơi chỉ chát riêng với mỗi con đó thôi.

**Vòng 2 (bổ sung):**
> Trên tool có thể chỉnh: cho phép tất cả gd đồ cho leader, hay chỉ người được cài. Chat với người
> chơi khác thì luôn phải @[000-999] ở trước. Có tự động chat cộng đồng, 5s 1 lần, tự đặt text (chỉ
> leader), báo cáo tình trạng kho, ví dụ kho đồ 70/360 — chỗ trống tính cả hành trang và rương của
> tổng clone. Các clone đứng ở 1 khu khác cho đỡ rối, khi cần thì đến khu chính. Leader vừa chat
> riêng báo tình hình vừa chat publish để tiện quan sát. Kiểm tra kho + rút đồ qua tin nhắn thì chỉ
> người được cài trên tool mới làm được. Trên tool có thể điều phối giao dịch item gì cho ai, xem
> được trạng thái. Chỉ giao dịch item không khoá. Item bị gộp thì phải tách ra rồi giao dịch. Mọi tài
> khoản đều có cơ chế tự đánh vào bản thân giống NSOLITE (chống disconnect). Cài được toạ độ x/y
> của leader; clone đứng đâu cũng được.

**Vòng 3 (chốt lại sau khi MINH cảnh báo):**
> Chế độ Tất cả: mặc định tất cả. Hồi chiêu đổi khu là 10s, đếm ngược từ lúc đặt chân tới khu mới —
> clone đã đứng ở khu khác từ trước thì chỉ cần đổi khu luôn là xong. "70/360" là ô đã dùng. Chat
> cộng đồng để người cùng khu biết. Mặc định bot chỉ giao tiếp với chủ, có thể nhiều chủ. Còn giao
> cho ai thì tùy chủ quyết định, trên tool hoặc qua bot. Log đầy đủ theo ngày.

## 2. Quyết định đã chốt

| # | Quyết định | Nguồn |
|---|---|---|
| D1 | **Chỉ chuyển đồ bằng GIAO DỊCH** (gói 43–58). Không dùng vứt–nhặt, gian hàng hay gia tộc. | MINH đề xuất. Đồ vứt ra đất không có chủ sở hữu (`GIAO_DICH.md` §8). Giao dịch đã chạy thật trong mod Giao/Nhận đồ của NSOTRUNGDUC. |
| D2 | **Người giao dịch với kho có thể dùng client bất kỳ và bấm tay.** `gdvp` / `nhan` của NSOTRUNGDUC chỉ là một trường hợp tương thích. | user |
| D3 | **Một Leader là đầu mối duy nhất** (chat + nạp đồ). Leader **chỉ là cửa nhận**, luôn cố giữ túi trống. | user |
| D4 | Khi rút, **clone đang giữ món giao thẳng** cho người nhận, không đi qua Leader. | user |
| D5 | **Người nhận tự tới khu chính.** Clone không đi tìm người ở map khác. | user. Không có gói nào cho biết người chơi đang ở map nào. |
| D6 | **Kênh lệnh gồm giao diện tool và chat riêng**, cùng đổ vào một hàng chờ. | user |
| D7 | **Sức chứa:** Leader cất rương của nó, rồi dồn đồ sang clone. Clone tự cất rương của mình. | user |
| D8 | **Auto mua bán để sau.** Bộ máy giao dịch phải có sẵn tham số xu và móc kiểm tra những gì đối phương đặt vào. | user |
| D9 | **Nguồn tra cứu:** SERVER_FACTS → NINJAPC → MODGAME → NSOTRUNGDUC / NSOCHIP. **Không dùng `E:\srcnso`** (server lậu). | user |
| D10 | Tên project: **NSOKHODO**. | user |
| D11 | **Máy chủ chọn được trong cài đặt.** Một kho chỉ gồm các acc cùng một máy chủ. | user |
| D12 | **Map mặc định 22 (Làng Tone).** Map, khu chính và khu phụ đều cài được. | user |
| D13 | **Leader đứng đúng toạ độ x/y cài sẵn. Clone không cố định toạ độ**, chỉ cần đúng map + khu phụ. | user |
| D14 | **Clone ở khu phụ**, chỉ sang khu chính khi cần giao nhận. | user |
| D15 | **Kho nhận hết** mọi thứ giao dịch được: **mọi món không khoá và cả xu**. | user (*"Kho nhận hết"*) |
| D16 | **Chế độ nhận** chỉnh được trên tool: **Chỉ Chủ kho** hoặc **Tất cả mọi người** được nạp đồ vào Leader. **Mặc định: Tất cả.** | user — giữ nguyên sau khi đã được cảnh báo rủi ro §14 mục 8 |
| D17 | **Xem kho và rút đồ qua tin nhắn: chỉ Chủ kho** (danh sách cài trên tool). Người ngoài không được, kể cả khi đang bật chế độ Tất cả. | user |
| D18 | **Tin gửi cho người chơi khác, riêng hay cộng đồng, luôn bắt đầu bằng `@NNN`** (000–999). | user. Server chính khoá chat khi nội dung lặp lại (`NSOLITEPRO/docs/features/BAO_TA_TL.md`). |
| D19 | **Leader tự rao ở chat cộng đồng 5 giây một lần**, nội dung tự đặt, báo tình trạng kho. **Chỉ Leader rao.** | user |
| D20 | **Leader báo sự kiện qua chat riêng VÀ chat cộng đồng.** | user |
| D21 | **Tool điều phối được:** giao món gì, bao nhiêu, cho ai; xem trạng thái từng acc và từng phiên. | user |
| D22 | **Chỉ giao dịch món không khoá. Món bị gộp chồng mà chỉ lấy một phần thì tách ra trước.** | user + client gốc |
| D23 | **Mọi acc (Leader + clone) tự đánh chính mình để chống rớt mạng**, giống NSOLITEPRO. | user. Đã có sẵn: `AutoModeBase.Heartbeat` (gói 61, 60 giây/lần) + `KeepAliveController.TickDocIm` (gói 93). |
| D24 | **Bot chỉ chat riêng với Chủ kho** (mặc định). **Có thể có nhiều Chủ kho.** Người lạ nạp đồ được (D16), nhưng không nhận tin riêng nào. | user |
| D25 | **Giao cho ai là do Chủ kho quyết**, trên tool hoặc qua lệnh chat. Người nhận có thể là **bất kỳ tên nào**. | user |
| D26 | **Log đầy đủ, mỗi ngày một file**, không ghi đè (§8.1). | user |
| D27 | Câu rao `70/360` nghĩa là **ô đã dùng / tổng ô**. "Chat cộng đồng" là **chat khu** (gói −23), để người cùng khu biết. | user |
| D28 | **Hồi chiêu đổi khu là 10 giây, tính từ lúc đặt chân tới khu mới.** Clone đứng yên ở khu phụ đủ lâu thì đổi sang khu chính được **ngay**. | user (luật game) |
| D29 | **Chạy trên Windows Server 2012.** Dùng **C# / .NET Framework 4.5.2 (`net452`) / WinForms**, giống NSOLITEPRO và NSOBAOTATL. Chi tiết ở §3.1. | user (yêu cầu môi trường) + bằng chứng: NSOLITEPRO net452 đã chạy "pass" trên VPS Server 2012 (`NSOLITEPRO/docs/WORKLOG.md:2020-2033`) |

## 3. Phạm vi

**CÓ (giai đoạn 1–4):**
- Đăng nhập nhiều acc, chia proxy, tự đăng nhập lại, tự đánh chống rớt mạng. Phần này đã có trong lõi.
- Đi tới map kho; Leader về khu chính và đứng đúng x/y; clone về khu phụ.
- Bộ máy giao dịch với hai vai: **nhận** và **giao**.
- Leader nhận đồ và xu; cất rương; dồn đồ sang clone.
- Clone sang khu chính theo lệnh; lấy đồ từ rương, tách chồng, giao cho người nhận.
- Sổ kho gồm túi và rương; nhật ký nhập/xuất.
- Lệnh chat riêng; Leader tự rao và báo sự kiện ở chat cộng đồng.
- Giao diện: tổng kho, điều phối, hàng chờ, trạng thái, cài đặt.

**KHÔNG (đừng thêm vào nếu chưa bàn lại):**
- Đánh quái, nhặt đồ, PK, đập đồ.
- Auto mua bán, rao bán hàng, **rút xu** (P5).
- Clone đi tìm người ở map khác.
- Chat thế giới.
- Bản Android, obfuscate.

## 3.1 Công nghệ và môi trường chạy (D29)

| Hạng mục | Chốt | Vì sao |
|---|---|---|
| Ngôn ngữ | **C#** | Toàn bộ lõi (~30.000 dòng: protocol, điều hướng, quản lý nhiều acc) đã có sẵn bằng C# |
| Nền tảng | **.NET Framework 4.5.2** — csproj kiểu SDK, `<TargetFramework>net452</TargetFramework>` | Server 2012 có sẵn .NET 4.5, **không phải cài runtime**. Target net452 còn là **chốt chặn lúc build**: gọi API mới hơn 4.5.2 thì không build được. |
| `App.config` | `supportedRuntime sku=".NETFramework,Version=v4.5"` + `gcServer enabled="true"` | Máy chỉ có 4.5 gốc vẫn mở được app. Server GC giúp chịu tải nhiều acc (giữ nguyên như NSOBAOTATL). |
| Giao diện | **WinForms**, tiếng Việt **có dấu** | Dùng qua Remote Desktop trên VPS. Log trong file ghi không dấu (§8.1). |
| Thư viện ngoài | **Không có.** Không NuGet, không thư viện JSON; dữ liệu ghi dạng pipe-delimited / `key=value` | Chép 1 file exe là chạy; không bị lệch phiên bản DLL trên VPS |
| Build | `dotnet build NSOKHODO.sln -c Release` **trên máy dev** | VPS chỉ cần file exe, không cần SDK |
| Triển khai | Chép `NSOKHODO.exe` (map `.bin` + ảnh nhúng sẵn, khoảng 1 MB). Thư mục `Data/` và `Logs/` tự tạo cạnh exe. | Giống NSOBAOTATL: một file, chạy ngay |
| Đa luồng | `Thread` / `ThreadPool`, hạn chế `async`/`await`; stack 256 KB mỗi luồng | Theo NSOBAOTATL (đo được 1.800 acc chạy trong một tiến trình) |

**Luật code bắt buộc khi chạy Server 2012:**
1. **Không gọi API mới hơn 4.5.2.** Ví dụ `DateTimeOffset.ToUnixTime*`, `Array.Empty`, `Task.CompletedTask`. Cần Unix-time thì tính tay từ mốc `1970-01-01 UTC`. Bài học: NSOLITEPRO từng crash `Method not found` trên VPS vì đúng lỗi này (`WORKLOG.md:2028`).
2. **Không dùng cú pháp cần kiểu dữ liệu mà 4.5 không có**: tuple `(a, b)` (cần `System.ValueTuple`), `Span<T>`, `IAsyncEnumerable`… `LangVersion latest` cho phép viết, nhưng thiếu kiểu thì build báo lỗi — chốt chặn vẫn hoạt động.
3. ⚠ **Kẽ hở nhỏ của chốt chặn:** app khai cần 4.5, nhưng build theo bộ API của 4.5.2. Vài API **chỉ có từ 4.5.1 hoặc 4.5.2** vẫn build được mà sẽ crash trên máy **chỉ có 4.5 gốc**. Hai cách xử lý:
   - tránh dùng các API đó;
   - hoặc cài .NET 4.5.2 trở lên trên VPS. Server 2012 hỗ trợ tới 4.8, và thường Windows Update đã tự cài.
4. **Kiểm tra bản .NET trên VPS** (PowerShell):
   ```powershell
   (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
   ```
   Kết quả: `378389` = 4.5 · `379893` = 4.5.2 · `393295` trở lên = 4.6+ · `528040` trở lên = 4.8.
5. **Không phụ thuộc HTTPS.** Nếu sau này có gọi web (vd báo qua Telegram), .NET 4.5 mặc định chỉ dùng TLS 1.0, phải tự bật TLS 1.2 qua `ServicePointManager`. Hiện bot chỉ dùng TCP thô tới server game nên chưa gặp vấn đề này.

**Không chọn .NET mới hơn (6/8)** vì phải port toàn bộ lõi, exe tự chứa runtime nặng hơn nhiều, và mất lợi thế "chép 1 file là chạy" đã được kiểm chứng trên chính VPS này.

**Rủi ro môi trường (ngoài phạm vi tool):** Windows Server 2012 đã **hết hỗ trợ bảo mật từ 10/2023**. Nếu VPS mở Remote Desktop ra Internet thì nên đổi port, đặt mật khẩu mạnh, hoặc giới hạn IP được truy cập.

## 4. Vai trò, bố trí, cài đặt

| Vai | Số lượng | Ở đâu | Việc |
|---|---|---|---|
| **Chủ kho** | danh sách tên, user cài | — | Người chơi thật. Được xem kho và rút đồ qua tin nhắn. |
| **Leader** | 1, cộng 1 dự phòng | khu chính, đúng x/y | Nhận chat, nhận đồ, dọn túi, rao trạng thái. |
| **Clone** | khoảng 10 | khu phụ, đứng đâu cũng được | Chứa đồ (túi + rương); sang khu chính khi được điều. |

### Cài đặt kho (một kho = một bộ cài đặt)

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `MayChu` | — | Mọi acc trong kho phải cùng máy chủ; tool từ chối acc khác máy chủ. |
| `Map` | 22 (Làng Tone) | |
| `KhuChinh` | — | Khu của Leader, nơi giao nhận với người chơi. |
| `KhuPhu` | — | Khu của clone. Bắt buộc khác `KhuChinh`. |
| `LeaderX`, `LeaderY` | — | Có nút **"Lấy chỗ đang đứng"** (đã có ở NSOBAOTATL). |
| `Leader`, `LeaderDuPhong` | — | Chọn trong lưới acc. |
| `ChuKho` | rỗng | Danh sách tên nhân vật. |
| `CheDoNhan` | **TatCa** | `TatCa` \| `ChiChuKho` (D16). |
| `ChatVoiNguoiLa` | **tắt** | Tắt = không gửi tin riêng cho ai ngoài Chủ kho (D24). |
| `NguongNhan` | 12 | Túi Leader còn ít hơn số ô này thì từ chối nạp. |
| `ChoNguoi` | 120 s | Thời gian chờ tối đa mỗi bước khi đối phương là người. |
| `ChoNguoiLa` | 45 s | Như trên, cho người ngoài danh sách Chủ kho (khi đang ở chế độ `TatCa`). |
| `ChoBot` | 20 s | Như trên, khi đối phương là bot. Theo NSOCHIP Auto Sell. |
| `ChoCoMat` | 10 phút | Lệnh rút chờ người nhận có mặt ở khu chính tối đa bao lâu. |
| `RaoBat`, `RaoNhipGiay`, `RaoMau` | bật · 5 · `Kho do {dung}/{tong}` | §9.2 |
| `BaoCongDong` | bật | Sao sự kiện sang chat cộng đồng (D20). |
| `LogFile` | **bật** | Log đầy đủ theo ngày (§8.1). |
| `GiuLogNgay` | 0 | Số ngày giữ log; 0 = giữ hết. |
| `LogHexGiaoDich` | bật | Ghi hex mọi gói giao dịch (37/43–46/56–58) và gói túi đi kèm (8/9). |

**Map 22 = Làng Tone** (`NSOBAOTATL/src/.../UI/MapNames.cs:40`).
- Clone mới (taskId = 0) **đã vào được** map 22 (`NSOBAOTATL/docs/STATUS.md`: `No path 22->21`, tức nó đang đứng ở 22).
- Map 22 có NPC rương (template 5) và NPC đổi khu (template 13) hay không thì **phải đo** (M7, M15).

**Leader dự phòng:**
- Mọi bot chạy trong một tiến trình, và **mọi clone đều chuyển tiếp** tin nhắn của Chủ kho về bộ điều phối.
- Leader offline thì bộ điều phối cho acc dự phòng sang khu chính đứng đúng x/y. Dự phòng nhận vai Leader, báo ở chat cộng đồng và nhắn Chủ kho tên Leader mới.

## 5. Luồng NẠP (người chơi → Leader)

Người chơi tới khu chính, chỉ vào Leader rồi mời giao dịch bằng tay, hoặc gõ `gdvp` / `gd <id>`.

| Bước | Sự kiện / điều kiện | Leader làm gì |
|---|---|---|
| N1 | nhận **43** `{id}` | Đổi id ra tên bằng `OtherPlayers` cùng khu. **Không tìm thấy** → bỏ qua (giống client gốc) + log. **Không được nạp** (`CheDoNhan = ChiChuKho` và tên không thuộc Chủ kho) → gửi **56**, không nhắn gì. **Đang có phiên với người lạ mà Chủ kho mời** → huỷ phiên người lạ (**57**), rồi nhận Chủ kho: **Chủ kho luôn được ưu tiên**. **Đang có phiên khác** → 56. **Ô trống túi < `NguongNhan`** → 56. Nếu người mời là Chủ kho thì báo *"Đang dọn kho, thử lại sau ~N giây"* (§9.3); người lạ thì không nhắn gì (D24). |
| N2 | hợp lệ | Gửi **44** `{id}`. Chờ gói 37, tối đa 15 giây. |
| N3 | nhận **37** `{tên}` | Tên khác người vừa được nhận lời → **57**. |
| N4 | — | Gửi ngay **45** `{xu 0, n 0}` (khoá rỗng). |
| N5 | chờ **45** của đối phương | Chờ tối đa `ChoNguoi`, hoặc `ChoNguoiLa` nếu là người lạ. |
| N6 | nhận **45** `{xu, n, món…}` | **Kiểm tra `n ≤ ô trống thực tế`**, tính mỗi món cần một ô mới. Không đủ → **57**; nếu là Chủ kho thì báo *"Kho chỉ nhận thêm K món lúc này"*. Đủ → ghi lại danh sách món + xu sẽ nhận. |
| N7 | — | Chờ **1.500 ms**, rồi gửi **46**. |
| N8 | chờ **58** | Gói 8/9 về túi đi qua handler có sẵn. |
| N9 | nhận **58** `{xu mới}` | Cập nhật xu. Gửi **57** để dọn phiên. Ghi nhật ký. Báo *"Đã nhận N món + X xu từ <tên>"* ở chat cộng đồng, và qua chat riêng nếu người nạp là Chủ kho. Sau 2 giây **đối chiếu** túi trước/sau: lệch thì ghi `LECH` và đánh dấu cần đồng bộ lại. |
| — | nhận **57** bất kỳ lúc nào | Đóng phiên, ghi lý do. |

- **Leader không bao giờ đặt đồ khi đang nạp**, nên chế độ `TatCa` (mặc định) không làm mất đồ. Rủi ro là người lạ đổ rác làm đầy kho, hoặc treo phiên (§14 mục 8).
- **Tương thích `gdvp`:**
  - `gdvp` không có timeout. Kho từ chối thì nó vẫn mời lại mỗi 5 giây, người chơi phải tự dừng.
  - `gdvp` đổ **toàn bộ** đồ không khoá trong túi acc chính. Món muốn giữ thì cất rương trước, hoặc dùng `gd <id>`.

## 6. Dọn kho — Leader luôn giữ túi trống

Dọn kho **chỉ chạy khi Leader rảnh**: không có phiên giao dịch, và không có người nào mời trong 10 giây gần nhất. Phiên nạp của người chơi **luôn được ưu tiên** chen vào giữa hai lượt dọn.

```
Túi Leader ──(17: cất rương, không cần giao dịch)──► Rương Leader
Rương Leader ──(16: lấy ra ≤12 món)──► Túi Leader ──(giao dịch bot↔bot tại khu chính)──► Túi Clone
Túi Clone ──(về khu phụ; 17 khi ô trống < NguongNhan)──► Rương Clone
```

1. **Bước nhanh:** túi Leader có món → cất hết vào rương Leader.
2. **Bước nền:** rương Leader còn món → gọi **một** clone sang khu chính → Leader lấy ≤12 món ra túi → giao dịch sang clone → lặp lại tới khi clone gần đầy hoặc rương Leader hết → clone về khu phụ và tự cất rương.
   - Mỗi lượt Leader chỉ lấy ra tối đa 12 món, nên túi Leader luôn còn ít nhất `(số ô túi − 12)` ô cho người chơi.
3. **Chọn clone nhận:** ưu tiên clone **đang giữ cùng loại món có thể chồng**, sau đó tới clone **nhiều ô trống nhất** (túi + rương). Clone phải online và rảnh.
4. **Mỗi lúc chỉ 1 clone được gọi sang khu chính để dọn**, cho khu chính đỡ rối.
5. **Hết chỗ:** mọi clone đều đầy → trạng thái **KHO ĐẦY**. Từ chối nạp và báo (§9.3).
6. **Xu:** Leader giữ xu nhận được. Chưa dồn xu sang clone; trần xu mỗi nhân vật chưa biết (M16).

**Nếu phép đo M7 cho thấy rương không dùng được:**
- bỏ bước 1 và phần "tự cất rương";
- Leader giao thẳng từ túi sang clone;
- sức chứa chỉ còn bằng số ô túi;
- ngưỡng ở N1 là cơ chế duy nhất chặn người chơi nạp quá nhanh.

## 7. Luồng RÚT (clone → người nhận)

**Nguồn lệnh** — Chủ kho quyết định giao cho ai (D25):
- **Tool:** người nhận là **tên nhân vật bất kỳ**.
- **Chat riêng:** chỉ Chủ kho ra lệnh được. Người nhận mặc định là chính người gửi lệnh; muốn giao cho người khác thì thêm `cho <tên>` (§9.4).
- **Người nhận không phải Chủ kho** thì không nhận tin riêng nào (D24). Chủ kho ra lệnh nhận báo tiến độ thay.

| Bước | Việc |
|---|---|
| R1 | **Nhận lệnh** `(templateId, [cấp +], số lượng hoặc "hết", người nhận)` → vào hàng chờ, cấp số lệnh `#N`. |
| R2 | **Lập kế hoạch:** đọc sổ kho, chọn theo thứ tự ít clone nhất → ít lượt nhất → món trong túi trước món trong rương. Mỗi lượt ≤12 ô. Lấy một phần chồng thì đánh dấu cần tách. |
| R3 | **Chuẩn bị ngay tại khu phụ:** món trong rương → lấy ra (16). Lấy một phần chồng → tách (cmd 22 rồi −28/−85), sau đó so túi trước/sau để tìm ô chứa phần tách. |
| R4 | **Chờ người nhận có mặt ở khu chính.** Leader thấy người đó trong `OtherPlayers`. Chưa có → báo Chủ kho ra lệnh: *"Lệnh #12: 5 × <tên> cho <người nhận>. Tới Làng Tone khu K để nhận."* Quá `ChoCoMat` → huỷ lệnh và báo. |
| R5 | Clone **sang khu chính** bằng một lệnh đổi khu. Clone đã đứng ở khu phụ quá 10 giây thì đổi được **ngay** (D28). Sau đó báo *"<tên clone> sẽ mời <người nhận>"* → đi sát người nhận: \|dx\| ≤ 40, \|dy\| ≤ 30 (dưới ngưỡng client 60/40). |
| R6 | Gửi **43** `{id người nhận}`, mời lại mỗi 5 giây, tối đa `ChoNguoi`. |
| R7 | Nhận **37**, kiểm tra đúng tên → gửi **45** `{0, n, vị trí…}`. |
| R8 | **Chờ 45 của người nhận.** Chưa thấy 45 thì **không gửi 46**. Nếu người nhận đặt kèm đồ, coi như nạp: kiểm tra chỗ trống như N6. |
| R9 | Chờ **1.500 ms**, rồi gửi **46**. |
| R10 | Nhận **58**: xoá các ô đã giao khỏi túi local (server không gửi gói xoá ô, đo M2), gửi **57**, ghi nhật ký, cập nhật tiến độ. Còn thiếu → lượt tiếp hoặc clone khác. |
| R11 | Nhận **57** trước khi xong: tính là một lần hỏng. **Hỏng 2 lần liên tiếp** → tạm dừng lệnh và báo. |
| R12 | Xong → clone về khu phụ. Lúc này đã qua hơn 10 giây kể từ khi tới khu chính, vì một phiên giao dịch luôn dài hơn thế. Leader báo *"Xong lệnh #12: đã giao 5 × <tên> cho <người nhận>"*. Món đã lấy ra khỏi rương mà chưa giao được thì clone tự cất lại. |

- **Mỗi lúc chỉ một clone giao cho một người nhận.** Khu chính chỉ có Leader, người nhận và tối đa một clone đang giao; nếu Leader đang dọn kho thì thêm một clone dọn.
- **Chưa biết** server làm gì khi túi người nhận đầy (M4). Trong lúc chưa đo, mỗi lượt chỉ giao **đúng số món đã xin**.

## 8. Sổ kho

- **Nguồn dữ liệu:**
  - túi của từng acc: gói đăng nhập, sub 115 định kỳ, các gói 8/9/7/10/18;
  - rương: gói 31, **chỉ có khi đã mở**, nên lưu kèm thời điểm đọc.
- **Khoá gộp:** `(templateId, upgrade, isExpires)`. Ghi theo **nội dung**, không giữ tham chiếu object (sub 115 dựng lại object khoảng 20 giây một lần — `NSOLITEPRO/docs/reference/SERVER_FACTS.md:127`).
- **Món khoá** vẫn có thể nằm trong túi acc (có từ trước). Chúng hiện riêng với cờ *"khoá — không giao được"* và **không tính** vào hàng rút được.
- **Mỗi dòng hiện:** ID, tên tiếng Việt, cấp +, có hạn hay không, tổng số lượng, số ô, phân bố theo từng acc (túi / rương).
- **Sức chứa** — số dùng cho câu rao "70/360":
  - `{tong}` = Σ **clone** (số ô túi + số ô rương).
  - `{dung}` = số ô đang có đồ trong phần trên.
  - `{trong}` = `{tong} − {dung}`.
  - **Không tính Leader**, vì Leader chỉ là cửa nhận (D3).
  - Clone chưa từng đọc rương → chỉ tính phần túi, và câu rao thêm dấu `~` để báo con số chưa đủ.
- **Xu:** tổng xu của Leader và các clone, hiện trên tool.
- **Giới hạn giai đoạn 1:** không hiện dòng thuộc tính của món (gói 42 không xử lý món trong rương). Món có hạn chỉ hiện cảnh báo.
- **Lưu đĩa:**
  - `Data/kho.txt`, pipe-delimited; clone offline vẫn thấy hàng, kèm *"lần cuối thấy lúc…"*;
  - `Data/tenmon.txt`: cache tên món theo phiên bản dữ liệu.

### 8.1 Log đầy đủ theo ngày (D26)

**Vì sao không dùng lại `FileLog` của lõi:** `Logging/FileLog.cs` **mặc định tắt**, chỉ ghi **một file**, và khi vượt 5 MB thì chuyển sang `OldLog` — file cũ trước đó bị ghi đè. Tức là log cũ **bị mất**, trái với yêu cầu "đầy đủ".

| File | Nội dung | Một dòng gồm |
|---|---|---|
| `Logs/yyyy-MM-dd/app.log` | **Mọi thứ**: đăng nhập / rớt / đăng nhập lại, đổi khu, rương, tách chồng, quyết định của bộ điều phối, lỗi | `HH:mm:ss.fff [acc] [nhóm] nội dung` |
| `Logs/yyyy-MM-dd/giaodich.csv` | Mỗi **phiên** giao dịch một dòng | thời gian bắt đầu/kết thúc, vai (nạp / rút / dọn), acc bot, đối tác (tên + id), là Chủ kho hay không, món (tpl, +, hạn, số lượng), xu, kết quả (XONG / HUY + lý do), số lệnh, lệch hay không |
| `Logs/yyyy-MM-dd/chat.log` | **Mọi tin chat** vào/ra: riêng và cộng đồng, gồm cả tin rao và tin bị bỏ qua của người lạ | `HH:mm:ss [acc] [RIENG/CONGDONG] [VAO/RA] <người> nội dung` |
| `Logs/yyyy-MM-dd/lenh.csv` | Mỗi **lệnh rút** một dòng, ghi khi lệnh kết thúc | số lệnh, nguồn (tool / chat + tên), món, số lượng xin / đã giao, người nhận, các clone đã giao, thời gian, kết quả |
| `Logs/yyyy-MM-dd/hex.log` | Hex các gói giao dịch và gói túi đi kèm (khi `LogHexGiaoDich` bật) | `HH:mm:ss.fff [acc] S→C cmd len hex` |

- **Sang ngày mới** (theo giờ máy) thì mở thư mục mới. **Không xoay theo dung lượng, không ghi đè.** Một file vượt 50 MB thì mở phần tiếp theo `app_2.log`, `app_3.log`…
- **Ghi qua hàng đợi, gom một lần mỗi giây**, giống `Logger` hiện có. Lỗi ghi đĩa không được làm chết app.
- **`GiuLogNgay`** (mặc định 0 = giữ hết): lớn hơn 0 thì lúc khởi động xoá các thư mục ngày cũ hơn số ngày đó.
- **Không bao giờ ghi mật khẩu** vào log.
- **Tool có nút "Mở thư mục log hôm nay"**.
- **Log trong file ghi không dấu**, theo quy ước Auto của họ NSOLITEPRO. **Riêng tên nhân vật, tên món và nội dung chat giữ nguyên** như server gửi, ghi file bằng UTF-8.

## 9. Chat

### 9.1 Luật chung (mọi acc, mọi kênh)

- **Mọi tin gửi đi đều bắt đầu bằng `@NNN `** (bộ đếm 000–999 xoay vòng, dùng chung toàn tiến trình, theo khuôn `KichYenCaller.TemGoi`).
- **Khi đọc tin đến,** bỏ tem ở đầu trước khi xử lý (`BoTem`).
- **Riêng tin rao định kỳ:** tem còn giúp câu rao **không bao giờ trùng nhau**, kể cả khi số liệu kho không đổi.

### 9.2 Leader tự rao (chat cộng đồng, gói −23)

- **Chỉ Leader rao.** Bật/tắt bằng `RaoBat`, nhịp `RaoNhipGiay` (mặc định **5 giây**).
- **Mẫu câu tự đặt** (`RaoMau`), với các biến:

| Biến | Nghĩa |
|---|---|
| `{dung}` `{tong}` `{trong}` | sức chứa (§8) |
| `{online}` `{tongacc}` | số clone đang online / tổng số clone |
| `{lenh}` | số lệnh rút đang chờ |
| `{leader}` | tên Leader hiện tại |

- Ví dụ `Kho do {dung}/{tong}` → `@417 Kho do 70/360`.
- **Bằng chứng nhịp 5 giây dùng được trên server chính:** NSOCHIP "Auto Sell" rao hàng ở chat map đúng 5 giây/lần (`NSOCHIP/src_frozen/en.java:481-533`).
- **Chưa đo:** server chính có chặn hoặc khoá chat cộng đồng khi rao dày không (M14).

### 9.3 Leader báo sự kiện (D20)

- **Mỗi sự kiện gửi hai nơi:**
  - **chat riêng** cho **Chủ kho** liên quan: người ra lệnh, hoặc Chủ kho vừa nạp. Không gửi cho người lạ, trừ khi bật `ChatVoiNguoiLa`;
  - **chat cộng đồng**, nếu `BaoCongDong` bật.
- **Sự kiện:** nhận lệnh · chờ người nhận · clone sẽ mời · xong lệnh · nạp xong · từ chối nạp (đầy / đang dọn) · KHO ĐẦY · đổi Leader.
- **Hàng đợi chat cộng đồng** dùng chung với tin rao:
  - **sự kiện chen trước** tin rao định kỳ;
  - giữa hai tin cộng đồng luôn cách ít nhất `RaoNhipGiay`.
- **Chat riêng:** mỗi acc gửi tối đa 1 tin mỗi 3 giây (sàn của `KichYenCaller`). Mỗi người nhận được tối đa 1 tin *"đang dọn / đầy"* mỗi 30 giây.
- **Độ dài tin:** giữ **dưới 100 ký tự**; độ dài tối đa trên server chính chưa đo (M10).

### 9.4 Lệnh chat riêng — CHỈ Chủ kho (D17)

- **Gửi cho ai:** gửi cho Leader. Gửi cho clone bất kỳ cũng được, vì mọi bot đều chuyển tiếp.
- **Có thể nhiều Chủ kho.** Mỗi Chủ kho thấy toàn bộ kho và huỷ được lệnh của chính mình.
- **Người ngoài danh sách:** bị bỏ qua, không trả lời (D24).
- **Cách đọc lệnh:** tách bằng dấu cách, không phân biệt dấu và hoa thường.

| Lệnh | Việc | Ví dụ trả lời |
|---|---|---|
| `kho` | tóm tắt | `@102 Kho: 10/10 online, 70/360 o, 0 lenh cho` |
| `tim <từ khoá>` | tìm theo tên, tối đa 5 kết quả | `@103 457 Da cap 5 x120 · 458 Da cap 6 x33` |
| `co <id>` | kho có bao nhiêu một món | `@104 457 Da cap 5: 120 (3 clone)` |
| `lay <id> [sl\|het] [+cấp] [cho <tên>]` | tạo lệnh rút; không có `cho` thì giao cho chính người ra lệnh | `@105 Lenh #12: 5 x Da cap 5 cho Abc. Toi Lang Tone khu 3` |
| `huy [#số]` | huỷ lệnh của chính mình | `@106 Da huy lenh #12` |
| `nap` | hỏi Leader đang nhận được bao nhiêu món | `@107 San sang nhan 36 mon` |

## 10. Giao diện — ⚠ BỐ CỤC PHẢI TRÌNH USER DUYỆT TRƯỚC KHI CODE

Đây là luật chung của NSOBAOTATL và NSOLITEPRO. Phần dưới chỉ là danh sách **chức năng** cần có, chưa phải bố cục.

- **Lưới acc** (VirtualMode của NSOBAOTATL), thêm các cột:
  - *Vai*: Leader / Dự phòng / Clone;
  - *Khu*;
  - *Túi trống*, *Rương trống*;
  - *Trạng thái kho*: Sẵn sàng / Đang nhận / Đang giao / Đang dọn / Đang sang khu / ĐẦY / Offline;
  - *Phiên GD*: đối phương + bước hiện tại, ví dụ `Chờ 45 · 34s`.
- **Tổng kho:** bảng món + ô tìm + dòng sức chứa `70/360` + tổng xu.
- **Điều phối** (D21): chọn món → số lượng → người nhận (gõ tên bất kỳ, hoặc chọn Chủ kho) → **Giao** → vào hàng chờ.
- **Hàng chờ:** số lệnh, món, số lượng, người nhận, nguồn (tool / chat + tên), tiến độ, clone đang giao, nút Huỷ.
- **Nhật ký:** nhập / xuất / dọn, lọc theo ngày, theo acc, theo đối tác (đọc từ `Logs/<ngày>/giaodich.csv`). Có nút mở thư mục log.
- **Cài đặt:** toàn bộ bảng ở §4.

## 11. Kiến trúc

```
MainForm ──view──> FleetManager ──owns──> N × NsoClient
                        │                      │
                        │                 KhoMode (thay StandMode)
                        │                   ├─ TradeEngine   (1 phiên, 2 vai)
                        │                   ├─ RuongRunner   (mở rương, 16/17)
                        │                   └─ Heartbeat 60 s + KeepAlive (CÓ SẴN, D23)
                        │
                        └──> KhoDieuPhoi (MỘT cho mỗi tiến trình)
                               ├─ SoKho        (túi + rương, sức chứa, lưu đĩa)
                               ├─ HangCho      (lệnh rút từ tool + chat)
                               ├─ BoChon       (clone nhận khi dọn / clone giao khi rút)
                               ├─ KhoLenh      (đọc lệnh chat, danh sách Chủ kho, CheDoNhan)
                               ├─ KenhChat     (tem @NNN, hàng đợi cộng đồng, rao 5 s, sàn chat riêng)
                               ├─ Leader       (chỉ định, dự phòng)
                               └─ NhatKy       (log theo ngày §8.1: app / giaodich / chat / lenh / hex)

Gửi:   TradeService — 43, 44, 45, 46, 56, 57                          (MỚI)
Nhận:  TradeHandler — 37, 43, 45, 46, 57, 58                          (MỚI + case trong MessageRouter)
Trạng thái: TradeState trong GameStateManager (ghi từ luồng mạng → phải khoá)   (MỚI)
Có sẵn: ChatService.SendPublicChat (−23), SendPrivateChat (−22); ItemService 16/17/22/−85
```

- **`KhoMode.Tick()`** xét theo thứ tự ưu tiên:
  1. đang có phiên giao dịch → `TradeEngine.Tick()`;
  2. có việc bộ điều phối giao → làm việc đó: sang khu, giao, nhận, lấy/cất rương, tách chồng;
  3. Leader rảnh → dọn kho (§6);
  4. không có việc → về đúng khu. Leader đứng đúng x/y; clone đứng đâu cũng được.
- **`TradeEngine`** cho phép gọi từ ngoài như sau:

  ```csharp
  Start(vai, tenDoiPhuong, idDoiPhuong, viTriMon[], xu, kiemHangDoiPhuong, choToiDa)
  ```

  - `kiemHangDoiPhuong(xu, món[]) → nhận / từ chối` là **móc cho auto mua bán** (D8). Hiện chỉ kiểm tra chỗ trống.
  - Máy trạng thái:

  ```
  RANH → (giao) MOI → CHO_37          (nhận) CHO_37
  CHO_37 → DA_KHOA(45) → CHO_45_DP → KIEM → CHO_TRUOC_46 → DA_46 → CHO_58 → XONG(57)
  bất kỳ bước nào: 57 đến / quá hạn / sai đối tác / bị Chủ kho chen → HUY(57)
  ```
- **Tự đánh chống rớt mạng (D23):** giữ nguyên `AutoModeBase.Heartbeat` — gói 61 nhắm vào charId của chính mình, 60 giây/lần, *"không gây sát thương, không bật cờ PK"* (`NSOBAOTATL/src/.../Auto/AutoModeBase.cs:104-120`).
  - **Tạm dừng khi đang trong phiên giao dịch** (thận trọng: chưa biết server xử lý thao tác đánh giữa phiên thế nào). Phiên dài nhất 2–3 phút, và gói 93 của `TickDocIm` vẫn giữ luồng đọc.
  - Tự đánh ở **làng** có tác dụng không thì phải đo (M13).
- **Đổi khu:** `Navigator.DoZoneChange` (đi tới NPC 13 rồi gửi cmd 28, hoặc dùng Khả di lệnh).
  - **Luật game (D28):** hồi chiêu 10 giây, **đếm từ lúc đặt chân tới khu mới**. Clone đứng yên ở khu phụ thì đổi sang khu chính được ngay.
  - ⚠ **Mốc của bot lệch với luật game:** bot đang lấy mốc tại lúc **gửi lệnh** (`Navigator.cs:1336`, `_lastZoneChange = DateTime.UtcNow` sau `SafeSleep(100)`), tức là **sớm hơn** lúc tới nơi. Chuyến về ngay sau một chuyến đi có thể bị server từ chối. Khi code, lấy mốc tại **lúc nhận thông tin khu mới** (MAP_INFO), và đo lại ở M15.
  - **Bộ chọn ưu tiên clone đã hết hồi chiêu đổi khu.** Clone vừa quay về khu phụ chưa đủ 10 giây thì để lượt sau.
- **Một kho = một tiến trình.** Không chia kho ra nhiều cửa sổ bằng `--list=`.
- **Luật đọc byte nâng cấp trong gói 45** (server gửi) khớp luật của gói 8 và 31 (`IsTypeBody || IsTypeNgocKham`). Luật này **khác** sub −127/115 (`HasUpgrade`). Đừng gộp.

## 12. Nguồn code

- **Khung:** tách từ **NSOBAOTATL** (lưới 1.800 acc, proxy, tự đăng nhập lại, đổi khu, "Lấy chỗ đang đứng", tự đánh 60 giây).
  - **Mốc tách:** NSOBAOTATL commit **`7715bcf`** (2026-09-16, `github.com/tungnv27/NSOBAOTATL`), mà bản thân commit đó lại tách từ NSOLITEPRO `79d9d86`. Khi tách, ghi mốc này vào `docs/NGUON_GOC.md` của NSOKHODO, theo đúng khuôn của NSOBAOTATL.
- **Chép thêm từ NSOLITEPRO:**
  - khuôn nhận chat của `KichYenCaller` (`TemGoi`, `BoTem`, `TryAccept`, danh sách trắng);
  - mở rương: `DanhVongMode.MoUiRuong`, `DapDoRunner.CatRuong`;
  - `TrainMode.FreeBagSlots`;
  - `UI/InventoryText.Name`.
- **Có sẵn trong lõi, chỉ cần dùng:**
  - gói 16/17; cmd 22 + −28/−85;
  - `ChatService` (−22, −23);
  - `ItemTemplateStore`, `OtherPlayers`;
  - `Navigator.CharBurstMove` / `DoZoneChange`;
  - `KeepAliveController`, `AutoModeBase.Heartbeat`.
- **Bỏ khỏi khung NSOBAOTATL:** `BaoTaTl`, phép đo T1. **Giữ `BinhMauRunner`** (vô hại).
- **`accounts.txt` giữ nguyên 23 cột**, để dùng qua lại được giữa ba tool.
- **Chi phí đã biết:** thêm **bản lõi đóng băng thứ ba**. Trong 61 commit gần đây của NSOLITEPRO có 16 commit sửa `Core/` hoặc `Protocol/` (`NSOBAOTATL/docs/NGUON_GOC.md`). Tách thư viện lõi dùng chung là việc riêng, không làm ở đây.

## 13. Lộ trình

### P0 — Đo trên server chính (làm TRƯỚC, bản debug có nút bấm tay + ghi hex; dùng đồ rẻ)

| Mã | Đo gì | Phục vụ |
|---|---|---|
| M1 | Một phiên người chơi nạp 1 món cho bot: hex + thứ tự 37/45/46/58/8/9 | §5 |
| M2 | Bot giao 1 món: server có gửi gói xoá ô cho bên giao không | R10 |
| M3 | Đồ có hạn dùng giao dịch được không | §5, §7 |
| M4 | Bên nhận thiếu ô (giao 2 món cho acc còn 1 ô): server huỷ phiên hay mất đồ | N6, R8 |
| M5 | Khoảng cách tối đa để mời: 40 / 60 / 100 / 150 px | R5 |
| M6 | Mời liên tục có bị chặn không, phải chờ bao lâu | R6 |
| M7 | Rương ở map 22: có NPC 5 không; gói 29 dài 3 hay 4 byte; số ô; có cần đứng gần NPC không | §6 |
| M8 | Tách chồng: chỉ −85, hay 22 + −85 | R3 |
| M9 | Gửi 46 sau 1,5 giây có được không | N7, R9 |
| M10 | Chat riêng: độ dài tối đa, có cần kết bạn không | §9 |
| M11 | Cấp độ tối thiểu để giao dịch (acc cấp 1–10) | clone mới |
| M12 | Khoảng 12 acc cùng một khu có bị đẩy sang khu khác không | §4 |
| M13 | Tự đánh (gói 61, charId của mình) ở làng: server phản hồi gì; acc có bị rớt sau 2 giờ đứng im không | D23 |
| M14 | Chat cộng đồng 5 giây/lần trong 1 giờ: có bị chặn hoặc khoá không | §9.2 |
| M15 | Đổi khu ở map 22: có NPC 13 không; thời gian chờ thực tế | §7, §6 |
| M16 | Trần xu mỗi nhân vật; nhận xu vượt trần thì sao | §6 bước 6 |

Kết quả ghi vào `GIAO_DICH.md` §9, kèm hex.

### P1 — Nạp vào Leader + sổ kho (chỉ túi) + chat

- **Làm:** luồng §5; cài đặt §4; `CheDoNhan`; sổ kho phần túi; §9.1–9.3; cột lưới; **log theo ngày §8.1**.
- **Xong khi:**
  - Leader ở khu chính đúng x/y, 3 clone ở khu phụ, chạy **24 giờ** không rớt hẳn;
  - nạp bằng tay **và** bằng `gdvp` tổng 50 món + xu, qua ≥5 lượt; sổ kho khớp túi thật 100%;
  - `TatCa` (mặc định): người lạ nạp được, **không nhận tin riêng nào**, và Chủ kho chen ngang được phiên người lạ. `ChiChuKho`: người lạ bị từ chối;
  - câu rao đổi số liệu đúng sau mỗi lượt nạp; mọi tin gửi đi đều có tem;
  - qua nửa đêm thì log sang thư mục ngày mới; mọi phiên đều có dòng trong `giaodich.csv`; `chat.log` có đủ tin vào và ra.

### P2 — Rút (tool + chat) + điều phối + tách chồng

- **Làm:** luồng §7; §9.4; hàng chờ; điều phối trên tool với người nhận bất kỳ.
- **Xong khi** rút được: 1 món, cả chồng, một phần chồng, món nằm rải ở 3 clone, giao cho một người **không** phải Chủ kho (một lần ra lệnh từ tool, một lần bằng `lay … cho <tên>`). Mỗi trường hợp chạy được với cả người bấm tay lẫn `nhan`. Hai Chủ kho ra lệnh cùng lúc thì các lệnh được xếp hàng đúng thứ tự.

### P3 — Dọn kho + rương + sức chứa đầy đủ

- **Làm:** §6 đầy đủ; sổ kho gồm rương; `{tong}` tính cả rương.
- **Xong khi:** nạp liên tục 100 món mà không lượt nào bị từ chối quá 1 phút; sau khi dọn, túi Leader về trống; sổ kho khớp.

### P4 — Hoàn thiện

- Leader dự phòng tự thay; lệnh `tim`; cache tên món; cảnh báo đồ có hạn; trạng thái KHO ĐẦY.

### P5 — Auto mua bán (CHƯA ƯU TIÊN)

- Bảng giá; lệnh mua/bán; rao bán hàng; chuyển xu; kiểm tra hàng và xu của khách qua móc `kiemHangDoiPhuong`; rút xu.
- Mẫu: NSOCHIP `en.java` (`GIAO_DICH.md` §7.2).

## 14. Rủi ro

| # | Rủi ro | Mức | Bằng chứng | Cách chặn |
|---|---|---|---|---|
| 1 | **Đồ khoá không vào kho được.** "Vứt rác tùm lum" chỉ đúng với đồ không khoá. | Cao — giới hạn cứng | Client chặn (`ONLY_NO_LOCK`); `gdvp` lọc `!h` | Không có cách vượt. |
| 2 | `gdvp` đổ **mọi** món không khoá của acc chính. | Vừa | `Class_aj.b()` | Cất rương trước, hoặc dùng `gd <id>`. |
| 3 | Bên nhận thiếu ô lúc chốt → có thể **mất đồ**. | Cao cho tới M4 | Chưa đo | Luôn kiểm tra trước khi gửi 46; mỗi món tính một ô mới. |
| 4 | Túi người nhận đầy khi rút: bot không biết túi người đó còn bao nhiêu ô. | Vừa | Không có gói nào cho biết | Giao đúng số món đã xin; M4. |
| 5 | Rương chưa từng chạy thật. | Cao cho §6 | `NSOLITEPRO/docs/WORKLOG.md:6665` | M7 trước; có phương án lùi. |
| 6 | Thêm bản lõi đóng băng thứ ba. | Vừa, lâu dài | 16/61 commit sửa lõi | `NGUON_GOC.md`; khi hỏng, việc đầu tiên là so lõi. |
| 7 | Một kho chỉ chạy trong một tiến trình. | Thấp | §11 | Ghi trong README. |
| 8 | **Chế độ `TatCa` là MẶC ĐỊNH** (user chốt sau khi đã được cảnh báo): người lạ đổ rác làm đầy kho, hoặc mở phiên rồi treo để giữ Leader bận. | Vừa | D16 | `ChoNguoiLa` = 45 giây; Chủ kho chen ngang được; KHO ĐẦY thì từ chối; `giaodich.csv` ghi tên người nạp để truy lại; chuyển sang `ChiChuKho` chỉ bằng một ô cài đặt. |
| 9 | Bị khoá chat vì rao 5 giây/lần. | Vừa | Server chính từng khoá chat riêng khi lặp; chat cộng đồng chưa đo | Tem `@NNN`; M14; nhịp rao cài được. |
| 10 | **Chat cộng đồng công khai tình trạng kho** cho mọi người trong khu. | Thấp | D19/D20 do user chọn | Tắt được bằng `RaoBat` / `BaoCongDong`. |
| 11 | Mốc hồi chiêu đổi khu của bot lấy lúc **gửi lệnh**, còn game đếm từ lúc **tới nơi** → chuyến về liền ngay sau chuyến đi có thể bị từ chối. | Thấp | `Navigator.cs:1336`; D28 | Lấy mốc lúc nhận MAP_INFO; bộ chọn ưu tiên clone đã hết hồi chiêu; M15. |
| 12 | Tự đánh ở làng có thể không tạo lưu lượng như mong đợi. | Vừa | Chưa đo ở làng | M13; `TickDocIm` (gói 93) vẫn giữ luồng đọc. |
| 13 | Sổ kho lệch sau sub 115. | Vừa | `SERVER_FACTS.md:127` | Ghi theo nội dung; đối chiếu sau mỗi phiên. |
| 14 | **Log giữ hết** (`GiuLogNgay = 0`) và rao 5 giây/lần → đĩa VPS đầy dần. Dung lượng mỗi ngày **chưa đo**. | Thấp → Vừa | D26 | Sau 1 ngày chạy P1 thì đo dung lượng thư mục ngày; nếu lớn thì đề xuất user đặt `GiuLogNgay`. `LogHexGiaoDich` tắt được. |

## 15. Câu hỏi còn mở

Không còn câu nào chặn việc bắt đầu P0.

**Đã xử lý:** NSOBAOTATL đã commit `7715bcf` (nhãn "CHUA TEST") và push lên `github.com/tungnv27/NSOBAOTATL`. Spec này nằm ở `github.com/tungnv27/NSOKHODO`.

**Đã trả lời (2026-09-16, vòng 3):** "70/360" = ô đã dùng (D27) · chat cộng đồng = chat khu (D27) · `CheDoNhan` mặc định `TatCa` (D16) · bot chỉ chat riêng với Chủ kho, có thể nhiều chủ (D24) · giao cho ai do Chủ kho quyết (D25) · log đầy đủ theo ngày (D26) · hồi chiêu đổi khu đếm từ lúc tới nơi (D28).
