using Microsoft.Extensions.Configuration;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — hai constructor và BuildConStr CHÉP NGUYÊN VĂN từ ảnh code thật.
//
// THÊM MỘT constructor nhận IConfiguration, để đọc thẳng từ appsettings.json mà
// vẫn dùng lại BuildConStr:
//     var db = new OracleDbInfo(AppSettings.Configuration.GetSection("Databases:FrontDb"));
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models
{
    public class OracleDbInfo
    {
        public string ConStr { get; set; } = null!;
        public string Schema { get; set; } = null!;

        public OracleDbInfo(string dataSource, string serviceName, string user, string password, string defaultSchema)
        {
            Schema = defaultSchema;
            BuildConStr(dataSource, serviceName, user, password);
        }

        public OracleDbInfo(string dataSource, string serviceName, string user, string password)
        {
            Schema = serviceName;
            BuildConStr(dataSource, serviceName, user, password);
        }

        /// <summary>
        /// THÊM SO VỚI BẢN THẬT. Đọc 5 khoá trong một section của appsettings.json.
        /// Bỏ trống Schema thì lấy ServiceName — y hệt overload 4 tham số ở trên.
        /// </summary>
        public OracleDbInfo(IConfiguration section)
        {
            var serviceName = section["ServiceName"]!;
            var schema = section["Schema"];

            Schema = string.IsNullOrWhiteSpace(schema) ? serviceName : schema;

            BuildConStr(section["DataSource"]!, serviceName, section["User"]!, section["Password"]!);
        }

        private void BuildConStr(string dataSource, string serviceName, string user, string password)
        {
            ConStr = $"Data Source={dataSource}:1521/{serviceName};User ID={user};Password={password};Connection Timeout=0;Pooling=true;";
        }
    }
}
