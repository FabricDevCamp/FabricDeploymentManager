using FabricDeploymentManager.Models;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.Operations;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FabricDeploymentManager.Services {

  public class AdoProjectManager {


    private string azureDevOpsOrganization;
    private string azureDevOpsBaseUrl;
    private EntraIdTokenManager tokenManager { get; }
    private AuthenticationResult authenticationResult { get; set; }
    private ItemDefinitionFactory itemDefinitionFactory;
    private SignalRLogger appLogger;

    public AdoProjectManager(IConfiguration Configuration,
                             EntraIdTokenManager TokenManager,
                             SignalRLogger AppLogger,
                             ItemDefinitionFactory ItemDefinitionFactory) {
      tokenManager = TokenManager;
      appLogger = AppLogger;
      itemDefinitionFactory = ItemDefinitionFactory;
      azureDevOpsOrganization = Configuration["AzureDevOps:Organization"];
      azureDevOpsBaseUrl = $"https://dev.azure.com/{azureDevOpsOrganization}";
    }

    #region "Internal plumbing details"

    private const string AdoProjectTemplateId = "b8a3a935-7e91-48b8-a94c-606d37c3e9f2";

    private static readonly string[] AdoUserPermissionScopes = new string[] {
      "499b84ac-1321-427f-aa17-267ca6975798/user_impersonation"
    };

    private static readonly string[] AdoServicePrincialPermissionScopes = new string[] {
      "499b84ac-1321-427f-aa17-267ca6975798/.default"
    };

    public static JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private async Task<string> GetAzureDevOpsAccessToken() {

      var timeNow = DateTimeOffset.UtcNow;

      if (authenticationResult == null ||
        timeNow >= authenticationResult.ExpiresOn) {
        authenticationResult = await tokenManager.GetAccessTokenResult(AdoServicePrincialPermissionScopes);
      }

      return authenticationResult.AccessToken;
    }

    private VssConnection GetAzureDevOpsConnection() {
      var orgUrl = new Uri(azureDevOpsBaseUrl);
      return new VssConnection(orgUrl, new VssOAuthAccessTokenCredential(GetAzureDevOpsAccessToken().Result));
    }

    private GitHttpClient gitHttpClient {
      get { return GetAzureDevOpsConnection().GetClient<GitHttpClient>(); }
    }

    private ProjectHttpClient projectClient {
      get { return GetAzureDevOpsConnection().GetClient<ProjectHttpClient>(); }
    }
    private OperationsHttpClient operationsClient {
      get { return GetAzureDevOpsConnection().GetClient<OperationsHttpClient>(); }
    }

    #endregion

    public async Task<List<TeamProjectReference>> GetProjects() {
      return (await projectClient.GetProjects()).ToList();
    }

    public async Task<TeamProjectReference> GetProject(string ProjectName) {

      var projects = await GetProjects();

      foreach (var project in projects) {
        if (project.Name == ProjectName) return project;
      }

      return null;

    }

    private async Task<Guid> GetProjectId(string ProjectName) {

      var projects = await GetProjects();

      foreach (var project in projects) {
        if (project.Name == ProjectName) return project.Id;
      }

      throw new ApplicationException("Could not find requested project");

    }

    public async Task<Guid> GetProjectRepoId(string ProjectName) {

      List<GitRepository> repos = await gitHttpClient.GetRepositoriesAsync(ProjectName);

      foreach (GitRepository repo in repos) {
        if (repo.Name == ProjectName) {
          return repo.Id;
        }
      }

      throw new ApplicationException("Cannot find project");
    }

    public async Task<List<string>> GetProjectBranches(string ProjectName) {

      var project = await GetProject(ProjectName);

      if (project == null) {
        throw new ApplicationException("Could not find requested project");
      }

      var projectId = project.Id;
      var projectRepoId = await GetProjectRepoId(ProjectName);

      var branches = await gitHttpClient.GetBranchesAsync(projectRepoId);

      return branches.Select(branch => branch.Name).ToList();

    }

    public async Task<string> GetMostRecentDailyBuildBranch(string ProjectName) {

      var project = await GetProject(ProjectName);

      if (project == null) {
        throw new ApplicationException("Could not find requested project");
      }

      var projectRepoId = await GetProjectRepoId(ProjectName);
      var branches = gitHttpClient.GetBranchesAsync(projectRepoId).Result;

      List<string> branchesList = new List<string>();

      foreach (var branch in branches) {
        if (branch.Name.Contains("daily-build")) {
          branchesList.Add(branch.Name);
        }
      }

      return branchesList.OrderByDescending(i => i).First();

    }

    public async Task<string> GetFirstDailyBuildBranch(string ProjectName) {

      var project = await GetProject(ProjectName);

      if (project == null) {
        throw new ApplicationException("Could not find requested project");
      }

      var projectRepoId = await GetProjectRepoId(ProjectName);
      var branches = gitHttpClient.GetBranchesAsync(projectRepoId).Result;

      List<string> branchesList = new List<string>();

      foreach (var branch in branches) {
        if (branch.Name.Contains("daily-build")) {
          branchesList.Add(branch.Name);
        }
      }

      return branchesList.OrderBy(i => i).First();

    }

    public async Task<bool> BranchAlreadyExist(string ProjectName, string BranchName) {

      var project = await GetProject(ProjectName);

      if (project == null) {
        throw new ApplicationException("Could not find requested project");
      }

      var projectRepoId = await GetProjectRepoId(ProjectName);
      var branches = gitHttpClient.GetBranchesAsync(projectRepoId).Result;

      foreach (var branch in branches) {
        if (branch.Name == BranchName) {
          return true;
        }
      }

      return false;

    }

    public async Task<string> CreateProject(string ProjectName, Workspace TargetWorkspace = null) {

      //appLogger.LogStep($"Create Azure DevOps project named [{ProjectName}]");

      await DeleteProjectIfItExists(ProjectName);

      var project = new TeamProject {
        Name = ProjectName,
        Description = "This is a sample project created to demonstrate GIT integration with Fabric.",
        Visibility = ProjectVisibility.Private,
        Capabilities = new Dictionary<string, Dictionary<string, string>>() {
         {  "versioncontrol", new Dictionary<string, string>() { { "sourceControlType", "Git" } } },
         {  "processTemplate", new Dictionary<string, string>() { { "templateTypeId", AdoProjectTemplateId } } }
       }
      };

      Task<OperationReference> queueCreateProjectTask = projectClient.QueueCreateProject(project);
      OperationReference operationReference = queueCreateProjectTask.Result;

      Operation operation = await operationsClient.GetOperationAsync(operationReference);

      while (!operation.Completed) {
        Thread.Sleep(3000);
        operation = await operationsClient.GetOperationAsync(operationReference);
      }

      string lastObjectId = await PushInitialContentWithReadMe(ProjectName, TargetWorkspace);

      // AppLogger.LogSubstep("Create project operation complete");

      return lastObjectId;
    }

    public async Task<TeamProjectReference> EnsureProjectExists(string ProjectName, Workspace TargetWorkspace = null) {

      var projects = await GetProjects();

      foreach (var project in projects) {
        if (project.Name == ProjectName) return project;
      }

      await CreateProject(ProjectName, TargetWorkspace);

      return await GetProject(ProjectName);

    }

    public async Task DeleteProject(Guid ProjectId) {
      OperationReference operationReference = await projectClient.QueueDeleteProject(ProjectId);
      Operation operation = await operationsClient.GetOperationAsync(operationReference);
      while (!operation.Completed) {
        Thread.Sleep(3000);
        operation = await operationsClient.GetOperationAsync(operationReference);
      }
    }

    public async Task DeleteProject(string ProjectName) {
      Guid projectId = await GetProjectId(ProjectName);
      await DeleteProject(projectId);
    }

    private async Task DeleteProjectIfItExists(string ProjectName) {
      var projects = await GetProjects();
      foreach (var project in projects) {
        if (project.Name == ProjectName) {
          // AppLogger.LogSubstep($"Deleting existing project with same name");
          await DeleteProject(project.Id);
          return;
        }
      }
    }

    public async Task DisplayProjects() {
      // AppLogger.LogStep("All Projects");
      foreach (var project in await GetProjects()) {
        //AppLogger.LogSubstep(project.Name + " - " + project.Id.ToString());
      }
    }

    public async Task<List<GitItem>> GetGitItemsFromAdoProject(string ProjectName, string BranchName = "main") {

      Guid repoId = await GetProjectRepoId(ProjectName);

      return await gitHttpClient.GetItemsAsync(repoId, download: true,
                                         recursionLevel: VersionControlRecursionType.Full);

    }

    public async Task<List<ItemDefinitonFile>> GetItemDefinitionFilesFromGitRepo(string ProjectName, string BranchName) {

      var gitItems = new List<ItemDefinitonFile>();

      Guid repoId = await GetProjectRepoId(ProjectName);

      GitVersionDescriptor gvd = new GitVersionDescriptor {
        VersionType = GitVersionType.Branch,
        Version = BranchName
      };

      var items = await gitHttpClient.GetItemsAsync(repoId, versionDescriptor: gvd, download: true,
                                                    recursionLevel: VersionControlRecursionType.Full);

      foreach (var item in items) {
        if (!item.IsFolder && item.Path.Substring(1).Contains("/")) {
          var contentStream = await gitHttpClient.GetItemContentAsync(repoId, item.Path, versionDescriptor: gvd);
          var contentReader = new StreamReader(contentStream);
          string content = contentReader.ReadToEnd();
          string path = item.Path;
          gitItems.Add(new ItemDefinitonFile {
            Content = content,
            FullPath = path.Substring(1)
          });
        }
      }

      return gitItems;
    }

    public async Task<DeploymentConfiguration> GetDeployConfigFromGitRepo(string ProjectName, string BranchName) {

      Guid repoId = await GetProjectRepoId(ProjectName);

      GitVersionDescriptor gvd = new GitVersionDescriptor {
        VersionType = GitVersionType.Branch,
        Version = BranchName
      };

      string itemPath = "/deploy.config.json";
      var item = await gitHttpClient.GetItemAsync(repoId, itemPath, versionDescriptor: gvd, download: true);

      var contentStream = await gitHttpClient.GetItemContentAsync(repoId, item.Path, versionDescriptor: gvd);
      var contentReader = new StreamReader(contentStream);
      string content = contentReader.ReadToEnd();
      return JsonSerializer.Deserialize<DeploymentConfiguration>(content, jsonSerializerOptions);
    }

    public async Task<string> PushInitialContentWithReadMe(string ProjectName, Workspace TargetWorkspace = null, string LastObjectId = "0000000000000000000000000000000000000000") {

      // update markdown content for ReadMe.md
      string ReadMeContent = string.Empty;

      if (TargetWorkspace == null) {
        ReadMeContent = itemDefinitionFactory.GetTemplateFile(@"AdoProjectTemplates\AdoReadMe.md");
      }
      else {
        string workspaceName = TargetWorkspace.DisplayName;
        string workspaceId = TargetWorkspace.Id.ToString();
        string workspaceUrl = $"https://app.powerbi.com/groups/{TargetWorkspace.Id.ToString()}";

        ReadMeContent = itemDefinitionFactory.GetTemplateFile(@"AdoProjectTemplates\AdoReadMeWithWorkspace.md")
                                             .Replace("{WORKSPACE_NAME}", workspaceName)
                                             .Replace("{WORKSPACE_ID}", workspaceId)
                                             .Replace("{WORKSPACE_URL}", workspaceUrl);
      }

      Guid repoId = await GetProjectRepoId(ProjectName);

      GitPush pushReadMe = new GitPush {
        RefUpdates = new List<GitRefUpdate>() {
        new GitRefUpdate {
          Name = "refs/heads/main",
          OldObjectId = LastObjectId
        }
      },
        Commits = new List<GitCommit> {
        new GitCommit {
          Comment = "Commit initial ReadMe.md",
          Changes = new List<GitChange> {
            new GitChange {
              ChangeType = VersionControlChangeType.Add,
              Item = new GitItem {
                Path = "/README.md"
              },
              NewContent = new ItemContent {
                Content = ReadMeContent,
                ContentType = ItemContentType.RawText
              }
            }
          }
        }
      }
      };

      GitPush pushResponse = await gitHttpClient.CreatePushAsync(pushReadMe, repoId);

      string oldObjectId = pushResponse.RefUpdates.FirstOrDefault().NewObjectId;

      return oldObjectId;

    }

    public async Task<bool> DoesFileExistInGitRepo(string ProjectName, string FileName) {

      var gitItems = new List<ItemDefinitonFile>();

      Guid repoId = await GetProjectRepoId(ProjectName);

      var items = await gitHttpClient.GetItemsAsync(repoId,
                                              download: true,
                                              recursionLevel: VersionControlRecursionType.Full);

      foreach (var item in items) {
        if (!item.IsFolder && item.Path.Substring(1) == FileName) {
          return true;
        }
      }

      return false;

    }

    public async Task<string> PushFileToGitRepo(string ProjectName, string FileName, string FileContent, string BranchName = "main") {

      var doesFileExist = await DoesFileExistInGitRepo(ProjectName, FileName);

      var repoId = await GetProjectRepoId(ProjectName);

      var repositories = await gitHttpClient.GetRepositoriesAsync(ProjectName);

      var mainRepository = repositories.First();
      var refs = await gitHttpClient.GetRefsAsync(ProjectName, mainRepository.Id, filter: $"heads/{BranchName}");
      var mainBranchRef = refs.First();

      string mainBranchObjectId = mainBranchRef.ObjectId;

      var changes = new List<GitChange>();

      changes.Add(new GitChange {
        ChangeType = doesFileExist ? VersionControlChangeType.Edit : VersionControlChangeType.Add,
        Item = new GitItem {
          Path = "/" + FileName
        },
        NewContent = new ItemContent {
          Content = Convert.ToBase64String(Encoding.ASCII.GetBytes(FileContent)),
          ContentType = ItemContentType.Base64Encoded
        }
      });

      var pushRequest = new GitPush {
        RefUpdates = new List<GitRefUpdate>() {
        new GitRefUpdate {
          Name = $"refs/heads/{BranchName}",
          OldObjectId = mainBranchObjectId
        }
      },
        Commits = new List<GitCommit>() {
        new GitCommit {
        Changes = changes,
        Comment = "Adding source files for import mode solution"
        }
       }
      };

      GitPush pushResponse = await gitHttpClient.CreatePushAsync(pushRequest, repoId);

      string oldObjectId = pushResponse.RefUpdates.FirstOrDefault().NewObjectId;

      return oldObjectId;

    }

    public async Task<string> PushChangesToGitRepo(string ProjectName, string Comment, List<GitChange> Changes, string BranchName = "main") {

      if (BranchName != "main") {
        await CreateBranch(ProjectName, BranchName);
      }
      var repoId = await GetProjectRepoId(ProjectName);

      var repositories = await gitHttpClient.GetRepositoriesAsync(ProjectName);

      var mainRepository = repositories.First();
      var refs = await gitHttpClient.GetRefsAsync(ProjectName, mainRepository.Id, filter: $"heads/{BranchName}");
      var branchRef = refs.First();

      string branchObjectId = branchRef.ObjectId;

      var pushRequest = new GitPush {
        RefUpdates = new List<GitRefUpdate>() {
        new GitRefUpdate {
          Name = $"refs/heads/{BranchName}",
          OldObjectId = branchObjectId
        }
      },
        Commits = new List<GitCommit>() {
        new GitCommit {
        Changes = Changes,
        Comment = Comment
        }
       }
      };

      GitPush pushResponse = await gitHttpClient.CreatePushAsync(pushRequest, repoId);

      string oldObjectId = pushResponse.RefUpdates.FirstOrDefault().NewObjectId;

      return oldObjectId;

    }

    public async Task CreateBranch(string ProjectName, string BranchName) {

      var existingProject = await GetProject(ProjectName);

      var repositories = await gitHttpClient.GetRepositoriesAsync(ProjectName);

      var mainRepository = repositories.First();

      var mainBranch = (await gitHttpClient.GetRefsAsync(ProjectName, mainRepository.Id, filter: $"heads/main")).First();

      var targetBranch = (await gitHttpClient.GetRefsAsync(ProjectName, mainRepository.Id, filter: $"heads/{BranchName}")).FirstOrDefault();


      string mainBranchObjectId = mainBranch.ObjectId;

      List<GitRefUpdate> newBranchUpdates = new List<GitRefUpdate>() {
      new GitRefUpdate {
        Name = $"refs/heads/{BranchName}",
        NewObjectId = mainBranchObjectId,
        OldObjectId = "0000000000000000000000000000000000000000"
      }
    };

      await gitHttpClient.UpdateRefsAsync(newBranchUpdates, mainRepository.Id);

    }

  }

}
