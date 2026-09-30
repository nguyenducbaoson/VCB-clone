# `AcqhParamController.ShlxConfig` — thêm phần tạo user portal

Cập nhật 30/09/2026. Method `ShlxConfig` bên bạn đang là:

```csharp
[Route("shlx")]
[HttpPost]
[HasPermission(Permissions.AcqhParamMid.Insert)]
public async Task<IActionResult> ShlxConfig([FromBody] ShlxConfigRequest payload)
{
    if (payload?.Items is null || payload.Items.Count == 0)
    {
        return HttpError.BaseError();
    }

    return await SendAcqRequest(ShlxConfigUrl, payload, minify: false);
}
```

Dưới đây là bản thay thế, cộng thêm việc tạo user portal cho những terminal
ACQHUB đã cài xong. Toàn bộ đã biên dịch và chạy 130 test trong repo khung
(`AcqHubShlxController` + `AcqHubShlxControllerTests`) trước khi viết ra đây.

---

## 1. Hai lớp dữ liệu MỚI — và vì sao KHÔNG nhét 5 cột vào `ShlxConfigItem`

`ShlxConfigItem` bị `ToJsonString()` rồi base64 vào trường `data` gửi sang ACQHUB.
Thêm thuộc tính vào nó là **thêm khoá vào bản tin gửi ra ngoài** — đúng lý do mục
"đừng gọi `AddAuditData`" trước đây.

Nặng hơn: `EMAIL` và `MOBILE` là dữ liệu cá nhân. Tách kiểu là thứ duy nhất bảo
đảm chúng không rời khỏi hệ thống VCB.

```csharp
/// <summary>Một dòng Excel: 6 cột của ACQHUB + 5 cột chỉ dùng để tạo user portal.</summary>
public class ShlxUploadItem
{
    // ── 6 cột gửi sang ACQHUB ──────────────────────────────────────────────
    public string MerchantId { get; set; } = "";
    public string TerminalId { get; set; } = "";
    public string TerminalName { get; set; } = "";
    public string DdAccountNumber { get; set; } = "";
    public string BranchCode { get; set; } = "";
    public string Province { get; set; } = "";

    // ── 5 cột Ở LẠI portal ─────────────────────────────────────────────────
    public string? UserName { get; set; }
    public decimal? RoleId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Mobile { get; set; }

    public ShlxConfigItem ToConfigItem() => new()
    {
        MerchantId = MerchantId,
        TerminalId = TerminalId,
        TerminalName = TerminalName,
        DdAccountNumber = DdAccountNumber,
        BranchCode = BranchCode,
        Province = Province,
    };
}

public class ShlxUploadRequest
{
    public List<ShlxUploadItem> Items { get; set; } = [];
}

/// <summary>Kết quả tạo user của MỘT terminal. KHÔNG đến từ ACQHUB.</summary>
public class ShlxUserResult
{
    public string terminal_id { get; set; } = "";
    public string user_name { get; set; } = "";
    public bool created { get; set; }
    public string message { get; set; } = "";
}
```

⚠️ **Viết hoa/thường phải GIỐNG `ShlxConfigItem` bên bạn.** Code bạn đang chạy
dùng `payload.Items` (chữ I hoa) và ACQHUB nhận được, nên `ToJsonString()` bên bạn
có đổi sang camelCase. Tôi viết PascalCase cho khớp. Nếu `ShlxConfigItem` bên bạn
lại viết thường thì đổi cả `ShlxUploadItem` cho giống — **quan trọng là hai lớp
cùng một kiểu**, vì `ToConfigItem()` gán thẳng field sang field.

`ShlxUserResult` thì cố ý để snake_case: nó nằm cạnh `results[]` của ACQHUB trong
cùng một response, để frontend đọc hai mảng theo cùng một kiểu.

## 2. Method `ShlxConfig` mới

```csharp
[Route("shlx")]
[HttpPost]
[HasPermission(Permissions.AcqhParamMid.Insert)]
public async Task<IActionResult> ShlxConfig([FromBody] ShlxUploadRequest payload)
{
    if (payload?.Items is null || payload.Items.Count == 0)
    {
        return HttpError.BaseError();
    }

    // Luồng này ghi cấu hình ở hệ thống ngoài, không rollback được, nên phải
    // truy ra được ai đã cài terminal nào.
    AppSettings.Logger.Warn(
        $"ShlxConfig: user={CurrentUserName} soLuong={payload.Items.Count} " +
        $"terminals={string.Join(",", payload.Items.Select(x => x.TerminalId))}");

    // ToConfigItem() bỏ lại 5 cột user — ACQHUB chỉ nhận đúng 6 cột nó khai.
    var acqPayload = new ShlxConfigRequest
    {
        Items = payload.Items.Select(x => x.ToConfigItem()).ToList()
    };

    var response = await SendAcqRequest(ShlxConfigUrl, acqPayload, minify: false);

    // ACQHUB TRƯỚC, user portal SAU. Ngược lại là tạo login cho một terminal có
    // thể chưa hề được cài — lỗi đó không tự lộ ra, user vẫn đăng nhập được, chỉ
    // là không có dữ liệu nào phía sau.
    return await TaoUserChoTerminalDaCai(response, payload.Items);
}
```

## 3. Hàm tạo user

Dán cùng class. Cần `using Newtonsoft.Json;` và `using Newtonsoft.Json.Linq;`.

```csharp
/// <summary>
/// UAT cho thấy cả 20 dòng MP_SHLX_USERS đều RoleId 28. Nếu lớp Roles bên bạn đã
/// có hằng tương ứng thì dùng hằng đó thay số này.
/// </summary>
private const decimal RoleShlxMacDinh = 28;

/// <summary>
/// Với mỗi terminal ACQHUB đã cài xong: chưa có user thì tạo, có rồi thì bỏ qua.
/// Mỗi dòng một kết quả, để màn hình chỉ được ra dòng nào hỏng vì sao.
/// </summary>
private async Task<IActionResult> TaoUserChoTerminalDaCai(
    IActionResult response, List<ShlxUploadItem> items)
{
    var goc = (response as ObjectResult)?.Value;

    if (goc == null)
        return response;   // SendAcqRequest trả lỗi -> không có terminal nào để gắn user

    JObject body;

    try
    {
        body = goc is string s ? JObject.Parse(s) : JObject.FromObject(goc);
    }
    catch (Exception ex)
    {
        // ACQHUB ĐÃ GHI XONG rồi. Không đọc được response là lỗi của portal, không
        // phải của lô — trả nguyên về để người dùng còn thấy kết quả cài đặt.
        AppSettings.Logger.Warn($"ShlxConfig: khong doc duoc response de tao user. {ex.Message}");
        return response;
    }

    var results = body.GetValue("results", StringComparison.OrdinalIgnoreCase) as JArray
                  ?? new JArray();

    var daCai = results
        .Where(x => x.Value<bool?>("success") == true)
        .Select(x => x.Value<string>("terminal_id") ?? "")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var ketQua = new List<ShlxUserResult>();

    foreach (var item in items)
    {
        // ACQHUB từ chối terminal này thì không có gì để gắn user vào.
        if (!daCai.Contains(item.TerminalId))
            continue;

        var dong = new ShlxUserResult { terminal_id = item.TerminalId };
        ketQua.Add(dong);

        if (string.IsNullOrWhiteSpace(item.UserName))
        {
            dong.message = "Thieu USER_NAME, khong tao duoc user.";
            continue;
        }

        var userName = item.UserName.KeepSafe().ToUpper();
        dong.user_name = userName;

        var roleId = item.RoleId ?? RoleShlxMacDinh;

        // MÀN HÌNH NÀY CHỈ ĐƯỢC TẠO USER SHLX.
        // ROLE_ID là một ô Excel do người dùng gõ. Nhận thẳng rồi đẩy sang
        // CreateUsers — nơi chấp nhận cả role VCB, BCA, Merchant — thì ai có quyền
        // cài SHLX cũng tự tạo được tài khoản quản trị (RoleId 19) chỉ bằng cách
        // sửa một ô trong file Excel.
        if (!Roles.IsShlxRoles(roleId))
        {
            dong.message = $"ROLE_ID {roleId} khong phai role SHLX (28, 29). Bo qua.";

            AppSettings.Logger.Warn(
                $"ShlxConfig: user={CurrentUserName} thu tao user {userName} " +
                $"voi ROLE_ID {roleId} ngoai nhom SHLX.");

            continue;
        }

        try
        {
            // "Đã tồn tại chưa" — đúng chốt `if (mpUser != null) continue;` của
            // ApiCommon.CreateUsers.
            var mpUser = await userService.GetUser(userName);

            if (mpUser != null)
            {
                dong.message = "User da ton tai, giu nguyen.";
                continue;
            }

            var newUser = new MpUserFull
            {
                UserName = userName,
                RoleId = roleId,
                FullName = string.IsNullOrWhiteSpace(item.FullName)
                    ? userName
                    : item.FullName.Trim(),
                BranchId = 1,
                Status = "0",
                Email = item.Email?.KeepSafe().ToLower(),
                Mobile = item.Mobile?.KeepNumber().PadLeft(10, '0').Trunc(10),
                Avatar = null,
                TerminalId = item.TerminalId.ToUpper().Trunc(10).Trim(),
                UserUpdate = AppSettings.AdminUsername
            };

            var password = $"Shlx@{DateTime.Now:yyMMdd}";

            var ghiDuoc = await userService.CreateNewUser(newUser, password);

            dong.created = ghiDuoc > 0;
            dong.message = ghiDuoc > 0 ? "" : "Khong ghi duoc user.";
        }
        catch (Exception ex)
        {
            // ACQHUB đã ghi xong cả lô. Ném ra giữa chừng là bỏ dở những dòng phía
            // sau mà không ai biết đã tới đâu.
            AppSettings.Logger.Error(ex,
                $"ShlxConfig: tao user {userName} cho terminal {item.TerminalId} that bai.");

            dong.message = "Loi khi tao user, xem log.";
        }
    }

    body["users"] = JArray.FromObject(ketQua);

    // Trả lại ĐÚNG KIỂU mà SendAcqRequest đã trả: chuỗi thì chuỗi, object thì
    // object. Đổi kiểu ở đây là frontend nhận khác đi mà không có gì báo.
    return Ok(goc is string ? body.ToString(Formatting.None) : (object)body);
}
```

## 4. BỐN chỗ phải tự kiểm trước khi chạy

**a) `userService` có sẵn trong `AcqhParamController` chưa?**

`ApiCommon.CreateUsers` gọi `userService.GetUser(...)` không qualify, nên nó là
field/tham số của `ApiCommon`. Nếu `AcqhParamController` **kế thừa** `ApiCommon`
thì dùng được ngay. Nếu không, thêm vào constructor:

```csharp
public AcqhParamController(IUserService userService, ...)
```

**b) Chữ ký hai hàm.** Ảnh code cho thấy:

```csharp
var mpUser = await userService.GetUser(userName, cancellationToken);
if (await userService.CreateNewUser(newUser, password, cancellationToken: cancellationToken) > 0)
```

`ShlxConfig` bên bạn **không nhận `CancellationToken`**. Hoặc thêm tham số
`CancellationToken cancellationToken` vào `ShlxConfig` rồi chuyền xuống (nên làm —
một lô 250 dòng mà người dùng đóng tab thì không có gì cắt được), hoặc gọi không
truyền như đoạn trên nếu hai tham số đó optional.

**c) `AppSettings.Logger.Error(ex, string)`** — kiểm đúng overload của NLog bên bạn.
Nếu không có, đổi thành `AppSettings.Logger.Error($"... {ex}")`.

**d) `SendAcqRequest` trả `Ok(string)` hay `Ok(object)`?**

Đoạn trên xử lý cả hai và **trả lại đúng kiểu đã nhận**, nên không phải sửa. Chỉ
cần biết để đọc log khi có vấn đề.

## 5. Thứ tự cột Excel — CHỖ DỄ SAI NHẤT

Frontend đọc Excel **theo vị trí cột**, không theo tiêu đề. Tôi đang giả định 5 cột
mới nằm ngay sau `BRANCH_CODE`:

| A | B | C | D | E | F | G | H | I | J | K |
|---|---|---|---|---|---|---|---|---|---|---|
| TERMINAL_ID | DD_ACCOUNT_NUMBER | TERMINAL_NAME | MERCHANT_ID | PROVINCE | BRANCH_CODE | USER_NAME | ROLE_ID | FULLNAME | EMAIL | MOBILE |

**File mẫu của bạn xếp khác là dữ liệu vào sai ô mà không lỗi nào báo.** Gửi tôi
ảnh dòng tiêu đề, hoặc tự đổi thứ tự trong `ezTable([...])` của
`ishlx-config.component.ts` cho khớp.

Bắt buộc: `USER_NAME`. Còn lại bỏ trống được — `ROLE_ID` mặc định 28, `FULLNAME`
mặc định lấy chính `USER_NAME`.

## 6. Mật khẩu ban đầu — điểm cần bạn quyết

`$"Shlx@{DateTime.Now:yyMMdd}"` là công thức **cố định** lấy từ nhánh SHLX của
`ApiCommon.CreateUsers`. Nghĩa là mọi user tạo trong cùng một ngày đều **trùng mật
khẩu**, và ai đọc được dòng code đó cũng đoán ra.

Nó chỉ chấp nhận được nếu portal **bắt đổi mật khẩu ở lần đăng nhập đầu**. Nếu
không bắt, đây là lỗ hổng chứ không phải tiện lợi. Cần kiểm `Status = "0"` có
nghĩa là "phải đổi mật khẩu" hay chỉ là "đang hoạt động".

## 7. Response sau khi thêm

```json
{
  "code": "01",
  "message": "Failed|One or more SHLX items failed",
  "results": [
    { "terminal_id": "V827308199", "success": true,  "result_code": "00", "result_message": "..." },
    { "terminal_id": "V827308100", "success": false, "result_code": "01", "result_message": "... already exist ..." }
  ],
  "users": [
    { "terminal_id": "V827308199", "user_name": "SHLX_CUCHI", "created": true,  "message": "" }
  ]
}
```

`users` **chỉ có dòng của terminal ACQHUB đã cài xong** — `V827308100` hỏng nên
không có dòng user nào. Đó là chủ ý, không phải thiếu.

## 8. Kiểm sau khi ghép

```bash
curl -sS -X POST http://localhost:8090/apimp/acqh/para/shlx \
  -H "Authorization: $TK" -H "Content-Type: application/json" \
  -d '{"items":[{"merchantId":"DVCBCAG03","terminalId":"V827308100",
       "terminalName":"CATQNINH_DSH1","ddAccountNumber":"2266168888",
       "branchCode":"01400","province":"QUANGNINH",
       "userName":"SHLX_TEST_01","roleId":28,"fullName":"Trung tam test",
       "email":"test@example.test","mobile":"0912345678"}]}'
```

Ba thứ phải đúng:

1. `results[0].success == true`
2. `users[0].created == true`
3. Dòng log `Request :` — giải base64 trường `data` ra **không được có** `userName`,
   `email`, `mobile`. Đây là chỗ hay hỏng nhất khi ai đó gộp hai lớp lại làm một.

Gọi lại lần hai: `results[0].success == false` ("already exist") và `users` rỗng.

---

## Ghi chú: `new HttpClient()` mỗi lần gọi

`SendAcqRequest` tạo `HttpClient` mới cho từng request — cạn cổng TCP khi gọi
nhiều, socket nằm `TIME_WAIT` vài phút sau `Dispose`.

13 chỗ đang gọi nó nên không sửa trong task này. Nhưng SHLX là luồng **gửi lô**,
gọi dồn hơn các luồng khác, nên nếu thấy `SocketException: Only one usage of each
socket address` thì đây là nguyên nhân, và cách sửa là `IHttpClientFactory`.
