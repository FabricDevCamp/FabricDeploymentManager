namespace FabricDeploymentManager.Models {

  public enum DeploymentPlanType {
    StagedDeployment,
    CustomerTenantDeployment
  }

  public class DeploymentPlan {

    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DeploymentPlanType DeploymentType { get; set; }

    public Dictionary<string, string> Parameters { get; set; }

    public void AddDeploymentParameter(string ParameterName, string DeploymentValue) {
      Parameters.Add(ParameterName, DeploymentValue);
    }

    public string TargetWorkspace {
      get { return $"Tenant - {Name}"; }
    }

    public const string webDatasourcePathParameter = "webDatasourcePath";
    public const string adlsServerPathParameter = "adlsServer";
    public const string adlsContainerNameParameter = "adlsContainerName";
    public const string adlsContainerPathParameter = "adlsContainerPath";
    public const string adlsAccountKey = "adlsAccountKey ";

    // default values
    public const string webDatasourceRootDefault = "https://fabricdevcamp.blob.core.windows.net/sampledata/ProductSales/";

    public const string adlsServerPathDefault = "https://fabricdevcamp.dfs.core.windows.net/";
    public const string adlsContainerNameDefault = "sampledata";
    public const string adlsContainerPathDefault = "/ProductSales/Dev";

    public DeploymentPlan(DeploymentPlanType DeploymentType) {
      this.DeploymentType = DeploymentType;
      Parameters = new Dictionary<string, string>();
    }

    public DeploymentPlan(string DeploymentId, string DeploymentName, DeploymentPlanType DeploymentType) {
      this.Id = DeploymentId;
      this.Name = DeploymentName;
      this.DeploymentType = DeploymentType;
      Parameters = new Dictionary<string, string>();
    }

  }

}