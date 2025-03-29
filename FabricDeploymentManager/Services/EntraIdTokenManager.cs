using Azure.Core;
using Azure.Identity;
using FabricDeploymentManager.Models;
using FabricDeploymentManager.Services.AsyncProcessing;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace FabricDeploymentManager.Services {

  public class EntraIdTokenManager {

    const string fabricApiPermissionScopeforSPN = "https://api.fabric.microsoft.com/.default";

    private string tenantId;
    private string clientId;
    private string clientSecret;
    private AuthenticationResult fabricAccessTokenResult;

    public EntraIdTokenManager(IConfiguration Configuration) {
      tenantId = Configuration["ServicePrincipal:TenantId"];
      clientId = Configuration["ServicePrincipal:ClientId"];
      clientSecret = Configuration["ServicePrincipal:ClientSecret"];
    }

    public async Task<AuthenticationResult> GetAccessTokenResult(string[] Scopes) {

      // Azure AD Application Id for service principal authentication
         string tenantSpecificAuthority = "https://login.microsoftonline.com/" + tenantId;

      var appConfidential =
          ConfidentialClientApplicationBuilder.Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(tenantSpecificAuthority)
            .Build();

      return await appConfidential.AcquireTokenForClient(Scopes).ExecuteAsync();

    }

    public async Task<AuthenticationResult> GetFabricAccessTokenResult() {
      var timeNow = DateTimeOffset.UtcNow;
      if (fabricAccessTokenResult == null ||
          timeNow  >= fabricAccessTokenResult.ExpiresOn) {
        string[] scopes = { EntraIdTokenManager.fabricApiPermissionScopeforSPN };
        fabricAccessTokenResult = await GetAccessTokenResult(scopes);
      }
      return fabricAccessTokenResult;
    }

    public async Task<string> GetFabricAccessToken() {
      return (await GetFabricAccessTokenResult()).AccessToken;
    }

  }
}

