using FabricDeploymentManager.Models;
using Microsoft.Fabric.Api.Core.Models;
using System.Text;
using System.Text.Json;

namespace FabricDeploymentManager.Services {

  public class ItemDefinitionFactory {

    private readonly IWebHostEnvironment hostingEnvironment;
    private SignalRLogger appLogger;
    private FabricRestApi fabricRestApi;

    public ItemDefinitionFactory(IWebHostEnvironment HostingEnvironment,
                                 SignalRLogger AppLogger,
                                 FabricRestApi FabricRestApi) {

      hostingEnvironment = HostingEnvironment;
      appLogger = AppLogger;
      fabricRestApi = FabricRestApi;
    }

    public ItemDefinition UpdateItemDefinitionPart(ItemDefinition ItemDefinition, string PartPath, Dictionary<string, string> SearchReplaceText) {
      var itemPart = ItemDefinition.Parts.Where(part => part.Path == PartPath).FirstOrDefault();
      if (itemPart != null) {
        ItemDefinition.Parts.Remove(itemPart);
        itemPart.Payload = SearchAndReplaceInPayload(itemPart.Payload, SearchReplaceText);
        ItemDefinition.Parts.Add(itemPart);
      }
      return ItemDefinition;
    }

    public string SearchAndReplaceInPayload(string Payload, Dictionary<string, string> SearchReplaceText) {
      byte[] PayloadBytes = Convert.FromBase64String(Payload);
      string PayloadContent = Encoding.UTF8.GetString(PayloadBytes, 0, PayloadBytes.Length);
      foreach (var entry in SearchReplaceText.Keys) {
        PayloadContent = PayloadContent.Replace(entry, SearchReplaceText[entry]);
      }
      return Convert.ToBase64String(Encoding.UTF8.GetBytes(PayloadContent));
    }

    public string GetTemplateFile(string Path) {
      string filePath = $@"{hostingEnvironment.WebRootPath}\templates\ItemDefinitionTemplateFiles\{Path}";

      return File.ReadAllText(filePath);
    }

    private string GetPartPath(string ItemFolderPath, string FilePath) {
      int ItemFolderPathOffset = ItemFolderPath.Length + 1;
      return FilePath.Substring(ItemFolderPathOffset).Replace("\\", "/");
    }

    public ItemDefinitionPart CreateInlineBase64Part(string Path, string Payload) {
      string base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(Payload));
      return new ItemDefinitionPart(Path, base64Payload, PayloadType.InlineBase64);
    }

    public CreateItemRequest GetSemanticModelCreateRequestFromBim(string DisplayName, string BimFile) {

      string part1FileContent = GetTemplateFile(@"SemanticModels\definition.pbism");
      string part2FileContent = GetTemplateFile($@"SemanticModels\{BimFile}");

      var createRequest = new CreateItemRequest(DisplayName, ItemType.SemanticModel);

      createRequest.Definition =
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbism", part1FileContent),
        CreateInlineBase64Part("model.bim", part2FileContent)
        });

      return createRequest;
    }

    public UpdateItemDefinitionRequest GetSemanticModelUpdateRequestFromBim(string DisplayName, string BimFile) {

      string part1FileContent = GetTemplateFile(@"SemanticModels\definition.pbism");
      string part2FileContent = GetTemplateFile($@"SemanticModels\{BimFile}");

      return new UpdateItemDefinitionRequest(
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbism", part1FileContent),
        CreateInlineBase64Part("model.bim", part2FileContent)
        }));
    }

    public CreateItemRequest GetSemanticDirectLakeModelCreateRequestFromBim(string DisplayName, string BimFile, string SqlEndpointServer, string SqlEndpointDatabase) {

      string part1FileContent = GetTemplateFile(@"SemanticModels\definition.pbism");
      string part2FileContent = GetTemplateFile($@"SemanticModels\{BimFile}")
                                                 .Replace("{SQL_ENDPOINT_SERVER}", SqlEndpointServer)
                                                 .Replace("{SQL_ENDPOINT_DATABASE}", SqlEndpointDatabase);

      var createRequest = new CreateItemRequest(DisplayName, ItemType.SemanticModel);

      createRequest.Definition =
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbism", part1FileContent),
        CreateInlineBase64Part("model.bim", part2FileContent)
        });

      return createRequest;
    }

    public CreateItemRequest GetReportCreateRequestFromReportJson(Guid SemanticModelId, string DisplayName, string ReportJson) {

      string part1FileContent = GetTemplateFile(@"Reports\definition.pbir").Replace("{SEMANTIC_MODEL_ID}", SemanticModelId.ToString());
      string part2FileContent = GetTemplateFile($@"Reports\{ReportJson}");
      string part3FileContent = GetTemplateFile(@"Reports\StaticResources\SharedResources\BaseThemes\CY24SU02.json");

      var createRequest = new CreateItemRequest(DisplayName, ItemType.Report);

      createRequest.Definition =
            new ItemDefinition(new List<ItemDefinitionPart>() {
            CreateInlineBase64Part("definition.pbir", part1FileContent),
            CreateInlineBase64Part("report.json", part2FileContent),
            CreateInlineBase64Part("StaticResources/SharedResources/BaseThemes/CY24SU02.json", part3FileContent),
            });

      return createRequest;

    }

    public UpdateItemDefinitionRequest GetUpdateRequestFromReportJson(Guid SemanticModelId, string DisplayName, string ReportJson) {

      string part1FileContent = GetTemplateFile(@"Reports\definition.pbir").Replace("{SEMANTIC_MODEL_ID}", SemanticModelId.ToString());
      string part2FileContent = GetTemplateFile($@"Reports\{ReportJson}");
      string part3FileContent = GetTemplateFile(@"Reports\StaticResources\SharedResources\BaseThemes\CY24SU02.json");
      string part4FileContent = GetTemplateFile(@"Reports\StaticResources\SharedResources\BuiltInThemes\NewExecutive.json");

      return new UpdateItemDefinitionRequest(
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbir", part1FileContent),
        CreateInlineBase64Part("report.json", part2FileContent),
        CreateInlineBase64Part("StaticResources/SharedResources/BaseThemes/CY24SU02.json", part3FileContent),
        CreateInlineBase64Part("StaticResources/SharedResources/BuiltInThemes/NewExecutive.json", part4FileContent)
        }));
    }

    public CreateItemRequest GetCreateNotebookRequestFromPy(Guid WorkspaceId, Item Lakehouse, string DisplayName, string PyFile) {

      var pyContent = GetTemplateFile($@"Notebooks\{PyFile}").Replace("{WORKSPACE_ID}", WorkspaceId.ToString())
                                                             .Replace("{LAKEHOUSE_ID}", Lakehouse.Id.ToString())
                                                             .Replace("{LAKEHOUSE_NAME}", Lakehouse.DisplayName);

      var createRequest = new CreateItemRequest(DisplayName, ItemType.Notebook);

      createRequest.Definition =
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("notebook-content.py", pyContent)
        });

      return createRequest;

    }

    public CreateItemRequest GetCreateNotebookRequestFromIpynb(Guid WorkspaceId, Item Lakehouse, string DisplayName, string IpynbFile) {

      var ipynbContent = GetTemplateFile($@"Notebooks\{IpynbFile}").Replace("{WORKSPACE_ID}", WorkspaceId.ToString())
                                                                   .Replace("{LAKEHOUSE_ID}", Lakehouse.Id.ToString())
                                                                   .Replace("{LAKEHOUSE_NAME}", Lakehouse.DisplayName);

      var createRequest = new CreateItemRequest(DisplayName, ItemType.Notebook);

      createRequest.Definition =
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("notebook-content.ipynb", ipynbContent)
        });

      createRequest.Definition.Format = "ipynb";

      return createRequest;

    }

    public CreateItemRequest GetDataPipelineCreateRequest(string DisplayName, string PipelineDefinition) {

      var createRequest = new CreateItemRequest(DisplayName, ItemType.DataPipeline);

      createRequest.Definition =
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("pipeline-content.json", PipelineDefinition)
        });

      return createRequest;
    }

    public CreateItemRequest GetDataPipelineCreateRequestForLakehouse(string DisplayName, string CodeContent, string WorkspaceId, string LakehouseId, string ConnectionId) {

      var createRequest = new CreateItemRequest(DisplayName, ItemType.DataPipeline);

      CodeContent = CodeContent
        .Replace("{CONNECTION_ID}", ConnectionId)
        .Replace("{WORKSPACE_ID}", WorkspaceId)
        .Replace("{LAKEHOUSE_ID}", LakehouseId);

      createRequest.Definition =
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("pipeline-content.json", CodeContent)
        });

      return createRequest;
    }

    public CreateItemRequest GetDataPipelineCreateRequestForWarehouse(string DisplayName, string CodeContent, string WorkspaceId, string WarehouseId, string WarehouseConnectString) {

      var createRequest = new CreateItemRequest(DisplayName, ItemType.DataPipeline);

      CodeContent = CodeContent
        .Replace("{WORKSPACE_ID}", WorkspaceId)
        .Replace("{WAREHOUSE_ID}", WarehouseId)
        .Replace("{WAREHOUSE_CONNECT_STRING}", WarehouseConnectString);

      createRequest.Definition =
        new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("pipeline-content.json", CodeContent)
        });

      return createRequest;
    }

    public CreateItemRequest GetCreateItemRequestFromFolder(string ItemFolder) {

      string ItemFolderPath = $@"{hostingEnvironment.WebRootPath}\templates\ItemDefinitionTemplateFolders\{ItemFolder}";

      string metadataFilePath = ItemFolderPath + @"\.platform";
      string metadataFileContent = File.ReadAllText(metadataFilePath);
      PlatformFileMetadata item = JsonSerializer.Deserialize<FabricPlatformFile>(metadataFileContent).metadata;

      CreateItemRequest itemCreateRequest = new CreateItemRequest(item.displayName, item.type);

      var parts = new List<ItemDefinitionPart>();

      List<string> ItemDefinitionFiles = Directory.GetFiles(ItemFolderPath, "*", SearchOption.AllDirectories).ToList<string>();

      foreach (string ItemDefinitionFile in ItemDefinitionFiles) {

        string fileContentBase64 = Convert.ToBase64String(File.ReadAllBytes(ItemDefinitionFile));

        parts.Add(new ItemDefinitionPart(GetPartPath(ItemFolderPath, ItemDefinitionFile), fileContentBase64, "InlineBase64"));

      }

      itemCreateRequest.Definition = new ItemDefinition(parts);

      return itemCreateRequest;
    }

    public ItemDefinition UpdateReportDefinitionWithRedirection(ItemDefinition ReportDefinition, Guid WorkspaceId, Dictionary<string, string> ReportRedirects) {

      var pbirDefinitionPart = ReportDefinition.Parts.Where(part => part.Path == "definition.pbir").First();

      byte[] payloadBytes = Convert.FromBase64String(pbirDefinitionPart.Payload);
      string payloadContent = Encoding.UTF8.GetString(payloadBytes, 0, payloadBytes.Length);

      var pbirDefinition = JsonSerializer.Deserialize<ReportDefinitionFile>(payloadContent);

      if ((pbirDefinition.datasetReference.byPath != null) &&
          (pbirDefinition.datasetReference.byPath.path != null) &&
          (pbirDefinition.datasetReference.byPath.path.Length > 0)) {

        string targetModelName = pbirDefinition.datasetReference.byPath.path.Replace("../", "")
                                                                            .Replace(".SemanticModel", "");

        var targetModel = fabricRestApi.GetSemanticModelByName(WorkspaceId, targetModelName);

        ReportDefinition.Parts.Remove(pbirDefinitionPart);

        string reportDefinitionPartTemplate = GetTemplateFile(@"Reports\definition.pbir");
        string reportDefinitionPartContent = reportDefinitionPartTemplate.Replace("{SEMANTIC_MODEL_ID}", targetModel.Id.ToString());
        var reportDefinitionPart = CreateInlineBase64Part("definition.pbir", reportDefinitionPartContent);
        ReportDefinition.Parts.Add(reportDefinitionPart);
        return ReportDefinition;
      }
      else {
        return UpdateItemDefinitionPart(ReportDefinition, "definition.pbir", ReportRedirects);
      }

    }

    public ItemDefinition UpdateReportDefinitionWithSemanticModelId(ItemDefinition ItemDefinition, Guid TargetModelId) {
      var partDefinition = ItemDefinition.Parts.Where(part => part.Path == "definition.pbir").First();
      ItemDefinition.Parts.Remove(partDefinition);
      string reportDefinitionPartTemplate = GetTemplateFile(@"Reports\definition.pbir");
      string reportDefinitionPartContent = reportDefinitionPartTemplate.Replace("{SEMANTIC_MODEL_ID}", TargetModelId.ToString());
      var reportDefinitionPart = CreateInlineBase64Part("definition.pbir", reportDefinitionPartContent);
      ItemDefinition.Parts.Add(reportDefinitionPart);
      return ItemDefinition;
    }

    // export workspace item definitions to solution exports folder

    public async Task ExportFromWorkspace(string WorkspaceName, string ExportName, string Comment) {

      await appLogger.LogSolution($"Exporting workspace [{WorkspaceName}] to export [{ExportName}]");

      DeleteSolutionExportFolderContents(ExportName);

      var workspace = await fabricRestApi.GetWorkspaceByName(WorkspaceName);
      var items = await fabricRestApi.GetWorkspaceItems(workspace.Id);

      var lakehouseNames = items.Where(item => item.Type == ItemType.Lakehouse).ToList().Select(lakehouse => lakehouse.DisplayName).ToList();

      // list of items types that should be exported
      List<ItemType> itemTypesForExport = new List<ItemType>() {
        ItemType.Notebook, ItemType.DataPipeline, ItemType.SemanticModel, ItemType.Report
      };

      await appLogger.LogStep("Exporting item definitions");

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

            await appLogger.LogSubstep($"Exporting item definition for [{targetFolder}]");

            foreach (var part in definition.Parts) {
              WriteFileToSolutionExportsFolder(ExportName, targetFolder, part.Path, part.Payload);
            }

          }

        }

      }

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

        string platformFileContent = JsonSerializer.Serialize(platformFile);
        string platformFileName = ".platform";
        // write item definition files to local folder
        string targetFolder = lakehouse.DisplayName + "." + lakehouse.Type;
        await appLogger.LogSubstep($"Exporting item definition for [{targetFolder}]");

        WriteFileToSolutionExportsFolder(ExportName, targetFolder, platformFileName, platformFileContent, false);

      }

      await appLogger.LogSubstep($"Exporting [deploy.config.json]");

      var config = await GenerateDeployConfigFile(ExportName, Comment, workspace, items);

      WriteFileToSolutionExportsFolder(ExportName, "", "deploy.config.json", config, false);

      await appLogger.LogStep("Solution exporting process complete");

    }

    public void DeleteSolutionExportFolderContents(string ExportName) {

      string solutionsFolder = $@"{hostingEnvironment.WebRootPath}\templates\SolutionExports\{ExportName}";
      if (Directory.Exists(solutionsFolder)) {
        DirectoryInfo di = new DirectoryInfo(solutionsFolder);
        foreach (FileInfo file in di.GetFiles()) { file.Delete(); }
        foreach (DirectoryInfo dir in di.GetDirectories()) { dir.Delete(true); }
      }
    }

    public void DeleteSolutionExport(string ExportName) {
      string exportFolder = $@"{hostingEnvironment.WebRootPath}\templates\SolutionExports\{ExportName}";
      DirectoryInfo di = new DirectoryInfo(exportFolder);
      di.Delete(true);
    }

    public void WriteFileToSolutionExportsFolder(string SolutionName, string ItemFolder, string FilePath, string FileContent, bool ConvertFromBase64 = true) {

      if (ConvertFromBase64) {
        byte[] bytes = Convert.FromBase64String(FileContent);
        FileContent = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
      }

      FilePath = FilePath.Replace("/", @"\");

      string solutionsFolder = $@"{hostingEnvironment.WebRootPath}\templates\SolutionExports\{SolutionName}";
      string itemFolder = $@"{solutionsFolder}\{ItemFolder}";

      Directory.CreateDirectory(itemFolder);

      string fullPath = itemFolder + @"\" + FilePath;

      Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

      File.WriteAllText(fullPath, FileContent);

    }

    public async Task<string> GenerateDeployConfigFile(string ExportName, string Comment, Workspace Workspace, List<Item> WorkspaceItems) {

      DeploymentConfiguration deployConfig = new DeploymentConfiguration {
        ExportName = ExportName,
        Comment = Comment,
        Created = DateTime.UtcNow.AddHours(-7),
        SourceItems = new List<DeploymentSourceItem>(),
        SourceLakehouses = new List<DeploymentSourceLakehouse>(),
        SourceConnections = new List<DeploymentSourceConnection>()
      };

      deployConfig.SourceWorkspaceId = Workspace.Id.ToString();

      var workspaceInfo = await fabricRestApi.GetWorkspaceInfo(Workspace.Id);
      deployConfig.SolutionName = workspaceInfo.Description;
      deployConfig.SourceWorkspaceDescription = workspaceInfo.Description;

      var sequencedWorkspaceItems = new List<Item>();

      var lakehouses = WorkspaceItems.Where(item => item.Type == ItemType.Lakehouse);
      var lakehouseNames = lakehouses.Select(item => item.DisplayName);

      sequencedWorkspaceItems.AddRange(lakehouses);
      sequencedWorkspaceItems.AddRange(WorkspaceItems.Where(item => item.Type == ItemType.Notebook));
      sequencedWorkspaceItems.AddRange(WorkspaceItems.Where(item => item.Type == ItemType.DataPipeline));

      // add semantic models that are not lakehouse default semantic models
      var semanticModels = WorkspaceItems.Where(item => item.Type == ItemType.SemanticModel);
      foreach (var model in semanticModels) {
        if (!lakehouseNames.Contains(model.DisplayName)) {
          sequencedWorkspaceItems.Add(model);
        }
      }

      sequencedWorkspaceItems.AddRange(WorkspaceItems.Where(item => item.Type == ItemType.Report));

      foreach (var item in sequencedWorkspaceItems) {

        // add each items to items collection
        deployConfig.SourceItems.Add(
        new DeploymentSourceItem {
          Id = item.Id.ToString(),
          DisplayName = item.DisplayName,
          Type = item.Type.ToString(),
        });

        // add lakehouses with SQL endpoint info to lakehouses collection
        if (item.Type == "Lakehouse") {
          var sqlEndpoint = await fabricRestApi.GetSqlEndpointForLakehouse(Workspace.Id, item.Id.Value);
          var lakehouse = new DeploymentSourceLakehouse {
            Id = item.Id.ToString(),
            DisplayName = item.DisplayName,
            Server = sqlEndpoint.ConnectionString,
            Database = sqlEndpoint.Id
          };

          var shortcuts = await fabricRestApi.GetLakehouseShortcuts(Workspace.Id, item.Id.Value);
          if (shortcuts.Count() > 0) {
            lakehouse.Shortcuts = new List<DeploymentSourceLakehouseShortcut>();
            foreach (var shortcut in shortcuts) {
              lakehouse.Shortcuts.Add(new DeploymentSourceLakehouseShortcut {
                ConnectionId = shortcut.Target.AdlsGen2.ConnectionId.ToString(),
                Name = shortcut.Name,
                Path = shortcut.Path,
                Location = shortcut.Target.AdlsGen2.Location.ToString(),
                Subpath = shortcut.Target.AdlsGen2.Subpath,
                Type = shortcut.Target.Type.ToString()
              });


            }
          }

          deployConfig.SourceLakehouses.Add(lakehouse);

        }

      }

      string connectionNamePrefix = $"Workspace[{Workspace.Id.ToString()}]-";

      var workspaceConnections = await fabricRestApi.GetWorkspaceConnections(Workspace.Id);

      foreach (var connection in workspaceConnections) {

        deployConfig.SourceConnections.Add(new DeploymentSourceConnection {
          Id = connection.Id.ToString(),
          DisplayName = connection.DisplayName.Replace(connectionNamePrefix, ""),
          Type = connection.ConnectionDetails.Type.ToString(),
          Path = connection.ConnectionDetails.Path,
          CredentialType = connection.CredentialDetails.CredentialType.Value.ToString(),
        });

      }

      var config = JsonSerializer.Serialize(deployConfig, jsonSerializerOptions);

      return config;
    }

    public List<string> GetSolutionExportNames() {

      string solutionsFoldersRoot = $@"{hostingEnvironment.WebRootPath}\templates\SolutionExports\";

      List<string> solutionFolderPaths = Directory.GetDirectories(solutionsFoldersRoot, "*", SearchOption.TopDirectoryOnly).ToList<string>();

      List<string> solutionFolders = new List<string>();

      foreach (string solutionFolderPath in solutionFolderPaths) {
        string folderName = solutionFolderPath.Substring(1 + solutionFolderPath.LastIndexOf(@"\"));
        solutionFolders.Add(folderName);        
      }

      return solutionFolders;

    }

    public List<DeploymentConfiguration> GetSolutionExports() {

      string exportFoldersRoot = $@"{hostingEnvironment.WebRootPath}\templates\SolutionExports\";

      List<string> exportFolderPaths = Directory.GetDirectories(exportFoldersRoot, "*", SearchOption.TopDirectoryOnly).ToList<string>();

      List<DeploymentConfiguration> exports = new List<DeploymentConfiguration>();

      foreach (string exportFolderPath in exportFolderPaths) {

        string deployConfigPath = exportFolderPath + @"\deploy.config.json";
        string deployConfigContent = File.ReadAllText(deployConfigPath);
        DeploymentConfiguration export = JsonSerializer.Deserialize<DeploymentConfiguration>(deployConfigContent, jsonSerializerOptions);
        exports.Add(export);
      }

      return exports;

    }

    public DeploymentConfiguration GetSolutionExport(string ExportName) {
      string exportFolderRoot = $@"{hostingEnvironment.WebRootPath}\templates\SolutionExports\{ExportName}";
      string deployConfigPath = exportFolderRoot + @"\deploy.config.json";
      string deployConfigContent = File.ReadAllText(deployConfigPath);
      return  JsonSerializer.Deserialize<DeploymentConfiguration>(deployConfigContent, jsonSerializerOptions);
    }

    public async Task<SolutionDeploymentPlan> GetSolutionDeploymentFromExport(string ExportName, DeploymentPlan Deployment) {

      var solutionDeploymentPlan = new SolutionDeploymentPlan(Deployment);

      await appLogger.LogStep($"Loading item definition files from exported solution");

      var itemDefinitionFiles = new List<ItemDefinitonFile>();

      string folderPath = $@"{hostingEnvironment.WebRootPath}\templates\SolutionExports\{ExportName}";

      List<string> filesInFolder = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).ToList<string>();

      foreach (string file in filesInFolder) {
        string relativeFilePathFromFolder = file.Replace(folderPath + @"\", "").Replace(@"\", "/");
        if (relativeFilePathFromFolder.Substring(1).Contains("/")) {
          itemDefinitionFiles.Add(new ItemDefinitonFile {
            FullPath = relativeFilePathFromFolder,
            Content = File.ReadAllText(file)
          });
        }
      }

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
      string deployConfigPath = folderPath + @"\deploy.config.json";
      string deployConfigContent = File.ReadAllText(deployConfigPath);
      DeploymentConfiguration deployConfig = JsonSerializer.Deserialize<DeploymentConfiguration>(deployConfigContent, jsonSerializerOptions);

      solutionDeploymentPlan.DeployConfig = deployConfig;

      return solutionDeploymentPlan;
    }

    public JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

  }
}