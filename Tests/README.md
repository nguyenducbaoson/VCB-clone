# Tests

Unit test cho `VcbPortalApi`. Kiểm tra **code và quy tắc nghiệp vụ có đúng không** —
không DB, không mạng, không file, không đồng hồ hệ thống.

```bash
dotnet test
```

Không cần cấu hình gì, không cần môi trường nào đang chạy. Không có test nào bị skip —
thấy `Skipped` là dấu hiệu có gì đó sai chỗ.

## Cấu trúc

Thư mục test soi gương thư mục nguồn.

```
VcbPortalApi/                          Tests/
├── Services/MpAppUserStatusService.cs  ├── Services/MpAppUserStatusTests.cs
└── Helpers/HMAC256.cs                  └── Helpers/HMAC256Tests.cs
```

| | Quy ước | Ví dụ |
|---|---|---|
| File | `<TênClass>Tests.cs` | `MpAppUserStatusTests.cs` |
| Namespace | `Tests.<thư mục>` | `Tests.Services` |
| Hàm test | `<Hàm>_<TìnhHuống>_<KếtQuảMongĐợi>` | `HmacSha256_ReturnsUppercaseHex` |

Tên hàm dài không sao. Khi fail, tên hiện trong log chính là câu mô tả lỗi.

## Cái gì được vào đây

Chỉ hàm **thuần**: cùng đầu vào luôn cho cùng đầu ra, không đụng gì bên ngoài.

Phép thử nhanh: *mở laptop trên máy bay, tắt wifi, chạy được không?*

Bảy thứ khiến một hàm không unit test được:

| Tránh | Ví dụ trong project |
|---|---|
| DB | `context.MpSessions.FirstOrDefaultAsync` |
| Mạng | `MpSsoClient` gọi VCB SSO |
| File | |
| `DateTime.Now` | `MpSsoAuthService.WriteSsoLogAsync` |
| `Guid.NewGuid()` | `MpSsoClient` sinh `msgID` |
| Biến môi trường | |
| Static có thể thay đổi | `AppSettings.SignatureSecretUat` |

Project **không cài** EF InMemory, driver DB, thư viện mock hay `HttpClient`. Cần bất
kỳ thứ nào trong số đó nghĩa là thứ đang test không còn là một đơn vị thuần.

## Viết code sao cho unit test được

Đây là phần quan trọng nhất, vì phần lớn logic hiện tại **chưa** ở dạng test được.

Tách **logic quyết định** khỏi **điều phối**:

```csharp
// ĐIỀU PHỐI — đọc ghi DB, gọi API, ghi log. Không unit test được.
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

Áp dụng dần với **code mới**, đừng đi refactor loạt controller cũ. Viết endpoint mới
thì tách khối quyết định ra `static` ngay từ đầu — lúc đó gần như không tốn công thêm.

Ví dụ với `MobilePartnerController.IssueSsoToken`: bảy trong mười ba nhánh chỉ dựa
trên giá trị đã có trong tay (role, bid/mid của user, mid/tid client gửi lên). Rút
chúng ra một hàm `static` khoảng 20 dòng là unit test được ngay, không cần DB.

## Kiểm tra test có thật sự bắt được lỗi

Test xanh chưa chắc có ích. Cách rẻ nhất: **cố tình phá code** rồi xem có đỏ đúng chỗ.

Làm việc này với `MpAppUserStatusTests` và nó phát hiện một chuyện đáng chú ý:

- Đảo nhánh `2..6` với nhánh `toàn 7` → **không test nào đỏ**. Hai điều kiện đó loại
  trừ nhau, đảo cũng không đổi hành vi.
- Đảo nhánh `toàn 7` với nhánh `toàn 0/7` → **2 test đỏ** (`"7"` và `"7,7"`). Tập chỉ
  có 7 thoả cả hai; đảo lại thì user đã hủy bị tính thành "Đã đăng ký".

Comment trong `ResolveStatus` trước đây ghi thứ tự bắt buộc là để `{2,7}` ra "Kích
hoạt" — không chính xác. Ràng buộc thật nằm ở hai nhánh cuối. Đã sửa comment.

Đó là giá trị của việc phá code thử: phân biệt ràng buộc thật với ràng buộc tưởng tượng.

## Bộ test này KHÔNG trả lời được gì

Theo đúng định nghĩa, unit test không chạm cấu hình và không chạm môi trường. Những
thứ sau vẫn xanh hết dù có sai:

- cấu hình sai, build nhầm môi trường, nối nhầm database
- `[Authorize]` hỏng, routing sai, middleware lỗi
- schema Oracle lệch với code (`ORA-00904`, `ORA-12899`)
- câu SQL viết tay sai cú pháp

Muốn phủ những thứ đó cần một loại test khác — gọi API thật qua HTTP. Đó là việc riêng,
không phải mở rộng của bộ này.

## Hai cái bẫy đã gặp

**`[InlineData]` không mang được `decimal`** — C# không cho hằng `decimal` trong
attribute. Khai tham số `int?`/`double?` rồi ép trong thân test. Số nguyên viết cho
tham số `double?` phải có hậu tố `d` (`40000001d`), nếu không ném `ArgumentException`
lúc chạy chứ không phải lỗi biên dịch.

**Đừng bao giờ đưa khoá thật vào test.** `HMAC256Tests` dùng vector kiểm thử công khai
của RFC 4231.
