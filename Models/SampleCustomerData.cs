namespace FabricDeploymentManager.Models {

  class SampleCustomerData {

    public static DeploymentPlan AdventureWorks {
      get {
        var Deployment = new DeploymentPlan("AdventureWorks", "Adventure Works", DeploymentPlanType.CustomerTenantDeployment);
        Deployment.Description = "The ultimate provider for the avid bicycle rider";

        // setup Web datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.webDatasourcePathParameter,
                                          DeploymentPlan.webDatasourceRootDefault + "Customers/AdventureWorks/");

        // setup ADLS datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.adlsServerPathParameter,
                                          DeploymentPlan.adlsServerPathDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerNameParameter,
                                          DeploymentPlan.adlsContainerNameDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerPathParameter,
                                          "/ProductSales/Customers/AdventureWorks/");

        return Deployment;
      }
    }

    public static DeploymentPlan Contoso {
      get {
        var Deployment = new DeploymentPlan("Contoso", "Contoso", DeploymentPlanType.CustomerTenantDeployment);
        Deployment.Description = "Your trusted source for world-famous pharmaceuticals";

        // setup Web datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.webDatasourcePathParameter,
                                          DeploymentPlan.webDatasourceRootDefault + "Customers/Contoso/");

        // setup ADLS datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.adlsServerPathParameter,
                                          DeploymentPlan.adlsServerPathDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerNameParameter,
                                          DeploymentPlan.adlsContainerNameDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerPathParameter,
                                          "/ProductSales/Customers/Contoso/");

        return Deployment;
      }
    }

    public static DeploymentPlan Fabricam {
      get {
        var Deployment = new DeploymentPlan("Fabrikam", "Fabrikam", DeploymentPlanType.CustomerTenantDeployment);
        Deployment.Description = "The Absolute WHY and WHERE for Enterprise Hardware";

        // setup Web datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.webDatasourcePathParameter,
                                          DeploymentPlan.webDatasourceRootDefault + "Customers/Fabrikam/");

        // setup ADLS datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.adlsServerPathParameter,
                                          DeploymentPlan.adlsServerPathDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerNameParameter,
                                          DeploymentPlan.adlsContainerNameDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerPathParameter,
                                          "/ProductSales/Customers/Fabrikam/");

        return Deployment;
      }
    }

    public static DeploymentPlan Northwind {
      get {
        var Deployment = new DeploymentPlan("Northwind", "Northwind Traders", DeploymentPlanType.CustomerTenantDeployment);
        Deployment.Description = "Microsoft's favorate fictional company";

        // setup Web datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.webDatasourcePathParameter,
                                          DeploymentPlan.webDatasourceRootDefault + "Customers/Northwind/");

        // setup ADLS datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.adlsServerPathParameter,
                                          DeploymentPlan.adlsServerPathDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerNameParameter,
                                          DeploymentPlan.adlsContainerNameDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerPathParameter,
                                          "/ProductSales/Customers/Northwind/");

        return Deployment;
      }
    }

    public static DeploymentPlan SeamarkFarms {
      get {
        var Deployment = new DeploymentPlan("SeamarkFarms", "Seamark Farms", DeploymentPlanType.CustomerTenantDeployment);
        Deployment.Description = "Sweet Sheep for Cheap";

        // setup Web datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.webDatasourcePathParameter,
                                          DeploymentPlan.webDatasourceRootDefault + "Customers/SeamarkFarms/");

        // setup ADLS datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.adlsServerPathParameter,
                                          DeploymentPlan.adlsServerPathDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerNameParameter,
                                          DeploymentPlan.adlsContainerNameDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerPathParameter,
                                          "/ProductSales/Customers/SeamarkFarms/");

        return Deployment;
      }
    }

    public static DeploymentPlan Wingtip {
      get {
        var Deployment = new DeploymentPlan("Wingtip", "Wingtip Toys", DeploymentPlanType.CustomerTenantDeployment);
        Deployment.Description = "Retro toys for nostalgic girls and boys";

        // setup Web datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.webDatasourcePathParameter,
                                          DeploymentPlan.webDatasourceRootDefault + "Customers/Wingtip/");

        // setup ADLS datasource path
        Deployment.AddDeploymentParameter(DeploymentPlan.adlsServerPathParameter,
                                          DeploymentPlan.adlsServerPathDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerNameParameter,
                                          DeploymentPlan.adlsContainerNameDefault);

        Deployment.AddDeploymentParameter(DeploymentPlan.adlsContainerPathParameter,
                                          "/ProductSales/Customers/Wingtip/");

        return Deployment;
      }
    }

    public static Dictionary<string, DeploymentPlan> AllCustomers {
      get {
        return new Dictionary<string, DeploymentPlan> {
          { AdventureWorks.Id, AdventureWorks },
          { Contoso.Id, Contoso },
          { Fabricam.Id, Fabricam },
          { Northwind.Id, Northwind},
          { SeamarkFarms.Id, SeamarkFarms},
          { Wingtip.Id, Wingtip}
        };
      }
    }

  }
}