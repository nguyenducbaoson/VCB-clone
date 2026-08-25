# Tests

Unit test cho `VcbPortalApi` — logic nghiệp vụ, service, và controller.
Không mạng, không Oracle, không cần môi trường nào đang chạy.

```bash
dotnet test
```

76 test, không test nào bị skip. Thấy `Skipped` là dấu hiệu có gì đó sai chỗ.

## Cấu trúc

Thư mục test soi gương thư mục nguồn.

```
VcbPortalApi/                               Tests/
├── Controllers/                            ├── Controllers/
│   ├── MerchantSsoController.cs            │   ├── MerchantSsoControllerTests.cs
│   └── Mobile/                             │   └── Mobile/
│       └── MobilePartnerController.cs      │       └── MobilePartnerControllerTests.cs
├── Services/                               ├── Services/
│   └── MpAppUserStatusService.cs           │   ├── MpAppUserStatusTests.cs
│                                           │   └── MpAppUserStatusServiceTests.cs
└── Helpers/HMAC256.cs                      ├── Helpers/HMAC256Tests.cs
                                            └── TestSupport/    ← đồ dùng chung
```

| | Quy ước | Ví dụ |
|---|---|---|
| File | `<TênClass>Tests.cs` | `MpAppUserStatusTests.cs` |
| Namespace | `Tests.<thư mục>` | `Tests.Controllers.Mobile` |
| Hàm test | `<Hàm>_<TìnhHuống>_<KếtQuảMongĐợi>` | `Login_BodyNull_TraVe400VaKhongGoiService` |

Tên hàm dài không sao. Khi fail, tên hiện trong log chính là câu mô tả lỗi.

## Ba mẫu, chọn theo thứ tự ưu tiên

### Mẫu 1 — hàm thuần · rẻ nhất, ưu tiên
[`Services/MpAppUserStatusTests.cs`](Services/MpAppUserStatusTests.cs)

Hàm `static`, vào ra thuần tuý, không đụng gì bên ngoài. `[Theory]` + `[InlineData]`
chạy một thân test với nhiều bộ dữ liệu — 24 dòng dữ liệu khớp 1-1 với bảng đặc tả
nghiệp vụ.

Cố gắng tách logic quyết định ra hàm `static` để test được kiểu này.

### Mẫu 2 — service có đụng DbContext
[`Services/MpAppUserStatusServiceTests.cs`](Services/MpAppUserStatusServiceTests.cs)

Dùng [`TestSupport/TestDb.cs`](TestSupport/TestDb.cs): EF Core InMemory, mỗi test một
database riêng theo `Guid` nên chạy song song không giẫm chân nhau.

Ba bước Arrange / Act / Assert, Act chỉ gọi **một** hàm.

### Mẫu 3 — controller · ca khó nhất, phổ biến nhất ở project này
[`Controllers/Mobile/MobilePartnerControllerTests.cs`](Controllers/Mobile/MobilePartnerControllerTests.cs)

Gọi thẳng action như gọi hàm thường. Không dựng web server, không mở cổng.

[`TestSupport/MobileTestKit.cs`](TestSupport/MobileTestKit.cs) gom bốn chỗ khó:

| Vướng | Cách gỡ |
|---|---|
| `CurrentUserName` là `protected`, không gán được | gắn claim vào `HttpContext.User`, key lấy từ `AppSettings.ClaimUserName` |
| `TryGetBearerTokenExpiresUtc` đọc header | gắn `Authorization: Bearer <jwt>` |
| `AppSettings.SigningCredentials` là `static` | gán trong Arrange, quên là `CreateToken` nổ |
| `MobileHelper` là `static`, không fake được | điều khiển bằng cách seed dữ liệu vào `MerchantContext` |

Ba hàm `AssertLoi` / `AssertUnauthorized` / `DocClaimTuToken` là chỗ **duy nhất** biết
khuôn response. Khuôn đổi thì sửa ba hàm đó, các test giữ nguyên.

[`Controllers/MerchantSsoControllerTests.cs`](Controllers/MerchantSsoControllerTests.cs)
là bản nhẹ hơn: controller có tách service, chỉ cần một fake
([`TestSupport/FakeMpSsoAuthService.cs`](TestSupport/FakeMpSsoAuthService.cs)) chứ
không cần DbContext.

## Thêm test cho một API mới

1. Tạo file trong thư mục soi gương thư mục nguồn, tên `<TênClass>Tests.cs`.
2. **Liệt kê nhánh** — mỗi lệnh `return` sớm trong action là một test.
3. Một hàm dựng **bối cảnh hợp lệ hoàn toàn** (như `BoiCanhHopLeRoleMid`), rồi mỗi
   test chỉ làm hỏng **đúng một thứ**. Nhờ vậy fail thì chắc chắn do nhánh đó, không
   phải do quên seed dữ liệu chỗ khác.
4. Đường thành công để cuối, kiểm tra kỹ nhất.
5. `dotnet test` xanh rồi mới commit.

Mẹo: viết nhánh lỗi trước. Nhánh thành công thường đã được bấm thử tay lúc code,
còn nhánh lỗi thì hiếm khi ai thử.

## Kiểm tra test có thật sự bắt được lỗi

Test xanh chưa chắc có ích. Cách rẻ nhất: **cố tình phá code** rồi xem có đỏ đúng chỗ.

Đã làm hai lần, và cả hai đều đáng ghi lại:

**Sửa claim `mid` của role MID từ `mId!.Value` (DB) thành `form.Mid!.Value` (client gửi)**
→ đúng một test đỏ (`IssueSsoToken_RoleMid_ClaimMidLayTuDbKhongLayTuForm`), 75 test kia
vẫn xanh. Đây là lỗi nâng quyền: user MID tự đặt mid nào cũng được. Test tay không bao
giờ phát hiện vì client thật không gửi mid rác.

**Đảo hai nhánh trong `ResolveStatus`:**
- Đảo `2..6` với `toàn 7` → **không test nào đỏ**. Hai điều kiện đó loại trừ nhau, đảo
  cũng không đổi hành vi.
- Đảo `toàn 7` với `toàn 0/7` → **2 test đỏ**. Tập chỉ có 7 thoả cả hai; đảo lại thì
  user đã hủy bị tính thành "Đã đăng ký".

Comment trong `ResolveStatus` trước đây ghi thứ tự bắt buộc là để `{2,7}` ra "Kích hoạt"
— không chính xác. Ràng buộc thật nằm ở hai nhánh cuối. Đã sửa comment.

Đó là giá trị của việc phá code thử: phân biệt ràng buộc thật với ràng buộc tưởng tượng.

## Bộ test này KHÔNG trả lời được gì

Gọi thẳng action nghĩa là **bỏ qua toàn bộ pipeline ASP.NET**. Những thứ sau vẫn xanh
hết dù có sai:

| | |
|---|---|
| `[Authorize(Policy = "MobileAppPolicy")]` | bị bỏ qua khi gọi trực tiếp — test không nói gì về việc endpoint có được bảo vệ đúng không |
| Routing, model binding, middleware | không chạy |
| Cấu hình sai, build nhầm môi trường, nối nhầm database | không thấy |
| Schema Oracle lệch với code | `ORA-00904`, `ORA-12899` chỉ xuất hiện khi chạy thật |
| Câu SQL viết tay | xem [`Sql/`](../Sql/) |

EF InMemory **không phải Oracle**: không kiểm tra ràng buộc, không biết `ORA-12899`,
dịch LINQ theo luật C# chứ không phải SQL. Dùng nó để test **logic**.

Muốn phủ những thứ trên cần gọi API thật qua HTTP — việc riêng, không phải mở rộng
của bộ này.

## Hai cái bẫy đã gặp

**`[InlineData]` không mang được `decimal`** — C# không cho hằng `decimal` trong
attribute. Khai tham số `int?`/`double?` rồi ép trong thân test. Số nguyên viết cho
tham số `double?` phải có hậu tố `d` (`40000001d`), nếu không ném `ArgumentException`
lúc chạy chứ không phải lỗi biên dịch.

**Đừng bao giờ đưa khoá thật vào test.** `HMAC256Tests` dùng vector kiểm thử công khai
của RFC 4231.

## Mang sang solution thật

- `<TargetFramework>` phải trùng `VcbPortalApi.csproj`. Bản này để `net10.0`.
- `Microsoft.EntityFrameworkCore.InMemory` phải cùng dòng version với
  `Microsoft.EntityFrameworkCore` mà API đang dùng.
- Project test dùng `Microsoft.NET.Sdk` thường, không có implicit using của Web SDK.
  Đó là lý do csproj phải khai thêm `Microsoft.Extensions.Logging`,
  `Microsoft.AspNetCore.Http` — thiếu là `CS0246: ILogger<> not found`.
- Các file có header **FILE KHUNG** trong `VcbPortalApi/` là bản dựng lại của type
  solution thật đã có (`ControllerCustom`, `MobileApiError`, entity, DbContext…) —
  **đừng chép đè**.
