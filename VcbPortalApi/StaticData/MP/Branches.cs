// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật nạp danh mục chi nhánh lúc khởi động rồi tra trong
// bộ nhớ. Ở đây để test tự nạp được, danh mục là một Dictionary gán từ ngoài.
// ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.StaticData.MP
{
    public static class Branches
    {
        public static Dictionary<decimal, string> Names { get; set; } = [];

        public static string? GetBranchName(decimal? branchId) =>
            branchId != null && Names.TryGetValue(branchId.Value, out var name) ? name : null;
    }
}
