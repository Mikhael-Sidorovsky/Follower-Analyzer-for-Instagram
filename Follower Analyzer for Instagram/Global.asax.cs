﻿using Follower_Analyzer_for_Instagram.Infrastructure;
using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Follower_Analyzer_for_Instagram
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {

            AutofacConfig.ConfigureContainer();
            AreaRegistration.RegisterAllAreas();
            // FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_Error(Object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            Response.Clear();

            HttpException httpException = exception as HttpException;
            string action = null;
          
            if (httpException != null)
            { 
                string msg = httpException.Message;

                switch (httpException.GetHttpCode())
                {
                    case 404:
                        action = "Http404";

                        if (httpException.TargetSite.Name == "HandleUnknownAction")
                        {
                            msg = "Not Found!";
                        }

                        break;
                    case 500:
                        action = "Http500";
                        break;
                }

                Server.ClearError();
                if (action != null)
                {
                    Response.Redirect($"~/Error/{action}/?errorMsg={msg}");
                }
            }
            else
            {
                Server.ClearError();
                Response.Redirect($"~/Error/Http500/?errorMsg={exception.Message}");
            }
        }

        void Application_End(object sender, EventArgs e)
        {
            try
            {
                Startup.ActivityAnalizingCancellationTokenSource.Cancel();
            }
            catch
            {

            }
        }
    }
}