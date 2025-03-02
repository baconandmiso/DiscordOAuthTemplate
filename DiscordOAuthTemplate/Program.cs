using AspNet.Security.OAuth.Discord;
using Discord;
using Discord.Rest;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = "Discord";
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie()
.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "";
    options.SaveTokens = true;
    options.Scope.Add("identify");
    options.Scope.Add("guilds");

    options.CallbackPath = new PathString("/signin-discord");

    options.AuthorizationEndpoint = DiscordAuthenticationDefaults.AuthorizationEndpoint;
    options.TokenEndpoint = DiscordAuthenticationDefaults.TokenEndpoint;
    options.UserInformationEndpoint = DiscordAuthenticationDefaults.UserInformationEndpoint;

    options.ClaimActions.Clear(); // Remove default claims

    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        // 認証チケットが作成されるときに呼び出されます。
        OnCreatingTicket = async context =>
        {
            try
            {
                context.Identity?.AddClaim(new Claim("urn:discord:access_token", context.AccessToken ?? ""));

                using var restClient = new DiscordRestClient();
                await restClient.LoginAsync(TokenType.Bearer, context.AccessToken);

                var user = await restClient.GetCurrentUserAsync();
                context.Identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, $"{user.Id}"));
                context.Identity?.AddClaim(new Claim(ClaimTypes.Name, $"{user.Username}"));
                context.Identity?.AddClaim(new Claim("urn:discord:globalname", $"{user.GlobalName}"));
                context.Identity?.AddClaim(new Claim("urn:discord:avatar", $"https://cdn.discordapp.com/avatars/{user.Id}/{user.AvatarId}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        },

        // 認証チケットが作成された後に呼び出されます。
        OnTicketReceived = context =>
        {
            return Task.CompletedTask;
        },
    };
});

// クッキーの設定
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login"; // ログインページのパス
    options.Cookie.SameSite = SameSiteMode.Lax; // クロスサイトに対してはGETのみ送信許可 (同一サイトはPOSTも送信可能)

    options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // クッキーの有効時間
    options.SlidingExpiration = true; // クッキーの有効時間を延長 (非アクティブの場合除く)
});

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
