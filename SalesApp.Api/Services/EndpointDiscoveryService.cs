using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.Attributes;
using SalesApp.DTOs;
using System.Reflection;

namespace SalesApp.Services
{
    public class EndpointDiscoveryService : IEndpointDiscoveryService
    {
        public List<EndpointInfo> GetAllEndpoints()
        {
            var endpoints = new List<EndpointInfo>();
            var controllers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract);

            foreach (var controller in controllers)
            {
                var controllerRoute = GetControllerRoute(controller);
                var controllerAuth = GetControllerAuthorization(controller);

                var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.IsPublic && !m.IsSpecialName && m.DeclaringType == controller);

                foreach (var action in actions)
                {
                    var httpMethods = GetHttpMethods(action);
                    var actionAuth = GetActionAuthorization(action);
                    var finalAuth = actionAuth ?? controllerAuth;

                    foreach (var httpMethod in httpMethods)
                    {
                        endpoints.Add(new EndpointInfo
                        {
                            Controller = controller.Name.Replace("Controller", ""),
                            Action = action.Name,
                            HttpMethod = httpMethod,
                            Route = $"{controllerRoute}/{GetActionRoute(action)}",
                            RequiredRoles = finalAuth?.RequiredRoles ?? new List<string>(),
                            AuthorizationType = finalAuth?.Type ?? "None"
                        });
                    }
                }
            }

            return endpoints;
        }

        private string GetControllerRoute(Type controller)
        {
            var routeAttr = controller.GetCustomAttribute<RouteAttribute>();
            return routeAttr?.Template?.Replace("[controller]", controller.Name.Replace("Controller", "").ToLower()) ?? "";
        }

        private AuthInfo? GetControllerAuthorization(Type controller)
        {
            var dynamicAuth = controller.GetCustomAttribute<DynamicRoleAuthorizeAttribute>();
            if (dynamicAuth != null)
            {
                var roles = (string[])typeof(DynamicRoleAuthorizeAttribute)
                    .GetField("_requiredRoles", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(dynamicAuth) ?? Array.Empty<string>();
                return new AuthInfo { Type = "DynamicRole", RequiredRoles = roles.ToList() };
            }

            var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();
            if (authorize != null)
            {
                return new AuthInfo 
                { 
                    Type = "Standard", 
                    RequiredRoles = authorize.Roles?.Split(',').Select(r => r.Trim()).ToList() ?? new List<string>() 
                };
            }

            return null;
        }

        private AuthInfo? GetActionAuthorization(MethodInfo action)
        {
            var dynamicAuth = action.GetCustomAttribute<DynamicRoleAuthorizeAttribute>();
            if (dynamicAuth != null)
            {
                var roles = (string[])typeof(DynamicRoleAuthorizeAttribute)
                    .GetField("_requiredRoles", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(dynamicAuth) ?? Array.Empty<string>();
                return new AuthInfo { Type = "DynamicRole", RequiredRoles = roles.ToList() };
            }

            var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
            if (authorize != null)
            {
                return new AuthInfo 
                { 
                    Type = "Standard", 
                    RequiredRoles = authorize.Roles?.Split(',').Select(r => r.Trim()).ToList() ?? new List<string>() 
                };
            }

            return null;
        }

        private List<string> GetHttpMethods(MethodInfo action)
        {
            var methods = new List<string>();
            
            if (action.GetCustomAttribute<HttpGetAttribute>() != null) methods.Add("GET");
            if (action.GetCustomAttribute<HttpPostAttribute>() != null) methods.Add("POST");
            if (action.GetCustomAttribute<HttpPutAttribute>() != null) methods.Add("PUT");
            if (action.GetCustomAttribute<HttpDeleteAttribute>() != null) methods.Add("DELETE");
            if (action.GetCustomAttribute<HttpPatchAttribute>() != null) methods.Add("PATCH");

            return methods.Any() ? methods : new List<string> { "GET" };
        }

        private string GetActionRoute(MethodInfo action)
        {
            var httpGet = action.GetCustomAttribute<HttpGetAttribute>();
            if (httpGet != null) return httpGet.Template ?? action.Name.ToLower();

            var httpPost = action.GetCustomAttribute<HttpPostAttribute>();
            if (httpPost != null) return httpPost.Template ?? action.Name.ToLower();

            var httpPut = action.GetCustomAttribute<HttpPutAttribute>();
            if (httpPut != null) return httpPut.Template ?? action.Name.ToLower();

            var httpDelete = action.GetCustomAttribute<HttpDeleteAttribute>();
            if (httpDelete != null) return httpDelete.Template ?? action.Name.ToLower();

            return action.Name.ToLower();
        }

        private class AuthInfo
        {
            public string Type { get; set; } = string.Empty;
            public List<string> RequiredRoles { get; set; } = new();
        }
    }
}