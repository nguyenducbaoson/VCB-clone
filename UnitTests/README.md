# UnitTests

Unit test đúng nghĩa: kiểm tra **một đơn vị code cô lập**, không DB, không mạng,
không file, không đồng hồ hệ thống.

Phép thử nhanh xem một test có thuộc về đây không: *mở laptop trên máy bay, tắt
wifi, chạy được không?* Được thì đúng chỗ.

## Chạy

```bash
dotnet test UnitTests
```

Không cần cấu hình gì. Không có test nào bị skip — skip ở đây là dấu hiệu sai chỗ.

## Khác gì ApiTests

| | UnitTests | ApiTests |
|---|---|---|
| Gọi gì | hàm C# trực tiếp | HTTP tới server đang chạy |
| Tham chiếu | `ProjectReference` tới VcbPortalApi | không tham chiếu gì |
| Cần môi trường | không | có |
| Tốc độ | mili giây | giây |
| Bắt lỗi gì | quy tắc nghiệp vụ sai | cấu hình sai, xác thực hỏng, schema lệch |
| Đỏ thì biết lỗi ở đâu | biết chính xác hàm nào | chỉ biết "chuỗi nào đó hỏng" |

Hai bộ bổ sung nhau, không thay thế nhau.

## Cái gì được vào đây

Chỉ hàm **thuần**: cùng đầu vào luôn cho cùng đầu ra, không đụng gì bên ngoài.

Bảy thứ khiến một hàm KHÔNG còn unit test được:

| Tránh | Ví dụ trong project |
|---|---|
| DB | `context.MpSessions.FirstOrDefaultAsync` |
| Mạng | `MpSsoClient` gọi VCB SSO |
| File | |
| `DateTime.Now` | `MpSsoAuthService.WriteSsoLogAsync` |
| `Guid.NewGuid()` | `MpSsoClient` sinh `msgID` |
| Biến môi trường | |
| Static có thể thay đổi | `AppSettings.SignatureSecretUat` |

Hàm dính một trong bảy thứ đó thì để `ApiTests` phủ, đừng cố nhét vào đây.

Không cài EF InMemory, không cài driver DB, không cài thư viện mock — nếu cần
những thứ đó thì thứ đang test không còn là một đơn vị thuần nữa.

## Viết code sao cho unit test được

Tách **logic quyết định** ra khỏi **điều phối**:

```csharp
// ĐIỀU PHỐI — đọc DB, ghi DB. Không unit test được, để ApiTests phủ.
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

Làm việc này khi viết `MpAppUserStatusTests` và nó phát hiện một chuyện đáng chú ý:

- Đảo nhánh `2..6` với nhánh `toàn 7` → **không test nào đỏ**. Vì hai điều kiện đó
  loại trừ nhau, đảo cũng không đổi hành vi.
- Đảo nhánh `toàn 7` với nhánh `toàn 0/7` → **2 test đỏ** (`"7"` và `"7,7"`). Vì tập
  chỉ có 7 thoả cả hai; đảo lại thì user đã hủy bị tính thành "Đã đăng ký".

Comment trong `ResolveStatus` trước đây ghi thứ tự bắt buộc là để `{2,7}` ra "Kích hoạt"
— không chính xác. Ràng buộc thật nằm ở hai nhánh cuối. Đã sửa lại comment.

Đó là giá trị của việc phá code thử: nó phân biệt được ràng buộc thật với ràng buộc
tưởng tượng.

## Đặt tên

| | Quy ước | Ví dụ |
|---|---|---|
| File | `<TênClass>Tests.cs` | `MpAppUserStatusTests.cs` |
| Thư mục | soi gương thư mục nguồn | `Services/`, `Helpers/` |
| Hàm test | `<Hàm>_<TìnhHuống>_<KếtQuảMongĐợi>` | `HmacSha256_ReturnsUppercaseHex` |

## Bẫy đã gặp

`[InlineData]` không mang được `decimal` — C# không cho hằng `decimal` trong attribute.
Khai tham số `int?` rồi ép trong thân test. Số nguyên viết cho tham số `double?` phải
có hậu tố `d` (`40000001d`), nếu không ném `ArgumentException` lúc chạy chứ không phải
lỗi biên dịch.

Và đừng bao giờ đưa khoá thật vào test. `HMAC256Tests` dùng vector kiểm thử công khai
của RFC 4231.
