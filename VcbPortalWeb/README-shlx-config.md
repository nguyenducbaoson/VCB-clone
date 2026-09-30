# Cài đặt SHLX qua ACQHUB

Cập nhật 30/09/2026. Đã chạy thật trên UAT, và bản có 5 cột user đã dựng + kiểm
bằng Angular 20 headless trước khi giao.

---

## 1. Ba file trong thư mục này

| File | Đặt vào `vcbportalweb` |
|---|---|
| `ishlx-config.component.ts` | `src/app/modules/acqhub/shlx-config/` |
| `ishlx-config.component.html` | cùng chỗ |
| `shlx-config.interface.ts` | `src/app/interfaces/` |

## 2. Màn hình làm gì

**Chỉ nhập bằng file Excel.** Không có form nhập tay.

```
Chon file .xlsx ─► luoi xem truoc ─► Confirm batch ─► ACQHUB cai terminal
                                                   └─► portal tao user
```

Hai việc, theo đúng thứ tự đó. Terminal nào ACQHUB **không** cài được thì
**không** tạo user cho nó — ngược lại là để lại một tài khoản đăng nhập được
nhưng không có dữ liệu nào phía sau, và lỗi đó không tự lộ ra.

## 3. Thứ tự cột Excel — CHỖ DỄ SAI NHẤT

Đọc **theo vị trí cột**, dữ liệu từ dòng 2. `label` trong `meta({ label })` chỉ
dùng để in thông báo lỗi.

| Cột | Tiêu đề | Bắt buộc | Kiểu ô |
|---|---|---|---|
| A | `TERMINAL_ID` | ✔ | text |
| B | `DD_ACCOUNT_NUMBER` | ✔ | **số** |
| C | `TERMINAL_NAME` | ✔ | text |
| D | `MERCHANT_ID` | ✔ | text |
| E | `PROVINCE` | ✔ | text |
| F | `BRANCH_CODE` | ✔ | text |
| G | `USER_NAME` | ✔ | text |
| H | `ROLE_ID` | — | số |
| I | `FULLNAME` | — | text |
| J | `EMAIL` | — | text |
| K | `MOBILE` | — | text |

⚠️ **Vị trí 5 cột G–K là GIẢ ĐỊNH của tôi**, chưa thấy file mẫu mới. File của bạn
xếp khác là dữ liệu vào sai ô mà **không lỗi nào báo** — cả 11 cột đều đọc bằng
`asText`. Sửa thứ tự trong `ezTable([...])` cho khớp, hoặc gửi ảnh dòng tiêu đề.

Bỏ trống thì: `ROLE_ID` → backend mặc định **28**; `FULLNAME` → backend lấy chính
`USER_NAME`.

`BRANCH_CODE` được đệm về 5 ký tự nếu Excel trả kiểu số (`1400` → `01400`), mọi
dòng bị đệm liệt kê ở chân dialog kèm `TERMINAL_ID` để đối chiếu file gốc.

⚠️ `DD_ACCOUNT_NUMBER` lưu dạng **số** trong template. Số tài khoản bắt đầu bằng 0
bị Excel cắt mất — cùng loại lỗi với `BRANCH_CODE` nhưng **không đệm lại được** vì
số tài khoản không có độ dài cố định. Chưa xử lý, chờ xác nhận quy tắc.

### `ROLE_ID` đọc bằng `asText` rồi mới đổi sang số — có lý do

Đọc thẳng bằng `ez.number()` thì ô trống thành `0`, mà `0` **không phải** "bỏ
trống": backend sẽ coi đó là ROLE_ID hợp lệ rồi từ chối cả dòng. Chuyển kiểu ở
bước `.map()` — nơi phân biệt được rỗng với số:

```ts
roleId: r[7] ? Number(r[7]) : null,
```

## 4. Bốn điều kiện chặn TRƯỚC khi gửi

Dòng nào vi phạm thì tô **đỏ**, nút Confirm bị khoá:

1. thiếu một trong 7 cột bắt buộc
2. `ROLE_ID` có giá trị nhưng không phải số
3. `USER_NAME` **trùng với dòng khác trong cùng file** (không phân biệt hoa thường)
4. — (ba điều trên là toàn bộ)

Điều 3 đáng nói: `MP_SHLX_USERS` khoá theo `USER_NAME` và giữ **một**
`TERMINAL_ID`. Hai dòng cùng `USER_NAME` thì chỉ dòng đầu tạo được user, dòng sau
nhận "đã tồn tại" rồi im lặng — terminal thứ hai cài xong mà không ai đăng nhập
được. Bắt trước khi gửi vì sau khi gửi thì ACQHUB đã ghi, không lùi được.

## 5. Gọi API

```ts
const res = (await this._requestService.post(
    environment.mainEndpoint + 'acqh/para/shlx?_=' + Date.now(),
    payload
)) as ShlxConfigResponse;
```

Route backend: `{tiền tố}/acqh/para/shlx`, method `AcqhParamController.ShlxConfig`.
Tiền tố do `[RouteGroup(RouteTags.Main)]` sinh ra nên Pilot tự thành `apivp`.

⚠️ Không có dấu `/` giữa `mainEndpoint` và chuỗi. Nếu `mainEndpoint` bên bạn
**không** kết thúc bằng `/` thì đổi thành `'/acqh/para/shlx?_='`.

Chia lô theo `environment.maxItemPerApi` — file 250 dòng thành 3 request.

## 6. NĂM cột user KHÔNG được gửi sang ACQHUB

Backend tách hai lớp: `ShlxUploadItem` (11 cột, Angular gửi lên) và
`ShlxConfigItem` (6 cột, đi vào trường `data` ký gửi ACQHUB).

Không phải chuyện sạch sẽ code. `ShlxConfigItem` bị base64 vào `data`; gộp hai lớp
là `EMAIL`, `MOBILE`, `USER_NAME` **rời khỏi hệ thống VCB** — và không có gì báo,
vì ACQHUB vẫn trả code 00. Repo khung có một bài test riêng cho điều này
(`Cot_user_KHONG_duoc_gui_sang_ACQHUB`): giải base64 trường `data` rồi kiểm không
có khoá nào trong 5 khoá đó.

## 7. Response

```json
{
  "code": "01",
  "message": "Failed|One or more SHLX items failed",
  "results": [
    { "terminal_id": "V827308199", "success": true,  "result_code": "00", "result_message": "..." },
    { "terminal_id": "V827308100", "success": false, "result_code": "01", "result_message": "... already exist ..." }
  ],
  "users": [
    { "terminal_id": "V827308199", "user_name": "SHLX_CUCHI", "created": true, "message": "" }
  ]
}
```

`code` ở ngoài là `"01"` khi **bất kỳ** terminal nào hỏng, kể cả khi phần lớn đã
thành công. Trạng thái thật của từng cái nằm ở `results[].success`.

`users` **chỉ có dòng của terminal ACQHUB đã cài xong**. Terminal hỏng không có
dòng user nào — đó là chủ ý, không phải thiếu.

## 8. Ba nhóm kết quả trên lưới

| Nhóm | Màu | Ở lại lưới? | Gửi lại được? |
|---|---|---|---|
| Terminal cài xong + user tạo xong | — | không, biến mất | — |
| Terminal ACQHUB từ chối | đỏ | có | **được** |
| Terminal cài xong, user KHÔNG tạo được | vàng | có | **KHÔNG** |

Nhóm vàng là nhóm mới và là nhóm nguy hiểm nhất. ACQHUB **đã ghi** terminal đó
rồi; bấm gửi lại chỉ nhận `"already exist"`, không sửa được gì. Phải tạo user bằng
màn hình quản lý người dùng. Dialog nói thẳng điều này thay vì để người dùng bấm
lại rồi tưởng hệ thống hỏng.

## 9. Mật khẩu ban đầu

`Shlx@yyMMdd` — công thức cố định trong nhánh SHLX của `ApiCommon.CreateUsers`.
Dialog hiện ra sau khi tạo xong để người cài còn báo lại.

**Đây không phải bí mật.** Mọi user tạo trong cùng một ngày đều trùng mật khẩu, và
ai đọc được dòng code đó cũng đoán ra. Nó chỉ chấp nhận được nếu portal **bắt đổi
mật khẩu ở lần đăng nhập đầu**. Cần kiểm `Status = "0"` nghĩa là gì — "phải đổi
mật khẩu" hay chỉ "đang hoạt động".

## 10. `ROLE_ID` bị chặn chỉ cho 28 và 29 — cố ý

`ROLE_ID` là một ô Excel do người dùng gõ. `ApiCommon.CreateUsers` chấp nhận cả
role VCB, BCA, Merchant. Chuyền thẳng ô đó vào là **quyền "cài đặt SHLX" trở thành
quyền tự tạo tài khoản quản trị** (RoleId 19) chỉ bằng cách sửa một ô trong file.

Backend từ chối mọi role ngoài `Roles.IsShlxRoles` và ghi log cảnh báo kèm tên
người gửi. Frontend không chặn — cố tình, để người dùng thấy lý do ở cột LỖI thay
vì file bị từ chối mà không hiểu vì sao.

## 11. Ba việc còn phải làm ở `vcbportalweb`

**a) Gỡ `ShlxConfigComponent` khỏi `declarations`** trong `bca.module.ts` —
component là `standalone: true`, để trong `declarations` sẽ ra `NG6008`. Module cha
vẫn cần `MatDialogModule` để gọi `MatDialog.open()`.

**b) Thêm nút mở dialog** vào màn hình cha:

```ts
openShlxConfig(): void {
    this._dialog.open(ShlxConfigComponent, {
        panelClass: 'shlx-config-dialog',
        disableClose: true,   // tranh bam ra ngoai lam mat file Excel da nap
        autoFocus: false,
    });
}
```

```html
<button class="report-button" mat-raised-button color="primary" type="button"
        (click)="openShlxConfig()">
    <mat-icon [svgIcon]="'heroicons_outline:cog-6-tooth'"></mat-icon>
    Cài đặt SHLX
</button>
```

`panelClass: 'shlx-config-dialog'` **bắt buộc** — CSS tô dòng đỏ/vàng gắn vào
class đó. Thiếu nó thì lưới vẫn chạy nhưng không dòng nào đổi màu.

**c) Kiểm `environment.mainEndpoint`** kết thúc bằng `/` — xem mục 5.

## 12. Còn phải tự kiểm

- **`onError({ type: 'skip_cell' })`** có phải giá trị hợp lệ trong thư viện `ez`
  thật không. Ở đây tôi dựng stub theo chữ ký suy từ ảnh code.
- **Thứ tự 5 cột mới** — mục 3.
- **`ez.string()` không `.required()`** có đúng là dạng "cho phép rỗng" không.
