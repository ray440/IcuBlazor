
using System;
using System.Threading.Tasks;
using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using BlazorStyled;
using System.Linq;

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
        public static IServiceCollection AddIcuBlazor
            (this IServiceCollection services, IcuConfig config)
        {
            services.AddSingleton(config);
            services.AddScoped<UIHelper>();
            services.AddTransient<IcuClient>(); // one for each IcuTestViewer
            services.AddBlazorStyled(!true, !true);
            services.AddTransient(typeof(RPC.IProxy), RPC.ProxyType(config));
            services.AddHttpClient("ICUapi");

            if (String.IsNullOrEmpty(config.IcuServer)) { // probably server
            } else { // client
                var uri = new Uri(config.IcuServer);
                ENV.IsLocalhost = uri.IsLoopback;
            }
            ENV.IsIcuEnabled = true;
            
            DBG.Verbosity = config.Verbosity;
            DBG.SetSystem("WA");
            return services;
        }

    }

}
