# Giao dịch giữa hai người chơi — hợp đồng giao thức

> **Tra cứu ngày 2026-09-16.** Đã có **test tay** trên server chính (§5.1, §9). **Chưa đo bằng bot** (P0b). Kết quả đo nằm ở §9.
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
| **[TAY]** | user test tay trên server chính bằng client thường (2026-09-16, `TEST_TAY.md`) |

## 1. Kết luận

- **Wire giao dịch giống hệt nhau ở ba bản 148, 180 và 251** [CODE]. Bản 217 cũng khớp cả tám gói [BC].
- Mỗi bên **khoá một lần** (gói 45: đặt đồ, xu và khoá trong **một** gói), rồi **đồng ý một lần** (gói 46). Không có bước đồng ý lần hai, không có gói mở khoá. Muốn đổi món thì phải huỷ (57).
- **Tối đa 12 ô mỗi lượt.** Mỗi ô đi **nguyên chồng**; muốn giao một phần chồng thì tách trước (§6).
- **Chỉ chuyển được xu.** Không có trường nào cho yên hay lượng.
- Chuỗi gói này đã chạy thật trên server chính qua mod Giao/Nhận đồ của NSOTRUNGDUC (user xác nhận 2026-06-23) [CHẠY — trên client 217, **không** phải trên bot C#].
- **Test tay 2026-09-16** [TAY] xác nhận các điểm cho phép làm kho:
  - giao dịch **không khoá** món;
  - bên nhận thiếu ô thì server tự huỷ phiên, **không mất đồ**;
  - cấp 1 giao dịch được;
  - đồ có hạn giao được.
  Chi tiết ở §5.1.

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

### 5.1 Luật phía server chính — đo bằng test tay [TAY]

| Luật | Kết quả | Test |
|---|---|---|
| Giao dịch có khoá món không | **Không.** Món chỉ bị khoá khi **đem ra dùng** | T0 |
| Bên giao khoá vượt số ô trống của bên nhận | **Server đóng phiên ngay**, không mất đồ. Lúc có lúc không hiện popup kiểu "đối phương không đủ hành trang" | T3 |
| Món có hạn sử dụng | Giao được | T2 |
| Cấp tối thiểu | Cấp 1 giao dịch được | T1 |
| Mời liên tục | Lời mời khoá **31 giây**. Phiên được đồng ý xong thì **mời lại được ngay** | T4 |
| Mời người đang giao dịch | Server báo **"Người chơi đang chờ hoàn thành một giao dịch khác."**; lời mời không tới, **không** bị khoá — 3 s sau mời lại được (M27) | T5 |
| Một bên rời khu / thoát trong lúc đã khoá | Phiên tự huỷ, đồ còn nguyên | T6 |
| Giao dịch một chiều (một bên khoá rỗng) | Được | T7 |
| Trần xu mỗi nhân vật | **2 tỷ** | T14 |
| Số lượng **một ô** khi giao dịch | **≤ 29.999**; ô 30.000 trở lên server không cho giao dịch. Một chồng trong túi / rương gộp được tới **32.000** → chồng lớn phải tách trước (D88) | M30 [USER] |
| Chat riêng tới acc chưa kết bạn | Nhận được, nhưng **phải gửi không dấu** | T10 |
| Chat khu 5 giây/lần có tem `@NNN` ở đầu | Không bị chặn (thử 3 phút) | T11 |
| Bán đồ cho NPC | **Không bán được gì** | T12 |

Câu chữ chính xác của các thông báo trên **chưa có** — bot gom ở P0b (M18).

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
| Tách chồng | **chỉ** `-28 {sub -85, byte slot, int qty}`, 1 ≤ qty < số trong ô | túi cập nhật (8/7) | `ItemService.SendSplitStack` (NSOKHODO). ⚠ Dòng cũ ghi "`22 {byte slot}` rồi −85" là **SAI**: cmd 22 là **tách trang bị** (SPEC D53) — đã gỡ khỏi `ItemService` |

**Lưu ý:**
- **NPC mở rương là template 5.** Nguồn: mod 180 `AutoDanhVong.java:990-996` và `LITE\Auto\DanhVong\DanhVongConst.cs:46`.
- ✅ **Mở rương KHÔNG cần menu NPC** (tra 2026-09-16 theo gợi ý T8 của user): mục menu "Thủ khố" của MODGAME (`GameScr.java:14356-14362`) làm đúng ba việc:
  1. tìm NPC template **5**;
  2. cách quá **22 px** theo trục ngang hoặc dọc thì đi tới sát NPC;
  3. gọi `d(4)`, tức `requestItem(4)` = `-30 {-103, 4}`, và chỉ gửi khi chưa có danh sách rương (`GameScr.java:13026-13031`).
- User xác nhận **đứng xa thì không cất được, phải sát NPC** [TAY]. ⇒ Bot đứng sát NPC 5 rồi gửi `-30 {-103, 4}` + 16/17.
- Vì không đi qua menu, **không còn phải lo** gói menu 29 dài khác nhau giữa hai bản (180: 3 byte; 251: 4 byte).
- **Số ô rương** do server gửi trong gói 31; client không ghi cứng con số nào.
- **Rương theo từng nhân vật**, không chia sẻ giữa các tài khoản [SUY].
- **Tách chồng:** NSOCHIP Auto Sell chỉ gửi −85 → **NSOKHODO chỉ gửi −85** (D53). Bước "22" mà `LITE` gửi trước là lệnh tách **trang bị**, không thuộc việc tách chồng; M8 chỉ còn kiểm −85 chạy thật.
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
| Bán cho NPC (gói 14) | Server chính **không bán được gì** [TAY, T12]. |
| Gian hàng (102–105) | Chặn cả đồ khoá **lẫn** đồ có hạn; phí 5.000 xu mỗi lần bán + thuế 5% (`mResources.cs:1803`, `:1815`). Chỉ hợp để chuyển xu, không hợp làm kho. |
| Kho gia tộc | Thành viên **không gửi đồ vào được**; chỉ "phát" ra cho thành viên (`-28 {-61}`). |
| Thư / quà | Không tồn tại trong client 251. |
| Gói 126 | Nộp đồ cho NPC/sự kiện, không có người nhận. |

## 9. Kết quả đo trên server chính

Kế hoạch đo: `SPEC.md` §13 (P0a test tay — ✅ xong 2026-09-16; P0b đo bằng bot — chưa làm). Kết quả P0b ghi **vào bảng này**, kèm hex.

| Mã | Câu hỏi | Kết quả |
|---|---|---|
| M1 | Khuôn gói 37/45/46/58/8/9 với `clientType 1 / "1.8.0"` | ✅ Khớp §3 [CHẠY 16/09]. Bên nhận: `45` (xu, n, n × {tpl short, [cấp], hạn, sl short}), `46`, rồi mỗi món một gói `8` (`ô, tpl, khoá, hạn, [sl short nếu xếp chồng]`), `58`, `57`. Ví dụ: `8 len=7 00 03 14 00 00 00 6A` = ô 0, Nham Thạch (788) × 106 |
| M2 | Server có gửi gói xoá ô cho bên giao không | ✅ **Không.** Bên giao chỉ nhận `45` (đối phương khoá 0 món), `46`, `58`, `57` [CHẠY 16/09] → bot phải tự xoá ô đã đưa (`PhienGiaoDich.XoaODaDua`) |
| M3 | Đồ có hạn dùng giao dịch được không | ✅ **Được** [TAY, T2] |
| M4 | Bên nhận thiếu ô → server huỷ phiên hay mất đồ | ✅ **Server đóng phiên ngay** khi bên giao khoá vượt số ô trống; **không mất đồ**; popup "không đủ hành trang" lúc có lúc không [TAY, T3] |
| M5 | Khoảng cách tối đa để mời | ◐ **Server có kiểm:** mời từ xa ~1.100 px → *"Khoảng cách quá xa không thể giao dịch"*, lời mời không tới [CHẠY 16/09]. Ngưỡng chưa đo; đứng cách ≤ 40 px thì luôn được. Bot gặp câu này thì lại sát rồi mời lại (≤ 3 lần) |
| M6 | Mời liên tục có bị chặn không; phải chờ bao lâu | ✅ Khoá **31 s**; phiên xong thì mời lại được **ngay**; người đang giao dịch → "đối phương đang có giao dịch khác" [TAY, T4, T5]. Mời lại trong 30 s (sau một lời mời **đã tới**) → *"Bạn đã gởi yêu cầu giao dịch. Sau 30 giây nữa mới được gởi tiếp"*; lời mời **bị từ chối** (M19) thì không bị khoá [CHẠY 16/09]. Bị **bot từ chối** (gói 56) thì **có** khoá; phiên **đã mở rồi huỷ** thì mời lại ngay (M28) |
| M7 | Rương ở map 22: NPC, số ô, có cần đứng gần NPC không | ✅ NPC **Thủ khố** (= NPC 5 theo MODGAME), **phải đứng sát**; mở không cần menu (§6) [TAY, T8]. **Nhân vật cấp 1: rương 30 ô** (gói `31`: `… 1E` + 30 ô) [CHẠY 16/09]. Trả lời chuyển món: cất `17 {ô túi, ô rương}`, lấy `16 {ô rương, ô túi}` |
| M8 | Tách chồng bằng `−28/−85` có chạy không; phần tách rơi vào ô nào. *(Câu cũ "hay hai bước 22 + −85" SAI: cmd 22 là **tách trang bị**, phá món đã nâng cấp — SPEC D53. Bot không bao giờ gửi cmd 22.)* | ✅ **Chạy.** Tách 10 từ chồng 106 ở ô 0 → phần tách nằm ở **ô trống đầu tiên** (ô 1), kết quả về trong < 0,2 s [CHẠY 16/09] |
| M9 | Gửi 46 sau 1,5 s có bị server từ chối không | ✅ **Không** — mọi phiên (nạp, dọn, rút) đều xong ~0,2 s sau 46 [CHẠY 16/09] |
| M10 | Chat riêng: độ dài tối đa, có cần kết bạn không, ngưỡng khoá chat | ◐ **Không cần kết bạn**; **phải gửi không dấu** [TAY, T10]. **Độ dài tối đa: chưa có** → M10b (P0b) |
| M11 | Cấp độ tối thiểu để giao dịch | ✅ **Cấp 1 giao dịch được** [TAY, T1] |
| M12 | Khoảng 12 acc trong một khu có bị đẩy sang khu khác không | ✅ **Không** — 11 acc của kho + 3–6 người lạ cùng ở khu 0 Làng Tone, không ai bị đẩy [CHẠY 16/09]. Nhưng **khu đầy thì không vào được**: lúc 18:56 các acc đăng nhập bị xếp vào khu 21–22 (khu thấp đã đầy) |
| M13 | Tự đánh (gói 61, charId của mình) ở làng: server phản hồi gì; đứng 2 giờ có rớt không | ◐ Nhân vật **không cầm vũ khí** → server đáp *"Vũ khí không thích hợp"* mỗi 60 s (không có đòn nào). Vẫn là luồng hai chiều nên giữ được kết nối: đứng ~1 giờ không rớt [CHẠY 16/09]. **Chưa thử 2 giờ** |
| M14 | Chat cộng đồng (−23) 5 s/lần: có bị chặn hoặc khoá không | ◐ Có tem `@NNN` thì **không bị** (thử 3 phút) [TAY, T11]. Chưa thử dài |
| M15 | Đổi khu ở map 22: có NPC 13 không | ✅ **Có NPC 13** [TAY, T9]. Hồi chiêu 10 s đếm từ lúc tới khu mới (user) |
| M16 | Trần xu mỗi nhân vật | ✅ **2 tỷ** [TAY, T14]. Nhận vượt trần thì sao: chưa thử (bot kiểm trước để không bao giờ vượt) |
| **M17** | **Món nhận qua giao dịch có bị chuyển thành KHOÁ không** | ✅ **Không** — món chỉ khoá khi **đem ra dùng** [TAY, T0] |
| M18 | Nguyên văn các câu server báo (không đủ hành trang, đang có giao dịch khác, mời liên tục) | ✅ Thiếu ô (bên giao thấy): *"Đối phương không đủ ô trống để chứa vật phẩm giao dịch"* [TAY, ảnh user 16/09]. Mời lại sớm: *"Bạn đã gởi yêu cầu giao dịch. Sau 30 giây nữa mới được gởi tiếp"*. Người nhận mới vào game: *"&lt;tên&gt; do not accept."* (tiếng Anh). Xa: *"Khoảng cách quá xa không thể giao dịch"*. Vào lại quá sớm: *"Bạn chỉ có thể vào lại game sau N giây nữa"* (N ≈ 22 sau khi vừa thoát) [CHẠY 16/09]. Người được mời đang giao dịch: *"Người chơi đang chờ hoàn thành một giao dịch khác."* [CHẠY 17/09] |
| **M19** | **Mời người vừa vào game** | ✅ Người **nhận** vào game chưa tới **~55–60 s** → server trả *"&lt;tên&gt; do not accept."* ngay, **không chuyển** lời mời (đo: +46 s, +52 s bị chặn; +58 s tới). Người **mời** vừa vào game (+1 s, +19 s) **không** bị chặn. **Đổi khu không** bị chặn (+6 s sau khi tới khu mới vẫn nhận) [CHẠY 16/09]. → Bộ điều phối chờ clone vào game ≥ 90 s mới cho nhận hàng; phiên giao gặp câu này thì mời lại sau 10 s |
| **M20** | **Khu lúc đăng nhập** | ✅ Server **tự xếp khu** lúc vào game (khu thấp nhất còn chỗ), **không** giữ khu lúc thoát: 16/09 18:56 vào khu 21–22, 20:17 cả 9 acc vào khu 0 [CHẠY]. → Clone phải tự về khu phụ sau mỗi lần đăng nhập |
| **M21** | **Toạ độ người khác nhìn thấy** | ✅ Acc đứng y=216 nhưng người khác thấy y=164 (= 216 − 52, đúng bước nhảy chống AFK) [CHẠY 16/09]. Nguyên nhân + cách chữa: M23. → Khi tới sát người nhận, bot giữ độ cao của mình nếu lệch ≤ 60 px (D67) |
| **M22** | **Cờ "có hạn" của cùng một món** | ✅ **Không ổn định giữa các gói.** Người lạ đưa 5 Tử tinh thạch trung cấp (456): khung giao dịch (37/45) và gói `8` ghi **4 món "không hạn" + 1 "có hạn"**; acc nhận **đăng nhập lại** → danh sách túi ghi **cả 5 "có hạn"**. Kho đọc mới (túi lúc đăng nhập + gói 31) thì 61/61 món 456 đều "có hạn", trong khi app của user 4 phút trước ghi 47 món "không hạn". Món clone giao đi (đã "có hạn" từ danh sách) thì gói giao dịch vẫn ghi "có hạn" [CHẠY 17/09]. → Cờ hạn **không thuộc khoá món** (D72) |
| **M23** | **Server xử lý gói di chuyển (cmd 1) thế nào** | ✅ Đo bằng hai acc (một acc gửi, một acc đứng xem toạ độ qua cmd 1) [CHẠY 17/09, Làng Tone khu 0, đứng y=216]: **(a)** một gói lệch đơn lẻ (y−52, hoặc x±24) → người xem thấy ngay, **~1,3 s sau server phát lại vị trí cũ** (không nhận). **(b)** chuỗi chống AFK 4 gói cách 250 ms (y−10, −52, −40, 0) → người xem thấy 216 rồi **~1,2 s sau bật về 164 và giữ luôn**; người vào khu sau cũng thấy 164 (4/4 lần). Gửi lại 216 **một gói** sau đó (7 s, hay 1,5 s) → **vẫn 164**. **(c)** đi bằng `CharBurstMove` (gói đích gửi **3 lần**, lần 1–2 cách 20 ms) → server nhận, kể cả khi đang kẹt 164. **(d)** chuỗi 4 gói + **3 gói về chỗ cũ** (sau 50 ms hoặc 400 ms) → đứng đúng 216 (5/5, kể cả đang kẹt); Leader chạy bản sửa trên kho thật: 3/3 nhịp đúng. Server **không** gửi gói kéo vị trí nào cho chính acc gửi. → D77 |
| **M24** | **Mời ngay sau khi đổi khu** | ✅ Clone vào khu 5 lúc 18.1 s, mời lúc 19.6 s (1,5 s sau) theo toạ độ người nhận nó đang thấy (điểm vào 420) trong khi người nhận đã đi tới 300: khung mở, hai bên khoá, **cùng đồng ý (46) rồi server huỷ** (cả hai nhận huỷ, không kèm câu chữ). Lần mời sau server báo *"Khoảng cách quá xa"*. Chờ **2,5 s** sau khi vào khu rồi mới lấy toạ độ → mời lúc +3,3 s, đạt ngay lần đầu [CHẠY 17/09]. → D79. Ghi thêm: phiên bị huỷ sau 46 **không** có câu báo "quá xa" — dễ bị xếp nhầm là "đối phương huỷ" |
| **M25** | **Nhận chồng sẽ tự gộp** | 🟡 User (test tay 17/09): người nhận chỉ còn 1 ô thì mỗi giao dịch chỉ đưa được **1** chồng, dù chồng đó sẽ gộp vào chồng sẵn có → server đòi đủ ô trống cho **từng** ô giao [TAY]. Nhận 10 chồng mà món đó đang ở **rương** người nhận → chiếm 10 ô túi, cất rương thì gộp vào chồng sẵn có (tungkhodo1 không tốn ô nào) [CHẠY 17/09]. → D81 |
| **M26** | **Byte cấp của thú cưỡi** | ✅ `251/Controller.cs`: danh sách túi đầy đủ (sub 115) đọc cấp cho `isTypeBody ∥ isTypeMounts ∥ isTypeNgocKham`; rương (gói 31, dòng 2138), thêm món (gói 8, dòng 901), khung giao dịch (45, dòng 1488) chỉ `isTypeBody ∥ isTypeNgocKham` [CODE]. Code bot đọc khớp từng gói → không lệch byte; chỉ khoá món lệch → D80 |
| **M27** | **Mời người đang giao dịch với người khác** | ✅ Server trả *"Người chơi đang chờ hoàn thành một giao dịch khác."*, lời mời **không tới**. Mời lại sau **3 s** và **7 s** (người kia đã rảnh) → **tới ngay**, không có câu khoá 30 s [CHẠY 17/09 11:02, ba acc tungkhodo7/8/9]. → Phiên giao dịch mời lại sau 3 s (D87); người chơi mời trúng lúc Leader đang chuyển đồ chỉ cần bấm lại |
| **M28** | **Bị từ chối và nhận-rồi-huỷ khác nhau thế nào** | ✅ **Bot từ chối** (gói 56): người mời mời lại sau 4 s và 9 s đều nhận *"Bạn đã gởi yêu cầu giao dịch. Sau 30 giây nữa mới được gởi tiếp"*; 37 s sau mới tới [CHẠY 17/09 11:18]. **Phiên đã mở rồi bên nhận huỷ** (57): mời lại sau 1 s, 3 s, 7 s đều tới [CHẠY 11:01–11:03]; Leader nhận rồi huỷ ngay khi khung mở → người chơi mời lại sau 3 s tới [CHẠY 11:37]. → D87 |
| **M29** | **Chỗ đứng cạnh Leader ở Làng Tone khu 0 + tốc độ bot↔bot** | ✅ Leader đứng (365, 216). **x = 335 không phải đất** (mép tầng): clone đi tới (335, 216) → server kéo xuống (335, 288) (`[Pos] cmd52 … lech 0,72`), người khác vẫn thấy (335, 216); Leader mời 4 lần đều *"Khoảng cách quá xa"* (hai lần chạy 11:14, 11:30). **x = 395** cùng tầng: mọi lượt đạt. Clone đi ngay ~1 s sau khi vào khu thì có lúc cả khu vẫn thấy nó ở điểm vào (420) trong khi nó tưởng đã tới 395. Giao dịch bot↔bot 12 món: **2–3 s** (11 lượt) [CHẠY 17/09]. → D86: chỗ đứng xét cờ đất của ô bản đồ, chờ 2,5 s rồi mới đi, Leader lại gần theo toạ độ chính nó thấy |
| **M30** | **Số lượng tối đa một ô khi giao dịch** | 🟡 User (17/09): **29.999** giao dịch được, **30.000** thì game không cho; chồng xếp trong túi / rương gộp tối đa **32.000**. Client 251 không có luật này (không có hằng 29999 / 30000 / 32000 nào liên quan) → server chặn. Server báo câu gì khi đặt ô 30.000: **chưa đo** (kho chưa có chồng nào quá 146). → D88 |

## 10. Sai sót tài liệu đã phát hiện (chưa sửa ở nguồn)

| File | Sai gì |
|---|---|
| `NSOTRUNGDUC\docs\mods\giao-nhan-do.md:59-63`, `:107-108` | Nghĩa của `j()` / `dd` / `db` — xem §7.1 |
| `D:\10\NSOTool\docs\reverse-engineering\nso251\item_inventory.md:218-240` | Ghi gói 43 (S→C) có `readUTF` và gói 45 có `isLock`. Code thật của cả ba bản đều **không** đọc hai trường này. |
| `NSOLITEPRO\docs\reference\SERVER_FACTS.md:982` | Ghi "Rương (cmd 4)". Thực ra là **typeUI 4**; danh sách rương đến bằng **gói 31**, còn cmd 4 là `PLAYER_ATTACK`. |
| `NSOLITEPRO\docs\reference\SERVER_FACTS.md:213` | Ghi "id đọc từ stream" cho item template; code đang chạy thật lại dùng **chỉ số vòng lặp**. |
