namespace VcbPortalApi.Models.SSO
{
    // ─────────────────────────────────────────────────────────────────────────────
    // CẦN SỬA FILE CÓ SẴN: VcbPortalApi/Models/SSO/ValidateTokenRequest.cs
    //
    // ValidateTokenResponsePayload hiện chỉ có:
    //     loginURL, loginTokenSSO, userId, userFullName, userRole, userOf, userCIF
    //
    // Thiếu othersInfo — mà toàn bộ việc đối chiếu username/bid/mid/tid dựa vào nó.
    // Response thật trả về:
    //     "othersInfo": { "mid": "M001", "bid": "B001", "userDes": "user_digimerchant_01",
    //                     "tid": "T001" }
    //
    // Thêm 2 property sau vào class ValidateTokenResponsePayload có sẵn (thêm mới,
    // không đụng field đang có, nên không ảnh hưởng luồng SSO hiện tại):
    //
    //     public Dictionary<string, object>? othersInfo { get; set; }
    //     public string? custClass { get; set; }
    //
    // ─────────────────────────────────────────────────────────────────────────────
    //
    // GetLinkResponsePayload đã bỏ: othersInfo trả đủ username/bid/mid/tid nên không
    // cần gọi thêm API GetLink nữa. Chỉ còn đúng 1 lời gọi sang SSO.
}
