namespace Muonroi.AspNetCore.RuleEngine.Tests;

public class UiEngineChangesControllerTests
{
    private readonly ICatalogScanService _catalogScanService = Substitute.For<ICatalogScanService>();
    private readonly IRuleChangeStore _changeStore = Substitute.For<IRuleChangeStore>();
    private readonly IRuleChangeProposalStore _proposalStore = Substitute.For<IRuleChangeProposalStore>();
    private readonly IOptionsMonitor<RuleOptions> _ruleOptionsMonitor = Substitute.For<IOptionsMonitor<RuleOptions>>();
    private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();
    private readonly IAuthorizationPolicyProvider _policyProvider = Substitute.For<IAuthorizationPolicyProvider>();
    private readonly IMDateTimeService _dateTimeService = Substitute.For<IMDateTimeService>();
    private readonly IUiEngineSchemaNotifier _schemaNotifier = Substitute.For<IUiEngineSchemaNotifier>();
    private readonly IMLog<UiEngineChangesController> _logger = Substitute.For<IMLog<UiEngineChangesController>>();
    private readonly UiEngineChangesController _controller;

    public UiEngineChangesControllerTests()
    {
        _ruleOptionsMonitor.CurrentValue.Returns(new RuleOptions());
        _controller = new UiEngineChangesController(
            _catalogScanService,
            _changeStore,
            _proposalStore,
            _ruleOptionsMonitor,
            _authorizationService,
            _policyProvider,
            _dateTimeService,
            _schemaNotifier,
            _logger);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task Validate_ShouldReturnOk()
    {
        // Arrange
        var request = new RuleOrderChangeRequest
        {
            EndpointRoute = "/test",
            OrderedRuleCodes = ["rule1"]
        };

        var binding = new MUiEngineCatalogBinding
        {
            EndpointRoute = "/test",
            Rules = { new MUiEngineCatalogRuleRef { Code = "rule1" } }
        };

        _catalogScanService.BuildBindingsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogBinding> { binding });
        _catalogScanService.ScanRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogRuleDescriptor> 
            { 
                new() { Code = "rule1" } 
            });

        // Act
        var result = await _controller.Validate(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var validation = (RuleOrderChangeValidationResult)okResult.Value!;
        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Apply_ShouldReturnOk_WhenAuthorizedAndValid()
    {
        // Arrange
        var request = new RuleOrderChangeRequest
        {
            EndpointRoute = "/test",
            OrderedRuleCodes = ["rule1"]
        };

        var binding = new MUiEngineCatalogBinding
        {
            EndpointRoute = "/test",
            Rules = { new MUiEngineCatalogRuleRef { Code = "rule1" } }
        };

        _catalogScanService.BuildBindingsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogBinding> { binding });
        _catalogScanService.ScanRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogRuleDescriptor> { new() { Code = "rule1" } });
        
        _changeStore.ApplyAsync(Arg.Any<RuleOrderChangeRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RuleChangeRecord { EndpointRoute = "/test", NewOrder = ["rule1"] });

        _authorizationService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());

        // Act
        var result = await _controller.Apply(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        await _changeStore.Received().ApplyAsync(Arg.Any<RuleOrderChangeRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _schemaNotifier.Received().NotifySchemaChangedAsync(Arg.Any<MUiEngineSchemaVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Propose_ShouldReturnOk()
    {
        // Arrange
        var request = new ProposeRuleChangeRequest
        {
            EndpointRoute = "/test",
            OrderedRuleCodes = ["rule1"]
        };

        var binding = new MUiEngineCatalogBinding
        {
            EndpointRoute = "/test",
            Rules = { new MUiEngineCatalogRuleRef { Code = "rule1" } }
        };

        _catalogScanService.BuildBindingsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogBinding> { binding });
        _catalogScanService.ScanRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogRuleDescriptor> { new() { Code = "rule1" } });

        _proposalStore.ProposeAsync(Arg.Any<ProposeRuleChangeRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RuleChangeProposal { EndpointRoute = "/test", OrderedRuleCodes = ["rule1"] });

        _authorizationService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());

        // Act
        var result = await _controller.Propose(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        await _proposalStore.Received().ProposeAsync(Arg.Any<ProposeRuleChangeRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task History_ShouldReturnList()
    {
        // Arrange
        _changeStore.GetHistoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleChangeRecord>());

        // Act
        var result = await _controller.History("/test", "tenant1", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Rollback_ShouldReturnOk()
    {
        // Arrange
        _changeStore.RollbackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RuleChangeRecord());
        _authorizationService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());

        var request = new RuleOrderRollbackRequest { EndpointRoute = "/test", TenantId = "tenant1" };

        // Act
        var result = await _controller.Rollback(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ListPendingProposals_ShouldReturnList()
    {
        // Arrange
        _proposalStore.ListPendingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleChangeProposal>());

        // Act
        var result = await _controller.ListPendingProposals("tenant1", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ApproveProposal_ShouldReturnOk()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var proposal = new RuleChangeProposal 
        { 
            ProposalId = proposalId, 
            EndpointRoute = "/test", 
            Status = ProposalStatus.Pending,
            OrderedRuleCodes = ["rule1"]
        };
        
        _proposalStore.GetAsync(proposalId, Arg.Any<CancellationToken>()).Returns(proposal);
        _proposalStore.ApproveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(proposal);
        
        var binding = new MUiEngineCatalogBinding
        {
            EndpointRoute = "/test",
            Rules = { new MUiEngineCatalogRuleRef { Code = "rule1" } }
        };
        _catalogScanService.BuildBindingsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogBinding> { binding });
        _catalogScanService.ScanRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MUiEngineCatalogRuleDescriptor> { new() { Code = "rule1" } });

        _changeStore.ApplyAsync(Arg.Any<RuleOrderChangeRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RuleChangeRecord { NewOrder = ["rule1"] });
        _authorizationService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());

        var request = new ReviewProposalRequest { ReviewNote = "ok" };

        // Act
        var result = await _controller.ApproveProposal(proposalId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RejectProposal_ShouldReturnOk()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var proposal = new RuleChangeProposal 
        { 
            ProposalId = proposalId, 
            Status = ProposalStatus.Pending 
        };
        _proposalStore.GetAsync(proposalId, Arg.Any<CancellationToken>()).Returns(proposal);
        _proposalStore.RejectAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(proposal);
        _authorizationService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());

        var request = new ReviewProposalRequest { ReviewNote = "no" };

        // Act
        var result = await _controller.RejectProposal(proposalId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
