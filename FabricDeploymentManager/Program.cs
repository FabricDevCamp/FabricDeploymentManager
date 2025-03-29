using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Rewrite;
using FabricDeploymentManager.Services;
using FabricDeploymentManager.Services.AsyncProcessing;

namespace FabricDeploymentManager {

  public class Program {
  
    public static void Main(string[] args) {

      var builder = WebApplication.CreateBuilder(args);

      builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                      .AddMicrosoftIdentityWebApp(builder.Configuration, "AzureAd");

      builder.Services.AddScoped<EntraIdTokenManager>();
      builder.Services.AddScoped<SignalRLogger>();
      builder.Services.AddScoped<PowerBiRestApi>();
      builder.Services.AddScoped<ItemDefinitionFactory>();
      builder.Services.AddScoped<AdoProjectManager>();
      builder.Services.AddScoped<FabricRestApi>();
      builder.Services.AddScoped<DeploymentManager>();
      builder.Services.AddScoped<BackgroundTaskDispatcher>();

      builder.Services.AddHostedService<BackgroundProvisioningService>();
      builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

      builder.Services.AddSignalR(config => config.EnableDetailedErrors = true);

      builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

      builder.Services.AddRazorPages();

      var app = builder.Build();

      // Configure the HTTP request pipeline.
      if (!app.Environment.IsDevelopment()) {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
      }

      app.UseHttpsRedirection();
      app.UseStaticFiles();

      app.UseRouting();

      app.UseAuthentication();
      app.UseAuthorization();

      app.MapHub<ClientNotificaionHub>("/notificationHub");

      app.MapControllerRoute(
          name: "default",
          pattern: "{controller=Home}/{action=Index}/{id?}");

      app.MapRazorPages();

      app.UseRewriter(
          new RewriteOptions().Add(context => {
            if (context.HttpContext.Request.Path == "/MicrosoftIdentity/Account/SignedOut") {
              context.HttpContext.Response.Redirect("/Home/Index");
            }
          })
      );

      app.Run();

    }
  }
}
