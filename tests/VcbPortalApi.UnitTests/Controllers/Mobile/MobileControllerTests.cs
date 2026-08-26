using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Controllers.Mobile;
using VcbPortalApi.DbContext;
using VcbPortalApi.Services;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Controllers.Mobile
{
    public class MobileControllerTests
    {
        private readonly FrontendContext _context = TestDb.Create<FrontendContext>();

        private MobileController CreateController(string? userName = TestDataHelper.DefaultUserName) =>
            new(_context,
                TestDb.Create<MerchantContext>(),
                new UserAppConfigService(_context),
                new TwoFaService(_context))
            {
                ControllerContext = TestHttpContext.Build(userName)
            };

        [Fact]
        public async Task Deactive_WhenDeactivationSucceeds_ReturnsOk()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.Should().BeOfType<OkResult>();
        }

        [Fact]
        public async Task Deactive_WhenUserNotFound_ReturnsBaseError()
        {
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.ShouldBeError("BaseError");
        }

        [Fact]
        public async Task Deactive_WhenNoIdentity_ReturnsBaseError()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            var controller = CreateController(userName: null);

            // Act
            var result = await controller.Deactive();

            // Assert
            result.ShouldBeError("BaseError");
        }

        [Fact]
        public async Task Deactive_WhenDatabaseThrows_ReturnsSystemErrorWithoutLeakingDetails()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            var controller = CreateController();
            _context.Dispose();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.ShouldBeError("SystemError");

            var body = result.Should().BeOfType<OkObjectResult>().Subject.Value!.ToString();
            body.Should().NotContain("Disposed", "khong duoc lo chi tiet loi ra client");
        }
    }
}
