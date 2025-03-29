using FabricDeploymentManager.Controllers;
using FabricDeploymentManager.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Identity.Web;

namespace FabricDeploymentManager.Services.AsyncProcessing {

  public class BackgroundTaskDispatcher {

    private readonly ILogger<BackgroundTaskDispatcher> logger;
    private DeploymentManager deploymentManager;
    private readonly IBackgroundTaskQueue taskQueue;

    public BackgroundTaskDispatcher(
            DeploymentManager DeploymentManager,
            ILogger<BackgroundTaskDispatcher> Logger,
            IBackgroundTaskQueue TaskQueue,
            IServiceProvider ServiceProvider) {
      deploymentManager = DeploymentManager;
      taskQueue = TaskQueue;
      logger = Logger;
    }

    public async Task DeploySolution(string TargetWorkspace, string SolutionName) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.DeploySolution(TargetWorkspace, SolutionName);

      });

      await Task.FromResult(0);

    }

    public async Task DeployWithParameters(string TargetWorkspace, string SolutionName, string Customer) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.DeployWithParameters(TargetWorkspace, SolutionName, Customer);

      });

      await Task.FromResult(0);

    }

    public async Task DeployFromWorkspace(string SourceWorkspace, string TargetWorkspace, string Customer) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.DeployFromWorkspace(SourceWorkspace, TargetWorkspace, Customer);

      });

      await Task.FromResult(0);

    }

    public async Task UpdateFromWorkspace(string SourceWorkspace, string TargetWorkspace, string Customer, string UpdateType) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.UpdateFromWorkspace(SourceWorkspace, TargetWorkspace, Customer, UpdateType);

      });

      await Task.FromResult(0);

    }

    public async Task ExportFromWorkspace(string SourceWorkspace, string ExportName, string Comment) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.ExportFromWorkspace(SourceWorkspace, ExportName, Comment);

      });

      await Task.FromResult(0);

    }

    public async Task DeployFromLocalExport(string ExportName, string TargetWorkspace, string Customer) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.DeployFromLocalExport(ExportName, TargetWorkspace, Customer);

      });

      await Task.FromResult(0);

    }

    public async Task UpdateFromLocalExport(string ExportName, string TargetWorkspace, string Customer, string UpdateType) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.UpdateFromLocalExport(ExportName, TargetWorkspace, Customer, UpdateType);

      });

      await Task.FromResult(0);

    }

    public async Task ExportFromWorkspaceToAdo(string WorkspaceId, string ExportName, string Comment) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.ExportFromWorkspaceToAdo(WorkspaceId, ExportName, Comment);

      });

      await Task.FromResult(0);

    }

    public async Task DeployFromAdoExport(string WorkspaceId, string ExportName, string TargetWorkspace, string Customer) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.DeployFromAdoExport(WorkspaceId, ExportName, TargetWorkspace, Customer);

      });

      await Task.FromResult(0);

    }

    public async Task UpdateFromAdoExport(string WorkspaceId, string ExportName, string TargetWorkspace, string Customer, string UpdateType) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.UpdateFromAdoExport(WorkspaceId, ExportName, TargetWorkspace, Customer, UpdateType);

      });

      await Task.FromResult(0);
    }

    public async Task UpdateAllTenantsFromExport(string ExportName, string UpdateType) {

      taskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) => {

        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeploymentManager>>();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<ClientNotificaionHub>>();
        var tenantBuilder = scope.ServiceProvider.GetRequiredService<DeploymentManager>();

        await deploymentManager.UpdateAllTenantsFromExport(ExportName, UpdateType);

      });

      await Task.FromResult(0);

    }

  }
}
