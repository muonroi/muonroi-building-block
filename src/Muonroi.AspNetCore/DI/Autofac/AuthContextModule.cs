using Autofac;
using Muonroi.Http.Http;

namespace Muonroi.AspNetCore.DI.Autofac;

internal class AuthContextModule : global::Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(delegate (IComponentContext context)
        {
            ILogger<AuthenticateHeaderHandler> logger = context.Resolve<ILogger<AuthenticateHeaderHandler>>();
            IConfiguration configuration = context.Resolve<IConfiguration>();
            IAuthenticateInfoContext authContext = context.Resolve<IAuthenticateInfoContext>();
            return new AuthenticateHeaderHandler(logger, authContext, configuration);
        }).InstancePerLifetimeScope();

        builder.Register(delegate (IComponentContext context)
            {
                IAuthContextFactory factory = context.Resolve<IAuthContextFactory>();
                return factory.Create();
            }).As<IAuthenticateInfoContext>()
            .As<ICurrentUserContext>()
            .InstancePerLifetimeScope();
    }
}
