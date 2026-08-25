# VcbPortalApi.UnitTests

```bash
dotnet test
```

76 test, không test nào bị skip. Không cần môi trường, không cần Oracle.

## Cấu trúc — mirror theo source

```
VcbPortalApi/                               tests/VcbPortalApi.UnitTests/
├── Controllers/                            ├── Controllers/
│   ├── MerchantSsoController.cs            │   ├── MerchantSsoControllerTests.cs
│   └── Mobile/                             │   └── Mobile/
│       └── MobilePartnerController.cs      │       └── MobilePartnerControllerTests.cs
├── Services/                               ├── Services/
│   └── MpAppUserStatusService.cs           │   ├── MpAppUserStatusLogicTests.cs
│                                           │   └── MpAppUserStatusServiceTests.cs
└── Helpers/HMAC256.cs                      ├── Helpers/
                                            │   ├── HMAC256Tests.cs
                                            │   └── TestDataHelper.cs    ← nhà máy dữ liệu
                                            └── Fixtures/                ← hạ tầng
                                                ├── TestDb.cs
                                                ├── TestHttpContext.cs
                                                └── MobileApiAssertions.cs
```

## Thư viện

| | Dùng làm gì |
|---|---|
| xUnit | test runner |
| Moq | mock interface (`IMpSsoAuthService`) |
| FluentAssertions | `result.Should().Be(...)` |
| EF Core InMemory | DbContext chạy trong bộ nhớ |

> **FluentAssertions ghim ở 7.2.0 có chủ ý.** Từ bản 8.0, Xceed đổi sang giấy phép
> thương mại — dùng trong sản phẩm thương mại phải mua license. 7.2.0 là bản cuối còn
> Apache 2.0, miễn phí. **Đừng nâng lên 8.x nếu chưa có license.** Cần thay thế miễn
> phí thì dùng `AwesomeAssertions` (fork của 7.x, API giống hệt).

## Pattern: AAA

```csharp
[Fact]
public async Task RefreshStatusAsync_WhenNoRegistration_ClearsBothColumns()
{
    // Arrange
    var user = _db.Seed(TestDataHelper.CreateAppUser(phoneposStatus: 2));

    // Act
    await CreateService().RefreshStatusAsync(TestDataHelper.DefaultUserName);

    // Assert
    user.PhoneposStatus.Should().BeNull();
}
```

Act chỉ gọi **một** hàm. Gọi nhiều thì fail không biết hàm nào hỏng.

## Naming

`Method_WhenCondition_ExpectedResult`

```
RefreshStatusAsync_WhenUserNotFound_DoesNotThrow
Login_WhenServiceThrows_ReturnsSystemErrorWithoutLeakingDetails
IssueSsoToken_WhenRoleMid_TakesMidFromDatabaseNotFromForm
```

Nhìn tên là biết test cái gì. Khi fail, tên hiện trong log chính là câu mô tả lỗi.

## Hạ tầng — dùng chung, không phải sửa khi thêm controller

| File | Dùng cho | Sửa khi thêm controller? |
|---|---|---|
| `Fixtures/TestDb.cs` | **mọi** DbContext | không |
| `Fixtures/TestHttpContext.cs` | **mọi** controller kế thừa `ControllerCustom` | không |
| `Fixtures/MobileApiAssertions.cs` | controller trả khuôn `MobileApiError` | không, nếu dùng chung khuôn |
| `Helpers/TestDataHelper.cs` | dữ liệu mẫu | có — thêm một hàm `CreateXxx` |

```csharp
// Bat ky DbContext nao - khong can helper rieng
using var fe = TestDb.Create<FrontendContext>();
fe.Seed(TestDataHelper.CreateSession());

// Bat ky controller nao ke thua ControllerCustom
var ctx = TestHttpContext.Build(userName: "VATID001");
```

`TestHttpContext.Build` gắn claim theo đúng hằng `AppSettings.Claim*`, nên mọi property
`protected` của `ControllerCustom` — `CurrentUserName`, `CurrentUserRoleId`,
`CurrentUserBid/Mid/Tid`, `CurrentUserSessionId` — điều khiển được từ một chỗ.
Cần claim nào ngoài username thì truyền qua tham số `claimThem`.

### Test data tách riêng

Đừng dựng data dài trong từng test. `TestDataHelper` cho mọi tham số một giá trị mặc
định hợp lệ, test chỉ đặt lại đúng thứ nó quan tâm:

```csharp
TestDataHelper.CreateAppUser(bid: null)      // chi thieu BID, con lai hop le
TestDataHelper.CreateUsersCommon(email: null) // chi thieu email
```

Giá trị mặc định lấy từ dữ liệu **thật** trên UAT, không phải nghĩ ra — dữ liệu mẫu
không giống thực tế thì test chỉ chứng minh code đúng với giả định của mình.

## Ba mẫu, theo thứ tự ưu tiên

**1. Hàm thuần** — `Services/MpAppUserStatusLogicTests.cs`. `[Theory]` + `[InlineData]`,
24 dòng dữ liệu khớp 1-1 với bảng đặc tả nghiệp vụ. Rẻ nhất, nên ưu tiên.

**2. Service có DbContext** — `Services/MpAppUserStatusServiceTests.cs`. `TestDb.Create<T>()`.

**3. Controller** — `Controllers/`. Gọi thẳng action, không dựng web server.
`MerchantSsoControllerTests` là ca dễ (có service để mock);
`MobilePartnerControllerTests` là ca khó (logic nằm hết trong action).

## Thêm test cho API mới

1. Tạo file trong thư mục mirror, tên `<TênClass>Tests.cs`.
2. **Liệt kê nhánh** — mỗi `return` sớm trong action là một test.
3. Một hàm dựng **bối cảnh hợp lệ hoàn toàn**, rồi mỗi test làm hỏng **đúng một thứ**.
4. Đường thành công để cuối.

Mẹo: viết nhánh lỗi trước. Nhánh thành công thường đã được bấm thử tay lúc code.

## Kiểm tra test có thật sự bắt được lỗi

Test xanh chưa chắc có ích. Cách rẻ nhất: **cố tình phá code** rồi xem có đỏ đúng chỗ.

Đã làm hai lần:

- Sửa claim `mid` của role MID từ DB thành lấy từ form → đúng 1 test đỏ
  (`IssueSsoToken_WhenRoleMid_TakesMidFromDatabaseNotFromForm`), 75 test kia xanh.
  Đây là lỗi nâng quyền mà test tay không bao giờ phát hiện.
- Đảo nhánh `2..6` với `toàn 7` trong `ResolveStatus` → **không test nào đỏ**, vì hai
  điều kiện đó loại trừ nhau. Đảo `toàn 7` với `toàn 0/7` → 2 test đỏ.
  Comment trong code ghi ràng buộc thứ tự sai chỗ; đã sửa.

## Bộ test này KHÔNG trả lời được gì

Gọi thẳng action nghĩa là **bỏ qua toàn bộ pipeline ASP.NET**:

| | |
|---|---|
| `[Authorize(Policy = "MobileAppPolicy")]` | bị bỏ qua — test không nói gì về việc endpoint có được bảo vệ đúng không |
| Routing, model binding, middleware | không chạy |
| Cấu hình sai, build nhầm môi trường, nối nhầm DB | không thấy |
| Schema Oracle lệch với code | `ORA-00904`, `ORA-12899` chỉ xuất hiện khi chạy thật |

EF InMemory **không phải Oracle**: không kiểm tra ràng buộc, dịch LINQ theo luật C#
chứ không phải SQL. Muốn phủ những thứ trên thì cần một **project Integration Test
riêng**, không trộn vào đây.

## Ba cái bẫy đã gặp

**`[InlineData]` không mang được `decimal`** — C# không cho hằng `decimal` trong
attribute. Khai tham số `int?`/`double?` rồi ép trong thân test. Số nguyên viết cho
tham số `double?` phải có hậu tố `d` (`40000001d`), nếu không ném `ArgumentException`
lúc chạy chứ không phải lỗi biên dịch.

**Namespace `VcbPortalApi.UnitTests` kéo `VcbPortalApi.DbContext` vào tầm nhìn**, che
mất kiểu `DbContext` của EF. Trong `Fixtures/TestDb.cs` phải viết đầy đủ
`Microsoft.EntityFrameworkCore.DbContext`.

**Đừng bao giờ đưa khoá thật vào test.** `HMAC256Tests` dùng vector kiểm thử công khai
của RFC 4231.
