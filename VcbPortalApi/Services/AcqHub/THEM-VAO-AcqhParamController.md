# Thêm endpoint SHLX vào `AcqhParamController`

Đoạn dán vào `AcqhParamController` trong solution thật.

**Không chép `AcqHubShlxClient` sang.** Nó viết lại đúng thứ `SendAcqRequest` đã
làm — bản khung không có `AcqRequest`/`Crypto`/`AppSettings.Cfg` nên buộc phải
dựng lại. Solution thật có sẵn, dùng thẳng thì chỉ còn **một** chỗ tính `checkSum`
trong cả hệ thống.

Giữ lại từ repo này: hai lớp **`ShlxConfigItem`** và **`ShlxConfigRequest`** —
chỉ là hình dạng dữ liệu, không trùng gì.

---

## 1. Khai URL ở đầu class

Cạnh 4 dòng sẵn có. **Phải là URL ĐẦY ĐỦ, không phải đường dẫn:**

```csharp
private readonly string ShlxConfigUrl = AppSettings.Cfg["AcqHub:ShlxConfigUrl"]!;
```

Lý do — dòng đầu của `SendAcqRequest`:

```csharp
using var client = new HttpClient() { BaseAddress = new Uri(url) };
...
var response = await client.PostAsync(url, content);
```

`new Uri(url)` ném `UriFormatException` nếu url là đường dẫn tương đối. Nên khoá
`AcqHub:ShlxConfigPath` bạn vừa thêm cần đổi tên và điền đủ host:

```jsonc
"AcqHub": {
  "ShlxConfigUrl": "http://__ACQHUB_HOST__:8829/api/acqhub/configpartner/v1/shlxconfig"
}
```

Đồng bộ luôn với 4 khoá `*Url` sẵn có.

## 2. Đoạn dán vào `AcqhParamController`

```csharp
/// <summary>
/// Cài đặt terminal SHLX qua ACQHUB. Màn hình portal gửi lên danh sách terminal
/// đọc từ file Excel; mỗi terminal có kết quả riêng trong results[].
/// </summary>
[Route("shlx")]
[HttpPost]
[HasPermission(Permissions.AcqhParamMid.Insert)]   // <- CAN CHON DUNG MUC, xem muc 5
public async Task<IActionResult> ShlxConfig([FromBody] ShlxConfigRequest payload)
{
    if (payload?.items is null || payload.items.Count == 0)
        return HttpError.BaseError();

    // Luồng này ghi cấu hình ở hệ thống ngoài, không rollback được, nên phải
    // truy ra được ai đã cài terminal nào.
    AppSettings.Logger.Warn(
        $"ShlxConfig: user={CurrentUserName} soLuong={payload.items.Count} " +
        $"terminals={string.Join(",", payload.items.Select(x => x.terminalId))}");

    // KHÔNG gọi AddAuditData — xem mục 4.
    return await SendAcqRequest(ShlxConfigUrl, payload);
}
```

Route đầy đủ: `{tiền tố}/acqh/para/shlx`. Tiền tố do `[RouteGroup(RouteTags.Main)]`
ở đầu class sinh ra, nên Pilot tự thành `apivp` — thứ mà chuỗi cứng `"apimp/..."`
không làm được.

## 3. Hai lớp dữ liệu

```csharp
public sealed class ShlxConfigItem
{
    public string merchantId { get; set; } = "";
    public string terminalId { get; set; } = "";
    public string terminalName { get; set; } = "";
    public string ddAccountNumber { get; set; } = "";
    public string branchCode { get; set; } = "";     // 5 ky tu, vi du "01400"
    public string province { get; set; } = "";
}

public sealed class ShlxConfigRequest
{
    public List<ShlxConfigItem> items { get; set; } = [];
}
```

Tên thuộc tính viết thường để `payload.ToJsonString()` sinh ra đúng bản tin mẫu.
Nếu `ToJsonString()` dùng camelCase naming policy thì đặt `PascalCase` cũng được
— kiểm một lần bằng dòng log `Payload : ` mà `SendAcqRequest` in ra.

## 4. ĐỪNG gọi `AddAuditData` cho luồng này

`AddAuditData` thêm **ba khoá vào gốc payload**:

```csharp
public VcbQrAccountPayload AddAuditData(AcqAuditData acqAuditData)
{
    RequestId = Guid.NewGuid().ToString("N");
    Action    = "CREATE";
    AuditData = acqAuditData;
    return this;
}
```

Payload đó thành trường `data` (base64) gửi sang ACQHUB. Bản tin mẫu của
shlxconfig chỉ có `{"items":[...]}` — thêm `RequestId`/`Action`/`AuditData` là gửi
khoá mà ACQHUB không khai. Có thể nó bỏ qua, cũng có thể từ chối.

Muốn giữ dấu vết thì ghi log phía portal như dòng `AppSettings.Logger.Warn` ở
mục 2, đừng nhét vào bản tin gửi ra ngoài.

## 5. Còn hai chỗ phải tự kiểm

**a) `Permissions` nào?**

Tôi để tạm `AcqhParamMid.Insert`. SHLX là cấu hình terminal, có thể cần mục riêng.

**b) `Minify()` có giữ `results[]` không?**

```csharp
var obj    = ParseAcqHubResponse(data);
var result = obj.Minify().ToJsonString();
return Ok(result);
```

Frontend đọc `results[].success` cho **từng terminal**. Nếu `Minify()` cắt bớt
trường — nhiều hàm tên kiểu này bỏ null/rỗng — thì `results` có thể mất, và màn
hình sẽ báo "0 of N succeeded" dù ACQHUB đã ghi xong.

Kiểm bằng dòng log `Response : ` rồi so với thứ trả về cho Angular. Nếu lệch, dùng
`Ok(data)` trả nguyên response thô cho riêng luồng này.

## 6. Sửa phía Angular

Component đang gọi `'acqh/shlx/cfg?_='`. Đổi thành:

```ts
environment.mainEndpoint + 'acqh/para/shlx?_=' + Date.now()
```

## 7. Kiểm sau khi ghép

```bash
curl -sS -X POST http://localhost:8090/apimp/acqh/para/shlx \
  -H "Authorization: $TK" -H "Content-Type: application/json" \
  -d '{"items":[{"merchantId":"DVCBCAG03","terminalId":"V827308100",
       "terminalName":"CATQNINH_DSH1","ddAccountNumber":"2266168888",
       "branchCode":"01400","province":"QUANGNINH"}]}'
```

Mong đợi `results[0].success == true`.

Ba dòng log `Payload :`, `Request :`, `Response :` mà `SendAcqRequest` in ra là
chỗ xem nhanh nhất khi có vấn đề — `Request` cho thấy `data`/`requestTime`/`checkSum`
thực sự gửi đi, `Response` cho thấy ACQHUB trả gì trước khi bị `Minify()` đụng vào.

---

## Ghi chú: `new HttpClient()` mỗi lần gọi

`SendAcqRequest` tạo `HttpClient` mới cho từng request. Cách này làm cạn cổng TCP
khi gọi nhiều — socket ở trạng thái `TIME_WAIT` vài phút sau khi `Dispose`.

13 chỗ đang gọi nó, nên đây không phải việc sửa trong task này. Nhưng SHLX là
luồng **gửi lô**, gọi dồn hơn các luồng khác, nên nếu sau này thấy lỗi
`SocketException: Only one usage of each socket address` thì đây là nguyên nhân,
và cách sửa là `IHttpClientFactory`.
