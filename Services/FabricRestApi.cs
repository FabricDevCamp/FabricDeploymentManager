using Microsoft.Fabric;
using FabricAdmin = Microsoft.Fabric.Api.Admin.Models;
using Microsoft.Fabric.Api;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Fabric.Api.Notebook.Models;
using Microsoft.Fabric.Api.Lakehouse.Models;
using Microsoft.Fabric.Api.Warehouse.Models;
using Microsoft.Fabric.Api.SemanticModel.Models;
using Microsoft.Fabric.Api.Report.Models;
using Microsoft.Fabric.Api.Utils;
using System.Text;
using System.Linq;
using System.Net.Http.Headers;
using FabricDeploymentManager.Services;
using Microsoft.AspNetCore.SignalR;
using FabricDeploymentManager.Services.AsyncProcessing;
using FabricDeploymentManager.Models;

namespace FabricDeploymentManager.Services {

  public class FabricRestApi {

    private EntraIdTokenManager tokenManager { get; }
    private SignalRLogger appLogger;
    private string fabricRestApiBaseUri;
    private string fabricCapacityId { get; }
    private string adminUserId { get; }
    private string tenantId;
    private string clientId;
    private string clientSecret;

    private string azureStorageAccountName;
    private string azureStorageAccountKey;
    private string azureStorageContainerName;
    private string azureStorageContainerPath;

    private string azureStorageServer;
    private string azureStoragePath;

    public FabricRestApi(IConfiguration Configuration,
                       EntraIdTokenManager TokenManager,
                       SignalRLogger AppLogger) {
      tokenManager = TokenManager;
      appLogger = AppLogger;
      fabricRestApiBaseUri = Configuration["Fabric:FabricRestApiBaseUrl"];
      fabricCapacityId = Configuration["Fabric:FabricCapacityId"];
      adminUserId = Configuration["Fabric:AdminUserId"];
      tenantId = Configuration["ServicePrincipal:TenantId"];
      clientId = Configuration["ServicePrincipal:ClientId"];
      clientSecret = Configuration["ServicePrincipal:ClientSecret"];
      azureStorageAccountName = Configuration["AzureStorage:AccountName"];
      azureStorageAccountKey = Configuration["AzureStorage:AccountKey"];
      azureStorageContainerName = Configuration["AzureStorage:ContainerName"];
      azureStorageContainerPath = Configuration["AzureStorage:ContainerPath"];
      azureStorageServer = $"https://{azureStorageAccountName}.dfs.core.windows.net";
      azureStoragePath = azureStorageContainerName + azureStorageContainerPath;
    }

    async Task<FabricClient> GetFabricClient() {
      string accessToken = await tokenManager.GetFabricAccessToken();
      return new FabricClient(accessToken, new Uri(fabricRestApiBaseUri));
    }

    public async Task<List<Workspace>> GetWorkspaces() {

      var fabricClient = await GetFabricClient();

      // get all workspaces (this includes My Workspapce)
      var allWorkspaces = fabricClient.Core.Workspaces.ListWorkspacesAsync().ToBlockingEnumerable().ToList();

      // filter out My Workspace
      return allWorkspaces.Where(workspace => workspace.Type == WorkspaceType.Workspace).ToList();
    }

    public async Task<List<WorkspaceRow>> GetWorkspaceRows() {

      var fabricClient = await GetFabricClient();

      var capacities = (await GetCapacities()).ToDictionary(capacity =>  capacity.Id, capacity => capacity);
      
      // get all workspaces (this includes My Workspapce)
      var workspaces = fabricClient.Core.Workspaces.ListWorkspacesAsync().ToBlockingEnumerable().ToList();
      // filter out My Workspace
      workspaces = workspaces.Where(workspace => workspace.Type == WorkspaceType.Workspace).ToList();
      // order by workspace name
      workspaces = workspaces.OrderBy(workspace => workspace.DisplayName).ToList();

      var workpaceRows = new List<WorkspaceRow>();
      foreach(var workspace in workspaces) {

        var capacityId = workspace.CapacityId;
        var capacity = workspace.CapacityId.HasValue ? capacities[workspace.CapacityId.Value] : null;
        workpaceRows.Add(new WorkspaceRow {
          Workspace = workspace,
          Capacity = capacity
        });
      }

      return workpaceRows;
    }

    public async Task<Capacity> GetCapacity(Guid CapacityId) {
      var capacities = await GetCapacities();
      foreach (var capacity in capacities) {
        if (capacity.Id == CapacityId) {
          return capacity;
        }
      }
      throw new ApplicationException("Could not find capcity");
    }

    public async Task<List<Capacity>> GetCapacities() {
      var fabricClient = await GetFabricClient();
      var capacities = fabricClient.Core.Capacities.ListCapacitiesAsync();
      return capacities.ToBlockingEnumerable().ToList();
    }

    public async Task<Workspace> GetWorkspace(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      return await fabricClient.Core.Workspaces.GetWorkspaceAsync(WorkspaceId);
    }

    public async Task<Workspace> GetWorkspaceByName(string WorkspaceName) {
      var fabricClient = await GetFabricClient();
      var workspaces = fabricClient.Core.Workspaces.ListWorkspaces().ToList();

      foreach (var workspace in workspaces) {
        if (workspace.DisplayName.Equals(WorkspaceName)) {
          return workspace;
        }
      }

      return null;
    }

    public async Task<WorkspaceInfo> GetWorkspaceInfo(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Workspaces.GetWorkspace(WorkspaceId);
    }

    public async Task<WorkspaceDetails> GetWorkspaceDetails(string WorkspaceId) {

      Guid workspaceId = new Guid(WorkspaceId);
      var workspace = await GetWorkspace(workspaceId);
      var capacity = await GetCapacity(workspace.CapacityId.Value);

      var workspaceItems = await GetWorkspaceItems(workspaceId);

      var sequencedWorkspaceItems = new List<Item>();

      var lakehouses = workspaceItems.Where(item => item.Type == ItemType.Lakehouse);
      var lakehouseNames = lakehouses.Select(item => item.DisplayName);

      sequencedWorkspaceItems.AddRange(lakehouses);
      sequencedWorkspaceItems.AddRange(workspaceItems.Where(item => item.Type == ItemType.Notebook));
      sequencedWorkspaceItems.AddRange(workspaceItems.Where(item => item.Type == ItemType.DataPipeline));

      // add semantic models that are not lakehouse default semantic models
      var semanticModels = workspaceItems.Where(item => item.Type == ItemType.SemanticModel);
      foreach (var model in semanticModels) {
        if (!lakehouseNames.Contains(model.DisplayName)) {
          sequencedWorkspaceItems.Add(model);
        }
      }

      sequencedWorkspaceItems.AddRange(workspaceItems.Where(item => item.Type == ItemType.Report));


      return new WorkspaceDetails {
        WorkspaceId = workspace.Id.ToString(),
        DisplayName = workspace.DisplayName,
        Description = workspace.Description,
        WorkpaceItems = sequencedWorkspaceItems,
        Capacity = capacity,
        WorkpaceConnetions = await GetWorkspaceConnections(workspaceId),
        WorkspaceMembers = await GetWorkspaceRoleAssignments(workspaceId),
        WorkspaceUrl = $"https://app.powerbi.com/groups/{WorkspaceId}"
      };

    }

    public async Task<Workspace> CreateWorkspace(string WorkspaceName, string CapacityId = null, string Description = null) {

      if(CapacityId == null) {
        CapacityId = fabricCapacityId;
      }

      var fabricClient = await GetFabricClient();

      var workspace = await GetWorkspaceByName(WorkspaceName);

      // delete workspace with same name if it exists
      if (workspace != null) {
        await DeleteWorkspace(workspace.Id);
        workspace = null;
      }

      var createRequest = new CreateWorkspaceRequest(WorkspaceName);
      createRequest.Description = Description;

      workspace = fabricClient.Core.Workspaces.CreateWorkspace(createRequest);
      Guid AdminUserId = new Guid(adminUserId);
      await AddUserAsWorkspaceMember(workspace.Id, AdminUserId, WorkspaceRole.Admin);

      if (CapacityId != null) {
        var capacityId = new Guid(CapacityId);
        await AssignWorkspaceToCapacity(workspace.Id, capacityId);
      }


      return workspace;
    }

    public async Task<Workspace> UpdateWorkspace(Guid WorkspaceId, string WorkspaceName, string Description = null) {

      var fabricClient = await GetFabricClient();

      var updateRequest = new UpdateWorkspaceRequest {
        DisplayName = WorkspaceName,
        Description = Description
      };

      return fabricClient.Core.Workspaces.UpdateWorkspace(WorkspaceId, updateRequest).Value;
    }

    public async Task<Workspace> UpdateWorkspaceDescription(string TargetWorkspace, string Description) {
      var workspace = await GetWorkspaceByName(TargetWorkspace);
      return await UpdateWorkspaceDescription(workspace.Id, Description);
    }

    public async Task<Workspace> UpdateWorkspaceDescription(Guid WorkspaceId, string Description) {

      var fabricClient = await GetFabricClient();

      var updateRequest = new UpdateWorkspaceRequest {
        Description = Description
      };

      return (await fabricClient.Core.Workspaces.UpdateWorkspaceAsync(WorkspaceId, updateRequest)).Value;
    }

    public async Task DeleteWorkspace(Guid WorkspaceId) {

      var fabricClient = await GetFabricClient();

      await DeleteWorkspaceResources(WorkspaceId);

      fabricClient.Core.Workspaces.DeleteWorkspace(WorkspaceId);
    }

    public async Task DeleteWorkspaceByName(string WorkspaceName) {
      var workspace = await GetWorkspaceByName(WorkspaceName);
      await DeleteWorkspace(workspace.Id);
    }

    public async Task DeleteWorkspaceResources(Guid WorkspaceId) {
      var connections = await GetConnections();
      foreach (var connection in connections) {
        if ((connection.DisplayName != null) &&
            (connection.DisplayName.Contains(WorkspaceId.ToString()))) {
          await DeleteConnection(connection.Id);
        }
      }
    }

    public async Task AssignWorkspaceToCapacity(Guid WorkspaceId, Guid CapacityId) {

      var fabricClient = await GetFabricClient();

      var assignRequest = new AssignWorkspaceToCapacityRequest(CapacityId);

      fabricClient.Core.Workspaces.AssignToCapacity(WorkspaceId, assignRequest);

    }

    public async Task ProvisionWorkspaceIdentity(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      fabricClient.Core.Workspaces.ProvisionIdentity(WorkspaceId);
    }

    public async Task AddUserAsWorkspaceMember(Guid WorkspaceId, Guid UserId, WorkspaceRole RoleAssignment) {
      var fabricClient = await GetFabricClient();
      var user = new Principal(UserId, PrincipalType.User);
      var roleAssignment = new AddWorkspaceRoleAssignmentRequest(user, RoleAssignment);
      fabricClient.Core.Workspaces.AddWorkspaceRoleAssignment(WorkspaceId, roleAssignment);
    }

    public async Task AddGroupAsWorkspaceMember(Guid WorkspaceId, Guid GroupId, WorkspaceRole RoleAssignment) {
      var fabricClient = await GetFabricClient();
      var group = new Principal(GroupId, PrincipalType.Group);
      var roleAssignment = new AddWorkspaceRoleAssignmentRequest(group, RoleAssignment);
      fabricClient.Core.Workspaces.AddWorkspaceRoleAssignment(WorkspaceId, roleAssignment);
    }

    public async Task AddServicePrincipalAsWorkspaceMember(Guid WorkspaceId, Guid ServicePrincipalObjectId, WorkspaceRole RoleAssignment) {
      var fabricClient = await GetFabricClient();
      var user = new Principal(ServicePrincipalObjectId, PrincipalType.ServicePrincipal);
      var roleAssignment = new AddWorkspaceRoleAssignmentRequest(user, RoleAssignment);
      fabricClient.Core.Workspaces.AddWorkspaceRoleAssignment(WorkspaceId, roleAssignment);
    }

    public async Task ViewWorkspaceRoleAssignments(Guid WorkspaceId) {

      var fabricClient = await GetFabricClient();

      var roleAssignments = fabricClient.Core.Workspaces.ListWorkspaceRoleAssignments(WorkspaceId);

      await appLogger.LogStep("Viewing workspace role assignments");
      foreach (var roleAssignment in roleAssignments) {
        await appLogger.LogSubstep($"{roleAssignment.Principal.DisplayName} ({roleAssignment.Principal.Type}) added in role of {roleAssignment.Role}");
      }

    }

    public async Task<IList<WorkspaceRoleAssignment>> GetWorkspaceRoleAssignments(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Workspaces.ListWorkspaceRoleAssignmentsAsync(WorkspaceId).ToBlockingEnumerable().ToList();
    }

    public async Task DeleteWorkspaceRoleAssignments(Guid WorkspaceId, Guid RoleAssignmentId) {
      var fabricClient = await GetFabricClient();
      fabricClient.Core.Workspaces.DeleteWorkspaceRoleAssignment(WorkspaceId, RoleAssignmentId);
    }

    public async Task<List<Connection>> GetConnections() {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Connections.ListConnections().ToList();
    }

    public async Task<List<Connection>> GetWorkspaceConnections(Guid WorkspaceId) {

      var allConnections = await GetConnections();
      var workspaceConnections = new List<Connection>();

      foreach (var connection in allConnections) {
        if ((connection.DisplayName != null) &&
            (connection.DisplayName.Contains(WorkspaceId.ToString()))) {
          workspaceConnections.Add(connection);
        }
      }

      return workspaceConnections;
    }

    public async Task<Connection> GetConnection(Guid ConnectionId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Connections.GetConnection(ConnectionId);
    }

    public async Task DisplayConnnections() {
      var connections = await GetConnections();

      foreach (var connection in connections) {
        Console.WriteLine($"Connection: {connection.Id}");
        Console.WriteLine($" - Display Name: {connection.DisplayName}");
        Console.WriteLine($" - Connectivity Type: {connection.ConnectivityType}");
        Console.WriteLine($" - Connection type: {connection.ConnectionDetails.Type}");
        Console.WriteLine($" - Connection path: {connection.ConnectionDetails.Path}");
        Console.WriteLine();
      }
    }

    public async Task DeleteConnection(Guid ConnectionId) {
      var fabricClient = await GetFabricClient();
      fabricClient.Core.Connections.DeleteConnection(ConnectionId);
    }

    public async Task DeleteConnectionIfItExists(string ConnectionName) {

      var connections = await GetConnections();

      foreach (var connection in connections) {
        if (connection.DisplayName == ConnectionName) {
          await DeleteConnection(connection.Id);
        }
      }

    }

    public async Task<Connection> GetConnectionByName(string ConnectionName) {

      var connections = await GetConnections();

      foreach (var connection in connections) {
        if (connection.DisplayName == ConnectionName) {
          return connection;
        }
      }

      return null;

    }

    public async Task<Connection> CreateConnection(CreateConnectionRequest CreateConnectionRequest) {

      var fabricClient = await GetFabricClient();

      var existingConnection = await GetConnectionByName(CreateConnectionRequest.DisplayName);
      if (existingConnection != null) {
        return existingConnection;
      }
      else {

        if (CreateConnectionRequest.PrivacyLevel == null) {
          CreateConnectionRequest.PrivacyLevel = PrivacyLevel.Organizational;
        }

        var connection = fabricClient.Core.Connections.CreateConnection(CreateConnectionRequest).Value;

        Guid AdminUserId = new Guid(adminUserId);
        await AddConnectionRoleAssignmentForUser(connection.Id, AdminUserId, ConnectionRole.Owner);
        return connection;
      }

    }

    public async Task AddConnectionRoleAssignmentForUser(Guid ConnectionId, Guid UserId, ConnectionRole Role) {
      var fabricClient = await GetFabricClient();
      var principal = new Principal(UserId, PrincipalType.User);
      var request = new AddConnectionRoleAssignmentRequest(principal, Role);
      fabricClient.Core.Connections.AddConnectionRoleAssignment(ConnectionId, request);
    }

    public async Task AddConnectionRoleAssignmentForServicePrincipal(Guid ConnectionId, Guid ServicePrincipalId, ConnectionRole Role) {
      var fabricClient = await GetFabricClient();
      var principal = new Principal(ServicePrincipalId, PrincipalType.ServicePrincipal);
      var request = new AddConnectionRoleAssignmentRequest(principal, Role);
      fabricClient.Core.Connections.AddConnectionRoleAssignment(ConnectionId, request);
    }

    public async Task GetSupportedConnectionTypes() {
      var fabricClient = await GetFabricClient();
      var connTypes = fabricClient.Core.Connections.ListSupportedConnectionTypes();

      foreach (var connType in connTypes) {
        Console.WriteLine(connType.Type);
      }

    }

    public async Task<Item> CreateItem(Guid WorkspaceId, CreateItemRequest CreateRequest) {
      var fabricClient = await GetFabricClient();
      var newItem = fabricClient.Core.Items.CreateItemAsync(WorkspaceId, CreateRequest).Result.Value;
      return newItem;
    }

    public async Task<List<Item>> GetItems(Guid WorkspaceId, string ItemType = null) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Items.ListItems(WorkspaceId, ItemType).ToList();
    }

    public async Task DeleteItem(Guid WorkspaceId, Item item) {
      var fabricClient = await GetFabricClient();
      var newItem = fabricClient.Core.Items.DeleteItem(WorkspaceId, item.Id.Value);
    }

    public async Task DisplayWorkspaceItems(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      List<Item> items = fabricClient.Core.Items.ListItems(WorkspaceId).ToList();

      foreach (var item in items) {
        Console.WriteLine($"{item.DisplayName} is a {item.Type} with an id of {item.Id}");
      }

    }

    public async Task<Item> UpdateItem(Guid WorkspaceId, Guid ItemId, string ItemName, string Description = null) {

      var fabricClient = await GetFabricClient();

      var updateRequest = new UpdateItemRequest {
        DisplayName = ItemName,
        Description = Description
      };

      var item = fabricClient.Core.Items.UpdateItem(WorkspaceId, ItemId, updateRequest).Value;

      return item;

    }

    public async Task<List<Item>> GetWorkspaceItems(Guid WorkspaceId, string ItemType = null) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Items.ListItems(WorkspaceId, ItemType).ToList();
    }

    public async Task<List<Item>> GetWorkspaceItems(Guid WorkspaceId, ItemType TargetItemType) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Items.ListItems(WorkspaceId, TargetItemType.ToString()).ToList();
    }

    public async Task<ItemDefinition> GetItemDefinition(Guid WorkspaceId, Guid ItemId, string Format = null) {
      var fabricClient = await GetFabricClient();
      var response = fabricClient.Core.Items.GetItemDefinitionAsync(WorkspaceId, ItemId, Format).Result.Value;
      return response.Definition;
    }

    public async Task UpdateItemDefinition(Guid WorkspaceId, Guid ItemId, UpdateItemDefinitionRequest UpdateRequest) {
      var fabricClient = await GetFabricClient();
      fabricClient.Core.Items.UpdateItemDefinition(WorkspaceId, ItemId, UpdateRequest);
    }

    public async Task<SemanticModel> GetSemanticModelByName(Guid WorkspaceId, string Name) {

      var fabricClient = await GetFabricClient();

      var models = fabricClient.SemanticModel.Items.ListSemanticModels(WorkspaceId);
      foreach (var model in models) {
        if (Name == model.DisplayName) {
          return model;
        }
      }
      return null;
    }

    public async Task<Report> GetReportByName(Guid WorkspaceId, string Name) {

      var fabricClient = await GetFabricClient();

      var reports = fabricClient.Report.Items.ListReports(WorkspaceId);
      foreach (var report in reports) {
        if (Name == report.DisplayName) {
          return report;
        }
      }
      return null;
    }

    public async Task<Item> CreateLakehouse(Guid WorkspaceId, string LakehouseName, bool EnableSchemas = false) {

      // Item create request for lakehouse des not include item definition
      var createRequest = new CreateItemRequest(LakehouseName, ItemType.Lakehouse);

      if (EnableSchemas) {
        createRequest.CreationPayload = new List<KeyValuePair<string, object>>() {
          new KeyValuePair<string, object>("enableSchemas", true)
      };
      }

      // create lakehouse
      return await CreateItem(WorkspaceId, createRequest);
    }

    public async Task RefreshLakehouseTableSchema(string SqlEndpointId) {

      string accessToken = await tokenManager.GetFabricAccessToken();

      string restUri = $"{fabricRestApiBaseUri}/v1.0/myorg/lhdatamarts/{SqlEndpointId}";

      HttpContent body = new StringContent("{ \"commands\":[{ \"$type\":\"MetadataRefreshCommand\"}]}");

      body.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");

      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add("Accept", "application/json");
      client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

      HttpResponseMessage response = await client.PostAsync(restUri, body);

    }

    public async Task<Shortcut> CreateLakehouseShortcut(Guid WorkspaceId, Guid LakehouseId, CreateShortcutRequest CreateShortcutRequest) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.OneLakeShortcuts.CreateShortcut(WorkspaceId, LakehouseId, CreateShortcutRequest).Value;
    }

    public async Task<List<Shortcut>> GetLakehouseShortcuts(Guid WorkspaceId, Guid LakehouseId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.OneLakeShortcuts.ListShortcuts(WorkspaceId, LakehouseId).ToList();
    }

    public async Task<Lakehouse> GetLakehouse(Guid WorkspaceId, Guid LakehousId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Lakehouse.Items.GetLakehouse(WorkspaceId, LakehousId).Value;
    }

    public async Task<Lakehouse> GetLakehouseByName(Guid WorkspaceId, string LakehouseName) {

      var fabricClient = await GetFabricClient();

      var lakehouses = fabricClient.Lakehouse.Items.ListLakehouses(WorkspaceId);

      foreach (var lakehouse in lakehouses) {
        if (lakehouse.DisplayName == LakehouseName) {
          return lakehouse;
        }
      }

      return null;
    }

    public async Task<Notebook> GetNotebookByName(Guid WorkspaceId, string NotebookName) {

      var fabricClient = await GetFabricClient();

      var notebooks = fabricClient.Notebook.Items.ListNotebooks(WorkspaceId);

      foreach (var notebook in notebooks) {
        if (notebook.DisplayName == NotebookName) {
          return notebook;
        }
      }

      return null;
    }

    public async Task<SqlEndpointProperties> GetSqlEndpointForLakehouse(Guid WorkspaceId, Guid LakehouseId) {

      var lakehouse = await GetLakehouse(WorkspaceId, LakehouseId);

      while ((lakehouse.Properties.SqlEndpointProperties == null) ||
             (lakehouse.Properties.SqlEndpointProperties.ProvisioningStatus != "Success")) {
        lakehouse = await GetLakehouse(WorkspaceId, LakehouseId);
        Thread.Sleep(10000); // wait 10 seconds
      }

      return lakehouse.Properties.SqlEndpointProperties;

    }

    public async Task<Item> CreateWarehouse(Guid WorkspaceId, string WarehouseName) {

      // Item create request for lakehouse des not include item definition
      var createRequest = new CreateItemRequest(WarehouseName, ItemType.Warehouse);

      // create lakehouse
      return await CreateItem(WorkspaceId, createRequest);
    }

    public async Task<Warehouse> GetWareHouseByName(Guid WorkspaceId, string WarehouseName) {

      var fabricClient = await GetFabricClient();

      var warehouses = fabricClient.Warehouse.Items.ListWarehouses(WorkspaceId);

      foreach (var warehouse in warehouses) {
        if (warehouse.DisplayName == WarehouseName) {
          return warehouse;
        }
      }

      return null;
    }

    public async Task<Warehouse> GetWarehouse(Guid WorkspaceId, Guid WarehouseId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Warehouse.Items.GetWarehouse(WorkspaceId, WarehouseId).Value;
    }

    public async Task<string> GetSqlConnectionStringForWarehouse(Guid WorkspaceId, Guid WarehouseId) {
      var warehouse = await GetWarehouse(WorkspaceId, WarehouseId);
      return warehouse.Properties.ConnectionString;
    }

    public async Task LoadLakehouseTableFromParquet(Guid WorkspaceId, Guid LakehouseId, string SourceFile, string TableName) {

      var fabricClient = await GetFabricClient();

      var loadTableRequest = new LoadTableRequest(SourceFile, PathType.File);
      loadTableRequest.Recursive = false;
      loadTableRequest.Mode = ModeType.Overwrite;
      loadTableRequest.FormatOptions = new Parquet();

      fabricClient.Lakehouse.Tables.LoadTableAsync(WorkspaceId, LakehouseId, TableName, loadTableRequest).Wait();

    }

    public async Task LoadLakehouseTableFromCsv(Guid WorkspaceId, Guid LakehouseId, string SourceFile, string TableName) {

      var fabricClient = await GetFabricClient();

      var loadTableRequest = new LoadTableRequest(SourceFile, PathType.File);
      loadTableRequest.Recursive = false;
      loadTableRequest.Mode = ModeType.Overwrite;
      loadTableRequest.FormatOptions = new Csv();

      fabricClient.Lakehouse.Tables.LoadTableAsync(WorkspaceId, LakehouseId, TableName, loadTableRequest).Wait();
    }

    public async Task RunNotebook(Guid WorkspaceId, Item Notebook, RunOnDemandItemJobRequest JobRequest = null) {

      var fabricClient = await GetFabricClient();

      await appLogger.LogOperationInProgress();

      var response = fabricClient.Core.JobScheduler.RunOnDemandItemJob(WorkspaceId, Notebook.Id.Value, "RunNotebook", JobRequest);

      if (response.Status == 202) {

        string location = response.GetLocationHeader();
        int? retryAfter = 20; // response.GetRetryAfterHeader();
        Guid JobInstanceId = response.GetTriggeredJobId();

        Thread.Sleep(retryAfter.Value * 1000);

        var jobInstance = fabricClient.Core.JobScheduler.GetItemJobInstance(WorkspaceId, Notebook.Id.Value, JobInstanceId).Value;

        while (jobInstance.Status == Status.NotStarted || jobInstance.Status == Status.InProgress) {
          await appLogger.LogOperationInProgress();
          Thread.Sleep(retryAfter.Value * 1000);
          jobInstance = fabricClient.Core.JobScheduler.GetItemJobInstance(WorkspaceId, Notebook.Id.Value, JobInstanceId).Value;
        }

        if (jobInstance.Status == Status.Completed) {
          return;
        }

        if (jobInstance.Status == Status.Failed) {
          await appLogger.LogSubstep("Notebook execution failed");
          await appLogger.LogSubstep(jobInstance.FailureReason.Message);
        }

        if (jobInstance.Status == Status.Cancelled) {
          await appLogger.LogSubstep("Notebook execution cancelled");
        }

        if (jobInstance.Status == Status.Deduped) {
          await appLogger.LogSubstep("Notebook execution deduped");
        }
      }
      else {
        await appLogger.LogStep("Notebook execution failed when starting");
      }

    }

    public async Task RunDataPipeline(Guid WorkspaceId, Item DataPipeline) {

      var fabricClient = await GetFabricClient();

      await appLogger.LogOperationInProgress();

      var response = fabricClient.Core.JobScheduler.RunOnDemandItemJob(WorkspaceId, DataPipeline.Id.Value, "Pipeline");

      if (response.Status == 202) {

        string location = response.GetLocationHeader();
        int? retryAfter = 10; // response.GetRetryAfterHeader();
        Guid JobInstanceId = response.GetTriggeredJobId();

        Thread.Sleep(retryAfter.Value * 1000);

        var jobInstance = fabricClient.Core.JobScheduler.GetItemJobInstance(WorkspaceId, DataPipeline.Id.Value, JobInstanceId).Value;

        while (jobInstance.Status == Status.NotStarted || jobInstance.Status == Status.InProgress) {
          Thread.Sleep(retryAfter.Value * 1000);
          await appLogger.LogOperationInProgress();
          jobInstance = fabricClient.Core.JobScheduler.GetItemJobInstance(WorkspaceId, DataPipeline.Id.Value, JobInstanceId).Value;
        }

        if (jobInstance.Status == Status.Completed) {
          return;
        }

        if (jobInstance.Status == Status.Failed) {
          await appLogger.LogStep("Data pipeline execution failed");
          await appLogger.LogSubstep(jobInstance.FailureReason.Message);
        }

        if (jobInstance.Status == Status.Cancelled) {
          await appLogger.LogSubstep("Data pipeline execution cancelled");
        }

        if (jobInstance.Status == Status.Deduped) {
          await appLogger.LogSubstep("Data pipeline execution deduped");
        }
      }
      else {
        await appLogger.LogStep("Data pipeline execution failed when starting");
      }
    }

    public async Task CreateShortcut(Guid WorkspaceId, Guid LakehouseId, CreateShortcutRequest CreateShortcutRequest) {
      var fabricClient = await GetFabricClient();
      var response = fabricClient.Core.OneLakeShortcuts.CreateShortcut(WorkspaceId, LakehouseId, CreateShortcutRequest).Value;
    }

    public async Task<Shortcut> CreateAdlsGen2Shortcut(Guid WorkspaceId, Guid LakehouseId, string Name, string Path, Uri Location, string Subpath, Guid ConnectionId) {

      var fabricClient = await GetFabricClient();

      var target = new CreatableShortcutTarget {
        AdlsGen2 = new AdlsGen2(Location, Subpath, ConnectionId)
      };

      var createRequest = new CreateShortcutRequest(Path, Name, target);

      return fabricClient.Core.OneLakeShortcuts.CreateShortcut(WorkspaceId, LakehouseId, createRequest).Value;

    }

    // create different types of connections

    public async Task<Connection> CreateSqlConnectionWithServicePrincipal(string Server, string Database, Workspace TargetWorkspace = null, Item TargetLakehouse = null) {

      string displayName = string.Empty;

      if (TargetWorkspace != null) {
        displayName += $"Workspace[{TargetWorkspace.Id.ToString()}]-";
        if (TargetLakehouse != null) {
          displayName += $"Lakehouse[{TargetLakehouse.DisplayName}]";
        }
        else {
          displayName += $"SQL";
        }
      }
      else {
        displayName += $"SQL-SPN-{Server}:{Database}";
      }

      string connectionType = "SQL";
      string creationMethod = "Sql";

      var creationMethodParams = new List<ConnectionDetailsParameter> {
      new ConnectionDetailsTextParameter("server", Server),
      new ConnectionDetailsTextParameter("database", Database)
    };

      var createConnectionDetails = new CreateConnectionDetails(connectionType, creationMethod, creationMethodParams);

      Credentials credentials = new ServicePrincipalCredentials(new Guid(tenantId),
                                                                new Guid(clientId),
                                                                clientSecret);

      var createCredentialDetails = new CreateCredentialDetails(credentials) {
        SingleSignOnType = SingleSignOnType.None,
        ConnectionEncryption = ConnectionEncryption.NotEncrypted,
        SkipTestConnection = false
      };

      var createConnectionRequest = new CreateCloudConnectionRequest(displayName,
                                                                     createConnectionDetails,
                                                                     createCredentialDetails);

      var connection = await CreateConnection(createConnectionRequest);

      return connection;

    }

    public async Task<Connection> CreateAnonymousWebConnection(string Url, Workspace TargetWorkspace = null) {

      string displayName = string.Empty;

      if (TargetWorkspace != null) {
        displayName += $"Workspace[{TargetWorkspace.Id.ToString()}]-";
      }

      displayName += $"Web";

      string connectionType = "Web";
      string creationMethod = "Web";

      var creationMethodParams = new List<ConnectionDetailsParameter> {
      new ConnectionDetailsTextParameter("url", Url)
    };

      var createConnectionDetails = new CreateConnectionDetails(connectionType, creationMethod, creationMethodParams);

      Credentials credentials = new AnonymousCredentials();

      var createCredentialDetails = new CreateCredentialDetails(credentials) {
        SingleSignOnType = SingleSignOnType.None,
        ConnectionEncryption = ConnectionEncryption.NotEncrypted,
        SkipTestConnection = false
      };

      var createConnectionRequest = new CreateCloudConnectionRequest(displayName,
                                                                     createConnectionDetails,
                                                                     createCredentialDetails);

      var connection = await CreateConnection(createConnectionRequest);

      return connection;
    }

    public async Task<Connection> CreateAnonymousWeb2Connection() {

      string displayName = string.Empty;


      displayName += $"Web2 - {Guid.NewGuid().ToString()}";

      string connectionType = "WebForPipeline";
      string creationMethod = "WebForPipeline.Contents";

      var creationMethodParams = new List<ConnectionDetailsParameter> {
        new ConnectionDetailsTextParameter("baseUrl", "https://api.fabric.microsoft.com/v1/"),
        new ConnectionDetailsTextParameter("audience", "https://api.fabric.microsoft.com/.default")
      };

      var createConnectionDetails = new CreateConnectionDetails(connectionType, creationMethod, creationMethodParams);

      Credentials credentials = new ServicePrincipalCredentials(new Guid(tenantId),
                                                                new Guid(clientId),
                                                                clientSecret);

      var createCredentialDetails = new CreateCredentialDetails(credentials) {
        SingleSignOnType = SingleSignOnType.None,
        ConnectionEncryption = ConnectionEncryption.NotEncrypted,
        SkipTestConnection = false
      };

      var createConnectionRequest = new CreateCloudConnectionRequest(displayName,
                                                                      createConnectionDetails,
                                                                      createCredentialDetails);

      var connection = await CreateConnection(createConnectionRequest);

      return connection;
    }

    public async Task<Connection> CreateAzureStorageConnectionWithServicePrincipal(string Server, string Path, Workspace TargetWorkspace = null) {

      string displayName = string.Empty;

      if (TargetWorkspace != null) {
        displayName += $"Workspace[{TargetWorkspace.Id.ToString()}]-";
      }

      displayName = $"ADLS";

      string connectionType = "AzureDataLakeStorage";
      string creationMethod = "AzureDataLakeStorage";

      var creationMethodParams = new List<ConnectionDetailsParameter> {
        new ConnectionDetailsTextParameter("server", Server),
        new ConnectionDetailsTextParameter("path", Path)
      };

      var createConnectionDetails = new CreateConnectionDetails(connectionType, creationMethod, creationMethodParams);

      Credentials creds = new ServicePrincipalCredentials(new Guid(tenantId),
                                                          new Guid(clientId),
                                                          clientSecret);

      var createCredentialDetails = new CreateCredentialDetails(creds) {
        SingleSignOnType = SingleSignOnType.None,
        ConnectionEncryption = ConnectionEncryption.NotEncrypted,
        SkipTestConnection = false
      };

      var createConnectionRequest = new CreateCloudConnectionRequest(displayName,
                                                                     createConnectionDetails,
                                                                     createCredentialDetails);

      var connection = await CreateConnection(createConnectionRequest);

      return connection;
    }

    public async Task<Connection> CreateAzureStorageConnectionWithAccountKey(string Server, string Path, Workspace TargetWorkspace = null) {

      string displayName = string.Empty;

      if (TargetWorkspace != null) {
        displayName += $"Workspace[{TargetWorkspace.Id.ToString()}]-";
      }

      displayName += $"ADLS";


      string connectionType = "AzureDataLakeStorage";
      string creationMethod = "AzureDataLakeStorage";

      var creationMethodParams = new List<ConnectionDetailsParameter> {
      new ConnectionDetailsTextParameter("server", Server),
      new ConnectionDetailsTextParameter("path", Path)
    };

      var createConnectionDetails = new CreateConnectionDetails(connectionType, creationMethod, creationMethodParams);

      Credentials creds = new KeyCredentials(azureStorageAccountKey);

      var createCredentialDetails = new CreateCredentialDetails(creds) {
        SingleSignOnType = SingleSignOnType.None,
        ConnectionEncryption = ConnectionEncryption.NotEncrypted,
        SkipTestConnection = false
      };

      var createConnectionRequest = new CreateCloudConnectionRequest(displayName,
                                                                     createConnectionDetails,
                                                                     createCredentialDetails);

      var connection = await CreateConnection(createConnectionRequest);


      return connection;
    }

    public async Task<Connection> CreateAzureStorageConnectionWithWorkspaceIdentity(string Server, string Path, bool ReuseExistingConnection = false) {

      string displayName = $"ADLS-AccountKey-{Server}-{Path}";

      string connectionType = "AzureDataLakeStorage";
      string creationMethod = "AzureDataLakeStorage";

      var creationMethodParams = new List<ConnectionDetailsParameter> {
      new ConnectionDetailsTextParameter("server", Server),
      new ConnectionDetailsTextParameter("path", Path)
    };

      var createConnectionDetails = new CreateConnectionDetails(connectionType, creationMethod, creationMethodParams);

      Credentials creds = new WorkspaceIdentityCredentials();

      var createCredentialDetails = new CreateCredentialDetails(creds) {
        SingleSignOnType = SingleSignOnType.None,
        ConnectionEncryption = ConnectionEncryption.NotEncrypted,
        SkipTestConnection = false
      };

      var createConnectionRequest = new CreateCloudConnectionRequest(displayName,
                                                                     createConnectionDetails,
                                                                     createCredentialDetails);

      return await CreateConnection(createConnectionRequest);

    }

    // GIT integration

    public async Task ConnectWorkspaceToGitRepository(Guid WorkspaceId, GitConnectRequest connectionRequest) {

      var fabricClient = await GetFabricClient();


      await appLogger.LogStep("Connecting workspace to Azure Dev Ops");

      var connectResponse = fabricClient.Core.Git.Connect(WorkspaceId, connectionRequest);

      await appLogger.LogSubstep("GIT connection established between workspace and Azure Dev Ops");

      // (2) initialize connection
      var initRequest = new InitializeGitConnectionRequest {
        InitializationStrategy = InitializationStrategy.PreferWorkspace
      };

      var initResponse = fabricClient.Core.Git.InitializeConnection(WorkspaceId, initRequest).Value;


      if (initResponse.RequiredAction == RequiredAction.CommitToGit) {
        // (2A) commit workspace changes to GIT
        await appLogger.LogSubstep("Committing changes to GIT repository");

        var commitToGitRequest = new CommitToGitRequest(CommitMode.All) {
          WorkspaceHead = initResponse.WorkspaceHead,
          Comment = "Initial commit to GIT"
        };

        fabricClient.Core.Git.CommitToGit(WorkspaceId, commitToGitRequest);

        await appLogger.LogSubstep("Workspace changes committed to GIT");
      }

      if (initResponse.RequiredAction == RequiredAction.UpdateFromGit) {
        // (2B) update workspace from source files in GIT
        await appLogger.LogSubstep("Updating workspace from source files in GIT");

        var updateFromGitRequest = new UpdateFromGitRequest(initResponse.RemoteCommitHash) {
          ConflictResolution = new WorkspaceConflictResolution(
            ConflictResolutionType.Workspace,
            ConflictResolutionPolicy.PreferWorkspace)
        };

        fabricClient.Core.Git.UpdateFromGit(WorkspaceId, updateFromGitRequest);
        await appLogger.LogSubstep("Workspace updated from source files in GIT");
      }

      await appLogger.LogSubstep("Workspace connection intialization complete");

    }

    public async Task DisconnectWorkspaceFromGitRepository(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      fabricClient.Core.Git.Disconnect(WorkspaceId);
    }

    public async Task<GitConnection> GetWorkspaceGitConnection(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Git.GetConnection(WorkspaceId);
    }

    public async Task<GitStatusResponse> GetWorkspaceGitStatus(Guid WorkspaceId) {
      var fabricClient = await GetFabricClient();
      return fabricClient.Core.Git.GetStatus(WorkspaceId).Value;
    }

    public async Task CommitWoGrkspaceToGit(Guid WorkspaceId) {

      var fabricClient = await GetFabricClient();

      await appLogger.LogStep("Committing workspace changes to GIT");

      var gitStatus = await GetWorkspaceGitStatus(WorkspaceId);

      var commitRequest = new CommitToGitRequest(CommitMode.All);
      commitRequest.Comment = "Workspaces changes after semantic model refresh";
      commitRequest.WorkspaceHead = gitStatus.WorkspaceHead;

      fabricClient.Core.Git.CommitToGit(WorkspaceId, commitRequest);

    }

    public async Task UpdateWorkspaceFromGit(Guid WorkspaceId) {

      var fabricClient = await GetFabricClient();

      await appLogger.LogStep("Syncing updates to workspace from GIT");

      var gitStatus = await GetWorkspaceGitStatus(WorkspaceId);

      var updateFromGitRequest = new UpdateFromGitRequest(gitStatus.RemoteCommitHash) {
        WorkspaceHead = gitStatus.WorkspaceHead,
        Options = new UpdateOptions { AllowOverrideItems = true },
        ConflictResolution = new WorkspaceConflictResolution(ConflictResolutionType.Workspace,
                                                             ConflictResolutionPolicy.PreferWorkspace)
      };

      fabricClient.Core.Git.UpdateFromGit(WorkspaceId, updateFromGitRequest);
    }

  }
}