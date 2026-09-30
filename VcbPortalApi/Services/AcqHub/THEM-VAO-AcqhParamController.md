# `AcqhParamController.ShlxConfig` — thêm phần tạo user portal

Cập nhật 30/09/2026. Tạo user bằng chính **`ApiCommon.CreateUsers`** bạn gửi, qua
**`ApiUserPayload`** sẵn có — 5 cột Excel mới đều đã có chỗ trong lớp đó.

Toàn bộ đã biên dịch và chạy 130 test trong repo khung (`AcqHubShlxController` +
`ApiCommon` + `AcqHubShlxControllerTests`) trước khi viết ra đây.

Dán theo đúng thứ tự 3 mục dưới. Mục 1 thay method đang có, mục 2 và 3 là phần
thêm mới.

---

## 1. `ShlxConfig` — DÁN ĐÈ TOÀN BỘ method hiện tại

Bốn chỗ đổi so với bản đang chạy: kiểu tham số, thêm `CancellationToken`, dựng
`acqPayload` riêng, và gọi tiếp `TaoUserChoTerminalDaCai`.

```csharp
[Route("shlx")]
[HttpPost]
[HasPermission(Permissions.AcqhParamMid.Insert)]
public async Task<IActionResult> ShlxConfig(
    [FromBody] ShlxUploadRequest payload, CancellationToken cancellationToken)
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
    return await TaoUserChoTerminalDaCai(response, payload.Items, cancellationToken);
}
```

`ShlxConfigRequest` **giữ nguyên**, không sửa gì — nó vẫn là thứ đi vào trường
`data` ký gửi ACQHUB. Cái đổi là **tham số vào**: `ShlxUploadRequest`.

## 2. `TaoUserChoTerminalDaCai` — thêm mới, cùng class

Gọi thẳng `CreateUsers`. Cần `using Newtonsoft.Json;` và `using Newtonsoft.Json.Linq;`.

⚠️ **`AcqhParamController` phải với tới được `CreateUsers`** — nó `protected` trong
`ApiCommon`, nên controller phải kế thừa `ApiCommon`. Xem mục 4a.

```csharp
/// <summary>
/// UAT cho thấy cả 20 dòng MP_SHLX_USERS đều RoleId 28. Nếu lớp Roles bên bạn đã
/// có hằng tương ứng thì dùng hằng đó thay số này.
/// </summary>
private const decimal RoleShlxMacDinh = 28;

/// <summary>
/// Với mỗi terminal ACQHUB đã cài xong: chưa có user thì tạo bằng CreateUsers,
/// có rồi thì bỏ qua.
///
/// GỌI TỪNG DÒNG MỘT chứ không đưa cả mảng vào CreateUsers. Lý do: nó chỉ trả về
/// { total, success } — một con số — và bỏ qua mọi dòng hỏng một cách lặng lẽ.
/// Đưa 250 dòng vào rồi nhận "243" thì không biết 7 dòng nào trượt, mà lúc đó
/// ACQHUB ĐÃ cài xong cả 250 terminal, không rollback được.
///
/// Chi phí thêm gần như bằng không: CreateUsers duyệt mảng một vòng, phần chạm
/// DB vẫn đúng chừng ấy lần.
/// </summary>
private async Task<IActionResult> TaoUserChoTerminalDaCai(
    IActionResult response, List<ShlxUploadItem> items, CancellationToken cancellationToken)
{
    // SendAcqRequest trả ContentResult — Content(json, "application/json") — CHỨ
    // KHÔNG phải ObjectResult. Bắt cả hai kiểu: đổi bên đó một dòng là luồng này
    // im lặng bỏ qua việc tạo user, không lỗi, không log, chỉ thiếu khoá "users".
    object? goc = response switch
    {
        ContentResult c => c.Content,
        ObjectResult o => o.Value,
        _ => null
    };

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

        // CreateUsers tự chuẩn hoá lại y hệt dòng này. Tính trước chỉ để BÁO CÁO
        // đúng tên user mà nó sẽ ghi.
        var userName = item.UserName.KeepSafe().ToUpper();
        dong.user_name = userName;

        var roleId = item.RoleId ?? RoleShlxMacDinh;

        // MÀN HÌNH NÀY CHỈ ĐƯỢC TẠO USER SHLX.
        // ROLE_ID là một ô Excel do người dùng gõ. Đưa thẳng vào CreateUsers — nơi
        // chấp nhận cả role VCB, BCA, Merchant — thì ai có quyền cài SHLX cũng tự
        // tạo được tài khoản quản trị (RoleId 19) chỉ bằng cách sửa một ô trong file.
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
            // CreateUsers cũng kiểm điều này, nhưng nó `continue` lặng lẽ — "đã có
            // sẵn" và "ghi hỏng" đều ra success = 0. Hỏi trước để phân biệt được
            // hai thứ đó trên màn hình.
            var mpUser = await userService.GetUser(userName, cancellationToken);

            if (mpUser != null)
            {
                dong.message = "User da ton tai, giu nguyen.";
                continue;
            }

            var apiUser = new ApiUserPayload
            {
                UserName = userName,
                RoleId = roleId,
                FullName = item.FullName,
                Email = item.Email,
                Mobile = item.Mobile,

                // Company CHÍNH LÀ TerminalId — nhánh SHLX của CreateUsers viết
                // `TerminalId = user.Company.ToUpper().Trunc(10).Trim()`.
                // Để null là nó `continue` và không tạo gì cả.
                Company = item.TerminalId,

                // Password = null: để CreateUsers tự sinh "Shlx@yyMMdd". Đặt ở đây
                // là hai chỗ cùng quyết định một mật khẩu, sớm muộn cũng lệch.
                Password = null
            };

            var soTao = SoUserDaTao(await CreateUsers([apiUser], cancellationToken));

            dong.created = soTao > 0;
            dong.message = soTao > 0 ? "" : "CreateUsers khong tao user nao.";
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

    var json = body.ToString(Formatting.None);

    // Trả lại ĐÚNG KIỂU mà SendAcqRequest đã trả. Sửa Content ngay trên object cũ
    // để giữ nguyên ContentType và StatusCode — dựng Content() mới là mất chúng,
    // frontend nhận content-type khác mà không có gì báo.
    if (response is ContentResult ketQuaContent)
    {
        ketQuaContent.Content = json;
        return ketQuaContent;
    }

    return Ok(goc is string ? json : (object)body);
}

/// <summary>
/// Đọc `success` ra khỏi `Ok(new { total, success })` của CreateUsers. Kiểu vô
/// danh nên phải qua reflection — đổi CreateUsers sang trả về một lớp có tên thì
/// bỏ được hàm này.
/// </summary>
private static int SoUserDaTao(IActionResult result)
{
    if ((result as ObjectResult)?.Value is not { } value)
        return 0;

    return value.GetType().GetProperty("success")?.GetValue(value) as int? ?? 0;
}
```

## 3. Ba lớp dữ liệu mới — và vì sao KHÔNG nhét 5 cột vào `ShlxConfigItem`

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

    // ── 5 cột Ở LẠI portal, chuyển thẳng sang ApiUserPayload ───────────────
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

Năm trường user khớp 1-1 với `ApiUserPayload`, trừ `Company` — trường đó nhận
`TerminalId` chứ không phải một cột Excel riêng.

⚠️ **Viết hoa/thường phải GIỐNG `ShlxConfigItem` bên bạn.** Code bạn đang chạy
dùng `payload.Items` (chữ I hoa) và ACQHUB nhận được, nên `ToJsonString()` bên bạn
có đổi sang camelCase. Tôi viết PascalCase cho khớp. Nếu `ShlxConfigItem` bên bạn
lại viết thường thì đổi cả `ShlxUploadItem` cho giống — **quan trọng là hai lớp
cùng một kiểu**, vì `ToConfigItem()` gán thẳng field sang field. Lệch thì không
biên dịch được, sẽ báo ngay chứ không âm thầm.

`ShlxUserResult` thì cố ý để snake_case: nó nằm cạnh `results[]` của ACQHUB trong
cùng một response, để frontend đọc hai mảng theo cùng một kiểu.

---

## 4. BỐN chỗ phải tự kiểm

**a) `AcqhParamController` có với tới `CreateUsers` và `userService` không?**

`CreateUsers` là `protected` trong `ApiCommon`, `userService` cũng là thành viên
của lớp đó. Kiểm nhanh: gõ `CreateUsers(` trong method, xem IntelliSense có hiện không.

- **Có** → controller đã kế thừa `ApiCommon`, xong.
- **Không** → đổi lớp cha thành `ApiCommon`, hoặc chuyển `CreateUsers` lên chỗ
  dùng chung được.

Nếu không đụng được `userService` mà `CreateUsers` thì có, bỏ đoạn `GetUser` đi —
`CreateUsers` vẫn tự bỏ qua user đã tồn tại, chỉ là màn hình không phân biệt được
"đã có sẵn" với "ghi hỏng", cả hai ra chung một câu.

**b) Chữ ký `CreateUsers`.** Ảnh code cho thấy:

```csharp
protected async Task<IActionResult> CreateUsers(
    ApiUserPayload[] users, CancellationToken cancellationToken)
```

Tôi truyền `[apiUser]` (collection expression, C# 12). Bản cũ hơn thì viết
`new[] { apiUser }`.

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

**Cả 11 cột đều bắt buộc.** Frontend chặn không cho gửi nếu thiếu ô nào, và kiểm
thêm ba thứ hỏng âm thầm nếu để lọt:

| Cột | Luật | Không chặn thì sao |
|---|---|---|
| `ROLE_ID` | phải là 28 hoặc 29 | backend từ chối **sau khi** ACQHUB đã cài xong terminal — dòng đó thành "đã cài mà không có user", gửi lại không sửa được |
| `EMAIL` | phải có `@` và dấu chấm | `KeepSafe()` chỉ lọc ký tự lạ, không kiểm cấu trúc. Lưu sai thì hàng tháng sau mới lộ, lúc user quên mật khẩu mà thư không tới |
| `MOBILE` | 9 hoặc 10 chữ số | `PadLeft(10,'0').Trunc(10)`: 11 chữ số bị **cắt mất chữ số cuối**, lưu nhầm số mà không báo gì. 9 chữ số là Excel nuốt số 0 đầu, `PadLeft` trả lại đúng — chỉ thông báo |

`ShlxUploadItem.RoleId`/`FullName` vẫn để nullable và backend vẫn mặc định 28 /
lấy `UserName`. Đó là lớp đỡ cho ai gọi thẳng API, không phải đường đi bình thường.

## 6. Mật khẩu ban đầu — điểm cần bạn quyết

`CreateUsers` tự sinh `$"Shlx@{DateTime.Now:yyMMdd}"` khi `Password` là null. Công
thức **cố định**: mọi user tạo trong cùng một ngày đều **trùng mật khẩu**, và ai
đọc được dòng code đó cũng đoán ra.

Nó chỉ chấp nhận được nếu portal **bắt đổi mật khẩu ở lần đăng nhập đầu**. Nếu
không bắt, đây là lỗ hổng chứ không phải tiện lợi. Cần kiểm `Status = "0"` có
nghĩa là "phải đổi mật khẩu" hay chỉ là "đang hoạt động".

Muốn mỗi user một mật khẩu riêng thì thêm cột `PASSWORD` vào Excel và gán vào
`apiUser.Password` — `CreateUsers` đã có sẵn `user.Password ?? ...`.

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

Bốn thứ phải đúng:

1. `results[0].success == true`
2. `users[0].created == true`
3. `MP_USERS_COMMON` + `MP_SHLX_USERS` có dòng `SHLX_TEST_01`, `TERMINAL_ID` đúng
4. Dòng log `Request :` — giải base64 trường `data` ra **không được có** `userName`,
   `email`, `mobile`. Đây là chỗ hay hỏng nhất khi ai đó gộp hai lớp lại làm một.

Gọi lại lần hai: `results[0].success == false` ("already exist") và `users` rỗng.

Đổi `"roleId":28` thành `"roleId":19` rồi gọi với terminal mới: phải ra
`created: false` kèm câu "khong phai role SHLX", và **không** có user nào được tạo.

---

## Ghi chú: `new HttpClient()` mỗi lần gọi

`SendAcqRequest` tạo `HttpClient` mới cho từng request — cạn cổng TCP khi gọi
nhiều, socket nằm `TIME_WAIT` vài phút sau `Dispose`.

13 chỗ đang gọi nó nên không sửa trong task này. Nhưng SHLX là luồng **gửi lô**,
gọi dồn hơn các luồng khác, nên nếu thấy `SocketException: Only one usage of each
socket address` thì đây là nguyên nhân, và cách sửa là `IHttpClientFactory`.
