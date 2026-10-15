using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Website.Controllers;

namespace <Namespace>;

/// <summary>
/// Turns a member's 2FA off only when they enter a current code, as the backoffice does for users.
/// Umbraco's own UmbTwoFactorLoginController.Disable needs no code, so anyone holding a signed-in
/// session could strip the second factor; BlockCodelessTwoFactorDisableFilter below shuts it.
/// </summary>
[UmbracoMemberAuthorize]
public class MemberTwoFactorController : SurfaceController
{
    private readonly IMemberManager _memberManager;
    private readonly IMemberTwoFactorLoginService _memberTwoFactorLoginService;

    public MemberTwoFactorController(
        IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoDatabaseFactory databaseFactory,
        ServiceContext services,
        AppCaches appCaches,
        IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
        IMemberManager memberManager,
        IMemberTwoFactorLoginService memberTwoFactorLoginService)
        : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
    {
        _memberManager = memberManager;
        _memberTwoFactorLoginService = memberTwoFactorLoginService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(string providerName, string code)
    {
        MemberIdentityUser? member = await _memberManager.GetCurrentMemberAsync();
        if (member is null)
        {
            return Unauthorized();
        }

        Attempt<TwoFactorOperationStatus> result =
            await _memberTwoFactorLoginService.DisableByCodeAsync(providerName, member.Key, code);

        if (result.Success is false)
        {
            ModelState.AddModelError(nameof(code), "That code is not valid. Try the current code from your app.");
            return CurrentUmbracoPage();
        }

        return RedirectToCurrentUmbracoPage();
    }
}

/// <summary>
/// Umbraco routes its own surface controllers too, so UmbTwoFactorLoginController.Disable stays reachable
/// at /umbraco/surface/UmbTwoFactorLogin/Disable even when no form posts to it. Answer 404 there.
/// </summary>
public class BlockCodelessTwoFactorDisableFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor action
            && action.ControllerTypeInfo.AsType() == typeof(UmbTwoFactorLoginController)
            && action.ActionName == nameof(UmbTwoFactorLoginController.Disable))
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

public class MemberTwoFactorControllerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.Configure<MvcOptions>(options => options.Filters.Add<BlockCodelessTwoFactorDisableFilter>());
}
