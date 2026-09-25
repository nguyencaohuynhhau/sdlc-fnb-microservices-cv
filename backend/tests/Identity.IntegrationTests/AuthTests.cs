using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Identity.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AuthTests(IdentityFixture fx) : IClassFixture<IdentityFixture>
{
    private readonly HttpClient _http = fx.Factory.CreateClient();

    private sealed record Tokens(string AccessToken, string RefreshToken, string Role, int ExpiresIn);

    private Task<HttpResponseMessage> Login(string user, string password) =>
        _http.PostAsJsonAsync("/api/auth/login", new { username = user, password });

    private Task<HttpResponseMessage> Refresh(string token) =>
        _http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });

    private static async Task<string?> Detail(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;

    private async Task<Tokens> LoginOk(string user)
    {
        var res = await Login(user, IdentityFixture.Password);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Tokens>())!;
    }

    [Fact]
    public async Task Login_Success_ReturnsTokensAndRole()
    {
        var t = await LoginOk("Owner");

        t.Role.Should().Be("Owner");
        t.ExpiresIn.Should().Be(3600);
        t.AccessToken.Split('.').Should().HaveCount(3);
        t.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WrongPassword_401_SameMessageAsUnknownUser()
    {
        var wrongPassword = await Login("cashier", "definitely-wrong");
        var unknownUser = await Login("nobody-here", "definitely-wrong");

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var detail = await Detail(wrongPassword);
        detail.Should().Be("Tên đăng nhập hoặc mật khẩu không đúng.");
        (await Detail(unknownUser)).Should().Be(detail);
    }

    [Fact]
    public async Task Login_TenFailures_Returns423()
    {
        for (var i = 0; i < 10; i++)
        {
            (await Login("lockme", "wrong-password")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // Đúng mật khẩu cũng bị chặn: khoá theo tài khoản, không theo mật khẩu.
        var locked = await Login("lockme", IdentityFixture.Password);

        locked.StatusCode.Should().Be((HttpStatusCode)423);
        (await Detail(locked)).Should().Be("Tài khoản tạm khoá 15 phút vì đăng nhập sai quá nhiều lần.");
    }

    [Fact]
    public async Task Refresh_Rotates_OldTokenRejected()
    {
        var first = await LoginOk("cashier");

        var rotated = await Refresh(first.RefreshToken);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await rotated.Content.ReadFromJsonAsync<Tokens>())!;
        second.RefreshToken.Should().NotBe(first.RefreshToken);

        (await Refresh(first.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReusedToken_RevokesFamily()
    {
        var first = await LoginOk("rotator");
        var second = (await (await Refresh(first.RefreshToken)).Content.ReadFromJsonAsync<Tokens>())!;

        // Kẻ trộm dùng lại token cũ → cả token mới của chủ thật cũng bị thu hồi.
        (await Refresh(first.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var afterTheft = await Refresh(second.RefreshToken);

        afterTheft.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Detail(afterTheft)).Should().Be("Phiên đăng nhập đã hết hạn. Đăng nhập lại nhé.");
    }
}
