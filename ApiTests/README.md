# ApiTests

Test gọi API **thật đang chạy** qua HTTP. Không mock, không seed, không tham chiếu
code của service nào.

> **Đây là API test (end-to-end), KHÔNG phải unit test.** Gọi đúng tên để khỏi hiểu
> nhầm khi báo cáo. Nó test cả stack thật — routing, xác thực, cấu hình, Oracle —
> đổi lại không cô lập được thành phần nào và không ép được trạng thái DB.

## Chạy

```powershell
$env:VCB_API_BASEURL = "https://uat-host/api/v1"
$env:VCB_API_TOKEN   = "<bearer token cua mot user da dang nhap>"
$env:VCB_API_TID     = "40000001"
$env:VCB_API_MID     = "68100000000097"    # user role BID moi can

dotnet test ApiTests
```

Chưa đặt `VCB_API_BASEURL` thì test **skip**, không fail.

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

> **Skip không phải pass.** Đọc số `Skipped` trong kết quả.

## Cấu trúc

```
TestSupport/          ← hạ tầng dùng chung, gần như không sửa
├── ApiEnv.cs             cấu hình môi trường + [ApiFact] tự skip
├── ApiClient.cs          gọi HTTP
├── ApiResult.cs          đọc kết quả trả về
├── Jwt.cs                đọc claim trong token (không kiểm tra chữ ký)
└── AssemblyConfig.cs     tắt chạy song song

VcbPortalApi/         ← 1 file cho mỗi controller
└── MobilePartnerApiTests.cs

AddRedis/             ← service khác, sau này
```

Phụ thuộc chỉ đi một chiều: file test dùng `TestSupport/`, `TestSupport/` chỉ dùng
thư viện chuẩn .NET. Không có `ProjectReference` tới service nào — cố ý, xem cuối file.

## Thêm test cho endpoint mới

Copy `VcbPortalApi/MobilePartnerApiTests.cs`. Ba nhóm, viết theo thứ tự:

```csharp
private const string Endpoint = "ma/partner/token";

// 1. Khong co quyen -> 401
var api = new ApiClient(token: null);            // khong gan Authorization
var api = new ApiClient(token: "rac");           // token sai

// 2. Dau vao sai -> dung ma loi nghiep vu
var api = new ApiClient();
var result = await api.PostFormAsync(Endpoint, ("PartnerCode", null));
Assert.True(result.Field("code")?.Contains("PartnerCode") == true, result.Describe);

// 3. Duong thanh cong
var result = await api.PostFormAsync(Endpoint, ("PartnerCode", "PHONEPOS"), ("Tid", ApiEnv.Tid));
Assert.True(result.IsSuccess, result.Describe);
```

Service khác thì `new ApiClient(ApiEnv.AddRedis)` và `[ApiFact(ApiEnv.AddRedis)]`.

**Luôn truyền `result.Describe` vào Assert** — nó in method, URL, status và body, đủ để dựng
lại lời gọi từ log Test Explorer mà không phải mở Postman.

## Bốn quyết định thiết kế

**HttpClient dùng chung cho cả phiên chạy, mỗi service một cái.** Tạo mới mỗi test là
lỗi kinh điển của .NET: mỗi `HttpClient` giữ connection pool riêng, socket đóng rồi
vẫn nằm ở `TIME_WAIT` khoảng 4 phút, chạy vài chục test là cạn cổng và bắt đầu lỗi
`address in use` rải rác — rất khó lần ra. Vì client dùng chung nên token gắn theo
**từng request**, không đặt mặc định trên client; nhờ vậy test nhánh 401 vẫn dùng
chung client với các test khác.

**Tắt chạy song song** (`AssemblyConfig.cs`). Bộ test đánh vào một môi trường dùng
chung; song song sẽ dội request cùng lúc và làm phiền người khác đang test tay. Bỏ
dòng đó nếu môi trường chịu được tải.

**`Field()` tìm không phân biệt hoa thường, tìm cả object con.** Viết lỏng có chủ ý:
`code`, `Code`, `resCode` đều lấy được, khuôn response đổi không phải sửa test.

**Không tham chiếu project nào.** Ba lý do: chĩa vào local/UAT/production đều được;
code service đổi không làm test vỡ biên dịch (chỉ vỡ khi hợp đồng HTTP đổi — đúng thứ
cần biết); và test không với tới được secret trong `AppSettings`.

## Giới hạn — biết trước để khỏi mất công

| | |
|---|---|
| Chậm | mỗi test một vòng mạng |
| Dễ đỏ oan | môi trường sập, token hết hạn, người khác sửa dữ liệu |
| Đỏ không chỉ ra lỗi ở đâu | chỉ nói "có gì đó trong chuỗi hỏng" |
| Không ép được trạng thái DB | nhánh "user chưa có session" không chạm tới được |
| Không chạy được trong pipeline build | phải có môi trường sống |

**Token sống khoảng 42 phút.** Thấy tất cả test đỏ với 401 thì việc đầu tiên là lấy
token mới, đừng vội nghi code. Và token phải cùng môi trường với base URL — token UAT
gọi vào PRD sẽ 401 vì chữ ký không khớp.

Nhánh nào chỉ xảy ra khi DB ở trạng thái đặc biệt thì hoặc bỏ, hoặc chuẩn bị sẵn vài
user UAT cố định mỗi user một trạng thái rồi truyền username qua biến môi trường.

## Muốn có unit test thật

Bộ này không thay được unit test. Cách rẻ nhất để có dần: mỗi khi viết **hàm nghiệp vụ
thuần** mới (vào ra rõ ràng, không đụng DB), tách thành `static` rồi viết vài
`[Theory]` cho nó — chạy mili giây, không cần môi trường, đỏ là chỉ đúng một chỗ.

Đừng cố quay lại unit test cho các controller đã có: logic nằm hết trong action,
helper là `static`, cấu hình là `static` — muốn test phải sửa code đang chạy
production, không đáng.
