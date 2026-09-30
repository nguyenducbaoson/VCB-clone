namespace VcbPortalApi.Services.AcqHub
{
    /// <summary>
    /// Cấu hình gọi ACQHUB cho luồng cài đặt SHLX.
    ///
    /// Chia làm hai chỗ, theo đúng thứ gì đổi và thứ gì không:
    ///   appsettings.json        → ShlxConfigPath (đường dẫn, giống nhau mọi môi trường)
    ///   appsettings.{Env}.json  → BaseUrl, ClientId, SecretKey (khác nhau từng nơi)
    ///
    /// Cấu hình .NET ghép hai nhóm "AcqHub" này lại theo từng khoá, nên khai ở hai
    /// file không mất gì — khác với mảng, vốn ghép theo chỉ số.
    ///
    /// TRONG SOLUTION THẬT: ClientId và SecretKey chính là AppSettings.AcqClientId
    /// và AppSettings.AcqSecretKey mà AcqRequest đang dùng — trỏ về cùng giá trị,
    /// đừng tạo cặp khoá thứ hai.
    /// </summary>
    public sealed class AcqHubShlxOptions
    {
        public const string SectionName = "AcqHub";

        /// <summary>Chỉ host, không có đường dẫn. Ví dụ http://__ACQHUB_HOST__:8829</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Đường dẫn endpoint. Đoạn CUỐI của nó cũng chính là apiname dùng khi tính
        /// checkSum ("shlxconfig"), nên đừng thêm dấu / ở cuối — apiname sẽ thành
        /// chuỗi rỗng và ACQHUB từ chối mọi request.
        /// </summary>
        public string ShlxConfigPath { get; set; } = "/api/acqhub/configpartner/v1/shlxconfig";

        /// <summary>= AppSettings.AcqClientId. Bản tin mẫu dùng "portal_api".</summary>
        public string ClientId { get; set; } = "portal_api";

        /// <summary>
        /// = AppSettings.AcqSecretKey.
        /// ĐỪNG commit giá trị thật — ghi đè bằng biến môi trường AcqHub__SecretKey.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>URL đầy đủ ghép từ BaseUrl + ShlxConfigPath.</summary>
        public string ShlxConfigUrl =>
            BaseUrl.TrimEnd('/') + "/" + ShlxConfigPath.TrimStart('/');
    }
}
