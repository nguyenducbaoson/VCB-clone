using AutoFixture;

namespace VcbPortalApi.UnitTests.Fixtures
{
    /// <summary>
    /// Base class cho test cần sinh dữ liệu ngẫu nhiên.
    ///
    /// <see cref="Fixture"/> tự tạo object với giá trị ngẫu nhiên hợp lệ, dùng khi
    /// giá trị cụ thể KHÔNG quan trọng với test:
    ///
    ///     var request = Fixture.Create&lt;MerchantSsoLoginRequest&gt;();
    ///
    /// Giá trị nào test thật sự quan tâm thì đặt tay, đừng để AutoFixture sinh —
    /// người đọc phải thấy được đâu là dữ liệu có ý nghĩa:
    ///
    ///     var request = Fixture.Build&lt;MerchantSsoLoginRequest&gt;()
    ///                          .With(x =&gt; x.AccessTokenSSO, "token-can-test")
    ///                          .Create();
    ///
    /// Test dùng dữ liệu cố định hoàn toàn thì không cần kế thừa class này.
    /// </summary>
    public abstract class UnitTestBase
    {
        protected readonly Fixture Fixture;

        protected UnitTestBase()
        {
            Fixture = new Fixture();

            // Model có tham chiếu lồng nhau; bỏ ThrowingRecursionBehavior để
            // AutoFixture cắt vòng thay vì ném exception.
            Fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                   .ForEach(b => Fixture.Behaviors.Remove(b));
            Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        }
    }
}
