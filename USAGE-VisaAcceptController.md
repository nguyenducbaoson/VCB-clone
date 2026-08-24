# Gắn `IMpAppUserStatusService` vào `VisaAcceptController.Register`

Để ở file `.md` chứ không phải `.cs` vì đoạn này tham chiếu các kiểu chỉ có trong
solution thật (`VisaAcceptRegisterRequest`, `HttpError`, `Roles`, `cardIssuanceService`,
`ForwardRegisterAsync`…), đưa vào harness compile-check sẽ đỏ.

## 1. Inject service

```csharp
public sealed class VisaAcceptController : ControllerBase   // hoặc base class sẵn có
{
    private readonly IMpAppUserStatusService _mpAppUserStatusService;
    private readonly ILogger<VisaAcceptController> _logger;

    public VisaAcceptController(
        // ... các dependency đang có ...
        IMpAppUserStatusService mpAppUserStatusService,
        ILogger<VisaAcceptController> logger)
    {
        // ... gán các dependency đang có ...
        _mpAppUserStatusService = mpAppUserStatusService;
        _logger = logger;
    }
}
```

Đăng ký trong `Program.cs` / `Startup.cs`:

```csharp
builder.Services.AddScoped<IMpAppUserStatusService, MpAppUserStatusService>();
```

## 2. Action `Register` sau khi sửa

```csharp
/// <summary>MID trở lên được đăng ký thẻ Visa Accept.</summary>
[HttpPost("register")]
public async Task<IActionResult> Register(
    [FromBody] VisaAcceptRegisterRequest request,
    CancellationToken cancellationToken)
{
    var actor = await ResolveActorIdentityAsync(cancellationToken);
    if (actor == null || !Roles.IsMidOrAbove(actor.RoleId))
        return HttpError.Unauthorized();

    if (request.cards == null || request.cards.Count == 0)
        return HttpError.BaseError();

    var cards = new JArray();
    foreach (var card in request.cards)
    {
        if (string.IsNullOrWhiteSpace(card.userName) || string.IsNullOrWhiteSpace(card.vcbtoken))
            return HttpError.BaseError();

        cards.Add(new JObject
        {
            ["cif"] = string.Empty,
            ["network"] = string.IsNullOrWhiteSpace(card.network) ? "VISAACCEPT" : card.network.Trim(),
            ["userName"] = card.userName.Trim(),
            ["bid"] = card.bid ?? string.Empty,
            ["mid"] = card.mid ?? string.Empty,
            ["tid"] = card.tid ?? string.Empty,
            ["bidName"] = card.bidName ?? string.Empty,
            ["midName"] = card.midName ?? string.Empty,
            ["vcbtoken"] = card.vcbtoken.Trim(),
            ["cardMask"] = card.cardMask ?? string.Empty,
            ["cardAcctNo"] = card.cardAcctNo ?? string.Empty,
            ["expDate"] = card.expDate ?? string.Empty,
            ["bin"] = card.bin ?? string.Empty,
            ["productId"] = card.productId ?? string.Empty,
            ["sellerId"] = card.sellerId ?? string.Empty,
            ["corpCif"] = card.corpCif ?? string.Empty,
            ["branch"] = card.branch ?? string.Empty,
        });
    }

    var payload = new JObject
    {
        ["cards"] = cards,
        ["auditData"] = BuildAuditData(CurrentUserName),
    };

    return await ForwardRegisterAsync(
        cardIssuanceService.RegisterAsync(payload, cancellationToken),
        onSuccess: async () =>
        {
            // STATUS '0' = mới đăng ký (web register thành công)
            await UpsertPartnerCardsAfterRegisterAsync(
                request.cards,
                MpAppPartnerCardReg.StatusJustRegistered,
                cancellationToken);

            // Đồng bộ PHONEPOS_STATUS / VISAACCEPT_STATUS. Phải chạy SAU Upsert ở trên,
            // vì hàm này tính lại trạng thái từ chính các dòng vừa ghi.
            await RefreshStatusForCardsAsync(request.cards, cancellationToken);
        });
}

/// <summary>
/// Helper của CONTROLLER, không phải của service.
///
/// Nhiệm vụ: bóc username từ danh sách thẻ rồi gọi service — service chỉ nhận MỘT username,
/// nên phải lặp. Distinct vì một user có thể đăng ký nhiều thẻ trong cùng request; refresh
/// nhiều lần cho cùng user chỉ tốn query chứ kết quả không đổi.
/// </summary>
private async Task RefreshStatusForCardsAsync(
    IEnumerable<VisaAcceptCardRequest> cards, CancellationToken cancellationToken)
{
    var usernames = cards
        .Select(c => c.userName?.Trim())
        .Where(u => !string.IsNullOrWhiteSpace(u))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    foreach (var username in usernames)
    {
        try
        {
            await _mpAppUserStatusService.RefreshStatusAsync(username!, cancellationToken);
        }
        catch (Exception ex)
        {
            // Đăng ký đã thành công và dòng partner đã ghi. Ném lỗi ở đây sẽ khiến client
            // tưởng thất bại rồi đăng ký lại. Trạng thái là dữ liệu suy ra được từ
            // MP_APP_PARTNER_CARD_REG, lần refresh sau sẽ tự đúng.
            _logger.LogError(ex, "Không đồng bộ được trạng thái cho {Username}", username);
        }
    }
}
```

`VisaAcceptCardRequest` là tên giả định cho kiểu phần tử của `request.cards` — đổi cho khớp
kiểu thật trong `VisaAcceptRegisterRequest`.

## 3. Ba chỗ khác với đoạn đang viết dở

**Vị trí gọi.** Bản hiện tại đặt ngay trước `ForwardRegisterAsync`, tức chạy **trước** khi
`UpsertPartnerCardsAfterRegisterAsync` ghi dữ liệu. Trạng thái tính ra sẽ luôn trễ một nhịp:
lần đăng ký đầu tiên của một user cho ra `null` thay vì `0`.

**Username truyền vào.** Bản hiện tại truyền `CurrentUserName` — đó là user MID trở lên đang
thao tác, không phải chủ thẻ. Dòng ghi vào `MP_APP_PARTNER_CARD_REG` dùng `card.userName`,
nên phải refresh theo các username đó.

**`_ = await`.** `RefreshStatusAsync` trả `Task` không có giá trị nên `_ = await ...` không
biên dịch được. Chỉ `await`.

## 4. Áp dụng tương tự cho các luồng khác

Bất cứ chỗ nào ghi/sửa `MP_APP_PARTNER_CARD_REG` đều cần gọi lại — kích hoạt (STATUS 2–6),
hủy (STATUS 7), PhonePOS register. Nguyên tắc: gọi **sau** khi dòng đã ghi xong, và refresh
theo username của dòng vừa đụng vào.
