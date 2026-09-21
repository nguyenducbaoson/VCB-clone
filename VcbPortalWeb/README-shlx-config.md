# Cài đặt SHLX qua ACQHUB — những việc còn lại

## 1. Frontend không gọi thẳng ACQHUB

Tài liệu đưa endpoint `http://__ACQHUB_HOST__:8829/api/acqhub/configpartner/v1/shlxconfig`,
nhưng Angular **không** gọi vào đó được:

- `checkSum` là chuỗi SHA-256 64 ký tự, tính từ một khoá bí mật. Để khoá trong
  trình duyệt là bất kỳ ai mở DevTools cũng lấy được rồi tự gọi ACQHUB.
- `__ACQHUB_HOST__` là IP nội bộ, trình duyệt của người dùng không tới được.
- ACQHUB không bật CORS cho domain portal.

Nên frontend gọi `VcbPortalApi`, backend mới là chỗ ký và gọi sang ACQHUB —
đúng khuôn `ImportShlxComponent` sẵn có (`environment.mainEndpoint + url.bca.shlx`).

## 2. Hợp đồng giữa Angular và VcbPortalApi

**Request** `POST {mainEndpoint}/apimp/acqhub/shlx-config`

```json
{
  "items": [
    {
      "merchantId": "__MERCHANT_ID__",
      "terminalId": "__TERMINAL_ID__",
      "terminalName": "__TERMINAL_NAME__",
      "ddAccountNumber": "__ACCOUNT_NUMBER__",
      "branchCode": "01234",
      "province": "__PROVINCE__"
    }
  ]
}
```

**Response** — trả nguyên hình dạng ACQHUB để frontend đọc `results[]`:

```json
{
  "code": "00",
  "message": "Succeed|Success",
  "requestId": "__REQUEST_ID__",
  "subCode": "00",
  "subMessage": "Succeed|Success",
  "serverTime": "20260916175239",
  "nodeOut": "__ACQHUB_HOST__",
  "operation": "ShlxConfig",
  "results": [
    {
      "terminal_id": "__TERMINAL_ID__",
      "merchant_account_id": 0,
      "success": true,
      "result_code": "00",
      "result_message": "QR terminal and merchant config saved"
    }
  ]
}
```

## 3. Backend làm gì

1. Nhận `items`, dựng JSON `{"items":[...]}` rồi **base64** → trường `data`
2. Đặt `clientId` = `portal_api`, `requestTime` = epoch milliseconds
3. Tính `checkSum` (SHA-256, khoá lấy từ appsettings — cùng chỗ với nhóm `AcqHub`)
4. POST sang `{AcqHub:ShlxConfigUrl}` — thêm khoá này vào 4 file appsettings,
   giá trị UAT là `http://__ACQHUB_HOST__:8829/api/acqhub/configpartner/v1/shlxconfig`
5. Đọc `results[]`, **với mỗi terminal `success: true` thì tạo user portal**
   (yêu cầu "cài đặt thành công API ACQHUB xong thì tạo user portal luôn")
6. Trả nguyên response ACQHUB về cho frontend

Điểm cần chốt với người ra yêu cầu:

- **Tạo user portal theo terminal hay theo merchant?** Một merchant có nhiều
  terminal; tạo user cho từng terminal sẽ ra nhiều user trùng merchant.
- **Nếu tạo user portal hỏng thì sao?** ACQHUB đã ghi cấu hình rồi, không rollback
  được. Nên trả về trạng thái riêng cho bước này, đừng nuốt lỗi.
- **Gọi lại cùng terminal thì ACQHUB xử lý thế nào** — ghi đè hay báo trùng?
  Quyết định việc người dùng bấm gửi lại có an toàn không.

## 4. Thêm vào `src/app/const/api-url.ts`

```ts
export const url = {
  // ... giữ nguyên phần cũ
  acqhub: {
    shlxConfig: 'acqh/shlx/cfg',   // theo quy uoc 'acqh/...', sua dung route backend
  },
};
```

Sửa lại cho khớp tiền tố thật của backend — `BuildSettings.FixedEndpoint` là
`apimp` ở Dev/Uat/Prod nhưng `apivp` ở Pilot.

## 5. Khai báo component trong module

Component để `standalone: false` theo đúng lối của dự án, nên phải khai trong
NgModule của `modules/acqhub`. Module đó cần import:

```ts
MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule,
MatSelectModule, MatDialogModule, MatDividerModule, MatProgressSpinnerModule,
ReactiveFormsModule, DxDataGridModule
```

Thiếu `DxDataGridModule` là gặp đúng lỗi đang có ở `shlx.component.html`:
`'dxi-data-grid-column' is not a known element`.

## 6. Mở dialog

```ts
this._dialog.open(ShlxConfigComponent, {
  panelClass: 'shlx-config-dialog',
  disableClose: true,
  autoFocus: false,
});
```

## 7. Ba quyết định đã chốt

**`MERCHANT_ID`: để người dùng tự nhập.** Chưa rõ giá trị `__MERCHANT_ID__` trong
mockup đến từ đâu, nên tạm để ô trống bắt buộc nhập. Khi biết nguồn (user đang
đăng nhập, hay chọn từ danh sách) thì chỉ cần `setValue` trong constructor.

**`BRANCH_CODE`: đệm về 5 ký tự, nhưng hiện danh sách các dòng đã đệm.**

Mẫu ghi `01234`, ô định dạng text nên đọc ra đúng. Nhưng nếu file mất định dạng,
Excel trả về số `1234` — gửi đi là sai chi nhánh mà không lỗi nào báo.

Gửi `1234` thì chắc chắn sai, nên đệm luôn tốt hơn để nguyên. Nhưng không sửa
lặng lẽ: mọi dòng bị đệm được liệt kê ngay dưới lưới xem trước, kèm TERMINAL_ID,
để người dùng đối chiếu với file gốc trước khi bấm gửi.

Nếu mã chi nhánh không phải luôn 5 chữ số thì sửa số `5` trong `asBranchCode()`.

**`H270:O270`: không đọc, chỉ là ghi chú cho người dùng.**

Lý do: payload ACQHUB chỉ có đúng 6 trường, không có chỗ cho dữ liệu từ vùng đó.
Câu "Remaining database values use the Excel H270:O270 defaults" đọc như lời giải
thích cho người dùng rằng *các cột khác trong DB sẽ lấy giá trị mặc định*, chứ
không phải yêu cầu ứng dụng đọc mấy ô đó rồi gửi đi.

Nên frontend giữ nguyên câu đó làm ghi chú trên màn hình và **không** đọc vùng
H270:O270. Nếu thực ra đó là dữ liệu phải gửi kèm thì cả hợp đồng API lẫn
component đều phải sửa — báo tôi.

## 8. Backend đã có sẵn

Bạn nói phần backend đã xong, nên chỉ cần chỉnh `url.acqhub.shlxConfig` trỏ
đúng đường dẫn thật. Nếu hợp đồng backend khác với mục 2 ở trên — tên trường,
hình dạng response — gửi tôi để sửa lại `shlx-config.interface.ts` và hàm
`_send()`.
