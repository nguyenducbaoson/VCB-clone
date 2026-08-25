# VcbPortalApi.ApiTests

Test gọi API **thật đang chạy** qua HTTP. Không mock, không seed, không tham chiếu
code của API.

## Chạy

```powershell
$env:VCB_API_BASEURL = "https://uat-host/api/v1"
$env:VCB_API_TOKEN   = "<bearer token cua mot user da dang nhap>"
$env:VCB_API_TID     = "40000001"
$env:VCB_API_MID     = "68100000000097"   # user role BID moi can

dotnet test VcbPortalApi.ApiTests
```

Chưa đặt `VCB_API_BASEURL` + `VCB_API_TOKEN` thì test **skip**, không fail.

Đổi môi trường = đổi biến. Cùng bộ test chĩa vào local, UAT hay production đều được.
Không có gì trong source cần sửa.

> **Skip không phải pass.** Đọc số `Skipped` trong kết quả.

Token lấy bằng cách đăng nhập trên app hoặc gọi API login rồi copy ra. Đừng commit
token vào git.

## Cấu trúc

```
TestSupport/ApiEnv.cs      cấu hình + [ApiFact]/[ApiTheory] tự skip
TestSupport/ApiClient.cs   gọi HTTP, đọc field JSON không phân biệt hoa thường
TestSupport/Jwt.cs         đọc claim trong token trả về (không kiểm tra chữ ký)
MobilePartnerApiTests.cs   MẪU — copy file này cho endpoint mới
```

Toàn bộ khung là 3 file trong `TestSupport/`. Thêm endpoint mới chỉ thêm 1 file test.

## Viết test cho endpoint mới

Copy `MobilePartnerApiTests.cs`. Ba nhóm, viết theo thứ tự:

```csharp
private const string Endpoint = "ma/partner/token";

// 1. Không có quyền -> 401
using var api = new ApiClient(token: null);        // không gắn Authorization
using var api = new ApiClient(token: "rac");       // token sai

// 2. Đầu vào sai -> đúng mã lỗi nghiệp vụ
var res = await api.PostFormAsync(Endpoint, ("PartnerCode", null));
Assert.True(res.Field("code")?.Contains("PartnerCode") == true, res.MoTa);

// 3. Đường thành công
var res = await api.PostFormAsync(Endpoint, ("PartnerCode", "PHONEPOS"), ("Tid", ApiEnv.Tid));
Assert.Equal(HttpStatusCode.OK, res.Status);
```

`ApiClient` có `PostFormAsync`, `PostJsonAsync`, `GetAsync`.

Mọi `Assert` nên truyền kèm `res.MoTa` — nó in ra status và body, để khi fail biết
ngay API trả về cái gì thay vì phải chạy lại bằng Postman.

`res.Field("code")` tìm field trong JSON **không phân biệt hoa thường và tìm cả
trong object con**. Cố ý viết lỏng để không phụ thuộc khuôn response — `code`,
`Code`, `resCode` đều lấy được, khuôn đổi cũng không phải sửa test.

## Được gì và mất gì so với unit test

| | Test API (bộ này) | Unit test |
|---|---|---|
| Chạy trên stack thật (routing, `[Authorize]`, middleware, Oracle, cấu hình) | có | không |
| Bắt lỗi "local ngon, UAT chết" | có | không |
| Dựng được trạng thái DB tuỳ ý | **không** | có |
| Cần môi trường chạy được | cần | không |
| Tốc độ | vài giây/test | mili giây |

Hệ quả quan trọng: **nhánh nào chỉ xảy ra khi DB ở trạng thái đặc biệt thì không
kiểm tra được từ đây.** Ví dụ với `IssueSsoToken`: user chưa có session, email rỗng,
mid/tid không thuộc bid — muốn chạm tới phải sửa dữ liệu UAT, mà làm vậy sẽ phá dữ
liệu của người khác đang test tay.

Những nhánh đó hoặc chấp nhận bỏ, hoặc chuẩn bị sẵn vài user UAT cố định mỗi user
một trạng thái, rồi đặt username qua biến môi trường.

## Đừng ghi vào môi trường đang dùng chung

Test hiện tại chỉ phát token, không sửa dữ liệu. Giữ nguyên tính chất đó. Endpoint
nào có ghi thì hoặc dùng user test riêng, hoặc dọn dẹp ngay trong test.

Và cân nhắc kỹ trước khi chĩa vào **production** — dù chỉ đọc, mỗi lần chạy vẫn là
lưu lượng thật, log thật, và có endpoint tính vào hạn mức.
