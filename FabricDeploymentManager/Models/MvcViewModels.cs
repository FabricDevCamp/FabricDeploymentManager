using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.TeamFoundation.Core.WebApi;

namespace FabricDeploymentManager.Models {

  public class WorkspaceRow {
    public Workspace Workspace { get; set; }
    public Capacity Capacity { get; set; }
  }

  public class WorkspaceDetails {
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string WorkspaceId { get; set; }
    public string WorkspaceUrl { get; set; }
    public Capacity Capacity { get; set; }
    public List<Item> WorkpaceItems { get; set; }
    public List<Connection> WorkpaceConnetions { get; set; }
    public IList<WorkspaceRoleAssignment> WorkspaceMembers { get; set; }
    public TeamProjectReference AdoProject { get; set; }
    public bool HasAdoExports { get; set; }
  }

  public class DeploySolutionModel {
    public string TargetWorkspace { get; set; }
    public string SolutionName { get; set; }
    public List<SelectListItem> AvailableSolutions { get; set; }
  }

  public class DeployWithParametersModel {
    public string TargetWorkspace { get; set; }
    public string SolutionName { get; set; }
    public List<SelectListItem> AvailableSolutions { get; set; }
    public string Customer { get; set; }
    public List<SelectListItem> AvailableCustomers { get; set; }
  }

  public class UpdateFromWorkspaceModel {
    public string SourceWorkspace { get; set; }
    public string TargetWorkspace { get; set; }
    public List<SelectListItem> AvailableTargetWorkspaces { get; set; }
    public string Customer { get; set; }
  }

  public class ExportFromWorkspaceModel {
    public string SourceWorkspace { get; set; }
    public string ExportName { get; set; }
    public List<SelectListItem> AvailableSourceWorkspaces { get; set; }
  }

  public class DeployFromWorkspaceModel {
    public string SourceWorkspace { get; set; }
    public string TargetWorkspace { get; set; }
    public List<SelectListItem> AvailableSourceWorkspaces { get; set; }
    public string Customer { get; set; }
    public List<SelectListItem> AvailableCustomers { get; set; }
  }

  public class DeployFromExportModel {
    public string TargetWorkspace { get; set; }
    public string ExportName { get; set; }
    public List<SelectListItem> AvailableExports { get; set; }
    public List<SelectListItem> AvailableCustomers { get; set; }
    public string Customer { get; set; }
  }

  public class UpdateFromExportModel {
    public string TargetWorkspace { get; set; }
    public string ExportName { get; set; }
    public Dictionary<string, List<string>> ExportsDictionary { get; set; }
    public List<SelectListItem> AvailableExports { get; set; }
    public List<SelectListItem> AvailableTargetWorkspaces { get; set; }
    public string Customer { get; set; }
  }

  public class ExportFromWorkspaceToAdoModel {
    public Workspace SourceWorkspace { get; set; }
    public string SuggestedExportName { get; set; }
  }

  public class DeployFromAdoExportModel {
    public Workspace SourceWorkspace { get; set; }
    public string ExportName { get; set; }
    public List<SelectListItem> AvailableExports { get; set; }
    public List<SelectListItem> AvailableCustomers { get; set; }
    public string Customer { get; set; }
  }

  public class UpdateFromAdoExportModel {
    public string WorkspaceId { get; set; }
    public string SolutionName { get; set; }
    public string TargetWorkspace { get; set; }
    public string ExportName { get; set; }
    public string Customer { get; set; }
    public List<SelectListItem> AvailableExports { get; set; }
    public List<SelectListItem> AvailableTargetWorkspaces { get; set; }
  }

  public class ExportDetails {
    public DeploymentConfiguration Export { get; set; }
    public List<Workspace> TargetWorkspaces { get; set; }
  }

  public class AdoProjectRow {
    public TeamProjectReference AdoProject { get; set; }
    public List<string> ExportNames{ get; set; }
  }
  
  public class EmbeddedReport {
    public string id;
    public string name;
    public string datasetId;
    public string embedUrl;
    public string reportType;
  }

  public class EmbeddedDataset {
    public string id;
    public string name;
    public string createReportEmbedURL;
  }

  public class EmbeddedViewModel {
    public string tenantName { get; set; }
    public List<EmbeddedReport> reports { get; set; }
    public List<EmbeddedDataset> datasets { get; set; }
    public string embedToken { get; set; }
  }

}
