using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VcbPortalApi.Controllers.Frontend;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Services.AcqHub;
using VcbPortalApi.Services.MP;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.UnitTests.Fixtures;
using Xunit;

namespace VcbPortalApi.UnitTests.Controllers.Frontend
{
    /// <summary>
    /// Đi hết đường: controller → client → AcqRequest dựng phong bì → ACQHUB giả
    /// → tạo user portal.
    ///
    /// Dùng AcqHubShlxClient THẬT chứ không mock, chỉ thay tầng HTTP. Mock client đi
    /// thì bài test chỉ chứng minh controller gọi đúng hàm — không chứng minh được
    /// phong bì gửi sang ACQHUB có đúng không, mà đó mới là chỗ hay sai.
    ///
    /// IUserService thì giả lập: hợp đồng của nó chỉ có hai vế — GetUser trả null
    /// khi chưa có, CreateNewUser trả số dòng ghi được — và cả hai kiểm được bằng
    /// một từ điển trong bộ nhớ.
    /// </summary>
    public class AcqHubShlxControllerTests
    {
        private static readonly string Secret = VcbPortalApi.AppSettings.AcqSecretKey;

        private static ShlxUploadItem MotDong(string terminalId = "V827308102") => new()
        {
            merchantId = "MERCHANT_TEST",
            terminalId = terminalId,
            terminalName = "TEN_TERMINAL",
            ddAccountNumber = "0000000000",
            branchCode = "01400",
            province = "TINH_TEST",
            userName = "SHLX_" + terminalId,
            roleId = Roles.RoleShlx,
            fullName = "Trung tam sat hach",
            email = "shlx@example.test",
            mobile = "0912345678",
        };

        private static ShlxUploadRequest MotTerminal(string terminalId = "V827308102") =>
            new() { items = [MotDong(terminalId)] };

        /// <summary>
        /// ACQHUB giả: kiểm checkSum theo đúng công thức của AcqRequest, rồi trả kết
        /// quả cho từng terminal đọc được trong data. Terminal có đuôi "_HONG" thì báo
        /// thất bại — để kiểm được trường hợp một lô thành công MỘT PHẦN.
        /// </summary>
        private static HttpMessageHandler AcqHubGiaLap(Action<string>? ghiLaiData = null)
            => new StubHandler(req =>
        {
            var root = JsonDocument.Parse(req.Content!.ReadAsStringAsync().Result).RootElement;

            var data = root.GetProperty("data").GetString()!;
            var clientId = root.GetProperty("clientId").GetString()!;
            var requestTime = root.GetProperty("requestTime").GetInt64();
            var checkSum = root.GetProperty("checkSum").GetString()!;

            var raw = string.Join("|", data, clientId, requestTime, "shlxconfig", Secret);
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

            if (expected != checkSum)
                return Json("""{"code":"01","message":"Failed","results":[]}""");

            var giaiMa = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            ghiLaiData?.Invoke(giaiMa);

            var terminals = JsonDocument.Parse(giaiMa)
                .RootElement.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("terminalId").GetString()!)
                .ToArray();

            var results = string.Join(",", terminals.Select(t =>
                $$"""{"terminal_id":"{{t}}","success":{{(t.EndsWith("_HONG") ? "false" : "true")}}}"""));

            return Json($$"""{"code":"00","message":"Succeed","results":[{{results}}]}""");
        });

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body) };

        private static AcqHubShlxController CreateController(
            HttpMessageHandler? handler = null, IUserService? userService = null)
        {
            var client = new AcqHubShlxClient(
                new HttpClient(handler ?? AcqHubGiaLap()),
                Options.Create(new AcqHubShlxOptions
                {
                    BaseUrl = "http://acqhub.test:8829",
                    ShlxConfigPath = "/api/acqhub/configpartner/v1/shlxconfig",
                }),
                NullLogger<AcqHubShlxClient>.Instance);

            return new AcqHubShlxController(
                client,
                userService ?? new UserServiceGiaLap(),
                NullLogger<AcqHubShlxController>.Instance)
            {
                ControllerContext = TestHttpContext.Build(),
            };
        }

        private static ShlxConfigResponse Body(IActionResult res) =>
            res.Should().BeOfType<OkObjectResult>()
                .Which.Value.Should().BeOfType<ShlxConfigResponse>().Subject;

        // ── Phần ACQHUB ────────────────────────────────────────────────────────

        [Fact]
        public async Task Danh_sach_rong_thi_BadRequest_khong_goi_ACQHUB()
        {
            var res = await CreateController()
                .ShlxConfig(new ShlxUploadRequest(), CancellationToken.None);

            res.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Body_null_thi_BadRequest()
        {
            var res = await CreateController().ShlxConfig(null!, CancellationToken.None);

            res.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Mot_terminal_thanh_cong_thi_tra_nguyen_response_ACQHUB()
        {
            var body = Body(await CreateController()
                .ShlxConfig(MotTerminal(), CancellationToken.None));

            body.code.Should().Be("00");
            body.results.Should().ContainSingle();
            body.results[0].terminal_id.Should().Be("V827308102");
            body.results[0].success.Should().BeTrue();
        }

        [Fact]
        public async Task Lo_thanh_cong_MOT_PHAN_thi_giu_nguyen_ket_qua_tung_terminal()
        {
            // Đây là lý do controller trả nguyên response chứ không rút gọn thành
            // một cờ thành/bại: frontend cần biết terminal nào hỏng để giữ lại dòng đó.
            var request = new ShlxUploadRequest
            {
                items = [MotDong("TERM_OK_1"), MotDong("TERM_HONG"), MotDong("TERM_OK_2")],
            };

            var body = Body(await CreateController().ShlxConfig(request, CancellationToken.None));

            body.code.Should().Be("00", "phong bi hop le — code ngoai van la 00");
            body.results.Should().HaveCount(3);
            body.results.Count(x => x.success).Should().Be(2);
            body.results.Single(x => !x.success).terminal_id.Should().Be("TERM_HONG");
        }

        [Fact]
        public async Task CheckSum_sai_thi_ACQHUB_tu_choi_ca_lo()
        {
            var handler = new StubHandler(_ =>
                Json("""{"code":"01","message":"Failed","results":[]}"""));

            var body = Body(await CreateController(handler)
                .ShlxConfig(MotTerminal(), CancellationToken.None));

            body.code.Should().Be("01");
            body.results.Should().BeEmpty("bi chan truoc khi xu ly terminal nao");
            body.users.Should().BeEmpty("khong terminal nao duoc cai thi khong tao user nao");
        }

        // ── Năm cột user KHÔNG được rời khỏi hệ thống ─────────────────────────

        [Fact]
        public async Task Cot_user_KHONG_duoc_gui_sang_ACQHUB()
        {
            // Bài quan trọng nhất của nhóm này. USER_NAME/EMAIL/MOBILE là dữ liệu nội
            // bộ và dữ liệu cá nhân; ACQHUB không khai khoá nào trong số đó. Gộp
            // ShlxUploadItem với ShlxConfigItem làm một là chúng lọt hết vào trường
            // data — và không có gì báo, vì ACQHUB vẫn trả code 00.
            string? daGui = null;

            await CreateController(AcqHubGiaLap(x => daGui = x))
                .ShlxConfig(MotTerminal(), CancellationToken.None);

            daGui.Should().NotBeNull();

            foreach (var khoa in new[] { "userName", "roleId", "fullName", "email", "mobile" })
                daGui.Should().NotContain(khoa);

            daGui.Should().NotContain("0912345678").And.NotContain("shlx@example.test");
            daGui.Should().Contain("V827308102", "6 cot ACQHUB van phai co du");
        }

        // ── Tạo user portal ───────────────────────────────────────────────────

        [Fact]
        public async Task Terminal_cai_xong_ma_chua_co_user_thi_tao_moi()
        {
            var users = new UserServiceGiaLap();

            var body = Body(await CreateController(userService: users)
                .ShlxConfig(MotTerminal(), CancellationToken.None));

            body.users.Should().ContainSingle();
            body.users[0].created.Should().BeTrue();
            body.users[0].user_name.Should().Be("SHLX_V827308102");
            body.users[0].terminal_id.Should().Be("V827308102");

            var tao = users.DaCo["SHLX_V827308102"];
            tao.RoleId.Should().Be(Roles.RoleShlx);
            tao.TerminalId.Should().Be("V827308102");
            tao.FullName.Should().Be("Trung tam sat hach");
            tao.Email.Should().Be("shlx@example.test");
            tao.Mobile.Should().Be("0912345678");
            tao.Status.Should().Be("0");

            // Mật khẩu do CreateUsers sinh, controller truyền Password = null. Hai
            // chỗ cùng quyết định một mật khẩu là sớm muộn cũng lệch nhau.
            users.MatKhauCuoi.Should().Be($"Shlx@{DateTime.Now:yyMMdd}");
        }

        [Fact]
        public async Task User_da_ton_tai_thi_khong_tao_lai()
        {
            var users = new UserServiceGiaLap();
            users.DaCo["SHLX_V827308102"] = new MpUserFull { UserName = "SHLX_V827308102" };

            var body = Body(await CreateController(userService: users)
                .ShlxConfig(MotTerminal(), CancellationToken.None));

            users.SoLanGoiCreate.Should().Be(0);
            body.users.Should().ContainSingle();
            body.users[0].created.Should().BeFalse();
            body.users[0].message.Should().Contain("da ton tai");
        }

        [Fact]
        public async Task ACQHUB_tu_choi_terminal_thi_KHONG_tao_user_cho_no()
        {
            // Thứ tự có lý do: tạo user trước rồi ACQHUB hỏng là để lại một tài khoản
            // đăng nhập được nhưng không có terminal nào phía sau — lỗi im lặng.
            var users = new UserServiceGiaLap();

            var request = new ShlxUploadRequest
            {
                items = [MotDong("TERM_OK_1"), MotDong("TERM_HONG")],
            };

            var body = Body(await CreateController(userService: users)
                .ShlxConfig(request, CancellationToken.None));

            users.SoLanGoiCreate.Should().Be(1);
            users.DaCo.Should().ContainKey("SHLX_TERM_OK_1");
            users.DaCo.Should().NotContainKey("SHLX_TERM_HONG");

            body.users.Should().ContainSingle("chi terminal da cai moi co dong ket qua");
            body.users[0].terminal_id.Should().Be("TERM_OK_1");
        }

        [Theory]
        [InlineData(19)]   // quản trị
        [InlineData(13)]   // kiểm soát viên
        [InlineData(2)]    // MID
        public async Task ROLE_ID_ngoai_nhom_SHLX_bi_tu_choi(int roleId)
        {
            // ROLE_ID là một ô Excel do người dùng gõ. Không chặn ở đây thì quyền
            // "cài đặt SHLX" trở thành quyền tự tạo tài khoản quản trị.
            var users = new UserServiceGiaLap();

            var request = MotTerminal();
            request.items[0].roleId = roleId;

            var body = Body(await CreateController(userService: users)
                .ShlxConfig(request, CancellationToken.None));

            users.SoLanGoiCreate.Should().Be(0);
            body.users[0].created.Should().BeFalse();
            body.users[0].message.Should().Contain("khong phai role SHLX");
        }

        [Fact]
        public async Task ROLE_ID_de_trong_thi_mac_dinh_la_role_SHLX()
        {
            var users = new UserServiceGiaLap();

            var request = MotTerminal();
            request.items[0].roleId = null;

            await CreateController(userService: users).ShlxConfig(request, CancellationToken.None);

            users.DaCo["SHLX_V827308102"].RoleId.Should().Be(Roles.RoleShlx);
        }

        [Fact]
        public async Task FULLNAME_de_trong_thi_lay_chinh_USER_NAME()
        {
            var users = new UserServiceGiaLap();

            var request = MotTerminal();
            request.items[0].fullName = null;

            await CreateController(userService: users).ShlxConfig(request, CancellationToken.None);

            users.DaCo["SHLX_V827308102"].FullName.Should().Be("SHLX_V827308102");
        }

        [Fact]
        public async Task Thieu_USER_NAME_thi_bao_ra_chu_khong_im_lang()
        {
            // Terminal đã cài xong rồi. Bỏ qua im lặng là người dùng tưởng đã có user.
            var users = new UserServiceGiaLap();

            var request = MotTerminal();
            request.items[0].userName = "   ";

            var body = Body(await CreateController(userService: users)
                .ShlxConfig(request, CancellationToken.None));

            users.SoLanGoiCreate.Should().Be(0);
            body.users.Should().ContainSingle();
            body.users[0].message.Should().Contain("Thieu USER_NAME");
        }

        [Fact]
        public async Task Tao_user_hong_thi_van_lam_tiep_nhung_dong_sau()
        {
            // ACQHUB đã ghi xong cả lô. Ném ra giữa chừng là bỏ dở mà không ai biết
            // đã tới đâu.
            var users = new UserServiceGiaLap
            {
                KhiTao = u => u.UserName.EndsWith("_2")
                    ? throw new InvalidOperationException("ORA-00001")
                    : 1,
            };

            var request = new ShlxUploadRequest
            {
                items = [MotDong("TERM_1"), MotDong("TERM_2"), MotDong("TERM_3")],
            };

            var body = Body(await CreateController(userService: users)
                .ShlxConfig(request, CancellationToken.None));

            body.users.Should().HaveCount(3);
            body.users.Count(x => x.created).Should().Be(2);
            body.users.Single(x => !x.created).terminal_id.Should().Be("TERM_2");
        }

        // ── Móc JSON ra khỏi thứ SendAcqRequest trả về ────────────────────────

        [Fact]
        public void Doc_duoc_than_JSON_tu_ContentResult()
        {
            // BẢN THẬT TRẢ KIỂU NÀY. Debugger trên máy thật: response là ContentResult
            // với Content = chuỗi JSON, ContentType = "application/json".
            // Trước đây tôi chỉ bắt ObjectResult nên `as ObjectResult` ra null và toàn
            // bộ phần tạo user bị bỏ qua lặng lẽ — không lỗi, không log, response chỉ
            // thiếu khoá "users".
            var res = new ContentResult
            {
                Content = """{"code":"00","results":[{"terminal_id":"T1","success":true}]}""",
                ContentType = "application/json",
                StatusCode = 200,
            };

            var body = AcqHubShlxController.DocThanJson(res);

            body.Should().NotBeNull();
            body!["code"]!.Value<string>().Should().Be("00");
            ((JArray)body["results"]!).Should().ContainSingle();
        }

        [Fact]
        public void Doc_duoc_than_JSON_tu_ObjectResult_ca_chuoi_lan_object()
        {
            AcqHubShlxController
                .DocThanJson(new OkObjectResult("""{"code":"00"}"""))
                !["code"]!.Value<string>().Should().Be("00");

            AcqHubShlxController
                .DocThanJson(new OkObjectResult(new { code = "00" }))
                !["code"]!.Value<string>().Should().Be("00");
        }

        [Fact]
        public void Khong_doc_duoc_than_JSON_thi_tra_null_chu_khong_nem()
        {
            AcqHubShlxController.DocThanJson(new BadRequestResult()).Should().BeNull();
            AcqHubShlxController.DocThanJson(new ContentResult { Content = "khong phai json" })
                .Should().BeNull();
            AcqHubShlxController.DocThanJson(new OkObjectResult(null)).Should().BeNull();
        }

        [Fact]
        public void Tra_lai_ContentResult_thi_giu_nguyen_ContentType_va_StatusCode()
        {
            // Dựng Content() mới là mất hai thứ này. Frontend nhận content-type khác
            // đi mà không có gì báo.
            var goc = new ContentResult
            {
                Content = """{"code":"00"}""",
                ContentType = "application/json",
                StatusCode = 207,
            };

            var ra = AcqHubShlxController.TraLaiDungKieu(
                goc, new JObject { ["code"] = "00", ["users"] = new JArray() });

            var c = ra.Should().BeOfType<ContentResult>().Subject;

            c.ContentType.Should().Be("application/json");
            c.StatusCode.Should().Be(207);
            c.Content.Should().Contain("users");
        }

        [Fact]
        public void Tra_lai_ObjectResult_chuoi_thi_van_la_chuoi()
        {
            // SendAcqRequest bên nào trả Ok(string) thì phải trả lại string, không
            // phải object — đổi kiểu là frontend parse khác đi.
            var ra = AcqHubShlxController.TraLaiDungKieu(
                new OkObjectResult("""{"code":"00"}"""), new JObject { ["code"] = "00" });

            ((OkObjectResult)ra).Value.Should().BeOfType<string>();
        }

        // ── Đọc success ra khỏi kiểu vô danh của CreateUsers ──────────────────

        [Theory]
        [InlineData("success")]
        [InlineData("Success")]
        [InlineData("SUCCESS")]
        public void Doc_duoc_success_du_CreateUsers_doi_chu_hoa(string ten)
        {
            // CreateUsers trả Ok(new { total, success }) — kiểu VÔ DANH, không có
            // hợp đồng nào giữ tên trường đứng yên. Tra khoá phân biệt hoa thường
            // thì đổi một chữ cái là hàm này trả 0 cho MỌI dòng: màn hình báo "chưa
            // tạo được user" hàng loạt trong khi user đã nằm trong DB, và không ai
            // nghi ngờ gì vì ACQHUB vẫn xanh.
            var body = new JObject { [ten] = 7, ["total"] = 9 };

            AcqHubShlxController.SoUserDaTao(new OkObjectResult(body)).Should().Be(7);
        }

        [Fact]
        public void Khong_doc_duoc_success_thi_tra_0_chu_khong_nem()
        {
            AcqHubShlxController.SoUserDaTao(new OkObjectResult(new { total = 9 }))
                .Should().Be(0);

            AcqHubShlxController.SoUserDaTao(new BadRequestResult()).Should().Be(0);
        }

        // ── Đồ giả lập ────────────────────────────────────────────────────────

        private sealed class UserServiceGiaLap : IUserService
        {
            public readonly Dictionary<string, MpUserFull> DaCo =
                new(StringComparer.OrdinalIgnoreCase);

            public int SoLanGoiCreate;
            public string? MatKhauCuoi;

            /// <summary>Đặt vào để giả lập DB ném lỗi ở một dòng nhất định.</summary>
            public Func<MpUserFull, int>? KhiTao;

            public Task<MpUserCommon?> GetUser(
                string userName, CancellationToken cancellationToken = default) =>
                Task.FromResult(DaCo.TryGetValue(userName, out var u) ? (MpUserCommon?)u : null);

            public Task<int> CreateNewUser(
                MpUserFull user,
                string? password = null,
                CancellationToken cancellationToken = default)
            {
                SoLanGoiCreate++;
                MatKhauCuoi = password;

                var n = KhiTao?.Invoke(user) ?? 1;

                if (n > 0) DaCo[user.UserName] = user;

                return Task.FromResult(n);
            }
        }

        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(respond(request));
        }
    }
}
