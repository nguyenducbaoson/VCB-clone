# Tests

Một project, hai lane. Chạy `dotnet test` là chạy cả hai.

```
Tests/
├── Unit/              ← unit test: gọi hàm C# trực tiếp, không DB, không mạng
│   ├── Services/MpAppUserStatusTests.cs
│   └── Helpers/HMAC256Tests.cs
├── Api/               ← API test: gọi HTTP tới server đang chạy
│   ├── VcbPortalApi/MobilePartnerApiTests.cs
│   └── AddRedis/
├── TestSupport/       ← hạ tầng dùng chung cho lane Api
└── Tests.csproj
```

| | `Unit/` | `Api/` |
|---|---|---|
| Gọi gì | hàm C# trực tiếp | HTTP tới server |
| Cần môi trường | không | có |
| Tốc độ | mili giây | giây |
| Bắt lỗi gì | quy tắc nghiệp vụ sai | cấu hình sai, xác thực hỏng, schema lệch |
| Đỏ thì biết lỗi ở đâu | biết chính xác hàm nào | chỉ biết "chuỗi nào đó hỏng" |
| Chưa cấu hình môi trường | vẫn chạy | tự **skip** |

Hai lane bổ sung nhau, không thay thế nhau.

## Chạy

```bash
dotnet test                                      # ca hai lane
dotnet test --filter FullyQualifiedName~Unit     # chi unit
dotnet test --filter FullyQualifiedName~Api      # chi api
```

Lane `Api/` cần biến môi trường, chưa đặt thì skip chứ không fail:

```powershell
$env:VCB_API_BASEURL = "https://uat-host/api/v1"
$env:VCB_API_TOKEN   = "<bearer token cua mot user da dang nhap>"
$env:VCB_API_TID     = "40000001"
$env:VCB_API_MID     = "68100000000097"    # user role BID moi can
```

Trong Visual Studio đừng set tay từng lần — tạo `test.runsettings` cạnh `.sln`:

```xml
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <VCB_API_BASEURL>https://uat-host/api/v1</VCB_API_BASEURL>
      <VCB_API_TOKEN>eyJhbGciOi...</VCB_API_TOKEN>
      <VCB_API_TID>40000001</VCB_API_TID>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

**Test** → **Configure Run Settings** → **Select Solution Wide runsettings File**.
File này chứa token thật → thêm vào `.gitignore` ngay.

> **Skip không phải pass.** Đọc số `Skipped`. Trước khi deploy phải có một lần chạy
> lane `Api/` với `Skipped: 0`.

---

# Lane Unit/

Kiểm tra **một đơn vị code cô lập**. Phép thử nhanh: *mở laptop trên máy bay, tắt
wifi, chạy được không?* Được thì đúng chỗ.

## Cái gì được vào đây

Chỉ hàm **thuần**: cùng đầu vào luôn cho cùng đầu ra, không đụng gì bên ngoài.
Bảy thứ khiến một hàm không còn unit test được:

| Tránh | Ví dụ trong project |
|---|---|
| DB | `context.MpSessions.FirstOrDefaultAsync` |
| Mạng | `MpSsoClient` gọi VCB SSO |
| File | |
| `DateTime.Now` | `MpSsoAuthService.WriteSsoLogAsync` |
| `Guid.NewGuid()` | `MpSsoClient` sinh `msgID` |
| Biến môi trường | |
| Static có thể thay đổi | `AppSettings.SignatureSecretUat` |

Dính một trong bảy thứ đó thì để lane `Api/` phủ, đừng cố nhét vào `Unit/`.

Project **không cài** EF InMemory, driver DB hay thư viện mock — cần những thứ đó
nghĩa là test đó thuộc lane `Api/`.

## Viết code sao cho unit test được

Tách **logic quyết định** khỏi **điều phối**:

```csharp
// ĐIỀU PHỐI — đọc ghi DB. Không unit test được, để lane Api/ phủ.
public async Task RefreshStatusAsync(string username, CancellationToken ct)
{
    var rows = await _db.Set<MpAppPartnerCardReg>()...
    user.PhoneposStatus = ResolveForPartner(rows, MpPartner.PhonePos);   // ← gọi hàm thuần
    await _db.SaveChangesAsync(ct);
}

// LOGIC QUYẾT ĐỊNH — static, vào ra thuần tuý. Unit test được.
public static decimal? ResolveForPartner(
    IEnumerable<(string? Partner, string? Status)> rows, string partner) { ... }
```

Phần lớn bug nghiệp vụ nằm ở hàm thuần. Phần điều phối thì API test bắt tốt hơn.

## Kiểm tra test có thật sự bắt được lỗi

Test xanh chưa chắc có ích. Cách rẻ nhất: **cố tình phá code** rồi xem có đỏ đúng chỗ.

Làm việc này với `MpAppUserStatusTests` và nó phát hiện một chuyện đáng chú ý:

- Đảo nhánh `2..6` với nhánh `toàn 7` → **không test nào đỏ**. Vì hai điều kiện đó
  loại trừ nhau, đảo cũng không đổi hành vi.
- Đảo nhánh `toàn 7` với nhánh `toàn 0/7` → **2 test đỏ** (`"7"` và `"7,7"`). Vì tập
  chỉ có 7 thoả cả hai; đảo lại thì user đã hủy bị tính thành "Đã đăng ký".

Comment trong `ResolveStatus` trước đây ghi thứ tự bắt buộc là để `{2,7}` ra "Kích
hoạt" — không chính xác. Ràng buộc thật nằm ở hai nhánh cuối. Đã sửa comment.

Đó là giá trị của việc phá code thử: phân biệt ràng buộc thật với ràng buộc tưởng tượng.

---

# Lane Api/

Gọi API **thật đang chạy** qua HTTP. Không mock, không seed.

> Đây là API test (end-to-end), **không phải unit test**. Gọi đúng tên khi báo cáo.

## Thêm test cho endpoint mới

Copy `Api/VcbPortalApi/MobilePartnerApiTests.cs`. Ba nhóm, viết theo thứ tự:

```csharp
private const string Endpoint = "ma/partner/token";

// 1. Khong co quyen -> 401
var api = new ApiClient(token: null);            // khong gan Authorization
var api = new ApiClient(token: "not-a-valid-jwt");

// 2. Dau vao sai -> dung ma loi nghiep vu
var api = new ApiClient();
var result = await api.PostFormAsync(Endpoint, ("PartnerCode", null));
Assert.True(result.Field("code")?.Contains("PartnerCode") == true, result.Describe);

// 3. Duong thanh cong
var result = await api.PostFormAsync(Endpoint, ("PartnerCode", "PHONEPOS"), ("Tid", ApiEnv.Tid));
Assert.True(result.IsSuccess, result.Describe);
```

Service khác thì `new ApiClient(ApiEnv.AddRedis)` và `[ApiFact(ApiEnv.AddRedis)]`.

**Luôn truyền `result.Describe` vào Assert** — nó in method, URL, status và body, đủ
để dựng lại lời gọi từ log Test Explorer mà không phải mở Postman.

## Kỷ luật quan trọng nhất của lane này

Project **có** `ProjectReference` tới `VcbPortalApi` vì lane `Unit/` cần. Nghĩa là
code trong `Api/` **có thể** gọi thẳng vào service hay đọc `AppSettings` — nhưng
đừng bao giờ làm vậy.

Lane `Api/` chỉ được nói chuyện qua HTTP. Gọi thẳng vào code là mất hết ý nghĩa: nó
không còn kiểm tra routing, `[Authorize]`, middleware, cấu hình môi trường nữa.

Trước khi gộp hai project thì điều này được đảm bảo bằng kỹ thuật. Giờ chỉ còn kỷ
luật — đây là cái giá của việc gộp.

## Ba quyết định thiết kế

**HttpClient dùng chung cho cả phiên chạy, mỗi service một cái.** Tạo mới mỗi test là
lỗi kinh điển của .NET: mỗi `HttpClient` giữ connection pool riêng, socket đóng rồi
vẫn nằm ở `TIME_WAIT` khoảng 4 phút, chạy vài chục test là cạn cổng và bắt đầu lỗi
`address in use` rải rác. Vì client dùng chung nên token gắn theo **từng request**,
không đặt mặc định trên client; nhờ vậy test nhánh 401 vẫn dùng chung client.

**Tắt chạy song song** (`TestSupport/AssemblyConfig.cs`). Lane `Api/` đánh vào một môi
trường dùng chung; song song sẽ dội request cùng lúc và làm phiền người khác đang
test tay. Nó cũng làm lane `Unit/` chạy tuần tự — với 30 test thì không đáng kể.

**`Field()` tìm không phân biệt hoa thường, tìm cả object con.** Viết lỏng có chủ ý:
`code`, `Code`, `resCode` đều lấy được, khuôn response đổi không phải sửa test.

## Giới hạn — biết trước để khỏi mất công

| | |
|---|---|
| Chậm | mỗi test một vòng mạng |
| Dễ đỏ oan | môi trường sập, token hết hạn, người khác sửa dữ liệu |
| Không ép được trạng thái DB | nhánh "user chưa có session" không chạm tới được |
| Không chạy được trong pipeline build | phải có môi trường sống |

**Token sống khoảng 42 phút.** Thấy tất cả test đỏ với 401 thì việc đầu tiên là lấy
token mới, đừng vội nghi code. Token phải cùng môi trường với base URL — token UAT
gọi vào PRD sẽ 401 vì chữ ký không khớp.

---

## Đặt tên

| | Quy ước | Ví dụ |
|---|---|---|
| File | `<TênClass>Tests.cs` | `MpAppUserStatusTests.cs` |
| Thư mục | soi gương thư mục nguồn | `Unit/Services/`, `Unit/Helpers/` |
| Hàm test | `<Hàm>_<TìnhHuống>_<KếtQuảMongĐợi>` | `HmacSha256_ReturnsUppercaseHex` |

Ngoại lệ: file trong `Api/VcbPortalApi/` khai namespace `Tests.Api` chứ không phải
`Tests.Api.VcbPortalApi`. Một namespace tên `VcbPortalApi` lồng trong `Tests.Api` sẽ
che khuất namespace gốc `VcbPortalApi` của project API, làm các `using` bên trong
không phân giải đúng.

## Hai cái bẫy đã gặp

**`[InlineData]` không mang được `decimal`** — C# không cho hằng `decimal` trong
attribute. Khai tham số `int?`/`double?` rồi ép trong thân test. Số nguyên viết cho
tham số `double?` phải có hậu tố `d` (`40000001d`), nếu không ném `ArgumentException`
lúc chạy chứ không phải lỗi biên dịch.

**Đừng bao giờ đưa khoá thật vào test.** `HMAC256Tests` dùng vector kiểm thử công khai
của RFC 4231.
