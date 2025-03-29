using Azure;
using Azure.Core;
using System.Linq;
using FabricDeploymentManager.Models;
using FabricDeploymentManager.Services;
using FabricDeploymentManager.Services.AsyncProcessing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Identity.Web;
using Microsoft.TeamFoundation.Core.WebApi;
using System.Text.Json;
using System.Text;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace FabricDeploymentManager.Services {

  public class DeploymentManager {

    private readonly ILogger<DeploymentManager> logger;
    private SignalRLogger appLogger;
    private PowerBiRestApi powerBiRestApi;
    private FabricRestApi fabricRestApi;
    private ItemDefinitionFactory itemDefinitionFactory;
    private AdoProjectManager adoProjectManager;

    private string fabricCapacityId { get; }
    private string azureStorageAccountName;
    private string azureStorageContainerName;
    private string azureStorageContainerPath;
    private string azureStorageServer;
    private string azureStoragePath;

    public DeploymentManager(IConfiguration Configuration,
                                 ILogger<DeploymentManager> Logger,
                                 SignalRLogger AppLogger,
                                 PowerBiRestApi PowerBiRestApi,
                                 FabricRestApi FabricRestApi,
                                 ItemDefinitionFactory ItemDefinitionFactory,
                                 AdoProjectManager AdoProjectManager) {
      logger = Logger;
      appLogger = AppLogger;
      powerBiRestApi = PowerBiRestApi;
      itemDefinitionFactory = ItemDefinitionFactory;
      fabricRestApi = FabricRestApi;
      fabricCapacityId = Configuration["Fabric:FabricCapacityId"];
      azureStorageAccountName = Configuration["AzureStorage:AccountName"];
      azureStorageContainerName = Configuration["AzureStorage:ContainerName"];
      azureStorageContainerPath = Configuration["AzureStorage:ContainerPath"];
      azureStorageServer = $"https://{azureStorageAccountName}.dfs.core.windows.net/";
      azureStoragePath = azureStorageContainerName + azureStorageContainerPath;
      adoProjectManager = AdoProjectManager;
    }

    public async Task<IList<Capacity>> GetCapacities() {
      return await fabricRestApi.GetCapacities();
    }

    public async Task<IList<Connection>> GetConnections() {
      return await fabricRestApi.GetConnections();
    }

    public async Task<Connection> GetConnection(Guid ConnectionId) {
      return await fabricRestApi.GetConnection(ConnectionId);
    }

    public async Task DeleteConnection(Guid ConnectionId) {
      await fabricRestApi.DeleteConnection(ConnectionId);
    }

    public async Task<Workspace> GetWorkspace(string WorkspaceId) {
      return await fabricRestApi.GetWorkspace(new Guid(WorkspaceId));
    }

    public async Task<WorkspaceDetails> GetWorkspaceDetails(string WorkspaceId) {
      var workspaceDetails = await fabricRestApi.GetWorkspaceDetails(WorkspaceId);
      workspaceDetails.AdoProject = await adoProjectManager.GetProject(workspaceDetails.DisplayName);
      if(workspaceDetails.AdoProject != null) {
        var branches = (await adoProjectManager.GetProjectBranches(workspaceDetails.DisplayName)).Where(branch => branch != "main");
        workspaceDetails.HasAdoExports = (branches.Count() > 0);
      }
      else {
        workspaceDetails.HasAdoExports = false;
      }
        return workspaceDetails;
    }

    public async Task<string> GetNextWorkspaceName() {
      var workspaces = await fabricRestApi.GetWorkspaces();

      var workspaceNames = workspaces.Select(workspace => workspace.DisplayName);

      int counter = 1;
      string displayName = $"Customer Tenant {counter.ToString("00")}";
      while (workspaceNames.Contains(displayName)) {
        counter += 1;
        displayName = $"Customer Tenant {counter.ToString("00")}";
      }

      return displayName;
    }

    public List<string> GetSolutionExportNames() {
      return itemDefinitionFactory.GetSolutionExportNames();
    }

    public List<DeploymentConfiguration> GetSolutionExports() {
      return itemDefinitionFactory.GetSolutionExports();
    }

    public void DeleteExport(string ExportName) {
      itemDefinitionFactory.DeleteSolutionExport(ExportName);
    }

    public async Task DeleteWorkspace(string WorkspaceId) {
      await fabricRestApi.DeleteWorkspace(new Guid(WorkspaceId));
    }

    public Dictionary<string, string> AvailableSolutions {
      get {
        return new Dictionary<string, string> {
          {"PowerBiSolution","Custom Power BI Solution" },
          {"NotebookSolution","Custom Notebook Solution" },
          {"ShortcutSolution","Custom Shortcut Solution" },
          {"DataPipelineSolution","Custom Data Pipeline Solution" },
        };
      }
    }

    public async Task<EmbeddedViewModel> GetEmbeddedViewModel(string WorkspaceId) {
      return await powerBiRestApi.GetEmbeddedViewModel(new Guid(WorkspaceId));
    }

    public async Task AddSolutionItem(string WorkspaceId, string ItemName) {

      Guid workspaceId = new Guid(WorkspaceId);
      var workspace = await fabricRestApi.GetWorkspace(new Guid(WorkspaceId));

      switch (ItemName) {

        case "TimeIntelligenceReport":
          await appLogger.LogSolution($"Adding [Product Sales Time Intelligence.Report] to [{workspace.DisplayName}]");
          var models = await fabricRestApi.GetItems(workspace.Id, "SemanticModel");
          Item productSalesModel = null;

          await appLogger.LogStep($"Looking for compatible semantic model");

          foreach (var currentModel in models) {
            if (currentModel.DisplayName == "Product Sales Imported Model" ||
               currentModel.DisplayName == "Product Sales DirectLake Model") {
              productSalesModel = currentModel;
              await appLogger.LogSubstep($"Found [{currentModel.DisplayName}.SemanticModel]");
              break;
            }
          }

          if (productSalesModel != null) {
            string reportDefinitionFolder2 = "Product Sales Time Intelligence.Report";
            await appLogger.LogStep($"Creating [{reportDefinitionFolder2}]");
            var createReportRequest2 = itemDefinitionFactory.GetCreateItemRequestFromFolder(reportDefinitionFolder2);
            createReportRequest2.Definition = itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReportRequest2.Definition, productSalesModel.Id.Value);
            await fabricRestApi.CreateItem(workspace.Id, createReportRequest2);
            await appLogger.LogSubstep($"Report created");
          }

          break;

        case "Top10CitiesReport":
          await appLogger.LogSolution($"Adding [Product Sales Top 10 Cities.Report] to [{workspace.DisplayName}]");

          var models3 = await fabricRestApi.GetItems(workspace.Id, "SemanticModel");

          Item productSalesModel3 = null;

          await appLogger.LogStep($"Looking for compatible semantic model");

          foreach (var currentModel in models3) {
            if (currentModel.DisplayName == "Product Sales Imported Model" ||
               currentModel.DisplayName == "Product Sales DirectLake Model") {
              productSalesModel3 = currentModel;
              await appLogger.LogSubstep($"Found [{currentModel.DisplayName}.SemanticModel]");
              break;
            }
          }

          if (productSalesModel3 != null) {
            string reportDefinitionFolder3 = "Product Sales Top 10 Cities.Report";
            await appLogger.LogStep($"Creating [{reportDefinitionFolder3}]");
            var createReportRequest3 = itemDefinitionFactory.GetCreateItemRequestFromFolder(reportDefinitionFolder3);
            createReportRequest3.Definition = itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReportRequest3.Definition, productSalesModel3.Id.Value);
            await fabricRestApi.CreateItem(workspace.Id, createReportRequest3);
            await appLogger.LogSubstep($"Report created");
          }


          break;

        case "PowerHour":
          await appLogger.LogSolution($"Adding [Power Hour.Report] to [{workspace.DisplayName}]");

          string modelDefinitionFolder = "Power Hour.SemanticModel";
          await appLogger.LogStep($"Creating [{modelDefinitionFolder}]");
          var createModelRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(modelDefinitionFolder);
          var model = await fabricRestApi.CreateItem(workspace.Id, createModelRequest);
          await appLogger.LogSubstep($"Semantic model created");
        
          await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value);
          string reportDefinitionFolder1 = "Power Hour.Report";
          await appLogger.LogStep($"Creating [{reportDefinitionFolder1}]");

          var createReportRequest1 = itemDefinitionFactory.GetCreateItemRequestFromFolder(reportDefinitionFolder1);
          createReportRequest1.Definition = itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReportRequest1.Definition, model.Id.Value);
          await fabricRestApi.CreateItem(workspace.Id, createReportRequest1);
          await appLogger.LogSubstep($"Semantic model created");

          break;

        default:
          throw new ApplicationException($"Add Solution Item command has unknown item name [{ItemName}]");
      }

      await appLogger.LogStep($"Workspace item successfully added");


    }

    public async Task CreateAdoProject(string WorkspaceId) {
      Guid workspaceId = new Guid(WorkspaceId);
      var workspace = await fabricRestApi.GetWorkspace(new Guid(WorkspaceId));
      var workspaceName = workspace.DisplayName;
      await adoProjectManager.CreateProject(workspaceName);
    }
 
    public async Task<List<Workspace>> GetWorkspaces() {
      return (await fabricRestApi.GetWorkspaces()).OrderBy(ws => ws.DisplayName).ToList();
    }

    public async Task<Workspace> GetWorkspace(Guid WorkspaceId) {
      return await fabricRestApi.GetWorkspace(WorkspaceId);
    }

    public async Task<List<WorkspaceRow>> GetWorkspaceRows() {
      return await fabricRestApi.GetWorkspaceRows();
    }

    public async Task DeploySolution(string TargetWorkspace, string SolutionName) {

      if (SolutionName == "DeployAllSolutions") {
        await DeployPowerBiSolution("Custom Power BI Solution");
        await DeployNotebookSolution("Custom Notebook Solution");
        await DeployShortcutSolution("Custom Shortcut Solution");
        await DeployDataPipelineSolution("Custom Data Pipeline Solution");
      }
      else {
        switch (SolutionName) {
          case "PowerBiSolution":
            await DeployPowerBiSolution(TargetWorkspace);
            break;
          case "NotebookSolution":
            await DeployNotebookSolution(TargetWorkspace);
            break;
          case "ShortcutSolution":
            await DeployShortcutSolution(TargetWorkspace);
            break;
          case "DataPipelineSolution":
            await DeployDataPipelineSolution(TargetWorkspace);
            break;
          default:
            await appLogger.LogStep($"Deploy command has unknown solution name [{SolutionName}]");
            break;
        }
      }
    }

    public async Task DeployWithParameters(string TargetWorkspace, string SolutionName, string Customer) {

      if (Customer == "DeployToAllCustomers") {

        foreach (DeploymentPlan customer in SampleCustomerData.AllCustomers.Values) {

          TargetWorkspace = $"Tenant - {customer.Name}";

          switch (SolutionName) {
            case "PowerBiSolution":
              await DeployPowerBiSolutionWithParameters(TargetWorkspace, customer);
              break;
            case "NotebookSolution":
              await DeployNotebookSolutionWithParameters(TargetWorkspace, customer);
              break;
            case "ShortcutSolution":
              await DeployShortcutSolutionWithParameters(TargetWorkspace, customer);
              break;
            case "DataPipelineSolution":
              await DeployDataPipelineSolutionWithParameters(TargetWorkspace, customer);
              break;
            default:
              await appLogger.LogStep($"Deploy To Tenant command has unknown solution name [{SolutionName}]");
              break;
          }

        }
      }
      else {

        DeploymentPlan customer = SampleCustomerData.AllCustomers[Customer];

        switch (SolutionName) {
          case "PowerBiSolution":
            await DeployPowerBiSolutionWithParameters(TargetWorkspace, customer);
            break;
          case "NotebookSolution":
            await DeployNotebookSolutionWithParameters(TargetWorkspace, customer);
            break;
          case "ShortcutSolution":
            await DeployShortcutSolutionWithParameters(TargetWorkspace, customer);
            break;
          case "DataPipelineSolution":
            await DeployDataPipelineSolutionWithParameters(TargetWorkspace, customer);
            break;
          default:
            await appLogger.LogStep($"Deploy To Tenant command has unknown solution name [{SolutionName}]");
            break;
        }
      }

    }

    public async Task DeployFromWorkspace(string SourceWorkspace, string TargetWorkspace, string Customer) {

      if (Customer == "DeployToAllCustomers") {
        foreach (DeploymentPlan currentCustomer in SampleCustomerData.AllCustomers.Values) {
          await DeployFromWorkspace(SourceWorkspace, "Tenant - " + currentCustomer.Name, currentCustomer);
        }
      }
      else {
        DeploymentPlan customer = SampleCustomerData.AllCustomers[Customer];
        await DeployFromWorkspace(SourceWorkspace, TargetWorkspace, customer);
      }
    }

    public async Task UpdateFromWorkspace(string SourceWorkspace, string TargetWorkspace, string Customer, string UpdateType) {

      if (Customer == "UpdateAllCustomerTenants") {
        foreach (DeploymentPlan customer in SampleCustomerData.AllCustomers.Values) {
          TargetWorkspace = $"Tenant - {customer.Name}";
          if (TargetWorkspace != null)
            switch (UpdateType) {
              case "FullUpdate":
                await UpdateFromWorkspace(SourceWorkspace, TargetWorkspace, customer, DeleteOrphanedItems:true);
                break;
              case "ReportsOnly":
                await UpdateReportsFromWorkspace(SourceWorkspace, TargetWorkspace, customer);
                break;
              default:
                await appLogger.LogStep($"Update command has unknown type name [{UpdateType}]");
                break;
            }
        }
      }
      else {

        DeploymentPlan customer = SampleCustomerData.AllCustomers[Customer];

        switch (UpdateType) {
          case "FullUpdate":
            await UpdateFromWorkspace(SourceWorkspace, TargetWorkspace, customer, DeleteOrphanedItems:true);
            break;
          case "ReportsOnly":
            await UpdateReportsFromWorkspace(SourceWorkspace, TargetWorkspace, customer);
            break;
          default:
            await appLogger.LogStep($"Update command has unknown type name [{UpdateType}]");
            break;
        }

      }
    }

    public async Task DeployFromLocalExport(string ExportName, string TargetWorkspace, string Customer) {

      if (Customer == "DeployToAllCustomers") {
        foreach (DeploymentPlan currentCustomer in SampleCustomerData.AllCustomers.Values) {
          await DeployFromLocalExport(ExportName, "Tenant - " + currentCustomer.Name, currentCustomer);
        }
      }
      else {
        DeploymentPlan customer = SampleCustomerData.AllCustomers[Customer];
        await DeployFromLocalExport(ExportName, TargetWorkspace, customer);
      }

    }

    public async Task UpdateFromLocalExport(string ExportName, string TargetWorkspace, string Customer, string UpdateType) {

      if (Customer == "UpdateAllCustomerTenants") {
        foreach (DeploymentPlan customer in SampleCustomerData.AllCustomers.Values) {
          TargetWorkspace = $"Tenant - {customer.Name}";
          if (TargetWorkspace != null)
            switch (UpdateType) {
              case "FullUpdate":
                await UpdateFromLocalExport(ExportName, TargetWorkspace, customer);
                break;
              case "ReportsOnly":
                await UpdateReportsFromLocalExport(ExportName, TargetWorkspace, customer);
                break;
              default:
                await appLogger.LogStep($"Update command has unknown type name [{UpdateType}]");
                break;
            }
        }
      }
      else {

        DeploymentPlan customer = SampleCustomerData.AllCustomers[Customer];

        switch (UpdateType) {
          case "FullUpdate":
            await UpdateFromLocalExport(ExportName, TargetWorkspace, customer);
            break;
          case "ReportsOnly":
            await UpdateReportsFromLocalExport(ExportName, TargetWorkspace, customer);
            break;
          default:
            await appLogger.LogStep($"Update command has unknown type name [{UpdateType}]");
            break;
        }

      }
    }

    public async Task UpdateAllTenantsFromExport(string ExportName, string UpdateType) {

      var export = itemDefinitionFactory.GetSolutionExport(ExportName);
      string solutionName = export.SolutionName;
      var allWorkspaces = await fabricRestApi.GetWorkspaces();

      var targetWorkspaces = allWorkspaces.Where(workspace => !workspace.DisplayName.Contains("Solution"))
                                          .Where(workspace => workspace.Description == solutionName).ToList();

      foreach (var targetWorkspace in targetWorkspaces) {
        string workspaceName = targetWorkspace.DisplayName;
        var customer = SampleCustomerData.AllCustomers.First(customer => customer.Value.TargetWorkspace == workspaceName).Value;

        switch (UpdateType) {
          case "FullUpdate":
            await UpdateFromLocalExport(ExportName, workspaceName, customer, DeleteOrphanedItems: true);
            break;
          case "ReportsOnly":
            await UpdateReportsFromLocalExport(ExportName, workspaceName, customer);
            break;
          default:
            await appLogger.LogStep($"Update command has unknown type name [{UpdateType}]");
            break;
        }

      }

    }

    public async Task ExportFromWorkspaceToAdo(string WorkspaceId, string ExportName, string Comment) {
      var workspace = await fabricRestApi.GetWorkspace(new Guid(WorkspaceId));
      string workspaceName = workspace.DisplayName;
      string projectName = workspaceName;
      await ExportFromWorkspaceToAdo(workspaceName, projectName, ExportName, Comment);
    }

    public async Task DeployFromAdoExport(string WorkspaceId, string ExportName, string TargetWorkspace, string Customer) {

      Guid workspaceId = new Guid(WorkspaceId);
      var workspace = await fabricRestApi.GetWorkspace(workspaceId);
      string workspaceName = workspace.DisplayName;

      if (Customer == "DeployToAllCustomers") {
        foreach (DeploymentPlan currentCustomer in SampleCustomerData.AllCustomers.Values) {
          await DeployFromAdoExport(workspaceName, ExportName, currentCustomer.TargetWorkspace, currentCustomer);
        }
      }
      else {
        DeploymentPlan customer = SampleCustomerData.AllCustomers[Customer];
        await DeployFromAdoExport(workspaceName, ExportName, TargetWorkspace, customer);
      }

    }

    public async Task UpdateFromAdoExport(string WorkspaceId, string ExportName, string TargetWorkspace, string Customer, string UpdateType) {

      var solutionWorkspace = await fabricRestApi.GetWorkspace(new Guid(WorkspaceId));
      string solutionName = solutionWorkspace.DisplayName;
      string adoProjectName = solutionName; 


      if (TargetWorkspace == "UpdateAllCustomerTenants") {

        var workspaces = await GetWorkspaces();
        var targetWorkspaces = workspaces.Where(workspace => !workspace.DisplayName.Contains("Custom"))
                                         .Where(workspace => workspace.Description == solutionName).ToList();

        foreach (var targetWorkspace in targetWorkspaces) {
          string targetWorkspaceName = targetWorkspace.DisplayName;
          var customer = SampleCustomerData.AllCustomers.Where(customer => customer.Value.TargetWorkspace == targetWorkspaceName).First().Value;
          switch (UpdateType) {
            case "FullUpdate":
              await UpdateFromAdoExport(solutionName, ExportName, customer.TargetWorkspace, customer, DeleteOrphanedItems:true);
              break;
            case "ReportsOnly":
              await UpdateReportsFromAdoExport(solutionName, ExportName, customer.TargetWorkspace, customer, DeleteOrphanedItems: true);
              break;
            default:
              await appLogger.LogStep($"Update command has unknown type name [{UpdateType}]");
              break;
          }
        }               
      }
      else {

        DeploymentPlan customer = SampleCustomerData.AllCustomers.Where(customer => customer.Value.Name == Customer).First().Value;

        switch (UpdateType) {
          case "FullUpdate":
            await UpdateFromAdoExport(solutionName, ExportName, TargetWorkspace, customer, DeleteOrphanedItems: true);
            break;
          case "ReportsOnly":
            await UpdateReportsFromAdoExport(solutionName, ExportName, TargetWorkspace, customer, DeleteOrphanedItems: true);
            break;
          default:
            await appLogger.LogStep($"Update command has unknown type name [{UpdateType}]");
            break;
        }

      }
    }

    // method implementations

    public async Task CreateAndBindSemanticModelConnecton(Workspace Workspace, Guid SemanticModelId, Item Lakehouse = null) {

      var datasources = (await powerBiRestApi.GetDatasourcesForSemanticModel(Workspace.Id, SemanticModelId)).Value;

      foreach (var datasource in datasources) {

        if (datasource.DatasourceType.ToLower() == "sql") {

          string sqlEndPointServer = datasource.ConnectionDetails.Server;
          string sqlEndPointDatabase = datasource.ConnectionDetails.Database;

          // you cannot create the connection until your configure a service principal
          await appLogger.LogSubstep($"Creating connection for semantic model");
          var sqlConnection = await fabricRestApi.CreateSqlConnectionWithServicePrincipal(sqlEndPointServer, sqlEndPointDatabase, Workspace, Lakehouse);
          await appLogger.LogSubstep($"Binding connection to semantic model");
          await powerBiRestApi.BindSemanticModelToConnection(Workspace.Id, SemanticModelId, sqlConnection.Id);
        }

        if (datasource.DatasourceType.ToLower() == "web") {
          string url = datasource.ConnectionDetails.Url;

          await appLogger.LogSubstep($"Creating Web connection for semantic model");
          var webConnection = await fabricRestApi.CreateAnonymousWebConnection(url, Workspace);

          await appLogger.LogSubstep($"Binding connection to semantic model");
          await powerBiRestApi.BindSemanticModelToConnection(Workspace.Id, SemanticModelId, webConnection.Id);

          await appLogger.LogSubOperationStart($"Refreshing semantic model");
          await powerBiRestApi.RefreshDataset(Workspace.Id, SemanticModelId);
          await appLogger.LogOperationComplete();
        }

      }
    }

    public async Task<Workspace> DeployPowerBiSolution(string TargetWorkspace) {

      await appLogger.LogSolution($"Deploying Power BI Solution to [{TargetWorkspace}]");

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace);
      await appLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Power BI Solution");

      string semanticModelName = "Product Sales Imported Model";
      string reportName = "Product Sales Summary";

      await appLogger.LogStep($"Creating [{semanticModelName}.SemanticModel]");
      var saleModelCreateRequest = itemDefinitionFactory.GetSemanticModelCreateRequestFromBim(semanticModelName, "sales_model_import.bim");
      var model = await fabricRestApi.CreateItem(workspace.Id, saleModelCreateRequest);

      await appLogger.LogSubstep($"Semantic model created with Id of {model.Id.Value.ToString()}");
      //await RecordOperationActvity(Operation, "CreateSemanticModel", $"Created semantic model named {semanticModelName}");

      await appLogger.LogSubstep($"Creating Web connection for semantic model");
      var url = await powerBiRestApi.GetWebDatasourceUrl(workspace.Id, model.Id.Value);
      var connection = await fabricRestApi.CreateAnonymousWebConnection(url, workspace);

      //await RecordOperationActvity(Operation, "CreateConnection", $"Created anonymous connection semantic for model");

      await appLogger.LogSubstep($"Binding connection to semantic model");
      await powerBiRestApi.BindSemanticModelToConnection(workspace.Id, model.Id.Value, connection.Id);
      //await RecordOperationActvity(Operation, "BindConnection", $"Bound connection to semantic model");

      await appLogger.LogSubOperationStart($"Refreshing semantic model");
      await powerBiRestApi.RefreshDataset(workspace.Id, model.Id.Value);
      await appLogger.LogOperationComplete();

      await appLogger.LogStep($"Creating [{reportName}.Report]");

      var createRequestReport =
        itemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, reportName, "product_sales_summary.json");

      var report = await fabricRestApi.CreateItem(workspace.Id, createRequestReport);

      await appLogger.LogSubstep($"New report created with Id of [{report.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return workspace;

    }

    public async Task<Workspace> DeployNotebookSolution(string TargetWorkspace) {

      await appLogger.LogSolution($"Deploying Lakehouse Solution to [{TargetWorkspace}]");

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace);
      await appLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Lakehouse Solution");

      string lakehouseName = "sales";
      string semanticModelName = "Product Sales DirectLake Model";
      string reportName = "Product Sales Summary";

      // create connection to track Web Url for redirects
      string defaultWebUrl = "https://fabricdevcamp.blob.core.windows.net/sampledata/ProductSales/Dev";
      var connection = await fabricRestApi.CreateAnonymousWebConnection(defaultWebUrl, workspace);

      await appLogger.LogStep($"Creating [{lakehouseName}.Lakehouse]");
      var lakehouse = await fabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
      await appLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

      // create and run notebook to build bronze layer
      string notebook1Name = "Create Lakehouse Tables";
      await appLogger.LogStep($"Creating [{notebook1Name}.Notebook]");
      var notebook1CreateRequest = itemDefinitionFactory.GetCreateNotebookRequestFromPy(workspace.Id, lakehouse, notebook1Name, "CreateLakehouseTables.py");
      var notebook1 = await fabricRestApi.CreateItem(workspace.Id, notebook1CreateRequest);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook1.Id.Value.ToString()}]");
      await appLogger.LogSubOperationStart($"Running notebook");
      await fabricRestApi.RunNotebook(workspace.Id, notebook1);
      await appLogger.LogOperationComplete();

      await appLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
      var sqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
      await appLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
      await appLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

      await fabricRestApi.RefreshLakehouseTableSchema(sqlEndpoint.Id);

      await appLogger.LogStep($"Creating [{semanticModelName}.SemanticModel]");
      var modelCreateRequest =
       itemDefinitionFactory.GetSemanticDirectLakeModelCreateRequestFromBim(semanticModelName, "sales_model_DirectLake.bim", sqlEndpoint.ConnectionString, sqlEndpoint.Id);

      var model = await fabricRestApi.CreateItem(workspace.Id, modelCreateRequest);

      await appLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

      await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value, lakehouse);

      await appLogger.LogStep($"Creating [{reportName}.Report]");

      var createRequestReport =
        itemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, reportName, "product_sales_summary.json");

      var report = await fabricRestApi.CreateItem(workspace.Id, createRequestReport);
      await appLogger.LogSubstep($"Report created with Id of [{report.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return workspace;

    }

    public async Task<Workspace> DeployShortcutSolution(string TargetWorkspace) {

      string lakehouseName = "sales";
      string semanticModelName = "Product Sales DirectLake Model";
      string report1Name = "Product Sales Summary";
      string report2Name = "Product Sales Time Intelligence";

      await appLogger.LogSolution("Deploy Lakehouse Solution with Shortcut");

      await appLogger.LogStep($"Creating new workspace [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace, fabricCapacityId);
      await appLogger.LogSubstep($"Workspace created with Id of [{workspace.Id.ToString()}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Shortcut Solution");

      await appLogger.LogStep($"Creating [{lakehouseName}.Lakehouse]");
      var lakehouse = await fabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
      await appLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

      await appLogger.LogStep("Creating ADLS connection for shortcut");
      await appLogger.LogSubstep($"Location: {azureStorageServer}/{azureStoragePath}");

      var connection = await fabricRestApi.CreateAzureStorageConnectionWithAccountKey(azureStorageServer,
                                                                                      azureStoragePath,
                                                                                      workspace);

      await appLogger.LogSubstep($"Connection created with Id of {connection.Id}");

      // get data required to create shortcut
      string name = "sales-data";
      string path = "Files";
      Uri location = new Uri(azureStorageServer);
      string shortcutSubpath = azureStoragePath;

      await appLogger.LogStep($"Creating [{lakehouseName}.Lakehouse.Shortcut] with path [{path}/{name}]");
      var shortcut = await fabricRestApi.CreateAdlsGen2Shortcut(workspace.Id, lakehouse.Id.Value, name, path, location, shortcutSubpath, connection.Id);
      await appLogger.LogSubstep($"Shortcut created");

      // create and run notebook to build silver layer
      string notebook1Name = "Create 01 Silver Layer";
      await appLogger.LogStep($"Creating [{notebook1Name}.Notebook]");
      var notebook1CreateRequest = itemDefinitionFactory.GetCreateNotebookRequestFromPy(workspace.Id, lakehouse, notebook1Name, "BuildSilverLayer.py");
      var notebook1 = await fabricRestApi.CreateItem(workspace.Id, notebook1CreateRequest);

      await appLogger.LogSubstep($"Notebook created with Id of [{notebook1.Id.Value.ToString()}]");
      await appLogger.LogSubOperationStart($"Running notebook");
      await fabricRestApi.RunNotebook(workspace.Id, notebook1);
      await appLogger.LogOperationComplete();

      // create and run notebook to build gold layer
      string notebook2Name = "Create 02 Gold Layer";
      await appLogger.LogStep($"Creating [{notebook2Name}.Notebook]");
      var notebook2CreateRequest = itemDefinitionFactory.GetCreateNotebookRequestFromPy(workspace.Id, lakehouse, notebook2Name, "BuildGoldLayer.py");
      var notebook2 = await fabricRestApi.CreateItem(workspace.Id, notebook2CreateRequest);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook2.Id.Value.ToString()}]");
      await appLogger.LogSubOperationStart($"Running notebook");
      await fabricRestApi.RunNotebook(workspace.Id, notebook2);
      await appLogger.LogOperationComplete();

      await appLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
      var sqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
      await appLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
      await appLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

      await appLogger.LogSubstep("Refreshing lakehouse table schema");
      await fabricRestApi.RefreshLakehouseTableSchema(sqlEndpoint.Id);

      await appLogger.LogStep($"Creating [{semanticModelName}.SemanticModel]");
      var modelCreateRequest = itemDefinitionFactory.GetSemanticDirectLakeModelCreateRequestFromBim(semanticModelName,
                                                                                                    "sales_model_DirectLake.bim",
                                                                                                    sqlEndpoint.ConnectionString,
                                                                                                    sqlEndpoint.Id);

      var model = await fabricRestApi.CreateItem(workspace.Id, modelCreateRequest);
      await appLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

      await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value, lakehouse);

      await appLogger.LogStep($"Creating [{report1Name}.Report]");
      var createRequestReport1 =
        itemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, report1Name, "product_sales_summary.json");

      var report1 = await fabricRestApi.CreateItem(workspace.Id, createRequestReport1);
      await appLogger.LogSubstep($"Report created with Id of [{report1.Id.Value.ToString()}]");

      await appLogger.LogStep($"Creating [{report2Name}.Report]");
      var createRequestReport2 =
        itemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, report2Name, "product_sales_time_intelligence.json");

      var report2 = await fabricRestApi.CreateItem(workspace.Id, createRequestReport2);
      await appLogger.LogSubstep($"Report created with Id of [{report2.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return workspace;

    }

    public async Task<Workspace> DeployDataPipelineSolution(string TargetWorkspace) {

      string lakehouseName = "sales";
      string semanticModelName = "Product Sales DirectLake Model";
      string report1Name = "Product Sales Summary";
      string report2Name = "Product Sales Time Intelligence";
      string report3Name = "Product Sales Top 10 Cities";

      await appLogger.LogSolution("Deploy Lakehouse Solution with Data Pipeline");

      await appLogger.LogStep($"Creating new workspace [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace, fabricCapacityId);
      await appLogger.LogSubstep($"Workspace created with Id of [{workspace.Id.ToString()}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Data Pipeline Solution");

      await appLogger.LogStep($"Creating [{lakehouseName}.Lakehouse]");
      var lakehouse = await fabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
      await appLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

      // create and run notebook to build silver layer
      string notebook1Name = "Build 01 Silver Layer";
      await appLogger.LogStep($"Creating [{notebook1Name}.Notebook]");
      var notebook1CreateRequest = itemDefinitionFactory.GetCreateNotebookRequestFromPy(workspace.Id, lakehouse, notebook1Name, "BuildSilverLayer.py");
      var notebook1 = await fabricRestApi.CreateItem(workspace.Id, notebook1CreateRequest);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook1.Id.Value.ToString()}]");

      // create and run notebook to build gold layer
      string notebook2Name = "Build 02 Gold Layer";
      await appLogger.LogStep($"Creating [{notebook2Name}.Notebook]");
      var notebook2CreateRequest = itemDefinitionFactory.GetCreateNotebookRequestFromPy(workspace.Id, lakehouse, notebook2Name, "BuildGoldLayer.py");
      var notebook2 = await fabricRestApi.CreateItem(workspace.Id, notebook2CreateRequest);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook2.Id.Value.ToString()}]");

      await appLogger.LogStep("Creating ADLS connection for data pipeline");
      await appLogger.LogSubstep($"Location: {azureStorageServer}/{azureStoragePath}");

      var connection = await fabricRestApi.CreateAzureStorageConnectionWithAccountKey(azureStorageServer,
                                                                                      azureStoragePath,
                                                                                      workspace);

      await appLogger.LogSubstep($"Connection created with Id of {connection.Id}");

      string pipelineName = "Create Lakehouse Tables";
      await appLogger.LogStep($"Creating [{pipelineName}.DataPipline]");

      string pipelineDefinitionTemplate = itemDefinitionFactory.GetTemplateFile(@"DataPipelines\CreateLakehouseTables.json");
      string pipelineDefinition = pipelineDefinitionTemplate.Replace("{WORKSPACE_ID}", workspace.Id.ToString())
                                                            .Replace("{LAKEHOUSE_ID}", lakehouse.Id.Value.ToString())
                                                            .Replace("{CONNECTION_ID}", connection.Id.ToString())
                                                            .Replace("{CONTAINER_NAME}", azureStorageContainerName)
                                                            .Replace("{CONTAINER_PATH}", azureStorageContainerPath)
                                                            .Replace("{NOTEBOOK_ID_BUILD_SILVER}", notebook1.Id.Value.ToString())
                                                            .Replace("{NOTEBOOK_ID_BUILD_GOLD}", notebook2.Id.Value.ToString());

      var pipelineCreateRequest = itemDefinitionFactory.GetDataPipelineCreateRequest(pipelineName, pipelineDefinition);
      var pipeline = await fabricRestApi.CreateItem(workspace.Id, pipelineCreateRequest);

      await appLogger.LogSubstep($"DataPipline created with Id [{pipeline.Id.Value.ToString()}]");

      await appLogger.LogSubOperationStart($"Running data pipeline");
      await fabricRestApi.RunDataPipeline(workspace.Id, pipeline);
      await appLogger.LogOperationComplete();

      await appLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
      var sqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
      await appLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
      await appLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

      await appLogger.LogSubstep("Refreshing lakehouse table schema");
      await fabricRestApi.RefreshLakehouseTableSchema(sqlEndpoint.Id);

      await appLogger.LogStep($"Creating [{semanticModelName}.SemanticModel]");
      var modelCreateRequest = itemDefinitionFactory.GetSemanticDirectLakeModelCreateRequestFromBim(semanticModelName,
                                                                                                    "sales_model_DirectLake.bim",
                                                                                                    sqlEndpoint.ConnectionString,
                                                                                                    sqlEndpoint.Id);

      var model = await fabricRestApi.CreateItem(workspace.Id, modelCreateRequest);
      await appLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

      await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value, lakehouse);

      await appLogger.LogStep($"Creating [{report1Name}.Report]");
      var createRequestReport1 =
        itemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, report1Name, "product_sales_summary.json");

      var report1 = await fabricRestApi.CreateItem(workspace.Id, createRequestReport1);
      await appLogger.LogSubstep($"Report created with Id of [{report1.Id.Value.ToString()}]");

      await appLogger.LogStep($"Creating [{report2Name}.Report]");
      var createRequestReport2 =
        itemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, report2Name, "product_sales_time_intelligence.json");

      var report2 = await fabricRestApi.CreateItem(workspace.Id, createRequestReport2);
      await appLogger.LogSubstep($"Report created with Id of [{report2.Id.Value.ToString()}]");

      await appLogger.LogStep($"Creating [{report3Name}.Report]");
      var createRequestReport3 =
        itemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, report3Name, "product_sales_top_ten_cities_report.json");

      var report3 = await fabricRestApi.CreateItem(workspace.Id, createRequestReport3);
      await appLogger.LogSubstep($"Report created with Id of [{report3.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return workspace;

    }

    public ItemDefinition CustomizeReportTitle(CreateItemRequest CreateRequest, DeploymentPlan Deployment) {
      return CustomizeReportTitle(CreateRequest.Definition, CreateRequest.DisplayName, Deployment);
    }

    public ItemDefinition CustomizeReportTitle(ItemDefinition ReportDefinition, string ReportDisplayName, DeploymentPlan Deployment) {

      // don't customize report titles for staged deployments
      if (Deployment.DeploymentType == DeploymentPlanType.CustomerTenantDeployment) {

        var reportTitleRedirect = new Dictionary<string, string>();
        reportTitleRedirect.Add(ReportDisplayName, $"{Deployment.Name} {ReportDisplayName}");

        return itemDefinitionFactory.UpdateItemDefinitionPart(ReportDefinition,
                                                              "report.json",
                                                              reportTitleRedirect);
      }
      else {
        return ReportDefinition;
      }

    }

    public async Task DisplayDeploymentParameters(DeploymentPlan Deployment) {
      if ((Deployment.Parameters != null) &&
        (Deployment.Parameters.Count > 0)) {
        await appLogger.LogTableHeader("Loading parameters from deployment plan");
        foreach (var parameter in Deployment.Parameters) {
          await appLogger.LogTableRow(parameter.Key, parameter.Value);
        }
      }
    }

    public async Task<Workspace> DeployPowerBiSolutionWithParameters(string TargetWorkspace, DeploymentPlan Deployment) {

      await appLogger.LogSolution("Deploy Power BI Solution with Deployment Parameters");

      await DisplayDeploymentParameters(Deployment);

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace);
      await appLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Power BI Solution");

      string modelDefinitionFolder = "Product Sales Imported Model.SemanticModel";
      var createModelRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(modelDefinitionFolder);
      await appLogger.LogStep($"Creating [{createModelRequest.DisplayName}.SemanticModel]");

      if (Deployment.Parameters.ContainsKey(DeploymentPlan.webDatasourcePathParameter)) {
        var webUrl = Deployment.Parameters[DeploymentPlan.webDatasourcePathParameter];
        var semanticModelRedirects = new Dictionary<string, string>() {
          {"{WEB_DATASOURCE_PATH}", webUrl }
        };

        createModelRequest.Definition =
          itemDefinitionFactory.UpdateItemDefinitionPart(createModelRequest.Definition,
                                                         "definition/expressions.tmdl",
                                                         semanticModelRedirects);
      }

      var model = await fabricRestApi.CreateItem(workspace.Id, createModelRequest);
      await appLogger.LogSubstep($"New semantic model created with Id of [{model.Id.Value.ToString()}]");

      await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value);

      string reportDefinitionFolder = "Product Sales Summary.Report";
      var createReportRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(reportDefinitionFolder);
      await appLogger.LogStep($"Creating [{createReportRequest.DisplayName}.Report]");

      await appLogger.LogSubstep("Customizing report title header with customer name");
      createReportRequest.Definition = CustomizeReportTitle(createReportRequest, Deployment);

      createReportRequest.Definition = itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReportRequest.Definition, model.Id.Value);

      var report = await fabricRestApi.CreateItem(workspace.Id, createReportRequest);
      await appLogger.LogSubstep($"Report created with Id of [{report.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return workspace;

    }

    public async Task<Workspace> DeployNotebookSolutionWithParameters(string TargetWorkspace, DeploymentPlan Deployment) {

      string lakehouseName = "sales";

      await appLogger.LogSolution("Deploy Notebook Solution with Deployment Parameters");

      await DisplayDeploymentParameters(Deployment);
      var parameters = Deployment.Parameters;

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace);
      await appLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Notebook Solution");

      // create connection to track Web Url for redirects
      string defaultWebUrl = "https://fabricdevcamp.blob.core.windows.net/sampledata/ProductSales/Dev";
      var connection = await fabricRestApi.CreateAnonymousWebConnection(defaultWebUrl, workspace);

      await appLogger.LogStep($"Creating [{lakehouseName}.Lakehouse]");
      var lakehouse = await fabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
      await appLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

      // create and run notebook to build bronze layer
      string notebook1Name = "Create Lakehouse Tables";
      await appLogger.LogStep($"Creating [{notebook1Name}.Notebook]");

      var notebookCreateRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder("Create Lakehouse Tables.Notebook");

      var notebookRedirects = new Dictionary<string, string>() {
        {"{WORKSPACE_ID}", workspace.Id.ToString()},
        {"{LAKEHOUSE_ID}", lakehouse.Id.Value.ToString() },
        {"{LAKEHOUSE_NAME}", lakehouse.DisplayName }
      };

      if (Deployment.Parameters.ContainsKey(DeploymentPlan.webDatasourcePathParameter)) {

        await appLogger.LogSubstep($"Updating Web URL in notebook definition");
        var webUrl = Deployment.Parameters[DeploymentPlan.webDatasourcePathParameter];
        notebookRedirects.Add("{WEB_DATASOURCE_PATH}", webUrl);

        notebookCreateRequest.Definition =
          itemDefinitionFactory.UpdateItemDefinitionPart(notebookCreateRequest.Definition,
                                                         "notebook-content.py",
                                                         notebookRedirects);
      }

      var notebook = await fabricRestApi.CreateItem(workspace.Id, notebookCreateRequest);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook.Id.Value.ToString()}]");

      await appLogger.LogSubOperationStart($"Running notebook");
      await fabricRestApi.RunNotebook(workspace.Id, notebook);
      await appLogger.LogOperationComplete();

      await appLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
      var sqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
      await appLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
      await appLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

      await fabricRestApi.RefreshLakehouseTableSchema(sqlEndpoint.Id);

      string modelDefinitionFolder = "Product Sales DirectLake Model.SemanticModel";
      var createModelRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(modelDefinitionFolder);
      await appLogger.LogStep($"Creating [{createModelRequest.DisplayName}.SemanticModel]");

      var semanticModelRedirects = new Dictionary<string, string>() {
        {"{SQL_ENDPOINT_SERVER}", sqlEndpoint.ConnectionString },
        {"{SQL_ENDPOINT_DATABASE}", sqlEndpoint.Id },
      };

      createModelRequest.Definition =
        itemDefinitionFactory.UpdateItemDefinitionPart(createModelRequest.Definition,
                                                       "definition/expressions.tmdl",
                                                       semanticModelRedirects);

      var model = await fabricRestApi.CreateItem(workspace.Id, createModelRequest);
      await appLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

      await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value, lakehouse);

      string reportDefinitionFolder = "Product Sales Summary.Report";
      var createReportRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(reportDefinitionFolder);
      await appLogger.LogStep($"Creating [{createReportRequest.DisplayName}.Report]");

      createReportRequest.Definition = itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReportRequest.Definition, model.Id.Value);

      await appLogger.LogSubstep("Customizing report title header with customer name");
      createReportRequest.Definition = CustomizeReportTitle(createReportRequest, Deployment);

      var report = await fabricRestApi.CreateItem(workspace.Id, createReportRequest);
      await appLogger.LogSubstep($"Report created with Id of [{report.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return workspace;

    }

    public async Task<Workspace> DeployShortcutSolutionWithParameters(string TargetWorkspace, DeploymentPlan Deployment) {

      string lakehouseName = "sales";

      await appLogger.LogSolution("Deploy Shortcut Solution with Deployment Parameters");

      await DisplayDeploymentParameters(Deployment);

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace);
      await appLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Shortcut Solution");

      await appLogger.LogStep($"Creating [{lakehouseName}.Lakehouse]");
      var lakehouse = await fabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
      await appLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

      string adlsServer = azureStorageServer;
      string adlsPath = azureStoragePath;

      if ((Deployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
          (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
          (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

        string adlsContainerName = Deployment.Parameters[DeploymentPlan.adlsContainerNameParameter];
        string adlsContainerPath = Deployment.Parameters[DeploymentPlan.adlsContainerPathParameter];
        adlsServer = Deployment.Parameters[DeploymentPlan.adlsServerPathParameter];

        adlsPath = $@"/{adlsContainerName}{adlsContainerPath}";

      }

      await appLogger.LogStep($"Creating ADLS connection");
      await appLogger.LogSubstep($"Path: {adlsServer}/{adlsPath}");
      var connection = await fabricRestApi.CreateAzureStorageConnectionWithAccountKey(adlsServer,
                                                                                      adlsPath,
                                                                                      workspace);

      await appLogger.LogSubstep($"Connection created with Id of [{connection.Id}]");

      // get data required to create shortcut
      string name = "sales-data";
      string path = "Files";
      Uri location = new Uri(adlsServer);
      string shortcutSubpath = adlsPath;

      await appLogger.LogStep("Creating OneLake Shortcut with ADLS connection to provide access to bonze layer data files");
      var shortcut = await fabricRestApi.CreateAdlsGen2Shortcut(workspace.Id, lakehouse.Id.Value, name, path, location, shortcutSubpath, connection.Id);
      await appLogger.LogSubstep($"Shortcut created successfully");

      // set up redirect for all notebooks
      var notebookRedirects = new Dictionary<string, string>() {
        {"{WORKSPACE_ID}", workspace.Id.ToString()},
        {"{LAKEHOUSE_ID}", lakehouse.Id.Value.ToString() },
        {"{LAKEHOUSE_NAME}", lakehouse.DisplayName }
      };

      // create notebook to build silver layer
      string notebook1DefinitionFolder = "Create 01 Silver Layer.Notebook";
      var createNotebook1Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(notebook1DefinitionFolder);
      createNotebook1Request.Definition =
          itemDefinitionFactory.UpdateItemDefinitionPart(createNotebook1Request.Definition,
                                                         "notebook-content.py",
                                                         notebookRedirects);

      await appLogger.LogStep($"Creating [{createNotebook1Request.DisplayName}.Notebook]");
      var notebook1 = await fabricRestApi.CreateItem(workspace.Id, createNotebook1Request);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook1.Id.Value.ToString()}]");

      await appLogger.LogSubOperationStart($"Running notebook");
      await fabricRestApi.RunNotebook(workspace.Id, notebook1);
      await appLogger.LogOperationComplete();

      // create notebook to build gold layer
      string notebook2DefinitionFolder = "Create 02 Gold Layer.Notebook";
      var createNotebook2Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(notebook2DefinitionFolder);
      createNotebook2Request.Definition =
          itemDefinitionFactory.UpdateItemDefinitionPart(createNotebook2Request.Definition,
                                                         "notebook-content.py",
                                                         notebookRedirects);

      await appLogger.LogStep($"Creating [{createNotebook2Request.DisplayName}.Notebook]");
      var notebook2 = await fabricRestApi.CreateItem(workspace.Id, createNotebook2Request);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook2.Id.Value.ToString()}]");

      await appLogger.LogSubOperationStart($"Running notebook");
      await fabricRestApi.RunNotebook(workspace.Id, notebook2);
      await appLogger.LogOperationComplete();

      await appLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
      var sqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
      await appLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
      await appLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

      await appLogger.LogSubstep("Refreshing lakehouse table schema");
      await fabricRestApi.RefreshLakehouseTableSchema(sqlEndpoint.Id);

      string modelDefinitionFolder = "Product Sales DirectLake Model.SemanticModel";
      var createModelRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(modelDefinitionFolder);
      await appLogger.LogStep($"Creating [{createModelRequest.DisplayName}.SemanticModel]");

      var semanticModelRedirects = new Dictionary<string, string>() {
        {"{SQL_ENDPOINT_SERVER}", sqlEndpoint.ConnectionString },
        {"{SQL_ENDPOINT_DATABASE}", sqlEndpoint.Id },
      };

      createModelRequest.Definition =
        itemDefinitionFactory.UpdateItemDefinitionPart(createModelRequest.Definition,
                                                       "definition/expressions.tmdl",
                                                       semanticModelRedirects);

      var model = await fabricRestApi.CreateItem(workspace.Id, createModelRequest);
      await appLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

      await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value, lakehouse);

      string report1DefinitionFolder = "Product Sales Summary.Report";
      var createReport1Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(report1DefinitionFolder);
      await appLogger.LogStep($"Creating [{createReport1Request.DisplayName}.Report]");
      createReport1Request.Definition =
        itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReport1Request.Definition, model.Id.Value);
      await appLogger.LogSubstep("Customizing report title header with customer name");
      createReport1Request.Definition = CustomizeReportTitle(createReport1Request, Deployment);
      var report1 = await fabricRestApi.CreateItem(workspace.Id, createReport1Request);
      await appLogger.LogSubstep($"Report created with Id of [{report1.Id.Value.ToString()}]");

      string report2DefinitionFolder = "Product Sales Time Intelligence.Report";
      var createReport2Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(report2DefinitionFolder);
      await appLogger.LogStep($"Creating [{createReport2Request.DisplayName}.Report]");
      createReport2Request.Definition =
        itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReport2Request.Definition, model.Id.Value);
      await appLogger.LogSubstep("Customizing report title header with customer name");
      createReport2Request.Definition = CustomizeReportTitle(createReport2Request, Deployment);
      var report2 = await fabricRestApi.CreateItem(workspace.Id, createReport2Request);
      await appLogger.LogSubstep($"Report created with Id of [{report2.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return workspace;

    }

    public async Task<Workspace> DeployDataPipelineSolutionWithParameters(string TargetWorkspace, DeploymentPlan Deployment) {

      string lakehouseName = "sales";

      await appLogger.LogSolution("Deploy Data Pipeline Solution with Deployment Parameters");

      await DisplayDeploymentParameters(Deployment);

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspace}]");
      var workspace = await fabricRestApi.CreateWorkspace(TargetWorkspace);
      await appLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

      await fabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Data Pipeline Solution");

      await appLogger.LogStep($"Creating [{lakehouseName}.Lakehouse]");
      var lakehouse = await fabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
      await appLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

      // set up redirect for all notebooks
      var notebookRedirects = new Dictionary<string, string>() {
        {"{WORKSPACE_ID}", workspace.Id.ToString()},
        {"{LAKEHOUSE_ID}", lakehouse.Id.Value.ToString() },
        {"{LAKEHOUSE_NAME}", lakehouse.DisplayName }
      };

      // create notebook to build silver layer
      string notebook1DefinitionFolder = "Build 01 Silver Layer.Notebook";
      var createNotebook1Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(notebook1DefinitionFolder);
      createNotebook1Request.Definition =
          itemDefinitionFactory.UpdateItemDefinitionPart(createNotebook1Request.Definition,
                                                         "notebook-content.py",
                                                         notebookRedirects);

      await appLogger.LogStep($"Creating [{createNotebook1Request.DisplayName}.Notebook]");
      var notebook1 = await fabricRestApi.CreateItem(workspace.Id, createNotebook1Request);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook1.Id.Value.ToString()}]");

      // create notebook to build gold layer
      string notebook2DefinitionFolder = "Build 02 Gold Layer.Notebook";
      var createNotebook2Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(notebook2DefinitionFolder);
      createNotebook2Request.Definition =
          itemDefinitionFactory.UpdateItemDefinitionPart(createNotebook2Request.Definition,
                                                         "notebook-content.py",
                                                         notebookRedirects);

      await appLogger.LogStep($"Creating [{createNotebook2Request.DisplayName}.Notebook]");
      var notebook2 = await fabricRestApi.CreateItem(workspace.Id, createNotebook2Request);
      await appLogger.LogSubstep($"Notebook created with Id of [{notebook2.Id.Value.ToString()}]");

      // create connection for data pipeline
      string adlsServer = azureStorageServer;
      string adlsPath = azureStoragePath;
      string adlsContainerName = azureStorageContainerName;
      string adlsContainerPath = azureStorageContainerPath;

      if ((Deployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
          (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
          (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

        adlsContainerName = Deployment.Parameters[DeploymentPlan.adlsContainerNameParameter];
        adlsContainerPath = Deployment.Parameters[DeploymentPlan.adlsContainerPathParameter];

        adlsServer = Deployment.Parameters[DeploymentPlan.adlsServerPathParameter];
        adlsPath = adlsContainerName + adlsContainerPath;

      }

      await appLogger.LogStep($"Creating ADLS connection");
      await appLogger.LogSubstep($"Path: {adlsServer}/{adlsPath}");

      var connection = await fabricRestApi.CreateAzureStorageConnectionWithAccountKey(adlsServer,
                                                                                adlsPath,
                                                                                workspace);

      await appLogger.LogSubstep($"Connection created with Id of {connection.Id}");

      string pipelineDefinitionFolder = "Create Lakehouse Tables.DataPipeline";
      var createPipelineRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(pipelineDefinitionFolder);
      await appLogger.LogStep($"Creating [{createPipelineRequest.DisplayName}.DataPipeline]");

      // set up data pipeline redirects
      var pipelineRedirects = new Dictionary<string, string>() {
      { "{WORKSPACE_ID}", workspace.Id.ToString() },
      { "{LAKEHOUSE_ID}", lakehouse.Id.Value.ToString() },
      { "{CONNECTION_ID}", connection.Id.ToString() },
      { "{CONTAINER_NAME}", adlsContainerName},
      { "{CONTAINER_PATH}", adlsContainerPath },
      { "{NOTEBOOK_ID_BUILD_SILVER}", notebook1.Id.Value.ToString() },
      { "{NOTEBOOK_ID_BUILD_GOLD}", notebook2.Id.Value.ToString() },
    };

      await appLogger.LogSubstep("Updating ADLS connection data in data pipeline definition");
      createPipelineRequest.Definition =
          itemDefinitionFactory.UpdateItemDefinitionPart(createPipelineRequest.Definition,
                                                         "pipeline-content.json",
                                                         pipelineRedirects);

      var pipeline = await fabricRestApi.CreateItem(workspace.Id, createPipelineRequest);
      await appLogger.LogSubstep($"Data Pipeline created with Id of [{pipeline.Id.Value.ToString()}]");

      await appLogger.LogSubOperationStart($"Running data pipeline");
      await fabricRestApi.RunDataPipeline(workspace.Id, pipeline);
      await appLogger.LogOperationComplete();

      await appLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
      var sqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
      await appLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
      await appLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

      await appLogger.LogSubstep("Refreshing lakehouse table schema");
      await fabricRestApi.RefreshLakehouseTableSchema(sqlEndpoint.Id);

      string modelDefinitionFolder = "Product Sales DirectLake Model.SemanticModel";
      var createModelRequest = itemDefinitionFactory.GetCreateItemRequestFromFolder(modelDefinitionFolder);
      await appLogger.LogStep($"Creating [{createModelRequest.DisplayName}.SemanticModel]");

      var semanticModelRedirects = new Dictionary<string, string>() {
        {"{SQL_ENDPOINT_SERVER}", sqlEndpoint.ConnectionString },
        {"{SQL_ENDPOINT_DATABASE}", sqlEndpoint.Id },
      };

      createModelRequest.Definition =
        itemDefinitionFactory.UpdateItemDefinitionPart(createModelRequest.Definition,
                                                       "definition/expressions.tmdl",
                                                       semanticModelRedirects);

      var model = await fabricRestApi.CreateItem(workspace.Id, createModelRequest);
      await appLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

      await CreateAndBindSemanticModelConnecton(workspace, model.Id.Value, lakehouse);


      string report1DefinitionFolder = "Product Sales Summary.Report";
      var createReport1Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(report1DefinitionFolder);
      await appLogger.LogStep($"Creating [{createReport1Request.DisplayName}.Report]");
      createReport1Request.Definition =
        itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReport1Request.Definition, model.Id.Value);
      await appLogger.LogSubstep("Customizing report title header with customer name");
      createReport1Request.Definition = CustomizeReportTitle(createReport1Request, Deployment);
      var report1 = await fabricRestApi.CreateItem(workspace.Id, createReport1Request);
      await appLogger.LogSubstep($"Report created with Id of [{report1.Id.Value.ToString()}]");

      string report2DefinitionFolder = "Product Sales Time Intelligence.Report";
      var createReport2Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(report2DefinitionFolder);
      await appLogger.LogStep($"Creating [{createReport2Request.DisplayName}.Report]");
      createReport2Request.Definition =
        itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReport2Request.Definition, model.Id.Value);
      await appLogger.LogSubstep("Customizing report title header with customer name");
      createReport2Request.Definition = CustomizeReportTitle(createReport2Request, Deployment);
      var report2 = await fabricRestApi.CreateItem(workspace.Id, createReport2Request);
      await appLogger.LogSubstep($"Report created with Id of [{report2.Id.Value.ToString()}]");

      string report3DefinitionFolder = "Product Sales Top 10 Cities.Report";
      var createReport3Request = itemDefinitionFactory.GetCreateItemRequestFromFolder(report3DefinitionFolder);
      await appLogger.LogStep($"Creating [{createReport3Request.DisplayName}.Report]");
      createReport3Request.Definition =
        itemDefinitionFactory.UpdateReportDefinitionWithSemanticModelId(createReport3Request.Definition, model.Id.Value);
      await appLogger.LogSubstep("Customizing report title header with customer name");
      createReport3Request.Definition = CustomizeReportTitle(createReport3Request, Deployment);
      var report3 = await fabricRestApi.CreateItem(workspace.Id, createReport3Request);
      await appLogger.LogSubstep($"Report created with Id of [{report3.Id.Value.ToString()}]");

      await appLogger.LogSolutionComplete("Solution deployment complete");

      return workspace;
    }

    public async Task<Dictionary<string, string>> RecreateWorkspaceConnections(Workspace SourceWorkspace, Workspace TargetWorkspace, DeploymentPlan Deployment) {

      var workspaceConnections = await fabricRestApi.GetWorkspaceConnections(SourceWorkspace.Id);

      if (workspaceConnections.Where(conn => !conn.DisplayName.Contains("Lakehouse")).ToList().Count > 0) {
        await appLogger.LogStep("Recreating connections found in source workspace");
      }

      var connectionRedirects = new Dictionary<string, string>();

      foreach (var sourceConnection in workspaceConnections) {

        // ignore lakehouse connections
        if (!sourceConnection.DisplayName.Contains("Lakehouse")) {

          Connection targetConnection = null;

          switch (sourceConnection.ConnectionDetails.Type) {

            case "Web":
              string sourceWebUrl = sourceConnection.ConnectionDetails.Path;
              string targetWebUrl = sourceWebUrl;

              if (Deployment.Parameters.ContainsKey(DeploymentPlan.webDatasourcePathParameter)) {
                targetWebUrl = Deployment.Parameters[DeploymentPlan.webDatasourcePathParameter];
              }

              await appLogger.LogSubstep($"Web: {targetWebUrl}");
              targetConnection = await fabricRestApi.CreateAnonymousWebConnection(targetWebUrl, TargetWorkspace);

              // redirect connection Id
              connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

              // redirect connection path
              connectionRedirects.Add(sourceWebUrl, targetWebUrl);

              break;

            case "AzureDataLakeStorage":
              string sourceAdlsConnectionPath = sourceConnection.ConnectionDetails.Path;
              string sourceAdlsServer = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[0] + "dfs.core.windows.net";
              string sourceAdlsPath = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[1];

              int pathSlash = sourceAdlsPath.Substring(1).IndexOf("/");
              string sourceAdlsContainerName = sourceAdlsPath.Substring(1, pathSlash);
              string sourceAdlsContainerPath = sourceAdlsPath.Substring(pathSlash + 1);

              string targetAdlsServer = sourceAdlsServer;
              string targetAdlsPath = sourceAdlsPath;
              string targetAdlsContainerName = sourceAdlsContainerName;
              string targetAdlsContainerPath = sourceAdlsContainerPath;

              if ((Deployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
                  (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
                  (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

                targetAdlsServer = Deployment.Parameters[DeploymentPlan.adlsServerPathParameter];
                targetAdlsContainerName = Deployment.Parameters[DeploymentPlan.adlsContainerNameParameter];
                targetAdlsContainerPath = Deployment.Parameters[DeploymentPlan.adlsContainerPathParameter];
                targetAdlsPath = "/" + targetAdlsContainerName + targetAdlsContainerPath;
              }

              await appLogger.LogSubstep($"ADLS: {targetAdlsServer}{targetAdlsPath}");
              targetConnection = await fabricRestApi.CreateAzureStorageConnectionWithAccountKey(targetAdlsServer, targetAdlsPath, TargetWorkspace);

              // redirect connection Id
              connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

              // redirect connection path
              connectionRedirects.Add(sourceAdlsServer, targetAdlsServer);
              connectionRedirects.Add(sourceAdlsContainerName, targetAdlsContainerName);
              connectionRedirects.Add(sourceAdlsContainerPath, targetAdlsContainerPath);

              break;

            default:
              throw new ApplicationException("Unexpected connection type");
          }
        }
      }

      return connectionRedirects;

    }

    public async Task<Workspace> DeployFromWorkspace(string SourceWorkspace, string TargetWorkspace, DeploymentPlan Deployment) {

      await appLogger.LogSolution($"Deploying solution from source workspace [{SourceWorkspace}] to [{TargetWorkspace}]");

      await DisplayDeploymentParameters(Deployment);

      // create data collections to track substitution data
      var connectionRedirects = new Dictionary<string, string>();
      var sqlEndPointIds = new Dictionary<string, Item>();
      var lakehouseNames = new List<string>();
      var shortcutRedirects = new Dictionary<string, string>();
      var notebookRedirects = new Dictionary<string, string>();
      var dataPipelineRedirects = new Dictionary<string, string>();
      var semanticModelRedirects = new Dictionary<string, string>();
      var reportRedirects = new Dictionary<string, string>();

      var sourceWorkspace = await fabricRestApi.GetWorkspaceByName(SourceWorkspace);

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspace}]");
      var targetWorkspace = await fabricRestApi.CreateWorkspace(TargetWorkspace);
      await appLogger.LogSubstep($"New workspace created with Id of [{targetWorkspace.Id}]");

      var sourceWorkspaceInfo = await fabricRestApi.GetWorkspaceInfo(sourceWorkspace.Id);
      await fabricRestApi.UpdateWorkspaceDescription(targetWorkspace.Id, sourceWorkspaceInfo.Description);

      // add connection redirect for deployment pipelines
      connectionRedirects = await RecreateWorkspaceConnections(sourceWorkspace, targetWorkspace, Deployment);

      // make deep copy of connectionRedirects for starting point for other redirects
      shortcutRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      notebookRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      semanticModelRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      dataPipelineRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;

      // add redirects for workspace id
      notebookRedirects.Add(sourceWorkspace.Id.ToString(), targetWorkspace.Id.ToString());
      dataPipelineRedirects.Add(sourceWorkspace.Id.ToString(), targetWorkspace.Id.ToString());

      await appLogger.LogStep($"Deploying Workspace Items");

      var lakehouses = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "Lakehouse");
      foreach (var sourceLakehouse in lakehouses) {

        Guid sourceLakehouseId = sourceLakehouse.Id.Value;
        var sourceLakehouseSqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(sourceWorkspace.Id, sourceLakehouse.Id.Value);

        await appLogger.LogSubstep($"Creating [{sourceLakehouse.DisplayName}.Lakehouse]");
        var targetLakehouse = await fabricRestApi.CreateLakehouse(targetWorkspace.Id, sourceLakehouse.DisplayName);

        var targetLakehouseSqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(targetWorkspace.Id, targetLakehouse.Id.Value);

        // add lakehouse names and Ids to detect lakehouse default semantic model
        lakehouseNames.Add(targetLakehouse.DisplayName);
        sqlEndPointIds.Add(targetLakehouseSqlEndpoint.Id, targetLakehouse);

        // add redirect for lakehouse id
        notebookRedirects.Add(sourceLakehouse.Id.Value.ToString(), targetLakehouse.Id.Value.ToString());
        dataPipelineRedirects.Add(sourceLakehouse.Id.Value.ToString(), targetLakehouse.Id.Value.ToString());

        // add redirect for sql endpoint database name 
        semanticModelRedirects.Add(sourceLakehouseSqlEndpoint.Id, targetLakehouseSqlEndpoint.Id);

        // add redirect for sql endpoint server location
        if (!semanticModelRedirects.Keys.Contains(sourceLakehouseSqlEndpoint.ConnectionString)) {
          // only add sql endpoint server location once because it has same value for all lakehouses in the same workspace
          semanticModelRedirects.Add(sourceLakehouseSqlEndpoint.ConnectionString, targetLakehouseSqlEndpoint.ConnectionString);
        }

        // copy shortcuts
        var shortcuts = await fabricRestApi.GetLakehouseShortcuts(sourceWorkspace.Id, sourceLakehouseId);
        foreach (var shortcut in shortcuts) {

          if (shortcut.Target.Type == Microsoft.Fabric.Api.Core.Models.Type.AdlsGen2) {

            string sourceShortcutName = shortcut.Name;
            string sourceShortcutPath = shortcut.Path;
            string sourceShortcutLocation = shortcut.Target.AdlsGen2.Location.ToString();
            string sourceShortcutSubpath = shortcut.Target.AdlsGen2.Subpath;

            string targetShortcutName = sourceShortcutName;
            string targetShortcutPath = sourceShortcutPath;
            string targetShortcutLocation = sourceShortcutLocation;
            string targetShortcutSubpath = sourceShortcutSubpath;

            if ((Deployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
                (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
                (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

              targetShortcutLocation = Deployment.Parameters[DeploymentPlan.adlsServerPathParameter];

              targetShortcutSubpath = "/" + Deployment.Parameters[DeploymentPlan.adlsContainerNameParameter] +
                                             Deployment.Parameters[DeploymentPlan.adlsContainerPathParameter];

            }

            Guid targetConnectionId = new Guid(connectionRedirects[shortcut.Target.AdlsGen2.ConnectionId.ToString()]);

            await appLogger.LogSubstep($"Creating [{targetLakehouse.DisplayName}.{targetLakehouse.Type}.Shortcut]" +
                                       $" with path of [{targetShortcutPath.Substring(1)}/{targetShortcutName}]");

            await fabricRestApi.CreateAdlsGen2Shortcut(targetWorkspace.Id,
                                                 targetLakehouse.Id.Value,
                                                 targetShortcutName,
                                                 targetShortcutPath,
                                                 new Uri(targetShortcutLocation),
                                                 targetShortcutSubpath,
                                                 targetConnectionId);
          }

        }
      }

      var notebooks = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "Notebook");
      foreach (var sourceNotebook in notebooks) {
        await appLogger.LogSubstep($"Creating [{sourceNotebook.DisplayName}.Notebook]");
        var createRequest = new CreateItemRequest(sourceNotebook.DisplayName, sourceNotebook.Type);

        var notebookDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceNotebook.Id.Value);

        createRequest.Definition = itemDefinitionFactory.UpdateItemDefinitionPart(notebookDefinition,
                                                                                  "notebook-content.py",
                                                                                  notebookRedirects);

        var targetNotebook = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

        dataPipelineRedirects.Add(sourceNotebook.Id.Value.ToString(), targetNotebook.Id.Value.ToString());

        if (createRequest.DisplayName.Contains("Create")) {
          await appLogger.LogSubOperationStart($"Running  [{sourceNotebook.DisplayName}.Notebook]");
          await fabricRestApi.RunNotebook(targetWorkspace.Id, targetNotebook);
          await appLogger.LogOperationComplete();
        }

      }

      var pipelines = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "DataPipeline");
      foreach (var sourcePipeline in pipelines) {

        await appLogger.LogSubstep($"Creating [{sourcePipeline.DisplayName}.DataPipeline]");
        var createRequest = new CreateItemRequest(sourcePipeline.DisplayName, sourcePipeline.Type);

        var pipelineDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourcePipeline.Id.Value);

        createRequest.Definition = itemDefinitionFactory.UpdateItemDefinitionPart(pipelineDefinition,
                                                                         "pipeline-content.json",
                                                                          dataPipelineRedirects);

        var targetPipeline = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

        if (createRequest.DisplayName.Contains("Create")) {
          await appLogger.LogSubOperationStart($"Running  [{sourcePipeline.DisplayName}.DataPipeline]");
          await fabricRestApi.RunDataPipeline(targetWorkspace.Id, targetPipeline);
          await appLogger.LogOperationComplete();
        }

      }

      foreach (var sqlEndPointId in sqlEndPointIds.Keys) {
        await fabricRestApi.RefreshLakehouseTableSchema(sqlEndPointId);
      }

      var models = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "SemanticModel");
      foreach (var sourceModel in models) {

        // ignore default semantic models for lakehouses
        if (!lakehouseNames.Contains(sourceModel.DisplayName)) {

          await appLogger.LogSubstep($"Creating [{sourceModel.DisplayName}.SemanticModel]");

          // get model definition from source workspace
          var sourceModelDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceModel.Id.Value);

          // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
          var modelDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(sourceModelDefinition,
                                                                               "definition/expressions.tmdl",
                                                                               semanticModelRedirects);

          // use item definition to create clone in target workspace
          var createRequest = new CreateItemRequest(sourceModel.DisplayName, sourceModel.Type);
          createRequest.Definition = modelDefinition;
          var targetModel = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

          // track mapping between source semantic model and target semantic model
          reportRedirects.Add(sourceModel.Id.Value.ToString(), targetModel.Id.Value.ToString());

          var semanticModelDatasource = (await powerBiRestApi.GetDatasourcesForSemanticModel(targetWorkspace.Id, targetModel.Id.Value)).Value.FirstOrDefault();

          Item lakehouse = null;
          if (semanticModelDatasource.DatasourceType.ToLower() == "sql") {
            lakehouse = sqlEndPointIds[semanticModelDatasource.ConnectionDetails.Database];
          }

          await CreateAndBindSemanticModelConnecton(targetWorkspace, targetModel.Id.Value, lakehouse);

        }
      }

      var reports = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "Report");
      foreach (var sourceReport in reports) {

        // use item definition to create clone in target workspace
        await appLogger.LogSubstep($"Creating [{sourceReport.DisplayName}.Report]");

        // get model definition from source workspace
        var sourceReportDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceReport.Id.Value);

        var createRequest = new CreateItemRequest(sourceReport.DisplayName, sourceReport.Type);

        // update definition.pbir to point to correct semantic model
        createRequest.Definition = itemDefinitionFactory.UpdateItemDefinitionPart(sourceReportDefinition,
                                                                                  "definition.pbir",
                                                                                  reportRedirects);

        // update report.json with report title customization
        createRequest.Definition = CustomizeReportTitle(createRequest, Deployment);

        var targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

      }

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return targetWorkspace;

    }

    public async Task<Dictionary<string, string>> GetWorkspaceConnectionRedirects(Workspace SourceWorkspace, Workspace TargetWorkspace, DeploymentPlan Deployment) {

      var sourceWorkspaceConnections = await fabricRestApi.GetWorkspaceConnections(SourceWorkspace.Id);
      var targetWorkspaceConnections = await fabricRestApi.GetWorkspaceConnections(TargetWorkspace.Id);

      if (targetWorkspaceConnections.Where(conn => !conn.DisplayName.Contains("Lakehouse")).ToList().Count > 0) {
        await appLogger.LogStep("Discovering connections found in target workspace");
      }

      var connectionRedirects = new Dictionary<string, string>();

      foreach (var sourceConnection in sourceWorkspaceConnections) {
        // ignore lakehouse connections
        if (!sourceConnection.DisplayName.Contains("Lakehouse")) {
          int workspaceNameOffset = 48;
          string sourceConnectionName = sourceConnection.DisplayName.Substring(workspaceNameOffset);
          foreach (var targetConnection in targetWorkspaceConnections) {
            string targetConnectionName = targetConnection.DisplayName.Substring(workspaceNameOffset);
            if (sourceConnectionName == targetConnectionName) {

              switch (sourceConnection.ConnectionDetails.Type) {

                case "Web":
                  string sourceWebUrl = sourceConnection.ConnectionDetails.Path;
                  string targetWebUrl = sourceWebUrl;

                  if (Deployment.Parameters.ContainsKey(DeploymentPlan.webDatasourcePathParameter)) {
                    targetWebUrl = Deployment.Parameters[DeploymentPlan.webDatasourcePathParameter];
                  }

                  await appLogger.LogSubstep($"Web: {targetWebUrl}");

                  // redirect connection Id
                  connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

                  // redirect connection path
                  connectionRedirects.Add(sourceWebUrl, targetWebUrl);

                  break;

                case "AzureDataLakeStorage":
                  string sourceAdlsConnectionPath = sourceConnection.ConnectionDetails.Path;
                  string sourceAdlsServer = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[0] + "dfs.core.windows.net";
                  string sourceAdlsPath = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[1];

                  int pathSlash = sourceAdlsPath.Substring(1).IndexOf("/");
                  string sourceAdlsContainerName = sourceAdlsPath.Substring(1, pathSlash);
                  string sourceAdlsContainerPath = sourceAdlsPath.Substring(pathSlash + 1);

                  string targetAdlsServer = sourceAdlsServer;
                  string targetAdlsPath = sourceAdlsPath;
                  string targetAdlsContainerName = sourceAdlsContainerName;
                  string targetAdlsContainerPath = sourceAdlsContainerPath;

                  if ((Deployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
                      (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
                      (Deployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

                    targetAdlsServer = Deployment.Parameters[DeploymentPlan.adlsServerPathParameter];
                    targetAdlsContainerName = Deployment.Parameters[DeploymentPlan.adlsContainerNameParameter];
                    targetAdlsContainerPath = Deployment.Parameters[DeploymentPlan.adlsContainerPathParameter];
                    targetAdlsPath = "/" + targetAdlsContainerName + targetAdlsContainerPath;
                  }

                  await appLogger.LogSubstep($"ADLS: {targetAdlsServer}{targetAdlsPath}");

                  // redirect connection Id
                  connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

                  // redirect connection path
                  connectionRedirects.Add(sourceAdlsServer, targetAdlsServer);
                  connectionRedirects.Add(sourceAdlsContainerName, targetAdlsContainerName);
                  connectionRedirects.Add(sourceAdlsContainerPath, targetAdlsContainerPath);

                  break;

                default:
                  throw new ApplicationException("Unexpected connection type");
              }

            }
          }
        }

      }

      return connectionRedirects;
    }

    public async Task<Workspace> UpdateFromWorkspace(string SourceWorkspace, string TargetWorkspace, DeploymentPlan Deployment, bool DeleteOrphanedItems = false) {

      await appLogger.LogSolution($"Updating solution from source workspace [{SourceWorkspace}] to [{TargetWorkspace}]");

      await DisplayDeploymentParameters(Deployment);

      // create data collections to track substitution data
      var connectionRedirects = new Dictionary<string, string>();
      var sqlEndPointIds = new List<string>();
      var lakehouseNames = new List<string>();
      var shortcutRedirects = new Dictionary<string, string>();
      var notebookRedirects = new Dictionary<string, string>();
      var dataPipelineRedirects = new Dictionary<string, string>();
      var semanticModelRedirects = new Dictionary<string, string>();
      var reportRedirects = new Dictionary<string, string>();

      var sourceWorkspace = await fabricRestApi.GetWorkspaceByName(SourceWorkspace);
      var sourceWorkspaceItems = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id);

      var targetWorkspace = await fabricRestApi.GetWorkspaceByName(TargetWorkspace);
      var targetWorkspaceItems = await fabricRestApi.GetWorkspaceItems(targetWorkspace.Id);

      // update target workspace description
      var sourceWorkspaceInfo = await fabricRestApi.GetWorkspaceInfo(sourceWorkspace.Id);
      await fabricRestApi.UpdateWorkspaceDescription(targetWorkspace.Id, sourceWorkspaceInfo.Description);

      // add connection redirect
      connectionRedirects = await GetWorkspaceConnectionRedirects(sourceWorkspace, targetWorkspace, Deployment);

      // make deep copy of connectionRedirects for starting point for other redirects
      shortcutRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      notebookRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      semanticModelRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      dataPipelineRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;

      // add redirects for workspace id
      notebookRedirects.Add(sourceWorkspace.Id.ToString(), targetWorkspace.Id.ToString());
      dataPipelineRedirects.Add(sourceWorkspace.Id.ToString(), targetWorkspace.Id.ToString());

      await appLogger.LogStep($"Processing workspace item updates");

      // create lakehouses if they do not exist in target
      var lakehouses = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "Lakehouse");
      foreach (var sourceLakehouse in lakehouses) {

        Guid sourceLakehouseId = sourceLakehouse.Id.Value;
        var sourceLakehouseSqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(sourceWorkspace.Id, sourceLakehouse.Id.Value);

        var targetLakehouse = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(sourceLakehouse.DisplayName) &&
                                                                 (item.Type == sourceLakehouse.Type))).FirstOrDefault();

        if (targetLakehouse != null) {
          // nothing to do here if lakehouse already exists
        }
        else {
          // create lakehouse if it doesn't exist in target workspace
          await appLogger.LogSubstep($"Creating [{sourceLakehouse.DisplayName}.{sourceLakehouse.Type}]");
          targetLakehouse = await fabricRestApi.CreateLakehouse(targetWorkspace.Id, sourceLakehouse.DisplayName);
        }

        var targetLakehouseSqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(targetWorkspace.Id, targetLakehouse.Id.Value);

        // add lakehouse names to detect lakehouse default semantic model
        lakehouseNames.Add(targetLakehouse.DisplayName);

        // add redirect for lakehouse id
        notebookRedirects.Add(sourceLakehouse.Id.Value.ToString(), targetLakehouse.Id.Value.ToString());
        dataPipelineRedirects.Add(sourceLakehouse.Id.Value.ToString(), targetLakehouse.Id.Value.ToString());

        // add redirect for sql endpoint database name 
        semanticModelRedirects.Add(sourceLakehouseSqlEndpoint.Id, targetLakehouseSqlEndpoint.Id);

        // add redirect for sql endpoint server location
        if (!semanticModelRedirects.Keys.Contains(sourceLakehouseSqlEndpoint.ConnectionString)) {
          // only add sql endpoint server location once because it has same value for all lakehouses in the same workspace
          semanticModelRedirects.Add(sourceLakehouseSqlEndpoint.ConnectionString, targetLakehouseSqlEndpoint.ConnectionString);
        }


      }

      // create or update notebooks
      var notebooks = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "Notebook");
      foreach (var sourceNotebook in notebooks) {

        var sourceNotebookDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id,
                                                                       sourceNotebook.Id.Value);

        var notebookDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(sourceNotebookDefinition,
                                                                       "notebook-content.py",
                                                                        notebookRedirects);

        var targetNotebook = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(sourceNotebook.DisplayName) &&
                                                                (item.Type == sourceNotebook.Type))).FirstOrDefault();

        if (targetNotebook != null) {
          // update existing notebook
          await appLogger.LogSubstep($"Updating [{sourceNotebook.DisplayName}.{sourceNotebook.Type}]");
          var updateRequest = new UpdateItemDefinitionRequest(notebookDefinition);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetNotebook.Id.Value, updateRequest);
        }
        else {
          // create new notebook
          await appLogger.LogSubstep($"Creating [{sourceNotebook.DisplayName}.{sourceNotebook.Type}]");
          var createRequest = new CreateItemRequest(sourceNotebook.DisplayName, sourceNotebook.Type);
          createRequest.Definition = notebookDefinition;
          targetNotebook = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

          // run new notebook
          await appLogger.LogSubOperationStart($"Running  [{sourceNotebook.DisplayName}.Notebook]");
          await fabricRestApi.RunNotebook(targetWorkspace.Id, targetNotebook);
          await appLogger.LogOperationComplete();

        }

        dataPipelineRedirects.Add(sourceNotebook.Id.Value.ToString(), targetNotebook.Id.Value.ToString());

      }

      // create or update data pipelines
      var pipelines = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "DataPipeline");
      foreach (var sourcePipeline in pipelines) {

        var sourcePipelineDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourcePipeline.Id.Value);

        var pipelineDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(sourcePipelineDefinition,
                                                                       "pipeline-content.json",
                                                                        dataPipelineRedirects);

        var targetPipeline = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(sourcePipeline.DisplayName) &&
                                                                (item.Type == sourcePipeline.Type))).FirstOrDefault();

        if (targetPipeline != null) {
          // update existing pipeline
          await appLogger.LogSubstep($"Updating [{targetPipeline.DisplayName}.{targetPipeline.Type}]");
          var updateRequest = new UpdateItemDefinitionRequest(pipelineDefinition);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetPipeline.Id.Value, updateRequest);
        }
        else {
          // create new pipeline
          await appLogger.LogSubstep($"Creating [{sourcePipeline.DisplayName}.{sourcePipeline.Type}]");
          var createRequest = new CreateItemRequest(sourcePipeline.DisplayName, sourcePipeline.Type);
          createRequest.Definition = pipelineDefinition;
          targetPipeline = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

          // run pipeline if 'Create' is in its DislayName
          if (createRequest.DisplayName.Contains("Create")) {
            await appLogger.LogSubOperationStart($"Running  [{sourcePipeline.DisplayName}.DataPipeline]");
            await fabricRestApi.RunDataPipeline(targetWorkspace.Id, targetPipeline);
            await appLogger.LogOperationComplete();
          }

        }
      }

      // create or update semantic models
      var models = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "SemanticModel");
      foreach (var sourceModel in models) {

        // ignore default semantic model for lakehouse
        if (!lakehouseNames.Contains(sourceModel.DisplayName)) {

          // get model definition from source workspace
          var sourceModelDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceModel.Id.Value);

          // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
          var modelDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(sourceModelDefinition,
                                                                       "definition/expressions.tmdl",
                                                                       semanticModelRedirects);

          var targetModel = targetWorkspaceItems.Where(item => (item.Type == sourceModel.Type) &&
                                                               (item.DisplayName == sourceModel.DisplayName)).FirstOrDefault();

          if (targetModel != null) {
            await appLogger.LogSubstep($"Updating [{sourceModel.DisplayName}.{sourceModel.Type}]");
            var updateRequest = new UpdateItemDefinitionRequest(modelDefinition);
            await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetModel.Id.Value, updateRequest);
          }
          else {
            await appLogger.LogSubstep($"Creating [{sourceModel.DisplayName}.{sourceModel.Type}]");
            var createRequest = new CreateItemRequest(sourceModel.DisplayName, sourceModel.Type);
            createRequest.Definition = modelDefinition;
            targetModel = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

            await CreateAndBindSemanticModelConnecton(targetWorkspace, targetModel.Id.Value);

          }

          // track mapping between source semantic model and target semantic model
          reportRedirects.Add(sourceModel.Id.Value.ToString(), targetModel.Id.Value.ToString());

        }

      }

      // create or update reports
      var reports = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "Report");
      foreach (var sourceReport in reports) {

        // get model definition from source workspace
        var sourceReportDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceReport.Id.Value);

        // update definition.pbir to point to correct semantic model
        var reportDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(sourceReportDefinition,
                                                                             "definition.pbir",
                                                                             reportRedirects);

        // update report.json with report title customiztions
        reportDefinition = CustomizeReportTitle(reportDefinition, sourceReport.DisplayName, Deployment);

        var targetReport = targetWorkspaceItems.FirstOrDefault(item => (item.Type == "Report") &&
                                                                       (item.DisplayName == sourceReport.DisplayName));

        if (targetReport != null) {
          // update existing report
          await appLogger.LogSubstep($"Updating [{sourceReport.DisplayName}.{sourceReport.Type}]");
          var updateRequest = new UpdateItemDefinitionRequest(reportDefinition);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetReport.Id.Value, updateRequest);
        }
        else {
          // use item definition to create clone in target workspace
          await appLogger.LogSubstep($"Creating [{sourceReport.DisplayName}.{sourceReport.Type}]");
          var createRequest = new CreateItemRequest(sourceReport.DisplayName, sourceReport.Type);
          createRequest.Definition = reportDefinition;
          targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
        }
      }

      // delete orphaned items in target workspace
      if (DeleteOrphanedItems) {

        List<string> sourceWorkspaceItemNames = new List<string>();

        sourceWorkspaceItemNames.AddRange(
          sourceWorkspaceItems.Select(item => $"{item.DisplayName}.{item.Type}")
        );

        var lakehouseNamesInTarget = targetWorkspaceItems
                                       .Where(item => item.Type == "Lakehouse")
                                       .Select(item => item.DisplayName).ToList();

        foreach (var item in targetWorkspaceItems) {
          string itemName = $"{item.DisplayName}.{item.Type}";
          if (!sourceWorkspaceItemNames.Contains(itemName) &&
             (item.Type != "SQLEndpoint") &&
             !(item.Type == "SemanticModel" && lakehouseNamesInTarget.Contains(item.DisplayName))) {
            try {
              await appLogger.LogSubstep($"Deleting [{itemName}]");
              await fabricRestApi.DeleteItem(targetWorkspace.Id, item);
            }
            catch {
              await appLogger.LogSubstep($"Could not delete [{itemName}]");

            }
          }
        }


      }

      await appLogger.LogSolutionComplete("Update Processing complete");

      return targetWorkspace;

    }

    public async Task<Workspace> UpdateReportsFromWorkspace(string SourceWorkspace, string TargetWorkspace, DeploymentPlan Deployment, bool DeleteOrphanedItems = false) {

      await appLogger.LogSolution($"Updating reports from source workspace [{SourceWorkspace}] to [{TargetWorkspace}]");

      var sourceWorkspace = await fabricRestApi.GetWorkspaceByName(SourceWorkspace);
      var sourceWorkspaceItems = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id);

      var targetWorkspace = await fabricRestApi.GetWorkspaceByName(TargetWorkspace);
      var targetWorkspaceItems = await fabricRestApi.GetWorkspaceItems(targetWorkspace.Id);

      await appLogger.LogStep($"Processing report updates");

      // create or update reports
      var reports = await fabricRestApi.GetWorkspaceItems(sourceWorkspace.Id, "Report");
      foreach (var sourceReport in reports) {

        var sourceSemanticModel = await powerBiRestApi.GetDatasetForReport(sourceWorkspace.Id, sourceReport.Id.Value);

        var targetSemanticModel = await fabricRestApi.GetSemanticModelByName(targetWorkspace.Id, sourceSemanticModel.Name);

        if (targetSemanticModel == null) {
          await appLogger.LogSubstep($"Cannot Update [{sourceReport.DisplayName}.{sourceReport.Type}] due to missing semantic model");
        }
        else {

          var reportRedirects = new Dictionary<string, string>();
          reportRedirects.Add(sourceSemanticModel.Id, targetSemanticModel.Id.ToString());

          // get model definition from source workspace
          var sourceReportDefinition = await fabricRestApi.GetItemDefinition(sourceWorkspace.Id, sourceReport.Id.Value);

          // update definition.pbir to point to correct semantic model
          var reportDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(sourceReportDefinition,
                                                                               "definition.pbir",
                                                                               reportRedirects);

          // update report.json with report title customiztions
          reportDefinition = CustomizeReportTitle(reportDefinition, sourceReport.DisplayName, Deployment);

          var targetReport = targetWorkspaceItems.FirstOrDefault(item => (item.Type == "Report") &&
                                                                         (item.DisplayName == sourceReport.DisplayName));

          if (targetReport != null) {
            // update existing report
            await appLogger.LogSubstep($"Updating [{sourceReport.DisplayName}.{sourceReport.Type}]");
            var updateRequest = new UpdateItemDefinitionRequest(reportDefinition);
            await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetReport.Id.Value, updateRequest);
          }
          else {
            // use item definition to create clone in target workspace
            await appLogger.LogSubstep($"Creating [{sourceReport.DisplayName}.{sourceReport.Type}]");
            var createRequest = new CreateItemRequest(sourceReport.DisplayName, sourceReport.Type);
            createRequest.Definition = reportDefinition;
            targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
          }

        }

      }

      await appLogger.LogSolutionComplete("Report updates complete");

      return targetWorkspace;

    }

     public async Task<Dictionary<string, string>> RecreateWorkspaceConnections(List<DeploymentSourceConnection> WorkspaceConnections, Workspace TargetWorkspace, SolutionDeploymentPlan SolutionDeployment) {

      var workspaceConnections = SolutionDeployment.DeployConfig.SourceConnections;

      if (workspaceConnections.Where(conn => !conn.DisplayName.Contains("Lakehouse")).ToList().Count > 0) {
        await appLogger.LogStep("Recreating connections found in source workspace");
      }

      var connectionRedirects = new Dictionary<string, string>();

      foreach (var sourceConnection in workspaceConnections) {

        // ignore lakehouse connections
        if (!sourceConnection.DisplayName.Contains("Lakehouse")) {

          Connection targetConnection = null;

          switch (sourceConnection.Type) {

            case "Web":
              string sourceWebUrl = sourceConnection.Path;
              string targetWebUrl = sourceWebUrl;

              if (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.webDatasourcePathParameter)) {
                targetWebUrl = SolutionDeployment.Parameters[DeploymentPlan.webDatasourcePathParameter];
              }

              await appLogger.LogSubstep($"Web: {targetWebUrl}");
              targetConnection = await fabricRestApi.CreateAnonymousWebConnection(targetWebUrl, TargetWorkspace);

              // redirect connection Id
              connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

              // redirect connection path
              connectionRedirects.Add(sourceWebUrl, targetWebUrl);

              break;

            case "AzureDataLakeStorage":
              string sourceAdlsConnectionPath = sourceConnection.Path;
              string sourceAdlsServer = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[0] + "dfs.core.windows.net";
              string sourceAdlsPath = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[1];

              int pathSlash = sourceAdlsPath.Substring(1).IndexOf("/");
              string sourceAdlsContainerName = sourceAdlsPath.Substring(1, pathSlash);
              string sourceAdlsContainerPath = sourceAdlsPath.Substring(pathSlash + 1);

              string targetAdlsServer = sourceAdlsServer;
              string targetAdlsPath = sourceAdlsPath;
              string targetAdlsContainerName = sourceAdlsContainerName;
              string targetAdlsContainerPath = sourceAdlsContainerPath;


              if ((SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
                  (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
                  (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

                targetAdlsServer = SolutionDeployment.Parameters[DeploymentPlan.adlsServerPathParameter];
                targetAdlsContainerName = SolutionDeployment.Parameters[DeploymentPlan.adlsContainerNameParameter];
                targetAdlsContainerPath = SolutionDeployment.Parameters[DeploymentPlan.adlsContainerPathParameter];
                targetAdlsPath = "/" + targetAdlsContainerName + targetAdlsContainerPath;

              }

              await appLogger.LogSubstep($"ADLS: {targetAdlsServer}{targetAdlsPath}");

              targetConnection = await fabricRestApi.CreateAzureStorageConnectionWithAccountKey(targetAdlsServer, targetAdlsPath, TargetWorkspace);

              // redirect connection Id
              connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

              // redirect connection path
              connectionRedirects.Add(sourceAdlsServer, targetAdlsServer);
              connectionRedirects.Add(sourceAdlsContainerName, targetAdlsContainerName);
              connectionRedirects.Add(sourceAdlsContainerPath, targetAdlsContainerPath);

              break;

            default:
              throw new ApplicationException("Unexpected connection type");

          }
        }

      }

      return connectionRedirects;

    }

    // generic export logic

    public async Task<Workspace> DeployFromExport(string SolutionFolder, string TargetWorkspaceName, SolutionDeploymentPlan SolutionDeployment) {

      // create data collections to track substitution data
      var connectionRedirects = new Dictionary<string, string>();
      var sqlEndPointIds = new Dictionary<string, Item>();
      var lakehouseNames = new List<string>();
      var shortcutRedirects = new Dictionary<string, string>();
      var notebookRedirects = new Dictionary<string, string>();
      var dataPipelineRedirects = new Dictionary<string, string>();
      var semanticModelRedirects = new Dictionary<string, string>();
      var reportRedirects = new Dictionary<string, string>();

      await appLogger.LogStep($"Creating new workspace named [{TargetWorkspaceName}]");
      var targetWorkspace = await fabricRestApi.CreateWorkspace(TargetWorkspaceName);
      await appLogger.LogSubstep($"New workspace created with id of {targetWorkspace.Id.ToString()}");

      var targetWorkspaceDescription = SolutionDeployment.DeployConfig.SourceWorkspaceDescription;
      await fabricRestApi.UpdateWorkspaceDescription(targetWorkspace.Id, targetWorkspaceDescription);

      var sourceWorkspaceConnections = SolutionDeployment.DeployConfig.SourceConnections;

      connectionRedirects = await RecreateWorkspaceConnections(sourceWorkspaceConnections, targetWorkspace, SolutionDeployment);

      // make deep copy of connectionRedirects for starting point for other redirects
      shortcutRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      notebookRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      semanticModelRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      dataPipelineRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;

      notebookRedirects.Add(SolutionDeployment.GetSourceWorkspaceId(), targetWorkspace.Id.ToString());
      dataPipelineRedirects.Add(SolutionDeployment.GetSourceWorkspaceId(), targetWorkspace.Id.ToString());

      await appLogger.LogStep($"Deploying Workspace Items");

      foreach (var lakehouse in SolutionDeployment.GetLakehouses()) {

        var sourceLakehouse = SolutionDeployment.GetSourceLakehouse(lakehouse.DisplayName);
        Guid sourceLakehouseId = new Guid(sourceLakehouse.Id);

        await appLogger.LogSubstep($"Creating [{lakehouse.ItemName}]");
        var targetLakehouse = await fabricRestApi.CreateLakehouse(targetWorkspace.Id, lakehouse.DisplayName);

        var targetLakehouseSqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(targetWorkspace.Id, targetLakehouse.Id.Value);

        // add lakehouse names and Ids to detect lakehouse default semantic model
        lakehouseNames.Add(targetLakehouse.DisplayName);
        sqlEndPointIds.Add(targetLakehouseSqlEndpoint.Id, targetLakehouse);

        // add lakehouse redirect Ids for other workspace items
        notebookRedirects.Add(sourceLakehouse.Id, targetLakehouse.Id.Value.ToString());
        dataPipelineRedirects.Add(sourceLakehouse.Id, targetLakehouse.Id.Value.ToString());

        // add redirect for sql endpoint database name 
        semanticModelRedirects.Add(sourceLakehouse.Database, targetLakehouseSqlEndpoint.Id);

        // add redirect for sql endpoint server location
        if (!semanticModelRedirects.Keys.Contains(sourceLakehouse.Server)) {
          // only add sql endpoint server location once because it has same value for all lakehouses in the same workspace
          semanticModelRedirects.Add(sourceLakehouse.Server, targetLakehouseSqlEndpoint.ConnectionString);
        }

        // copy shortcuts
        var shortcuts = sourceLakehouse.Shortcuts;
        if (shortcuts != null) {
          foreach (var shortcut in shortcuts) {

            if (shortcut.Type.ToLower() == "adlsgen2") {

              string sourceShortcutName = shortcut.Name;
              string sourceShortcutPath = shortcut.Path;
              string sourceShortcutLocation = shortcut.Location;
              string sourceShortcutSubpath = shortcut.Subpath;

              string targetShortcutName = sourceShortcutName;
              string targetShortcutPath = sourceShortcutPath;
              string targetShortcutLocation = sourceShortcutLocation;
              string targetShortcutSubpath = sourceShortcutSubpath;

              if ((SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
                  (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
                  (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

                targetShortcutLocation = SolutionDeployment.Parameters[DeploymentPlan.adlsServerPathParameter];

                targetShortcutSubpath = "/" + SolutionDeployment.Parameters[DeploymentPlan.adlsContainerNameParameter] +
                                              SolutionDeployment.Parameters[DeploymentPlan.adlsContainerPathParameter];

              }

              Guid targetConnectionId = new Guid(connectionRedirects[shortcut.ConnectionId]);

              await appLogger.LogSubstep($"Creating [{lakehouse.ItemName}.Shortcut] with path of [{targetShortcutPath.Substring(1)}/{targetShortcutName}]");

              await fabricRestApi.CreateAdlsGen2Shortcut(targetWorkspace.Id,
                                                   targetLakehouse.Id.Value,
                                                   targetShortcutName,
                                                   targetShortcutPath,
                                                   new Uri(targetShortcutLocation),
                                                   targetShortcutSubpath,
                                                   targetConnectionId);

            }

          }
        }
      }

      foreach (var notebook in SolutionDeployment.GetNotebooks()) {

        var sourceNotebook = SolutionDeployment.GetSourceNotebook(notebook.DisplayName);

        await appLogger.LogSubstep($"Creating [{notebook.ItemName}]");
        var createRequest = new CreateItemRequest(notebook.DisplayName, notebook.Type);
        createRequest.Definition = itemDefinitionFactory.UpdateItemDefinitionPart(notebook.Definition, "notebook-content.py", notebookRedirects);
        var targetNotebook = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

        dataPipelineRedirects.Add(sourceNotebook.Id, targetNotebook.Id.Value.ToString());

        if (createRequest.DisplayName.Contains("Create")) {
          await appLogger.LogSubOperationStart($"Running  [{notebook.ItemName}]");
          await fabricRestApi.RunNotebook(targetWorkspace.Id, targetNotebook);
          await appLogger.LogOperationComplete();
        }

      }

      foreach (var pipeline in SolutionDeployment.GetDataPipelines()) {

        await appLogger.LogSubstep($"Creating [{pipeline.ItemName}]");
        var createRequest = new CreateItemRequest(pipeline.DisplayName, pipeline.Type);
        createRequest.Definition = itemDefinitionFactory.UpdateItemDefinitionPart(pipeline.Definition,
                                                                                  "pipeline-content.json",
                                                                                  dataPipelineRedirects);

        var targetPipeline = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

        if (createRequest.DisplayName.Contains("Create")) {
          await appLogger.LogSubOperationStart($"Running  [{createRequest.DisplayName}.DataPipeline]");
          await fabricRestApi.RunDataPipeline(targetWorkspace.Id, targetPipeline);
          await appLogger.LogOperationComplete();
        }

      }

      foreach (var sqlEndPointId in sqlEndPointIds.Keys) {
        await fabricRestApi.RefreshLakehouseTableSchema(sqlEndPointId);
      }

      foreach (var model in SolutionDeployment.GetSemanticModels()) {

        // ignore default semantic model for lakehouse
        if (!lakehouseNames.Contains(model.DisplayName)) {

          var sourceModel = SolutionDeployment.GetSourceSemanticModel(model.DisplayName);

          // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
          var modelDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(model.Definition, "definition/expressions.tmdl", semanticModelRedirects);

          // use item definition to create clone in target workspace
          await appLogger.LogSubstep($"Creating [{model.ItemName}]");
          var createRequest = new CreateItemRequest(model.DisplayName, model.Type);
          createRequest.Definition = modelDefinition;
          var targetModel = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

          // track mapping between source semantic model and target semantic model
          reportRedirects.Add(sourceModel.Id, targetModel.Id.Value.ToString());

          var semanticModelDatasource = (await powerBiRestApi.GetDatasourcesForSemanticModel(targetWorkspace.Id, targetModel.Id.Value)).Value.FirstOrDefault();

          Item lakehouse = null;
          if (semanticModelDatasource.DatasourceType.ToLower() == "sql") {
            lakehouse = sqlEndPointIds[semanticModelDatasource.ConnectionDetails.Database];
          }

          await CreateAndBindSemanticModelConnecton(targetWorkspace, targetModel.Id.Value, lakehouse);

        }

      }

      foreach (var report in SolutionDeployment.GetReports()) {

        var sourceReport = SolutionDeployment.GetSourceReport(report.DisplayName);

        // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
        var reportDefinition = itemDefinitionFactory.UpdateReportDefinitionWithRedirection(report.Definition, targetWorkspace.Id, reportRedirects);

        // update report.json with report title customization
        reportDefinition = CustomizeReportTitle(reportDefinition, report.DisplayName, SolutionDeployment);

        // use item definition to create clone in target workspace
        await appLogger.LogSubstep($"Creating [{report.ItemName}]");
        var createRequest = new CreateItemRequest(report.DisplayName, report.Type);
        createRequest.Definition = reportDefinition;
        var targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

      }

      await appLogger.LogSolutionComplete("Solution provisioning complete");

      return targetWorkspace;

    }

    public async Task<Dictionary<string, string>> GetWorkspaceConnectionRedirects(List<DeploymentSourceConnection> WorkspaceConnections, Guid TargetWorkspaceId, SolutionDeploymentPlan SolutionDeployment) {

      var sourceWorkspaceConnections = WorkspaceConnections;
      var targetWorkspaceConnections = await fabricRestApi.GetWorkspaceConnections(TargetWorkspaceId);

      // make sure there is at least one connection that is not lakehouse connection
      if (targetWorkspaceConnections.Where(conn => !conn.DisplayName.Contains("Lakehouse")).ToList().Count > 0) {
        await appLogger.LogStep("Discovering connections found in target workspace");
      }

      var connectionRedirects = new Dictionary<string, string>();

      foreach (var sourceConnection in WorkspaceConnections) {
        // ignore lakehouse connections
        if (!sourceConnection.DisplayName.Contains("Lakehouse")) {
          int workspaceNameOffset = 48;
          string sourceConnectionName = sourceConnection.DisplayName;
          foreach (var targetConnection in targetWorkspaceConnections) {
            string targetConnectionName = targetConnection.DisplayName.Substring(workspaceNameOffset);
            if (sourceConnectionName == targetConnectionName) {

              switch (sourceConnection.Type) {

                case "Web":
                  string sourceWebUrl = sourceConnection.Path;
                  string targetWebUrl = sourceWebUrl;

                  if (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.webDatasourcePathParameter)) {
                    targetWebUrl = SolutionDeployment.Parameters[DeploymentPlan.webDatasourcePathParameter];
                  }

                  await appLogger.LogSubstep($"Web: {targetWebUrl}");

                  // redirect connection Id
                  connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

                  // redirect connection path
                  connectionRedirects.Add(sourceWebUrl, targetWebUrl);

                  break;

                case "AzureDataLakeStorage":
                  string sourceAdlsConnectionPath = sourceConnection.Path;
                  string sourceAdlsServer = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[0] + "dfs.core.windows.net";
                  string sourceAdlsPath = sourceAdlsConnectionPath.Split("dfs.core.windows.net")[1];

                  int pathSlash = sourceAdlsPath.Substring(1).IndexOf("/");
                  string sourceAdlsContainerName = sourceAdlsPath.Substring(1, pathSlash);
                  string sourceAdlsContainerPath = sourceAdlsPath.Substring(pathSlash + 1);

                  string targetAdlsServer = sourceAdlsServer;
                  string targetAdlsPath = sourceAdlsPath;
                  string targetAdlsContainerName = sourceAdlsContainerName;
                  string targetAdlsContainerPath = sourceAdlsContainerPath;

                  if ((SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsServerPathParameter)) &&
                      (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerNameParameter)) &&
                      (SolutionDeployment.Parameters.ContainsKey(DeploymentPlan.adlsContainerPathParameter))) {

                    targetAdlsServer = SolutionDeployment.Parameters[DeploymentPlan.adlsServerPathParameter];
                    targetAdlsContainerName = SolutionDeployment.Parameters[DeploymentPlan.adlsContainerNameParameter];
                    targetAdlsContainerPath = SolutionDeployment.Parameters[DeploymentPlan.adlsContainerPathParameter];
                    targetAdlsPath = "/" + targetAdlsContainerName + targetAdlsContainerPath;
                  }

                  await appLogger.LogSubstep($"ADLS: {targetAdlsServer}{targetAdlsPath}");

                  // redirect connection Id
                  connectionRedirects.Add(sourceConnection.Id.ToString(), targetConnection.Id.ToString());

                  // redirect connection path
                  connectionRedirects.Add(sourceAdlsServer, targetAdlsServer);
                  connectionRedirects.Add(sourceAdlsContainerName, targetAdlsContainerName);
                  connectionRedirects.Add(sourceAdlsContainerPath, targetAdlsContainerPath);

                  break;

                default:
                  throw new ApplicationException("Unexpected connection type");
              }

            }
          }
        }

      }

      return connectionRedirects;
    }

    public async Task UpdateFromExport(string ExportName, string TargetWorkspaceName, SolutionDeploymentPlan SolutionDeployment, bool DeleteOrphanedItems = false) {

      var connectionRedirects = new Dictionary<string, string>();
      var sqlEndPointIds = new Dictionary<string, Item>();
      var lakehouseNames = new List<string>();
      var shortcutRedirects = new Dictionary<string, string>();
      var notebookRedirects = new Dictionary<string, string>();
      var dataPipelineRedirects = new Dictionary<string, string>();
      var semanticModelRedirects = new Dictionary<string, string>();
      var reportRedirects = new Dictionary<string, string>();

      var sourceWorkspaceItems = SolutionDeployment.DeployConfig.SourceItems;

      var targetWorkspace = await fabricRestApi.GetWorkspaceByName(TargetWorkspaceName);
      var targetWorkspaceItems = await fabricRestApi.GetWorkspaceItems(targetWorkspace.Id);

      var targetWorkspaceDesciption = SolutionDeployment.DeployConfig.SourceWorkspaceDescription;
      await fabricRestApi.UpdateWorkspaceDescription(targetWorkspace.Id, targetWorkspaceDesciption);

      connectionRedirects = await GetWorkspaceConnectionRedirects(SolutionDeployment.DeployConfig.SourceConnections, targetWorkspace.Id, SolutionDeployment);

      // copy connections dictionary for shortcuts, notebooks and data pipelines
      // make deep copy of connectionRedirects for starting point for other redirects
      shortcutRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      notebookRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      semanticModelRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      dataPipelineRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;

      notebookRedirects.Add(SolutionDeployment.GetSourceWorkspaceId(), targetWorkspace.Id.ToString());
      dataPipelineRedirects.Add(SolutionDeployment.GetSourceWorkspaceId(), targetWorkspace.Id.ToString());

      await appLogger.LogStep($"Processing workspace item updates");

      var lakehouses = SolutionDeployment.GetLakehouses();
      foreach (var lakehouse in lakehouses) {
        var sourceLakehouse = SolutionDeployment.GetSourceLakehouse(lakehouse.DisplayName);
        var targetLakehouse = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(lakehouse.DisplayName) &&
                                                                 (item.Type == lakehouse.Type))).FirstOrDefault();

        if (targetLakehouse != null) {
          // update item - nothing to do for lakehouse        
        }
        else {
          // create item
          await appLogger.LogSubstep($"Creating [{lakehouse.ItemName}]");
          targetLakehouse = await fabricRestApi.CreateLakehouse(targetWorkspace.Id, lakehouse.DisplayName);
        }

        var targetLakehouseSqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(targetWorkspace.Id, targetLakehouse.Id.Value);

        // add lakehouse names to detect lakehouse default semantic model
        lakehouseNames.Add(targetLakehouse.DisplayName);

        // add redirects for lakehouse id
        notebookRedirects.Add(sourceLakehouse.Id, targetLakehouse.Id.Value.ToString());
        dataPipelineRedirects.Add(sourceLakehouse.Id, targetLakehouse.Id.Value.ToString());

        // add redirect for sql endpoint database name 
        semanticModelRedirects.Add(sourceLakehouse.Database, targetLakehouseSqlEndpoint.Id);

        // add redirect for sql endpoint server location
        if (!semanticModelRedirects.Keys.Contains(sourceLakehouse.Server)) {
          // only add sql endpoint server location once because it has same value for all lakehouses in the same workspace
          semanticModelRedirects.Add(sourceLakehouse.Server, targetLakehouseSqlEndpoint.ConnectionString);
        }

        // inspect shortcuts - currnently no logic to add missing shortcuts in UPDATE workflow
        var targetShortcuts = await fabricRestApi.GetLakehouseShortcuts(targetWorkspace.Id, targetLakehouse.Id.Value);
        var targetShortcutPaths = targetShortcuts.Select(shortcut => shortcut.Path + "/" + shortcut.Name).ToList();
        if (sourceLakehouse.Shortcuts != null) {
          foreach (var shortcut in sourceLakehouse.Shortcuts) {
            string shortcutPath = shortcut.Path + "/" + shortcut.Name;
            if (!targetShortcutPaths.Contains(shortcutPath)) {
              await appLogger.LogSubstep($"New shortcut {shortcutPath}");
            }
          }
        }
      }

      var notebooks = SolutionDeployment.GetNotebooks();
      foreach (var notebook in notebooks) {
        var sourceNotebook = SolutionDeployment.GetSourceNotebook(notebook.DisplayName);
        var targetNotebook = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(sourceNotebook.DisplayName) &&
                                                                (item.Type == sourceNotebook.Type))).FirstOrDefault();

        ItemDefinition notebookDefiniton = itemDefinitionFactory.UpdateItemDefinitionPart(notebook.Definition,
                                                                                          "notebook-content.py",
                                                                                          notebookRedirects);

        if (targetNotebook != null) {
          // update existing notebook
          await appLogger.LogSubstep($"Updating [{notebook.ItemName}]");
          var updateRequest = new UpdateItemDefinitionRequest(notebookDefiniton);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetNotebook.Id.Value, updateRequest);
        }
        else {
          // create item
          await appLogger.LogSubstep($"Creating [{notebook.ItemName}]");
          var createRequest = new CreateItemRequest(notebook.DisplayName, notebook.Type);
          createRequest.Definition = itemDefinitionFactory.UpdateItemDefinitionPart(notebook.Definition, "notebook-content.py", notebookRedirects);
          targetNotebook = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
          if (createRequest.DisplayName.Contains("Create")) {
            await appLogger.LogSubOperationStart($"Running  [{notebook.ItemName}]");
            await fabricRestApi.RunNotebook(targetWorkspace.Id, targetNotebook);
            await appLogger.LogOperationComplete();
          }
        }

        dataPipelineRedirects.Add(sourceNotebook.Id, targetNotebook.Id.Value.ToString());

      }

      var pipelines = SolutionDeployment.GetDataPipelines();
      foreach (var pipeline in pipelines) {
        var sourcePipeline = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "DataPipeline" &&
                                                                           item.DisplayName.Equals(pipeline.DisplayName)));

        var targetPipeline = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(pipeline.DisplayName) &&
                                                                (item.Type == pipeline.Type))).FirstOrDefault();

        ItemDefinition pipelineDefiniton = itemDefinitionFactory.UpdateItemDefinitionPart(pipeline.Definition,
                                                                                          "pipeline-content.json",
                                                                                          dataPipelineRedirects);

        if (pipelineDefiniton != null) {
          // update existing notebook
          await appLogger.LogSubstep($"Updating [{pipeline.ItemName}]");
          var updateRequest = new UpdateItemDefinitionRequest(pipelineDefiniton);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetPipeline.Id.Value, updateRequest);
        }
        else {
          // create item
          await appLogger.LogSubstep($"Creating [{pipeline.ItemName}]");
          var createRequest = new CreateItemRequest(pipeline.DisplayName, pipeline.Type);
          createRequest.Definition = pipelineDefiniton;
          targetPipeline = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

          // run pipeline if 'Create' is in its DislayName
          if (createRequest.DisplayName.Contains("Create")) {
            await appLogger.LogSubOperationStart($"Running  [{sourcePipeline.DisplayName}.DataPipeline]");
            await fabricRestApi.RunDataPipeline(targetWorkspace.Id, targetPipeline);
            await appLogger.LogOperationComplete();
          }

        }
      }

      var models = SolutionDeployment.GetSemanticModels();
      foreach (var model in models) {

        // ignore default semantic model for lakehouse
        if (!lakehouseNames.Contains(model.DisplayName)) {

          var sourceModel = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "SemanticModel" &&
                                                                         item.DisplayName == model.DisplayName));

          var targetModel = targetWorkspaceItems.Where(item => (item.Type == model.Type) &&
                                                               (item.DisplayName == model.DisplayName)).FirstOrDefault();

          // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
          var modelDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(model.Definition,
                                                                               "definition/expressions.tmdl",
                                                                               semanticModelRedirects);

          if (targetModel != null) {
            await appLogger.LogSubstep($"Updating [{model.ItemName}]");
            // update existing model
            var updateRequest = new UpdateItemDefinitionRequest(modelDefinition);
            await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetModel.Id.Value, updateRequest);
          }
          else {
            await appLogger.LogSubstep($"Creating [{model.ItemName}]");
            var createRequest = new CreateItemRequest(model.DisplayName, model.Type);
            createRequest.Definition = modelDefinition;
            targetModel = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

            await CreateAndBindSemanticModelConnecton(targetWorkspace, targetModel.Id.Value);
          }

          // track mapping between source semantic model and target semantic model
          reportRedirects.Add(sourceModel.Id, targetModel.Id.Value.ToString());

        }

      }

      // reports
      var reports = SolutionDeployment.GetReports();
      foreach (var report in reports) {

        var targetReport = targetWorkspaceItems.FirstOrDefault(item => (item.Type == "Report" &&
                                                                        item.DisplayName == report.DisplayName));

        // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
        var reportDefinition = itemDefinitionFactory.UpdateReportDefinitionWithRedirection(report.Definition,
                                                                                           targetWorkspace.Id,
                                                                                           reportRedirects);

        // update report.json with report title customization
        reportDefinition = CustomizeReportTitle(reportDefinition, report.DisplayName, SolutionDeployment);

        if (targetReport != null) {
          // update existing report
          await appLogger.LogSubstep($"Updating [{report.ItemName}]");
          var updateRequest = new UpdateItemDefinitionRequest(reportDefinition);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetReport.Id.Value, updateRequest);
        }
        else {
          // use item definition to create clone in target workspace
          await appLogger.LogSubstep($"Creating [{report.ItemName}]");
          var createRequest = new CreateItemRequest(report.DisplayName, report.Type);
          createRequest.Definition = reportDefinition;
          targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
        }
      }

      // delete orphaned items
      if (DeleteOrphanedItems) {

        List<string> sourceWorkspaceItemNames = new List<string>();
        sourceWorkspaceItemNames.AddRange(
          sourceWorkspaceItems.Select(item => $"{item.DisplayName}.{item.Type}")
        );

        var lakehouseNamesInTarget = targetWorkspaceItems.Where(item => item.Type == "Lakehouse").Select(item => item.DisplayName).ToList();

        foreach (var item in targetWorkspaceItems) {
          string itemName = $"{item.DisplayName}.{item.Type}";
          if (!sourceWorkspaceItemNames.Contains(itemName) &&
             (item.Type != "SQLEndpoint") &&
             !(item.Type == "SemanticModel" && lakehouseNamesInTarget.Contains(item.DisplayName))) {
            try {
              await appLogger.LogSubstep($"Deleting [{itemName}]");
              await fabricRestApi.DeleteItem(targetWorkspace.Id, item);
            }
            catch {
              await appLogger.LogSubstep($"Could not delete [{itemName}]");

            }
          }
        }
      }

      await appLogger.LogStep("Solution update complete");
    }

    public async Task UpdateReportsFromExport(string ExportName, string TargetWorkspaceName, SolutionDeploymentPlan SolutionDeployment, bool DeleteOrphanedItems = false) {

      var targetWorkspace = await fabricRestApi.GetWorkspaceByName(TargetWorkspaceName);
      var targetWorkspaceItems = await fabricRestApi.GetWorkspaceItems(targetWorkspace.Id);

      await appLogger.LogStep($"Processing report item updates");

      List<string> lakehouseNames = SolutionDeployment.GetLakehouses().Select(item => item.DisplayName).ToList();
      var reportRedirects = new Dictionary<string, string>();

      var models = SolutionDeployment.GetSemanticModels();

      foreach (var model in models) {

        var sourceModel = SolutionDeployment.GetSourceSemanticModel(model.DisplayName);

        // ignore default semantic model for lakehouse
        if (!lakehouseNames.Contains(sourceModel.DisplayName)) {

          var targetModel = targetWorkspaceItems.Where(item => (item.Type == sourceModel.Type) &&
                                                               (item.DisplayName == sourceModel.DisplayName)).FirstOrDefault();

          if (targetModel != null) {
            reportRedirects.Add(sourceModel.Id, targetModel.Id.Value.ToString());
          }

        }

        // reports
        var reports = SolutionDeployment.GetReports();
        foreach (var report in reports) {

          var sourceReport = SolutionDeployment.GetSourceReport(report.DisplayName);

          var targetReport = targetWorkspaceItems.FirstOrDefault(item => (item.Type == "Report" &&
                                                                          item.DisplayName == report.DisplayName));

          // update definition.pbir to point to correct semantic model
          var reportDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(report.Definition,
                                                                               "definition.pbir",
                                                                               reportRedirects);

          // update report.json with report title customiztions
          reportDefinition = CustomizeReportTitle(reportDefinition, sourceReport.DisplayName, SolutionDeployment);


          if (targetReport != null) {
            // update existing report
            await appLogger.LogSubstep($"Updating [{sourceReport.DisplayName}.{sourceReport.Type}]");
            var updateRequest = new UpdateItemDefinitionRequest(reportDefinition);
            await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetReport.Id.Value, updateRequest);
          }
          else {
            // use item definition to create clone in target workspace
            await appLogger.LogSubstep($"Creating [{sourceReport.DisplayName}.{sourceReport.Type}]");
            var createRequest = new CreateItemRequest(sourceReport.DisplayName, sourceReport.Type);
            createRequest.Definition = reportDefinition;
            targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
          }

        }

      }
      await appLogger.LogStep("Solution update complete");

    }

    // local export logic

    public async Task ExportFromWorkspace(string SourceWorkspace, string ExportName, string Comment) {
      await itemDefinitionFactory.ExportFromWorkspace(SourceWorkspace, ExportName, Comment);
    }

    public async Task<Workspace> DeployFromLocalExport(string ExportName, string TargetWorkspaceName, DeploymentPlan Deployment) {

      await appLogger.LogSolution($"Deploying from export [{ExportName}] to workspace [{TargetWorkspaceName}]");

      await DisplayDeploymentParameters(Deployment);

      var solutionDeployment = await itemDefinitionFactory.GetSolutionDeploymentFromExport(ExportName, Deployment);

      await DeployFromExport(ExportName, TargetWorkspaceName, solutionDeployment);

      var targetWorkspace = await fabricRestApi.GetWorkspaceByName(TargetWorkspaceName);

      return targetWorkspace;

    }
 
    public async Task UpdateFromLocalExport(string ExportName, string TargetWorkspaceName, DeploymentPlan Deployment, bool DeleteOrphanedItems = false) {

      await appLogger.LogSolution($"Updating from export [{ExportName}] to workspace [{TargetWorkspaceName}]");

      await DisplayDeploymentParameters(Deployment);

      var solutionDeployment = await itemDefinitionFactory.GetSolutionDeploymentFromExport(ExportName, Deployment);

      var connectionRedirects = new Dictionary<string, string>();
      var sqlEndPointIds = new Dictionary<string, Item>();
      var lakehouseNames = new List<string>();
      var shortcutRedirects = new Dictionary<string, string>();
      var notebookRedirects = new Dictionary<string, string>();
      var dataPipelineRedirects = new Dictionary<string, string>();
      var semanticModelRedirects = new Dictionary<string, string>();
      var reportRedirects = new Dictionary<string, string>();

      var sourceWorkspaceItems = solutionDeployment.DeployConfig.SourceItems;

      var targetWorkspace = await fabricRestApi.GetWorkspaceByName(TargetWorkspaceName);
      var targetWorkspaceItems = await fabricRestApi.GetWorkspaceItems(targetWorkspace.Id);

      var targetWorkspaceDesciption = solutionDeployment.DeployConfig.SourceWorkspaceDescription;
      await fabricRestApi.UpdateWorkspaceDescription(targetWorkspace.Id, targetWorkspaceDesciption);

      connectionRedirects = await GetWorkspaceConnectionRedirects(solutionDeployment.DeployConfig.SourceConnections, targetWorkspace.Id, solutionDeployment);

      // copy connections dictionary for shortcuts, notebooks and data pipelines
      // make deep copy of connectionRedirects for starting point for other redirects
      shortcutRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      notebookRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      semanticModelRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;
      dataPipelineRedirects = connectionRedirects.ToDictionary(entry => entry.Key, entry => entry.Value); ;

      notebookRedirects.Add(solutionDeployment.GetSourceWorkspaceId(), targetWorkspace.Id.ToString());
      dataPipelineRedirects.Add(solutionDeployment.GetSourceWorkspaceId(), targetWorkspace.Id.ToString());

      await appLogger.LogStep($"Processing workspace item updates");

      var lakehouses = solutionDeployment.GetLakehouses();
      foreach (var lakehouse in lakehouses) {
        var sourceLakehouse = solutionDeployment.GetSourceLakehouse(lakehouse.DisplayName);
        var targetLakehouse = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(lakehouse.DisplayName) &&
                                                                 (item.Type == lakehouse.Type))).FirstOrDefault();

        if (targetLakehouse != null) {
          // update item - nothing to do for lakehouse        
        }
        else {
          // create item
          await appLogger.LogSubstep($"Creating [{lakehouse.ItemName}]");
          targetLakehouse = await fabricRestApi.CreateLakehouse(targetWorkspace.Id, lakehouse.DisplayName);
        }

        var targetLakehouseSqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(targetWorkspace.Id, targetLakehouse.Id.Value);

        // add lakehouse names to detect lakehouse default semantic model
        lakehouseNames.Add(targetLakehouse.DisplayName);

        // add redirects for lakehouse id
        notebookRedirects.Add(sourceLakehouse.Id, targetLakehouse.Id.Value.ToString());
        dataPipelineRedirects.Add(sourceLakehouse.Id, targetLakehouse.Id.Value.ToString());

        // add redirect for sql endpoint database name 
        semanticModelRedirects.Add(sourceLakehouse.Database, targetLakehouseSqlEndpoint.Id);

        // add redirect for sql endpoint server location
        if (!semanticModelRedirects.Keys.Contains(sourceLakehouse.Server)) {
          // only add sql endpoint server location once because it has same value for all lakehouses in the same workspace
          semanticModelRedirects.Add(sourceLakehouse.Server, targetLakehouseSqlEndpoint.ConnectionString);
        }

        // inspect shortcuts - currnently no logic to add missing shortcuts in UPDATE workflow
        var targetShortcuts = await fabricRestApi.GetLakehouseShortcuts(targetWorkspace.Id, targetLakehouse.Id.Value);
        var targetShortcutPaths = targetShortcuts.Select(shortcut => shortcut.Path + "/" + shortcut.Name).ToList();
        if (sourceLakehouse.Shortcuts != null) {
          foreach (var shortcut in sourceLakehouse.Shortcuts) {
            string shortcutPath = shortcut.Path + "/" + shortcut.Name;
            if (!targetShortcutPaths.Contains(shortcutPath)) {
              await appLogger.LogSubstep($"New shortcut {shortcutPath}");
            }
          }
        }
      }

      var notebooks = solutionDeployment.GetNotebooks();
      foreach (var notebook in notebooks) {
        var sourceNotebook = solutionDeployment.GetSourceNotebook(notebook.DisplayName);
        var targetNotebook = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(sourceNotebook.DisplayName) &&
                                                                (item.Type == sourceNotebook.Type))).FirstOrDefault();

        ItemDefinition notebookDefiniton = itemDefinitionFactory.UpdateItemDefinitionPart(notebook.Definition,
                                                                                          "notebook-content.py",
                                                                                          notebookRedirects);

        if (targetNotebook != null) {
          // update existing notebook
          await appLogger.LogSubstep($"Updating [{notebook.ItemName}]");
          var updateRequest = new UpdateItemDefinitionRequest(notebookDefiniton);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetNotebook.Id.Value, updateRequest);
        }
        else {
          // create item
          await appLogger.LogSubstep($"Creating [{notebook.ItemName}]");
          var createRequest = new CreateItemRequest(notebook.DisplayName, notebook.Type);
          createRequest.Definition = itemDefinitionFactory.UpdateItemDefinitionPart(notebook.Definition, "notebook-content.py", notebookRedirects);
          targetNotebook = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
          if (createRequest.DisplayName.Contains("Create")) {
            await appLogger.LogSubOperationStart($"Running  [{notebook.ItemName}]");
            await fabricRestApi.RunNotebook(targetWorkspace.Id, targetNotebook);
            await appLogger.LogOperationComplete();
          }
        }

        dataPipelineRedirects.Add(sourceNotebook.Id, targetNotebook.Id.Value.ToString());

      }

      var pipelines = solutionDeployment.GetDataPipelines();
      foreach (var pipeline in pipelines) {
        var sourcePipeline = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "DataPipeline" &&
                                                                           item.DisplayName.Equals(pipeline.DisplayName)));

        var targetPipeline = targetWorkspaceItems.Where(item => (item.DisplayName.Equals(pipeline.DisplayName) &&
                                                                (item.Type == pipeline.Type))).FirstOrDefault();

        ItemDefinition pipelineDefiniton = itemDefinitionFactory.UpdateItemDefinitionPart(pipeline.Definition,
                                                                                          "pipeline-content.json",
                                                                                          dataPipelineRedirects);

        if (pipelineDefiniton != null) {
          // update existing notebook
          await appLogger.LogSubstep($"Updating [{pipeline.ItemName}]");
          var updateRequest = new UpdateItemDefinitionRequest(pipelineDefiniton);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetPipeline.Id.Value, updateRequest);
        }
        else {
          // create item
          await appLogger.LogSubstep($"Creating [{pipeline.ItemName}]");
          var createRequest = new CreateItemRequest(pipeline.DisplayName, pipeline.Type);
          createRequest.Definition = pipelineDefiniton;
          targetPipeline = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

          // run pipeline if 'Create' is in its DislayName
          if (createRequest.DisplayName.Contains("Create")) {
            await appLogger.LogSubOperationStart($"Running  [{sourcePipeline.DisplayName}.DataPipeline]");
            await fabricRestApi.RunDataPipeline(targetWorkspace.Id, targetPipeline);
            await appLogger.LogOperationComplete();
          }

        }
      }

      var models = solutionDeployment.GetSemanticModels();
      foreach (var model in models) {

        // ignore default semantic model for lakehouse
        if (!lakehouseNames.Contains(model.DisplayName)) {

          var sourceModel = sourceWorkspaceItems.FirstOrDefault(item => (item.Type == "SemanticModel" &&
                                                                         item.DisplayName == model.DisplayName));

          var targetModel = targetWorkspaceItems.Where(item => (item.Type == model.Type) &&
                                                               (item.DisplayName == model.DisplayName)).FirstOrDefault();

          // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
          var modelDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(model.Definition,
                                                                               "definition/expressions.tmdl",
                                                                               semanticModelRedirects);

          if (targetModel != null) {
            await appLogger.LogSubstep($"Updating [{model.ItemName}]");
            // update existing model
            var updateRequest = new UpdateItemDefinitionRequest(modelDefinition);
            await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetModel.Id.Value, updateRequest);
          }
          else {
            await appLogger.LogSubstep($"Creating [{model.ItemName}]");
            var createRequest = new CreateItemRequest(model.DisplayName, model.Type);
            createRequest.Definition = modelDefinition;
            targetModel = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);

            await CreateAndBindSemanticModelConnecton(targetWorkspace, targetModel.Id.Value);
          }

          // track mapping between source semantic model and target semantic model
          reportRedirects.Add(sourceModel.Id, targetModel.Id.Value.ToString());

        }

      }

      // reports
      var reports = solutionDeployment.GetReports();
      foreach (var report in reports) {

        var targetReport = targetWorkspaceItems.FirstOrDefault(item => (item.Type == "Report" &&
                                                                        item.DisplayName == report.DisplayName));

        // update expressions.tmdl with SQL endpoint info for lakehouse in feature workspace
        var reportDefinition = itemDefinitionFactory.UpdateReportDefinitionWithRedirection(report.Definition,
                                                                                           targetWorkspace.Id,
                                                                                           reportRedirects);

        // update report.json with report title customization
        reportDefinition = CustomizeReportTitle(reportDefinition, report.DisplayName, solutionDeployment);

        if (targetReport != null) {
          // update existing report
          await appLogger.LogSubstep($"Updating [{report.ItemName}]");
          var updateRequest = new UpdateItemDefinitionRequest(reportDefinition);
          await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetReport.Id.Value, updateRequest);
        }
        else {
          // use item definition to create clone in target workspace
          await appLogger.LogSubstep($"Creating [{report.ItemName}]");
          var createRequest = new CreateItemRequest(report.DisplayName, report.Type);
          createRequest.Definition = reportDefinition;
          targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
        }
      }

      // delete orphaned items
      if (DeleteOrphanedItems) {

        List<string> sourceWorkspaceItemNames = new List<string>();
        sourceWorkspaceItemNames.AddRange(
          sourceWorkspaceItems.Select(item => $"{item.DisplayName}.{item.Type}")
        );

        var lakehouseNamesInTarget = targetWorkspaceItems.Where(item => item.Type == "Lakehouse").Select(item => item.DisplayName).ToList();

        foreach (var item in targetWorkspaceItems) {
          string itemName = $"{item.DisplayName}.{item.Type}";
          if (!sourceWorkspaceItemNames.Contains(itemName) &&
             (item.Type != "SQLEndpoint") &&
             !(item.Type == "SemanticModel" && lakehouseNamesInTarget.Contains(item.DisplayName))) {
            try {
              await appLogger.LogSubstep($"Deleting [{itemName}]");
              await fabricRestApi.DeleteItem(targetWorkspace.Id, item);
            }
            catch {
              await appLogger.LogSubstep($"Could not delete [{itemName}]");

            }
          }
        }
      }

      await appLogger.LogStep("Solution update complete");
    }

    public async Task UpdateReportsFromLocalExport(string ExportName, string TargetWorkspaceName, DeploymentPlan Deployment, bool DeleteOrphanedItems = false) {

      await appLogger.LogSolution($"Updating reports from export [{ExportName}] to workspace [{TargetWorkspaceName}]");
  
      var solutionDeployment = await itemDefinitionFactory.GetSolutionDeploymentFromExport(ExportName, Deployment);

      var targetWorkspace = await fabricRestApi.GetWorkspaceByName(TargetWorkspaceName);
      var targetWorkspaceItems = await fabricRestApi.GetWorkspaceItems(targetWorkspace.Id);

      await appLogger.LogStep($"Processing report item updates");

      List<string> lakehouseNames = solutionDeployment.GetLakehouses().Select(item => item.DisplayName).ToList();
      var reportRedirects = new Dictionary<string, string>();

      var models = solutionDeployment.GetSemanticModels();

      foreach (var model in models) {

        var sourceModel = solutionDeployment.GetSourceSemanticModel(model.DisplayName);

        // ignore default semantic model for lakehouse
        if (!lakehouseNames.Contains(sourceModel.DisplayName)) {

          var targetModel = targetWorkspaceItems.Where(item => (item.Type == sourceModel.Type) &&
                                                               (item.DisplayName == sourceModel.DisplayName)).FirstOrDefault();

          if (targetModel != null) {
            reportRedirects.Add(sourceModel.Id, targetModel.Id.Value.ToString());
          }

        }

        // reports
        var reports = solutionDeployment.GetReports();
        foreach (var report in reports) {

          var sourceReport = solutionDeployment.GetSourceReport(report.DisplayName);

          var targetReport = targetWorkspaceItems.FirstOrDefault(item => (item.Type == "Report" &&
                                                                          item.DisplayName == report.DisplayName));

          // update definition.pbir to point to correct semantic model
          var reportDefinition = itemDefinitionFactory.UpdateItemDefinitionPart(report.Definition,
                                                                               "definition.pbir",
                                                                               reportRedirects);

          // update report.json with report title customiztions
          reportDefinition = CustomizeReportTitle(reportDefinition, sourceReport.DisplayName, Deployment);


          if (targetReport != null) {
            // update existing report
            await appLogger.LogSubstep($"Updating [{sourceReport.DisplayName}.{sourceReport.Type}]");
            var updateRequest = new UpdateItemDefinitionRequest(reportDefinition);
            await fabricRestApi.UpdateItemDefinition(targetWorkspace.Id, targetReport.Id.Value, updateRequest);
          }
          else {
            // use item definition to create clone in target workspace
            await appLogger.LogSubstep($"Creating [{sourceReport.DisplayName}.{sourceReport.Type}]");
            var createRequest = new CreateItemRequest(sourceReport.DisplayName, sourceReport.Type);
            createRequest.Definition = reportDefinition;
            targetReport = await fabricRestApi.CreateItem(targetWorkspace.Id, createRequest);
          }

        }

      }
      await appLogger.LogStep("Solution update complete");

    }

    public DeploymentConfiguration GetSolutionExport(string ExportName) {
      return itemDefinitionFactory.GetSolutionExport(ExportName);
    }
    
    public async Task<ExportDetails> GetExportDetail(string ExportName) {

      var export = itemDefinitionFactory.GetSolutionExport(ExportName);
      string solutionName = export.SolutionName;
      var allWorkspaces = await fabricRestApi.GetWorkspaces();

      var targetWorkspaces = allWorkspaces.Where(workspace => !workspace.DisplayName.Contains("Solution"))
                                          .Where(workspace => workspace.Description == solutionName).ToList();
      return new ExportDetails {
        Export = export,
        TargetWorkspaces = targetWorkspaces
      };
    }

    // Azure Dev Ops logic

    public async Task<List<TeamProjectReference>> GetAdoProjects() {

      return await adoProjectManager.GetProjects();

    }

    public async Task<List<AdoProjectRow>> GetAdoProjectRow() {

      var projectReows = new List<AdoProjectRow>();

      var adoProjects = await adoProjectManager.GetProjects();
      foreach (var adoProject in adoProjects) {
        var branchNames = await adoProjectManager.GetProjectBranches(adoProject.Name);
        var exportNames = branchNames.Where(branch => branch != "main").ToList();
        projectReows.Add(new AdoProjectRow {
          AdoProject = adoProject,
          ExportNames = exportNames
        });
      }

      return projectReows;

    }

    public async Task DeleteAdoProject(string ProjectId) {
      await adoProjectManager.DeleteProject(new Guid(ProjectId));
    }

    public async Task<List<string>> GetAdoProjectExports(string ProjectName) {
      return (await adoProjectManager.GetProjectBranches(ProjectName)).Where(export => export != "main").ToList();
    }
  
    public async Task<SolutionDeploymentPlan> GetSolutionDeploymentFromAdoExport(string ProjectName, DeploymentPlan Deployment, string BranchName) {

      var solutionDeploymentPlan = new SolutionDeploymentPlan(Deployment);

      await appLogger.LogStep($"Loading item definition files from ADO export");

      await appLogger.LogSubstep($"Loading from ADO branch [{BranchName}]");

      var itemDefinitionFiles = await adoProjectManager.GetItemDefinitionFilesFromGitRepo(ProjectName, BranchName);

      var items = itemDefinitionFiles.OrderBy(item => item.FullPath);

      DeploymentItem currentItem = null;

      foreach (var item in items) {
        if (item.FileName == ".platform") {
          await appLogger.LogSubstep($"Loading [{item.ItemName}]");
          FabricPlatformFile platformFile = JsonSerializer.Deserialize<FabricPlatformFile>(item.Content);
          PlatformFileMetadata itemMetadata = platformFile.metadata;
          PlatformFileConfig config = platformFile.config;

          currentItem = new DeploymentItem {
            DisplayName = itemMetadata.displayName,
            LogicalId = config.logicalId,
            Type = itemMetadata.type,
            Definition = new ItemDefinition(new List<ItemDefinitionPart>())
          };

          solutionDeploymentPlan.DeploymentItems.Add(currentItem);
        }
        else {
          string encodedContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Content));
          currentItem.Definition.Parts.Add(
            new ItemDefinitionPart(item.Path, encodedContent, PayloadType.InlineBase64)
          );
        }
      }

      await appLogger.LogSubstep($"Loading [deploy.config.json]");
      DeploymentConfiguration deployConfig = await adoProjectManager.GetDeployConfigFromGitRepo(ProjectName, BranchName);

      solutionDeploymentPlan.DeployConfig = deployConfig;

      return solutionDeploymentPlan;
    }

    public async Task DeployFromAdoExport(string ProjectName, string ExportName, string TargetWorkspaceName, DeploymentPlan Deployment) {

      await appLogger.LogSolution($"Deploying from ADO export [{ExportName}] to workspace [{TargetWorkspaceName}]");

      await DisplayDeploymentParameters(Deployment);

      var solutionDeployment = await GetSolutionDeploymentFromAdoExport(ProjectName, Deployment, ExportName);

      await DeployFromExport(ProjectName, TargetWorkspaceName, solutionDeployment);

    }

    public async Task UpdateFromAdoExport(string ProjectName, string ExportName, string TargetWorkspaceName, DeploymentPlan Deployment, bool DeleteOrphanedItems = false) {

      await appLogger.LogSolution($"Updating from ADO export [{ExportName}] to workspace [{TargetWorkspaceName}]");

      await DisplayDeploymentParameters(Deployment);

      var solutionDeployment = await GetSolutionDeploymentFromAdoExport(ProjectName, Deployment, ExportName);

      await UpdateFromExport(ExportName, TargetWorkspaceName, solutionDeployment, DeleteOrphanedItems: true);

    }

    public async Task UpdateReportsFromAdoExport(string ProjectName, string ExportName, string TargetWorkspaceName, DeploymentPlan Deployment, bool DeleteOrphanedItems = false) {

      await appLogger.LogSolution($"Updating report from ADO export [{ExportName}] to workspace [{TargetWorkspaceName}]");

      await DisplayDeploymentParameters(Deployment);

      var solutionDeployment = await GetSolutionDeploymentFromAdoExport(ProjectName, Deployment, ExportName);

      await UpdateReportsFromExport(ExportName, TargetWorkspaceName, solutionDeployment, DeleteOrphanedItems: true);

    }

    public async Task<string> GetSuggestedExportName(string ProjectName) {
      string BranchName = $"{ProjectName} {DateTime.Now.ToString("yyyy-MM-dd")}";
      if (await adoProjectManager.BranchAlreadyExist(ProjectName, BranchName)) {
        BranchName = $"{ProjectName} {DateTime.Now.ToString("yyyy-MM-dd-HH-mm")}";
      }
      BranchName = BranchName.Replace(" ", "_");
      return BranchName;
    }

    public async Task ExportFromWorkspaceToAdo(string WorkspaceName, string ProjectName, string BranchName, string Comment) {

      var project = await adoProjectManager.EnsureProjectExists(ProjectName);

      await appLogger.LogSolution($"Exporting [{WorkspaceName}] to ADO project branch [{BranchName}]");

      await appLogger.LogStep($"Getting item definition files from source workspace [{WorkspaceName}]");

      var changes = new List<GitChange>();

      var workspace = await fabricRestApi.GetWorkspaceByName(WorkspaceName);
      var items = await fabricRestApi.GetWorkspaceItems(workspace.Id);

      var lakehouses = await fabricRestApi.GetWorkspaceItems(workspace.Id, "Lakehouse");
      foreach (var lakehouse in lakehouses) {

        // fetch item definition from workspace
        var platformFile = new FabricPlatformFile {
          schema = "https://developer.microsoft.com/json-schemas/fabric/gitIntegration/platformProperties/2.0.0/schema.json",
          config = new PlatformFileConfig {
            logicalId = Guid.Empty.ToString(),
            version = "2.0"
          },
          metadata = new PlatformFileMetadata {
            displayName = lakehouse.DisplayName,
            type = "Lakehouse"
          }
        };

        string platformFileContent = JsonSerializer.Serialize(platformFile, jsonSerializerOptions);
        string platformFileName = ".platform";
        // write item definition files to local folder
        string targetFolder = lakehouse.DisplayName + "." + lakehouse.Type;
        await appLogger.LogSubstep($"Getting item definition files for [{targetFolder}]");

        changes.Add(new GitChange {
          ChangeType = VersionControlChangeType.Add,
          Item = new GitItem {
            Path = $"{lakehouse.DisplayName}.Lakehouse/{platformFileName}"
          },
          NewContent = new ItemContent {
            Content = Convert.ToBase64String(Encoding.ASCII.GetBytes(platformFileContent)),
            ContentType = ItemContentType.Base64Encoded
          }
        });

      }

      var lakehouseNames = lakehouses.Select(lakehouse => lakehouse.DisplayName).ToList();

      // list of items types that should be exported
      List<ItemType> itemTypesForExport = new List<ItemType>() {
      ItemType.Notebook, ItemType.DataPipeline, ItemType.SemanticModel, ItemType.Report
    };

      foreach (var item in items) {

        // only include supported item types
        if (itemTypesForExport.Contains(item.Type)) {

          // filter out lakehouse default semntic models
          if ((item.Type != ItemType.SemanticModel) ||
              (!lakehouseNames.Contains(item.DisplayName))) {

            // fetch item definition from workspace
            var definition = await fabricRestApi.GetItemDefinition(workspace.Id, item.Id.Value);

            // write item definition files to local folder
            string targetFolder = item.DisplayName + "." + item.Type;

            await appLogger.LogSubstep($"Getting item definition files for [{targetFolder}]");

            foreach (var part in definition.Parts) {
              changes.Add(new GitChange {
                ChangeType = VersionControlChangeType.Add,
                Item = new GitItem {
                  Path = targetFolder + "/" + part.Path
                },
                NewContent = new ItemContent {
                  Content = part.Payload,
                  ContentType = ItemContentType.Base64Encoded
                }
              });
            }

          }

        }

      }

      await appLogger.LogStep($"Generating [deploy.config.json]");

      var config = await itemDefinitionFactory.GenerateDeployConfigFile(BranchName, Comment, workspace, items);

      changes.Add(new GitChange {
        ChangeType = VersionControlChangeType.Add,
        Item = new GitItem {
          Path = "/deploy.config.json"
        },
        NewContent = new ItemContent {
          Content = Convert.ToBase64String(Encoding.ASCII.GetBytes(config)),
          ContentType = ItemContentType.Base64Encoded
        }
      });

      await adoProjectManager.PushChangesToGitRepo(ProjectName, Comment, changes, BranchName);
        
      await appLogger.LogStep("Solution folder export process complete");

    }

    public JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

  }
}
