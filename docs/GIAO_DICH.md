# Giao dịch giữa hai người chơi — hợp đồng giao thức

> **Tra cứu ngày 2026-09-16, CHƯA đo trên server chính.** Mọi dòng "phải đo" nằm ở §9.
> Sửa file này **tại chỗ** khi có kết quả đo mới, kèm hex.

## 0. Nguồn và nhãn

**Thứ tự tin cậy** (giống NSOLITEPRO `docs/reference/SOURCE_PROJECTS.md` §0):

```
SERVER_FACTS (đo hex) → NINJAPC 251 → MODGAME 180 → NSOTRUNGDUC 217 / NSOCHIP 148
```

⛔ **KHÔNG dùng `E:\srcnso`.** Đó là source server lậu; user chỉ chơi bản chính (chốt 2026-09-16).

| Viết tắt | Đường dẫn |
|---|---|
| `251` | `C:\Users\acer\Desktop\NINJAPC\NinjaSchool_251_src\` — client chính chủ của đúng server này |
| `180` | `C:\Users\acer\Desktop\MODGAME\src\` |
| `217` | `C:\Users\acer\Desktop\NSOTRUNGDUC\src\` (jar gốc: `217_trungduc3.jar`) |
| `148` | `C:\Users\acer\Desktop\NSOCHIP\src_frozen\` |
| `LITE` | `D:\10\NSOLITEPRO\src\NSOLITEPRO\` |

**Nhãn mức chắc chắn:**

| Nhãn | Nghĩa |
|---|---|
| **[CODE]** | đọc thấy trong code decompile |
| **[BC]** | đọc bytecode (`javap -c`) |
| **[CHẠY]** | đã chạy thật trên server chính |
| **[SUY]** | suy luận |
| **[CHƯA ĐO]** | chưa kiểm chứng |

## 1. Kết luận

- **Wire giao dịch giống hệt nhau ở ba bản 148, 180 và 251** [CODE]. Bản 217 cũng khớp cả tám gói [BC].
- Mỗi bên **khoá một lần** (gói 45: đặt đồ, xu và khoá trong **một** gói), rồi **đồng ý một lần** (gói 46). Không có bước đồng ý lần hai, không có gói mở khoá. Muốn đổi món thì phải huỷ (57).
- **Tối đa 12 ô mỗi lượt.** Mỗi ô đi **nguyên chồng**; muốn giao một phần chồng thì tách trước (§6).
- **Chỉ chuyển được xu.** Không có trường nào cho yên hay lượng.
- Chuỗi gói này đã chạy thật trên server chính qua mod Giao/Nhận đồ của NSOTRUNGDUC (user xác nhận 2026-06-23) [CHẠY — trên client 217, **không** phải trên bot C#].

## 2. Mã lệnh

Nguồn: `251\Cmd.cs:441-483`.

| Mã | Tên 251 | Chiều |
|---|---|---|
| 37 | OPEN_UI_TRADE | S→C |
| 43 | TRADE_INVITE | C→S, S→C |
| 44 | TRADE_INVITE_ACCEPT | C→S |
| 45 | TRADE_LOCK_ITEM | C→S, S→C |
| 46 | TRADE_ACCEPT | C→S, S→C |
| 56 | TRADE_INVITE_CANCEL | C→S |
| 57 | TRADE_CANCEL | C→S, S→C |
| 58 | TRADE_OK | S→C |

- Không có sub-command cho giao dịch.
- **Trong `LITE` hiện chưa có opcode nào ở trên.** Các gói 37, 43–46, 56–58 đang rơi vào nhánh "cmd CHUA XU LY" (`LITE\Controller\MessageRouter.cs:677-685`).

## 3. Wire

### 3.1 Client gửi

| Gói | Payload | 251 | 217 |
|---|---|---|---|
| 43 mời | `int charId đối phương` | `Service.cs:1535` | `Class_fp.s(int)` :1492 |
| 44 nhận lời | `int charId người mời` | `Service.cs:1072` | `Class_fp.l(int)` :971 |
| 45 khoá | `int xu, byte n, n × byte viTriTúi` | `Service.cs:1221-1254` | `Class_fp.a(int, Class_dh[])` :1140 |
| 46 đồng ý | rỗng | `Service.cs:1203` | `Class_fp.j()` :1119 |
| 56 từ chối lời mời | rỗng | `Service.cs:1167` | `Class_fp.h()` :1083 |
| 57 huỷ | rỗng | `Service.cs:1185` | `Class_fp.i()` :1101 |

- Gói 45 **có byte đếm** và **không có số lượng**, vì mỗi món là cả chồng.
- Khoá rỗng `45 {0, 0}` là hợp lệ: nút "Khóa G.Dịch" luôn hiện (`251\GameScr.cs:10674-10677`). Lệnh `nhan` của 217 dùng đúng gói này [BC].
- ⚠ **217 có hàm trùng tên dễ nhầm:** `h(int)` là gói 40; `i(int)` và `j(int)` là sub −103 và −105. Chúng khác `h()`, `i()`, `j()` không tham số.

### 3.2 Server gửi

| Gói | Payload | Client làm gì | Nguồn |
|---|---|---|---|
| 43 | `int charId người mời` | Tìm người mời trong danh sách nhân vật **cùng khu**. **Không thấy → bỏ qua im lặng.** | `251\Controller.cs:1193-1203`; 217 `Class_by` @13009 [BC] |
| 37 | `UTF tên đối phương` | Mở khung; đặt lại mọi trạng thái; tạo 2 mảng 12 ô | `251\Controller.cs:1884`; 217 @12288 |
| 45 | `int xu, byte n`, rồi n × `{short tpl, [byte upgrade], bool isExpires, short qty}` | Đối phương đã khoá. **Không có `isLock`.** | `251\Controller.cs:1474-1500`; 217 @13227 |
| 46 | rỗng | Đối phương đã đồng ý | `251\Controller.cs:1466`; 217 @13491 |
| 57 | rỗng | Huỷ: trả đồ và xu về local, **client gốc gửi lại 57**, đóng khung | `251\Controller.cs:531`; 217 @14467 |
| 58 | `int xu mới` | Xong: xoá hai mảng, gán xu, báo "Bạn nhận được …". **Client gốc cũng gửi 57 ngay sau đó**, vì khung vẫn đang mở | `251\Controller.cs:536-555`, `GameScr.cs:3127-3130`; 217 @14485 |
| 44, 56 | — | **Không có case nào.** Bên mời không nhận gói riêng khi bị từ chối [CODE]; có thể server báo bằng chữ qua −24 [SUY] | `180\Controller.java:176-189` |

**Byte `upgrade` trong gói 45:** chỉ có khi `template.Type ∈ 0..15` hoặc `== 34` (`251\Item.cs:298-314`).
- Luật này **giống** gói 8 và 31 trong `LITE` (`IsTypeBody || IsTypeNgocKham`).
- Luật này **khác** sub −127/115 (`HasUpgrade`, có thêm thú cưỡi 29..33).
- ⇒ Bot **phải có bảng item template** thì mới đọc được gói 45.
- Nếu n > 12, client gốc tràn mảng. Bot nên chặn và coi là gói hỏng.

### 3.3 Đồ về túi sau giao dịch

- **Bên nhận:**
  - **8** = ô mới: `byte idx, short tpl, bool isLock, [byte upg], bool isExpires, ushort qty`;
  - **9** = cộng dồn: `ubyte idx, short qty`.
  - Các gói này đến **trước** gói 58 [SUY: handler 8/9 gom tên món vào chuỗi mà gói 58 in ra, và chỉ gom khi khung còn mở].
  - `LITE` đã có handler cho cả 8 và 9 [CHẠY].
- **Bên giao:**
  - client **tự xoá ô** ngay lúc chọn món; gói 58 xoá hẳn, gói 57 trả lại.
  - **Không thấy server gửi gói xoá ô** [SUY mạnh, CHƯA ĐO — M2].
  - Thêm một dấu hiệu: gói 10 (xoá ô) trong 217 gọi `u()`, và nếu nó đến giữa phiên thì sẽ huỷ luôn giao dịch.
- **Xu của cả hai bên:** lấy từ `int` trong gói 58.

## 4. Sơ đồ tuần tự: A giao ≤12 món cho B

```
A→S  43 {int idB}                    mời; lặp mỗi 5 s tới khi có 37
S→B  43 {int idA}                    B phải thấy A trong khu, nếu không thì bỏ qua
B→S  44 {int idA}                    (hoặc 56 {} = từ chối; A không nhận gói nào)
S→A  37 {UTF "B"} ; S→B 37 {UTF "A"} khung mở ở cả hai bên
B→S  45 {0, 0}                       B khoá rỗng ngay
S→A  45 {0, 0}
A→S  45 {0, n, n × idxTúi}           A khoá
S→B  45 {0, n, n × (tpl,[upg],hạn,qty)}
     … B kiểm n ≤ ô trống; không đủ → 57
     … chờ 1,5 s (217 Class_ap) — UI gốc 251 chờ 5 s
A→S  46 {}                           (217 Class_aj gửi 46 mà không chờ 45 của B — bot KHÔNG làm vậy)
S→B  46 {}
B→S  46 {}
S→A  46 {}
S→B  8/9 × k                         đồ vào túi B
S→A  58 {int xu} ; S→B 58 {int xu}   xong
A→S  57 {} ; B→S 57 {}               dọn phiên, giống client gốc
Huỷ: X→S 57 → S→Y 57 → Y trả đồ local, đóng khung, gửi lại 57
```

Nguồn: `217` [BC]; khớp `251` [CODE]. Thứ tự "8/9 trước 58" là [SUY].

## 5. Luật phía client

| Luật | Giá trị | Nguồn |
|---|---|---|
| Số ô mỗi lượt | ≤ 12; đầy thì báo "Vật phẩm giao dịch đã đầy" | `251\GameScr.cs:2309`, `:20738` |
| Món khoá | **Chặn** — "Chỉ được vật phẩm không khóa" | `251\GameScr.cs:20723-20746` |
| Món có hạn dùng | **Không chặn** (gian hàng mới chặn) | `251\GameScr.cs:20723-20746` so với `:19211` |
| Đồ đang mặc | Không chọn được, vì chỉ lấy từ túi | `251\GameScr.cs:20731` |
| Tiền | Chỉ **xu**, `0 < xu ≤ xu đang có` | `251\GameScr.cs:20468-20499` |
| Chờ trước khi Đồng ý | Nút chỉ hiện khi **cả hai đã khoá** và **đã qua 5 s** | `251\GameScr.cs:20706-20712`; 217 `Class_cw.java:7761-7766` |
| Khoảng cách để hiện menu Giao dịch | ngang < 60 px, dọc < 40 px | `251\GameScr.cs:2782-2783` |
| Đối phương | cùng khu; không chết; không phải phân thân; không đang ở lôi đài khi còn sống | `251\GameScr.cs:2840-2869`, `:20574`; `Controller.cs:1197` |
| Cấp độ | **Client không kiểm tra** | — |
| Timeout phiên | **Client không có** | — |

- **Mốc 5 giây có bị server ép không?** 217 `Class_ap` gửi 46 sau 1,5 s, mod Giao/Nhận gửi sau 1,2 s, và đều chạy OK. ⇒ [SUY] server không ép chặt mốc này. Cần đo lại (M9).
- **Chuỗi thông báo (nguyên văn, `251\mResources.cs`):**
  - `INVITETRADE` "mời bạn giao dịch. Bạn có muốn giao dịch không?"
  - `LOCK1` "Khóa G.Dịch"
  - `ACCEPT` "Đồng ý"
  - `SENDMONEY` "Chuyển xu"
  - `TRADE_FULL` "Vật phẩm giao dịch đã đầy"
  - `ONLY_NO_LOCK` "Chỉ được vật phẩm không khóa"
  - `NOT_ENOUGH_COIN_IN1` "Bạn không đủ xu để chuyển."
  - `RECEIVE` "Bạn nhận được"
  - `BAG_FULL` "Hành trang không đủ chỗ trống"
- **Lỗi từ server** (túi đầy, đối phương bận…) đến dưới dạng chữ qua −24/−25/−26/53. Client không chứa các câu này, nên **phải bắt câu thật khi đo**.

## 6. Túi, rương, tách chồng

| Việc | Gửi | Nhận | Trong `LITE` |
|---|---|---|---|
| Xin danh sách rương | `-30 {sub -103, byte 4}` | `31 {int xuRương, ubyte n, n × {short tpl; ≠ −1: bool lock, [byte upg nếu body‖ngọc khảm], bool exp, short qty}}` | `ItemService.cs:233`; `ItemHandler.cs:768` [CODE, **CHƯA CHẠY**] |
| Mở rương | `40 {short 5}` rồi `29 {…}` → chờ `30 {byte 4, …}` | — | `DanhVongMode.MoUiRuong` :1513 (NSOBAOTATL đã bỏ) |
| Túi → rương | `17 {byte bagIdx}` | `17 {ubyte bagIdx, ubyte boxIdx}` (ô đích có sẵn cùng món thì cộng dồn) | `ItemService.cs:170`; `ItemHandler.cs:808` |
| Rương → túi | `16 {byte boxIdx}` | `16 {ubyte boxIdx, ubyte bagIdx}` | `ItemService.cs:163`; `ItemHandler.cs:838` |
| Tách chồng | `22 {byte slot}` rồi `-28 {sub -85, byte slot, int qty}` | túi cập nhật (8/7) | `ItemService.SendSplitItem` :43, `SendSplitConfirm` :51 |

**Lưu ý:**
- **NPC mở rương là template 5.** Nguồn: mod 180 `AutoDanhVong.java:990-996` và `LITE\Auto\DanhVong\DanhVongConst.cs:46`.
- ⚠ **Gói menu 29 dài khác nhau giữa hai bản:**
  - 180: `byte npc, byte menu, byte option` (3 byte).
  - 251: `byte typeClose, byte npc, byte menu, byte option` (4 byte).
  - Bot khai `"1.8.0"`, gần dòng 180. **Phải đo (M7).**
- **Số ô rương** do server gửi trong gói 31; client không ghi cứng con số nào.
- **Rương theo từng nhân vật**, không chia sẻ giữa các tài khoản [SUY].
- **Tách chồng:** NSOCHIP Auto Sell chỉ gửi −85; còn `LITE` gửi hai bước theo 251. Chọn cách nào thì **đo M8** rồi quyết.
- **Sau khi tách không có gói nào nói phần tách nằm ở ô nào.** Phải so túi trước/sau; NSOCHIP làm y như vậy.

## 7. Mẫu có sẵn

### 7.1 NSOTRUNGDUC 217 — `gd` / `gdvp` / `nhan` [BC]

- **`gd <id>` và `gdvp`** → `Class_aj(tênNgườiĐangChỉ, id)`; `gdvp` truyền `id = −1` (`docs\reverse-engineering\decoded\decoded_Class_bw.java:1368-1383`).
- **`Class_aj`** — bên giao (`src\Class_aj.java:96-181`). Mỗi tick:
  - Hết món khớp → báo "Đã giao dịch hết vp" và dừng hẳn auto.
  - Tìm người nhận theo tên (phân biệt hoa thường, trong danh sách nhân vật cùng khu):
    - lệch > 50 px → tele trượt tới;
    - ngược lại → gửi 43 mỗi 5 s.
  - Khung mở → lấy `min(số món, 12)` → gửi 45 → chờ 1 s → gửi 46 → xoá ô local → chờ khung đóng.
  - **Lọc món:**
    - `gdvp` chỉ lấy món **không khoá**;
    - ⚠ `gd <id>` **KHÔNG lọc khoá**, chỉ so id;
    - không lọc hạn dùng.
  - **Không có timeout. Không kiểm tra đối phương đã khoá hay chưa trước khi gửi 46.**
- **`Class_ap`** — bên nhận (`src\Class_ap.java:33-63`):
  - nhớ map/khu, lệch thì tự quay về;
  - khung mở → gửi `45 {0, 0}` → chờ đối phương khoá → chờ **1,5 s** → gửi 46 → chờ khung đóng.
  - **Tự nhận lời mời nằm ở dispatcher gói 43:** khi task đang chạy là `Class_ap`, nó gửi 44 cho **bất kỳ ai** mời, không so tên.
- **Mod Giao/Nhận đồ** (`src\ModGnProto.java`, `ModGnBridge.java`, `ModGiaoNhan.java`, `tools\PatchTungvz.java`; tài liệu `docs\mods\giao-nhan-do.md`):
  - phối hợp hai acc qua chat riêng bằng tiền tố `#GN`;
  - các mốc thời gian: trong khung 60 s, giữa hai lượt 25 s, trước khi đồng ý 1,2 s;
  - bài học đã trả giá: **hai bên cùng gửi 43 KHÔNG thành "tự đồng ý"**, mà ra hai hộp thoại treo; phải gửi 44.
- ⚠ **`docs\mods\giao-nhan-do.md` ghi sai ba chỗ** (đối chiếu bytecode 2026-09-16):
  - `j()` là **Đồng ý (46)**, không phải "khoá";
  - `dd` là **xu mình đưa**, không phải con trỏ UI;
  - `db = 1` là đã khoá, `db = 2` là đã đồng ý.
  - Các chú thích sai tương tự còn ở `PatchTungvz.java:1269-1270` và `ModGnBridge.java:52`.

### 7.2 NSOCHIP 148 — "Auto Sell v4 By Leo" (`src_frozen\en.java`) [CODE+BC]

Mẫu gần nhất cho **auto mua bán** (giai đoạn P5).

- **Nhận lệnh** qua chat riêng: `buy <tên> <sl>`, `mua <sl> <tên>`, `donate`, `sell`.
- **Tự nhận lời mời:** khi task Auto Sell đang chạy, nhận 43 thì gửi 44 ngay; không có gì để mua thì gửi 56 và nhắn lại.
- **Chờ đồng bộ:** gói 37/46/58 đánh thức luồng chờ; gói 57 bật cờ huỷ. Hàm chờ **tối đa 20 s**.
- **Bán** (`d()` :312-477):
  1. tách chồng bằng −85;
  2. đi tới người mua, gửi 43, chờ 37;
  3. xoá ô local, gửi `45 {0, n, idx}`;
  4. chờ khách khoá; kiểm tra khách có đủ xu và khách **không** đặt món nào;
  5. chờ 6 s, gửi 46;
  6. lỗi → gửi 57 và `-30 {-107}` (sắp xếp túi để gộp lại phần đã tách).
- **Mua** (`c()` :186-310): chờ khách khoá trước → kiểm tra món và tính tiền → gửi `45 {giá, 0}` → chờ 5 s → gửi 46.

## 8. Các kênh đã loại

| Kênh | Lý do loại |
|---|---|
| Vứt rồi nhặt | Gói có món nằm trên map **không có trường chủ sở hữu** (`251\ItemMap.cs`); người khác nhặt được. Server có câu "Vật phẩm của người khác", nhưng bảo hộ cho **đồ vứt** thì không ai biết. Client chặn vứt đồ khoá. |
| Gian hàng (102–105) | Chặn cả đồ khoá **lẫn** đồ có hạn; phí 5.000 xu mỗi lần bán + thuế 5% (`mResources.cs:1803`, `:1815`). Chỉ hợp để chuyển xu, không hợp làm kho. |
| Kho gia tộc | Thành viên **không gửi đồ vào được**; chỉ "phát" ra cho thành viên (`-28 {-61}`). |
| Thư / quà | Không tồn tại trong client 251. |
| Gói 126 | Nộp đồ cho NPC/sự kiện, không có người nhận. |

## 9. Chưa biết — phải đo trên server chính

Kế hoạch đo chi tiết: `SPEC.md` §13 P0 (M1–M12). Ghi kết quả **vào bảng này**, kèm hex.

| Mã | Câu hỏi | Kết quả |
|---|---|---|
| M1 | Khuôn gói 37/45/46/58/8/9 với `clientType 1 / "1.8.0"` | — |
| M2 | Server có gửi gói xoá ô cho bên giao không | — |
| M3 | Đồ có hạn dùng giao dịch được không | — |
| M4 | Bên nhận thiếu ô → server huỷ phiên hay mất đồ | — |
| M5 | Khoảng cách tối đa để mời | — |
| M6 | Mời liên tục có bị chặn không; phải chờ bao lâu | — |
| M7 | Rương ở map 22: có NPC 5 không, gói 29 dài 3 hay 4 byte, số ô, có cần đứng gần NPC không | — |
| M8 | Tách chồng: một bước (−85) hay hai bước (22 + −85) | — |
| M9 | Gửi 46 sau 1,5 s có bị server từ chối không | — |
| M10 | Chat riêng: độ dài tối đa, có cần kết bạn không, ngưỡng khoá chat | — |
| M11 | Cấp độ tối thiểu để giao dịch | — |
| M12 | Khoảng 12 acc trong một khu có bị đẩy sang khu khác không | — |
| M13 | Tự đánh (gói 61, charId của mình) ở làng: server phản hồi gì; đứng 2 giờ có rớt không | — |
| M14 | Chat cộng đồng (−23) 5 s/lần trong 1 giờ: có bị chặn hoặc khoá không | — |
| M15 | Đổi khu ở map 22: có NPC 13 không, thời gian chờ thực tế | — |
| M16 | Trần xu mỗi nhân vật; nhận xu vượt trần thì sao | — |

## 10. Sai sót tài liệu đã phát hiện (chưa sửa ở nguồn)

| File | Sai gì |
|---|---|
| `NSOTRUNGDUC\docs\mods\giao-nhan-do.md:59-63`, `:107-108` | Nghĩa của `j()` / `dd` / `db` — xem §7.1 |
| `D:\10\NSOTool\docs\reverse-engineering\nso251\item_inventory.md:218-240` | Ghi gói 43 (S→C) có `readUTF` và gói 45 có `isLock`. Code thật của cả ba bản đều **không** đọc hai trường này. |
| `NSOLITEPRO\docs\reference\SERVER_FACTS.md:982` | Ghi "Rương (cmd 4)". Thực ra là **typeUI 4**; danh sách rương đến bằng **gói 31**, còn cmd 4 là `PLAYER_ATTACK`. |
| `NSOLITEPRO\docs\reference\SERVER_FACTS.md:213` | Ghi "id đọc từ stream" cho item template; code đang chạy thật lại dùng **chỉ số vòng lặp**. |
