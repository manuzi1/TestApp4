using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(TestApp4.Startup))]
namespace TestApp4
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
