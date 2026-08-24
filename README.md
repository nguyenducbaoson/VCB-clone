# SSO Digibank → DigiMerchant — phần BE

## Merchant authorization API

Endpoint mới nhận request từ client và gọi tiếp Merchant API:

`POST /api/v1/merchant-info/authorization`

Request body:

```json
{
  "bid": "xxxx",
  "mid": "xxxx",
  "tid": "xxxx",
  "auditData": {
    "channel": "appxxx",
    "channelIp": "appxxx",
    "channelUser": "appxxx",
    "channelUserBranch": "appxxx",
    "channelTime": "20260822111111"
  },
  "requestID": "xxxxxxxxxxxxxxxxxxx"
}
```

Client phải gửi `Authorization: Bearer <token>`. Controller chuyển tiếp token,
request body, HTTP status và response body tới `http://localhost:8095`.

Đăng ký trong `Program.cs` của solution chính:

```csharp
builder.Services.AddHttpClient<IMerchantInfoAuthorizationClient, MerchantInfoAuthorizationClient>(client =>
{
    var baseUrl = builder.Configuration["MerchantInfoApi:BaseUrl"]
        ?? throw new InvalidOperationException("Thiếu MerchantInfoApi:BaseUrl");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int?>("MerchantInfoApi:TimeoutSeconds") ?? 15);
});
```

Ghép vào solution **VcbPortalApi**, dùng lại envelope SSO có sẵn (`SsoBaseMessage` và họ hàng).

**Đúng 1 endpoint BE, gọi đúng 1 API của SSO.**

```
AppDigBank --deep link (one time token)--> AppMerchant
                                                |
                                POST /api/v1/merchant-sso/login
                                                |
                                    ValidateTokenRequest (đã ký)
                                                v
                            VCB SSO /TokenManager/ValidateAccessToken
                                                |
                              payload.othersInfo { bid, mid, tid, userDes }
                                                |
                                    đối chiếu MP_APP_USERS
                                                |
                              requirePassword = true / false
```

`requirePassword = true` → AppMerchant hiện màn hình nhập mật khẩu (UC-05).
`requirePassword = false` → vào thẳng trang chủ DigiMerchant (UC-06).

## File thêm vào solution

```
Models/SSO/ClientContext.cs              <- DTO thiết bị AppMerchant gửi lên
Models/SSO/MerchantSsoLoginRequest.cs    <- request + result
Models/SSO/ApiResponse.cs                <- wrapper + bảng mã lỗi (xem TODO trong file)
Models/SSO/MpSsoContracts.cs             <- CHỈ LÀ GHI CHÚ patch, không có class
Services/Sso/IMpSsoClient.cs
Services/Sso/MpSsoClient.cs              <- gọi ValidateAccessToken
Services/Sso/MpSsoVerifyResult.cs
Services/Sso/MpSsoOptions.cs
Services/MpSsoAuthService.cs             <- LÕI: đối chiếu + truy vấn DB + ghi log
Controllers/MerchantSsoController.cs
```

Không có tầng repository — theo convention của solution, truy vấn `MP_APP_USERS` và ghi
`MP_SSO_LOG` nằm luôn trong `MpSsoAuthService` qua DbContext.

## VIỆC PHẢI LÀM TRƯỚC KHI BUILD

**1. Thêm `othersInfo` vào `ValidateTokenResponsePayload`** (file `Models/SSO/ValidateTokenRequest.cs`):

```csharp
public Dictionary<string, object>? othersInfo { get; set; }
public string? custClass { get; set; }
```

Không thêm thì Newtonsoft bỏ qua im lặng, `othersInfo` luôn null, và **mọi request đều fail**.

**2. Đổi `VcbPortalDbContext`** thành tên DbContext thật (`TODO(DbContext)` trong `MpSsoAuthService`).

**3. Đối chiếu tên property entity `MP_APP_USERS`** (`TODO(entity)`). Giả định theo quy tắc
scaffold EF: `USERNAME`→`Username`, `DEVICEID`→**`Deviceid`**, `FCM_TOKEN`→`FcmToken`,
`BRANCH_ID`→`BranchId`. Sai thì compiler chỉ thẳng vào dòng cần sửa.

**4. Điền `MpSso:BaseUrl`** — hiện là placeholder `https://sso-uat.placeholder.local`.

**5. Kiểm tra `AppSettings` có key HMAC cho UAT không** — hằng số tên là `SsoPrdHmacSecretKey`
("Prd"). Test UAT bằng key Prd thì chữ ký sai và SSO từ chối mà không nói lý do.

## Đăng ký DI

```csharp
builder.Services.Configure<MpSsoOptions>(builder.Configuration.GetSection(MpSsoOptions.SectionName));
builder.Services.Configure<MpAuthOptions>(builder.Configuration.GetSection(MpAuthOptions.SectionName));
builder.Services.AddScoped<IMpSsoAuthService, MpSsoAuthService>();

var mpSso = builder.Configuration.GetSection(MpSsoOptions.SectionName).Get<MpSsoOptions>()!;
builder.Services.AddHttpClient<IMpSsoClient, MpSsoClient>(c =>
{
    c.BaseAddress = new Uri(mpSso.BaseUrl);
    c.Timeout = TimeSpan.FromSeconds(mpSso.TimeoutSeconds);
});
```

`ModelState.IsValid` trong controller chỉ chạy khi tắt filter 400 tự động:

```csharp
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
```

Đây là setting **toàn app**. Nếu solution đang dựa vào hành vi mặc định cho controller khác
thì đừng đổi — thay vào đó bỏ đoạn check `ModelState` trong `MerchantSsoController`.

## API

### `POST /api/v1/merchant-sso/login`

```json
{
  "accessTokenSSO": "...",
  "client": {
    "sessionId": "phiên-thiết-bị",
    "clientIp": "203.0.113.45",
    "deviceId": "imei-hoặc-device-token",
    "deviceType": "iPhone14,5",
    "appVersion": "3.2.1",
    "osType": "IOS",
    "osVersion": "17.4"
  }
}
```

```json
{
  "code": "00",
  "message": "Success",
  "data": {
    "username": "user_digimerchant_01",
    "bid": 1, "mid": 1, "tid": 1,
    "roleId": 2, "branchId": 10,
    "userCif": "24094302",
    "userFullName": "NGUYEN VAN TIEN",
    "requirePassword": false,
    "requirePasswordReason": null
  }
}
```

## Response của SSO — 2 chỗ khác tài liệu

**`header`/`payload` là OBJECT lồng, không phải chuỗi JSON** như `SsoBaseMessage` khai báo.
Deserialize thẳng vào `SsoBaseResponse` sẽ ném *"Unexpected token: StartObject"*.
`MpSsoClient` đọc bằng `JObject` và chấp nhận **cả hai dạng**.

Hệ quả: chữ ký tính trên `header + payload`, mà khi chúng là object thì mình phải serialize
lại — lệch một khoảng trắng hay thứ tự key là chữ ký sai dù bản tin hợp lệ. Khi lệch, log ghi
đủ `expected`, `actual` và chuỗi đã ký. Nếu UAT lệch liên tục, hỏi VCB chính xác chuỗi nào
được ký, hoặc tạm đặt `MpSso:VerifyResponseSignature = false` để đi tiếp.

Chữ ký mẫu 64 ký tự hex hoa ⇒ HMAC-SHA256 + `.ToUpper()`, khớp `ValidateTokenRequest.CreateSignature()`.

## BID/MID/TID — điểm cần BA/VCB chốt

SSO trả `"bid": "B001"`, `"mid": "M001"`, `"tid": "T001"` — **có chữ**.
Cột `MP_APP_USERS.BID/MID/TID` là Oracle **NUMBER**. Không có cách so nào đúng chắc chắn
cho tới khi biết `"B001"` ứng với giá trị nào trong DB.

Chọn bằng `MpAuth:HierarchyCompare`:

| Giá trị | Cách so | `"B001"` vs `BID` |
|---|---|---|
| `DigitsOnly` *(mặc định)* | bỏ chữ, so số | `1` — khớp nếu `BID = 1` |
| `Numeric` | parse nguyên chuỗi | luôn lệch |
| `Exact` | so chuỗi với `BID.ToString()` | khớp nếu DB lưu đúng `"B001"` |
| `Skip` | chỉ so username | bỏ qua |

Mặc định `DigitsOnly` để luồng chạy được, nhưng **phải đối chiếu dữ liệu UAT thật rồi chốt lại**.
Nếu `BID` trong DB là `1001` mà SSO gửi `"B001"` thì mọi khách sẽ bị chặn.

**Username vẫn so chính xác tuyệt đối** — đó là chốt định danh, BID/MID/TID chỉ là kiểm tra phụ.

## Nguyên tắc bảo mật

- **Username tra DB lấy từ `othersInfo.userDes` của SSO, không lấy từ request.** Client chỉ
  gửi token, không gửi username — không có cách nào đổi username để mượn tài khoản khác.
- Verify chữ ký response mặc định bật.
- **Token không bao giờ ghi ra `ILogger`.** Vẫn ghi vào `MP_SSO_LOG.TOKEN` — đó là convention
  sẵn có của luồng SSO hiện tại, là bảng DB có phân quyền, và token là one-time.
- Chi tiết sai lệch (giá trị SSO vs DB) chỉ ghi log, không trả về client.
- Mọi trường hợp không chắc chắn về thiết bị đều nghiêng về **yêu cầu nhập mật khẩu**.

## `clientIp` — điểm dễ hỏng nhất khi lên UAT

SSO so khớp `ValidateTokenRequestPayload.clientIP` với IP lúc cấp token, sai thì trả `resCode 12`.
Phải là IP **thiết bị khách hàng**, không phải IP server DigiMerchant.

- AppMerchant **nên gửi tường minh** `client.clientIp`.
- Không gửi thì BE lấy `RemoteIpAddress`, quy về IPv4 (`::ffff:10.0.0.1` → `10.0.0.1`,
  `::1` → `127.0.0.1`) vì tài liệu giới hạn 15 ký tự, rồi ghi log cảnh báo.
- BE sau gateway/LB thì phải bật `ForwardedHeaders` và khai `KnownProxies`.

## Cờ "đã xác thực lần đầu" (BR-08/BR-09)

`MP_APP_USERS` không có cột cờ riêng nên dùng cột **`DEVICEID`**.
Luồng này chỉ **ĐỌC**, không ghi — việc ghi thuộc luồng đăng nhập/kích hoạt thiết bị sẵn có
của DigiMerchant. Đúng tinh thần BR-09: kích hoạt ở máy khác thì `DEVICEID` đổi, lần SSO sau
bị bắt nhập lại mật khẩu.

| `DEVICEID` trong DB | `deviceId` client gửi | Kết quả |
|---|---|---|
| rỗng | bất kỳ | `requirePassword = true` |
| khác | khác | `requirePassword = true` — đổi thiết bị |
| khớp | khớp | `requirePassword = false` — vào thẳng |
| bất kỳ | không gửi | `requirePassword = true` — fail-safe |

Tắt bằng `MpAuth:UseDeviceIdForFirstAuth = false` → luôn bắt nhập mật khẩu.

## Chưa kiểm tra được

- **BR-12/BR-13 (user bị khóa)**: `MP_APP_USERS` không có cột trạng thái, `ValidateAccessToken`
  cũng không trả trạng thái user DigiMerchant. Hiện chỉ kiểm tra user có tồn tại (mã `20`).
- **BR-01/BR-02 (liên kết Active)**: `othersInfo` không trả trạng thái liên kết. Nhưng SSO chỉ
  cấp token khi có `accountLinkCode`, nên token hợp lệ đã hàm ý liên kết tồn tại.
- **`userCIF` là `int`** trong `ValidateTokenResponsePayload` (mẫu: `24094302`). Nếu CIF thật
  có số 0 đứng đầu thì đã mất từ lúc parse — khi đó phải đổi property sang `string`.
  Đáng ngờ vì `MpSsoLog.UserCif` lại là `string`.

## Ghi log tra soát — `MP_SSO_LOG`

Mọi lần gọi (thành công lẫn thất bại) đều ghi một dòng qua `MpSsoAuthService.WriteSsoLogAsync`.
Hàm này **nuốt mọi exception** và `Detach` entity khỏi change tracker — mất một dòng log không
đáng để chặn khách đăng nhập, và cũng không được để nó làm hỏng `SaveChanges` sau đó.

`MP_SSO_LOG` không có cột cho MID/TID, username DigiMerchant, deviceId hay requirePassword,
nên dồn vào cột `Response` dạng JSON. Cột `Response` là VARCHAR2 nên cắt ở 4000 ký tự —
**chỉnh cho khớp độ dài thật** (`TODO(DDL)`).

## Mã lỗi trả về AppMerchant

| Code | Ý nghĩa | resCode SSO |
|---|---|---|
| 00 | Thành công | 0 |
| 01 | Request không hợp lệ | — |
| 10 | Token SSO không hợp lệ | 10 |
| 11 | Client IP không hợp lệ | 12 |
| 12 | SSO timeout / không kết nối được | 99–199 |
| 13 | Response sai định dạng hoặc chữ ký không hợp lệ | — |
| 20 | Username không có trong MP_APP_USERS | — |
| 22 | Username lệch | — |
| 23 | BID/MID/TID lệch | — |
| 99 | Lỗi hệ thống | — |

## Compile-check

Code đã build sạch (0 lỗi, 0 warning) trên .NET 10 với **stub** mô phỏng các kiểu của
`VcbPortalApi`. Harness ở
`%LOCALAPPDATA%\Temp\claude\C--Users-Admin\<session>\scratchpad\buildcheck`.
Đây chỉ chứng minh code tự nhất quán — chưa build với solution thật.
