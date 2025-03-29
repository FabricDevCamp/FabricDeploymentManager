
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using FabricDeploymentManager.Models;
using FabricDeploymentManager.Services.AsyncProcessing;
using Microsoft.AspNetCore.SignalR;

namespace FabricDeploymentManager.Services {

  public class PowerBiRestApi {

    private EntraIdTokenManager tokenManager { get; }
    private Guid fabricCapacityId { get; }
    private IHubContext<ClientNotificaionHub> notificationHub;

    public PowerBiRestApi(IConfiguration configuration,
                          EntraIdTokenManager TokenManager,
                          IHubContext<ClientNotificaionHub> NotificationHub) {
      fabricCapacityId = new Guid(configuration["Fabric:FabricCapacityId"]);
      tokenManager = TokenManager;
      notificationHub = NotificationHub;
    }

    public async Task SendClientInProgressPing() {
      await notificationHub.Clients.All.SendAsync("ClientInProgressPing");
    }

    public async Task<PowerBIClient> GetPowerBiClient() {
      string accessToken = await tokenManager.GetFabricAccessToken();
      var tokenCredentials = new TokenCredentials(accessToken, "Bearer");
      return new PowerBIClient(new Uri("https://api.powerbi.com/"), tokenCredentials);
    }

    public async Task<IList<Group>> GetPowerBiWorkspaces(PowerBIClient pbiClient) {
      var workspaces = (await pbiClient.Groups.GetGroupsAsync()).Value;
      return workspaces;
    }

    public async Task<Group> CreateWorkspace(string DisplayName) {

      var pbiClient = await GetPowerBiClient();

      // create new app workspace
      GroupCreationRequest request = new GroupCreationRequest(DisplayName);
      Group pbiWorkspace = await pbiClient.Groups.CreateGroupAsync(request);

      Guid workspaceId = pbiWorkspace.Id;

      await pbiClient.Groups.AssignToCapacityAsync(workspaceId, new AssignToCapacityRequest(this.fabricCapacityId));

      return pbiWorkspace;
    }

    public async Task BindDatasetToConnection(string WorkspaceId, string DatasetId, string ConnectionId) {

      PowerBIClient pbiClient = await GetPowerBiClient();

      BindToGatewayRequest bindRequest = new BindToGatewayRequest {
        //GatewayObjectId = new Guid("00000000-0000-0000-0000-000000000000"),
        DatasourceObjectIds = new List<Guid?>()
      };

      bindRequest.DatasourceObjectIds.Add(new Guid(ConnectionId));

      await pbiClient.Datasets.BindToGatewayInGroupAsync(new Guid(WorkspaceId), DatasetId, bindRequest);

    }

    public async Task RefreshDataset(Guid WorkspaceId, string DatasetId) {
      await RefreshDataset(WorkspaceId, new Guid(DatasetId));
    }

    public async Task RefreshDataset(Guid WorkspaceId, Guid DatasetId) {

      PowerBIClient pbiClient = await GetPowerBiClient();

      var refreshRequest = new DatasetRefreshRequest {
        NotifyOption = NotifyOption.NoNotification,
        Type = DatasetRefreshType.Automatic
      };

      var responseStartFresh = await pbiClient.Datasets.RefreshDatasetInGroupAsync(WorkspaceId, DatasetId.ToString(), refreshRequest);

      var responseStatusCheck = await pbiClient.Datasets.GetRefreshExecutionDetailsInGroupAsync(WorkspaceId, DatasetId, new Guid(responseStartFresh.XMsRequestId));

      await SendClientInProgressPing();
      Console.Write(".");

      while (responseStatusCheck.Status == "Unknown") {
        await Task.Delay(5000);
        await SendClientInProgressPing();
        Console.Write(".");
        responseStatusCheck = await pbiClient.Datasets.GetRefreshExecutionDetailsInGroupAsync(WorkspaceId, DatasetId, new Guid(responseStartFresh.XMsRequestId));
      }
      Console.WriteLine();
    }

    public async Task PatchDirectLakeDatasetCredentials(string WorkspaceId, string DatasetId) {
      var pbiClient = await GetPowerBiClient();

      var datasources = (await pbiClient.Datasets.GetDatasourcesInGroupAsync(new Guid(WorkspaceId), DatasetId)).Value;
      var SqlEndpointDatasource = datasources[0];

      while (SqlEndpointDatasource.DatasourceId == null) {
        await Task.Delay(5000);
        datasources = (await pbiClient.Datasets.GetDatasourcesInGroupAsync(new Guid(WorkspaceId), DatasetId)).Value;
        SqlEndpointDatasource = datasources[0];
      }

      Guid? datasourceId = SqlEndpointDatasource.DatasourceId;
      IList<Guid?> datasourceIds = new List<Guid?>() { datasourceId };

      var gatewayId = SqlEndpointDatasource.GatewayId.Value;

      //BindToGatewayRequest bindRequest = new BindToGatewayRequest {
      //  GatewayObjectId = gatewayId , //new Guid("00000000-0000-0000-0000-000000000000"),
      //  DatasourceObjectIds = datasourceIds

      //};

      //await pbiClient.Datasets.BindToGatewayInGroupAsync(new Guid(WorkspaceId), DatasetId, bindRequest);

      // create credential details
      var CredentialDetails = new CredentialDetails();
      CredentialDetails.CredentialType = CredentialType.OAuth2;
      CredentialDetails.UseCallerAADIdentity = true;
      CredentialDetails.EncryptedConnection = EncryptedConnection.Encrypted;
      CredentialDetails.EncryptionAlgorithm = EncryptionAlgorithm.None;
      CredentialDetails.PrivacyLevel = PrivacyLevel.Private;
      CredentialDetails.UseEndUserOAuth2Credentials = false;

      // create UpdateDatasourceRequest 
      UpdateDatasourceRequest req = new UpdateDatasourceRequest(CredentialDetails);

      // Execute Patch command to update Azure SQL datasource credentials
      await pbiClient.Gateways.UpdateDatasourceAsync((Guid)gatewayId, (Guid)datasourceId, req);
    }

    public async Task<Datasources> GetDatasourcesForSemanticModel(Guid WorkspaceId, Guid DatasetId) {
      var pbiClient = await GetPowerBiClient();
      return await pbiClient.Datasets.GetDatasourcesInGroupAsync(WorkspaceId, DatasetId.ToString());
    }

    public async Task<Guid> GetDatasetIdForReport(Guid WorkspaceId, Guid ReportId) {
      var pbiClient = await GetPowerBiClient();
      var report = await pbiClient.Reports.GetReportInGroupAsync(WorkspaceId, ReportId);
      return new Guid(report.DatasetId);
    }

    public async Task<Dataset> GetDatasetForReport(Guid WorkspaceId, Guid ReportId) {
      var pbiClient = await GetPowerBiClient();
      var report = await pbiClient.Reports.GetReportInGroupAsync(WorkspaceId, ReportId);
      return await pbiClient.Datasets.GetDatasetInGroupAsync(WorkspaceId, report.DatasetId);
    }

    public async Task<string> GetWebDatasourceUrl(Guid WorkspaceId, Guid DatasetId) {
      var pbiClient = await GetPowerBiClient();
      var datasources = await pbiClient.Datasets.GetDatasourcesInGroupAsync(WorkspaceId, DatasetId.ToString());
      var datasource = datasources.Value.First();
      if (datasource.DatasourceType.Equals("Web")) {
        return datasource.ConnectionDetails.Url;
      }
      else {
        throw new ApplicationException("Error - expecting Web connection");
      }
    }

    public async Task BindReportToSemanticModel(Guid WorkspaceId, Guid SemanticModelId, Guid ReportId) {
      var pbiClient = await GetPowerBiClient();
      RebindReportRequest bindRequest = new RebindReportRequest(SemanticModelId.ToString());
      await pbiClient.Reports.RebindReportInGroupAsync(WorkspaceId, ReportId, bindRequest);
    }

    public async Task BindSemanticModelToConnection(Guid WorkspaceId, Guid SemanticModelId, Guid ConnectionId) {
      var pbiClient = await GetPowerBiClient();

      BindToGatewayRequest bindRequest = new BindToGatewayRequest {
        DatasourceObjectIds = new List<Guid?>()
      };

      bindRequest.DatasourceObjectIds.Add(ConnectionId);

      await pbiClient.Datasets.BindToGatewayInGroupAsync(WorkspaceId, SemanticModelId.ToString(), bindRequest);

    }

    public async Task<EmbeddedViewModel> GetEmbeddedViewModel(Guid WorkspaceId) {

      var pbiClient = await GetPowerBiClient();
      var workspace = await pbiClient.Groups.GetGroupAsync(WorkspaceId);
      var datasets = (await pbiClient.Datasets.GetDatasetsInGroupAsync(WorkspaceId)).Value;
      var embeddedDatasets = new List<EmbeddedDataset>();
      
      foreach (var dataset in datasets) {
        embeddedDatasets.Add(new EmbeddedDataset {
          id = dataset.Id,
          name = dataset.Name,
          createReportEmbedURL = dataset.CreateReportEmbedURL
        });
      }

      var reports = (await pbiClient.Reports.GetReportsInGroupAsync(WorkspaceId)).Value;
      
      var embeddedReports = new List<EmbeddedReport>();
      foreach (var report in reports) {
        embeddedReports.Add(new EmbeddedReport {
          id = report.Id.ToString(),
          name = report.Name,
          embedUrl = report.EmbedUrl,
          datasetId = report.DatasetId,
          reportType = report.ReportType
        });
      }

      IList<GenerateTokenRequestV2Dataset> datasetRequests = new List<GenerateTokenRequestV2Dataset>();
      IList<string> datasetIds = new List<string>();

      foreach (var dataset in datasets) {
        datasetRequests.Add(new GenerateTokenRequestV2Dataset(dataset.Id, xmlaPermissions: XmlaPermissions.ReadOnly));
        datasetIds.Add(dataset.Id);
      }

      IList<GenerateTokenRequestV2Report> reportRequests = new List<GenerateTokenRequestV2Report>();
      foreach (var report in reports) {
        Boolean userCanEdit = report.ReportType.Equals("PowerBIReport");
        reportRequests.Add(new GenerateTokenRequestV2Report(report.Id, allowEdit: userCanEdit));
      }

      var workspaceRequests = new List<GenerateTokenRequestV2TargetWorkspace>();
      workspaceRequests.Add(new GenerateTokenRequestV2TargetWorkspace(WorkspaceId));

      GenerateTokenRequestV2 tokenRequest =
        new GenerateTokenRequestV2 {
          Datasets = datasetRequests,
          Reports = reportRequests,
          TargetWorkspaces = workspaceRequests
        };

      // call to Power BI Service API and pass GenerateTokenRequest object to generate embed token
      var EmbedTokenResult = pbiClient.EmbedToken.GenerateToken(tokenRequest);

      return new EmbeddedViewModel {
        tenantName = workspace.Name.Replace("Tenant - ", ""),
        reports = embeddedReports,
        datasets = embeddedDatasets,
        embedToken = EmbedTokenResult.Token,
      };

    }

  }
}
