namespace Quickstart.Kubernetes.Api.Controllers;

/// <summary>
/// Exposes the bound <see cref="KubernetesConfigs"/> and the supported
/// <see cref="KubernetesClusterType"/> values from the Muonroi.Kubernetes package.
/// </summary>
[ApiController]
[Route("api/cluster")]
public class ClusterController(IOptions<KubernetesConfigs> configs) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. Read the bound cluster configuration
    //    GET /api/cluster/config
    //
    //    KubernetesConfigs is bound from the "KubernetesConfigs" section and exposes
    //    ClusterType (KubernetesClusterType) and ClusterEndpoint (API server URL).
    // ---------------------------------------------------------------------------
    [HttpGet("config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        KubernetesConfigs value = configs.Value;
        return Ok(new
        {
            sectionName = KubernetesConfigs.SectionName, // "KubernetesConfigs"
            clusterType = value.ClusterType.ToString(),
            clusterEndpoint = value.ClusterEndpoint
        });
    }

    // ---------------------------------------------------------------------------
    // 2. List supported cluster types
    //    GET /api/cluster/types
    // ---------------------------------------------------------------------------
    [HttpGet("types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetClusterTypes()
    {
        return Ok(new
        {
            clusterTypes = Enum.GetNames<KubernetesClusterType>(),
            descriptions = new
            {
                K8S = "Upstream Kubernetes",
                K3S = "K3s lightweight Kubernetes distribution",
                Eks = "Amazon EKS managed Kubernetes"
            }
        });
    }
}
