# NSOKHODO — Kho đồ chung cho Ninja School Online

> **Trạng thái: ✅ ĐÃ CHỐT — SPEC v1.1 (2026-09-16, 7 vòng với user; v1.1 = dùng trọn .NET 4.5.2, D49). CHƯA CODE.**
> Sửa spec sau mốc này thì ghi thêm một vòng ở §1 và một dòng quyết định ở §2, **không sửa ngầm**.
> **Test tay (P0a) XONG:** T0 cho thấy **giao dịch KHÔNG khoá đồ** → thiết kế đi tiếp. Kết quả đầy đủ: `TEST_TAY.md`.
> **Việc tiếp theo:** P0b — dựng khung project và đo những gì tay không đo được (§13). **Chỉ bắt đầu khi user ra lệnh.**
> **Hợp đồng giao thức giao dịch** (gói tin, nguồn, mức chắc chắn): `GIAO_DICH.md`. Đọc file đó trước khi code.

---

## 0. Tóm tắt

Bot **headless** giữ khoảng 10 acc clone online 24/7 ở **Làng Tone (map 22)**.

- **Leader** đứng ở **khu chính**, tại toạ độ user cài; không cài thì đứng đâu cũng được. Đây là acc **duy nhất** người chơi nhắn tin và nạp đồ vào.
- **Clone** đứng ở **khu phụ** (cho đỡ rối). Chỉ khi cần giao nhận chúng mới sang khu chính.
- Leader nhận đồ rồi tự dọn đi chỗ khác: rương của nó, rồi các clone theo **kệ hàng**.
- Tool biết **tổng kho có gì, nằm ở đâu, còn bao nhiêu chỗ**.
- Khi có lệnh rút, tool **giữ chỗ** các món, chọn clone đang giữ món. Clone đó sang khu chính, tách chồng nếu cần, rồi **giao thẳng** cho người nhận.
- Leader báo tình hình qua **chat riêng** và **chat cộng đồng**, và tự rao trạng thái kho theo kiểu **rao thông minh**.
- **Mọi tin chat gửi đi đều không dấu và có tem `@NNN` ở đầu.**
- **Bot không bao giờ dùng hay mặc món nào**, vì đồ chỉ bị khoá khi được đem ra dùng.

## 1. Ý tưởng gốc và các vòng chốt (user, nguyên văn)

**Vòng 1:**
> Mình tạo khoảng 10 con clone đi. Làm kho chung. Xong mình sẽ vứt rác tùm lum gì cũng vứt vào đó.
> Mấy con đó sẽ onl 24/7. NV của tool là: Tổng kho đang có những cái gì. Khi nào mình muốn lấy item,
> tool sẽ phải tự phân phối acc giao cho mình, thay vì phải thủ công lọ mọ phân chia vào từng acc.

> Cần 1 con acc làm key để chat với người dùng. Con key đó sẽ làm nhiệm vụ leader. Nhận item, nó
> cất đâu kệ nó. Điều clone giao ai kệ nó. Người chơi chỉ chát riêng với mỗi con đó thôi.

**Vòng 2:**
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

**Vòng 4 (MINH đề xuất 11 ý tưởng, user chọn nhóm A + B + C):**
test tay trước khi code · rao thông minh · kệ hàng theo loại · thùng rác tự dọn · giữ chỗ khi nhận
lệnh · gói rút · theo dõi món + báo cáo ngày · tự dùng túi vải.
**Không chọn (nhóm D — đừng đề xuất lại nếu user chưa hỏi):** nhiều kho trên một máy, kho bang hội
có phân quyền, báo qua Telegram.

**Vòng 5 (kết quả test tay của user):**
> T0 chỉ khóa khi sử dụng. T3: ngay khi A khoá vượt ô trống, server đóng giao dịch ngay, có popup
> (lúc có lúc không) kiểu "đối phương không đủ hành trang". T2 có gd được đồ có hạn. T1 lv1 giao
> dịch được. T4 mời cố định 31s, nhưng nếu đồng ý thì có thể gd lại luôn không mất thời gian chờ.
> T5 "đối phương đang có giao dịch khác". T6 có. T7 có. T8 tìm theo chức năng thủ khố trên các phiên
> bản; đi xa không cất được, phải sát NPC. T9 có NPC id 13. T12 không bán được gì cả — cần thì gd vào
> clone, tôi tự dọn con clone đó. T13 không tự mở rộng. T14 tối đa giữ được 2 tỷ xu. T10 có nhận
> được, nhưng phải gửi tiếng Việt không dấu. T11 nếu thêm @[000-999] thì không bị.

**Vòng 6:**
> Leader tôi muốn cài được tọa độ đứng. Nếu không cài thì đứng đâu thì đứng. T13 đúng. T8 đúng.

**Vòng 7 (sau khi chốt v1):**
> Dùng 4.5.2 đi.

## 2. Quyết định đã chốt

| # | Quyết định | Nguồn |
|---|---|---|
| D1 | **Chỉ chuyển đồ bằng GIAO DỊCH** (gói 43–58). Không dùng vứt–nhặt, gian hàng hay gia tộc. | MINH đề xuất. Đồ vứt ra đất không có chủ sở hữu (`GIAO_DICH.md` §8). |
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
| D15 | **Kho nhận hết** mọi thứ giao dịch được: **mọi món không khoá và cả xu**. | user |
| D16 | **Chế độ nhận** chỉnh được trên tool: **Chỉ Chủ kho** hoặc **Tất cả**. **Mặc định: Tất cả.** | user — giữ nguyên sau khi đã được cảnh báo (§14 mục 8) |
| D17 | **Xem kho và rút đồ qua tin nhắn: chỉ Chủ kho.** Người ngoài không được, kể cả khi đang bật chế độ Tất cả. | user |
| D18 | **Mọi tin gửi cho người chơi khác, riêng hay cộng đồng, đều bắt đầu bằng `@NNN`** (000–999). | user. T11 xác nhận: có tem thì không bị chặn. |
| D19 | **Leader tự rao ở chat cộng đồng**, nội dung tự đặt, báo tình trạng kho. **Chỉ Leader rao.** Nhịp: xem D31. | user |
| D20 | **Leader báo sự kiện qua chat riêng VÀ chat cộng đồng.** | user |
| D21 | **Tool điều phối được:** giao món gì, bao nhiêu, cho ai; xem trạng thái từng acc và từng phiên. | user |
| D22 | **Chỉ giao dịch món không khoá. Món bị gộp chồng mà chỉ lấy một phần thì tách ra trước.** | user + client gốc |
| D23 | **Mọi acc tự đánh chính mình để chống rớt mạng**, giống NSOLITEPRO. | user. Đã có sẵn: `AutoModeBase.Heartbeat` (gói 61, 60 giây/lần) + `KeepAliveController.TickDocIm` (gói 93). |
| D24 | **Bot chỉ chat riêng với Chủ kho** (mặc định). **Có thể có nhiều Chủ kho.** Người lạ nạp đồ được (D16), nhưng không nhận tin riêng nào. | user |
| D25 | **Giao cho ai là do Chủ kho quyết**, trên tool hoặc qua lệnh chat. Người nhận có thể là **bất kỳ tên nào**. | user |
| D26 | **Log đầy đủ, mỗi ngày một thư mục**, không ghi đè (§8.1). | user |
| D27 | Câu rao `70/360` = **ô đã dùng / tổng ô**. "Chat cộng đồng" = **chat khu** (gói −23). | user |
| D28 | **Hồi chiêu đổi khu 10 giây, tính từ lúc đặt chân tới khu mới.** | user (luật game) |
| D29 | **Chạy trên Windows Server 2012: C# / .NET Framework 4.5.2 (`net452`) / WinForms** (§3.1). | user + bằng chứng NSOLITEPRO đã chạy "pass" trên VPS (`NSOLITEPRO/docs/WORKLOG.md:2020-2033`) |

### Quyết định vòng 4 (ý tưởng MINH, user chọn)

| # | Quyết định | Chi tiết |
|---|---|---|
| D30 | **Test tay trước khi code** | `TEST_TAY.md` — **đã xong 2026-09-16** |
| D31 | **Rao thông minh** là mặc định; rao đều 5 giây vẫn chọn được | §9.2 |
| D32 | **Kệ hàng theo loại** | §6.1 |
| D33 | ~~Thùng rác tự bán / vứt~~ → **thay bằng D43** | T12: không bán được gì |
| D34 | **Giữ chỗ khi nhận lệnh** | §7, §8 |
| D35 | **Gói rút** | §7, §9.4 |
| D36 | **Theo dõi món + báo cáo ngày** | §9.3, §9.4 |
| D37 | ~~Tự dùng túi vải~~ → **HUỶ (D44)** | T13 |

### Quyết định vòng 5 (từ kết quả test tay)

| # | Kết quả test | Quyết định |
|---|---|---|
| D38 | **T0:** đồ **chỉ bị khoá khi đem ra dùng**; giao dịch không khoá. | Kho khả thi. **Bot không bao giờ dùng hay mặc món nào** → **bỏ `BinhMauRunner`**; `KhoMode` chặn mọi lệnh dùng món (gói 11) và mặc đồ. |
| D39 | **T3:** bên giao khoá **vượt số ô trống** bên nhận → **server đóng phiên ngay**, không mất đồ; lúc có lúc không hiện popup kiểu "đối phương không đủ hành trang". | Bot **vẫn tự kiểm** trước khi đồng ý, để báo đúng lý do. Khi mình là bên giao mà phiên bị đóng ngay sau lúc khoá → hiểu là **người nhận thiếu ô**, tạm dừng lệnh và báo (R11). |
| D40 | **T4:** lời mời bị khoá **31 giây**; nhưng phiên được đồng ý xong thì **mời lại được ngay**. | Mời lại cách **31 giây** (`MoiLaiGiay`). Rút nhiều lượt cho cùng một người thì mời lượt sau ngay khi lượt trước xong. |
| D41 | **T5:** mời người đang giao dịch → server báo **"đối phương đang có giao dịch khác"**. | Chủ kho **không chen ngang được bằng lời mời** khi Leader đang bận. Phải chen bằng **chat `nap`** (§5, §9.4). Clone mời người nhận đang bận → chờ 31 giây rồi mời lại. |
| D42 | **T8:** rương là NPC **"Thủ khố"**, phải đứng **sát NPC** mới cất/lấy được. MODGAME: NPC template **5**, đứng cách ≤ **22 px**, rồi mở bằng `-30 {-103, 4}`, **không qua menu NPC** (`MODGAME/src/GameScr.java:14356-14362`, `:13026-13031`). | Cần cất / lấy rương thì **Leader tự đi tới Thủ khố**, xong thì quay về chỗ đứng (D48). Clone dùng Thủ khố ở khu phụ. Không cần gói menu 29. |
| D49 | **Vòng 7 (user):** *"Dùng 4.5.2 đi."* | **Dùng trọn .NET Framework 4.5.2.** Được dùng mọi API tới 4.5.2. `App.config` khai `sku=".NETFramework,Version=v4.5.2"` (MINH đề xuất kèm, để thiếu runtime thì **báo ngay lúc mở app** thay vì crash giữa chừng). Thay cho luật "trường hợp xấu nhất: chỉ 4.5 gốc" của v1. Chi tiết ở §3.1. |
| D48 | **Vòng 6 (user):** *"Leader tôi muốn cài được toạ độ đứng. Nếu không cài thì đứng đâu thì đứng."* | `LeaderX/LeaderY` **mặc định để trống**. **Có cài** → Leader đứng đúng chỗ đó, đi Thủ khố xong thì quay về. **Không cài** → Leader không bị kéo đi đâu; đi Thủ khố xong thì đứng luôn ở đó. |
| D43 | **T12:** **không bán được gì**; rác thì giao vào một clone, user tự dọn tay. | Thùng rác = **kệ Rác**: dồn rác vào clone thuộc kệ Rác; tool **nhả clone** đó (đăng xuất) để user đăng nhập tay dọn, rồi **nhận lại** (§6.2). |
| D44 | **T13:** "không tự mở rộng". | **Bỏ D37** (tự dùng túi vải). |
| D45 | **T14:** mỗi nhân vật giữ tối đa **2 tỷ xu**. | Kiểm tra trước khi nhận xu. Leader vượt `XuNguong` thì dồn xu sang clone (§6 bước 6). |
| D46 | **T10:** chat riêng tới acc chưa kết bạn **nhận được**, nhưng **phải gửi tiếng Việt không dấu**. | **Bỏ dấu mọi tin gửi đi**, kể cả tên món và mẫu rao (§9.1). |
| D47 | **T1:** acc **cấp 1** giao dịch được. | Clone kho **không cần luyện cấp**. |
| — | **T2** đồ có hạn giao được · **T6** rời khu/thoát thì phiên tự huỷ, đồ nguyên · **T7** giao dịch một chiều (một bên khoá rỗng) được · **T9** Làng Tone có NPC đổi khu (id 13) · **T11** chat khu 5 giây/lần có tem thì không bị chặn | Khớp giả định cũ, không đổi thiết kế. |

## 3. Phạm vi

**CÓ (giai đoạn 1–4):**
- Đăng nhập nhiều acc, chia proxy, tự đăng nhập lại, tự đánh chống rớt mạng. Phần này đã có trong lõi.
- Đi tới map kho; Leader về khu chính (đứng đúng toạ độ nếu có cài); clone về khu phụ.
- Bộ máy giao dịch với hai vai: **nhận** và **giao**.
- Leader nhận đồ và xu; cất rương; dồn đồ sang clone theo kệ; dồn xu khi gần trần.
- Clone sang khu chính theo lệnh; lấy đồ từ rương, tách chồng, giao cho người nhận.
- Sổ kho gồm túi và rương; giữ chỗ; nhật ký nhập/xuất; kệ Rác + nhả/nhận lại clone.
- Lệnh chat riêng; Leader tự rao và báo sự kiện ở chat cộng đồng.
- Giao diện: tổng kho, điều phối, hàng chờ, trạng thái, kệ, gói rút, theo dõi, cài đặt.

**KHÔNG (đừng thêm vào nếu chưa bàn lại):**
- Đánh quái, nhặt đồ, PK, đập đồ.
- **Dùng hay mặc bất kỳ món nào** (D38).
- Bán cho NPC, vứt đồ (D43), tự mở rộng túi (D44).
- Auto mua bán, rao bán hàng, **rút xu** (P5).
- Clone đi tìm người ở map khác.
- Chat thế giới.
- Bản Android, obfuscate.

## 3.1 Công nghệ và môi trường chạy (D29)

| Hạng mục | Chốt | Vì sao |
|---|---|---|
| Ngôn ngữ | **C#** | Toàn bộ lõi (~30.000 dòng: protocol, điều hướng, quản lý nhiều acc) đã có sẵn bằng C# |
| Nền tảng | **.NET Framework 4.5.2** (D49) — csproj kiểu SDK, `<TargetFramework>net452</TargetFramework>` | Server 2012 hỗ trợ tới .NET 4.8; bản 4.5.2 thường đã có qua Windows Update. Target net452 còn là **chốt chặn lúc build**: gọi API mới hơn 4.5.2 thì không build được. |
| `App.config` | `supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5.2"` + `gcServer enabled="true"` | **Khác NSOBAOTATL / NSOLITEPRO** (hai bên khai `v4.5`). Khai 4.5.2 thì máy thiếu runtime sẽ **từ chối mở app kèm thông báo rõ ràng**, không mở được rồi crash giữa chừng. Server GC giúp chịu tải nhiều acc. |
| Giao diện | **WinForms**, tiếng Việt **có dấu** | Dùng qua Remote Desktop trên VPS. **Chat gửi vào game thì không dấu** (D46). |
| Thư viện ngoài | **Không có.** Không NuGet, không thư viện JSON; dữ liệu ghi dạng pipe-delimited / `key=value` | Chép 1 file exe là chạy; không bị lệch phiên bản DLL trên VPS |
| Build | `dotnet build NSOKHODO.sln -c Release` **trên máy dev** | VPS chỉ cần file exe, không cần SDK |
| Triển khai | Chép `NSOKHODO.exe` (map `.bin` + ảnh nhúng sẵn, khoảng 1 MB). Thư mục `Data/` và `Logs/` tự tạo cạnh exe. | Giống NSOBAOTATL: một file, chạy ngay |
| Đa luồng | `Thread` / `ThreadPool`, hạn chế `async`/`await`; stack 256 KB mỗi luồng | Theo NSOBAOTATL (đo được 1.800 acc chạy trong một tiến trình) |

**Luật code bắt buộc khi chạy Server 2012:**
1. **Không gọi API mới hơn 4.5.2.** Ví dụ `DateTimeOffset.ToUnixTime*`, `Array.Empty`, `Task.CompletedTask`. Cần Unix-time thì tính tay từ mốc `1970-01-01 UTC`. Bài học: NSOLITEPRO từng crash `Method not found` trên VPS vì đúng lỗi này (`WORKLOG.md:2028`).
2. **Không dùng cú pháp cần kiểu dữ liệu mà 4.5 không có**: tuple `(a, b)` (cần `System.ValueTuple`), `Span<T>`, `IAsyncEnumerable`… `LangVersion latest` cho phép viết, nhưng thiếu kiểu thì build báo lỗi — chốt chặn vẫn hoạt động.
3. **Dùng trọn 4.5.2 (D49).** Được dùng mọi API tới 4.5.2. Khai báo runtime trong `App.config` phải **khớp** với target (`v4.5.2`), để máy thiếu runtime thì báo ngay lúc mở.
   - *(Thay luật v1 "chỉ dùng API của 4.5 gốc".)*
   - ⚠ **Rủi ro còn lại:** VPS có thể **chỉ có 4.5 gốc**. User không có số phiên bản; NSOLITEPRO "pass" chỉ chứng minh có **từ 4.5 trở lên**, vì bên đó khai `v4.5`.
   - Nếu mở app mà Windows báo **thiếu .NET Framework 4.5.2** → **cài .NET Framework 4.8** (hỗ trợ Server 2012, bao trùm 4.5.2), rồi mở lại. Không phải sửa code.
4. **Kiểm tra bản .NET trên VPS** trước khi chép exe lên (PowerShell):
   ```powershell
   (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
   ```
   Kết quả: `378389` = 4.5 · `379893` = 4.5.2 · `393295` trở lên = 4.6+ · `528040` trở lên = 4.8.
5. **Không phụ thuộc HTTPS.** Nếu sau này có gọi web, .NET 4.5 mặc định chỉ dùng TLS 1.0, phải tự bật TLS 1.2 qua `ServicePointManager`. Hiện bot chỉ dùng TCP thô tới server game.

**Không chọn .NET mới hơn (6/8)** vì phải port toàn bộ lõi, exe tự chứa runtime nặng hơn nhiều, và mất lợi thế "chép 1 file là chạy" đã được kiểm chứng trên chính VPS này.

**Rủi ro môi trường (ngoài phạm vi tool):** Windows Server 2012 đã **hết hỗ trợ bảo mật từ 10/2023**. Nếu VPS mở Remote Desktop ra Internet thì nên đổi port, đặt mật khẩu mạnh, hoặc giới hạn IP được truy cập.

## 4. Vai trò, bố trí, cài đặt

| Vai | Số lượng | Ở đâu | Việc |
|---|---|---|---|
| **Chủ kho** | danh sách tên, user cài | — | Người chơi thật. Được xem kho và rút đồ qua tin nhắn. |
| **Leader** | 1, cộng 1 dự phòng | khu chính; đúng toạ độ nếu có cài, không cài thì đứng đâu cũng được | Nhận chat, nhận đồ, cất rương, dồn đồ, rao trạng thái. |
| **Clone** | khoảng 10, **cấp 1 là đủ** (D47) | khu phụ, đứng đâu cũng được | Chứa đồ (túi + rương); sang khu chính khi được điều. |

### Cài đặt kho

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `MayChu` | — | Mọi acc trong kho phải cùng máy chủ; tool từ chối acc khác máy chủ. |
| `Map` | 22 (Làng Tone) | |
| `KhuChinh` | — | Khu của Leader, nơi giao nhận với người chơi. |
| `KhuPhu` | — | Khu của clone. Bắt buộc khác `KhuChinh`. |
| `LeaderX`, `LeaderY` | **trống** | D48. **Trống** = Leader đứng đâu cũng được. **Có giá trị** = Leader luôn quay về đúng chỗ này. Nhập tay, hoặc bấm **"Lấy chỗ đang đứng"** (đã có ở NSOBAOTATL); nút **"Xoá toạ độ"** để trở lại trống. Đặt xa Thủ khố thì mỗi lần cất rương Leader phải đi qua lại vài giây. |
| `Leader`, `LeaderDuPhong` | — | Chọn trong lưới acc. |
| `ChuKho` | rỗng | Danh sách tên nhân vật. |
| `CheDoNhan` | **TatCa** | `TatCa` \| `ChiChuKho` (D16). |
| `ChatVoiNguoiLa` | **tắt** | Tắt = không gửi tin riêng cho ai ngoài Chủ kho (D24). |
| `NguongNhan` | 12 | Túi Leader còn ít hơn số ô này thì từ chối nạp. |
| `ChoNguoi` | 120 s | Thời gian chờ tối đa mỗi bước khi đối phương là người. |
| `ChoNguoiLa` | 45 s | Như trên, cho người ngoài danh sách Chủ kho. |
| `ChoBot` | 20 s | Như trên, khi đối phương là bot (theo NSOCHIP Auto Sell). |
| `MoiLaiGiay` | **31** | Khoảng cách giữa hai lần mời cùng một người (D40). |
| `GiuCuaGiay` | 60 | Sau lệnh `nap`, Leader chỉ nhận lời mời của Chủ kho đó trong khoảng này (D41). |
| `ChoCoMat` | 10 phút | Lệnh rút chờ người nhận có mặt ở khu chính tối đa bao lâu. |
| `XuTran` | 2.000.000.000 | Trần xu mỗi nhân vật (D45, T14). |
| `XuNguong` | 1.000.000.000 | Xu của Leader vượt mức này thì dồn sang clone. |
| `RaoBat`, `RaoMau` | bật · `Kho do {dung}/{tong}` | §9.2 |
| `RaoCheDo` | **ThongMinh** | `ThongMinh` \| `Deu` (D31) |
| `RaoNhipGiay` | 5 | Nhịp khi có người ngoài trong khu hoặc số liệu vừa đổi; cũng là nhịp của chế độ `Deu` |
| `RaoVangGiay` | 300 | Nhịp khi khu vắng và số liệu không đổi (chỉ dùng cho `ThongMinh`) |
| `KeHang` | tự đoán | Bảng clone → kệ; template → nhóm (§6.1) |
| `RacDanhSach` | rỗng | Template id do user đánh dấu là rác (§6.2) |
| `RacBaoNguong` | 80 % | Kệ Rác đầy quá mức này thì báo user vào dọn |
| `GoiRut` | rỗng | Tên gói → danh sách (id, số lượng) (§7) |
| `TheoDoi` | rỗng | id → ngưỡng báo (§9.3) |
| `BaoCaoGio` | tắt | Giờ gửi báo cáo ngày, ví dụ `22:00` (§9.3) |
| `BaoCongDong` | bật | Sao sự kiện sang chat cộng đồng (D20). |
| `LogFile` | **bật** | Log đầy đủ theo ngày (§8.1). |
| `GiuLogNgay` | 0 | Số ngày giữ log; 0 = giữ hết. |
| `LogHexGiaoDich` | bật | Ghi hex mọi gói giao dịch (37/43–46/56–58), gói túi đi kèm (8/9) và **mọi tin chữ server gửi** (−24/−25/−26/53) trong lúc có phiên. |

**Map 22 = Làng Tone** (`NSOBAOTATL/src/.../UI/MapNames.cs:40`).
- Có NPC đổi khu (id 13, T9) và Thủ khố (T8).
- Clone mới (taskId = 0) đã vào được map 22 (`NSOBAOTATL/docs/STATUS.md`).
- **Chưa biết:** số ô rương (đọc từ gói 31 lúc chạy), số người tối đa mỗi khu (M12).

**Leader dự phòng:**
- Mọi bot chạy trong một tiến trình, và **mọi clone đều chuyển tiếp** tin nhắn của Chủ kho về bộ điều phối.
- Leader offline thì bộ điều phối cho acc dự phòng sang khu chính, đứng đúng toạ độ đã cài (nếu có). Dự phòng nhận vai Leader, báo ở chat cộng đồng và nhắn Chủ kho tên Leader mới.

## 5. Luồng NẠP (người chơi → Leader)

Người chơi tới khu chính, chỉ vào Leader rồi mời giao dịch bằng tay, hoặc gõ `gdvp` / `gd <id>`.

| Bước | Sự kiện / điều kiện | Leader làm gì |
|---|---|---|
| N1 | nhận **43** `{id}` | Đổi id ra tên bằng `OtherPlayers` cùng khu. **Không tìm thấy** → bỏ qua (giống client gốc) + log. **Không được nạp** (`CheDoNhan = ChiChuKho` và tên không thuộc Chủ kho) → **56**, không nhắn gì. **Đang giữ cửa** cho một Chủ kho khác (D41) → **56**. **Ô trống túi < `NguongNhan`** → **56**; nếu người mời là Chủ kho thì báo *"Dang don kho, thu lai sau ~N giay"*. |
| N2 | hợp lệ | Gửi **44** `{id}`. Chờ gói 37, tối đa 15 giây. |
| N3 | nhận **37** `{tên}` | Tên khác người vừa được nhận lời → **57**. |
| N4 | — | Gửi ngay **45** `{xu 0, n 0}` (khoá rỗng — T7 xác nhận được). |
| N5 | chờ **45** của đối phương | Chờ tối đa `ChoNguoi`, hoặc `ChoNguoiLa` nếu là người lạ. |
| N6 | nhận **45** `{xu, n, món…}` | **Kiểm tra `n ≤ ô trống thực tế`** (mỗi món tính một ô mới) và **`xu Leader + xu ≤ XuTran`**. Không đạt → **57**; nếu là Chủ kho thì báo lý do (*"Kho chi nhan them K mon"* / *"Leader sap cham tran xu"*). Đạt → ghi lại danh sách món + xu sẽ nhận. |
| N7 | — | Chờ **1.500 ms**, rồi gửi **46**. |
| N8 | chờ **58** | Gói 8/9 về túi đi qua handler có sẵn. |
| N9 | nhận **58** `{xu mới}` | Cập nhật xu. Gửi **57** để dọn phiên. Ghi nhật ký. Báo *"Da nhan N mon + X xu tu <ten>"* ở chat cộng đồng, và qua chat riêng nếu người nạp là Chủ kho. Sau 2 giây **đối chiếu** túi trước/sau: lệch thì ghi `LECH` và đánh dấu cần đồng bộ lại. |
| — | nhận **57** bất kỳ lúc nào | Đóng phiên, ghi lý do kèm mọi tin chữ server gửi ngay trước đó. |

**Chủ kho chen ngang (D41):**
- Server **không cho** Chủ kho mời Leader khi Leader đang giao dịch (T5). Vì vậy Chủ kho nhắn **`nap`** cho Leader. Leader sẽ:
  1. đang có phiên với **người lạ** hoặc **phiên dọn kho** (bot↔bot) → huỷ ngay (**57**); cả hai loại phiên này huỷ đều an toàn, đồ ở nguyên chỗ cũ;
  2. **giữ cửa** `GiuCuaGiay` (60 giây) cho riêng Chủ kho đó: lời mời của người khác → 56;
  3. trả lời *"@NNN San sang nhan K mon. Moi giao dich ngay"*.
- Đang có phiên với **một Chủ kho khác** thì **không** huỷ; trả lời *"Dang giao dich voi chu kho khac, doi ~N giay"*.

**Ghi chú:**
- **Leader không bao giờ đặt đồ khi đang nạp**, nên chế độ `TatCa` (mặc định) không làm mất đồ. Rủi ro là người lạ đổ rác làm đầy kho, hoặc treo phiên (§14 mục 8).
- **Tương thích `gdvp`:**
  - `gdvp` không có timeout, và mời lại mỗi 5 giây. Server chỉ nhận một lời mời mỗi 31 giây, nên phần lớn lời mời của nó bị bỏ qua (T4) — vô hại.
  - `gdvp` đổ **toàn bộ** đồ không khoá trong túi acc chính. Món muốn giữ thì cất rương trước, hoặc dùng `gd <id>`.
  - Kho từ chối thì `gdvp` vẫn mời mãi; người chơi phải tự dừng.

## 6. Dọn kho — Leader luôn giữ túi trống

**Điều kiện chạy:**
- Leader rảnh: không có phiên giao dịch, không có ai mời trong 10 giây gần nhất, không đang giữ cửa.
- **Không có Chủ kho nào trong khu chính.**
- Người lạ có trong khu vẫn dọn; họ mời trong lúc Leader bận thì server báo "đang có giao dịch khác" (T5).
- Chủ kho nhắn `nap` thì phiên dọn đang chạy bị huỷ ngay (§5).

```
Túi Leader ──(sát Thủ khố; 17: cất rương, không cần giao dịch)──► Rương Leader
Rương Leader ──(16: lấy ra ≤12 món cùng kệ)──► Túi Leader ──(giao dịch bot↔bot tại khu chính)──► Túi Clone
Túi Clone ──(về khu phụ, tới Thủ khố; 17 khi ô trống < NguongNhan)──► Rương Clone
```

**Mở rương (D42):**
- Acc cần dùng rương (Leader hoặc clone) **tự đi tới NPC 5 trong khu của mình**, làm xong thì về lại chỗ đứng theo D48 (Leader) hoặc đứng luôn tại đó (clone).
- Đứng cách NPC 5 ≤ 22 px → gửi `-30 {-103, 4}` **một lần mỗi phiên đăng nhập** (client gốc chỉ gửi khi chưa có danh sách rương) → nhận gói **31** → dùng **16/17** trong lúc vẫn đứng sát NPC.
- Không dùng menu NPC.

**Các bước:**
1. **Bước nhanh:** túi Leader có món → cất hết vào rương Leader.
2. **Bước nền:** rương Leader còn món → gọi **một** clone sang khu chính → Leader lấy ≤12 món **cùng kệ** ra túi → giao dịch sang clone → **mời lại ngay** cho lượt kế tiếp (D40) → lặp tới khi clone gần đầy hoặc hết món của kệ đó → clone về khu phụ, tới Thủ khố, tự cất rương.
   - Mỗi lượt Leader chỉ lấy ra tối đa 12 món, nên túi Leader luôn còn ít nhất `(số ô túi − 12)` ô cho người chơi.
3. **Chọn clone nhận** (clone phải online, rảnh, không bị nhả, đã hết hồi chiêu đổi khu), theo thứ tự:
   1. clone thuộc **kệ của nhóm món** đó mà **đang giữ cùng loại món có thể chồng** (§6.1);
   2. clone thuộc kệ đó có **nhiều ô trống nhất** (túi + rương);
   3. **kệ đó đầy** → clone thuộc kệ "Khác" → nếu cũng đầy thì clone bất kỳ còn chỗ (**trừ kệ Rác**), và ghi log `KE_TRAN`.
4. **Mỗi lúc chỉ 1 clone được gọi sang khu chính để dọn.**
5. **Hết chỗ:** mọi clone đều đầy → trạng thái **KHO ĐẦY**. Từ chối nạp và báo (§9.3).
6. **Xu (D45):** Leader có xu vượt `XuNguong` → một phiên bot↔bot chuyển phần vượt sang clone **ít xu nhất**. Mỗi clone nhận tối đa tới `XuTran − 100.000.000` để chừa chỗ. **Tổng trần xu của kho = số acc × 2 tỷ.**

**Phương án lùi** nếu P0b cho thấy rương không dùng được bằng bot:
- bỏ bước 1 và phần "tự cất rương";
- Leader giao thẳng từ túi sang clone;
- sức chứa chỉ còn bằng số ô túi.

### 6.1 Kệ hàng theo loại (D32)

- **Mỗi clone thuộc một kệ.** Kệ mặc định:

| Kệ | Nhận món nào | Cách đoán mặc định |
|---|---|---|
| Trang bị | đồ mặc, vũ khí | `ItemTemplate.Type` 0–15 (`IsTypeBody`) |
| Thú cưỡi & ngọc | thú cưỡi, ngọc khảm | `Type` 29–33 (`IsTypeMount`), 34 (`IsTypeNgocKham`) |
| Có thể chồng | đá, bình, nguyên liệu… | template xếp chồng được |
| **Rác** | món trong `RacDanhSach` | user đánh dấu (§6.2) |
| Khác | mọi thứ còn lại, và là chỗ tràn khi kệ khác đầy | — |

- **User đổi được** trên tool: kệ của từng clone, nhóm của từng template, thêm hoặc bớt kệ.
- Luật đoán theo `Type` ngoài ba khoảng đã biết (0–15, 29–33, 34) **chưa có nguồn**. Chỉ khi thấy template thật trong kho mới chốt thêm; đừng đoán.
- **Lợi ích:** rút một món thường chỉ cần **một clone**; đồ cùng loại dồn về một chỗ nên cộng dồn được, **tốn ít ô hơn**.

### 6.2 Kệ Rác — dọn tay (D43)

Server không cho bán món nào (T12), nên tool **không tự bán, không tự vứt**.

- **Món trong `RacDanhSach`** (user đánh dấu trên tool) được dồn vào clone thuộc **kệ Rác**. Tool **không tự đoán** món nào là rác.
- Kệ Rác đầy quá `RacBaoNguong` → báo Chủ kho: *"Ke Rac day 80%, vao don <ten clone>"*.
- **Nhả clone** (nút trên tool):
  1. clone đang có phiên hoặc đang làm việc → chờ xong;
  2. bot **đăng xuất** clone và đánh dấu **ĐÃ NHẢ**;
  3. bộ chọn không dùng clone đó nữa; món trên clone hiện với nhãn *"đang nhả — không rút được"*, và **nhả mọi chỗ đang giữ** trên clone đó;
  4. user đăng nhập tay bằng client thường để dọn.
- **Nhận lại** (nút trên tool): user đã thoát game ở client thường → bot đăng nhập lại → đọc lại túi và rương → cập nhật sổ kho → ghi log số món trước/sau (`NHA_CLONE`).
- **Không nhả Leader.** Muốn dọn Leader thì đổi vai Leader sang dự phòng trước.
- ⚠ **Bot đang đăng nhập mà user đăng nhập tay cùng acc** thì hai bên đá nhau. Nút "Nhận lại" phải cảnh báo *"Đã thoát client thường chưa?"*.

## 7. Luồng RÚT (clone → người nhận)

**Nguồn lệnh** — Chủ kho quyết định giao cho ai (D25):
- **Tool:** người nhận là **tên nhân vật bất kỳ**.
- **Chat riêng:** chỉ Chủ kho ra lệnh được. Người nhận mặc định là chính người gửi lệnh; muốn giao cho người khác thì thêm `cho <tên>` (§9.4).
- **Người nhận không phải Chủ kho** thì không nhận tin riêng nào (D24). Chủ kho ra lệnh nhận báo tiến độ thay.

| Bước | Việc |
|---|---|
| R1 | **Nhận lệnh** `(templateId, [cấp +], số lượng hoặc "hết", người nhận)` → vào hàng chờ, cấp số lệnh `#N`. **Gói rút** (D35) được bung ra thành nhiều dòng món trong **cùng một lệnh**. Kho thiếu món nào thì báo rõ món đó và hỏi lại: giao phần đang có, hay huỷ cả gói. |
| R2 | **Lập kế hoạch + GIỮ CHỖ** (D34): đọc sổ kho, **chỉ tính phần khả dụng** (tổng − đã giữ − món trên clone đang nhả). Chọn theo thứ tự ít clone nhất → ít lượt nhất → món trong túi trước món trong rương. Mỗi lượt ≤12 ô. Lấy một phần chồng thì đánh dấu cần tách. **Giữ chỗ ngay.** Không đủ → báo *"chi con K (L dang giu cho lenh khac)"*. |
| R3 | **Chuẩn bị ngay tại khu phụ:** món trong rương → tới Thủ khố, lấy ra (16). Lấy một phần chồng → tách (cmd 22 rồi −28/−85; cách nào đúng thì đo M8), sau đó so túi trước/sau để tìm ô chứa phần tách. |
| R4 | **Chờ người nhận có mặt ở khu chính.** Leader thấy người đó trong `OtherPlayers`. Chưa có → báo Chủ kho ra lệnh: *"Lenh #12: 5 x <ten mon> cho <nguoi nhan>. Toi Lang Tone khu K de nhan."* Quá `ChoCoMat` → huỷ lệnh và báo. |
| R5 | Clone **sang khu chính** bằng một lệnh đổi khu. Clone đã đứng ở khu phụ quá 10 giây thì đổi được **ngay** (D28). Báo *"<clone> se moi <nguoi nhan>"* → đi sát người nhận: \|dx\| ≤ 40, \|dy\| ≤ 30 (dưới ngưỡng client 60/40). |
| R6 | Gửi **43** `{id người nhận}`. Chưa có 37 thì **mời lại sau 31 giây** (D40), tối đa `ChoNguoi` (≈ 4 lần). Server báo người nhận **đang có giao dịch khác** (T5) → cũng chờ 31 giây rồi mời lại. |
| R7 | Nhận **37**, kiểm tra đúng tên → gửi **45** `{0, n, vị trí…}`. |
| R8 | **Chờ 45 của người nhận.** Chưa thấy 45 thì **không gửi 46**. Nếu người nhận đặt kèm đồ, coi như nạp: kiểm tra chỗ trống và trần xu như N6. |
| R9 | Chờ **1.500 ms**, rồi gửi **46**. |
| R10 | Nhận **58**: xoá các ô đã giao khỏi túi local (server có gửi gói xoá ô không thì đo M2), gửi **57**, ghi nhật ký, cập nhật tiến độ, nhả phần giữ chỗ đã giao. Còn thiếu → **mời lại ngay** cho lượt tiếp (D40), hoặc chuyển sang clone khác. |
| R11 | **Phiên bị đóng trước khi xong:** **(a)** đóng **ngay sau khi clone khoá** (kèm hoặc không kèm câu "không đủ hành trang") → hiểu là **người nhận thiếu ô** (D39) → **tạm dừng** lệnh, báo *"<nguoi nhan> thieu o hanh trang (can K o). Don tui roi nhan: tiep #12"*. **(b)** lý do khác → tính một lần hỏng; **hỏng 2 lần liên tiếp** → tạm dừng và báo. |
| R12 | Xong → clone về khu phụ (đã qua hơn 10 giây kể từ lúc tới khu chính). Leader báo *"Xong lenh #12: da giao 5 x <ten mon> cho <nguoi nhan>"*. Món đã lấy ra khỏi rương mà chưa giao được thì clone tự cất lại. |

- **Nhả giữ chỗ khi:** giao xong phần đó · lệnh bị huỷ hoặc quá hạn · clone giữ món bị offline quá 5 phút, hoặc bị nhả (lập lại kế hoạch với clone khác).
- **Giữ chỗ phải khớp với thực tế:** sau mỗi sub 115 (server gửi lại túi), đối chiếu món đang giữ với túi thật. Món đã biến mất → nhả chỗ giữ, lập lại kế hoạch, ghi log `GIU_CHO_LECH`.
- **Cùng một người nhận có nhiều lệnh** → gộp chung vào các lượt 12 ô để bớt số phiên giao dịch.
- **Mỗi lúc chỉ một clone giao cho một người nhận.** Khu chính chỉ có Leader, người nhận, tối đa một clone đang giao, và tối đa một clone dọn.

## 8. Sổ kho

- **Nguồn dữ liệu:**
  - túi của từng acc: gói đăng nhập, sub 115 định kỳ, các gói 8/9/7/10/18;
  - rương: gói 31, **chỉ có khi đã mở** (đứng sát Thủ khố), nên lưu kèm thời điểm đọc.
- **Khoá gộp:** `(templateId, upgrade, isExpires)`. Ghi theo **nội dung**, không giữ tham chiếu object (sub 115 dựng lại object khoảng 20 giây một lần — `NSOLITEPRO/docs/reference/SERVER_FACTS.md:127`).
- **Món khoá** vẫn có thể nằm trong túi acc (có từ trước). Chúng hiện riêng với cờ *"khoá — không giao được"* và **không tính** vào hàng rút được.
- **Mỗi dòng hiện:** ID, tên tiếng Việt, **kệ**, cấp +, có hạn hay không, **tổng / đang giữ / khả dụng**, số ô, phân bố theo từng acc (túi / rương), cờ **rác**, **theo dõi**, **đang nhả**.
- **Giữ chỗ lưu cùng sổ kho** (`Data/kho.txt`), để khởi động lại app thì lệnh đang dở không mất chỗ đã giữ.
- **Sức chứa** — số dùng cho câu rao "70/360" (D27):
  - `{tong}` = Σ **clone** (số ô túi + số ô rương), **không tính Leader** (cửa nhận) và **không tính kệ Rác**;
  - `{dung}` = số ô đang có đồ trong phần trên;
  - `{trong}` = `{tong} − {dung}`;
  - clone chưa từng đọc rương → chỉ tính phần túi, và câu rao thêm dấu `~` để báo con số chưa đủ.
- **Xu:** tổng xu của Leader và các clone; hiện cả **khoảng trống xu** còn lại (Σ `XuTran` − tổng).
- **Giới hạn giai đoạn 1:** không hiện dòng thuộc tính của món (gói 42 không xử lý món trong rương). Món có hạn chỉ hiện cảnh báo.
- **Lưu đĩa:**
  - `Data/kho.txt`, pipe-delimited; clone offline vẫn thấy hàng, kèm *"lần cuối thấy lúc…"*;
  - `Data/tenmon.txt`: cache tên món theo phiên bản dữ liệu.

### 8.1 Log đầy đủ theo ngày (D26)

**Vì sao không dùng lại `FileLog` của lõi:** `Logging/FileLog.cs` **mặc định tắt**, chỉ ghi **một file**, và khi vượt 5 MB thì chuyển sang `OldLog` — file cũ trước đó bị ghi đè. Tức là log cũ **bị mất**, trái với yêu cầu "đầy đủ".

| File | Nội dung | Một dòng gồm |
|---|---|---|
| `Logs/yyyy-MM-dd/app.log` | **Mọi thứ**: đăng nhập / rớt / đăng nhập lại, đổi khu, rương, tách chồng, nhả / nhận lại clone, quyết định của bộ điều phối, lỗi | `HH:mm:ss.fff [acc] [nhóm] nội dung` |
| `Logs/yyyy-MM-dd/giaodich.csv` | Mỗi **phiên** giao dịch một dòng | thời gian bắt đầu/kết thúc, vai (nạp / rút / dọn / dồn xu), acc bot, đối tác (tên + id), là Chủ kho hay không, món (tpl, +, hạn, số lượng), xu, kết quả (XONG / HUY + lý do + tin chữ server), số lệnh, lệch hay không |
| `Logs/yyyy-MM-dd/chat.log` | **Mọi tin chat** vào/ra: riêng và cộng đồng, gồm cả tin rao và tin bị bỏ qua của người lạ | `HH:mm:ss [acc] [RIENG/CONGDONG] [VAO/RA] <người> nội dung` |
| `Logs/yyyy-MM-dd/lenh.csv` | Mỗi **lệnh rút** một dòng, ghi khi lệnh kết thúc | số lệnh, nguồn (tool / chat + tên), món, số lượng xin / đã giao, người nhận, các clone đã giao, thời gian, kết quả |
| `Logs/yyyy-MM-dd/hex.log` | Hex các gói giao dịch, gói túi đi kèm và tin chữ server (khi `LogHexGiaoDich` bật) | `HH:mm:ss.fff [acc] S→C cmd len hex` |

- **Sang ngày mới** (theo giờ máy) thì mở thư mục mới. **Không xoay theo dung lượng, không ghi đè.** Một file vượt 50 MB thì mở phần tiếp theo `app_2.log`, `app_3.log`…
- **Ghi qua hàng đợi, gom một lần mỗi giây**, giống `Logger` hiện có. Lỗi ghi đĩa không được làm chết app.
- **`GiuLogNgay`** (mặc định 0 = giữ hết): lớn hơn 0 thì lúc khởi động xoá các thư mục ngày cũ hơn số ngày đó.
- **Không bao giờ ghi mật khẩu** vào log.
- **Tool có nút "Mở thư mục log hôm nay"**.
- **Chữ do bot tự viết trong log thì không dấu**, theo quy ước của họ NSOLITEPRO. Chữ **nhận từ server** (tên nhân vật, tên món, tin chat đến) **giữ nguyên**. Tin **gửi đi** ghi đúng bản đã gửi (không dấu). File ghi bằng UTF-8.

## 9. Chat

### 9.1 Luật chung (mọi acc, mọi kênh)

- **Mọi tin gửi đi đều bắt đầu bằng `@NNN `** (bộ đếm 000–999 xoay vòng, dùng chung toàn tiến trình, theo khuôn `KichYenCaller.TemGoi`).
- **Mọi tin gửi đi đều KHÔNG DẤU (D46).**
  - Hàm `BoDau` đổi mọi ký tự tiếng Việt có dấu thành không dấu, `đ/Đ` thành `d/D`.
  - Áp dụng cho **toàn bộ** tin: tên món lấy từ server, mẫu rao do user đặt, tên nhân vật.
  - Bỏ dấu xong còn ký tự ngoài ASCII (ví dụ ký tự lạ trong tên nhân vật) → thay bằng `?` và ghi log.
- **Khi đọc tin đến:** bỏ tem ở đầu (`BoTem`), và so khớp lệnh **không phân biệt dấu / hoa thường**.
- **Tem giúp mọi tin không bao giờ trùng nhau**, kể cả tin rao khi số liệu kho không đổi. T11 xác nhận có tem thì không bị chặn.

### 9.2 Leader tự rao (chat cộng đồng, gói −23)

- **Chỉ Leader rao.** Bật/tắt bằng `RaoBat`.
- **Nhịp rao theo `RaoCheDo` (D31):**

| Chế độ | Khi nào rao |
|---|---|
| **`ThongMinh`** (mặc định) | Rao mỗi `RaoNhipGiay` (5 giây) **khi** trong khu chính có **người ngoài** (không phải acc của kho), **hoặc** số liệu kho vừa đổi (trong 60 giây gần nhất). Còn lại thì mỗi `RaoVangGiay` (300 giây) rao một lần. |
| `Deu` | Mỗi `RaoNhipGiay` giây, bất kể có ai hay không. |

- **Vì sao mặc định là `ThongMinh`:**
  - `Deu` 5 giây suốt 24 giờ là **17.280 tin/ngày**.
  - T11 mới thử 3 phút, và Auto Sell chỉ chứng minh server **cho phép** nhịp đó. Chưa có gì chứng minh rao liên tục cả ngày là an toàn trước việc bị báo spam.
- **"Người ngoài"** = người trong `OtherPlayers` của Leader mà không phải Leader, dự phòng, hay clone của kho.
- **Mẫu câu tự đặt** (`RaoMau`), với các biến:

| Biến | Nghĩa |
|---|---|
| `{dung}` `{tong}` `{trong}` | sức chứa (§8) |
| `{online}` `{tongacc}` | số clone đang online / tổng số clone |
| `{lenh}` | số lệnh rút đang chờ |
| `{leader}` | tên Leader hiện tại |

- Ví dụ: mẫu `Kho đồ {dung}/{tong}` → gửi đi `@417 Kho do 70/360` (bỏ dấu tự động).

### 9.3 Leader báo sự kiện (D20)

- **Mỗi sự kiện gửi hai nơi:**
  - **chat riêng** cho **Chủ kho** liên quan: người ra lệnh, hoặc Chủ kho vừa nạp. Không gửi cho người lạ, trừ khi bật `ChatVoiNguoiLa`;
  - **chat cộng đồng**, nếu `BaoCongDong` bật.
- **Sự kiện:** nhận lệnh · chờ người nhận · clone sẽ mời · xong lệnh · lệnh tạm dừng (người nhận thiếu ô / hỏng 2 lần) · nạp xong · từ chối nạp (đầy / đang dọn / gần trần xu) · KHO ĐẦY · **kệ Rác gần đầy** · nhả / nhận lại clone · đổi Leader · **món theo dõi**.
- **Theo dõi món (D36):** Chủ kho đặt `theo <id> [N]`:
  - **có `N`** → báo khi tổng khả dụng **xuống dưới N**, và báo lần nữa khi **lên lại ≥ N**;
  - **không có `N`** → báo mỗi khi món đó **về kho**;
  - mỗi món báo tối đa 1 lần mỗi 10 phút.
- **Báo cáo ngày (D36):** đến `BaoCaoGio`, Leader nhắn riêng cho **mọi Chủ kho**, ví dụ:
  `@512 Hom nay: nhap 120 mon (8 luot), xuat 30 (3 lenh). Kho 190/360, ke Rac 40/60, xu 1,2 ty`.
  Tin dài hơn giới hạn thì tách làm nhiều tin, mỗi tin có tem riêng.
- **Hàng đợi chat cộng đồng** dùng chung với tin rao:
  - **sự kiện chen trước** tin rao định kỳ;
  - giữa hai tin cộng đồng luôn cách ít nhất `RaoNhipGiay`.
- **Chat riêng:** mỗi acc gửi tối đa 1 tin mỗi 3 giây (sàn của `KichYenCaller`). Mỗi người nhận được tối đa 1 tin *"đang dọn / đầy"* mỗi 30 giây.
- **Độ dài tin:** giữ **dưới 100 ký tự**; độ dài tối đa trên server chính chưa đo (M10).

### 9.4 Lệnh chat riêng — CHỈ Chủ kho (D17)

- **Gửi cho ai:** gửi cho Leader. Gửi cho clone bất kỳ cũng được, vì mọi bot đều chuyển tiếp.
- **Có thể nhiều Chủ kho.** Mỗi Chủ kho thấy toàn bộ kho, và huỷ / tiếp được lệnh của chính mình.
- **Người ngoài danh sách:** bị bỏ qua, không trả lời (D24).
- **Cách đọc lệnh:** tách bằng dấu cách, không phân biệt dấu và hoa thường.

| Lệnh | Việc | Ví dụ trả lời |
|---|---|---|
| `kho` | tóm tắt | `@102 Kho: 10/10 online, 70/360 o, 0 lenh cho` |
| `nap` | **chen ngang + giữ cửa 60 giây** để nạp ngay (D41) | `@107 San sang nhan 36 mon. Moi giao dich ngay` |
| `tim <từ khoá>` | tìm theo tên, tối đa 5 kết quả | `@103 457 Da cap 5 x120 · 458 Da cap 6 x33` |
| `co <id>` | kho có bao nhiêu một món | `@104 457 Da cap 5: 120 (3 clone)` |
| `lay <id> [sl\|het] [+cấp] [cho <tên>]` | tạo lệnh rút; không có `cho` thì giao cho chính người ra lệnh | `@105 Lenh #12: 5 x Da cap 5 cho Abc. Toi Lang Tone khu 3` |
| `lay goi <tên> [cho <tên người>]` | rút cả gói (D35) | `@109 Lenh #13: goi dapdo (4 mon). Thieu: Da cap 7 (con 2/5)` |
| `goi` | liệt kê các gói rút đã định sẵn | `@108 Goi: dapdo, hoimau, sukien` |
| `tiep [#số]` | chạy tiếp lệnh đang tạm dừng (R11) | `@111 Tiep lenh #12` |
| `huy [#số]` | huỷ lệnh của chính mình | `@106 Da huy lenh #12` |
| `theo <id> [N]` · `botheo <id>` | bật / tắt theo dõi món (D36) | `@110 Theo doi 457: bao khi duoi 50` |

## 10. Giao diện — ⚠ BỐ CỤC PHẢI TRÌNH USER DUYỆT TRƯỚC KHI CODE

Đây là luật chung của NSOBAOTATL và NSOLITEPRO. Phần dưới chỉ là danh sách **chức năng** cần có, chưa phải bố cục.

- **Lưới acc** (VirtualMode của NSOBAOTATL), thêm các cột:
  - *Vai*: Leader / Dự phòng / Clone; *Kệ*; *Khu*;
  - *Túi trống*, *Rương trống*, *Xu*;
  - *Trạng thái kho*: Sẵn sàng / Đang nhận / Đang giao / Đang dọn / Đang sang khu / Giữ cửa / ĐẦY / **ĐÃ NHẢ** / Offline;
  - *Phiên GD*: đối phương + bước hiện tại, ví dụ `Chờ 45 · 34s`.
  - Nút trên từng dòng clone: **Nhả clone** / **Nhận lại**.
- **Tổng kho:** bảng món + ô tìm + lọc theo kệ + dòng sức chứa `70/360` (và theo từng kệ) + tổng xu, khoảng trống xu.
  - Chuột phải vào một món: *Đánh dấu rác* · *Theo dõi…* · *Đổi kệ cho loại món này*.
- **Kệ hàng:** bảng clone → kệ; bảng nhóm món → kệ; sức chứa từng kệ (gồm kệ Rác).
- **Gói rút:** thêm, sửa, xoá gói (tên + danh sách món + số lượng); nút *Rút gói…*.
- **Theo dõi:** danh sách món đang theo dõi + ngưỡng; giờ báo cáo ngày.
- **Điều phối** (D21): chọn món → số lượng → người nhận (gõ tên bất kỳ, hoặc chọn Chủ kho) → **Giao** → vào hàng chờ.
- **Hàng chờ:** số lệnh, món, số lượng, người nhận, nguồn (tool / chat + tên), tiến độ, clone đang giao, trạng thái (chờ / đang giao / tạm dừng + lý do), nút *Tiếp* / *Huỷ*.
- **Nhật ký:** nhập / xuất / dọn / dồn xu / nhả clone, lọc theo ngày, theo acc, theo đối tác (đọc từ `Logs/<ngày>/`). Có nút mở thư mục log.
- **Cài đặt:** toàn bộ bảng ở §4. Riêng toạ độ Leader: ô X / Y (để trống = tự do), nút *Lấy chỗ đang đứng*, nút *Xoá toạ độ*.

## 11. Kiến trúc

```
MainForm ──view──> FleetManager ──owns──> N × NsoClient
                        │                      │
                        │                 KhoMode (thay StandMode; CHẶN dùng/mặc món — D38)
                        │                   ├─ TradeEngine   (1 phiên, 2 vai)
                        │                   ├─ RuongRunner   (tới sát Thủ khố, −30/−103, 16/17)
                        │                   └─ Heartbeat 60 s + KeepAlive (CÓ SẴN, D23)
                        │
                        └──> KhoDieuPhoi (MỘT cho mỗi tiến trình)
                               ├─ SoKho        (túi + rương, sức chứa, GIỮ CHỖ, xu, lưu đĩa)
                               ├─ HangCho      (lệnh rút từ tool + chat, bung gói rút, tạm dừng / tiếp)
                               ├─ BoChon       (clone nhận khi dọn theo KỆ / clone giao khi rút / clone nhận xu)
                               ├─ KeHang       (clone → kệ, template → nhóm, kệ Rác, nhả / nhận lại clone)
                               ├─ TheoDoi      (ngưỡng món, báo cáo ngày)
                               ├─ KhoLenh      (đọc lệnh chat, danh sách Chủ kho, CheDoNhan, giữ cửa)
                               ├─ KenhChat     (tem @NNN, BỎ DẤU, hàng đợi cộng đồng, rao thông minh / đều, sàn chat riêng)
                               ├─ Leader       (chỉ định, dự phòng)
                               └─ NhatKy       (log theo ngày §8.1: app / giaodich / chat / lenh / hex)

Gửi:   TradeService — 43, 44, 45, 46, 56, 57                          (MỚI)
Nhận:  TradeHandler — 37, 43, 45, 46, 57, 58                          (MỚI + case trong MessageRouter)
Trạng thái: TradeState trong GameStateManager (ghi từ luồng mạng → phải khoá)   (MỚI)
Có sẵn: ChatService.SendPublicChat (−23), SendPrivateChat (−22); ItemService 16/17/22/−85, −30/−103
```

- **`KhoMode.Tick()`** xét theo thứ tự ưu tiên:
  1. đang có phiên giao dịch → `TradeEngine.Tick()`;
  2. có việc bộ điều phối giao → làm việc đó: sang khu, giao, nhận, lấy/cất rương, tách chồng, dồn xu;
  3. rảnh → dọn kho (§6): Leader cất rương và chuyển đồ đi; clone cất rương;
  4. không có việc → về đúng khu. Leader: **có cài toạ độ** thì về đúng chỗ đó, **không cài** thì đứng yên tại chỗ (D48). Clone: ở khu phụ, đứng đâu cũng được.
- **Chặn dùng đồ (D38):** `KhoMode` và mọi runner **không được gọi** gói dùng món (11) hay mặc/tháo đồ. `BinhMauRunner` **bị bỏ** khỏi khung. Có một ca kiểm tra trong `tools/kiemtra` bảo đảm không đường nào gửi các gói đó.
- **`TradeEngine`** cho phép gọi từ ngoài như sau:

  ```csharp
  Start(vai, tenDoiPhuong, idDoiPhuong, viTriMon[], xu, kiemHangDoiPhuong, choToiDa)
  ```

  - `kiemHangDoiPhuong(xu, món[]) → nhận / từ chối` là **móc cho auto mua bán** (D8). Hiện kiểm tra chỗ trống và trần xu.
  - Máy trạng thái:

  ```
  RANH → (giao) MOI ──31 s──► MOI_LAI … → CHO_37          (nhận) CHO_37
  CHO_37 → DA_KHOA(45) → CHO_45_DP → KIEM → CHO_TRUOC_46 → DA_46 → CHO_58 → XONG(57)
  bất kỳ bước nào: 57 đến / quá hạn / sai đối tác / bị `nap` chen ngang → HUY(57)
  57 đến ngay sau DA_KHOA ở vai giao → HUY_THIEU_O (D39)
  ```
  - Mỗi lần huỷ đều ghi kèm **mọi tin chữ server gửi** trong 3 giây trước đó. Server báo lỗi bằng chữ, câu chữ chính xác chưa biết (P0b sẽ gom).
- **Tự đánh chống rớt mạng (D23):** giữ nguyên `AutoModeBase.Heartbeat` — gói 61 nhắm vào charId của chính mình, 60 giây/lần, *"không gây sát thương, không bật cờ PK"* (`NSOBAOTATL/src/.../Auto/AutoModeBase.cs:104-120`).
  - **Tạm dừng khi đang trong phiên giao dịch** (thận trọng). Gói 93 của `TickDocIm` vẫn giữ luồng đọc.
  - Tự đánh ở **làng** có tác dụng không thì phải đo (M13).
- **Đổi khu:** `Navigator.DoZoneChange` (NPC 13 có ở Làng Tone — T9).
  - **Luật game (D28):** hồi chiêu 10 giây, **đếm từ lúc đặt chân tới khu mới**.
  - ⚠ **Mốc của bot lệch với luật game:** bot đang lấy mốc tại lúc **gửi lệnh** (`Navigator.cs:1336`), tức là sớm hơn lúc tới nơi. Khi code, lấy mốc tại **lúc nhận thông tin khu mới** (MAP_INFO).
  - **Bộ chọn ưu tiên clone đã hết hồi chiêu đổi khu.**
- **Một kho = một tiến trình.** Không chia kho ra nhiều cửa sổ bằng `--list=`.
- **Luật đọc byte nâng cấp trong gói 45** (server gửi) khớp luật của gói 8 và 31 (`IsTypeBody || IsTypeNgocKham`). Luật này **khác** sub −127/115 (`HasUpgrade`). Đừng gộp.

## 12. Nguồn code

- **Khung:** tách từ **NSOBAOTATL** (lưới 1.800 acc, proxy, tự đăng nhập lại, đổi khu, "Lấy chỗ đang đứng", tự đánh 60 giây).
  - **Mốc tách:** NSOBAOTATL commit **`7715bcf`** (2026-09-16, `github.com/tungnv27/NSOBAOTATL`), mà bản thân commit đó lại tách từ NSOLITEPRO `79d9d86`. Khi tách, ghi mốc này vào `docs/NGUON_GOC.md` của NSOKHODO, theo đúng khuôn của NSOBAOTATL.
- **Chép thêm từ NSOLITEPRO:**
  - khuôn nhận chat của `KichYenCaller` (`TemGoi`, `BoTem`, `TryAccept`, danh sách trắng);
  - vòng cất/lấy rương kiểu `DapDoRunner.CatRuong`. **Không cần** phần mở rương qua menu NPC của `DanhVongMode.MoUiRuong` — mở theo MODGAME (D42);
  - `TrainMode.FreeBagSlots`;
  - `UI/InventoryText.Name`.
- **Có sẵn trong lõi, chỉ cần dùng:**
  - gói 16/17, `-30/-103`; cmd 22 + −28/−85; sub −91 (không dùng);
  - `ChatService` (−22, −23);
  - `ItemTemplateStore`, `OtherPlayers`;
  - `Navigator.CharBurstMove` / `DoZoneChange`;
  - `KeepAliveController`, `AutoModeBase.Heartbeat`.
- **Bỏ khỏi khung NSOBAOTATL:** `BaoTaTl`, phép đo T1, **`BinhMauRunner`** (D38 — nó uống bình trong túi, tức là tiêu hao đồ trong kho).
- **`App.config` sửa có chủ đích:** `sku` `v4.5` → **`v4.5.2`** (D49). Ghi vào `NGUON_GOC.md` mục "Sửa có chủ đích" khi tách.
- **`accounts.txt` giữ nguyên 23 cột**, để dùng qua lại được giữa ba tool.
- **Chi phí đã biết:** thêm **bản lõi đóng băng thứ ba**. Trong 61 commit gần đây của NSOLITEPRO có 16 commit sửa `Core/` hoặc `Protocol/` (`NSOBAOTATL/docs/NGUON_GOC.md`). Tách thư viện lõi dùng chung là việc riêng, không làm ở đây.

## 13. Lộ trình

### P0a — Test tay: ✅ XONG 2026-09-16

Kết quả ở `TEST_TAY.md` và `GIAO_DICH.md` §9. Tóm tắt:

| Mã | Kết quả | Dẫn tới |
|---|---|---|
| **M17** (T0) | ✅ Giao dịch **không** khoá đồ; đồ chỉ khoá khi đem dùng | D38 |
| M4 (T3) | ✅ Server đóng phiên ngay khi khoá vượt số ô trống; không mất đồ | D39 |
| M3 (T2) | ✅ Đồ có hạn giao được | — |
| M11 (T1) | ✅ Cấp 1 giao dịch được | D47 |
| M6 (T4, T5) | ✅ Mời khoá 31 giây; xong phiên thì mời lại được ngay; người đang giao dịch thì không mời được | D40, D41 |
| M7 (T8) | ◐ Thủ khố, phải sát NPC. **Số ô rương chưa biết** | D42 |
| M15 (T9) | ✅ NPC 13 có ở Làng Tone | — |
| M16 (T14) | ✅ Trần 2 tỷ xu | D45 |
| M10 (T10) | ◐ Không cần kết bạn; **phải không dấu**. **Độ dài tối đa chưa biết** | D46 |
| M14 (T11) | ◐ 5 giây/lần có tem: không bị chặn trong 3 phút. Chưa thử dài | D31 giữ nguyên |
| T12 | ✅ Không bán được gì | D43 |
| T13 | ✅ Không làm tự mở rộng | D44 |

### P0b — Đo bằng bot (việc tiếp theo)

Dựng khung project (tách từ NSOBAOTATL `7715bcf`), viết `TradeService` / `TradeHandler`, thêm vài nút bấm tay và ghi hex. Dùng **đồ rẻ**.

| Mã | Đo gì | Phục vụ |
|---|---|---|
| M1 | Một phiên người chơi nạp 1 món cho bot: hex + thứ tự 37/45/46/58/8/9 | §5 |
| M2 | Bot giao 1 món: server có gửi gói xoá ô cho bên giao không | R10 |
| M5 | Khoảng cách tối đa để mời: 40 / 60 / 100 / 150 px | R5 |
| M7b | Bot mở rương ở sát Thủ khố bằng `-30/-103 {4}`; **số ô rương**; 16/17 chạy thật | §6 |
| M8 | Tách chồng: chỉ −85, hay 22 + −85 | R3 |
| M9 | Gửi 46 sau 1,5 giây có được không | N7, R9 |
| M10b | Độ dài tối đa của tin chat riêng | §9 |
| M12 | Khoảng 12 acc cùng một khu có bị đẩy sang khu khác không | §4 |
| M13 | Tự đánh (gói 61, charId của mình) ở làng: server phản hồi gì; đứng 2 giờ có rớt không | D23 |
| M18 | **Nguyên văn các câu server báo**: không đủ hành trang (T3), đang có giao dịch khác (T5), mời liên tục (T4) — lấy từ log gói −24/−25/−26/53 | R11, D41 |

Kết quả ghi vào `GIAO_DICH.md` §9, kèm hex.

### P1 — Nạp vào Leader + sổ kho (chỉ túi) + chat

- **Làm:** luồng §5 (gồm `nap` chen ngang / giữ cửa, kiểm trần xu); cài đặt §4; sổ kho phần túi; §9.1–9.3 (tem, **bỏ dấu**, rao thông minh); cột lưới; **log theo ngày §8.1**; chặn dùng đồ (D38).
- **Xong khi:**
  - Leader ở khu chính, 3 clone ở khu phụ, chạy **24 giờ** không rớt hẳn;
  - có cài toạ độ → Leader luôn về đúng chỗ, kể cả sau khi đi Thủ khố hoặc đăng nhập lại; xoá toạ độ → Leader đứng yên tại chỗ;
  - nạp bằng tay **và** bằng `gdvp` tổng 50 món + xu, qua ≥5 lượt; sổ kho khớp túi thật 100%;
  - `TatCa` (mặc định): người lạ nạp được và không nhận tin riêng nào; Chủ kho nhắn `nap` thì phiên người lạ bị huỷ, Chủ kho nạp được trong 60 giây. `ChiChuKho`: người lạ bị từ chối;
  - mọi tin gửi đi có tem **và không có dấu**; câu rao đổi số liệu đúng sau mỗi lượt nạp;
  - qua nửa đêm thì log sang thư mục ngày mới; mọi phiên đều có dòng trong `giaodich.csv`.

### P2 — Rút (tool + chat) + điều phối + tách chồng

- **Làm:** luồng §7; §9.4; hàng chờ; điều phối trên tool với người nhận bất kỳ; **giữ chỗ (D34) ngay từ đầu**; mời lại 31 giây; tạm dừng / `tiep` khi người nhận thiếu ô.
- **Xong khi:**
  - rút được: 1 món, cả chồng, một phần chồng, món nằm rải ở 3 clone;
  - giao được cho một người **không** phải Chủ kho (một lần ra lệnh từ tool, một lần bằng `lay … cho <tên>`);
  - mỗi trường hợp chạy được với cả người bấm tay lẫn `nhan`;
  - người nhận thiếu ô → lệnh tạm dừng đúng lý do, `tiep` chạy tiếp được;
  - hai Chủ kho **cùng xin món chỉ đủ cho một người** → lệnh sau nhận báo *"dang giu cho lenh khac"*.

### P3 — Dọn kho + rương + kệ hàng + kệ Rác + dồn xu

- **Làm:** §6 đầy đủ; kệ hàng §6.1; kệ Rác + nhả / nhận lại clone §6.2; sổ kho gồm rương; `{tong}` tính cả rương; dồn xu.
- **Xong khi:**
  - nạp liên tục 100 món mà không lượt nào bị từ chối quá 1 phút; sau khi dọn, túi Leader về trống; sổ kho khớp;
  - món đi đúng kệ; kệ đầy thì tràn sang "Khác" và có log `KE_TRAN`; rác chỉ vào kệ Rác;
  - nhả clone → user dọn tay → nhận lại → sổ kho cập nhật đúng; không bị đá acc;
  - Leader vượt `XuNguong` thì xu được dồn sang clone, không clone nào vượt trần.

### P4 — Hoàn thiện

- Leader dự phòng tự thay; lệnh `tim`; cache tên món; cảnh báo đồ có hạn; trạng thái KHO ĐẦY.
- **Gói rút** (D35); **theo dõi món + báo cáo ngày** (D36).

### P5 — Auto mua bán (CHƯA ƯU TIÊN)

- Bảng giá; lệnh mua/bán; rao bán hàng; chuyển xu; kiểm tra hàng và xu của khách qua móc `kiemHangDoiPhuong`; rút xu.
- Mẫu: NSOCHIP `en.java` (`GIAO_DICH.md` §7.2).

## 14. Rủi ro

| # | Rủi ro | Mức | Bằng chứng | Cách chặn |
|---|---|---|---|---|
| 1 | **Đồ khoá không vào kho được.** "Vứt rác tùm lum" chỉ đúng với đồ không khoá. | Cao — giới hạn cứng | Client chặn (`ONLY_NO_LOCK`); `gdvp` lọc `!h` | Không có cách vượt. |
| 2 | `gdvp` đổ **mọi** món không khoá của acc chính. | Vừa | `Class_aj.b()` | Cất rương trước, hoặc dùng `gd <id>`. |
| 3 | ~~Bên nhận thiếu ô lúc chốt → mất đồ~~ | **Đã loại trừ** | T3: server đóng phiên ngay, không mất đồ | Bot vẫn tự kiểm để báo đúng lý do. |
| 4 | Người nhận thiếu ô khi rút. | Thấp | T3 | Server tự chặn; bot tạm dừng lệnh và báo (R11). |
| 5 | Rương **bằng bot** chưa từng chạy thật. | Vừa | `NSOLITEPRO/docs/WORKLOG.md:6665`; T8 chỉ là test tay | M7b ở P0b; có phương án lùi (§6). |
| 6 | Thêm bản lõi đóng băng thứ ba. | Vừa, lâu dài | 16/61 commit sửa lõi | `NGUON_GOC.md`; khi hỏng, việc đầu tiên là so lõi. |
| 7 | Một kho chỉ chạy trong một tiến trình. | Thấp | §11 | Ghi trong README. |
| 8 | **Chế độ `TatCa` là MẶC ĐỊNH** (user chốt sau khi đã được cảnh báo): người lạ đổ rác làm đầy kho, hoặc mở phiên rồi treo để giữ Leader bận. | Vừa | D16; T5 (Leader bận thì Chủ kho không mời được) | `ChoNguoiLa` = 45 giây; Chủ kho chen ngang bằng `nap`; KHO ĐẦY thì từ chối; `giaodich.csv` ghi tên người nạp; chuyển sang `ChiChuKho` bằng một ô cài đặt. |
| 9 | Bị khoá chat hoặc bị báo spam vì rao dày. | Vừa | T11 mới thử 3 phút | Tem `@NNN`; **rao thông minh là mặc định** (D31); nhịp rao cài được. |
| 10 | **Chat cộng đồng công khai tình trạng kho** cho mọi người trong khu. | Thấp | D19/D20 do user chọn | Tắt được bằng `RaoBat` / `BaoCongDong`. |
| 11 | Mốc hồi chiêu đổi khu của bot lấy lúc **gửi lệnh**, còn game đếm từ lúc **tới nơi**. | Thấp | `Navigator.cs:1336`; D28 | Lấy mốc lúc nhận MAP_INFO; bộ chọn ưu tiên clone đã hết hồi chiêu. |
| 12 | Tự đánh ở làng có thể không tạo lưu lượng như mong đợi. | Vừa | Chưa đo ở làng | M13; `TickDocIm` (gói 93) vẫn giữ luồng đọc. |
| 13 | Sổ kho lệch sau sub 115. | Vừa | `SERVER_FACTS.md:127` | Ghi theo nội dung; đối chiếu sau mỗi phiên. |
| 14 | **Log giữ hết** (`GiuLogNgay = 0`) và chat rao → đĩa VPS đầy dần. Dung lượng mỗi ngày **chưa đo**. | Thấp → Vừa | D26 | Sau 1 ngày chạy P1 thì đo; nếu lớn thì đề xuất đặt `GiuLogNgay`. `LogHexGiaoDich` tắt được. |
| 15 | ~~Đồ nhận qua giao dịch bị khoá~~ | **Đã loại trừ** | T0 | — |
| 16 | **Bot lỡ dùng hoặc mặc đồ trong kho** → món bị khoá vĩnh viễn, không giao được nữa. | **Cao** nếu xảy ra | T0: đồ khoá khi đem dùng | Bỏ `BinhMauRunner`; `KhoMode` chặn gói dùng / mặc; một ca kiểm tra bảo đảm không đường nào gửi các gói đó (D38). |
| 17 | **Kệ hàng lệch**: kệ đầy, hoặc tool đoán sai nhóm món. | Thấp | D32 | Tràn sang "Khác" kèm log `KE_TRAN`; chỉ đoán theo 3 khoảng `Type` đã có nguồn; user sửa được. |
| 18 | **Nhả clone**: user đăng nhập tay trong lúc bot còn giữ acc (hoặc ngược lại) → hai bên đá nhau, bot tự đăng nhập lại đè lên user. | Vừa | Server đá phiên khi đăng nhập trùng | Nhả = đăng xuất và **tắt tự đăng nhập lại** cho acc đó; "Nhận lại" hỏi xác nhận đã thoát client thường. |
| 19 | **Chưa biết nguyên văn câu server báo lỗi** → phân loại sai lý do huỷ phiên. | Vừa | T3: popup "lúc có lúc không" | Suy lý do **theo thời điểm** gói 57 đến (D39), không dựa vào chữ; gom câu chữ ở M18. |
| 20 | Tên nhân vật hoặc tên món có ký tự bỏ dấu không hết → tin bị server chặn. | Thấp | T10: phải không dấu | `BoDau` + thay ký tự lạ bằng `?` + ghi log. |

## 15. Câu hỏi còn mở

**Không còn câu hỏi mở.** Spec đã chốt (v1.1).

- **Số phiên bản .NET của VPS:** user không có. Chốt dùng trọn 4.5.2 (D49); thiếu runtime thì cài .NET 4.8 (§3.1 luật 3).
- **P0b:** chờ user ra lệnh. Việc đầu tiên khi bắt đầu:
  1. tách khung từ NSOBAOTATL `7715bcf` (kèm `NGUON_GOC.md`);
  2. viết `CLAUDE.md` + `docs/STATUS.md` theo khuôn họ NSO;
  3. rồi mới viết `TradeService` / `TradeHandler` và các nút đo.

**User đã xác nhận (vòng 6):** T13 = bỏ hẳn tự dùng túi vải (D44) · T8 = rương là NPC 5 theo mục "Thủ khố" của MODGAME (D42) · toạ độ Leader cài được, không cài thì tự do (D48).

**Đã xử lý:** NSOBAOTATL đã commit `7715bcf` (nhãn "CHUA TEST") và push lên `github.com/tungnv27/NSOBAOTATL`. Spec này nằm ở `github.com/tungnv27/NSOKHODO`.
