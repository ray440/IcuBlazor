
using System.Threading.Tasks;
using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using BlazorStyled;

namespace IcuBlazor
{

    public static class TaskExtensions
    {
        public static async Task Log(this Task t, string title)
        {
            var iid = BaseUtils.PrefixedID("..");
            DBG.Indent(3, title + " { "+iid);
            await t;
            DBG.Indent(-3, "} "+iid);
        }
        public static async Task<T> LogT<T>(this Task<T> t, string title)
        {
            var iid = BaseUtils.PrefixedID("..");
            DBG.Indent(3, title + " { "+iid);
            var r = await t;
            DBG.Indent(-3, "} "+iid);
            return r;
        }
    }


    public static partial class IcuBlazorAppBuilderExtensions
    {
        public static IServiceCollection AddIcuBlazor // for CSB
            (this IServiceCollection services, IcuConfig config)
        {
            services.AddSingleton(config);
            services.AddScoped<UIHelper>();
            services.AddTransient<IcuClient>(); // one for each TestViewer
            services.AddBlazorStyled(!true, !true);

            services.AddHttpClient("ICUapi");
            if (config.EnableServerTests) {
                var sp = services.BuildServiceProvider();
                Web.Http = sp.GetService<HttpClient>();
            }

            ENV.IsIcuEnabled = true;
            DBG.Verbosity = config.Verbosity;
            DBG.SetSystem("WA");
            return services;
        }

    }

}
