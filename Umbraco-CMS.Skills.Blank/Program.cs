
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();

// Marker type for WebApplicationFactory<BlankProgram>, which only needs a public type in this
// assembly to locate its entry point and content root. Named BlankProgram rather than Program on
// purpose: two global-namespace `Program` types can't both be referenced from one using directive,
// and the other reference host already owns that name.
public partial class BlankProgram { }
