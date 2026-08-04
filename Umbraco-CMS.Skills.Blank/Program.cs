
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Templates on this host are written to Views/ at RUNTIME by each example's package migration, so the
// view engine has to be able to compile a .cshtml that didn't exist at build time. Without this every
// front-end URL answers 404: Umbraco resolves the content, finds no usable template, and gives up.
builder.Services.AddMvc().AddRazorRuntimeCompilation();

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
