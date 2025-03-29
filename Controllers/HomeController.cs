using FabricDeploymentManager.Models;
using FabricDeploymentManager.Services;
using FabricDeploymentManager.Services.AsyncProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Fabric.Api.Core.Models;
using System.Diagnostics;
using System.Threading.Tasks;



namespace FabricDeploymentManager.Controllers {

  [Authorize]
  public class HomeController : Controller {

    private readonly ILogger<HomeController> logger;
    private DeploymentManager deploymentManager;
    private readonly BackgroundTaskDispatcher taskDispatcher;
    private bool enableAdoLinks;

    public HomeController(ILogger<HomeController> Logger,
                          IConfiguration Configuration,
                          DeploymentManager DeploymentManager,
                          BackgroundTaskDispatcher TaskDispatcher) {
      logger = Logger;
      deploymentManager = DeploymentManager;
      taskDispatcher = TaskDispatcher;
      enableAdoLinks = Configuration["AzureDevOps:EnableAdoLinks"] == "true";
    }

    public override void OnActionExecuted(ActionExecutedContext context) {
      base.OnActionExecuted(context);
      ViewBag.EnableAdoLinks = enableAdoLinks;
    }

    public async Task<IActionResult> Index() {
      var viewModel = await deploymentManager.GetWorkspaceRows();
      return View(viewModel);
    }

    public async Task<IActionResult> LaunchAsyncMonitor() {
      await Task.FromResult(0);
      return View();
    }

    public async Task<IActionResult> AsyncOperationsMonitor() {
      await Task.FromResult(0);
      return View();
    }

    public async Task<IActionResult> Embed(string WorkspaceId) {
      var viewModel = await deploymentManager.GetEmbeddedViewModel(WorkspaceId);
      return View(viewModel);
    }

    public async Task<IActionResult> Capacities() {
      var viewModel = await deploymentManager.GetCapacities();
      return View(viewModel);
    }

    public async Task<IActionResult> Connections() {
      var viewModel = await deploymentManager.GetConnections();
      return View(viewModel);
    }

    public IActionResult Exports() {
      var viewModel = deploymentManager.GetSolutionExports();
      return View(viewModel);
    }

    public  async Task<IActionResult> Export(string ExportName) {
      var viewModel = await deploymentManager.GetExportDetail(ExportName);
      return View(viewModel);
    }

    public IActionResult DeleteExport(string ExportName) {
      deploymentManager.DeleteExport(ExportName);
      return RedirectToAction("Exports");
    }

    public async Task<IActionResult> Connection(string ConnectionId) {
      var viewModel = await deploymentManager.GetConnection(new Guid(ConnectionId));
      return View(viewModel);
    }

    public async Task<IActionResult> DeleteConnection(string ConnectionId) {
      await deploymentManager.DeleteConnection(new Guid(ConnectionId));
      return RedirectToAction("Connections");
    }

    public async Task<IActionResult> DeleteAdoProject(string ProjectId) {
      await deploymentManager.DeleteAdoProject(ProjectId);
      return RedirectToAction("AdoProjects");
    }

    public async Task<IActionResult> Workspace(string WorkspaceId) {     
      var workspace = await deploymentManager.GetWorkspaceDetails(WorkspaceId);
      return View(workspace);
    }

    public async Task<IActionResult> DeleteWorkspace(string WorkspaceId) {
      await deploymentManager.DeleteWorkspace(WorkspaceId);
      return RedirectToAction("Index", "Home");

    }

    public async Task<IActionResult> AddSolutionItem(string WorkspaceId, string ItemName) {
      await deploymentManager.AddSolutionItem(WorkspaceId, ItemName);
      return RedirectToAction("Workspace", "Home", new { WorkspaceId = WorkspaceId });
    }
 
    public IActionResult DeploySolution() {

      var exportsPickList = deploymentManager.AvailableSolutions.Select(
        solution => new SelectListItem {
          Text = solution.Value,
          Value = solution.Key
        }).ToList();

      exportsPickList.Add(new SelectListItem {
        Text = "[Deploy All Solutions]",
        Value = "DeployAllSolutions"
      });

      return View(new DeploySolutionModel {
        AvailableSolutions = exportsPickList
      });
    }

    [HttpPost]
    public async Task<IActionResult> DeploySolution(string TargetWorkspace, string SolutionName) {
      await taskDispatcher.DeploySolution(TargetWorkspace, SolutionName);
      return RedirectToAction("Index", "Home");
    }

    public IActionResult DeployWithParameters() {

      var exportsPickList = deploymentManager.AvailableSolutions.Select(
        solution => new SelectListItem {
          Text = solution.Value,
          Value = solution.Key
        }).ToList();

      var customersPickList = SampleCustomerData.AllCustomers.Select(
        customer => new SelectListItem {
          Text = customer.Value.Name,
          Value = customer.Key
        }).ToList();

      customersPickList.Add(new SelectListItem {
        Text = "[Deploy To All Customers]",
        Value = "DeployToAllCustomers"
      });

      return View(new DeployWithParametersModel {
        AvailableSolutions = exportsPickList,
        AvailableCustomers = customersPickList
      });

    }

    [HttpPost]
    public async Task<IActionResult> DeployWithParameters(string TargetWorkspace, string SolutionName, string Customer) {
      await taskDispatcher.DeployWithParameters(TargetWorkspace, SolutionName, Customer);
      return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> DeployFromWorkspace() {

      var workspaces = await deploymentManager.GetWorkspaces();

      var sourceWorkspacesPickList = workspaces.Select(
        workspace => new SelectListItem {
          Text = workspace.DisplayName,
          Value = workspace.DisplayName
        }).ToList();

      var customersPickList = SampleCustomerData.AllCustomers.Select(
        customer => new SelectListItem {
          Text = customer.Value.Name,
          Value = customer.Key
        }).ToList();

      customersPickList.Add(new SelectListItem {
        Text = "[Deploy To All Customers]",
        Value = "DeployToAllCustomers"
      });

      return View(new DeployFromWorkspaceModel {
        AvailableSourceWorkspaces = sourceWorkspacesPickList,
        AvailableCustomers = customersPickList
      });

    }

    [HttpPost]
    public async Task<IActionResult> DeployFromWorkspace(string SourceWorkspace, string TargetWorkspace, string Customer) {
      await taskDispatcher.DeployFromWorkspace(SourceWorkspace, TargetWorkspace, Customer);
      return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> UpdateFromWorkspace() {

      var workspaces = await deploymentManager.GetWorkspaces();

      var sourceWorkspacesPickList = workspaces.Select(
        workspace => new SelectListItem {
          Text = workspace.DisplayName,
          Value = workspace.DisplayName
        }).ToList();

      var targetWorkspacesPickList = workspaces.Where(workspace => workspace.DisplayName.Contains("Tenant")).Select(
        workspace => new SelectListItem {
          Text = workspace.DisplayName,
          Value = workspace.DisplayName + "|" +workspace.Description
        }).ToList();


      var customersPickList = SampleCustomerData.AllCustomers.Select(
        customer => new SelectListItem {
          Text = customer.Value.Name,
          Value = customer.Key
        }).ToList();

      customersPickList.Add(new SelectListItem {
        Text = "[Update All Customer Tenants]",
        Value = "UpdateAllCustomerTenants"
      });


      return View(new UpdateFromWorkspaceModel {
        AvailableTargetWorkspaces = targetWorkspacesPickList,
      });

    }

    [HttpPost]
    public async Task<IActionResult> UpdateFromWorkspace(string SourceWorkspace, string TargetWorkspace, string Customer, string UpdateType) {
      await taskDispatcher.UpdateFromWorkspace(SourceWorkspace, TargetWorkspace, Customer, UpdateType);
      return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> ExportFromWorkspace() {

      var workspaces = (await deploymentManager.GetWorkspaces()).Where(workspace => workspace.DisplayName.Contains("Solution")).ToList();

      var sourceWorkspacesPickList = workspaces.Select(
        workspace => new SelectListItem {
          Text = workspace.DisplayName,
          Value = workspace.DisplayName
        }).ToList();


      return View(new ExportFromWorkspaceModel {
        AvailableSourceWorkspaces = sourceWorkspacesPickList
      });

    }

    [HttpPost]
    public async Task<IActionResult> ExportFromWorkspace(string SourceWorkspace, string ExportName, string Comment) {
      await taskDispatcher.ExportFromWorkspace(SourceWorkspace, ExportName, Comment);
      return RedirectToAction("Index", "Home");
    }

    public IActionResult DeployFromExport() {

      var exportsPickList = deploymentManager.GetSolutionExportNames().Select(
        export => new SelectListItem {
          Text = export,
          Value = export
        }).ToList();

      var customersPickList = SampleCustomerData.AllCustomers.Select(
        customer => new SelectListItem {
          Text = customer.Value.Name,
          Value = customer.Key
        }).ToList();

      customersPickList.Add(new SelectListItem {
        Text = "[Deploy To All Customers]",
        Value = "DeployToAllCustomers"
      });

      return View(new DeployFromExportModel {
        AvailableExports = exportsPickList,
        AvailableCustomers = customersPickList,
      });

    }

    [HttpPost]
    public async Task<IActionResult> DeployFromExport(string ExportName, string TargetWorkspace, string Customer) {
      await taskDispatcher.DeployFromLocalExport(ExportName, TargetWorkspace, Customer);
      return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> UpdateFromExport() {

      var exports = deploymentManager.GetSolutionExports();
      var exportsDictionary = new Dictionary<string, List<string>>();
      foreach (var export in exports) {
        if (!exportsDictionary.ContainsKey(export.SolutionName)) {
          exportsDictionary[export.SolutionName] = new List<string>();
        }

        exportsDictionary[export.SolutionName].Add(export.ExportName);

      }

      var exportsPickList = exports.Select(
        export => new SelectListItem {
          Text = export.ExportName,
          Value = export.ExportName
        }).ToList();    

      var workspaces = await deploymentManager.GetWorkspaces();

      var targetWorkspacesPickList = workspaces.Where(workspace => workspace.DisplayName.Contains("Tenant")).Select(
      workspace => new SelectListItem {
        Text = workspace.DisplayName,
        Value = workspace.DisplayName + "|" + workspace.Description
      }).ToList();


      return View(new UpdateFromExportModel {
        ExportsDictionary = exportsDictionary,
        AvailableExports = exportsPickList,
        AvailableTargetWorkspaces = targetWorkspacesPickList
      });

    }

    [HttpPost]
    public async Task<IActionResult> UpdateFromExport(string ExportName, string TargetWorkspace, string Customer, string UpdateType) {
      await taskDispatcher.UpdateFromLocalExport(ExportName, TargetWorkspace, Customer, UpdateType);
      return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> UpdateAllTenantsFromExport(string ExportName, string UpdateType) {
      await taskDispatcher.UpdateAllTenantsFromExport(ExportName, UpdateType);
      return RedirectToAction("Export", "Home", new { ExportName = ExportName });
    }

    public async Task<IActionResult> AdoProjects() {
      var model = await deploymentManager.GetAdoProjectRow();
      return View(model);
    }

    public async Task<IActionResult> CreateAdoProject(string WorkspaceId) {
      await deploymentManager.CreateAdoProject(WorkspaceId);
      return RedirectToAction("Workspace", "Home", new { WorkspaceId = WorkspaceId });
    }

    public async Task<IActionResult> ExportFromWorkspaceToAdo(string WorkspaceId) {

      Guid workspaceId = new Guid(WorkspaceId);
      var workspace = await deploymentManager.GetWorkspace(workspaceId);

      return View(new ExportFromWorkspaceToAdoModel {
        SourceWorkspace = workspace,
        SuggestedExportName = await deploymentManager.GetSuggestedExportName(workspace.DisplayName)
      });

    }

    [HttpPost]
    public async Task<IActionResult> ExportFromWorkspaceToAdo(string WorkspaceId, string ExportName, string Comment) {
      await taskDispatcher.ExportFromWorkspaceToAdo(WorkspaceId, ExportName, Comment);
      return RedirectToAction("Workspace", "Home", new { WorkspaceId = WorkspaceId });
    }

    public async Task<IActionResult> DeployFromAdoExport(string WorkspaceId) {

      Guid workspaceId = new Guid(WorkspaceId);
      var workspace = await deploymentManager.GetWorkspace(workspaceId);

      var exports = await deploymentManager.GetAdoProjectExports(workspace.DisplayName);
      var exportsPickList = exports.Select(
        export => new SelectListItem {
          Text = export,
          Value = export
        }).ToList();

      var customersPickList = SampleCustomerData.AllCustomers.Select(
        customer => new SelectListItem {
          Text = customer.Value.Name,
          Value = customer.Key
        }).ToList();

      customersPickList.Add(new SelectListItem {
        Text = "[Deploy To All Customers]",
        Value = "DeployToAllCustomers"
      });

      return View(new DeployFromAdoExportModel {
        SourceWorkspace = workspace,
        AvailableExports = exportsPickList,
        AvailableCustomers = customersPickList,
      });

    }

    [HttpPost]
    public async Task<IActionResult> DeployFromAdoExport(string WorkspaceId, string ExportName, string TargetWorkspace, string Customer) {
      await taskDispatcher.DeployFromAdoExport(WorkspaceId, ExportName, TargetWorkspace, Customer);
      return RedirectToAction("Workspace", "Home", new { WorkspaceId = WorkspaceId });

    }

    public async Task<IActionResult> UpdateFromAdoExport(string WorkspaceId) {

      var solutionWorkspace = await deploymentManager.GetWorkspace(new Guid(WorkspaceId));
      var exports = await deploymentManager.GetAdoProjectExports(solutionWorkspace.DisplayName);

      var exportsPickList = exports.Select(
        export => new SelectListItem {
          Text = export,
          Value = export
        }).ToList();
   
      var workspaces = await deploymentManager.GetWorkspaces();

      var targetWorkspacesPickList = 
        workspaces.Where(workspace => !workspace.DisplayName.Contains("Custom"))
                  .Where(workspace => workspace.Description == solutionWorkspace.DisplayName)
                  .Select(workspace => new SelectListItem {
                    Text = workspace.DisplayName,
                    Value = workspace.DisplayName
                  }).ToList();

      if(targetWorkspacesPickList.Count > 0) {
        targetWorkspacesPickList.Add(new SelectListItem {
          Text = "[Update All Customer Tenants]",
          Value = "UpdateAllCustomerTenants"
        });

      }

      return View(new UpdateFromAdoExportModel {
        WorkspaceId = solutionWorkspace.Id.ToString(),
        SolutionName = solutionWorkspace.DisplayName,        
        AvailableExports = exportsPickList,
        AvailableTargetWorkspaces = targetWorkspacesPickList
      });

    }

    [HttpPost]
    public async Task<IActionResult> UpdateFromAdoExport(string WorkspaceId, string ExportName, string TargetWorkspace, string Customer, string UpdateType) {
      await taskDispatcher.UpdateFromAdoExport(WorkspaceId, ExportName, TargetWorkspace, Customer, UpdateType);
      return RedirectToAction("Workspace", "Home", new { WorkspaceId = WorkspaceId });

    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
      return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
 
  }
}
